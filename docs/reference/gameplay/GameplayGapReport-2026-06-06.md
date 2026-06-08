# CellAO Gameplay Gap Report

Date: 2026-06-06

Scope:
- Current CellAO repo: `C:\Users\Mike\Documents\Cellao-Clean`
- Reverse-engineering source set: `C:\Users\Mike\Documents\AO stripdown\Anarchy Online`
- Research/tooling set: `C:\Users\Mike\Documents\New project`

Method:
- Inspected ZoneEngine controllers, handlers, packet helpers, playfield runtime, current docs, smoke tooling, and current dirty tree.
- This is a codebase condition report. It does not claim live behavior unless recent playtests or existing docs already recorded it.
- Gameplay/protocol fixes should continue using the project source-of-truth order: official live capture, private-server capture as reference, stripdown source/contracts, local assumptions last.

## Current Position

CellAO is now past the first "can log in and poke the world" stage. Important systems have been made playable through targeted packet/runtime repairs:

- Login state and sit/stand baseline are fixed. `FullCharacter` must stay on message version `26`.
- Equipment is substantially working: weapon visuals, weapon attacks, dual pistols, armor visuals, HUD/deck/util items, stat changes, equip delay, and equipment persistence have all had successful playtests.
- Death/respawn white-screen loop was fixed and playtested.
- Corpse creation, corpse use, looting, corpse despawn, XP text, and corpse credit text have been repaired enough for normal local testing.
- Player trade item and credit transfer have been repaired.
- Vendor shop buy/sell flow, shop close, and pricing math have been repaired.
- Surgery clinic use, implant install/remove, implant stat changes, persistence, treatment lockout, and lockout persistence have been repaired.
- Fair Trade entry and basic ICC armor shop flow have been repaired enough for playtesting.

The main codebase problem is that many gameplay systems are still implemented through handler/playfield shortcuts while controller methods remain stubbed. That makes behavior fragile: the client packet path may work, but the core gameplay contract is not complete.

## Immediate Work

1. Clean and commit the current dirty tree after confirming the latest login/surgery-clinic render state.

Current dirty files:

- `CellAO\Server\ZoneEngine\Core\Functions\GameFunctions\systemtext.cs`
- `CellAO\Server\ZoneEngine\Core\Functions\GameFunctions\teleportproxy.cs`
- `CellAO\Server\ZoneEngine\Core\MessageHandlers\FullCharacterMessageHandler.cs`
- `CellAO\Server\ZoneEngine\Core\MessageHandlers\StatMessageHandler.cs`
- `CellAO\Server\ZoneEngine\Core\Packets\WeaponItemFullUpdate.cs`
- `CellAO\Server\ZoneEngine\Core\Playfields\Playfield.cs`
- `CellAO\Server\ZoneEngine\Core\ZoneClient.cs`
- `CellAO\Server\ZoneEngine\ZoneEngine.csproj`
- `CellAO\Server\ZoneEngine\ChatCommands\ChatCommandGiveCredits.cs`
- `tools-temp\live-shop-close-baseline.txt`

Risk: these files span death, stats, full-character login, item classification, playfield runtime, and shop/credit helper work. Leaving them dirty while starting another system will make regressions hard to isolate.

2. Confirm and lock the item-classification repair around `WeaponItemFullUpdate`.

Observed code issue:
- Use-only items such as the portable surgery clinic can look weapon-like if `attackrange`, `itemdelay`, or `rechargedelay` are treated as weapon proof.
- Latest dirty patch narrows weapon classification to damage-bearing items. This needs one login test with the surgery clinic in inventory/equipped state, then a commit.

Why immediate:
- Full-character login and inventory item serialization affect every gameplay system. Bad item classification can cause load-screen hangs or bad client rendering.

3. Add source/smoke assertions for the systems now known to work.

Priority assertions:
- Login full-character version and live-style state.
- Equipment persistence: equipped weapon/armor/HUD/util/deck/implant -> reload -> correct full update and appearance.
- Corpse credit/XP message path: real stat change and displayed text agree.
- Vendor shop buy/sell: displayed price, actual credit delta, inventory delta.
- Player trade: item and credit transfer with no stale trade-window inventory.
- Surgery clinic: first use text, 30-second implant access, treatment lockout, logout/relog lockout persistence.
- Fair Trade: enter, immediate movement grace, room movement, terminal open/close, no false exit trigger.

Why immediate:
- These are repeatedly fixed systems. Without source assertions, the next protocol or inventory change can re-break them silently.

4. Stop treating NPC movement as an isolated AI problem until packet replay is in place.

Evidence:
- `NPCController.cs` now contains custom motion-segment prediction, throttling, follow stop distance, and combat stop logic.
- `CellAO/Documentation/ProjectWorkingReference.md` records NPC chase as unresolved and requires capture/replay comparison before more chase changes.
- `docs/reference/packets/StripdownDirectRepairCandidates.md` explicitly says stripdown gives packet contracts, not enough runtime policy to keep changing chase behavior blindly.

Next useful work:
- Build a replay comparison around captured official/private chase data and local outgoing `FollowTarget` / `CharDCMove` / position updates.
- Only change runtime chase after local packet flow is shown to differ from captured flow.

## Needs Repair

### Controllers Are Incomplete

`PlayerController.cs` still has major gameplay stubs:

- Follow/patrol: `DoFollow()`, `StartPatrolling()`.
- Item stack operations: split/join/combine.
- Tradeskill bridge methods: source changed, target changed, build pressed.
- Logout/login/stop logout controller methods.
- Target info.
- Chat command dispatch.

`NPCController.cs` is thinner:

- Many normal character actions are not implemented for NPCs.
- NPC movement is currently custom runtime logic, not a clean threat/chase/attack contract.
- NPC stat sending explicitly clears the player-targeted stat set before sending, which should be reviewed before NPC stat-driven visuals/effects are expanded.

Repair direction:
- Pick one controller contract at a time and route the handler path through it.
- Do not keep adding direct special cases to `Playfield.cs` unless the handler/controller split is impossible for that packet.

### `Playfield.cs` Is A High-Risk Catch-All

`Playfield.cs` currently owns too much:

- Heartbeat loop.
- Wall/statel collision.
- Post-zone collision grace.
- Combat ticks.
- NPC home/follow/combat behavior.
- Death and respawn.
- Corpse registration, full update, loot, credits, despawn.
- XP rewards.
- Loot table loading.
- Playfield teleport handling.

Repair direction:
- Do not refactor blindly.
- After smoke assertions exist, split stable subsystems into small services: `CorpseService`, `DeathRespawnService`, `VendorRuntime`, `CombatTickService`, `StatelCollisionService`.
- Keep public behavior identical during extraction.

### Logout Close-Box Flow Is Still A Known Gameplay Gap

Observed earlier:
- Pressing X on the active local client starts timed logout but did not visually sit that same client.
- Remote clients could see the character sit.

Repair direction:
- Capture official close-box logout packet flow.
- Compare local self-client vs other-client packets.
- Likely area: local client needs a self-directed state/posture/appearance update in addition to playfield broadcast, but do not patch without capture.

### NPC Movement/Combat Is Not Gameplay-Ready

Current status:
- Combat can start and enemies can attack.
- NPC chase/follow remains visually wrong under player movement.
- Melee attacks should not require a hard stop; ranged attacks usually should.
- Aggro profile gating exists, but real NPC behavior variety is not mapped.

Repair direction:
- Separate the problem into packet flow and behavior policy.
- Packet flow first: prove local movement packet sequence against capture.
- Behavior second: threat target -> chase while out of range -> attack when range permits -> continue chase if player moves.
- Add high-visibility logs only as replay inputs, not permanent noisy logs.

### Corpse/Loot Is Functional But Not Clean

Current status:
- Corpse use, item loot, credits, XP text, and despawn have worked in playtests.
- `docs/reference/packets/StripdownDirectRepairCandidates.md` still calls out corpse/full update and despawn packet distinctions.

Repair direction:
- Keep runtime `Despawn` path that playtests proved.
- Add assertions for `CorpseFullUpdate`, `InventoryUpdate`, `ClientMoveItemToInventory`, `ContainerAddItem`, credit award, and despawn order.
- Improve corpse visuals only with captured corpse packet data per mob family.

### Shops And Trade Need Regression Protection

Current status:
- Player trade item/credit transfer worked after repairs.
- Vendor shop buy/sell and math worked after repairs.
- Shop close required live capture to fix.

Known weak spots:
- `TradeMessageHandler.cs` is large and contains both player trade and vendor shop handling.
- Stale item windows and duplicate-looking client inventory bugs have already happened.
- Shop database coverage is incomplete; terminals can report missing shop IDs if no DB entry exists.

Repair direction:
- Add focused smoke tests for player trade, vendor buy, vendor sell, close/decline, and stale window clearing.
- Split player trade and vendor shop runtime only after assertions are in place.
- Expand shop DB coverage after terminal IDs are captured, not by guessing.

### Playfield/Interior Mapping Needs A Data Pass

Current status:
- Fair Trade entry/room movement improved after post-zone collision grace and statel handling repairs.
- Incorrect exit triggers and oversized/overlapping statel volumes were observed.

Repair direction:
- Build a statel/proxy map from live captures and local statel listings.
- Keep Fair Trade as the first stable fixture because it is a high-use building.
- Add a manual regression checklist: enter, wait, enter and run immediately, open shop, close shop, run through all three rooms, exit intentionally.

### Nanos Are Mostly Skeleton-Level

Evidence:
- `CharacterActionMessageHandler.cs` calls `client.Controller.CastNano(...)` and has TODOs for cast delay, nanoskill requirements, nano cost, and enough nano checks.
- `CastNanoSpellMessageHandler.cs` only sends the outbound cast packet.

Missing:
- Nano execution timing.
- Requirement checks.
- Nano cost and modifiers.
- Cooldowns/lockouts.
- Hostile/friendly target validation.
- Effects over time.
- Nano resist.
- Proper nano damage/heal/status text flow.
- Uploaded nano/book handling beyond simple functions.

Recommended next target after current cleanup:
- Pick one low-level heal or buff nano.
- Capture live use.
- Implement full requirement/cost/effect/stat-message path for that one nano.

### Tradeskills Are Partial

Evidence:
- `TradeSkillReceiver.cs` has real combine logic.
- `PlayerController` tradeskill methods are still stubs.

Missing/weak:
- UI packet flow and controller integration need validation.
- Skill requirements, quality interpolation, XP, delete flags, and inventory updates need smoke coverage.
- The code adds XP directly to `xp` during tradeskill success; that should be verified against live behavior.

Recommended path:
- Test one known simple tradeskill combine from live data.
- Capture UI sequence.
- Route controller stubs to `TradeSkillReceiver` or replace with a clearer service.

### Research, Perks, Missions, Quests Are Mostly Missing

Evidence:
- `ResearchRequestMessageHandler.cs` returns a hard-coded list of research IDs.
- `ResearchUpdateMessageHandler.cs` sends those IDs with fixed unknown fields.
- Perks data exists as XML, but no complete perk runtime was found in ZoneEngine.
- Mission/quest stats exist, but no complete mission/quest gameplay loop was found.

Missing:
- Research selection/progression.
- Perk training/untraining/actions.
- Mission terminal flow.
- Mission objective state.
- Quest dialogs/rewards.
- Team mission state.

Recommended path:
- Do not start here until combat, shops, inventory, and nanos have regression coverage.

### Teams And Orgs Need Separate Audits

Teams:
- `PlayerController` has an in-memory `TeamRuntime`.
- Team play was reported not working earlier.
- Needs live capture for invite/accept/member update packet flow.

Orgs:
- `OrgClient.cs` exists and has many TODOs around offline kick, disband, reload for other members, and null handling.
- Needs a separate org command/UI audit before gameplay claims.

### Pets Are Not Gameplay-Ready

Evidence:
- Pet-related message classes and stats exist.
- No complete pet summon/control/combat loop was found in the inspected ZoneEngine path.

Missing:
- Pet creation from nano.
- Pet owner binding.
- Pet commands.
- Pet follow/combat behavior.
- Pet despawn/zoning.
- Pet UI update flow.

Recommended path:
- Do pets after NPC movement and one nano execution path are stable.

### Bank, Bags, Stack Splitting, And Containers Need Validation

Evidence:
- `BankMessageHandler.cs` can send bank slots.
- Inventory pages and bag/container types exist.
- Player stack split/join methods are stubbed.

Missing/weak:
- Bank open/use flow needs playtest.
- Bag open/move/item persistence needs smoke coverage.
- Stack splitting/joining is not implemented in controller.
- Container UI packet flow needs live comparison.

Recommended path:
- Test bank terminal next to Fair Trade after shop stability.
- Then test bags and stacks.

## Missing Gameplay Systems

These systems are either not implemented, not found as complete gameplay loops, or not yet tested enough to trust:

- Full NPC AI: aggro radius, assist/social aggro, threat tables, pathing/follow, combat animations, ranged vs melee behavior.
- Full nano system.
- Perks and research.
- Missions and quests.
- Pets.
- Proper teams.
- Organizations.
- PvP flags, duels, towers, gas suppression, faction conflict.
- Full vendor/shop database coverage.
- Player shops / market-style systems.
- Mail/GMI/AH style systems, if desired for this client era.
- Vehicles/yalm/mechs.
- Doors/elevators/grid/whompah coverage beyond local tested statels.
- Insurance/save terminal full behavior.
- Character creation/new-player setup beyond current login compatibility.
- Level-up/IP/skill reset/training UI flows.
- Side/faction/token systems.
- Full NPC dialogs and KnuBot trading.

## Improvement Targets

1. Add a `KnownGoodFlows` test set.

The repo has smoke tooling under `tools-temp`, but the repaired gameplay paths need stronger source assertions and repeatable local smoke scripts. Use the current working systems as fixtures before expanding.

2. Keep packet docs source-separated.

Official live, private-server, stripdown, and local captures must stay labeled separately. Do not mix private-server S2C richness with official-live authority.

3. Add a small runtime telemetry switch.

Useful fields:
- packet key/action,
- character identity,
- target identity,
- playfield,
- stat changes,
- inventory source/target,
- trade/shop state,
- corpse identity,
- NPC state and chase reason.

This should be toggleable. Always-on debug spam has already made some failures harder to read.

4. Extract code only after behavior is locked.

Do not refactor `Playfield.cs` while behavior is still actively changing. First add tests/assertions, then extract one subsystem at a time.

5. Prefer capture-driven one-feature slices.

Best slices:
- one nano,
- one bank terminal,
- one tradeskill combine,
- one team invite/accept,
- one pet summon,
- one Fair Trade shop terminal group,
- one hostile mob chase/combat/death/loot loop.

## Recommended Work Order

### Tomorrow Morning

1. Confirm current dirty fixes with one login test.
2. Build and run the smoke/source assertion suite.
3. Commit the dirty tree if the login/surgery/shop/credit fixes are confirmed.
4. Add regression assertions for the latest fixed flows.

### Next Gameplay Target

Bank and container flow is the best next target if Mike wants lower-risk progress:
- It is core gameplay.
- It sits next to Fair Trade/shop testing.
- It exercises inventory pages without the complexity of NPC movement.

Nano execution is the best next target if Mike wants combat depth:
- Start with one simple heal/buff.
- Capture live.
- Implement requirements, nano cost, stat effect, and text.

NPC movement should wait until the replay comparison exists:
- It is still the most failure-prone system.
- More live observation without a replay comparator will keep producing ambiguous conclusions.

## Concrete Repair Backlog

Priority 0:
- Verify current dirty login/item-classification/surgery changes.
- Commit or revert only the intended dirty changes after verification.
- Add assertions for corpse credits, vendor credits, trade credits, equipment reload, and surgery lockout.

Priority 1:
- Bank terminal open/deposit/withdraw/persistence.
- Bag/container open/move/persistence.
- Stack split/join.
- One nano execution path.
- Player logout close-box self-visual sit/timer.

Priority 2:
- Tradeskill combine flow.
- Team invite/accept/member update.
- NPC dialog answer flow.
- Fair Trade terminal/shop DB coverage.
- Corpse visual cleanup per mob family.

Priority 3:
- NPC chase replay comparator and movement repair.
- Pet summon/control.
- Missions/quests.
- Perks/research.
- Organizations.
- PvP/tower systems.
