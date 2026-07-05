# Player Combat Lifecycle Ownership Checkpoint - 2026-07-05

This checkpoint documents the current player combat lifecycle seams before any
player-combat runtime boundary extraction. It is an audit and guardrail slice
only. It does not change gameplay behavior.

## Current ownership seams

### AttackMessageHandler

- Owns player attack start state assignment from the incoming Attack message.
- Clears the player's fighting target for missing, suppressed, or immune targets.
- Resets the current combat tick through Playfield.
- Echoes attack state back to the playfield.
- Delegates NPC aggro acquisition through Playfield after the player attack
  state is set.

### Playfield

- Owns player combat ticking inside the non-NPC branch of DoCombatTick.
- Owns player fighting-target validation and clearing.
- Owns damage application, AttackInfo emission, and killing-hit routing.
- Owns player death-side combat stop sequencing.
- Owns StopFight packet emission for player and mixed player/NPC target clear.
- Owns the shared combat tick dictionaries while delegating NPC tracking cleanup
  through PlayfieldRuntimeSystems.

### PlayfieldRuntimeSystems and NPCRuntimeService

- Own NPC combat lifecycle ownership only.
- Do not currently own player combat lifecycle orchestration.

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

The next safe architecture slice should introduce a named player combat
lifecycle boundary, or a broader combat runtime boundary, that can own player
attack start, stop, target clear, combat tick, and player death orchestration
without changing packet payloads, packet order, damage rules, NPC combat rules,
or existing Playfield lifecycle traces.

Guardrail:

- `PlayfieldLifecycleTraceTests.PlayerCombatLifecycleOwnershipCheckpointDocumentsCurrentSeams`
