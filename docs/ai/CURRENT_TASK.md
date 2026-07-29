# Current Task

## Active

### ACG mission kill — empty corpse crash / no loot

Operational ACG NPCs registered corpses as `lootClass=Empty credits=0 lifetimeSeconds=0` and despawned in ~20ms (`RegisterCorpse` forced empty for `IsOperationalNpc`). Client disconnects on kill; no loot window.

**Fix:** use mission-interior trash loot path (sparse items + credits 21–87) for operational ACG NPCs so corpses stay lootable.

**Retest:** restart Zone → kill trash in mish → corpse with credits (sometimes item) → open loot → no client crash.
