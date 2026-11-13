<#
.SYNOPSIS
  Deploys AutoReadMod into the game Mods folder without shipping 0Harmony.dll.

.DESCRIPTION
  Prefers bazel-bin\src\AutoReadMod.dll when present. Copies AutoReadMod.dll, GearsAPI.dll, ModInfo.xml, ModSettings.xml, icon.png, and Config\Localization.csv.
  Preserves existing Config.xml / ModSettings.xml in the game Mods folder (does not overwrite user settings).
  Removes any leftover Mods\AutoReadMod\0Harmony.dll so the game uses Mods\0_TFP_Harmony.
#>
param(
  [string]$Configuration = "Release",
  [string]$GameInstallDir = $(if ($env:7_DAYS_TO_DIE_GAME_PATH) { $env:7_DAYS_TO_DIE_GAME_PATH } else { "C:\Program Files (x86)\Steam\steamapps\common\7 Days To Die" }),
  [string]$DllPath = "",
  [switch]$OverwriteConfig
  # Also overwrites ModSettings.xml when set
)

$ErrorActionPreference = "Stop"

$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
$GameInstallDir = $GameInstallDir.TrimEnd('\', '/')

if ([string]::IsNullOrWhiteSpace($DllPath)) {
  $bazelDll = Join-Path $repoRoot "bazel-bin\src\AutoReadMod.dll"
  if (Test-Path $bazelDll) {
    $DllPath = $bazelDll
  } else {
    $DllPath = Join-Path $repoRoot "src\bin\$Configuration\net48\AutoReadMod.dll"
  }
}

if (-not (Test-Path $DllPath)) {
  Write-Host "ERROR: Mod DLL not found: $DllPath" -ForegroundColor Red
  Write-Host "Build first: bazel build //src:AutoReadMod   or   .\tools\build.ps1" -ForegroundColor Yellow
  exit 1
}

$modPath = Join-Path $GameInstallDir "Mods\AutoReadMod"
$tfpHarmony = Join-Path $GameInstallDir "Mods\0_TFP_Harmony"
if (-not (Test-Path $tfpHarmony)) {
  Write-Host "WARNING: Mods\0_TFP_Harmony not found under $GameInstallDir" -ForegroundColor Yellow
  Write-Host "  AutoRead requires the official Harmony wrapper. Verify Steam game files if missing." -ForegroundColor Yellow
}

if (-not (Test-Path $modPath)) {
  New-Item -ItemType Directory -Path $modPath -Force | Out-Null
  Write-Host "Created mod directory: $modPath" -ForegroundColor Green
}

Write-Host "Deploying AutoReadMod to game directory..." -ForegroundColor Cyan

Copy-Item -LiteralPath $DllPath -Destination (Join-Path $modPath "AutoReadMod.dll") -Force
Write-Host "Copied AutoReadMod.dll" -ForegroundColor Green

$modInfo = Join-Path $repoRoot "src\ModInfo.xml"
Copy-Item -LiteralPath $modInfo -Destination (Join-Path $modPath "ModInfo.xml") -Force
Write-Host "Copied ModInfo.xml" -ForegroundColor Green

$gearsApi = Join-Path $repoRoot "third_party\gearsapi\lib\net472\GearsAPI.dll"
if (-not (Test-Path $gearsApi)) {
  Write-Host "ERROR: Missing GearsAPI.dll at $gearsApi" -ForegroundColor Red
  exit 1
}
Copy-Item -LiteralPath $gearsApi -Destination (Join-Path $modPath "GearsAPI.dll") -Force
Write-Host "Copied GearsAPI.dll" -ForegroundColor Green

$configSrc = Join-Path $repoRoot "src\Config.xml"
$configDst = Join-Path $modPath "Config.xml"
if ($OverwriteConfig -or -not (Test-Path $configDst)) {
  Copy-Item -LiteralPath $configSrc -Destination $configDst -Force
  Write-Host "Copied Config.xml" -ForegroundColor Green
} else {
  Write-Host "Kept existing Config.xml (use -OverwriteConfig to replace)" -ForegroundColor Cyan
}

$modSettingsSrc = Join-Path $repoRoot "src\ModSettings.xml"
$modSettingsDst = Join-Path $modPath "ModSettings.xml"
if ($OverwriteConfig -or -not (Test-Path $modSettingsDst)) {
  Copy-Item -LiteralPath $modSettingsSrc -Destination $modSettingsDst -Force
  Write-Host "Copied ModSettings.xml" -ForegroundColor Green
} else {
  Write-Host "Kept existing ModSettings.xml (use -OverwriteConfig to replace)" -ForegroundColor Cyan
}

$locSrc = Join-Path $repoRoot "src\Config\Localization.csv"
$locDir = Join-Path $modPath "Config"
if (-not (Test-Path $locDir)) {
  New-Item -ItemType Directory -Path $locDir -Force | Out-Null
}
Copy-Item -LiteralPath $locSrc -Destination (Join-Path $locDir "Localization.csv") -Force
Write-Host "Copied Config\Localization.csv" -ForegroundColor Green

$iconSrc = Join-Path $repoRoot "icon.png"
if (-not (Test-Path $iconSrc)) {
  Write-Host "ERROR: Missing $iconSrc" -ForegroundColor Red
  exit 1
}
Copy-Item -LiteralPath $iconSrc -Destination (Join-Path $modPath "icon.png") -Force
Write-Host "Copied icon.png" -ForegroundColor Green

$staleHarmony = Join-Path $modPath "0Harmony.dll"
if (Test-Path $staleHarmony) {
  Remove-Item -LiteralPath $staleHarmony -Force
  Write-Host "Removed leftover 0Harmony.dll (use Mods\0_TFP_Harmony instead)" -ForegroundColor Yellow
}

Write-Host "`nDeployment complete!" -ForegroundColor Green
Write-Host "Mod location: $modPath" -ForegroundColor Yellow
Write-Host "Runtime Harmony: Mods\0_TFP_Harmony (not bundled)" -ForegroundColor Yellow
Write-Host "`nRestart 7 Days to Die to load the mod." -ForegroundColor Cyan
exit 0
