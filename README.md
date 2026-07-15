# AutoReadMod (7 Days to Die)

AutoReadMod forces the player to automatically read books, magazines, schematics, and recipes when they open a loot container. Optionally, **Read When Taken** leaves loot visible until you take items into inventory (including ground pickups).

Release zip names use the **mod version**. See [`docs/COMPATIBILITY.md`](docs/COMPATIBILITY.md) for tested 7 Days to Die versions and [`CHANGELOG.md`](CHANGELOG.md) for release history. Maintainers: [`docs/releases/README.md`](docs/releases/README.md).

## Install

1. **Disable Easy Anti-Cheat (EAC).** Harmony DLL mods require EAC off for both single-player and multiplayer.
2. Extract the release zip into your 7 Days to Die `Mods` folder so **`Mods\AutoReadMod\`** contains:

```text
<7 Days To Die>\Mods\AutoReadMod\
  AutoReadMod.dll
  GearsAPI.dll
  ModInfo.xml
  Config.xml
  ModSettings.xml
  icon.png
  Config\
    Localization.csv
```

`Config.xml` and `ModSettings.xml` sit next to the DLL. `icon.png` is the Gears/menu mod icon. `Config\Localization.csv` is only for in-game Gears menu labels.

Do **not** add `0Harmony.dll` to this folder. The game already provides Harmony via **`Mods\0_TFP_Harmony`** (shipped with 7 Days to Die). If that folder is missing, verify Steam game files.

If the `Mods` folder does not exist, create it next to `7DaysToDie.exe`.

3. **Optional in-game settings menu:** install [Gears](https://www.nexusmods.com/7daystodie/mods/4017) and [Quartz](https://www.nexusmods.com/7daystodie/mods/2409) (EAC off). Without them, edit `Config.xml` as before.
4. **Multiplayer:** install the **same mod version** on the server and every client that should use it. Mixed versions are unsupported. For synced enforcement settings, also install matching Gears+Quartz on the server and clients.

## Configuration

### In-game (Gears)

With Gears+Quartz installed, open the Mods settings UI (main menu or Esc):

- **World / Behavior** (synced for the save when Gears is on the server): Enabled, Skip already read, Read When Taken, Books, Magazines, Schematics. Host/admin changes apply for the world; clients should not expect to override these.
- **Client** (your machine only): Debug mode and visualization (notifications, max visible, colors).

Hot knobs apply without restarting when changed through Gears. World values also refresh the next time loot is processed.

### Config.xml (always available)

Edit `Mods\AutoReadMod\Config.xml` after installing (or when not using Gears):

- `Enabled`: turns the whole mod on or off without uninstalling.
- `SkipAlreadyRead`: leave already-maxed / already-known readables in loot.
- `ReadOnInventoryAdd`: when true, do not auto-read on open loot; auto-read when readables enter your inventory (take / pickup / quest / trader / bundle).
- `DebugMode`: writes extra troubleshooting logs (client-only intent).
- `ItemTypes`: choose whether books, magazines, and schematics/recipes are auto-read.
- `Visualization`: controls the on-screen notification list and colors (client-only intent).

Restart the game after changing `Config.xml` by hand. Gears-driven changes write back to `Config.xml` as a best-effort mirror.

## Multiplayer

Install the same mod version on the server and every client that should use it. EAC must be off for the server and clients. Unfortunately this means it is not crossplay.

This mod is primarily client-side. **With Gears** on server and clients, World Behavior settings sync with the save. **Without Gears**, keep `Config.xml` aligned across installs for consistent behavior. That is not anti-cheat.

## Update Or Uninstall

To update, replace the old `Mods\AutoReadMod` folder with the new release contents. Preserve your old `Config.xml` / `ModSettings.xml` only after checking whether the new release changed configuration keys.

To uninstall, delete `Mods\AutoReadMod`.

## Known issues

See [`docs/KNOWN_ISSUES.md`](docs/KNOWN_ISSUES.md).

## Troubleshooting

- If the mod does not load, confirm the folder layout above, that **`Mods\0_TFP_Harmony`** exists, and that EAC is disabled. Do not place a second `0Harmony.dll` inside `AutoReadMod`.
- If items are not auto-read, check `Enabled` and the relevant `ItemTypes` entries in `Config.xml` (or Gears World Behavior settings).
- If the in-game Mods menu is missing settings, confirm Gears and Quartz are installed and load early (`00000-Gears`).
- If you need more detail, set `DebugMode` to `true`, reproduce the issue, then inspect the client log for `AutoReadMod` / `[HYP]` lines. For normal manual launches (e.g. Steam), the newest file is usually `%AppData%\Roaming\7DaysToDie\logs\output_log_client__*.txt`. Some scripted or integration launches persist logs under `%AppData%\LocalLow\The Fun Pimps\7 Days To Die\Player.log` instead.

## Maintainers

Build, test, deploy, and publishing workflows live in:

- [`docs/DEVELOPING.md`](docs/DEVELOPING.md)
- [`docs/PUBLISHING.md`](docs/PUBLISHING.md)
- [`docs/releases/README.md`](docs/releases/README.md)
- [`docs/PROJECT_OVERVIEW.md`](docs/PROJECT_OVERVIEW.md)
- [`docs/DESIGN.md`](docs/DESIGN.md)
- [`docs/FUTURE_WORK.md`](docs/FUTURE_WORK.md)
