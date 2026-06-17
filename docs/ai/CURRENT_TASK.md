# Current Task

Generated: 2026-06-17

## Current Objective

Validate the safe Rex Larsson B18C `QuestFullUpdate` sender in a controlled live smoke test.

Scope:

- Preserve existing gated Rex content-driven dialogue behavior.
- Keep `AO_REBIRTH_ENABLE_ARETE_REX_DIALOGUE_ROUTING` and `AO_REBIRTH_ENABLE_ARETE_REX_QUEST_PREVIEW` disabled by default.
- Use only the new DTO/serializer-based B18C sender when both gates are explicitly enabled.
- Keep raw captured `QuestFullUpdate` replay permanently blocked.
- Do not implement completion, rewards, inventory, XP/credits, DB writes, quest deletion, Cargo Box behavior, broad Arete quest routing, or action `59` interpretation.

## Current Status

- Safe B18C `QuestFullUpdate` DTO/body serialization is implemented.
- With captured header/body comparison values, the serialized B18C packet matches captured packet `20260614-194454` `#2757` byte-for-byte.
- Focused messaging and ZoneEngine builds pass.
- Arete validation harness passes 131 cases.
- Rex inactive content dry-run passes.

## Active Questions

- Manual smoke with both gates enabled still needs in-client verification:
  - Does the client remain stable?
  - Does `Mission:5514B18C` appear in the mission window?
  - Does the mission display the captured title/objective correctly?

## Validation Plan

- Run `git diff --check`.
- Start engines with both Rex gates enabled only for the controlled smoke.
- Teleport to Arete Landing with `/tp 3624.599 787.7465 51.745 6553`.
- Talk to Rex and select the B18C start option.
- Confirm no rewards, inventory, XP/credits, DB writes, Quest Delete, or completion behavior occurs.
- Restore both gates off after smoke.
