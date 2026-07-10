<#
.SYNOPSIS
  Deploys AutoReadMod into the game Mods folder without shipping 0Harmony.dll.

.DESCRIPTION
  Prefers bazel-bin\src\AutoReadMod.dll when present. Copies AutoReadMod.dll and ModInfo.xml.
  Preserves an existing Config.xml in the game Mods folder (does not overwrite user settings).
  Removes any leftover Mods\AutoReadMod\0Harmony.dll so the game uses Mods\0_TFP_Harmony.
#>
param(
  [string]$Configuration = "Release",
  [string]$GameInstallDir = $(if ($env:7_DAYS_TO_DIE_GAME_PATH) { $env:7_DAYS_TO_DIE_GAME_PATH } else { "C:\Program Files (x86)\Steam\steamapps\common\7 Days To Die" }),
  [string]$DllPath = "",
  [switch]$OverwriteConfig
)

$ErrorActionPreference = "Stop"

$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
$GameInstallDir = $GameInstallDir.TrimEnd('\', '/')

if ([string]::IsNullOrWhiteSpace($DllPath)) {
  $bazelDll = Join-Path $repoRoot "bazel-bin\src\AutoReadMod.dll"
  if (Test-Path $bazelDll) {
    $DllPath = $bazelDll
  } else {
    $DllPath = Join-Path $repoRoot "src\bin\$Configuration\net472\AutoReadMod.dll"
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

$configSrc = Join-Path $repoRoot "src\Config.xml"
$configDst = Join-Path $modPath "Config.xml"
if ($OverwriteConfig -or -not (Test-Path $configDst)) {
  Copy-Item -LiteralPath $configSrc -Destination $configDst -Force
  Write-Host "Copied Config.xml" -ForegroundColor Green
} else {
  Write-Host "Kept existing Config.xml (use -OverwriteConfig to replace)" -ForegroundColor Cyan
}

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
