# Rex Safe QuestFullUpdate Sender Result

Generated: 2026-06-17

## Summary

Implemented a safe, DTO/serializer-based `QuestFullUpdate` sender for Rex Larsson `Mission:5514B18C`.

The previous raw captured packet replay remains blocked. The new sender constructs the B18C mission display packet from decoded evidence and sends it through the normal `ZoneClient.SendCompressed(MessageBody)` path so the live server supplies current header framing, sender, receiver, packet numbering, and compression.

The sender remains behind both disabled-by-default gates:

```powershell
$env:AO_REBIRTH_ENABLE_ARETE_REX_DIALOGUE_ROUTING = '1'
$env:AO_REBIRTH_ENABLE_ARETE_REX_QUEST_PREVIEW = '1'
```

No completion, reward, inventory, XP, credit, DB, Quest Delete, Cargo Box, mission-state, or action `59` behavior was added.

## Files Inspected

- `AI_START_HERE.md`
- `AGENTS.md`
- `docs/ai/AI_START.md`
- `docs/ai/CURRENT_TASK.md`
- `docs/project/PROJECT_STATE.md`
- `docs/generated/rex_b18c_quest_preview_result.md`
- `docs/generated/rex_questfullupdate_decoding_result.md`
- `docs/generated/rex_larsson_packet_semantics_result.md`
- `docs/generated/rex_objective_event_semantics_result.md`
- `docs/generated/npc_dialogue_content_architecture_result.md`
- `AORebirth/Server/ZoneEngine/Core/Arete/Quests/RexQuestPreviewEmitter.cs`
- `AORebirth/Server/ZoneEngine/Content/Arete/rex-larsson/quests/rex-larsson.quests.json`
- `AORebirth/Server/ZoneEngine/Content/Arete/rex-larsson/dialogue/rex-larsson.dialogue.json`
- `AORebirth/Libraries/Source/AOtomation/AOtomation.Messaging/src/SmokeLounge.AOtomation.Messaging/Messages/N3Messages/QuestFullUpdateMessage.cs`
- `AORebirth/Libraries/Source/AOtomation/AOtomation.Messaging/src/SmokeLounge.AOtomation.Messaging/Messages/N3Messages/CreateQuestMessage.cs`
- `AORebirth/Libraries/Source/AOtomation/AOtomation.Messaging/src/SmokeLounge.AOtomation.Messaging/Messages/N3Messages/QuestAlternativeMessage.cs`
- `AORebirth/Libraries/Source/AOtomation/AOtomation.Messaging/src/SmokeLounge.AOtomation.Messaging/GameData/QuestInfo.cs`
- `AORebirth/Libraries/Source/AOtomation/AOtomation.Messaging/src/SmokeLounge.AOtomation.Messaging/GameData/QuestActionList.cs`
- `AORebirth/Libraries/Source/AOtomation/AOtomation.Messaging/src/SmokeLounge.AOtomation.Messaging/Serialization/MessageSerializer.cs`
- `AORebirth/Libraries/Source/AOtomation/AOtomation.Messaging/src/SmokeLounge.AOtomation.Messaging/Serialization/SerializerResolverBuilder.cs`
- `AORebirth/Libraries/Source/AOtomation/AOtomation.Messaging/src/SmokeLounge.AOtomation.Messaging/Serialization/Serializers/StringSerializer.cs`
- `AORebirth/Libraries/Source/AOtomation/AOtomation.Messaging/src/SmokeLounge.AOtomation.Messaging/Serialization/StreamWriter.cs`
- `AORebirth/Server/ZoneEngine/Core/ZoneClient.cs`
- `tools-temp/external/aosharp-github/AOSharp.Common/SmokeLounge/AOtomation/Messaging/GameData/Quest.cs`
- `tools-temp/external/aosharp-github/AOSharp.Common/SmokeLounge/AOtomation/Messaging/GameData/QuestAction.cs`
- `tools-temp/external/aosharp-github/AOSharp.Common/SmokeLounge/AOtomation/Messaging/Messages/N3Messages/QuestFullUpdateMessage.cs`
- `tools-temp/arete-analysis/scripts/decode_rex_questfullupdate.ps1`
- `tools-temp/AOSharpLiveCapture/bin/Debug/captures/20260614-194454/packets.hex.log`
- `tools-temp/AOSharpLiveCapture/bin/Debug/captures/20260614-194454/events.log`
- `tools-temp/arete-framework-validation/Run-AreteFrameworkValidation.ps1`
- `tools-temp/arete-framework-validation/Run-RexLarssonContentDryRun.ps1`

## Files Changed

- `AORebirth/Libraries/Source/AOtomation/AOtomation.Messaging/src/SmokeLounge.AOtomation.Messaging/GameData/CharacterInfo.cs`
- `AORebirth/Libraries/Source/AOtomation/AOtomation.Messaging/src/SmokeLounge.AOtomation.Messaging/GameData/MissionItemReward.cs`
- `AORebirth/Libraries/Source/AOtomation/AOtomation.Messaging/src/SmokeLounge.AOtomation.Messaging/GameData/Quest.cs`
- `AORebirth/Libraries/Source/AOtomation/AOtomation.Messaging/src/SmokeLounge.AOtomation.Messaging/GameData/QuestActionInfo.cs`
- `AORebirth/Libraries/Source/AOtomation/AOtomation.Messaging/src/SmokeLounge.AOtomation.Messaging/Messages/N3Messages/QuestFullUpdateMessage.cs`
- `AORebirth/Libraries/Source/AOtomation/AOtomation.Messaging/src/SmokeLounge.AOtomation.Messaging/Serialization/SerializerResolverBuilder.cs`
- `AORebirth/Libraries/Source/AOtomation/AOtomation.Messaging/src/SmokeLounge.AOtomation.Messaging/Serialization/Serializers/Custom/QuestFullUpdateMessageSerializer.cs`
- `AORebirth/Libraries/Source/AOtomation/AOtomation.Messaging/src/SmokeLounge.AOtomation.Messaging/SmokeLounge.AOtomation.Messaging.csproj`
- `AORebirth/Server/ZoneEngine/Core/Arete/Quests/RexQuestPreviewEmitter.cs`
- `AORebirth/Server/ZoneEngine/Core/Arete/Quests/SafeQuestFullUpdateSender.cs`
- `AORebirth/Server/ZoneEngine/ZoneEngine.csproj`
- `docs/ai/CURRENT_TASK.md`
- `docs/project/PROJECT_STATE.md`
- `docs/generated/rex_safe_questfullupdate_sender_result.md`

## Why Raw Replay Was Unsafe

The raw replay path used captured wire bytes through the byte-array send path. That path bypassed normal `MessageBody` serialization and carried captured packet body assumptions from the live session.

Evidence:

- Manual smoke on 2026-06-17 hard-hung the client when the raw captured B18C `QuestFullUpdate` was emitted.
- Tool-side AOSharp decoded captured B18C cleanly, but the local runtime `QuestInfo` model did not match the captured `QuestFullUpdate` body shape.
- AOSharp uses a `Quest` body with null-terminated `ShortInfo`, length-prefixed-plus-null `LongInfo`, and a different field sequence from local `QuestInfo`.
- The safe sender now uses normal `ZoneClient.SendCompressed(MessageBody)` framing and a recovered body serializer instead of captured raw frame replay.

## Safe Sender Behavior

Added `SafeQuestFullUpdateSender`.

Behavior:

- Supports only `Mission:5514B18C`.
- Builds a `QuestFullUpdateMessage` with one decoded `Quest`.
- Uses the current character identity for the message identity, `UnknownId2`, `PlayerIds`, and `PlayerIds2`.
- Uses evidence-backed Rex and B18C constants from packet `#2757`.
- Sends through `source.Controller.Client.SendCompressed(message)`.
- Logs before send and logs any serialization/send failure.
- Fails closed if the source character, client, or identity is missing.

The sender does not emit:

- `CreateQuest`
- `QuestAlternative`
- `Quest Delete`
- rewards
- inventory updates
- XP or credit updates
- DB writes
- mission-state transitions
- completion packets

## Emitter Changes

`RexQuestPreviewEmitter` still enforces:

- Rex-only target identity `SimpleChar:782DE568`.
- Arete-only playfield `6553`.
- Captured B18C dialogue trigger: node `rex_194454_004`, answer index `0`.
- Dialogue gate enabled.
- Quest-preview gate enabled.

When all checks pass, it calls `SafeQuestFullUpdateSender.TrySendB18CPreview(source)`.

Raw captured packet emission remains removed.

## Serialization Verification

A local serialization check built the B18C message from `SafeQuestFullUpdateSender.CreateB18CPreviewMessage` using the captured player identity/header values from packet `#2757`.

Result:

- Actual serialized length: `1031`
- Captured packet length: `1031`
- First byte difference: `-1`
- Conclusion: with captured header values, the DTO-built packet matches `tools-temp/AOSharpLiveCapture/bin/Debug/captures/20260614-194454/packets.hex.log` packet `#2757` byte-for-byte.

Live sending will still use current AO Rebirth server framing instead of captured sender/packet numbering.

## Validation

- `SmokeLounge.AOtomation.Messaging` focused build: passed.
- `ZoneEngine` focused build with project references disabled: passed.
- Arete validation harness: passed 131 cases.
- Rex inactive content dry-run: passed.
- B18C DTO serialization comparison against captured packet `#2757`: passed byte-for-byte with captured header values.

`git diff --check` passed with normal LF-to-CRLF working-copy warnings.

## Manual Smoke Instructions

Enable both Rex gates only for the controlled smoke:

```powershell
cd C:\Users\Mike\Documents\AORebirth
$env:AO_REBIRTH_ENABLE_ARETE_REX_DIALOGUE_ROUTING = '1'
$env:AO_REBIRTH_ENABLE_ARETE_REX_QUEST_PREVIEW = '1'
.\start-engines.ps1
```

In the client:

```text
/tp 3624.599 787.7465 51.745 6553
```

Talk to Rex and advance:

1. `I don't really feel like telling you any of my secrets. If you'll excuse me, I need to go now.`
2. `Who said I don't have any ID?`
3. `Get to the point, Rex.`
4. `I'll do it if you promise to tell me who your contact is.`

Expected:

- Client should not hang.
- Rex should advance to `Excellent choice...`.
- B18C should appear in the mission window if the recovered DTO body is accepted by the client.
- No mission completion, rewards, inventory changes, XP/credits, DB writes, Quest Delete, or Cargo Box behavior should occur.

Restore default gate-off state after smoke:

```powershell
Remove-Item Env:\AO_REBIRTH_ENABLE_ARETE_REX_DIALOGUE_ROUTING -ErrorAction SilentlyContinue
Remove-Item Env:\AO_REBIRTH_ENABLE_ARETE_REX_QUEST_PREVIEW -ErrorAction SilentlyContinue
.\stop-engines.ps1
.\start-engines.ps1
```

## Manual Smoke Status

Engines were started after the build with both Rex gates enabled:

- `ChatEngine.exe`
- `LoginEngine.exe`
- `ZoneEngine.exe`

In-client mission-window result is pending user smoke.

## Remaining Unknowns

- Manual in-client smoke is still required.
- Action `59` remains unresolved.
- `Quest Delete` gameplay meaning remains unresolved.
- Mission completion, objective execution, and mission-state persistence remain disabled.
- Cargo Box identity and live spawn/interact behavior remain unresolved.

## Next Step

Run the controlled live smoke with both Rex gates enabled. If the client remains stable and B18C appears, keep the sender gated and proceed to the next evidence-backed phase: mission display/state observation only, still without completion or rewards.
