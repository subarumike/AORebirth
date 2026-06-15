# Current Task

Generated: 2026-06-15

## Current Objective

Extend AOSharp Live Capture with passive enemy-state logging for NPC/dynel health, level, location, and lifecycle evidence.

Scope:

- Extend capture logging only.
- Do not automate gameplay.
- Do not alter AO client behavior.
- Do not change packet definitions.
- Do not interpret captured data beyond recording fields exposed by existing AOSharp/AOtomation message classes.

## Current Findings

- Existing AOSharp capture hooks raw packet receive/send and decoded N3 receive/send.
- Existing decoded packet classes expose enough fields for passive enemy state capture:
  - `SimpleCharFullUpdateMessage`: identity, level, health, health damage, position, NPC/player info.
  - `StatMessage`: identity plus `Health`, `MaxHealth`, and `Level` stat updates.
  - `HealthDamageMessage`: target identity, target HP, and amount.
  - `AttackMessage`, `AttackInfoMessage`, `SpecialAttackInfoMessage`, and `MissedAttackInfoMessage`: combat target/defender evidence.
  - `CharDCMoveMessage` and `SetPosMessage`: identity and position.
  - `DespawnMessage`: identity lifecycle evidence.
- `SimpleItemFullUpdateMessage` has stats and optional position, but is item-oriented; it is only consumed for enemy state when its identity is a trackable simple-character identity.

## Current Implementation State

- Added an in-memory enemy state dictionary keyed by entity identity.
- Added per-entity state fields: level, current health, max health, position, first-seen time, last-update time, and death-logged state.
- Added streaming `enemy-state.csv` with columns: `timestamp,entityId,level,currentHealth,maxHealth,x,y,z,eventType`.
- Added `enemy-state.json` grouped by `entityId`, containing the full recorded timeline.
- Added passive lifecycle capture from dynel spawn, CharInPlay/character-seen, damage/combat packets, health/stat updates, position packets, death detection when current health reaches zero, and despawn/character-gone events.
- Added enemy-state counters to `capture_info.json`.
- Added validation that flags a capture as incomplete if combat packets are observed but no enemy-state rows are written.

## Validation

- `git status --short --branch` showed only the existing uncommitted logger/doc changes before this extension.
- `tools-temp/AOSharpLiveCapture/AOSharpLiveCapture.csproj` Debug build succeeded with `0` warnings and `0` errors after the enemy-state changes.
- `git diff --check` passed; Git reported only normal Windows LF-to-CRLF working-copy warnings.

## Next Step

Run a live combat capture from a freshly injected or freshly zoned AO client and verify `enemy-state.csv`, `enemy-state.json`, and `capture_info.json` enemy counters contain expected NPC rows.
