# Development Status

**Last Updated:** 2026-03-13

---

## What Works

- **Mod loads successfully** — ModApi init, Harmony patches apply
- **Container-open patch** — Triggers when player opens a loot container (XUiC_LootWindowGroup)
- **Readable item identification** — Books, magazines, schematics, recipes identified by name patterns (see `ItemIdentificationTests`)
- **Config filtering** — ReadBooks, ReadMagazines, ReadSchematics toggles work
- **Effect application** — `ItemClass.FireEvent(MinEventTypes.onSelfPrimaryActionEnd, ...)` runs the item's effect_groups (AddProgressionLevel, GiveExp, etc.) directly
- **Item removal** — Items are removed from container after effects are applied
- **Stacked items** — Each item in a stack is processed in turn
- **No inventory dependency** — Works regardless of player inventory space; items stay in container until consumed

---

## Implementation Notes

**Location:** [src/AutoReader.cs](../src/AutoReader.cs)

We use `ItemClass.FireEvent(MinEventTypes.onSelfPrimaryActionEnd, evtParams)` rather than `ItemActionEat.consume()`. This fires the same event the game uses when the player reads an item from a container, so effect_groups (AddProgressionLevel, GiveExp, ModifyCVar, etc.) run correctly. We then remove the item from the container ourselves. This approach does not require moving items to the player inventory, avoiding inventory-space issues.

**API exploration:** [src/ApiExperiments.cs](../src/ApiExperiments.cs) tests alternative APIs (`ItemClass.Interact()`, `ItemActionEat.ExecuteAction()`, etc.). Enable `ExperimentalApiTesting` in config to run API tests on the first readable item. Check logs for results.

---

## Other Try-Catch Usage (Acceptable)

- **SafeLog** — try-catch for Unity not available in test environment
- **Config loading** — file-system errors
- **ModApi init** — startup errors
