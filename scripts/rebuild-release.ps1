param(
    [switch]$NoRestart
)

$ErrorActionPreference = "Stop"
$repoRoot = Split-Path -Parent $PSScriptRoot
$solution = Join-Path $repoRoot "WinNotch.sln"
$exe = Join-Path $repoRoot "src\WinNotch.TrayApp\bin\Release\net8.0-windows10.0.19041.0\WinNotch.exe"
$shutdownEventName = "WinNotch_GracefulShutdown_{8A3F2B1C-5D4E-6F7A-8B9C-0D1E2F3A4B5C}"

function Wait-WinNotchExit([int]$milliseconds) {
    $deadline = (Get-Date).AddMilliseconds($milliseconds)
    do {
        $remaining = Get-Process -Name "WinNotch" -ErrorAction SilentlyContinue
        if (-not $remaining) { return $true }
        Start-Sleep -Milliseconds 100
    } while ((Get-Date) -lt $deadline)
    return $false
}

$running = @(Get-Process -Name "WinNotch" -ErrorAction SilentlyContinue)
if ($running.Count -gt 0) {
    Write-Host "[WinNotch] Requesting graceful shutdown..."
    $signaled = $false

    try {
        $shutdownEvent = [System.Threading.EventWaitHandle]::OpenExisting($shutdownEventName)
        try {
            $null = $shutdownEvent.Set()
            $signaled = $true
        }
        finally {
            $shutdownEvent.Dispose()
        }
    }
    catch {
        # Older builds do not expose the named shutdown event. In that case try
        # WM_CLOSE first so MainWindow_Closing can still unpin tracked windows.
        Write-Host "[WinNotch] Graceful signal unavailable; trying window close for older build..."
    }

    if ($signaled -and (Wait-WinNotchExit 5000)) {
        Write-Host "[WinNotch] Graceful shutdown complete."
    }
    else {
        $remaining = @(Get-Process -Name "WinNotch" -ErrorAction SilentlyContinue)
        foreach ($process in $remaining) {
            try { $null = $process.CloseMainWindow() } catch { }
        }

        # MainWindow uses explicit application shutdown, so an old build may keep
        # the tray process alive after its window closes. Give cleanup a moment,
        # then force only the already-cleaned process if necessary.
        Start-Sleep -Milliseconds 900

        if (-not (Wait-WinNotchExit 1200)) {
            Write-Warning "WinNotch did not exit gracefully; forcing remaining process(es) after cleanup attempt."
            Get-Process -Name "WinNotch" -ErrorAction SilentlyContinue | Stop-Process -Force
            if (-not (Wait-WinNotchExit 3000)) {
                throw "WinNotch is still running and may keep build output files locked."
            }
        }
    }
}

Write-Host "[WinNotch] Building Release..."
pushd $repoRoot
try {
    dotnet build $solution -c Release
    if ($LASTEXITCODE -ne 0) {
        throw "Release build failed with exit code $LASTEXITCODE."
    }
}
finally {
    popd
}

if ($NoRestart) {
    Write-Host "[WinNotch] Build succeeded. Restart skipped."
    exit 0
}

if (-not (Test-Path $exe)) {
    throw "Build succeeded but WinNotch.exe was not found at: $exe"
}

Write-Host "[WinNotch] Build succeeded. Starting WinNotch..."
Start-Process -FilePath $exe -WorkingDirectory (Split-Path -Parent $exe)
