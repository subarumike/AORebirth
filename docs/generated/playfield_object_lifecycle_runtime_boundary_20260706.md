# Playfield Object Lifecycle Runtime Boundary - 2026-07-06

## Scope

This checkpoint covers the broad Playfield decomposition audit for runtime spawn, despawn, object registration, and object lifecycle seams.

## Audit Finding

NPC spawn, activation, immediate removal, home-state cleanup, dead-NPC processing, and corpse despawn timing were already routed through `NPCRuntimeService` and `PlayfieldRuntimeSystems`. Generic dynel typed lookup/registration is already behind `PlayfieldDynelRegistry`.

The next safe object-lifecycle seams were:

- instanced object removal from `Pool`
- corpse despawn cleanup ordering

## Boundary Change

`PlayfieldObjectLifecycleRuntimeService` now owns:

- `Pool.Instance.RemoveObject(entity)` routing for `Playfield.DisconnectClient`
- corpse despawn cleanup order:
  1. send existing despawn callback
  2. clear NPC corpse despawn schedule
  3. remove corpse state
  4. remove pending corpse credit award

`Playfield` still supplies callbacks for packet emission and its corpse-state collections.

## Intentionally Outside This Boundary

The following remain outside this slice:

- DB/object construction
- spawn data selection
- corpse registration data construction
- corpse loot and credit rolling
- corpse inventory handle allocation
- corpse full-update packet construction
- packet serialization
- combat logic
- XP/reward algorithms
- NPC/player lifecycle internals

## Behavior

No object order, packet order, identity behavior, persistence behavior, or gameplay behavior was changed.
