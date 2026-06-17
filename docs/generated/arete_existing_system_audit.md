# Arete Existing System Audit

Generated: 2026-06-15

Scope: framework audit only. No SQL, runtime behavior, quest implementation, NPC implementation, or game data generation was performed.

## Source Evidence Reviewed

- `tools-temp/arete-analysis/arete_extraction_summary.md`
- `tools-temp/arete-analysis/npc_list.json`
- `tools-temp/arete-analysis/dialogue_trees.json`
- `tools-temp/arete-analysis/quest_chains.json`
- `AORebirth/Server/ZoneEngine/Core/KnuBot/BaseKnuBot.cs`
- `AORebirth/Server/ZoneEngine/Core/KnuBot/KnuBotDialogTree.cs`
- `AORebirth/Server/ZoneEngine/Core/Controllers/NPCController.cs`
- `AORebirth/Server/ZoneEngine/Core/MessageHandlers/TradeMessageHandler.cs`
- `AORebirth/Server/ZoneEngine/Core/MessageHandlers/KnuBotOpenChatWindowMessageHandler.cs`
- `AORebirth/Server/ZoneEngine/Core/MessageHandlers/KnuBotAppendTextMessageHandler.cs`
- `AORebirth/Server/ZoneEngine/Core/MessageHandlers/KnuBotAnswerListMessageHandler.cs`
- `AORebirth/Server/ZoneEngine/Core/MessageHandlers/KnuBotAnswerMessageHandler.cs`
- `AORebirth/Server/ZoneEngine/Core/MessageHandlers/KnuBotCloseChatWindowMessageHandler.cs`
- `AORebirth/Server/ZoneEngine/Core/MessageHandlers/KnuBotStartTradeMessageHandler.cs`
- `AORebirth/Server/ZoneEngine/Core/MessageHandlers/KnuBotTradeMessageHandler.cs`
- `AORebirth/Server/ZoneEngine/Core/MessageHandlers/KnuBotFinishTradeMessageHandler.cs`
- `AORebirth/Server/ZoneEngine/Core/MessageHandlers/CharacterActionMessageHandler.cs`
- `AORebirth/Server/ZoneEngine/Core/MessageHandlers/GenericCmdMessageHandler.cs`
- `AORebirth/Server/ZoneEngine/Core/MessageHandlers/TemplateActionMessageHandler.cs`
- `AORebirth/Server/ZoneEngine/Core/MessageHandlers/FullCharacterMessageHandler.cs`
- `AORebirth/Server/ZoneEngine/Core/Playfields/Playfield.cs`
- `AORebirth/Server/ZoneEngine/Script/ScriptCompiler.cs`
- `AORebirth/Server/ZoneEngine/Scripts/InfoBot.cs`
- `AORebirth/Server/ZoneEngine/Scripts/KnuBotFlappy.cs`
- `AORebirth/Server/ZoneEngine/Scripts/KnuBotItemGiver.cs`
- `AORebirth/Server/ZoneEngine/ChatCommands/Npc.cs`
- `AORebirth/Libraries/Source/AORebirth.Database/Entities/DBMobSpawn.cs`
- `AORebirth/Libraries/Source/AORebirth.Database/Dao/MobSpawnDao.cs`
- `AORebirth/Libraries/Source/AORebirth.Enums/StatIds.cs`
- `AORebirth/Libraries/Source/AORebirth.Stats/Stats.cs`
- `AORebirth/Libraries/Source/AORebirth.Stats/StatNamesDefaults.cs`
- `AORebirth/Libraries/Source/AOtomation/AOtomation.Messaging/src/SmokeLounge.AOtomation.Messaging/Messages/N3Messages/CreateQuestMessage.cs`
- `AORebirth/Libraries/Source/AOtomation/AOtomation.Messaging/src/SmokeLounge.AOtomation.Messaging/Messages/N3Messages/QuestFullUpdateMessage.cs`
- `AORebirth/Libraries/Source/AOtomation/AOtomation.Messaging/src/SmokeLounge.AOtomation.Messaging/Messages/N3Messages/QuestAlternativeMessage.cs`
- `AORebirth/Libraries/Source/AOtomation/AOtomation.Messaging/src/SmokeLounge.AOtomation.Messaging/GameData/QuestInfo.cs`
- `AORebirth/Libraries/Source/AOtomation/AOtomation.Messaging/src/SmokeLounge.AOtomation.Messaging/GameData/QuestActionList.cs`
- `AORebirth/Libraries/Source/AOtomation/AOtomation.Messaging/src/SmokeLounge.AOtomation.Messaging/GameData/QuestCharInfo.cs`
- `AORebirth/Libraries/Source/AOtomation/AOtomation.Messaging/src/SmokeLounge.AOtomation.Messaging/GameData/QuestIdentity.cs`
- `AORebirth/Libraries/Source/AOtomation/AOtomation.Messaging/src/SmokeLounge.AOtomation.Messaging/Messages/N3Messages/CharacterActionMessage.cs`
- `AORebirth/Libraries/Source/AOtomation/AOtomation.Messaging/src/SmokeLounge.AOtomation.Messaging/Messages/N3Messages/CharacterActionType.cs`
- `AORebirth/Libraries/Source/AOtomation/AOtomation.Messaging/src/SmokeLounge.AOtomation.Messaging/Messages/N3Messages/GenericCmdMessage.cs`
- `AORebirth/Libraries/Source/AOtomation/AOtomation.Messaging/src/SmokeLounge.AOtomation.Messaging/Messages/N3Messages/GenericCmdAction.cs`
- `AORebirth/Libraries/Source/AOtomation/AOtomation.Messaging/src/SmokeLounge.AOtomation.Messaging/Messages/N3Messages/TemplateActionMessage.cs`

## Extraction Context

The Arete extraction contains 26 dialogue NPC groups, 48 mission groups, and 468 dialogue events. Rex Larsson is an identity-backed target:

- NPC: Rex Larsson
- Identity: `(SimpleChar:782DE568)`
- Folder: `20260614-194454`
- Dialogue events: 21 total, including 8 `KnuBotAnswerList` events and 7 `KnuBotAnswer` selections
- Observed missions for this slice: `(Mission:5514B18C)`, `(Mission:5514B18D)`, `(Mission:5514B18E)`

The extraction warns that KnuBot answer lists are visible branch/options evidence, not independently verified NPC spoken prompt text. Mission titles, objective text, and source NPC relationships for the first three missions are unavailable or uncertain in the allowed logs.

## Existing Dialogue And NPC Interaction Systems

| Location | Class names | Purpose | Reusable components | Limitations | Recommended changes |
| --- | --- | --- | --- | --- | --- |
| `AORebirth/Server/ZoneEngine/Core/KnuBot/BaseKnuBot.cs` | `BaseKnuBot` | Owns one active KnuBot conversation, opens the chat window, emits appended text, answer lists, trade windows, rejected items, and close-window messages. | Packet emission helpers: `OpenWindow`, `WriteLine`, `SendAnswerList`, `StartTrade`, `CloseChatWindow`. Conversation start/answer dispatch already exists. | Content is implemented by C# subclasses. Only one weak-referenced character is tracked. It has no data loader, mission state integration, content provenance, or explicit condition/action model. | Keep the packet-emission surface, but add a data-driven adapter that can execute captured dialogue definitions without creating one C# class per NPC. |
| `AORebirth/Server/ZoneEngine/Core/KnuBot/KnuBotDialogTree.cs` | `KnuBotDialogTree`, `KnuBotOptionId`, `KnuBotCondition`, `KnuBotAction`, `KnuBotActionStruct` | Represents a compiled dialogue tree using delegates and explicit next-node IDs. | Node validation, option index handling, `root`, `parent`, and `self` transitions are useful. | Conditions and actions are delegates tied to compiled script methods. The tree cannot load raw captured nodes directly and cannot attach structured condition/action metadata. | Use as a compatibility concept, not as the final Arete content format. A new data model should serialize node IDs, options, conditions, actions, and evidence IDs. |
| `AORebirth/Server/ZoneEngine/Core/Controllers/NPCController.cs` | `NPCController` | Controls NPC behavior. Holds `BaseKnuBot KnuBot`, starts KnuBot dialogue through `Trade`, and attaches KnuBot through `SetKnuBot`. | Existing NPC identity, target lookup, and KnuBot start path are directly reusable. | Dialogue is started through trade/open interaction only. `KnuBot` is a concrete script object, not a registry lookup. It has no quest-aware or data-driven session creation path. | Add an NPC dialogue resolver that checks registry data by NPC identity/script key and starts a data-backed dialogue session through the existing controller. |
| `AORebirth/Server/ZoneEngine/Core/MessageHandlers/TradeMessageHandler.cs` | `TradeMessageHandler` | Handles `TradeAction.Open`. When the target is an NPC, it calls `NPCController.Trade`. | Existing client-to-NPC interaction entry point is reusable for KnuBot dialogue. | Does not distinguish quest dialogue, vendor trade, and special NPC interactions beyond current target/controller checks. | Keep current path, but let `NPCController.Trade` or a small resolver choose between compiled KnuBot and data-backed dialogue content. |
| `AORebirth/Server/ZoneEngine/Core/MessageHandlers/KnuBotOpenChatWindowMessageHandler.cs` | `KnuBotOpenChatWindowMessageHandler` | Sends open chat window packets. | Reusable packet filler for opening KnuBot windows. | Outbound-only; no content/state logic. | Wrap behind a dialogue packet emitter interface so data-backed dialogue can reuse it. |
| `AORebirth/Server/ZoneEngine/Core/MessageHandlers/KnuBotAppendTextMessageHandler.cs` | `KnuBotAppendTextMessageHandler` | Sends appended KnuBot text. | Reusable for verified NPC prompt/body text when evidence exists. | Rex prompt bodies are not currently verified, so this should not be used to invent missing text. | Allow empty/no prompt emission when capture evidence lacks text. Emit only captured text. |
| `AORebirth/Server/ZoneEngine/Core/MessageHandlers/KnuBotAnswerListMessageHandler.cs` | `KnuBotAnswerListMessageHandler` | Sends visible answer options. | Directly reusable for captured Rex answer lists. | No condition filtering or evidence metadata. | Filter options in the data-backed session before calling this handler. |
| `AORebirth/Server/ZoneEngine/Core/MessageHandlers/KnuBotAnswerMessageHandler.cs` | `KnuBotAnswerMessageHandler` | Receives selected KnuBot answer indexes and calls `NPCController.KnuBot.Answer`. | Existing inbound answer dispatch is reusable. | Assumes every KnuBot NPC has a non-null compiled `BaseKnuBot`. No null guard, no data-backed dialogue session dispatch. | Route answer events through a dialogue-session registry. Fall back to current compiled KnuBot behavior for legacy scripts. |
| `AORebirth/Server/ZoneEngine/Core/MessageHandlers/KnuBotCloseChatWindowMessageHandler.cs` | `KnuBotCloseChatWindowMessageHandler` | Receives/sends close-window events. | Existing close semantics are reusable. | Assumes compiled KnuBot session. No data-backed cleanup hook. | Add session cleanup through the same registry used for answer dispatch. |
| `AORebirth/Server/ZoneEngine/Core/MessageHandlers/KnuBotStartTradeMessageHandler.cs` | `KnuBotStartTradeMessageHandler` | Sends KnuBot item hand-in trade window. | Useful for later dialogue actions that require item hand-in. | Finish/trade handling is incomplete and should not be used for Rex until verified by capture and implementation. | Keep out of the first Rex framework slice unless a captured, decoded item hand-in requires it. |
| `AORebirth/Server/ZoneEngine/Core/MessageHandlers/KnuBotTradeMessageHandler.cs` and `KnuBotFinishTradeMessageHandler.cs` | `KnuBotTradeMessageHandler`, `KnuBotFinishTradeMessageHandler` | Partial inbound trade-window support for KnuBot item hand-ins. | Could become an action integration point later. | `KnuBotFinishTradeMessageHandler` is TODO-only; `KnuBotTradeMessageHandler` removes inventory item but does not route to a quest/dialogue action. | Treat as out of scope for the first Rex framework slice. Do not depend on it for mission completion without a separate evidence and behavior pass. |
| `AORebirth/Server/ZoneEngine/Script/ScriptCompiler.cs` | `ScriptCompiler` | Discovers compiled scripts and creates KnuBot instances by class name. | Legacy script discovery and `CreateKnuBot` are reusable for existing content. | Requires compiled classes. Does not load JSON/data definitions. | Add a separate content loader/registry; do not overload `ScriptCompiler` with data content unless a narrow compatibility adapter is needed. |
| `AORebirth/Server/ZoneEngine/Scripts/InfoBot.cs`, `KnuBotFlappy.cs`, `KnuBotItemGiver.cs` | `InfoBot`, `InfoBotKnu`, `KnuBotFlappy`, `KnuBotItemGiver` | Example compiled KnuBot scripts. | Demonstrate current node/action/answer list patterns. | They hardcode text, branch logic, and actions in C# methods. | Use only as migration references. Do not implement Rex as a new hardcoded script for this phase. |
| `AORebirth/Server/ZoneEngine/Core/Playfields/Playfield.cs` | `Playfield.LoadMobSpawns` | Loads mob spawns and attaches compiled KnuBot scripts from `DBMobSpawn.KnuBotScriptName`. | Existing spawn-time attachment point is reusable. | Attaches only compiled KnuBot classes by script name. | Add a content-registry attachment path keyed by NPC identity and/or a non-schema content key. |
| `AORebirth/Libraries/Source/AORebirth.Database/Entities/DBMobSpawn.cs` | `DBMobSpawn` | Database entity for spawned NPCs, including `KnuBotScriptName`. | Stores a legacy KnuBot script reference. | Schema is not sufficient for data-backed content provenance or multiple content packs, and schema changes are out of scope. | For the first framework slice, avoid schema changes. Resolve Rex by identity/content file. A later approved schema pass can add durable content references if needed. |
| `AORebirth/Server/ZoneEngine/ChatCommands/Npc.cs` | `Npc` chat command | Can attach a compiled KnuBot script name to a saved NPC spawn. | Useful administrative precedent for linking NPCs to dialogue content. | Compiled-script only, writes `KnuBotScriptName`, not suitable for captured-data packs. | Do not use for Arete data loading yet. Add tooling later only after the data model is stable. |

## Existing Quest And Mission Systems

| Location | Class names | Purpose | Reusable components | Limitations | Recommended changes |
| --- | --- | --- | --- | --- | --- |
| `AORebirth/Libraries/Source/AOtomation/.../CreateQuestMessage.cs` | `CreateQuestMessage` | Serializes a create-quest packet with `QuestIdentity`. | Packet model can be used by a future mission emitter. | No ZoneEngine handler or mission service currently found. | Add a mission packet emitter only after behavior is verified against captures. |
| `AORebirth/Libraries/Source/AOtomation/.../QuestFullUpdateMessage.cs` | `QuestFullUpdateMessage` | Serializes `QuestInfo[]` full quest updates. | Model has enough shape to hold titles, rewards, actions, and identities if decoded evidence exists. | Many fields are unknown. There is no server-side content authoring or sender found in ZoneEngine. | Build a mission definition model first. Packet mapping should be a separate adapter with tests. |
| `AORebirth/Libraries/Source/AOtomation/.../QuestAlternativeMessage.cs` | `QuestAlternativeMessage` | Serializes mission-terminal alternatives. | Relevant to generated terminal missions, not the Rex NPC slice. | Not connected to Arete NPC mission flow. | Keep out of the Rex slice. |
| `AORebirth/Libraries/Source/AOtomation/.../QuestInfo.cs` | `QuestInfo` | Describes quest identity, short info, info, rewards, actions, character info, factions, and unknown fields. | Useful DTO for packet output once evidence is decoded. | Field semantics are incomplete. Rex titles/objectives are not available in allowed logs. | Do not fill unknown fields by guess. Use only decoded, captured fields. |
| `AORebirth/Libraries/Source/AOtomation/.../QuestActionList.cs` | `QuestActionList` | Represents quest action/location-style data inside `QuestInfo`. | Potential future bridge for objectives/actions. | Field names are mostly unknown. | Require a packet-aware decode pass before authoring mission objectives from it. |
| `AORebirth/Libraries/Source/AOtomation/.../QuestCharInfo.cs` and `QuestIdentity.cs` | `QuestCharInfo`, `QuestIdentity` | Supporting quest DTOs. | Potential packet serialization support. | No runtime lifecycle integration. | Keep as DTOs until a quest service exists. |
| `AORebirth/Libraries/Source/AOtomation/.../N3MessageType.cs` | `N3MessageType` | Defines `Quest`, `CreateQuest`, `QuestAlternative`, and `QuestFullUpdate` message IDs. | Confirms message IDs exist in AOtomation. | A `QuestMessage` class was not found in the searched AOtomation source, even though capture logs decode a `QuestMessage`. | Before implementing mission delete/complete packets, verify whether `QuestMessage` exists elsewhere, is generated dynamically, or needs a model. |
| `AORebirth/Server/ZoneEngine/Core/MessageHandlers/CharacterActionMessageHandler.cs` | `CharacterActionMessageHandler` | Handles many inbound/outbound character actions. Unknown/default actions are announced to the playfield. | Could become an event source for mission actions if a verified action code is added. | Captured Rex mission action is numeric `59`, but `CharacterActionType` in AOtomation does not name `0x3B`. No mission branch exists in this handler. | Do not guess action `59`. Add mission handling only after packet behavior is verified and named in a compatibility-safe way. |
| `AORebirth/Libraries/Source/AOtomation/.../CharacterActionMessage.cs` and `CharacterActionType.cs` | `CharacterActionMessage`, `CharacterActionType` | DTO and enum for character action packets. | Captured fields match mission evidence: action, target, parameter1, parameter2. | Action `59` is not defined in the enum. | Introduce explicit evidence-backed naming only after validation. Until then, the framework should store raw numeric action evidence separately. |
| `AORebirth/Server/ZoneEngine/Core/MessageHandlers/GenericCmdMessageHandler.cs` | `GenericCmdMessageHandler` | Handles generic use/get/drop actions, item use, statels, corpses, and event holders. | Could route quest-object interactions later. | Current behavior is object/event based, not mission-state based. No Arete quest-object interpretation is present. | Add mission-object action routing only when captured object evidence is decoded. |
| `AORebirth/Server/ZoneEngine/Core/MessageHandlers/TemplateActionMessageHandler.cs` | `TemplateActionMessageHandler` | Sends item template action packets, mostly inventory/equipment feedback. | Possible reward item emission helper later. | Not a quest system. | Keep reward item actions out of the first Rex slice until reward evidence is confirmed. |
| `AORebirth/Server/ZoneEngine/Core/MessageHandlers/FullCharacterMessageHandler.cs` | `FullCharacterMessageHandler` | Sends character full update, including mission bit stats. | Mission bit stats are sent to the client at login. | Mission bits alone are not a mission lifecycle system. No mapping exists between Arete mission IDs and bits. | Treat mission bits as a possible persistence primitive, not as the framework model. |
| `AORebirth/Libraries/Source/AORebirth.Stats/Stats.cs`, `StatNamesDefaults.cs`, `AORebirth.Enums/StatIds.cs` | `Stats`, `StatNamesDefaults`, `StatIds` | Defines `MissionBits1` through `MissionBits12` and related stat IDs. | Existing stats can preserve compact boolean mission flags if a mapping is verified. | No content mapping, no active quest collection, no mission chain metadata. | Add a mission state abstraction above stats. Map to mission bits only with evidence and tests. |

## Packet Handler Findings

- NPC interaction path exists through `TradeMessageHandler` -> `NPCController.Trade` -> `BaseKnuBot.StartDialog`.
- KnuBot answer and close packet handlers exist, but they assume a compiled `BaseKnuBot` instance on the NPC.
- Quest DTOs exist for `CreateQuest`, `QuestFullUpdate`, and `QuestAlternative`.
- No ZoneEngine `QuestFullUpdateMessageHandler`, `CreateQuestMessageHandler`, `QuestAlternativeMessageHandler`, or generic quest lifecycle handler was found.
- `N3MessageType.Quest` exists, but a `QuestMessage` DTO was not found in the searched AOtomation source. Captures decode `QuestMessage`, so this needs a packet-model audit before implementation.
- Captured Rex mission action `59` is not named in `CharacterActionType`; the framework should store it as raw evidence until verified.

## Can Existing Systems Support Arete With Only Configuration?

Not yet.

The existing dialogue system can display KnuBot windows and answer lists, but it expects compiled C# subclasses. The existing quest system has packet DTOs and mission bit stats, but no data-driven mission registry, mission state store, mission lifecycle service, or mission packet emission path.

Configuration alone is insufficient unless the configuration is backed by new loaders, registries, session tracking, and mission-state services.

## Minimum Recommended Change Set

The minimum framework should add documentation-backed, data-driven layers without changing gameplay behavior until the Rex implementation phase:

- Dialogue content definitions loaded from captured evidence.
- NPC dialogue registry keyed by NPC identity and optional content pack ID.
- Dialogue session store keyed by player identity and NPC identity.
- Condition evaluator that supports known mission state checks and leaves unresolved conditions disabled.
- Action executor that can offer/start/progress/complete missions only through evidence-backed actions.
- Mission definition registry keyed by mission identity.
- Mission state store per character with states such as unavailable, offered, active, ready-to-complete, completed, and removed.
- Packet emitter adapters that call existing KnuBot handlers and future quest packet handlers without embedding content in handlers.

## Audit Conclusion

AO Rebirth has useful KnuBot and packet primitives, but not a sufficient Arete-ready quest/dialogue framework. The next coding step should be a narrow data-driven framework scaffold, not hardcoded Rex content.
