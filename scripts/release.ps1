<#
.SYNOPSIS
    Builds, packages, and uploads a new release of PolyStock to GitHub Releases.
.DESCRIPTION
    Executes build-release.ps1, then uses `vpk upload github` to upload the release assets
    (installer, nupkg packages, RELEASES feed) directly to https://github.com/progressivesystems2-bit/Polystock.
.PARAMETER Version
    The new version number (e.g. 1.0.0, 1.0.1). If omitted, uses current version in PolyStock.WPF.csproj.
.PARAMETER Token
    GitHub Personal Access Token with repo permissions. If omitted, uses $env:GITHUB_TOKEN.
.PARAMETER ReleaseNotes
    Optional markdown release notes string or path to a markdown file.
.PARAMETER PreRelease
    If specified, creates the release as a Pre-Release instead of stable.
#>

[CmdletBinding()]
param(
    [string]$Version = "",
    [string]$Token = "",
    [string]$ReleaseNotes = "",
    [switch]$PreRelease
)

$ErrorActionPreference = "Stop"

$repoRoot = Split-Path -Parent $PSScriptRoot
Set-Location $repoRoot

$repoUrl = "https://github.com/progressivesystems2-bit/Polystock"

# 1. Resolve GitHub Token
if ([string]::IsNullOrWhiteSpace($Token)) {
    $Token = $env:GITHUB_TOKEN
}
if ([string]::IsNullOrWhiteSpace($Token)) {
    $Token = [Environment]::GetEnvironmentVariable("GITHUB_TOKEN", "User")
}
if ([string]::IsNullOrWhiteSpace($Token)) {
    $Token = [Environment]::GetEnvironmentVariable("GITHUB_TOKEN", "Machine")
}

if ([string]::IsNullOrWhiteSpace($Token)) {
    Write-Error "GitHub Token not found. Please provide -Token parameter or set `$env:GITHUB_TOKEN environment variable."
    exit 1
}

# 2. Build and Package
Write-Host ">>> Step 1/2: Building and Packaging PolyStock..." -ForegroundColor Cyan
if (![string]::IsNullOrWhiteSpace($Version)) {
    & "$PSScriptRoot\build-release.ps1" -Version $Version
} else {
    & "$PSScriptRoot\build-release.ps1"
    [xml]$csprojXml = Get-Content (Join-Path $repoRoot "src\PolyStock.WPF\PolyStock.WPF.csproj")
    $Version = $csprojXml.Project.PropertyGroup.Version
}

if ($LASTEXITCODE -ne 0) {
    throw "Build and packaging step failed."
}

# 3. Upload to GitHub via Velopack
Write-Host ">>> Step 2/2: Uploading Release to GitHub ($repoUrl)..." -ForegroundColor Cyan
$releasesDir = Join-Path $repoRoot "Releases"

$uploadArgs = @(
    "upload", "github",
    "--repoUrl", $repoUrl,
    "--token", $Token,
    "--outputDir", $releasesDir,
    "--publish"
)

if ($PreRelease) {
    $uploadArgs += "--pre"
}

vpk @uploadArgs

if ($LASTEXITCODE -ne 0) {
    throw "vpk upload github failed with exit code $LASTEXITCODE."
}

# Upload PolyStock-Setup.exe alias asset to GitHub release
$setupExe = Join-Path $releasesDir "PolyStock-Setup.exe"
if (Test-Path $setupExe) {
    try {
        $headers = @{
            "Authorization" = "Bearer $Token"
            "User-Agent" = "PolyStock"
            "Accept" = "application/vnd.github.v3+json"
        }
        $rel = Invoke-RestMethod -Uri "https://api.github.com/repos/progressivesystems2-bit/Polystock/releases/tags/$Version" -Headers $headers
        $hasAlias = $rel.assets | Where-Object { $_.name -eq "PolyStock-Setup.exe" }
        if (-not $hasAlias) {
            Write-Host "Uploading PolyStock-Setup.exe alias to release..." -ForegroundColor DarkGray
            $uploadHeaders = @{
                "Authorization" = "Bearer $Token"
                "User-Agent" = "PolyStock"
                "Content-Type" = "application/octet-stream"
            }
            $uploadUrl = "https://uploads.github.com/repos/progressivesystems2-bit/Polystock/releases/$($rel.id)/assets?name=PolyStock-Setup.exe"
            $bytes = [System.IO.File]::ReadAllBytes($setupExe)
            Invoke-RestMethod -Uri $uploadUrl -Method Post -Headers $uploadHeaders -Body $bytes | Out-Null
        }
    } catch {
        Write-Host "Alias asset note: $($_.Exception.Message)" -ForegroundColor DarkGray
    }
}

Write-Host ""
Write-Host "==========================================================" -ForegroundColor Green
Write-Host "🎉 Release Published Successfully to GitHub!" -ForegroundColor Green
Write-Host "View Release at: $repoUrl/releases" -ForegroundColor Cyan
Write-Host "==========================================================" -ForegroundColor Green
