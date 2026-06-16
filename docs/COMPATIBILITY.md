# Game version compatibility

Use this table to see which **7 Days to Die** builds work with which **mod** version. Update it when maintainers verify a combination or when a game update breaks the mod.

| Mod version | 7 Days to Die (tested / expected) | Notes |
|-------------|-----------------------------------|-------|
| 0.1.0       | 2.6                               | Initial release for Alpha 21 / game 2.6. **Not compatible with 3.0.** |
| 1.0.0       | 3.0.0 (b252) experimental         | **Breaking release for v3.0 only** — Harmony targets, loot APIs, and UI hooks changed; do not install on 2.6. Loot hook on `XUiC_LootWindow.SetTileEntityChest`; `ITileEntityLootable` write-back via reflection; post-read UI sync via `SetSlots` + slot-controller refresh and `OnOpen` pending refresh. Maintainer smoke PASS 2026-06-16. |

When you publish a release, add or adjust a row if the supported game range changed.
