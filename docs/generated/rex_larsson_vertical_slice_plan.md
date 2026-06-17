# Rex Larsson Vertical Slice Plan

Generated: 2026-06-15

Scope: implementation plan only. This document does not implement Rex Larsson, create missions, generate SQL, create NPC spawns, or change runtime behavior.

## Target

- NPC: Rex Larsson
- Identity: `(SimpleChar:782DE568)`
- Capture folder: `20260614-194454`
- Evidence type: `knubot-target`
- Confidence: `identity-and-name`
- Observed level: `15`
- Sample position: `3624.599, 51.745, 787.7465`
- First seen: `2026-06-15T00:46:54.8661621Z`
- Last seen: `2026-06-15T00:49:10.8288533Z`

## Captured Dialogue Evidence

Rex has 21 dialogue events:

- 4 `KnuBotOpenChatWindow`
- 8 `KnuBotAnswerList`
- 7 `KnuBotAnswer`
- 2 `KnuBotCloseChatWindow`
- 0 `NpcMessage`

The visible answer options are:

| Sequence | Timestamp | Options | Captured selected index |
| ---: | --- | --- | --- |
| 1 | `2026-06-15T00:46:55.9780554Z` | `I don't really feel like telling you any of my secrets. If you'll excuse me, I need to go now.` / `Goodbye` | `0` at `2026-06-15T00:47:00.8648673Z` |
| 2 | `2026-06-15T00:47:01.9349845Z` | `Who said I don't have any ID?` / `Tell me who to talk to.` / `Goodbye` | `0` at `2026-06-15T00:47:04.7744986Z` |
| 3 | `2026-06-15T00:47:05.8448062Z` | `Get to the point, Rex.` / `Goodbye` | `0` at `2026-06-15T00:47:06.6539321Z` |
| 4 | `2026-06-15T00:47:07.6040523Z` | `I'll do it if you promise to tell me who your contact is.` / `Goodbye` | `0` at `2026-06-15T00:47:09.1035673Z` |
| 5 | `2026-06-15T00:47:10.2447301Z` | `Goodbye` | No answer before first close in captured sequence |
| 6 | `2026-06-15T00:49:02.9588113Z` | `I've done what you asked. Can you tell me who your contact is?` / `Goodbye` | `0` at `2026-06-15T00:49:03.8283253Z` |
| 7 | `2026-06-15T00:49:04.8888429Z` | `I can see why you don't have too many friends.` / `Goodbye` | `0` at `2026-06-15T00:49:05.5879767Z` |
| 8 | `2026-06-15T00:49:06.5684993Z` | `Goodbye` | `0` at `2026-06-15T00:49:10.3181950Z` |

Important limitation: these are visible answer/branch option lists. NPC prompt bodies are not independently expanded in the allowed logs and must remain absent until verified.

## Captured Mission Evidence

| Mission | First/last seen | Event evidence | Completion/removal evidence |
| --- | --- | --- | --- |
| `(Mission:5514B18C)` | `2026-06-15T00:48:42.2131275Z` | `CharacterAction` action `59`, target `(Mission:5514B18C)`, `parameter1=56003`, `parameter2=1427419532` | `Quest` action `Delete` at the same timestamp |
| `(Mission:5514B18D)` | `2026-06-15T00:48:56.4762819Z` | `CharacterAction` action `59`, target `(Mission:5514B18D)`, `parameter1=56003`, `parameter2=1427419533` | `Quest` action `Delete` at the same timestamp |
| `(Mission:5514B18E)` | `2026-06-15T00:49:01.8992326Z` | `CharacterAction` action `59`, target `(Mission:5514B18E)`, `parameter1=56003`, `parameter2=1427419534` | `Quest` action `Delete` at the same timestamp |

Mission titles are `null`. Objectives are empty. Source NPC relationship is `uncertain-not-linked-by-allowed-logs` for all three.

## Vertical Slice Purpose

The vertical slice should prove that AO Rebirth can:

- Load captured dialogue and mission evidence as data.
- Resolve NPC dialogue by identity.
- Start a data-backed KnuBot dialogue session.
- Emit captured answer options.
- Track selected answer indexes.
- Query mission state for conditions.
- Represent mission offer/progress/completion transitions without hardcoding Rex-specific logic.
- Keep unresolved fields unresolved.

This is not a quest content implementation phase.

## Proposed Data Flow

1. Load a `DialogueContentPack` containing Rex identity and captured option-list nodes.
2. Load a `MissionContentPack` containing mission identities `(Mission:5514B18C)`, `(Mission:5514B18D)`, and `(Mission:5514B18E)` with null title/objective fields and raw evidence events.
3. Register Rex by `(SimpleChar:782DE568)` in the dialogue registry.
4. Register the three mission identities in the mission registry.
5. When the client opens Rex interaction, create a `DialogueSession`.
6. Emit the first captured Rex answer list.
7. On selected answer index, advance through the captured branch sequence.
8. Mission-related dialogue actions remain declarations until mission packet behavior is verified.
9. Mission state transitions are represented as framework definitions and tested in isolation.

## Proposed Rex Dialogue Definition Shape

Use node IDs derived from evidence sequence, not invented story labels:

| Node ID | Evidence source | Options | Prompt text |
| --- | --- | --- | --- |
| `rex_194454_001` | `KnuBotAnswerList` at `2026-06-15T00:46:55.9780554Z` | Sequence 1 options | `null`, not verified |
| `rex_194454_002` | `KnuBotAnswerList` at `2026-06-15T00:47:01.9349845Z` | Sequence 2 options | `null`, not verified |
| `rex_194454_003` | `KnuBotAnswerList` at `2026-06-15T00:47:05.8448062Z` | Sequence 3 options | `null`, not verified |
| `rex_194454_004` | `KnuBotAnswerList` at `2026-06-15T00:47:07.6040523Z` | Sequence 4 options | `null`, not verified |
| `rex_194454_005` | `KnuBotAnswerList` at `2026-06-15T00:47:10.2447301Z` | Sequence 5 option | `null`, not verified |
| `rex_194454_006` | `KnuBotAnswerList` at `2026-06-15T00:49:02.9588113Z` | Sequence 6 options | `null`, not verified |
| `rex_194454_007` | `KnuBotAnswerList` at `2026-06-15T00:49:04.8888429Z` | Sequence 7 options | `null`, not verified |
| `rex_194454_008` | `KnuBotAnswerList` at `2026-06-15T00:49:06.5684993Z` | Sequence 8 option | `null`, not verified |

Transitions between nodes should be based on captured selected index `0` where present. Alternative branches such as `Goodbye` should close the session only if the close behavior is explicit or configured as a generic built-in close action for `Goodbye`.

## Proposed Mission Definition Shape

Each Rex mission should be represented as a raw evidence-backed definition:

```text
missionIdentity: (Mission:5514B18C)
title: null
objectives: []
sourceNpcRelationship: uncertain-not-linked-by-allowed-logs
evidence:
  - CharacterAction action 59, parameter1 56003, parameter2 1427419532
  - Quest action Delete
```

Repeat the same structure for `(Mission:5514B18D)` and `(Mission:5514B18E)` with their captured parameter2 values.

## How The First Three Missions Would Flow Through The Framework

The planned flow is intentionally abstract because titles, objectives, and source binding are not fully decoded.

1. Dialogue reaches a node whose captured option/action evidence is associated with mission progression.
2. `DialogueActionExecutor` calls `MissionService` with a mission action definition.
3. `MissionService` validates the current character mission state and the mission definition.
4. The mission state store records the state transition.
5. A packet emitter may emit client-visible quest packets only after packet behavior is verified.
6. When a captured completion/removal trigger is verified, `MissionService` marks the mission as completed or removed.
7. Chaining unlocks the next mission through mission-state conditions, not Rex-specific code.

For this planning phase, all three mission links stay marked as uncertain until a packet-aware pass verifies the exact semantics.

## Integration Points

- NPC identity resolution: `NPCController` and dialogue registry.
- Open interaction: `TradeMessageHandler` -> `NPCController.Trade`.
- Answer dispatch: `KnuBotAnswerMessageHandler` -> data-backed `DialogueSession`.
- Close dispatch: `KnuBotCloseChatWindowMessageHandler` -> session cleanup.
- Mission domain: `MissionService`, `MissionRegistry`, `MissionStateStore`.
- Packet output: existing KnuBot handlers first, quest packet emitter later.

## Risks

- Captured option lists do not include verified NPC prompt bodies.
- Mission action `59` is not named in current AOtomation `CharacterActionType`.
- `QuestMessage` is visible in captures but was not found as a source DTO in the searched AOtomation tree.
- `Quest Delete` may mean completion, removal, or another client-side state cleanup; this needs verification.
- Current KnuBot answer handling assumes a compiled `BaseKnuBot` instance and needs safe dispatch for data-backed sessions.
- Mission persistence is unresolved without schema changes, so early framework tests should use an in-memory state store.

## Remaining Work Before Implementing Rex

1. Add dialogue content definition models and loader.
2. Add mission definition models and loader.
3. Add registries for NPC dialogue and missions.
4. Add in-memory dialogue session and mission state stores.
5. Add a KnuBot packet-emitter adapter over existing handlers.
6. Add tests using the captured Rex option-list evidence and three mission identities.
7. Audit or implement the missing `QuestMessage` DTO only after packet behavior is verified.
8. Verify action `59` semantics before any runtime mission action is emitted.
9. Decide mission persistence through an approved design pass.

## Stop Point

Stop after framework scaffolding is designed and documented. Do not implement Rex gameplay content until the framework is reviewed and the unresolved mission/dialogue evidence is handled.
