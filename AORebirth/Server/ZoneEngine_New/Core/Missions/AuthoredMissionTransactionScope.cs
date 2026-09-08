namespace ZoneEngine_New.Core.Missions;

using System;
using System.Collections.Generic;
using AORebirth.Interfaces.Persistence.Missions;

/// <summary>
/// Adapts the existing mission domain service to an already-open DAO transaction. Nested
/// service operations reuse it; they cannot start a second connection or independently commit.
/// Unsupported read/workflow APIs throw, rather than silently escaping the transaction.
/// </summary>
internal sealed class AuthoredMissionTransactionScope(IMissionDaoTransaction transaction) : IMissionDao
{
    public T Execute<T>(int characterId, Func<IMissionDaoTransaction, T> operation) => Execute(characterId, null!, operation);
    public T Execute<T>(int characterId, string accountKey, Func<IMissionDaoTransaction, T> operation)
    {
        if (characterId != transaction.CharacterId || (accountKey != null && !string.Equals(accountKey, transaction.AccountKey, StringComparison.OrdinalIgnoreCase)))
            throw new InvalidOperationException("Nested authored mission scope changed owner/account.");
        return operation(transaction);
    }
    public MissionStateData GetMission(MissionKeyData key) => transaction.GetMission(key);
    public IList<MissionStateData> GetMissions(int characterId) => transaction.GetMissions(characterId);
    public MissionAccountFlagData GetAccountFlag(string accountKey, string flagKey) => transaction.GetAccountFlag(accountKey, flagKey);
    public string ResolveCharacterAccountKey(int characterId) => characterId == transaction.CharacterId ? transaction.AccountKey : throw new InvalidOperationException("Owner mismatch.");
    public MissionCharacterSnapshotData ReadCharacter(int characterId) => throw new NotSupportedException("Snapshot reads belong outside the mutation scope.");
    public IList<MissionAccountFlagData> GetAccountFlags(string accountKey) => throw new NotSupportedException("Full account snapshot is not an authored mutation.");
    public MissionRollFeeResult TryChargeRollFee(MissionRollFeeRequest request) => throw new NotSupportedException();
    public bool MarkStartAreaSelectionPending(int characterId) => throw new NotSupportedException();
    public string GetStartAreaSelectionState(int characterId) => throw new NotSupportedException();
    public bool TryCompleteStartAreaSelection(int characterId, string selectedState) => throw new NotSupportedException();
}
