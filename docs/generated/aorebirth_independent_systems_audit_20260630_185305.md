# AORebirth Independent Systems Audit

Generated: 2026-06-30 18:53 local

Scope: independent codebase audit. No gameplay or code changes were made.

## Executive Summary

The dominant root cause is not one gameplay feature. The codebase repeatedly regresses because packet-order-sensitive gameplay lifecycles are implemented directly inside broad runtime handlers instead of through small, named flow coordinators with fixture-backed sequence tests.

The largest concentration of risk is `Playfield.cs`. It owns playfield loading, captured Arete spawns, captured robot patrol replay, private city org state, combat ticks, movement follow commands, player death, NPC death, corpse creation, loot, social status packets, and world/ready packet ordering. This makes every targeted fix a high-blast-radius change.

The second root cause is one-off captured packet patches inside input handlers, especially `GenericCmdMessageHandler.cs` and `OrgClient.cs`. These patches are often correct for a capture, but they are embedded in generic command flow without a separate state/lifecycle boundary, so owner/non-owner, player/NPC, corpse/item, and org/city cases can overlap.

The third root cause is missing packet-order and lifecycle regression tests. Existing serializer tests cover some message body shapes, but most failing systems were not body-shape-only bugs. They were sequence, target identity, state, lifecycle, or timing bugs.

The first foundational fix should be a packet/lifecycle regression harness for `Playfield`-owned flows. It should assert ordered packets and state transitions for a small set of scenarios before more combat, city, org, or visibility behavior is changed.

## Method

Inputs reviewed:

- `AI_START_HERE.md`
- `AGENTS.md`
- `docs/ai/WORKFLOW.md`
- `docs/ai/CURRENT_TASK.md`
- `docs/project/AOREBIRTH_REGRESSION_BACKLOG.md`
- `docs/generated/aorebirth_full_code_sweep_20260630_174335.md`

Code surfaces inspected:

- `AORebirth/Server/ZoneEngine/Core/Playfields/Playfield.cs`
- `AORebirth/Server/ZoneEngine/Core/Controllers/NPCController.cs`
- `AORebirth/Server/ZoneEngine/Core/MessageHandlers/GenericCmdMessageHandler.cs`
- `AORebirth/Server/ZoneEngine/Core/MessageHandlers/AttackMessageHandler.cs`
- `AORebirth/Server/ZoneEngine/Core/MessageHandlers/CharacterActionMessageHandler.cs`
- `AORebirth/Server/ZoneEngine/Core/MessageHandlers/CharInPlayMessageHandler.cs`
- `AORebirth/Server/ZoneEngine/Core/MessageHandlers/FullCharacterMessageHandler.cs`
- `AORebirth/Server/ZoneEngine/Core/MessageHandlers/OrgClientMessageHandler.cs`
- `AORebirth/Server/ZoneEngine/Core/PacketHandlers/ClientConnected.cs`
- `AORebirth/Server/ZoneEngine/Core/PacketHandlers/OrgClient.cs`
- `AORebirth/Server/ZoneEngine/Core/Packets/SimpleCharFullUpdate.cs`
- `AORebirth/Server/ZoneEngine/Core/CombatDamageRules.cs`
- `AORebirth/Libraries/Source/AORebirth.Database/Misc.cs`
- `AORebirth/Libraries/Source/AOtomation/AOtomation.Messaging/src/SmokeLounge.AOtomation.Messaging.Tests/N3RecoveredContractTests.cs`
- `tools-temp/AOSharpLiveCapture/Main.cs`

Conclusions were ranked from code evidence: ownership concentration, packet/lifecycle sensitivity, runtime data coupling, lack of test coverage, and the types of regressions recently produced by those structures.

## Top Root Problem Areas

### 1. Playfield is a packet-order and lifecycle god object

Risk level: P1

Evidence:

- `Playfield.cs:91` defines `Playfield`.
- `Playfield.cs:390` through `Playfield.cs:394` load statels, mob spawns, captured Arete spawns, vendors, and static dynels.
- `Playfield.cs:176` and `Playfield.cs:178` hardcode captured private-city org values.
- `Playfield.cs:228` through `Playfield.cs:253` define captured cleaning robot combat, corpse, loot, and weapon constants.
- `Playfield.cs:275` through `Playfield.cs:287` define captured Arete robot spawn rows and a `tools-temp` patrol replay path.
- `Playfield.cs:1182` sends private city pre-`FullCharacter` ready-block packets.
- `Playfield.cs:2296`, `Playfield.cs:2531`, `Playfield.cs:2591`, `Playfield.cs:2682`, and `Playfield.cs:2932` participate in combat timing and robot-specific attack flow.
- `Playfield.cs:3155`, `Playfield.cs:3311`, `Playfield.cs:3394`, `Playfield.cs:4112`, `Playfield.cs:4125`, `Playfield.cs:4218`, and `Playfield.cs:4520` cover death, corpse use, corpse full updates, loot, and stop-fight behavior.

Files/classes involved:

- `AORebirth/Server/ZoneEngine/Core/Playfields/Playfield.cs`
- `AORebirth/Libraries/Source/AORebirth.Core/Playfields/IPlayfield.cs`

Why it causes regressions:

Unrelated lifecycle phases are interleaved in one class. A robot death fix can touch corpse packet shape, player death behavior, stop-fight ordering, loot, despawn timing, and combat target state. A private-city ready-block fix can sit next to normal login/zoning and character visibility paths. This makes narrow fixes technically possible but structurally unsafe.

Systems affected:

- Combat
- NPC movement
- Death/corpse/loot
- Private city init
- Player visibility
- Playfield loading
- Captured content injection
- Social status and ready-block packet ordering

Examples of likely caused regressions:

- Cleaning robot death/corpse changes causing client crash.
- Private city org ready-block ordering needing repeated fixes.
- Robot health/combat timing/attack text fixes affecting unrelated combat paths.
- Player vs NPC corpse handling needing explicit separation.

Recommended stabilization task:

`AORebirth - Add Playfield Packet Lifecycle Regression Harness`

Start with no behavior change. Add a small harness that can exercise and assert packet order/state transitions for selected `Playfield` flows: private city entry, same-playfield character visibility, robot combat tick, robot death/corpse/despawn.

Validation needed:

- Static/order assertions for emitted packets.
- No AO client launch.
- Build only if production code changes are required.

What must not be touched:

- Do not refactor all of `Playfield.cs` first.
- Do not change combat, city, org, or corpse behavior while adding the harness.

### 2. Generic command handling is a stack of one-off captured routes

Risk level: P1

Evidence:

- `GenericCmdMessageHandler.cs:622` starts the `Use` command branch with Rex B18D handling.
- `GenericCmdMessageHandler.cs:652` handles the private city guest key terminal.
- `GenericCmdMessageHandler.cs:656` handles captured City Controller use.
- `GenericCmdMessageHandler.cs:678` routes dead NPC corpse use.
- `GenericCmdMessageHandler.cs:682` and `GenericCmdMessageHandler.cs:686` handle captured/grid terminal routes.
- `GenericCmdMessageHandler.cs:690` handles surgery clinic use.
- `GenericCmdMessageHandler.cs:827` implements guest key terminal behavior.
- `GenericCmdMessageHandler.cs:879` implements City Controller behavior.
- `GenericCmdMessageHandler.cs:1504`, `GenericCmdMessageHandler.cs:1584`, and `GenericCmdMessageHandler.cs:1699` implement more captured terminal routes.
- `GenericCmdMessageHandler.cs:2279` defines `CityControllerWindowCloseMessageHandler` in the same file.

Files/classes involved:

- `AORebirth/Server/ZoneEngine/Core/MessageHandlers/GenericCmdMessageHandler.cs`
- `CityControllerWindowCloseMessageHandler`

Why it causes regressions:

The generic `Use` path is acting as a dispatcher, feature router, packet builder, inventory mutator, corpse router, and UI close handler. Routes are ordered by branch position, not by an explicit interaction model. Capture-specific code is correct in isolation but fragile when multiple targets share identity families, target types, corpse routing, or UI callback behavior.

Systems affected:

- Guest Key Generator
- City Controller owner/member path
- City Controller non-org/guest path
- City Controller close button
- Corpse use acknowledgement
- Grid terminals
- Surgery clinics
- Rex quest targets

Examples of likely caused regressions:

- Guest Key Generator target identity mismatch.
- City Controller open crash or wrong limited menu.
- City Controller X close requiring a separate captured window identity handler.
- Dead NPC corpse use needing special routing away from normal `Use`.

Recommended stabilization task:

`AORebirth - Split GenericCmd Use Into Explicit Interaction Routes`

Do this incrementally: create an interaction route table or small per-feature handlers that return a result object. Do not change packet payloads. First preserve branch order with tests, then move one route at a time.

Validation needed:

- Static route-selection tests for key targets.
- Packet sequence snapshots for guest key, City Controller open, City Controller close, corpse use.

What must not be touched:

- Do not implement new city systems.
- Do not change captured packet bodies.
- Do not reorder routes without a test proving the old and new target selection match.

### 3. Combat, movement, and death lack an explicit lifecycle state machine

Risk level: P1

Evidence:

- `AttackMessageHandler.cs:57` handles attack messages.
- `AttackMessageHandler.cs:92` sets the fighting target directly.
- `AttackMessageHandler.cs:127` sets the target's fighting target.
- `Playfield.cs:2296` schedules captured robot combat ticks.
- `Playfield.cs:2531` branches robot combat source behavior.
- `Playfield.cs:2682` moves the robot toward combat target.
- `Playfield.cs:2724` through `Playfield.cs:2743` send combat FollowTarget movement.
- `Playfield.cs:3155` kills NPC targets.
- `Playfield.cs:4520` stops fights for a dead target.
- `NPCController.cs:310` emits captured patrol replay.
- `NPCController.cs:357`, `NPCController.cs:380`, and `NPCController.cs:391` control visible FollowTarget movement updates.
- `NPCController.cs:868` stops follow for combat range.

Files/classes involved:

- `AttackMessageHandler`
- `Playfield`
- `NPCController`
- `CombatDamageRules`

Why it causes regressions:

Combat is spread across input handling, playfield heartbeat/tick logic, NPC movement, damage selection, stop-fight, corpse conversion, and packet emission. There is no single state model for `Idle -> Aggro -> Chasing -> InRange -> Attacking -> Dying -> Corpse -> Despawned`. As a result, range fixes, animation timing fixes, movement fixes, and corpse fixes fight each other.

Systems affected:

- Cleaning robot combat
- Robot chase behavior
- Attack start/stop
- Damage packet text
- Death and corpse conversion
- Quest kill progress

Examples of likely caused regressions:

- Robot teleporting or jerky movement after movement patches.
- Combat range patches causing repeated "previous action" or repeated attack feedback.
- Damage text requiring SpecialAttackWeapon context before `AttackInfo`.
- Death/corpse order causing client crash.

Recommended stabilization task:

`AORebirth - Model Cleaning Robot Combat Lifecycle Without Behavior Changes`

Add a narrow lifecycle object or diagnostic state trace for the captured cleaning robot only. First observe and assert states; do not change timing or movement packets.

Validation needed:

- Static trace/order validation for attack start, chase, attack tick, stop fight, death, corpse, despawn.
- Mike live-tests only after a later behavior-changing task.

What must not be touched:

- Do not hardcode player stats.
- Do not rewrite all NPC movement.
- Do not change generic combat formulas in the modeling task.

### 4. Packet-order-sensitive systems lack sequence tests

Risk level: P1

Evidence:

- `N3RecoveredContractTests.cs:38`, `N3RecoveredContractTests.cs:46`, and `N3RecoveredContractTests.cs:569` cover some N3 message ids and FollowTarget body shapes.
- `N3RecoveredContractTests.cs:482` covers private city guest access card packet bodies.
- No inspected test covers City Controller open/close sequence, `/org info` response sequence, private city org init before `FullCharacter`, same-playfield visibility order, robot death/corpse/despawn order, or guest key lifecycle.
- `ClientConnected.cs:174`, `ClientConnected.cs:182`, and `ClientConnected.cs:185` emit SimpleCharFullUpdate, private city ready block, and FullCharacter in a sensitive order.
- `Playfield.cs:1182`, `Playfield.cs:1199`, `Playfield.cs:3980`, and `Playfield.cs:3987` emit ready-block/private-city/towers/cities packets.
- `FullCharacterMessageHandler.cs:57` builds FullCharacter with many unknown fields and inventory decisions.
- `SimpleCharFullUpdate.cs:58` builds SCFU visibility state.

Files/classes involved:

- `ClientConnected`
- `Playfield`
- `FullCharacterMessageHandler`
- `SimpleCharFullUpdate`
- `N3RecoveredContractTests`

Why it causes regressions:

Most recent failures were not just serializer body mismatches. They were order, target identity, state timing, or lifecycle bugs. Body-shape tests are useful but not sufficient. The code lacks scenario-level packet sequence tests around the exact flows that keep breaking.

Systems affected:

- Private city init
- `/org info`
- City Controller UI
- Same-playfield visibility
- Robot death/corpse
- Guest key lifecycle

Examples of likely caused regressions:

- `/org info` response sent but not displayed.
- Org init before `FullCharacter` needing repair.
- Two players in same playfield not rendering each other.
- City Controller close button not closing the UI.

Recommended stabilization task:

`AORebirth - Add Scenario Packet Order Tests For Existing Captured Flows`

Start with three tests: private city init before `FullCharacter`, City Controller open/close, and robot death/corpse/despawn. Keep them as black-box sequence assertions where possible.

Validation needed:

- Focused test run for new scenario tests.
- `git diff --check`.

What must not be touched:

- Do not modify packet payloads while adding tests.
- Do not launch AO client.

### 5. Runtime behavior depends on local capture artifacts under tools-temp

Risk level: P1

Evidence:

- `Playfield.cs:286` and `Playfield.cs:287` point captured cleaning robot patrol replay at `tools-temp/AOSharpLiveCapture/bin/Debug/captures/20260629-193121/movement-packets.csv`.
- `Playfield.cs:627` loads the path for patrol replay.
- `Playfield.cs:754` and `Playfield.cs:763` search filesystem candidates for the replay file.
- `NPCController.cs:298` accepts captured patrol replay segments.
- `NPCController.cs:310` emits replay movement.

Files/classes involved:

- `Playfield`
- `NPCController`
- `tools-temp/AOSharpLiveCapture` output

Why it causes regressions:

Capture output is evidence, but runtime behavior should not depend on a local uncommitted capture folder. This creates machine-specific behavior and makes later debugging ambiguous: a missing or changed local file can silently change NPC movement.

Systems affected:

- Cleaning robot idle patrol
- Fresh clone behavior
- CI/static validation
- Capture-to-runtime data provenance

Examples of likely caused regressions:

- Robots not moving on machines without Mike's capture folder.
- Movement behavior changing after capture-folder cleanup.
- Agents treating capture dumps as runtime content instead of evidence.

Recommended stabilization task:

`AORebirth - Promote Captured Patrol Replay To Committed Data`

Move the selected decoded movement rows into a committed evidence/data file and make runtime load from that file only. Keep raw captures as evidence references, not runtime dependencies.

Validation needed:

- Static file existence and parse validation.
- Mike live-tests robot idle patrol after the data move.

What must not be touched:

- Do not change movement algorithm.
- Do not regenerate captures.
- Do not run capture tooling.

### 6. Player and NPC death/corpse behavior share unsafe surfaces

Risk level: P1

Evidence:

- `Playfield.cs:2358` logs that player corpse visual is skipped because current `CorpseFullUpdate` is NPC-loot oriented and breaks modern death teleport flow.
- `Playfield.cs:4112` sends player corpse full update by calling the same corpse update helper.
- `Playfield.cs:4125` is the shared `SendCorpseFullUpdate` helper.
- `Playfield.cs:4218` sends corpse update from stored NPC corpse state.
- `GenericCmdMessageHandler.cs:674` acknowledges direct corpse use.
- `GenericCmdMessageHandler.cs:678` routes dead NPC corpse use separately.

Files/classes involved:

- `Playfield`
- `GenericCmdMessageHandler`
- `IPlayfield`

Why it causes regressions:

The same naming and helper surface covers player death visuals, NPC loot corpse creation, corpse use, corpse loot containers, and delayed despawn. The code already has a guard because one corpse shape breaks player death flow. That is strong evidence that player and NPC lifecycles need separate boundaries.

Systems affected:

- Player death/respawn
- NPC death
- Cleaning robot corpse
- Loot and credits
- Client crash risk

Examples of likely caused regressions:

- Client crash after killing robot.
- Need for separate dead-NPC corpse routing.
- Player corpse visual being disabled to avoid using NPC corpse packet shape.

Recommended stabilization task:

`AORebirth - Separate NPC Loot Corpse Lifecycle From Player Death Lifecycle`

Introduce naming and a small coordinator that makes it impossible to send NPC loot corpse packets through player death paths. Preserve existing packet bodies.

Validation needed:

- Static/order validation for robot death/corpse.
- Existing player death/respawn smoke after code changes.

What must not be touched:

- Do not change loot tables.
- Do not change player death visuals unless the task explicitly selects player death.

### 7. Captured constants are spread across runtime handlers without ownership boundaries

Risk level: P2

Evidence:

- `Playfield.cs:176` and `Playfield.cs:178` hold captured private-city org values.
- `Playfield.cs:228` through `Playfield.cs:253` hold captured robot constants.
- `Playfield.cs:275` through `Playfield.cs:283` hold captured robot spawn rows.
- `GenericCmdMessageHandler.cs:98` through `GenericCmdMessageHandler.cs:120` hold captured guest key constants.
- `GenericCmdMessageHandler.cs:130` through `GenericCmdMessageHandler.cs:158` hold captured City Controller constants.
- `OrgClient.cs:55`, `OrgClient.cs:57`, and `OrgClient.cs:59` hold captured org info fallback constants.

Files/classes involved:

- `Playfield`
- `GenericCmdMessageHandler`
- `OrgClient`

Why it causes regressions:

Captured constants are valid scaffolding when tied to evidence, but they become dangerous when they are spread across large handlers and used as production decision logic. It becomes unclear whether a value is a proven packet constant, a test fixture, a fallback, or live gameplay data.

Systems affected:

- Private city
- Guest keys
- City Controller
- `/org info`
- Cleaning robot combat/spawn/loot

Examples of likely caused regressions:

- Confusing organization id with faction/side.
- Runtime City Controller identities needing repeated patches.
- Robot HP/stat constants being changed in one path but not another.

Recommended stabilization task:

`AORebirth - Create Evidence-Owned Constant Registry`

Start documentation/data-only: move constants into evidence-named containers or metadata files with capture references and explicit scope. Do not change behavior first.

Validation needed:

- Build after any code movement.
- Focused packet/static tests around moved constants.

What must not be touched:

- Do not generalize private city ownership.
- Do not implement org systems.
- Do not alter packet values.

### 8. Org and city systems are patched as packet responses, not domain lifecycles

Risk level: P2

Evidence:

- `OrgClientMessageHandler.cs:22` routes captured `/org info`.
- `OrgClientMessageHandler.cs:32` through `OrgClientMessageHandler.cs:73` handle CityAdvantages.
- `OrgClient.cs:63` handles captured org info.
- `OrgClient.cs:148` handles BankAdd shape.
- `OrgClient.cs:522`, `OrgClient.cs:538`, `OrgClient.cs:597`, and `OrgClient.cs:868` show lifecycle TODOs for org management.
- `GenericCmdMessageHandler.cs:879` through `GenericCmdMessageHandler.cs:1000` builds City Controller AOTransportSignal responses directly.

Files/classes involved:

- `OrgClient`
- `OrgClientMessageHandler`
- `GenericCmdMessageHandler`
- `Playfield`

Why it causes regressions:

The code can respond to exact captured packets, but there is no clear org/city lifecycle boundary that owns membership state, permissions, controller menu state, guest keys, and org info. This forces every feature into target-specific packet patches.

Systems affected:

- `/org info`
- `/org bank` and future OrgClient commands
- City Controller owner/member path
- City Controller non-org/guest path
- Guest key permissions
- Private city ready/init

Examples of likely caused regressions:

- `/org info` response sent but not displayed until shape/order was fixed.
- City Controller owner/non-owner menu and close behavior needing separate patches.
- Guest key lifecycle remaining incomplete after creation worked.

Recommended stabilization task:

`AORebirth - Define Narrow City Org State Boundary`

Do not implement new features. Define what state is authoritative for existing captured behavior: org id/name/rank, controller owner/non-owner mode, guest key ownership/expiry.

Validation needed:

- Static validation that current packet responses are populated from the boundary.
- Mike live-tests only after behavior-changing follow-up tasks.

What must not be touched:

- Do not implement city purchase.
- Do not implement org management.
- Do not implement CityAdvantages unless explicitly selected.

### 9. Database import parser is hand-rolled and blocks development when it fails

Risk level: P0

Evidence:

- `Misc.cs:67` defines `CheckDatabase`.
- `Misc.cs:154` calls `ExecuteSqlStatementsUntilFirstInsert`.
- `Misc.cs:169` calls `ExecuteLargeSqlInserts`.
- `Misc.cs:204` starts large insert batching.
- `Misc.cs:331`, `Misc.cs:342`, `Misc.cs:346`, and `Misc.cs:401` perform hand parsing/substrings.
- `Misc.cs:415` flushes SQL batches.
- `Misc.cs:55` caps batches at `256 * 1024`.

Files/classes involved:

- `AORebirth/Libraries/Source/AORebirth.Database/Misc.cs`

Why it causes regressions:

This is not a gameplay regression source, but it is a structural development blocker. It uses string splitting and substring parsing for SQL import behavior that needs fixture coverage. Failures prevent other developers from reaching gameplay testing.

Systems affected:

- Fresh clone setup
- ZoneEngine startup
- SQL import/update path
- Developer onboarding

Examples of likely caused regressions:

- `ArgumentOutOfRangeException` during startup import.
- MySQL `max_allowed_packet` failure on large insert execution.

Recommended stabilization task:

`AORebirth - Add Fixture-Backed Database Import Regression Test`

Start with parser-only or fixture-backed tests. Do not change schema or run destructive database operations.

Validation needed:

- Fixture proving no negative substring length.
- Fixture proving large insert batching stays below configured size.

What must not be touched:

- Do not change database schema.
- Do not wipe or mass-edit data.
- Do not change gameplay code.

### 10. Capture tooling is valuable but not yet separated from comparison and runtime data workflows

Risk level: P2

Evidence:

- `tools-temp/AOSharpLiveCapture/Main.cs:187` writes `enemy-state.csv`.
- `tools-temp/AOSharpLiveCapture/Main.cs:195` writes `movement-packets.csv`.
- `tools-temp/AOSharpLiveCapture/Main.cs:455` writes `dynels.csv`.
- `tools-temp/AOSharpLiveCapture/Main.cs:1113` exports FollowTarget packets.
- `tools-temp/AOSharpLiveCapture/Main.cs:1149`, `tools-temp/AOSharpLiveCapture/Main.cs:1236`, and `tools-temp/AOSharpLiveCapture/Main.cs:1340` decode FollowTarget variants.
- `tools-temp/AOSharpLiveCapture/Main.cs:3338` reports combat packets without enemy state rows.
- `tools-temp/AOSharpLiveCapture/Main.cs:3365` reports no FollowTarget movement packets observed.

Files/classes involved:

- `tools-temp/AOSharpLiveCapture/Main.cs`
- Generated capture reports
- Runtime consumers in `Playfield` / `NPCController`

Why it causes regressions:

Capture tooling now captures more useful evidence, but static dynel data, active behavior packets, comparison reports, and runtime replay data are still too easy to mix together. Runtime code consuming `tools-temp` output is the clearest boundary violation.

Systems affected:

- NPC spawn reconstruction
- Enemy combat capture
- Movement comparison
- Runtime patrol replay
- Future capture-backed parity tasks

Examples of likely caused regressions:

- Static dynel dump being used to infer behavior.
- Movement patches based on insufficient decoded fields.
- Runtime patrol depending on local capture folders.

Recommended stabilization task:

`AORebirth - Separate Capture Evidence, Comparison Output, And Runtime Data`

Create a documented promotion workflow: raw capture -> generated comparison report -> reviewed committed runtime data. Do not let runtime load raw capture folders.

Validation needed:

- Existing capture folders can still be analyzed.
- Runtime data file is committed and parse-validated.

What must not be touched:

- Do not launch capture tooling.
- Do not change AORebirth gameplay while defining the workflow.

## Cross-Cutting Patterns

- Packet order logic is embedded directly in gameplay handlers instead of named sequence coordinators.
- Shared files do unrelated jobs: `Playfield.cs` mixes loading, ready blocks, combat, movement, corpse, loot, city, and captured robot data.
- Generic input handlers route target-specific gameplay patches without explicit interaction ownership.
- Gameplay lifecycles are implicit. Combat, death, corpse, guest key, and controller menu states are spread across methods instead of state machines or phase objects.
- Runtime behavior depends on `tools-temp` capture output.
- Capture constants are valid but scattered across runtime classes without ownership metadata.
- Player and NPC behavior share helper surfaces that already proved unsafe around corpse visuals.
- Serializer tests exist, but scenario packet-order tests are missing for the systems that actually regressed.
- Capture tooling, comparison reports, and runtime replay data are not separated enough.
- Documentation drift is a symptom of weak ownership boundaries, not the primary root cause.

## What Not To Fix First

- Do not make another melee range tweak first. It is a symptom of missing combat lifecycle and packet sequence evidence.
- Do not broadly refactor `Playfield.cs` first. Add tests/harnesses before moving behavior.
- Do not consolidate constants first. That helps maintainability but does not stop lifecycle/order regressions by itself.
- Do not rewrite NPC movement from visual symptoms. Movement must stay capture-backed.
- Do not implement broader `/org` or city ownership systems. Current org/city issues are boundary and lifecycle issues, not feature-completeness issues.
- Do not treat documentation cleanup as the root fix. Docs drift matters, but it follows from code ownership and task-scope ambiguity.
- Do not patch database schema or wipe data to address importer failures.

## Recommended First 3 Foundation Tasks

### 1. AORebirth - Add Playfield Packet Lifecycle Regression Harness

Why it is first:

The largest repeated regression source is packet/lifecycle order in `Playfield`-owned flows. A harness gives future fixes a safety net before combat, corpse, city, org, or visibility behavior changes again.

Scope:

- Add a test or diagnostic harness that can capture emitted packet order for selected flows.
- Start with existing behavior only.
- Cover private city ready block, robot death/corpse/despawn, and same-playfield visibility entry order.

Files likely involved:

- `Playfield.cs`
- `ClientConnected.cs`
- `CharInPlayMessageHandler.cs`
- test project or new test fixture surface

Validation required:

- Focused packet-order tests pass.
- `git diff --check`.
- No AO client launch.

What not to touch:

- Do not change packet payloads.
- Do not refactor `Playfield.cs`.
- Do not alter combat, city, org, or visibility behavior.

### 2. AORebirth - Promote Captured Robot Runtime Data Out Of tools-temp

Why it is first:

Runtime patrol behavior currently depends on a local capture output path. This is a machine-specific dependency and a direct evidence/runtime boundary violation.

Scope:

- Promote selected decoded patrol rows into a committed data file.
- Runtime loads only from committed data.
- Raw capture remains evidence only.

Files likely involved:

- `Playfield.cs`
- `NPCController.cs`
- new committed runtime/evidence data file

Validation required:

- Static parse validation for committed patrol data.
- Mike live-tests robot patrol after the change.

What not to touch:

- Do not change movement packet shape.
- Do not change chase/combat movement.
- Do not run capture tooling.

### 3. AORebirth - Add Fixture-Backed Database Import Regression Test

Why it is first:

This is the only P0 development blocker found. It does not explain most gameplay regressions, but it can prevent other developers from starting the server and validating any fixes.

Scope:

- Add parser/import fixtures for known problem SQL shapes.
- Prove batching stays below packet limits.
- Prove malformed insert parsing fails safely.

Files likely involved:

- `Misc.cs`
- database test fixtures
- database/parser test project

Validation required:

- Focused database import/parser test.
- No destructive database operations.

What not to touch:

- Do not change database schemas.
- Do not wipe or mass-edit data.
- Do not change gameplay systems.

## Follow-Up Backlog Corrections

- Add a new P1 foundation item: `Playfield Packet Lifecycle Regression Harness`. This should be above symptom-specific combat, city, org, and visibility packet fixes.
- Keep `P0-DB-001` as P0, but classify it as a development availability blocker rather than the primary cause of gameplay regressions.
- Keep `P1-COMBAT-001`, but make it depend on the lifecycle harness or an equivalent capture-backed packet sequence comparison.
- Keep `P1-MOVE-001`, but broaden it to a runtime data boundary task: raw capture folders must not be runtime dependencies.
- Add a P1 item for `GenericCmd Use route separation` because guest key, City Controller, corpse use, grid, surgery, and quest target behavior are all stacked in one branch.
- Add a P1 item for `NPC loot corpse lifecycle separation from player death lifecycle`.
- Move pure constant consolidation to P3 unless it is paired with tests; constants are a maintainability problem, but lifecycle/order bugs are the regression driver.
- Add an explicit prerequisite to org/city backlog work: define a narrow state boundary before adding more command/menu behavior.
