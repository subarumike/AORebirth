# Mission DAO parallel handoff

## Outcome and scope

The existing SQL-backed mission persistence contract is complete for the roadmap's mission vertical slice. No second DAO, speculative API, external facade, DI framework, schema migration, runtime adapter, or ZoneEngine wiring was added. Integration-ready means the **persistence contract is ready for the runtime owner to consume**; it does not mean ZoneEngine_New is integrated, full Linux acceptance passes, or generated-mission file persistence has been migrated.

Original accepted branch: codex/mission-dao-build-acceptance at 19f6122a0e19e17a1db017675b386a2506fc81cf, preserved unchanged.

- Starting primary checkout: master at cf1e12b894b1247b34f96f832b217c1cfb828213, with pre-existing untracked "quest example from PRK.txt". It was not modified.
- Starting/clean comparison SHA: 19f6122a0e19e17a1db017675b386a2506fc81cf.
- Working branch: codex/mission-dao-parallel-ready.
- Working checkout: C:\Users\Mike\Documents\AORebirth\tools-temp\worktree-snapshots\mission-dao-parallel-ready.
- Detached clean baseline: sibling mission-dao-parallel-base.
- Accepted/persistence/other registered developer worktrees were preserved. No reset, clean, stash, rebase, merge, pruning, deployment, schema write, or client control occurred.
- No changes under either ZoneEngine project, their references, root solution, shared project-state/task documents, deployment scripts, or unrelated DAO domains.
- The finishing SHA is the commit containing this report (see git history); validation ran against its source changes before commit.

Read completely: DAO_REFACTOR_AUDIT.md, DAO_REFACTOR_ROADMAP.md, and docs/reports/MISSION_PERSISTENCE_DAO_BUILD_ACCEPTANCE.md. Repository startup/workflow instructions were also read. Conclusions are grounded in this checkout, not another developer's unfinished branch.

## Architecture and implementation completed

    runtime handler -> runtime/domain service -> IMissionDao -> MySqlMissionDao -> Connector/MySQL

| Production path | Responsibility / change |
| --- | --- |
| AORebirth/Libraries/Source/AORebirth.Interfaces/Persistence/Missions/IMissionDao.cs | Existing neutral DTOs, keys, statuses, DAO and transaction contract. Public signatures unchanged; ordering, authentication boundary, lifecycle and fee error behavior clarified. |
| AORebirth/Libraries/Source/AORebirth.Database/Domain/Missions/MySqlMissionDao.cs | Single implementation, parameterized Dapper SQL and owned transactions. Configured default connection rejects non-MySQL before mission SQL; secondary rollback errors now preserved for roll fees as well as Execute. |
| AORebirth/Libraries/Source/AORebirth.Database/DatabaseDaoFactory.cs | Existing non-generic CreateMissionDao retained; lazy construction/ownership documented. No new API/framework. |
| AORebirth/Libraries/Source/AORebirth.Database/Dao/NewCharacterStartAreaSelectionDao.cs | Three public methods/constants retained as forwarding compatibility shim; duplicate SQL removed. MySqlMissionDao owns the one implementation. |

The Windows Interfaces/Database projects already include these files and references; no production project changes are needed. Interfaces retains no Database dependency. Database retains Interfaces, existing enums/utility infrastructure, Dapper and MySqlConnector. Linux inventories already include these existing source files. The isolated test project links these actual sources, including the compatibility shim, and does not reimplement the DAO.

### Construction and provider limits

Use DatabaseDaoFactory.CreateMissionDao() at the **composition root only**, then pass IMissionDao into the runtime. The factory has no gameplay dependencies and performs no database operation at construction. A DAO instance owns no persistent connection; every public operation obtains/disposes a fresh connection. Runtime handlers must not receive a connection factory or construct a concrete DAO.

The default constructor obtains the configured connection from Connector.GetConnection(). Anything other than a MySqlConnector.MySqlConnection is disposed and rejected before mission SQL. Connector may already have opened that connection; rejection is not promised before acquisition. Connector's own upstream failure/cleanup behavior was not redesigned. The injected Func<IDbConnection> constructor is an implementation/testing seam: decorators are accepted, but callers must supply a fresh owned MySQL-capable connection per operation, never a shared live connection.

MySQL is the only demonstrated implementation. ON DUPLICATE KEY, locking, affected rows, schema collation and MySQL error codes remain intentional. **No MSSQL/PostgreSQL parity is claimed.** The unsupported-provider test uses an ADO double, not either server. Source-isolated factory tests use a test-only Connector host, not production configuration or engine startup.

## Exact contract

Source of truth: AORebirth/Libraries/Source/AORebirth.Interfaces/Persistence/Missions/IMissionDao.cs, namespace AORebirth.Interfaces.Persistence.Missions. No SQL, table/column names, provider/ADO types, runtime entities, lazy queries, gameplay definitions, packets or engine assemblies cross this boundary.

    public interface IMissionDao
    {
        MissionStateData GetMission(MissionKeyData key);
        IList<MissionStateData> GetMissions(int characterId);
        MissionCharacterSnapshotData ReadCharacter(int characterId);
        string ResolveCharacterAccountKey(int characterId);
        MissionAccountFlagData GetAccountFlag(string accountKey, string flagKey);
        IList<MissionAccountFlagData> GetAccountFlags(string accountKey);
        MissionRollFeeResult TryChargeRollFee(MissionRollFeeRequest request);
        bool MarkStartAreaSelectionPending(int characterId);
        string GetStartAreaSelectionState(int characterId);
        bool TryCompleteStartAreaSelection(int characterId, string selectedState);
        T Execute<T>(int characterId, Func<IMissionDaoTransaction, T> operation);
        T Execute<T>(int characterId, string accountKey, Func<IMissionDaoTransaction, T> operation);
    }

    public interface IMissionDaoTransaction
    {
        int CharacterId { get; }
        string AccountKey { get; }
        MissionStateData GetMission(MissionKeyData key);
        IList<MissionStateData> GetMissions(int characterId);
        void SaveMission(MissionKeyData key, MissionStateData record);
        MissionObjectiveProgressData GetObjective(MissionObjectiveKeyData key);
        void SaveObjective(MissionObjectiveKeyData key, MissionObjectiveProgressData record);
        bool TryAddObservation(MissionObjectiveObservationData observation);
        MissionFlagData GetFlag(MissionKeyData key, string flagKey);
        void SaveFlag(MissionKeyData key, MissionFlagData flag);
        MissionAccountFlagData GetAccountFlag(string accountKey, string flagKey);
        void SaveAccountFlag(string accountKey, MissionAccountFlagData flag);
        MissionRewardStageData GetReward(MissionRewardKeyData key);
        MissionRewardClaimResultData TryClaimReward(MissionRewardKeyData key,
            string rewardType, string claimToken, long claimedAtUtcTicks, long claimExpiresAtUtcTicks);
        bool TryMarkRewardApplied(MissionRewardKeyData key, string claimToken,
            long expectedVersion, string effectReference, long appliedAtUtcTicks, out MissionRewardStageData stage);
        bool TryMarkRewardFailed(MissionRewardKeyData key, string claimToken,
            long expectedVersion, string error, long failedAtUtcTicks, out MissionRewardStageData stage);
        MissionAtomicStatRewardResultData TryApplyCharacterStatReward(MissionRewardKeyData key,
            string rewardType, IList<MissionStatMutationData> mutations,
            string effectReference, long appliedAtUtcTicks);
    }

### DTO and result mapping

- MissionKeyData = CharacterId + QuestId; objective/reward keys extend it with ObjectiveId/RewardKey.
- MissionStateData = CharacterId, QuestId, State, CurrentStepId, OfferedAtUtcTicks, AcceptedAtUtcTicks, CompletedAtUtcTicks, FailedAtUtcTicks, AbandonedAtUtcTicks, CreatedAtUtcTicks, UpdatedAtUtcTicks, Version.
- MissionObjectiveProgressData = CharacterId, QuestId, ObjectiveId, Progress, RequiredCount, LastObservationKey, CreatedAtUtcTicks, UpdatedAtUtcTicks, Version.
- MissionObjectiveObservationData carries character/quest/objective identity plus ObservationKey, EventType, SourceIdentity, TargetIdentity and ObservedAtUtcTicks; it is not a packet or observation dispatcher.
- MissionFlagData = character/quest/flag key, nullable Value, creation/update ticks and Version. MissionAccountFlagData = AccountKey, FlagKey, nullable Value/SourceQuestId, creation/update ticks and Version.
- MissionRewardStageData carries reward key/type, Status, Attempts, error/effect/claim data, claim/applied/creation/update ticks and Version.
- MissionStatMutationData = StatIdentityType, StatId, Kind, Value, MinimumValue, MaximumValue. MissionStatValueData = StatIdentityType, StatId, Value.
- MissionCharacterSnapshotData buffers/clones CharacterId + Missions/Objectives/Flags/Rewards. Observations/account flags are not silently added.
- MissionRewardClaimResultData = Status, Stage, Message. MissionAtomicStatRewardResultData additionally carries StatValues.
- MissionRollFeeRequest = CharacterType, CharacterId, BatchIdentity, Fee, AppliedAtUtcTicks. Result = Status, CashBefore, CashAfter, Failure.
- Lifecycle values remain Offered=1, Active=2, Completed=3, Failed=4, Abandoned=5. No Expired enum was invented. Existing reward/claim/fee enum values remain unchanged.
- Start-area constants remain pending, arete, icc_shuttleport. TryComplete requires exact lowercase completed values.

### Durable semantics callers must preserve

Missing single records return null; empty collections are buffered/non-null. Provider failures propagate, **except** the three legacy start-area convenience methods, which log and return false/null. Those fallbacks cannot distinguish absence from outage.

Ordering uses SQL durable keys under database collation: missions QuestId; objectives QuestId,ObjectiveId; flags QuestId,FlagKey; rewards QuestId,RewardKey; account flags FlagKey. Tests use controlled ASCII order; no universal ordinal/non-ASCII/provider-independent ordering promise is made.

Execute owns a synchronous transaction. Character-only scope rejects cross-character keys but is not session authentication. Account scope additionally locks/checks the character's Username ownership before account writes. Account-key reads still require caller authorization. Do not retain/share scopes, return Task, nest DAO transactions or run packets/items/playfield actions inside callbacks.

New versioned DTOs use Version <= 0; updates require the current positive Version and exact affected rows. Failed SQL writes/stale affected-row checks poison the scope even if caught inside the callback. Rollback restores Version on the **DTO instances passed to the DAO**, not arbitrary copies or all other mutated fields. Reload/discard results after failure. Execute and TryChargeRollFee preserve the original exception and attach a secondary rollback exception to Exception.Data["MissionDao.RollbackFailure"].

A commit exception does not prove rollback: commit may already be durable. Tests simulate both pre-commit failure and a lost acknowledgement after real MySQL commit. Reconcile by durable keys before retrying; do not invent a new fee/reward/observation identity or blindly replay external effects.

## Tables and operations represented

Existing definitions are in AORebirth/Libraries/Source/AORebirth.Database/SqlTables; none changed.

| Table | Existing DAO responsibility |
| --- | --- |
| missionstates | Keyed reads, snapshot, versioned insert/update of caller-selected lifecycle/timestamps. |
| missionobjectiveprogress | Keyed/snapshot reads; versioned progress insert/update. |
| missionobjectiveobservations | Parameterized insert with durable duplicate-key detection; observation/progress may share Execute. |
| missionflags | Keyed/snapshot reads, versioned flag writes, start-area conditional operations. |
| missionaccountflags | Account-key reads/scoped versioned writes, including completion+account flag. |
| missionrewardledger | Reward claim/lease, applied/failed compare-and-set, snapshot, atomic stat reward and roll-fee ledger. |
| stats | Existing character-stat reward mutations and roll-fee cash debit; no generic stats DAO. |
| characters | Read Username; lock/check character-account ownership. No character mutation API. |

Not applicable: physical per-mission delete and automatic expiry. Abandonment/completion are existing versioned lifecycle writes. Physical cleanup in Database/Dao/CharacterDao.cs belongs to character deletion and remains untouched; do not split it into independent mission DAO calls. Generated accepted mission removal/expiry are separate runtime-owned file behavior below. No runtime payload was hidden in flags to evade the unchanged-schema boundary.

## Deferred SQL integration map

**DEFERRED_ZONEENGINE_CALL_SITES=47** counts explicit SQL-contract/composition review points below: 21 adapter calls, 16 service repository calls, 4 reward coordinator calls, 4 direct convenience/account calls and 2 construction points. It does not mean 47 broken/unwired calls or 47 necessary edits. Accepted legacy ZoneEngine already uses the DAO at these sites. Future ZoneEngine_New integration should reuse their semantics after its owner releases the files; new-engine target paths must be chosen by that owner, not invented here.

Paths/line numbers describe unchanged ZoneEngine source at START_SHA. Nested service transaction operations are listed in their enclosing transaction row and mapped individually by TransactionAdapter rows, rather than counted twice as independent transactions.

### Z01 — GetMission

```text
CURRENT_ZONEENGINE_FILE=AORebirth/Server/ZoneEngine/Core/Missions/MissionDaoRepositoryAdapter.cs:28
CURRENT_METHOD=GetMission
CURRENT_LEGACY_PERSISTENCE_CALL=missionDao.GetMission
TARGET_IMISSIONDAO_METHOD=IMissionDao.GetMission
INPUT_MAPPING=MissionKey(CharacterId, QuestId) -> MissionKeyData
RETURN_MAPPING=MissionStateData/null -> MissionStateRecord/null
ERROR_BEHAVIOR=Propagate DAO/validation errors; never translate a provider error into missing data.
TRANSACTION_EXPECTATION=Buffered read, one owned connection; no runtime side effects.
BEHAVIORAL_NOTES=MissionDataMapper copies fields and casts enum numeric values; no gameplay inference.
```

### Z02 — GetMissions

```text
CURRENT_ZONEENGINE_FILE=AORebirth/Server/ZoneEngine/Core/Missions/MissionDaoRepositoryAdapter.cs:33
CURRENT_METHOD=GetMissions
CURRENT_LEGACY_PERSISTENCE_CALL=missionDao.GetMissions
TARGET_IMISSIONDAO_METHOD=IMissionDao.GetMissions
INPUT_MAPPING=characterId unchanged
RETURN_MAPPING=IList<MissionStateData> -> ordered IList<MissionStateRecord>
ERROR_BEHAVIOR=Propagate DAO/validation errors; never translate a provider error into missing data.
TRANSACTION_EXPECTATION=Buffered read, one owned connection; no runtime side effects.
BEHAVIORAL_NOTES=MissionDataMapper copies fields and casts enum numeric values; no gameplay inference.
```

### Z03 — ReadCharacter

```text
CURRENT_ZONEENGINE_FILE=AORebirth/Server/ZoneEngine/Core/Missions/MissionDaoRepositoryAdapter.cs:38
CURRENT_METHOD=ReadCharacter
CURRENT_LEGACY_PERSISTENCE_CALL=missionDao.ReadCharacter
TARGET_IMISSIONDAO_METHOD=IMissionDao.ReadCharacter
INPUT_MAPPING=characterId unchanged
RETURN_MAPPING=Snapshot CharacterId, Missions, Objectives, Flags, Rewards -> MissionCharacterSnapshot
ERROR_BEHAVIOR=Propagate DAO/validation errors; never translate a provider error into missing data.
TRANSACTION_EXPECTATION=One owned read transaction for the four collections.
BEHAVIORAL_NOTES=MissionDataMapper copies fields and casts enum numeric values; no gameplay inference.
```

### Z04 — GetAccountFlag

```text
CURRENT_ZONEENGINE_FILE=AORebirth/Server/ZoneEngine/Core/Missions/MissionDaoRepositoryAdapter.cs:43
CURRENT_METHOD=GetAccountFlag
CURRENT_LEGACY_PERSISTENCE_CALL=missionDao.GetAccountFlag
TARGET_IMISSIONDAO_METHOD=IMissionDao.GetAccountFlag
INPUT_MAPPING=accountKey, flagKey unchanged
RETURN_MAPPING=MissionAccountFlagData/null -> MissionAccountFlagRecord/null
ERROR_BEHAVIOR=Propagate DAO/validation errors; never translate a provider error into missing data.
TRANSACTION_EXPECTATION=Buffered read, one owned connection; no runtime side effects.
BEHAVIORAL_NOTES=MissionDataMapper copies fields and casts enum numeric values; no gameplay inference.
```

### Z05 — GetAccountFlags

```text
CURRENT_ZONEENGINE_FILE=AORebirth/Server/ZoneEngine/Core/Missions/MissionDaoRepositoryAdapter.cs:48
CURRENT_METHOD=GetAccountFlags
CURRENT_LEGACY_PERSISTENCE_CALL=missionDao.GetAccountFlags
TARGET_IMISSIONDAO_METHOD=IMissionDao.GetAccountFlags
INPUT_MAPPING=accountKey unchanged
RETURN_MAPPING=IList<MissionAccountFlagData> -> ordered IList<MissionAccountFlagRecord>
ERROR_BEHAVIOR=Propagate DAO/validation errors; never translate a provider error into missing data.
TRANSACTION_EXPECTATION=Buffered read, one owned connection; no runtime side effects.
BEHAVIORAL_NOTES=MissionDataMapper copies fields and casts enum numeric values; no gameplay inference.
```

### Z06 — Execute<T>(characterId, accountKey, operation)

```text
CURRENT_ZONEENGINE_FILE=AORebirth/Server/ZoneEngine/Core/Missions/MissionDaoRepositoryAdapter.cs:66
CURRENT_METHOD=Execute<T>(characterId, accountKey, operation)
CURRENT_LEGACY_PERSISTENCE_CALL=missionDao.Execute + TransactionAdapter
TARGET_IMISSIONDAO_METHOD=IMissionDao.Execute<T>(characterId, accountKey, operation)
INPUT_MAPPING=Same character/account scope; adapt synchronous callback; character-only overload at51 passes null account.
RETURN_MAPPING=T unchanged; returned records valid only after successful commit.
ERROR_BEHAVIOR=Propagate; failed writes poison the scope even if caught. Reload after any failed Execute; secondary rollback error remains inspectable.
TRANSACTION_EXPECTATION=One connection and one transaction around the entire callback.
BEHAVIORAL_NOTES=No nested transactions, retained scope, async callbacks or packet/item effects. See rollback-copy risk below.
```

### Z07 — TransactionAdapter.GetMission

```text
CURRENT_ZONEENGINE_FILE=AORebirth/Server/ZoneEngine/Core/Missions/MissionDaoRepositoryAdapter.cs:98
CURRENT_METHOD=TransactionAdapter.GetMission
CURRENT_LEGACY_PERSISTENCE_CALL=transaction.GetMission
TARGET_IMISSIONDAO_METHOD=IMissionDaoTransaction.GetMission
INPUT_MAPPING=MissionKey -> MissionKeyData
RETURN_MAPPING=MissionStateData/null -> MissionStateRecord/null
ERROR_BEHAVIOR=Propagate DAO/validation errors; never translate a provider error into missing data.
TRANSACTION_EXPECTATION=Uses the existing outer Execute connection/transaction, never a separate DAO connection.
BEHAVIORAL_NOTES=MissionDataMapper copies fields and casts enum numeric values; no gameplay inference.
```

### Z08 — TransactionAdapter.GetMissions

```text
CURRENT_ZONEENGINE_FILE=AORebirth/Server/ZoneEngine/Core/Missions/MissionDaoRepositoryAdapter.cs:103
CURRENT_METHOD=TransactionAdapter.GetMissions
CURRENT_LEGACY_PERSISTENCE_CALL=transaction.GetMissions
TARGET_IMISSIONDAO_METHOD=IMissionDaoTransaction.GetMissions
INPUT_MAPPING=characterId unchanged
RETURN_MAPPING=IList<MissionStateData> -> ordered IList<MissionStateRecord>
ERROR_BEHAVIOR=Propagate DAO/validation errors; never translate a provider error into missing data.
TRANSACTION_EXPECTATION=Uses the existing outer Execute connection/transaction, never a separate DAO connection.
BEHAVIORAL_NOTES=MissionDataMapper copies fields and casts enum numeric values; no gameplay inference.
```

### Z09 — TransactionAdapter.SaveMission

```text
CURRENT_ZONEENGINE_FILE=AORebirth/Server/ZoneEngine/Core/Missions/MissionDaoRepositoryAdapter.cs:109
CURRENT_METHOD=TransactionAdapter.SaveMission
CURRENT_LEGACY_PERSISTENCE_CALL=transaction.SaveMission
TARGET_IMISSIONDAO_METHOD=IMissionDaoTransaction.SaveMission
INPUT_MAPPING=MissionKey -> MissionKeyData; MissionStateRecord -> MissionStateData including Version
RETURN_MAPPING=void; CopyBack(data, record) updates Version
ERROR_BEHAVIOR=Propagate; failed writes poison the scope even if caught. Reload after any failed Execute; secondary rollback error remains inspectable.
TRANSACTION_EXPECTATION=Uses the existing outer Execute connection/transaction, never a separate DAO connection.
BEHAVIORAL_NOTES=Keep keys/versions intact. CopyBack happens before outer commit; discard/reload separate domain objects after failure.
```

### Z10 — TransactionAdapter.GetObjective

```text
CURRENT_ZONEENGINE_FILE=AORebirth/Server/ZoneEngine/Core/Missions/MissionDaoRepositoryAdapter.cs:115
CURRENT_METHOD=TransactionAdapter.GetObjective
CURRENT_LEGACY_PERSISTENCE_CALL=transaction.GetObjective
TARGET_IMISSIONDAO_METHOD=IMissionDaoTransaction.GetObjective
INPUT_MAPPING=MissionObjectiveKey(Mission, ObjectiveId) -> MissionObjectiveKeyData
RETURN_MAPPING=MissionObjectiveProgressData/null -> MissionObjectiveProgressRecord/null
ERROR_BEHAVIOR=Propagate DAO/validation errors; never translate a provider error into missing data.
TRANSACTION_EXPECTATION=Uses the existing outer Execute connection/transaction, never a separate DAO connection.
BEHAVIORAL_NOTES=MissionDataMapper copies fields and casts enum numeric values; no gameplay inference.
```

### Z11 — TransactionAdapter.SaveObjective

```text
CURRENT_ZONEENGINE_FILE=AORebirth/Server/ZoneEngine/Core/Missions/MissionDaoRepositoryAdapter.cs:121
CURRENT_METHOD=TransactionAdapter.SaveObjective
CURRENT_LEGACY_PERSISTENCE_CALL=transaction.SaveObjective
TARGET_IMISSIONDAO_METHOD=IMissionDaoTransaction.SaveObjective
INPUT_MAPPING=Objective key and all progress fields including Version -> neutral DTO
RETURN_MAPPING=void; CopyBack(data, record) updates Version
ERROR_BEHAVIOR=Propagate; failed writes poison the scope even if caught. Reload after any failed Execute; secondary rollback error remains inspectable.
TRANSACTION_EXPECTATION=Uses the existing outer Execute connection/transaction, never a separate DAO connection.
BEHAVIORAL_NOTES=Keep keys/versions intact. CopyBack happens before outer commit; discard/reload separate domain objects after failure.
```

### Z12 — TransactionAdapter.TryAddObservation

```text
CURRENT_ZONEENGINE_FILE=AORebirth/Server/ZoneEngine/Core/Missions/MissionDaoRepositoryAdapter.cs:128
CURRENT_METHOD=TransactionAdapter.TryAddObservation
CURRENT_LEGACY_PERSISTENCE_CALL=transaction.TryAddObservation
TARGET_IMISSIONDAO_METHOD=IMissionDaoTransaction.TryAddObservation
INPUT_MAPPING=CharacterId, QuestId, ObjectiveId, ObservationKey, EventType, SourceIdentity, TargetIdentity, ObservedAtUtcTicks copied
RETURN_MAPPING=bool inserted; copied observation fields returned through CopyBack
ERROR_BEHAVIOR=Propagate; failed writes poison the scope even if caught. Reload after any failed Execute; secondary rollback error remains inspectable.
TRANSACTION_EXPECTATION=Uses the existing outer Execute connection/transaction, never a separate DAO connection.
BEHAVIORAL_NOTES=Duplicate observation returns false; other SQL failures throw.
```

### Z13 — TransactionAdapter.GetFlag

```text
CURRENT_ZONEENGINE_FILE=AORebirth/Server/ZoneEngine/Core/Missions/MissionDaoRepositoryAdapter.cs:136
CURRENT_METHOD=TransactionAdapter.GetFlag
CURRENT_LEGACY_PERSISTENCE_CALL=transaction.GetFlag
TARGET_IMISSIONDAO_METHOD=IMissionDaoTransaction.GetFlag
INPUT_MAPPING=MissionKey -> MissionKeyData; flagKey unchanged
RETURN_MAPPING=MissionFlagData/null -> MissionFlagRecord/null
ERROR_BEHAVIOR=Propagate DAO/validation errors; never translate a provider error into missing data.
TRANSACTION_EXPECTATION=Uses the existing outer Execute connection/transaction, never a separate DAO connection.
BEHAVIORAL_NOTES=MissionDataMapper copies fields and casts enum numeric values; no gameplay inference.
```

### Z14 — TransactionAdapter.SaveFlag

```text
CURRENT_ZONEENGINE_FILE=AORebirth/Server/ZoneEngine/Core/Missions/MissionDaoRepositoryAdapter.cs:142
CURRENT_METHOD=TransactionAdapter.SaveFlag
CURRENT_LEGACY_PERSISTENCE_CALL=transaction.SaveFlag
TARGET_IMISSIONDAO_METHOD=IMissionDaoTransaction.SaveFlag
INPUT_MAPPING=MissionKey -> MissionKeyData; all MissionFlagRecord fields including Version -> DTO
RETURN_MAPPING=void; CopyBack(data, flag) updates Version
ERROR_BEHAVIOR=Propagate; failed writes poison the scope even if caught. Reload after any failed Execute; secondary rollback error remains inspectable.
TRANSACTION_EXPECTATION=Uses the existing outer Execute connection/transaction, never a separate DAO connection.
BEHAVIORAL_NOTES=Keep keys/versions intact. CopyBack happens before outer commit; discard/reload separate domain objects after failure.
```

### Z15 — TransactionAdapter.GetAccountFlag

```text
CURRENT_ZONEENGINE_FILE=AORebirth/Server/ZoneEngine/Core/Missions/MissionDaoRepositoryAdapter.cs:148
CURRENT_METHOD=TransactionAdapter.GetAccountFlag
CURRENT_LEGACY_PERSISTENCE_CALL=transaction.GetAccountFlag
TARGET_IMISSIONDAO_METHOD=IMissionDaoTransaction.GetAccountFlag
INPUT_MAPPING=accountKey, flagKey unchanged; must match locked account scope
RETURN_MAPPING=MissionAccountFlagData/null -> MissionAccountFlagRecord/null
ERROR_BEHAVIOR=Propagate DAO/validation errors; never translate a provider error into missing data.
TRANSACTION_EXPECTATION=Uses the existing outer Execute connection/transaction, never a separate DAO connection.
BEHAVIORAL_NOTES=MissionDataMapper copies fields and casts enum numeric values; no gameplay inference.
```

### Z16 — TransactionAdapter.SaveAccountFlag

```text
CURRENT_ZONEENGINE_FILE=AORebirth/Server/ZoneEngine/Core/Missions/MissionDaoRepositoryAdapter.cs:154
CURRENT_METHOD=TransactionAdapter.SaveAccountFlag
CURRENT_LEGACY_PERSISTENCE_CALL=transaction.SaveAccountFlag
TARGET_IMISSIONDAO_METHOD=IMissionDaoTransaction.SaveAccountFlag
INPUT_MAPPING=accountKey and all MissionAccountFlagRecord fields including Version -> DTO
RETURN_MAPPING=void; CopyBack(data, flag) updates Version
ERROR_BEHAVIOR=Propagate; failed writes poison the scope even if caught. Reload after any failed Execute; secondary rollback error remains inspectable.
TRANSACTION_EXPECTATION=Uses the existing outer Execute connection/transaction, never a separate DAO connection.
BEHAVIORAL_NOTES=Keep keys/versions intact. CopyBack happens before outer commit; discard/reload separate domain objects after failure.
```

### Z17 — TransactionAdapter.GetReward

```text
CURRENT_ZONEENGINE_FILE=AORebirth/Server/ZoneEngine/Core/Missions/MissionDaoRepositoryAdapter.cs:160
CURRENT_METHOD=TransactionAdapter.GetReward
CURRENT_LEGACY_PERSISTENCE_CALL=transaction.GetReward
TARGET_IMISSIONDAO_METHOD=IMissionDaoTransaction.GetReward
INPUT_MAPPING=MissionRewardKey(Mission, RewardKey) -> MissionRewardKeyData
RETURN_MAPPING=MissionRewardStageData/null -> MissionRewardStageRecord/null
ERROR_BEHAVIOR=Propagate DAO/validation errors; never translate a provider error into missing data.
TRANSACTION_EXPECTATION=Uses the existing outer Execute connection/transaction, never a separate DAO connection.
BEHAVIORAL_NOTES=MissionDataMapper copies fields and casts enum numeric values; no gameplay inference.
```

### Z18 — TransactionAdapter.TryClaimReward

```text
CURRENT_ZONEENGINE_FILE=AORebirth/Server/ZoneEngine/Core/Missions/MissionDaoRepositoryAdapter.cs:170
CURRENT_METHOD=TransactionAdapter.TryClaimReward
CURRENT_LEGACY_PERSISTENCE_CALL=transaction.TryClaimReward
TARGET_IMISSIONDAO_METHOD=IMissionDaoTransaction.TryClaimReward
INPUT_MAPPING=Reward key, rewardType, claimToken, claimedAtUtcTicks, claimExpiresAtUtcTicks unchanged
RETURN_MAPPING=Status numeric enum cast; Stage -> domain; Message unchanged
ERROR_BEHAVIOR=Propagate; failed writes poison the scope even if caught. Reload after any failed Execute; secondary rollback error remains inspectable.
TRANSACTION_EXPECTATION=Uses the existing outer Execute connection/transaction, never a separate DAO connection.
BEHAVIORAL_NOTES=Keep existing status/claim token/version checks; false or rejected is not permission to apply effects.
```

### Z19 — TransactionAdapter.TryMarkRewardApplied

```text
CURRENT_ZONEENGINE_FILE=AORebirth/Server/ZoneEngine/Core/Missions/MissionDaoRepositoryAdapter.cs:193
CURRENT_METHOD=TransactionAdapter.TryMarkRewardApplied
CURRENT_LEGACY_PERSISTENCE_CALL=transaction.TryMarkRewardApplied
TARGET_IMISSIONDAO_METHOD=IMissionDaoTransaction.TryMarkRewardApplied
INPUT_MAPPING=Reward key, claimToken, expectedVersion, effectReference, appliedAtUtcTicks unchanged
RETURN_MAPPING=bool unchanged; out MissionRewardStageData -> out domain stage
ERROR_BEHAVIOR=Propagate; failed writes poison the scope even if caught. Reload after any failed Execute; secondary rollback error remains inspectable.
TRANSACTION_EXPECTATION=Uses the existing outer Execute connection/transaction, never a separate DAO connection.
BEHAVIORAL_NOTES=Keep existing status/claim token/version checks; false or rejected is not permission to apply effects.
```

### Z20 — TransactionAdapter.TryMarkRewardFailed

```text
CURRENT_ZONEENGINE_FILE=AORebirth/Server/ZoneEngine/Core/Missions/MissionDaoRepositoryAdapter.cs:213
CURRENT_METHOD=TransactionAdapter.TryMarkRewardFailed
CURRENT_LEGACY_PERSISTENCE_CALL=transaction.TryMarkRewardFailed
TARGET_IMISSIONDAO_METHOD=IMissionDaoTransaction.TryMarkRewardFailed
INPUT_MAPPING=Reward key, claimToken, expectedVersion, error, failedAtUtcTicks unchanged
RETURN_MAPPING=bool unchanged; out MissionRewardStageData -> out domain stage
ERROR_BEHAVIOR=Propagate; failed writes poison the scope even if caught. Reload after any failed Execute; secondary rollback error remains inspectable.
TRANSACTION_EXPECTATION=Uses the existing outer Execute connection/transaction, never a separate DAO connection.
BEHAVIORAL_NOTES=Keep existing status/claim token/version checks; false or rejected is not permission to apply effects.
```

### Z21 — TransactionAdapter.TryApplyCharacterStatReward

```text
CURRENT_ZONEENGINE_FILE=AORebirth/Server/ZoneEngine/Core/Missions/MissionDaoRepositoryAdapter.cs:231
CURRENT_METHOD=TransactionAdapter.TryApplyCharacterStatReward
CURRENT_LEGACY_PERSISTENCE_CALL=transaction.TryApplyCharacterStatReward
TARGET_IMISSIONDAO_METHOD=IMissionDaoTransaction.TryApplyCharacterStatReward
INPUT_MAPPING=Reward key, rewardType, mapped StatIdentityType/StatId/Kind/Value/MinimumValue/MaximumValue list, effectReference, ticks
RETURN_MAPPING=Status numeric enum cast; Stage and StatValues mapped; Message unchanged
ERROR_BEHAVIOR=Propagate; failed writes poison the scope even if caught. Reload after any failed Execute; secondary rollback error remains inspectable.
TRANSACTION_EXPECTATION=Uses the existing outer Execute connection/transaction, never a separate DAO connection.
BEHAVIORAL_NOTES=Keep existing status/claim token/version checks; false or rejected is not permission to apply effects.
```

### Z22 — GetMission

```text
CURRENT_ZONEENGINE_FILE=AORebirth/Server/ZoneEngine/Core/Missions/PersistentMissionService.cs:41
CURRENT_METHOD=GetMission
CURRENT_LEGACY_PERSISTENCE_CALL=repository.GetMission(key)
TARGET_IMISSIONDAO_METHOD=IMissionDao.GetMission
INPUT_MAPPING=Validated characterId + trimmed questId -> MissionKeyData.
RETURN_MAPPING=DTO/null -> existing domain record/null.
ERROR_BEHAVIOR=Propagate DAO/validation errors; never translate a provider error into missing data.
TRANSACTION_EXPECTATION=Buffered read, one owned connection; no runtime side effects.
BEHAVIORAL_NOTES=Invalid input remains runtime-level null.
```

### Z23 — GetMissions

```text
CURRENT_ZONEENGINE_FILE=AORebirth/Server/ZoneEngine/Core/Missions/PersistentMissionService.cs:46
CURRENT_METHOD=GetMissions
CURRENT_LEGACY_PERSISTENCE_CALL=repository.GetMissions(characterId)
TARGET_IMISSIONDAO_METHOD=IMissionDao.GetMissions
INPUT_MAPPING=Positive characterId unchanged.
RETURN_MAPPING=Ordered DTO list -> existing domain list.
ERROR_BEHAVIOR=Propagate DAO/validation errors; never translate a provider error into missing data.
TRANSACTION_EXPECTATION=Buffered read, one owned connection; no runtime side effects.
BEHAVIORAL_NOTES=Invalid character remains empty list in service.
```

### Z24 — GetObjective

```text
CURRENT_ZONEENGINE_FILE=AORebirth/Server/ZoneEngine/Core/Missions/PersistentMissionService.cs:60
CURRENT_METHOD=GetObjective
CURRENT_LEGACY_PERSISTENCE_CALL=repository.ReadCharacter(characterId)
TARGET_IMISSIONDAO_METHOD=IMissionDao.ReadCharacter
INPUT_MAPPING=characterId; runtime selects questId/objectiveId from returned Objectives.
RETURN_MAPPING=Snapshot -> existing matching objective/null.
ERROR_BEHAVIOR=Propagate DAO/validation errors; never translate a provider error into missing data.
TRANSACTION_EXPECTATION=One snapshot read transaction.
BEHAVIORAL_NOTES=Do not add an unscoped top-level GetObjective API.
```

### Z25 — OfferMission

```text
CURRENT_ZONEENGINE_FILE=AORebirth/Server/ZoneEngine/Core/Missions/PersistentMissionService.cs:96
CURRENT_METHOD=OfferMission
CURRENT_LEGACY_PERSISTENCE_CALL=repository.Execute: GetMission prerequisites; SaveMission + SaveObjective
TARGET_IMISSIONDAO_METHOD=IMissionDao.Execute -> IMissionDaoTransaction: GetMission prerequisites; SaveMission + SaveObjective
INPUT_MAPPING=Validated mission key; definition-derived Offered state, initial step, resolved objectives, and now ticks.
RETURN_MAPPING=Mapped DTOs feed the unchanged MissionOperationResult status/record/objective/message.
ERROR_BEHAVIOR=Propagate; failed writes poison the scope even if caught. Reload after any failed Execute; secondary rollback error remains inspectable.
TRANSACTION_EXPECTATION=One character transaction covering mission and all initial objectives.
BEHAVIORAL_NOTES=Runtime still decides prerequisites, repeatability, and objective requirements.
```

### Z26 — AcceptMission

```text
CURRENT_ZONEENGINE_FILE=AORebirth/Server/ZoneEngine/Core/Missions/PersistentMissionService.cs:171
CURRENT_METHOD=AcceptMission
CURRENT_LEGACY_PERSISTENCE_CALL=repository.Execute: GetMission + SaveMission
TARGET_IMISSIONDAO_METHOD=IMissionDao.Execute -> IMissionDaoTransaction: GetMission + SaveMission
INPUT_MAPPING=Same key; current Version; caller sets Active, AcceptedAtUtcTicks, UpdatedAtUtcTicks.
RETURN_MAPPING=Mapped DTOs feed the unchanged MissionOperationResult status/record/objective/message.
ERROR_BEHAVIOR=Propagate; failed writes poison the scope even if caught. Reload after any failed Execute; secondary rollback error remains inspectable.
TRANSACTION_EXPECTATION=One character transaction.
BEHAVIORAL_NOTES=Offered-only transition; existing Active/Completed idempotency remains runtime behavior.
```

### Z27 — ChangeStep

```text
CURRENT_ZONEENGINE_FILE=AORebirth/Server/ZoneEngine/Core/Missions/PersistentMissionService.cs:218
CURRENT_METHOD=ChangeStep
CURRENT_LEGACY_PERSISTENCE_CALL=repository.Execute: GetMission + SaveMission
TARGET_IMISSIONDAO_METHOD=IMissionDao.Execute -> IMissionDaoTransaction: GetMission + SaveMission
INPUT_MAPPING=Same key; normalized step; current Version and now ticks.
RETURN_MAPPING=Mapped DTOs feed the unchanged MissionOperationResult status/record/objective/message.
ERROR_BEHAVIOR=Propagate; failed writes poison the scope even if caught. Reload after any failed Execute; secondary rollback error remains inspectable.
TRANSACTION_EXPECTATION=One character transaction.
BEHAVIORAL_NOTES=Only runtime validates active state and step eligibility.
```

### Z28 — ObserveObjective

```text
CURRENT_ZONEENGINE_FILE=AORebirth/Server/ZoneEngine/Core/Missions/PersistentMissionService.cs:274
CURRENT_METHOD=ObserveObjective
CURRENT_LEGACY_PERSISTENCE_CALL=repository.Execute: GetMission + GetObjective + SaveObjective + TryAddObservation
TARGET_IMISSIONDAO_METHOD=IMissionDao.Execute -> IMissionDaoTransaction: GetMission + GetObjective + SaveObjective + TryAddObservation
INPUT_MAPPING=Observation character/key; event/source/target strings and ticks; progress/required count from runtime.
RETURN_MAPPING=Mapped DTOs feed the unchanged MissionOperationResult status/record/objective/message.
ERROR_BEHAVIOR=Propagate; failed writes poison the scope even if caught. Reload after any failed Execute; secondary rollback error remains inspectable.
TRANSACTION_EXPECTATION=One character transaction for dedupe record and progress changes.
BEHAVIORAL_NOTES=Preserve duplicate-observation false branch, count refresh, and runtime clamping.
```

### Z29 — CompleteMission

```text
CURRENT_ZONEENGINE_FILE=AORebirth/Server/ZoneEngine/Core/Missions/PersistentMissionService.cs:358
CURRENT_METHOD=CompleteMission
CURRENT_LEGACY_PERSISTENCE_CALL=repository.Execute: CompleteWithinTransaction: GetMission + GetObjective + SaveMission
TARGET_IMISSIONDAO_METHOD=IMissionDao.Execute -> IMissionDaoTransaction: CompleteWithinTransaction: GetMission + GetObjective + SaveMission
INPUT_MAPPING=Validated key; current Version; caller sets Completed and completion/update ticks.
RETURN_MAPPING=Mapped DTOs feed the unchanged MissionOperationResult status/record/objective/message.
ERROR_BEHAVIOR=Propagate; failed writes poison the scope even if caught. Reload after any failed Execute; secondary rollback error remains inspectable.
TRANSACTION_EXPECTATION=One character transaction.
BEHAVIORAL_NOTES=DAO does not decide whether objectives are complete.
```

### Z30 — CompleteAndActivateNextMission

```text
CURRENT_ZONEENGINE_FILE=AORebirth/Server/ZoneEngine/Core/Missions/PersistentMissionService.cs:394
CURRENT_METHOD=CompleteAndActivateNextMission
CURRENT_LEGACY_PERSISTENCE_CALL=repository.Execute: GetMission/GetObjective prerequisites; SaveMission current and next; SaveObjective next
TARGET_IMISSIONDAO_METHOD=IMissionDao.Execute -> IMissionDaoTransaction: GetMission/GetObjective prerequisites; SaveMission current and next; SaveObjective next
INPUT_MAPPING=Current and next keys share character; mapped DTOs and same now ticks.
RETURN_MAPPING=Mapped DTOs feed the unchanged MissionOperationResult status/record/objective/message.
ERROR_BEHAVIOR=Propagate; failed writes poison the scope even if caught. Reload after any failed Execute; secondary rollback error remains inspectable.
TRANSACTION_EXPECTATION=One character transaction across both missions and next objectives.
BEHAVIORAL_NOTES=Do not split into independent Complete and Accept calls.
```

### Z31 — CompleteMissionWithAccountFlag

```text
CURRENT_ZONEENGINE_FILE=AORebirth/Server/ZoneEngine/Core/Missions/PersistentMissionService.cs:558
CURRENT_METHOD=CompleteMissionWithAccountFlag
CURRENT_LEGACY_PERSISTENCE_CALL=repository.Execute: GetAccountFlag + CompleteWithinTransaction + SaveAccountFlag
TARGET_IMISSIONDAO_METHOD=IMissionDao.Execute -> IMissionDaoTransaction: GetAccountFlag + CompleteWithinTransaction + SaveAccountFlag
INPUT_MAPPING=characterId, trimmed accountKey, mission key, normalized flagKey/value/source quest/ticks.
RETURN_MAPPING=Mapped DTOs feed the unchanged MissionOperationResult status/record/objective/message.
ERROR_BEHAVIOR=Propagate; failed writes poison the scope even if caught. Reload after any failed Execute; secondary rollback error remains inspectable.
TRANSACTION_EXPECTATION=One character+account transaction with locked ownership check.
BEHAVIORAL_NOTES=Completion and account unlock must commit together; preserve conflict checks.
```

### Z32 — SetAccountFlag

```text
CURRENT_ZONEENGINE_FILE=AORebirth/Server/ZoneEngine/Core/Missions/PersistentMissionService.cs:644
CURRENT_METHOD=SetAccountFlag
CURRENT_LEGACY_PERSISTENCE_CALL=repository.Execute: GetMission + GetAccountFlag + SaveAccountFlag
TARGET_IMISSIONDAO_METHOD=IMissionDao.Execute -> IMissionDaoTransaction: GetMission + GetAccountFlag + SaveAccountFlag
INPUT_MAPPING=characterId, trimmed accountKey, source quest, normalized flagKey, value, ticks.
RETURN_MAPPING=Mapped DTOs feed the unchanged MissionOperationResult status/record/objective/message.
ERROR_BEHAVIOR=Propagate; failed writes poison the scope even if caught. Reload after any failed Execute; secondary rollback error remains inspectable.
TRANSACTION_EXPECTATION=One character+account transaction with locked ownership check.
BEHAVIORAL_NOTES=Runtime requires source mission completed and prevents conflicting grant.
```

### Z33 — SetFlag

```text
CURRENT_ZONEENGINE_FILE=AORebirth/Server/ZoneEngine/Core/Missions/PersistentMissionService.cs:719
CURRENT_METHOD=SetFlag
CURRENT_LEGACY_PERSISTENCE_CALL=repository.Execute: GetMission + GetFlag + SaveFlag
TARGET_IMISSIONDAO_METHOD=IMissionDao.Execute -> IMissionDaoTransaction: GetMission + GetFlag + SaveFlag
INPUT_MAPPING=characterId, quest key, normalized flagKey, nullable value, Version and ticks.
RETURN_MAPPING=Mapped DTOs feed the unchanged MissionOperationResult status/record/objective/message.
ERROR_BEHAVIOR=Propagate; failed writes poison the scope even if caught. Reload after any failed Execute; secondary rollback error remains inspectable.
TRANSACTION_EXPECTATION=One character transaction.
BEHAVIORAL_NOTES=Same value remains AlreadyApplied; no mission creation as a side effect.
```

### Z34 — SetTerminalState (FailMission/AbandonMission)

```text
CURRENT_ZONEENGINE_FILE=AORebirth/Server/ZoneEngine/Core/Missions/PersistentMissionService.cs:837
CURRENT_METHOD=SetTerminalState (FailMission/AbandonMission)
CURRENT_LEGACY_PERSISTENCE_CALL=repository.Execute: GetMission + SaveMission
TARGET_IMISSIONDAO_METHOD=IMissionDao.Execute -> IMissionDaoTransaction: GetMission + SaveMission
INPUT_MAPPING=key, current Version; runtime sets Failed or Abandoned and matching timestamp.
RETURN_MAPPING=Mapped DTOs feed the unchanged MissionOperationResult status/record/objective/message.
ERROR_BEHAVIOR=Propagate; failed writes poison the scope even if caught. Reload after any failed Execute; secondary rollback error remains inspectable.
TRANSACTION_EXPECTATION=One character transaction.
BEHAVIORAL_NOTES=Not physical deletion; runtime retains allowed-transition checks and mission history.
```

### Z35 — GetAccountFlag

```text
CURRENT_ZONEENGINE_FILE=AORebirth/Server/ZoneEngine/Core/Missions/PersistentMissionService.cs:695
CURRENT_METHOD=GetAccountFlag
CURRENT_LEGACY_PERSISTENCE_CALL=repository.GetAccountFlag
TARGET_IMISSIONDAO_METHOD=IMissionDao.GetAccountFlag
INPUT_MAPPING=Trimmed accountKey, flagKey.
RETURN_MAPPING=DTO/null -> existing account flag/null.
ERROR_BEHAVIOR=Propagate DAO/validation errors; never translate a provider error into missing data.
TRANSACTION_EXPECTATION=Buffered read, one owned connection; no runtime side effects.
BEHAVIORAL_NOTES=Read by account key is not session authorization; caller authenticates.
```

### Z36 — GetFlag

```text
CURRENT_ZONEENGINE_FILE=AORebirth/Server/ZoneEngine/Core/Missions/PersistentMissionService.cs:761
CURRENT_METHOD=GetFlag
CURRENT_LEGACY_PERSISTENCE_CALL=repository.Execute -> transaction.GetFlag
TARGET_IMISSIONDAO_METHOD=IMissionDao.Execute -> IMissionDaoTransaction.GetFlag
INPUT_MAPPING=characterId, mapped mission key, trimmed flagKey.
RETURN_MAPPING=DTO/null -> existing flag/null.
ERROR_BEHAVIOR=Propagate DAO/validation errors; never translate a provider error into missing data.
TRANSACTION_EXPECTATION=One character read transaction.
BEHAVIORAL_NOTES=Preserve invalid-input null behavior above DAO.
```

### Z37 — Reload (login/reconnect/zoning/restart wrappers)

```text
CURRENT_ZONEENGINE_FILE=AORebirth/Server/ZoneEngine/Core/Missions/PersistentMissionService.cs:818
CURRENT_METHOD=Reload (login/reconnect/zoning/restart wrappers)
CURRENT_LEGACY_PERSISTENCE_CALL=repository.ReadCharacter
TARGET_IMISSIONDAO_METHOD=IMissionDao.ReadCharacter
INPUT_MAPPING=Positive characterId; reload reason stays runtime-only.
RETURN_MAPPING=Snapshot DTO -> MissionReloadResult.Snapshot; reason/message unchanged.
ERROR_BEHAVIOR=Propagate DAO/validation errors; never translate a provider error into missing data.
TRANSACTION_EXPECTATION=One snapshot read transaction.
BEHAVIORAL_NOTES=Do not convert provider error into empty successful login hydration.
```

### Z38 — ExecuteExternal / claim

```text
CURRENT_ZONEENGINE_FILE=AORebirth/Server/ZoneEngine/Core/Missions/MissionRewardCoordinator.cs:191
CURRENT_METHOD=ExecuteExternal / claim
CURRENT_LEGACY_PERSISTENCE_CALL=repository.Execute -> transaction.TryClaimReward
TARGET_IMISSIONDAO_METHOD=IMissionDao.Execute -> IMissionDaoTransaction.TryClaimReward
INPUT_MAPPING=characterId; mapped reward key; resolved rewardType; claimToken; claimedAt and expiresAt
RETURN_MAPPING=MissionRewardClaimResultData -> claim Status/Stage/Message
ERROR_BEHAVIOR=Propagate; failed writes poison the scope even if caught. Reload after any failed Execute; secondary rollback error remains inspectable.
TRANSACTION_EXPECTATION=One character transaction per call; external effects are not part of a database transaction.
BEHAVIORAL_NOTES=Persist claim before running the external effect.
```

### Z39 — ExecuteExternal / applied

```text
CURRENT_ZONEENGINE_FILE=AORebirth/Server/ZoneEngine/Core/Missions/MissionRewardCoordinator.cs:244
CURRENT_METHOD=ExecuteExternal / applied
CURRENT_LEGACY_PERSISTENCE_CALL=repository.Execute -> transaction.TryMarkRewardApplied
TARGET_IMISSIONDAO_METHOD=IMissionDao.Execute -> IMissionDaoTransaction.TryMarkRewardApplied
INPUT_MAPPING=characterId; reward key; same claimToken; claim.Stage.Version; effectReference; finishedAt; out stage
RETURN_MAPPING=bool and mapped out stage
ERROR_BEHAVIOR=Propagate; failed writes poison the scope even if caught. Reload after any failed Execute; secondary rollback error remains inspectable.
TRANSACTION_EXPECTATION=One character transaction per call; external effects are not part of a database transaction.
BEHAVIORAL_NOTES=External effect already ran; a false mark requires reconciliation with an idempotent effect adapter.
```

### Z40 — ExecuteExternal / failed

```text
CURRENT_ZONEENGINE_FILE=AORebirth/Server/ZoneEngine/Core/Missions/MissionRewardCoordinator.cs:272
CURRENT_METHOD=ExecuteExternal / failed
CURRENT_LEGACY_PERSISTENCE_CALL=repository.Execute -> transaction.TryMarkRewardFailed
TARGET_IMISSIONDAO_METHOD=IMissionDao.Execute -> IMissionDaoTransaction.TryMarkRewardFailed
INPUT_MAPPING=characterId; reward key; same claimToken; claim.Stage.Version; effect error; finishedAt; out stage
RETURN_MAPPING=bool and mapped out stage
ERROR_BEHAVIOR=Propagate; failed writes poison the scope even if caught. Reload after any failed Execute; secondary rollback error remains inspectable.
TRANSACTION_EXPECTATION=One character transaction per call; external effects are not part of a database transaction.
BEHAVIORAL_NOTES=Preserve RetryableFailure and conflict messages; no automatic side-effect retry.
```

### Z41 — ExecuteAtomicCharacterStats

```text
CURRENT_ZONEENGINE_FILE=AORebirth/Server/ZoneEngine/Core/Missions/MissionRewardCoordinator.cs:312
CURRENT_METHOD=ExecuteAtomicCharacterStats
CURRENT_LEGACY_PERSISTENCE_CALL=repository.Execute -> transaction.TryApplyCharacterStatReward
TARGET_IMISSIONDAO_METHOD=IMissionDao.Execute -> IMissionDaoTransaction.TryApplyCharacterStatReward
INPUT_MAPPING=characterId; reward key; rewardType; mapped resolved stat mutations; effectReference; now ticks
RETURN_MAPPING=MissionAtomicStatRewardResultData -> domain Status/Stage/StatValues/Message
ERROR_BEHAVIOR=Propagate; failed writes poison the scope even if caught. Reload after any failed Execute; secondary rollback error remains inspectable.
TRANSACTION_EXPECTATION=One character transaction per call; external effects are not part of a database transaction.
BEHAVIORAL_NOTES=Stats and ledger must commit together; update runtime stats only after success.
```

### Z42 — ResolveAccountKey

```text
CURRENT_ZONEENGINE_FILE=AORebirth/Server/ZoneEngine/Core/Missions/MissionRuntime.cs:151
CURRENT_METHOD=ResolveAccountKey
CURRENT_LEGACY_PERSISTENCE_CALL=dao.ResolveCharacterAccountKey
TARGET_IMISSIONDAO_METHOD=IMissionDao.ResolveCharacterAccountKey
INPUT_MAPPING=characterId unchanged.
RETURN_MAPPING=Trimmed Username string/null.
ERROR_BEHAVIOR=Propagate DAO/validation errors; never translate a provider error into missing data.
TRANSACTION_EXPECTATION=Buffered read, one owned connection; no runtime side effects.
BEHAVIORAL_NOTES=Not an account creation API; uninitialized DAO currently yields null.
```

### Z43 — TryChargeRollFee

```text
CURRENT_ZONEENGINE_FILE=AORebirth/Server/ZoneEngine/Core/Missions/MissionRollFeeService.cs:84
CURRENT_METHOD=TryChargeRollFee
CURRENT_LEGACY_PERSISTENCE_CALL=dao.TryChargeRollFee
TARGET_IMISSIONDAO_METHOD=IMissionDao.TryChargeRollFee
INPUT_MAPPING=Identity.Type -> CharacterType; Identity.Instance -> CharacterId; batchIdentity; caller fee; DateTime.UtcNow.Ticks.
RETURN_MAPPING=CashBefore/CashAfter; Applied or AlreadyApplied -> success; InsufficientCredits -> flag/false; Conflict -> Failure/false.
ERROR_BEHAVIOR=Propagate; failed writes poison the scope even if caught. Reload after any failed Execute; secondary rollback error remains inspectable.
TRANSACTION_EXPECTATION=One DAO-owned transaction for locked cash, debit and roll-fee ledger.
BEHAVIORAL_NOTES=Fee calculation, offer publication, UI/stat synchronization remain runtime; never charge again with a new key after uncertain commit.
```

### Z44 — Schedule / queued callback

```text
CURRENT_ZONEENGINE_FILE=AORebirth/Server/ZoneEngine/Core/NewCharacterStartAreaSelectionRuntime.cs:92
CURRENT_METHOD=Schedule / queued callback
CURRENT_LEGACY_PERSISTENCE_CALL=GetMissionDao().GetStartAreaSelectionState
TARGET_IMISSIONDAO_METHOD=IMissionDao.GetStartAreaSelectionState
INPUT_MAPPING=character.Identity.Instance -> characterId.
RETURN_MAPPING=string/null; existing ordinal-ignore-case pending comparison.
ERROR_BEHAVIOR=Legacy convenience returns null on database failure; not proof the row is absent.
TRANSACTION_EXPECTATION=Buffered read, one owned connection; no runtime side effects.
BEHAVIORAL_NOTES=Prompt scheduling, player checks and packets remain runtime.
```

### Z45 — TryHandleAnswer

```text
CURRENT_ZONEENGINE_FILE=AORebirth/Server/ZoneEngine/Core/NewCharacterStartAreaSelectionRuntime.cs:141
CURRENT_METHOD=TryHandleAnswer
CURRENT_LEGACY_PERSISTENCE_CALL=GetMissionDao().TryCompleteStartAreaSelection
TARGET_IMISSIONDAO_METHOD=IMissionDao.TryCompleteStartAreaSelection
INPUT_MAPPING=character.Identity.Instance; selectedState exact lowercase constant.
RETURN_MAPPING=bool; false -> existing retry prompt; true -> runtime session cleanup/teleport.
ERROR_BEHAVIOR=Invalid state/id or provider failure returns false.
TRANSACTION_EXPECTATION=One conditional UPDATE, only pending -> selected; no external effects in SQL.
BEHAVIORAL_NOTES=Permanent selection and zoning behavior stay unchanged.
```

### Z46 — Initialize(bool databaseAlreadyValidated)

```text
CURRENT_ZONEENGINE_FILE=AORebirth/Server/ZoneEngine/Program.cs:509
CURRENT_METHOD=Initialize(bool databaseAlreadyValidated)
CURRENT_LEGACY_PERSISTENCE_CALL=DatabaseDaoFactory.CreateMissionDao + three Initialize calls
TARGET_IMISSIONDAO_METHOD=Construction: DatabaseDaoFactory.CreateMissionDao returns IMissionDao
INPUT_MAPPING=Existing configured database infrastructure; share DAO with MissionRuntime, MissionRollFeeService, NewCharacterStartAreaSelectionRuntime.
RETURN_MAPPING=IMissionDao; no connection opened by factory.
ERROR_BEHAVIOR=Existing outer initialization catch; connection/provider failures happen on operation, not factory construction.
TRANSACTION_EXPECTATION=No transaction during construction.
BEHAVIORAL_NOTES=Composition root only. Future ZoneEngine_New owner chooses equivalent root; do not register from handlers or create another DAO.
```

### Z47 — Initialize(registries, IMissionDao)

```text
CURRENT_ZONEENGINE_FILE=AORebirth/Server/ZoneEngine/Core/Missions/MissionRuntime.cs:102
CURRENT_METHOD=Initialize(registries, IMissionDao)
CURRENT_LEGACY_PERSISTENCE_CALL=new MissionDaoRepositoryAdapter(dao)
TARGET_IMISSIONDAO_METHOD=Consume injected IMissionDao through existing runtime adapter
INPUT_MAPPING=Valid registry remains runtime-only; injected DAO does not receive registry.
RETURN_MAPPING=Existing PersistentMissionService/RewardCoordinator initialization.
ERROR_BEHAVIOR=Reject null DAO; retain registry validation.
TRANSACTION_EXPECTATION=No database work during adapter construction.
BEHAVIORAL_NOTES=Existing repository overload at105 remains test seam. Do not move runtime-dependent mapper into Database.
```

## Runtime-owned persistence excluded from the SQL wiring count

All paths in this table are under AORebirth/Server/ZoneEngine/Core/Missions and were inspected read-only. These are **not** missing IMissionDao methods. Their file formats carry generated offer/quest identities, projections, layouts, operational state, expiry and other runtime data not represented by the existing SQL mission slice. Replacing them is not a mechanical DAO call substitution and needs a separate runtime/data-migration decision.

For every entry: TARGET_IMISSIONDAO_METHOD=N/A. INPUT_MAPPING/RETURN_MAPPING=no lossless mapping established in the unchanged SQL schema. ERROR_BEHAVIOR=retain the store's current success/failure/quarantine behavior, never substitute an empty SQL snapshot. TRANSACTION_EXPECTATION=existing file persistence/replace/recovery semantics; not enlisted in MySQL Execute. BEHAVIORAL_NOTES=leave untouched; do not serialize arbitrary runtime payloads into missionflags. The exact legacy persistence entry points are:

| CURRENT_ZONEENGINE_FILE | CURRENT_METHOD / CURRENT_LEGACY_PERSISTENCE_CALL |
| --- | --- |
| MissionOfferStore.cs | Initialize:247; TryStoreRoll:399; TryPublishBatch:576; TryBeginFeeCharge:701; TryDiscardBatch:818; TryClaimForAcceptance:906; TryReleaseClaim:993; TryRestoreUnprojectedClaim:1061; TryMarkAccepted:1151; TryReconcileAccepted:1176; DiscardPreparedOnRestoration:1218; ExpirePending:1267; TryGetFeeChargePending:1319; TryGetPendingRollForLogin:1413; TryGetOffer:1512; IsIdentityInUse:1573; Snapshot:1582. Owner ledger persistence, not missionstates CRUD. |
| MissionOfferIdentityStore.cs | Load:132; TryAllocate:144. Durable generated-offer identity allocation. |
| MissionAcceptedStore.cs | Register:84; TryRegisterGenerated:150; TryRegisterGeneratedProjection:207; GetAll:262; TryGet:295; TryResolve:311; TryResolveGeneratedProjection:330; Remove:364; TryRemoveExactPersisted:402; Clear:476. Accepted mission sidecar/projection behavior; Remove is not SQL AbandonMission. |
| MissionAcgAcceptedProjectionStore.cs | LoadAll:73; TryCreate:81; TryReplace:159; TryGetByAcceptedQuest:247; TryGetByOwnerOffer:273. |
| MissionAcgBindingStore.cs | LoadAll:81; TryCreate:144; TryReplace:179. Layout/binding persistence. |
| MissionAcgExpiryStateStore.cs | LoadAll:91/133; TryLoad:193; TryCreate:220/231; TryReplace:265. Real generated-mission expiry state; not a new SQL Expired lifecycle enum. |
| MissionAcgTokenProgressStore.cs | LoadAll:92/140; TryLoad:240; TryCreate:267/280; TryReplace:315. |
| MissionAcgObjectiveStore.cs | LoadAll:75; TryCreate:125; TryReplace:157; TryDelete:211. Generated objective state is not interchangeable with SQL objective progress. |
| MissionAcgRuntimeStateStore.cs | TryLoad:47; TryWrite:95; TryDelete:195. |
| MissionAcgOperationalStateStore.cs | TryLoad:43; TryWrite:85; TryDelete:158. |
| MissionAcgSpatialStateStore.cs | TryLoad:52; TryWrite:100; TryDelete:167. |

MissionKeyStore is an in-memory/runtime item association, not a new SQL mission-key DAO. Deleting an inventory key does not authorize deleting a mission record. Reward/target catalogs are content inputs, not player-state DAO operations.

Outside ZoneEngine, LoginEngine/Packets/CharacterName.cs:278 already calls IMissionDao.MarkStartAreaSelectionPending(charid) for the existing new-character path. Its input is charid; output bool is currently ignored; false remains ambiguous between failure and non-pending state. No login/character workflow was edited. The retained Database start-area shim maps MarkPending -> MarkStartAreaSelectionPending, GetState -> GetStartAreaSelectionState, and TryComplete -> TryCompleteStartAreaSelection, passing arguments/results unchanged.

## Tests and guard

Exact files:

- Tools/MissionDaoValidation/Program.cs: existing disposable MySQL suite entry point/basic lifecycle, transaction, reward, fee and ownership checks.
- Tools/MissionDaoValidation/HardeningChecks.cs: existing version restoration, scope lifetime, poisoned writes, invalid stat batches, provider read/write failures, stale versions, cancellation, leases/concurrency, snapshots and seven multi-write rollback cutpoints; invokes the new contract checks.
- Tools/MissionDaoValidation/ParallelContractChecks.cs: 55 additional assertions, bringing the isolated suite from 147 to 202. Actual production DAO/source is used; successful SQL executes against disposable MySQL.
- Tools/MissionDaoValidation/IsolatedHost.cs: conditional test-only Connector/LogUtil host; factory injection is reset in finally and never changes production Connector.
- Tools/MissionDaoValidation/MissionDaoValidation.csproj: isolated configuration also links the actual legacy compatibility shim. Normal production-project-reference mode remains intact.
- Tools/DaoArchitectureGuard/dao_architecture_guard.py: mission-only checks plus positive/negative self-test fixtures. No new global unrelated-domain guard activation or baseline exception was added.

| Required behavior | Evidence / disposition |
| --- | --- |
| Zero/one/multiple missions | ValidateReadCardinalityAndParameters: null single, fully empty snapshot, exact one then two ordered missions, cross-character absence. |
| Insert/update/completion | Existing lifecycle/version/isolation checks; all existing terminal states and timestamps round-trip. |
| Delete/expiry | N/A as SQL DAO APIs. Abandoned state is covered; generated file delete/expiry explicitly deferred above. |
| Ownership/lost update | Existing wrong-character, account ownership, key/record mismatch, stale version and exact-row tests. |
| Optional/null/parameters/order | Nullable fields and quote/backslash/semicolon-containing keys/values round-trip; values cannot execute SQL; reverse-inserted objective/flag/reward keys return in documented order. Existing snapshot detachment/account ordering checks retained. |
| Factory/open/begin/create-command/execute-command failures | Injected exception identity/type propagates; owned connection and acquired transaction are disposed. |
| Commit failure | Before real commit -> row absent after reload; after real commit -> durable row exists despite exception. DTO Version restored and escaped scope closed in both cases. |
| Rollback failure | Existing Execute original/secondary exception identity checks; roll-fee command failure now also preserves the secondary rollback error. |
| Partial write | Existing seven mutation cutpoints, child/ledger SQL failures, cancellation/stale failure; new fee debit succeeds then ledger insertion fails, rollback restores cash, stable-key retry debits once. |
| Construction/provider contract | Factory lazy; actual MySQL operation works through configured test host; non-MySQL connection rejected/disposed before a command; no parity claim. |
| Compatibility shim | Missing/pending/completed/idempotency, exact state casing, invalid input, same durable row as DAO and preserved false/null provider-error fallback. |

Fault decorators are test instrumentation, not another DAO/provider implementation. The fee rollback-failure decorator rolls back the actual transaction before throwing the injected secondary error so it leaves no locks; an existing pure ADO failure test separately covers an unsuccessful rollback. These are deterministic failure-path tests, not a simulation of every network/disk/server failure.

The guard rejects provider/ADO references and SQL in neutral contracts, engine/runtime types in persistence, nested mission runtime SQL/provider/concrete DAO/factory dependencies in both engine mission roots, and duplicate SQL in the compatibility shim. Comments/non-SQL string literals do not impersonate type dependencies. Composition roots remain outside the mission runtime subtree. This is a targeted source guard, not a complete C# semantic analyzer or assurance for arbitrary future folder names.

## Validation and baseline comparison

All commands ran locally in isolated worktrees. The MySQL wrapper uses its acknowledged disposable Docker fixture; no application database, schema file or live deployment was changed. Raw command/log/JSON evidence is retained under:

    C:\Users\Mike\Documents\AORebirth\tools-temp\worktree-snapshots\mission-dao-parallel-ready\build-verify\mission-parallel

Each JSON records cwd, starting SHA, command, read-lease use, UTC time, exit code, log hash and diagnostics. Base/dao folders are separate; failed development test runs remain as evidence and were not overwritten.

| Gate | Clean 19f6122a baseline | Final DAO sources |
| --- | --- | --- |
| Windows solution restore/build | PASS | PASS |
| Legacy Interfaces build | PASS | PASS |
| Legacy Database build | PASS | PASS |
| Isolated real-MySQL persistence suite | 147 PASS | **202 PASS twice** (mysql-isolated-3 and mysql-isolated-4) |
| Mission-only architecture guard/self-tests | PASS | PASS |
| Full project-reference MySQL suite | FAIL before tests | Same baseline failure |
| Unfiltered AOtomation compatibility suite | FAIL before tests | Same baseline failure |
| SourceInventoryGuard --check | FAIL | Same baseline failure |
| Generated combat --check | PASS | PASS |
| Generated mission-level graph --check | PASS | PASS |
| Secret scan | PASS | PASS |
| git diff --check | PASS | PASS |

Confirmed shared failures:

1. Full project-reference MySQL suite: CS2001, missing AORebirth.Enums/ItemType.cs referenced by LinuxBuild/source-inventory/AORebirth.Enums.CompileItems.props:18. No full-project MySQL test execution is claimed.
2. SourceInventoryGuard: STALE inventory for AORebirth.Enums.csproj. This gate stops there; no claim that all later inventories passed.
3. Unfiltered AOtomation compilation: 18 diagnostics on both baseline and DAO (17 missing AggDef fixture members and one missing PlayfieldAnarchyFMessage.Unknown1). Relevant mission/runtime compatibility tests are included but do not execute; they were not removed/excluded to manufacture a pass.

No shared source/inventory/ZoneEngine fixes were made. Windows builds do not substitute for Linux acceptance. The isolated compatibility-shim checks pass but do not validate engine adapters or host lifecycle.

Two development test runs failed because the new fixture initially attempted reward claims on an active mission and then targeted the wrong ledger table name for fault injection. Only the test fixture was corrected: completed state before claim, actual missionrewardledger insertion hook. Both corrected full isolated runs pass. This was not a production mission defect.

## Remaining risks and future owner handoff

1. **Adapter rollback-copy risk:** MissionDaoRepositoryAdapter SaveMission:106, SaveObjective:118, SaveFlag:139, SaveAccountFlag:151 create a DTO, save it and CopyBack before Execute commits. DAO rollback restores that DTO's Version, not the separately copied domain record. Execute:66 contains no corresponding domain-copy rollback restoration. Existing callers must discard/reload those objects on failure. If future runtime retains them, its owner must add failure reconciliation/version restoration or use the same neutral DTO objects. Do not move the runtime mapper into Database.
2. Unknown commit outcomes require durable reconciliation and idempotent external effects. A successful item/packet effect is not rolled back by MySQL. No automatic retry/cross-system exactly-once guarantee is claimed.
3. Start-area fallback null/false masks the reason for failure by existing contract; do not use it as proof of missing data. No return-type change was made.
4. Full Linux/runtime compatibility remains blocked by independently reproduced baseline failures. Engine integration requires those gates to pass after their owners repair them.
5. Generated mission offer/accepted/key/entrance/zoning/file-expiry behavior is outside this unchanged-schema SQL slice. Completing that future migration is a separate design/ownership task, not part of the YES readiness statement here.
6. Character deletion is a cross-aggregate workflow; its cleanup is unchanged. No mission hard-delete was introduced.
7. DAO scope is not authorization: the runtime must derive character/account identity from the authenticated session. Do not trust client-supplied account keys.
8. Another developer may change runtime methods while this work is isolated. Reconcile this line-number map with that developer's stable commit; never overwrite their work.

### Future integration files

After ownership is explicitly released, the runtime owner should start at the actual new-engine composition root and its mission service/adapter equivalents. This task deliberately creates no ZoneEngine_New target file or DI registration. For the current legacy source, the exact reference/review set is:

- AORebirth/Server/ZoneEngine/Program.cs
- AORebirth/Server/ZoneEngine/Core/Missions/MissionRuntime.cs
- AORebirth/Server/ZoneEngine/Core/Missions/MissionDaoRepositoryAdapter.cs
- AORebirth/Server/ZoneEngine/Core/Missions/MissionDataMapper.cs
- AORebirth/Server/ZoneEngine/Core/Missions/PersistentMissionService.cs
- AORebirth/Server/ZoneEngine/Core/Missions/MissionRewardCoordinator.cs
- AORebirth/Server/ZoneEngine/Core/Missions/MissionRollFeeService.cs
- AORebirth/Server/ZoneEngine/Core/NewCharacterStartAreaSelectionRuntime.cs

These are **review targets, not a request to edit every file**. Existing legacy wiring already satisfies most of the map. Add focused runtime adapter/rollback tests in the existing AOtomation test project when its shared compilation baseline is repaired.

Future minimal wiring should NOT edit the stable Interfaces mission DTO/DAO contract, Database mission implementation/factory/shim, schema/migrations, unrelated DAOs, generated content, deployment scripts or root solution just to adapt runtime entities. It should not alter packet formats, mission generation, player sessions, entrances/playfields/zoning or the file stores listed above without a separate gameplay/integration authorization. Keep another developer's active files untouched until ownership is released.

### Recommended integration order

1. Agree the stable runtime commit and ownership handoff; preserve separate branches and compare the 47 review points.
2. Consume the existing factory at the new runtime composition root; pass IMissionDao to mission services. Keep any runtime/domain mapping on the runtime side.
3. Preserve keys, nullable values, statuses, ordering and full transaction groups exactly as mapped. Resolve adapter copy-back failure handling before retaining mutable domain objects across failures.
4. Add adapter contract tests for missing vs error, wrong owner, stale version, failed outer commit after copied versions, and multi-write rollback; do not change gameplay rules.
5. Run persistence and runtime gates below after baseline owners repair shared compilation. Only then let Mike validate local mission window, acceptance, logoff/login, zoning, deletion/key behavior; no automated client control.
6. Review a small integration-only commit. Do not bundle generated-file migration, schema changes or deployment. Live Linux deployment requires later explicit authorization and the governed exact-SHA release workflow.

## Acceptance commands

Run from the chosen isolated checkout, using cmd.exe and the documented wrappers:

    git diff --check
    call Tools\run_mission_dao_validation.cmd --isolated-sources
    call Tools\run_mission_dao_validation.cmd --isolated-sources
    call Tools\run_dao_architecture_guard.cmd --mission-persistence-only
    call Tools\run_mission_dao_validation.cmd
    call Tools\run_aotomation_messaging_tests.cmd
    call Tools\generate_capture_backed_npc_combat_inventory.cmd --check
    call Tools\generate_mission_level_graph.cmd --check
    call Tools\scan_secrets.cmd

For legacy MSBuild/SourceInventoryGuard, select Python with Tools\select_python_runtime.cmd, obtain the configured VS MSBuild using vswhere, then use the existing generated-combat read-lease command form:

    "%AO_REBIRTH_PYTHON_EXE%" Tools\generated_combat_pipeline.py --run-read-lease -- "C:\Program Files\Microsoft Visual Studio\18\Community\MSBuild\Current\Bin\MSBuild.exe" AORebirth\AORebirth.sln /t:Restore /p:RestorePackagesConfig=true /p:Configuration=Debug /m:1 /nr:false /v:minimal
    "%AO_REBIRTH_PYTHON_EXE%" Tools\generated_combat_pipeline.py --run-read-lease -- "C:\Program Files\Microsoft Visual Studio\18\Community\MSBuild\Current\Bin\MSBuild.exe" AORebirth\Libraries\Source\AORebirth.Interfaces\AORebirth.Interfaces.csproj /t:Build /p:Configuration=Debug /m:1 /nr:false /v:minimal
    "%AO_REBIRTH_PYTHON_EXE%" Tools\generated_combat_pipeline.py --run-read-lease -- "C:\Program Files\Microsoft Visual Studio\18\Community\MSBuild\Current\Bin\MSBuild.exe" AORebirth\Libraries\Source\AORebirth.Database\AORebirth.Database.csproj /t:Build /p:Configuration=Debug /m:1 /nr:false /v:minimal
    "%AO_REBIRTH_PYTHON_EXE%" Tools\generated_combat_pipeline.py --run-read-lease -- "C:\Program Files\Microsoft Visual Studio\18\Community\MSBuild\Current\Bin\MSBuild.exe" AORebirth\AORebirth.sln /t:Build /p:Configuration=Debug /m:1 /nr:false /v:minimal
    "%AO_REBIRTH_PYTHON_EXE%" Tools\generated_combat_pipeline.py --run-read-lease -- dotnet run --project LinuxBuild/Tools/SourceInventoryGuard/SourceInventoryGuard.csproj -- --repository-root . --manifest LinuxBuild/source-inventory/inventory.json --check

The MSBuild path above is the executable resolved on this machine. Other hosts must use their configured VS toolchain. Retained per-gate .cmd files record the executed commands. No global guard activation is part of this task.

## Files inspected / changed

Inspected: the three required audit/roadmap/acceptance documents, startup/workflow instructions, the mission contract/implementation/factory/shim, Connector and mapper/transaction helpers, mission SQL definitions, legacy and Linux project/reference/source inventories, DAO test sources/wrappers, mission guard, CharacterDao cleanup, the eight mapped runtime files, eleven file stores, and LoginEngine's existing start-area call. Runtime reads were for this map only.

Changed: the four production persistence paths in the architecture table; Tools/MissionDaoValidation/HardeningChecks.cs, ParallelContractChecks.cs, IsolatedHost.cs and MissionDaoValidation.csproj; Tools/DaoArchitectureGuard/dao_architecture_guard.py; this report. Ignored build-verify orchestration/logs are local evidence, not product changes.

    ZONEENGINE_FILES_CHANGED=NO
    ZONEENGINE_NEW_FILES_CHANGED=NO
    ZONEENGINE_PROJECT_CHANGED=NO
    MISSION_DAO_INTERFACE_COMPLETE=YES
    MISSION_DAO_IMPLEMENTATION_COMPLETE=YES
    MISSION_DAO_INTEGRATION_READY=YES
    MYSQL_TESTS=202 PASS twice
    DAO_GUARD=PASS
    DATABASE_SCHEMA_CHANGED=NO
    RUNTIME_MISSION_LOGIC_CHANGED=NO
    PACKET_BEHAVIOR_CHANGED=NO
    LIVE_DEPLOYMENT_PERFORMED=NO
    DEFERRED_ZONEENGINE_CALL_SITES=47

Discord-ready: Mission persistence is ready for the runtime handoff. 202 MySQL checks pass twice, Windows builds and mission guard pass. No ZoneEngine/schema/gameplay/live changes. The handoff maps 47 existing SQL integration review points and calls out the separate runtime/file-store work and pre-existing Linux/test build blockers.
