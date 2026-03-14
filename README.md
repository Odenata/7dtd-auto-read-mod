# AutoReadMod (7 Days to Die)

AutoReadMod forces the player to automatically read books, magazines, schematics, and recipes when they open a loot container.

- **Project overview**: `docs/PROJECT_OVERVIEW.md`
- **Design notes**: `docs/DESIGN.md`
- **Development status**: `docs/DEVELOPMENT_STATUS.md`

## Prerequisites

- **.NET SDK** (for `dotnet build` / `dotnet test`)
- **.NET Framework 4.7.2 Developer Pack** — the mod targets `net472`; the SDK alone is not enough.
  Install: [.NET Framework 4.7.2 Developer Pack](https://dotnet.microsoft.com/download/dotnet-framework/net472) (choose "Developer Pack").
  If you skip this, the build will fail with MSB3644; `.\tools\build.ps1` will print the same install link.
- A local **7 Days to Die** install (only needed for deploy/integration testing)

Optional:

- Bazel (experimental / in-progress .NET support)

## Dependencies

This mod depends on [7dtd-mod-dev-tools](../7dtd-mod-dev-tools) (sibling checkout recommended):

- **Build/deploy**: `tools/build.ps1`, `deploy.ps1`, `test-integration.ps1` delegate to dev-tools
- **Mocks**: `Assembly-CSharp-Mock`, `UnityEngine-CoreModule-Mock` for builds without game install
- **Harmony**: `@seven_days_to_die_mod_dev_tools//third_party/harmony`
- **WORKSPACE**: `local_repository` for dev-tools

## Build

From repo root:

```powershell
.\tools\build.ps1 -Configuration Release
```

## Unit tests

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
