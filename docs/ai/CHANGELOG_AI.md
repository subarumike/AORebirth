# AI Changelog

## 2026-06-09 - Verified Player Trade Display And Commit Behavior

Change: Recorded the completed player-to-player trade verification after adding temporary trace logging.

Files affected:

- `CellAO/Server/ZoneEngine/Core/MessageHandlers/TradeMessageHandler.cs`
- `tools-temp/CellAOCombatSmokeTests/Run-CombatSmokeTests.ps1`
- `docs/ai/CURRENT_TASK.md`
- `docs/project/PROJECT_STATE.md`
- `docs/ai/CHANGELOG_AI.md`

Reason: Player trade credit and item display needed verification after prior reports of credit desync and stale trade-window visuals.

Result:

- Credit-only trade behaved as expected.
- Item-only trade behaved as expected.
- Mixed item-plus-credit trade behaved as expected.
- Cancel/decline trade behaved as expected.
- No player trade display or commit defect was reproduced.
- Temporary `TRADE_*` logging remains available for future trade investigation.

Follow-up work:

- Build broader inventory/container regression coverage for repaired corpse loot, player trade, vendor shop, equipment, and normal inventory move flows.
- Keep NPC movement out of scope unless explicitly selected later.

## 2026-06-08 - Completed Corpse Credit Investigation

Change: Fixed the corpse credit client-visible value path and removed duplicate corpse credit chat.

Files affected:

- `CellAO/Server/ZoneEngine/Core/Packets/CorpseFullUpdate.cs`
- `CellAO/Server/ZoneEngine/Core/Playfields/Playfield.cs`
- `tools-temp/CellAOCombatSmokeTests/Run-CorpseCreditTraceAssertions.ps1`
- `docs/ai/CURRENT_TASK.md`
- `docs/project/PROJECT_STATE.md`
- `docs/ai/CHANGELOG_AI.md`

Reason: Local playtest showed corpse credits could display as stale/hardcoded values such as `111`, and after the packet offset repair the client displayed duplicate corpse credit text when the server also sent manual `ChatText` feedback.

Result:

- `CorpseFullUpdate` now patches corpse cash at offset `207`, the cash value word after stat id `61` at offset `203`.
- The old hardcoded `111` corpse cash template value is not preserved.
- Manual server corpse credit `ChatText` feedback was suppressed because the current client displays the corrected corpse credit message from the corpse cash/stat flow.
- Focused corpse credit assertions were added and retained for credit roll ranges, offset `207`, delayed cash mutation, stat emission, and item loot not mutating cash.
- Cliff Malle playtest passed with one correct `You received 3 credits from the corpse.` message.

Follow-up work:

- Trace player trade credit/item display behavior next.
- Keep NPC movement out of scope unless explicitly selected later.

## 2026-06-02 - Created AI Documentation System

Change: Added root AI handoff documentation.

Files affected:

- `AI_START_HERE.md`
- `docs/project/OVERVIEW.md`
- `docs/project/ARCHITECTURE.md`
- `docs/ai/CURRENT_TASK.md`
- `docs/project/KNOWN_DECISIONS.md`
- `docs/project/ROADMAP.md`
- `docs/project/FEATURES.md`
- `docs/backlog/BUGS.md`
- `docs/ai/TESTING.md`
- `docs/archive/ai/SESSION_NOTES.md`
- `docs/ai/CODE_STANDARDS.md`
- `docs/ai/CHANGELOG_AI.md`
- `docs/backlog/TODO.md`
- `docs/archive/ai/LESSONS_LEARNED.md`

Reason: Preserve project context across AI context compaction, session resets, model changes, and handoffs.

Follow-up work:

- Keep `docs/ai/CURRENT_TASK.md` current after each major work block.
- Add source-backed findings to `docs/project/KNOWN_DECISIONS.md` when behavior is locked.
- Move resolved bugs from `docs/backlog/BUGS.md` into changelog entries.
