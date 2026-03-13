param(
  [int]$TimeoutSeconds = 60,
  [string]$GameInstallDir = $(if ($env:SEVEN_DTD_INSTALL_DIR) { $env:SEVEN_DTD_INSTALL_DIR } else { "" })
)

$ErrorActionPreference = "Stop"

$devTools = Join-Path $PSScriptRoot "..\\..\\7dtd-mod-dev-tools\\tools\\test-integration.ps1"
& $devTools -TimeoutSeconds $TimeoutSeconds -GameInstallDir $GameInstallDir -ModLogPattern "Runtime API export completed|Runtime API export failed"
exit $LASTEXITCODE

