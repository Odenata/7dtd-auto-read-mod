# Known issues

Issues we accept for now; tracked here so players and maintainers are not surprised.

## Magazine skill notification can show an intermediate level

When several copies of the **same** magazine are consumed in one auto-read pass (for example from opening a bundle), the vanilla UI text that shows crafting skill progress—often something like **`[current]/[max]`**—may reflect the skill **after the first** magazine only, not the level **after every** copy was applied.

**Actual progression** matches the number of magazines consumed; skill grants are not lost. Only the **notification string** can be misleading on multi-copy reads.

Not important enough to fix right now.
