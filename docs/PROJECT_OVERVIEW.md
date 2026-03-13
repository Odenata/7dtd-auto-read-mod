# AutoReadMod (7 Days to Die)

AutoReadMod automatically consumes **readable loot** (magazines, books, recipes) when you encounter it in containers, so it can’t be banked for later or traded to other players.

## Goals

- Provide a small, understandable example of a Harmony-based **7 Days to Die** C# mod.
- Keep the build/deploy workflow reproducible and portable.
- Maintain strong automated testing (unit tests + an optional integration smoke test).

## Non-goals

- Guarantee full hermeticity for *deployment* (it necessarily depends on a local game install).
- Provide a stable public API (this mod targets specific game versions and must track changes).

## References

- [Fandom Wiki ModAPI](https://7daystodie.fandom.com/wiki/ModAPI)
- [Unofficial Modding Discord forum topic](https://community.thefunpimps.com/threads/unnofficial-modding-discord.23400/)
- [Immersive Crosshair example mod](https://github.com/rdok/7dtd_immersive_crosshair)

