param(
  [int]$TimeoutSeconds = 60,
  [string]$GameInstallDir = $(if ($env:7_DAYS_TO_DIE_GAME_PATH) { $env:7_DAYS_TO_DIE_GAME_PATH } else { "" })
)

$ErrorActionPreference = "Stop"

$devTools = Join-Path $PSScriptRoot "..\\..\\7dtd-mod-dev-tools\\tools\\integration-test\\test-integration.ps1"
& $devTools -TimeoutSeconds $TimeoutSeconds -GameInstallDir $GameInstallDir -ModLogPattern "AutoReadMod loaded successfully"
exit $LASTEXITCODE

