# Current Task

## Active

TASK ID: DUNGEON-LIFECYCLE-COMPLETION-001

Complete the remaining shared named-dungeon lifecycle system across PF127 and PF1931.

## Preserved concurrent mission work

### ACG mission kill — empty corpse crash / no loot

Operational ACG NPCs registered corpses as `lootClass=Empty credits=0 lifetimeSeconds=0` and despawned in ~20ms (`RegisterCorpse` forced empty for `IsOperationalNpc`). Client disconnects on kill; no loot window.

**Offline repair complete:** operational ACG corpse currency now uses the
capture-backed inclusive `21–87` range with overflow-safe deterministic
arithmetic. Corpse registration, opening, transfer, deletion, delayed credits,
duplicate death, and cleanup are scoped to the exact accepted quest, owner,
live PF2, runtime NPC, and corpse. A verified Kill completion retains only its
exact pending/available corpse until that corpse retires, then resumes the
existing durable cleanup without replaying rewards. Restart reconciliation
requires both persisted dead state and the Stage 4 `ObjectiveVerified` phase.
Ordinary/authored corpses are unchanged.

**Deferred live smoke:** restart Zone → kill trash in mish → corpse with
capture-backed credits and explicitly unresolved-empty item contents → open
once → no client crash → mission remains completable.
