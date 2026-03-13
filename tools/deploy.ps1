param(
  [string]$Configuration = "Release",
  [string]$GameInstallDir = $(if ($env:SEVEN_DTD_INSTALL_DIR) { $env:SEVEN_DTD_INSTALL_DIR } else { "" })
)

$ErrorActionPreference = "Stop"

$devTools = Join-Path $PSScriptRoot "..\\7dtd-mod-dev-tools\\tools\\deploy.ps1"
& $devTools -ModRepoPath (Join-Path $PSScriptRoot "..") -ModName "AutoReadMod" -Configuration $Configuration -GameInstallDir $GameInstallDir
exit $LASTEXITCODE

