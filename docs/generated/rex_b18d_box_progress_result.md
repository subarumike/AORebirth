# Rex B18D Box Progress Result

Generated: 2026-06-18

## Goal

Keep the narrow Rex B18D box-use handoff available while correcting the static dynel data to exact packet evidence.

This remains a packet-window handoff only. It does not implement persistence, rewards, inventory changes, XP/credits, objective storage, broad static-dynel event execution, or general quest semantics.

## Evidence Used

- `tools-temp/AOSharpLiveCapture/bin/Debug/captures/20260614-194454/events.log`
  - `6327`, `6333`, `7798`: `GenericCmd Action=Use` targets `Terminal:56D9B4AF`.
  - `6337-6338`: `CharacterAction Action=59` targeting `Mission:5514B18D`.
  - `6341-6342`: `Quest Delete` for `Mission:5514B18D`.
  - `6343-6344`: next `QuestFullUpdate` for `Mission:5514B18E`.
- `tools-temp/AOSharpLiveCapture/bin/Debug/captures/20260614-205724/events.log`
  - `5493-5494`: exact `SimpleItemFullUpdate identity=(Terminal:56D9B4AF)`.
- `tools-temp/AOSharpLiveCapture/bin/Debug/captures/20260614-214819/events.log`
  - `14605-14606`, `15738-15739`, `19521-19522`: repeated exact full update.
- `tools-temp/AOSharpLiveCapture/bin/Debug/captures/20260614-215831/events.log`
  - `10939-10940`, `11431-11432`: repeated exact full update.
- `tools-temp/AOSharpLiveCapture/bin/Debug/captures/20260614-215831/packets.hex.log`
  - `9058-9059`, `9328-9329`: raw packet evidence, including the two zero stat ids serialized as `AnimPlay=0` and `AnimPos=0`.

## Behavior Added Earlier

The existing gated Rex-only, B18D-only box-use route is unchanged:

- Gate: `AO_REBIRTH_ENABLE_ARETE_REX_B18D_BOX_PROGRESS`
- Requires the existing Rex gates through `RexB18CObjectiveProgressTracker.AreAllGatesEnabled`.
- Only handles `Terminal:56D9B4AF`.
- Only handles players in playfield `6553`.
- Acknowledges `GenericCmd Action=Use`.
- Sends the captured B18D handoff packet sequence once per character:
  - `CharacterAction Action=59` targeting `Mission:5514B18D`
  - `Quest Delete` for `Mission:5514B18D`
  - safe DTO-built `QuestFullUpdate` for `Mission:5514B18E`

## Behavior Still Disabled

- Rewards.
- Inventory changes.
- XP or credits.
- Mission persistence.
- General quest-state semantics.
- General DB-backed `StaticDynel` event execution.
- B18E completion.
- Interpretation of action `59` or `Quest Delete` beyond captured mission-window handoff.

## Corrected Static Dynel Row

The static dynel SQL is now based on the exact captured `SimpleItemFullUpdate` for packet identity `Terminal:56D9B4AF`, not the rendered object label and not nearby clutter:

- `Type`: `51005`
- `Instance`: `1457108143` (`0x56D9B4AF`)
- `Playfield`: `6553`
- `Position`: `(3621.576, 51.745, 780.4768)`
- `Rotation`: `(0, -0.7101817, 0, 0.7040185)`
- Stats:
  - `Flags=139265`
  - `StaticInstance=297277`
  - `ACGItemLevel=1`
  - `ACGItemTemplateID=297277`
  - `ACGItemTemplateID2=297277`
  - `MultipleCount=1`
  - `AnimPlay=0`
  - `AnimPos=0`

Rejected local smoke attempts are not represented in the corrected SQL:

- Nearby `Terminal:57369E8E` `Junk` anchor.
- Template `285300`.
- Explicit `Mesh=18794`.

## Files Inspected

- `AI_START_HERE.md`
- `AGENTS.md`
- `docs/ai/AI_START.md`
- `docs/ai/CURRENT_TASK.md`
- `docs/project/PROJECT_STATE.md`
- `docs/project/KNOWN_DECISIONS.md`
- `docs/project/ARCHITECTURE.md`
- `docs/ai/WORKFLOW.md`
- `AORebirth/Server/ZoneEngine/Core/MessageHandlers/GenericCmdMessageHandler.cs`
- `AORebirth/Server/ZoneEngine/Core/Arete/Quests/RexB18DBoxProgressTracker.cs`
- `AORebirth/Server/ZoneEngine/Core/Arete/Quests/SafeQuestFullUpdateSender.cs`
- `tools-temp/AOSharpLiveCapture/bin/Debug/captures/20260614-194454/events.log`
- `tools-temp/AOSharpLiveCapture/bin/Debug/captures/20260614-205724/events.log`
- `tools-temp/AOSharpLiveCapture/bin/Debug/captures/20260614-214819/events.log`
- `tools-temp/AOSharpLiveCapture/bin/Debug/captures/20260614-215831/events.log`
- `tools-temp/AOSharpLiveCapture/bin/Debug/captures/20260614-215831/packets.hex.log`

## Files Changed

- `tools-temp/sql-staging/arete_cargo_box_staticdynel.sql`
- `docs/generated/rex_b18d_box_progress_result.md`
- `docs/generated/rex_b18d_cargo_box_staticdynel_result.md`
- `docs/ai/CURRENT_TASK.md`
- `docs/project/PROJECT_STATE.md`

## Next Smoke Steps

1. Keep the Rex gates enabled:
   - `AO_REBIRTH_ENABLE_ARETE_REX_DIALOGUE_ROUTING=1`
   - `AO_REBIRTH_ENABLE_ARETE_REX_QUEST_PREVIEW=1`
   - `AO_REBIRTH_ENABLE_ARETE_REX_B18C_PROGRESS=1`
   - `AO_REBIRTH_ENABLE_ARETE_REX_B18D_BOX_PROGRESS=1`
2. Relog or zone back into Arete Landing so playfield `6553` reloads for the client.
3. Inspect the object loaded from `Terminal:56D9B4AF`.
4. Use that exact target and confirm whether the B18D handoff reaches `Mission:5514B18E`.

## Validation Status

- Capture files were checked and remain unmodified: `git status --short -- tools-temp/AOSharpLiveCapture` returned no output.
- Corrected SQL was generated from packet identity/full-update evidence.
- Corrected SQL was applied locally to `cellao_codex_clean`.
- DB verification found one scoped row for `Type=51005`, `Instance=1457108143`, `Playfield=6553`.
- DB row now matches captured position `(3621.576, 51.745, 780.4768)` and rotation `(0, -0.7101817, 0, 0.7040185)`.
- DB stat decode matches the captured eight stat entries.
- ZoneEngine was restarted after the SQL apply with the four Rex gates enabled.
- Live client smoke still needs to inspect the corrected object and exact-target B18D use behavior.
- Build was not required for this correction because no runtime code or project files changed.
