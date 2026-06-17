# Rex B18C Quest Preview Result

Generated: 2026-06-17

## Summary

Added a controlled Rex Larsson B18C quest-preview path behind a second disabled-by-default gate:

```powershell
$env:AO_REBIRTH_ENABLE_ARETE_REX_DIALOGUE_ROUTING = '1'
$env:AO_REBIRTH_ENABLE_ARETE_REX_QUEST_PREVIEW = '1'
```

Initial implementation attempted to emit only the captured `QuestFullUpdate` display/update for `Mission:5514B18C` when both gates were enabled and the captured Rex option at node `rex_194454_004`, answer index `0`, was selected.

Manual smoke showed that replaying the raw captured `QuestFullUpdate` frame causes a hard client hang. The live emission path is now blocked even when the preview gate is enabled. Rex dialogue remains usable, but B18C will not appear in the client mission window until a safe QuestFullUpdate sender is built.

No mission completion, rewards, inventory changes, XP/credits, DB writes, mission bits, `Quest Delete`, Cargo Box behavior, or action `59` interpretation were added.

## Files Inspected

- `AI_START_HERE.md`
- `AGENTS.md`
- `docs/ai/AI_START.md`
- `docs/ai/CURRENT_TASK.md`
- `docs/project/PROJECT_STATE.md`
- `docs/project/KNOWN_DECISIONS.md`
- `docs/project/ARCHITECTURE.md`
- `docs/generated/npc_dialogue_content_architecture_result.md`
- `docs/generated/rex_live_dialogue_option_progression_result.md`
- `docs/generated/rex_questfullupdate_decoding_result.md`
- `docs/generated/rex_larsson_packet_semantics_result.md`
- `docs/generated/rex_objective_event_semantics_result.md`
- `docs/generated/rex_objective_playback_service_result.md`
- `AORebirth/Server/ZoneEngine/Core/Arete/Dialogue/ContentDrivenNpcDialogueRouter.cs`
- `AORebirth/Server/ZoneEngine/Core/Arete/Dialogue/AreteRexDialogueRouter.cs`
- `AORebirth/Server/ZoneEngine/Core/MessageHandlers/KnuBotAnswerMessageHandler.cs`
- `AORebirth/Server/ZoneEngine/Core/Arete/Dialogue/DialogueModels.cs`
- `AORebirth/Server/ZoneEngine/Core/Arete/Dialogue/DialogueSessionService.cs`
- `AORebirth/Server/ZoneEngine/Core/Arete/Dialogue/DialogueActionReferenceValidator.cs`
- `AORebirth/Server/ZoneEngine/Core/Arete/AreteNoOpActionRecorder.cs`
- `AORebirth/Server/ZoneEngine/Content/Arete/rex-larsson/manifest.json`
- `AORebirth/Server/ZoneEngine/Content/Arete/rex-larsson/dialogue/rex-larsson.dialogue.json`
- `AORebirth/Server/ZoneEngine/Content/Arete/rex-larsson/quests/rex-larsson.quests.json`
- `AORebirth/Libraries/Source/AOtomation/AOtomation.Messaging/src/SmokeLounge.AOtomation.Messaging/Messages/N3Messages/QuestFullUpdateMessage.cs`
- `AORebirth/Libraries/Source/AOtomation/AOtomation.Messaging/src/SmokeLounge.AOtomation.Messaging/Messages/N3Messages/CreateQuestMessage.cs`
- `AORebirth/Libraries/Source/AOtomation/AOtomation.Messaging/src/SmokeLounge.AOtomation.Messaging/Messages/N3Messages/QuestAlternativeMessage.cs`
- `AORebirth/Libraries/Source/AOtomation/AOtomation.Messaging/src/SmokeLounge.AOtomation.Messaging/GameData/QuestInfo.cs`
- `AORebirth/Libraries/Source/AOtomation/AOtomation.Messaging/src/SmokeLounge.AOtomation.Messaging/GameData/QuestActionList.cs`
- `AORebirth/Libraries/Source/AOtomation/AOtomation.Messaging/src/SmokeLounge.AOtomation.Messaging/Messages/N3Message.cs`
- `AORebirth/Libraries/Source/AOtomation/AOtomation.Messaging/src/SmokeLounge.AOtomation.Messaging/Messages/Header.cs`
- `AORebirth/Libraries/Source/AOtomation/AOtomation.Messaging/src/SmokeLounge.AOtomation.Messaging/Serialization/MessageSerializer.cs`
- `AORebirth/Libraries/Source/AOtomation/AOtomation.Messaging/src/SmokeLounge.AOtomation.Messaging/Serialization/Serializers/HeaderSerializer.cs`
- `tools-temp/arete-analysis/scripts/decode_rex_questfullupdate.ps1`
- `tools-temp/AOSharpLiveCapture/bin/Debug/captures/20260614-194454/packets.hex.log`
- `tools-temp/arete-framework-validation/Run-RexLarssonContentDryRun.ps1`
- `tools-temp/arete-framework-validation/Run-AreteFrameworkValidation.ps1`

## Files Changed

- `AORebirth/Server/ZoneEngine/Core/Arete/Dialogue/ContentDrivenNpcDialogueRouter.cs`
- `AORebirth/Server/ZoneEngine/Core/Arete/Quests/RexQuestPreviewEmitter.cs`
- `AORebirth/Server/ZoneEngine/ZoneEngine.csproj`
- `docs/ai/CURRENT_TASK.md`
- `docs/project/PROJECT_STATE.md`
- `docs/generated/rex_b18c_quest_preview_result.md`

## Quest Packet Path Identified

The Rex B18C capture contains `QuestFullUpdate` packets for the mission display/update:

- `tools-temp/AOSharpLiveCapture/bin/Debug/captures/20260614-194454/packets.hex.log:2835`
- packet `#2757`
- mission `Mission:5514B18C`
- title `Terminate 5 Malfunctioning Cleaning Robots`
- objective `Kill 5 Malfunctining Cleaning Robots.`

The Rex segment did not contain `CreateQuest` or `QuestAlternative` for the initial B18C display. `QuestFullUpdate` is therefore the evidence-backed preview packet for this phase.

Tool-side AOSharp can decode the captured packet and expose the decoded fields. The local runtime `QuestFullUpdateMessage` DTO does not currently deserialize this captured B18C frame safely, and runtime must not depend on AOSharp tooling assemblies. A live DTO send path was therefore not available.

The first smoke implementation replayed the captured B18C `QuestFullUpdate` frame and patched:

- AO header sender to local ZoneEngine sender `0x0356`
- AO header receiver to the current character identity
- four captured `SimpleChar:78CB984B` player identity references to the current character identity

That raw replay is now blocked because it hard-hung the client.

## Emitter Behavior

Added `RexQuestPreviewEmitter`.

It only applies when all of these are true:

- `AO_REBIRTH_ENABLE_ARETE_REX_DIALOGUE_ROUTING` is enabled.
- `AO_REBIRTH_ENABLE_ARETE_REX_QUEST_PREVIEW` is enabled.
- source character is in playfield `6553`.
- target NPC is Rex Larsson `SimpleChar:782DE568`.
- selected dialogue node is `rex_194454_004`.
- selected answer index is `0`.

The preview packet is no longer emitted. When the preview gate is enabled and the B18C option is selected, the emitter logs a blocked result and sends no quest packet.

The blocked raw packet had been emitted after the captured Rex prompt for node `rex_194454_005` and before the next answer list, matching the captured packet ordering. The hard hang therefore points at unsafe packet replay/body serialization rather than a dialogue-order mismatch.

Diagnostics log whether the B18C preview was skipped, attempted, emitted, or failed and explicitly note that no persistence, rewards, inventory, XP/credits, Quest Delete, or completion occurred.

## Content Marker

No JSON action marker was added.

Reason: the existing validator only supports the known inactive action types `OfferMission`, `AcceptMission`, `CompleteMission`, `FailMission`, `AbandonMission`, and `EndDialogue`. Adding `PreviewQuestFullUpdate` as a dialogue action would break existing Rex aggregate validation or require validation framework changes, which were out of scope for this phase.

The existing captured content already provides a stable marker: node `rex_194454_004`, answer index `0`, with captured text `I'll do it if you promise to tell me who your contact is.`

## Manual Smoke Instructions

Current safe smoke setup keeps the quest preview gate off:

```powershell
cd C:\Users\Mike\Documents\AORebirth
$env:AO_REBIRTH_ENABLE_ARETE_REX_DIALOGUE_ROUTING = '1'
Remove-Item Env:\AO_REBIRTH_ENABLE_ARETE_REX_QUEST_PREVIEW -ErrorAction SilentlyContinue
.\start-engines.ps1
```

In the client:

```text
/tp 3624.599 787.7465 51.745 6553
```

Talk/trade with Rex and advance the captured path:

1. `I don't really feel like telling you any of my secrets. If you'll excuse me, I need to go now.`
2. `Who said I don't have any ID?`
3. `Get to the point, Rex.`
4. `I'll do it if you promise to tell me who your contact is.`

Expected visible result in the current safe state:

- Rex dialogue remains visible and advances to `Excellent choice...`.
- The client mission window does not receive B18C yet.
- No reward, inventory, XP, credit, DB, completion, Quest Delete, or Cargo Box behavior should occur.

Restore default behavior after smoke:

```powershell
Remove-Item Env:\AO_REBIRTH_ENABLE_ARETE_REX_DIALOGUE_ROUTING -ErrorAction SilentlyContinue
Remove-Item Env:\AO_REBIRTH_ENABLE_ARETE_REX_QUEST_PREVIEW -ErrorAction SilentlyContinue
.\stop-engines.ps1
.\start-engines.ps1
```

## Manual Smoke Result

Manual smoke with both Rex gates enabled was run in-client by Mike on 2026-06-17.

Result:

- Rex dialogue advanced to the B18C accept path.
- The raw captured `QuestFullUpdate` preview emitted once.
- The client hard-hung when trying to get the mission.
- With the quest preview gate off, Rex dialogue can complete again, but no mission appears in the mission window.

Mitigation:

- The live raw `QuestFullUpdate` emission path is blocked.
- Engines were restored to the safe test state with Rex dialogue routing enabled and quest preview emission off/blocked.

## Validation

- Focused ZoneEngine build: passed.
- Rex dry-run: passed.
- Arete validation harness: passed 131 cases.
- `git diff --check`: passed with normal LF-to-CRLF working-copy warnings.

## Confirmed Disabled Behavior

- No SQL.
- No schema change.
- No Cargo Box changes.
- No broad quest framework.
- No broad Arete quest routing.
- No routing for other NPCs.
- No mission completion.
- No rewards.
- No inventory mutation.
- No XP or credit mutation.
- No DB writes.
- No character stat mutation.
- No action `59` interpretation.
- No `Quest Delete` interpretation.
- No new validation infrastructure.
- No report/export tooling.

## Remaining Blockers

- Client-visible mission window result failed: raw captured QuestFullUpdate replay caused a hard client hang.
- A safe QuestFullUpdate sender is required before another mission-window test.
- The local AOtomation runtime quest DTOs still cannot safely deserialize/emit the captured B18C packet.
- Action `59` remains unresolved.
- `Quest Delete` gameplay meaning remains unresolved.
- Cargo Box identity and spawn/interact behavior remain unresolved and intentionally untouched.

## Next Implementation Step

Build a safe runtime QuestFullUpdate sender/body serializer for the captured B18C mission before attempting another live mission-window smoke. Do not re-enable raw captured frame replay.
