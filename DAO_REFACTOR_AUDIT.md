# AORebirth DAO Refactor Audit

Status: COMPLETE  
Audit date: 2026-09-01  
Scope: current tracked AORebirth repository; no runtime, schema, packet, gameplay, connector, or startup behavior was changed.

## 1. Executive finding

AORebirth already has two useful persistence foundations, but it does not yet have the requested domain-oriented DAO boundary:

1. `AORebirth.Interfaces.IDatabaseConnector` plus `MySQLConnector`, `MSSqlConnector`, `NpgsqlConnector`, and `Connector` form a database **provider/connection abstraction**.
2. `AORebirth.Database.Dao.Dao<T,TU>`, `IDao<T>`, table attributes, `DB*` entities, and the concrete `*Dao` classes form a **table-oriented data mapper/active-record utility**.

Neither foundation stops runtime code from naming database entities and tables, choosing transactions, opening connections, or embedding SQL. Production assemblies still consume `AORebirth.Database`, `DBCharacter`, `DBStats`, `DBItem`, and table-shaped generic methods directly. Domain objects also implement `IDatabaseObject.Read/Write`, so persistence ownership is mixed into `Character`, stats, and inventory objects.

The repository contains **7 production source files outside `AORebirth.Database` that execute embedded SQL**. Five are domain/service persistence implementations and two are embedded engine database-readiness validators. A further three production sites own provider connections without embedding SQL: `BaseInventoryPage.Write`, `AccountBrokerService.Program`, and `BotService.Program`.

The current production truth is MySQL-oriented. Connector classes for MSSQL and PostgreSQL compile, and generic identity retrieval selects `LAST_INSERT_ID()`, `@@SCOPE_IDENTITY`, or `LASTVAL()`. However, active custom paths use MySQL-only syntax including backticks, `LIMIT`, `FOR UPDATE`, `INSERT IGNORE`, `ON DUPLICATE KEY UPDATE`, `DATABASE()`, `information_schema`, `CURRENT_TIMESTAMP(6)`, and `LAST_INSERT_ID()`. Linux readiness explicitly requires `SQLType=MySql`. Provider parity is therefore **structurally present but not behaviorally proven**.

## 2. Audit method and coverage

The audit searched tracked source, project, command, PHP, and SQL surfaces for the requested identifiers and operations:

```text
MySqlConnection / MySqlCommand
SqlConnection / SqlCommand
NpgsqlConnection / NpgsqlCommand
IDbConnection / IDbCommand
DbConnection / DbCommand
ExecuteReader / ExecuteNonQuery / ExecuteScalar
SELECT / INSERT / UPDATE / DELETE / REPLACE / CALL
Connector.Instance / Connector.GetConnection
IDatabaseConnector / IDatabaseObject
```

The following runtime and persistence areas were then traced manually rather than treated as string counts:

- `AORebirth.Database`, `AORebirth.Interfaces`, and `AORebirth.Stats`;
- ZoneEngine, LoginEngine, ChatEngine, WebEngine, AccountBrokerService, and BotService;
- character creation, hydration, save, delete, online state, reconnect, and stale-online recovery;
- inventory, stats, uploaded nanos, active nanos, perks, missions/quests, organizations, accounts, chat/social state, NPC/world content, vendors, legacy loot, GMI, and bot identity;
- database preflight, schema/migration, validation, Linux integration, and legacy/generated surfaces;
- current Windows and Linux project references and validation wrappers.

`bin`, `obj`, restored packages, capture outputs, third-party `msgpack-cli`, and generated evidence payloads were excluded from source-site counts. SQL schema files and validation fixtures were classified separately. False positives such as gameplay methods named `Delete`, packet `Update` types, and serialization `Read`/`Write` methods were manually rejected.

## 3. Current dependency and ownership map

```text
LoginEngine / ChatEngine / ZoneEngine / WebEngine
        |                    |
        +--> AORebirth.Core -+
        |         |
        |         +--> AORebirth.Database   (current wrong direction)
        |
        +------------> AORebirth.Database
                              |
                              +--> AORebirth.Interfaces.IDatabaseConnector
                              +--> Dapper
                              +--> MySqlConnector / SqlClient / Npgsql

AccountBrokerService --> AORebirth.AccountBroker --> System.Data + SQL
BotServiceHost -------> AORebirth.BotService -----> System.Data + SQL
```

Evidence:

- `AORebirth.Core.csproj` references `AORebirth.Database` and Dapper.
- LoginEngine, ChatEngine, ZoneEngine, and WebEngine reference `AORebirth.Database` directly.
- `AORebirth.Database.csproj` references `AORebirth.Interfaces`, not the engines.
- AccountBroker and BotService own their own ADO.NET persistence rather than using `AORebirth.Database`.
- All principal Windows projects target .NET Framework 4.8; Linux uses explicit companion projects and source inventories.

The current direction makes database implementation types visible to almost every runtime layer. The useful inversion point is already available: `AORebirth.Database` depends on `AORebirth.Interfaces`, and engines/Core also depend on `AORebirth.Interfaces`.

## 4. Existing abstraction roles

| Component | Current role | Evidence | DAO assessment |
| --- | --- | --- | --- |
| `IDatabaseConnector` | Provider-neutral factory for an `IDbConnection` and connection string. | `AORebirth.Interfaces/IDatabaseConnector.cs:42-58` | Provider abstraction, not DAO. KEEP. |
| `MySQLConnector` | Creates unopened `MySqlConnection`. | `MySQLConnector.cs:47-95` | Provider adapter. KEEP. |
| `MSSqlConnector` | Creates unopened SqlClient connection; uses Microsoft.Data.SqlClient on Linux and System.Data.SqlClient on Windows. | `MSSqlConnector.cs:41-91` | Provider adapter. KEEP, but parity unproven. |
| `NpgsqlConnector` | Creates unopened `NpgsqlConnection`. | `NpgsqlConnector.cs:47-95` | Provider adapter. KEEP, but parity unproven. |
| `Connector` | Static configuration-driven provider selector; opens every returned connection and caches provider/config state. | `Connector.cs:60-120` | Provider composition/connection factory. EVOLVE behind injected DAO implementations; retain compatibility during migration. |
| `IDatabaseObject` | Adds parameterless `Read()`/`Write()` persistence behavior to domain objects. | `IDatabaseObject.cs:36-50`; implemented by stats/inventory hierarchy. | Persistence leakage into domain. DEPRECATE, then REMOVE LATER after all consumers migrate. |
| `IDao<T>` / `Dao<T,TU>` | Generic table CRUD, reflection-based SQL generation, optional external connection/transaction, static singleton. | `IDao.cs`; `Dao.cs:55-694` | Useful temporary mapper inside implementation layer, but not a public domain DAO. DEPRECATE from runtime consumption; REMOVE LATER or keep internal only. |
| `DB*` entities | Table-shaped rows annotated with `[Tablename]`. | `AORebirth.Database/Entities` | Implementation DTOs. KEEP internal to data access; stop returning them to engines. |
| specific legacy `*Dao` classes | Mix generic CRUD, domain methods, schema checks, and cross-table orchestration. | `AORebirth.Database/Dao` | EVOLVE into implementation details/adapters; do not expose their singleton APIs to runtime. |

### Important generic-DAO semantics

- `Add`, `Delete`, and `Save` open a connection and transaction when the caller does not supply them; supplied transactions are honored.
- `Get`, `GetAll`, `GetWhere`, and `Count` return table entities directly.
- `SqlMapperUtil.SetIdentity` is dialect-switched, but generated CRUD SQL and custom DAOs are not sufficient proof of provider parity.
- `Dao<T,TU>.Instance` and `DaoSingleton<T>` are service locators. They hide construction and make architecture testing difficult.
- `DBCharacter`, `DBStats`, and other database rows cross into packet, engine, and gameplay code, so table shape is part of the runtime API today.

## 5. Classification summary

| Category | Meaningful current sites | Disposition |
| --- | --- | --- |
| A. Production gameplay/runtime persistence | Character/stats/inventory domain `Read/Write`; Zone login/save/logout; mission repository and roll fee; active nanos; perks; GMI; world/NPC/vendor/loot loaders; runtime organization and item operations. | Migrate to domain DAOs by vertical slice. |
| B. Authentication/account persistence | `LoginDataDao`; LoginEngine handlers/console paths; AccountBrokerService SQL and host connection creation. | Split legacy game-account DAO from unified identity DAO. |
| C. Chat/social persistence | Chat authentication/character directory; online state; buddy CSV in `characters`; `receivedmessages`; organization reads. | Use character/account/org/chat-social DAOs. |
| D. Bot/service persistence | `IPersistentBotRepository`, in-memory implementation, `AdoNetBotRepository`, BotService provider construction. | Existing domain seam is good; move ADO implementation below DAO boundary. |
| E. Administrative tooling | Zone/Login chat or console commands using character, organization, NPC, vendor, item, teleport, and account DAOs. | May consume domain DAOs; no need to ban domain DAO use. Direct SQL remains forbidden in production command code. |
| F. Schema/validation/migration tooling | `Tools/DatabasePreflight`, engine `ValidateDatabase` modes, `Misc.CheckDatabase`, account identity migrations/validator, schema SQL, Linux database contract tools. | Keep separate from gameplay DAOs and explicitly allow direct SQL. |
| G. Tests | AccountBrokerValidation, UnifiedAccountFlowValidation, BotSchemaValidation, Stage6/7 MySQL integration, DatabasePreflightSelfTests, AOtomation mission/hydration tests. | Direct SQL allowed for fixtures/assertions; production dependency rules still tested. |
| H. Generated or legacy | obsolete SQL tables, commented `BuddyListDao`, root `LftSearch_*` copies, SQL staging, inactive schema tables with no consumer. | Preserve until separately proven removable. |
| I. Unknown / needs investigation | `Misc.GetOrgMembers` targets legacy `characters_stats` and has no current callsite; `playfields` SQL has no runtime consumer; several required legacy tables have deletion/preflight but no active read/write owner; optional GMI schema definition is absent. | Do not invent DAOs or remove schema; resolve before its migration phase. |

## 6. Direct production SQL outside `AORebirth.Database`

Counting rule: one site is one production source file outside `AORebirth.Database` that constructs and executes SQL. This produces 7 sites: 5 domain/service persistence sites plus 2 embedded validation sites.

| # | File / class / methods | Category | SQL and tables | Read/write; transaction; connection | Provider coupling | Proposed owner | Risk / tests / dependencies |
| --- | --- | --- | --- | --- | --- | --- | --- |
| 1 | `ZoneEngine/Program.cs`, `ValidateDatabase`, `ValidateRequiredDatabaseColumn` | F | Reads `DATABASE()`, `information_schema.tables/columns/statistics`, every required table, `characters.Online`, and the `charactersactivenanos` contract. | Read-only. Owns `Connector.GetConnection`; no transaction. | Explicitly requires MySQL; backticks, `DATABASE()`, MySQL metadata. | Keep as database readiness tooling, preferably shared with `DatabasePreflight`, not a gameplay DAO. | Startup behavior is release-critical. Existing Linux contract/smoke gates. |
| 2 | `ChatEngine/Program.cs`, `ValidateDatabase` | F | Same required-table and metadata/read-access validation subset. | Read-only; owns `Connector` connection; no transaction. | Explicit MySQL gate. | Shared readiness component/tooling. | Startup behavior risk; Linux ChatEngine gates. |
| 3 | `ZoneEngine/StaleOnlineRecovery.cs`, `AdoNetStaleOnlineRecoveryStore` | A | `SELECT/UPDATE characters.Online`, `SELECT DATABASE()`. | Serializable transaction begins in constructor; rows are locked `FOR UPDATE`; bounded update, post-count, explicit commit/rollback; store owns connection. | MySQL `DATABASE()` and `FOR UPDATE`. | `ICharacterDao.RecoverStaleOnlineState` or a dedicated online-state operation owned by character DAO. | Highest session-safety risk. Existing stale-online/reconnect contract coverage; add real MySQL rollback/concurrency tests. |
| 4 | `ZoneEngine/Core/Missions/MySqlMissionRepository.cs` | A | Reads/writes `missionstates`, `missionobjectiveprogress`, `missionobjectiveobservations`, `missionflags`, `missionaccountflags`, `missionrewardledger`, `characters`, `stats`. | Read methods own one connection. `Execute` owns one transaction and supplies a transaction object to all mission operations. | MySQL class and syntax: backticks, `FOR UPDATE`, `INSERT IGNORE`, `ON DUPLICATE KEY UPDATE`. | `IMissionDao` implementation. | High atomicity risk but best existing seam. Strong in-memory mission tests; missing live DAO integration/rollback parity tests. |
| 5 | `ZoneEngine/Core/Missions/MissionRollFeeService.cs`, persisted fee path and `ReadCash` | A | Locks `missionrewardledger` and `stats`; upserts cash; inserts fee ledger. | Owns connection and transaction; stat debit and idempotency ledger commit together. | MySQL `FOR UPDATE` and `ON DUPLICATE KEY UPDATE`. | `IMissionDao.TryChargeRollFee` or mission transaction operation. | Currency duplication/loss risk. Add concurrent duplicate/idempotency MySQL tests before movement. |
| 6 | `AORebirth.AccountBroker/AccountBrokerService.cs` | B | `login`, `characters`, `account_identities`, `account_game_mappings`, `account_external_mappings`, `account_provisioning_jobs`, email verification and password-reset token tables. | Each public operation opens via injected factory. Mutations generally use ReadCommitted or default transactions; provisioning and password/token transitions are atomic. | MySQL timestamps, `FOR UPDATE`, `LAST_INSERT_ID()`. Host directly constructs `MySqlConnection`. | `IAccountIdentityDao` plus `IAccountDao` for legacy login credentials/rows; service retains policy/orchestration. | Authentication and account corruption risk. AccountBrokerValidation and UnifiedAccountFlowValidation cover idempotency, recovery, tokens, concurrency, and login compatibility. |
| 7 | `AORebirth.BotService/BotPersistence.cs`, `AdoNetBotRepository` | D | `bot_principals`, `bot_credentials`, `bot_scopes`, `bot_audit_events`, plus account identity metadata checks. | Injected connection factory; mutation helper owns ReadCommitted transaction and rollback. | MySQL metadata, `DATABASE()`, `LIMIT`, `FOR UPDATE`. Host reflectively constructs configured provider connection. | `IBotDao`; existing `IPersistentBotRepository` is a near-equivalent domain interface. | Credential/audit/rotation risk. BotSchemaValidation includes rollback, concurrency, constraints, and lifecycle coverage. |

### Connection ownership without embedded SQL

| File / method | Current behavior | Required migration |
| --- | --- | --- |
| `AORebirth.Core/Inventory/BaseInventoryPage.Write` | Runtime domain object calls `Connector.GetConnection`, begins a transaction, deletes/reinserts `items` and `instanceditems`. | Move the complete page replacement transaction into `IInventoryDao.ReplaceContainer`. |
| `AccountBrokerService/Program.cs` | Composition root directly creates `MySqlConnection`. | Let the database implementation/factory own provider construction; host receives `IAccountIdentityDao`. |
| `BotService/Program.cs` | Reflects a configured provider type and returns `IDbConnection`. | Move provider construction under DAO implementation/factory; host receives `IBotDao`. |

## 7. Current table-oriented DAO map

| Current implementation | Tables / behavior | Principal consumers | Future domain owner | Migration risk |
| --- | --- | --- | --- | --- |
| `LoginDataDao` | `login` read/count/insert/password/GM/expansions; logs off account characters. `SetGM` currently has no username predicate and must be characterized, not silently corrected during migration. | Login auth and console; Chat auth/GM; Zone login; Web index. | `IAccountDao` | Critical authentication/default/null behavior. |
| `CharacterDao` | `characters`; buddy CSV; ownership lookup; location; online state; logged-in list. Character delete also removes organization, stats, inventory, received messages, mission state, timers, nanos, meshes, uploads, and perks in one transaction. | Login, Chat, Zone, Web, Mail recipient lookup, mission account resolution. | `ICharacterDao` plus coordinated child cleanup. | Highest aggregate and cross-engine risk. |
| `StatDao` | `stats`; bulk replace, bulk upsert, one-stat reads, organization stat clearing. | Character creation/hydration/save, Login/Chat lists, combat XP, active nanos, organizations, NPC spawn stats. | `ICharacterStatsDao` and transaction participation in mission/org operations. | Defaults, unsigned conversions, sparse rows, transaction visibility. |
| `ItemDao` / `InstancedItemDao` | `items`, `instanceditems`; container load, delete, replace; instanced stat blob. | Inventory pages, character creation loadouts, item deletion. | `IInventoryDao` | Item loss/duplication, binary data, slot/container key semantics. |
| `UploadedNanosDao` | `charactersuploadednanos`; append missing uploads. | Character read/write; nano upload; pet shell. | `ICharacterNanoDao` | Current write is additive, not replace; preserve exact semantics. |
| `CharacterActiveNanosDao` | `charactersactivenanos`; load, delete/replace, expire. | ActiveNanoRuntimeService, morph flight. | `ICharacterNanoDao` | `ReplaceActiveNanos` is currently multiple independent transactions; do not claim atomicity. |
| `CharacterPerksDao` | `charactersperks`; required-table check; additive writes; deletes/reset. | Character read/write; PerkRuntimeService. | `ICharacterPerkDao` | Runtime required-table fail-closed behavior and additive semantics. |
| `OrganizationDao` | `organizations`; create/read/existence/government/leader. | Zone org handler, Chat org channels, city/playfield logic. | `IOrganizationDao` | Organization and member-stat changes are not consistently one transaction. |
| `ReceivedMessagesDao` | `receivedmessages`; recent sender history. | Chat `BuddyList.LoadRecentMsgsList`. | `IChatSocialDao` | Low; verify ordering/retention. |
| `BuddyListDao` | Entire implementation commented out. Active buddies are CSV in `characters.BuddyList`. | No active DAO consumer. | `IChatSocialDao` only after behavior is characterized. | Legacy/unknown; do not revive table assumptions. |
| `MobSpawnDao` / `MobSpawnStatDao` | `mobspawns`, `mobspawns_stats`. | `PlayfieldDbMobSpawnRuntimeService`, admin NPC command. | `INpcDao` | Runtime identity and population behavior. |
| `MobTemplateDao` | `mobtemplate`; hash/name lookups and full template projection. | NPC creation, pets, spawn commands, legacy loot adapter. | `INpcDao` | Large binary/array fields, random-level defaults, identity coupling. |
| `StaticDynelDao` | `staticdynels`. | Playfield content provider, Nascence statue lookup. | `IPlayfieldDao` | Serialized stats blob and file/DB content precedence. |
| `TeleportDao` | `teleports`. | Teleport/proxy admin commands. | `IPlayfieldDao` | Mostly administrative; active runtime path must be confirmed. |
| `VendorDao` / `VendorTemplateDao` / `ShopInventoryTemplateDao` | `vendors`, `vendortemplate`, `shopinventorytemplates`. | Vendor spawn/entity/inventory, shop admin command. | `IVendorCatalogDao` | QL randomization and missing-template fallback must stay outside DAO. |
| `ItemNamesDao` | `itemnames`. | Trade skills, vendors, DailyLogin, GMI, loot display, scripts/admin. | `IItemCatalogDao` | Broad read-only fan-out; cache/null behavior. |
| `TradeSkillDao` | `tradeskill`. | `TradeSkill` runtime initialization. | `IItemCatalogDao` | Read-only rules load. |
| `MobDroptableDao` | `mobdroptable`. | Legacy fallback in `GlobalLootRuntimeService`. | `ILootDao` | Must not absorb capture-backed/code-defined loot catalogs. |
| `GmiVaultDao` | Optional `gmi_vault`, `gmi_vault_item`; load, availability check, atomic header+item replace. | `GmiRuntimeService`. | `IGmiVaultDao` | Optional schema absent from repository; fail-closed behavior is mandatory. |
| `NewCharacterStartAreaSelectionDao` | `missionflags`; pending/read/conditional complete. | Login character positioning, Zone selection runtime. | `IMissionDao` (or character provisioning coordinator calling it). | MySQL upsert/conditional state transition. |
| generic lookup DAOs (`VendorTemplateDao`, etc.) | Table-shaped `Get/GetAll/GetWhere/Add/Save/Delete`. | Engines/Core directly. | Internal implementation helpers only. | Public generic CRUD must disappear from runtime. |

## 8. Persistence call chains

### 8.1 Login authentication and character list

```text
UserCredentialsHandler
  -> LoginDataDao.GetByUsername(login)
  -> password verification and flags
  -> CharacterList.Create
       -> CharacterDao.GetAllForUser(characters)
       -> StatDao.GetById(stats: level, breed, sex, profession) per character
```

The DAO target is not `GetAll<DBCharacter>`. It is `IAccountDao.LoadForAuthentication` and `ICharacterDao.ListForAccount`, with a character-list DTO that preserves missing-stat defaults and list ordering.

### 8.2 Character creation

```text
CharacterName.CheckAgainstDatabase
  -> CharacterDao.ExistsByName
  -> CharacterName.CreateNewChar
       -> CharacterDao.Add(characters)                         [transaction 1]
       -> LoginDataDao.GetByUsername(login)                    [reads]
       -> StatDao.BulkReplace(stats)                           [transaction 2]
       -> profession starter loadout -> ItemDao.Save(items)   [transaction 3]
       -> StarterVitalStats / StarterXpStats                  [additional calls]
  -> SendNameToStartPlayfield
       -> CharacterDao.Save + SetPlayfield                    [separate commits]
       -> NewCharacterStartAreaSelectionDao.MarkPending       [separate commit]
```

Character creation is **not currently one atomic database transaction**. The DAO migration must first preserve that observable boundary. Making it atomic is desirable but is a separate behavior change requiring explicit tests and approval.

### 8.3 Zone login/hydration

```text
ZoneClient.CreateCharacter
  -> CharacterDao.Get
  -> optional legacy playfield remap -> CharacterDao.Save/SetPlayfield
  -> new Character(...).Read()
       -> CharacterDao.Get
       -> UploadedNanosDao.ReadNanos
       -> CharacterPerksDao.ReadPacketIds
       -> BaseInventoryPages.Read
            -> each BaseInventoryPage.Read
                 -> ItemDao.GetAllInContainer
                 -> InstancedItemDao.GetAll
       -> Dynel.Read -> Stats.Read -> StatDao.GetAll(stats)
  -> reconnect-specific perk/inventory reload
  -> Character.Stats.Read again
  -> MissionRuntime.ReloadForLogin/Reconnect/Zoning
       -> PersistentMissionService -> IMissionRepository -> MySqlMissionRepository
  -> ActiveNanoRuntimeService restore
```

Hydration has fail-closed inventory trust state. DAO DTO mapping must not bypass `Loading/Hydrated/Failed` behavior or turn an error into an empty inventory.

### 8.4 Character save/logout

```text
Character.Write
  -> BaseInventoryPages.Write
       -> each page BaseInventoryPage.Write                    [one transaction per page]
  -> CharacterDao.Save(characters)                            [separate transaction]
  -> CharacterDao.SetPlayfield                               [separate transaction]
  -> UploadedNanosDao.WriteNanos                             [additive independent writes]
  -> CharacterPerksDao.WritePerks                            [additive independent writes]
  -> Dynel.Write -> Stats.Write -> StatDao.BulkUpsert         [separate transaction]
Character.Dispose
  -> Save
  -> CharacterDao.SetOffline                                 [must remain after save]
```

This is not a single character aggregate transaction. The ordering—especially save before offline—is a current behavioral contract.

### 8.5 Character deletion

`CharacterDao.Delete` and authenticated `DeleteForUser` own a single transaction spanning:

```text
organizations + organization membership stats
items + instanceditems
receivedmessages
stats
missionflags + missionstates + missionobjectiveprogress
missionobjectiveobservations + missionrewardledger
characterstimers + charactersactivenanos + charactersmeshs
charactersuploadednanos + charactersperks
characters
```

This is the most important cross-DAO transaction boundary. It must remain one implementation operation such as `ICharacterDao.DeleteCharacterForAccount`, not a domain service loop over independent DAOs.

### 8.6 Inventory mutation

- Page hydration reads uninstanced and instanced rows separately, maps stat blobs, then marks the page hydrated.
- Page replacement deletes/reinserts both item kinds in one transaction for that page.
- Aggregate inventory save loops pages, so multiple pages are separate transactions.
- `BaseInventoryPage.Remove` deletes one row before in-memory removal.
- `InventoryContainerRuntimeService.DeleteInventoryItemAction` calls `ItemDao.Delete` and then removes the in-memory item.

The future `IInventoryDao` must expose container/page operations, not item-table CRUD, and must preserve the fail-closed hydration gate.

### 8.7 Missions and quests

`MissionRuntime` constructs `PersistentMissionService` and `MissionRewardCoordinator` over `MySqlMissionRepository`. `IMissionRepository` is already domain-specific and has an in-memory implementation. Its transaction interface atomically owns mission state, objectives, dedupe observations, flags, account flags, reward claims, and character-stat rewards. `MissionRollFeeService` is a second SQL owner that atomically couples cash with an idempotency ledger.

This is the repository's strongest existing DAO seam and the recommended first production slice.

### 8.8 Account Broker

`AccountBrokerService` mixes domain policy with SQL. Its public operations correctly define useful transaction boundaries: authentication snapshot, account provisioning, legacy linking, external mapping, password change/reset, and email verification. These boundaries should become `IAccountIdentityDao` methods/transactional commands; password hashing, validation, recovery decisions, and HTTP behavior remain in domain/service code.

### 8.9 Chat/social

- Chat authentication reads `login`, verifies character ownership, and creates a chat character.
- Character directory/name/online/playfield/stat/org queries are scattered through packet builders and client objects.
- Active buddies are persisted as CSV in `characters.BuddyList`; the table-based `BuddyListDao` is commented out.
- `receivedmessages` supplies recent sender history.
- Chat and Zone both write `characters.Online`, so online state is cross-engine and race-sensitive.

### 8.10 NPC, playfield, vendor, loot, and item catalogs

- `PlayfieldContentDataProvider.ResolveStaticDynels` reads `staticdynels` and then applies runtime mapping/validation.
- `PlayfieldDbMobSpawnRuntimeService` reads `mobspawns` and `mobspawns_stats`; runtime identity adaptation stays in ZoneEngine.
- `NonPlayerCharacterHandler` and pet code read `mobtemplate`.
- `VendorHandler`, `Vendor`, and `VendorInventoryPage` read vendor/template/shop rows; QL selection and item construction are gameplay behavior, not DAO behavior.
- `GlobalLootRuntimeService` reads all `mobtemplate` and `mobdroptable` rows only for the legacy database adapter. Capture-backed and documented code catalogs are separate and must not move into a database DAO.
- `itemnames` and `tradeskill` are read-only catalog sources with broad runtime fan-out.
- Playfield metadata itself is loaded from `playfields.dat` and `XML Data/Playfields.xml`; the checked-in `playfields.sql` is not an active runtime authority.

### 8.11 GMI

`GmiVaultDao.Save` atomically upserts the vault header, deletes previous item rows, and reinserts the ordered item snapshot. Missing optional tables are cached as unavailable and runtime work is skipped. No checked-in schema definition for the GMI tables was found; that is an explicit evidence gap, not permission to invent a schema.

### 8.12 Bot service

BotService already separates `IPersistentBotRepository` from `AdoNetBotRepository` and provides an in-memory implementation. Mutation operations use one ReadCommitted transaction for principal, credential, scope, and audit changes. The remaining architecture issue is placement: ADO.NET and SQL live in the domain library, and host construction owns provider creation.

## 9. Transaction boundary inventory

| Operation | Current atomic boundary | DAO rule |
| --- | --- | --- |
| Character delete | One transaction across character and all enumerated owned tables. | One DAO operation; never split across independent calls. |
| Character create | Multiple transactions/calls. | Preserve first; only add atomic provisioning in a separately approved behavior slice. |
| Inventory page replace | One transaction per page across `items` and `instanceditems`. | `ReplaceContainer` owns it. Aggregate character inventory is not currently atomic. |
| Stat bulk replace | Delete + all inserts in one transaction. | Preserve. |
| Stat bulk upsert | One write transaction, but existing-row reads use `GetById` outside the supplied transaction. | Characterize before changing isolation/visibility. |
| Active nano replace | Delete and adds are independent transactions. | Preserve initially; atomic correction requires explicit acceptance. |
| Uploaded nano/perk save | Additive independent inserts. | Preserve additive semantics. |
| Mission mutation/reward | One repository transaction; stat reward and ledger can commit together. | DAO transaction command required. |
| Mission roll fee | Cash stat + fee ledger in one transaction with row locks. | DAO operation required. |
| Stale-online recovery | Serializable transaction with `FOR UPDATE`, bounded update, verification, commit/rollback. | DAO operation required. |
| Online set/offline | Independent single-row updates across Login/Chat/Zone; file-based ownership guard coordinates handoff. | Preserve ordering and ownership guard. |
| GMI save | Header upsert + delete/reinsert items in one transaction. | One DAO operation. |
| Account provisioning/link/token/password | One transaction per public command, generally ReadCommitted. | One account-identity DAO command per boundary. |
| Bot lifecycle mutation | One ReadCommitted transaction including audit. | One bot DAO command per boundary. |
| Organization create/member changes | Multiple independent operations in current handlers. | Preserve first; do not manufacture atomicity during mechanical migration. |

## 10. Provider-support assessment

### Confirmed

- Configuration and `Connector` recognize `MySql`, `MsSql`, and `PostgreSQL`.
- All three connector classes implement `IDatabaseConnector` and return `IDbConnection`.
- Generic insert identity selection has three dialect branches.
- MSSQL uses platform-specific SqlClient imports while retaining the same connector contract.

### Not confirmed

- There are no current MSSQL/PostgreSQL DAO integration suites comparable to MySQL validation.
- Active schema files use MySQL DDL.
- Mission, GMI, account, bot, perks, and readiness paths contain MySQL-only SQL.
- Linux readiness and production evidence explicitly require MySQL.

Conclusion: the migration must **preserve the connector classes and configuration options**, but must document individual DAO implementations as MySQL-only until provider integration tests prove parity. It must not silently route MSSQL/PostgreSQL through MySQL SQL or declare them supported based only on connection construction.

## 11. Tooling, tests, generated, and unknown surfaces

### F. Schema/validation/migration tooling that may retain direct SQL

- `Tools/DatabasePreflight/DatabasePreflightCommand.cs` and self-tests.
- ZoneEngine and ChatEngine headless `ValidateDatabase` modes, until safely shared with preflight tooling.
- `AORebirth.Database/Misc.CheckDatabase` and `SqlTables/*.sql` (legacy interactive schema bootstrap).
- `AORebirth.Database/Migrations/*.sql`.
- `Tools/AccountIdentitySchema` validator and SQL.
- Linux Stage6/Stage7 MySQL integration/security tools and contract fixtures.
- Bot schema and account/unified-flow validation tools.

These are not gameplay DAOs. The architecture guard must allow them by path/project, not by arbitrary filename comments.

### G. Reusable tests

- `PersistentMissionFoundationTests`: identity isolation, dedupe across reconstruction, lifecycle reload, reward retry, atomic stat+ledger, account flags, rollback.
- `QuestRuntimePersistenceTests`: character isolation, restart/retry behavior, atomic handoff, reward idempotency.
- `LoginSessionHydrationSafetyContractTests`: inventory fail-closed behavior, reconnect ordering, optional GMI behavior.
- `AccountBrokerValidation`: provisioning recovery/idempotency, legacy links, password and email tokens, concurrency, login password compatibility.
- `UnifiedAccountFlowValidation`: HTTP-to-database end-to-end account flow.
- `BotSchemaValidation`: schema, repository lifecycle, transaction rollback, concurrent rotation, constraints, credential secrecy.
- `DatabasePreflightSelfTests`, Stage6 MySQL integration, Stage7 MySQL security integration, and Linux offline/contract smoke tests.

The `AORebirth.Database/DatabaseTests` project contains only assembly metadata and supplies no useful current DAO tests.

### H. Generated/legacy

- `.sql.obsolete` tables are historical and must remain outside production DAO scope.
- `BuddyListDao` is commented code and is not runtime truth.
- `tools-temp/sql-staging` and evidence SQL are administrative/staging artifacts.
- root `LftSearch_before_pull.cs` and `LftSearch_49058412.cs` are not normal engine paths.

### I. Explicit unresolved items

1. `Misc.GetOrgMembers` queries `characters_stats`, while the governed schema uses `stats`; no current production callsite was found.
2. `playfields.sql` exists, but runtime playfield metadata comes from files and no active DAO/entity consumes the table.
3. `characterstimers`, `charactersmeshs`, `mobspawnsactivenanos`, `mobspawnsinventory`, `mobspawnsmeshs`, and `mobspawnsuploadednanos` are required by startup and/or deleted during character cleanup, but no complete current runtime read/write owner was found in this audit.
4. GMI runtime expects `gmi_vault` and `gmi_vault_item`, but their schema definition is not checked in under the audited repository.
5. Mail runtime uses character lookup for recipient/sender resolution, but no mail persistence table or durable mail body store was found. Do not create `IMailDao` without new evidence.
6. Shop/vendor persistence is catalog-style read behavior; no separate transactional shop-session persistence was found.

## 12. Current-state counts

```text
DIRECT_RUNTIME_SQL_SITES=7
DOMAIN_OR_SERVICE_SQL_SITES=5
EMBEDDED_VALIDATION_SQL_SITES=2
GOVERNED_CORE_TABLES=34
ALLOWED_ACCOUNT_EXTENSION_TABLES=6
PROVIDER_CONNECTORS=3
USEFUL_CURRENT_DATABASE_TEST_PROJECTS=0
IMPLEMENTATION_STARTED=NO
```

## 13. Post-audit implementation checkpoint (2026-09-01)

The completed audit above remains the historical pre-implementation snapshot.
Phase 1 infrastructure and the Phase 2 mission-persistence slice now implement
the recommended dependency direction:

```text
Mission runtime and services
        -> IMissionDao
        -> MySqlMissionDao
        -> existing Connector
```

The production mission repository SQL, generated roll-fee SQL, start-area flag
operations, and MissionRuntime account-key lookup now cross the DAO contract.
The existing domain service contract is preserved by an explicit Zone adapter.
The deterministic architecture guard retains five reviewed legacy runtime SQL
sites outside the mission slice and reports zero direct mission runtime SQL
sites. Character deletion retains its existing cross-owner mission-table cleanup
inside the character aggregate transaction pending the later character phase.

No schema, packet, mission definition, reward, or gameplay changes are part of
this slice. The provider remains the existing MySQL-specific Connector; provider
parity is an unchanged limitation. Disposable MySQL and governed exact-SHA
acceptance remain validation gates, not permission to use production data.
