# Current Task

## Current Focus

Resume Abmouth implementation from the already-completed captures now that AOSharpLiveCapture preserves a complete, replayable evidence set by default.

## Remaining Step

1. Use captures `20260712-224840` and `20260712-232137` to finish the captured Abmouth spawn/fight slice without inventing unobserved boss behavior.
2. Treat decoder/export failures with intact raw packets as offline work, not a reason to repeat gameplay.

## Constraints

- Default capture must never filter by focus, enemy type, marker, or validation mode.
- Preserve exact raw bytes before attempting classification or semantic decoding.
- Existing raw captures must be retro-decoded before requesting another gameplay capture.
- Capture counts are evidence, never proof of a complete loot pool or unobserved behavior.
- Do not change database schemas or write runtime loot data to the database.

## Completion Evidence

The lossless raw packet index, shared direct SCFU decoder, durable stop/finalization path, offline retro-decoder, and exact fixture validation are complete. The global loot registry, corpse inventory owner, normalized spawn/group/respawn definitions, population controller, and shared scheduler are active. Live Thief and Filth Flea behavior is accepted. The two completed Abmouth captures contain recoverable raw SCFU/lifecycle evidence; no repeat capture is required for those observed events.
