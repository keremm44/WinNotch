param(
    [int]$WarmupSeconds = 10,
    [int]$SoakSeconds = 30,
    [double]$MaxPrivateGrowthMB = 32,
    [int]$MaxHandleGrowth = 80,
    [int]$MaxThreadGrowth = 10
)

$ErrorActionPreference = "Stop"
$repoRoot = Split-Path -Parent $PSScriptRoot
$exe = Join-Path $repoRoot "src\WinNotch.TrayApp\bin\Release\net8.0-windows10.0.19041.0\WinNotch.exe"
if (-not (Test-Path $exe)) {
    throw "SOAK: WinNotch.exe not found. Build Release before running the soak gate."
}

$settingsDir = Join-Path $env:LOCALAPPDATA "WinNotch"
$settingsPath = Join-Path $settingsDir "settings.json"
New-Item -ItemType Directory -Force -Path $settingsDir | Out-Null
@{
    ModuleA_DragDrop = $true
    ModuleB_Clipboard = $true
    ModuleC_Media = $true
    ModuleE_Screenshot = $true
    TargetMonitorIndex = 0
    AutoStart = $false
    DiagnosticsEnabled = $false
    VisibilityMode = "Auto"
    ReactionLevel = "Balanced"
} | ConvertTo-Json -Compress | Set-Content -Path $settingsPath -Encoding UTF8

Get-Process -Name "WinNotch" -ErrorAction SilentlyContinue |
    Stop-Process -Force -ErrorAction SilentlyContinue
Start-Sleep -Milliseconds 700

$process = $null
try {
    $process = Start-Process -FilePath $exe -PassThru
    Start-Sleep -Seconds $WarmupSeconds
    $process.Refresh()
    if ($process.HasExited) {
        throw "SOAK: process exited during warm-up with code $($process.ExitCode)."
    }

    $basePrivateMB = $process.PrivateMemorySize64 / 1MB
    $baseHandles = $process.HandleCount
    $baseThreads = $process.Threads.Count

    Start-Sleep -Seconds $SoakSeconds
    $process.Refresh()
    if ($process.HasExited) {
        throw "SOAK: process exited during soak with code $($process.ExitCode)."
    }

    $finalPrivateMB = $process.PrivateMemorySize64 / 1MB
    $finalHandles = $process.HandleCount
    $finalThreads = $process.Threads.Count

    $privateGrowthMB = $finalPrivateMB - $basePrivateMB
    $handleGrowth = $finalHandles - $baseHandles
    $threadGrowth = $finalThreads - $baseThreads

    Write-Host ("SOAK Baseline PrivateMB={0} Handles={1} Threads={2}" -f `
        [Math]::Round($basePrivateMB, 2), $baseHandles, $baseThreads)
    Write-Host ("SOAK Final PrivateMB={0} Handles={1} Threads={2}" -f `
        [Math]::Round($finalPrivateMB, 2), $finalHandles, $finalThreads)
    Write-Host ("SOAK Growth PrivateMB={0} Handles={1} Threads={2}" -f `
        [Math]::Round($privateGrowthMB, 2), $handleGrowth, $threadGrowth)

    if ($privateGrowthMB -gt $MaxPrivateGrowthMB) {
        throw "SOAK: private memory grew by $([Math]::Round($privateGrowthMB, 2)) MB (limit $MaxPrivateGrowthMB MB)."
    }
    if ($handleGrowth -gt $MaxHandleGrowth) {
        throw "SOAK: handle count grew by $handleGrowth (limit $MaxHandleGrowth)."
    }
    if ($threadGrowth -gt $MaxThreadGrowth) {
        throw "SOAK: thread count grew by $threadGrowth (limit $MaxThreadGrowth)."
    }
}
finally {
    if ($null -ne $process) {
        try {
            $process.Refresh()
            if (-not $process.HasExited) {
                Stop-Process -Id $process.Id -Force -ErrorAction SilentlyContinue
            }
        }
        finally {
            $process.Dispose()
        }
    }
}
