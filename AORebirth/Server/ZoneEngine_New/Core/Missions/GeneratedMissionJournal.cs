namespace ZoneEngine_New.Core.Missions;

using System;
using System.Linq;
using AORebirth.Interfaces.Persistence.Missions;
using SmokeLounge.AOtomation.Messaging.GameData;
using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;
using ZoneEngine.Core.Missions;

/// <summary>Projects the committed DAO identity and the immutable offered wire fields.</summary>
internal static class GeneratedMissionJournal
{
    internal static QuestFullUpdateMessage Project(Identity player, GeneratedMissionBinding binding, QuestInfo offered, int expiry)
    {
        if (expiry <= 0 || binding.OwnerId != player.Instance || binding.QuestInstance <= 0
            || binding.ObjectiveInstance <= 0 || offered.QuestActions is not { Length: 1 })
            throw new InvalidOperationException("An owned accepted mission and its single frozen objective are required.");
        var source = offered.QuestActions[0];
        var objective = Id(binding.ObjectiveType, binding.ObjectiveInstance);
        var type = (MissionRollType)binding.Offer.MissionType;
        var action = new QuestActionInfo
        {
            Version = type switch { MissionRollType.FindItem => 15, MissionRollType.FindItemReturn => 8,
                MissionRollType.KillPerson or MissionRollType.FindPerson or MissionRollType.RepairMachine => 16,
                _ => throw new InvalidOperationException("Unsupported accepted mission type.") },
            Action = Copy(source.Action), UnknownId1 = Copy(source.Unknown1), UnknownId2 = Copy(source.Unknown2),
            UnknownId3 = Copy(source.Unknown3), UnknownId4 = Copy(source.Unknown4), UnknownId5 = Copy(source.Unknown9),
            UnknownId6 = Copy(source.Unknown14), UnknownId7 = Id(binding.Offer.IssuingTerminalType, binding.Offer.IssuingTerminalInstance),
            Unknown1 = source.Unknown5, Unknown2 = source.Unknown6, Unknown3 = source.Unknown7, Unknown4 = source.Unknown8,
            Unknown5 = source.Unknown10, Unknown6 = source.Unknown11, Unknown7 = source.Unknown12, Unknown8 = source.Unknown13,
            Unknown9 = source.Unknown16, Unknown10 = binding.Offer.EntranceLow, Unknown11 = binding.Offer.EntranceHigh,
            UnknownHash1 = GeneratedMissionWire.ExpiryField(expiry), PlayfieldId = Copy(source.Playfield),
            Position = new(binding.Offer.DestinationX, binding.Offer.DestinationY, binding.Offer.DestinationZ)
        };
        if (type is MissionRollType.KillPerson or MissionRollType.FindPerson) action.UnknownId2 = objective;
        else if (type is MissionRollType.FindItem or MissionRollType.FindItemReturn) action.Action = objective;
        else
        {
            if (binding.MissionItem == null) throw new InvalidOperationException("Repair mission has no durable component.");
            action.Action = Id(binding.MissionItem.ItemType, binding.MissionItem.InstanceId);
            action.UnknownId1 = objective;
        }
        var quest = new Quest
        {
            QuestId = Id(binding.QuestType, binding.QuestInstance), UnknownId1 = Id(binding.BuildingType, binding.BuildingInstance),
            UnknownId2 = objective, UnknownId3 = Id(0xC76D, binding.KeyInstance),
            ShortInfo = offered.ShortInfo ?? "", LongInfo = offered.Info ?? "", MissionIconId = offered.MissionIconId,
            QuestActions = [action], PlayerIds = [Copy(player)], PlayerIds2 = [Copy(player)],
            MissionItemData = (offered.ItemRewards ?? []).Select(value => new MissionItemReward { LowId = value.LowId, HighId = value.HighId, Ql = value.Quality }).ToArray(),
            QuestIdentities = type == MissionRollType.FindPerson ? [new QuestIdentity { Unknown1 = objective, Unknown2 = 64 }] : [],
            UnknownArray1 = offered.Unknown18 ?? [], UnknownArray2 = offered.Unknown19 ?? [], CharacterInfos = [], FactionInfos = [],
            UnknownHash1 = GeneratedMissionWire.ExpiryField(offered.UnknownHash),
            Unknown1 = offered.Unknown1, Unknown2 = offered.Unknown2, Unknown3 = offered.Unknown3, Unknown4 = offered.Unknown4,
            Unknown5 = offered.RewardDescriptorVersion, Unknown6 = offered.CashReward, Unknown7 = offered.Unknown6,
            Unknown8 = offered.ExperienceReward, Unknown9 = 0x03F1, Unknown10 = 0x03F1,
            Unknown11 = offered.Unknown7, Unknown12 = offered.Unknown8, Unknown13 = offered.Unknown9,
            Unknown14 = offered.Unknown10, Unknown15 = offered.Unknown11, Unknown16 = offered.Unknown12, Unknown17 = offered.Unknown13,
            Unknown20 = offered.Unknown15, Unknown21 = offered.Unknown16, Unknown22 = offered.Unknown20,
            Unknown23 = offered.Unknown21, Unknown24 = offered.Unknown22, Unknown25 = offered.Unknown24,
            Unknown26 = offered.Unknown25, Unknown27 = offered.Unknown26, Unknown28 = offered.Unknown27
        };
        return new QuestFullUpdateMessage { Identity = Copy(player), Quests = [quest] };
    }
    static Identity Id(int type, int instance) => new() { Type = (IdentityType)type, Instance = instance };
    static Identity Copy(Identity value) => value == null ? Identity.None : Id((int)value.Type, value.Instance);
}
