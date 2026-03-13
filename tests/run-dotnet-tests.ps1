param(
  [string]$Configuration = "Release"
)

$ErrorActionPreference = "Stop"

Push-Location (Join-Path $PSScriptRoot "..")
try {
  dotnet test "tests/GameApiExporter.Tests.csproj" -c $Configuration
  exit $LASTEXITCODE
} finally {
  Pop-Location
}

