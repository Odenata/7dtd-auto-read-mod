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
  - schematic and recipe are checked before magazine so names like `gunMagazineSchematic` classify as schematic, not magazine
- **Consumption**: For each readable stack, find `ItemActionEat`. Prefer the vanilla read path: build `ItemInventoryData` and `ItemActionData` (via `CreateModifierData` or ctor), then call `consume(actionData)` once per unit — that path applies schematics, perks, and recipe unlocks. **Fallback** (e.g. odd builds): per unit call `ExecuteInstantAction(player, stack, isHeldItem: false, null)`; vanilla does not fire `onSelfPrimaryActionEnd` when `stackController` is null, so the mod fires it after each successful instant action. `successfulConsumes` increments when the stack count drops or `ExecuteInstantAction` returns true (legacy only). **Loot UI reads** still resolve `XUiC_ItemStack` by the loot slot index, but `ItemInventoryData.slotIdx` for eat/consume/GainSkill/LearnRecipe uses **`-1`** so loot indices are never treated as player toolbelt slots (avoids `InvalidCastException` spam). **`ItemActionLearnRecipe`** runs after a successful eat/consume when present (vanilla schematic layout: Eat + LearnRecipe). **Schematic/recipe loot:** if recipes still do not unlock, call **`CraftingManager.UnlockRecipe`** using names from **`RecipesToLearn`**, **`ItemClass.MoldTarget`**, and class-name heuristics (strip `schematic` / `recipe` suffix).
- **Bundle open**: If `ExecuteInstantAction` returns false (e.g. loot bundle failure), auto-read of new bundle outputs is skipped.
- **Empty loot on open**: Wait up to ~180 frames for slots to populate, then auto-read once when items appear (covers timer-delayed `OpenContainer`).
- **Removal**:
  - loot containers: clear slot or keep same stack reference after in-place count changes
  - bundle outputs in player inventory: update only the added quantity so existing stacked items remain intact
- **Visualization**: Container reads enqueue full rows (`AddItemStack` + type color + strip trailing `(N)`). **Bundle reads**: vanilla already shows loot rows; mod only enqueues **decorate-only** events (match `CollectedItemList+Data` by `ItemStack` / icon, color + strip `(N)`). See [FUTURE_WORK.md](FUTURE_WORK.md) for deferred ideas.

## Configuration

See `src/Config.xml`, `src/ModSettings.xml`, and `ModConfig.cs`:

- **Enabled** — Enable/disable overall behavior (**Gears World** when present)
- **SkipAlreadyRead** — Leave no-benefit units in loot (**Gears World**)
- **ItemTypes/Books|Magazines|Schematics** — Include those kinds (**Gears World**)
- **DebugMode** — Verbose logging (**Gears Global / client**)
- **Visualization**: Enabled, max visible, per-kind colors (**Gears Global / client**)

Soft-dep: ship `GearsAPI.dll`; `GearsModApi` (`IGearsModApi`) applies settings live. Without Gears, Config.xml alone is used. World CurrentValues are re-synced on loot processing because GearsAPI has no World `OnSettingChanged`.

## Testing Strategy

- **Hermetic Bazel unit/tool tests**: Mock-based tests run through native Bazel `net_xunit_test` targets with pinned external dependencies from `MODULE.bazel`.
- **IDE/MSBuild compatibility**: The `.csproj` files remain available for editor support and optional local `dotnet test`, but they are no longer the source of truth for the hermetic test graph.
- **Bundle/unit coverage**: Unit tests exercise bundle-output classification, `wasSuccessful` gating, deferred loot timing, `ItemActionEat.consume` / `ExecuteInstantAction` contracts, success counting, added-inventory detection, stacked bundle results, and config gating.
- **Integration smoke test**: Deploy to local game install, verify the mod loads successfully and the Harmony patches apply cleanly.
