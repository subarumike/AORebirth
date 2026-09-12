# Character, stats and inventory persistence DAO

OUTCOME: acceptance in progress; this is not a completion receipt.

STARTING_SHA: `af7e2357544425b630014d29e1c3c4e3dfcce5d8`.
BRANCH: `codex/character-inventory-persistence-dao-001`.
WORKTREE: `C:\Users\Mike\Documents\AORebirth\tools-temp\character-inventory-dao001`.

Accepted integration source `c47f463d375664d2d7594451b0b8204d9eac6f27` is an ancestor. The intervening differences are CURRENT_TASK, PROJECT_STATE and the integration receipt only. The source integration checkout was clean. No other developer branch or unrelated dirty files were changed. User authorization is local commits only: no push, merge, deployment, production changes, schema changes or client launch.

## Ownership and transaction map

Paths are relative to this worktree. Runtime adapters below are in `AORebirth/Server/ZoneEngine_New/Core/Data`. Their SQL/transaction implementation moved to `AORebirth/Libraries/Source/AORebirth.Database/Domain/Characters/MySqlCharacterPersistenceDao*.cs`. Neutral contracts/DTOs are in `AORebirth/Libraries/Source/AORebirth.Interfaces/Persistence/Characters`.

| Caller / adapter | DAO operation | Tables | Boundary / guard |
| --- | --- | --- | --- |
| CharacterHydrationService / MySqlCharacterRepository | LoadCharacter | characters | Full existing gameplay projection; directory/admission/online ownership stays ICharacterDao |
| CharacterHydrationService / MySqlStatRepository | LoadStats | stats | All existing character base rows, same owner type |
| PlayerInventory / MySqlInventoryRepository | LoadCarriedItems, LoadBankItems, LoadContainerItems | item_instances | Same carried pages and separate bank/bag owners |
| ItemInstanceIdAllocator | LeaseItemInstanceIds | item_instance_id_sequence | One transaction, same LAST_INSERT_ID block lease |
| CharacterSnapshotService / MySqlCharacterRepository | SaveSnapshot | characters, stats | Location/Online plus base stats in one transaction; DTO constructed while holding Player.PersistenceGate |
| Existing standalone data callers | SaveLocation, SaveStats, InsertItem, UpdateItemLocation, SaveItemLocations | characters / stats / item_instances | DAO-owned transaction per operation |
| InventoryFlushService / MySqlCharacterCoalesceCommit | SaveInventoryAndUploadedNanos | item_instances, charactersuploadednanos | Existing coalesced boundary, per-player gate, restore known failures, quarantine unknown outcomes |
| InventoryActionService and InventoryMoveService / MySqlInventoryMutationPersistence | CommitInventoryMutation | item_instances, charactersuploadednanos, stats | RepeatableRead; park, insert, final locations, child-range guard, stack CAS, nanos, final base stats; publish/ack only after commit |
| Existing TradeService / MySqlTradePersistence | CommitItemCredits | characters row locks, item_instances, charactersuploadednanos, stats | Existing one/two-participant transaction; stable ID lock order and ordered runtime gates; no new trading feature |
| NanoService / MySqlActiveNanoRepository | LoadActiveNanos, CommitActiveNanos | characters row locks, charactersactivenanos, stats | Existing sorted-owner active nano/base-stat transaction; no new morph policy |
| ItemTemplateCatalog / MySqlItemNameRepository | LoadItemNames | itemnames | Shared read, existing runtime cache |

Runtime repositories now map DTOs, resolve configuration and translate/log errors. They contain no storage statements or transaction implementation. `DatabaseDaoFactory.CreateCharacterPersistenceDao` exposes configured construction; NewEngine explicitly registers the shared DAO with its existing provider factory. Windows/legacy and Linux source inventories include the shared sources. Isolated DAO validators link canonical IdentityType/CharacterStat enum sources rather than inventing constants.

## Full field coverage

- Character load: Id, Name, FirstName, LastName, Playfield, X/Y/Z, HeadingW/X/Y/Z. Snapshot updates only location, heading and Online; Username, names and all other columns remain untouched. Existing account-owner/admission checks still use the directory DAO. Full gameplay hydration/validation was not replaced by directory data.
- Stats: every loaded StatId/StatValue remains present. Snapshot saves non-Unset Base values, not effective bonuses. Existing derived rows remain governed by existing runtime calculations; no new stat defaults were introduced.
- Items: InstanceId, ContainerType, ContainerInstance, ContainerPlacement, ItemType, LowId, HighId, Quality, StackCount, Source are mapped in both directions. IDs are neither minted nor changed during hydration. Unique-slot parking/insertion/final-placement order and expected stack-count CAS are retained. Bank, nested bags and worn pages retain their separate identities.
- Uploaded nanos: NanoId by CharacterId, existing insert-if-missing behavior. Active nanos: NanoId, Strain, NanoInstance, DurationCentiseconds, ExpiresAtUtcTicks. Morph baseline stats stay coupled to active nano persistence.
- Cash remains a base stat in the existing item/credit transaction. Runtime equipment/nano effects are rebuilt rather than persisted into their base values.

## Failure and stale-owner handling

The DAO owns connection/transaction lifetime. Failure before COMMIT triggers rollback. Failure after COMMIT is attempted raises neutral `CharacterPersistenceCommitOutcomeUnknownException`, translated into existing runtime `DatabaseCommitOutcomeUnknownException`. Rollback/disposal errors cannot replace the primary failure. Cleanup errors after success are conservatively uncertain. There is no automatic replay.

Existing Player.PersistenceGate ordering, authoritative player/session checks in transfer/disconnect paths, coalesced writer lookup, quarantine, close and reload remain in force. The snapshot DTO is built inside the gate, so a waiting snapshot observes newly committed/published credit values instead of submitting a previously built aggregate. No distributed ownership or schema version was added.

Real disposable MySQL tests inject failure before any write and after each of six mutation statements, before sending COMMIT, after the real COMMIT succeeds but before acknowledgement, and during cleanup. Fresh connections verify complete old fingerprints or complete expected new state. Snapshot partial writes and lost acknowledgement are covered. Replaying the same item identity is rejected without losing parked locations or adding duplicate rows.

The real InventoryMoveService failure fixture verifies no success acknowledgement after either failure class. Known failures roll back without quarantine; unknown outcomes close/quarantine the player, reject the stale snapshot and block replay. A fresh DAO load discovers the committed location and permits a new authorized move. A concurrent snapshot waits while the existing real item/credit transaction commits and publishes; its subsequent save preserves the newer cash value.

## Gameplay and lifecycle evidence boundaries

- The application fixture selects actual armor template `21793` from current `items.dat`; actual LootableDynel open, InventoryMoveService loot claim, timed equip, replace/swap, unequip and re-equip operations create/move durable item instances. These are not seeded worn rows substituted for equipment operations.
- Nonzero modifier proof is explicitly synthetic: a +11 ProjectileAC OnWear modifier exists only in the application fixture's memory. Repeated rebases and DAO reload preserve base 100 and apply the modifier once. It is never written to GameData or shipped as runtime content.
- Important pre-existing content limitation: inspection through the actual item catalog found 131,384 templates, including 15,961 armor templates, with no armor wearable-effect definitions loaded. Therefore this task cannot claim capture-backed real armor effect acceptance. Storage identity/placement survives real engine processes; the nonzero modifier test is separate application evidence.
- Connected fixtures subsequently load the same application-mutated IDs in real LoginEngine/NewEngine processes; six item rows are compared, including the two actually looted/equipped items. Existing morph, authored mission and generated mission assertions remain present.
- Added connected route exercises supported GM transfer 4582 -> 800 -> 4582, redirect admission, a real GM cash-stat mutation, logout, fresh-auth login and different-process restart. It does not claim arbitrary TCP-loss recovery or official-client acceptance.

## Preserved authorities and remaining bypasses

- Existing authored/generated mission transactions, corpse credit claims, reward ledgers and mission-owned inventory operations remain IMissionDao-owned. No duplicate mission/reward authority or shared aggregate save is added.
- Legacy CharacterDao creation/deletion and legacy-engine persistence remain outside this NewEngine slice. Directory/account consumers retain the accepted shared DAO stack. Other systems still require their own migration; this is not a claim that every AORebirth system is DAO-owned.
- Website/AccountBroker/BotService storage boundaries are unchanged. Two obsolete NewEngine character/stat exceptions were removed from the architecture guard; other documented exceptions remain.
- Unsafe legacy LoginDataDao.SetGM is neither changed nor called. Test GM ability is seeded only in the disposable fixture, scoped to its character.
- No schema/migration change, production operation, capture/JSON persistence, new vendor/trade feature or official client launch.

## Validation receipt pending

Baseline exact-source Windows acceptance at starting SHA: PASS, all 12 mandatory stages. Initial new build and 534 NewEngine tests: PASS. Initial schema/disposable fault suite: PASS. Gameplay and runtime uncertainty application fixtures: PASS. Final source SHA, refreshed full suite counts, connected result, Windows acceptance and Linux publication result will be recorded after validation completes.

Logs are local ignored artifacts in `build-verify/persistence-dao/`. Exact commands used are the repository wrappers: NewZoneEngineBuild/build.cmd, Tools/build_aorebirth_debug.cmd, Tools/run_account_dao_validation.cmd, Tools/run_character_dao_validation.cmd, Tools/run_mission_dao_validation.cmd, Tools/run_zoneengine_schema_validation.cmd, Tools/run_newengine_connected_acceptance.cmd, Tools/accept_windows_source.cmd and LinuxBuild/publish-zoneengine.cmd. Final receipt will include arguments and log names. Linux host execution and official client gameplay are NOT RUN.
