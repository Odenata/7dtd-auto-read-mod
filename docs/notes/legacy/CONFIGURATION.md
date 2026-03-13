# Configuration Guide

## Overview

The Auto-Read Mod can be configured via XML without needing to uninstall or recompile the mod.

## Configuration File Location

After first launch, the configuration file is created at:
```
%APPDATA%\..\LocalLow\The Fun Pimps\7 Days To Die\Mods\AutoReadMod\Config.xml
```

On Windows, this typically resolves to:
```
C:\Users\<YourUsername>\AppData\LocalLow\The Fun Pimps\7 Days To Die\Mods\AutoReadMod\Config.xml
```

## Configuration Options

### Enable/Disable Mod

```xml
<Enabled>true</Enabled>
```

Set to `false` to disable the mod without uninstalling it.

### Item Types

Configure which types of items are automatically read:

```xml
<ItemTypes>
  <Books>true</Books>
  <Magazines>true</Magazines>
  <Schematics>true</Schematics>
</ItemTypes>
```

Set any to `false` to prevent auto-reading that item type.

## Example Configurations

### Disable Mod Completely
```xml
<?xml version="1.0" encoding="UTF-8"?>
<AutoReadModConfig>
  <Enabled>false</Enabled>
  <ItemTypes>
    <Books>true</Books>
    <Magazines>true</Magazines>
    <Schematics>true</Schematics>
  </ItemTypes>
</AutoReadModConfig>
```

### Only Auto-Read Schematics
```xml
<?xml version="1.0" encoding="UTF-8"?>
<AutoReadModConfig>
  <Enabled>true</Enabled>
  <ItemTypes>
    <Books>false</Books>
    <Magazines>false</Magazines>
    <Schematics>true</Schematics>
  </ItemTypes>
</AutoReadModConfig>
```

### Only Auto-Read Books and Magazines
```xml
<?xml version="1.0" encoding="UTF-8"?>
<AutoReadModConfig>
  <Enabled>true</Enabled>
  <ItemTypes>
    <Books>true</Books>
    <Magazines>true</Magazines>
    <Schematics>false</Schematics>
  </ItemTypes>
</AutoReadModConfig>
```

## Applying Configuration Changes

1. Exit 7 Days to Die completely
2. Edit the Config.xml file
3. Save the file
4. Restart 7 Days to Die

The mod will load the new configuration on startup.

## Default Configuration

If the configuration file doesn't exist, the mod creates one with these defaults:
- Enabled: `true`
- Books: `true`
- Magazines: `true`
- Schematics: `true`

## Troubleshooting

### Configuration Not Loading
- Check the game console (F1) for error messages
- Verify XML syntax is correct
- Ensure file is saved with UTF-8 encoding
- Check file permissions

### Configuration File Not Created
- Launch the game at least once with the mod installed
- Check the mod is loaded (console shows "Auto-Read Mod loaded successfully")
- Manually create the directory and file if needed

### Reset to Defaults
Delete the Config.xml file and restart the game. The mod will create a new default configuration.
