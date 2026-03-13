param(
  [string]$Configuration = "Release"
)

$ErrorActionPreference = "Stop"

$devTools = Join-Path $PSScriptRoot "..\\..\\7dtd-mod-dev-tools\\tools\\build.ps1"
& $devTools -ModRepoPath (Join-Path $PSScriptRoot "..") -Configuration $Configuration -CsprojPath "src/GameApiExporter.csproj" -DllPath "src/bin/$Configuration/net472/GameApiExporter.dll"
exit $LASTEXITCODE

