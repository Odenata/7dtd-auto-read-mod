# Design

## Core idea

When a container inventory is opened, AutoReadMod scans items that are “readable” (magazines, books, recipes) and triggers the same consumption behavior the game uses when the player reads them manually.

## Patch points

- **Container open / loot UI open**: hook the game code path that creates or shows container inventory UI and then post-process the inventory contents.

## Configuration

Configuration is expected to be controlled via a mod-local config file (see `src/Config.xml`) and/or in-mod defaults. The mod should allow:

- Enable/disable overall behavior
- Include/exclude categories (magazines/books/recipes)
- Optional filtering rules (by item name, tags, or progression state)

## Testing strategy

- **Unit tests**: exercise filtering, identification, and “read” dispatch logic using mocks (no live game install).
- **Integration smoke test**: deploy to a local game install and verify the mod loads and logs expected startup messages.

