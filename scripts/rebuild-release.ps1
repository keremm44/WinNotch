param(
    [switch]$NoRestart
)

$ErrorActionPreference = "Stop"
$repoRoot = Split-Path -Parent $PSScriptRoot
$solution = Join-Path $repoRoot "WinNotch.sln"
$exe = Join-Path $repoRoot "src\WinNotch.TrayApp\bin\Release\net8.0-windows10.0.19041.0\WinNotch.exe"

Write-Host "[WinNotch] Stopping running WinNotch process(es)..."
Get-Process -Name "WinNotch" -ErrorAction SilentlyContinue | Stop-Process -Force

$deadline = (Get-Date).AddSeconds(5)
do {
    $remaining = Get-Process -Name "WinNotch" -ErrorAction SilentlyContinue
    if (-not $remaining) { break }
    Start-Sleep -Milliseconds 100
} while ((Get-Date) -lt $deadline)

if (Get-Process -Name "WinNotch" -ErrorAction SilentlyContinue) {
    throw "WinNotch is still running and may keep build output files locked."
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
