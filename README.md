# AutoReadMod (7 Days to Die)

AutoReadMod automatically consumes **readable loot** (magazines, books, recipes) when you encounter it in containers.

- **Project overview**: `docs/PROJECT_OVERVIEW.md`
- **Design notes**: `docs/DESIGN.md`

## Prerequisites

- **.NET SDK** (for `dotnet build` / `dotnet test`)
- **.NET Framework 4.7.2 Developer Pack** — the mod targets `net472`; the SDK alone is not enough.  
  Install: [.NET Framework 4.7.2 Developer Pack](https://dotnet.microsoft.com/download/dotnet-framework/net472) (choose “Developer Pack”).  
  If you skip this, the build will fail with MSB3644; `.\tools\build.ps1` will print the same install link.
- A local **7 Days to Die** install (only needed for deploy/integration testing)

Optional:

- Bazel (experimental / in-progress .NET support)

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

- `SEVEN_DTD_INSTALL_DIR="C:\Program Files (x86)\Steam\steamapps\common\7 Days To Die"`

Then:

```powershell
.\tools\deploy.ps1 -Configuration Release
.\tools\test-integration.ps1 -TimeoutSeconds 60
```

