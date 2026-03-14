# Tests

This directory contains unit tests for the AutoReadMod project.

## Test Framework

C# unit tests use **xUnit** and **Bazel**.

## Running Tests

```bash
bazel test //tests:all
```

## Test Files

- `ModApiTests.cs` — ModApi initialization
- `ModConfigTests.cs` — Configuration loading
- `AutoReaderTests.cs` — Item reading logic
- `ItemIdentificationTests.cs` — Item name pattern matching
- `ConfigurationFilteringTests.cs` — Config-based filtering
- `DebugLoggingTests.cs` — DebugMode and logging
- `BuildToolTests.cs`, `DeployToolTests.cs`, `PackageToolTests.cs` — Build/deploy tool validation

For detailed coverage and test cases, see [TEST_DOCUMENTATION.md](TEST_DOCUMENTATION.md).

## Test Philosophy

1. **C# Tests**: Full unit tests with mocking where needed
2. **Integration Tests**: `tools/test-integration.ps1` tests the mod in the actual game

## Known Limitations

- Integration tests require the game to be installed
- Some tests may fail if game installation path differs from default
- Item consumption logic cannot be fully tested without game DLLs and a player performing actions and making observations
