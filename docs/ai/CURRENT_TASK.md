# Current Task

## Current Focus

Validate the capture-backed Vergil Aeneid boss slice in Subway playfield `127` after implementing the two completed fights and repairing the always-on combat/loot projections.

## Remaining Step

1. Private-smoke Vergil's spawn/appearance, paired level/HP variants, E-Beamer combat, level-specific heals, corpse, credits, three-item loot shape, reopen behavior, and post-loot cleanup.
2. Confirm level 31 uses captured nano `43827` for its `187`-point direct heal and level 30 uses captured nano `43880` for its `34`-point self-heal plus `14000 ms` duration. Neither nano summoned an NPC in the captures; later HoT ticks and the interrupted level-31 cast remain unresolved.
3. Leave Vergil respawn disabled until its timing is observed; neither completed fight remained open long enough to prove that delay.

## Constraints

- Default capture must never filter by focus, enemy type, marker, or validation mode.
- Preserve exact raw bytes before attempting classification or semantic decoding.
- Existing raw captures must be retro-decoded before requesting another gameplay capture.
- Capture counts are evidence, never proof of a complete loot pool or unobserved behavior.
- Do not change database schemas or write runtime loot data to the database.

## Completion Evidence

Captures `20260712-232711` and `20260712-234401` are raw-complete and require no repeat. Earlier capture `20260709-222339` supplies Vergil's exact SCFU spawn/appearance and QL23 Cast-Off E-Beamer. The generalized Subway encounter runtime now keeps Abmouth scripting profile-gated while adding Vergil's captured spawn variants, weapon-owned damage rolls/cadence, level-specific direct/self-heal delivery, exact 420-byte corpse, observed credit set, and two three-item corpse alternatives. AOSharpLiveCapture now exports inventory and combat through always-on reflection/type-name paths instead of concrete casts or focus-marker gates; raw logging is unchanged.
