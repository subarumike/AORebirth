# Arete Quest Framework Plan

Generated: 2026-06-15

Scope: architecture plan only. This document does not implement quests, generate SQL, change schema, generate game data, or alter runtime behavior.

## Design Goal

Represent Arete missions as data-driven state machines loaded from captured evidence. The system should support mission offer, active state, progress, completion, removal, and chaining without hardcoding individual missions or guessing missing quest text.

The first planning target is Rex Larsson and missions `(Mission:5514B18C)`, `(Mission:5514B18D)`, and `(Mission:5514B18E)`.

## Existing Reusable Pieces

- `QuestFullUpdateMessage`, `QuestInfo`, and related AOtomation DTOs can eventually emit quest journal data when fields are verified.
- `CreateQuestMessage` can eventually represent quest creation.
- Character stats include mission bit fields (`MissionBits1` through `MissionBits12`) and the full character packet sends them.
- `CharacterActionMessage` carries captured mission action fields: action, target, parameter1, and parameter2.
- `GenericCmdMessageHandler` can be a future integration point for quest object use.
- `TemplateActionMessageHandler` can be a later reward/inventory packet helper.

These pieces are not enough by themselves. AO Rebirth needs a mission runtime above packet DTOs and stats.

## Existing Gaps

- No ZoneEngine quest lifecycle service was found.
- No `QuestFullUpdateMessageHandler`, `CreateQuestMessageHandler`, or `QuestAlternativeMessageHandler` was found in ZoneEngine.
- `N3MessageType.Quest` exists, but a `QuestMessage` DTO was not found in the searched AOtomation source.
- Captured Rex mission action `59` is not named in `CharacterActionType`.
- Mission titles and objectives for the Rex slice are unavailable in the allowed logs.
- Source NPC relationships are marked uncertain in `quest_chains.json`.
- Mission bits exist, but there is no mapping from Arete mission identities to mission bit storage.

## Proposed Components

| Component | Purpose | Responsibilities | Dependencies | Integration points |
| --- | --- | --- | --- | --- |
| `MissionContentPack` | Loaded collection of mission definitions from captured evidence. | Store pack ID, source captures, mission definitions, confidence, and validation errors. | JSON loader, evidence metadata. | ZoneEngine startup/content registry. |
| `MissionDefinition` | Data definition for one mission identity. | Store mission identity, optional verified title, optional verified objectives, states, transitions, rewards, and evidence. | Mission stages, transitions, reward definitions. | Mission registry and dialogue actions. |
| `MissionStageDefinition` | One stage inside a mission state machine. | Store stage ID, display data if verified, objective definitions, completion criteria, and evidence confidence. | Conditions and criteria. | Mission state evaluator. |
| `MissionTransitionDefinition` | Declarative movement between mission states/stages. | Define from-state, trigger, conditions, actions, and to-state. | Trigger router, condition evaluator, action executor. | Dialogue actions, combat/object/inventory events later. |
| `MissionConditionDefinition` | Data form of state-gating logic. | Represent mission state checks, item presence, objective completion, and known stats. | Character state, inventory, mission state. | Dialogue option conditions and mission transitions. |
| `MissionActionDefinition` | Data form of mission side effects. | Represent offer, accept, set active, mark complete, remove, grant reward, update bits, and emit packets. | Mission state store, packet emitter, reward services later. | Dialogue action executor and event router. |
| `MissionRegistry` | Runtime lookup by mission identity. | Load definitions, validate uniqueness, expose mission definitions to services. | Content loader. | Dialogue actions and mission event router. |
| `MissionStateStore` | Per-character mission state. | Track mission state, stage, timestamps, completion flags, and evidence/debug metadata. | Character identity, persistence adapter. | Mission service and dialogue conditions. |
| `MissionService` | Main domain API for missions. | Offer, accept, progress, complete, remove, and query missions through validated transitions. | Registry, state store, condition/action evaluator. | Dialogue action executor, future packet handlers. |
| `MissionEventRouter` | Converts runtime events into mission triggers. | Accept dialogue triggers, character actions, generic use events, inventory deltas, combat events, and stat changes. | Mission service. | Existing message handlers and gameplay systems. |
| `MissionPacketEmitter` | Adapter for client-visible mission packets. | Emit create/update/delete packets only after DTO behavior is verified. | AOtomation quest DTOs, message handlers. | Mission service action executor. |
| `MissionPersistenceAdapter` | Persistence boundary. | Store mission state without leaking DB details into mission logic. | Existing character stats or future approved storage. | Character save/load lifecycle. |

## Mission State Model

Use explicit states before mapping to packets or stats:

- `unavailable`
- `offered`
- `accepted`
- `active`
- `readyToComplete`
- `completed`
- `removed`
- `blocked`

The Rex capture currently has completion/removal evidence for the first three mission identities but does not expose titles, objective text, or source NPC relationship with certainty. The state model must allow unresolved display fields.

## Trigger Types

Initial trigger types should be:

- `dialogueOptionSelected`
- `dialogueNodeEntered`
- `missionCharacterActionObserved`
- `questDeleteObserved`
- `genericUse`
- `inventoryDelta`
- `statDelta`
- `combatEvent`

For the first implementation slice, only dialogue and mission-state triggers should be enabled. `genericUse`, `inventoryDelta`, `statDelta`, and `combatEvent` should remain planned extension points unless evidence for the exact mission step is decoded.

## Persistence Strategy

No schema changes are approved for this phase.

Recommended first implementation:

- In-memory state store for tests and local framework validation.
- A persistence interface with no database dependency in the domain model.
- A later approved persistence pass to decide whether Arete mission state maps to existing stats, a new table, or another storage mechanism.

Do not write mission state directly to mission bits until a mapping is verified. Mission bits may be useful, but they are not the framework model.

## Packet Strategy

Packet handling should be adapter-based:

- The domain state changes first.
- A packet emitter translates verified state changes into client-visible packets.
- Unknown packet fields remain unset or unsupported until verified.
- The captured `Quest Delete` evidence should be treated as removal/completion evidence, not final semantic proof, until packet behavior is validated.
- Captured `CharacterAction` action `59` should stay raw numeric evidence until the action is named and tested.

## Content Definition Requirements

Each mission definition should store:

- `missionIdentity`
- `sourceCaptures`
- `title` and `titleConfidence`
- `objectives[]` and `objectiveConfidence`
- `sourceNpcRelationship`
- `states[]`
- `transitions[]`
- `evidenceEvents[]`
- `unresolvedFields[]`

For Rex missions, `title` remains `null`, `objectives` remains empty, and player-facing quest journal text must not be generated.

## Rex Mission Evidence

| Mission | Evidence | Captured action | Parameters | Completion/removal evidence | Limits |
| --- | --- | --- | --- | --- | --- |
| `(Mission:5514B18C)` | `npc-interactions.log` and `system-messages.log` from `20260614-194454` | `CharacterAction` action `59` targeting the mission | `parameter1=56003`, `parameter2=1427419532` | `Quest` action `Delete` at the same timestamp | Title/objectives unavailable, source NPC uncertain |
| `(Mission:5514B18D)` | `npc-interactions.log` and `system-messages.log` from `20260614-194454` | `CharacterAction` action `59` targeting the mission | `parameter1=56003`, `parameter2=1427419533` | `Quest` action `Delete` at the same timestamp | Title/objectives unavailable, source NPC uncertain |
| `(Mission:5514B18E)` | `npc-interactions.log` and `system-messages.log` from `20260614-194454` | `CharacterAction` action `59` targeting the mission | `parameter1=56003`, `parameter2=1427419534` | `Quest` action `Delete` at the same timestamp | Title/objectives unavailable, source NPC uncertain |

## Mission Chaining

Mission chaining should be represented as transitions between definitions, not hardcoded branch code.

Example structure:

- A dialogue action may offer or activate a mission.
- A mission completion action may unlock the next mission.
- Dialogue conditions may show different options based on mission state.
- Missing or uncertain source-NPC links must be represented as unresolved, not guessed.

For Rex, the exact source relationship is uncertain in the allowed logs. The vertical slice plan can describe how the first three mission identities would flow through the framework, but implementation should wait for packet-aware validation before binding them to specific dialogue actions.

## Validation Requirements For Later Code

- Load content pack with null title/objective fields without failure.
- Reject mission definitions with duplicate identities.
- Reject transitions to undefined states.
- Ensure unsupported triggers do not execute silently.
- Verify dialogue conditions can query mission state.
- Verify mission actions do not emit quest packets when required packet models are missing.
- Verify login, zoning, vendors, combat, and existing compiled KnuBot scripts still behave as before.

## Recommended First Code Target

Build the framework skeleton only:

- `MissionDefinition` and related data models.
- JSON loader and validation.
- In-memory `MissionStateStore`.
- `MissionRegistry`.
- `MissionService` with no packet emission by default.
- Unit fixtures using the three Rex mission identities and raw evidence fields.

Only after that should Rex content be wired through dialogue and mission actions.
