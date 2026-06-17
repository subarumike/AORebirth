# Rex B18C Live Objective Progress Result

Generated: 2026-06-17

## Summary

Added the first gated live objective-progress path for Rex Larsson `Mission:5514B18C`.

The mission display path is unchanged: Rex dialogue and the safe DTO-based B18C `QuestFullUpdate` sender still control whether the mission appears in the client mission window. This change adds only in-memory progress tracking after the preview is successfully sent and only when a third disabled-by-default gate is explicitly enabled:

```powershell
$env:AO_REBIRTH_ENABLE_ARETE_REX_DIALOGUE_ROUTING = '1'
$env:AO_REBIRTH_ENABLE_ARETE_REX_QUEST_PREVIEW = '1'
$env:AO_REBIRTH_ENABLE_ARETE_REX_B18C_PROGRESS = '1'
```

No quest completion, Quest Delete, rewards, inventory changes, XP/credit implementation, DB writes, persistent mission state, Cargo Box behavior, B18D/B18E behavior, broad Arete routing, or action `59` interpretation was added.

## Files Inspected

- `AI_START_HERE.md`
- `AGENTS.md`
- `docs/ai/AI_START.md`
- `docs/ai/CURRENT_TASK.md`
- `docs/project/PROJECT_STATE.md`
- `docs/project/KNOWN_DECISIONS.md`
- `docs/project/ARCHITECTURE.md`
- `docs/ai/WORKFLOW.md`
- `docs/generated/rex_safe_questfullupdate_sender_result.md`
- `docs/generated/rex_b18c_quest_preview_result.md`
- `docs/generated/rex_objective_playback_service_result.md`
- `docs/generated/rex_objective_event_semantics_result.md`
- `AORebirth/Server/ZoneEngine/Core/Arete/Quests/SafeQuestFullUpdateSender.cs`
- `AORebirth/Server/ZoneEngine/Core/Arete/Quests/RexQuestPreviewEmitter.cs`
- `AORebirth/Server/ZoneEngine/Core/Arete/Quests/ObjectivePlaybackService.cs`
- `AORebirth/Server/ZoneEngine/Core/Arete/Quests/ObjectivePlaybackModels.cs`
- `AORebirth/Server/ZoneEngine/Content/Arete/rex-larsson/quests/rex-larsson.quests.json`
- `AORebirth/Server/ZoneEngine/Core/Arete/Dialogue/ContentDrivenNpcDialogueRouter.cs`
- `AORebirth/Server/ZoneEngine/Core/Playfields/Playfield.cs`
- `AORebirth/Libraries/Source/AORebirth.Core/Entities/ICharacter.cs`
- `AORebirth/Libraries/Source/AORebirth.Core/Entities/Dynel.cs`
- `AORebirth/Libraries/Source/AORebirth.Core/NPCHandler/NonPlayerCharacterHandler.cs`
- `AORebirth/Server/ZoneEngine/ZoneEngine.csproj`
- `AORebirth/Config/Config.xml`
- local `cellao_codex_clean.mobspawns` rows for playfield `6553` using read-only `SELECT`

## Files Changed

- `AORebirth/Server/ZoneEngine/Core/Arete/Quests/RexB18CObjectiveProgressTracker.cs`
- `AORebirth/Server/ZoneEngine/Core/Arete/Quests/RexQuestPreviewEmitter.cs`
- `AORebirth/Server/ZoneEngine/Core/Playfields/Playfield.cs`
- `AORebirth/Server/ZoneEngine/ZoneEngine.csproj`
- `docs/ai/CURRENT_TASK.md`
- `docs/project/PROJECT_STATE.md`
- `docs/generated/rex_b18c_live_objective_progress_result.md`

## Safe Preview Checkpoint

The prompt-provided checkpoint is now recorded in `docs/ai/CURRENT_TASK.md`:

- Rex dialogue works.
- The safe B18C `QuestFullUpdate` sender works.
- `Mission:5514B18C` appears in the client mission window.
- Before this change, B18C did not progress when robots died.

## Live Mob-Death Event Point

The narrow live event point is:

- `AORebirth/Server/ZoneEngine/Core/Playfields/Playfield.cs`
- method: `KillNpcTarget(ICharacter attacker, ICharacter target)`

This point already has:

- killer player/client candidate through `attacker`
- killed NPC through `target`
- target identity through `target.Identity`
- target name through `target.Name`
- playfield through `attacker.Playfield` and `target.Playfield`

The hook is placed immediately after the existing `SendNpcDeathAnimation(target)` call, matching the captured `CharacterAction Action=Death` signal, and before existing XP/corpse handling. The hook is an observer only.

## B18C In-Memory Active State

Added `RexB18CObjectiveProgressTracker`.

Activation behavior:

- Requires all three gates:
  - `AO_REBIRTH_ENABLE_ARETE_REX_DIALOGUE_ROUTING`
  - `AO_REBIRTH_ENABLE_ARETE_REX_QUEST_PREVIEW`
  - `AO_REBIRTH_ENABLE_ARETE_REX_B18C_PROGRESS`
- Activates only after `SafeQuestFullUpdateSender.TrySendB18CPreview` returns an emitted result.
- Requires the source character to be a player in playfield `6553`.
- Stores state in memory only, keyed by player identity.
- Starts `Mission:5514B18C` at `0/5`.

Tracked progress fields:

- mission ID
- objective ID
- objective type
- current count
- required count
- completed flag
- matched evidence count
- ignored evidence count
- last matched evidence reference

## B18C Kill-Progress Behavior

Death observation behavior:

- Requires all three gates.
- Requires the attacker to be a player.
- Requires the attacker to be in Arete Landing `6553`.
- Requires an active in-memory B18C preview record for that player.
- Counts only target name `Malfunctioning Cleaning Robot`.
- Caps objective count at `5/5`.
- Ignores extra matching kills after `5/5`.
- Logs safe diagnostics with `ARETE_REX_B18C_PROGRESS`.

At `5/5`, only the in-memory objective record is marked complete.

Still forbidden and not implemented:

- Quest Delete
- mission completion packet
- rewards
- inventory changes
- XP/credit implementation
- DB writes
- B18D offer or chain progression
- action `59`
- persistent mission state

## Mission Window Progress Refresh

Mission window progress refresh is server-side logs only in this phase.

No progress `QuestFullUpdate`, quest feedback packet, `QuestAlternative`, `CreateQuest`, or Quest Delete packet is emitted because the safe DTO fields for progress refresh have not been proven. The tracker logs progress such as `1/5`, `2/5`, up to `5/5` when matching robot deaths occur.

## Robot Spawn Evidence And Runtime Spawn Check

The capture evidence already contains named `Malfunctioning Cleaning Robot` entities, repeated spawns, deaths, corpses, level, HP, monster data, and coordinates.

Representative evidence from `tools-temp/AOSharpLiveCapture/bin/Debug/captures/20260614-194454/events.log`:

- `events.log:63`: `(SimpleChar:78D3ACAA)` `name=Malfunctioning Cleaning Robot`, level `1`, HP `12/12`, position `(3595.793, 51.745, 799.1069)`, `monsterData=297023`.
- `events.log:64`: `(SimpleChar:78D3AC6E)` `name=Malfunctioning Cleaning Robot`, level `1`, HP `12/12`, position `(3618.986, 51.745, 794.955)`, `monsterData=297023`.
- `events.log:2719-2722`: `(SimpleChar:78D3ACC6)` named robot spawn/full update at `(3606.319, 51.745, 801.3757)`, level `1`, HP `12/12`, `monsterData=297023`.
- `events.log:3062-3064`, `3251-3265`, and `3431-3432`: corpse evidence for `Remains of Malfunctioning Cleaning Robot`.
- `events.log:3390-3408`: `(SimpleChar:78D3ACC9)` named robot spawn/full update at `(3617.602, 51.745, 783.9747)`, level `1`, HP `12/12`, `monsterData=297023`.

The existing Rex content pack also records nine B18C death references after the mission offer. The issue is not missing capture evidence.

Read-only local DB verification checked `cellao_codex_clean.mobspawns` for playfield `6553`.

Result:

- Arete `mobspawns` row count: `1`
- Existing row: `Rex Larsson`
- `Malfunctioning Cleaning Robot` rows in playfield `6553`: `0`

No SQL writes were made.

Live 5/5 smoke is therefore blocked in the current local runtime because the captured robot evidence has not yet been promoted into runtime `mobspawns` rows. The next phase should convert the captured robot observations into an explicitly isolated, evidence-backed Arete robot spawn data change.

## Manual Smoke Status

The code path is implemented and compiled, but live progress to `5/5` was not manually completed in-client because the local Arete runtime has no `Malfunctioning Cleaning Robot` spawn rows yet, despite the capture containing the needed robot evidence.

Expected smoke steps once an evidence-backed robot spawn exists:

```powershell
$env:AO_REBIRTH_ENABLE_ARETE_REX_DIALOGUE_ROUTING = '1'
$env:AO_REBIRTH_ENABLE_ARETE_REX_QUEST_PREVIEW = '1'
$env:AO_REBIRTH_ENABLE_ARETE_REX_B18C_PROGRESS = '1'
.\stop-engines.ps1
.\start-engines.ps1
```

Client:

```text
/tp 3624.599 787.7465 51.745 6553
```

Then:

1. Talk to Rex.
2. Start B18C so it appears in the mission window.
3. Kill `Malfunctioning Cleaning Robot`.
4. Watch ZoneEngine logs for `ARETE_REX_B18C_PROGRESS progress=...`.

## Validation

- Focused ZoneEngine build: passed.
- Rex inactive content dry-run: passed.
- Arete validation harness: passed 131 cases with `-SkipBuild`.
- `git diff --check`: passed with line-ending warnings only.

## Gate-Off Restore

After validation, all three Rex gates should be cleared before default runtime:

```powershell
Remove-Item Env:\AO_REBIRTH_ENABLE_ARETE_REX_DIALOGUE_ROUTING -ErrorAction SilentlyContinue
Remove-Item Env:\AO_REBIRTH_ENABLE_ARETE_REX_QUEST_PREVIEW -ErrorAction SilentlyContinue
Remove-Item Env:\AO_REBIRTH_ENABLE_ARETE_REX_B18C_PROGRESS -ErrorAction SilentlyContinue
.\stop-engines.ps1
.\start-engines.ps1
```

## Remaining Blockers

- Local Arete currently has no runtime `Malfunctioning Cleaning Robot` spawns to kill.
- Captured robot evidence exists and must be promoted into isolated runtime spawn data before live `5/5` smoke.
- Mission window progress packet fields are not proven.
- Exact live per-kill mission feedback packet semantics remain unresolved.
- Action `59` remains unresolved.
- Quest Delete gameplay meaning remains unresolved.
- B18D Cargo Box identity and behavior remain unresolved.

## Next Implementation Step

Promote the captured Arete `Malfunctioning Cleaning Robot` observations into evidence-backed runtime spawn data or an explicitly isolated test-spawn path, then run the gated live smoke to prove the in-memory B18C progress logs reach `5/5`. Keep mission completion, Quest Delete, rewards, inventory, XP/credits, and DB persistence disabled until their packet semantics are independently proven.
