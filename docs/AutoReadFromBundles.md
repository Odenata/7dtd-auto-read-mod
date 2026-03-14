# AutoRead from Bundles

Implemented behavior:

- When a player opens bundle items backed by `ItemActionOpenBundle` or `ItemActionOpenLootBundle`, AutoReadMod snapshots the player inventory before the bundle opens and inspects the added contents afterward.
- Newly created readable items are auto-read with the same effect path used for container auto-read: verify `ItemActionEat`, fire `onSelfPrimaryActionEnd`, and remove only the successfully consumed count.
- Bundle items themselves are still not treated as readable items. Only their generated contents are considered.
- If bundle results stack onto an existing readable stack, only the newly added quantity is consumed so pre-existing items are left alone.
- Existing config flags still apply to bundle outputs:
  - `ReadBooks`
  - `ReadMagazines`
  - `ReadSchematics`

Verification completed:

- Automated unit tests cover readable-item classification, bundle-output detection, config gating, and stacked bundle-output consumption.
- Local integration smoke test confirmed the mod builds, deploys, and loads in the game.
