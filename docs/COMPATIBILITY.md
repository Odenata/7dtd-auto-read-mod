# Game version compatibility

Use this table to see which **7 Days to Die** builds work with which **mod** version. Update it when maintainers verify a combination or when a game update breaks the mod.

| Mod version | 7 Days to Die (tested / expected) | Notes |
|-------------|-----------------------------------|-------|
| 0.1.0       | 2.6                               | Initial release. |
| 0.1.0       | 3.0.0 (b252) experimental         | v3.0 compatibility: loot hook on `XUiC_LootWindow.SetTileEntityChest`; `ITileEntityLootable` write-back via reflection (`TryWriteLootItem`, `UpdateSlot`, `bTouched`/`bWasTouched`); post-read UI sync via `SetSlots` + slot-controller refresh and `OnOpen` pending refresh (prevents drag/shift-click dup). Init-verified patch load (zero skips). Maintainer smoke PASS 2026-06-16: instant read, items vanish from loot UI, bundles + Nerd outfit OK. |

When you publish a release, add or adjust a row if the supported game range changed.
