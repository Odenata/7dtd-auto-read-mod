# Wrapper for bazel run //tools:deploy. Prints non-hermetic warning and invokes tools\deploy.ps1 from repo root.
param(
  [string]$Configuration = "Release",
  [string]$GameInstallDir = $(if ($env:7_DAYS_TO_DIE_GAME_PATH) { $env:7_DAYS_TO_DIE_GAME_PATH } else { "C:\Program Files (x86)\Steam\steamapps\common\7 Days To Die" })
)

$ErrorActionPreference = "Stop"

Write-Host ""
Write-Host "WARNING: This deploy is NOT hermetic. Bazel does not control:" -ForegroundColor Yellow
Write-Host "  - Game install directory (env 7_DAYS_TO_DIE_GAME_PATH or default Steam path)" -ForegroundColor Yellow
Write-Host "  - 0Harmony.dll from sibling repo 7dtd-mod-dev-tools" -ForegroundColor Yellow
Write-Host "  - Mod repo files (ModInfo.xml, Config.xml)" -ForegroundColor Yellow
Write-Host ""

$repoRoot = if ($env:BUILD_WORKSPACE_DIRECTORY) { $env:BUILD_WORKSPACE_DIRECTORY } else { Split-Path $PSScriptRoot -Parent }
$deployScript = Join-Path $repoRoot "tools\deploy.ps1"
if (-not (Test-Path $deployScript)) {
  Write-Host "ERROR: Script not found: $deployScript" -ForegroundColor Red
  exit 1
}

Push-Location $repoRoot
try {
  $params = @{ Configuration = $Configuration }
  if (-not [string]::IsNullOrWhiteSpace($GameInstallDir)) {
    $params["GameInstallDir"] = $GameInstallDir
  }
  & $deployScript @params
  exit $LASTEXITCODE
} finally {
  Pop-Location
}
