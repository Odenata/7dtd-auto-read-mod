# Development Status

**Last Updated:** 2026-03-13

---

## What Works

- **Mod loads successfully** — ModApi init, Harmony patches apply
- **Container-open patch** — Triggers when player opens a loot container (XUiC_LootWindowGroup)
- **Readable item identification** — Books, magazines, schematics, recipes identified by name patterns (see `ItemIdentificationTests`)
- **Config filtering** — ReadBooks, ReadMagazines, ReadSchematics toggles work
- **Item consumption** — `ItemActionEat.consume()` is invoked to apply reading effects (buffs, recipe learning)
- **Item removal** — Items are removed from container after consumption
- **Stacked items** — Each item in a stack is consumed in turn

---

## Try-Catch Workaround (Needs Healthier Solution)

**Location:** [src/AutoReader.cs](../src/AutoReader.cs) lines 152–172

The inner loop around `eatAction.consume(actionData)` catches exceptions and still increments `successfulConsumes++` with the comment "effects might have been applied before the error". This:

- Masks partial failures
- Makes debugging harder
- Can lead to inconsistent state (e.g., effects applied but item not removed, or vice versa)

**Exploration in progress:** [src/ApiExperiments.cs](../src/ApiExperiments.cs) tests alternative APIs:

- `ItemClass.ExecuteAction()`
- `ItemClass.Interact()`
- Other approaches

Enable `ExperimentalApiTesting` in config to run API tests on the first readable item instead of normal processing. Check logs for results.

---

## Other Try-Catch Usage (Acceptable)

- **SafeLog** — try-catch for Unity not available in test environment
- **Config loading** — file-system errors
- **ModApi init** — startup errors
