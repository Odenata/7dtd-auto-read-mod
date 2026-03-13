param(
  [string]$Configuration = "Release"
)

$ErrorActionPreference = "Stop"

Push-Location (Join-Path $PSScriptRoot "..")
try {
  dotnet test "tests/AutoReadMod.Tests.csproj" -c $Configuration
  exit $LASTEXITCODE
} finally {
  Pop-Location
}

