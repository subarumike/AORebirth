# Character read/online DAO parallel foundation handoff

This is a validated persistence-foundation handoff, not permission to integrate runtime consumers. The character suite passes529 assertions twice on frozen code, with disposable cleanup PASS; account273 and mission202 regressions pass. Reproduced broader baseline failures remain explicit. Provenance resolves to the commit containing this report.

## 1. Provenance and isolated scope

- START_SHA: `e3acc4c58132809fd67bd2fe8aa58939109fe0dc`.
- Source branch: `codex/account-dao-parallel-foundation`.
- Work branch: `codex/character-read-online-dao-parallel-foundation`.
- Isolated worktree: `C:\Users\Mike\Documents\AORebirth\tools-temp\worktree-snapshots\character-dao-parallel-foundation`.
- Read-only baseline audit worktree: `C:\Users\Mike\Documents\AORebirth\tools-temp\worktree-snapshots\account-dao-parallel-foundation`.
- END_SHA / committed-tree identity: `RESOLVE_COMMIT_CONTAINING_THIS_REPORT`. A commit cannot embed its own hash; the final completion message supplies it. Resolve with `git log -1 --format=%H -- docs/reports/CHARACTER_DAO_PARALLEL_HANDOFF.md` after commit.

The pre-creation inventory is `build-verify/character-parallel/worktree-inventory.txt`. It records the primary `master` at `cf1e12b894b1247b34f96f832b217c1cfb828213`, the clean account foundation at the exact start SHA, and the pre-existing worktrees without mutating them. Initial primary status contained only the unrelated untracked `quest example from PRK.txt`; it was preserved. Both newly created character worktrees were clean before task edits:

- Feature worktree: the isolated path above, branch `codex/character-read-online-dao-parallel-foundation`.
- Clean detached comparison worktree: `C:\Users\Mike\Documents\AORebirth\tools-temp\worktree-snapshots\character-dao-parallel-base`, detached at the exact START_SHA.

The pre-creation inventory intentionally does not list these later additions. The full post-creation registry is recorded in build-verify/character-parallel/worktree-inventory-final.txt. Final source/worktree provenance, exact changed-file hashes, protected-path proof, all named assertions and governed command records are carried by the [machine-readable acceptance evidence](../../Tools/CharacterDaoValidation/acceptance-evidence.json). Existing unrelated worktrees, including stale/prunable registry entries, were not removed, pruned, reset or cleaned.

The primary worktree is not an implementation target. Other agents' new character source/test/guard files in this isolated worktree are shared task work, not material to overwrite or stash. No reset, clean, rebase, merge, live deployment, application-database operation or runtime wiring is part of this work.

## 2. Exact ICharacterDao contract

Namespace: `AORebirth.Interfaces.Persistence.Characters`. Public surface is exactly eight named methods:

```csharp
public interface ICharacterDao
{
    CharacterDirectoryData LoadById(int characterId);
    CharacterDirectoryData LoadByName(string name);
    IList<CharacterDirectoryData> ListForAccount(string accountUsername);
    bool IsOwnedByAccount(string accountUsername, uint characterId);
    int MarkOnline(int characterId);
    int MarkOffline(int characterId);
    IList<CharacterDirectoryData> ListLoggedIn();
    StaleOnlineRecoveryData RecoverStaleOnline(string expectedDatabase);
}
```

`LoadById` also supplies a nullable online-state projection and name-by-ID lookup. `LoadByName(...) != null` supplies name existence; it is not a duplicate-count test. No redundant helper-per-method, generic query, CRUD, Save, Create, Delete, location, stats or social API is introduced.

## 3. Exact neutral DTOs and results

All four contract files are under `AORebirth/Libraries/Source/AORebirth.Interfaces/Persistence/Characters/`.

```csharp
public sealed class CharacterDirectoryData
{
    public int CharacterId { get; set; }
    public string AccountUsername { get; set; }
    public string Name { get; set; }
    public string FirstName { get; set; }
    public string LastName { get; set; }
    public int Playfield { get; set; }
    public int? Online { get; set; }
}

public sealed class StaleOnlineCharacterData
{
    public StaleOnlineCharacterData(int characterId, int previousOnline);
    public int CharacterId { get; private set; }
    public int PreviousOnline { get; private set; }
}

public sealed class StaleOnlineRecoveryData
{
    public StaleOnlineRecoveryData(
        string databaseName, IEnumerable<StaleOnlineCharacterData> rows,
        int rowsUpdated, long? postUpdateNonzeroCount);
    public string DatabaseName { get; private set; }
    public IReadOnlyList<StaleOnlineCharacterData> Rows { get; private set; }
    public int RowsUpdated { get; private set; }
    public long? PostUpdateNonzeroCount { get; private set; }
    public bool CleanupRequired { get; } // Rows.Count != 0
}
```

The result constructor rejects null rows and copies them into a read-only collection; row results have no public setters. `PostUpdateNonzeroCount == null` records that the no-cleanup path did not perform a post-update count. The signatures above abbreviate constructor/accessor bodies, not the exposed members.

Field justification is consumer-backed: Chat `CharacterBase.ReadNames` and `LoginCharacter.Read` need Name/FirstName/LastName; Login `CharacterList.LoadCharacters` needs ID/Name/Playfield; LFT needs Playfield; ClientConnected and legacy account lookup need AccountUsername; ownership and online-state callers need identity and raw nullable Online. No coordinates, heading, textures, BuddyList, organization data, inventory or stat fields are carried.

## 4. MySqlCharacterDao implementation files

Implementation: `AORebirth/Libraries/Source/AORebirth.Database/Domain/Characters/MySqlCharacterDao.cs`.

The sealed implementation owns explicit parameterized SQL, private stale-row mapping and private transaction-outcome/resource helpers. It neither returns nor depends on `DBCharacter`, engine assemblies or gameplay types. Constructors store a connection factory and perform no work. Every operation obtains one fresh returned connection and disposes it; reads are buffered before return. Online writes own one default-isolation transaction; recovery owns one Serializable transaction. There is no DAO-to-DAO call, runtime service locator or automatic recovery.

The public injected `Func<IDbConnection>` constructor follows the existing deterministic-test seam: it trusts the supplied factory to return a fresh MySQL-capable connection or test decorator. This is not a promise of arbitrary-provider support. The default production connection path enforces MySQL before character SQL.

## 5. DatabaseDaoFactory addition

The factory addition is:

```csharp
public static ICharacterDao CreateCharacterDao()
{
    return new MySqlCharacterDao();
}
```

The account and mission factory methods remain. Factory construction opens no connection, runs no SQL, initializes no engine and registers nothing in an engine dependency container. Compile-only source links in isolated account/mission validation projects may include the new contract/implementation solely because their tests compile the actual extended factory; no existing assertions may be removed to manufacture a pass.

Exact factory/source/project changes and SHA256 values are enumerated in the [acceptance evidence](../../Tools/CharacterDaoValidation/acceptance-evidence.json). Authoritative compile entries are added only to Interfaces and Database; their governed Linux compile-item inventories were written and checked successfully.

## 6. Represented table and columns

The existing `characters` table is the only character DAO table.

| Existing column | Neutral field | Scope |
|---|---|---|
| Id | CharacterId | Identity/predicates/captured IDs |
| Username | AccountUsername | Account directory and ownership |
| Name | Name | Name lookup/display |
| FirstName | FirstName | Existing Chat name projection |
| LastName | LastName | Existing Chat name projection |
| Playfield | Playfield | Read-only directory field |
| Online | Online / PreviousOnline | Raw nullable state and explicit0/1 writes |

`SELECT DATABASE()` identifies the connected database for recovery; it does not introduce a schema object. No schema definition, migration, constraint, column type or application database is changed. Canonical schema permits duplicate Name and nullable Online. Existing account-identity/broker schemas are not game-account or character authority in this slice.

## 7. Query, collation, null, order and affected-row semantics

- ID lookup uses the identity predicate and a single-or-default result; absence is null.
- Name lookup uses unchanged input and the existing MySQL column equality; duplicate names are legal, and the first matching row is unspecified. No `ORDER BY`, normalization, trimming or added uniqueness policy is introduced.
- Account listing matches unchanged Username and returns a buffered possibly empty list. Ordinary lists have no universal ordering guarantee.
- Ownership requires exactly one matching ID+account row. The supplied account identity is not authenticated by the DAO.
- Directory reads preserve null/empty names and nullable Online. MySQL collation determines case and trailing-space equality. A caller's pre-existing trim or whitespace check remains caller behavior, not DAO behavior.
- Online writes update only Online for the specified ID, use1/0 respectively and return the raw provider affected-row count. Missing row returns0; same-value updates vary with `UseAffectedRows`. Exact caller parity may require intentionally discarding the count because legacy setters return void.
- Failures propagate. No provider/mapping failure becomes null, empty collection or offline.
- Deliberate projection difference: legacy generic reads materialize `SELECT *` into the aggregate. The new seven-column read does not map omitted position, heading, texture or buddy columns. It may therefore succeed when an omitted aggregate column would have failed legacy mapping. Do not promise identical aggregate readiness or failure coverage.
- Legacy `DBCharacter.Online` is nonnullable int and materialized SQL NULL becomes its default0; the new DTO preserves NULL. Existing `IsOnline` callers can explicitly use `row == null ? 0 : row.Online ?? 0` outside the DAO.
- `GetCharacterNameById` compatibility is `row == null ? string.Empty : row.Name ?? string.Empty`, not a persistence-layer empty-string fallback.

## 8. Online-state behavior

A missing row, found Online=NULL, found0, found1 and other signed nonzero values remain distinguishable through `LoadById`. Only `ListLoggedIn` filters exactly1. Stale recovery captures every non-null nonzero value, including negative or nonstandard values, without assigning gameplay meaning.

Existing callers disagree intentionally: Login's stale-presence branch compares exactly1, organization kick tests0, Chat wire paths cast integers to uint, and some callers catch failures while others propagate. Section16 records each case. A Boolean replacement would lose these semantics.

The existing ownership guard retains its process dictionary, reference counts, file byte locks,5s acquisition limit and25ms retry policy. The DAO does not acquire/release leases, decide handoff ownership, inspect processes or ports, sleep, or log users out.

## 9. Complete stale-recovery transaction contract

Future replacement target is only the database portion of `AORebirth/Server/ZoneEngine/StaleOnlineRecovery.cs`:

| Legacy store stage | New operation responsibility |
|---|---|
| AdoNetStaleOnlineRecoveryStore constructor | Fresh connection; begin Serializable |
| DatabaseName and runtime expected-database check | SELECT DATABASE(); exact Ordinal comparison |
| ReadNonzeroRows | Capture Id/Online IS NOT NULL AND <>0, ORDER BY Id FOR UPDATE |
| ClearRows | Update only captured IDs still non-null/nonzero |
| CountNonzeroRows | Verify no nonzero rows remain |
| Commit | Only after exact affected count and zero post-count |
| Uncommitted Dispose rollback | Explicit rollback plus owned resource disposal |

`RecoverStaleOnline(expectedDatabase)` performs these stages on one owned connection/transaction. Null/empty/whitespace-only expectedDatabase is rejected before connection acquisition. A nonempty mismatch, including case mismatch or added whitespace, fails after database identification but before capture/mutation; inputs are not rewritten.

For a nonempty capture, changed rows must equal the captured count and the verification count must be0 before commit. The returned ascending IDs/prior values, actual database name, affected count,0 post-count and CleanupRequired=true are detached audit data.

For an empty capture, no UPDATE, post-count or COMMIT occurs. The new implementation explicitly rolls back this read-only transaction, returning an empty capture, RowsUpdated=0, PostUpdateNonzeroCount=null and CleanupRequired=false. Legacy reached the corresponding rollback indirectly through disposal.

Every pre-commit failure triggers rollback; every returned resource is disposed. Concurrent writers and provider locking semantics must be demonstrated by disposable tests, not asserted from SQL text alone. A verified0 is a fact at verification time, not a guarantee that another runtime writer cannot subsequently mark a row online.

Process checks, exclusive process/port locks, listener detection/reservation, command-line handling, console/audit formatting and startup safety checks remain in ZoneEngine. Never call recovery without the runtime's exclusive-safety preconditions.

Stale store/runtime wrapper mapping is separate from the53 direct CharacterDao invocations: it performs direct SQL rather than calling CharacterDao.

## 10. Commit, rollback and cleanup uncertainty

A thrown commit exception does not prove rollback or unchanged data. Test both failure before durable commit and lost acknowledgement after a real commit; reconcile with a new connection before deciding on a retry. Do not automatically repeat an operational online write or recovery merely because its acknowledgement was lost.

The new implementation preserves the primary exception and records secondary failures in `Exception.Data`:

- `CharacterDao.RollbackFailure`
- `CharacterDao.TransactionDisposeFailure`
- `CharacterDao.ConnectionDisposeFailure`

A cleanup failure with no earlier primary failure itself propagates. Legacy generic online writes can replace the primary error with rollback/disposal failure; legacy stale-store Dispose suppressed rollback failure. The new behavior is a deliberate diagnostic-contract improvement requiring future integration acceptance. It does not modify legacy files or their current runtime diagnostics.

## 11. Provider and connection limitations

MySQL is the only demonstrated provider. Default construction rejects and disposes a non-MySQL returned provider connection before character SQL. Shared `Connector.GetConnection()` may already open that connection before returning it; an error before it returns is shared infrastructure ownership, not a resource the DAO can dispose. This work does not fix or claim to fix an upstream Connector open-failure leak.

The injected connection factory is a trusted test seam for MySQL/decorators; it is not cross-provider compatibility. No MSSQL/PostgreSQL parity is claimed. Ordinary writes preserve legacy explicit default-isolation transactions rather than silently becoming autocommit writes.

## 12. Complete test assertion inventory

The frozen final candidate executes **529 named harness assertions**. The full ordered assertion labels, category counts, source hashes, result markers and final raw-log hashes are recorded in the [machine-readable acceptance evidence](../../Tools/CharacterDaoValidation/acceptance-evidence.json); this is the exact assertion inventory, not an inferred count from source grep.

| Assertion category | Count |
|---|---:|
| fixture |3|
| contract |11|
| directory |73|
| matched-rows |25|
| changed-rows |25|
| stale |29|
| ownership |42|
| faults |248|
| uncertain |32|
| concurrency |28|
| synthetic-defensive |5|
| legacy-offline |8|
| Total |529|

The eight legacy-offline harness assertions invoke unchanged stale11 + handoff13 + five hydration source-contract methods (**29 original cases**). These29 are nested checks, not an extra unique total to add to529. Hydration validation is the five unchanged source-contract methods, **not** execution of the full MSTest suite. Both final frozen-code runs pass529 with disposable cleanup PASS. No production DAO behavior was replaced by a test-local reimplementation. Covered families:

1. Actual canonical-schema directory, all seven fields, raw null/empty/hostile strings, case/trailing-space matching and detached results.
2. Account listing, filtering/no cross-account rows and unordered normalized legacy parity.
3. Real duplicate Name behavior; ownership exactly-one and synthetic impossible duplicate-ID results clearly separated.
4. NULL/0/1/nonstandard online states, exactly1 listing and nonzero recovery.
5. Both affected-row modes, missing/same-value/isolated writes and unchanged non-Online columns.
6. Each public operation's acquisition/open/read/execute/mapping/cleanup failure behavior and owned-resource disposal.
7. Recovery database match/mismatch, Serializable transaction, ascending locked capture, bounded IDs, affected mismatch and post-count rollback.
8. No-row no-write/no-count/no-commit path; before/after mutation failures; commit-before-durable and lost-after-durable acknowledgement; rollback/disposal secondary diagnostics and reconciliation.
9. Concurrent writers/row locks/no partial cleanup.
10. Lazy actual factory, mission/account method preservation, unsupported configured provider and contract/guard architecture.
11. Actual unchanged stale-recovery and handoff/session tests linked without an engine host.

Existing test/tool-only census: **17 direct CharacterDao invocations**, all `TOOL_OR_TEST_ONLY`, separate from the production53:

| Exact test file | Direct calls |
|---|---|
| Tools/Stage6MySqlIntegrationTests/Program.cs |215 GetByCharName;259 Add;289 Get (3) |
| Tools/Stage7MySqlSecurityIntegrationTests/Program.cs |395/396/397 GetByCharName;450 Add;569/583/693/773/774 Get;590/595 IsCharacterOnAccount;739/741 IsOnline;740 SetOffline (14) |

AccountDaoValidation Program/FailureChecks contain no direct CharacterDao call; their unchanged legacy LoginDataDao by-character checks use it indirectly. The new characterization suite is not counted as a new production consumer. Existing stale11, handoff13, hydration5 and ownership6 fixtures were read by the test audit owner; their current execution results are recorded only once fresh runs finish.

No legacy deletion was executed for this directory/online foundation. The test-only creation/write callsites above are census evidence, not permission to run their broad application-dependent harnesses.

## 13. Account and mission regression results

Accepted start baseline: account273 PASS twice; mission202 PASS. These historical accepted counts are not substituted for this branch's fresh regression run.

| Suite | Fresh clean e3acc4c baseline | Character working branch |
|---|---|---|
| Account DAO |273 PASS on serial retry|273 PASS first attempt|
| Isolated mission DAO |202 PASS on serial third attempt|202 PASS first attempt|
| Character DAO final run1/run2 |New foundation, not present at baseline|529 PASS twice; cleanup PASS twice|
| LoginAuthenticationValidation |14/14 PASS|14/14 PASS|

Current paired account evidence: `base/account-mysqlretry.json` and `work/account-mysql.json`. The initial baseline account fixture hit its startup deadline with MySqlException1042 and cleanup PASS (`base/account-mysql.json`); it did not execute273 assertions and is retained as an infrastructure attempt, not counted as a passing run.

Current paired mission evidence: `base/mission-isolatedserial3.json` and `work/mission-isolated.json`. The first two baseline attempts failed with `ERROR=InvalidOperationException:disposable-mysql-startup-timeout`; the serial third attempt ran the unchanged complete202 assertions. The successful base/work mission raw logs have identical SHA256 `634224e0f7d03bdb2f90689e0f186758f1c6cfc759b9c668e6b3510f8fd2b6d4`. The ordinary full mission wrapper's independent CS2001 baseline blocker remains in section24.

The governed complete isolated mission command is `call Tools\\run_mission_dao_validation.cmd --isolated-sources`. This does not exclude or reduce the202 DAO assertions. Account command is `call Tools\\run_account_dao_validation.cmd`.

Final character evidence: `work/character-mysql1.json` and `work/character-mysql2.json`, both exit0, CHARACTER_DAO_CHECKS=529, CHARACTER_DAO_MYSQL_INTEGRATION=PASS and CHARACTER_DAO_DISPOSABLE_CLEANUP=PASS. Both raw logs have SHA256 `aee48297a4da235a686c165759450a940170cc45b342b23af2202b2e20bbeae9`. Their ordered assertion labels and category totals are retained in acceptance-evidence.json.

Unchanged stale/handoff tests and five hydration source-contract methods execute in the new source-isolated suite, with29 original cases beneath eight of the529 harness assertions; the full hydration MSTest suite is not claimed. LoginAuthenticationValidation logs retain the expected caught ArithmeticException decrypt-failure exercise.

All successful SQL runs must use governed disposable MySQL fixtures, never the application database. No tests are excluded to produce a green result.

## 14. Architecture guard scope

Validated bounded scope: `DAO_GUARD_SCOPE=CHARACTER_ACCOUNT_AND_MISSION`.

The guard extends, rather than weakens, accepted account/mission rules. Character contracts reject SQL, System.Data/provider/connection/reader/command/transaction/Dapper/Connector/DBCharacter leaks, generic CRUD and excluded aggregate/social/stat/delete ownership. The implementation rejects engine/runtime dependencies. Future runtime-domain dependency checks keep factory/concrete DAO dependencies out where current scoped rules apply.

No repository-wide fail-mode expansion or broad exception for legacy consumers is authorized. Current working character scoped command passes74 character self-checks and56 account self-checks, with zero character/account/mission boundary violations. Existing account-only56 and mission-only modes pass on both baseline and working branch. Evidence: build-verify/character-parallel/work/character-guard.json and paired account-guard.json/mission-guard.json files. The default repository-wide guard still fails as documented in section24.

## 15. Complete production census and inspection evidence

Census unit is one direct active production invocation, not a text hit, a helper definition, an enclosing method or a test assertion. **53 active direct invocations:45 under Server and8 under Libraries.** All53 remain on legacy runtime paths.

| Classification | Server | Libraries | Total |
|---|---:|---:|---:|
| SAFE_CHARACTER_DAO_CUTOVER |15|3|18|
| ZONEENGINE_OWNER_REQUIRED |17|5|22|
| DEFERRED_TO_CHARACTER_AGGREGATE |10|0|10|
| DEFERRED_TO_CHARACTER_DELETE_TRANSACTION |2|0|2|
| DEFERRED_TO_CHAT_SOCIAL_DAO |1|0|1|
| Total |45|8|53|

There are35 non-safe-category calls, but **53 runtime-unmigrated direct calls**. Accordingly `DEFERRED_CHARACTER_CALL_SITES=53` records remaining legacy runtime calls, not merely the35 non-safe classifications. Counts do not decrease because a new parallel contract exists.

Search corpus: all tracked repository files, with the user's complete search-term set (CharacterDao.Instance, CharacterDao., DBCharacter, every named helper, ownership guard, stale recovery and characters.Online). Searches also inspected inherited Get/GetAll/GetWhere/Add/Save/Delete/Count and followed variables receiving CharacterDao results. Text matches in DBCharacterActiveNano/DBCharacterPerk are distinct entities, not DBCharacter consumers. Helper-internal generic calls are catalogued below but not double-counted as external production consumers.

The read-only Server search found46 direct invocations including the uncompiled PlayerController1 copy; excluding that source gives45. Two additional tracked loose root LftSearch copies each contain one Get, but have no compile-reference hits. They are independently classified INACTIVE_OR_UNPROVEN, not hidden or counted as production:

- `AORebirth/Server/ZoneEngine/Core/Controllers/PlayerController1.cs:964`: LogoffCharacter -> SetOffline. No current project/source-inventory reference found.
- `AORebirth/LftSearch_49058412.cs`: ResolvePlayfield -> Get.
- `AORebirth/LftSearch_before_pull.cs`: ResolvePlayfield -> Get.

Inactive copies were inspected to classify the call and compile references, not represented as complete active-production full reads.

Complete active Server consumer files read (full files, with bounded overlapping reads where output was large):

```text
AORebirth/Server/ChatEngine/CoreClient/Character.cs
AORebirth/Server/ChatEngine/CoreClient/CharacterBase.cs
AORebirth/Server/ChatEngine/CoreServer/ChatServer.cs
AORebirth/Server/ChatEngine/Lists/BuddyList.cs
AORebirth/Server/ChatEngine/PacketHandlers/Authenticate.cs
AORebirth/Server/ChatEngine/PacketHandlers/LftSearch.cs
AORebirth/Server/ChatEngine/PacketHandlers/LoginCharacter.cs
AORebirth/Server/ChatEngine/PacketHandlers/PlayerNameLookup.cs
AORebirth/Server/ChatEngine/PacketHandlers/Tell.cs
AORebirth/Server/ChatEngine/Packets/AccountCharacterList.cs
AORebirth/Server/LoginEngine/CoreClient/LoginHandoffLifecycle.cs
AORebirth/Server/LoginEngine/MessageHandlers/SelectCharacterHandler.cs
AORebirth/Server/LoginEngine/Packets/CharacterName.cs
AORebirth/Server/LoginEngine/Packets/CheckLogin.cs
AORebirth/Server/LoginEngine/QueryBase/CharacterList.cs
AORebirth/Server/WebEngine/Websites/IndexPHP.cs
AORebirth/Server/ZoneEngine/ChatCommands/Npc.cs
AORebirth/Server/ZoneEngine/Core/Controllers/PlayerController.cs
AORebirth/Server/ZoneEngine/Core/Entities/Character.cs
AORebirth/Server/ZoneEngine/Core/Mail/MailRuntimeService.cs
AORebirth/Server/ZoneEngine/Core/PacketHandlers/ClientConnected.cs
AORebirth/Server/ZoneEngine/Core/PacketHandlers/OrgClient.cs
AORebirth/Server/ZoneEngine/Core/Playfields/NascenceDungeonLeaseRehydrate.cs
AORebirth/Server/ZoneEngine/Core/ZoneClient.cs
AORebirth/Server/ZoneEngine_New/Core/Data/MySqlCharacterRepository.cs
```

Six full reads already performed in the preceding account audit (Chat Character/Authenticate, Login CharacterName/CheckLogin, Web IndexPHP, Zone ClientConnected) were re-grounded by an empty diff from522cbf3a to the exact e3acc4c baseline; those current files are unchanged. No old chat assertion substitutes for their repository content.

Complete Libraries audit files read by the owning audit agent:

```text
AORebirth/Libraries/Source/AORebirth.Core/Encryption/LoginEncryption.cs
AORebirth/Libraries/Source/AORebirth.Core/NPCHandler/NonPlayerCharacterHandler.cs
AORebirth/Libraries/Source/AORebirth.Database/Dao/LoginDataDao.cs
AORebirth/Libraries/Source/AORebirth.Database/Dao/CharacterOnlineOwnershipGuard.cs
AORebirth/Libraries/Source/AORebirth.Database/Dao/CharacterDao.cs
AORebirth/Libraries/Source/AORebirth.Database/Entities/DBCharacter.cs
AORebirth/Libraries/Source/AORebirth.Database/Dao/Dao.cs
AORebirth/Libraries/Source/AORebirth.Database/SqlMapperUtil.cs
AORebirth/Libraries/Source/AORebirth.Database/Dao/BuddyListDao.cs
AORebirth/Libraries/Source/AORebirth.Database/SqlTables/characters.sql
```

Additional required authority/reference sources are AI_START_HERE.md, docs/project/DEVELOPMENT_AUTHORITY.md, docs/project/PROJECT_STATE.md, docs/ai/CURRENT_TASK.md, docs/project/KNOWN_DECISIONS.md, docs/project/SUBSYSTEMS.md, docs/project/ARCHITECTURE.md, docs/ai/WORKFLOW.md, DAO_REFACTOR_AUDIT.md, DAO_REFACTOR_ROADMAP.md and the mission/account handoffs. The coordinating audit read the unchanged ZoneEngine/StaleOnlineRecovery.cs completely and mapped every database stage in section9. Canonical character schema and inherited mapper/DAO files are listed above; exact source-linked test inventory and hashes are in the acceptance evidence. No exact CharacterDao/DBCharacter runtime consumer was found in Stats, AccountBroker or BotService; account-identity/broker SQL remains separately owned.

Important non-invocation consumers:

- `LoginEngine/Packets/CheckLogin.cs` validates account/id then delegates ownership through LoginEncryption; the actual DAO call is L01, not an extra direct call.
- `LoginHandoffLifecycle.cs:32` delegates TryClearLoginOwnership; ZoneClient's AcquireZoneOwnership call remains runtime-owned. Their three actual DAO writes are L06-L08.
- `NascenceDungeonLeaseRehydrate.cs` receives DBCharacter and uses position/Playfield plus stats/leases; it has no CharacterDao invocation and cannot use this narrow DTO as a drop-in.
- `ZoneEngine_New/Core/Data/MySqlCharacterRepository.cs` owns direct SQL for position/heading hydration. Its CharacterDao mention is a comment, not a call. It remains DEFERRED_TO_CHARACTER_AGGREGATE with ZONEENGINE_OWNER_REQUIRED integration.
- StaleOnlineRecovery directly owns its database store and runtime command; it is separately mapped in section9 rather than added to53.

## 16. One-to-one future integration ledger

Every entry below has the thirteen requested fields. Paths/line numbers identify the accepted e3acc4c runtime baseline. SAFE means the bounded persistence primitive can represent the call with the stated compatibility mapping, **not** that this branch authorizes or performs a runtime edit. Every engine edit still requires its owner. All target error mappings keep presentation/runtime fallbacks outside the DAO.

### S01

```text
CURRENT_FILE=AORebirth/Server/ChatEngine/CoreClient/Character.cs:79
CURRENT_CLASS_OR_METHOD=Character(uint, Client) constructor
CURRENT_CHARACTER_DAO_CALL=GetCharacterNameById((int)characterId)
TARGET_ICHARACTERDAO_OPERATION=LoadById
INPUT_MAPPING=Preserve characterId!=0 short circuit and uint-to-int cast.
RETURN_MAPPING=row == null ? string.Empty : row.Name ?? string.Empty
CURRENT_NOT_FOUND_BEHAVIOR=Empty string assigned to characterName.
CURRENT_ERROR_BEHAVIOR=Provider/mapping failures propagate; no local fallback.
TARGET_ERROR_BEHAVIOR=DAO propagates; retain the stated caller fallback outside the DAO.
PACKET_OR_RUNTIME_RISK=Name seed timing stays after base constructor; preserve existing account/stat lazy properties.
REQUIRES_OTHER_DAO=NO for this read
REQUIRES_ENGINE_OWNER=YES, Chat owner
CUTOVER_STATUS=SAFE_CHARACTER_DAO_CUTOVER
```

### S02

```text
CURRENT_FILE=AORebirth/Server/ChatEngine/CoreClient/CharacterBase.cs:91
CURRENT_CLASS_OR_METHOD=CharacterBase.ReadNames
CURRENT_CHARACTER_DAO_CALL=Get((int)CharacterId)
TARGET_ICHARACTERDAO_OPERATION=LoadById
INPUT_MAPPING=Same uint-to-int ID cast.
RETURN_MAPPING=Name, FirstName, LastName; assign only when row exists.
CURRENT_NOT_FOUND_BEHAVIOR=Return false; existing fields remain.
CURRENT_ERROR_BEHAVIOR=Provider/mapping failures propagate; no local fallback.
TARGET_ERROR_BEHAVIOR=DAO propagates; retain the stated caller fallback outside the DAO.
PACKET_OR_RUNTIME_RISK=Separate clan-stat and organization reads remain in caller; missing organization may still dereference null.
REQUIRES_OTHER_DAO=StatDao and OrganizationDao in current composition
REQUIRES_ENGINE_OWNER=YES, Chat owner
CUTOVER_STATUS=SAFE_CHARACTER_DAO_CUTOVER
```

### S03

```text
CURRENT_FILE=AORebirth/Server/ChatEngine/CoreServer/ChatServer.cs:140
CURRENT_CLASS_OR_METHOD=ChatServer.OnClientDisconnect
CURRENT_CHARACTER_DAO_CALL=SetOffline((int)cl.Character.CharacterId)
TARGET_ICHARACTERDAO_OPERATION=MarkOffline
INPUT_MAPPING=Same ID cast; only nonzero ID branch.
RETURN_MAPPING=Discard raw affected count for existing void behavior.
CURRENT_NOT_FOUND_BEHAVIOR=Zero affected rows ignored.
CURRENT_ERROR_BEHAVIOR=Provider/mapping failures propagate; no local fallback.
TARGET_ERROR_BEHAVIOR=DAO preserves primary failure and records secondary rollback failure; uncertain commit requires reconciliation. Caller policy remains unchanged.
PACKET_OR_RUNTIME_RISK=Occurs after LftRegistry.Remove and before ConnectedClients.Remove; no bot-only restriction.
REQUIRES_OTHER_DAO=NO
REQUIRES_ENGINE_OWNER=YES, Chat/Zone ownership coordination
CUTOVER_STATUS=ZONEENGINE_OWNER_REQUIRED
```

### S04

```text
CURRENT_FILE=AORebirth/Server/ChatEngine/CoreServer/ChatServer.cs:586
CURRENT_CLASS_OR_METHOD=ChatServer.DistributeVicinityChat
CURRENT_CHARACTER_DAO_CALL=GetCharacterNameById(vicinityChatMessage.SenderId)
TARGET_ICHARACTERDAO_OPERATION=LoadById
INPUT_MAPPING=SenderId unchanged.
RETURN_MAPPING=row == null ? string.Empty : row.Name ?? string.Empty
CURRENT_NOT_FOUND_BEHAVIOR=Empty name sent in NameLookupResult.
CURRENT_ERROR_BEHAVIOR=Propagates into ISComDataReceived outer catch, which logs and swallows.
TARGET_ERROR_BEHAVIOR=DAO propagates; retain the stated caller fallback outside the DAO.
PACKET_OR_RUNTIME_RISK=Name-lookup packet precedes vicinity packet for unknown sender. Preserve outer catch and packet order.
REQUIRES_OTHER_DAO=NO
REQUIRES_ENGINE_OWNER=YES, Chat owner
CUTOVER_STATUS=SAFE_CHARACTER_DAO_CUTOVER
```

### S05

```text
CURRENT_FILE=AORebirth/Server/ChatEngine/Lists/BuddyList.cs:61
CURRENT_CLASS_OR_METHOD=BuddyList.LoadBuddyList
CURRENT_CHARACTER_DAO_CALL=Get(charId).GetBuddiesIds()
TARGET_ICHARACTERDAO_OPERATION=NONE: social projection excluded
INPUT_MAPPING=charId unchanged if later social migration.
RETURN_MAPPING=Requires BuddyList CSV parsing, not the directory DTO.
CURRENT_NOT_FOUND_BEHAVIOR=Null row dereference throws.
CURRENT_ERROR_BEHAVIOR=Provider/mapping failures propagate; no local fallback.
TARGET_ERROR_BEHAVIOR=No operation in this foundation; legacy path retained.
PACKET_OR_RUNTIME_RISK=Buddy membership and CSV semantics are not directory persistence.
REQUIRES_OTHER_DAO=Future IChatSocialDao
REQUIRES_ENGINE_OWNER=YES, Chat owner
CUTOVER_STATUS=DEFERRED_TO_CHAT_SOCIAL_DAO
```

### S06

```text
CURRENT_FILE=AORebirth/Server/ChatEngine/PacketHandlers/Authenticate.cs:144
CURRENT_CLASS_OR_METHOD=Authenticate.Read
CURRENT_CHARACTER_DAO_CALL=IsCharacterOnAccount(loginData.Username, characterId)
TARGET_ICHARACTERDAO_OPERATION=IsOwnedByAccount
INPUT_MAPPING=Use the resolved account row's Username and original uint characterId.
RETURN_MAPPING=Boolean unchanged.
CURRENT_NOT_FOUND_BEHAVIOR=False -> Invalid login packet and disconnect.
CURRENT_ERROR_BEHAVIOR=Provider/mapping failures propagate; no local fallback.
TARGET_ERROR_BEHAVIOR=DAO propagates; retain the stated caller fallback outside the DAO.
PACKET_OR_RUNTIME_RISK=Keep account validation/authentication order; do not accept an untrusted supplied identity as authorization.
REQUIRES_OTHER_DAO=IAccountDao for separate account authentication lookup
REQUIRES_ENGINE_OWNER=YES, Chat/authentication owner
CUTOVER_STATUS=SAFE_CHARACTER_DAO_CUTOVER
```

### S07

```text
CURRENT_FILE=AORebirth/Server/ChatEngine/PacketHandlers/LftSearch.cs:854
CURRENT_CLASS_OR_METHOD=LftSearch.ResolvePlayfield
CURRENT_CHARACTER_DAO_CALL=Get((int)characterId)
TARGET_ICHARACTERDAO_OPERATION=LoadById
INPUT_MAPPING=Same cast; only after live LftPlayfieldRegistry lookup misses.
RETURN_MAPPING=Found positive Playfield returned; otherwise 0.
CURRENT_NOT_FOUND_BEHAVIOR=Missing/nonpositive Playfield ->0.
CURRENT_ERROR_BEHAVIOR=Exception caught and mapped to0 in this helper.
TARGET_ERROR_BEHAVIOR=DAO propagates; retain the stated caller fallback outside the DAO.
PACKET_OR_RUNTIME_RISK=Registry-first fallback remains runtime; RK/SL filtering and level/profession reads stay outside DAO.
REQUIRES_OTHER_DAO=StatDao in surrounding LFT composition
REQUIRES_ENGINE_OWNER=YES, Chat owner
CUTOVER_STATUS=SAFE_CHARACTER_DAO_CUTOVER
```

### S08

```text
CURRENT_FILE=AORebirth/Server/ChatEngine/PacketHandlers/LoginCharacter.cs:81
CURRENT_CLASS_OR_METHOD=LoginCharacter.Read ownership check
CURRENT_CHARACTER_DAO_CALL=IsCharacterOnAccount(client.AuthenticatedUsername, playerId)
TARGET_ICHARACTERDAO_OPERATION=IsOwnedByAccount
INPUT_MAPPING=Retain whitespace-authenticated-username short circuit and uint playerId.
RETURN_MAPPING=Boolean unchanged.
CURRENT_NOT_FOUND_BEHAVIOR=False -> Invalid character ownership and disconnect.
CURRENT_ERROR_BEHAVIOR=Provider/mapping failures propagate; no local fallback.
TARGET_ERROR_BEHAVIOR=DAO propagates; retain the stated caller fallback outside the DAO.
PACKET_OR_RUNTIME_RISK=Authenticated session is authoritative; ownership check still precedes writes and name load.
REQUIRES_OTHER_DAO=NO for primitive
REQUIRES_ENGINE_OWNER=YES, Chat/authentication owner
CUTOVER_STATUS=SAFE_CHARACTER_DAO_CUTOVER
```

### S09

```text
CURRENT_FILE=AORebirth/Server/ChatEngine/PacketHandlers/LoginCharacter.cs:90
CURRENT_CLASS_OR_METHOD=LoginCharacter.Read bot branch
CURRENT_CHARACTER_DAO_CALL=SetOnline((int)playerId)
TARGET_ICHARACTERDAO_OPERATION=MarkOnline
INPUT_MAPPING=Same cast, bot branch only.
RETURN_MAPPING=Discard affected count.
CURRENT_NOT_FOUND_BEHAVIOR=Zero affected rows ignored.
CURRENT_ERROR_BEHAVIOR=Provider/mapping failures propagate; no local fallback.
TARGET_ERROR_BEHAVIOR=DAO preserves primary failure and records secondary rollback failure; uncertain commit requires reconciliation. Caller policy remains unchanged.
PACKET_OR_RUNTIME_RISK=Bot online write precedes the later Get and login-success packets; do not broaden to all clients.
REQUIRES_OTHER_DAO=NO
REQUIRES_ENGINE_OWNER=YES, Chat/Zone ownership coordination
CUTOVER_STATUS=ZONEENGINE_OWNER_REQUIRED
```

### S10

```text
CURRENT_FILE=AORebirth/Server/ChatEngine/PacketHandlers/LoginCharacter.cs:93
CURRENT_CLASS_OR_METHOD=LoginCharacter.Read character-name load
CURRENT_CHARACTER_DAO_CALL=Get((int)playerId)
TARGET_ICHARACTERDAO_OPERATION=LoadById
INPUT_MAPPING=Same uint-to-int playerId cast.
RETURN_MAPPING=Name, FirstName, LastName copied unchanged.
CURRENT_NOT_FOUND_BEHAVIOR=Unconditional row dereference throws after ownership check if row disappeared.
CURRENT_ERROR_BEHAVIOR=Provider/mapping failures propagate; no local fallback.
TARGET_ERROR_BEHAVIOR=DAO propagates; retain the stated caller fallback outside the DAO.
PACKET_OR_RUNTIME_RISK=Ownership-to-load race remains; converting missing into success/default would change behavior. Preserve subsequent client flags and packets.
REQUIRES_OTHER_DAO=NO
REQUIRES_ENGINE_OWNER=YES, Chat owner
CUTOVER_STATUS=SAFE_CHARACTER_DAO_CUTOVER
```

### S11

```text
CURRENT_FILE=AORebirth/Server/ChatEngine/PacketHandlers/PlayerNameLookup.cs:82
CURRENT_CLASS_OR_METHOD=PlayerNameLookup.Read name resolution
CURRENT_CHARACTER_DAO_CALL=GetByCharName(playerName)
TARGET_ICHARACTERDAO_OPERATION=LoadByName
INPUT_MAPPING=Retain caller rejection of empty, ':' and '::'; remaining name unchanged.
RETURN_MAPPING=Found CharacterId cast to uint; missing -> BotRouter.TryResolveBotName fallback.
CURRENT_NOT_FOUND_BEHAVIOR=Unresolved ID stays uint.MaxValue; reply still uses requested name.
CURRENT_ERROR_BEHAVIOR=Provider/mapping failures propagate; no local fallback.
TARGET_ERROR_BEHAVIOR=DAO propagates; retain the stated caller fallback outside the DAO.
PACKET_OR_RUNTIME_RISK=Do not move BotRouter fallback or replace requested-name spelling with persisted spelling.
REQUIRES_OTHER_DAO=NO for directory read
REQUIRES_ENGINE_OWNER=YES, Chat owner
CUTOVER_STATUS=SAFE_CHARACTER_DAO_CUTOVER
```

### S12

```text
CURRENT_FILE=AORebirth/Server/ChatEngine/PacketHandlers/PlayerNameLookup.cs:100
CURRENT_CLASS_OR_METHOD=PlayerNameLookup.Read presence response
CURRENT_CHARACTER_DAO_CALL=IsOnline((int)playerId)
TARGET_ICHARACTERDAO_OPERATION=LoadById
INPUT_MAPPING=Same cast; called only for resolved ID.
RETURN_MAPPING=(row == null ? 0 : row.Online ?? 0), followed by existing wire cast.
CURRENT_NOT_FOUND_BEHAVIOR=Zero online status.
CURRENT_ERROR_BEHAVIOR=Exception caught and mapped to0 before BuddyOnlineStatus packet.
TARGET_ERROR_BEHAVIOR=DAO propagates; retain the stated caller fallback outside the DAO.
PACKET_OR_RUNTIME_RISK=This caller intentionally hides provider failure as offline; that fallback stays outside DAO.
REQUIRES_OTHER_DAO=NO
REQUIRES_ENGINE_OWNER=YES, Chat owner
CUTOVER_STATUS=SAFE_CHARACTER_DAO_CUTOVER
```

### S13

```text
CURRENT_FILE=AORebirth/Server/ChatEngine/PacketHandlers/Tell.cs:92
CURRENT_CLASS_OR_METHOD=Tell.Read known-client seed
CURRENT_CHARACTER_DAO_CALL=IsOnline((int)tellClient.Character.CharacterId)
TARGET_ICHARACTERDAO_OPERATION=LoadById
INPUT_MAPPING=Same recipient-character ID cast, after bot and connected-client routing.
RETURN_MAPPING=(row == null ? 0 : row.Online ?? 0), preserving uint wire cast.
CURRENT_NOT_FOUND_BEHAVIOR=Zero online status.
CURRENT_ERROR_BEHAVIOR=Provider/mapping failures propagate; no local fallback.
TARGET_ERROR_BEHAVIOR=DAO propagates; retain the stated caller fallback outside the DAO.
PACKET_OR_RUNTIME_RISK=Unlike PlayerNameLookup, this call does not catch failure; preserve PlayerName/BuddyOnlineStatus ordering and nonstandard-value cast.
REQUIRES_OTHER_DAO=NO
REQUIRES_ENGINE_OWNER=YES, Chat owner
CUTOVER_STATUS=SAFE_CHARACTER_DAO_CUTOVER
```

### S14

```text
CURRENT_FILE=AORebirth/Server/ChatEngine/Packets/AccountCharacterList.cs:62
CURRENT_CLASS_OR_METHOD=AccountCharacterList.Create directory read
CURRENT_CHARACTER_DAO_CALL=GetAllForUser(username)
TARGET_ICHARACTERDAO_OPERATION=ListForAccount
INPUT_MAPPING=username unchanged.
RETURN_MAPPING=Reuse one buffered list for count and all parallel ID/name/level/online arrays; Id->CharacterId.
CURRENT_NOT_FOUND_BEHAVIOR=Empty collection generates zero-length lists.
CURRENT_ERROR_BEHAVIOR=Provider/mapping failures propagate; no local fallback.
TARGET_ERROR_BEHAVIOR=DAO propagates; retain the stated caller fallback outside the DAO.
PACKET_OR_RUNTIME_RISK=No ORDER BY promise. Preserve one directory snapshot and separate per-row stat/presence query timing.
REQUIRES_OTHER_DAO=StatDao stat54 for level
REQUIRES_ENGINE_OWNER=YES, Chat owner
CUTOVER_STATUS=SAFE_CHARACTER_DAO_CUTOVER
```

### S15

```text
CURRENT_FILE=AORebirth/Server/ChatEngine/Packets/AccountCharacterList.cs:86
CURRENT_CLASS_OR_METHOD=AccountCharacterList.Create per-row presence
CURRENT_CHARACTER_DAO_CALL=IsOnline(character.Id)
TARGET_ICHARACTERDAO_OPERATION=LoadById
INPUT_MAPPING=Enumerated CharacterId unchanged.
RETURN_MAPPING=row == null ? 0 : row.Online ?? 0, preserving uint conversion.
CURRENT_NOT_FOUND_BEHAVIOR=Zero presence for disappeared row.
CURRENT_ERROR_BEHAVIOR=Provider/mapping failures propagate; no local fallback.
TARGET_ERROR_BEHAVIOR=DAO propagates; retain the stated caller fallback outside the DAO.
PACKET_OR_RUNTIME_RISK=Do not substitute earlier ListForAccount.Online without accepting changed read timing. Reuse list order across packet arrays.
REQUIRES_OTHER_DAO=StatDao for separate packet field
REQUIRES_ENGINE_OWNER=YES, Chat owner
CUTOVER_STATUS=SAFE_CHARACTER_DAO_CUTOVER
```

### S16

```text
CURRENT_FILE=AORebirth/Server/LoginEngine/CoreClient/LoginHandoffLifecycle.cs:27
CURRENT_CLASS_OR_METHOD=CharacterDaoLoginHandoffOnlineStore.SetOnline
CURRENT_CHARACTER_DAO_CALL=SetOnline(characterId)
TARGET_ICHARACTERDAO_OPERATION=MarkOnline
INPUT_MAPPING=Existing validated handoff characterId unchanged.
RETURN_MAPPING=Discard raw affected count.
CURRENT_NOT_FOUND_BEHAVIOR=Zero affected rows ignored.
CURRENT_ERROR_BEHAVIOR=Provider/mapping failures propagate; no local fallback.
TARGET_ERROR_BEHAVIOR=DAO preserves primary failure and records secondary rollback failure; uncertain commit requires reconciliation. Caller policy remains unchanged.
PACKET_OR_RUNTIME_RISK=Lifecycle.MarkOnline calls store before recording Marked state; failed write must not advance lifecycle. Existing ownership store interface stays runtime.
REQUIRES_OTHER_DAO=NO
REQUIRES_ENGINE_OWNER=YES, Login/Zone handoff owner
CUTOVER_STATUS=ZONEENGINE_OWNER_REQUIRED
```

### S17

```text
CURRENT_FILE=AORebirth/Server/LoginEngine/MessageHandlers/SelectCharacterHandler.cs:95
CURRENT_CLASS_OR_METHOD=SelectCharacterHandler.Read stale-presence branch
CURRENT_CHARACTER_DAO_CALL=IsOnline(selectCharacterMessage.CharacterId)
TARGET_ICHARACTERDAO_OPERATION=LoadById
INPUT_MAPPING=ID unchanged after authenticated-account ownership validation.
RETURN_MAPPING=row == null ? 0 : row.Online ?? 0; preserve comparison ==1, not !=0.
CURRENT_NOT_FOUND_BEHAVIOR=Zero; no stale-clear branch.
CURRENT_ERROR_BEHAVIOR=Provider/mapping failures propagate; no local fallback.
TARGET_ERROR_BEHAVIOR=DAO propagates; retain the stated caller fallback outside the DAO.
PACKET_OR_RUNTIME_RISK=This read and stale-clear write occur before the later redirect try/catch. Nonstandard2 does not take this branch.
REQUIRES_OTHER_DAO=NO for primitive
REQUIRES_ENGINE_OWNER=YES, Login/Zone handoff owner
CUTOVER_STATUS=ZONEENGINE_OWNER_REQUIRED
```

### S18

```text
CURRENT_FILE=AORebirth/Server/LoginEngine/MessageHandlers/SelectCharacterHandler.cs:100
CURRENT_CLASS_OR_METHOD=SelectCharacterHandler.Read stale-presence clear
CURRENT_CHARACTER_DAO_CALL=SetOffline(selectCharacterMessage.CharacterId)
TARGET_ICHARACTERDAO_OPERATION=MarkOffline
INPUT_MAPPING=Same ID, only when previous IsOnline==1.
RETURN_MAPPING=Discard affected count.
CURRENT_NOT_FOUND_BEHAVIOR=Zero affected rows ignored.
CURRENT_ERROR_BEHAVIOR=Provider/mapping failures propagate; no local fallback.
TARGET_ERROR_BEHAVIOR=DAO preserves primary failure and records secondary rollback failure; uncertain commit requires reconciliation. Caller policy remains unchanged.
PACKET_OR_RUNTIME_RISK=Write remains before MarkCharacterOnlineForHandoff and outside later redirect catch. Do not redesign stale-session policy.
REQUIRES_OTHER_DAO=NO
REQUIRES_ENGINE_OWNER=YES, Login/Zone handoff owner
CUTOVER_STATUS=ZONEENGINE_OWNER_REQUIRED
```

### S19

```text
CURRENT_FILE=AORebirth/Server/LoginEngine/Packets/CharacterName.cs:139
CURRENT_CLASS_OR_METHOD=CharacterName.CheckAgainstDatabase
CURRENT_CHARACTER_DAO_CALL=ExistsByName(this.Name)
TARGET_ICHARACTERDAO_OPERATION=LoadByName
INPUT_MAPPING=this.Name unchanged; preserve existing preceding caller checks.
RETURN_MAPPING=row != null; not an exactly-one count.
CURRENT_NOT_FOUND_BEHAVIOR=False -> existing CreateNewChar path; true ->0.
CURRENT_ERROR_BEHAVIOR=Provider/mapping failures propagate; no local fallback.
TARGET_ERROR_BEHAVIOR=DAO propagates; retain the stated caller fallback outside the DAO.
PACKET_OR_RUNTIME_RISK=Duplicate character names are schema-permitted; existence remains true. Creation itself is not approved for migration.
REQUIRES_OTHER_DAO=NO for existence; creation has other persistence
REQUIRES_ENGINE_OWNER=YES, Login owner
CUTOVER_STATUS=SAFE_CHARACTER_DAO_CUTOVER
```

### S20

```text
CURRENT_FILE=AORebirth/Server/LoginEngine/Packets/CharacterName.cs:166
CURRENT_CLASS_OR_METHOD=CharacterName.TryDeleteChar(int)
CURRENT_CHARACTER_DAO_CALL=Delete(charid)
TARGET_ICHARACTERDAO_OPERATION=NONE: deletion transaction excluded
INPUT_MAPPING=charid unchanged in retained legacy path.
RETURN_MAPPING=Legacy void -> true if no exception.
CURRENT_NOT_FOUND_BEHAVIOR=Legacy operation does not require an existing character row.
CURRENT_ERROR_BEHAVIOR=Catch logs exception and returns false.
TARGET_ERROR_BEHAVIOR=No operation in this foundation; legacy path retained.
PACKET_OR_RUNTIME_RISK=Cross-aggregate transaction; never split into independently committed calls.
REQUIRES_OTHER_DAO=Organizations/stats/inventory/messages/missions/timers/nanos/meshes/perks
REQUIRES_ENGINE_OWNER=YES, deletion/runtime owner
CUTOVER_STATUS=DEFERRED_TO_CHARACTER_DELETE_TRANSACTION
```

### S21

```text
CURRENT_FILE=AORebirth/Server/LoginEngine/Packets/CharacterName.cs:188
CURRENT_CLASS_OR_METHOD=CharacterName.TryDeleteChar(string,int)
CURRENT_CHARACTER_DAO_CALL=DeleteForUser(accountName, charid)
TARGET_ICHARACTERDAO_OPERATION=NONE: deletion transaction excluded
INPUT_MAPPING=Authenticated accountName and ID retained.
RETURN_MAPPING=Legacy bool unchanged.
CURRENT_NOT_FOUND_BEHAVIOR=Invalid account/id, missing owned row or final delete count!=1 -> false.
CURRENT_ERROR_BEHAVIOR=Catch logs exception and returns false.
TARGET_ERROR_BEHAVIOR=No operation in this foundation; legacy path retained.
PACKET_OR_RUNTIME_RISK=Account-scoped delete must remain one cross-aggregate transaction; no new deletion API.
REQUIRES_OTHER_DAO=Organizations/stats/inventory/messages/missions/timers/nanos/meshes/perks
REQUIRES_ENGINE_OWNER=YES, deletion/runtime owner
CUTOVER_STATUS=DEFERRED_TO_CHARACTER_DELETE_TRANSACTION
```

### S22

```text
CURRENT_FILE=AORebirth/Server/LoginEngine/Packets/CharacterName.cs:260
CURRENT_CLASS_OR_METHOD=CharacterName.SendNameToStartPlayfield read
CURRENT_CHARACTER_DAO_CALL=Get(charid)
TARGET_ICHARACTERDAO_OPERATION=NONE: aggregate mutation source excluded
INPUT_MAPPING=charid unchanged.
RETURN_MAPPING=DBCharacter mutated for playfield and X/Y/Z then persisted.
CURRENT_NOT_FOUND_BEHAVIOR=Null skips save/playfield/pending-intro sequence.
CURRENT_ERROR_BEHAVIOR=Provider/mapping failures propagate; no local fallback.
TARGET_ERROR_BEHAVIOR=No operation in this foundation; legacy path retained.
PACKET_OR_RUNTIME_RISK=Syntactic read is part of location-save workflow, not a drop-in directory projection.
REQUIRES_OTHER_DAO=Mission pending-intro persistence in surrounding path
REQUIRES_ENGINE_OWNER=YES, Login/Zone aggregate owner
CUTOVER_STATUS=DEFERRED_TO_CHARACTER_AGGREGATE
```

### S23

```text
CURRENT_FILE=AORebirth/Server/LoginEngine/Packets/CharacterName.cs:263
CURRENT_CLASS_OR_METHOD=CharacterName.SendNameToStartPlayfield coordinate save
CURRENT_CHARACTER_DAO_CALL=Save(character, new { Id, Playfield, X, Y, Z })
TARGET_ICHARACTERDAO_OPERATION=NONE: Save/location excluded
INPUT_MAPPING=Existing selected fields and values retained.
RETURN_MAPPING=Legacy affected count ignored.
CURRENT_NOT_FOUND_BEHAVIOR=Missing update count ignored.
CURRENT_ERROR_BEHAVIOR=Provider/mapping failures propagate; no local fallback.
TARGET_ERROR_BEHAVIOR=No operation in this foundation; legacy path retained.
PACKET_OR_RUNTIME_RISK=Keep currently separate save/SetPlayfield/mission stages; do not create atomicity by accident.
REQUIRES_OTHER_DAO=Mission persistence in surrounding path
REQUIRES_ENGINE_OWNER=YES, aggregate/location owner
CUTOVER_STATUS=DEFERRED_TO_CHARACTER_AGGREGATE
```

### S24

```text
CURRENT_FILE=AORebirth/Server/LoginEngine/Packets/CharacterName.cs:268
CURRENT_CLASS_OR_METHOD=CharacterName.SendNameToStartPlayfield playfield save
CURRENT_CHARACTER_DAO_CALL=SetPlayfield(charid, (int)IdentityType.Playfield, playfield)
TARGET_ICHARACTERDAO_OPERATION=NONE: SetPlayfield excluded
INPUT_MAPPING=Same ID, pfType and chosen playfield; legacy pfType is unused.
RETURN_MAPPING=Legacy void unchanged.
CURRENT_NOT_FOUND_BEHAVIOR=Zero affected rows ignored.
CURRENT_ERROR_BEHAVIOR=Provider/mapping failures propagate; no local fallback.
TARGET_ERROR_BEHAVIOR=No operation in this foundation; legacy path retained.
PACKET_OR_RUNTIME_RISK=Location save is not online-state persistence; preserve duplicate/separate write timing.
REQUIRES_OTHER_DAO=NO for this write
REQUIRES_ENGINE_OWNER=YES, aggregate/location owner
CUTOVER_STATUS=DEFERRED_TO_CHARACTER_AGGREGATE
```

### S25

```text
CURRENT_FILE=AORebirth/Server/LoginEngine/Packets/CharacterName.cs:302
CURRENT_CLASS_OR_METHOD=CharacterName.CreateNewChar
CURRENT_CHARACTER_DAO_CALL=Add(newCharacter)
TARGET_ICHARACTERDAO_OPERATION=NONE: creation excluded
INPUT_MAPPING=DBCharacter constructed with Name/Username and empty first/last names.
RETURN_MAPPING=Generated ID assigned to entity and used by later initialization.
CURRENT_NOT_FOUND_BEHAVIOR=Not applicable to insert.
CURRENT_ERROR_BEHAVIOR=Insert/identity and downstream initialization failures propagate.
TARGET_ERROR_BEHAVIOR=No operation in this foundation; legacy path retained.
PACKET_OR_RUNTIME_RISK=Creation spans separate account/stat/loadout initialization; no CreateCharacter operation or new transaction boundary.
REQUIRES_OTHER_DAO=Account/stat/item/loadout persistence
REQUIRES_ENGINE_OWNER=YES, Login/aggregate owner
CUTOVER_STATUS=DEFERRED_TO_CHARACTER_AGGREGATE
```

### S26

```text
CURRENT_FILE=AORebirth/Server/LoginEngine/QueryBase/CharacterList.cs:61
CURRENT_CLASS_OR_METHOD=CharacterList.LoadCharacters
CURRENT_CHARACTER_DAO_CALL=GetAllForUser(accountName)
TARGET_ICHARACTERDAO_OPERATION=ListForAccount
INPUT_MAPPING=accountName unchanged.
RETURN_MAPPING=Id->CharacterId, Name and Playfield; detached list composed with stat-derived fields.
CURRENT_NOT_FOUND_BEHAVIOR=Empty collection produces no characters.
CURRENT_ERROR_BEHAVIOR=Provider/mapping failures propagate; no local fallback.
TARGET_ERROR_BEHAVIOR=DAO propagates; retain the stated caller fallback outside the DAO.
PACKET_OR_RUNTIME_RISK=Level/Breed/Gender/Profession come from StatDao; missing stat rows can throw. Preserve unspecified directory order.
REQUIRES_OTHER_DAO=StatDao54/4/59/60; future ICharacterStatsDao
REQUIRES_ENGINE_OWNER=YES, Login owner
CUTOVER_STATUS=SAFE_CHARACTER_DAO_CUTOVER
```

### S27

```text
CURRENT_FILE=AORebirth/Server/WebEngine/Websites/IndexPHP.cs:83
CURRENT_CLASS_OR_METHOD=IndexPHP.CreateContent
CURRENT_CHARACTER_DAO_CALL=GetLoggedInCharacters()
TARGET_ICHARACTERDAO_OPERATION=ListLoggedIn
INPUT_MAPPING=No input.
RETURN_MAPPING=Render Name and Playfield from rows with Online==1; same unordered list semantics.
CURRENT_NOT_FOUND_BEHAVIOR=Empty collection -> empty display table.
CURRENT_ERROR_BEHAVIOR=Provider/mapping failures propagate; no local fallback.
TARGET_ERROR_BEHAVIOR=DAO propagates; retain the stated caller fallback outside the DAO.
PACKET_OR_RUNTIME_RISK=Rendered level currently literal 'lvl'; do not invent a stat field or conflate broker website identities.
REQUIRES_OTHER_DAO=IAccountDao for separate registered-account count only
REQUIRES_ENGINE_OWNER=YES, Web owner
CUTOVER_STATUS=SAFE_CHARACTER_DAO_CUTOVER
```

### S28

```text
CURRENT_FILE=AORebirth/Server/ZoneEngine/ChatCommands/Npc.cs:98
CURRENT_CLASS_OR_METHOD=Npc.ExecuteCommand save branch
CURRENT_CHARACTER_DAO_CALL=Get(mob.Identity.Instance)
TARGET_ICHARACTERDAO_OPERATION=LoadById
INPUT_MAPPING=Preserve existing ID<1000000 short circuit and collision-check ID.
RETURN_MAPPING=row != null rejects player/NPC identity collision.
CURRENT_NOT_FOUND_BEHAVIOR=Null permits existing NPC save path.
CURRENT_ERROR_BEHAVIOR=Provider/mapping failures propagate; no local fallback.
TARGET_ERROR_BEHAVIOR=DAO propagates; retain the stated caller fallback outside the DAO.
PACKET_OR_RUNTIME_RISK=NPC/template/MobSpawn/stat mutations remain outside this DAO; failed lookup must not look absent.
REQUIRES_OTHER_DAO=MobSpawn/stat DAOs in surrounding command
REQUIRES_ENGINE_OWNER=YES, Zone/NPC owner
CUTOVER_STATUS=ZONEENGINE_OWNER_REQUIRED
```

### S29

```text
CURRENT_FILE=AORebirth/Server/ZoneEngine/ChatCommands/Npc.cs:170
CURRENT_CLASS_OR_METHOD=Npc.ExecuteCommand remove branch
CURRENT_CHARACTER_DAO_CALL=Get(target.Instance)
TARGET_ICHARACTERDAO_OPERATION=LoadById
INPUT_MAPPING=Same target.Instance after existing type/controller checks.
RETURN_MAPPING=row != null refuses removal.
CURRENT_NOT_FOUND_BEHAVIOR=Null permits NPC removal path.
CURRENT_ERROR_BEHAVIOR=Provider/mapping failures propagate; no local fallback.
TARGET_ERROR_BEHAVIOR=DAO propagates; retain the stated caller fallback outside the DAO.
PACKET_OR_RUNTIME_RISK=Do not turn this presence check into character deletion or alter target validation.
REQUIRES_OTHER_DAO=MobSpawn DAO in surrounding command
REQUIRES_ENGINE_OWNER=YES, Zone/NPC owner
CUTOVER_STATUS=ZONEENGINE_OWNER_REQUIRED
```

### S30

```text
CURRENT_FILE=AORebirth/Server/ZoneEngine/ChatCommands/Npc.cs:209
CURRENT_CLASS_OR_METHOD=Npc.ExecuteCommand knubot branch
CURRENT_CHARACTER_DAO_CALL=Get(cmob.Identity.Instance)
TARGET_ICHARACTERDAO_OPERATION=LoadById
INPUT_MAPPING=Preserve low-ID short circuit and runtime NPC checks.
RETURN_MAPPING=row != null refuses player collision.
CURRENT_NOT_FOUND_BEHAVIOR=Null permits existing script/MobSpawn change.
CURRENT_ERROR_BEHAVIOR=Provider/mapping failures propagate; no local fallback.
TARGET_ERROR_BEHAVIOR=DAO propagates; retain the stated caller fallback outside the DAO.
PACKET_OR_RUNTIME_RISK=Runtime NPC/script ownership remains separate.
REQUIRES_OTHER_DAO=MobSpawn/script persistence in surrounding command
REQUIRES_ENGINE_OWNER=YES, Zone/NPC owner
CUTOVER_STATUS=ZONEENGINE_OWNER_REQUIRED
```

### S31

```text
CURRENT_FILE=AORebirth/Server/ZoneEngine/Core/Controllers/PlayerController.cs:966
CURRENT_CLASS_OR_METHOD=PlayerController.LogoffCharacter
CURRENT_CHARACTER_DAO_CALL=SetOffline(this.Character.Identity.Instance)
TARGET_ICHARACTERDAO_OPERATION=MarkOffline
INPUT_MAPPING=Runtime Character.Identity.Instance unchanged.
RETURN_MAPPING=Discard affected count.
CURRENT_NOT_FOUND_BEHAVIOR=Zero affected rows ignored.
CURRENT_ERROR_BEHAVIOR=Provider/mapping failures propagate; no local fallback.
TARGET_ERROR_BEHAVIOR=DAO preserves primary failure and records secondary rollback failure; uncertain commit requires reconciliation. Caller policy remains unchanged.
PACKET_OR_RUNTIME_RISK=Logout/disposal/zone-transfer ordering is owned by runtime; this direct write has no added ownership check.
REQUIRES_OTHER_DAO=NO
REQUIRES_ENGINE_OWNER=YES, Zone lifecycle owner
CUTOVER_STATUS=ZONEENGINE_OWNER_REQUIRED
```

### S32

```text
CURRENT_FILE=AORebirth/Server/ZoneEngine/Core/Entities/Character.cs:514
CURRENT_CLASS_OR_METHOD=Character.Read
CURRENT_CHARACTER_DAO_CALL=Get(this.Identity.Instance)
TARGET_ICHARACTERDAO_OPERATION=NONE: full hydration excluded
INPUT_MAPPING=Runtime character ID retained.
RETURN_MAPPING=Uses Name/FirstName/LastName, X/Y/Z and heading X/Y/Z/W.
CURRENT_NOT_FOUND_BEHAVIOR=Missing character skips these assignments but continues other hydration and can return true.
CURRENT_ERROR_BEHAVIOR=Provider/mapping failures propagate; no local fallback.
TARGET_ERROR_BEHAVIOR=No operation in this foundation; legacy path retained.
PACKET_OR_RUNTIME_RISK=Directory DTO cannot replace full hydration. Keep timers, nanos, perks, inventory and base.Read sequencing.
REQUIRES_OTHER_DAO=Inventory/nano/perk/stat persistence
REQUIRES_ENGINE_OWNER=YES, Zone aggregate owner
CUTOVER_STATUS=DEFERRED_TO_CHARACTER_AGGREGATE
```

### S33

```text
CURRENT_FILE=AORebirth/Server/ZoneEngine/Core/Entities/Character.cs:567
CURRENT_CLASS_OR_METHOD=Character.Write
CURRENT_CHARACTER_DAO_CALL=Save(this.GetDBCharacter())
TARGET_ICHARACTERDAO_OPERATION=NONE: full character save excluded
INPUT_MAPPING=Full DBCharacter snapshot, including position/heading/Online=1.
RETURN_MAPPING=Legacy affected count ignored.
CURRENT_NOT_FOUND_BEHAVIOR=Missing row update count ignored.
CURRENT_ERROR_BEHAVIOR=Provider/mapping failures propagate; no local fallback.
TARGET_ERROR_BEHAVIOR=No operation in this foundation; legacy path retained.
PACKET_OR_RUNTIME_RISK=Inventory.Write failure can return false before save; preserve snapshot fields and separate subsequent saves.
REQUIRES_OTHER_DAO=Inventory/nano/perk and base persistence
REQUIRES_ENGINE_OWNER=YES, Zone aggregate owner
CUTOVER_STATUS=DEFERRED_TO_CHARACTER_AGGREGATE
```

### S34

```text
CURRENT_FILE=AORebirth/Server/ZoneEngine/Core/Entities/Character.cs:569
CURRENT_CLASS_OR_METHOD=Character.Write separate playfield save
CURRENT_CHARACTER_DAO_CALL=SetPlayfield(...)
TARGET_ICHARACTERDAO_OPERATION=NONE: location save excluded
INPUT_MAPPING=Same character ID/playfield values; pfType remains legacy-unused.
RETURN_MAPPING=Legacy void; count ignored.
CURRENT_NOT_FOUND_BEHAVIOR=Zero affected rows ignored.
CURRENT_ERROR_BEHAVIOR=Provider/mapping failures propagate; no local fallback.
TARGET_ERROR_BEHAVIOR=No operation in this foundation; legacy path retained.
PACKET_OR_RUNTIME_RISK=A second independently committed write after full save; do not merge with online operation.
REQUIRES_OTHER_DAO=Other aggregate persistence in surrounding Write
REQUIRES_ENGINE_OWNER=YES, Zone aggregate owner
CUTOVER_STATUS=DEFERRED_TO_CHARACTER_AGGREGATE
```

### S35

```text
CURRENT_FILE=AORebirth/Server/ZoneEngine/Core/Entities/Character.cs:609
CURRENT_CLASS_OR_METHOD=Character.Dispose(bool)
CURRENT_CHARACTER_DAO_CALL=SetOffline(charId)
TARGET_ICHARACTERDAO_OPERATION=MarkOffline
INPUT_MAPPING=Captured character ID unchanged.
RETURN_MAPPING=Discard affected count.
CURRENT_NOT_FOUND_BEHAVIOR=Zero affected rows ignored.
CURRENT_ERROR_BEHAVIOR=Provider/mapping failures propagate; no local fallback.
TARGET_ERROR_BEHAVIOR=DAO preserves primary failure and records secondary rollback failure; uncertain commit requires reconciliation. Caller policy remains unchanged.
PACKET_OR_RUNTIME_RISK=Save and inventory/controller disconnect/dispose precede offline write. Preserve save-before-offline and duplicate lifecycle calls.
REQUIRES_OTHER_DAO=Existing aggregate Save path remains
REQUIRES_ENGINE_OWNER=YES, Zone lifecycle owner
CUTOVER_STATUS=ZONEENGINE_OWNER_REQUIRED
```

### S36

```text
CURRENT_FILE=AORebirth/Server/ZoneEngine/Core/Mail/MailRuntimeService.cs:109
CURRENT_CLASS_OR_METHOD=MailRuntimeService.TrySendMail
CURRENT_CHARACTER_DAO_CALL=GetByCharName(recipient)
TARGET_ICHARACTERDAO_OPERATION=LoadByName
INPUT_MAPPING=Caller already computes (Recipient ?? empty).Trim(); pass that result unchanged, no DAO trim.
RETURN_MAPPING=Id->CharacterId and persisted Name feed in-memory mail recipient identity/queue.
CURRENT_NOT_FOUND_BEHAVIOR=Sets Unknown mail recipient failure and returns false.
CURRENT_ERROR_BEHAVIOR=Provider/mapping failures propagate; no local fallback.
TARGET_ERROR_BEHAVIOR=DAO propagates; retain the stated caller fallback outside the DAO.
PACKET_OR_RUNTIME_RISK=Lookup occurs before attachment removal/cash debit. In-memory mail, inventory/stat updates and notifications stay runtime.
REQUIRES_OTHER_DAO=Inventory/stats in mail workflow, not directory primitive
REQUIRES_ENGINE_OWNER=YES, Zone/mail owner
CUTOVER_STATUS=ZONEENGINE_OWNER_REQUIRED
```

### S37

```text
CURRENT_FILE=AORebirth/Server/ZoneEngine/Core/Mail/MailRuntimeService.cs:547
CURRENT_CLASS_OR_METHOD=MailRuntimeService.TryReturnToSender primary lookup
CURRENT_CHARACTER_DAO_CALL=GetByCharName(originalSender)
TARGET_ICHARACTERDAO_OPERATION=LoadByName
INPUT_MAPPING=Existing caller-trimmed SenderName, after empty/self/system-sender checks.
RETURN_MAPPING=Found CharacterId/Name; missing proceeds to ID fallback when SenderId!=0.
CURRENT_NOT_FOUND_BEHAVIOR=ID fallback, then online-player fallback, then original-name queue.
CURRENT_ERROR_BEHAVIOR=Provider failure propagates; fallback is only on missing row.
TARGET_ERROR_BEHAVIOR=DAO propagates; retain the stated caller fallback outside the DAO.
PACKET_OR_RUNTIME_RISK=Preserve lookup/fallback order before RemoveMail; do not turn missing character into mail deletion failure.
REQUIRES_OTHER_DAO=In-memory mail/runtime online resolver
REQUIRES_ENGINE_OWNER=YES, Zone/mail owner
CUTOVER_STATUS=ZONEENGINE_OWNER_REQUIRED
```

### S38

```text
CURRENT_FILE=AORebirth/Server/ZoneEngine/Core/Mail/MailRuntimeService.cs:551
CURRENT_CLASS_OR_METHOD=MailRuntimeService.TryReturnToSender ID fallback
CURRENT_CHARACTER_DAO_CALL=Get(mail.SenderId)
TARGET_ICHARACTERDAO_OPERATION=LoadById
INPUT_MAPPING=Only after missing name row and nonzero SenderId; same signed ID.
RETURN_MAPPING=Found CharacterId/Name; missing uses online-player/original-name fallback.
CURRENT_NOT_FOUND_BEHAVIOR=Online player if found; otherwise keep original name and stored SenderId.
CURRENT_ERROR_BEHAVIOR=Provider failure propagates, not fallback.
TARGET_ERROR_BEHAVIOR=DAO propagates; retain the stated caller fallback outside the DAO.
PACKET_OR_RUNTIME_RISK=Duplicate-name selection and runtime name queue remain unchanged; no mail persistence added here.
REQUIRES_OTHER_DAO=In-memory mail/runtime online resolver
REQUIRES_ENGINE_OWNER=YES, Zone/mail owner
CUTOVER_STATUS=ZONEENGINE_OWNER_REQUIRED
```

### S39

```text
CURRENT_FILE=AORebirth/Server/ZoneEngine/Core/PacketHandlers/ClientConnected.cs:656
CURRENT_CLASS_OR_METHOD=ClientConnected.InitializeActionableState
CURRENT_CHARACTER_DAO_CALL=Get(character.Identity.Instance)
TARGET_ICHARACTERDAO_OPERATION=LoadById
INPUT_MAPPING=Runtime character ID unchanged.
RETURN_MAPPING=AccountUsername used for separate account lookup; retain account-derived GM/expansion writes.
CURRENT_NOT_FOUND_BEHAVIOR=Logs missing character, writes/sends expansion default2, returns.
CURRENT_ERROR_BEHAVIOR=Provider/mapping failures propagate; no local fallback.
TARGET_ERROR_BEHAVIOR=DAO propagates; retain the stated caller fallback outside the DAO.
PACKET_OR_RUNTIME_RISK=Missing account also follows existing expansion default2; preserve preceding actionable-state/stat writes and packet order.
REQUIRES_OTHER_DAO=IAccountDao; existing stats persistence
REQUIRES_ENGINE_OWNER=YES, Zone initialization owner
CUTOVER_STATUS=ZONEENGINE_OWNER_REQUIRED
```

### S40

```text
CURRENT_FILE=AORebirth/Server/ZoneEngine/Core/PacketHandlers/OrgClient.cs:484
CURRENT_CLASS_OR_METHOD=OrgClient.Read case13 kick lookup
CURRENT_CHARACTER_DAO_CALL=GetByCharName(message.CommandArgs)
TARGET_ICHARACTERDAO_OPERATION=LoadByName
INPUT_MAPPING=CommandArgs unchanged.
RETURN_MAPPING=CharacterId identifies runtime target.
CURRENT_NOT_FOUND_BEHAVIOR=Sends no-character chat message and breaks.
CURRENT_ERROR_BEHAVIOR=Provider/mapping failures propagate; no local fallback.
TARGET_ERROR_BEHAVIOR=DAO propagates; retain the stated caller fallback outside the DAO.
PACKET_OR_RUNTIME_RISK=Organization/rank checks and stat mutations remain outside DAO. Do not fix surrounding kick semantics in this foundation.
REQUIRES_OTHER_DAO=Organization/runtime stats composition
REQUIRES_ENGINE_OWNER=YES, Zone/organization owner
CUTOVER_STATUS=ZONEENGINE_OWNER_REQUIRED
```

### S41

```text
CURRENT_FILE=AORebirth/Server/ZoneEngine/Core/PacketHandlers/OrgClient.cs:517
CURRENT_CLASS_OR_METHOD=OrgClient.Read case13 online branch
CURRENT_CHARACTER_DAO_CALL=IsOnline(client.Controller.Character.Identity.Instance)
TARGET_ICHARACTERDAO_OPERATION=LoadById
INPUT_MAPPING=Preserve actual initiator ID, not kickee ID; discrepancy is pre-existing.
RETURN_MAPPING=row == null ? 0 : row.Online ?? 0; only0 takes offline TODO branch.
CURRENT_NOT_FOUND_BEHAVIOR=Zero -> breaks; no offline kick implemented.
CURRENT_ERROR_BEHAVIOR=Provider/mapping failures propagate; no local fallback.
TARGET_ERROR_BEHAVIOR=DAO propagates; retain the stated caller fallback outside the DAO.
PACKET_OR_RUNTIME_RISK=Apparent wrong-subject query is a runtime bug candidate requiring separate acceptance, not a DAO correction.
REQUIRES_OTHER_DAO=Organization/runtime stats composition
REQUIRES_ENGINE_OWNER=YES, Zone/organization owner
CUTOVER_STATUS=ZONEENGINE_OWNER_REQUIRED
```

### S42

```text
CURRENT_FILE=AORebirth/Server/ZoneEngine/Core/PacketHandlers/OrgClient.cs:1040
CURRENT_CLASS_OR_METHOD=OrgClient.ResolveLeaderName
CURRENT_CHARACTER_DAO_CALL=GetCharacterNameById(leaderId)
TARGET_ICHARACTERDAO_OPERATION=LoadById
INPUT_MAPPING=Only positive leaderId branch, unchanged.
RETURN_MAPPING=row == null ? empty : row.Name ?? empty; nonempty wins.
CURRENT_NOT_FOUND_BEHAVIOR=Fallback to targetCharacter.Name, then captured org leader name for1970177, else empty.
CURRENT_ERROR_BEHAVIOR=Caught and suppressed; uses same fallback chain.
TARGET_ERROR_BEHAVIOR=DAO propagates; retain the stated caller fallback outside the DAO.
PACKET_OR_RUNTIME_RISK=Preserve captured display fallback and separate organization resolution; no organization fields in DTO.
REQUIRES_OTHER_DAO=OrganizationDao for surrounding org info
REQUIRES_ENGINE_OWNER=YES, Zone/organization owner
CUTOVER_STATUS=ZONEENGINE_OWNER_REQUIRED
```

### S43

```text
CURRENT_FILE=AORebirth/Server/ZoneEngine/Core/ZoneClient.cs:317
CURRENT_CLASS_OR_METHOD=ZoneClient.CreateCharacter
CURRENT_CHARACTER_DAO_CALL=Get(charId)
TARGET_ICHARACTERDAO_OPERATION=NONE: aggregate hydration/reconnect excluded
INPUT_MAPPING=charId unchanged.
RETURN_MAPPING=Full DBCharacter used for position/heading, dungeon lease decisions and possible full save.
CURRENT_NOT_FOUND_BEHAVIOR=Throws 'Character ... not found' exception.
CURRENT_ERROR_BEHAVIOR=Provider/mapping failures propagate; no local fallback.
TARGET_ERROR_BEHAVIOR=No operation in this foundation; legacy path retained.
PACKET_OR_RUNTIME_RISK=Narrow DTO cannot feed full hydration or Nascence rehydrate helper; no runtime adapter in this task.
REQUIRES_OTHER_DAO=Stats/inventory/mission/nano/perk and runtime lease composition
REQUIRES_ENGINE_OWNER=YES, Zone aggregate owner
CUTOVER_STATUS=DEFERRED_TO_CHARACTER_AGGREGATE
```

### S44

```text
CURRENT_FILE=AORebirth/Server/ZoneEngine/Core/ZoneClient.cs:328
CURRENT_CLASS_OR_METHOD=ZoneClient.CreateCharacter legacy dungeon conversion
CURRENT_CHARACTER_DAO_CALL=Save(character)
TARGET_ICHARACTERDAO_OPERATION=NONE: full save excluded
INPUT_MAPPING=Mutated DBCharacter with allocated dungeon Playfield.
RETURN_MAPPING=Legacy affected count ignored.
CURRENT_NOT_FOUND_BEHAVIOR=Zero affected rows ignored.
CURRENT_ERROR_BEHAVIOR=Provider/mapping failures propagate; no local fallback.
TARGET_ERROR_BEHAVIOR=No operation in this foundation; legacy path retained.
PACKET_OR_RUNTIME_RISK=Keep legacy dungeon conversion and full snapshot persistence; no location or lease logic moved.
REQUIRES_OTHER_DAO=Dungeon lease/runtime allocation
REQUIRES_ENGINE_OWNER=YES, Zone aggregate owner
CUTOVER_STATUS=DEFERRED_TO_CHARACTER_AGGREGATE
```

### S45

```text
CURRENT_FILE=AORebirth/Server/ZoneEngine/Core/ZoneClient.cs:329
CURRENT_CLASS_OR_METHOD=ZoneClient.CreateCharacter legacy dungeon playfield save
CURRENT_CHARACTER_DAO_CALL=SetPlayfield(...)
TARGET_ICHARACTERDAO_OPERATION=NONE: location save excluded
INPUT_MAPPING=Same characterId/pfType/allocated playfield.
RETURN_MAPPING=Legacy void; count ignored.
CURRENT_NOT_FOUND_BEHAVIOR=Zero affected rows ignored.
CURRENT_ERROR_BEHAVIOR=Provider/mapping failures propagate; no local fallback.
TARGET_ERROR_BEHAVIOR=No operation in this foundation; legacy path retained.
PACKET_OR_RUNTIME_RISK=Separate playfield write follows full save and precedes subsequent hydration; preserve ordering.
REQUIRES_OTHER_DAO=Dungeon lease/runtime allocation
REQUIRES_ENGINE_OWNER=YES, Zone aggregate owner
CUTOVER_STATUS=DEFERRED_TO_CHARACTER_AGGREGATE
```

### L01

```text
CURRENT_FILE=AORebirth/Libraries/Source/AORebirth.Core/Encryption/LoginEncryption.cs:159
CURRENT_CLASS_OR_METHOD=LoginEncryption.IsCharacterOnAccount
CURRENT_CHARACTER_DAO_CALL=IsCharacterOnAccount(UserName, CharacterID)
TARGET_ICHARACTERDAO_OPERATION=IsOwnedByAccount
INPUT_MAPPING=UserName unchanged; CharacterID remains uint.
RETURN_MAPPING=Boolean unchanged.
CURRENT_NOT_FOUND_BEHAVIOR=False; exactly one matching account+ID required.
CURRENT_ERROR_BEHAVIOR=Provider/mapping failures propagate; no local fallback.
TARGET_ERROR_BEHAVIOR=DAO propagates; retain the stated caller fallback outside the DAO.
PACKET_OR_RUNTIME_RISK=Database ownership read is not session authorization; authoritative authenticated name remains caller responsibility.
REQUIRES_OTHER_DAO=NO
REQUIRES_ENGINE_OWNER=YES, authentication integration owner
CUTOVER_STATUS=SAFE_CHARACTER_DAO_CUTOVER
```

### L02

```text
CURRENT_FILE=AORebirth/Libraries/Source/AORebirth.Core/NPCHandler/NonPlayerCharacterHandler.cs:231
CURRENT_CLASS_OR_METHOD=NonPlayerCharacterHandler.InstantiateMobSpawn
CURRENT_CHARACTER_DAO_CALL=Get(mob.Id)
TARGET_ICHARACTERDAO_OPERATION=LoadById
INPUT_MAPPING=Preserve mob.Id<1000000 short circuit and mob.Id.
RETURN_MAPPING=Projection nonnull means player-identity collision.
CURRENT_NOT_FOUND_BEHAVIOR=Null permits existing NPC spawn.
CURRENT_ERROR_BEHAVIOR=Provider/mapping failures propagate; no local fallback.
TARGET_ERROR_BEHAVIOR=DAO propagates; retain the stated caller fallback outside the DAO.
PACKET_OR_RUNTIME_RISK=Never treat failed read as absent; retain NPC identity/template policy.
REQUIRES_OTHER_DAO=NO for primitive; surrounding NPC runtime unchanged
REQUIRES_ENGINE_OWNER=YES, NPC integration owner
CUTOVER_STATUS=SAFE_CHARACTER_DAO_CUTOVER
```

### L03

```text
CURRENT_FILE=AORebirth/Libraries/Source/AORebirth.Database/Dao/LoginDataDao.cs:64
CURRENT_CLASS_OR_METHOD=LoginDataDao.GetByCharacterId
CURRENT_CHARACTER_DAO_CALL=Get(charId)
TARGET_ICHARACTERDAO_OPERATION=LoadById equivalent primitive; complete account path stays IAccountDao.LoadByCharacterId
INPUT_MAPPING=charId unchanged.
RETURN_MAPPING=Missing row or null/empty AccountUsername -> null account; otherwise account-name lookup.
CURRENT_NOT_FOUND_BEHAVIOR=Character/name/account absence all collapse to null.
CURRENT_ERROR_BEHAVIOR=Provider/mapping failures propagate; no local fallback.
TARGET_ERROR_BEHAVIOR=DAO propagates; retain the stated caller fallback outside the DAO.
PACKET_OR_RUNTIME_RISK=Do not wire DAOs to each other; approved MySqlAccountDao bounded direct character lookup stays independent.
REQUIRES_OTHER_DAO=IAccountDao for complete outcome
REQUIRES_ENGINE_OWNER=YES, account-consumer integration owner
CUTOVER_STATUS=SAFE_CHARACTER_DAO_CUTOVER
```

### L04

```text
CURRENT_FILE=AORebirth/Libraries/Source/AORebirth.Database/Dao/LoginDataDao.cs:111
CURRENT_CLASS_OR_METHOD=LoginDataDao.LogoffChars enumeration
CURRENT_CHARACTER_DAO_CALL=GetAllForUser(user)
TARGET_ICHARACTERDAO_OPERATION=ListForAccount
INPUT_MAPPING=user unchanged.
RETURN_MAPPING=Enumerate detached CharacterId values only.
CURRENT_NOT_FOUND_BEHAVIOR=Empty collection -> no writes.
CURRENT_ERROR_BEHAVIOR=Read failures propagate before per-row writes.
TARGET_ERROR_BEHAVIOR=DAO propagates; retain the stated caller fallback outside the DAO.
PACKET_OR_RUNTIME_RISK=Force-logoff enumeration and per-row commits are not atomic; policy bypasses ownership guard today.
REQUIRES_OTHER_DAO=NO
REQUIRES_ENGINE_OWNER=YES, Login admin/Zone ownership owner
CUTOVER_STATUS=ZONEENGINE_OWNER_REQUIRED
```

### L05

```text
CURRENT_FILE=AORebirth/Libraries/Source/AORebirth.Database/Dao/LoginDataDao.cs:114
CURRENT_CLASS_OR_METHOD=LoginDataDao.LogoffChars write
CURRENT_CHARACTER_DAO_CALL=SetOffline(character.Id)
TARGET_ICHARACTERDAO_OPERATION=MarkOffline
INPUT_MAPPING=Each enumerated CharacterId unchanged.
RETURN_MAPPING=Discard raw affected count.
CURRENT_NOT_FOUND_BEHAVIOR=Missing row count0 ignored.
CURRENT_ERROR_BEHAVIOR=Failure propagates; earlier rows may already be committed; legacy rollback can replace primary.
TARGET_ERROR_BEHAVIOR=DAO preserves primary failure and records secondary rollback failure; uncertain commit requires reconciliation. Caller policy remains unchanged.
PACKET_OR_RUNTIME_RISK=No implicit batch transaction, ownership lock or automatic all-account logout API.
REQUIRES_OTHER_DAO=NO
REQUIRES_ENGINE_OWNER=YES, Login admin/Zone ownership owner
CUTOVER_STATUS=ZONEENGINE_OWNER_REQUIRED
```

### L06

```text
CURRENT_FILE=AORebirth/Libraries/Source/AORebirth.Database/Dao/CharacterOnlineOwnershipGuard.cs:34
CURRENT_CLASS_OR_METHOD=AcquireZoneOwnership existing lease branch
CURRENT_CHARACTER_DAO_CALL=SetOnline(characterId)
TARGET_ICHARACTERDAO_OPERATION=MarkOnline
INPUT_MAPPING=Validated positive characterId unchanged.
RETURN_MAPPING=Ignore count; preserve reference-count increment before call.
CURRENT_NOT_FOUND_BEHAVIOR=Count0 ignored; lease reference still returned.
CURRENT_ERROR_BEHAVIOR=Failure propagates, existing ReferenceCount remains incremented.
TARGET_ERROR_BEHAVIOR=DAO preserves primary failure and records secondary rollback failure; uncertain commit requires reconciliation. Caller policy remains unchanged.
PACKET_OR_RUNTIME_RISK=Keep Sync/dictionary/reference counts/file locks outside DAO; increment-on-failure is separate runtime review.
REQUIRES_OTHER_DAO=NO
REQUIRES_ENGINE_OWNER=YES, Zone ownership owner
CUTOVER_STATUS=ZONEENGINE_OWNER_REQUIRED
```

### L07

```text
CURRENT_FILE=AORebirth/Libraries/Source/AORebirth.Database/Dao/CharacterOnlineOwnershipGuard.cs:60
CURRENT_CLASS_OR_METHOD=AcquireZoneOwnership new lease branch
CURRENT_CHARACTER_DAO_CALL=SetOnline(characterId)
TARGET_ICHARACTERDAO_OPERATION=MarkOnline
INPUT_MAPPING=Positive ID after byte-lock acquisition.
RETURN_MAPPING=Ignore count; create held lease only after write success.
CURRENT_NOT_FOUND_BEHAVIOR=Count0 ignored; missing row still receives lease.
CURRENT_ERROR_BEHAVIOR=Catch releases stream then rethrows; release failure can replace original.
TARGET_ERROR_BEHAVIOR=DAO preserves primary failure and records secondary rollback failure; uncertain commit requires reconciliation. Caller policy remains unchanged.
PACKET_OR_RUNTIME_RISK=Keep5s acquisition timeout,25ms retry, file-lock lifetime and post-write lease insertion outside DAO.
REQUIRES_OTHER_DAO=NO
REQUIRES_ENGINE_OWNER=YES, Zone ownership owner
CUTOVER_STATUS=ZONEENGINE_OWNER_REQUIRED
```

### L08

```text
CURRENT_FILE=AORebirth/Libraries/Source/AORebirth.Database/Dao/CharacterOnlineOwnershipGuard.cs:83
CURRENT_CLASS_OR_METHOD=TryClearLoginOwnership
CURRENT_CHARACTER_DAO_CALL=SetOffline(characterId)
TARGET_ICHARACTERDAO_OPERATION=MarkOffline
INPUT_MAPPING=Positive ID only after successful TryAcquire.
RETURN_MAPPING=Ignore count and return Cleared; lock unavailable returns ZoneOwned without SQL.
CURRENT_NOT_FOUND_BEHAVIOR=Count0 still maps to Cleared.
CURRENT_ERROR_BEHAVIOR=Write error propagates; finally releases stream; release failure may replace original.
TARGET_ERROR_BEHAVIOR=DAO preserves primary failure and records secondary rollback failure; uncertain commit requires reconciliation. Caller policy remains unchanged.
PACKET_OR_RUNTIME_RISK=Preserve guard's no-write-on-ZoneOwned gate. DAO does not clear or acquire runtime ownership.
REQUIRES_OTHER_DAO=NO
REQUIRES_ENGINE_OWNER=YES, Login/Zone ownership owner
CUTOVER_STATUS=ZONEENGINE_OWNER_REQUIRED
```


## 17. Safe read/online cutover candidates

The18 SAFE entries are S01,S02,S04,S06,S07,S08,S10-S15,S19,S26,S27,L01-L03. They represent pure name/directory/ownership/presence persistence that can be mapped without adding aggregate fields. Preserve each entry's missing/error behavior, packet sequencing and query timing.

No write is labelled automatically safe for runtime replacement: online write sites participate in cross-engine ownership, bot, logout or handoff behavior and remain owner-required. The DAO's tested capability does not authorize changing those runtime policies.

The account-derived L03 path should normally be integrated through the existing IAccountDao.LoadByCharacterId outcome, not by creating DAO-to-DAO calls. L02's primitive is a collision read, but actual NPC-runtime integration still requires its owner.

## 18. ZoneEngine-owner-required sites

The22 owner-required direct entries are S03,S09,S16-S18,S28-S31,S35-S42,L04-L08. They include Login/Chat writers because their flags interact with Zone ownership.

The guard's outer callers, existing stale-recovery database store and ZoneEngine_New direct hydration repository also require engine-owner approval; they are not additional direct CharacterDao invocations. Online-state primitive replacement must keep existing lease acquisition/release, state transitions, cleanup ordering and failure handling.

Specific unresolved runtime hazards are recorded, not repaired:

- OrgClient case13 checks the initiator's online status although the surrounding operation targets the kickee.
- Existing held-lease acquisition increments a reference count before its online write and leaves it incremented if that write fails.
- Current account force-logoff bypasses ownership guard and commits each character separately.
- Chat disconnect writes offline for any nonzero character ID, without a bot-only guard.
- Full character saves write Online=1, while later lifecycle cleanup writes0; reordering these operations changes behavior.

## 19. Character aggregate exclusions and legacy helper coverage

The10 direct aggregate entries are S22-S25,S32-S34,S43-S45. They remain on DBCharacter and legacy CharacterDao because they create, hydrate or save data beyond the seven-field directory.

| Legacy surface | Character foundation compatibility or deferral |
|---|---|
| Get | LoadById only for directory/presence callers; full hydration/save inputs remain aggregate |
| GetAllForUser | ListForAccount; internally invokes inherited GetAll with Username predicate |
| GetByCharName | LoadByName; internally GetAll + FirstOrDefault, no ordering |
| GetCharacterNameById | LoadById + caller empty-string compatibility |
| ExistsByName | LoadByName != null; duplicates still count as existing |
| IsCharacterOnAccount | IsOwnedByAccount, exact-one matching ID/account |
| GetLoggedInCharacters | ListLoggedIn; internally GetWhere(Online=1), buffered |
| IsOnline | LoadById; caller maps missing/NULL to0 when retaining legacy behavior |
| SetOnline / SetOffline | MarkOnline / MarkOffline behind existing runtime policy; raw affected count may be discarded |
| SetPlayfield | DEFERRED_TO_CHARACTER_AGGREGATE; pfType currently ignored, only Playfield/Id saved |
| AddBuddy / RemoveBuddy | DEFERRED_TO_CHAT_SOCIAL_DAO; Get + CSV mutation + restricted BuddyList/Id Save |
| Delete / DeleteForUser / DeleteOwnedData | DEFERRED_TO_CHARACTER_DELETE_TRANSACTION |
| Inherited GetAll / GetWhere | Used by existing helpers; no arbitrary public replacement query API |
| Inherited Add | Actual creation call S25; aggregate excluded |
| Inherited Save | Full/selected saves S23,S33,S44; helper saves remain excluded or explicit online primitives |
| Inherited Delete | Existing character delete wraps base.Delete in shared cross-table transaction; excluded |
| Inherited Count | Searched; no additional active external CharacterDao Count call found |
| CharacterOnlineOwnershipGuard | Runtime/file-lock owner retained; database writes only can later use ICharacterDao |
| AdoNetStaleOnlineRecoveryStore | Database stages represented by RecoverStaleOnline; runtime exclusivity retained |

The helper definitions/internal calls are not added to the external53. BuddyListDao is commented legacy source, not a reason to add a second social persistence owner. No production external AddBuddy/RemoveBuddy invocation was found in the audited surface; their declared functionality is still explicitly deferred.

## 20. Character deletion deferral

`CHARACTER_DELETE_STATUS=DEFERRED_HIGH_RISK_CROSS_AGGREGATE_TRANSACTION`.

S20 and S21 are the two external deletion entry points. DeleteOwnedData is an internal stage, not a separate safe API. The existing transaction covers character removal, organization leader cleanup, organization/member stats, items/instanced items, received messages, character stats, mission flags/state/progress/observations/reward ledger, timers, active nanos, meshes, uploaded nanos and perks.

Do not copy this transaction into the initial character DAO, split it into independent commits or add DeleteCharacter/DeleteForAccount. Existing ownership validation, affected-row checks, caller true/false/error behavior and all cross-table rollback semantics need a separately approved deletion design.

## 21. Buddy and social deferral

`BUDDY_PERSISTENCE_STATUS=DEFERRED_TO_CHAT_SOCIAL_DAO`.

S05 consumes BuddyList CSV through DBCharacter.GetBuddiesIds. AddBuddy/RemoveBuddy load a row, mutate CSV and save BuddyList/Id; their concurrency and parsing behavior remain legacy. Received-message history is also social persistence, except where its deletion participates in the larger character-delete transaction.

The initial DTO carries none of these fields. No buddy list, recent-message or mail persistence API is introduced. The mail directory lookups S36-S38 remain pure lookups conceptually; their in-memory queue, item/credit operations, expiry and notification policy are not migrated.

## 22. Stats composition dependency

`CHARACTER_STATS_STATUS=DEFERRED_TO_CHARACTER_STATS_DAO`.

Login's character list currently composes directory identity/name/Playfield with StatDao values:54 level,4 breed,59 gender,60 profession. Chat's account-character packet separately reads level54 and online state. Chat CharacterBase composes clan stat5 and organization data after assigning name fields.

These stat fields do not exist in the character directory table projection and must not be invented as DTO properties. Future packet construction may compose ICharacterDao with a separately approved ICharacterStatsDao. Missing-stat exceptions, defaults already in individual runtime callers, packet order and parallel-array alignment must be accepted explicitly. No new stats DAO or runtime stat behavior is part of this branch.

## 23. Location and save deferral

`CHARACTER_LOCATION_STATUS=DEFERRED_TO_CHARACTER_AGGREGATE`.

Playfield is exposed read-only because directory/LFT/Web callers consume it. That does not authorize writing it. SetPlayfield, X/Y/Z, heading, reconnect hydration, legacy dungeon allocation and Nascence lease rehydration stay with the aggregate/runtime owners.

NewZone's current direct SQL projection includes coordinates/heading and its own missing/error semantics; LoadById is not a replacement for that repository. No SaveLocation, SetPlayfield, SaveCharacter, profile update or aggregate DTO is provided.

## 24. Known baseline failures and validation evidence

Four broader failures were freshly reproduced by the exact same commands on the clean detached e3acc4c baseline and the working branch. Evidence is under `build-verify/character-parallel/{base,work}/`; each JSON records command, worktree, start SHA, exit code, normalized diagnostics, raw-log path and SHA256. The work JSON's SHA identifies its baseline HEAD, not a claim that uncommitted source is the committed baseline.

| Gate | Exact command | Baseline / working outcome |
|---|---|---|
| Default repository guard | `call Tools\\run_dao_architecture_guard.cmd` | Same two NEW_VIOLATION paths; FAIL |
| Full mission wrapper | `call Tools\\run_mission_dao_validation.cmd` | Same CS2001 missing ItemType source; FAIL before isolated mission assertions |
| AOtomation compatibility | `call Tools\\run_aotomation_messaging_tests.cmd` | Identical normalized18-diagnostic arrays; FAIL |
| Whole source inventory | `dotnet run --project LinuxBuild/Tools/SourceInventoryGuard/SourceInventoryGuard.csproj -- --repository-root . --manifest LinuxBuild/source-inventory/inventory.json --check` | Same stale Enums inventory; FAIL |

Exact default-guard diagnostics (both `dao-guard.json` files; both raw logs also have the same SHA256 `37b04698f724409dd6398578e9fb448c4a5b09713a0e34bf403c0d2adbd7ef32`):

```text
NEW_VIOLATION=AORebirth/Server/ZoneEngine_New/Core/Data/MySqlCharacterRepository.cs
NEW_VIOLATION=AORebirth/Server/ZoneEngine_New/Core/Data/MySqlStatRepository.cs
```

Exact normalized mission and inventory diagnostics (identical between their paired JSON files; `<ROOT>` is the evidence collector's worktree-path normalization):

```text
CSC : error CS2001: Source file '<ROOT>/AORebirth/Libraries/Source/AORebirth.Enums/ItemType.cs' could not be found. [<ROOT>/LinuxBuild/Projects/AORebirth.Enums.Linux.csproj]
STALE: source inventory does not match <ROOT>/AORebirth/Libraries/Source/AORebirth.Enums/AORebirth.Enums.csproj
```

The AOtomation18 diagnostics are17 instances of this exact CS1061 body:

```text
'CapturedEnemySpecialAttackWeaponPacketFixture' does not contain a definition for 'AggDef' and no accessible extension method 'AggDef' accepting a first argument of type 'CapturedEnemySpecialAttackWeaponPacketFixture' could be found (are you missing a using directive or an assembly reference?)
```

Their exact source locations, relative to `AORebirth/Libraries/Source/AOtomation/AOtomation.Messaging/src/SmokeLounge.AOtomation.Messaging.Tests/`, are:

- CapturedEnemyCombatGeneratedPacketFixtureTests.cs: (89,49),(194,29).
- CapturedEnemyCombatPacketFactoryTests.cs: (354,39),(506,39),(574,74),(733,43),(822,39),(1019,39),(1106,39),(1203,39),(1290,39),(1380,39),(1468,39),(1936,39),(2034,39).
- OrdinaryEnemyCombatSetupGeneratorTests.cs: (840,33),(1091,33).

The eighteenth diagnostic is `N3RecoveredContractTests.cs(483,25): error CS0117: 'PlayfieldAnarchyFMessage' does not contain a definition for 'Unknown1'`. All18 target SmokeLounge.AOtomation.Messaging.Tests.csproj. Full per-line diagnostics are retained in both compatibility JSONs and logs; their normalized arrays were compared for exact equality, not just equal counts.

Fresh baseline/working PASS gates: Windows solution restore/build, Interfaces build, Database build, generated combat and mission checks, secret scan, LoginAuthenticationValidation build and14/14 execution, AccountBrokerValidation build, UnifiedAccountFlowValidation build and whitespace. Broker/unified executables were not run against unsafe application services.

Current scoped source-inventory write/check for Interfaces and Database: PASS. Whole-inventory failure remains separate and unrepaired. Character scoped guard74 self-checks, account56 self-checks and mission boundary: PASS. Fresh account273 and mission202 regressions pass as detailed in section13; failed fixture startups are retained separately, not counted as passing regressions. Final Database and solution builds also pass on the frozen code (work/databasefinal.json and work/solutionfinal.json).

No unrelated owner failure was repaired, suppressed or excluded. The committed [acceptance evidence](../../Tools/CharacterDaoValidation/acceptance-evidence.json) records the complete aggregate gate manifest, source-tree hashes and normalized failure comparisons.

## 25. Exact future integration files an owner may edit

Section16 gives the exact current callsite file list. A separately approved integration owner may edit those specific current consumer files when performing the corresponding ordered slice; this foundation grants no blanket permission to edit all of them.

Initial candidate groups:

1. Name/identity: Chat CoreClient/Character.cs, CoreClient/CharacterBase.cs, CoreServer/ChatServer.cs, PacketHandlers/PlayerNameLookup.cs; Core/Encryption/LoginEncryption.cs and NPCHandler/NonPlayerCharacterHandler.cs only with their owners.
2. Ownership: Chat PacketHandlers/Authenticate.cs and LoginCharacter.cs; Login Packets/CheckLogin.cs / CoreClient/LoginHandoffLifecycle.cs as coordinating wrappers only when authorized.
3. Directory lists: Login QueryBase/CharacterList.cs, Chat Packets/AccountCharacterList.cs, Web Websites/IndexPHP.cs.
4. Presence reads: the exact S07,S12,S13,S15,S17,S41 sites, preserving each fallback and current target.
5. Writes: existing CharacterOnlineOwnershipGuard.cs, LoginHandoffLifecycle.cs, SelectCharacterHandler.cs, ChatServer.cs/LoginCharacter.cs, PlayerController.cs and Character.cs; assess LoginDataDao.LogoffChars separately.
6. Stale database-store replacement only: ZoneEngine/StaleOnlineRecovery.cs, leaving runtime safeguards/audit formatting in place.

All engine/Core paths above are anchored by their full repository-relative paths in sections15-16. ZoneEngine_New/Core/Data/MySqlCharacterRepository.cs is listed as a separate later aggregate-owner file, not an initial directory cutover candidate. Any composition-root/factory wiring or engine project edit requires a fresh owner-approved integration scope; none is performed now.

## 26. Deliberately unchanged files and ownership boundaries

No file under these exact ownership roots is an implementation target:

```text
AORebirth/Server/ZoneEngine/
AORebirth/Server/ZoneEngine_New/
AORebirth/Server/LoginEngine/
AORebirth/Server/ChatEngine/
AORebirth/Server/WebEngine/
AORebirth/Libraries/Source/AORebirth.Core/
AORebirth/Libraries/Source/AORebirth.Stats/
AORebirth/Libraries/Source/AORebirth.AccountBroker/
AORebirth/Libraries/Source/AORebirth.BotService/
```

All exact current consumer paths enumerated in sections15-16 remain unchanged, including the three read-only inactive copies. Additional exact protected files include:

```text
AORebirth/Libraries/Source/AORebirth.Database/Dao/CharacterDao.cs
AORebirth/Libraries/Source/AORebirth.Database/Entities/DBCharacter.cs
AORebirth/Libraries/Source/AORebirth.Database/Dao/CharacterOnlineOwnershipGuard.cs
AORebirth/Libraries/Source/AORebirth.Database/Dao/LoginDataDao.cs
AORebirth/Server/ZoneEngine/StaleOnlineRecovery.cs
DAO_REFACTOR_AUDIT.md
docs/project/PROJECT_STATE.md
```

IAccountDao/MySqlAccountDao and mission contracts/implementation remain unchanged. Existing isolated validation projects receive only compile-source dependencies needed to compile the actual extended factory; account273 and mission202 assertions are unchanged. Engine project files and engine source inventories, root solution files, schema definitions and deployment scripts are untouched. Exact changed-file hashes and protected-path verification are in the [acceptance evidence](../../Tools/CharacterDaoValidation/acceptance-evidence.json).

## 27. Recommended future cutover order (not performed)

1. Pure name and identity reads, with explicit empty-string/null and projection-failure acceptance.
2. Account ownership checks, retaining authoritative authenticated session input.
3. Account character directory lists, preserving unspecified order and runtime/stat composition.
4. Online-state reads, with explicit missing/NULL/0/1/nonstandard and caller-catch mappings.
5. Online-state writes behind existing ownership guard, accepting diagnostic differences and preserving lifecycle order.
6. Stale-online database-store replacement, keeping all runtime exclusivity/audit safeguards.
7. Separate later aggregate creation/hydration/save/location/deletion work.

Each is a small owner-coordinated integration commit after the parallel runtime work is stable. Do not mark roadmap Phase3 complete, reduce runtime legacy-call counts, enable a new runtime route or deploy from this foundation alone.

## 28. Acceptance commands for a separately authorized integration

Use repository-approved wrappers exactly; run locally on Windows first. Current foundation wrapper commands are:

```bat
Tools\run_character_dao_validation.cmd
Tools\run_account_dao_validation.cmd
Tools\run_mission_dao_validation.cmd
git diff --check
```

Run the character suite twice. Preserve the complete account273 and mission202 gates. The full mission wrapper currently encounters the reproduced baseline CS2001 blocker; section13 records the separately approved complete isolated202-check command and successful paired results. Do not omit assertions or silently substitute a partial suite.

Exact commands freshly exercised for the broader/scoped gates (copied from current evidence JSONs, not rediscovered command guesses):

```bat
"C:\Program Files\Microsoft Visual Studio\18\Community\MSBuild\Current\Bin\MSBuild.exe" "AORebirth/AORebirth.sln" /t:Restore /p:Configuration=Debug /m:1 /nr:false /v:minimal /p:RestorePackagesConfig=true
"C:\Program Files\Microsoft Visual Studio\18\Community\MSBuild\Current\Bin\MSBuild.exe" "AORebirth/AORebirth.sln" /t:Build /p:Configuration=Debug /m:1 /nr:false /v:minimal
"C:\Program Files\Microsoft Visual Studio\18\Community\MSBuild\Current\Bin\MSBuild.exe" "AORebirth/Libraries/Source/AORebirth.Interfaces/AORebirth.Interfaces.csproj" /t:Build /p:Configuration=Debug /m:1 /nr:false /v:minimal
"C:\Program Files\Microsoft Visual Studio\18\Community\MSBuild\Current\Bin\MSBuild.exe" "AORebirth/Libraries/Source/AORebirth.Database/AORebirth.Database.csproj" /t:Build /p:Configuration=Debug /m:1 /nr:false /v:minimal
call Tools\generate_capture_backed_npc_combat_inventory.cmd --check
call Tools\generate_mission_level_graph.cmd --check
call Tools\scan_secrets.cmd
"C:\Program Files\Microsoft Visual Studio\18\Community\MSBuild\Current\Bin\MSBuild.exe" "Tools/LoginAuthenticationValidation/LoginAuthenticationValidation.csproj" /t:Build /p:Configuration=Debug /m:1 /nr:false /v:minimal
Tools\LoginAuthenticationValidation\bin\Debug\LoginAuthenticationValidation.exe
"C:\Program Files\Microsoft Visual Studio\18\Community\MSBuild\Current\Bin\MSBuild.exe" "Tools/AccountBrokerValidation/AccountBrokerValidation.csproj" /t:Build /p:Configuration=Debug /m:1 /nr:false /v:minimal
"C:\Program Files\Microsoft Visual Studio\18\Community\MSBuild\Current\Bin\MSBuild.exe" "Tools/UnifiedAccountFlowValidation/UnifiedAccountFlowValidation.csproj" /t:Build /p:Configuration=Debug /m:1 /nr:false /v:minimal
call Tools\run_dao_architecture_guard.cmd --account-persistence-only
call Tools\run_dao_architecture_guard.cmd --mission-persistence-only
call Tools\run_dao_architecture_guard.cmd --character-persistence-only
dotnet run --project LinuxBuild/Tools/SourceInventoryGuard/SourceInventoryGuard.csproj -- --repository-root . --legacy-project AORebirth/Libraries/Source/AORebirth.Interfaces/AORebirth.Interfaces.csproj --output LinuxBuild/source-inventory/AORebirth.Interfaces.CompileItems.props --check
dotnet run --project LinuxBuild/Tools/SourceInventoryGuard/SourceInventoryGuard.csproj -- --repository-root . --legacy-project AORebirth/Libraries/Source/AORebirth.Database/AORebirth.Database.csproj --output LinuxBuild/source-inventory/AORebirth.Database.CompileItems.props --check
```

The failing default guard, full mission wrapper, compatibility and whole-inventory commands are given verbatim in section24 so a future owner can reproduce the same baseline comparison. The complete governed command/evidence inventory is also linked in acceptance-evidence.json. The new source-isolated character runner links unchanged stale/handoff/hydration tests; assertion labels and results are in section12. No engine startup command is an acceptance step here.

A future runtime integration additionally needs owner-approved packet/handoff/reconnect/logout/bot/list/online-status acceptance that exercises the specific changed callers. Do not launch the AO client, engine hosts, application database cleanup or deployment as part of these foundation tests. Live gameplay validation remains a separate user/owner action.

## 29. Remaining risks and completion markers

Remaining risks are explicit: no runtime integration; owner-coordinated online transitions; aggregate projection differences; duplicate-name selection with unspecified ordering; MySQL-only support; upstream Connector failures before return; provider affected-row modes; concurrent writers after verification; commit acknowledgement uncertainty; and intentional new rollback/disposal diagnostics. Existing incorrect runtime target choices or ownership reference-count behaviors are not silently repaired.

Report generation encountered one read-only cmd search parsing error for a quoted token ending in a space; no files changed through that command. Subsequent bounded reads used shell-safe single-token patterns. Large consumer reads were completed through overlapping targeted reads rather than treating truncated output as complete.

Final persistence-foundation acceptance is complete. Runtime cutover is still deferred; completion flags do not erase the four reproduced broader baseline failures:

```text
START_SHA=e3acc4c58132809fd67bd2fe8aa58939109fe0dc
END_SHA=RESOLVE_COMMIT_CONTAINING_THIS_REPORT
BRANCH=codex/character-read-online-dao-parallel-foundation
PRIMARY_WORKTREE_PRESERVED=YES
ZONEENGINE_FILES_CHANGED=NO
ZONEENGINE_NEW_FILES_CHANGED=NO
LOGINENGINE_FILES_CHANGED=NO
CHATENGINE_FILES_CHANGED=NO
WEBENGINE_FILES_CHANGED=NO
CORE_FILES_CHANGED=NO
STATS_FILES_CHANGED=NO
LEGACY_CHARACTER_DAO_CHANGED=NO
CHARACTER_ONLINE_OWNERSHIP_GUARD_CHANGED=NO
CHARACTER_DAO_FOUNDATION_COMPLETE=YES
CHARACTER_DAO_SAFE_SURFACE_READY=YES
CHARACTER_DAO_RUNTIME_INTEGRATED=NO
MYSQL_CHARACTER_IMPLEMENTATION=YES
DATABASE_DAO_FACTORY_UPDATED=YES
CHARACTER_IDENTITY_READS_READY=YES
CHARACTER_ACCOUNT_LIST_READS_READY=YES
CHARACTER_OWNERSHIP_READS_READY=YES
CHARACTER_ONLINE_READS_READY=YES
CHARACTER_ONLINE_WRITES_READY=YES
STALE_ONLINE_DATABASE_RECOVERY_READY=YES
CHARACTER_DELETE_STATUS=DEFERRED_HIGH_RISK_CROSS_AGGREGATE_TRANSACTION
BUDDY_PERSISTENCE_STATUS=DEFERRED_TO_CHAT_SOCIAL_DAO
CHARACTER_LOCATION_STATUS=DEFERRED_TO_CHARACTER_AGGREGATE
CHARACTER_STATS_STATUS=DEFERRED_TO_CHARACTER_STATS_DAO
CHARACTER_MYSQL_TESTS=529 PASS twice
ACCOUNT_DAO_REGRESSION_TESTS=273 PASS
MISSION_DAO_REGRESSION_TESTS=202 PASS
DAO_GUARD=PASS
DAO_GUARD_SCOPE=CHARACTER_ACCOUNT_AND_MISSION
DATABASE_SCHEMA_CHANGED=NO
RUNTIME_CHARACTER_BEHAVIOR_CHANGED=NO
RUNTIME_LOGIN_BEHAVIOR_CHANGED=NO
RUNTIME_MISSION_LOGIC_CHANGED=NO
PACKET_BEHAVIOR_CHANGED=NO
LIVE_DEPLOYMENT_PERFORMED=NO
DEFERRED_CHARACTER_CALL_SITES=53
COMMIT=RESOLVE_COMMIT_CONTAINING_THIS_REPORT
```
