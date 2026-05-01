# AutoReadMod (7 Days to Die)

AutoReadMod forces the player to automatically read books, magazines, schematics, and recipes when they open a loot container.

Release zip names use the **mod version**. See [`docs/COMPATIBILITY.md`](docs/COMPATIBILITY.md) for tested 7 Days to Die versions.

## Install

1. Turn Easy Anti-Cheat off. Harmony DLL mods require EAC disabled.
2. Extract the release zip into your 7 Days to Die `Mods` folder.
3. Verify this layout:

```text
<7 Days To Die>\Mods\AutoReadMod\
  0Harmony.dll
  AutoReadMod.dll
  Config.xml
  ModInfo.xml
```

If the `Mods` folder does not exist, create it next to `7DaysToDie.exe`.

## Configuration

Edit `Mods\AutoReadMod\Config.xml` after installing.

- `Enabled`: turns the whole mod on or off without uninstalling.
- `DebugMode`: writes extra troubleshooting logs.
- `ItemTypes`: choose whether books, magazines, and schematics/recipes are auto-read.
- `Visualization`: controls the on-screen notification list and colors.

Restart the game after changing the config.

## Multiplayer

Install the same mod version on the server and every client that should use it. EAC must be off for the server and clients. Unfortunately this means it is not crossplay.

This mod is primarily client-side. If a server owner wants consistent behavior, keep `Config.xml` aligned across installs.

## Update Or Uninstall

To update, replace the old `Mods\AutoReadMod` folder with the new release contents. Preserve your old `Config.xml` only after checking whether the new release changed configuration keys.

To uninstall, delete `Mods\AutoReadMod`.

## Troubleshooting

- If the mod does not load, confirm the folder layout above and that EAC is disabled.
- If items are not auto-read, check `Enabled` and the relevant `ItemTypes` entries in `Config.xml`.
- If you need more detail, set `DebugMode` to `true`, reproduce the issue, then inspect the client log for `AutoReadMod` / `[HYP]` lines. For normal manual launches (e.g. Steam), the newest file is usually `%AppData%\Roaming\7DaysToDie\logs\output_log_client__*.txt`. Some scripted or integration launches persist logs under `%AppData%\LocalLow\The Fun Pimps\7 Days To Die\Player.log` instead.

## Maintainers

Build, test, deploy, and publishing workflows live in:

- [`docs/DEVELOPING.md`](docs/DEVELOPING.md)
- [`docs/PUBLISHING.md`](docs/PUBLISHING.md)
- [`docs/PROJECT_OVERVIEW.md`](docs/PROJECT_OVERVIEW.md)
- [`docs/DESIGN.md`](docs/DESIGN.md)
- [`docs/FUTURE_WORK.md`](docs/FUTURE_WORK.md)
