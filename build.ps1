[CmdletBinding()]
param(
    [switch]$Run
)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$project = Join-Path $root "src\TrayTemps\TrayTemps.csproj"
$output = Join-Path $root "outputs"
$dotnetRoot = Join-Path $env:TEMP "traytemps-dotnet8"
$dotnet = Join-Path $dotnetRoot "dotnet.exe"

if (-not (Test-Path $dotnet)) {
    New-Item -ItemType Directory -Force -Path $dotnetRoot | Out-Null

    $installer = Join-Path $dotnetRoot "dotnet-install.ps1"
    Write-Host "Downloading .NET installer..." -ForegroundColor Cyan
    Invoke-WebRequest "https://dot.net/v1/dotnet-install.ps1" -OutFile $installer

    Write-Host "Installing temporary .NET 8 SDK..." -ForegroundColor Cyan
    & powershell -NoProfile -ExecutionPolicy Bypass -File $installer `
        -Channel 8.0 `
        -Quality GA `
        -InstallDir $dotnetRoot

    if ($LASTEXITCODE -ne 0) {
        throw ".NET SDK installation failed."
    }
}

New-Item -ItemType Directory -Force -Path $output | Out-Null

$arguments = @(
    "publish",
    $project,
    "-c", "Release",
    "-r", "win-x64",
    "--self-contained", "true",
    "-p:PublishSingleFile=true",
    "-p:IncludeNativeLibrariesForSelfExtract=true",
    "-p:IncludeAllContentForSelfExtract=true",
    "-p:DebugType=none",
    "-p:DebugSymbols=false",
    "-o", $output
)

& $dotnet @arguments
if ($LASTEXITCODE -ne 0) {
    throw "dotnet publish failed."
}

$exe = Join-Path $output "TrayTemps.exe"
Write-Host ""
Write-Host "Built: $exe" -ForegroundColor Green

if ($Run) {
    Start-Process -FilePath $exe
}
