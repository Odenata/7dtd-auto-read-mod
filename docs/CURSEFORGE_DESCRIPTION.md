# Auto Read Mod

Auto Read Mod automatically reads books, magazines, schematics, and recipes when you open a loot container in 7 Days to Die.

## Features

- Automatically consumes readable loot when containers open.
- Supports books, magazines, schematics, and recipes.
- Configurable item-type toggles in `Config.xml`.

## Installation

1. **Disable Easy Anti-Cheat (EAC).** This is a Harmony DLL mod; EAC must be off.
2. Extract the release zip into your 7 Days to Die `Mods` folder. After extraction, **`Mods\AutoReadMod\`** must contain exactly:

```text
Mods\AutoReadMod\
  AutoReadMod.dll
  ModInfo.xml
  Config.xml
```

There is **no** nested `Config\` folder — `Config.xml` sits next to the DLL. Do **not** add `0Harmony.dll` here; the game provides Harmony via **`Mods\0_TFP_Harmony`**.

3. **Multiplayer:** install the **same mod version** on the server and every client.

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
