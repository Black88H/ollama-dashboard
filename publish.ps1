#Requires -Version 5.1
<#
.SYNOPSIS
    Release-Build-Skript fuer Ollama Dashboard (Velopack + GitHub).

.DESCRIPTION
    1. Publiziert die App als self-contained win-x64 Executable.
    2. Packt mit 'vpk' einen Velopack-Installer (Setup.exe) + Update-Pakete (.nupkg).
    3. Optional: Laedt direkt in ein GitHub-Release hoch (-Upload).

.EXAMPLE
    # Einfacher Build:
    .\publish.ps1 -Version "1.2.0"

    # Build + automatischer GitHub-Upload:
    .\publish.ps1 -Version "1.2.0" -Upload -GitHubToken "ghp_XXXXXXXXXXXX"

.NOTES
    Voraussetzungen (einmalig):
        dotnet tool install -g vpk
        dotnet tool update  -g vpk
#>

param(
    [Parameter(Mandatory)]
    [string] $Version,

    [string] $GitHubOwner = "Black88H",
    [string] $GitHubRepo  = "ollama-dashboard",

    [switch] $Upload,
    [string] $GitHubToken = "",

    [switch] $SkipBuild
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

# ---------------------------------------------------------------------------
# Pfade
# ---------------------------------------------------------------------------
$Root        = $PSScriptRoot
$ProjectFile = Join-Path $Root "src\OllamaDashboard\OllamaDashboard.csproj"
$PublishDir  = Join-Path $Root "publish\win-x64"
$ReleaseDir  = Join-Path $Root "releases"
$IconPath    = Join-Path $Root "src\OllamaDashboard\Assets\icon.ico"

# ---------------------------------------------------------------------------
# Hilfsfunktionen
# ---------------------------------------------------------------------------
function Write-Step([string]$msg) {
    Write-Host ""
    Write-Host "==> $msg" -ForegroundColor Cyan
}

function Assert-Tool([string]$name) {
    if (-not (Get-Command $name -ErrorAction SilentlyContinue)) {
        Write-Error "'$name' nicht gefunden. Installieren mit: dotnet tool install -g $name"
    }
}

function Invoke-Checked([string]$label, [scriptblock]$cmd) {
    & $cmd
    if ($LASTEXITCODE -ne 0) {
        throw "$label fehlgeschlagen (Exit-Code $LASTEXITCODE)"
    }
}

# ---------------------------------------------------------------------------
# Step 0: Tools pruefen
# ---------------------------------------------------------------------------
Write-Step "Pruefe Voraussetzungen..."
Assert-Tool "dotnet"
Assert-Tool "vpk"

Write-Host "  dotnet  : $(dotnet --version)"
Write-Host "  Version : $Version"
Write-Host "  Repo    : https://github.com/$GitHubOwner/$GitHubRepo"

# ---------------------------------------------------------------------------
# Step 1: dotnet publish  (Release, self-contained, win-x64)
# ---------------------------------------------------------------------------
if (-not $SkipBuild) {
    Write-Step "dotnet publish..."

    if (Test-Path $PublishDir) {
        Remove-Item $PublishDir -Recurse -Force
    }

    $publishArgs = @(
        'publish', $ProjectFile
        '--configuration', 'Release'
        '--runtime',       'win-x64'
        '--self-contained','true'
        "-p:Version=$Version"
        '-p:PublishSingleFile=false'
        '-p:DebugType=None'
        '-p:DebugSymbols=false'
        '--output',        $PublishDir
    )

    Invoke-Checked "dotnet publish" { & dotnet @publishArgs }

    Write-Host "  Ausgabe: $PublishDir" -ForegroundColor Green
}

# ---------------------------------------------------------------------------
# Step 2: vpk pack  ->  Setup.exe + .nupkg + RELEASES
# ---------------------------------------------------------------------------
Write-Step "vpk pack -- erstelle Installer und Update-Pakete..."

if (Test-Path $ReleaseDir) {
    Remove-Item $ReleaseDir -Recurse -Force
}

$vpkArgs = @(
    'pack'
    '--packId',      'OllamaDashboard'
    '--packVersion', $Version
    '--packDir',     $PublishDir
    '--mainExe',     'OllamaDashboard.exe'
    '--packTitle',   'Ollama Dashboard'
    '--outputDir',   $ReleaseDir
    '--runtime',     'win-x64'
)

if (Test-Path $IconPath) {
    $vpkArgs += '--icon', $IconPath
}

Invoke-Checked "vpk pack" { & vpk @vpkArgs }

Write-Host ""
Write-Host "  Erstellte Dateien in '$ReleaseDir':" -ForegroundColor Green
Get-ChildItem $ReleaseDir | ForEach-Object { Write-Host "    $($_.Name)" }

# ---------------------------------------------------------------------------
# Step 3 (optional): GitHub-Upload
# ---------------------------------------------------------------------------
if ($Upload) {
    if ([string]::IsNullOrWhiteSpace($GitHubToken)) {
        Write-Error "Bitte -GitHubToken angeben fuer den GitHub-Upload."
    }

    Write-Step "vpk upload github -- lade Release-Assets hoch..."

    $uploadArgs = @(
        'upload', 'github'
        '--repoUrl',     "https://github.com/$GitHubOwner/$GitHubRepo"
        '--token',       $GitHubToken
        '--tag',         "v$Version"
        '--outputDir',   $ReleaseDir
        '--releaseName', "Ollama Dashboard v$Version"
    )

    Invoke-Checked "vpk upload" { & vpk @uploadArgs }

    Write-Host "  Upload abgeschlossen." -ForegroundColor Green
}

# ---------------------------------------------------------------------------
# Fertig
# ---------------------------------------------------------------------------
Write-Host ""
Write-Host "=============================================" -ForegroundColor Green
Write-Host "  Build v$Version abgeschlossen!" -ForegroundColor Green
Write-Host "=============================================" -ForegroundColor Green

if (-not $Upload) {
    Write-Host ""
    Write-Host "Naechste Schritte -- manueller GitHub-Upload:" -ForegroundColor Yellow
    Write-Host ""
    Write-Host "  1. GitHub Release erstellen:"
    Write-Host "     https://github.com/$GitHubOwner/$GitHubRepo/releases/new"
    Write-Host ""
    Write-Host "  2. Tag:   v$Version"
    Write-Host "     Titel: Ollama Dashboard v$Version"
    Write-Host ""
    Write-Host "  3. Diese Dateien aus '$ReleaseDir' als Assets hochladen:"
    Write-Host ""
    Write-Host "     OllamaDashboard-$Version-win-Setup.exe" -ForegroundColor White
    Write-Host "       -> Installer fuer Erstinstallation"
    Write-Host ""
    Write-Host "     OllamaDashboard-$Version-win-full.nupkg" -ForegroundColor White
    Write-Host "       -> Vollpaket fuer automatische Updates"
    Write-Host ""
    Write-Host "     RELEASES" -ForegroundColor White
    Write-Host "       -> Update-Manifest (PFLICHT, sonst findet die App kein Update)"
    Write-Host ""
    Write-Host "  Tipp: Automatischer Upload mit einem Befehl:"
    Write-Host "    .\publish.ps1 -Version '$Version' -Upload -GitHubToken 'ghp_XXX'" -ForegroundColor Cyan
    Write-Host ""
}
