# 7 Days to Die Mod Attempt

## Goals

- Learn how to set up a repository that can build and test a 7 Days to Die mod
- Learn the C# programming language to a beginner level, enough to write the mod
- Learn how to produce DLL files and deploy them to the 7 Days to Die game directory for integration testing
- Learn how to package the files for distribution
- Produce a mod from this project which can be used as an example to set up other projects for 7 Days to Die mods

## Example Mod Idea

This mod will force the player to automatically read the magazines, books, and recipes that they encounter in containers.

By doing this, players can't wait to use these resources until they have boost gear at their base and they can't share these resources with other players.

## Modding References

- [Fandom Wiki ModAPI](https://7daystodie.fandom.com/wiki/ModAPI)
- [Unofficial Modding Discord Forum Topic](https://community.thefunpimps.com/threads/unnofficial-modding-discord.23400/)
- [Immersive Crosshair Mod Example Repo](https://github.com/rdok/7dtd_immersive_crosshair) mentioned from [forum](https://forums.nexusmods.com/topic/13495841-developing-mods-for-7-days-to-die-using-harmony/)
- [Server Tools Example Repo](https://bitbucket.org/obsessive-coder/sevendaystodie-servertools/src/core/)

## Repo Tools

This project shall live in the `learning-grounds` repository under `seven_days_to_die_mod_attempt` and shall be hermetic, using Bazel as the build and test tool. It shall use SonarQube Community Edition to enforce no-slip code coverage with a 100% target. It shall use semantic versioning and keep track of compatibility with the different game versions.

## Instructions for building this project

### Set up project

Create the appropriate top-level files in the `seven_days_to_die_mod_attempt` directory to set up Bazel, SonarQube, and versioning as described in the [Repo Tools](#repo-tools) section above.

### Identify game code that can be post-fixed to modify the game behavior

We will need to identify the C# classes from the 7 Days to Die game files which we can modify. I believe this is done through post-fixing, but I have no experience with C# so I am learning some of this through the forums and repositories linked in the [Modding References](#modding-references) section above. I haven't found a true public API for modding the game. The game is installed at `C:\Program Files (x86)\Steam\steamapps\common\7 Days To Die`, from which we should find and read the current game version to establish semantic version compatibility and we should treat these files as read-only. We will only write to the `mods` directory when we want to deploy the mod for integration testing.

### Create post-fix for opening in-game container inventories to which we can hook auto-reading

We will need to identify the game code that is responsible for opening in-game inventories. We will then create a post-fix that allows us to hook into this behavior and trigger our auto-reading functionality. If there is already logic from the game that allows adding a callback to this action, we should use it.

Add comprehensive unit tests.

### Create auto-reading functionality

We will need to create the logic that automatically reads magazines, books, and recipes when the player encounters them in containers. This may involve simulating the reading action in some way.

Add comprehensive unit tests.

### Create tool for generating and validating DLL files

We will need to create a tool that can generate DLL files from our C# code and validate that they are correctly formatted for use in the 7 Days to Die game. This tool should also handle deploying the DLL files to the appropriate directory for integration testing.

Add comprehensive unit tests.

### Create packaging script for distribution

We will need to create a script that can package the necessary files for distribution as a mod. This should include the DLL files and any other resources needed for the mod to function.

Add comprehensive unit tests.