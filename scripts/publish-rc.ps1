param(
    [string]$Configuration = "Release",
    [string]$Runtime = "win-x64",
    [string]$OutputRoot = "artifacts"
)

$ErrorActionPreference = "Stop"
$repoRoot = Split-Path -Parent $PSScriptRoot
$project = Join-Path $repoRoot "src\WinNotch.TrayApp\WinNotch.TrayApp.csproj"
$outputRootPath = Join-Path $repoRoot $OutputRoot
$publishDir = Join-Path $outputRootPath $Runtime
$zipPath = Join-Path $outputRootPath "WinNotch-0.2.0-rc1-$Runtime.zip"

if (Test-Path $publishDir) {
    Remove-Item $publishDir -Recurse -Force
}
New-Item -ItemType Directory -Force -Path $publishDir | Out-Null
New-Item -ItemType Directory -Force -Path $outputRootPath | Out-Null

Write-Host "[WinNotch] Publishing $Configuration / $Runtime..."
& dotnet publish $project `
    -c $Configuration `
    -r $Runtime `
    --self-contained true `
    -p:PublishSingleFile=true `
    -p:IncludeNativeLibrariesForSelfExtract=true `
    -p:PublishReadyToRun=false `
    -p:DebugType=None `
    -p:DebugSymbols=false `
    -p:Deterministic=true `
    -o $publishDir

if ($LASTEXITCODE -ne 0) {
    throw "RC publish failed with exit code $LASTEXITCODE."
}

$exe = Join-Path $publishDir "WinNotch.exe"
if (-not (Test-Path $exe)) {
    throw "RC publish succeeded but WinNotch.exe was not found at: $exe"
}

$versionInfo = (Get-Item $exe).VersionInfo
$productVersion = $versionInfo.ProductVersion
$fileVersion = $versionInfo.FileVersion
if ([string]::IsNullOrWhiteSpace($productVersion) -or -not $productVersion.StartsWith("0.2.0-rc1")) {
    throw "Unexpected ProductVersion '$productVersion'; expected 0.2.0-rc1."
}
if ([string]::IsNullOrWhiteSpace($fileVersion) -or -not $fileVersion.StartsWith("0.2.0.0")) {
    throw "Unexpected FileVersion '$fileVersion'; expected 0.2.0.0."
}

if (Test-Path $zipPath) {
    Remove-Item $zipPath -Force
}
Compress-Archive -Path (Join-Path $publishDir "*") -DestinationPath $zipPath -CompressionLevel Optimal

Write-Host "[WinNotch] RC package ready: $zipPath"
Write-Host "[WinNotch] ProductVersion=$productVersion FileVersion=$fileVersion"
