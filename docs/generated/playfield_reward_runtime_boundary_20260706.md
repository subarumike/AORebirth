# Playfield Reward Runtime Boundary - 2026-07-06

## Scope

This checkpoint covers the broad Playfield decomposition audit for quest, mission, and reward runtime seams.

## Audit Finding

Mission state updates and dialogue-driven mission actions are already outside `Playfield` in the Arete quest/dialogue services. Corpse credits, corpse inventory update sequencing, and corpse container packet construction remain coupled to corpse state and packet/stat mutation, so they were not moved in this slice.

The safe orchestration seam was NPC-death reward hook routing:

- quest death observation
- combat XP callback invocation

## Boundary Change

`PlayfieldRewardRuntimeService` now owns the NPC-death reward hook order:

1. observe quest NPC-death progress
2. invoke the existing combat XP callback

`NPCRuntimeService` keeps NPC death lifecycle order and delegates only this reward-hook sequence. `Playfield` still owns the existing XP calculation, stat mutation, reward feedback packet emission, and stat persistence.

## Intentionally Outside This Boundary

The following remain outside this slice:

- XP reward calculation
- quest and mission algorithms
- loot tables and corpse loot selection
- corpse credit timing and credit stat mutation
- inventory algorithms
- packet construction and serialization
- database loading
- combat logic
- NPC/player lifecycle internals

## Behavior

No reward order, packet order, persistence behavior, gameplay behavior, or validation rules were changed.
