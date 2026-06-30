# AORebirth Regression Backlog

## Purpose

This backlog was generated from `docs/generated/aorebirth_full_code_sweep_20260630_174335.md`. Its purpose is to keep future Codex work scoped, prioritized, and separated by system instead of drifting between private city work, org commands, player visibility, capture tooling, database startup, and enemy combat in the same task.

This is a backlog, not an active implementation plan. Each item should become its own narrowly scoped Codex task before code changes are made.

## Current Active Scope

Current active gameplay scope: `Captured Cleaning Robot Combat Parity`.

Private-city, org, player-visibility, database, and capture-tooling items are still important backlog items, but they are not automatically the active implementation task unless Mike explicitly selects one.

## Priority Legend

- P0: startup failure, data loss, crash, or blocks development
- P1: major gameplay broken or live-client crash
- P2: incomplete gameplay parity or fragile partial system
- P3: cleanup, refactor, validation, documentation

## P0 Items

### P0-DB-001 - Add fixture-backed database startup/import regression test

Priority: P0
Status: Broken; needs validation
Evidence: The audit records developer startup failures in `Misc.CheckDatabase()`, including `ArgumentOutOfRangeException` from `Substring` and `MySqlException: Got a packet bigger than 'max_allowed_packet' bytes`. The current importer uses `ExecuteSqlStatementsUntilFirstInsert`, `ExecuteLargeSqlInserts`, insert parsing, and batch flushing in `Misc.cs`.
Why it matters: Fresh clones and developer onboarding are blocked if ZoneEngine cannot start after database import.
Likely files: `AORebirth/Libraries/Source/AORebirth.Database/Misc.cs`, database test or fixture files, SQL import fixture files.
What not to touch: Database schema, destructive database operations, gameplay code.
Validation: Add a parser/import fixture that proves no negative substring length and proves large insert batching remains below packet limits.
Live test: Not required; this is startup/import validation.
Suggested next task: `AORebirth - Add Fixture-Backed Database Import Regression Test`

## P1 Items

### P1-COMBAT-001 - Cleaning robot melee range and attack-start parity

Priority: P1
Status: Broken; needs capture
Evidence: The audit records that out-of-range melee damage can start across the platform and that attack animation does not line up until the robot closes distance. Recent guessed range patches were reverted.
Why it matters: Combat cannot be trusted if damage, animation, and valid melee range are out of sync.
Likely files: `AORebirth/Server/ZoneEngine/Core/Playfields/Playfield.cs`, `AORebirth/Server/ZoneEngine/Core/Controllers/NPCController.cs`, combat message handlers, `CombatDamageRules.cs`.
What not to touch: Player stat hardcoding, unrelated damage formulas, NPC patrol replay, private city, org commands.
Validation: Compare one live capture and one private capture from out-of-range attack start through first valid hit/animation, then patch only the proven packet or state difference.
Live test: Mike confirms attack cannot damage from invalid range and that attack animation starts at the live-matching point.
Suggested next task: `AORebirth - Capture-Backed Melee Range And Attack-Start Parity`

### P1-MOVE-001 - Move captured robot patrol replay data out of tools-temp

Priority: P1
Status: Partial; needs validation
Evidence: The audit found robot patrol replay loading from `tools-temp/AOSharpLiveCapture/bin/Debug/captures/20260629-193121/movement-packets.csv` through `Playfield.cs`, with replay state emitted by `NPCController.cs`.
Why it matters: Runtime gameplay depends on a local capture folder that may not exist on fresh clones, other developer machines, or CI.
Likely files: `AORebirth/Server/ZoneEngine/Core/Playfields/Playfield.cs`, `AORebirth/Server/ZoneEngine/Core/Controllers/NPCController.cs`, a new committed capture-derived data file.
What not to touch: NPC movement algorithms, combat chase, live capture tooling behavior.
Validation: Static validation that the committed route file exists, decodes, and maps expected captured robot identities.
Live test: Mike confirms robots still patrol like live after the data file move.
Suggested next task: `AORebirth - Promote Cleaning Robot Patrol Replay Data To Committed Evidence`

### P1-CORPSE-001 - Protect cleaning robot death and corpse packet order from client crashes

Priority: P1
Status: Partial; needs validation
Evidence: The audit records that incorrect corpse visual paths previously caused a live client crash. Current code sends NPC loot corpse `CorpseFullUpdate` and explicitly skips player corpse visual because the NPC corpse shape breaks player death teleport flow.
Why it matters: Corpse packet shape mistakes can crash the AO client.
Likely files: `AORebirth/Server/ZoneEngine/Core/Playfields/Playfield.cs`, corpse loot handlers, death/respawn handlers.
What not to touch: Player death corpse visuals, unrelated loot systems, robot movement.
Validation: Add or run an order validation for `StopFight -> CharacterAction Death Parameter2=500 -> CorpseFullUpdate -> delayed despawn`.
Live test: Mike confirms robot death spawns a usable corpse, corpse loot works, corpse despawns, and the AO client does not crash.
Suggested next task: `AORebirth - Cleaning Robot Death Corpse And Despawn Order Validation`

### P1-CITY-001 - City Controller non-org, guest, and limited-menu path status

Priority: P1
Status: Unknown; needs live test
Evidence: The audit lists City Controller open/close as exact captured signal paths and flags the controller as high risk because window open/close relies on captured signal and window identities. Prior work included non-org/guest menu crash reports.
Why it matters: The City Controller is a live-client-crash-sensitive UI path, and non-owner/guest access must not receive owner-only packet shapes.
Likely files: `AORebirth/Server/ZoneEngine/Core/MessageHandlers/GenericCmdMessageHandler.cs`, City Controller packet/message classes.
What not to touch: City purchase, ownership management, CityAdvantages, OrgClient flows.
Validation: Compare owner/member and non-org/guest controller use captures against private output and validate the limited menu response path separately.
Live test: Mike confirms non-org/guest controller open does not crash and shows only the captured limited menu behavior.
Suggested next task: `AORebirth - Validate City Controller Non-Org Limited Menu Path`

## P2 Items

### P2-CITY-001 - Private city zoning and initialization order regression coverage

Priority: P2
Status: Working; needs validation
Evidence: The audit says private city zoning/init was reported working, but packet order remains sensitive and lacks a focused static test for org init before `FullCharacter`.
Why it matters: Small ready-block order changes can leave the client in the wrong city/org state.
Likely files: `AORebirth/Server/ZoneEngine/Core/Playfields/Playfield.cs`, private city init packet handlers, org stat/init helpers.
What not to touch: City purchase, CityAdvantages, ownership management.
Validation: Static/order validation that private city entry sends captured org init state before `FullCharacter`.
Live test: Mike confirms private city zoning still works after validation-only changes.
Suggested next task: `AORebirth - Add Private City Init Order Regression Validation`

### P2-CITY-002 - Guest Key Generator lifecycle parity

Priority: P2
Status: Partial
Evidence: Guest Key Generator creates captured City Access Card template `280642`, but the audit says lifecycle parity is unproven.
Why it matters: Guest keys must be usable by guests after zoning and expire after the intended window.
Likely files: `AORebirth/Server/ZoneEngine/Core/MessageHandlers/GenericCmdMessageHandler.cs`, inventory persistence files, item expiration/lifecycle files.
What not to touch: City purchase, CityAdvantages, org commands.
Validation: Generate multiple keys, zone, relog if supported, verify overflow placement, verify expiration, and verify stale key use fails.
Live test: Mike confirms keys persist and expire like live.
Suggested next task: `AORebirth - Guest Key Lifecycle Parity`

### P2-CITY-003 - City Controller owner and member path regression validation

Priority: P2
Status: Working; needs validation
Evidence: The audit says City Controller use recognizes captured/runtime controller ids and sends captured menu signals, but this path relies on hardcoded captured identities and text.
Why it matters: Owner/member access must keep the captured menu shape without drifting into speculative city systems.
Likely files: `AORebirth/Server/ZoneEngine/Core/MessageHandlers/GenericCmdMessageHandler.cs`, city controller packet helpers.
What not to touch: CityAdvantages, OrgClient command flows, ownership management, purchase logic.
Validation: Validate captured AOTransportSignal sequence and org text for owner/member controller use.
Live test: Mike confirms owner/member menu opens and remains usable.
Suggested next task: `AORebirth - City Controller Owner Member Menu Regression Validation`

### P2-CITY-004 - City Controller close handling regression validation

Priority: P2
Status: Working; needs validation
Evidence: The audit says X close handling is implemented through `CityControllerWindowCloseMessageHandler` with captured window instance `0x0000C000` and signal `7`.
Why it matters: The close button is exact-window-id sensitive and previously failed.
Likely files: `AORebirth/Server/ZoneEngine/Core/MessageHandlers/GenericCmdMessageHandler.cs`, close-window packet classes.
What not to touch: Controller open menu shape, city ownership systems, OrgClient commands.
Validation: Static packet validation for close-window request identity and captured close response payload.
Live test: Mike confirms the X button closes the City Controller window.
Suggested next task: `AORebirth - Add City Controller Close Packet Regression Validation`

### P2-ORG-001 - OrgClient and broader /org command scope cleanup

Priority: P2
Status: Partial
Evidence: The audit says `/org info` works, but OrgClient now has nearby command behavior, CityAdvantages handling, and org lifecycle TODOs.
Why it matters: Broad org systems are easy to accidentally implement beyond captured evidence.
Likely files: `AORebirth/Server/ZoneEngine/Core/PacketHandlers/OrgClient.cs`, `AORebirth/Server/ZoneEngine/Core/MessageHandlers/OrgClientMessageHandler.cs`.
What not to touch: `/org create`, invite, leave, promote, demote, kick, disband, bank add/remove, tax, vote, contract, CityAdvantages unless explicitly selected.
Validation: Document and validate only intentionally supported OrgClient commands.
Live test: Mike confirms selected `/org` commands only when each command is explicitly in scope.
Suggested next task: `AORebirth - Document And Quarantine OrgClient Command Scope`

### P2-ORG-002 - /org info and org init packet shape/order validation

Priority: P2
Status: Working; needs validation
Evidence: The audit says `/org info` displays, but the OrgClient/OrgServer response and org init ordering are packet-shape sensitive.
Why it matters: A tiny target identity, response shape, or init-order drift can make `/org info` silently do nothing again.
Likely files: `AORebirth/Server/ZoneEngine/Core/PacketHandlers/OrgClient.cs`, `AORebirth/Server/ZoneEngine/Core/MessageHandlers/OrgClientMessageHandler.cs`, private city/org init code.
What not to touch: Full `/org` management, org bank flows, CityAdvantages.
Validation: Static validation for captured OrgServer `/org info` shape and org init before `FullCharacter`.
Live test: Mike confirms `/org info` displays after zoning/login.
Suggested next task: `AORebirth - Add Org Info Packet Shape And Init Order Regression Validation`

### P2-VIS-001 - Same-playfield two-client visibility regression checklist

Priority: P2
Status: Working; needs validation
Evidence: The audit says player visibility was reported fixed, but TODOs remain near same-playfield update behavior in `CharInPlayMessageHandler.cs`.
Why it matters: Player rendering and movement visibility are core multiplayer behavior and can regress with packet-order changes.
Likely files: `AORebirth/Server/ZoneEngine/Core/MessageHandlers/CharInPlayMessageHandler.cs`, player connection/init handlers, SimpleChar/FullCharacter update classes.
What not to touch: Combat AI, private city, org commands.
Validation: Two-client checklist for first viewer, second viewer, zoning in/out, relog, movement, and remote model rendering.
Live test: Mike confirms both clients see each other consistently in the same playfield.
Suggested next task: `AORebirth - Same-Playfield Two-Client Visibility Regression Checklist`

### P2-PACKET-001 - SimpleChar, FullCharacter, and SCFU packet ordering validation

Priority: P2
Status: Needs validation
Evidence: The audit separates visibility and packet-ordering risks, and notes that capture timing can miss pre-existing `SimpleCharFullUpdate` data unless zoning occurs after capture starts.
Why it matters: Rendering, combat target identity, org state, and visibility can all depend on correct full-update ordering.
Likely files: SimpleChar update classes, FullCharacter message handlers, connection and zoning init handlers.
What not to touch: Packet payload guesses, unrelated login flow rewrites, combat behavior.
Validation: Static/order tests for login/zoning ready blocks and same-playfield character update ordering.
Live test: Mike confirms visible character models appear after zoning and relog.
Suggested next task: `AORebirth - Add Character Update Packet Ordering Regression Validation`

### P2-COMBAT-001 - Cleaning robot combat stats, timing, and damage parity

Priority: P2
Status: Partial
Evidence: The audit records captured robot MonsterData `297023`, HP `12`, RunSpeed `5/6`, damage text fixes, and fallback damage rules. Mike later confirmed damage text was fixed, but combat timing and stat parity remain sensitive.
Why it matters: Robot combat is the current active gameplay target and should stay capture-backed.
Likely files: `AORebirth/Server/ZoneEngine/Core/Playfields/Playfield.cs`, `AORebirth/Server/ZoneEngine/Core/Controllers/NPCController.cs`, `AORebirth/Server/ZoneEngine/Core/CombatDamageRules.cs`.
What not to touch: Hardcoded player stat guesses, unrelated NPC systems, private city.
Validation: Capture-backed comparison of robot HP, attack interval, damage type, and damage amount.
Live test: Mike confirms robot HP, attack speed, damage amount, and damage type feel live-matching.
Suggested next task: `AORebirth - Cleaning Robot Combat Stats And Timing Parity`

### P2-CORPSE-001 - Cleaning robot death, corpse, despawn, and loot parity

Priority: P2
Status: Partial; needs validation
Evidence: The audit says robot corpse output exists, but corpse loot and credit values need capture-backed validation.
Why it matters: On live, the fought enemy changes into a corpse that contains actual loot; the live enemy itself does not hold loot.
Likely files: `AORebirth/Server/ZoneEngine/Core/Playfields/Playfield.cs`, corpse loot generation, inventory/container handlers.
What not to touch: Player death, player corpse visuals, unrelated loot tables.
Validation: Verify death order, corpse identity, loot items, credit amount, corpse use, and delayed despawn against capture.
Live test: Mike confirms robot corpse appears, can be looted, contains captured loot/credits, and despawns.
Suggested next task: `AORebirth - Cleaning Robot Loot And Corpse Lifecycle Parity`

### P2-MOVE-001 - Cleaning robot idle patrol and movement tooling fixtures

Priority: P2
Status: Partial; needs validation
Evidence: The audit says FollowTarget packet shape now has serializer tests and AOSharpLiveCapture emits structured `movement-packets.csv`, but no committed replay fixture proves movement decode against a known capture folder.
Why it matters: Movement fixes went wrong when based on visual guesses and raw hex interpretation.
Likely files: `tools-temp/AOSharpLiveCapture/Main.cs`, movement packet tests, captured replay fixture data, `NPCController.cs`.
What not to touch: Runtime movement behavior until fixture comparison proves the difference.
Validation: Add replay/fixture validation for selected live and private movement captures.
Live test: Mike confirms movement only after fixture-backed patch tasks.
Suggested next task: `AORebirth - Add FollowTarget Movement Decode Replay Fixture`

### P2-CAPTURE-001 - Capture tooling and live/private comparison workflow backlog

Priority: P2
Status: Partial
Evidence: The audit says static dynel dump is for positions/static stats only, active packet capture is for behavior, and no compact live/private comparison report currently identifies movement field differences.
Why it matters: Capture-backed parity work depends on keeping evidence types separate and comparison output usable.
Likely files: `tools-temp/AOSharpLiveCapture/Main.cs`, generated capture reports, docs/ai workflow references.
What not to touch: AORebirth gameplay code, live capture launch behavior unless explicitly selected.
Validation: Generate a compact comparison report from one live and one private capture without changing gameplay.
Live test: Not required for report generation; Mike supplies captures.
Suggested next task: `AORebirth - Add Live Private Capture Comparison Report`

### P2-MAGIC-001 - Hardcoded captured constants and magic values inventory

Priority: P2
Status: Partial
Evidence: The audit lists captured org ids, city controller ids, guest key template, robot MonsterData, death parameter, and static identity constants spread across gameplay code.
Why it matters: Capture constants are valid evidence, but scattering them makes later systems harder to reason about and easier to regress.
Likely files: `Playfield.cs`, `GenericCmdMessageHandler.cs`, `OrgClient.cs`, `PlayfieldLoader.cs`.
What not to touch: Do not generalize city ownership, org membership, or robot combat while active parity work is unstable.
Validation: Documentation-only inventory first; code consolidation only in a later explicit task.
Live test: Not required for documentation; required after any code consolidation.
Suggested next task: `AORebirth - Inventory Capture Constants And Evidence Owners`

### P2-VALIDATION-001 - Static, unit, and regression validation gaps

Priority: P2
Status: Needs validation
Evidence: The audit found serializer contract coverage for some N3 messages but missing focused tests for guest key lifecycle, controller close, `/org info`, private city init order, visibility, robot combat, robot corpse, database import, and capture-tool replay.
Why it matters: Capture-backed fixes keep regressing because there are few narrow tests around exact packet/order behavior.
Likely files: messaging test projects, ZoneEngine test surfaces, capture-tool replay tests, docs validation checklists.
What not to touch: Gameplay implementation code while adding test-only coverage unless an explicit test requires a seam.
Validation: Add one small validation per selected behavior, starting with the highest-priority system.
Live test: Not required for static tests; required for final gameplay confidence.
Suggested next task: `AORebirth - Add Focused Regression Tests For Capture-Backed Gameplay`

## P3 Items

### P3-DOCS-001 - Documentation and task-state drift cleanup

Priority: P3
Status: Partial
Evidence: The audit says `CURRENT_TASK.md` still reflects older enemy work and `PROJECT_STATE.md` still says the active cleanup/current task is surgery-clinic implant-install behavior.
Why it matters: Stale task docs cause future agents to enter the wrong subsystem and re-open completed work.
Likely files: `docs/ai/CURRENT_TASK.md`, `docs/project/PROJECT_STATE.md`, this backlog.
What not to touch: Gameplay code, generated capture evidence.
Validation: `git diff --check`; no build required for docs-only changes.
Live test: Not required.
Suggested next task: `AORebirth - Refresh Active Task And Project State Documentation`

### P3-CONSTANTS-001 - Consolidate captured constants after active parity stabilizes

Priority: P3
Status: Needs validation
Evidence: The audit recommends moving proven constants into narrow evidence-named classes or data files only after active combat work stabilizes.
Why it matters: Centralized evidence constants make future audits easier without changing behavior.
Likely files: `Playfield.cs`, `GenericCmdMessageHandler.cs`, `OrgClient.cs`, potential evidence constant/data files.
What not to touch: Behavior, packet shapes, city ownership, org systems.
Validation: Build plus focused smoke/static validation after any code movement.
Live test: Mike confirms no behavior changed.
Suggested next task: `AORebirth - Consolidate Captured Constants Without Behavior Changes`

### P3-TODO-001 - Clean stale TODOs only after owning systems are selected

Priority: P3
Status: Unknown
Evidence: The audit found TODOs near player visibility and org lifecycle paths.
Why it matters: TODO cleanup can accidentally widen scope if it is done outside the owning gameplay task.
Likely files: `CharInPlayMessageHandler.cs`, `OrgClient.cs`, connection/init handlers.
What not to touch: Any TODO outside the selected subsystem.
Validation: Documentation or code review depending on selected TODO.
Live test: Only required if behavior changes.
Suggested next task: `AORebirth - Triage Stale TODOs By Owning Subsystem`

### P3-CAPTURE-001 - Make capture comparison reports easier to use

Priority: P3
Status: Partial
Evidence: The audit says no single compact report currently says "live vs private movement fields differ here."
Why it matters: Better comparison reports reduce guesswork and shorten packet-backed debugging loops.
Likely files: `tools-temp/AOSharpLiveCapture/Main.cs`, generated report scripts, docs/generated templates.
What not to touch: Gameplay runtime behavior.
Validation: Run report generation against existing capture folders and confirm concise live/private differences.
Live test: Not required.
Suggested next task: `AORebirth - Generate Compact Live Private Packet Diff Reports`

## Recommended Next Single Task

Recommended next task: `AORebirth - Add Fixture-Backed Database Import Regression Test`.

Reason: the audit classifies database startup/import as the only P0-level issue because it can block fresh-clone startup and other developers before gameplay work can even be tested. This should be handled before more gameplay parity work unless Mike explicitly prioritizes the active cleaning robot combat issue next.
