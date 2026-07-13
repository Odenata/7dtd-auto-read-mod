# Known issues

Issues we accept for now; tracked here so players and maintainers are not surprised.

## Resolved: entity bags and non-static loot containers missed (fixed in 1.0.1)

**1.0.0** only hooked `XUiC_LootWindow.SetTileEntityChest` — tile-entity chests (world POI loot). Entity bags, dropped backpacks, and other non-static loot UIs were not auto-read.

**Fix in 1.0.1:** additional hook on `XUiC_BagContainer.SetBag` covers entity bag / non-static loot paths. Use **1.0.1+** on official stable **3.0.0 (b259)**; **1.0.0** remains experimental-b252 only.

## Resolved: INT Mastery Photographic Memory on loot auto-read (fixed in 1.0.2)

**Before 1.0.2**, INT Mastery Photographic Memory could miss its bonus skill-point roll on the loot-container synthetic path when only `$perkIntellectMasteryPointChance` was set (no Nerd Outfit / combined CVar).

**Fix in 1.0.2:** magazine double-point rolls use `$nerdOutfitSkillPointChance` when present, otherwise fall back to `$perkIntellectMasteryPointChance` for INT Mastery ≥ 3 (with an eligibility gate matching vanilla).

Bonus skill points are still not shown separately in the auto-read notification (see below).

## Maxed magazines / already-read books (opt-in skip in 1.0.2)

By default (`SkipAlreadyRead=false`), auto-read still consumes readables that grant no benefit — same as earlier releases.

Set `<SkipAlreadyRead>true</SkipAlreadyRead>` in `Config.xml` (or the Gears World Behavior switch) to stop consuming units once magazines are at max crafting skill, books/perks are already unlocked, or schematics/recipes are already known. Partial stacks leave the remainder in the container.

**1.0.2 detection notes:** most vanilla schematics unlock via CVar (not `CraftingManager`); books use perk max / level ≥ 1 when max cannot be resolved. Restart the game after changing `Config.xml` by hand; Gears UI changes apply without restart.

## Magazine skill notification can show an intermediate level

When several copies of the **same** magazine are consumed in one auto-read pass (for example from opening a bundle), the vanilla UI text that shows crafting skill progress—often something like **`[current]/[max]`**—may reflect the skill **after the first** magazine only, not the level **after every** copy was applied.

**Actual progression** matches the number of magazines consumed; skill grants are not lost. Only the **notification string** can be misleading on multi-copy reads.

Not important enough to fix right now.

## Nerd Outfit / INT Mastery extra skill points are not visualized

When a **Nerd Outfit** or **INT Mastery Photographic Memory** bonus triggers during auto-read, the player can gain an extra crafting skill point beyond the magazine's normal point.

**Actual progression** includes the extra point. The current auto-read notification only reports the readable item consumption, not the separate bonus grant.

Nice-to-have follow-up: add a dedicated notification or decorate the existing notification when the bonus roll grants an extra point.

## Resolved: loot open spammed `InvalidCastException` / toolbelt XUi errors (fixed in mod)

Opening a loot container with readables could flood the log with **`InvalidCastException: Specified cast is not valid`** from **`ItemActionEat.OnHoldingUpdate`** / **`IsActionRunning`** and **`[XUi] Error while updating window group 'toolbelt'`**.

**Cause:** loot slot indices (0…N) were passed as **`ItemInventoryData.slotIdx`** for eat / min-event paths. The game treated them as **player inventory** indices and left **`ItemActionEat`** action data inconsistent on those slots.

**Fix:** use **`slotIdx = -1`** for eat/consume/skill **`ItemInventoryData`** whenever reading from open loot; keep the real index only for resolving the loot **`XUiC_ItemStack`** controller. **Recipe unlock:** invoke **`ItemActionLearnRecipe.ExecuteAction`** after consume, and if needed **`CraftingManager.UnlockRecipe`** using **`RecipesToLearn`**, **`MoldTarget`**, and name heuristics.
