# ZoneEngine_New inventory reconciliation

Starting checkpoint: `6dab287bd52590d7b9c0e7054bef95160a1594ba`.
Scope: existing inventory contracts only. No schema change, production access, live client,
capture startup, new stacking formulas, or alteration of the solved trade transaction.

## Initial exact action inventory

Paths below are relative to `AORebirth/Server` unless prefixed `Messaging` (the shared
`AORebirth/Libraries/Source/AOtomation/AOtomation.Messaging` project). `LegacyInventory`
means `ZoneEngine/Core/InventoryContainerRuntimeService.cs`; `NewMoves` means
`ZoneEngine_New/Core/Inventory/InventoryMoveService.cs`. This matrix precedes repairs.

| Action | Legacy handler/service | New handler/service | Packet contract | DAO path and initial transaction boundary | Existing test coverage | Initial status |
| --- | --- | --- | --- | --- | --- | --- |
| Corpse/chest pickup and loot transfer | ClientMoveItemToInventory handler; LegacyInventory loot transfer | ClientMoveItemToInventory handler; NewMoves.TryResolveSource/ApplyMove | ClientMoveItemToInventory; ContainerAddItem acknowledgement | New item_instances insertion via InventoryFlushService/ICharacterCoalesceCommit; acknowledgement initially precedes commit | Loot evidence fixtures; PlayerInventoryOverflowTests | PARTIAL: implemented, durability/publication gap |
| Main inventory relocation | ClientMoveItemToInventory handler; LegacyInventory | ClientMoveItemToInventory handler; NewMoves | ClientMoveItemToInventory; ContainerAddItem | Location write-behind; no instance remint; initial memory/ack before durability | Packet serialization; overflow tests | PARTIAL: durable rollback not at acknowledgement boundary |
| Equip/unequip/swap | LegacyInventory requirement/timed move paths | NewMoves.TryBeginEquipmentMove/CompletePending | ClientMoveItemToInventory; unequip CharacterAction; ContainerAddItem | Swap park-and-move SQL is atomic when eventually flushed; memory/publication initially precedes it | Existing gameplay code guards changed pending source/destination; no dedicated action tests | PARTIAL |
| Backpack open/close | GenericCmd Use; legacy container runtime | GenericCmd Use; Item.TryUseBackpack | GenericCmd; InventoryUpdate; ChestItemFullUpdate; CharacterAction | Lazy GetContainerItems; reads only for open; instance-owned container handle | Packet tests and Item/PlayerInventory runtime | PARTIAL: implemented, targeted negative tests needed |
| Backpack contents transfer | ContainerAddItem/ClientMoveItemToInventory; LegacyInventory | ClientContainerAddItem/ClientMoveItemToInventory; NewMoves | Renamed ClientContainerAddItem inbound; ContainerAddItem outbound | Instance-owned bag handle; location write-behind | Shared packet tests | PARTIAL |
| Bank open/move | GenericCmd OpenBank and legacy container runtime | ItemUseFunctions.OpenBank; NewMoves | BankMessage; ClientContainerAddItem; ClientMoveItemToInventory | Lazy GetBankItems; movement write-behind | Shared packet tests | PARTIAL: movement exists; bank session access semantics need audit |
| Split stack | CharacterAction Split; LegacyInventory.SplitInventoryItemStackAction | None | CharacterAction Split, slot target and Parameter2 amount | Legacy mutates quantity/adds row/page.Write without validation; New has no stack-update DAO API | No split action test found in New; legacy source is behavioral reference, not safe validation authority | MISSING; unchecked legacy arithmetic is LEGACY_BUG_NOT_TO_PORT |
| Merge stack | No dedicated proven request route in inspected handlers | None; occupied ordinary move is refused | Not established by an enum or visual assumption | None established | No accepted merge request fixture found in inspected packet/action tests | UNPROVEN: do not invent target/count semantics |
| Delete/destroy carried item | CharacterAction DeleteItem; LegacyInventory.DeleteInventoryItemAction | None | CharacterAction DeleteItem + acknowledgement | Legacy ItemDao delete then memory removal; New must retire authoritative item_instances row | Mission legacy-fallthrough source contracts, not New action tests | MISSING |
| Use item / use world dynel | GenericCmd; LegacyInventory and specialized services | GenericCmd; Item.Use/ItemUseFunctions; StaticDynel.TryUse | GenericCmd request/reply + existing function-specific packets | Functions include Hit/Set/flags/text/UploadNano/OpenBank; only uploaded nanos write-behind; Item.Use initially ignores execution result | GenericCmdUseRouteClassifierTests; packet tests | PARTIAL: stronger per-function/requirement/consumption audit required |
| Consume item/stack | LegacyInventory.ConsumeInventoryStackItem and specialized item-use routes | No stack consumption path | Existing GenericCmd Use and DeleteItem publication | New StackCount changes cannot currently be persisted by location-only update interface | No New consume test | MISSING for accepted consuming item routes; no blanket consumable assumption |
| Player trade transfer | TradeMessage/legacy trade runtime | TradeMessageHandler/TradeService | Shared TradeMessage and existing grant/close frames | BOTH inventories/nanos/Cash commit atomically through ITradePersistence before grants/Complete; unknown commit quarantines | TradePersistenceTests, actual disposable MySQL late-failure rollback and retry | RECONCILED at checkpoint; not rewritten |
| Vendor buy/sell | Legacy trade/shop runtime | TradeService.CommitShop | TradeMessage; AddTemplate; existing Complete | Retirement, purchase inserts, nanos and final Cash in one transaction; durable main-inventory capacity required | TradePersistenceTests incl full/failed shop and late-failure DB transaction | RECONCILED at checkpoint |
| Overflow/full destination | Legacy overflow/client packet behavior | PlayerInventory.TryPlace + Item/Trade rules | Existing captured overflow TemplateAction/ContainerAddItem pairs | Overflow deliberately memory-only; persisted rows cannot enter it; paid purchase cannot land there | PlayerInventoryOverflowTests; shop full test | RECONCILED stronger safety, not durable storage |
| Invalid ownership/slot/item/pending equip | Legacy per-route checks vary | Owned carried pages/bag handles; pending item/destination reference guards | Slot identity is not item-instance identity | New mutations must retain InstanceId and owner; unknown commit quarantine already available | Trade ownership/rollback tests; more action-specific tests required | PARTIAL |
| Disconnect/reconnect/rollback | Legacy ownership and inventory saves | Spawn cleanup + InventoryFlushService + CharacterSnapshotService | Existing session protocol | Checkpoint gates snapshots and write-behind, no replay after unknown COMMIT | SessionOwnershipTests; TradePersistenceTests; disposable DB suite | RECONCILED infrastructure; repair-specific action tests still needed |

## Evidence boundaries

- Legacy split identifies a slot and quantity; the wire contract does not supply a mutable
  item-instance id or operation nonce. Domain expected-instance checks can reject stale
  internal calls, but cannot claim to distinguish an old replay from a deliberate identical
  new slot-addressed command. Conservation and bounds must hold for both.
- A template id never identifies the mutable row. Backpack identity, owner, container slot,
  item instance id, and low/high template ids remain separate.
- Runtime consumes accepted item/templates and compiled contracts, not capture folders.
- Unproven merge semantics and specialized use effects remain explicit, not synthesized.

## Repairs and validation

Implemented scoped repairs:

| Action | Repair and durable boundary | Focused tests authored |
| --- | --- | --- |
| Delete | `InventoryActionService.TryDelete`: exact owned slot/current instance; retain retired row under `None` with instance-specific placement; preserve permanent garden keys; same-transaction locked child-range check rejects nonempty bag retirement; publish only after commit | successful delete, duplicate empty slot, foreign/stale instance, protected key, failed write, empty bag success, known/nonhydrated nonempty bag preserves parent and children |
| Split | `TrySplit`: positive strict-subset quantity, Stackable/CantSplit, free same-page slot, distinct allocated instance; source expected-count update plus new row in one transaction | quantity/identity conservation, full destination, malformed amounts, stale/locked instance, repeated valid commands, rollback, unknown commit |
| Move/equipment swap/loot transfer | `InventoryMoveService.ApplyMove` plans destination and swap rows before a single mutation transaction; only committed moves acknowledge and notify loot source; no independent write-behind ack | ordinary move durable-before-ack and failure, duplicate source, owned bank-to-backpack and foreign bag rejection; existing equipment rule code retained |
| Hydration | `ItemBuilder.TryFromInstanceRecord` restores positive persisted StackCount after template construction, avoiding template MultipleCount resurrecting consumed/split quantity | stored count differs from template default, malformed zero count, reload after split |
| Consumable upload crystal | `TryUseNanoCrystal`: all supported OnUse UploadNano programs and one-item consumption in the same transaction | precommit memory/packet isolation, failed second consumption, unsupported mixed function rejection |
| Quabbit 301782 → 301749 | `TryOpenQuabbit`: exact Legacy QL1 grant, existing-owned behavior, main-inventory durable storage, captured Overflow presentation; sealed retirement plus opened row atomic | exact frame order, no duplicate opened item, full inventory, failed grant, duplicate empty source |
| Health/nano stim 291043/291044 | `TryUseVitalItem`: consume one plus final Health/CurrentNano in the same transaction; use declared Hit/LockSkill, preserving existing Legacy QL interpolation only when an amount is missing | atomic restore/consume, capped vitals, failed late write preserves both, repeat rejection, skill expiry |
| Recharger 291082/291083 | same declared Hit/LockSkill specialization but never consumes the stack, matching Legacy | declared amounts and duration, capped vitals, unchanged durable quantity, cooldown repeat rejection |
| Mission reward inventory | `InventoryGrantPlan` is capacity/identity planning only under the player persistence gate; caller must write exact Rows through its mission DAO transaction before `PublishAfterCommit` | plan purity, full inventory, publication once; mission DAO agent owns actual combined reward transaction tests |

`MySqlInventoryMutationPersistence` is a SQL-free coordinator. It calls existing inventory,
uploaded-nano, and stat repository transaction primitives. `MySqlInventoryRepository.WriteStackCounts`
requires the expected stored count, so stale quantity rolls back the earlier inserts/locations.
Container retirement also checks the exact container-instance child range under FOR UPDATE
in an explicitly repeatable-read transaction. Legacy's parent-only deletion of a populated bag
is classified as a bug, not a behavior to copy; child rows and history remain untouched.
Vendor bag offers were separately verified: existing `TradeService.AddOfferedItem` rejects
all containers before removing them or entering the offer, so shop retirement cannot orphan
their children. A focused handler regression now preserves a populated bag, its child, cash,
and empty offer while asserting no persistence call or trade acknowledgement. Trade runtime
was not rewritten for a path already safely rejected.
No inventory schema change was introduced. Normal precommit failure changes neither memory nor
acknowledgements; unknown COMMIT outcome quarantines the existing player and cannot retry, flush,
or snapshot over an indeterminate durable result. Existing trade/shop transaction code is unchanged.

Skill cooldowns use the existing inventory owner tick, not sleeping threads. Their supported
SpecialUnavailable/Available fields match Legacy `CharacterActionMessageHandler:976–1008`.
Vital item quantities, bounds, interpolation and reusable recharger behavior come from
Legacy `InventoryContainerRuntimeService:1120–1365`; they are not new balancing values.
Focused Windows checkpoint: PASS 160/160 and startup validation PASS in
`.codex-inventory-tests.log`. A subsequent consume-frame ordering assertion and disconnected
skill-lock cleanup need the next combined nano/inventory run. The disposable DAO suite also
passed actual split success, stale expected-count rollback of inserted/moved rows, and a late
uploaded-nano CHECK failure rolling back item insert/location/count together. The schema agent
owns the exact disposable evidence log and the pending late FinalStats failure case.
Exact-SHA cross-OS acceptance is still a separate root gate, not inferred from unit tests.

## Accepted specialized behavior at initial audit (retained history)

The authored-item entries below were initial dependency findings. Their current runtime
connections and remaining world-activation gaps are distinguished in the later authored
reconciliation checkpoint; do not read this historical list as the final disposition.

These are supported Legacy paths, not unknown contracts and not documented retirements:

- Sealed lockpick 295999 → 95577 also completes BuyLockpick and offers Strongbox:
  `ZoneEngine/Core/Arete/Quests/StanGoodmanQuestRuntime:434–502`. The item exchange alone
  would omit the accepted quest transition; it needs the same mission transaction.
- Marco nano packages grant accepted contents and complete the BuyNano tip/rewards:
  `ZoneEngine/Core/Playfields/CapturedAreteMarcoSpidaNanoPackageRuntime.TryHandleCrystalUse`.
  The same mission transaction must own package consumption and all reward rows.
- Nascense DOJA chip 284954, pet shells, and token-board uses are explicitly dispatched by
  `ZoneEngine/Core/InventoryContainerRuntimeService:1017–1072`; generic item functions do
  not replace their quest, pet-ownership/spawn, or board-upgrade contracts.
- HUD vehicle wear needs accepted placement/requirements and OnWear morph cleanup through
  `ZoneEngine/Core/VehicleHudWearRuntime`; ordinary equipment row persistence alone does
  not implement the vehicle presentation/state contract.
- Merge has no established dedicated request or accepted amount contract in the inspected
  handler/test corpus. It remains unproven; ordinary occupied-slot behavior is not relabeled
  as a merge or used to invent stack limits.

Accordingly this report does **not** declare complete inventory gameplay replacement readiness.
InventoryActionTests are unit/action tests using a transaction fake; actual MySQL rollback and
cross-engine acceptance are separate required evidence, not implied by a mocked PASS.

### Authored quest-item adapter dependency (initial 2026-09-08 audit; retained history)

The remaining Stan/Marco/DOJA paths have concrete accepted contracts; their absence is not
a documented retirement and is not a request for new capture evidence. The smallest safe
adapter is not an isolated inventory callback:

| Accepted path | Existing authoritative dependency | Required New runtime connection |
| --- | --- | --- |
| Sealed lockpick use | `StanGoodmanQuestRuntime:434–502` grants 95577 QL1, consumes 295999, then `:693–715` completes 555BD124 and activates 555BE9C5 before captured handoff QFU | One owner-gated transaction must retire the sealed instance, optionally insert the lockpick, complete/activate the authored mission states, then publish the exact grant/delete/handoff frames |
| Marco package / Buy Nano tip | `CapturedAreteMarcoSpidaNanoPackageRuntime:34–135` prechecks every accepted item template and skips already-owned unique contents; `StanGoodmanQuestRuntime:333–363` completes the tip and applies XP, cash, and item 223373 | Same transaction for every package/reward row, source retirement, mission state and reward ledger; captured content-first, source-delete, tip-feedback ordering only after confirmed commit |
| Nascense DOJA use | `DojaChipQuestRuntime:30–82` validates level, character/account cooldown and accepted chip, emits TemplateAction but does **not** consume the chip; `:128–163` offers/accepts turn-in mission | Authored definition/registry initialization plus durable acceptance and captured QFU publication; do not implement this as ordinary consumable use |
| DOJA turn-in/relogin | `DojaChipQuestRuntime:205–269,271–350,585–681` restores remaining cooldown and coordinates rewards/account flags; `DojaChipPacketSender:24–115` retains accepted raw QFU/delete packets | Actual Scarlett trade adapter, combined chip/reward/cooldown transaction, account-key ownership, and authored login/zone restore; no fabricated generic QFU or refreshed cooldown on reconnect |

`IMissionDaoTransaction:379–420` supports authored state/objectives/flags, reward ledger and
atomic stat rewards, but has no item insert/retire primitive. `PersistentMissionService`
owns a transaction per public mutation, while `MissionRewardCoordinator:161–280` deliberately
executes external effects between separate claim/complete transactions. Calling the inventory
coordinator from either route would not make the composed operation atomic. Generated mission
item support is a separate capability; it must not be presented as an authored adapter.

New currently links the pure authored models/service/repository adapter but does not initialize
the accepted `QuestContentRegistry` and `MissionDefinitionCatalog` used by
`MissionRuntime:116–150`, or connect the authored journal and dialogue/trade hooks. Safe migration
therefore requires a shared neutral DAO transaction item capability, the existing accepted
definition/packet projections, and one owner/session-fenced authored lifecycle adapter. This is
a runtime dependency gap, not evidence that a new schema is required. No authored item grant,
mission completion, or success acknowledgement has been added across two independent commits.

Shell-only nanos are similarly distinct from shell use. `PlayerController:460–464` intentionally
creates only a shell, without a default active-nano/NCU row; `PetShellItemService:281–349` selects
a free main-inventory slot and sends AddTemplate after granting. `InventoryGrantPlan` can provide
the pure rows/publication for that cast, but the rows must join the nano-cost transaction before
the specialization is enabled. Shell use at `PetShellItemService:138–193` additionally requires
real strain-specific living-pet uniqueness and world summon success before consumption; an
inventory-only placeholder cannot replace those contracts.

### Authored inventory reconciliation checkpoint (2026-09-08)

The dependency findings above describe the initial audit, not the final implementation.
The accepted flint-novak and Nascense DOJA quest packs now initialize the existing validated
`QuestContentRegistry` and `MissionDefinitionCatalog` through `AuthoredQuestCatalog.Load`.
The existing pure domain service runs inside one outer DAO transaction through
`AuthoredMissionTransactionScope`; nested state operations cannot open another connection.
`IMissionInventoryMutationTransaction` adds a neutral item-row capability to the existing
`MySqlMissionDao` implementation, using its exact insert/retire primitives. No authored
schema, gameplay SQL, new external reward-claim phase, or guessed packet fields were added.

| Action | Current New route and durable operation | Current status and focused proof |
| --- | --- | --- |
| Sealed lockpick 295999 | `Item.Use` → `AuthoredQuestService.TryUseItem`; optional exact 95577 QL1 grant, historical source retirement, BuyLockpick completion and Strongbox acceptance in one commit | Runtime use connected; success, duplicate, capacity, stale row, late failure and unknown-commit tests |
| Marco Doctor/Enforcer packages | Same route; unchanged `CapturedMarcoSpidaNanoPackageProvider` supplies all accepted contents; unique/capacity/template checks precede every write; source, contents, BuyNano completion, XP/Cash/item reward and ledger share commit | Runtime use connected; exact Doctor contents, reward publication order, live-stat base, full rollback tests; unchanged provider owns Enforcer values |
| Nascense DOJA chip use | Same route; exact level and persisted character/account cooldown checks; turn-in mission accepted but chip is not consumed; captured TemplateAction/QFU only after commit | Runtime use connected; no-consume/replay/account-cooldown tests |
| DOJA restore | `AuthoredQuestService.Restore`; existing accepted raw packet template uses remaining durable expiry, not a fresh 18-hour countdown; expired cooldown cleanup retains terminal mission history | Login/zone restore connected by root; exact remaining-expiry/no-refresh test |
| Strongbox lockpick interaction | `TryUseLockpickOnStrongbox` leaves the lockpick, optionally grants 248306, completes Strongbox and accepts DeliverFactory atomically | Transaction method and deterministic test present; **world strongbox identity/use invocation still missing** |
| Stan factory turn-in | `TryTurnInFactory`; exact source retirement, 296572 reward, 2596 XP/1240 Cash, ledger and Sarah/BuyNano transitions before accepted-trade callback | Transaction method and deterministic test present; **trusted Stan dialogue/trade invocation still missing** |
| DOJA Scarlett turn-in | `TryTurnInDoja`; exact chip retirement, complete one-level persisted XP projection (including level/IP/vitals), side-token ledger, character/account cooldown and mission state share one commit | Transaction method; full-level/death-pool/token/cooldown success, late rollback and unknown-commit tests; **trusted Scarlett trade invocation still missing** |

`MySqlMissionDao.Execute` now locks the actual character row even without account scope,
uses RepeatableRead for row/gap locks, rejects foreign account ownership before invoking
the operation, and distinguishes operation rollback from unknown commit outcome. The item
capability verifies exact instance/template/quality/count/source/location and rejects any
surviving child rows before retiring a parent. Historical items are retained. A transport
failure at commit quarantines the exact player/session without memory publication, packets,
automatic retry, write-behind or logout snapshot; confirmed rollback leaves memory untouched.

The new DOJA tests exposed a concrete Legacy bug: `DojaChipQuestRuntime:609–614` passes the
active cooldown mission to `PersistentMissionService.SetAccountFlag`, whose `:649–656`
requires a completed source mission, and then ignores the rejected result. New does not
weaken the permanent-access flag rule. `SaveDojaAccountCooldown` instead checks the completed
turn-in and active cooldown inside the same owner/account-scoped transaction, then saves the
timed restriction with the existing DAO CAS and exact cooldown SourceQuestId. This is a
`LEGACY_BUG_NOT_TO_PORT`, not an invented reward or account identity fallback.

#### Remaining supported runtime regressions

- New `SpawnService:99–111` instantiates `NpcCharacter` from a GameData template and assigns
  a fresh runtime identity. Its `MobTemplate` binding does not carry an authored dialogue
  authority, and the inspected `GameData/MobTemplates.json` has no exact Stan Goodman or
  Scarlett Dalquist entries. `NpcCharacter.TryUse:50–69` currently routes only real vendors.
- Legacy `ScarlettDalquistSpawn:29–83` has an explicit PF7010/captured-position/reserved-ID
  activation using generic BART presentation. That factory and its accepted identity binding
  are not consumed by New. Legacy `StanGoodmanQuestRuntime:1256–1270` accepts the captured ID
  or a Goodman-name fallback; the latter is not a safe substitute for a registered New world
  identity. No name-only or captured-number-only authority was introduced.
- Therefore callable transaction methods are **not full Stan/Scarlett gameplay parity**.
  The next bounded slice needs actual accepted authored NPC/strongbox activation, an explicit
  runtime identity binding, owner/session/range-fenced dialogue/trade sessions and the exact
  captured KnuBot handlers. These are known supported runtime gaps, not requests for new AO
  captures or evidence of deliberate contract retirement.
- Existing terminal authored mission/reward identities do not establish a fresh daily reward
  generation after an expired completed DOJA cycle. New does not reset history or bypass the
  ledger to manufacture repeat rewards; that existing daily-repeat lifecycle gap remains.
  Because shared `AcceptMission` returns AlreadyApplied for Completed, New explicitly requires
  an Active result before publishing a turn-in QFU. Terminal history cannot generate false
  fresh acceptance or consume another chip; a dedicated regression test preserves this guard.

#### Validation scope

- Final focused Windows checkpoint: **PASS 308/308**, zero failed/skipped, startup validation
  **PASS**, recorded in `build-verify/gameplay-focused-final.log`. All fourteen authored tests pass.
  For provenance, the earlier 300-test run passed 298 and exposed the two DOJA account-flag
  failures described above; the repaired source then passed without relaxing either assertion.
  The subsequent terminal-cycle Active-state guard and fourteenth authored test are included
  in the final 308-test count; `.codex-inventory-tests.log` retains the earlier 307-test gate.
- `Tools/ZoneEngineSchemaValidation/AuthoredMissionSmoke.cs` uses the actual DAO and existing
  baseline mission tables in the harness-owned disposable database. It checks one atomic
  item/state/objective/observation/character-flag/account-flag/stat/ledger commit; a late XP
  constraint failure; restart/reward replay; absent owner/foreign account/foreign quest;
  stale item CAS and nonempty parent retirement. The actual disposable suite **PASS** includes
  every authored check, whole-wrapper exit 0, runtime restart **PASS**, and owned Docker
  container/network residue **NONE**. Evidence: `Tools/ZoneEngineSchemaValidation/validation.latest.log`.
  This actual DAO result is separate from, and not inferred from, the action fakes.
- Earlier disposable inventory gates passed split success, stale-count rollback, late nano
  and late final-stat rollback, populated-bag retirement rollback, and empty-bag retirement
  after an owned child move. Player trade atomicity remains unchanged and its actual DAO
  rollback proofs remain part of the shared validation suite.
