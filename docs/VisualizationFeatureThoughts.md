# Visualization Feature

This document is just my initial thoughts and can be safely removed once the feature is developed.

## Problem Statement

When the player opens a container and the AutoReadMod automatically triggers the reading action, it is not obvious to the player that something has happened. The game does have built-in notification splashes that pop up on the bottom left of the screen; this includes experience increases and, in the case of magazines, an "X / Total". There may also be audio cues for some reading items. These are easy to miss, so it feels less rewarding when reading items are found.

## Proposal

Add a brief splash on screen for each of the items that are automatically read.

### Configurable

This visualization feature should be defaulted On, but individual users may choose to disable it through the mod's config file.

Visualization choices such as the splash locations, sizes, and how long they appear should be configurable. During development I can tune those to a good default value, but users should be able to set their own preferences.

### Visualization

These splashes should use the items' inventory icons since those are recognizable. They could show up next to the container's inventory on the screen. They could fade away after a few seconds (configurable), giving the player enough time to feel they have been rewarded without being too intrusive.
