# Known issues

Issues we accept for now; tracked here so players and maintainers are not surprised.

## Magazine skill notification can show an intermediate level

When several copies of the **same** magazine are consumed in one auto-read pass (for example from opening a bundle), the vanilla UI text that shows crafting skill progress—often something like **`[current]/[max]`**—may reflect the skill **after the first** magazine only, not the level **after every** copy was applied.

**Actual progression** matches the number of magazines consumed; skill grants are not lost. Only the **notification string** can be misleading on multi-copy reads.

Not important enough to fix right now.

## Resolved: loot open spammed `InvalidCastException` / toolbelt XUi errors (fixed in mod)

Opening a loot container with readables could flood the log with **`InvalidCastException: Specified cast is not valid`** from **`ItemActionEat.OnHoldingUpdate`** / **`IsActionRunning`** and **`[XUi] Error while updating window group 'toolbelt'`**.

**Cause:** loot slot indices (0…N) were passed as **`ItemInventoryData.slotIdx`** for eat / min-event paths. The game treated them as **player inventory** indices and left **`ItemActionEat`** action data inconsistent on those slots.

**Fix:** use **`slotIdx = -1`** for eat/consume/skill **`ItemInventoryData`** whenever reading from open loot; keep the real index only for resolving the loot **`XUiC_ItemStack`** controller. **Recipe unlock:** invoke **`ItemActionLearnRecipe.ExecuteAction`** after consume, and if needed **`CraftingManager.UnlockRecipe`** using **`RecipesToLearn`**, **`MoldTarget`**, and name heuristics.
