<#
.SYNOPSIS
    Builds, publishes, and packages PolyStock using Velopack.
.DESCRIPTION
    Compiles PolyStock in Release mode, publishes the WPF win-x64 executable,
    and runs `vpk pack` to generate PolyStock-Setup.exe and update packages.
.PARAMETER Version
    The version to build (e.g. "1.0.0"). If omitted, reads the version from PolyStock.WPF.csproj.
.PARAMETER Clean
    If specified, deletes existing output in the Releases directory before packing.
.PARAMETER OutputDir
    Output directory for the Velopack release packages. Default is 'Releases'.
#>

[CmdletBinding()]
param(
    [string]$Version = "1.0.2",
    [switch]$Clean,
    [string]$OutputDir = "Releases"
)

$ErrorActionPreference = "Stop"

$repoRoot = Split-Path -Parent $PSScriptRoot
Set-Location $repoRoot

$csprojPath = Join-Path $repoRoot "src\PolyStock.WPF\PolyStock.WPF.csproj"
if (-not (Test-Path $csprojPath)) {
    throw "Cannot find project file at: $csprojPath"
}

# 1. Determine Version
[xml]$csprojXml = Get-Content $csprojPath
if ([string]::IsNullOrWhiteSpace($Version)) {
    $Version = $csprojXml.Project.PropertyGroup.Version
    if ([string]::IsNullOrWhiteSpace($Version)) {
        $Version = "1.0.0"
    }
} else {
    Write-Host "Updating version to $Version in $csprojPath..." -ForegroundColor Cyan
    $content = Get-Content $csprojPath -Raw
    $content = [regex]::Replace($content, '<Version>.*?</Version>', "<Version>$Version</Version>")
    $content = [regex]::Replace($content, '<AssemblyVersion>.*?</AssemblyVersion>', "<AssemblyVersion>$Version</AssemblyVersion>")
    $content = [regex]::Replace($content, '<FileVersion>.*?</FileVersion>', "<FileVersion>$Version</FileVersion>")
    Set-Content -Path $csprojPath -Value $content -NoNewline
}

Write-Host "==========================================================" -ForegroundColor Green
Write-Host "   PolyStock Velopack Packaging - Version $Version" -ForegroundColor Green
Write-Host "==========================================================" -ForegroundColor Green

# 2. Check for vpk CLI
if (-not (Get-Command "vpk" -ErrorAction SilentlyContinue)) {
    Write-Warning "'vpk' CLI was not found in PATH. Attempting to install dotnet tool 'vpk'..."
    dotnet tool install -g vpk
}

# 3. Setup Directories
$publishDir = Join-Path $repoRoot "publish"
$releasesDir = Join-Path $repoRoot $OutputDir

if (Test-Path $publishDir) {
    Write-Host "Cleaning publish directory: $publishDir" -ForegroundColor DarkGray
    Remove-Item -Recurse -Force $publishDir
}

if ($Clean -and (Test-Path $releasesDir)) {
    Write-Host "Cleaning releases directory: $releasesDir" -ForegroundColor DarkGray
    Remove-Item -Recurse -Force $releasesDir
}

# If re-packaging the same version, remove prior package artifacts so Velopack can re-pack cleanly
$existingVersionFile = Join-Path $releasesDir "PolyStock-$Version-full.nupkg"
if (Test-Path $existingVersionFile) {
    Write-Host "Re-packaging version $Version. Refreshing Releases directory..." -ForegroundColor DarkGray
    Remove-Item -Recurse -Force $releasesDir
}

if (-not (Test-Path $releasesDir)) {
    New-Item -ItemType Directory -Path $releasesDir | Out-Null
}

# 4. Optional: Download latest releases feed from GitHub to generate delta updates
$repoUrl = "https://github.com/progressivesystems2-bit/Polystock"
if (-not $Clean) {
    Write-Host "Checking for existing releases on GitHub for delta creation..." -ForegroundColor Cyan
    try {
        $token = $env:GITHUB_TOKEN
        if ([string]::IsNullOrWhiteSpace($token)) {
            $token = [Environment]::GetEnvironmentVariable("GITHUB_TOKEN", "User")
        }
        if ([string]::IsNullOrWhiteSpace($token)) {
            $token = [Environment]::GetEnvironmentVariable("GITHUB_TOKEN", "Machine")
        }
        $tokenArgs = @()
        if (![string]::IsNullOrWhiteSpace($token)) {
            $tokenArgs = @("--token", $token)
        }
        vpk download github --repoUrl $repoUrl --outputDir $releasesDir @tokenArgs 2>$null
        Write-Host "Existing release feed synchronized successfully." -ForegroundColor Green
    } catch {
        Write-Host "No prior releases found or sync skipped. Full release package will be built." -ForegroundColor DarkGray
    }
}

# 5. Publish WPF Application
Write-Host "Publishing PolyStock (Release, win-x64)..." -ForegroundColor Cyan
dotnet publish $csprojPath -c Release -r win-x64 --self-contained false -o $publishDir

if ($LASTEXITCODE -ne 0) {
    throw "dotnet publish failed with exit code $LASTEXITCODE."
}

# Ensure Database Seed is present in publish output
$dbSeed = Join-Path $repoRoot "Data\PolyStock.accdb"
$publishDb = Join-Path $publishDir "Data\PolyStock.accdb"
if ((Test-Path $dbSeed) -and (-not (Test-Path $publishDb))) {
    $publishDataDir = Join-Path $publishDir "Data"
    if (-not (Test-Path $publishDataDir)) { New-Item -ItemType Directory -Path $publishDataDir | Out-Null }
    Copy-Item $dbSeed $publishDb -Force
    Write-Host "Bundled seed database into publish output." -ForegroundColor DarkGray
}

# 6. Velopack Pack
Write-Host "Packaging with Velopack (vpk pack)..." -ForegroundColor Cyan
vpk pack `
    --packId "PolyStock" `
    --packVersion "$Version" `
    --packDir "$publishDir" `
    --packTitle "PolyStock" `
    --packAuthors "Tauras Technology" `
    --mainExe "PolyStock.WPF.exe" `
    --runtime "win-x64" `
    --framework "net8-x64-desktop" `
    --outputDir "$releasesDir"

if ($LASTEXITCODE -ne 0) {
    throw "vpk pack failed with exit code $LASTEXITCODE."
}

$winSetup = Join-Path $releasesDir "PolyStock-win-Setup.exe"
$genericSetup = Join-Path $releasesDir "PolyStock-Setup.exe"
if (Test-Path $winSetup) {
    Copy-Item $winSetup $genericSetup -Force
}

Write-Host "==========================================================" -ForegroundColor Green
Write-Host "Packaging Complete!" -ForegroundColor Green
Write-Host "Setup Installer: $(Join-Path $releasesDir 'PolyStock-Setup.exe')" -ForegroundColor Yellow
Write-Host "Package Folder:  $releasesDir" -ForegroundColor Yellow
Write-Host "==========================================================" -ForegroundColor Green
