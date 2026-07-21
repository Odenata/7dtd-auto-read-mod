# Game version compatibility

Use this table to see which **7 Days to Die** builds work with which **mod** version. Update it when maintainers verify a combination or when a game update breaks the mod. Player-facing release history: [`../CHANGELOG.md`](../CHANGELOG.md).



| Mod version | 7 Days to Die (tested / expected) | Notes |

|-------------|-----------------------------------|-------|

| 0.1.0       | 2.6                               | Initial release for Alpha 21 / game 2.6. **Not compatible with 3.0.** |

| 1.0.0       | 3.0.0 (b252) experimental         | **Breaking release for v3.0 only** — targeted experimental b252; Harmony targets, loot APIs, and UI hooks changed; do not install on 2.6. Loot hook on `XUiC_LootWindow.SetTileEntityChest` (tile-entity chests only); `ITileEntityLootable` write-back via reflection; post-read UI sync via `SetSlots` + slot-controller refresh and `OnOpen` pending refresh. Maintainer smoke PASS 2026-06-16. |

| 1.0.1       | 3.0.0 (b259) stable               | Verified against official stable (Steam). Adds entity bag / non-static loot via `XUiC_BagContainer.SetBag`. Install requires EAC off. Server and clients need the same mod version. |
| 1.0.2       | 3.0.0 (b259) stable               | INT Mastery Photographic Memory loot double-point fix; opt-in `<SkipAlreadyRead>` (default false). Same bag/loot hooks as 1.0.1. |
| 1.1.0       | 3.0.0 (b259) stable               | No longer ships `0Harmony.dll`. Requires the game's official `Mods\0_TFP_Harmony`. Install folder is DLL + ModInfo + Config only. |
| 1.2.0       | 3.0.0 (b259) stable               | Optional Gears settings UI; HarmonyX compile alignment. Same install layout as 1.1.0. |
| 1.2.1       | 3.0.0 (b259) stable               | Loot book-set Complete perk unlock; loot magazines honor sandbox Magazine Progress (`PointsPerMagazine`) for base and Nerd/INT bonus grants. |
| 1.2.2       | 3.0.0 (b259) stable               | Fix Magazine Progress read (static `GetInt`); client-configurable read / book-set-complete audio. |
| 1.2.3       | 3.0.0 (b259) stable               | Auto-read audio cues actually play via `Audio.Manager.Play`. |
| 1.3.0       | 3.0.0 (b259) stable               | Optional World **Read When Taken** (`ReadOnInventoryAdd`): auto-read on inventory ingress instead of open loot. |
| 1.3.1       | 3.0.0 (b259) stable               | Dedicated server InitMod no longer crashes when `Assembly.Location` is empty (`Mod.Path`). |
| 1.4.0       | 3.0.0 (b259) stable               | Client **Show Read Effects** tooltips for newly unlocked books / Complete / schematics / recipes. |






When you publish a release, add or adjust a row if the supported game range changed.

