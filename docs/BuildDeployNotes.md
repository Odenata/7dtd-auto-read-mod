# Build and deploy notes

Open items are tracked in [docs/TODO.md](TODO.md).

## Known issue: PowerShell build/deploy environment

Build or deploy may fail when run from the user’s local PowerShell due to environment differences (e.g. DLL locked by another process, or path/execution policy issues). Until that is fixed:

- **Workaround:** Build from this repo (e.g. Cursor/IDE terminal or a separate PowerShell that doesn’t lock the DLL). If `dotnet build` fails with “file is being used by another process”, the compiled DLL is still in `src\obj\Release\net472\`; you can deploy by copying from there (see below).
## Visualization – count text

We remove the "(0)" (inventory total) from the notification and keep "+1" (or "+2", etc.) by: (1) stripping a trailing parenthetical `\s*\(\d+\)\s*$` from label text, and (2) clearing any label whose text is only `(digits)`. We re-apply this strip each frame for our entries (in the same pass as color re-apply) so if the game formatter overwrites the text we strip again. With DebugMode enabled, we log "Count text strip: before '...' after '...'" (throttled) when a change is made.

## Visualization – background hue

Setting the notification background UISprite’s `color` makes the bar darker/more opaque but type-based hue (amber vs blue, etc.) does not show in-game; the game likely uses only alpha or a single tint for that element. We also tint the **icon** sprite by type so book vs magazine etc. can be distinguished by icon color where the engine respects it. We set **UILabel color** by type (book = amber, magazine = blue, etc.) so the notification text is distinct when sprite hue is not visible.

## Deploy from obj when bin is locked

If the build compiles but fails copying to `bin\Release\net472\`:

1. Copy `src\obj\Release\net472\AutoReadMod.dll` to the game’s `Mods\AutoReadMod\` folder.
2. Copy `0Harmony.dll` (from 7dtd-mod-dev-tools third_party), `ModInfo.xml`, and `Config.xml` from the mod repo into the same folder.
