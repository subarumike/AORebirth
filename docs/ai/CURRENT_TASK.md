# Current Task

Generated: 2026-06-18

## Current Objective

Implement and validate the gated Rex B18E completion and B18F handoff path.

Scope:

- Return NPC: Rex Larsson, `SimpleChar:782DE568`.
- Playfield: Arete Landing `6553`.
- Completion mission: `Mission:5514B18E`.
- Next mission: `Mission:5514B18F`.
- Next NPC evidence: Marcus Stone, `SimpleChar:782DE567`.
- B18E delete evidence: `tools-temp/AOSharpLiveCapture/bin/Debug/captures/20260614-194454/packets.hex.log:5947`, packet `#5495`.
- B18F QuestFullUpdate evidence: `tools-temp/AOSharpLiveCapture/bin/Debug/captures/20260614-194454/packets.hex.log:5949`, packet `#5497`.
- XP reward evidence: same-player XP changes from `870` to `1160`, proving `+290 XP`.

## Current Status

- B18E completion is gated by `AO_REBIRTH_ENABLE_ARETE_REX_B18E_COMPLETION`, disabled by default.
- The handler also requires the existing Rex dialogue, quest preview, and B18D preview gates.
- Completion triggers only when a player in Arete has reached in-memory `B18EPreviewed` state and opens Rex's captured return dialogue branch.
- The implementation sends a DTO-built `QuestMessage Action=Delete` for only `Mission:5514B18E`.
- The implementation grants exactly `+290 XP` through the existing stat update path.
- The implementation sends a DTO-built `QuestFullUpdate` for only `Mission:5514B18F`, title/objective `Talk to Marcus Stone`.
- Per-character in-memory completion state prevents duplicate B18E delete, duplicate XP, or duplicate B18F send on retry.

## Explicitly Disabled / Not Implemented

- No action `59`.
- No credits.
- No item rewards.
- No inventory mutation.
- No DB mission persistence.
- No Marcus Stone dialogue or B18F implementation.
- No raw packet replay.
- No SQL or schema changes.
- No broad quest framework changes.

## Validation Status

- Focused ZoneEngine build passed.
- Rex inactive content dry-run passed.
- B18E Quest Delete DTO body matches captured packet `#5495` byte-for-byte from the N3 body onward.
- B18F QuestFullUpdate DTO body matches captured packet `#5497` byte-for-byte from the N3 body onward.
- Manual in-client smoke is still needed to confirm:
  - B18E is removed from the mission window.
  - XP increases by 290.
  - B18F appears as `Talk to Marcus Stone`.
  - Client remains stable.

## Next Validation Step

Start engines with all Rex gates enabled, then in client:

1. Complete B18C.
2. Complete B18D by using exact Cargo Box target `Terminal:56D9B4AF`.
3. Return to Rex when B18E is active.
4. Confirm B18E is removed.
5. Confirm XP increases by 290.
6. Confirm B18F appears as `Talk to Marcus Stone`.
7. Confirm no credits, items, inventory mutation, action `59`, DB mission persistence, or Marcus Stone dialogue occurs.
