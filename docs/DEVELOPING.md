# Developer workflow

This document is for maintainers building, testing, deploying, or publishing from source. Player installation and configuration instructions are in the top-level [`README.md`](../README.md).

## Source tree

Sources live at the repository root (`src/`, `tests/`, `tools/`). Do not commit or edit files under a nested `bazel-*` execroot mirror.

## Dependencies

This repo expects `7dtd-mod-dev-tools` as a sibling checkout next to this repo, for example:

```text
repos\
  7dtd-auto-read-mod\
  7dtd-mod-dev-tools\
```

The PowerShell wrappers in `tools/` delegate to that sibling checkout. Bazel fetches pinned external repositories from `MODULE.bazel` for mocks, Harmony, xUnit, and the xUnit runner.

For local MSBuild or `dotnet` paths, the mod targets .NET Framework 4.7.2. `Directory.Build.props` reads:

- `SevenDtdInstallDir`, defaulting from `7_DAYS_TO_DIE_GAME_PATH`.
- `SevenDtdDevToolsDir`, defaulting to the sibling `7dtd-mod-dev-tools` checkout.

## Build

Hermetic Bazel build:

```bash
bazel build //src:AutoReadMod
```

PowerShell fallback using host tooling:

```powershell
.\tools\build.ps1 -Configuration Release
```

The host build writes `src\bin\Release\net472\AutoReadMod.dll`.

## Test

Hermetic Bazel suites:

```bash
bazel test //tests:unit
bazel test //tests:tools
```

Windows Git Bash may require `BAZEL_SH` to point to `bash.exe` for Bazel test infrastructure:

```powershell
$env:BAZEL_SH = "C:\Program Files\Git\bin\bash.exe"
```

If Bazel was already running before setting `BAZEL_SH`, run `bazel shutdown` before the next test.

IDE/editor compatibility path:

```powershell
dotnet test .\tests\AutoReadMod.Tests.csproj -c Release
```

`VisualizationTests` (container/bundle notification queue) are only run through `dotnet test` above. Hermetic Bazel `//tests:unit` omits them because the net472 xUnit console runner does not mirror the same mock/type identity as `dotnet test` for the reflection-heavy consume path.

The Bazel tests intentionally use explicit pinned repositories in `MODULE.bazel` instead of extension-generated `use_repo(...)` imports, so `bazel mod tidy` does not strip required xUnit dependencies.

## Deploy to a local game install

Set the game install directory:

```powershell
$env:7_DAYS_TO_DIE_GAME_PATH = "C:\Program Files (x86)\Steam\steamapps\common\7 Days To Die"
```

Then deploy:

```powershell
.\tools\deploy.ps1 -Configuration Release
```

The deploy wrapper builds by default and copies the mod to:

```text
<game>\Mods\AutoReadMod\
```

## Integration smoke test

After deploy, run:

```powershell
.\tools\test-integration.ps1 -TimeoutSeconds 60
```

The smoke test uses the shared dev-tools integration script and checks for the log pattern:

```text
AutoReadMod loaded successfully
```

This is local host-tool or live-game evidence, not hermetic CI proof.

## Manual playtesting logs

When you start the game yourself (typical Steam launch), client logs are usually rotating files here:

```text
%AppData%\Roaming\7DaysToDie\logs\output_log_client__*.txt
```

Automated or scripted runs may instead write `%AppData%\LocalLow\The Fun Pimps\7 Days To Die\Player.log`. Search for `AutoReadMod` or `[HYP]` when debugging read paths.

## Related docs

- [`PROJECT_OVERVIEW.md`](PROJECT_OVERVIEW.md): project goals and build requirements.
- [`DESIGN.md`](DESIGN.md): runtime behavior and test strategy.
- [`COMPATIBILITY.md`](COMPATIBILITY.md): mod version vs game version table.
- [`PUBLISHING.md`](PUBLISHING.md): release checklist and zip layout.
- [`FUTURE_WORK.md`](FUTURE_WORK.md): deferred ideas and failed experiments worth remembering.
