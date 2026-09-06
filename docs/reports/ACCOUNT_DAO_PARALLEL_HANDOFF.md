# Legacy Game-Account DAO Parallel Handoff

Status: account persistence foundation accepted: 273 disposable-MySQL checks PASS twice; existing mission 202 checks PASS; account/mission scoped guards PASS. Runtime cutover, schema changes and deployment remain unauthorized. Four broader baseline failures are explicitly retained below; this is not full-runtime/Linux acceptance.

## 1. Provenance and isolated ownership

```text
START_SHA=522cbf3a618d859efce62562d7c9e227bdcb4309
SOURCE_BRANCH=codex/mission-dao-parallel-ready
BRANCH=codex/account-dao-parallel-foundation
WORKTREE=C:\Users\Mike\Documents\AORebirth\tools-temp\worktree-snapshots\account-dao-parallel-foundation
END_SHA=RESOLVE_COMMIT_CONTAINING_THIS_REPORT
COMMIT=RESOLVE_COMMIT_CONTAINING_THIS_REPORT
```

The final commit containing this handoff is obtained with `git rev-parse HEAD` in the isolated worktree after the commit. A document cannot embed its own final Git commit hash without changing that hash. The completion report supplies the final SHA. The branch was created clean at the exact starting SHA, with a second detached clean baseline at `tools-temp/worktree-snapshots/account-dao-parallel-base`. The primary checkout remains on `master` at `cf1e12b894b1247b34f96f832b217c1cfb828213`, with its pre-existing untracked `quest example from PRK.txt` preserved. The full worktree inventory is recorded in `build-verify/account-parallel/worktree-inventory.txt`; every pre-existing worktree is retained, including the stale/prunable entry. No reset, clean, stash, merge, rebase, worktree removal, primary edit, or live action was performed.

This foundation is deliberately unconsumed by runtime code. Legacy `LoginDataDao` remains unchanged and available. Temporary duplicate persistence implementation is intentional until a separately owned runtime integration and acceptance step; there is no runtime dual-write.

## 2. Scope and architecture

```text
Future runtime/domain consumer
    -> IAccountDao
    -> MySqlAccountDao
    -> existing Connector / MySQL
```

Owned surface: legacy game-account persistence, primarily `login`, plus a read-only character-to-account lookup. Authentication decisions, password hashing, packets, profile validation policy, online state, runtime initialization, and unified-account orchestration remain outside the DAO.

Eight safe operations are implemented; GM mutation, `LogoffChars`, identity provisioning, tokens, external mappings, and bot persistence are excluded. Phase 3 is not complete: none of the 21 existing production `LoginDataDao` calls is replaced by this branch, so the runtime legacy-consumer count does not decrease.

## 3. Interface and DTO contract

Contract source: `AORebirth/Libraries/Source/AORebirth.Interfaces/Persistence/Accounts/IAccountDao.cs`; namespace `AORebirth.Interfaces.Persistence.Accounts`. The four adjacent DTO/result files are `GameAccountData.cs`, `GameAccountAuthenticationData.cs`, `NewGameAccountData.cs`, and `GameAccountLookupResult.cs` (also owns the lookup-status enum). The interface uses sealed neutral data classes, primitives and `DateTime`, compatible with the existing .NET Framework 4.8/C# toolchain.

```csharp
public interface IAccountDao
{
    GameAccountAuthenticationData LoadForAuthentication(string username);
    GameAccountData LoadByUsername(string username);
    GameAccountLookupResult LoadByCharacterId(int characterId);
    long CountRegisteredAccounts();
    bool UsernameExists(string username);
    int CreateGameAccount(NewGameAccountData account);
    int ChangePassword(string username, string passwordHash);
    int SetExpansions(string username, int expansions);
}
```

| Type | Exact data fields / properties |
| --- | --- |
| `GameAccountData` | `int AccountId`, `DateTime CreationDate`, `string Email`, `string FirstName`, `string LastName`, `string Username`, `string PasswordHash`, `int AllowedCharacters`, `int Flags`, `int AccountFlags`, `int Expansions`, `int GmLevel` |
| `GameAccountAuthenticationData` | `int AccountId`, `string Username`, `string PasswordHash`, `int AllowedCharacters`, `int Flags`, `int AccountFlags`, `int Expansions`, `int GmLevel` |
| `NewGameAccountData` | `string Email`, `string FirstName`, `string LastName`, `string Username`, `string PasswordHash`, `int AllowedCharacters`, `int Flags`, `int AccountFlags`, `int Expansions`, `int GmLevel`; no ID or timestamp input |
| `GameAccountLookupResult` | Read-only `Status`, `CharacterUsername`, and `Account` properties; account payload is `GameAccountData` |
| `GameAccountLookupStatus` | `CharacterNotFound`, `CharacterUsernameMissing`, `AccountNotFound`, `Found` |

`LoadForAuthentication` retrieves data only; it does not authenticate. The password hash is opaque data. No DAO method generates, validates, upgrades, or reinterprets hashes. Read-only GM loading is supported; no GM mutation method exists.

## 4. Implementation and mapping

Implementation source: `AORebirth/Libraries/Source/AORebirth.Database/Domain/Accounts/MySqlAccountDao.cs`; namespace `AORebirth.Database.Domain.Accounts`. Explicit SQL aliases map directly into the neutral account DTOs. The private sealed `CharacterAccountName` implementation row distinguishes an absent character row from a row with null Username. No separate mapper or public database entity is introduced.

All SQL, parameter binding, provider acquisition, connection ownership and mapping stay in the database implementation. Runtime code receives no `DBLoginData`, `DBCharacter`, generic legacy mapper, Dapper, connection, transaction, command, SQL, table name, or provider type through this contract.

| Database field | Full neutral account field | Authentication projection |
| --- | --- | --- |
| `Id` | `AccountId` | `AccountId` |
| `CreationDate` | `CreationDate` | Not exposed |
| `Email` | `Email` | Not exposed |
| `FirstName` | `FirstName` | Not exposed |
| `LastName` | `LastName` | Not exposed |
| `Username` | `Username` | `Username` |
| `Password` | `PasswordHash` | `PasswordHash` |
| `AllowedCharacters` | `AllowedCharacters` | `AllowedCharacters` |
| `Flags` | `Flags` | `Flags` |
| `AccountFlags` | `AccountFlags` | `AccountFlags` |
| `Expansions` | `Expansions` | `Expansions` |
| `GM` | `GmLevel` | `GmLevel` |

No expansion masking, GM clamping, account-name canonicalization, profile defaults, or login-policy decisions are added by mapping. Tests must distinguish supplied zero/negative/large integers from schema defaults and preserve nullable strings.

## 5. Lazy factory construction

`AORebirth/Libraries/Source/AORebirth.Database/DatabaseDaoFactory.cs` adds `CreateAccountDao()` returning `IAccountDao`. Construction opens no connection and runs no SQL. The existing mission factory remains supported.

The future composition root constructs the DAO once and passes the interface to services. Do not pass the factory or a connection factory into packet handlers. The implementation's injected `Func<IDbConnection>` follows the mission test seam: provide a fresh owned MySQL-capable connection for each operation, not a global runtime service locator or shared live connection.

## 6. Tables, columns, reads and writes

| Operation | Tables / columns | Mutation and atomic boundary |
| --- | --- | --- |
| `LoadForAuthentication` | `login`: ID, username, password, character limit, flags, account flags, expansions, GM | Buffered read; no write |
| `LoadByUsername` | All 12 `login` fields listed above | Buffered read; no write |
| `LoadByCharacterId` | `characters.Id`, `characters.Username`, then matching `login` fields | Two reads on one owned connection; no character mutation and no transaction/snapshot guarantee |
| `CountRegisteredAccounts` | `login` row count | `long` count, including zero |
| `UsernameExists` | Matching `login.Username` rows | Exactly-one-match result, not generic `Any()` |
| `CreateGameAccount` | Eleven non-ID `login` fields | One insert; application-local `DateTime.Now`; database assigns ID; raw affected-row result |
| `ChangePassword` | `login.Password`, predicate `Username` | One parameterized update, `LIMIT 1`; raw affected-row result |
| `SetExpansions` | `login.Expansions`, predicate `Username` | One parameterized update, no `LIMIT`; raw affected-row result |

No schema, index, migration, runtime schema-repair, delete, account-identity table, token table, or online-state mutation belongs to this branch. The exact checked-in schema definitions and disposable fixture constraints are listed with validation evidence, not inferred from a production database.

## 7. Connection, provider and stable error semantics

Each DAO operation obtains and disposes a fresh connection. Results are detached/buffered; readers do not escape. The default configured-provider path rejects and disposes a non-MySQL connection before executing account SQL. `Connector` may already have opened the configured connection before the check; rejection is not promised before connection acquisition. Upstream `Connector` behavior is unchanged. If `Connector.GetConnection()` itself fails while opening a connection before returning it, the account DAO never receives that connection and cannot dispose it. Tests of disposal after a returned injected connection's open failure must not be represented as a repair of that shared Connector limitation.

`LoadByCharacterId` retains two distinct logical reads but uses one owned connection rather than the legacy implementation's two connections. This is a deliberate resource-lifetime difference, not a claim of an atomic account/character snapshot. A concurrent rename/delete may still change what the second read observes. No authentication/session ownership validation is implied by the supplied character ID.

Null/empty usernames are passed as data without trimming or normalization; equality and trailing-space/case behavior follow the actual MySQL column collation. A missing account returns null. Character lookup additionally distinguishes absent character, missing character username, absent login account, and found account. Failures are not hidden as any of those not-found states.

All new DAO persistence failures propagate. `CreateGameAccount(null)` throws `ArgumentNullException` before acquiring a connection. Creation writes the caller's hash unchanged and uses `DateTime.Now`; it does not accept or honor a caller-supplied timestamp. Mutation results are provider affected-row counts, not proof of authentication, an automatic account ID, or a guaranteed physical value change. Same-value update counts depend on configured MySQL affected-row settings.

MySQL is the only demonstrated provider. There is no MSSQL/PostgreSQL parity claim. Unsupported-provider doubles test rejection/cleanup, not SQL compatibility with those servers.

## 8. Legacy behavior characterization and compatibility ledger

Source: `AORebirth/Libraries/Source/AORebirth.Database/Dao/LoginDataDao.cs`, `Dao.cs`, `SqlMapperUtil.cs`, and `Entities/DBLoginData.cs`.

| Legacy operation | Existing behavior / difference that future integration must handle |
| --- | --- |
| `GetByUsername:82` | Inherited buffered `GetAll(new { Username })`, then `FirstOrDefault()`. No ordering, trimming, normalization, duplicate rejection, or `LIMIT`; absent -> null; errors propagate. Duplicate first row is unspecified. |
| `GetByCharacterId:62` | `CharacterDao.Get` on one connection, then account lookup on another. Missing character, null/empty character username, and absent login all collapse to null. No write or authorization check. |
| `GetRegisteredCount:95` | `COUNT(*)` -> `long`, `.Single()`; errors propagate. |
| `Exists:219` | Matching ID row count equals exactly one. Both zero and multiple matches return false. No current production call found. |
| `WriteLoginData:162` | Eleven-column insert, application-local `DateTime.Now`; supplied DTO `CreationDate` ignored. No explicit transaction, ID readback or DTO-ID update. Logs and rethrows. New DAO's count is additive information. |
| `WriteNewPassword:201` | Username predicate and `LIMIT 1`; raw affected count. Logs every caught error and returns zero, conflating failure with missing/possibly unchanged result. New DAO propagates failure instead. |
| `SetExpansions:139` | Username predicate, no `LIMIT`; discards count; logs/swallows failure. New DAO returns count and propagates failure. |
| `SetGM:124` | User argument unused; updates every login row, discards count, logs/swallows failure. No new mutation equivalent. |
| `LogoffChars:109` | Enumerates account characters and independently commits each `SetOffline`; failure can leave a partial set. No new account equivalent. |
| Inherited `GetWhere` | Current `Program.CheckUsername` uses `!Any()`, which means unavailable for one or multiple matches. Exact-one `UsernameExists` is equivalent only under the governed unique-username invariant. |
| Inherited `GetAll` | Current Windows `Program.CheckDatabase` eagerly materializes every account as a readiness probe. It is not an account-list feature. A count-only probe is weaker and is not a silent drop-in replacement. |
| Inherited `Add` | Only Stage6/Stage7 fixtures currently call it. Its transaction and identity-copyback behavior are not the same as `WriteLoginData`; do not imply `CreateGameAccount` replaces that fixture-specific contract automatically. |

No production inherited `Add`, `Save`, `Delete`, `Count`, or `Exists` invocation was found. No new generic query/CRUD API is justified. The disposable results below retain canonical UNIQUE/NOT NULL constraints. Real zero/one legacy/new parity is executed; invalid-schema duplicate/null shapes are explicitly synthetic new-DAO reader tests, not a claim of physically duplicated MySQL rows.

## 9. Tests, assertions and validation status

Use actual production contract/implementation source in the isolated account validation project, not a test reimplementation. Successful SQL tests must run only in the governed disposable MySQL fixture. Existing legacy operations may run against equivalent disposable fixtures without editing `LoginDataDao`.

Account validation files are `Tools/AccountDaoValidation/AccountDaoValidation.csproj`, `Program.cs`, `FailureChecks.cs`, `DisposableMySql.cs`, and `IsolatedHost.cs`; approved entry point is `Tools/run_account_dao_validation.cmd`. The C# runner follows the mission validation structure and uses disposable MySQL; it does not use a new Python runner.

`Tools/MissionDaoValidation/MissionDaoValidation.csproj` links the actual Accounts sources only so the extended actual `DatabaseDaoFactory` compiles in its source-isolated host. Its existing 202 mission assertions are not replaced or reduced.

Both final runs of `call Tools\run_account_dao_validation.cmd` passed the same **273 named assertions** in the same order. All assertions and both raw-log SHA-256 digests are retained in `Tools/AccountDaoValidation/acceptance-evidence.json`. Local raw artifacts: `build-verify/account-parallel/work/account-mysql1.log` and `account-mysql2.log`. Each run created and removed its own fixture; cleanup PASS. The runner cannot accept an application connection string and never reads application DB configuration.

Assertion counts: contract 9; reads 32; creation 21; resolution 16; matched-row mode 33; changed-row mode 33; concurrency 3; failure/resource ownership 119; synthetic defensive readers 7. The 119-check matrix covers every operation's fresh owned connections on repeated success, disposal after open/command/reader failures, unchanged original exceptions, partial-read rejection and uncertain autocommit outcomes. Seven synthetic invalid-schema checks are explicitly separated from real SQL.

Canonical schemas remain `AORebirth/Libraries/Source/AORebirth.Database/SqlTables/login.sql` and `characters.sql`: InnoDB, login unique/non-null username, non-null account fields, non-null character username, no account foreign key. Actual username collation is `latin1_swedish_ci`. Case and trailing-space matching follow that collation; leading whitespace is not stripped. Empty names are permitted and round-trip. Null character owner insertion is rejected by MySQL; its defensive result is separately reader-simulated. Physical duplicate account rows and duplicate-password-update behavior are not applicable under the retained unique index; actual password SQL/binding retains `LIMIT 1`.

Schema defaults are AllowedCharacters=6, Flags=0, AccountFlags=0, Expansions=127, GM=0. The complete insert supplies every value, so CLR zero values remain zero rather than silently using database defaults. Persisted creation time uses application-local `DateTime.Now`, with whole-second DATETIME precision and `Unspecified` read kind; no UTC conversion. Boundary years 1000/9999 round-trip. Hash values remain caller-supplied.

Same-value password/expansion updates return 1 in matched-row mode (`UseAffectedRows=false`) and 0 in changed-row mode; missing/null names return 0. Each changed target returns 1; other accounts are unchanged. Single statements autocommit: pre-execution errors produce no write, but acknowledgement errors injected after successful SQL can leave a durable write. Failure is not proof of rollback, so future callers must reconcile unknown outcomes instead of blindly retrying.

Existing offline `LoginAuthenticationValidation`: **14/14 PASS** on base and branch. Existing AccountBroker and Unified validation project builds: PASS on both. Their runtime suites were not executed: the former inherits an arbitrary DB connection and deletes all rows in six identity tables; the latter drops/recreates identity tables and starts HTTP/SMTP hosts. ProductionLoginAcceptance defaults to production login connections. Those are not safe foundation/no-runtime-initialization gates and no historical result is reported as a current pass.

Required evidence ledger:

| Gate / assertion family | Result |
| --- | --- |
| Existing/missing/null/empty username; full/auth field mapping; exact opaque hash and integers | PASS |
| Zero/one/multiple counts; exact-one existence; duplicate username characterization | PASS; duplicate insertion rejected; invalid multi-row shape synthetic |
| Four character-lookup statuses; null/empty/collation/absent-account ownership-read cases | PASS; nullable-owner persistence rejected, defensive null result synthetic |
| Quote, slash, semicolon and parameter-like usernames remain data | PASS |
| Creation complete round-trip, local timestamp, duplicate/constraint failures, no partial extra row | PASS |
| Password missing/existing/same-value/error/LIMIT behavior; exact hash persisted | PASS; physical duplicate update N/A under UNIQUE |
| Expansion exact target/missing/error/other-row isolation | PASS |
| Legacy GM multiple-row mutation, ignored username and exact affected count in disposable fixture | PASS characterization; new GM mutation excluded |
| Lazy factory, fresh connection, disposal success/failure, open/command failure, unsupported provider | PASS; shared Connector limitation remains as section 7 |
| Account contract/implementation boundary and positive/negative guard fixtures | PASS; 56 account guard self-checks |
| Complete account disposable MySQL suite run 1 / run 2 | 273 PASS / 273 PASS |
| Existing complete 202-check mission MySQL regression suite | 202 PASS on clean base and branch |
| Windows solution restore/build; Interfaces build; Database build | PASS on clean base and branch |
| DAO guard/self-tests; generated checks; secret scan; whitespace; source inventory | Scoped guards, generated checks, secrets and whitespace PASS; scoped inventories PASS; unscoped guard/full inventory baseline FAIL |
| Relevant existing account/authentication/unified-account validations | LoginAuthentication 14 PASS; broker/unified builds PASS; unsafe runtime suites not run |

## 10. Future production integration map (21 direct invocations)

Counting rule: one entry per actual production invocation of `LoginDataDao`, including inherited calls. These are 21 invocations in 13 source files: 18 ordinary account mappings, one blocked GM mutation, one character/online-state operation, and one startup-readiness operation. Tests, comments, manifests and indirect downstream consumers are separate. No tracked `ZoneEngine_New` account lookup was found at this base.

`SAFE_TO_CUT_OVER` means the persistence operation is available for a future authorized integration; it does not mean this branch performs cutover or that compatibility decisions/tests may be skipped. Legacy runtime fallback stays above the DAO. In particular, adopting a truthful mutation result instead of a legacy swallowed error is an explicit runtime behavior decision.

### A01 - Core authentication hash lookup

```text
CURRENT_FILE=AORebirth/Libraries/Source/AORebirth.Core/Encryption/LoginEncryption.cs:416
CURRENT_CLASS_OR_METHOD=LoginEncryption.GetLoginPassword
CURRENT_LOGIN_DATA_DAO_CALL=GetByUsername(RecvLogin)
TARGET_IACCOUNTDAO_OPERATION=LoadForAuthentication
INPUT_MAPPING=RecvLogin unchanged
RETURN_MAPPING=Found.PasswordHash; missing -> string.Empty
CURRENT_ERROR_BEHAVIOR=Database errors propagate; missing account logs Debug(Database) with username and returns empty string
TARGET_ERROR_BEHAVIOR=Propagate persistence failure; preserve missing fallback/logging above DAO
RUNTIME_BEHAVIOR_RISK=No hash changes; retain rejection on missing hash and existing credential-validation logic
REQUIRES_OTHER_DAO=NO for this read
CUTOVER_STATUS=SAFE_TO_CUT_OVER
```

Indirect consumer: `ChatEngine/PacketHandlers/AuthenticateBot.cs:81` calls the three-argument `IsValidLogin`, which uses this lookup. Bot authentication validates the login key/password; normal Chat authentication below does not. `CheckLogin` supplies its hash to the four-argument validation overload instead.

### A02 - Expansion special stat

```text
CURRENT_FILE=AORebirth/Libraries/Source/AORebirth.Stats/SpecialStats/StatExpansion.cs:61
CURRENT_CLASS_OR_METHOD=StatExpansion.GetValue
CURRENT_LOGIN_DATA_DAO_CALL=GetByCharacterId(Stats.Owner.Instance).Expansions
TARGET_IACCOUNTDAO_OPERATION=LoadByCharacterId
INPUT_MAPPING=Owner.Instance unchanged
RETURN_MAPPING=Found.Account.Expansions; all non-found statuses -> 0
CURRENT_ERROR_BEHAVIOR=Catches every Exception, including null dereference/provider failure, and returns 0
TARGET_ERROR_BEHAVIOR=DAO failure propagates; retain catch-and-zero in Stats unless separately approved
RUNTIME_BEHAVIOR_RISK=Do not globally translate provider failure to missing account; preserve this caller-specific fallback
REQUIRES_OTHER_DAO=NO; read-only character-to-account lookup is explicitly permitted
CUTOVER_STATUS=SAFE_TO_CUT_OVER
```

### A03 - GM special stat

```text
CURRENT_FILE=AORebirth/Libraries/Source/AORebirth.Stats/SpecialStats/StatGMLevel.cs:86
CURRENT_CLASS_OR_METHOD=StatGmLevel.GetValue
CURRENT_LOGIN_DATA_DAO_CALL=GetByCharacterId(Stats.Owner.Instance)
TARGET_IACCOUNTDAO_OPERATION=LoadByCharacterId
INPUT_MAPPING=Retain null Stats/Owner guard returning 0 before DAO; otherwise Owner.Instance
RETURN_MAPPING=Found.Account.GmLevel; all non-found statuses -> 0
CURRENT_ERROR_BEHAVIOR=Missing account -> 0; database errors propagate
TARGET_ERROR_BEHAVIOR=Propagate failure; retain missing fallback
RUNTIME_BEHAVIOR_RISK=Unlike StatExpansion, this getter has no broad error catch
REQUIRES_OTHER_DAO=NO
CUTOVER_STATUS=SAFE_TO_CUT_OVER
```

### A04 - Chat cached GM level

```text
CURRENT_FILE=AORebirth/Server/ChatEngine/CoreClient/Character.cs:95
CURRENT_CLASS_OR_METHOD=Character.CharacterGMLevel getter
CURRENT_LOGIN_DATA_DAO_CALL=GetByCharacterId((int)CharacterId).GM
TARGET_IACCOUNTDAO_OPERATION=LoadByCharacterId
INPUT_MAPPING=Existing uint-to-int cast unchanged
RETURN_MAPPING=Found.Account.GmLevel -> cached characterGMLevel
CURRENT_ERROR_BEHAVIOR=Missing lookup causes null dereference; provider errors propagate
TARGET_ERROR_BEHAVIOR=Propagate failure; missing-result behavior must be explicitly retained or separately repaired
RUNTIME_BEHAVIOR_RISK=Cache sentinel is -1; stored GM=-1 causes repeat reads; adding zero fallback silently changes behavior
REQUIRES_OTHER_DAO=NO for this read
CUTOVER_STATUS=SAFE_TO_CUT_OVER subject to explicit missing-result compatibility
```

### A05 - Normal Chat authentication account lookup

```text
CURRENT_FILE=AORebirth/Server/ChatEngine/PacketHandlers/Authenticate.cs:130
CURRENT_CLASS_OR_METHOD=Authenticate.Read
CURRENT_LOGIN_DATA_DAO_CALL=GetByUsername(userName)
TARGET_IACCOUNTDAO_OPERATION=LoadByUsername
INPUT_MAPPING=Packet username unchanged after existing whitespace rejection
RETURN_MAPPING=Stored Username -> separate character ownership check
CURRENT_ERROR_BEHAVIOR=Missing/blank stored Username -> LoginError and disconnect; database errors propagate
TARGET_ERROR_BEHAVIOR=Same missing handling in caller; propagate failure
RUNTIME_BEHAVIOR_RISK=Current code consumes but does not validate login key/password and does not check Flags despite comments; do not add policy during DAO cutover
REQUIRES_OTHER_DAO=YES for separate CharacterDao.IsCharacterOnAccount
CUTOVER_STATUS=SAFE_TO_CUT_OVER
```

### A06 - Login authentication attributes

```text
CURRENT_FILE=AORebirth/Server/LoginEngine/MessageHandlers/UserCredentialsHandler.cs:122
CURRENT_CLASS_OR_METHOD=UserCredentialsHandler.Handle
CURRENT_LOGIN_DATA_DAO_CALL=GetByUsername(challengedAccount)
TARGET_IACCOUNTDAO_OPERATION=LoadForAuthentication
INPUT_MAPPING=Challenged account from existing authentication-attempt flow
RETURN_MAPPING=Stored Username, Expansions, AllowedCharacters unchanged
CURRENT_ERROR_BEHAVIOR=Missing/blank Username -> RejectAuthentication; database errors propagate
TARGET_ERROR_BEHAVIOR=Preserve missing rejection in runtime; propagate failure
RUNTIME_BEHAVIOR_RISK=Keep challenge generation, password checks, character-list construction and authentication completion ordering unchanged
REQUIRES_OTHER_DAO=YES for separate character list, not this account read
CUTOVER_STATUS=SAFE_TO_CUT_OVER
```

### A07 - New character GM stat seed

```text
CURRENT_FILE=AORebirth/Server/LoginEngine/Packets/CharacterName.cs:336
CURRENT_CLASS_OR_METHOD=CharacterName.CreateNewChar
CURRENT_LOGIN_DATA_DAO_CALL=GetByUsername(AccountName).GM
TARGET_IACCOUNTDAO_OPERATION=LoadByUsername
INPUT_MAPPING=AccountName unchanged
RETURN_MAPPING=GmLevel -> existing stats entry for stat 215
CURRENT_ERROR_BEHAVIOR=Missing account null dereference/provider error propagates
TARGET_ERROR_BEHAVIOR=Propagate failure; explicit missing-result compatibility required
RUNTIME_BEHAVIOR_RISK=Character row is already committed before this read; do not silently make creation atomic
REQUIRES_OTHER_DAO=YES for existing separate character/stats/loadout persistence
CUTOVER_STATUS=SAFE_TO_CUT_OVER subject to explicit missing-result compatibility
```

### A08 - New character expansion stat seed

```text
CURRENT_FILE=AORebirth/Server/LoginEngine/Packets/CharacterName.cs:412
CURRENT_CLASS_OR_METHOD=CharacterName.CreateNewChar
CURRENT_LOGIN_DATA_DAO_CALL=GetByUsername(AccountName).Expansions
TARGET_IACCOUNTDAO_OPERATION=LoadByUsername
INPUT_MAPPING=AccountName unchanged
RETURN_MAPPING=Expansions -> existing stats entry for stat 389
CURRENT_ERROR_BEHAVIOR=Missing account null dereference/provider error propagates
TARGET_ERROR_BEHAVIOR=Propagate failure; explicit missing-result compatibility required
RUNTIME_BEHAVIOR_RISK=Second independent account read; combining it with A07 changes timing/snapshot boundaries
REQUIRES_OTHER_DAO=YES for separate stats persistence
CUTOVER_STATUS=SAFE_TO_CUT_OVER subject to explicit missing-result compatibility
```

### A09 - Console username availability (inherited generic query)

```text
CURRENT_FILE=AORebirth/Server/LoginEngine/Program.cs:116
CURRENT_CLASS_OR_METHOD=Program.CheckUsername
CURRENT_LOGIN_DATA_DAO_CALL=!GetWhere(new { Username=username }).Any()
TARGET_IACCOUNTDAO_OPERATION=!UsernameExists(username), conditional on governed unique Username invariant
INPUT_MAPPING=Username unchanged
RETURN_MAPPING=Invert existence for availability
CURRENT_ERROR_BEHAVIOR=Errors propagate; one or multiple matching rows means unavailable
TARGET_ERROR_BEHAVIOR=Errors propagate
RUNTIME_BEHAVIOR_RISK=Exact-one UsernameExists is false for duplicates, unlike Any; prove unique schema before treating these as equivalent
REQUIRES_OTHER_DAO=NO
CUTOVER_STATUS=SAFE_TO_CUT_OVER under verified unique Username schema
```

### A10 - Console account creation

```text
CURRENT_FILE=AORebirth/Server/LoginEngine/Program.cs:247
CURRENT_CLASS_OR_METHOD=Program.AddUser
CURRENT_LOGIN_DATA_DAO_CALL=WriteLoginData(login)
TARGET_IACCOUNTDAO_OPERATION=CreateGameAccount
INPUT_MAPPING=Caller-generated hash/profile/integer fields unchanged; no timestamp input because legacy DAO chooses DateTime.Now
RETURN_MAPPING=Successful completion -> existing success output; returned count is new information, not an account ID
CURRENT_ERROR_BEHAVIOR=DAO logs/rethrows; AddUser catches and prints failure, otherwise success
TARGET_ERROR_BEHAVIOR=Propagate persistence/constraint error; caller retains failure UI
RUNTIME_BEHAVIOR_RISK=Hashing and argument policy stay in Program; current supplied DBLoginData.CreationDate is ignored
REQUIRES_OTHER_DAO=NO
CUTOVER_STATUS=SAFE_TO_CUT_OVER
```

### A11 - Windows startup database-readiness probe

```text
CURRENT_FILE=AORebirth/Server/LoginEngine/Program.cs:333
CURRENT_CLASS_OR_METHOD=Program.CheckDatabase
CURRENT_LOGIN_DATA_DAO_CALL=GetAll()
TARGET_IACCOUNTDAO_OPERATION=N/A; explicit database-readiness owner, not an account-list API
INPUT_MAPPING=None
RETURN_MAPPING=Successful eager full-row materialization -> true, including an empty table
CURRENT_ERROR_BEHAVIOR=Catches every Exception and returns false; initialization then aborts
TARGET_ERROR_BEHAVIOR=Retain explicit readiness failure signal
RUNTIME_BEHAVIOR_RISK=CountRegisteredAccounts is a weaker projection and does not prove every account field can materialize; no silent replacement
REQUIRES_OTHER_DAO=NO; database-preflight/readiness separation
CUTOVER_STATUS=TOOL_OR_TEST_ONLY role; active production startup readiness remains deferred to its owner
```

The category denotes a readiness role, not an inactive file. `Program.cs` is active Windows code under `#if !AOREBIRTH_LINUX`. Do not remove this check or claim a full account list is required by gameplay.

### A12 - Console account-character logoff

```text
CURRENT_FILE=AORebirth/Server/LoginEngine/Program.cs:672
CURRENT_CLASS_OR_METHOD=Program.LogoffCharacters
CURRENT_LOGIN_DATA_DAO_CALL=LogoffChars(obj[1])
TARGET_IACCOUNTDAO_OPERATION=NONE
INPUT_MAPPING=Username argument
RETURN_MAPPING=Void
CURRENT_ERROR_BEHAVIOR=Errors propagate; earlier character updates can remain committed
TARGET_ERROR_BEHAVIOR=Future character/online-state contract decision
RUNTIME_BEHAVIOR_RISK=Account character enumeration followed by independently committed Online=0 writes; preserve session/online ownership order
REQUIRES_OTHER_DAO=YES, ICharacterDao/online-state owner
CUTOVER_STATUS=DEFERRED_TO_CHARACTER_DAO
```

### A13 - Console GM mutation (blocked)

```text
CURRENT_FILE=AORebirth/Server/LoginEngine/Program.cs:740
CURRENT_CLASS_OR_METHOD=Program.SetGMLevel
CURRENT_LOGIN_DATA_DAO_CALL=SetGM(obj[1],gmlevel)
TARGET_IACCOUNTDAO_OPERATION=NONE; GM mutation deliberately excluded
INPUT_MAPPING=Console advertises username plus GM integer
RETURN_MAPPING=Void; caller unconditionally prints success
CURRENT_ERROR_BEHAVIOR=DAO logs/swallows errors; caller can print success after failure; SQL updates every account
TARGET_ERROR_BEHAVIOR=No safe target in this task
RUNTIME_BEHAVIOR_RISK=Explicit conflict between one-account wording and unscoped SQL; separate approved behavioral repair required
REQUIRES_OTHER_DAO=NO
CUTOVER_STATUS=BLOCKED_BY_SETGM_SEMANTICS
```

### A14 - Console expansions update

```text
CURRENT_FILE=AORebirth/Server/LoginEngine/Program.cs:759
CURRENT_CLASS_OR_METHOD=Program.SetExpansions
CURRENT_LOGIN_DATA_DAO_CALL=SetExpansions(obj[1],expansions)
TARGET_IACCOUNTDAO_OPERATION=SetExpansions
INPUT_MAPPING=Username and integer unchanged
RETURN_MAPPING=Explicitly handle new affected count; old return is void
CURRENT_ERROR_BEHAVIOR=DAO logs/swallows; caller prints success even for missing account or update failure
TARGET_ERROR_BEHAVIOR=Persistence errors propagate; not converted to not-found
RUNTIME_BEHAVIOR_RISK=Preserving swallowed-error UI needs caller compatibility handling; adopting truthful outcome UI requires explicit behavior approval
REQUIRES_OTHER_DAO=NO
CUTOVER_STATUS=SAFE_TO_CUT_OVER with explicit error/result compatibility mapping
```

### A15 - Console password hash update

```text
CURRENT_FILE=AORebirth/Server/LoginEngine/Program.cs:817
CURRENT_CLASS_OR_METHOD=Program.SetPassword
CURRENT_LOGIN_DATA_DAO_CALL=WriteNewPassword(new DBLoginData { Username=username, Password=hashed })
TARGET_IACCOUNTDAO_OPERATION=ChangePassword
INPUT_MAPPING=Username and already-generated hash unchanged
RETURN_MAPPING=Affected rows -> current zero-failure/nonzero-success branch
CURRENT_ERROR_BEHAVIOR=DAO logs/returns 0 on errors; zero also means missing and may mean unchanged depending on provider setting
TARGET_ERROR_BEHAVIOR=Persistence errors propagate; retaining old console fallback requires a caller catch
RUNTIME_BEHAVIOR_RISK=Keep LIMIT 1 and actual affected-row semantics; hashing remains caller-owned
REQUIRES_OTHER_DAO=NO
CUTOVER_STATUS=SAFE_TO_CUT_OVER with explicit error/result compatibility mapping
```

### A16 - Login flags query

```text
CURRENT_FILE=AORebirth/Server/LoginEngine/QueryBase/LoginFlags.cs:76
CURRENT_CLASS_OR_METHOD=LoginFlags.GetLoginFlags
CURRENT_LOGIN_DATA_DAO_CALL=GetByUsername(recvLogin)
TARGET_IACCOUNTDAO_OPERATION=LoadForAuthentication
INPUT_MAPPING=recvLogin unchanged
RETURN_MAPPING=Found.Flags -> flagsL; missing leaves existing flagsL unchanged
CURRENT_ERROR_BEHAVIOR=Provider errors propagate; initial flagsL default is 0 but a reused instance retains its prior value on missing
TARGET_ERROR_BEHAVIOR=Propagate failure; keep missing-state behavior in caller
RUNTIME_BEHAVIOR_RISK=CheckLogin requires Flags==0; do not silently reset a reused instance or change login policy
REQUIRES_OTHER_DAO=NO
CUTOVER_STATUS=SAFE_TO_CUT_OVER
```

### A17 - Login name existence read

```text
CURRENT_FILE=AORebirth/Server/LoginEngine/QueryBase/LoginName.cs:75
CURRENT_CLASS_OR_METHOD=LoginName.GetLoginName
CURRENT_LOGIN_DATA_DAO_CALL=GetByUsername(recvLogin)
TARGET_IACCOUNTDAO_OPERATION=LoadForAuthentication
INPUT_MAPPING=recvLogin unchanged
RETURN_MAPPING=First read checks presence after resetting loginN to null
CURRENT_ERROR_BEHAVIOR=Provider errors propagate; missing leaves loginN null
TARGET_ERROR_BEHAVIOR=Propagate failure
RUNTIME_BEHAVIOR_RISK=Retain stored Username and existing case comparisons in CheckLogin; no normalization in DAO
REQUIRES_OTHER_DAO=NO
CUTOVER_STATUS=SAFE_TO_CUT_OVER
```

### A18 - Login name second read

```text
CURRENT_FILE=AORebirth/Server/LoginEngine/QueryBase/LoginName.cs:78
CURRENT_CLASS_OR_METHOD=LoginName.GetLoginName
CURRENT_LOGIN_DATA_DAO_CALL=GetByUsername(recvLogin).Username
TARGET_IACCOUNTDAO_OPERATION=LoadForAuthentication
INPUT_MAPPING=Same recvLogin
RETURN_MAPPING=Stored Username -> loginN
CURRENT_ERROR_BEHAVIOR=Provider errors propagate; deletion between reads can produce null dereference
TARGET_ERROR_BEHAVIOR=Propagate failure; explicit missing-result handling required
RUNTIME_BEHAVIOR_RISK=Reusing first result changes the existing two-read timing/race; do not silently combine during mechanical cutover
REQUIRES_OTHER_DAO=NO
CUTOVER_STATUS=SAFE_TO_CUT_OVER subject to explicit missing-result compatibility
```

### A19 - Login password query

```text
CURRENT_FILE=AORebirth/Server/LoginEngine/QueryBase/LoginPasswd.cs:80
CURRENT_CLASS_OR_METHOD=LoginPasswd.GetLoginPassword
CURRENT_LOGIN_DATA_DAO_CALL=GetByUsername(recvLogin)
TARGET_IACCOUNTDAO_OPERATION=LoadForAuthentication
INPUT_MAPPING=recvLogin unchanged
RETURN_MAPPING=Found.PasswordHash -> passwdL; missing remains null after explicit reset
CURRENT_ERROR_BEHAVIOR=Provider errors propagate
TARGET_ERROR_BEHAVIOR=Propagate failure; retain missing null
RUNTIME_BEHAVIOR_RISK=No hash generation, validation, upgrade or reinterpretation in persistence
REQUIRES_OTHER_DAO=NO
CUTOVER_STATUS=SAFE_TO_CUT_OVER
```

### A20 - Web registered-account count

```text
CURRENT_FILE=AORebirth/Server/WebEngine/Websites/IndexPHP.cs:84
CURRENT_CLASS_OR_METHOD=IndexPHP.CreateContent
CURRENT_LOGIN_DATA_DAO_CALL=GetRegisteredCount()
TARGET_IACCOUNTDAO_OPERATION=CountRegisteredAccounts
INPUT_MAPPING=None
RETURN_MAPPING=long unchanged -> registeredCount and generated HTML
CURRENT_ERROR_BEHAVIOR=Errors propagate
TARGET_ERROR_BEHAVIOR=Errors propagate
RUNTIME_BEHAVIOR_RISK=Zero is not failure; do not narrow count to int
REQUIRES_OTHER_DAO=YES for separate logged-in-character display, not this count
CUTOVER_STATUS=SAFE_TO_CUT_OVER
```

### A21 - Legacy Zone login account attributes

```text
CURRENT_FILE=AORebirth/Server/ZoneEngine/Core/PacketHandlers/ClientConnected.cs:670
CURRENT_CLASS_OR_METHOD=ClientConnected.InitializeActionableState
CURRENT_LOGIN_DATA_DAO_CALL=GetByUsername(characterData.Username)
TARGET_IACCOUNTDAO_OPERATION=LoadByUsername
INPUT_MAPPING=Previously loaded characterData.Username unchanged
RETURN_MAPPING=Found -> expansionValue=Expansions|2 plus GM stat; missing -> expansionValue stays 2 and GM is not updated
CURRENT_ERROR_BEHAVIOR=Missing logged; provider errors propagate; earlier actionable/stat writes may already have happened
TARGET_ERROR_BEHAVIOR=Propagate failure; retain missing fallback in runtime
RUNTIME_BEHAVIOR_RISK=Expansion OR 2 and stat mutations stay ZoneEngine behavior, not account mapping
REQUIRES_OTHER_DAO=YES for prior character lookup and separate stat writes
CUTOVER_STATUS=SAFE_TO_CUT_OVER
```

## 11. SetGM blocked semantics

```text
SET_GM_CUTOVER_STATUS=BLOCKED_PREEXISTING_UNSCOPED_UPDATE
```

`LoginDataDao.cs:124-136` accepts a username but only binds GM to `UPDATE login SET GM=@gm`. The sole production caller, `LoginEngine/Program.cs:728-745`, advertises `setgm <username> <gmlevel>` and prints success for that named account. It prints a GM range but only performs integer parsing, not range enforcement. The DAO also swallows errors, so success output is not proof of persistence.

Executed the unchanged legacy method through actual MySqlConnector commands in both final disposable runs. Exact observations:

| Affected-row mode | Supplied-name category | Total fixture rows | Actual affected count | Rows at requested GM afterward |
| --- | --- | --- | --- | --- |
| Matched | Existing | 8 | 8 | 8 |
| Matched | Missing | 8 | 8 | 8 |
| Matched | Null, repeat same GM | 8 | 8 | 8 |
| Changed | Existing | 9 | 9 | 9 |
| Changed | Missing | 9 | 9 | 9 |
| Changed | Null, repeat same GM | 9 | 0 | 9 |

The second mode has an additional account created by its mutation tests. The observer proves the actual SQL has only the `gm` parameter and no username parameter/predicate. All rows are affected by the assignment regardless of the supplied name; same-value physical changes differ only by provider setting. Error injection also proves legacy logging/swallowing. Do not execute this characterization against an application/live database.

No unsafe all-account operation and no misleading username-scoped `SetGmLevel` is added. A separate explicitly approved repair must decide intended username-scoped behavior, error/result semantics and caller feedback before cutover. Read-only GM data and caller-supplied GM during new-account creation remain supported.

## 12. LogoffChars and character-domain dependency

```text
LOGOFF_CHARS_STATUS=DEFERRED_TO_CHARACTER_DAO
```

The only tracked caller is A12, `LoginEngine/Program.cs:672`. `LoginDataDao.cs:111` calls `CharacterDao.GetAllForUser`, which uses an account-username query (`CharacterDao.cs:301-304`); each `SetOffline` calls generic `Save` with ID and `Online=0` (`CharacterDao.cs:464-466`). These are separate owned transactions, not an atomic account operation.

No `LogoffChars`, online-state mutation, character deletion, character write, or session coordination is added to `IAccountDao`. The future character/online-state owner must preserve session ownership, error handling, partial-update behavior and save/offline order or obtain approval for a separate repair.

## 13. AccountIdentity and BotService exclusions

```text
ACCOUNT_IDENTITY_DAO_STARTED=NO
```

`AORebirth.AccountBroker/AccountBrokerService.cs` has no `LoginDataDao` invocation. Its own login SQL is part of larger identity workflows and is not migrated through independent account DAO connections:

| Existing excluded site | Why it remains separate |
| --- | --- |
| `ChangePassword:360`, login update `:404` | Identity/password policy and transactional workflow |
| `ResetPassword:556`, login update `:622` | Token consumption/password transition and existing transaction |
| `CreateGameAccount:647`, `CreateOrLinkGameAccount:1002`, login insert `:1021` | Identity/provisioning/mapping coordination, not standalone legacy account creation |
| Game-account read helpers `:1256-1297` | Includes existing transaction participation, locking and normalized-name variants |
| Identity/mapping/provisioning/token helpers | Future `IAccountIdentityDao`, not the eight-operation game-account surface |

Their classification is `DEFERRED_TO_ACCOUNT_IDENTITY_DAO`. Do not split existing transactions by replacing individual broker SQL statements with separate `IAccountDao` calls. No email-verification/password-reset token, external mapping, provisioning job, identity, bot principal/credential/scope/audit, or authentication-policy contract is added.

## 14. Tests/tools and inactive references, classified separately

The five executable test invocations are not counted in the 21 production entries:

| ID | Existing file / line | Invocation / purpose | Classification |
| --- | --- | --- | --- |
| T01 | `LinuxBuild/Tools/Stage6MySqlIntegrationTests/Program.cs:212` | `GetByUsername(username)==null` fixture-collision check | `TOOL_OR_TEST_ONLY` |
| T02 | `LinuxBuild/Tools/Stage6MySqlIntegrationTests/Program.cs:233` | Inherited `Add(login)`; count plus entity-ID mutation used by fixture | `TOOL_OR_TEST_ONLY` |
| T03 | `LinuxBuild/Tools/Stage7MySqlSecurityIntegrationTests/Program.cs:393` | `GetByUsername(fixture.AccountA)==null` collision check | `TOOL_OR_TEST_ONLY` |
| T04 | `LinuxBuild/Tools/Stage7MySqlSecurityIntegrationTests/Program.cs:394` | `GetByUsername(fixture.AccountB)==null` collision check | `TOOL_OR_TEST_ONLY` |
| T05 | `LinuxBuild/Tools/Stage7MySqlSecurityIntegrationTests/Program.cs:421` | Inherited `Add(login)` fixture insertion | `TOOL_OR_TEST_ONLY` |

Stage5 repository checks, Stage7 security contracts, Stage3 offline-smoke comments, compatibility API manifests, source inventories and WebCore source checks reference the legacy symbols as test assertions/metadata, not runtime invocations. Comments in `StatLife.cs:84`, `CharacterPerksDao.cs:84` and `GmiVaultDao.cs:207` are not callers.

No actual current production consumer is classified `NO_LONGER_ACTIVE`. Historical documentation saying AddUser uses `Exists` is stale: the current implementation uses inherited `GetWhere(...).Any()`. The absence of a production `Exists` caller does not permit altering the documented legacy duplicate behavior in its characterization test.

## 15. Inspected-file inventory and deliberate non-edits

Complete direct-consumer reads used for the map:

- `AORebirth/Libraries/Source/AORebirth.Core/Encryption/LoginEncryption.cs`
- `AORebirth/Libraries/Source/AORebirth.Stats/SpecialStats/StatExpansion.cs`
- `AORebirth/Libraries/Source/AORebirth.Stats/SpecialStats/StatGMLevel.cs`
- `AORebirth/Server/ChatEngine/CoreClient/Character.cs`
- `AORebirth/Server/ChatEngine/PacketHandlers/Authenticate.cs`
- `AORebirth/Server/LoginEngine/MessageHandlers/UserCredentialsHandler.cs`
- `AORebirth/Server/LoginEngine/Packets/CharacterName.cs`
- `AORebirth/Server/LoginEngine/Program.cs`
- `AORebirth/Server/LoginEngine/QueryBase/LoginFlags.cs`
- `AORebirth/Server/LoginEngine/QueryBase/LoginName.cs`
- `AORebirth/Server/LoginEngine/QueryBase/LoginPasswd.cs`
- `AORebirth/Server/WebEngine/Websites/IndexPHP.cs`
- `AORebirth/Server/ZoneEngine/Core/PacketHandlers/ClientConnected.cs`

Additional persistence/call-chain inspection:

- `AORebirth/Libraries/Source/AORebirth.Database/Dao/LoginDataDao.cs` (complete)
- `AORebirth/Libraries/Source/AORebirth.Database/Entities/DBLoginData.cs` (complete)
- `AORebirth/Libraries/Source/AORebirth.Database/Dao/Dao.cs` (complete)
- `AORebirth/Libraries/Source/AORebirth.Database/Dao/CharacterDao.cs` (account character enumeration/offline methods)
- `AORebirth/Libraries/Source/AORebirth.Database/SqlMapperUtil.cs` (parameterized read/count SQL generation)
- `AORebirth/Server/LoginEngine/Packets/CheckLogin.cs` (complete)
- `AORebirth/Server/ChatEngine/PacketHandlers/AuthenticateBot.cs` (complete)
- `AORebirth/Libraries/Source/AORebirth.AccountBroker/AccountBrokerService.cs` (identity boundary and login SQL ownership)

New production source reconciled directly with this report (complete reads): all five `Interfaces/Persistence/Accounts` files named in section 3 and `Database/Domain/Accounts/MySqlAccountDao.cs`.

Governance/baseline inputs: `AGENTS.md`, `AI_START_HERE.md`, `docs/project/DEVELOPMENT_AUTHORITY.md`, `docs/ai/WORKFLOW.md`, `DAO_REFACTOR_AUDIT.md`, `DAO_REFACTOR_ROADMAP.md`, and `docs/reports/MISSION_DAO_PARALLEL_HANDOFF.md`. Additional inspected sources: canonical `AORebirth.Database/SqlTables/login.sql` and `characters.sql`; `Connector.cs`; actual legacy `CharacterDao`, `OrganizationDao`, `StatDao`, `ItemDao`, `InstancedItemDao`, `ReceivedMessagesDao`, `IDao`, `TablenameAttribute`, `SqlMapperUtil`, and their DB entity dependencies linked in the new test project (complete). Dedicated validation programs/project files read completely: `Tools/LoginAuthenticationValidation`, `Tools/AccountBrokerValidation`, `Tools/UnifiedAccountFlowValidation`; also `Tools/AccountIdentitySchema/AccountIdentitySchemaValidationRunner/Program.cs`, `Tools/ProductionLoginAcceptance/Program.cs`, `LinuxBuild/Tools/Stage8OfflineSmokeTests/LoginHandoffLifecycleTests.cs`, and the Stage6/Stage7 account fixture callsites. Authoritative Windows Interfaces/Database projects, companion Linux projects/inventories and `LinuxBuild/Tools/SourceInventoryGuard/Program.cs` were inspected for the scoped governed include update. The existing mission test host/runner and architecture guard were inspected to extend those workflows. New account production and test source, account guard changes/self-tests, and this report were reviewed.

Existing project dependencies remain: Interfaces already references shared messaging/Cell/Enums/Utility; Database already owns provider/Dapper and legacy mapper infrastructure. No new project reference or engine dependency is introduced. Purity is enforced on the new Accounts contract/implementation surface, not claimed retroactively for every pre-existing Interfaces assembly dependency.

Every production consumer listed above is deliberately unchanged. Also unchanged: `LoginDataDao.cs`, `DBLoginData.cs`, existing character/mission/runtime behavior, engine project files, both ZoneEngine source inventories, AccountBroker/BotService, migrations and schema. The scoped final diff/status review confirms no changes under any protected runtime, engine, Core, Stats, AccountBroker, BotService, schema, legacy mapper or deployment path; the new files are confined to the allowed account/test/report areas.

Exact changed-file inventory (24 files):

- Five account contract files listed in section 3.
- `AORebirth/Libraries/Source/AORebirth.Database/Domain/Accounts/MySqlAccountDao.cs`.
- `AORebirth/Libraries/Source/AORebirth.Database/DatabaseDaoFactory.cs`.
- `AORebirth/Libraries/Source/AORebirth.Interfaces/AORebirth.Interfaces.csproj`.
- `AORebirth/Libraries/Source/AORebirth.Database/AORebirth.Database.csproj`.
- `LinuxBuild/source-inventory/AORebirth.Interfaces.CompileItems.props` and `AORebirth.Database.CompileItems.props`.
- `Tools/AccountDaoValidation/AccountDaoValidation.csproj`, `Program.cs`, `FailureChecks.cs`, `DisposableMySql.cs`, `IsolatedHost.cs`, `README.md`, `acceptance-evidence.json`.
- `Tools/run_account_dao_validation.cmd`.
- `Tools/DaoArchitectureGuard/dao_architecture_guard.py` and `Tools/run_dao_architecture_guard.cmd`.
- `Tools/MissionDaoValidation/MissionDaoValidation.csproj` (two source includes only; mission tests unchanged).
- `DAO_REFACTOR_ROADMAP.md` (parallel foundation note only).
- This handoff, `docs/reports/ACCOUNT_DAO_PARALLEL_HANDOFF.md`.

Source inventories were written and checked only through SourceInventoryGuard's supported `--legacy-project ... --output ... --write/--check` process after the source files were made Git-visible. No global regeneration, unrelated Enums repair, or engine inventory edit was performed. `PROJECT_STATE.md`, `CURRENT_TASK.md`, root solution, original audit and deployment scripts remain unchanged under this task's explicit ownership boundary.

## 16. Known baseline failures and current comparison

Current-task reproduction on clean `522cbf3a618d859efce62562d7c9e227bdcb4309` is required before labelling any broad failure pre-existing. Earlier mission handoff results are context, not current proof.

| Gate | Exact command | Clean base diagnostics | Account branch diagnostics | Attribution |
| --- | --- | --- | --- | --- |
| Default repository-wide DAO guard | `call Tools\\run_dao_architecture_guard.cmd` | Two unlisted SQL sites: `ZoneEngine_New/Core/Data/MySqlCharacterRepository.cs` and `MySqlStatRepository.cs`; 7 sites against 5 manifest entries | Same two sites | Current clean-base/worktree reproduction reported; no manifest weakening |
| Full-project mission validation | `call Tools\\run_mission_dao_validation.cmd` | CS2001: missing Enums `ItemType.cs` | Same CS2001 | Current clean-base/worktree reproduction reported; source-isolated regression remains separate |
| Full AOtomation compatibility | `call Tools\\run_aotomation_messaging_tests.cmd` | 18 diagnostics: 17 missing AggDef fixture members, 1 missing PlayfieldAnarchyFMessage Unknown1 | Same 18 diagnostics | Current clean-base/worktree reproduction reported; no test exclusions |
| Full source inventory | `dotnet run --project LinuxBuild/Tools/SourceInventoryGuard/SourceInventoryGuard.csproj -- --repository-root . --manifest LinuxBuild/source-inventory/inventory.json --check` | STALE Enums inventory | Same stale inventory | Current clean-base/worktree reproduction reported; scoped Interfaces/Database checks PASS |

The exact normalized diagnostic arrays for full mission, compatibility and default guard are equal between base and branch, not merely matching counts; retained with commands and raw-log hashes in `acceptance-evidence.json`. Base raw logs remain under `build-verify/account-parallel/base`; branch logs under `work`.

Relevant compiler diagnostics: CS2001 for `AORebirth.Enums/ItemType.cs`; CS1061 for `CapturedEnemySpecialAttackWeaponPacketFixture.AggDef` in `CapturedEnemyCombatGeneratedPacketFixtureTests.cs:89,194`, `CapturedEnemyCombatPacketFactoryTests.cs:354,506,574,733,822,1019,1106,1203,1290,1380,1468,1936,2034`, `OrdinaryEnemyCombatSetupGeneratorTests.cs:840,1091`; CS0117 for `PlayfieldAnarchyFMessage.Unknown1` in `N3RecoveredContractTests.cs:483`. Full inventory stops with `STALE: source inventory does not match .../AORebirth.Enums/AORebirth.Enums.csproj`. Default guard reports `NEW_VIOLATION` for both existing new-engine repository files. No protected source or known-violations manifest was changed.

An initial clean-base mission fixture exceeded startup time; an overlapping branch attempt refused the occupied named resource. Serial retries passed all 202 checks on each tree and cleaned the owned fixtures. The account fixture's first development attempt used Docker internal mode, which suppressed host port publication here; its cleanup passed. The new account runner now uses the proven mission dedicated-bridge/loopback-publication pattern, and both final runs passed.

Workflow execution errors were recorded, not labelled project blockers: escaped-backslash/space regex operands, a cmd wildcard path, guessed nonexistent helper paths, and an inline quoted selected-Python invocation failed read-only/launch parsing. Safe corrections used fixed-string `rg -F -e`, `rg -g` directory filtering, exact documented paths and a separate CMD wrapper with per-line environment expansion. One report patch failed line-context matching and made no file change; it was reapplied with complete line context. The staged whitespace gate found two extra EOF blank lines in new files, which were removed; the final staged check passed. No repository or database data was discarded by these failures. Do not suppress failing tests, exclude compile items, repair unrelated inventory, or change runtime files to manufacture a pass. A passing source-isolated DAO suite is persistence evidence, not full Linux/runtime integration acceptance.

## 17. Exact future integration files and ownership

The future runtime owner may need the 13 existing production consumer files in section 15, explicit composition roots, and their associated runtime contract tests. Account injection into `AORebirth.Core` encryption and `AORebirth.Stats` requires a deliberate constructor/initialization seam; do not replace the legacy singleton with a new global mutable DAO singleton.

Potential composition roots for a future separately approved integration:

- `AORebirth/Server/LoginEngine/Program.cs` and the actual Linux Login composition source selected by its current project.
- `AORebirth/Server/ChatEngine/Program.cs` and its actual platform composition source.
- `AORebirth/Server/WebEngine/Program.cs` if this legacy website generator remains an active target.
- `AORebirth/Server/ZoneEngine/Program.cs` or the stable new-engine composition source designated by its owner, after coordination.

These are ownership review points, not instructions to wire every engine or invent a new-engine adapter now. Verify exact stable files at the future integration SHA. There is no existing `ZoneEngine_New` account callsite to migrate mechanically at this baseline.

Future safe account integration must not casually edit `LoginDataDao.SetGM`, `LogoffChars`, AccountBroker SQL, identity/token transactions, BotService, character aggregate persistence, game packet behavior, schema, or deployment scripts. GM repair, character online-state migration and identity persistence require their separately approved scopes. Preserve legacy implementation until integration acceptance and the later cleanup window.

## 18. Recommended future cutover order

1. Agree the stable integration SHA and per-file ownership with active engine developers; retain this foundation as a separate reviewed branch.
2. Accept the persistence tests and account guard, and resolve required shared baseline build failures with their owners.
3. Add explicit composition-root/constructor or initialization injection using `IAccountDao`; no runtime connection/provider/factory exposure.
4. Migrate read-only account/count callsites with exact DTO and missing/error compatibility tests. Preserve separate character ownership checks and existing authentication policy.
5. Handle each caller's fallback explicitly: Stats expansion catch-to-zero, GM missing behavior, login query state, cached Chat GM, and Zone expansion OR 2 remain runtime-owned.
6. Migrate account creation/password/expansion mutation only after deciding affected-row and legacy error/UI compatibility; preserve caller hashing, local creation time, LIMIT and transaction boundaries.
7. Keep startup readiness (A11), logoff (A12), GM mutation (A13), AccountBroker identity and BotService out of the ordinary account call substitution.
8. Run full runtime/authentication/ownership regressions and Windows acceptance. Mike performs any authorized local client validation; agents do not launch/control the client.
9. Make a small integration-only commit after acceptance. Any later Linux deployment requires explicit authorization and governed exact-SHA provenance. No deployment is authorized by this handoff.

## 19. Acceptance commands and completion markers

Run from the chosen isolated checkout using `cmd.exe`. Exact persistence/generation acceptance commands used:

```cmd
call Tools\run_account_dao_validation.cmd
call Tools\run_account_dao_validation.cmd
call Tools\run_mission_dao_validation.cmd --isolated-sources
call Tools\run_dao_architecture_guard.cmd --account-persistence-only
call Tools\run_dao_architecture_guard.cmd --mission-persistence-only
call Tools\generate_capture_backed_npc_combat_inventory.cmd --check
call Tools\generate_mission_level_graph.cmd --check
call Tools\scan_secrets.cmd
git diff --check
git diff --cached --check
dotnet run --project LinuxBuild/Tools/SourceInventoryGuard/SourceInventoryGuard.csproj -- --repository-root . --legacy-project AORebirth/Libraries/Source/AORebirth.Interfaces/AORebirth.Interfaces.csproj --output LinuxBuild/source-inventory/AORebirth.Interfaces.CompileItems.props --check
dotnet run --project LinuxBuild/Tools/SourceInventoryGuard/SourceInventoryGuard.csproj -- --repository-root . --legacy-project AORebirth/Libraries/Source/AORebirth.Database/AORebirth.Database.csproj --output LinuxBuild/source-inventory/AORebirth.Database.CompileItems.props --check
```

Windows build commands ran under the generated-combat read lease with per-worktree TEMP/TMP and no global process shutdown. Resolved tool: `C:\Program Files\Microsoft Visual Studio\18\Community\MSBuild\Current\Bin\MSBuild.exe`. Exact form: quoted MSBuild path, quoted project path, `/t:Build /p:Configuration=Debug /m:1 /nr:false /v:minimal`. Solution restore uses `/t:Restore /p:RestorePackagesConfig=true`. Projects: `AORebirth/AORebirth.sln`, Windows Interfaces and Database projects, `Tools/LoginAuthenticationValidation/LoginAuthenticationValidation.csproj`, `Tools/AccountBrokerValidation/AccountBrokerValidation.csproj`, `Tools/UnifiedAccountFlowValidation/UnifiedAccountFlowValidation.csproj`. Run the offline auth executable `Tools\LoginAuthenticationValidation\bin\Debug\LoginAuthenticationValidation.exe`. Full exact command strings and current results are in the evidence JSON; orchestration artifacts are `build-verify/account-parallel/validate.py` and `validate.cmd`. No engine was initialized.

Existing documented commands relevant to later integration include:

```cmd
git diff --check
cmd /d /c Tools\run_account_dao_validation.cmd
cmd /d /c Tools\run_dao_architecture_guard.cmd
cmd /d /c Tools\run_aotomation_messaging_tests.cmd
cmd /d /c Tools\build_aorebirth_debug.cmd
cmd /d /c Tools\accept_windows_source.cmd --expected-sha <accepted-integration-sha>
```

The future owner must add complete account MySQL runs twice, the existing complete mission suite, relevant account/authentication/unified-account suites, generated checks, secret scan, and guarded source inventory. Exact-SHA acceptance is not equivalent to a live deployment authorization.

Completion markers:

```text
PRIMARY_WORKTREE_PRESERVED=YES
ZONEENGINE_FILES_CHANGED=NO
ZONEENGINE_NEW_FILES_CHANGED=NO
LOGINENGINE_FILES_CHANGED=NO
CHATENGINE_FILES_CHANGED=NO
WEBENGINE_FILES_CHANGED=NO
STATS_FILES_CHANGED=NO
ACCOUNT_BROKER_FILES_CHANGED=NO
ACCOUNT_DAO_FOUNDATION_COMPLETE=YES
ACCOUNT_DAO_SAFE_SURFACE_READY=YES
ACCOUNT_DAO_RUNTIME_INTEGRATED=NO
MYSQL_ACCOUNT_IMPLEMENTATION=YES
DATABASE_DAO_FACTORY_UPDATED=YES
SET_GM_CUTOVER_STATUS=BLOCKED_PREEXISTING_UNSCOPED_UPDATE
LOGOFF_CHARS_STATUS=DEFERRED_TO_CHARACTER_DAO
ACCOUNT_IDENTITY_DAO_STARTED=NO
ACCOUNT_MYSQL_TESTS=273 PASS twice
MISSION_DAO_REGRESSION_TESTS=202 PASS
DAO_GUARD=PASS
DAO_GUARD_SCOPE=ACCOUNT_AND_MISSION_WITH_SELF_TESTS
UNSCOPED_DAO_GUARD=FAIL_REPRODUCED_CLEAN_BASELINE
DATABASE_SCHEMA_CHANGED=NO
RUNTIME_LOGIN_BEHAVIOR_CHANGED=NO
RUNTIME_MISSION_LOGIC_CHANGED=NO
PASSWORD_HASH_BEHAVIOR_CHANGED=NO
PACKET_BEHAVIOR_CHANGED=NO
LIVE_DEPLOYMENT_PERFORMED=NO
DEFERRED_ACCOUNT_CALL_SITES=21
```

The 21-call count includes all unchanged direct production invocations, not the five test invocations or separate AccountBroker identity SQL. No Phase 3 completion or runtime-consumer reduction is claimed.

## 20. Remaining risks and stop boundary

- Runtime remains on legacy persistence until a separate authorized integration. This branch does not fix the current unscoped SetGM defect or the caller's misleading success output.
- The stable new mutation error contract propagates failures that legacy console helpers swallow; integration needs explicit compatibility behavior, not accidental exceptions or automatic corrected UI.
- Duplicate fixtures characterize historical behavior, but governed unique username constraints determine whether `UsernameExists` can replace `.Any()` safely. First-row selection without ordering must not be advertised as deterministic.
- `LoadByCharacterId` is a lookup, not authorization or an atomic snapshot. Character missing/username missing/account missing are explicit result states; the future caller selects its existing fallback.
- Password hash storage is unchanged; this does not validate or endorse existing authentication policy. Normal Chat, bot Chat and Login paths have distinct current policy.
- The new authentication projection intentionally reads eight required fields, while legacy `GetByUsername` materializes all twelve. Provider/mapping failures in omitted profile/time columns are therefore not evidence of exact failure parity for that narrower read; future authentication integration must include its data-readiness assumptions in acceptance.
- Creation time remains application-local `DateTime.Now`; MySQL precision/kind/collation/affected-row settings must be reported from disposable tests rather than guessed.
- The Windows startup full-row readiness probe is not replaced by an account count or speculative listing API.
- Full-runtime/Linux acceptance may still depend on independently reproduced shared failures; isolated source tests must not be represented as runtime acceptance.
- Shared project/factory/guard changes require focused diff review. Neither ZoneEngine nor any active developer's work may be altered by this foundation.

Stop after foundation implementation, disposable tests, guard, minimal status note and this handoff are accepted. Do not begin runtime cutover, `ICharacterDao`, `IAccountIdentityDao`, schema migration, or live deployment.
