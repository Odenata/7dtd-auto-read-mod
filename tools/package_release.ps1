<#
.SYNOPSIS
  Creates a CurseForge/GitHub release zip under dist\.

.DESCRIPTION
  Runs prepare_mod.ps1, stages the deployable files under a single top-level
  AutoReadMod\ folder, and writes AutoReadMod-<VERSION>.zip
  (mod version only; game compatibility is documented in docs/COMPATIBILITY.md).

  Generated zips are release artifacts and should not be committed.

.PARAMETER Configuration
  MSBuild configuration when using the default dotnet output path (default Release).

.PARAMETER DllPath
  Override path to AutoReadMod.dll, usually bazel-bin\src\AutoReadMod.dll.

.PARAMETER ModRepoRoot
  Repository root (parent of tools). Defaults to parent of this script's directory.

.PARAMETER OutputDir
  Directory for release artifacts. Defaults to <repo>\dist.

.PARAMETER Build
  If set, runs tools\build.ps1 before prepare when the DLL is missing or -Build is explicit.
#>
param(
  [string]$Configuration = "Release",
  [string]$DllPath = "",
  [string]$ModRepoRoot = "",
  [string]$OutputDir = "",
  [switch]$Build
)

$ErrorActionPreference = "Stop"

if ([string]::IsNullOrWhiteSpace($ModRepoRoot)) {
  $ModRepoRoot = Split-Path $PSScriptRoot -Parent
}
$ModRepoRoot = (Resolve-Path $ModRepoRoot).Path

if ([string]::IsNullOrWhiteSpace($OutputDir)) {
  $OutputDir = Join-Path $ModRepoRoot "dist"
}

$versionFile = Join-Path $ModRepoRoot "VERSION"
if (-not (Test-Path $versionFile)) {
  Write-Host "ERROR: VERSION not found at $versionFile" -ForegroundColor Red
  exit 1
}

$version = (Get-Content -LiteralPath $versionFile -Raw).Trim()
if ([string]::IsNullOrWhiteSpace($version)) {
  Write-Host "ERROR: VERSION is empty." -ForegroundColor Red
  exit 1
}

function Get-SafeArchiveLabel([string]$raw) {
  $t = ($raw.Trim() -replace '[\\/:*?"<>|]+', "-" -replace "\s+", "-").Trim("-")
  if ([string]::IsNullOrWhiteSpace($t)) { return "unknown" }
  return $t
}

$modLabel = Get-SafeArchiveLabel $version

$bazelDll = Join-Path $ModRepoRoot "bazel-bin\src\AutoReadMod.dll"
$msbuildDll = Join-Path $ModRepoRoot "src\bin\$Configuration\net48\AutoReadMod.dll"
if ([string]::IsNullOrWhiteSpace($DllPath)) {
  # Prefer the hermetic Bazel output (same as deploy.ps1). Falling back to the
  # MSBuild path alone previously packaged a stale DLL with a fresh ModInfo.
  if (Test-Path $bazelDll) {
    $DllPath = $bazelDll
  } else {
    $DllPath = $msbuildDll
  }
}

if ($Build -or -not (Test-Path $DllPath)) {
  $buildScript = Join-Path $PSScriptRoot "build.ps1"
  if (-not (Test-Path $buildScript)) {
    Write-Host "ERROR: build.ps1 not found at $buildScript" -ForegroundColor Red
    exit 1
  }
  & $buildScript -Configuration $Configuration
  if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
  if (-not (Test-Path $DllPath)) {
    if (Test-Path $bazelDll) {
      $DllPath = $bazelDll
    } elseif (Test-Path $msbuildDll) {
      $DllPath = $msbuildDll
    }
  }
}

$prepareScript = Join-Path $PSScriptRoot "prepare_mod.ps1"
if (-not (Test-Path $prepareScript)) {
  Write-Host "ERROR: prepare_mod.ps1 not found at $prepareScript" -ForegroundColor Red
  exit 1
}

$prepareArgs = @{ ModRepoRoot = $ModRepoRoot; Configuration = $Configuration; DllPath = $DllPath }
& $prepareScript @prepareArgs
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

$preparedDir = Join-Path $ModRepoRoot "prepared_mod_files"
if (-not (Test-Path $preparedDir)) {
  Write-Host "ERROR: prepared_mod_files not found after prepare." -ForegroundColor Red
  exit 1
}

New-Item -ItemType Directory -Path $OutputDir -Force | Out-Null

$stageRoot = Join-Path $OutputDir "_package"
$stageModDir = Join-Path $stageRoot "AutoReadMod"
if (Test-Path $stageRoot) {
  Remove-Item -Path $stageRoot -Recurse -Force
}
New-Item -ItemType Directory -Path $stageModDir -Force | Out-Null

$requiredFiles = @(
  "AutoReadMod.dll",
  "GearsAPI.dll",
  "ModInfo.xml",
  "Config.xml",
  "ModSettings.xml",
  "icon.png"
)

foreach ($file in $requiredFiles) {
  $src = Join-Path $preparedDir $file
  if (-not (Test-Path $src)) {
    Write-Host "ERROR: Missing prepared file: $src" -ForegroundColor Red
    exit 1
  }
}

# Ship the full prepared tree (DLL, GearsAPI, settings, icon, Config\Localization.csv, …).
Copy-Item -Path (Join-Path $preparedDir "*") -Destination $stageModDir -Recurse -Force

$zipPath = Join-Path $OutputDir "AutoReadMod-$modLabel.zip"
if (Test-Path $zipPath) {
  Remove-Item -Path $zipPath -Force
}

Compress-Archive -Path $stageModDir -DestinationPath $zipPath -CompressionLevel Optimal
Remove-Item -Path $stageRoot -Recurse -Force

Write-Host "`nRelease package created:" -ForegroundColor Cyan
Write-Host $zipPath -ForegroundColor Yellow
Write-Host "Mod version: $version (game compatibility: docs/COMPATIBILITY.md)" -ForegroundColor Cyan
Write-Host "Zip root: AutoReadMod/" -ForegroundColor Cyan
exit 0
