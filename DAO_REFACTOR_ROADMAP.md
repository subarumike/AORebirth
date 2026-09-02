# AORebirth DAO Refactor Roadmap

Status: Phase 1 infrastructure and Phase 2 mission slice implemented; final environment-dependent acceptance remains.
Basis: `DAO_REFACTOR_AUDIT.md` and current repository source on 2026-09-01.

Implementation checkpoint (2026-09-01): production MissionRuntime, generated
mission roll-fee persistence, and new-character start-area selection now consume
`IMissionDao`. Mission SQL is owned by `MySqlMissionDao` in
`AORebirth.Database`; the Zone adapter retains the existing
`IMissionRepository` service boundary. The architecture guard reports five
reviewed non-mission legacy runtime SQL sites and zero mission runtime SQL sites.
Disposable MySQL validation passes 30 isolated lifecycle, rollback,
concurrency, reward, roll-fee, and start-area checks with labelled resource
cleanup; exact-SHA acceptance is recorded only when its governed environment is
available.

## 1. Target architecture

```text
Engine / runtime / game code
        |
        v
Domain services and orchestration
        |
        v
Domain DAO interfaces + persistence DTOs
        |
        v
DAO implementations and mapping
        |
        v
IDatabaseConnector / Connector / provider connections
        |
        v
MySQL / PostgreSQL / MSSQL
```

The enforced end state is:

- runtime and game code contain no SQL or table names;
- runtime and game code do not reference Dapper, `System.Data`, provider namespaces, `Connector`, generic legacy DAO types, or `DB*` entities;
- domain services call operations such as `LoadCharacter`, `ReplaceContainer`, `SaveCharacterLocation`, `TryChargeMissionRollFee`, and `SetCharacterOnlineState`;
- DAO implementations own SQL, table mappings, connection lifetime, transactions, provider dialect, null/default conversion, and implementation DTO mapping;
- database preflight, migrations, schema validators, administrative database tools, and integration-test fixtures remain explicitly separate and may use direct SQL;
- no DAO implementation references ZoneEngine, LoginEngine, ChatEngine, WebEngine, or gameplay runtime classes.

## 2. Project and namespace decision

### Recommended Phase 1 placement: reuse `AORebirth.Interfaces`

Do **not** create new projects in Phase 1. Add domain DAO contracts and neutral persistence DTOs under:

```text
AORebirth/Libraries/Source/AORebirth.Interfaces/Persistence/
  Accounts/
  Characters/
  Inventory/
  Missions/
  World/
  Social/
  Services/
```

Use namespaces such as `AORebirth.Interfaces.Persistence.Characters`.

Why this is repository-specific and lower risk:

- `AORebirth.Database` already references `AORebirth.Interfaces`.
- Core and the engines already reference `AORebirth.Interfaces`.
- The interfaces project does not reference `AORebirth.Database`.
- It avoids adding and synchronizing another legacy Windows project, Linux companion project, solution entry, publish inventory, and source-inventory guard surface before the first slice proves a need.

Add implementations under domain folders in the existing database project:

```text
AORebirth/Libraries/Source/AORebirth.Database/Domain/
  Accounts/
  Characters/
  Inventory/
  Missions/
  World/
  Social/
  Services/
```

The current `AORebirth.Database/Dao` folder remains as a compatibility implementation layer during migration. New runtime code must not consume it.

### When a new abstractions project would become justified

Only extract `AORebirth.DataAccess.Abstractions` later if one of these is proven:

- `AORebirth.Interfaces` dependencies prevent a Linux or standalone service consumer;
- DAO contracts require a release cadence independent of the broad interfaces assembly;
- project-level architecture enforcement cannot distinguish provider and domain contracts in one assembly.

If that gate is met, create both:

```text
AORebirth/Libraries/Source/AORebirth.DataAccess.Abstractions/
LinuxBuild/Projects/AORebirth.DataAccess.Abstractions.Linux.csproj
LinuxBuild/source-inventory/AORebirth.DataAccess.Abstractions.CompileItems.props
```

and update the solution, Windows projects, Linux projects, source inventory, and publish guards in one governed slice. Do not pre-create them.

## 3. Contract and DTO rules

1. DAO interfaces are domain-oriented; no `IGenericRepository<T>`, `GetAll<T>`, `Save<T>`, untyped `object whereParameters`, or table-name parameters.
2. Interfaces must not expose `IDbConnection`, `IDbTransaction`, Dapper, provider types, `DB*` entities, or SQL exceptions.
3. Persistence DTOs are sealed .NET Framework 4.8-compatible classes with explicit fields. Do not use C# record-only features unsupported by the current toolchain.
4. Reuse an existing domain value object only when it is already in a lower-level shared assembly and contains no engine/gameplay behavior. Do not make `AORebirth.Database` reference ZoneEngine to reuse a type.
5. Existing `DB*` entities remain implementation-only mapping rows.
6. DAO results distinguish not-found, empty, and failed states where current behavior distinguishes them. Inventory hydration is the critical example.
7. UTC ticks, `DateTime`, enums, binary blobs, signed/unsigned conversion, sparse stat defaults, case comparison, and ordering are explicit contract fields/tests.
8. Provider-specific error details may be logged inside implementation; domain callers receive stable result/exception semantics.

## 4. Construction and dependency injection

Use explicit constructor or initialization injection, not a heavy DI framework and not a new global service locator.

### Composition model

Each executable `Program` remains the composition root. It may reference the DAO interfaces and `AORebirth.Database` implementations. It creates one immutable per-engine dependency set and passes interfaces into runtime services:

```text
Program
  -> Connector-backed implementation factory
  -> ZonePersistence / LoginPersistence / ChatPersistence
  -> constructors or explicit Initialize(...) methods
```

Recommended factory shape in `AORebirth.Database`:

```text
DatabaseDaoFactory
  CreateMissionDao()
  CreateCharacterDao()
  CreateAccountDao()
  ...
```

The factory may use `Connector` internally. Domain code never receives the factory, connector, or a connection. Avoid a generic `Get<T>()` service locator.

For static legacy entry points such as `MissionRuntime`, use the existing explicit `Initialize` seam and require the interface argument. For systems without a seam, add a narrow one during that vertical slice; do not create a process-wide mutable DAO singleton.

AccountBrokerService and BotService hosts should construct DAO implementations at their composition roots and pass the interfaces into domain services. Provider connection construction moves below the DAO implementation boundary.

## 5. Proposed domain DAOs

The roadmap proposes **17 domain DAOs**. These boundaries are based on current operations and atomicity, not one interface per table.

### 5.1 `IAccountDao`

- Responsibility: legacy game-account authentication and account attributes.
- Consumers: LoginEngine, ChatEngine, ZoneEngine, Account Broker adapter, approved Login console administration.
- Absorbs: `LoginDataDao` and runtime account-shaped generic calls.
- Tables: `login`; ownership lookups may delegate to `ICharacterDao` rather than join implicitly.
- Operations: `LoadForAuthentication`, `LoadByUsername`, `LoadByCharacterId`, `CountRegisteredAccounts`, `CreateGameAccount`, `ChangePassword`, `SetGmLevel`, `SetExpansions`.
- Transactions: one row/command except account provisioning, which remains owned by `IAccountIdentityDao` transaction coordination.
- DTOs: `GameAccountAuthenticationData`, `GameAccountData`, `NewGameAccountData`.
- Coupling: preserve current password hash and username/case rules; characterize the current unscoped `SetGM` behavior before migration.
- Order: after mission pilot, before character creation migration.

### 5.2 `IAccountIdentityDao`

- Responsibility: unified identity, game/external mappings, provisioning jobs, email verification, password reset persistence.
- Consumers: `AccountBrokerService` only; HTTP host calls the service.
- Absorbs: SQL and ADO.NET in `AORebirth.AccountBroker/AccountBrokerService.cs`.
- Tables: six account extension tables plus transactional access to `login` and read-only `characters` account display.
- Operations: domain commands matching current transaction boundaries: identity lookup, provision/link account, reserve/confirm mapping, issue/cancel/consume tokens, load account characters, update password.
- Transactions: preserve ReadCommitted/default boundaries and row locks for tokens/linking.
- DTOs: reuse public account snapshots only if moved to neutral interfaces; otherwise map to persistence data classes.
- Coupling: hashing and policy stay in AccountBroker service; SQL timestamps/identity retrieval stay implementation-side.
- Order: accounts/services phase after legacy account DAO is stable.

### 5.3 `ICharacterDao`

- Responsibility: character identity/profile, account ownership, location, online state, buddy CSV compatibility, and atomic deletion.
- Consumers: Login, Chat, Zone, Web legacy page, Mail recipient lookup, mission account-key adapter.
- Absorbs: `CharacterDao`, `Misc.LogOffAll/LogOffCharacter`, stale-online store SQL, character lookup calls.
- Tables: `characters` plus the full owned-table set for deletion; organization/stat cleanup participates in the same implementation transaction.
- Operations: `LoadCharacter`, `LoadByName`, `ListForAccount`, `CharacterNameExists`, `BelongsToAccount`, `SaveProfileAndLocation`, `SaveLocation`, `Get/SetOnlineState`, `ListOnlineCharacters`, `RecoverStaleOnlineState`, `Add/RemoveBuddy`, `DeleteCharacterForAccount`.
- Transactions: deletion and stale recovery remain atomic exactly as audited. Online state remains separate from character save in the first migration.
- DTOs: `CharacterData`, `CharacterSummaryData`, `CharacterLocationData`, `StaleOnlineRecoveryData`.
- Coupling: must not return `DBCharacter`; preserve save-before-offline and file ownership guard ordering.
- Order: after mission pilot and account read path; high-risk phase.

### 5.4 `ICharacterStatsDao`

- Responsibility: sparse character/NPC stat persistence.
- Consumers: character hydration/save, Login/Chat character summaries, combat XP, active nanos, organizations, mission atomic rewards.
- Absorbs: `StatDao` and direct stats SQL in mission paths.
- Tables: `stats`.
- Operations: `LoadStats`, `LoadStatOrDefault`, `ReplaceStats`, `UpsertStats`, `ClearOrganizationMembership`, and transaction-scoped stat reward operations owned by mission DAO implementation.
- Transactions: preserve bulk-replace atomicity and current bulk-upsert behavior before isolation improvements.
- DTOs: `PersistedStatValue` with explicit type/instance/stat/value.
- Coupling: unsigned runtime values and signed DB values require tests; defaults are domain mapping, not SQL guessing.
- Order: character core phase.

### 5.5 `IInventoryDao`

- Responsibility: persisted container snapshots and single-item mutations for instanced/uninstanced items.
- Consumers: inventory hydration, save, delete actions, character creation loadouts.
- Absorbs: `ItemDao`, `InstancedItemDao`, and connection/transaction code in `BaseInventoryPage`.
- Tables: `items`, `instanceditems`.
- Operations: `LoadContainer`, `ReplaceContainer`, `RemoveItem`, `SaveStarterLoadout`.
- Transactions: `ReplaceContainer` must delete/reinsert both item kinds in one page transaction; aggregate page loop remains outside initially.
- DTOs: `PersistedContainerSnapshot`, `PersistedItemData`, `PersistedInstancedItemData` with binary stat payload.
- Coupling: preserve container type/instance/placement casing, stat blob decoding, identity, stack counts, and hydration failure distinction.
- Order: after character DAO read seam; highest item-loss risk.

### 5.6 `ICharacterNanoDao`

- Responsibility: uploaded and active nano persistence.
- Consumers: `Character`, upload/pet shell, ActiveNanoRuntimeService, morph runtime.
- Absorbs: `UploadedNanosDao`, `CharacterActiveNanosDao`.
- Tables: `charactersuploadednanos`, `charactersactivenanos`.
- Operations: `LoadUploadedNanos`, `AddUploadedNano`, `AddMissingUploadedNanos`, `LoadActiveNanos`, `ReplaceActiveNanos`, `DeleteExpiredActiveNanos`.
- Transactions: preserve additive uploads and currently non-atomic active replacement in the mechanical migration; add atomic replacement only as a separately approved improvement.
- DTOs: `UploadedNanoData`, `ActiveNanoData` with explicit UTC tick fields.
- Coupling: nano IDs, stack/time conversions, expiry edge cases.
- Order: character substate phase.

### 5.7 `ICharacterPerkDao`

- Responsibility: trained perk packet IDs and reset.
- Consumers: Character and PerkRuntimeService.
- Absorbs: `CharacterPerksDao`.
- Tables: `charactersperks`.
- Operations: `ValidateSchema`, `LoadTrainedPerks`, `AddTrainedPerk`, `AddMissingTrainedPerks`, `RemoveTrainedPerk`, `ResetTrainedPerks`.
- Transactions: preserve additive writes and fail-closed required-table check.
- DTOs: simple character ID + packet ID values.
- Coupling: packet ID is the proven persistence identity; do not reinterpret it.
- Order: character substate phase.

### 5.8 `IMissionDao`

- Responsibility: mission/quest lifecycle, objectives, dedupe observations, character/account flags, rewards, stat rewards, roll fees, and new-character start selection flag.
- Consumers: `PersistentMissionService`, `MissionRewardCoordinator`, `MissionRollFeeService`, MissionRuntime, start-area selection service.
- Absorbs: `MySqlMissionRepository`, mission SQL in `MissionRollFeeService`, `NewCharacterStartAreaSelectionDao`, and mission delete participation.
- Tables: all six mission tables, `stats`, ownership read from `characters`.
- Operations: the current `IMissionRepository` surface plus `TryChargeRollFee`, start-area flag operations, and an internal deletion participant for character delete.
- Transactions: preserve repository transaction callback/command semantics; reward/stat and roll-fee/stat atomicity are mandatory.
- DTOs: neutral equivalents of current mission records and keys.
- Coupling: current mission domain models live in ZoneEngine, so use mapper adapters; database implementation must not reference ZoneEngine.
- Order: recommended first production slice.

### 5.9 `IOrganizationDao`

- Responsibility: organization metadata and membership queries/mutations.
- Consumers: Zone org/city/playfield paths and Chat organization channels.
- Absorbs: `OrganizationDao`, organization-related `StatDao` operations, any proven successor to legacy `Misc.GetOrgMembers`.
- Tables: `organizations`, membership represented by `stats` stat 5; character names as needed.
- Operations: load/create/existence/government/change leader/list members/disband.
- Transactions: initially preserve current separate operations; character deletion keeps its cross-table transaction.
- DTOs: `OrganizationData`, `OrganizationMemberData`.
- Coupling: distinguish side, organization ID/name, and rank; do not resurrect `characters_stats` without proof.
- Order: social phase.

### 5.10 `IChatSocialDao`

- Responsibility: buddy CSV compatibility and recent-message history.
- Consumers: ChatEngine buddy/tell flows.
- Absorbs: buddy methods on `CharacterDao`, `ReceivedMessagesDao`.
- Tables: `characters.BuddyList`, `receivedmessages`; do not assume active `buddylist` table.
- Operations: `LoadBuddyIds`, `AddBuddy`, `RemoveBuddy`, `LoadRecentSenders`, `AddRecentSender`, `RemoveRecentSender`.
- Transactions: current read-modify-write buddy behavior is race-prone; preserve before adding optimistic concurrency.
- DTOs: IDs and `RecentMessageData`.
- Coupling: exact CSV empty/null parsing and ordering require characterization tests.
- Order: social phase.

### 5.11 `IPlayfieldDao`

- Responsibility: database-backed static dynels and proven teleport/proxy persistence only.
- Consumers: PlayfieldContentDataProvider, Nascence statue lookup, approved admin commands.
- Absorbs: `StaticDynelDao`, `TeleportDao`, any proven proxy destination path.
- Tables: `staticdynels`, `teleports`, potentially `proxydestinations` only after a live callsite is confirmed.
- Operations: `LoadStaticDynels`, `LoadTeleportDefinitions`, approved admin save operations.
- Transactions: read-only runtime; single-row administrative writes.
- DTOs: `StaticDynelData` retaining binary stats; `TeleportData`.
- Coupling: playfield metadata from files stays outside DAO; do not create operations for inactive `playfields` table.
- Order: remaining runtime persistence phase.

### 5.12 `INpcDao`

- Responsibility: legacy database NPC templates, spawn definitions, and spawn stats.
- Consumers: PlayfieldDbMobSpawnRuntimeService, NonPlayerCharacterHandler, pets, approved admin spawn commands.
- Absorbs: `MobTemplateDao`, `MobSpawnDao`, `MobSpawnStatDao`.
- Tables: `mobtemplate`, `mobspawns`, `mobspawns_stats`; add other mobspawn child tables only after an active owner is traced.
- Operations: `LoadTemplateByHash`, `SearchTemplatesByName`, `LoadSpawnsForPlayfield`, `LoadSpawnStats`, administrative insert operations.
- Transactions: reads; admin insert semantics preserved.
- DTOs: neutral NPC template/spawn/stat rows retaining arrays/blobs exactly.
- Coupling: gameplay construction, random level selection, identity mapping, and capture-backed catalogs stay above DAO.
- Order: world-content phase.

### 5.13 `IVendorCatalogDao`

- Responsibility: vendor placement/template/shop inventory catalog reads and approved admin writes.
- Consumers: VendorHandler, Vendor, VendorInventoryPage, MakeShop.
- Absorbs: `VendorDao`, `VendorTemplateDao`, `ShopInventoryTemplateDao`.
- Tables: `vendors`, `vendortemplate`, `shopinventorytemplates`.
- Operations: `LoadVendorsForPlayfield`, `LoadVendorTemplate`, `LoadShopInventoryTemplate`, approved administrative save.
- Transactions: read-only runtime.
- DTOs: vendor/template/shop-entry data.
- Coupling: QL randomization, item construction, fallback choice, and packet behavior remain domain logic.
- Order: world-content phase.

### 5.14 `IItemCatalogDao`

- Responsibility: persisted item names and trade-skill recipe catalog.
- Consumers: trade skills, vendor/captured content checks, DailyLogin, GMI, loot display, scripts/admin.
- Absorbs: `ItemNamesDao`, `TradeSkillDao`.
- Tables: `itemnames`, `tradeskill`.
- Operations: `LoadItemName`, `LoadItemNames`, `LoadTradeSkillRecipes`.
- Transactions: read-only.
- DTOs: `ItemNameData`, `TradeSkillRecipeData`.
- Coupling: item templates loaded from `items.dat` are not database rows and remain separate.
- Order: world-content phase.

### 5.15 `ILootDao`

- Responsibility: legacy database loot fallback only.
- Consumers: GlobalLootRuntimeService adapter.
- Absorbs: `MobDroptableDao` and the database portion of `MobTemplateDao.GetAll` used for loot matching.
- Tables: `mobdroptable`, required template match fields from `mobtemplate`.
- Operations: `LoadLegacyLootEntries` as one domain projection.
- Transactions: read-only snapshot.
- DTOs: `LegacyLootEntryData`.
- Coupling: capture-backed/documented code catalogs, corpse state, and selection logic remain runtime code.
- Order: remaining runtime persistence phase.

### 5.16 `IGmiVaultDao`

- Responsibility: optional GMI vault snapshot persistence.
- Consumers: GmiRuntimeService.
- Absorbs: `GmiVaultDao`.
- Tables: optional `gmi_vault`, `gmi_vault_item`.
- Operations: `IsAvailable`, `LoadVault`, `ReplaceVault`.
- Transactions: replace header/items atomically.
- DTOs: `GmiVaultData`, `GmiVaultItemData` preserving slot order.
- Coupling: missing-schema fail-closed behavior; no schema invention.
- Order: after the schema authority/evidence gap is resolved.

### 5.17 `IBotDao`

- Responsibility: bot principals, credentials, scopes, organization assignment, audit, and schema readiness.
- Consumers: bot management/auth/runtime services.
- Absorbs: `AdoNetBotRepository`; evolve existing `IPersistentBotRepository` rather than duplicate it abruptly.
- Tables: bot principal/credential/scope/audit tables and required account identity metadata.
- Operations: current domain-specific repository surface.
- Transactions: preserve ReadCommitted lifecycle transactions and audit coupling.
- DTOs: existing bot domain records may be reused only if the implementation layer can depend on their contract assembly without a cycle.
- Coupling: credential hashing/verification and authorization policy remain services.
- Order: accounts/chat/services phase.

### Intentionally not proposed

- No `IMailDao`: durable mail storage was not found.
- No generic repository.
- No DAO for packet or gameplay catalogs.
- No DAO for file-based playfield metadata/hydration.
- No DAO for every schema table with no proven consumer.

## 6. Recommended first production slice: mission persistence

### Why mission is first

Mission persistence is safer than character persistence as the pilot because it already has:

- a domain-specific `IMissionRepository` interface;
- an in-memory implementation;
- a separate `PersistentMissionService`;
- explicit transaction semantics;
- extensive identity, idempotency, rollback, reload, reward, and quest tests;
- a single production composition point in `MissionRuntime.Initialize`.

Character persistence is not the first slice because its current aggregate crosses Login/Chat/Zone, domain objects own `Read/Write`, save is deliberately multi-transaction, delete is cross-table atomic, and online state is tied to reconnect ownership. It is the highest-risk migration, not the best architecture pilot.

Bot persistence has an even cleaner seam, but it exercises service persistence rather than the core ZoneEngine/domain/DAO/provider dependency direction. It should follow after the mission pilot or in the later services phase.

### Exact current files

Production interface/service/implementation:

```text
AORebirth/Server/ZoneEngine/Core/Missions/IMissionRepository.cs
AORebirth/Server/ZoneEngine/Core/Missions/InMemoryMissionRepository.cs
AORebirth/Server/ZoneEngine/Core/Missions/MySqlMissionRepository.cs
AORebirth/Server/ZoneEngine/Core/Missions/PersistentMissionService.cs
AORebirth/Server/ZoneEngine/Core/Missions/MissionRewardCoordinator.cs
AORebirth/Server/ZoneEngine/Core/Missions/MissionRollFeeService.cs
AORebirth/Server/ZoneEngine/Core/Missions/MissionRuntime.cs
AORebirth/Libraries/Source/AORebirth.Database/Dao/NewCharacterStartAreaSelectionDao.cs
AORebirth/Server/ZoneEngine/Core/NewCharacterStartAreaSelectionRuntime.cs
AORebirth/Server/LoginEngine/Packets/CharacterName.cs
```

Tests/build:

```text
AORebirth/Libraries/Source/AOtomation/AOtomation.Messaging/src/
  SmokeLounge.AOtomation.Messaging.Tests/PersistentMissionFoundationTests.cs
  SmokeLounge.AOtomation.Messaging.Tests/QuestRuntimePersistenceTests.cs
Tools/run_aotomation_messaging_tests.cmd
Tools/run_mandatory_integration_gate.cmd
Tools/build_aorebirth_debug.cmd
LinuxBuild/Projects/ZoneEngine.Linux.csproj
LinuxBuild/source-inventory/ZoneEngine.CompileItems.props
```

### SQL paths and tables

- `MySqlMissionRepository` owns all mission state/objective/flag/account-flag/reward operations plus character ownership and atomic stat reward.
- `MissionRollFeeService` independently owns fee ledger and cash stat SQL.
- `NewCharacterStartAreaSelectionDao` owns a state flag in `missionflags`.
- `CharacterDao.DeleteOwnedData` deletes mission-owned rows inside the character-deletion transaction; this deletion participant remains in place for the first slice or is invoked through an internal transaction participant without splitting the outer transaction.

Tables:

```text
missionstates
missionobjectiveprogress
missionobjectiveobservations
missionflags
missionaccountflags
missionrewardledger
stats
characters
```

### Expected contracts and implementations

Add neutral contracts in `AORebirth.Interfaces/Persistence/Missions`:

```text
IMissionDao
IMissionDaoTransaction
MissionStateData
MissionObjectiveProgressData
MissionObjectiveObservationData
MissionFlagData
MissionAccountFlagData
MissionRewardStageData
MissionCharacterSnapshotData
MissionStatMutationData
MissionRollFeeRequest / MissionRollFeeResult
```

Add implementation/mapping under:

```text
AORebirth.Database/Domain/Missions/MySqlMissionDao.cs
AORebirth.Database/Domain/Missions/MissionDataMapper.cs
```

Add a Zone adapter:

```text
ZoneEngine/Core/Missions/MissionDaoRepositoryAdapter.cs
```

The adapter implements the current `IMissionRepository` while mapping mission domain records to neutral DAO data. This avoids changing `PersistentMissionService` and every quest in the first slice. After acceptance, the duplicate repository interface can be evolved or retired in a later focused change.

### Migration steps

1. Freeze exact current SQL and transaction behavior with MySQL integration tests for every operation used by the service.
2. Add neutral DAO data classes/interfaces to `AORebirth.Interfaces`; update Windows and Linux compile inventories through the documented source-inventory workflow.
3. Implement `MySqlMissionDao` in `AORebirth.Database` by moving SQL mechanically, preserving statement text, order, isolation, locks, row counts, exception behavior, null/default mapping, and ticks.
4. Add `MissionDaoRepositoryAdapter` in ZoneEngine and run existing in-memory service tests against it using an in-memory/fake `IMissionDao` test double.
5. Move roll-fee SQL into one DAO transaction operation; leave fee calculation and response behavior in `MissionRollFeeService`.
6. Move start-area flag persistence into mission DAO operations; retain Login/Zone orchestration and state strings.
7. Change `MissionRuntime.Initialize` default composition to receive the DAO from the engine composition root. Keep the existing injectable overload for tests.
8. Remove Dapper, `System.Data`, `Connector`, and SQL references from mission runtime files.
9. Keep character-deletion SQL/transaction participation unchanged until the character slice; document this temporary cross-owner dependency in the guard allowlist.
10. Run focused mission tests, new MySQL DAO integration tests, the full AOtomation suite, Windows build/acceptance, and Linux acceptance on the exact SHA.

### Existing tests to reuse

- all `PersistentMissionFoundationTests`;
- all `QuestRuntimePersistenceTests`;
- mission filters in `run_mandatory_integration_gate.cmd`;
- Stage6 MySQL integration infrastructure pattern;
- Windows build wrapper and exact-SHA acceptance;
- Linux source inventory, publish, offline smoke, and exact-SHA acceptance.

### Missing tests required before cutover

1. MySQL DAO round-trip for every persisted mission record and nullable field.
2. Transaction rollback after each mutation stage.
3. duplicate observation concurrency and unique constraint behavior.
4. reward claim lease concurrency and stale-token rejection.
5. atomic stat reward + ledger rollback and idempotent retry.
6. roll-fee double-submit concurrency; exact cash and ledger result.
7. account-key ownership mismatch under row lock.
8. start-area pending/complete conditional update parity.
9. UTC tick extremes and string length boundary parity.
10. source/dependency guard proving mission runtime contains no SQL/provider references.

### Rollback strategy

- Keep the old `MySqlMissionRepository` source available behind a single composition switch only during the slice; default remains legacy until all acceptance gates pass.
- Do not dual-write. A shadow read may compare normalized results, but production writes must have one owner.
- Rollback is a composition revert to the legacy repository at the same schema; no schema rollback is required.
- Remove the temporary switch and legacy implementation only in a later cleanup commit after live acceptance.

### Acceptance criteria

```text
MISSION_SQL_IN_ZONE_RUNTIME=0
MISSION_PROVIDER_API_IN_ZONE_RUNTIME=0
MISSION_SERVICE_TESTS=PASS
MISSION_DAO_MYSQL_INTEGRATION=PASS
ROLLBACK_AND_CONCURRENCY_TESTS=PASS
DATABASE_SCHEMA_CHANGED=NO
PACKET_BEHAVIOR_CHANGED=NO
GAMEPLAY_BEHAVIOR_CHANGED=NO
WINDOWS_BUILD=PASS
WINDOWS_ACCEPTANCE=PASS
LINUX_ACCEPTANCE=PASS
```

## 7. Phased migration

### Phase 0 — Baseline and inventory lock

- Goal: freeze the factual audit and prevent scope drift.
- Exact scope: the 7 direct SQL files, legacy DAO map, consumer list, transaction matrix, provider syntax inventory, schema/unknown ledger.
- Files/subsystems: both DAO documents; no production edits.
- Dependencies: clean tracked source and authoritative schema/preflight lists.
- Changes expected: documentation only; optionally add a machine-readable legacy exception manifest in Phase 1.
- Tests required: `git diff --check`; source-search reconciliation.
- Acceptance gate: every known site has category/owner/risk and direct SQL count is reproducible.
- Rollback boundary: delete/revert documentation only.
- Risks: false positives or missed dynamically-built SQL.
- Must not change: code, schema, startup, packets, gameplay, connectors.

### Phase 1 — DAO infrastructure and guard in audit mode

- Goal: create the contract/implementation/construction conventions needed by Phase 2 without changing runtime behavior.
- Exact scope: persistence folders/namespaces in `AORebirth.Interfaces` and `AORebirth.Database`; non-generic factory; architecture guard; tests for guard rules. Add only contracts needed by the first mission slice, not all 17 interfaces as empty placeholders.
- Files/subsystems: `AORebirth.Interfaces.csproj`, `AORebirth.Database.csproj`, relevant Linux project/source inventory, `Tools/DaoArchitectureGuard`, wrapper, mandatory integration gate wiring.
- Dependencies: Phase 0 site manifest and current build workflow.
- Changes expected: compile-only contracts/factory conventions; guard reports current exceptions but fails on new violations.
- Tests required: guard positive/negative fixture tests, Windows debug build, Linux compile/source-inventory validation.
- Acceptance gate: no runtime construction switched; guard finds exactly the baseline exceptions and rejects a test violation in production paths while permitting Tools/Tests/Migrations.
- Rollback boundary: remove new contracts/tool and project includes; no data/schema effect.
- Risks: fragile path matching or false SQL-string detection.
- Must not change: runtime DAO calls, SQL text, provider selection, schema, startup, packets, gameplay.

Phase 1 implementation checklist:

```text
[x] Add only mission DAO contracts/DTOs under AORebirth.Interfaces/Persistence/Missions
[x] Add DatabaseDaoFactory mission creation method
[x] Add deterministic path-based DaoArchitectureGuard
[x] Baseline known violations in a reviewed manifest with owner and target phase
[x] Add guard fixture tests for runtime, Database, Tools, Tests, Migrations
[x] Wire guard to run_mandatory_integration_gate.cmd after it is stable
[x] Update Windows/Linux compile inventories through approved workflows
[x] Prove zero intended runtime behavior changes through contract/regression tests
```

### Phase 2 — First vertical slice: missions

Implementation status (2026-09-01): code-complete with disposable MySQL
validation passing; governed exact-SHA acceptance remains environment-dependent.

- Goal: move mission SQL below DAO interfaces while preserving all behavior and transaction semantics.
- Exact scope: files, tables, adapters, tests, construction, and rollback listed in section 6.
- Files/subsystems: mission runtime/service/repository, DAO contracts/implementation, start-area flag, build inventories.
- Dependencies: Phase 1 contracts/factory/guard.
- Changes expected: SQL moves to `AORebirth.Database`; runtime consumes adapter/interface.
- Tests required: existing mission suites plus new MySQL round-trip, rollback, idempotency, concurrency, and provider-dialect classification tests.
- Acceptance gate: section 6 criteria and exact-SHA Windows/Linux acceptance.
- Rollback boundary: composition revert, same schema, no dual-write.
- Risks: transaction/order/mapping drift, mission DTO divergence.
- Must not change: mission definitions, rewards, packet bodies, gameplay progression, schema, startup behavior.

Phase 2 implementation checklist:

```text
[x] Add IMissionDao and neutral mission persistence DTOs
[x] Move mission repository, roll-fee, and start-area SQL into MySqlMissionDao
[x] Add MissionDaoRepositoryAdapter and explicit composition-root construction
[x] Remove SQL/provider dependencies from mission runtime files
[x] Preserve character-deletion mission cleanup for the later character slice
[x] Add adapter, architecture, guard, provider, rollback, idempotency, and concurrency tests
[x] Update authoritative Windows and guarded Linux compile inventories
[x] Run disposable MySQL validation: 30 checks, rollback/concurrency PASS, zero residue
[ ] Record governed exact-SHA Windows and Linux acceptance
```

### Phase 3 — Character/account read and online-state seams

- Goal: establish shared character/account DAOs before moving aggregate saves.
- Exact scope: authentication loads, character lists/lookups/ownership, online state, stale recovery, Web read-only counts/list.
- Files/subsystems: Login handlers/queries, Chat auth/directory, Zone stale recovery and handoff, Web IndexPHP, `CharacterDao` read/online methods.
- Dependencies: proven construction model and cross-engine DTOs.
- Changes expected: engines receive `IAccountDao`/`ICharacterDao`; SQL/DB entities disappear from read paths; stale recovery becomes one DAO operation.
- Tests required: correct/wrong login, account ownership, list ordering/default stats, online handoff/reconnect, serializable stale recovery, concurrent state tests.
- Acceptance gate: exact packet/login results and reconnect suite unchanged; no engine runtime `DBCharacter` on migrated paths.
- Rollback boundary: per-engine composition revert; same schema.
- Risks: case sensitivity, null/default values, online races, startup cleanup.
- Must not change: password hash, login protocol, online ownership order, startup readiness behavior.

### Phase 4 — Character aggregate, inventory, stats, nanos, perks

- Goal: remove persistence from `Character`, stats, and inventory domain objects.
- Exact scope: `IDatabaseObject` implementations, character hydration/save/logout/delete, inventory containers, stats, uploads, active nanos, perks, character creation persistence calls.
- Files/subsystems: AORebirth.Core Character/Inventory, AORebirth.Stats, Login CharacterName/loadouts, Zone inventory/nano/perk services, legacy DAOs.
- Dependencies: Phase 3 character/account interfaces and exhaustive snapshot tests.
- Changes expected: domain services orchestrate DAO calls; `IInventoryDao.ReplaceContainer`; character deletion remains one DAO transaction; Read/Write methods become domain serialization/hydration methods or are retired.
- Tests required: empty vs failed hydration, binary instanced items, multi-page saves, delete rollback at every child table, save-before-offline, starter character snapshots, null/default/stat/enum conversions.
- Acceptance gate: no item/stat/nano/perk loss across restart/reconnect; deletion all-or-nothing; runtime Core no longer references Database/Dapper/System.Data for migrated code.
- Rollback boundary: one vertical sub-slice at a time (inventory, stats, nanos, perks), same schema.
- Risks: **highest overall**—item loss/duplication, partial save, reconnect zombie state, transaction expansion.
- Must not change: current multi-transaction save/create semantics unless separately approved; inventory hydration safety; packet/gameplay behavior.

### Phase 5 — Organizations and chat/social

- Goal: migrate social persistence without confusing organization identity/rank/side.
- Exact scope: organizations, member stat queries, buddy CSV, recent messages, Chat directory consumers.
- Files/subsystems: OrgClient, Chat channels/client/buddy/tell flows, organization and received-message DAOs.
- Dependencies: character/stats DAO.
- Changes expected: `IOrganizationDao`/`IChatSocialDao`; DB entities removed from Chat/Zone handlers.
- Tests required: org create/disband/leader, member queries, buddy CSV null/empty/order/concurrency, recent-message retention, chat login/list behavior.
- Acceptance gate: cross-engine org/chat behavior matches; no use of legacy `characters_stats` or active `buddylist` assumptions.
- Rollback boundary: social operations independently switchable.
- Risks: current non-atomic org operations, CSV lost updates, null org rows.
- Must not change: chat protocol, organization semantics, schema.

### Phase 6 — Accounts, Account Broker, and BotService

- Goal: move service SQL below DAO interfaces while retaining security and transaction guarantees.
- Exact scope: Account Broker SQL/factory, bot ADO repository/factory, legacy account administrative methods.
- Files/subsystems: AccountBroker library/host, BotService library/host, Database implementations, validation tools.
- Dependencies: `IAccountDao`, proven DAO factory, account/bot integration infrastructure.
- Changes expected: service code becomes SQL/provider-free; existing repository contracts evolve through adapters.
- Tests required: full AccountBrokerValidation, UnifiedAccountFlowValidation, BotSchemaValidation, concurrency/rollback/security tests, LoginEngine credential acceptance.
- Acceptance gate: same password/token/credential/audit results; SQL only in implementation/tool/test projects.
- Rollback boundary: service composition revert; no schema migration in the refactor slice.
- Risks: auth outage, token replay, identity mapping duplication, credential exposure.
- Must not change: password hash, public routes/protocol, token semantics, bot authorization, schema.

### Phase 7 — World content, NPCs, vendors, item catalogs, loot, GMI

- Goal: remove table-shaped data access from remaining Zone/Core runtime.
- Exact scope: static dynels/teleports, legacy NPC spawns/templates/stats, vendors/shops, item names/tradeskills, legacy DB loot, optional GMI.
- Files/subsystems: PlayfieldContentDataProvider, PlayfieldDbMobSpawnRuntimeService, NPC/pet code, vendor/item/loot/GMI services and DAOs.
- Dependencies: proven read-only DAO patterns; GMI schema authority resolved before GMI cutover.
- Changes expected: neutral DTOs and domain adapters; runtime catalogs remain gameplay owners.
- Tests required: deterministic row-to-domain mapping, binary arrays/blobs, missing-template behavior, QL range behavior, content precedence, GMI unavailable/replace rollback.
- Acceptance gate: same world population/vendor/loot/GMI behavior; file and capture-backed catalogs remain separate.
- Rollback boundary: one content domain at a time.
- Risks: activation of dormant content, randomization drift, binary mapping, optional schema behavior.
- Must not change: spawn activation, packet/gameplay content, playfield file hydration, capture-backed loot, schema.

### Phase 8 — Architectural enforcement in fail mode

- Goal: make reintroduction of runtime SQL/provider coupling a build failure.
- Exact scope: remove resolved baseline exceptions; enable guard in mandatory Windows and Linux gates; project-reference boundary checks.
- Files/subsystems: guard/tool wrapper, integration gate, Linux acceptance/source inventory, project files.
- Dependencies: all planned runtime sites migrated or explicitly deferred with approved owner/date.
- Changes expected: fail closed on new production violations.
- Tests required: guard fixture suite, full mandatory integration, Windows exact-SHA acceptance, Linux acceptance.
- Acceptance gate: production runtime allowed list is empty except composition roots and explicitly separated readiness components; tools/tests/migrations still pass.
- Rollback boundary: revert guard wiring only; do not restore migrated runtime SQL.
- Risks: false positives blocking delivery or overbroad allowlists weakening enforcement.
- Must not change: runtime behavior or schema.

### Phase 9 — Legacy cleanup

- Goal: remove compatibility APIs only after all consumers and rollback windows are closed.
- Exact scope: Core/engine project references to Database, `IDatabaseObject`, generic `IDao<T>`, `Dao<T,TU>` public surface, Dao singletons, unused DB entities/DAOs, temporary adapters/switches.
- Files/subsystems: project references, Interfaces, Database legacy folders, source inventories, docs.
- Dependencies: Phase 8 clean guard and accepted live operation.
- Changes expected: provider/data implementation remains; table mapper may stay internal if still useful.
- Tests required: full Windows/Linux acceptance and dependency graph test.
- Acceptance gate: Engine/Core projects reference interfaces, not database implementation; no temporary rollback switches; no unsupported deletion.
- Rollback boundary: cleanup commits separated by component; migrations already accepted.
- Risks: hidden reflection/config consumers and legacy admin tools.
- Must not change: schema or behavior; do not remove inactive artifacts without exact consumer proof.

## 8. Architectural enforcement design

### Recommended mechanism: deterministic repository source/dependency guard

Add a small repository-owned guard tool rather than a Roslyn analyzer or raw CI grep.

Why:

- the repository already uses source-reading contract tests and command wrappers;
- projects are legacy .NET Framework 4.8 with explicit Linux source inventories;
- adding Roslyn packages/analyzers to every legacy and Linux project is higher-risk;
- raw grep confuses gameplay words such as `Delete`/`Update`, comments, tests, and migrations;
- a repository tool can use explicit path categories and a reviewed migration baseline.

The guard should:

1. Define production roots and explicit exemptions (`AORebirth.Database`, Tools, test projects, migrations, schema SQL).
2. Reject production `using`/references for Dapper, `System.Data`, MySqlConnector, Npgsql, SqlClient, and `AORebirth.Database.Dao` outside executable composition/readiness files.
3. Reject provider/connection symbols: `IDbConnection`, `IDbCommand`, `DbConnection`, `DbCommand`, provider connections/commands, `Connector.GetConnection`, `ExecuteReader`, `ExecuteNonQuery`, `ExecuteScalar`.
4. Lex C# comments and string literals sufficiently to detect SQL command strings using SQL structure (verb plus SQL clauses), not a bare word match.
5. Reject known table names in production string literals outside allowed readiness/tooling paths.
6. Inspect project references so Core/domain assemblies cannot regain a reference to `AORebirth.Database` after removal.
7. Read a tracked exception manifest containing exact path, category, owner DAO, and target phase. Any unlisted violation fails; exception count may only decrease without explicit review.
8. Emit compact machine-readable totals for Windows/Linux gates.

Suggested outputs:

```text
DAO_ARCHITECTURE_GUARD=PASS|FAIL
PRODUCTION_SQL_SITES=<count>
PROVIDER_API_SITES=<count>
LEGACY_BASELINE_EXCEPTIONS=<count>
NEW_VIOLATIONS=<count>
```

Run order:

```text
focused guard fixture tests
-> Tools/run_dao_architecture_guard.cmd
-> Tools/run_mandatory_integration_gate.cmd
-> Tools/accept_windows_source.cmd
-> Linux exact-SHA acceptance
```

Do not allow source comments such as `DAO-GUARD-IGNORE` to suppress findings. Exemptions belong only in the reviewed manifest.

## 9. Testing strategy

### DAO unit tests

- mapping between DB rows and neutral persistence DTOs;
- null/default/enum/signed-unsigned/date/tick/binary conversions;
- ordering and not-found/empty/error results;
- factory/composition and provider dialect selection;
- no real database.

### DAO integration tests

- run against governed disposable MySQL fixtures, never production;
- exact current schema, constraints, indexes, identity behavior, locks, isolation, and rollback;
- one suite per DAO vertical slice;
- transaction failure injection after each statement boundary;
- concurrency for idempotency, online state, inventory, rewards, account tokens, and credential rotation.

MSSQL/PostgreSQL integration suites are required before declaring a migrated DAO supported on those providers. Until then, fail with an explicit unsupported-dialect result rather than running MySQL SQL.

### Runtime regression tests

- existing AOtomation mission, quest, login hydration, reconnect, inventory, world population, vendor, loot, and subsystem tests;
- packet and gameplay assertions stay above DAO tests;
- adapters are tested with in-memory/fake DAOs.

### Schema compatibility tests

- existing DatabasePreflight contract remains authoritative for the 34 core tables;
- account/bot schema validators remain separate;
- DAO integration tests validate columns/indexes they rely on without auto-migrating;
- no DAO creates or repairs schema at runtime.

### Windows acceptance

Use documented wrappers only:

```cmd
cmd /d /c tools\run_aotomation_messaging_tests.cmd
cmd /d /c tools\build_aorebirth_debug.cmd
cmd /d /c Tools\accept_windows_source.cmd --expected-sha <sha>
```

Add `--mandatory-gate` when the full integration gate is required for the acceptance event.

### Linux acceptance

- update companion project/source inventories through the governed workflow;
- build from the exact Windows-accepted SHA in the controlled Linux acceptance workspace;
- require source SHA, clean source, restore, build, tests, publish, and offline smoke PASS;
- no Linux-only DAO behavior or implementation.

## 10. High-risk behavior ledger

| Risk | Required control |
| --- | --- |
| Transaction boundary drift | Statement/order/isolation snapshot tests and failure injection before cutover. |
| Connection/reader lifetime | DAO owns and disposes all; buffered results returned, no live readers escape. |
| Null/default changes | Character/stat/account/catalog mapping tables and exact fixtures. |
| Enum/signed conversion | Boundary tests for AO identities, stats, flags, qualities, and provider numeric types. |
| UTC/local time | Preserve current ticks and SQL timestamp behavior; test kind/precision explicitly. |
| Binary data | Round-trip instanced item stats and NPC/static-dynel blobs byte-for-byte. |
| Identity/auto-increment | Test MySQL identity retrieval and rollback; do not infer cross-provider parity. |
| Case sensitivity | Account/character/item/org lookup tests against current collation and normalization. |
| Provider-specific SQL | Explicit dialect implementations or fail closed; no silent fallback. |
| Multi-statement operations | One DAO command for character delete, mission rewards/fees, GMI replace, account provisioning, bot lifecycle. |
| Locking/concurrency | Real MySQL tests for `FOR UPDATE`, duplicate observations, tokens, online state, buddy updates, rewards. |
| Character online state | Preserve login/zone/chat ordering and `CharacterOnlineOwnershipGuard`. |
| Inventory consistency | Preserve hydration trust gate and per-page transaction boundary; byte/slot snapshots. |
| Login/authentication | Full correct/wrong password and character ownership acceptance; unchanged hash. |
| Startup preflight | Keep readiness/tooling separate and behavior-identical; do not force it through gameplay DAOs. |
| Schema expectations | Preflight/validators remain authoritative; no runtime schema creation in DAO work. |

## 11. Completion and rollback policy

Every vertical slice must be independently reversible without a schema rollback. Prefer composition rollback at the same schema. Do not dual-write unless a separate design proves reconciliation and failure semantics. Shadow reads, if used, must be read-only and compare normalized DTOs without affecting runtime selection.

Do not remove legacy DAO code in the same commit that first activates its replacement. Activation, acceptance, and cleanup are separate rollback boundaries.

## 12. Terminal summary

```text
DAO_REFACTOR_AUDIT=COMPLETE
DIRECT_RUNTIME_SQL_SITES=5
PROPOSED_DAOS=17
RECOMMENDED_FIRST_SLICE=MISSION_PERSISTENCE
PHASE_COUNT=10
HIGHEST_RISK_AREA=CHARACTER_AGGREGATE_TRANSACTIONS
MISSION_RUNTIME_DIRECT_SQL_SITES=0
IMPLEMENTATION_STARTED=YES
```
