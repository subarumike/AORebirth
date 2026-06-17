# NPC Dialogue Content Architecture Result

Generated: 2026-06-16

## Summary

NPC dialogue now has a small shared routing shape for two supported content sources:

- Legacy scripted KnuBot dialogue attached from spawn data by `KnuBotScriptName`.
- JSON content-pack-driven dialogue registered in code and displayed through the existing KnuBot packet UI.

Rex Larsson remains the only registered content-driven NPC. The route is still disabled by default and still uses the existing gate:

```powershell
$env:AO_REBIRTH_ENABLE_ARETE_REX_DIALOGUE_ROUTING = '1'
```

No quest packets, mission execution, rewards, inventory changes, XP/credits, DB writes, mission bits, action `59` interpretation, or `Quest Delete` interpretation were added.

## Files Inspected

- `AI_START_HERE.md`
- `AGENTS.md`
- `docs/ai/AI_START.md`
- `docs/ai/CURRENT_TASK.md`
- `docs/project/PROJECT_STATE.md`
- `docs/project/KNOWN_DECISIONS.md`
- `docs/project/ARCHITECTURE.md`
- `docs/ai/WORKFLOW.md`
- `docs/generated/rex_larsson_live_dialogue_routing_result.md`
- `docs/generated/rex_objective_playback_service_result.md`
- `docs/generated/rex_objective_event_semantics_result.md`
- `docs/generated/rex_questfullupdate_decoding_result.md`
- `docs/generated/rex_larsson_inactive_content_pack_result.md`
- `docs/generated/rex_live_dialogue_option_progression_result.md`
- `AORebirth/Server/ZoneEngine/Core/Controllers/NPCController.cs`
- `AORebirth/Server/ZoneEngine/Core/MessageHandlers/TradeMessageHandler.cs`
- `AORebirth/Server/ZoneEngine/Core/MessageHandlers/KnuBotAnswerMessageHandler.cs`
- `AORebirth/Server/ZoneEngine/Core/MessageHandlers/KnuBotCloseChatWindowMessageHandler.cs`
- `AORebirth/Server/ZoneEngine/Core/MessageHandlers/AttackMessageHandler.cs`
- `AORebirth/Server/ZoneEngine/Core/KnuBot/BaseKnuBot.cs`
- `AORebirth/Server/ZoneEngine/Core/KnuBot/KnuBotDialogTree.cs`
- `AORebirth/Server/ZoneEngine/Core/Playfields/Playfield.cs`
- `AORebirth/Libraries/Source/AORebirth.Database/Entities/DBMobSpawn.cs`
- `AORebirth/Server/ZoneEngine/Core/Arete/Dialogue/AreteRexDialogueRouter.cs`
- `AORebirth/Server/ZoneEngine/Core/Arete/Dialogue/DialogueContentRegistry.cs`
- `AORebirth/Server/ZoneEngine/Core/Arete/Dialogue/DialogueSessionService.cs`
- `AORebirth/Server/ZoneEngine/Core/Arete/AreteAggregateContentValidator.cs`
- `AORebirth/Server/ZoneEngine/Core/Arete/AreteContentManifestLoader.cs`
- `AORebirth/Server/ZoneEngine/Content/Arete/rex-larsson/manifest.json`
- `AORebirth/Server/ZoneEngine/Content/Arete/rex-larsson/dialogue/rex-larsson.dialogue.json`
- `AORebirth/Server/ZoneEngine/Content/Arete/rex-larsson/quests/rex-larsson.quests.json`
- `tools-temp/sql-staging/rex_larsson_mobspawn.sql`
- `tools-temp/arete-framework-validation/Run-RexLarssonContentDryRun.ps1`
- `tools-temp/arete-framework-validation/Run-AreteFrameworkValidation.ps1`

## Files Changed

- `AORebirth/Server/ZoneEngine/Core/Arete/Dialogue/ContentDrivenNpcDialogueRouter.cs`
- `AORebirth/Server/ZoneEngine/Core/Arete/Dialogue/AreteRexDialogueRouter.cs`
- `AORebirth/Server/ZoneEngine/Core/Controllers/NPCController.cs`
- `AORebirth/Server/ZoneEngine/Core/MessageHandlers/TradeMessageHandler.cs`
- `AORebirth/Server/ZoneEngine/Core/MessageHandlers/KnuBotAnswerMessageHandler.cs`
- `AORebirth/Server/ZoneEngine/Core/MessageHandlers/KnuBotCloseChatWindowMessageHandler.cs`
- `AORebirth/Server/ZoneEngine/Core/MessageHandlers/AttackMessageHandler.cs`
- `AORebirth/Server/ZoneEngine/ZoneEngine.csproj`
- `docs/ai/CURRENT_TASK.md`
- `docs/project/PROJECT_STATE.md`
- `docs/generated/npc_dialogue_content_architecture_result.md`

## Legacy Scripted KnuBot Path

Legacy NPC dialogue is still spawn-script driven:

- `DBMobSpawn.KnuBotScriptName` carries the legacy script name.
- `Playfield.LoadMobSpawns` instantiates NPCs and, when `KnuBotScriptName` is non-empty, attaches a compiled `BaseKnuBot` through `NPCController.SetKnuBot`.
- `NPCController.Trade` calls `BaseKnuBot.StartDialog` when no content-driven route has claimed the interaction.
- `BaseKnuBot` and `KnuBotDialogTree` own compiled C# dialogue content, conditions, and actions.
- `KnuBotAnswerMessageHandler` and `KnuBotCloseChatWindowMessageHandler` continue routing answer/close packets to the attached legacy KnuBot when content-driven routing does not handle them.

This path remains the default for all NPCs, including Rex when the content-driven gate is disabled.

## Content-Driven JSON Path

New captured-content NPCs use a registration plus JSON manifest:

- `ContentDrivenNpcDialogueRouter` owns the shared live route.
- The router has a narrow in-code registration table.
- The only current registration is Rex Larsson:
  - NPC: `Rex Larsson`
  - Identity: `SimpleChar:782DE568`
  - Playfield: `6553 Arete Landing`
  - Gate: `AO_REBIRTH_ENABLE_ARETE_REX_DIALOGUE_ROUTING`
  - Manifest: `AORebirth/Server/ZoneEngine/Content/Arete/rex-larsson/manifest.json`
- The router validates the manifest through the existing aggregate validator, loads the dialogue pack into `DialogueContentRegistry`, starts an in-memory `DialogueSessionService` session, and displays the current node through the existing KnuBot open/append-text/answer-list/close packet helpers.
- Dialogue actions remain no-op recorded actions. They are not executed against character, inventory, DB, quest, reward, XP, credit, or stat systems.

`AreteRexDialogueRouter` now exists as a compatibility wrapper over `ContentDrivenNpcDialogueRouter`. Live call sites use the shared router directly.

## Routing Decision Order

The routing order is intentionally conservative:

1. Client `TradeAction.Open` reaches `TradeMessageHandler`.
2. `TradeMessageHandler` asks `ContentDrivenNpcDialogueRouter.TryStartDialogueForTarget` first.
3. If the target NPC is not registered, the request falls through to existing player trade and legacy NPC trade behavior.
4. If the registered NPC gate is disabled, the request falls through to the legacy path.
5. If playfield context is present and does not match the registration, the request falls through to the legacy path.
6. If the manifest is missing or validation/loading fails, the router logs the failure and returns false so legacy behavior can continue.
7. If the content session starts, the router opens the KnuBot UI and sends content-pack-backed prompt/options.
8. KnuBot answer and close packet handlers ask the shared router first. If no active registered content session exists, they continue to the attached legacy KnuBot.

The same registered route is also used by `NPCController.Trade` so direct controller calls and trade-message calls make the same decision.

## Spawn Data, Content JSON, and Routing Code

Spawn data should contain only spawn/runtime identity facts:

- NPC identity/id
- playfield and coordinates
- visual/stat data needed to spawn the character
- legacy `KnuBotScriptName` only for compiled KnuBot scripts

Content JSON should contain content evidence:

- dialogue pack identity
- NPC dialogue identity and node graph
- captured prompt text and visible options
- conditions/actions as data
- quest/objective metadata where captured and still non-executable
- uncertainty and raw evidence references

Routing code should contain only routing policy:

- registered NPC identity
- optional playfield restriction
- disabled-by-default gate
- manifest path
- fallback behavior
- KnuBot packet UI reuse

Routing code must not contain dialogue text, quest text, objectives, rewards, mission semantics, or future NPC-specific hardcoding beyond the registration entry.

## Rex as First Content-Driven NPC

Rex Larsson is the first and only content-driven NPC registration. The implementation preserves the prior Rex behavior:

- Rex is detected by `SimpleChar:782DE568`.
- Rex routing is limited to playfield `6553`.
- Rex content loads from the existing manifest.
- Rex dialogue uses the existing KnuBot UI packet path.
- Rex answer clicks advance only the in-memory dialogue session.
- Rex combat suppression still applies only while the gate is enabled and the target is the registered Rex in the expected playfield.
- Rex objective playback dry-run remains separate and inactive.

## Future Arete Additions

Future content-driven NPCs should:

- Add a registration entry only after captured content exists and validates.
- Use a dedicated manifest path under the content tree.
- Keep identity and playfield restrictions explicit.
- Keep gates disabled by default until manual smoke verifies the route.
- Reuse `ContentDrivenNpcDialogueRouter` rather than adding another NPC-specific router.
- Preserve legacy KnuBot fallback unless a specific migration is approved.

Future content-driven NPCs must not:

- Rely on broad automatic discovery.
- Guess dialogue, quest, reward, objective, action, or packet semantics.
- Add executable mission actions before packet semantics are proven.
- Emit quest packets from the dialogue router.
- Mutate character state, inventory, XP, credits, mission bits, DB rows, or stats from this routing layer.
- Convert legacy KnuBot scripts as a side effect of adding captured JSON content.

## Manual Rex Smoke Instructions

Enable the route in the PowerShell session that starts ZoneEngine:

```powershell
cd C:\Users\Mike\Documents\AORebirth
$env:AO_REBIRTH_ENABLE_ARETE_REX_DIALOGUE_ROUTING = '1'
.\stop-engines.ps1
.\start-engines.ps1
```

Then in the local client:

```text
/tp 300 300 6553
```

Talk/trade with Rex Larsson. Expected visible behavior:

- Rex faces the player.
- The KnuBot dialogue window opens.
- The captured Rex root options appear.
- Selecting captured options advances to the next captured answer-list node or closes the window.
- No quest windows, quest updates, rewards, inventory changes, XP/credit changes, mission bit changes, DB writes, or stat mutations should occur.

Restore default behavior:

```powershell
Remove-Item Env:\AO_REBIRTH_ENABLE_ARETE_REX_DIALOGUE_ROUTING -ErrorAction SilentlyContinue
.\stop-engines.ps1
.\start-engines.ps1
```

## Validation

- Focused ZoneEngine build: passed.
- Rex content dry-run: passed.
- Arete validation harness: passed 131 cases.
- `git diff --check`: passed with normal LF-to-CRLF working-copy warnings.

## Risks

- The registration table is intentionally narrow and in-code. That is enough for Rex and prevents accidental broad routing, but later content expansion may need a reviewed manifest index.
- The content-driven route still needs manual in-client smoke after this standardization pass.
- The root architecture now supports content-driven NPC additions, but mission execution remains deliberately absent.

## Next Recommended Phase

Run the gated Rex in-client smoke against the shared router. After that passes, the next implementation phase should stay focused on one proven missing semantic at a time, most likely content-backed condition selection for already captured Rex dialogue states, still without quest packet emission or rewards.
