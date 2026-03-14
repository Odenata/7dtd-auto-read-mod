param(
  [string]$Configuration = "Release",
  [string]$TestClass = ""
)

$ErrorActionPreference = "Stop"

Push-Location (Join-Path $PSScriptRoot "..")
try {
  $dotnet = (Get-Command dotnet -ErrorAction SilentlyContinue | Select-Object -ExpandProperty Source -First 1)
  if (-not $dotnet) {
    $candidates = @()

    foreach ($root in @(
      ${env:ProgramFiles},
      ${env:ProgramW6432},
      ${env:ProgramFiles(x86)},
      "C:\Program Files",
      "C:\Program Files (x86)"
    )) {
      if ($root) {
        $candidate = Join-Path $root "dotnet\dotnet.exe"
        if (Test-Path $candidate) {
          $candidates += $candidate
        }
      }
    }

    $dotnet = $candidates | Select-Object -First 1
  }

  if (-not $dotnet) {
    throw "Unable to locate dotnet.exe. Ensure the .NET SDK is installed or available on PATH."
  }

  $arguments = @(
    "test",
    "tests/AutoReadMod.Tests.csproj",
    "-c",
    $Configuration
  )

  if ($TestClass) {
    $arguments += @(
      "--filter",
      "FullyQualifiedName~$TestClass"
    )
  }

  & $dotnet @arguments
  exit $LASTEXITCODE
} finally {
  Pop-Location
}

