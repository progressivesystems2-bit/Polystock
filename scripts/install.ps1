<#
.SYNOPSIS
    One-click installer script for PolyStock.
.DESCRIPTION
    Downloads the latest PolyStock-Setup.exe from GitHub Releases and launches the installer.
    Can be run locally or executed directly via:
    irm https://raw.githubusercontent.com/progressivesystems2-bit/Polystock/main/scripts/install.ps1 | iex
.PARAMETER Silent
    Runs the installer silently in the background.
#>

[CmdletBinding()]
param(
    [switch]$Silent
)

$ErrorActionPreference = "Stop"

Write-Host "==========================================================" -ForegroundColor Green
Write-Host "     PolyStock One-Click Installer (Tauras Technology)    " -ForegroundColor Green
Write-Host "==========================================================" -ForegroundColor Green

$repoUrl = "https://github.com/progressivesystems2-bit/Polystock"
$tempDir = [System.IO.Path]::GetTempPath()
$installerPath = Join-Path $tempDir "PolyStock-Setup.exe"

if (Test-Path $installerPath) {
    Remove-Item -Force $installerPath
}

Write-Host "Downloading latest PolyStock installer from GitHub..." -ForegroundColor Cyan

# Ensure TLS 1.2+
[Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12 -bor [Net.SecurityProtocolType]::Tls13
$downloadSuccess = $false

foreach ($exeName in @("PolyStock-win-Setup.exe", "PolyStock-Setup.exe")) {
    $downloadUrl = "$repoUrl/releases/latest/download/$exeName"
    try {
        Write-Host "Trying: $downloadUrl" -ForegroundColor DarkGray
        $webClient = New-Object System.Net.WebClient
        $webClient.Headers.Add("User-Agent", "PolyStock-Installer")
        $webClient.DownloadFile($downloadUrl, $installerPath)
        $downloadSuccess = $true
        break
    } catch {
        # Try next candidate
    }
}

if (-not $downloadSuccess) {
    Write-Host "Attempting fallback via GitHub API to query latest release assets..." -ForegroundColor Yellow
    
    try {
        $apiUrl = "https://api.github.com/repos/progressivesystems2-bit/Polystock/releases/latest"
        $headers = @{ "User-Agent" = "PolyStock-Installer" }
        if (![string]::IsNullOrWhiteSpace($env:GITHUB_TOKEN)) {
            $headers["Authorization"] = "Bearer $env:GITHUB_TOKEN"
        }
        $release = Invoke-RestMethod -Uri $apiUrl -Headers $headers
        $asset = $release.assets | Where-Object { $_.name -like "*Setup.exe" } | Select-Object -First 1
        if ($asset) {
            Write-Host "Found installer asset: $($asset.name). Downloading..." -ForegroundColor Cyan
            Invoke-WebRequest -Uri $asset.browser_download_url -OutFile $installerPath -Headers $headers
        } else {
            throw "No setup asset found in latest GitHub release."
        }
    } catch {
        throw "Failed to download PolyStock installer: $($_.Exception.Message)"
    }
}

if (-not (Test-Path $installerPath)) {
    throw "Installer file could not be verified at: $installerPath"
}

Write-Host "Launching PolyStock Installer..." -ForegroundColor Green
$startArgs = @()
if ($Silent) {
    $startArgs += @("--silent")
}

Start-Process -FilePath $installerPath -ArgumentList $startArgs

Write-Host "Installer launched successfully! PolyStock will start automatically once setup completes." -ForegroundColor Green
