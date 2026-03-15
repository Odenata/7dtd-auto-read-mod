# AutoReadMod (7 Days to Die)

AutoReadMod automatically reads books, magazines, schematics, and recipes when you open a loot container. No need to manually consume each item.

## Goals

- Auto-read readable items on container open with configurable filtering.
- Keep the build/deploy workflow reproducible and portable.
- Keep hermetic unit/tool tests Bazel-owned and mock-based.

## Non-Goals

- Guarantee full hermeticity for *deployment* (it necessarily depends on a local game install).
- Provide a stable public API of its own.
