#!/usr/bin/env pwsh

# LfsMinio Auto Installer for Windows (PowerShell)
# This script downloads the latest release and installs it to ~/.lfs-mirror

param(
    [string]$InstallDir = "$env:USERPROFILE\.lfs-mirror"
)

$ErrorActionPreference = "Stop"

Write-Host "LfsMinio Auto Installer" -ForegroundColor Green
Write-Host "======================" -ForegroundColor Green

# Create install directory
if (!(Test-Path $InstallDir)) {
    Write-Host "Creating install directory: $InstallDir" -ForegroundColor Yellow
    New-Item -ItemType Directory -Path $InstallDir -Force | Out-Null
}

# Get latest release info
Write-Host "Fetching latest release information..." -ForegroundColor Yellow
try {
    $releaseInfo = Invoke-RestMethod -Uri "https://api.github.com/repos/li-zhixin/LfsMinio/releases/latest"
} catch {
    Write-Error "Failed to fetch release information: $_"
    exit 1
}

$version = $releaseInfo.tag_name
Write-Host "Latest version: $version" -ForegroundColor Green

# Determine architecture and OS
$arch = if ([Environment]::Is64BitOperatingSystem) { "amd64" } else { "386" }
$os = "windows"

# Find the appropriate asset
$assetName = "lfs-minio-$os-$arch"
$asset = $releaseInfo.assets | Where-Object { $_.name -like "*$assetName*" }

if (!$asset) {
    Write-Error "No suitable release found for $os-$arch"
    exit 1
}

$downloadUrl = $asset.browser_download_url
$fileName = $asset.name
$downloadPath = Join-Path $env:TEMP $fileName

Write-Host "Downloading $fileName..." -ForegroundColor Yellow
try {
    Invoke-WebRequest -Uri $downloadUrl -OutFile $downloadPath -UseBasicParsing
} catch {
    Write-Error "Failed to download release: $_"
    exit 1
}

# Extract archive
Write-Host "Extracting to $InstallDir..." -ForegroundColor Yellow
try {
    if ($fileName.EndsWith('.zip')) {
        Expand-Archive -Path $downloadPath -DestinationPath $InstallDir -Force
    } elseif ($fileName.EndsWith('.tar.gz')) {
        # Use tar if available (Windows 10+)
        if (Get-Command tar -ErrorAction SilentlyContinue) {
            & tar -xzf $downloadPath -C $InstallDir
        } else {
            Write-Error "tar command not found. Please install tar or download the .zip version."
            exit 1
        }
    } else {
        Copy-Item $downloadPath $InstallDir -Force
    }
} catch {
    Write-Error "Failed to extract archive: $_"
    exit 1
}

# Clean up
Remove-Item $downloadPath -Force

# Add to PATH if not already present
$currentPath = [Environment]::GetEnvironmentVariable("PATH", "User")
if ($currentPath -notlike "*$InstallDir*") {
    Write-Host "Adding $InstallDir to user PATH..." -ForegroundColor Yellow
    $newPath = if ($currentPath) { "$currentPath;$InstallDir" } else { $InstallDir }
    [Environment]::SetEnvironmentVariable("PATH", $newPath, "User")
    
    # Update current session PATH
    $env:PATH = "$env:PATH;$InstallDir"
    
    Write-Host "Added to PATH. You may need to restart your terminal." -ForegroundColor Green
} else {
    Write-Host "Directory already in PATH." -ForegroundColor Green
}

Write-Host "" -ForegroundColor Green
Write-Host "Installation completed successfully!" -ForegroundColor Green
Write-Host "LfsMinio has been installed to: $InstallDir" -ForegroundColor Green
Write-Host "" -ForegroundColor Green
Write-Host " please restart your terminal." -ForegroundColor Yellow