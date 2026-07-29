namespace ZoneEngine.Core.Missions
{
    #region Usings ...

    using System;
    using AORebirth.Core.Entities;

    using SmokeLounge.AOtomation.Messaging.GameData;
    using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;

    #endregion

    internal sealed class MissionAcgAcceptedQfuContract
    {
        internal MissionRollType MissionType { get; set; }

        internal int QuestActionVersion { get; set; }

        internal int QuestIdentityFlag { get; set; }

        internal Identity AcceptedQuestIdentity { get; set; }

        internal Identity BuildingIdentity { get; set; }

        internal Identity RuntimeObjectiveIdentity { get; set; }

        internal Identity MissionItemIdentity { get; set; }

        internal Identity IssuingTerminalIdentity { get; set; }

        internal QuestFullUpdateMessage Message { get; set; }
    }

    /// <summary>
    /// Separate structured accepted-QFU builders for the five capture-backed mission contracts.
    /// </summary>
    internal static class MissionAcgAcceptedQfuBuilder
    {
        internal static MissionAcgAcceptedQfuContract Build(
            ICharacter character,
            QuestInfo acceptedState,
            MissionAcgInstanceBinding instanceBinding,
            MissionAcgObjectiveRecord objectiveRecord,
            int clientExpirySeconds)
        {
            if (character == null
                || acceptedState == null
                || instanceBinding == null
                || objectiveRecord == null)
            {
                throw new ArgumentNullException("Accepted QFU inputs are required.");
            }
            if (clientExpirySeconds <= 0)
            {
                throw new ArgumentOutOfRangeException(
                    "clientExpirySeconds",
                    "Accepted QFU requires a positive client-clock expiry.");
            }

            MissionRollType type = instanceBinding.MissionType;
            switch (type)
            {
                case MissionRollType.KillPerson:
                    return BuildKill(
                        character,
                        acceptedState,
                        instanceBinding,
                        objectiveRecord,
                        clientExpirySeconds);
                case MissionRollType.FindPerson:
                    return BuildFindPerson(
                        character,
                        acceptedState,
                        instanceBinding,
                        objectiveRecord,
                        clientExpirySeconds);
                case MissionRollType.FindItem:
                    return BuildFindItem(
                        character,
                        acceptedState,
                        instanceBinding,
                        objectiveRecord,
                        clientExpirySeconds);
                case MissionRollType.FindItemReturn:
                    return BuildReturnItem(
                        character,
                        acceptedState,
                        instanceBinding,
                        objectiveRecord,
                        clientExpirySeconds);
                case MissionRollType.RepairMachine:
                    return BuildRepair(
                        character,
                        acceptedState,
                        instanceBinding,
                        objectiveRecord,
                        clientExpirySeconds);
                default:
                    throw new InvalidOperationException(
                        "Unsupported generated mission accepted QFU type.");
            }
        }

        private static MissionAcgAcceptedQfuContract BuildKill(
            ICharacter character,
            QuestInfo state,
            MissionAcgInstanceBinding binding,
            MissionAcgObjectiveRecord objective,
            int clientExpirySeconds)
        {
            return BuildCore(
                character,
                state,
                binding,
                objective,
                16,
                0,
                clientExpirySeconds);
        }

        private static MissionAcgAcceptedQfuContract BuildFindPerson(
            ICharacter character,
            QuestInfo state,
            MissionAcgInstanceBinding binding,
            MissionAcgObjectiveRecord objective,
            int clientExpirySeconds)
        {
            return BuildCore(
                character,
                state,
                binding,
                objective,
                16,
                64,
                clientExpirySeconds);
        }

        private static MissionAcgAcceptedQfuContract BuildFindItem(
            ICharacter character,
            QuestInfo state,
            MissionAcgInstanceBinding binding,
            MissionAcgObjectiveRecord objective,
            int clientExpirySeconds)
        {
            return BuildCore(
                character,
                state,
                binding,
                objective,
                15,
                0,
                clientExpirySeconds);
        }

        private static MissionAcgAcceptedQfuContract BuildReturnItem(
            ICharacter character,
            QuestInfo state,
            MissionAcgInstanceBinding binding,
            MissionAcgObjectiveRecord objective,
            int clientExpirySeconds)
        {
            return BuildCore(
                character,
                state,
                binding,
                objective,
                8,
                0,
                clientExpirySeconds);
        }

        private static MissionAcgAcceptedQfuContract BuildRepair(
            ICharacter character,
            QuestInfo state,
            MissionAcgInstanceBinding binding,
            MissionAcgObjectiveRecord objective,
            int clientExpirySeconds)
        {
            if (objective.Binding.RequiredMissionItemTemplateId
                    != MissionAcgObjectiveContract.RepairComponentTemplateId
                || objective.Binding.RequiredMachineTemplateId
                    != MissionAcgObjectiveContract.RepairMachineTemplateId)
            {
                throw new InvalidOperationException(
                    "Repair QFU requires the captured component-to-machine contract.");
            }

            return BuildCore(
                character,
                state,
                binding,
                objective,
                16,
                0,
                clientExpirySeconds);
        }

        private static MissionAcgAcceptedQfuContract BuildCore(
            ICharacter character,
            QuestInfo state,
            MissionAcgInstanceBinding binding,
            MissionAcgObjectiveRecord objective,
            int version,
            int questIdentityFlag,
            int clientExpirySeconds)
        {
            Identity accepted = ToIdentity(binding.AcceptedQuestIdentity);
            Identity building = ToIdentity(binding.AcgBuildingIdentity);
            Identity runtimeObjective =
                ToIdentity(objective.Binding.RuntimeObjectiveIdentity);
            Identity missionItem = ToIdentity(objective.State.MissionItemIdentity);
            Identity terminal = ToIdentity(binding.IssuingTerminalIdentity);
            QuestActionList source =
                state.QuestActions != null && state.QuestActions.Length > 0
                    ? state.QuestActions[0]
                    : null;
            var action =
                new QuestActionInfo
                {
                    Version = version,
                    Action = source == null ? Identity.None : Copy(source.Action),
                    UnknownId1 = source == null ? Identity.None : Copy(source.Unknown1),
                    UnknownId2 = source == null ? Identity.None : Copy(source.Unknown2),
                    UnknownId3 = source == null ? Identity.None : Copy(source.Unknown3),
                    UnknownId4 = source == null ? Identity.None : Copy(source.Unknown4),
                    Unknown1 = source == null ? 0 : source.Unknown5,
                    Unknown2 = source == null ? 0 : source.Unknown6,
                    Unknown3 = source == null ? 0 : source.Unknown7,
                    Unknown4 = source == null ? 0 : source.Unknown8,
                    UnknownId5 = source == null ? Identity.None : Copy(source.Unknown9),
                    Unknown5 = source == null ? 0 : source.Unknown10,
                    Unknown6 = source == null ? 0 : source.Unknown11,
                    Unknown7 = source == null ? 0 : source.Unknown12,
                    Unknown8 = source == null ? 0 : source.Unknown13,
                    UnknownId6 = source == null ? Identity.None : Copy(source.Unknown14),
                    UnknownHash1 =
                        MissionRollService.IntToFixedBinaryString(clientExpirySeconds),
                    Unknown9 = source == null ? 0 : source.Unknown16,
                    UnknownId7 = terminal,
                    PlayfieldId =
                        source == null
                            ? ToIdentity(binding.ExteriorEntranceIdentity)
                            : Copy(source.Playfield),
                    Unknown10 = binding.ExteriorEntranceLow,
                    Unknown11 = binding.ExteriorEntranceHigh,
                    Position =
                        new Vector3(
                            binding.ExteriorX,
                            binding.ExteriorY,
                            binding.ExteriorZ)
                };

            switch (binding.MissionType)
            {
                case MissionRollType.KillPerson:
                case MissionRollType.FindPerson:
                    action.UnknownId2 = runtimeObjective;
                    break;
                case MissionRollType.FindItem:
                case MissionRollType.FindItemReturn:
                    action.Action = runtimeObjective;
                    break;
                case MissionRollType.RepairMachine:
                    action.Action = missionItem;
                    action.UnknownId1 = runtimeObjective;
                    break;
            }

            MissionItemReward[] itemRewards =
                state.ItemRewards == null
                    ? new MissionItemReward[0]
                    : Array.ConvertAll(
                        state.ItemRewards,
                        delegate(QuestItemShort item)
                        {
                            return new MissionItemReward
                                   {
                                       LowId = item.LowId,
                                       HighId = item.HighId,
                                       Ql = item.Quality,
                                       Unknown = 0
                                   };
                        });
            QuestIdentity[] questIdentities =
                questIdentityFlag == 0
                    ? new QuestIdentity[0]
                    : new[]
                      {
                          new QuestIdentity
                          {
                              Unknown1 = runtimeObjective,
                              Unknown2 = questIdentityFlag
                          }
                      };
            var quest =
                new Quest
                {
                    QuestId = accepted,
                    Unknown1 = state.Unknown1,
                    Unknown2 = state.Unknown2,
                    Unknown3 = state.Unknown3,
                    Unknown4 = state.Unknown4,
                    ShortInfo = state.ShortInfo ?? string.Empty,
                    LongInfo = state.Info ?? string.Empty,
                    UnknownId1 = building,
                    Unknown5 = state.RewardDescriptorVersion,
                    Unknown6 = state.CashReward,
                    Unknown7 = state.Unknown6,
                    Unknown8 = state.ExperienceReward,
                    Unknown9 = 0x03F1,
                    Unknown10 = 0x03F1,
                    MissionItemData = itemRewards,
                    Unknown11 = state.Unknown7,
                    Unknown12 = state.Unknown8,
                    Unknown13 = state.Unknown9,
                    UnknownHash1 =
                        MissionRollService.IntToFixedBinaryString(state.UnknownHash),
                    Unknown14 = state.Unknown10,
                    Unknown15 = state.Unknown11,
                    Unknown16 = state.Unknown12,
                    Unknown17 = state.Unknown13,
                    Unknown18 = 0,
                    UnknownId2 = runtimeObjective,
                    MissionIconId = state.MissionIconId,
                    Unknown20 = state.Unknown15,
                    Unknown21 = state.Unknown16,
                    QuestActions = new[] { action },
                    PlayerIds = new[] { Copy(character.Identity) },
                    UnknownArray1 = state.Unknown18 ?? new int[0],
                    UnknownArray2 = state.Unknown19 ?? new int[0],
                    CharacterInfos = new CharacterInfo[0],
                    Unknown22 = state.Unknown20,
                    PlayerIds2 = new[] { Copy(character.Identity) },
                    Unknown23 = state.Unknown21,
                    Unknown24 = state.Unknown22,
                    UnknownId3 = ToIdentity(binding.MissionKeyIdentity),
                    Unknown25 = state.Unknown24,
                    Unknown26 = state.Unknown25,
                    QuestIdentities = questIdentities,
                    Unknown27 = state.Unknown26,
                    FactionInfos = new Identity[0],
                    Unknown28 = state.Unknown27
                };
            var message =
                new QuestFullUpdateMessage
                {
                    Identity = Copy(character.Identity),
                    Unknown = 0,
                    Quests = new[] { quest }
                };
            return new MissionAcgAcceptedQfuContract
                   {
                       MissionType = binding.MissionType,
                       QuestActionVersion = version,
                       QuestIdentityFlag = questIdentityFlag,
                       AcceptedQuestIdentity = accepted,
                       BuildingIdentity = building,
                       RuntimeObjectiveIdentity = runtimeObjective,
                       MissionItemIdentity = missionItem,
                       IssuingTerminalIdentity = terminal,
                       Message = message
                   };
        }

        private static Identity ToIdentity(MissionAcgIdentityRecord value)
        {
            return value == null
                       ? Identity.None
                       : new Identity
                         {
                             Type = (IdentityType)value.Type,
                             Instance = value.Instance
                         };
        }

        private static Identity Copy(Identity value)
        {
            return value == null
                       ? Identity.None
                       : new Identity
                         {
                             Type = value.Type,
                             Instance = value.Instance
                         };
        }

    }
}
