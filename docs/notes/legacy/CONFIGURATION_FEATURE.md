# Configuration Feature Summary

## ✅ Implementation Complete

The Auto-Read Mod now supports XML-based configuration without requiring uninstallation or recompilation.

## New Files

### Source Code
- **src/ModConfig.cs** - Configuration loader and manager
- **src/Config.xml** - Default configuration template

### Tests
- **tests/ModConfigTests.cs** - Unit tests for configuration

### Documentation
- **CONFIGURATION.md** - User guide for configuration

## Configuration Options

### 1. Enable/Disable Mod
```xml
<Enabled>true</Enabled>
```
Users can disable the mod without uninstalling it.

### 2. Item Type Control
```xml
<ItemTypes>
  <Books>true</Books>
  <Magazines>true</Magazines>
  <Schematics>true</Schematics>
</ItemTypes>
```
Users can selectively enable/disable auto-reading for each item type.

## Technical Implementation

### Configuration Loading
- Loads from user's AppData directory on mod initialization
- Creates default config if none exists
- Logs configuration state to game console
- Graceful error handling with fallback to defaults

### Configuration Checking
- **AutoReader.ProcessContainer** checks `Enabled` flag first
- **AutoReader.IsReadableItem** respects individual item type settings
- Zero performance impact when disabled

### File Location
```
%APPDATA%\..\LocalLow\The Fun Pimps\7 Days To Die\Mods\AutoReadMod\Config.xml
```

## Code Changes

### Modified Files
1. **src/AutoReader.cs**
   - Added config check in `ProcessContainer`
   - Split item type checking in `IsReadableItem`

2. **src/ModApi.cs**
   - Loads and logs configuration on initialization

3. **tools/deploy.ps1** - Includes Config.xml
4. **tools/deploy.sh** - Includes Config.xml
5. **tools/package.ps1** - Includes Config.xml
6. **tools/package.sh** - Includes Config.xml
7. **src/BUILD** - Added Config.xml to data
8. **tools/BUILD** - Added Config.xml to deploy/package targets
9. **tests/BUILD** - Added ModConfigTests
10. **README.md** - Added configuration section

## Usage Examples

### Disable Mod Temporarily
```xml
<Enabled>false</Enabled>
```

### Only Auto-Read Schematics
```xml
<Books>false</Books>
<Magazines>false</Magazines>
<Schematics>true</Schematics>
```

### Default (Everything Enabled)
```xml
<Enabled>true</Enabled>
<Books>true</Books>
<Magazines>true</Magazines>
<Schematics>true</Schematics>
```

## Testing

### Unit Tests
- Configuration instance creation
- Default values verification
- All tests pass

### Integration Tests
- Mod disabled: Items not auto-read
- Books disabled: Only books remain
- All enabled: All items auto-read

## Benefits

1. **No Uninstall Needed** - Users can disable via config
2. **Granular Control** - Per-item-type configuration
3. **User-Friendly** - Simple XML editing
4. **Safe Defaults** - Creates default config automatically
5. **Persistent** - Settings survive game updates

## Documentation

- **CONFIGURATION.md** - Complete user guide
- **README.md** - Quick configuration reference
- **TESTING_CHECKLIST.md** - Configuration test cases

## Deployment

Configuration file is automatically:
- Included in deployment (tools/deploy.*)
- Included in distribution package (tools/package.*)
- Created on first run if missing

## Future Enhancements

Possible additions:
- In-game configuration UI
- Per-player configuration
- Whitelist/blacklist specific items
- Configuration reload command
