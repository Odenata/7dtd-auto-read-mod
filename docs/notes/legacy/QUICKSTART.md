# Quick Start Guide

## Prerequisites

1. Install Bazel 7.0.0+ (or use Bazelisk)
2. Install .NET SDK 8.0 or later (for local development)
3. Have 7 Days to Die installed

## Building the Mod

### Using Bazel (Recommended - Hermetic)

```bash
# Build the mod
bazel build //src:AutoReadMod

# Run all tests
bazel test //tests:all

# Deploy to game
bazel run //tools:deploy

# Package for distribution
bazel run //tools:package
```

### Using Convenience Aliases

```bash
# Build
bazel build //:build

# Test
bazel run //:test

# Deploy
bazel run //:deploy

# Package
bazel run //:package
```

### Using PowerShell Scripts (Alternative)

```powershell
# Build the mod
.\tools\build.ps1

# Run tests
.\tools\test.ps1

# Deploy to game for testing
.\tools\deploy.ps1

# Package for distribution
.\tools\package.ps1
```

### Using dotnet CLI (Local Development)

```bash
# Build
dotnet build src/AutoReadMod.csproj -c Release

# Test
dotnet test tests/AutoReadMod.Tests.csproj

# The DLL will be in: src/bin/Release/net6.0/AutoReadMod.dll
```

## Testing the Mod

1. Build the mod using one of the methods above
2. Run the deployment script: `.\tools\deploy.ps1`
3. Launch 7 Days to Die
4. Check the console log for "Auto-Read Mod loaded successfully"
5. Open a container with books/magazines/recipes
6. The items should be automatically read and removed

## Project Structure

```
seven_days_to_die_mod_attempt/
├── src/                    # Source code
│   ├── ModApi.cs          # Mod initialization
│   ├── ContainerOpenPatch.cs  # Harmony patch
│   ├── AutoReader.cs      # Auto-reading logic
│   ├── ModInfo.xml        # Mod metadata
│   └── AutoReadMod.csproj # Project file
├── tests/                 # Unit tests
│   ├── ModApiTests.cs
│   ├── AutoReaderTests.cs
│   └── *ToolTests.cs
├── tools/                 # Build and deployment scripts
│   ├── build.ps1
│   ├── deploy.ps1
│   └── package.ps1
├── BUILD.bazel           # Bazel build file
├── WORKSPACE             # Bazel workspace
├── VERSION               # Semantic version
└── README.md             # Documentation
```

## Troubleshooting

### Build Errors

- Ensure .NET SDK 8.0+ is installed: `dotnet --version`
- Check that game DLL references are correct in .csproj files
- Verify game installation path

### Mod Not Loading

- Check game console for errors
- Verify ModInfo.xml is in the mod directory
- Ensure DLL is in: `7 Days To Die/Mods/AutoReadMod/`

### Tests Failing

- Some tests may fail if game DLLs cannot be mocked
- Integration tests require the game to be installed
