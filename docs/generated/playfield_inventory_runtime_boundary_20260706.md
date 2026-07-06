# Playfield Inventory Runtime Boundary - 2026-07-06

## Scope

This checkpoint covers the broad Playfield decomposition audit for inventory, container, bank, and item-use runtime seams.

## Audit Finding

Backpack/container open-close sequencing, bank open/slot selection, item move/add/remove orchestration, item use, and handler-side container routing are already owned by `InventoryContainerRuntimeService`. The remaining safe `Playfield` inventory seams were runtime callback chains where `Playfield` directly called the inventory service for:

- death-respawn weapon visual mesh repair
- corpse-loot unique-item validation
- corpse-loot inventory insertion

## Boundary Change

`Playfield` now routes those callbacks through `PlayfieldRuntimeSystems`, which delegates to the existing `InventoryContainerRuntimeService`. This keeps `Playfield` as lifecycle and packet/world-state owner while keeping item/container orchestration behind the runtime facade.

## Intentionally Outside This Boundary

The following remain outside this slice:

- inventory algorithms and validation rules
- item serialization and packet construction
- database loading and persistence internals
- corpse state and corpse packet emission
- credits logic
- combat logic
- interaction handler internals

## Behavior

No gameplay behavior, packet ordering, persistence behavior, or validation rules were changed.
