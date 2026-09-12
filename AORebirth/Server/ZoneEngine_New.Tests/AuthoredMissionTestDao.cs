namespace ZoneEngine_New.Tests;

using System;
using System.Collections.Generic;
using System.Linq;
using AORebirth.Interfaces.Persistence.Missions;
using SmokeLounge.AOtomation.Messaging.GameData;

/// <summary>Transactional test storage: mutations operate on a private copy and publish only at commit.</summary>
internal sealed class AuthoredMissionTestDao : IMissionDao, IMissionDaoTransaction, IMissionInventoryMutationTransaction
{
    internal Dictionary<MissionKeyData, MissionStateData> Missions = [];
    internal Dictionary<MissionObjectiveKeyData, MissionObjectiveProgressData> Objectives = [];
    internal Dictionary<string, MissionFlagData> Flags = [];
    internal Dictionary<string, MissionAccountFlagData> AccountFlags = [];
    internal Dictionary<MissionRewardKeyData, MissionRewardStageData> Rewards = [];
    internal HashSet<string> Observations = [];
    internal Dictionary<int, MissionItemInstanceData> Items = [];
    internal Dictionary<int, long> Stats = [];
    internal int Calls;
    internal Exception? Failure;
    internal bool UnknownCommit;
    internal Action<AuthoredMissionTestDao>? BeforeCommit;
    public int CharacterId { get; private set; } = 111;
    public string AccountKey { get; private set; } = "account";

    public T Execute<T>(int characterId, Func<IMissionDaoTransaction, T> operation) => Execute(characterId, null!, operation);
    public T Execute<T>(int characterId, string accountKey, Func<IMissionDaoTransaction, T> operation)
    {
        if (characterId != 111 || accountKey != null && accountKey != "account") throw new InvalidOperationException("Owner mismatch.");
        Calls++;
        var working = new AuthoredMissionTestDao
        {
            CharacterId = characterId, AccountKey = accountKey!,
            Missions = Missions.ToDictionary(pair => pair.Key, pair => pair.Value.Clone()),
            Objectives = Objectives.ToDictionary(pair => pair.Key, pair => pair.Value.Clone()),
            Flags = Flags.ToDictionary(pair => pair.Key, pair => pair.Value.Clone()),
            AccountFlags = AccountFlags.ToDictionary(pair => pair.Key, pair => pair.Value.Clone()),
            Rewards = Rewards.ToDictionary(pair => pair.Key, pair => pair.Value.Clone()),
            Observations = new(Observations), Items = Items.ToDictionary(pair => pair.Key, pair => Copy(pair.Value)), Stats = new(Stats)
        };
        T result = operation(working);
        BeforeCommit?.Invoke(working);
        if (Failure != null) throw Failure;
        Missions = working.Missions; Objectives = working.Objectives; Flags = working.Flags; AccountFlags = working.AccountFlags;
        Rewards = working.Rewards; Observations = working.Observations; Items = working.Items; Stats = working.Stats;
        if (UnknownCommit) throw new MissionCommitOutcomeUnknownException(new InvalidOperationException("Transport lost after database commit."));
        return result;
    }

    public MissionStateData GetMission(MissionKeyData key) => Missions.GetValueOrDefault(key)?.Clone()!;
    public IList<MissionStateData> GetMissions(int characterId) => Missions.Values.Where(value => value.CharacterId == characterId).Select(value => value.Clone()).ToArray();
    public MissionCharacterSnapshotData ReadCharacter(int characterId) => new(characterId, GetMissions(characterId), Objectives.Values, Flags.Values, Rewards.Values);
    public string ResolveCharacterAccountKey(int characterId) => characterId == 111 ? "account" : throw new InvalidOperationException();
    public MissionAccountFlagData GetAccountFlag(string accountKey, string flagKey) => AccountFlags.GetValueOrDefault(accountKey + "|" + flagKey)?.Clone()!;
    public IList<MissionAccountFlagData> GetAccountFlags(string accountKey) => AccountFlags.Values.Where(value => value.AccountKey == accountKey).Select(value => value.Clone()).ToArray();
    public void SaveMission(MissionKeyData key, MissionStateData record)
    { CheckVersion(Missions.GetValueOrDefault(key)?.Version ?? 0, record.Version); record.Version++; Missions[key] = record.Clone(); }
    public MissionObjectiveProgressData GetObjective(MissionObjectiveKeyData key) => Objectives.GetValueOrDefault(key)?.Clone()!;
    public void SaveObjective(MissionObjectiveKeyData key, MissionObjectiveProgressData record)
    { CheckVersion(Objectives.GetValueOrDefault(key)?.Version ?? 0, record.Version); record.Version++; Objectives[key] = record.Clone(); }
    public bool TryAddObservation(MissionObjectiveObservationData value) => Observations.Add(value.CharacterId + "|" + value.QuestId + "|" + value.ObjectiveId + "|" + value.ObservationKey);
    public MissionFlagData GetFlag(MissionKeyData key, string flagKey) => Flags.GetValueOrDefault(key + "|" + flagKey)?.Clone()!;
    public void SaveFlag(MissionKeyData key, MissionFlagData record)
    { var index = key + "|" + record.FlagKey; CheckVersion(Flags.GetValueOrDefault(index)?.Version ?? 0, record.Version); record.Version++; Flags[index] = record.Clone(); }
    public void SaveAccountFlag(string accountKey, MissionAccountFlagData record)
    { var index = accountKey + "|" + record.FlagKey; CheckVersion(AccountFlags.GetValueOrDefault(index)?.Version ?? 0, record.Version); record.Version++; AccountFlags[index] = record.Clone(); }
    public MissionRewardStageData GetReward(MissionRewardKeyData key) => Rewards.GetValueOrDefault(key)?.Clone()!;

    public MissionAtomicStatRewardResultData TryApplyCharacterStatReward(MissionRewardKeyData key, string rewardType, IList<MissionStatMutationData> mutations, string effectReference, long now)
    {
        if (Missions.GetValueOrDefault(key.Mission)?.State != MissionLifecycleState.Completed) return new() { Status = MissionAtomicRewardStatus.Rejected };
        bool duplicate = Rewards.ContainsKey(key);
        if (!duplicate)
        {
            foreach (var mutation in mutations)
                Stats[mutation.StatId] = Math.Clamp(mutation.Kind == MissionStatMutationKind.Set ? mutation.Value : Stats.GetValueOrDefault(mutation.StatId) + mutation.Value,
                    mutation.MinimumValue, mutation.MaximumValue);
            Rewards.Add(key, new() { CharacterId = CharacterId, QuestId = key.Mission.QuestId, RewardKey = key.RewardKey,
                RewardType = rewardType, Status = MissionRewardStatus.Applied, EffectReference = effectReference, AppliedAtUtcTicks = now, Version = 1 });
        }
        return new() { Status = duplicate ? MissionAtomicRewardStatus.AlreadyApplied : MissionAtomicRewardStatus.Applied,
            StatValues = mutations.Select(value => new MissionStatValueData { StatIdentityType = value.StatIdentityType, StatId = value.StatId, Value = Stats[value.StatId] }).ToArray() };
    }

    public void ApplyInventoryMutation(IReadOnlyList<MissionItemInstanceData> grants, IReadOnlyList<MissionItemInstanceData> consumed)
    {
        foreach (var row in consumed)
        {
            if (!Items.TryGetValue(row.InstanceId, out var actual) || !Same(actual, row)) throw new InvalidOperationException("Stale item plan.");
            if (Items.Values.Any(value => value.ContainerType == (int)IdentityType.Container && value.ContainerInstance == row.InstanceId)) throw new InvalidOperationException("Container has children.");
            var retired = Copy(actual); retired.ContainerType = 0; retired.ContainerPlacement = retired.InstanceId; Items[row.InstanceId] = retired;
        }
        foreach (var row in grants)
        {
            if (row.ContainerType != 104 || row.ContainerInstance != CharacterId || Items.Values.Any(value => value.ContainerType == row.ContainerType
                && value.ContainerInstance == row.ContainerInstance && value.ContainerPlacement == row.ContainerPlacement)) throw new InvalidOperationException("Grant slot conflict.");
            Items.Add(row.InstanceId, Copy(row));
        }
    }
    internal static MissionItemInstanceData Copy(MissionItemInstanceData row) => new() { InstanceId = row.InstanceId, ContainerType = row.ContainerType,
        ContainerInstance = row.ContainerInstance, ContainerPlacement = row.ContainerPlacement, ItemType = row.ItemType, LowId = row.LowId, HighId = row.HighId,
        Quality = row.Quality, StackCount = row.StackCount, Source = row.Source };
    static bool Same(MissionItemInstanceData a, MissionItemInstanceData b) => a.InstanceId == b.InstanceId && a.ContainerType == b.ContainerType
        && a.ContainerInstance == b.ContainerInstance && a.ContainerPlacement == b.ContainerPlacement && a.ItemType == b.ItemType && a.LowId == b.LowId
        && a.HighId == b.HighId && a.Quality == b.Quality && a.StackCount == b.StackCount && a.Source == b.Source;
    static void CheckVersion(long actual, long expected) { if (actual != expected) throw new InvalidOperationException("Stale mission version."); }
    public MissionRewardClaimResultData TryClaimReward(MissionRewardKeyData key, string type, string token, long now, long expires) => throw new NotSupportedException("External reward effects forbidden in this test.");
    public bool TryMarkRewardApplied(MissionRewardKeyData key, string token, long version, string effect, long now, out MissionRewardStageData stage) => throw new NotSupportedException();
    public bool TryMarkRewardFailed(MissionRewardKeyData key, string token, long version, string error, long now, out MissionRewardStageData stage) => throw new NotSupportedException();
    public MissionRollFeeResult TryChargeRollFee(MissionRollFeeRequest request) => throw new NotSupportedException();
    public bool MarkStartAreaSelectionPending(int id) => throw new NotSupportedException();
    public string GetStartAreaSelectionState(int id) => throw new NotSupportedException();
    public bool TryCompleteStartAreaSelection(int id, string state) => throw new NotSupportedException();
}
