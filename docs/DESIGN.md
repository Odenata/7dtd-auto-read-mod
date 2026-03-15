# Design

## Core Idea

AutoReadMod uses Harmony patches to detect when readable items become available from supported sources:

- opening a loot container
- opening a bundle item from inventory

The mod identifies readable items (books, magazines, schematics, recipes), applies the item's read effects to the player, and removes only the successfully consumed count.

## Runtime Behavior

- **Container open**: Harmony patch on `XUiC_LootWindowGroup` (or equivalent) runs when the loot UI is shown.
- **Bundle open**: Harmony postfixes on `ItemActionOpenBundle.ExecuteInstantAction(...)` and `ItemActionOpenLootBundle.ExecuteInstantAction(...)` snapshot the player's inventory before the bundle opens, then inspect newly added items after the game populates the bundle results.
- **Item scan**: Identify readable items by name patterns:
  - books: names starting with `book`
  - magazines: names containing `magazine`
  - schematics: names ending with `schematic`
  - recipes: names ending with `recipe`
  - bundle items themselves are excluded from readable-item detection
- **Consumption**: For each readable item, find `ItemActionEat` on the item's actions, create `ItemInventoryData`, and fire `ItemClass.FireEvent(MinEventTypes.onSelfPrimaryActionEnd, ...)` once per consumed item.
- **Removal**:
  - loot containers: update the container slot after successful consumes
  - bundle outputs in player inventory: update only the added quantity so existing stacked items remain intact
- **Visualization**: When items are auto-read, the mod enqueues events and renders them in the game’s collected-item notification list (same area as loot pickups). Type-based label color comes from config (hex per kind: book, magazine, recipe, schematic). Entries persist for a configured duration; color and “(0)” strip are re-applied each frame for a short window so they stick if the game rebinds. For containers the list is append (newest at end); for bundle opens the list is prepend (newest at index 0), so we track bundle entries by index for re-apply.

## Configuration

See `src/Config.xml` and `ModConfig.cs`:

- **Enabled** — Enable/disable overall behavior
- **ReadBooks** — Include books
- **ReadMagazines** — Include magazines
- **ReadSchematics** — Include schematics and recipes
- **DebugMode** — Verbose logging
- **Visualization**: Enabled, duration, max visible, and per-kind colors (ColorBook, ColorMagazine, ColorSchematic, and ColorRecipe (which is the base/default) as hex in Config.xml)

## Testing Strategy

- **Hermetic Bazel unit/tool tests**: Mock-based tests run through native Bazel `net_xunit_test` targets with pinned external dependencies from `MODULE.bazel`.
- **IDE/MSBuild compatibility**: The `.csproj` files remain available for editor support and optional local `dotnet test`, but they are no longer the source of truth for the hermetic test graph.
- **Bundle/unit coverage**: Unit tests exercise bundle-output classification, added-inventory detection, stacked bundle results, and config gating against production helpers.
- **Integration smoke test**: Deploy to local game install, verify the mod loads successfully and the Harmony patches apply cleanly.
