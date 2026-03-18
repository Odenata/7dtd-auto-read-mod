# AutoReadMod (7 Days to Die)

AutoReadMod forces the player to automatically read books, magazines, schematics, and recipes when they open a loot container.

- **Project overview**: `docs/PROJECT_OVERVIEW.md`
- **Design notes**: `docs/DESIGN.md`
- **Future / deferred ideas**: `docs/FUTURE_WORK.md`

## Prerequisites

- **.NET SDK** (for `dotnet build` / `dotnet test`)
- **.NET Framework 4.7.2 Developer Pack** — the mod targets `net472`; the SDK alone is not enough.
  Install: [.NET Framework 4.7.2 Developer Pack](https://dotnet.microsoft.com/download/dotnet-framework/net472) (choose "Developer Pack").
  If you skip this, the build will fail with MSB3644; `.\tools\build.ps1` will print the same install link.
- A local **7 Days to Die** install (only needed for deploy/integration testing)

Optional:

- Bazel for hermetic unit/tool tests

## Dependencies

This mod uses `7dtd-mod-dev-tools` in two different ways:

- **Local build/deploy**: `tools/build.ps1`, `deploy.ps1`, `test-integration.ps1` still delegate to a sibling `../7dtd-mod-dev-tools` checkout
- **Hermetic Bazel tests**: `MODULE.bazel` fetches pinned external repos for mock assemblies, Harmony, the xUnit runner, and xUnit libraries

## Build

From repo root:

```powershell
.\tools\build.ps1 -Configuration Release
```

## Unit tests

Hermetic Bazel suites:

```powershell
bazel test //tests:unit
bazel test //tests:tools
```

Those Bazel tests are intentionally wired through explicit pinned repositories in `MODULE.bazel` instead of extension-generated `use_repo(...)` imports, so `bazel mod tidy` does not strip out required xUnit dependencies.

IDE/editor compatibility path:

```powershell
dotnet test .\tests\AutoReadMod.Tests.csproj -c Release
```

## Deploy (integration smoke testing)

Set the game install dir (recommended):

- `7_DAYS_TO_DIE_GAME_PATH="C:\Program Files (x86)\Steam\steamapps\common\7 Days To Die"`

Then:

```powershell
.\tools\deploy.ps1 -Configuration Release
.\tools\test-integration.ps1 -TimeoutSeconds 60
```
