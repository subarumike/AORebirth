# Rex B18D Cargo Box Use Path Result

Generated: 2026-06-18

## Scope

Read-only verification for Rex B18D Cargo Box identity and server-side `Use` routing.

No SQL, database data, Cargo Box row, template, transform, stat blob, StaticDynel row, runtime code, validation infrastructure, report/export tooling, B18D progression, rewards, inventory, XP/credits, `Quest Delete`, or action `59` interpretation was changed.

## Files Inspected

- `AI_START_HERE.md`
- `AGENTS.md`
- `docs/ai/AI_START.md`
- `docs/ai/CURRENT_TASK.md`
- `docs/ai/WORKFLOW.md`
- `docs/project/KNOWN_DECISIONS.md`
- `docs/project/PROJECT_STATE.md`
- `docs/project/ARCHITECTURE.md`
- `docs/generated/rex_b18d_cargo_box_staticdynel_result.md`
- `docs/generated/rex_objective_event_semantics_result.md`
- `docs/generated/rex_larsson_packet_semantics_result.md`
- `AORebirth/Server/ZoneEngine/Core/MessageHandlers/GenericCmdMessageHandler.cs`
- `AORebirth/Server/ZoneEngine/Core/Arete/Quests/RexB18DBoxProgressTracker.cs`
- `AORebirth/Server/ZoneEngine/Core/Arete/Quests/SafeQuestFullUpdateSender.cs`
- `AORebirth/Server/ZoneEngine/Core/Playfields/Playfield.cs`
- `AORebirth/Server/ZoneEngine/Core/Controllers/PlayerController.cs`
- `AORebirth/Server/ZoneEngine/Core/MessageHandlers/CharInPlayMessageHandler.cs`
- `AORebirth/Server/ZoneEngine/Core/MessageHandlers/SimpleItemFullUpdateMessageHandler.cs`
- `AORebirth/Libraries/Source/AORebirth.Core/Entities/StaticDynel.cs`
- `AORebirth/Libraries/Source/AORebirth.ObjectManager/PooledObject.cs`
- `AORebirth/Libraries/Source/AORebirth.ObjectManager/Pool.cs`
- `AORebirth/Built/Debug/ZoneEngineLog.txt`
- Local MySQL table `cellao_codex_clean.staticdynels`

## Files Changed

- `docs/generated/rex_b18d_cargo_box_use_path_result.md`

## DB Row Verification

Read-only query:

```sql
SELECT COUNT(*) AS MatchingRows
FROM staticdynels
WHERE Type=51005 AND Instance=1457108143 AND Playfield=6553;

SELECT Id, Type, Instance, Playfield, X, Y, Z,
       HeadingX, HeadingY, HeadingZ, HeadingW, HEX(stats) AS StatsHex
FROM staticdynels
WHERE Type=51005 AND Instance=1457108143 AND Playfield=6553;
```

Result: exactly one matching row.

| Id | Type | Instance | Identity | Playfield | X | Y | Z | HeadingX | HeadingY | HeadingZ | HeadingW |
| ---: | ---: | ---: | --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| 295 | 51005 | 1457108143 | `Terminal:56D9B4AF` | 6553 | 3621.58 | 51.745 | 780.477 | 0 | -0.710182 | 0 | 0.704018 |

Decoded `stats` blob:

| Stat | Value |
| --- | ---: |
| `Flags` | 139265 |
| `StaticInstance` | 297277 |
| `ACGItemLevel` | 1 |
| `ACGItemTemplateID` | 297277 |
| `ACGItemTemplateID2` | 297277 |
| `MultipleCount` | 1 |
| `AnimPlay` | 0 |
| `AnimPos` | 0 |

This matches the captured exact-identity `SimpleItemFullUpdate` evidence documented in `docs/generated/rex_b18d_cargo_box_staticdynel_result.md`.

## Evidence Table

| Field | Proposed/current value | Exact identity | Capture/source | Packet/log source | Confidence |
| --- | --- | --- | --- | --- | --- |
| Identity | `Terminal:56D9B4AF` | `Terminal:56D9B4AF` | Current DB row and captured use/full-update evidence | `20260614-194454/events.log:6327,6333`; later exact SIFU captures documented in `rex_b18d_cargo_box_staticdynel_result.md` | confirmed |
| DB row count | `1` | `Terminal:56D9B4AF` | `cellao_codex_clean.staticdynels` | Read-only SELECT on `Type=51005`, `Instance=1457108143`, `Playfield=6553` | confirmed |
| Playfield | `6553` | `Terminal:56D9B4AF` | Current DB row | Read-only SELECT | confirmed |
| Position | `3621.576, 51.745, 780.4768` captured; DB rounded display `3621.58, 51.745, 780.477` | `Terminal:56D9B4AF` | Exact SIFU evidence and current DB row | `rex_b18d_cargo_box_staticdynel_result.md`; read-only SELECT | confirmed |
| Rotation | `0, -0.7101817, 0, 0.7040185` captured; DB rounded display `0, -0.710182, 0, 0.704018` | `Terminal:56D9B4AF` | Exact SIFU evidence and current DB row | `rex_b18d_cargo_box_staticdynel_result.md`; read-only SELECT | confirmed |
| Template/stat source | `ACGItemTemplateID=297277` | `Terminal:56D9B4AF` | Exact SIFU stat blob and current DB row | Decoded DB `stats`; exact SIFU evidence | confirmed |
| Use signal | `GenericCmd Action=Use(3)` | `Terminal:56D9B4AF` | ZoneEngine live log | `ZoneEngineLog.txt` at `2026-06-18 03:16:30.9566` | confirmed |
| Server targetability | `LookAt target=0000C73D:56D9B4AF` | `Terminal:56D9B4AF` | ZoneEngine live log | `ZoneEngineLog.txt` at `2026-06-18 02:40:28.0179` and earlier | confirmed |
| Visual render | Client-side object visible in ongoing smoke, but Codex did not re-run a separate visual pass in this read-only report | `Terminal:56D9B4AF` | Manual smoke context plus server LookAt/Use logs | Server logs prove target/use, not mesh correctness | captured |

## StaticDynel Loading Path

Source path found:

1. `Playfield.LoadStaticDynels` loads rows from `StaticDynelDao.Instance.GetWhere(new { Playfield = playfieldIdentity.Instance })`.
2. It deserializes the row `stats` blob and requires `ACGItemTemplateID`.
3. It constructs `StaticDynel` with identity `(Type, Instance)`, parent playfield identity, and `ItemLoader.ItemList[id]`.
4. `StaticDynel` inherits `PooledObject`; `PooledObject` registers the object in `Pool.Instance.AddObject(parent, this)`.
5. Playfield entry sends `SimpleItemFullUpdate` for `Pool.Instance.GetAll<StaticDynel>(this.Identity)`.

Relevant files:

- `AORebirth/Server/ZoneEngine/Core/Playfields/Playfield.cs:254-289`
- `AORebirth/Libraries/Source/AORebirth.Core/Entities/StaticDynel.cs:50-74`
- `AORebirth/Libraries/Source/AORebirth.ObjectManager/PooledObject.cs:60-68`
- `AORebirth/Libraries/Source/AORebirth.ObjectManager/Pool.cs:133-180`
- `AORebirth/Server/ZoneEngine/Core/Playfields/Playfield.cs:1506-1509`
- `AORebirth/Server/ZoneEngine/Core/MessageHandlers/SimpleItemFullUpdateMessageHandler.cs:53-92`

## Use Interaction Result

Observed server log:

```text
2026-06-18 03:16:30.9566|INFO|Cell.Core.ServerBase|(127.0.0.1:38443) -> GenericCmd action=Use(3) temp1=0 count=1 temp4=1 user=CanbeAffected:18 target=Terminal:1457108143
2026-06-18 03:16:30.9566|DEBUG|Utility.LogUtil|ARETE_REX_B18D_BOX_PROGRESS use matched mission=Mission:5514B18D character=0000C350:00000012 target=0000C73D:56D9B4AF signal="GenericCmd Action=Use" evidence=20260614-194454/events.log:6327,6333 packetHandoffPending=true noRewards=true noDbWrites=true
2026-06-18 03:16:30.9566|DEBUG|Utility.LogUtil|Arete Rex B18D completion handoff sent character=0000C350:00000012 action59=Mission:5514B18D questDelete=Mission:5514B18D nextQuestFullUpdate=Mission:5514B18E capture=20260614-194454/events.log:6327-6344 packetHandoffOnly=true noRewards=true noInventory=true noXpCredits=true noDbWrites=true noPersistence=true noQuestSemantics=true
```

Earlier targetability log:

```text
2026-06-18 02:40:28.0179|DEBUG|Utility.LogUtil|LookAt target=0000C73D:56D9B4AF returnInfo=0
```

Read-only conclusion:

- Packet received: `GenericCmd Action=Use(3)`.
- Handler reached: `GenericCmdMessageHandler.Read`.
- Target identity received by the server: `Terminal:1457108143`, i.e. `Terminal:56D9B4AF`.
- The current gated Rex B18D path matched the exact identity and acknowledged the use.
- Current dirty-worktree behavior then sent the already-existing gated B18D-to-B18E packet-window handoff. This report did not add or modify that behavior.

## Exact Handler Path Reached

Current gated path:

1. `GenericCmdMessageHandler.Read` receives `GenericCmdAction.Use`.
2. `GenericCmdMessageHandler.cs:101-104` calls `RexB18DBoxProgressTracker.TryObserveBoxUse(character, target, out shouldSendB18DHandoff)`.
3. `RexB18DBoxProgressTracker.cs:57-60` matches only `Terminal:56D9B4AF`.
4. `RexB18DBoxProgressTracker.cs:62-70` requires the gated Rex/B18D environment configuration.
5. `RexB18DBoxProgressTracker.cs:73-80` requires a valid player in Arete playfield `6553`.
6. `RexB18DBoxProgressTracker.cs:83-105` records the one-time in-memory handoff state and returns `true`.
7. `GenericCmdMessageHandler.cs:106` acknowledges the use.
8. `GenericCmdMessageHandler.cs:109` calls `SafeQuestFullUpdateSender.TrySendB18DCompletionHandoff`.

Generic paths not reached while the B18D tracker consumes this target:

- DB-backed `StaticDynel`/`IEventHolder` route at `GenericCmdMessageHandler.cs:143-195`.
- `playfields.dat` statel route at `GenericCmdMessageHandler.cs:198-209`.
- `PlayerController.UseStatel` lookup at `PlayerController.cs:614-632`.

## Exact Stop Point

For the current enabled-gate smoke, interaction does not stop before the Rex B18D gated handoff. It reaches:

`GenericCmdMessageHandler.cs:101-111` -> `RexB18DBoxProgressTracker.TryObserveBoxUse` -> acknowledge -> `SafeQuestFullUpdateSender.TrySendB18DCompletionHandoff`.

For generic DB-backed StaticDynel interaction, the stop point is also `GenericCmdMessageHandler.cs:101-111`: the exact-target Rex B18D tracker returns `true`, so the later generic `Pool.Instance.Contains(target)` / `IEventHolder.OnUse` route is bypassed.

No evidence in this read-only pass proves that the Cargo Box should execute a template `OnUse` event through generic StaticDynel events. The observed current behavior is the gated Rex-specific exact-target use path.

## Visual Smoke Result

Codex did not restart engines or run a separate live-client visual pass during this read-only report. Existing live server logs from the ongoing smoke prove:

- The client targeted/looked at `Terminal:56D9B4AF`.
- The client sent `GenericCmd Action=Use` for `Terminal:56D9B4AF`.
- ZoneEngine received and matched that exact identity.

Mesh/appearance correctness is not promoted from visual observation here; the current data remains tied to the exact captured SIFU identity evidence.

## Validation

- `git status --short --branch` was run before editing.
- Read-only DB verification passed: exactly one `staticdynels` row for `Terminal:56D9B4AF` in playfield `6553`.
- Read-only DB stat decode matched the captured eight stat entries.
- ZoneEngine log inspection found exact target `LookAt` and `GenericCmd Use` evidence.
- No SQL or DB writes were performed.
- No runtime code was changed, so no build was required.
- `git diff --check` passed with line-ending warnings only on pre-existing dirty files.

## Recommended Next Implementation Task

Run a separate narrow implementation task to decide B18D use routing policy:

- Option A: keep the current evidence-backed Rex-specific exact-target gated handoff path for `Terminal:56D9B4AF`.
- Option B: design a separate evidence-backed DB-backed `StaticDynel` event route.

Do not combine that with Cargo Box visual/data changes, rewards, inventory, XP/credits, mission persistence, `Quest Delete` semantics, or action `59` interpretation.
