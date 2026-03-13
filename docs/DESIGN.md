# Design

## Core idea

When the mod initializes inside the live game process, `GameApiExporter` reflects the loaded game assemblies and writes a structured export to disk. The dev-tools repo then converts that export into the published markdown reference.

## Runtime behavior

- **Mod startup**: run the exporter once during initialization.
- **Assembly selection**: export the configured public surface from `Assembly-CSharp` and `Assembly-CSharp-firstpass`.
- **Disk output**: write deterministic tab-separated export files to the `LocalLow` mod area for later markdown generation.

## Configuration

Configuration is expected to be controlled via a mod-local config file (see `src/Config.xml`) and/or in-mod defaults. The mod should allow:

- Enable/disable overall behavior
- Enable/disable runtime export on startup
- Select which assemblies to export
- Choose the export output directory name

## Testing strategy

- **Unit tests**: keep basic script and configuration checks in place where useful.
- **Integration smoke test**: deploy to a local game install, verify the exporter runs, and confirm the runtime export files are produced.

