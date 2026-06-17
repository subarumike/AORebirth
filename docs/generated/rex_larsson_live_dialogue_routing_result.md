# Rex Larsson Live Dialogue Routing Result

Generated: 2026-06-15

## Summary

Rex Larsson now has a narrow, disabled-by-default live dialogue routing adapter that can route only `SimpleChar:782DE568` in playfield `6553 Arete Landing` through the captured Arete dialogue content pack.

The adapter is gated by the environment variable:

```powershell
$env:AO_REBIRTH_ENABLE_ARETE_REX_DIALOGUE_ROUTING = '1'
```

When the gate is absent or false, the existing KnuBot route remains unchanged.

`6553 Arete Landing` is present in both the source and built playfield allow-list files:

- `AORebirth/Server/ZoneEngine/XML Data/Playfields.xml`
- `AORebirth/Built/Debug/XML Data/Playfields.xml`

## Files Inspected

- `AI_START_HERE.md`
- `AGENTS.md`
- `docs/ai/AI_START.md`
- `docs/ai/CURRENT_TASK.md`
- `docs/project/PROJECT_STATE.md`
- `docs/project/KNOWN_DECISIONS.md`
- `docs/project/ARCHITECTURE.md`
- `docs/ai/WORKFLOW.md`
- `docs/generated/rex_objective_playback_service_result.md`
- `docs/generated/rex_objective_event_semantics_result.md`
- `docs/generated/rex_questfullupdate_decoding_result.md`
- `docs/generated/rex_larsson_packet_semantics_result.md`
- `docs/generated/rex_larsson_inactive_content_pack_result.md`
- `AORebirth/Server/ZoneEngine/Content/Arete/rex-larsson/manifest.json`
- `AORebirth/Server/ZoneEngine/Content/Arete/rex-larsson/dialogue/rex-larsson.dialogue.json`
- `AORebirth/Server/ZoneEngine/Content/Arete/rex-larsson/quests/rex-larsson.quests.json`
- `AORebirth/Server/ZoneEngine/XML Data/Playfields.xml`
- `AORebirth/Built/Debug/XML Data/Playfields.xml`
- `AORebirth/Server/ZoneEngine/Core/Controllers/NPCController.cs`
- `AORebirth/Server/ZoneEngine/Core/MessageHandlers/TradeMessageHandler.cs`
- `AORebirth/Server/ZoneEngine/Core/MessageHandlers/KnuBotAnswerMessageHandler.cs`
- `AORebirth/Server/ZoneEngine/Core/MessageHandlers/KnuBotCloseChatWindowMessageHandler.cs`
- `AORebirth/Server/ZoneEngine/Core/MessageHandlers/KnuBotOpenChatWindowMessageHandler.cs`
- `AORebirth/Server/ZoneEngine/Core/MessageHandlers/KnuBotAnswerListMessageHandler.cs`
- `AORebirth/Server/ZoneEngine/Core/MessageHandlers/KnuBotAppendTextMessageHandler.cs`
- `AORebirth/Server/ZoneEngine/Core/KnuBot/BaseKnuBot.cs`
- `AORebirth/Server/ZoneEngine/Core/KnuBot/KnuBotDialogTree.cs`
- `AORebirth/Server/ZoneEngine/Core/Arete/Dialogue/DialogueSessionService.cs`
- `AORebirth/Server/ZoneEngine/Core/Arete/Dialogue/DialogueSessionModels.cs`
- `AORebirth/Server/ZoneEngine/Core/Arete/Dialogue/DialogueContentRegistry.cs`
- `AORebirth/Server/ZoneEngine/Core/Arete/Dialogue/DialogueContentPackLoader.cs`
- `AORebirth/Server/ZoneEngine/Core/Arete/Dialogue/DialogueModels.cs`
- `AORebirth/Server/ZoneEngine/Core/Arete/AreteAggregateContentValidator.cs`
- `AORebirth/Server/ZoneEngine/Core/Arete/AreteContentManifestLoader.cs`
- `AORebirth/Server/ZoneEngine/ZoneEngine.csproj`
- `AORebirth/Libraries/Source/AOtomation/AOtomation.Messaging/src/SmokeLounge.AOtomation.Messaging/GameData/Identity.cs`
- `AORebirth/Libraries/Source/AOtomation/AOtomation.Messaging/src/SmokeLounge.AOtomation.Messaging/GameData/IdentityType.cs`
- `AORebirth/Libraries/Source/Utility/LogUtil.cs`
- `tools-temp/arete-framework-validation/Run-AreteFrameworkValidation.ps1`
- `tools-temp/arete-framework-validation/Run-RexLarssonContentDryRun.ps1`

## Files Changed

- `AORebirth/Server/ZoneEngine/Core/Arete/Dialogue/AreteRexDialogueRouter.cs`
- `AORebirth/Server/ZoneEngine/Core/Controllers/NPCController.cs`
- `AORebirth/Server/ZoneEngine/Core/MessageHandlers/KnuBotAnswerMessageHandler.cs`
- `AORebirth/Server/ZoneEngine/Core/MessageHandlers/KnuBotCloseChatWindowMessageHandler.cs`
- `AORebirth/Server/ZoneEngine/XML Data/Playfields.xml`
- `AORebirth/Server/ZoneEngine/ZoneEngine.csproj`
- `docs/ai/CURRENT_TASK.md`
- `docs/project/PROJECT_STATE.md`
- `docs/generated/rex_larsson_live_dialogue_routing_result.md`

## Existing Live Route Found

The current live NPC dialogue path is:

- Client `TradeAction.Open` reaches `TradeMessageHandler`.
- `TradeMessageHandler` resolves the target NPC and calls `NPCController.Trade(message.Identity)`.
- `NPCController.Trade` starts an attached KnuBot with `BaseKnuBot.StartDialog`.
- `BaseKnuBot.StartDialog` opens the KnuBot chat window, then sends answer-list options.
- `KnuBotAnswerMessageHandler` routes selected options back to the target NPC KnuBot.
- `KnuBotCloseChatWindowMessageHandler` routes client close back to the target NPC KnuBot as `WindowClosed`.

## Safety Gate

Added disabled-by-default gate:

```text
AO_REBIRTH_ENABLE_ARETE_REX_DIALOGUE_ROUTING
```

Truthy values are `1`, `true`, `yes`, or `on`.

No `Config.xml` edits were made. No DB config, schema, SQL, or source data was changed.

## Rex-Only Routing Behavior

When enabled, the target identity is Rex Larsson, and the available playfield context is `6553`:

- `NPCController.Trade` calls `AreteRexDialogueRouter.TryStartDialogue`.
- The router verifies Rex and the source character are in `6553 Arete Landing` before routing.
- The router resolves the player character from the same playfield.
- The router loads `AORebirth/Server/ZoneEngine/Content/Arete/rex-larsson/manifest.json`.
- Existing aggregate validation is run against the manifest before the dialogue registry is used.
- The existing `DialogueSessionService` starts an in-memory session for `SimpleChar:782DE568`.
- Existing KnuBot packet helpers open the dialogue window and send captured answer-list options.
- `KnuBotAnswerMessageHandler` advances the in-memory dialogue session for Rex only while the player is still in playfield `6553`.
- `KnuBotCloseChatWindowMessageHandler` clears the in-memory Rex session for that player.

The route does not call the mission action adapter, objective playback service, quest packet handlers, KnuBot scripts, inventory code, XP/credit code, DB code, or character stat mutation code.

## Client Display

With the gate enabled, opening Rex should show the captured root options from the Rex content pack:

- `I don't really feel like telling you any of my secrets. If you'll excuse me, I need to go now.`
- `Goodbye`

Selecting captured options advances through the captured answer-list nodes. Nodes with no options or explicit close/end transitions close the KnuBot window.

## Validation Result

Focused ZoneEngine build passed:

```powershell
& 'C:\Program Files\Microsoft Visual Studio\18\Community\MSBuild\Current\Bin\MSBuild.exe' 'AORebirth\Server\ZoneEngine\ZoneEngine.csproj' /t:Build /p:Configuration=Debug /p:BuildProjectReferences=false /p:GenerateSerializationAssemblies=Off /m:1 /nr:false /p:UseSharedCompilation=false /v:minimal
```

Existing Arete validation harness passed:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File 'tools-temp\arete-framework-validation\Run-AreteFrameworkValidation.ps1' -SkipBuild
```

Result:

```text
[PASS] Arete framework validation harness passed 131 cases.
```

Rex aggregate validation and dry-run passed:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File 'tools-temp\arete-framework-validation\Run-RexLarssonContentDryRun.ps1' -SkipBuild
```

Key result:

```text
[PASS] Rex aggregate validation passed.
[PASS] Rex Larsson inactive content dry-run passed.
Mission transitions executed: 0 (captured mission action meanings remain uncertain).
```

Source and built playfield allow-lists both include:

```text
<Playfield id="6553" expansion="0" disabled="false" x="100000" xscale="1.00000" z="100000" zscale="1.00000">
  <Name>Arete Landing</Name>
</Playfield>
```

`git diff --check` passed with normal LF-to-CRLF working-copy warnings.

## Manual Smoke Instructions

From a PowerShell session that will start ZoneEngine:

```powershell
cd C:\Users\Mike\Documents\AORebirth
$env:AO_REBIRTH_ENABLE_ARETE_REX_DIALOGUE_ROUTING = '1'
.\stop-engines.ps1
.\start-engines.ps1
```

Then:

- Log in locally.
- Teleport to Arete Landing:

```text
/tp 300 300 6553
```

- Trade/talk to Rex Larsson.
- Confirm the first answer list shows the two captured root options listed above.
- Select captured options and confirm options advance or close without quest windows, quest updates, rewards, inventory changes, XP/credit changes, or errors.

To return to default behavior:

```powershell
Remove-Item Env:\AO_REBIRTH_ENABLE_ARETE_REX_DIALOGUE_ROUTING -ErrorAction SilentlyContinue
.\stop-engines.ps1
.\start-engines.ps1
```

Manual live smoke was not run in this pass because it requires an interactive AO client session. Build, aggregate validation, framework harness validation, and Rex dry-run all passed.

## Remaining Disabled Behavior

- Mission offer/accept/complete execution remains disabled.
- Objective progress execution remains disabled in live routing.
- Quest packet emission remains disabled.
- Rewards, inventory, XP, credits, mission bits, DB writes, and character stat mutation remain disabled.
- Action `59` remains unresolved and uninterpreted.
- `Quest Delete` gameplay meaning remains unresolved and uninterpreted.
- Cargo Box exact identity remains unresolved.
- Routing remains Rex-only and disabled by default.

## Risks

- The route uses the existing KnuBot open/list/close packet helpers but has not yet been verified with an interactive client session.
- Rex root prompt text remains unavailable in captured evidence, so the first visible client content is the captured answer-list options.
- If the gate is enabled and the Rex manifest fails validation or cannot be found, the adapter logs the failure and falls back to existing KnuBot behavior.

## Next Recommended Phase

Run the manual local smoke with the gate enabled, capture ZoneEngine logs, and confirm that the client shows captured Rex options without any quest packet emission or character mutation.
