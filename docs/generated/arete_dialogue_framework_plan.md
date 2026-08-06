# Arete Dialogue Framework Plan

Generated: 2026-06-15

Scope: architecture plan only. This document does not implement dialogue, generate NPC content, create SQL, or change runtime behavior.

## Design Goal

Represent captured Arete NPC dialogue as data that can be loaded, validated, and executed without hardcoding individual NPC scripts. The first target is Rex Larsson, but the framework must be generic enough for the rest of Arete.

The plan uses only captured evidence. Missing NPC prompt bodies remain missing. The captured `KnuBotAnswerList` text is treated as visible answer-option evidence, not proof of NPC spoken text.

## Existing Reusable Pieces

- `BaseKnuBot` can open/close KnuBot windows and send text, answer lists, and KnuBot trade windows.
- `KnuBotAnswerListMessageHandler` can emit visible answer options.
- `KnuBotAnswerMessageHandler` receives selected answer indexes.
- `KnuBotCloseChatWindowMessageHandler` receives and sends close events.
- `NPCController.Trade` already starts KnuBot dialogue when an NPC has attached KnuBot content.
- `TradeMessageHandler` routes `TradeAction.Open` to the NPC controller.

These pieces should stay as packet/session primitives. Arete content should sit above them as data and session state.

## Proposed Components

| Component | Purpose | Responsibilities | Dependencies | Integration points |
| --- | --- | --- | --- | --- |
| `DialogueContentPack` | A loaded group of captured dialogue definitions. | Store pack ID, source capture folders, generated timestamp, confidence, NPC definitions, and validation errors. | File loader, JSON model, evidence metadata. | ZoneEngine startup or playfield-load content registry. |
| `NpcDialogueDefinition` | Data definition for one NPC's dialogue. | Map NPC identity, name, aliases, playfield hints, root node, and evidence source. | `DialogueNodeDefinition`, `Identity`. | NPC dialogue registry keyed by `(SimpleChar:instance)` and optional content key. |
| `DialogueNodeDefinition` | One dialogue state. | Store node ID, optional captured prompt text, option list, actions on enter, and evidence notes. | `DialogueOptionDefinition`, `DialogueActionDefinition`. | Data-backed dialogue session execution. |
| `DialogueOptionDefinition` | A selectable player option. | Store option index, captured option text, next node ID, conditions, actions, and evidence line references. | Condition evaluator, action executor. | Answer-list emission and answer handling. |
| `DialogueConditionDefinition` | Data form of a branch condition. | Represent mission-state checks, item checks, level/faction checks, and evidence confidence. | Mission state reader, character state. | Option filtering before sending answer lists. |
| `DialogueActionDefinition` | Data form of a side effect. | Represent mission offer/start/progress/complete, close window, open trade, emit verified text, set state, or no-op. | Mission service, packet emitter, inventory service for later phases. | Executed on node entry or option selection. |
| `DialogueRegistry` | Runtime lookup of NPC dialogue definitions. | Load packs, validate identities, detect duplicates, answer lookup requests by NPC identity. | Content loader. | `NPCController` or a new dialogue resolver. |
| `DialogueSession` | Per player/NPC conversation instance. | Track current node, last answer list, player identity, NPC identity, and active content pack. | Registry, condition evaluator, action executor. | KnuBot answer/close handlers. |
| `DialogueSessionStore` | Active dialogue session management. | Create, retrieve, update, and clear sessions keyed by player identity and NPC identity. | None beyond identity types. | KnuBot answer and close handlers. |
| `DialogueConditionEvaluator` | Evaluate option visibility and action eligibility. | Evaluate only supported, evidence-backed conditions. Mark unsupported conditions as blocked. | Mission state reader, character state. | Dialogue session before answer-list emission. |
| `DialogueActionExecutor` | Execute data-backed dialogue actions. | Dispatch mission actions, close windows, emit verified text, and leave unsupported actions inert with logging. | Mission service, KnuBot packet emitter. | Dialogue session on enter/answer. |
| `KnuBotDialoguePacketEmitter` | Adapter over current KnuBot handlers. | Send open window, append text, answer list, and close window using existing handlers. | Current KnuBot message handlers. | Dialogue session executor. |

## Data Model Requirements

Each dialogue content file should include:

- `contentPackId`
- `sourceCaptures`
- `npc.identity`
- `npc.name`
- `npc.confidence`
- `nodes[]`
- `nodes[].id`
- `nodes[].promptText` only when verified
- `nodes[].promptTextConfidence`
- `nodes[].options[]`
- `options[].index`
- `options[].text`
- `options[].textEvidence`
- `options[].selectedInCapture`
- `options[].conditions[]`
- `options[].actions[]`
- `options[].nextNodeId`

For Rex Larsson, the first pass should model captured answer-option sequences and selected index evidence. It should not create missing NPC spoken prompts.

## Loader And Validation Rules

- Reject duplicate NPC identity definitions within the same active content scope.
- Reject duplicate node IDs within one NPC definition.
- Reject options without captured text.
- Reject transitions to nonexistent node IDs unless the transition is a built-in close/end transition.
- Warn, but do not fail, when prompt text is missing because this is expected in the current Arete evidence.
- Warn when a condition/action type is not implemented.
- Preserve `sourceLog`, folder, timestamp, and confidence in the definition for auditability.

## Runtime Flow

1. Client opens an NPC interaction through the existing `TradeAction.Open` path.
2. `NPCController` asks a dialogue resolver whether data-backed dialogue exists for the target NPC identity.
3. If found, the resolver creates a `DialogueSession` for player identity + NPC identity.
4. The packet emitter opens the KnuBot window.
5. The session enters the root node.
6. The condition evaluator filters visible options.
7. The packet emitter sends `KnuBotAnswerList`.
8. `KnuBotAnswerMessageHandler` receives the selected answer index and routes it to the active `DialogueSession`.
9. The session validates the selected index against the last emitted option list.
10. The action executor runs evidence-backed actions.
11. The session advances to the next node or closes.

Legacy compiled KnuBot scripts should continue through `BaseKnuBot` until migrated.

## Conditions

Initial supported condition types should be small:

- `always`
- `missionStateEquals`
- `missionStateIn`
- `missionNotCompleted`
- `missionCompleted`

Unsupported conditions should not be guessed. They should be represented as `unsupported` with source evidence and should block the option until implemented.

## Actions

Initial supported action types should be small:

- `showVerifiedText`
- `showOptions`
- `closeDialogue`
- `offerMission`
- `acceptMission`
- `completeMission`
- `removeMission`
- `setMissionState`

For the Rex framework phase, actions should be planned but not executed in runtime code. When implementation starts, mission actions must call a mission service rather than mutate character stats directly.

## Rex Larsson Evidence Shape

The captured Rex answer options are:

| Sequence | Options |
| ---: | --- |
| 1 | `I don't really feel like telling you any of my secrets. If you'll excuse me, I need to go now.` / `Goodbye` |
| 2 | `Who said I don't have any ID?` / `Tell me who to talk to.` / `Goodbye` |
| 3 | `Get to the point, Rex.` / `Goodbye` |
| 4 | `I'll do it if you promise to tell me who your contact is.` / `Goodbye` |
| 5 | `Goodbye` |
| 6 | `I've done what you asked. Can you tell me who your contact is?` / `Goodbye` |
| 7 | `I can see why you don't have too many friends.` / `Goodbye` |
| 8 | `Goodbye` |

All captured selected answer indexes for Rex are `0`. Mapping an answer index to an option is based on nearby option-list context and should retain that uncertainty in the content pack.

## Integration Strategy

Use an adapter-first approach:

- Keep existing KnuBot packet handlers.
- Add a data-backed dialogue session path beside compiled KnuBot.
- Route answer/close packets by checking for an active data-backed session first.
- Fall back to compiled `BaseKnuBot` when no data-backed session exists.
- Keep content definitions outside compiled scripts.

## Limitations

- Rex NPC spoken prompt bodies are not verified in the allowed logs.
- KnuBot text direction includes both inbound and outbound observations; the framework should be packet-aware during implementation.
- The current handler assumes `NPCController.KnuBot` is non-null for KnuBot answers. Data-backed sessions need a safe dispatch path.
- The first content pack should avoid KnuBot item hand-in and trade flows until those flows are separately verified.

## Recommended First Code Target

Create only framework scaffolding in a later phase:

- Dialogue definition models.
- JSON loader.
- Registry validation.
- Session state model.
- Packet-emitter adapter over existing KnuBot message handlers.
- Tests using Rex captured option lists as fixture data.

Do not implement Rex gameplay content until the quest framework and mission-state model are in place.
