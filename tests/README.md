# Tests

This directory contains unit tests for the 7 Days to Die mod project.

## Test Framework

Tests use **Pester**, PowerShell's testing framework.

### Pester Version Compatibility

The tests are written for **Pester 5.x** syntax. If you have Pester 3.x installed, you'll see syntax errors like:
- `'-Be' is not a valid Should operator`
- `'-Match' is not a valid Should operator`

### Installing/Updating Pester

```powershell
# Install Pester 5.x
Install-Module -Name Pester -Force -SkipPublisherCheck

# Or update existing installation
Update-Module -Name Pester
```

### Running Tests

```powershell
# Run all tests
Invoke-Pester

# Run specific test file
Invoke-Pester tests\document-game-api.Tests.ps1

# Run with detailed output
Invoke-Pester -Output Detailed
```

## Test Files

### document-game-api.Tests.ps1

Unit tests for the `tools/document-game-api.ps1` script that extracts game API documentation.

**Test Coverage:**
- Script validation (syntax, parameters)
- Game version detection
- Assembly loading
- Type extraction
- Method/field/property extraction
- Error handling
- Output generation
- Documentation quality

**Purpose:**
These tests ensure the API extraction script:
1. Has valid PowerShell syntax
2. Handles errors gracefully
3. Extracts complete type information
4. Generates properly formatted documentation
5. Can be reliably used when game versions update

### C# Unit Tests

C# unit tests are in the `tests/` directory and use xUnit:
- `ModApiTests.cs` - Tests for ModApi initialization
- `ModConfigTests.cs` - Tests for configuration loading
- `AutoReaderTests.cs` - Tests for item reading logic
- `ToolValidationTests.cs` - Tests for build/deploy tools

Run C# tests with:
```bash
bazel test //tests:all
```

## Test Philosophy

1. **Script Tests**: Validate structure and error handling, not execution (since they require game files)
2. **C# Tests**: Full unit tests with mocking where needed
3. **Integration Tests**: `tools/test-integration.ps1` tests the mod in the actual game

## Continuous Integration

Tests should be run:
- Before committing changes
- After game version updates
- When modifying extraction scripts
- When changing mod logic

## Known Limitations

- PowerShell tests validate script structure, not execution with real game files
- Integration tests require the game to be installed
- Some tests may fail if game installation path differs from default
