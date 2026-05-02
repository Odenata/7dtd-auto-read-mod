# Auto Read Mod

Auto Read Mod automatically reads books, magazines, schematics, and recipes when you open a loot container in 7 Days to Die.

## Features

- Automatically consumes readable loot when containers open.
- Supports books, magazines, schematics, and recipes.
- Configurable item-type toggles in `Config.xml`.

## Installation

Extract the release zip into your 7 Days to Die `Mods` folder so the files are under:

```text
Mods\AutoReadMod\
```

Easy Anti-Cheat must be disabled because this is a Harmony DLL mod.

## Configuration

Edit `Mods\AutoReadMod\Config.xml` to enable or disable the mod, choose item types, adjust notification behavior, or turn on debug logging.

## Multiplayer

Install the same mod version on the server and clients that should use it. Keep `Config.xml` aligned.

Unfortunately this mod is not crossplay compatible. At this time there is no way to distribute the mod to the console versions of the game and EAC must be disabled.

## Compatibility

See `docs/COMPATIBILITY.md` in the source repository for the current mod-version to game-version table.

## Source and contributions

Source, development notes, and issue tracking are available on GitHub:

[https://github.com/Odenata/7dtd-auto-read-mod](https://github.com/Odenata/7dtd-auto-read-mod)
