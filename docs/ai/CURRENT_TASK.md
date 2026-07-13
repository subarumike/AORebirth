# Current Task

## Current Focus

Live-validate the global loot and ordinary-population foundations for Subway playfield `127`.

## Remaining Step

1. Capture ten kills and initial corpse opens for one enemy type in one session.
2. Use `corpse-loot-observations.csv` to retain empty outcomes, credits, enemy level, player level, item rows, and identity correlation.
3. Add only newly proven membership and weights to the global registry without treating the sample as complete or guaranteed.
4. Live-validate item variety, empty outcomes, credit conditioning, corpse lifetime, reopen, final-loot cleanup, and respawn.

## Constraints

- Ordinary enemies resolve the Subway dungeon-wide pool plus their enemy-type pool.
- Named enemies and bosses use dedicated tables rather than the ordinary enemy-type fallback.
- Capture counts are evidence and candidate weights, never proof of guaranteed loot or a complete pool.
- Do not infer player-level, enemy-level, quality, or drop-rate formulas beyond captured evidence.
- Do not change database schemas or write runtime loot data to the database.

## Completion Evidence

The global loot registry, corpse inventory owner, normalized spawn/group/respawn definitions, population controller, and shared scheduler are active. Automated parity is complete; live Thief, Filth Flea, and loot validation remains outstanding.
