namespace ZoneEngine.Core.Missions
{
    using System.Collections.Generic;
    using System.Linq;

    using Persistence = AORebirth.Interfaces.Persistence.Missions;

    internal static class MissionDataMapper
    {
        internal static Persistence.MissionKeyData ToData(MissionKey value)
        {
            return new Persistence.MissionKeyData(value.CharacterId, value.QuestId);
        }

        internal static Persistence.MissionObjectiveKeyData ToData(MissionObjectiveKey value)
        {
            return new Persistence.MissionObjectiveKeyData(ToData(value.Mission), value.ObjectiveId);
        }

        internal static Persistence.MissionRewardKeyData ToData(MissionRewardKey value)
        {
            return new Persistence.MissionRewardKeyData(ToData(value.Mission), value.RewardKey);
        }

        internal static Persistence.MissionStateData ToData(MissionStateRecord value)
        {
            if (value == null)
            {
                return null;
            }

            return new Persistence.MissionStateData
                   {
                       CharacterId = value.CharacterId,
                       QuestId = value.QuestId,
                       State = (Persistence.MissionLifecycleState)(int)value.State,
                       CurrentStepId = value.CurrentStepId,
                       OfferedAtUtcTicks = value.OfferedAtUtcTicks,
                       AcceptedAtUtcTicks = value.AcceptedAtUtcTicks,
                       CompletedAtUtcTicks = value.CompletedAtUtcTicks,
                       FailedAtUtcTicks = value.FailedAtUtcTicks,
                       AbandonedAtUtcTicks = value.AbandonedAtUtcTicks,
                       CreatedAtUtcTicks = value.CreatedAtUtcTicks,
                       UpdatedAtUtcTicks = value.UpdatedAtUtcTicks,
                       Version = value.Version
                   };
        }

        internal static MissionStateRecord ToDomain(Persistence.MissionStateData value)
        {
            if (value == null)
            {
                return null;
            }

            return new MissionStateRecord
                   {
                       CharacterId = value.CharacterId,
                       QuestId = value.QuestId,
                       State = (MissionLifecycleState)(int)value.State,
                       CurrentStepId = value.CurrentStepId,
                       OfferedAtUtcTicks = value.OfferedAtUtcTicks,
                       AcceptedAtUtcTicks = value.AcceptedAtUtcTicks,
                       CompletedAtUtcTicks = value.CompletedAtUtcTicks,
                       FailedAtUtcTicks = value.FailedAtUtcTicks,
                       AbandonedAtUtcTicks = value.AbandonedAtUtcTicks,
                       CreatedAtUtcTicks = value.CreatedAtUtcTicks,
                       UpdatedAtUtcTicks = value.UpdatedAtUtcTicks,
                       Version = value.Version
                   };
        }

        internal static Persistence.MissionObjectiveProgressData ToData(MissionObjectiveProgressRecord value)
        {
            if (value == null)
            {
                return null;
            }

            return new Persistence.MissionObjectiveProgressData
                   {
                       CharacterId = value.CharacterId,
                       QuestId = value.QuestId,
                       ObjectiveId = value.ObjectiveId,
                       Progress = value.Progress,
                       RequiredCount = value.RequiredCount,
                       LastObservationKey = value.LastObservationKey,
                       CreatedAtUtcTicks = value.CreatedAtUtcTicks,
                       UpdatedAtUtcTicks = value.UpdatedAtUtcTicks,
                       Version = value.Version
                   };
        }

        internal static MissionObjectiveProgressRecord ToDomain(Persistence.MissionObjectiveProgressData value)
        {
            if (value == null)
            {
                return null;
            }

            return new MissionObjectiveProgressRecord
                   {
                       CharacterId = value.CharacterId,
                       QuestId = value.QuestId,
                       ObjectiveId = value.ObjectiveId,
                       Progress = value.Progress,
                       RequiredCount = value.RequiredCount,
                       LastObservationKey = value.LastObservationKey,
                       CreatedAtUtcTicks = value.CreatedAtUtcTicks,
                       UpdatedAtUtcTicks = value.UpdatedAtUtcTicks,
                       Version = value.Version
                   };
        }

        internal static Persistence.MissionObjectiveObservationData ToData(
            MissionObjectiveObservationRecord value)
        {
            if (value == null)
            {
                return null;
            }

            return new Persistence.MissionObjectiveObservationData
                   {
                       CharacterId = value.CharacterId,
                       QuestId = value.QuestId,
                       ObjectiveId = value.ObjectiveId,
                       ObservationKey = value.ObservationKey,
                       EventType = value.EventType,
                       SourceIdentity = value.SourceIdentity,
                       TargetIdentity = value.TargetIdentity,
                       ObservedAtUtcTicks = value.ObservedAtUtcTicks
                   };
        }

        internal static Persistence.MissionFlagData ToData(MissionFlagRecord value)
        {
            if (value == null)
            {
                return null;
            }

            return new Persistence.MissionFlagData
                   {
                       CharacterId = value.CharacterId,
                       QuestId = value.QuestId,
                       FlagKey = value.FlagKey,
                       Value = value.Value,
                       CreatedAtUtcTicks = value.CreatedAtUtcTicks,
                       UpdatedAtUtcTicks = value.UpdatedAtUtcTicks,
                       Version = value.Version
                   };
        }

        internal static MissionFlagRecord ToDomain(Persistence.MissionFlagData value)
        {
            if (value == null)
            {
                return null;
            }

            return new MissionFlagRecord
                   {
                       CharacterId = value.CharacterId,
                       QuestId = value.QuestId,
                       FlagKey = value.FlagKey,
                       Value = value.Value,
                       CreatedAtUtcTicks = value.CreatedAtUtcTicks,
                       UpdatedAtUtcTicks = value.UpdatedAtUtcTicks,
                       Version = value.Version
                   };
        }

        internal static Persistence.MissionAccountFlagData ToData(MissionAccountFlagRecord value)
        {
            if (value == null)
            {
                return null;
            }

            return new Persistence.MissionAccountFlagData
                   {
                       AccountKey = value.AccountKey,
                       FlagKey = value.FlagKey,
                       Value = value.Value,
                       SourceQuestId = value.SourceQuestId,
                       CreatedAtUtcTicks = value.CreatedAtUtcTicks,
                       UpdatedAtUtcTicks = value.UpdatedAtUtcTicks,
                       Version = value.Version
                   };
        }

        internal static MissionAccountFlagRecord ToDomain(Persistence.MissionAccountFlagData value)
        {
            if (value == null)
            {
                return null;
            }

            return new MissionAccountFlagRecord
                   {
                       AccountKey = value.AccountKey,
                       FlagKey = value.FlagKey,
                       Value = value.Value,
                       SourceQuestId = value.SourceQuestId,
                       CreatedAtUtcTicks = value.CreatedAtUtcTicks,
                       UpdatedAtUtcTicks = value.UpdatedAtUtcTicks,
                       Version = value.Version
                   };
        }

        internal static Persistence.MissionRewardStageData ToData(MissionRewardStageRecord value)
        {
            if (value == null)
            {
                return null;
            }

            return new Persistence.MissionRewardStageData
                   {
                       CharacterId = value.CharacterId,
                       QuestId = value.QuestId,
                       RewardKey = value.RewardKey,
                       RewardType = value.RewardType,
                       Status = (Persistence.MissionRewardStatus)(int)value.Status,
                       Attempts = value.Attempts,
                       LastError = value.LastError,
                       EffectReference = value.EffectReference,
                       ClaimToken = value.ClaimToken,
                       ClaimedAtUtcTicks = value.ClaimedAtUtcTicks,
                       ClaimExpiresAtUtcTicks = value.ClaimExpiresAtUtcTicks,
                       AppliedAtUtcTicks = value.AppliedAtUtcTicks,
                       CreatedAtUtcTicks = value.CreatedAtUtcTicks,
                       UpdatedAtUtcTicks = value.UpdatedAtUtcTicks,
                       Version = value.Version
                   };
        }

        internal static MissionRewardStageRecord ToDomain(Persistence.MissionRewardStageData value)
        {
            if (value == null)
            {
                return null;
            }

            return new MissionRewardStageRecord
                   {
                       CharacterId = value.CharacterId,
                       QuestId = value.QuestId,
                       RewardKey = value.RewardKey,
                       RewardType = value.RewardType,
                       Status = (MissionRewardStatus)(int)value.Status,
                       Attempts = value.Attempts,
                       LastError = value.LastError,
                       EffectReference = value.EffectReference,
                       ClaimToken = value.ClaimToken,
                       ClaimedAtUtcTicks = value.ClaimedAtUtcTicks,
                       ClaimExpiresAtUtcTicks = value.ClaimExpiresAtUtcTicks,
                       AppliedAtUtcTicks = value.AppliedAtUtcTicks,
                       CreatedAtUtcTicks = value.CreatedAtUtcTicks,
                       UpdatedAtUtcTicks = value.UpdatedAtUtcTicks,
                       Version = value.Version
                   };
        }

        internal static Persistence.MissionStatMutationData ToData(MissionCharacterStatMutation value)
        {
            if (value == null)
            {
                return null;
            }

            return new Persistence.MissionStatMutationData
                   {
                       StatIdentityType = value.StatIdentityType,
                       StatId = value.StatId,
                       Kind = (Persistence.MissionStatMutationKind)(int)value.Kind,
                       Value = value.Value,
                       MinimumValue = value.MinimumValue,
                       MaximumValue = value.MaximumValue
                   };
        }

        internal static MissionCharacterStatValue ToDomain(Persistence.MissionStatValueData value)
        {
            if (value == null)
            {
                return null;
            }

            return new MissionCharacterStatValue
                   {
                       StatIdentityType = value.StatIdentityType,
                       StatId = value.StatId,
                       Value = value.Value
                   };
        }

        internal static MissionCharacterSnapshot ToDomain(Persistence.MissionCharacterSnapshotData value)
        {
            if (value == null)
            {
                return null;
            }

            return new MissionCharacterSnapshot(
                value.CharacterId,
                value.Missions.Select(ToDomain),
                value.Objectives.Select(ToDomain),
                value.Flags.Select(ToDomain),
                value.Rewards.Select(ToDomain));
        }

        internal static IList<MissionStateRecord> ToDomain(IList<Persistence.MissionStateData> values)
        {
            return (values ?? new Persistence.MissionStateData[0]).Select(ToDomain).ToList();
        }

        internal static IList<MissionAccountFlagRecord> ToDomain(
            IList<Persistence.MissionAccountFlagData> values)
        {
            return (values ?? new Persistence.MissionAccountFlagData[0]).Select(ToDomain).ToList();
        }

        internal static void CopyBack(Persistence.MissionStateData source, MissionStateRecord target)
        {
            MissionStateRecord mapped = ToDomain(source);
            target.QuestId = mapped.QuestId;
            target.CurrentStepId = mapped.CurrentStepId;
            target.Version = mapped.Version;
        }

        internal static void CopyBack(
            Persistence.MissionObjectiveProgressData source,
            MissionObjectiveProgressRecord target)
        {
            MissionObjectiveProgressRecord mapped = ToDomain(source);
            target.QuestId = mapped.QuestId;
            target.ObjectiveId = mapped.ObjectiveId;
            target.LastObservationKey = mapped.LastObservationKey;
            target.Version = mapped.Version;
        }

        internal static void CopyBack(Persistence.MissionFlagData source, MissionFlagRecord target)
        {
            MissionFlagRecord mapped = ToDomain(source);
            target.QuestId = mapped.QuestId;
            target.FlagKey = mapped.FlagKey;
            target.Version = mapped.Version;
        }

        internal static void CopyBack(
            Persistence.MissionAccountFlagData source,
            MissionAccountFlagRecord target)
        {
            MissionAccountFlagRecord mapped = ToDomain(source);
            target.AccountKey = mapped.AccountKey;
            target.FlagKey = mapped.FlagKey;
            target.Version = mapped.Version;
        }

        internal static void CopyBack(
            Persistence.MissionObjectiveObservationData source,
            MissionObjectiveObservationRecord target)
        {
            target.QuestId = source.QuestId;
            target.ObjectiveId = source.ObjectiveId;
            target.ObservationKey = source.ObservationKey;
        }
    }
}
