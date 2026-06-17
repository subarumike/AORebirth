# Current Task

Generated: 2026-06-16

## Current Objective

Standardize NPC dialogue architecture so legacy scripted KnuBot NPCs and new JSON content-pack-driven NPCs share a clear routing model.

Scope:

- Preserve legacy scripted KnuBot behavior and spawn-driven `KnuBotScriptName` attachment.
- Add the narrow shared content-driven NPC dialogue router needed to avoid a Rex-only one-off architecture.
- Keep Rex Larsson as the only registered content-driven NPC.
- Keep `AO_REBIRTH_ENABLE_ARETE_REX_DIALOGUE_ROUTING` disabled by default.
- Preserve Rex behavior, including KnuBot packet UI reuse and Rex-only Arete playfield registration.
- Do not add validation infrastructure, report/export tooling, broad automatic routing, other Arete NPCs, quest behavior, rewards, inventory, XP/credits, DB writes, mission bits, action `59`, or Quest Delete interpretation.

## Active Questions

- None. Use a small in-code registration table unless existing content loader code makes a manifest index simpler.

## Validation Plan

- Focused ZoneEngine build: passed.
- Rex dry-run: passed.
- Arete validation harness: passed 131 cases.
- `git diff --check`: passed with normal LF-to-CRLF warnings.
- Document manual Rex smoke instructions: done in `docs/generated/npc_dialogue_content_architecture_result.md`.
