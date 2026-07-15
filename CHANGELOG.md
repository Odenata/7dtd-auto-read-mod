# Changelog

All notable **AutoReadMod** releases. Draft notes live in [`docs/releases/unreleased.md`](docs/releases/unreleased.md). Game support matrix: [`docs/COMPATIBILITY.md`](docs/COMPATIBILITY.md).

Format: [Keep a Changelog](https://keepachangelog.com/en/1.1.0/).

## [1.3.0] - 2026-07-15

### Added

- **Read When Taken** (World Behavior / `ReadOnInventoryAdd`, default off): leave readables visible in open loot and entity bags; auto-read when they enter your inventory (taking loot, ground pickups, quest/trader rewards, and bundles). Supports shift-click and drag-and-drop into the backpack/toolbelt.

### Fixed

- Inventory-add mode: toolbelt `InvalidCastException` spam when auto-reading backpack stacks (bag slot indices were incorrectly used as hotbar slot indices).
- Inventory-add mode: ground pickups and controller-less transfers now apply book perks, magazine skill / INT bonus rolls, and schematic unlocks (not only consume + sound).

### Notes

- Inventory-add mode still auto-reads bundle contents immediately (bundles have no loot preview UI). Same mode also auto-reads quest/trader/admin grants and ground pickups by design.

## [1.2.3] - 2026-07-14

### Added

- Optional client audio: normal read sounds and book-set Complete cue (`Config.xml` / Gears Client → Audio; both default on).

### Fixed

- Loot auto-read now unlocks a book set’s Complete perk (8th unlock) when the last book in the set is read, and can repair characters stuck at 7/7 without Complete.
- Loot magazine skill grants respect the sandbox **Magazine Progress** (`PointsPerMagazine`) setting for the base grant and for Nerd Outfit / Photographic Memory bonus grants.

### Notes

- Audio uses vanilla one-shot `Audio.Manager.Play(entity, …)` (not `PlayInsidePlayerHead`).
- Magazine Progress is read via static `SandboxOptionManager.GetInt(PointsPerMagazine)`.

## [1.2.0] - 2026-07-13

### Added

- Optional Gears in-game settings (World-synced behavior knobs; client debug/visualization). Ships `GearsAPI.dll` + `ModSettings.xml`; Config.xml remains the fallback without Gears.
- Mod icon (`icon.png`) for the Gears Mods list via ModInfo `<Icon>`.

### Changed

- Compile and MSBuild prefer official HarmonyX (`Mods\0_TFP_Harmony` / vendored HarmonyX 2.13).
- Harmony patches use HarmonyX wrap-safe / optional soft-fail where appropriate; Harmony warnings bridge into Player.log.
- Gears menu labels use `Config/Localization.csv` with readable titles and “when enabled…” tooltips (V3 loads `.csv`, not `.txt`).
- ModInfo author shown as Odenata.

## [1.1.0] - 2026-07-09

### Changed

- No longer ships `0Harmony.dll`. Requires the game's official `Mods\0_TFP_Harmony`.
- Install folder is DLL + ModInfo + Config only.

### Notes

- Target game: 3.0.0 (b259) stable.

## [1.0.2] - 2026-07-09

### Added

- Opt-in `<SkipAlreadyRead>` (default false).

### Fixed

- INT Mastery Photographic Memory loot double-point path.

### Notes

- Same bag/loot hooks as 1.0.1. Target game: 3.0.0 (b259) stable.

## [1.0.1] - 2026-07-09

### Added

- Entity bag / non-static loot via `XUiC_BagContainer.SetBag`.

### Notes

- Verified against official stable (Steam). EAC off. Server and clients need the same mod version. Target game: 3.0.0 (b259) stable.

## [1.0.0] - 2026-06-16

### Changed

- **Breaking release for v3.0 only** — Harmony targets, loot APIs, and UI hooks changed; do not install on 2.6.
- Loot hook on `XUiC_LootWindow.SetTileEntityChest` (tile-entity chests only).

### Notes

- Target game: 3.0.0 (b252) experimental. Maintainer smoke PASS 2026-06-16.

## [0.1.0] - 2026-03-01

### Added

- Initial release for game 2.6.

### Notes

- **Not compatible with 3.0.**
