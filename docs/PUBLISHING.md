# Publishing

This document is for maintainers publishing release files to CurseForge or GitHub Releases.

The normal workflow is manual: build the mod, stage the deployable files, create a zip, upload it through the project dashboard, and paste the listing/changelog text. Keep generated zips out of git.

## Source files

- CurseForge description source: [`CURSEFORGE_DESCRIPTION.md`](CURSEFORGE_DESCRIPTION.md)
- Player README: [`../README.md`](../README.md)
- Current release version: [`../VERSION`](../VERSION)
- Mod vs game compatibility: [`COMPATIBILITY.md`](COMPATIBILITY.md)
- Release notes draft / history: [`releases/README.md`](releases/README.md), [`../CHANGELOG.md`](../CHANGELOG.md)

Keep these version fields aligned for each release (or use `cut --bump-version`):

- [`../VERSION`](../VERSION)
- [`../MODULE.bazel`](../MODULE.bazel) `module(version = "...")`
- [`../src/ModInfo.xml`](../src/ModInfo.xml) `<Version value="..." />`

Release zip names use **mod version only** (for example `dist\AutoReadMod-1.1.0.zip`). Update [`COMPATIBILITY.md`](COMPATIBILITY.md) when you confirm support for new game builds or when compatibility changes.

## Release checklist

1. Fill / review [`releases/unreleased.md`](releases/unreleased.md).
2. Preview and cut release notes (bumps version fields when using `--bump-version`):

   ```powershell
   .\tools\release_notes.ps1 preview
   .\tools\release_notes.ps1 cut --version X.Y.Z --bump-version
   ```

3. Decide the target 7 Days to Die version(s) supported by the release.
4. Update [`COMPATIBILITY.md`](COMPATIBILITY.md) if the supported game version range changed.
5. Update player-facing notes in [`../README.md`](../README.md) and [`CURSEFORGE_DESCRIPTION.md`](CURSEFORGE_DESCRIPTION.md) if behavior changed.
6. Run verification:

   ```bash
   bazel test //tests:unit
   bazel test //tests:tools
   ```

7. Build the DLL:

   ```bash
   bazel build //src:AutoReadMod
   ```

8. Create the release zip:

   ```powershell
   .\tools\package_release.ps1 -DllPath .\bazel-bin\src\AutoReadMod.dll
   ```

   Or, without `-DllPath`, the script uses `src\bin\Release\net472\AutoReadMod.dll` after `.\tools\build.ps1 -Configuration Release`.

9. Inspect the zip before upload. It should contain one top-level folder:

   ```text
   AutoReadMod/
     AutoReadMod.dll
     GearsAPI.dll
     Config.xml
     ModSettings.xml
     ModInfo.xml
     icon.png
     Config/
       Localization.csv
   ```

   Do **not** include `0Harmony.dll` or compile-only `InControl.dll`. Players use the game's official `Mods\0_TFP_Harmony`. Optional in-game menu requires Gears + Quartz separately.
10. Upload `dist\AutoReadMod-<version>.zip` to CurseForge or GitHub Releases (see console output for the exact path).
11. Select the supported 7 Days to Die game version(s), keeping them consistent with `COMPATIBILITY.md`.
12. Paste the new top section from [`../CHANGELOG.md`](../CHANGELOG.md) into the CurseForge / GitHub file changelog (do not invent notes at upload time).
13. Paste or update the project description from `CURSEFORGE_DESCRIPTION.md`.
14. Verify the uploaded file after approval by downloading it and checking the zip layout.
15. Create a matching Git tag and GitHub Release if you want GitHub to retain old downloadable versions.

## Archive policy

Do not commit generated release archives. Use:

- `dist/` for local release zips.
- `prepared_mod_files/` for local staging.
- CurseForge files for public CurseForge downloads.
- GitHub Releases for public archived versions.

Old public files can stay available unless a release is broken or unsafe. Prefer archiving over deleting if you need to hide an old file.

## Future automation

Release packaging is handled by [`../tools/package_release.ps1`](../tools/package_release.ps1), which runs [`../tools/prepare_mod.ps1`](../tools/prepare_mod.ps1) and writes `dist\AutoReadMod-<version>.zip`.
