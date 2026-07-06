# Player Combat Lifecycle Ownership Checkpoint - 2026-07-05

This checkpoint documents the first player combat runtime boundary slice. It is
an orchestration-only pass-through facade. It does not change gameplay behavior,
packet order, or combat rules.

## Pass-through boundary

`PlayerCombatRuntimeService` is wired through `PlayfieldRuntimeSystems` and
names the current player combat lifecycle seams:

- player attack start
- player attack cancellation
- player combat tick reset
- player combat tick entry
- player fighting-target clear
- player death entry

The service delegates each seam back to existing Playfield or handler callbacks.
Attack start now owns the existing player target/fighting-target mutation and
delegates tick reset back to Playfield. Attack cancellation and player
fighting-target stop/clear now own the existing fighting-target clear and
delegate tick/tracking cleanup back to Playfield. Other seams remain callback
pass-throughs. The service does not own algorithms, packet construction, packet
emission, damage rules, NPC runtime behavior, inventory, loot, credits, corpses,
movement, or database loading.

## Current ownership seams

### AttackMessageHandler

- Calls Playfield player combat entry points for attack start and cancellation.
- Echoes attack state back to the playfield.
- Delegates NPC aggro acquisition through Playfield after the player attack
  state is set.

### Playfield

- Keeps player combat tick reset/tracking storage behind PlayerCombatRuntimeService
  callbacks.
- Owns player combat ticking inside the non-NPC branch of DoCombatTick.
- Owns player fighting-target validation while PlayerCombatRuntimeService owns
  player fighting-target clear orchestration.
- Owns damage application, AttackInfo emission, and killing-hit routing.
- Owns player death-side combat stop sequencing.
- Owns StopFight packet emission for player and mixed player/NPC target clear.
- Owns the shared combat tick dictionaries while delegating NPC tracking cleanup
  through PlayfieldRuntimeSystems.

### PlayfieldRuntimeSystems and NPCRuntimeService

- `PlayfieldRuntimeSystems` owns the `PlayerCombatRuntimeService` facade.
- `NPCRuntimeService` remains NPC-only and does not own player combat lifecycle
  orchestration.

## What still remains outside

- Attack and damage algorithms remain in Playfield.
- Packet construction and packet emission remain in Playfield and handlers.
- StopFight emission remains in Playfield.
- NPC aggro and combat behavior remain in NPCRuntimeService.
- XP, rewards, loot, credits, inventory, and corpse containers remain in their
  current owners.
- Movement/pathing and database loading remain outside this service.

## Intentional exclusions

The next boundary must not change:

- Attack or damage algorithms.
- NPC runtime ownership.
- XP or reward logic.
- Loot, credits, inventory, or corpse containers.
- Packet serialization.
- Movement/pathing.
- Database loading or object construction.

## Intended next boundary

The next safe architecture slice should move one player combat lifecycle
orchestration path at a time behind `PlayerCombatRuntimeService` without moving
damage calculation, packet emission, NPC combat rules, or existing Playfield
lifecycle traces.

Guardrail:

- `PlayfieldLifecycleTraceTests.PlayerCombatRuntimeServiceIntroducesPassThroughBoundaryWithoutOwningAlgorithms`
