# Playfield Post Robot Spawn Extraction Audit

Generated: 2026-07-01 11:22 local

## Scope

This is a report-only audit of `Playfield.cs` after captured Arete robot spawn orchestration was extracted into `CapturedAreteRobotSpawnOrchestrator`.

No gameplay behavior, packet payload, packet order, lifecycle trace order, database state, capture tooling, private-city behavior, GenericCmd behavior, combat tuning, corpse/despawn behavior, or player visibility behavior was changed.

## Files Inspected

- `AI_START_HERE.md`
- `AGENTS.md`
- `docs/ai/WORKFLOW.md`
- `docs/ai/CURRENT_TASK.md`
- `docs/project/AOREBIRTH_REGRESSION_BACKLOG.md`
- `docs/generated/aorebirth_independent_systems_audit_20260630_1853.md` - prompt-named file was not present
- `docs/generated/aorebirth_independent_systems_audit_20260630_185305.md` - inspected as the concrete repo audit matching the prompt timestamp
- `docs/generated/playfield_remaining_responsibilities_audit_20260701_000000.md`
- `docs/project/PLAYFIELD_LIFECYCLE_HARNESS.md`
- `docs/generated/robot_patrol_replay_data_promotion_20260701.md`
- `AORebirth/Server/ZoneEngine/Core/Playfields/Playfield.cs`
- `AORebirth/Server/ZoneEngine/Core/Playfields/PlayfieldLifecycleTrace.cs`
- `AORebirth/Server/ZoneEngine/Core/Playfields/CapturedAreteRobotSpawnOrchestrator.cs`
- `AORebirth/Server/ZoneEngine/Core/Playfields/CapturedAreteRobotContentProvider.cs`
- `AORebirth/Server/ZoneEngine/Core/Playfields/NpcPatrolReplayCoordinator.cs`
- `AORebirth/Server/ZoneEngine/Core/Playfields/NpcCorpseLifecycleCoordinator.cs`
- `AORebirth/Server/ZoneEngine/Core/Playfields/NpcCombatTickCoordinator.cs`
- `AORebirth/Libraries/Source/AOtomation/AOtomation.Messaging/src/SmokeLounge.AOtomation.Messaging.Tests/PlayfieldLifecycleTraceTests.cs`

## Current Playfield Responsibility Map

| Area | Current owner | `Playfield.cs` role | Behavior-owning? | Guardrail status |
| --- | --- | --- | --- | --- |
| Captured Arete robot spawn orchestration | `CapturedAreteRobotSpawnOrchestrator` | Constructor delegates with `CapturedAreteRobotSpawns.SpawnForPlayfield(this, playfieldIdentity)` | Mostly no; Playfield remains runtime host and broadcaster | Trace/test coverage exists for load -> spawn -> replay assignment -> SCFU broadcast |
| Captured robot spawn/replay data | `CapturedAreteRobotContentProvider`, `NpcPatrolReplayCoordinator` | Holds static provider/coordinator instances | No for data parsing; yes for runtime hosting | Tests cover seven spawn rows, committed replay path, replay row counts, missing-file fallback, and replay assignment |
| NPC death/corpse/despawn entry | `NpcCorpseLifecycleCoordinator` | Delegates begin/process/finalize but still owns corpse registries, corpse full update, loot, credits, and combat cleanup helpers | Partial | Cleaning robot death order is covered; corpse use/loot is not covered |
| NPC combat tick/attack sequencing | `NpcCombatTickCoordinator` | Delegates NPC attack tick but still exposes shared combat helpers and owns player combat | Partial | Cleaning robot SpecialAttackWeapon -> Attack -> AttackInfo order is covered |
| Private-city ready/init | `Playfield.cs` | Sends org info, social/org stats, towers/cities, and captured city payloads | Yes | Lifecycle order is traced, but current tests do not exercise a real sender/packet recorder |
| Same-playfield visibility | `Playfield.cs` | Sends existing-player SCFU/`CharInPlay` snapshots and joining-player broadcasts | Yes | Lifecycle order is traced, but guardrail is still trace-level rather than a packet-recorder scenario |
| Statel/private-city entry and exit routing | `Playfield.cs` | Handles collision grace, private-city entry, private-city exit, social-status pre-teleport stat, and teleport call | Yes | No dedicated lifecycle guardrail identified |
| Teleport/zoning | `Playfield.cs` | Handles same-playfield grid teleport, cross-playfield redirect, despawn, raw coordinate/heading update, and destination playfield handoff | Yes | No dedicated lifecycle guardrail identified |
| Heartbeat orchestration | `Playfield.cs` | Orders pending corpse spawn/despawn, corpse credit awards, dead NPC processing, combat timers, movement, healing, and collision checks | Yes | Only selected NPC combat/death phases are traced; heartbeat order itself is not guarded |
| Player combat | `Playfield.cs` plus combat rules/helpers | Owns target validation, player damage application, attack info, health damage, kill dispatch, and stop fight | Yes | No player attack/range lifecycle guardrail identified |
| Player death/respawn | `Playfield.cs` | Owns death marking, respawn stat block, respawn teleport, SCFU resend, item updates, `FullCharacter`, ready block, and death action | Yes | No dedicated guardrail identified |
| Corpse use and loot | `Playfield.cs` | Owns corpse registry lookup, corpse open/use, loot item transfer, inventory update, container add, credits, and cleanup timing | Yes | No corpse-use/loot packet-order guardrail identified |
| Static dynel/vendor/mob loading | `Playfield.cs` | Constructor still calls statel, mob, vendor, and static dynel loaders directly | Yes for orchestration | No constructor-loading guardrail except captured robot content coverage |

## Responsibilities Already Delegated

- Captured Arete robot spawn setup is now outside `Playfield.cs` in `CapturedAreteRobotSpawnOrchestrator`.
- Captured Arete robot content definitions and committed patrol replay CSV loading are in `CapturedAreteRobotContentProvider`.
- Captured patrol replay segment assignment is in `NpcPatrolReplayCoordinator`.
- NPC death/despawn phase control is in `NpcCorpseLifecycleCoordinator`.
- NPC attack tick and captured cleaning robot attack packet context are in `NpcCombatTickCoordinator`.
- Lifecycle trace constants and expected order arrays are in `PlayfieldLifecycleTrace`.

## Responsibilities Still Behavior-Owning In Playfield

- Private-city pre-`FullCharacter` org state and post-`FullCharacter` towers/cities packets.
- Same-playfield SCFU and `CharInPlay` visibility ordering.
- Statel collision routing, private-city entry/exit, and teleport calls.
- Same-playfield and cross-playfield teleport packet/state sequencing.
- Heartbeat ordering across corpse, combat, movement, heal, and collision phases.
- Player combat attack validation, damage application, kill routing, and stop-fight cleanup.
- Player death/respawn packet block.
- Corpse registry, corpse open/use, loot transfer, credit award, and cleanup timing.
- Constructor-time statel, mob, vendor, and static dynel loading.

## Packet-Sensitive Areas Remaining

- Private-city org/init packets must remain before `FullCharacter`, with towers/cities after the ready block.
- Same-playfield visibility still depends on SCFU and `CharInPlay` ordering for joiner and existing-player snapshots.
- Statel collision and private-city entry/exit still mix collision state, social-status stat update, and teleport.
- Teleport/zoning still mixes despawn, coordinate/heading mutation, redirect, and playfield transfer.
- Heartbeat ordering still controls whether corpse spawn/despawn, combat, chase/patrol, and collision checks interleave safely.
- Player combat remains packet/state-sensitive around range, animation timing, attack info, damage, and kill dispatch.
- Player death/respawn is packet-order-sensitive and still shares nearby corpse packet helpers.
- Corpse use/loot remains packet-sensitive around open action, inventory update, container add, credits, and delayed despawn.

## Lifecycle Trace Coverage Status

Covered:

- Private-city ready/init expected stage order.
- Same-playfield visibility expected stage order.
- Cleaning robot NPC attack context before `AttackInfo`.
- Cleaning robot death/corpse/despawn order.
- Captured Arete robot spawn orchestration trace order.

Partially covered:

- Captured robot spawn setup is trace-covered, but only the orchestrator stages are guarded; the surrounding constructor loading order is not.
- Same-playfield visibility is trace-covered, but it is not yet a black-box packet-recorder test of actual joiner/broadcast sends.
- Private-city ready/init is trace-covered, but it is not yet a black-box packet-recorder test of actual `ZoneClient.SendCompressed` order.

Not covered:

- Statel/private-city entry and exit routing.
- Teleport/zoning lifecycle.
- Heartbeat phase order.
- Player combat lifecycle.
- Player death/respawn lifecycle.
- Corpse use/loot lifecycle.
- Towers/cities helper behavior outside the private-city trace.
- Social/org stat helper behavior outside the private-city trace.

## Recommended Next Guardrail Task

Recommended next guardrail task:

`AORebirth - Add Private City Ready Block Packet Recorder Guardrail`

Why:

- Private-city ready/init is still directly behavior-owned by `Playfield.cs`.
- The packet order is already known to be client-state-sensitive.
- Existing trace coverage gives a useful stage contract, but the next guardrail should assert the actual emitted packet/message order from the real `SendPrivateCityPreFullCharacterReadyBlock` and `SendPrivateCityPlayfieldReadyBlock` paths.
- This is safer than touching player combat, corpse loot, or teleport because it can be guarded as an existing working packet sequence without changing gameplay.

Minimum expected assertions:

- `OrgInfoPacket` and org/social stat updates occur before `FullCharacter` in the ready flow that calls these methods.
- `PlayfieldAllTowers` occurs before `PlayfieldAllCities`.
- Captured private-city all-cities payload remains only in the captured Montroyal private-city path.
- Empty towers/cities remains the fallback for non-captured private-city candidates.

## Recommended Next Extraction Task

Recommended next extraction task:

`AORebirth - Extract Private City Ready Init Sequencing From Playfield`

Why:

- It is localized compared to heartbeat, player combat, player death, teleport, and corpse loot.
- It is packet-order-sensitive enough to justify separation.
- It already has lifecycle trace labels and can be strengthened with a packet recorder before extraction.
- It can be extracted into a narrow coordinator/helper without implementing city ownership, CityAdvantages, OrgClient behavior, purchase logic, or guest-key lifecycle.

Suggested target boundary:

- A `PrivateCityReadyBlockCoordinator` or similarly narrow class owns:
  - private-city candidate checks;
  - captured Montroyal owned/non-owned city payload selection;
  - `OrgInfoPacket` send;
  - social/org stat send ordering;
  - `PlayfieldAllTowers` and `PlayfieldAllCities` ordering.

Keep in `Playfield.cs`:

- Runtime caller/wiring.
- Actual playfield identity ownership.
- Teleport/statel entry decisions until a separate guardrail exists.

## What Not To Touch Next

- Do not tune cleaning robot melee range, health, attack interval, movement, or damage in this separation task.
- Do not change corpse/despawn/loot behavior while extracting private-city ready/init.
- Do not touch GenericCmd, City Controller, guest key, org commands, or capture tooling.
- Do not extract heartbeat directly.
- Do not extract player combat or player death/respawn until trace/packet guardrails exist.
- Do not refactor teleport/statel routing until a dedicated lifecycle guardrail exists.
- Do not consolidate captured constants as part of the next extraction unless that is explicitly selected as a separate no-behavior task.

## Cleanup Performed

None.

No no-behavior cleanup was needed. The latest extraction left a clear orchestrator boundary for captured Arete robot spawn setup, and the remaining work should be planned as guardrail-first separation tasks rather than incidental cleanup.
