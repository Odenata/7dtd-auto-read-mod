# Design

## Core Idea

AutoReadMod uses a Harmony patch on the loot window to detect when the player opens a container. It scans the container for readable items (books, magazines, schematics, recipes), invokes `ItemActionEat.consume()` for each to apply effects, then removes them from the container.

## Runtime Behavior

- **Container open**: Harmony patch on `XUiC_LootWindowGroup` (or equivalent) runs when the loot UI is shown.
- **Item scan**: Iterate container slots, identify readable items by name patterns (book*, *magazine*, *schematic, *recipe; exclude *bundle*).
- **Consumption**: For each readable item, find `ItemActionEat` on the item's actions, create `ItemInventoryData` and `ItemActionData`, call `consume()`.
- **Removal**: After consumption, clear the slot via `container.UpdateSlot(slotIndex, ItemStack.Empty)`.

## Configuration

See `src/Config.xml` and `ModConfig.cs`:

- **Enabled** — Enable/disable overall behavior
- **ReadBooks** — Include books
- **ReadMagazines** — Include magazines
- **ReadSchematics** — Include schematics and recipes
- **DebugMode** — Verbose logging

## Testing Strategy

- **Hermetic Bazel unit/tool tests**: Mock-based tests run through native Bazel `net_xunit_test` targets with pinned external dependencies from `MODULE.bazel`.
- **IDE/MSBuild compatibility**: The `.csproj` files remain available for editor support and optional local `dotnet test`, but they are no longer the source of truth for the hermetic test graph.
- **Integration smoke test**: Deploy to local game install, verify mod loads and auto-read works.
