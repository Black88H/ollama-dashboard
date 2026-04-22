# ============================================================
#  Ollama Dashboard — Build & Publish Script
#  Usage:  .\build.ps1 [-Configuration Release] [-Version 0.2.0]
# ============================================================

param(
    [string]$Configuration = "Release",
    [string]$Version = "",
    [switch]$SkipInstaller
)

$ErrorActionPreference = "Stop"

$root = Split-Path -Parent $MyInvocation.MyCommand.Definition
$mainProj    = Join-Path $root "src\OllamaDashboard\OllamaDashboard.csproj"
$updaterProj = Join-Path $root "src\OllamaDashboard.Updater\OllamaDashboard.Updater.csproj"
$installerDir = Join-Path $root "installer"
$distDir     = Join-Path $root "dist"

Write-Host "==> Cleaning..." -ForegroundColor Cyan
dotnet clean $mainProj -c $Configuration | Out-Null
dotnet clean $updaterProj -c $Configuration | Out-Null

Write-Host "==> Restoring..." -ForegroundColor Cyan
dotnet restore $mainProj
dotnet restore $updaterProj

$versionArg = @()
if ($Version -ne "") {
    $versionArg = @("/p:Version=$Version", "/p:AssemblyVersion=$Version.0", "/p:FileVersion=$Version.0")
}

Write-Host "==> Publishing main app ($Configuration)..." -ForegroundColor Cyan
dotnet publish $mainProj `
    -c $Configuration `
    -r win-x64 `
    --no-self-contained `
    /p:PublishSingleFile=false `
    @versionArg

Write-Host "==> Publishing updater..." -ForegroundColor Cyan
dotnet publish $updaterProj `
    -c $Configuration `
    -r win-x64 `
    --no-self-contained `
    @versionArg

if (-not $SkipInstaller) {
    $iscc = (Get-Command iscc.exe -ErrorAction SilentlyContinue)
    if ($null -eq $iscc) {
        Write-Warning "Inno Setup (iscc.exe) nicht im PATH — überspringe Installer-Bau."
        Write-Warning "Installation: https://jrsoftware.org/isdl.php"
    } else {
        New-Item -ItemType Directory -Force -Path $distDir | Out-Null
        Write-Host "==> Building installer..." -ForegroundColor Cyan
        & iscc (Join-Path $installerDir "setup.iss")
    }
}

Write-Host ""
Write-Host "==> Done!" -ForegroundColor Green
Write-Host "Publish output: src\OllamaDashboard\bin\$Configuration\net8.0-windows\win-x64\publish\"
if (-not $SkipInstaller) {
    Write-Host "Installer:      dist\"
}
