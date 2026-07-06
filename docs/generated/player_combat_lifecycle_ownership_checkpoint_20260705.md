# Player Combat Lifecycle Ownership Checkpoint - 2026-07-05

This checkpoint documents the final player combat runtime ownership boundary
after the initial PlayerCombatRuntimeService extraction series. The service owns
player combat lifecycle orchestration only. It does not change gameplay
behavior, packet order, or combat rules.

## Final boundary

`PlayerCombatRuntimeService` is wired through `PlayfieldRuntimeSystems` and
owns player attack start, cancel/stop clear, combat tick orchestration,
invalid-target cleanup, and death combat cleanup.

Named owned seams:

- player attack start
- player attack cancellation
- player combat tick reset
- player combat tick entry
- player invalid-target cleanup
- player fighting-target clear
- player death-side combat cleanup
- player respawn-side combat cleanup

Attack start owns the existing player target/fighting-target mutation and
delegates tick reset back to Playfield. Attack cancellation and player
fighting-target stop/clear own the existing fighting-target clear and delegate
tick/tracking cleanup back to Playfield. Player combat tick owns the no-target,
target lookup, target validation, invalid-target clear, and validated-tick
dispatch orchestration while delegating lookup, logging, tracking cleanup,
timing, damage, packet emission, and world mutation back to Playfield.
Invalid-target cleanup is named in `ClearInvalidCombatTarget` and preserves the
existing log-before-clear order. Death combat cleanup is named in
`CleanupDeathCombat` and preserves the target clear, fighting-target clear,
tracking cleanup, stop-fighting-dead-target, and StopFight callback order for
both player death and player respawn cleanup paths.

The service does not own algorithms, packet construction, packet emission,
damage rules, NPC runtime behavior, inventory, loot, credits, corpses,
movement, or database loading.

## Current ownership seams

### AttackMessageHandler

- Calls Playfield player combat entry points for attack start and cancellation.
- Echoes attack state back to the playfield.
- Delegates NPC aggro acquisition through Playfield after the player attack
  state is set.
- Keeps only the legacy non-Playfield fallback for attack state mutation.

### Playfield

- Keeps player combat tick reset/tracking storage behind PlayerCombatRuntimeService
  callbacks.
- Keeps target lookup, invalid-target logging, attack timing, range checks,
  damage application, AttackInfo emission, and world mutation behind
  PlayerCombatRuntimeService callbacks.
- Keeps player death lifecycle outside PlayerCombatRuntimeService while routing
  death-side and respawn-side combat cleanup through the service.
- Routes player combat ticking through PlayfieldRuntimeSystems while
  PlayerCombatRuntimeService owns the tick orchestration decision flow.
- Owns damage application, AttackInfo emission, and killing-hit routing.
- Owns player death lifecycle behavior outside the service; PlayerCombatRuntimeService
  owns only death-side combat cleanup ordering.
- Owns StopFight packet emission for player and mixed player/NPC target clear.
- Owns packet emission, damage/range/timing algorithms, world mutation, death
  lifecycle, and object lookups.
- Owns the shared combat tick dictionaries while delegating NPC tracking cleanup
  through PlayfieldRuntimeSystems.

### PlayfieldRuntimeSystems and NPCRuntimeService

- `PlayfieldRuntimeSystems` owns the `PlayerCombatRuntimeService` facade.
- `NPCRuntimeService` remains NPC-only and does not own player combat lifecycle
  orchestration.

## What still remains outside

- Attack and damage algorithms remain in Playfield.
- Combat timing and range checks remain in Playfield.
- Packet construction and packet emission remain in Playfield and handlers.
- StopFight emission remains in Playfield.
- Object lookups and world-state mutation remain in Playfield.
- Player death lifecycle remains in Playfield.
- NPC aggro and combat behavior remain in NPCRuntimeService.
- XP, rewards, loot, credits, inventory, and corpse containers remain in their
  current owners.
- Movement/pathing and database loading remain outside this service.

## Intentional exclusions

This boundary must not change:

- Attack or damage algorithms.
- NPC runtime ownership.
- XP or reward logic.
- Loot, credits, inventory, or corpse containers.
- Packet serialization.
- Movement/pathing.
- Database loading or object construction.

## Final guardrail

The final guardrail asserts that PlayerCombatRuntimeService owns player combat
lifecycle orchestration only, Playfield still owns packet emission and combat
algorithms, and NPCRuntimeService remains NPC-only.

Guardrail:

- `PlayfieldLifecycleTraceTests.PlayerCombatRuntimeServiceFinalBoundaryOwnsLifecycleOrchestrationOnly`
