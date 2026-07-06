# NPCRuntimeService Boundary Checkpoint - 2026-07-05

This checkpoint documents the final NPC runtime ownership boundary after the
NPCRuntimeService refactor. It is an architecture guardrail only and does not
change gameplay behavior.

## NPCRuntimeService owns

- NPC activation and registry integration.
- Captured NPC spawn orchestration.
- NPC home-state bookkeeping.
- NPC aggro acquisition orchestration.
- NPC combat start, combat clear, combat tick, and combat tracking delegation.
- NPC patrol and follow tick orchestration.
- NPC death lifecycle ordering.
- Named reward and corpse hook orchestration during NPC death.
- Dead NPC despawn and NPC corpse despawn timing orchestration.

## PlayfieldRuntimeSystems owns

- The facade methods that expose NPC runtime ownership to Playfield.
- NPC-specific delegation names such as DespawnNpcImmediately, ProcessNpcCombatTick,
  ProcessNpcPatrolTick, BeginNpcDeath, ProcessDeadNpcDespawn,
  ScheduleNpcCorpseDespawn, ClearNpcCorpseDespawn, and
  ProcessDueNpcCorpseDespawns.

`ProcessDueNpcCorpseDespawns` owns the due-corpse selection and callback loop
directly. There is no separate list-returning due-despawn facade method.

## Playfield intentionally still owns

- Packet emission and Announce/SendCompressed call sites.
- Playfield object, corpse, and pending-corpse collections.
- Corpse visual materialization and CorpseFullUpdate construction.
- Corpse loot, credit, and corpse container construction.
- World-state mutation that is not NPC runtime ownership.
- Movement/pathing implementation details.
- Player lifecycle behavior.

## Guardrail

The boundary is guarded by
`PlayfieldLifecycleTraceTests.PlayfieldRuntimeSystemsFacadeOwnsSeparatedRuntimeCoordinators`.
That test asserts NPCRuntimeService owns NPC tick, patrol, combat, death,
corpse, and despawn orchestration while Playfield keeps packet emission,
corpse storage, loot, credits, and corpse container construction.
