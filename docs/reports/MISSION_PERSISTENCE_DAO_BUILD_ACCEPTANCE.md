# Mission persistence DAO build-baseline acceptance

## Determination

`CONDITIONALLY_MERGE_READY_BASELINE_FAILURE` applies to
`codex/mission-dao-build-acceptance`, including the rollback-diagnostic repair in
this report's commit. It does **not** approve the unmodified original DAO commit:
the acceptance check discovered a reproducible rollback-error regression there.

The legacy Windows solution, Interfaces, and Database build successfully. The
real source-isolated MySQL suite passes 147 checks twice; the mission boundary,
guard self-tests, secret scan, whitespace, and generated-staleness gates pass.
There are 21 distinct remaining shared diagnostics, all reproduced identically
on the clean pre-DAO baseline and outside this task's ownership. Full-project
mission testing and the broader test assembly remain blocked. This is not a
claim that the full repository or ZoneEngine_New integration passes.

The companion [machine-readable acceptance artifact](MISSION_PERSISTENCE_DAO_BUILD_ACCEPTANCE.json)
records all 120 matrix/witness command executions, their commands, working
directories, exit codes, elapsed times, first errors, complete diagnostic counts,
raw-log SHA-256 hashes, individual failure classifications, and initial worktree
state. Every nonzero command is classified; no relevant UNKNOWN, environmental,
current-master-only, or nondeterministic failure remains in this matrix.

## Exact source provenance and worktree safety

| Identity | SHA |
|---|---|
| DAO_SHA | `3b58aa7e02636f99d63b1907c5b2bfbc5815f705` |
| DAO_REMOTE_SHA | `3b58aa7e02636f99d63b1907c5b2bfbc5815f705` |
| DAO_BASE_SHA | `cf1e12b894b1247b34f96f832b217c1cfb828213` |
| CURRENT_MASTER_SHA | `cf1e12b894b1247b34f96f832b217c1cfb828213` |

Remote heads were verified with `git ls-remote origin`. The baseline is proved
by `git merge-base` and the mission branch creation reflog, which names `cf1e12b8`;
it was not inferred solely from `DAO_SHA^`. Master and the DAO base happen to
coincide, but were tested in separate checkouts.

Primary checkout: `C:\Users\Mike\Documents\AORebirth`, branch `master`, HEAD
`cf1e12b8`; starting status contained only untracked `quest example from PRK.txt`.
No primary tracked/untracked work was changed. The original mission branch and
its worktree were not edited, merged, rebased, reset, cleaned, or stashed.
The machine artifact preserves the full registered-worktree inventory, including
an already-prunable historical entry, which was left alone.

Acceptance checkout:
`C:\Users\Mike\Documents\AORebirth\tools-temp\worktree-snapshots\mission-dao-build-acceptance`.
It began clean on a new branch from exact DAO_SHA. Comparison checkouts were
created detached at the recorded SHA:

| State | Independent clean attempts, relative to the primary checkout |
|---|---|
| DAO_BASE_SHA | `tools-temp/dao-triage-base-1`, `tools-temp/dao-triage-base-2` |
| DAO_SHA | `tools-temp/dao-triage-dao-1`, `tools-temp/dao-triage-dao-2` |
| CURRENT_MASTER_SHA | `tools-temp/dao-triage-master-1`, `tools-temp/dao-triage-master-2` |

Each started with empty porcelain status, no copied bin/obj/generated outputs,
its own temporary directory, and the same restore policy. Instead of deleting
outputs to repeat failures, attempt 2 uses another brand-new checkout. This
provides stronger output isolation and avoids destructive cleanup. All failing
matrix commands reproduced. The checkouts/logs remain available for inspection;
no recursive cleanup of developer worktrees was performed.

After testing, these six new, clean comparison checkouts were moved with
`git worktree move` into `tools-temp/worktree-snapshots/` with the same directory
names, so they do not add untracked noise to the primary checkout. Exact source
and destination paths, clean status, and SHA were checked before each move.
Recorded command working directories describe their locations at execution time;
`retained_worktree_parent` in the JSON identifies their retained location.
The final primary status again contains only the original untracked text file.

## Authority, toolchain, and execution policy

Inspected `AI_START_HERE.md`, repository/posted `AGENTS.md`,
`docs/ai/WORKFLOW.md`, `docs/project/DEVELOPMENT_AUTHORITY.md`, and the relevant
database-safety decisions in `docs/ai/KNOWN_DECISIONS.md`. Project entry points,
explicit source inventories, schema fixtures, DAO tests, and guard definitions
were checked against the actual tree.

- Windows 10.0.26200, win-x64; Git 2.54.0.windows.1.
- Visual Studio MSBuild 18.8.2.30814:
  `C:\Program Files\Microsoft Visual Studio\18\Community\MSBuild\Current\Bin\MSBuild.exe`.
- .NET SDK 10.0.302; SDK MSBuild 18.6.11+35b593beb; host .NET 10.0.10.
- Approved Python selector chose CPython 3.13.14, 64-bit. No global.json override.
- Legacy production projects target .NET Framework 4.8; the MySQL validation
  project targets net10.0 with C# 7.3. No SDK/package upgrades were made.
- Toolchain log hashes are identical across all three states and the repair.
- Build/test execution used `cmd.exe`, not PowerShell. The initial attachment
  read used the session's default shell before that pasted constraint was known;
  no builds or repository changes were performed through it.
- Relevant inherited `AO_REBIRTH_*` overrides were removed; only the selected
  Python path was restored. Test wrappers supply their own disposable-test
  acknowledgement. Same PATH and ordinary package caches; no copied project
  outputs or developer/live database credentials.
- `DOTNET_CLI_TELEMETRY_OPTOUT=1`, `DOTNET_NOLOGO=1`,
  `MSBUILDDISABLENODEREUSE=1`, `PYTHONDONTWRITEBYTECODE=1`, `PYTHONHASHSEED=0`.
  TEMP/TMP are each checkout's `build-verify/acceptance-temp`.

The documented `tools/build_aorebirth_debug.cmd` wrapper kills compiler processes
globally by image name. Running that cleanup while other developers are active
would violate isolation. Therefore this matrix uses the documented MSBuild leaf
commands with `/m:1 /nr:false`, under the required generated-combat read lease:

```text
<selected-python> <checkout>/Tools/generated_combat_pipeline.py --run-read-lease -- cmd.exe /d /c <recorded-command-file>
```

This substitution is explicit, not a changed build configuration, excluded
project, suppressed error, or weakened gate. No global process cleanup was run.
The normal full solution entry point remains unchanged. Its existing project
membership is not proof of a ZoneEngine_New host build.

## Commands and results

For the following table, `MSBUILD` is the exact Visual Studio executable above.
Every MSBuild command uses `/p:Configuration=Debug /m:1 /nr:false /v:minimal`.
Exact expanded commands and raw command-file paths are in the JSON artifact.
Both clean attempts have the same outcome; the repair reran the same gates.

| Command / target | Base x2 | DAO x2 | Master x2 | Repair |
|---|---|---|---|---|
| MSBUILD `AORebirth/AORebirth.sln` `/t:Restore /p:RestorePackagesConfig=true` | PASS | PASS | PASS | PASS |
| MSBUILD `AORebirth/AORebirth.sln` `/t:Build` | PASS | PASS | PASS | PASS |
| MSBUILD `AORebirth/Libraries/Source/AORebirth.Interfaces/AORebirth.Interfaces.csproj` `/t:Build` | PASS | PASS | PASS | PASS |
| MSBUILD `AORebirth/Libraries/Source/AORebirth.Database/AORebirth.Database.csproj` `/t:Build` | PASS | PASS | PASS | PASS |
| `call tools\run_aotomation_messaging_tests.cmd` (unfiltered) | FAIL | FAIL | FAIL | Same FAIL |
| `dotnet restore Tools/MissionDaoValidation/MissionDaoValidation.csproj` | PASS | PASS | PASS | PASS |
| `dotnet build Tools/MissionDaoValidation/MissionDaoValidation.csproj --configuration Release --no-restore` | FAIL | FAIL | FAIL | Same FAIL |
| `call Tools\run_mission_dao_validation.cmd` (full project references) | FAIL | FAIL | FAIL | Same FAIL |
| `call Tools\run_dao_architecture_guard.cmd` (global) | FAIL | FAIL | FAIL | Same FAIL |
| `call Tools\scan_secrets.cmd` | PASS | PASS | PASS | PASS |
| `git diff --check` | PASS | PASS | PASS | PASS |
| `call Tools\generate_capture_backed_npc_combat_inventory.cmd --check` | PASS | PASS | PASS | PASS |
| `call Tools\generate_mission_level_graph.cmd --check` | PASS | PASS | PASS | PASS |
| `call Tools\run_mission_dao_validation.cmd --isolated-sources` | Unsupported | 131 PASS x2 | Unsupported | 147 PASS x2 |
| `call Tools\run_dao_architecture_guard.cmd --mission-persistence-only` | Unsupported | PASS x2 | Unsupported | PASS |

The isolated switches were added by DAO_SHA and do not exist on the baseline.
They are supplemental validation, not replacements for the explicitly failed
full-project/global commands. The unfiltered AOtomation assembly includes
`PersistentMissionFoundationTests`, `QuestRuntimePersistenceTests`, mission
adapter tests, and other persistence coverage. Its compilation fails before
any tests execute; none were selectively excluded. Historical `DatabaseTests`
contains only AssemblyInfo.cs and no executable test cases, so is not claimed
as persistence coverage.

The solution restore/build emits one existing NU1510 warning concerning
System.Text.Encoding.CodePages in RDBDataExtractor. Interfaces and Database
builds emit zero warnings. Full-reference mission build emits 30 warning
occurrences (including repeated build-summary diagnostics), unchanged between
states. AOtomation has 18 distinct error diagnostics and zero warnings;
full-reference mission build has two occurrences of one distinct CS2001 error;
its run wrapper has one occurrence; the global guard has two violations.
Counts are complete diagnostic-line counts, not guessed from the first error.

## Failure inventory and attribution

All shared rows below appear in both clean attempts of base, DAO, and master,
and again after the repair. Full source paths, columns, normalized diagnostic
text, SHA-256 signatures, command IDs, per-state observations, and suggested
owners/actions are recorded individually in the JSON (21 separate entries).

| Source and lines | Diagnostic | Classification | Owner / smallest next action |
|---|---|---|---|
| `CapturedEnemyCombatGeneratedPacketFixtureTests.cs` 89, 194 | CS1061: fixture lacks AggDef | BASELINE_PREEXISTING | Combat/capture fixture owner: reconcile test/model contract with evidence |
| `CapturedEnemyCombatPacketFactoryTests.cs` 354, 506, 574, 733, 822, 1019, 1106, 1203, 1290, 1380, 1468, 1936, 2034 | CS1061: fixture lacks AggDef | BASELINE_PREEXISTING | Same; do not remove assertions or invent a packet field |
| `OrdinaryEnemyCombatSetupGeneratorTests.cs` 840, 1091 | CS1061: fixture lacks AggDef | BASELINE_PREEXISTING | Same |
| `N3RecoveredContractTests.cs` 483 | CS0117: PlayfieldAnarchyFMessage lacks Unknown1 | BASELINE_PREEXISTING | Packet contract owner: reconcile the authoritative model and test |
| `LinuxBuild/source-inventory/AORebirth.Enums.CompileItems.props` 18 | CS2001: included AORebirth.Enums/ItemType.cs does not exist | BASELINE_PREEXISTING | LinuxBuild/Enums owner: reconcile explicit inventory with canonical sources |
| `ZoneEngine_New/Core/Data/MySqlCharacterRepository.cs` | NEW_VIOLATION: direct runtime SQL | BASELINE_PREEXISTING | ZoneEngine_New owner: resolve architecture boundary in separate reviewed change |
| `ZoneEngine_New/Core/Data/MySqlStatRepository.cs` | NEW_VIOLATION: direct runtime SQL | BASELINE_PREEXISTING | Same; no blanket guard suppression/baseline expansion here |
| `MySqlMissionDao.cs` 192 at DAO_SHA | Rollback exception hidden | DAO_INTRODUCED, fixed here | Mission persistence owner: retain secondary exception while rethrowing original |

The first four rows live under
`AORebirth/Libraries/Source/AOtomation/AOtomation.Messaging/src/SmokeLounge.AOtomation.Messaging.Tests/`;
ZoneEngine_New paths are under `AORebirth/Server/`.
These are ownership recommendations, not claims about which person authored the
failures. No pre-existing failure was repaired or silently accepted as green.
All shared failures block full combined integration acceptance, not the
independent passing legacy DAO builds or isolated persistence suite.

### Proven DAO regression and minimal repair

An identical source-linked witness ran twice per state, compiling the actual
DAO and neutral contract sources against the same provider packages/toolchain.
It injects an operation exception and an independently failing rollback through
IDbConnection/IDbTransaction, then checks whether the rollback exception remains
observable in the thrown exception graph/Data. This is a failure-injection test,
not a claimed substitute for real MySQL integration.

- Base/master: rollback visible, witness exit 0. Historically it masked the
  original exception; the secondary ORIGINAL_VISIBLE=FAIL marker describes that
  historical behavior, not the witness's rollback assertion.
- Original DAO: rollback hidden, witness exit 1, reproducible twice.
- Acceptance repair: original and rollback exceptions both visible, exit 0 twice.

Raw witness attempts 1/2 were retained; their aggregate metadata was superseded
during orchestration. Baseline/DAO/master were therefore rerun in fresh witness
project outputs as attempts 3/4. Those fully recorded runs and repaired attempts
1/2 are the eight witness records used by this report. No historical failed raw
log was erased or rewritten.

The only production repair changes Execute's catch blocks to retain the rollback
exception in the original exception's `Data["MissionDao.RollbackFailure"]`, then
rethrow the original exception with its stack intact. The interface documentation
names that diagnostic and the need to reconcile before retry. No new provider
type, transaction object, runtime object, or changed method signature crosses the
interface. Failed rollback/commit still means the durable outcome may be unknown;
restoring DTO versions is not proof of successful database rollback.

## Independent DAO change-set inventory

All ten files in `DAO_BASE_SHA..DAO_SHA` were inspected, with no unrelated file:

| File | Classification / conclusion |
|---|---|
| `AORebirth/Libraries/Source/AORebirth.Interfaces/Persistence/Missions/IMissionDao.cs` | Mission persistence interface; documentation-only contract clarification |
| `AORebirth/Libraries/Source/AORebirth.Database/Domain/Missions/MySqlMissionDao.cs` | MySQL mission DAO; transaction/validation hardening, no gameplay calculations |
| `Tools/MissionDaoValidation/HardeningChecks.cs` | DAO tests; real provider rollback/concurrency/isolation and narrow failure injection |
| `Tools/MissionDaoValidation/IsolatedHost.cs` | DAO test-only connector/logging shim; production SQL is not mocked |
| `Tools/MissionDaoValidation/MissionDaoValidation.csproj` | DAO test build; opt-in links actual sources; full references remain the default |
| `Tools/MissionDaoValidation/Program.cs` | DAO test runner; invokes added checks and reports source-isolated/full mode |
| `Tools/run_mission_dao_validation.cmd` | DAO test wrapper; explicit source-isolated switch, safe disposable acknowledgement |
| `Tools/DaoArchitectureGuard/dao_architecture_guard.py` | Architecture guard; checks persistence boundaries and negative fixtures |
| `Tools/run_dao_architecture_guard.cmd` | Guard wrapper; self-tests retained, global mode remains the default |
| `docs/reports/MISSION_PERSISTENCE_DAO_HANDOFF.md` | Contract/documentation; prior handoff and deferred integration |

This acceptance branch changes only MySqlMissionDao.cs, IMissionDao.cs,
HardeningChecks.cs, and these two acceptance reports. The original DTO signatures,
factory, project references, schema files, migrations, deployment scripts,
configuration, root solution, PROJECT_STATE, runtime code, packets, players,
sessions, playfields, handlers, and zoning are unchanged.

## MySQL acceptance and required failure behaviors

The existing wrapper creates a purpose-labelled disposable Docker container,
internal network and volume, a loopback-only port 33069, database
`aorebirth_mission_dao_validation`, and generated test credentials. Image is
`mysql@sha256:c592c15aaf4a1961e15d82eb31ea5987dda862d1c4b1e93424438c0e91dc1f8d`.
It initializes the unchanged characters, stats, missionstates,
missionobjectiveprogress, missionobjectiveobservations, missionflags,
missionaccountflags, and missionrewardledger SQL fixtures from
AORebirth.Database/SqlTables. It removes only its own created disposable resources.
No persistent developer/live schema or production credentials are used.

| Suite | Pass | Fail | Repetitions |
|---|---:|---:|---:|
| Exact original DAO isolated suite | 131 | 0 | 2 |
| Acceptance suite, all old checks retained | 147 | 0 | 2 |

The exact increase is 16: one rollback-diagnostic assertion; five child-provider
failure/rollback assertions; four cooperative callback-cancellation assertions;
two real-provider read-error assertions; four caught affected-row-mismatch
assertions. No existing assertion was weakened or removed.

| Required behavior | Passing evidence |
|---|---|
| Child-write failure cannot commit parent | Real latin1 conversion failure after parent insert; caught exception poisons transaction; fresh parent/child reads find neither row |
| Rollback failure visible | Original exception identity plus secondary exception identity assertion; independent baseline witness |
| Caught write cannot report success | Existing duplicate-write and ledger-after-stat failures, plus caught child write and row mismatch |
| Observation/read errors do not become absence | Existing invalid observation conversion surfaces; new queries against information_schema throw provider errors without altering schema |
| Cancellation has no partial state | Token cancelled/checked inside synchronous callback after parent/child writes; OperationCanceledException escapes; versions restored; fresh reads find neither row |
| Affected-row mismatch is not success | Existing stale-version tests plus a caught zero-row update poisons transaction and rolls back prior insert |
| Fresh DAO/connection reproduces state | Existing lifecycle/snapshot/reward/fee checks plus newly constructed DAO and fresh connections for rollback verification |

Duplicate observation keys, repeated reward applications, simultaneous claims,
expired claim/retry tokens, repeated roll fees, cross-character/account ownership,
DTO rollback/retry versions, scope lifetime, atomic stat/ledger writes, and seven
mutation cut points remain covered. Cancellation here is cooperative callback
cancellation; the synchronous contract has no asynchronous cancellation-token
API. No claim is made about forcibly terminating an in-flight MySQL command.

## Architecture, schema, and remaining integration risks

`call Tools\run_dao_architecture_guard.cmd --mission-persistence-only`: PASS,
including positive/negative self-tests. Neutral mission contracts expose no
database/provider/SQL/engine/session/packet/playfield types. DAO sources contain
no engine, handler, packet, player/session, playfield, or AOSharp dependencies.
Database transaction objects remain internal. DAO code persists caller-supplied
values; it does not choose QL, offers, reward amounts, probabilities, mission
types, objectives, destinations, or layouts.

Mission SQL remains in the Database project, including pre-existing character
deletion cleanup in Database/Dao/CharacterDao.cs. Legacy engine startup contains
table-name readiness lists, not newly introduced mission SQL. The global guard
still fails on the two non-mission ZoneEngine_New sites listed above; no guard
exceptions were added. Source review complements the guard: token checks alone
are not a formal proof of runtime independence.

There are no changed .sql files, schema definitions, or migrations in either
the original DAO change set or this acceptance repair. Disposable fixture
initialization is test setup, not a production schema change.

Remaining prerequisites for a later, small integration task:

1. Accept the DAO hardening **with this rollback repair**, not DAO_SHA alone.
2. Separately resolve the explicit Enums source inventory, packet/fixture test
   mismatches, and ZoneEngine_New global guard violations with their owners.
   Rerun full-reference mission tests and the unfiltered AOtomation assembly.
3. After ZoneEngine_New work stabilizes, agree on its neutral mission contract
   project/reference surface. Its current tailored net10 project graph is not
   validated by these legacy production builds or the source-isolated shim.
4. Only in that later integration commit, connect the thin neutral adapter and
   agreed DAO registration. Keep runtime effects outside synchronous Execute,
   respect ownership/version/idempotency semantics, and reconcile unknown outcomes.
5. Run combined local login/reload, accept/abandon, key, mission entrance and
   multi-zone tests with Mike. No game/client was launched here. Linux deployment
   requires a separately authorized exact-SHA acceptance/release task.

Legacy start-area convenience methods still log and return false/null on errors,
as documented before this repair. TryChargeRollFee's pre-existing rollback catch
also predates DAO_SHA. This task did not broaden the Execute repair to those
compatibility paths; future callers must not treat their fallback as proof of
absence/success. Source-isolated testing does not validate host configuration,
logging, DI, runtime adapters, or all production assembly references.

## Evidence retention and files inspected

Raw artifacts use the established ignored `build-verify` convention:

```text
C:\Users\Mike\Documents\AORebirth\tools-temp\worktree-snapshots\mission-dao-build-acceptance\build-verify\dao-acceptance
```

`base/{1,2}`, `dao/{1,2}`, and `master/{1,2}` hold complete original command files
and logs. `acceptance/acceptance/{1,2}` holds repair checks; `witness/` holds the
identical rollback-visibility source and its generated project builds. Top-level
metadata.json/results.json, acceptance/results.json, witness/results.json and
the orchestration scripts are retained locally. The committed JSON indexes every
used raw log by relative path and full SHA-256 and fingerprints the three tested
repair sources. Large transient logs/build outputs are not committed. Preserve
this ignored directory when archiving the local evidence.

Post-report source-hash/boundary checks, the staged whitespace check, and secret
scan are recorded separately in `final-gates/results.json` under that raw root.

Besides the ten DAO files above, inspected inputs included the authority docs,
legacy solution/Interfaces/Database and mission validation projects,
DatabaseDaoFactory, DatabaseTests project/file inventory, MissionDaoArchitectureTests,
PersistentMissionFoundationTests, QuestRuntimePersistenceTests, Linux Enums
project/source inventory, existing SQL fixture initialization and mission tables,
the shared compiler diagnostics, the mission/global guard scans, CharacterDao
cleanup, legacy startup readiness lists, and the ZoneEngine_New reference surface
(read-only). No unrelated dirty-file classification or live investigation occurred.

## Boundary statements

ZONEENGINE_NEW_FILES_CHANGED: NO

DATABASE_SCHEMA_CHANGED: NO

RUNTIME_MISSION_LOGIC_CHANGED: NO

LIVE_DEPLOYMENT_PERFORMED: NO
