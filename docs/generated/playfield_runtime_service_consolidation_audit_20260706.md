# Playfield Runtime Service Consolidation Audit - 2026-07-06

## Scope

This checkpoint audits the current Playfield runtime services for duplicate,
misleading, or overly thin pass-through seams after the recent decomposition
work.

## Audit Finding

Most runtime facade methods remain intentionally thin because
`PlayfieldRuntimeSystems` is the single boundary between `Playfield` and the
specialized services. Those pass-throughs preserve naming, ownership, and
validation boundaries.

Two safe cleanup seams were found:

- immediate NPC lifecycle naming still used generic `RemoveNpcImmediately`
- NPC corpse due-despawn processing exposed an unnecessary list-returning helper

## Consolidation

- Renamed the immediate NPC operation to `DespawnNpcImmediately` through
  `PlayfieldRuntimeSystems` and `NPCRuntimeService`.
- Folded due-corpse selection directly into
  `NPCRuntimeService.ProcessDueNpcCorpseDespawns`.

## Intentionally Unchanged

- `PlayfieldRuntimeSystems` remains the facade boundary.
- `PlayerCombatRuntimeService` keeps callback-based player combat orchestration.
- `PlayfieldTimedLifecycleRuntimeService` keeps heartbeat ordering.
- `PlayfieldLifecycleRuntimeService` keeps player respawn and transfer sequencing.
- `PlayfieldInteractionRuntimeService` keeps GenericCmd use dispatch ordering.
- `PlayfieldRewardRuntimeService` keeps NPC death reward hook ordering.
- `PlayfieldObjectLifecycleRuntimeService` keeps object and corpse lifecycle
  callback ordering.

No gameplay behavior, packet order, packet construction, combat logic, movement,
inventory, rewards, or persistence behavior changed.
