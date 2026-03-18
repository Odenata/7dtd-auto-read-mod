# Future work

## Magazine CollectedItemList label: `+N currentLevel/maxLevel` (e.g. `+2 30/75`)

**Goal:** When auto-reading magazines, show the same crafting-skill tier context vanilla uses in a *separate* notification (`XUiC_CollectedItemList.AddCraftingSkillNotification`), merged into the magazine row as `+2 30/75` (count read + level/max).

**Why it did not work in-game (attempt removed):**

1. **Vanilla split UI:** Loot rows from `AddItemStack` use `+count (inventoryTotal)` on the item row. Skill tier `{Level}/{MaxLevel}` is a different code path (`AddCraftingSkillNotification`) with a different row shape (skill icon). Overwriting the item row’s `UILabel.text` after `AddItemStack` either missed the correct label (nested hierarchy / multiple labels), was overwritten by a later UI refresh, or did not match when the list merged/updated rows.

2. **Skill resolution:** Tier was read post-read via `ItemActionGainSkill.SkillsToGain` (first entry) or `ItemClass.CraftingSkillGroup`, then `player.Progression.GetProgressionValue(name)`. That can fail if progression keys differ from XML skill names, if magazines use a different action stack than assumed, or if the local player reference used at visualization time is not the one whose progression updated.

**Reference implementation (for a future retry):**

| Piece | Approach |
|-------|----------|
| Decompiled | `ItemActionGainSkill` ~169277–169407 (`Skills_to_gain` → `ProgressionValue`); `AddCraftingSkillNotification` ~366850–366890 (`Level/MaxLevel` text); `AddItemStack` ~366997–366999 (`+N (inventory)`). |
| Resolve tier | After reads, `TryGetMagazineSkillTier(player, itemClass)` → first `SkillsToGain` or `CraftingSkillGroup`, then `GetProgressionValue` → `Level`, `ProgressionClass.MaxLevel`. |
| Event | Optional `MagazineSkillLevel` / `MagazineSkillMax` on `AutoReadVisualizationEvent` for `ReadableItemKind.Magazine`. |
| Overlay | After `AddItemStack` or when decorating bundle rows, set label to `+$"{Count} {level}/{max}"` on the correct `UILabel`, or **call `AddCraftingSkillNotification` once** next to the item row instead of mutating text (may need Harmony or API if not public). |
| Tests | Mock `Progression` / `ItemActionGainSkill` on `ItemClass`; unit-test tier resolution; in-game verify with one known magazine. |

**Alternative:** Leave item row as-is and accept duplicate notifications (mod row + vanilla skill row), or suppress vanilla duplicate if identifiable.
