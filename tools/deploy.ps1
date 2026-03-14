param(
  [string]$Configuration = "Release",
  [string]$GameInstallDir = $(if ($env:7_DAYS_TO_DIE_GAME_PATH) { $env:7_DAYS_TO_DIE_GAME_PATH } else { "" })
)

$ErrorActionPreference = "Stop"

$devTools = Join-Path $PSScriptRoot "..\\..\\7dtd-mod-dev-tools\\tools\\build-deploy\\deploy.ps1"
& $devTools -ModRepoPath (Join-Path $PSScriptRoot "..") -ModName "AutoReadMod" -Configuration $Configuration -GameInstallDir $GameInstallDir
exit $LASTEXITCODE

