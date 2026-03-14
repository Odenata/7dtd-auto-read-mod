# AutoReadMod (7 Days to Die)

AutoReadMod automatically reads books, magazines, schematics, and recipes when you open a loot container. No need to manually consume each item.

## Goals

- Auto-read readable items on container open with configurable filtering.
- Keep the build/deploy workflow reproducible and portable.

## Non-Goals

- Guarantee full hermeticity for *deployment* (it necessarily depends on a local game install).
- Provide a stable public API of its own.
- API export functionality (that lives in the sibling 7dtd-mod-dev-tools repo).

## References

- [Fandom Wiki ModAPI](https://7daystodie.fandom.com/wiki/ModAPI)
- [Unofficial Modding Discord forum topic](https://community.thefunpimps.com/threads/unnofficial-modding-discord.23400/)
- [Immersive Crosshair example mod](https://github.com/rdok/7dtd_immersive_crosshair)
