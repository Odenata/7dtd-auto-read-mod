param(
  [int]$TimeoutSeconds = 90,
  [string]$GameInstallDir = $(if ($env:SEVENDTD_GAME_PATH) { $env:SEVENDTD_GAME_PATH } elseif ($env:7_DAYS_TO_DIE_GAME_PATH) { $env:7_DAYS_TO_DIE_GAME_PATH } else { "" })
)

$ErrorActionPreference = "Stop"

$devTools = Join-Path $PSScriptRoot "..\\..\\7dtd-mod-dev-tools\\tools\\integration-test\\test-dedicated-server.ps1"
$params = @{
  TimeoutSeconds = $TimeoutSeconds
  ModLogPattern = "AutoReadMod loaded successfully"
}
if ($GameInstallDir) {
  $params["GameInstallDir"] = $GameInstallDir
}
& $devTools @params
exit $LASTEXITCODE
