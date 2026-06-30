# AORebirth Full Code Sweep And Regression Audit

Generated: 2026-06-30 17:43 local

Scope: read-only audit. No code fixes were made.

## Executive Summary

AORebirth is currently in a fragile but recoverable state. The highest-risk pattern is not one isolated subsystem; it is repeated capture-backed gameplay work landing as narrow runtime patches without enough static regression coverage, while the active-state documentation has drifted behind the real working state.

The current gameplay focus should remain `Malfunctioning Cleaning Robot` combat parity. The recent melee range / attack-start work was reverted, which restored the previous baseline, but the underlying out-of-range melee behavior remains unresolved and should not be patched again without a fresh live/private packet comparison.

Private city, guest key, city controller, `/org info`, player visibility, robot death/corpse, and robot idle movement all have capture-backed code paths present. Several are working by Mike's live testing, but many still rely on hardcoded captured identities, captured fallback strings, or tooling output under `tools-temp`. Those are acceptable as temporary parity scaffolding, but they are high-risk regression points.

## Current Git Status At Audit Start

`cmd /d /c git status --short --branch`

Result:

```text
## master...origin/master
```

## Files Inspected

- `AI_START_HERE.md`
- `AGENTS.md`
- `docs/ai/WORKFLOW.md`
- `docs/ai/CURRENT_TASK.md`
- `docs/project/PROJECT_STATE.md`
- `AORebirth/Server/ZoneEngine/Core/Playfields/Playfield.cs`
- `AORebirth/Server/ZoneEngine/Core/Controllers/NPCController.cs`
- `AORebirth/Server/ZoneEngine/Core/MessageHandlers/GenericCmdMessageHandler.cs`
- `AORebirth/Server/ZoneEngine/Core/MessageHandlers/OrgClientMessageHandler.cs`
- `AORebirth/Server/ZoneEngine/Core/PacketHandlers/OrgClient.cs`
- `AORebirth/Server/ZoneEngine/Core/MessageHandlers/CharInPlayMessageHandler.cs`
- `AORebirth/Server/ZoneEngine/Core/CombatDamageRules.cs`
- `AORebirth/Server/ZoneEngine/Content/Playfields/PlayfieldLoader.cs`
- `AORebirth/Libraries/Source/AORebirth.Database/Misc.cs`
- `AORebirth/Libraries/Source/AOtomation/AOtomation.Messaging/src/SmokeLounge.AOtomation.Messaging.Tests/N3RecoveredContractTests.cs`
- `tools-temp/AOSharpLiveCapture/Main.cs`

## Systems Audited

- Active task and project-state documentation
- Private city initialization and controller/terminal use paths
- OrgClient `/org info` handling and surrounding org command scope
- Same-playfield player visibility risk points
- Cleaning robot spawn, combat, movement, death, corpse, and loot lifecycle
- AOSharpLiveCapture static dynel, enemy fight, and movement packet tooling
- Database startup/import path
- Hardcoded captured values and magic packet constants
- Validation and test coverage gaps

## Confirmed Working Systems

Working status here means either current code contains the expected path or Mike reported successful live validation during recent work. This audit did not launch the AO client.

- Private city zoning and initialization are represented in code and were reported working after prior fixes.
- Guest Key Generator use path creates captured City Access Card template `280642` for the runtime terminal `Terminal:574B84AB` in `GenericCmdMessageHandler.cs:98`, `GenericCmdMessageHandler.cs:100`, and `GenericCmdMessageHandler.cs:867`.
- City Controller use path recognizes captured/runtime controller ids and sends captured menu signals in `GenericCmdMessageHandler.cs:130`, `GenericCmdMessageHandler.cs:132`, `GenericCmdMessageHandler.cs:943`, and `GenericCmdMessageHandler.cs:951`.
- City Controller X close handling is implemented by `CityControllerWindowCloseMessageHandler` with captured window instance `0x0000C000` in `GenericCmdMessageHandler.cs:2279` and `GenericCmdMessageHandler.cs:2317`.
- `/org info` has a decoded OrgClient path and captured OrgServer response fallback values in `OrgClient.cs:55`, `OrgClient.cs:57`, `OrgClient.cs:59`, and `OrgClientMessageHandler.cs:22`.
- Cleaning robot captured monster identity and death parameter are represented in `Playfield.cs:230` and `Playfield.cs:243`.
- Cleaning robot corpse output exists through `CorpseFullUpdate` helpers in `Playfield.cs:4125`, `Playfield.cs:4140`, and `Playfield.cs:4218`.
- FollowTarget packet shape has serializer contract coverage in `N3RecoveredContractTests.cs:569`, `N3RecoveredContractTests.cs:604`, and `N3RecoveredContractTests.cs:640`.
- AOSharpLiveCapture now emits movement packet output and health checks for decoded FollowTarget rows in `tools-temp/AOSharpLiveCapture/Main.cs:195`, `tools-temp/AOSharpLiveCapture/Main.cs:1111`, `tools-temp/AOSharpLiveCapture/Main.cs:1149`, and `tools-temp/AOSharpLiveCapture/Main.cs:3350`.

## Confirmed Broken Or Incomplete Systems

### P1 - Out-of-range melee attack behavior remains unresolved

Mike reported that a player can start melee damage against a robot from across the platform and that attack animation does not line up until the target closes distance. The later attempted combat-range patches were reverted at HEAD, so the regression was removed, but the original range/animation issue remains open.

Risk surface:

- `NPCController.cs:370` and `NPCController.cs:391` control visible FollowTarget chase updates.
- `Playfield.cs:2739` and `Playfield.cs:2743` log FollowTarget combat start/continue behavior.
- `CombatDamageRules.cs:7` and `CombatDamageRules.cs:9` still provide fallback damage values.

Do not patch this again by approximation. The next task needs a live/private capture comparison of one out-of-range attack start through first valid hit/animation.

### P1 - Robot patrol replay depends on `tools-temp` capture output

Cleaning robot patrols are currently loaded from a capture CSV path under `tools-temp`:

- `Playfield.cs:287` references `tools-temp/AOSharpLiveCapture/bin/Debug/captures/20260629-193121/movement-packets.csv`.
- `Playfield.cs:627` and `Playfield.cs:749` load the captured patrol replay path.
- `NPCController.cs:83`, `NPCController.cs:298`, and `NPCController.cs:310` hold replay state and emit captured patrol movement.

This can work on Mike's machine but is brittle for fresh clones, other developers, and CI. Captured replay data should be promoted to a committed data artifact before more behavior is layered on top.

### P1 - Database startup importer remains a developer-onboarding blocker

Recent logs from another developer showed startup failures in `Misc.CheckDatabase()`:

- `ArgumentOutOfRangeException` from `Substring`.
- `MySqlException: Got a packet bigger than 'max_allowed_packet' bytes`.

Current importer code includes batching and SQL value splitting:

- `Misc.cs:154` calls `ExecuteSqlStatementsUntilFirstInsert`.
- `Misc.cs:169` calls `ExecuteLargeSqlInserts`.
- `Misc.cs:204` starts large insert batching.
- `Misc.cs:331`, `Misc.cs:342`, `Misc.cs:346`, and `Misc.cs:401` parse insert values.
- `Misc.cs:415` flushes batches.

This area needs a fixture-backed startup/import test. Do not change schema or run destructive database operations.

### P2 - Active documentation has drifted behind current work

`docs/ai/CURRENT_TASK.md` correctly says the active task is cleaning robot combat parity at `CURRENT_TASK.md:3` and names `Malfunctioning Cleaning Robot` / MonsterData `297023` at `CURRENT_TASK.md:12`, but it is stale on completed and next work. It does not reflect later work on death/corpse/despawn, loot, idle patrol, movement packet replay, robot stat corrections, or reverted melee range attempts.

`docs/project/PROJECT_STATE.md` is worse: it still says the active cleanup/current task is surgery-clinic implant-install behavior at `PROJECT_STATE.md:11` and `PROJECT_STATE.md:13`. It also contains older robot/Rex state at `PROJECT_STATE.md:46`, `PROJECT_STATE.md:47`, `PROJECT_STATE.md:175`, and `PROJECT_STATE.md:294`.

This drift causes future agents to re-enter old private-city/org/surgery/quest contexts.

### P2 - Org command scope is no longer cleanly isolated

`/org info` works, but the org command area has more behavior than the simplified `/org info` task required:

- Captured `/org info` fallback constants are in `OrgClient.cs:55`, `OrgClient.cs:57`, and `OrgClient.cs:59`.
- CityAdvantages is handled in `OrgClientMessageHandler.cs:32`, `OrgClientMessageHandler.cs:49`, and `OrgClientMessageHandler.cs:73`.
- Org lifecycle TODOs remain in `OrgClient.cs:522`, `OrgClient.cs:538`, `OrgClient.cs:597`, and `OrgClient.cs:868`.

If CityAdvantages was added from later capture evidence, document it in the active project state. If not, quarantine it until explicitly selected.

### P2 - Guest key lifecycle needs a focused regression pass

The guest key generation packet path exists and was reported working, but lifecycle parity is still risky:

- Runtime terminal and item template are hardcoded in `GenericCmdMessageHandler.cs:98` and `GenericCmdMessageHandler.cs:100`.
- The creation log at `GenericCmdMessageHandler.cs:867` says the item is persisted.

The capture-backed lifecycle questions are still: duplicate key behavior, zoning/relog persistence, expiration, overflow inventory behavior, and whether stale keys remain usable.

### P2 - Private city behavior is still capture-constant heavy

Private city code intentionally carries captured constants, but the constants are spread through gameplay handlers:

- Owned org id/name in `Playfield.cs:176` and `Playfield.cs:178`.
- City controller identities/building ids/text in `GenericCmdMessageHandler.cs:130` through `GenericCmdMessageHandler.cs:158`.
- Controller target matching in `GenericCmdMessageHandler.cs:943`.

This is acceptable for narrow capture parity, but it is easy to regress when adding real city ownership later.

### P2 - Player visibility has known TODOs near live-rendering logic

Player visibility was reported fixed, but the area still has TODOs around same-playfield update behavior:

- `CharInPlayMessageHandler.cs:87` announces the entering player.
- `CharInPlayMessageHandler.cs:112` and `CharInPlayMessageHandler.cs:115` still contain TODO comments around the scan/cache path.

This needs a focused two-client regression test before changing nearby FullCharacter/SimpleCharFullUpdate ordering.

### P2 - Corpse and loot lifecycle is working but fragile

The robot death path now produces a body/loot, but recent history included a client crash from the wrong corpse visual path. Current code explicitly skips player corpse visuals because the NPC-loot `CorpseFullUpdate` breaks modern death teleport flow:

- `Playfield.cs:2358` logs that player corpse visual is skipped.
- `Playfield.cs:4112` and `Playfield.cs:4125` send corpse full updates.
- `Playfield.cs:4218` sends the stored corpse identity after death.

Keep player death and NPC corpse systems separate.

## High-Risk Regression Points

- Combat start/range/animation: high risk because the last guessed fix broke attack startup and required revert.
- Patrol replay: high risk because runtime behavior depends on a local `tools-temp` capture CSV.
- Corpse creation: high risk because incorrect corpse packet shape can crash the live client.
- Private city controller: high risk because window open/close relies on exact captured signal/window identities.
- Org initialization and `/org info`: high risk because small packet shape/order differences caused invisible client behavior.
- Database import: high risk because failures block new developers before gameplay testing.
- Active docs drift: high risk because stale task state causes agents to touch the wrong subsystem.

## Hardcoded And Magic Value Inventory

### Captured organization/private city constants

- `Playfield.cs:176` - captured org instance `1970177`.
- `Playfield.cs:178` - captured org name `Est. 2024`.
- `OrgClient.cs:55` - captured org info org instance `1970177`.
- `OrgClient.cs:57` - captured org name `Est. 2024`.
- `OrgClient.cs:59` - captured leader/member name `Celcius2024`.

### Captured private city object constants

- `GenericCmdMessageHandler.cs:98` - runtime Guest Key Generator terminal `0x574B84AB`.
- `GenericCmdMessageHandler.cs:100` - captured City Access Card template `280642`.
- `GenericCmdMessageHandler.cs:130` - captured City Controller `0x009C182E`.
- `GenericCmdMessageHandler.cs:132` - runtime City Controller `0x009C6010`.
- `GenericCmdMessageHandler.cs:134` - captured non-org City Controller `0x009CA011`.
- `GenericCmdMessageHandler.cs:136` through `GenericCmdMessageHandler.cs:144` - captured controller info/building identities.
- `GenericCmdMessageHandler.cs:150` through `GenericCmdMessageHandler.cs:158` - captured feedback/message/menu text constants.

### Captured cleaning robot constants

- `Playfield.cs:230` - captured robot MonsterData `297023`.
- `Playfield.cs:243` - captured death action `Parameter2=500`.
- `Playfield.cs:287` - captured movement CSV path under `tools-temp`.
- `CombatDamageRules.cs:7` - player fallback damage `15`.
- `CombatDamageRules.cs:9` - NPC fallback damage `1`.

### Loader/identity constants

- `PlayfieldLoader.cs:114` and `PlayfieldLoader.cs:139` construct static instance identities with `0xC0000000u`.

## Packet-Ordering Risks

- Private city ready-block ordering was repaired through capture-backed work, but there is no focused static test that asserts `OrgInfoPacket` and org stats precede `FullCharacter`.
- `/org info` now displays, but it is sensitive to OrgClient/OrgServer identity and response shape.
- City Controller X close relies on matching the captured window instance and returning signal `7`.
- Robot death should preserve the live order: `StopFight`, `CharacterAction Death Parameter2=500`, `CorpseFullUpdate`, delayed despawn. This needs a static/order validation.
- FollowTarget patrol parity should use exact captured packet semantics only. Approximation already produced worse movement.

## Persistence And Lifecycle Risks

- Guest key item persistence and expiration behavior need direct lifecycle validation.
- Corpse loot and credit values need capture-backed loot-table validation per corpse type.
- Robot spawns are now present, but captured spawn data and captured movement data are not yet cleanly separated from local tooling artifacts.
- `/org info` uses fallback org values when live org rows are unavailable; this is useful for the Montroyal/private-city test org but risky if generalized.
- Database import batching must be robust for fresh clones and large SQL rows without requiring manual MySQL config changes.

## Capture-Tooling Gaps

AOSharpLiveCapture is much more useful now, but the workflow boundaries should stay explicit:

- Mike launches capture tools; Codex analyzes completed capture folders.
- Static dynel dump is for positions/static stats only, not behavior.
- Active packet capture is for behavior.
- Fight capture and general capture now produce enemy/movement evidence, but pre-login `SimpleCharFullUpdate` cannot be captured because injection happens after the client is running.

Current capture tooling strengths:

- `tools-temp/AOSharpLiveCapture/Main.cs:455` writes `dynels.csv`.
- `tools-temp/AOSharpLiveCapture/Main.cs:187` writes `enemy-state.csv`.
- `tools-temp/AOSharpLiveCapture/Main.cs:195` writes `movement-packets.csv`.
- `tools-temp/AOSharpLiveCapture/Main.cs:227` documents `/aocap` commands.
- `tools-temp/AOSharpLiveCapture/Main.cs:1111` routes FollowTarget packets to structured export.
- `tools-temp/AOSharpLiveCapture/Main.cs:3350` warns when FollowTarget packets are observed but not decoded with usable path coordinates.

Remaining capture-tooling gaps:

- No committed replay fixture proves movement decoding against a known capture folder.
- No single compact comparison report currently says "live vs private movement fields differ here."
- No automated check prevents dynel static dump output from being confused with behavior evidence.

## Validation Gaps

Existing validation coverage is strongest at the N3 serializer contract level:

- `N3RecoveredContractTests.cs:38` covers `AttackInfo`.
- `N3RecoveredContractTests.cs:46` covers `FollowTarget`.
- `N3RecoveredContractTests.cs:569`, `N3RecoveredContractTests.cs:604`, and `N3RecoveredContractTests.cs:640` cover FollowTarget shapes.

Missing focused validations:

- Guest key generation/lifecycle order and expiry.
- City Controller open and X close packet sequence.
- `/org info` OrgServer response byte/field shape.
- Private city org init packet order before `FullCharacter`.
- Same-playfield two-client visibility/rendering.
- Cleaning robot attack start/range/animation.
- Cleaning robot death/corpse/despawn order.
- Cleaning robot loot/credits from captured corpse evidence.
- Database import batching against known large SQL fixtures.
- Capture-tool movement decode replay against `20260629-173605` and `20260629-193121`.

## Recommended Follow-Up Tasks Ordered By Priority

### P0 - Add a fixture-backed database startup/import regression test

Reason: new developers have hit startup crashes before gameplay can be tested.

Scope:

- Use a non-destructive fixture or parser-only test around `Misc.CheckDatabase()` helpers.
- Prove no negative substring length.
- Prove large insert batching stays below packet limits.
- Do not change schema.

### P1 - Capture-backed melee range and attack-start parity

Reason: this is the current gameplay blocker and recent guesses broke combat.

Scope:

- Mike captures one live out-of-range robot attack start through first valid hit/animation.
- Mike captures the same private-server scenario.
- Codex compares packet sequence only.
- Patch only the exact proven difference.

### P1 - Promote captured robot patrol replay data out of `tools-temp`

Reason: runtime behavior currently depends on a local capture folder path.

Scope:

- Move the selected captured movement rows into a committed data file.
- Keep the raw capture folder as evidence only.
- Add a static validation that the runtime route file exists and decodes.

### P1 - Cleaning robot death/corpse/despawn order validation

Reason: corpse packet mistakes can crash the client.

Scope:

- Add an order validation for `StopFight -> CharacterAction Death Parameter2=500 -> CorpseFullUpdate -> delayed despawn`.
- Keep player corpse visual code separate from NPC loot corpse code.

### P2 - Guest key lifecycle parity

Reason: creation works, but lifecycle behavior is not proven.

Scope:

- Validate duplicate key behavior, overflow slot, persistence across zoning/relog, expiration, and stale key use.

### P2 - Org command scope cleanup

Reason: `/org info` works, but adjacent org commands and CityAdvantages need clear task boundaries.

Scope:

- Document which OrgClient commands are intentionally implemented.
- Quarantine or mark any unvalidated command handlers.
- Do not implement broad org management.

### P2 - Two-client player visibility regression test

Reason: same-playfield rendering was repaired but remains a high-risk packet-order area.

Scope:

- Add a documented static or manual regression checklist for first viewer, second viewer, zoning in/out, movement, and relog.

### P3 - Consolidate captured constants

Reason: captured IDs are scattered through handlers.

Scope:

- Move proven constants into narrow evidence-named classes or data files only after active combat work stabilizes.
- Do not generalize private city or org ownership behavior.

## Do Not Touch Yet

- Do not patch melee range, attack animation, or combat timing without fresh live/private capture comparison.
- Do not rewrite NPC movement controllers from inference.
- Do not implement full `/org` command systems.
- Do not implement CityAdvantages unless a current task explicitly selects that captured path.
- Do not change database schemas.
- Do not merge player corpse visual logic with NPC loot corpse logic.
- Do not launch AO client or capture tooling from Codex.
- Do not edit unrelated dirty files if they appear.

## Suggested Next Single Codex Task

`AORebirth - Capture-Backed Melee Range And Attack-Start Parity`

Goal: compare one live capture and one private capture for starting melee against a cleaning robot while out of range, then patch only the exact packet/field/order difference that controls damage timing and visible attack animation.

This should happen before any more combat behavior changes.

