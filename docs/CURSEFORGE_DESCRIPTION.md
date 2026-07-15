# Auto Read Mod

Auto Read Mod automatically reads books, magazines, schematics, and recipes when you open a loot container in 7 Days to Die. An optional **Read When Taken** World setting leaves readables in open loot until you take them into inventory (also covers ground pickups).

## Features

- Automatically consumes readable loot when containers open (default).
- Optional **Read When Taken**: auto-read on inventory ingress instead of on open loot.
- Supports books, magazines, schematics, and recipes.
- Configurable item-type toggles in `Config.xml`.
- Optional in-game settings via [Gears](https://www.nexusmods.com/7daystodie/mods/4017) + [Quartz](https://www.nexusmods.com/7daystodie/mods/2409) (World-synced behavior; client visualization).

## Installation

1. **Disable Easy Anti-Cheat (EAC).** This is a Harmony DLL mod; EAC must be off.
2. Extract the release zip into your 7 Days to Die `Mods` folder. After extraction, **`Mods\AutoReadMod\`** should contain:

```text
Mods\AutoReadMod\
  AutoReadMod.dll
  GearsAPI.dll
  ModInfo.xml
  Config.xml
  ModSettings.xml
  icon.png
  Config\
    Localization.csv
```

Do **not** add `0Harmony.dll` here; the game provides Harmony via **`Mods\0_TFP_Harmony`**.

3. **Optional:** install Gears + Quartz for the in-game Mods settings menu.
4. **Multiplayer:** install the **same mod version** on the server and every client. For synced World behavior settings, install Gears+Quartz on server and clients too.

## Configuration

- **With Gears:** Esc / main menu Mods settings. World Behavior settings sync with the save; Client tab is local (debug + visualization).
- **Without Gears:** edit `Mods\AutoReadMod\Config.xml` (restart after hand-edits).

## Multiplayer

Install the same mod version on the server and clients that should use it. With Gears, keep World settings on the host/server. Without Gears, keep `Config.xml` aligned.

Unfortunately this mod is not crossplay compatible. At this time there is no way to distribute the mod to the console versions of the game and EAC must be disabled.

## Compatibility

See `docs/COMPATIBILITY.md` in the source repository for the current mod-version to game-version table.

## Troubleshooting

If the mod does not load, confirm the folder layout above, that `Mods\0_TFP_Harmony` exists, and that EAC is disabled.
