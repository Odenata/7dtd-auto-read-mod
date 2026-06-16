# Publishing

This document is for maintainers publishing release files to CurseForge or GitHub Releases.

The normal workflow is manual: build the mod, stage the deployable files, create a zip, upload it through the project dashboard, and paste the listing/changelog text. Keep generated zips out of git.

## Source files

- CurseForge description source: [`CURSEFORGE_DESCRIPTION.md`](CURSEFORGE_DESCRIPTION.md)
- Player README: [`../README.md`](../README.md)
- Current release version: [`../VERSION`](../VERSION)
- Mod vs game compatibility: [`COMPATIBILITY.md`](COMPATIBILITY.md)

Keep these version fields aligned for each release:

- [`../VERSION`](../VERSION)
- [`../MODULE.bazel`](../MODULE.bazel) `module(version = "...")`
- [`../src/ModInfo.xml`](../src/ModInfo.xml) `<Version value="..." />`

Release zip names use **mod version only** (for example `dist\AutoReadMod-1.0.0.zip`). Update [`COMPATIBILITY.md`](COMPATIBILITY.md) when you confirm support for new game builds or when compatibility changes.

## Release checklist

1. Decide the target 7 Days to Die version(s) supported by the release.
2. Update version fields if this is a new release.
3. Update [`COMPATIBILITY.md`](COMPATIBILITY.md) if the supported game version range changed.
4. Update player-facing notes in [`../README.md`](../README.md) and [`CURSEFORGE_DESCRIPTION.md`](CURSEFORGE_DESCRIPTION.md) if behavior changed.
5. Run verification:

   ```bash
   bazel test //tests:unit
   bazel test //tests:tools
   ```

6. Build the DLL:

   ```bash
   bazel build //src:AutoReadMod
   ```

7. Stage the release files under `prepared_mod_files\AutoReadMod\`:

   ```text
   AutoReadMod/
     0Harmony.dll
     AutoReadMod.dll
     Config.xml
     ModInfo.xml
   ```

8. Create `dist\AutoReadMod-<version>.zip` with one top-level `AutoReadMod\` folder.
9. Inspect the zip before upload.
10. Upload the zip to CurseForge or GitHub Releases.
11. Select the supported 7 Days to Die game version(s), keeping them consistent with `COMPATIBILITY.md`.
12. Add a markdown changelog for the file.
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

A dedicated package script can make the staging and zip steps repeatable once the manual process and project metadata are stable.
