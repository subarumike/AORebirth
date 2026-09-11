# Mission persistence DAO hardening and integration handoff

Date: 2026-09-05. Scope: persistence implementation, neutral contract documentation,
DAO validation, and mission architecture guard. Integration is deferred.

## Repository safety

- Primary branch: `master`, tracking `origin/master`.
- Starting SHA: `cf1e12b894b1247b34f96f832b217c1cfb828213`.
- Primary status: no tracked changes; untracked `quest example from PRK.txt` preserved.
- Task branch: `codex/mission-dao-persistence`, based on that exact SHA.
- Isolated worktree: `C:/Users/Mike/Documents/AORebirth/tools-temp/worktree-snapshots/mission-dao-persistence`.
- Task worktree started clean. Only the files listed under Changes are edited.
- No reset, clean, stash, branch switch in the primary checkout, merge, or rebase.
  Other worktrees were inventoried through Git metadata only, not edited or cleaned.
- Primary checkout was rechecked after validation: same SHA and same untracked file.

Registered worktrees at task start (paths relative to `C:/Users/Mike/Documents/`;
registration does not prove that a developer is currently using a worktree):

| Worktree | Branch or detached SHA |
| --- | --- |
| AORebirth | master, cf1e12b8 |
| AORebirth/tools-temp/codex-clean-worktree-20260820 | detached db82530b; Git reports prunable, left untouched |
| AORebirth/tools-temp/worktree-snapshots/zone-self-scfu | codex/fix-zone-self-scfu, 7f223eaa |
| AORebirth-clean-final-48e4afc4 | detached 48e4afc4 |
| AORebirth-integration-20260825 | codex/integrate-hydration-pf4582-20260825, 3fabacb3 |
| AORebirth-linked-f38bfc9e | detached f38bfc9e |
| AORebirth-linux-sync | codex/sync-linux-runtime, 6da841f9 |
| AORebirth-malis-live-build | codex/malis-live-build, 0d790abe |
| AORebirth-malis-mission-evidence | codex/malis-mission-evidence, 1cb8b18c |
| AORebirth-mission-ql-parity | codex/mission-harvest-ql-1-250, 9a95e539 |
| AORebirth-modern-mission-capture-planner | codex/modern-mission-capture-planner, aea19aba |
| AORebirth-new-zoneengine-integration | codex/integrate-new-zoneengine-20260903, cf1e12b8 |
| AORebirth-playfield-hydration-stage-1-acceptance | codex/playfield-hydration-stage-1-acceptance, 0887d8a2 |
| AORebirth-pr22-combat-hotfix | codex/pr22-combat-safety-hotfix, d57eb52d |
| AORebirth-pr22-hotfix-master | detached cf1e12b8 |
| AORebirth-safe-integration-20260825 | codex/safe-integration-20260825, 756b0807 |
| codex-playfield-hydration-stage-0-1/AORebirth | codex/playfield-hydration-stage-0-1, 98bbbce3 |

The task added only its isolated worktree to this registry.

## Existing operations and schema assessment

There is one mission persistence implementation: `MySqlMissionDao`, implementing
`IMissionDao`. It uses buffered Dapper mappings to the existing neutral DTOs.
There is no competing DAO, generic runtime repository, or new production project.

| Existing operation | Persistence/transaction boundary | Assessment |
| --- | --- | --- |
| GetMission / GetMissions | Character + quest identity; ordered buffered reads | Existing, retained |
| ReadCharacter | Missions, objectives, flags, reward ledger in one transaction | Existing, retained; coherent snapshot relies on MySQL repeatable-read isolation |
| SaveMission | Insert or version-checked update | Existing; rollback version handling hardened |
| GetObjective / SaveObjective | Character + quest + objective; version checks | Existing; rollback version handling hardened |
| TryAddObservation | Unique character/quest/objective/observation identity | Existing; duplicate-only suppression replaces INSERT IGNORE |
| GetFlag / SaveFlag | Character-scoped flag, version check | Existing; rollback version handling hardened |
| ResolveCharacterAccountKey / account flag reads | Trimmed account key, buffered results | Existing, retained |
| Execute with account / SaveAccountFlag | Locks character ownership before account access, version check | Existing; rollback version handling hardened |
| Claim / mark applied / mark failed | Completed mission prerequisite, token, lease, version, attempts | Existing, retained and exercised with concurrent claims and stale tokens |
| TryApplyCharacterStatReward | Stats + applied reward ledger in the same transaction | Existing; complete-batch validation and failure poisoning added |
| TryChargeRollFee | Cash row lock, debit + durable batch ledger | Existing; reject batch keys that cannot round-trip through effect-reference encoding |
| Start-area pending/read/complete | Existing upsert/read/conditional update convenience methods | Retained; false/null still conflates some failures with absence |

All six existing mission tables use InnoDB and unique domain keys:
`missionstates`, `missionobjectiveprogress`, `missionobjectiveobservations`,
`missionflags`, `missionaccountflags`, and `missionrewardledger`. `characters`
provides ownership; `stats` supplies cash and atomic stat rewards. Existing
DDL uses `latin1_general_ci`, signed INT stat values, and BIGINT ticks/versions.

The schema represents the existing Offered, Active, Completed, Failed, and
Abandoned lifecycle, including lifecycle timestamps, progress, observation
deduplication, flags, and reward recovery. The DAO persists a runtime-selected
state; it does not implement the gameplay transition rules. The lifecycle
round-trip tests are persistence tests, not authorization of new gameplay transitions.

There is no public physical DeleteMission or DeleteExpiredMissions operation.
Current runtime abandonment writes Abandoned state through SaveMission
(`PersistentMissionService.cs:771`). Physical character deletion already removes
character-owned mission rows inside `CharacterDao.DeleteOwnedData`'s wider
transaction (`CharacterDao.cs:233`). That ownership remains intact. Account flags
are account-owned and are not deleted with an individual character.

The schema has no first-class expiry timestamp, inventory key instance, or mission
entrance/zone relationship. Do not infer a new schema requirement from that:
existing runtime metadata/flags and content definitions need a separately scoped
integration review. This task does not claim that persistence alone implements
the mission entrance, generated-instance lifecycle, expiry policy, or UI deletion.

## Architecture audit

- Contract source uses only System types and neutral mission data. No connection,
  transaction provider, DB row, packet, player, session, or playfield types occur
  in its public signatures. The transaction interface exposes mission operations,
  not Commit/Rollback or SQL APIs.
- DAO implementation references no engine or gameplay class. Its existing lower
  dependencies are Dapper, Connector, StatIds, and Utility logging. The new
  MySqlException catch is implementation-only and handles duplicate key 1062.
- `DatabaseDaoFactory.CreateMissionDao` remains unchanged. Default construction
  uses the existing Connector; injected construction owns each returned connection.
- `IDatabaseConnector` and generic `IDao<T>` are legacy provider/mapper surfaces,
  not the mission contract. No generic table DTO or caller-owned provider transaction
  has been introduced into IMissionDao.
- No direct mission gameplay SQL was found outside AORebirth.Database. The two
  engine Program files mention mission tables for database readiness. Character
  deletion SQL remains inside AORebirth.Database. These are distinct responsibilities.
- The global architecture guard has two pre-existing unbaselined SQL sites:
  `ZoneEngine_New/Core/Data/MySqlCharacterRepository.cs` and
  `ZoneEngine_New/Core/Data/MySqlStatRepository.cs`. Their files and the reviewed
  exception manifest are unchanged.

Project structure matters for the later integration:

- Legacy Windows Database references Interfaces, Enums, Exceptions, and Utility.
  The Interfaces assembly itself has legacy messaging/utility dependencies, even
  though the mission contract source has no such dependencies.
- Windows and Linux inventories already include the existing mission contract and DAO.
- `WindowsBuildNet10/Projects/AORebirth.Interfaces.ZoneNew.WinNet10.csproj` includes
  only assembly info, IVector3, ICoordinate, and IQuaternion. It does not compile
  IMissionDao. The other developer will need an agreed project/reference change
  as well as DI/runtime adaptation in the later integration commit. This task
  neither changes that project nor claims the new server can already resolve the DAO.

## Changes

1. `AORebirth/Libraries/Source/AORebirth.Database/Domain/Missions/MySqlMissionDao.cs`:
   opens/disposes injected connections consistently; restores saved DTO versions
   after rollback; retains the original failure when rollback also fails; closes
   the callback transaction scope; prevents committing after a caught SQL write
   or optimistic-concurrency failure; prevalidates complete stat batches; prevents
   partial stat/ledger commits after caught failures; rejects undefined lifecycle
   enum values and unencodable fee batch keys; reports observation SQL failures
   while retaining duplicate replay behavior.
2. `AORebirth/Libraries/Source/AORebirth.Interfaces/Persistence/Missions/IMissionDao.cs`:
   documents lifetime, ownership, version, failure, and retry contracts. No public
   method signatures, DTO fields, or enum values changed.
3. `Tools/MissionDaoValidation/Program.cs`, `HardeningChecks.cs`,
   `MissionDaoValidation.csproj`, `IsolatedHost.cs`, and
   `Tools/run_mission_dao_validation.cmd`: extend the existing disposable test
   harness and add an explicit isolated-source mode. No second persistence implementation.
4. `Tools/DaoArchitectureGuard/dao_architecture_guard.py` and
   `Tools/run_dao_architecture_guard.cmd`: guard the contract and implementation
   directories, including nested source files; add negative fixtures for provider,
   database-row, queryable, SQL, and engine coupling; add a separately named
   mission-only result. The default global guard still reports the existing failures.
5. This report. No shared project, solution, PROJECT_STATE, deployment, engine,
   adapter, packet, player, playfield, or schema files changed.

## Validation and limits

Run from this task worktree:

```cmd
cmd /d /c Tools\run_mission_dao_validation.cmd --isolated-sources
cmd /d /c Tools\run_dao_architecture_guard.cmd --mission-persistence-only
```

- Source-isolated build with C# 7.3 and actual production mission source: PASS.
- Disposable MySQL integration, 131 checks: PASS.
- Rollback/concurrency suite: PASS.
- Mission persistence guard and positive/negative guard fixtures: PASS.
- Secret scan and whitespace validation: PASS.
- Disposable resource cleanup: PASS; read-only label checks found no remaining
  test containers, networks, or volumes. Only test-owned disposable data was removed.

Coverage includes all seven mission mutation-stage rollback cutpoints, insert/update
DTO version restoration and retry, rejected scope reuse, character/account isolation,
stale versions, nullable/tick round trips, buffered ordering/detachment, concurrent
observation/fee/claim/stat reward operations, expired leases, stale claim rejection,
failed reward retry, stat range overflow, and real SQL conversion failure after a
stat write but before the ledger insert.

The optional isolated mode compiles the real contract, DAO, factory, and StatIds
sources directly with Dapper/MySqlConnector. Only the unrelated legacy Connector
configuration and Utility logging host are replaced by explicit test host types.
All database behavior uses the real MySQL provider and the existing schema files
in an exclusively labelled disposable Docker instance. Application settings,
production assembly references, legacy log sinks, runtime adapters, packets,
and gameplay are not proven by this mode. No schema definition or deployed database
was changed; fixture creation uses the existing DDL unchanged.

The default full-project test mode remains available and was attempted first.
It is blocked at the starting SHA by `AORebirth.Enums.Linux.csproj`'s imported
inventory referencing missing `AORebirth.Enums/ItemType.cs` (CS2001).

The approved legacy AOtomation wrapper with the MissionDaoArchitectureTests
filter was also attempted. Its test assembly does not compile due to unrelated
missing `CapturedEnemySpecialAttackWeaponPacketFixture.AggDef` and
`PlayfieldAnarchyFMessage.Unknown1` members. Test filtering cannot bypass that
assembly build failure. Full Windows/Linux acceptance is not claimed.

## Integration contract and remaining risks

Use IMissionDao from the future domain/runtime service, with the MySQL implementation
created at the composition boundary. Agree on those shared files with their owner
before making the deferred project/registration/adapter commit.

- Execute is synchronous. Never return Task/ValueTask, retain the transaction,
  use it across threads, or perform packet/inventory/playfield effects inside it.
  Propagate exceptions. There are no automatic retries or nested enlistment.
- Saved DTO versions are usable for further writes within the same callback;
  success is durable only after Execute returns. On failure, discard other result
  snapshots/domain copies and reload. A legacy adapter that copied a DTO version
  before failure must discard its domain copy too; that adapter was not changed here.
- A network failure during commit can leave an unknown durable outcome even when
  rollback is attempted. Reconcile by durable identity before retrying. Reuse
  observation, fee, and reward keys. Do not infer absence from an exception.
- Existing start-area methods retain false/null error behavior for compatibility.
  Do not use them as evidence of absence when the database is unavailable.
- MySQL is the tested dialect. MSSQL/PostgreSQL parity is unproven; the current
  configurable Connector does not itself enforce the DAO's MySQL-only SQL.
- Maintain repeatable-read isolation for coherent ReadCharacter snapshots. The
  injected factory should supply a fresh connection per DAO operation, not a shared
  connection with altered isolation or an unrelated active transaction.
- Persisted reward claims are not an atomic inventory/key grant. Coordinate with
  the new inventory writer and reconcile external effects in the later runtime work.
- Keep abandonment/history separate from physical character cleanup and leave
  mission entrance/zoning policy with its existing owner.
- Clear the recorded shared-build/guard failures, then run combined local lifecycle
  acceptance before any governed Linux promotion. This branch is not deployment acceptance.

## Files inspected

Read-only source and guidance inventory, in addition to the changed files above:

- `AGENTS.md`, `AI_START_HERE.md`, `docs/project/DEVELOPMENT_AUTHORITY.md`;
  relevant sections of PROJECT_STATE, CURRENT_TASK, KNOWN_DECISIONS, SUBSYSTEMS,
  ARCHITECTURE, and `docs/ai/WORKFLOW.md`.
- `DAO_REFACTOR_AUDIT.md`, `DAO_REFACTOR_ROADMAP.md`.
- `AORebirth/Libraries/Source/AORebirth.Database/DatabaseDaoFactory.cs`,
  `Connector.cs`, `Dao/IDao.cs`, and `Dao/CharacterDao.cs`.
- `AORebirth/Libraries/Source/AORebirth.Interfaces/IDatabaseConnector.cs`.
- All six `AORebirth.Database/SqlTables/mission*.sql` definitions and the validator's
  unchanged characters/stats fixture inputs.
- Legacy Interfaces/Database project references, DatabaseTests project;
  Linux Database/Interfaces/Enums projects and mission compile inventory entries;
  `WindowsBuildNet10/Projects/AORebirth.Interfaces.ZoneNew.WinNet10.csproj`.
- `MissionDaoArchitectureTests.cs`; current test inventory for
  `PersistentMissionFoundationTests.cs` and `QuestRuntimePersistenceTests.cs`.
- `ZoneEngine/Core/Missions/PersistentMissionService.cs` abandonment entry point,
  ZoneEngine/ChatEngine Program mission construction/readiness references.

## Discord-ready handoff

Mission persistence hardening is ready on `codex/mission-dao-persistence`.
The existing DAO now handles rollback/retries and failed writes more safely.
Isolated MySQL checks and mission boundary guard pass. ZoneEngine_New integration
is deferred; shared build/guard blockers are recorded in this report.

```text
ZONEENGINE_NEW_FILES_CHANGED: NO
DATABASE_SCHEMA_CHANGED: NO
RUNTIME_MISSION_LOGIC_CHANGED: NO
LIVE_DEPLOYMENT_PERFORMED: NO
```
