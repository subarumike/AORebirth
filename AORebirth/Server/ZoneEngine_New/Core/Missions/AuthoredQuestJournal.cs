namespace ZoneEngine_New.Core.Missions;

using System;
using SmokeLounge.AOtomation.Messaging.GameData;
using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;
using ZoneEngine.Core;
using ZoneEngine.Core.Arete.Quests;
using ZoneEngine_New.Core.Entities;
using ZoneEngine_New.Core.Network;

internal static class AuthoredQuestJournal
{
    internal static bool Send(Player player, string questId, DateTime now)
    {
        QuestFullUpdateMessage? packet = questId switch
        {
            AuthoredQuestService.TalkStan => SafeQuestFullUpdateSender.CreateTalkToStanPreviewMessage(player.Identity),
            AuthoredQuestService.BuyLockpick => SafeQuestFullUpdateSender.CreateBuyLockpickPreviewMessage(player.Identity),
            AuthoredQuestService.Strongbox => SafeQuestFullUpdateSender.CreateStrongboxContentsPreviewMessage(player.Identity),
            AuthoredQuestService.DeliverFactory => SafeQuestFullUpdateSender.CreateDeliverAntonioFactoryPreviewMessage(player.Identity),
            AuthoredQuestService.TalkSarah => SafeQuestFullUpdateSender.CreateTalkToSarahGreenePreviewMessage(player.Identity),
            AuthoredQuestService.BuyNano => SafeQuestFullUpdateSender.CreateBuyNanoProgramsPreviewMessage(player.Identity),
            AuthoredQuestService.FindThief => SafeQuestFullUpdateSender.CreateFindTheThiefPreviewMessage(player.Identity),
            AuthoredQuestService.DeliverArmor => SafeQuestFullUpdateSender.CreateDeliverDnaLockedArmorPreviewMessage(player.Identity),
            AuthoredQuestService.TalkVernon => SafeQuestFullUpdateSender.CreateSpeakToVernonGodfrayPreviewMessage(player.Identity),
            _ => null
        };
        if (packet == null || player.Session == null) return false;
        // Same captured anchor + 48h tip projection as SafeQuestFullUpdateSender. Authored
        // tips have no durable elapsed expiry field; do not borrow generated-mission expiry.
        player.Session.Send(new GameTimeMessage { Identity = player.Identity, Unknown1 = 30024.0f, Unknown3 = 185408, Unknown4 = 80183.3125f });
        if (player.Session is IGameTimeSession clock) clock.RecordGameTimeSynchronization(now);
        player.Session.Send(packet);
        return true;
    }

    internal static void Delete(Player player, int questInstance)
    {
        foreach (byte[] packet in FlintKneecappingTipWire.CreateDeletePackets(player.Identity.Instance, questInstance))
            player.Session?.Send(packet);
        // Stan uses Flint's raw pair plus the existing typed delete. DOJA has a different
        // accepted two-frame contract and must not use this method.
        player.Session?.Send(new QuestMessage
        {
            Identity = player.Identity, Unknown = 0, Action = SmokeLounge.AOtomation.Messaging.Messages.N3Messages.QuestAction.Delete, Unknown1 = 0,
            Mission = new Identity { Type = IdentityType.Mission, Instance = questInstance }, Unknown2 = 0, Unknown3 = 0
        });
    }
}
