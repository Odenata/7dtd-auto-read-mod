# Nexus reply drafts

Short paste-ready replies for common Auto Read Mod support threads. Edit the version/build line if needed before posting.

---

## Bundled 0Harmony.dll / load errors (1.1.0+)

Thanks for the report. Older Auto Read zips shipped a classic Harmony **2.2.2** `0Harmony.dll`, which differs from the official Fun Pimps **`Mods\0_TFP_Harmony`** (HarmonyX). That mismatch can cause load errors.

**1.1.0+** no longer includes `0Harmony.dll`. Install so `Mods\AutoReadMod\` has only `AutoReadMod.dll`, `ModInfo.xml`, and `Config.xml`. Keep the game’s **`Mods\0_TFP_Harmony`** folder (verify Steam files if it is missing). Delete any leftover `0Harmony.dll` inside `AutoReadMod`.

---

## Bags / backpacks not auto-reading (1.0.0)

Thanks for the report. **1.0.0** only hooked tile-entity chest loot (`XUiC_LootWindow.SetTileEntityChest`), so entity bags, dropped backpacks, and other non-static loot UIs were missed.

**1.0.1+** hooks `XUiC_BagContainer.SetBag` for those paths. Please use **1.1.0** (or at least **1.0.1**) on official stable **3.0.0 (b259)**. EAC must be off, and server + clients need the same mod version.

---

## Crash / mod does not work

A few install checks fix most reports:

1. **EAC off** — Harmony DLL mods require Easy Anti-Cheat disabled.
2. **Folder layout** — after unzip, `Mods\AutoReadMod\` must contain exactly: `AutoReadMod.dll`, `ModInfo.xml`, `Config.xml`. There is **no** nested `Config\` folder and **no** `0Harmony.dll` in this folder (1.1.0+).
3. **Official Harmony** — `Mods\0_TFP_Harmony` must exist (ships with the game). Verify Steam files if missing.
4. **Game + mod version** — use **1.1.0** for official stable **3.0.0 (b259)**.
5. **Multiplayer** — same mod version on server and every client.

If it still fails, enable `DebugMode` in `Config.xml`, reproduce once, and attach the newest `output_log_client__*.txt` from `%AppData%\Roaming\7DaysToDie\logs\`.

---

## Zip / Config folder confusion

The release zip has one top-level folder: `AutoReadMod\`. Extract into your game's `Mods` folder so you get:

```text
Mods\AutoReadMod\
  AutoReadMod.dll
  ModInfo.xml
  Config.xml
```

`Config.xml` sits **next to** the DLL — not inside a `Config\` subfolder. If you see `Mods\AutoReadMod\Config\Config.xml`, move `Config.xml` up one level.

---

## Maxed magazine / already-read book still consumed

By default the mod still consumes maxed magazines and already-read books (same as older versions).

In **1.0.2+**, set `<SkipAlreadyRead>true</SkipAlreadyRead>` in `Config.xml` to leave units that would grant no progression/recipe benefit in the container (partial stacks supported). Default remains `false`. Restart the game after changing the config.

---

## INT Mastery Photographic Memory double point

**Fixed in 1.0.2** on the loot auto-read path: the mod rolls `$nerdOutfitSkillPointChance` when set, and otherwise falls back to `$perkIntellectMasteryPointChance` for INT Mastery Photographic Memory (perk ≥ 3).

Bonus points may still not show as a separate auto-read notification — progression is applied; only the splash text may omit the extra point.

---

## Experimental vs stable

| Mod | Game build | Notes |
|-----|------------|-------|
| 1.0.0 | 3.0.0 **(b252) experimental** | Initial v3 release; tile-entity chest loot only |
| 1.0.1 | 3.0.0 **(b259) stable** | Bag / non-static loot via `XUiC_BagContainer.SetBag` |
| 1.0.2 | 3.0.0 **(b259) stable** | INT Mastery loot double-point fix; opt-in `SkipAlreadyRead` |
| 1.1.0 | 3.0.0 **(b259) stable** | No bundled `0Harmony.dll`; requires official `Mods\0_TFP_Harmony` |

Do not use **1.0.0** on stable **b259** if bags matter — upgrade to **1.1.0**.
