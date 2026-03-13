# GameApiExporter (7 Days to Die)

GameApiExporter is a temporary in-game exporter used to extract the public game API from a live 7 Days to Die process and turn it into a structured markdown reference.

## Goals

- Provide a small, understandable example of a **7 Days to Die** C# mod that reflects game assemblies at runtime.
- Keep the build/deploy workflow reproducible and portable.
- Maintain a repeatable runtime extraction workflow for regenerating the API reference.

## Non-goals

- Guarantee full hermeticity for *deployment* (it necessarily depends on a local game install).
- Act as a gameplay mod that stays installed during normal play.
- Provide a stable public API of its own.

## References

- [Fandom Wiki ModAPI](https://7daystodie.fandom.com/wiki/ModAPI)
- [Unofficial Modding Discord forum topic](https://community.thefunpimps.com/threads/unnofficial-modding-discord.23400/)
- [Immersive Crosshair example mod](https://github.com/rdok/7dtd_immersive_crosshair)

