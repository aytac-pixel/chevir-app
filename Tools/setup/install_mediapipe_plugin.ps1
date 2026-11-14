# Windows version of install_mediapipe_plugin.sh.
# Usage (PowerShell, from the project folder):  .\Tools\setup\install_mediapipe_plugin.ps1 [-Tag v0.16.3]
param([string]$Tag = "latest")
$ErrorActionPreference = "Stop"

$root = Resolve-Path (Join-Path $PSScriptRoot "..\..")
$repo = "homuler/MediaPipeUnityPlugin"
$api = if ($Tag -eq "latest") { "https://api.github.com/repos/$repo/releases/latest" } else { "https://api.github.com/repos/$repo/releases/tags/$Tag" }

Write-Host "Looking up $repo release '$Tag'..."
$release = Invoke-RestMethod -Uri $api -Headers @{ "User-Agent" = "cevir-setup" }
$asset = $release.assets | Where-Object { $_.name -match '^com\.github\.homuler\.mediapipe-.*\.tgz$' } | Select-Object -First 1
if (-not $asset) {
    Write-Error "No com.github.homuler.mediapipe-*.tgz asset found. Download it from https://github.com/$repo/releases into Packages\."
}

$target = Join-Path $root "Packages\$($asset.name)"
Write-Host "Downloading $($asset.name)..."
Invoke-WebRequest -Uri $asset.browser_download_url -OutFile $target

$manifestPath = Join-Path $root "Packages\manifest.json"
$manifest = Get-Content $manifestPath -Raw -Encoding UTF8 | ConvertFrom-Json
$manifest.dependencies | Add-Member -NotePropertyName "com.github.homuler.mediapipe" -NotePropertyValue "file:$($asset.name)" -Force
$manifest | ConvertTo-Json -Depth 10 | Set-Content $manifestPath -Encoding UTF8
Write-Host "Done: Packages\manifest.json now references $($asset.name)."
