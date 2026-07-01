# Playfield Remaining Responsibilities Audit

Generated: 2026-07-01 local

## Scope

This is a report-only audit of remaining `Playfield.cs` responsibilities after the recent system-separation work. No extraction was performed and no gameplay behavior was changed.

## Files Inspected

- `AI_START_HERE.md`
- `AGENTS.md`
- `docs/ai/WORKFLOW.md`
- `docs/ai/CURRENT_TASK.md`
- `docs/project/AOREBIRTH_REGRESSION_BACKLOG.md`
- `docs/generated/aorebirth_independent_systems_audit_20260630_185305.md`
- `docs/project/PLAYFIELD_LIFECYCLE_HARNESS.md`
- `docs/generated/robot_patrol_replay_data_promotion_20260701.md`
- `AORebirth/Server/ZoneEngine/Core/Playfields/Playfield.cs`
- `AORebirth/Server/ZoneEngine/Core/Playfields/PlayfieldLifecycleTrace.cs`
- `AORebirth/Server/ZoneEngine/Core/Playfields/NpcCorpseLifecycleCoordinator.cs`
- `AORebirth/Server/ZoneEngine/Core/Playfields/NpcCombatTickCoordinator.cs`
- `AORebirth/Server/ZoneEngine/Core/Playfields/NpcPatrolReplayCoordinator.cs`
- `AORebirth/Server/ZoneEngine/Core/Playfields/CapturedAreteRobotContentProvider.cs`
- `AORebirth/Libraries/Source/AOtomation/AOtomation.Messaging/src/SmokeLounge.AOtomation.Messaging.Tests/PlayfieldLifecycleTraceTests.cs`

## Current Separation Baseline

`Playfield.cs` is no longer the sole owner of the cleaning robot combat lifecycle, but it is still the runtime hub that wires most packet-sensitive flows together.

Already delegated:

- NPC death/corpse/despawn sequencing is coordinated by `NpcCorpseLifecycleCoordinator`.
- NPC combat tick and captured cleaning robot attack packet context are coordinated by `NpcCombatTickCoordinator`.
- Captured robot patrol replay segment conversion is coordinated by `NpcPatrolReplayCoordinator`.
- Captured robot spawn rows and patrol replay CSV loading are centralized in `CapturedAreteRobotContentProvider`.
- Packet-order guardrails exist in `PlayfieldLifecycleTrace` and `PlayfieldLifecycleTraceTests`.

## Remaining Playfield Responsibility Map

| Area | Current `Playfield.cs` role | Behavior-owning? | Existing guardrail |
| --- | --- | --- | --- |
| Constructor-time content loading | Calls statel resolution, DB mob spawn loading, captured Arete robot loading, vendor loading, and static dynel loading | Yes | Captured robot content provider rows are tested, but actual spawn orchestration is not fully packet-observed |
| Captured Arete robot spawn orchestration | Creates NPC controller, spawns template, applies captured stats, assigns waypoints/replay, announces SCFU | Yes | Provider data tests cover source rows; lifecycle trace does not currently assert spawn SCFU or per-spawn runtime setup |
| Private city ready/init | Sends pre-`FullCharacter` org info/stat packets and post-`FullCharacter` towers/cities packets | Yes | `ExpectedPrivateCityReadyInitOrder` asserts relative order |
| Same-playfield visibility | Sends existing-player SCFU/`CharInPlay` to joiner and broadcasts joining-player SCFU/`CharInPlay` | Yes | `ExpectedSamePlayfieldVisibilityOrder` asserts order |
| Statel collision and captured private-city entry/exit | Detects statel contacts, Montroyal private-city entry, and private-city exit | Yes | No dedicated statel/private-city teleport trace beyond surrounding ready/init coverage |
| Teleport and zoning | Performs same-playfield grid teleport, cross-playfield redirect, despawn, playfield object transfer, and death-respawn teleport | Yes | No dedicated teleport lifecycle harness in the inspected tests |
| Heartbeat loop | Runs corpse spawn/despawn processing, stat/nano ticks, combat ticks, follow/patrol timers, and player collision checks | Yes | NPC combat/death paths are partially guarded; heartbeat ordering itself is not isolated |
| Player combat tick | Computes player-side attacks, range, damage, kill dispatch, and combat packet emission | Yes | Cleaning robot NPC attack coverage does not protect player attack sequencing |
| NPC combat wrapper | Delegates NPC combat ticks to `NpcCombatTickCoordinator` and exposes helper methods it calls back into | Partly | Cleaning robot attack order and combat constants are tested |
| NPC death/corpse wrapper | Delegates death entry and despawn timing to `NpcCorpseLifecycleCoordinator` while retaining corpse registry, corpse packets, loot rolls, and credit awards | Partly | Cleaning robot death/corpse/despawn order and timing constants are tested |
| Corpse use and loot | Owns corpse registry, corpse use validation, loot rolling, item creation, container packets, and credit award queue | Yes | No dedicated corpse-use/loot harness was identified in this audit |
| Player death/respawn | Owns player death marking, respawn stats, current-playfield respawn packet block, and teleport fallback | Yes | No dedicated player death/respawn packet-order harness was identified |

## Packet-Sensitive Areas Remaining

- Private city pre-`FullCharacter` org state and post-`FullCharacter` towers/cities ready block.
- Same-playfield player visibility SCFU and `CharInPlay` ordering.
- Captured Arete robot spawn announcement and runtime stat setup.
- Player attack packet sequencing and melee range handling.
- Corpse use, loot container response, item add/update ordering, and credit award delay.
- Player death/respawn packet block.
- Teleport/zoning packet sequence and statel collision re-entry guards.
- Heartbeat ordering between corpse spawn/despawn, combat, follow, patrol, healing, and collision checks.

## Risky Areas Not Ready To Extract

- Private city ready/init should not be extracted until the extraction target can preserve exact pre- and post-`FullCharacter` ordering through the existing trace harness.
- Same-playfield visibility should not be extracted until the harness can record the actual joiner/broadcast call paths, not only expected trace events.
- Player combat should not be extracted while range, animation, attack timing, and stat-derived damage are still under active correction.
- Corpse use/loot should not be extracted until there is a focused packet-order test for corpse open, loot item update/add, and credit award paths.
- Teleport/statel collision should not be extracted until there is a small lifecycle trace for same-playfield grid teleport and private-city entry/exit re-entry guards.
- Heartbeat should not be split directly; smaller coordinators should be extracted first so heartbeat becomes orchestration-only.

## Next Safest Extraction Candidate

Recommended next target: `AORebirth - Extract Captured Arete Robot Spawn Orchestration From Playfield`.

Why this is the safest next separation:

- The data provider and patrol replay coordinator already exist.
- The remaining Playfield-owned logic is narrow and localized around captured robot construction.
- The behavior is capture-backed and content-specific, which reduces cross-system blast radius.
- It can be extracted into a coordinator without changing packet payloads, timing, combat, patrol replay data, private-city behavior, or generic content loading.

Required guardrail before or during extraction:

- Add a focused test or trace that asserts the captured robot spawn coordinator consumes seven captured spawn definitions and applies name, MonsterData source, HP/life, level, run speed, spawn position, patrol waypoint setup, replay assignment, and SCFU announcement intent.

## Extraction Decision

Extraction was deferred. The current task requested an audit/guardrail pass, and the safest candidate still needs a small focused guardrail before moving runtime setup out of `Playfield.cs`.

## Validation Available For Recommended Extraction

Existing validation that helps:

- `CapturedAreteRobotContentProviderPreservesSpawnDefinitions`
- `CapturedAreteRobotContentProviderPreservesPatrolReplayRows`
- `NpcPatrolReplayCoordinatorBuildsCapturedAreteRobotSegments`
- Cleaning robot combat attack order tests.
- Cleaning robot death/corpse/despawn order tests.

Missing validation to add for the recommended extraction:

- A spawn orchestration test that proves the coordinator invokes the same spawn/setup/announce steps for each captured robot without requiring the AO client.

## Recommended Next Task Prompt Title

`AORebirth - Add Captured Arete Robot Spawn Orchestration Guardrail`
