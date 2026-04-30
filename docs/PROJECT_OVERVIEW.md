# AutoReadMod (7 Days to Die)

AutoReadMod automatically reads books, magazines, schematics, and recipes when you open a loot container. No need to manually consume each item.

## Goals

- Auto-read readable items on container open with configurable filtering.
- Keep the build/deploy workflow reproducible and portable.
- Keep hermetic unit/tool tests Bazel-owned and mock-based.

## Maintainer docs

- [`DEVELOPING.md`](DEVELOPING.md): build, test, deploy, and integration smoke workflow.
- [`PUBLISHING.md`](PUBLISHING.md): release checklist and zip layout.
- [`COMPATIBILITY.md`](COMPATIBILITY.md): mod version vs game version table.
- [`DESIGN.md`](DESIGN.md): runtime behavior and test strategy.

## Build requirements

- **Windows:** For `bazel build //...` or `bazel test ...` to succeed, Bazel’s test infrastructure needs a shell toolchain. Set **`BAZEL_SH`** to the path of `bash.exe` before running Bazel, e.g. for Git for Windows:

  ```powershell
  $env:BAZEL_SH = "C:\Program Files\Git\bin\bash.exe"
  ```

  (Use your actual Git Bash path if different; WSL or MSYS2 `bash` on PATH also work.) Builds that do not include test targets (e.g. `bazel build //src:all //tools:build`) do not require `BAZEL_SH`. If you set `BAZEL_SH` in a session where Bazel has already been run, run `bazel shutdown` first so the next build starts a new server that sees the variable.

## Non-Goals

- Guarantee full hermeticity for *deployment* (it necessarily depends on a local game install).
- Provide a stable public API of its own.
