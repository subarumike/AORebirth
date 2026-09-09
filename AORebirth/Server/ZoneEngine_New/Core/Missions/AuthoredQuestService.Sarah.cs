namespace ZoneEngine_New.Core.Missions;

using System;
using System.Linq;
using AORebirth.Interfaces.Persistence.Missions;
using SmokeLounge.AOtomation.Messaging.GameData;
using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;
using ZoneEngine_New.Core.Entities;
using ZoneEngine_New.Core.Inventory;

/// <summary>Existing capture 20260721-sara effects; all rows and mission handoffs commit before publication.</summary>
public sealed partial class AuthoredQuestService
{
    public const string FindThief = "Mission:555BE9F5", DeliverArmor = "Mission:555BE9F6", TalkVernon = "Mission:555BE9F7";

    public bool TryResolveSarahDialogueStart(Player player, out string? startNode)
    {
        startNode = null;
        lock (player.PersistenceGate)
        {
            if (!IsCurrent(player) || player.Playfield!.Identity.Instance != 6553) return false;
            try
            {
                if (HasCarried(player, 295618) || _dao.GetMission(new(player.Identity.Instance, DeliverArmor))?.State == MissionLifecycleState.Active)
                    startNode = "sarah_deliver_001";
                return true;
            }
            catch (Exception exception) { _logger.Error(exception, "Sarah dialogue state could not be read."); return false; }
        }
    }

    public bool AcceptSarahJob(Player player)
    {
        if (player.Playfield?.Identity.Instance != 6553) return false;
        return Mutate(player, null, (tx, service) =>
        {
            if (HasCarried(player, 295618) || new[] { FindThief, DeliverArmor, TalkVernon }.Any(quest =>
                tx.GetMission(new(player.Identity.Instance, quest))?.State is MissionLifecycleState.Active or MissionLifecycleState.Completed)) return null;
            CompleteIfPresent(service, player.Identity.Instance, TalkSarah, "mission_555BE9F3_talk_sarah",
                "sarah-greene-force-complete", "SarahGreeneQuestRuntime");
            Accept(service, player.Identity.Instance, FindThief);
            return () => { AuthoredQuestJournal.Delete(player, unchecked((int)0x555BE9F3)); AuthoredQuestJournal.Send(player, FindThief, _now()); };
        });
    }

    /// <summary>The owner handler must first resolve the exact current accepted thief-remains target and nearby player.</summary>
    public bool TryUseShopThiefRemains(Player player, Action publishAcknowledgement)
    {
        ArgumentNullException.ThrowIfNull(publishAcknowledgement);
        if (player.Playfield?.Identity.Instance != 6553) return false;
        return Mutate(player, null, (tx, service) =>
        {
            var deliver = tx.GetMission(new(player.Identity.Instance, DeliverArmor));
            // Legacy's completed-tip recovery reissued an unfinishable quest/item. Never erase that terminal history.
            if (deliver?.State == MissionLifecycleState.Completed
                || tx.GetMission(new(player.Identity.Instance, TalkVernon))?.State is MissionLifecycleState.Active or MissionLifecycleState.Completed)
                throw new InvalidOperationException("The thief recovery has already been delivered.");
            if (tx.GetMission(new(player.Identity.Instance, FindThief))?.State != MissionLifecycleState.Active
                && deliver?.State != MissionLifecycleState.Active && !HasCarried(player, 295618))
                throw new InvalidOperationException("Thief remains require the accepted unfinished Sarah quest.");
            var grants = HasCarried(player, 295618) ? Array.Empty<Item>() : new[] { CreateItem(295618, 200) };
            var plan = Plan(player, grants);
            CompleteIfPresent(service, player.Identity.Instance, FindThief, "mission_555BE9F5_find_thief",
                "sarah-greene-force-complete", "SarahGreeneQuestRuntime");
            Accept(service, player.Identity.Instance, DeliverArmor);
            if (tx is not IMissionInventoryMutationTransaction inventory) throw new InvalidOperationException("Mission DAO lacks atomic item effects.");
            inventory.ApplyInventoryMutation(plan.Rows.Select(ToMissionItem).ToArray(), []);
            return () =>
            {
                plan.PublishAfterCommit(false);
                publishAcknowledgement();
                foreach (var grant in grants) SendOverflowGrant(player, grant);
                AuthoredQuestJournal.Delete(player, unchecked((int)0x555BE9F5));
                AuthoredQuestJournal.Send(player, DeliverArmor, _now());
            };
        });
    }

    public bool TryTurnInDnaArmor(Player player, Identity slot, Item armor, Action publishAcceptedTrade)
    {
        ArgumentNullException.ThrowIfNull(publishAcceptedTrade);
        if (player.Playfield?.Identity.Instance != 6553 || (armor.LowId != 295618 && armor.HighId != 295618)) return false;
        return Mutate(player, null, (tx, service) =>
        {
            RequireSource(player, slot, armor);
            if (tx.GetMission(new(player.Identity.Instance, DeliverArmor))?.State is not (MissionLifecycleState.Active or MissionLifecycleState.Offered))
                throw new InvalidOperationException("Armor reward requires an unfinished accepted delivery mission.");
            var reward = CreateItem(296574, 1);
            var grants = HasCarried(player, 296574) ? Array.Empty<Item>() : new[] { reward };
            var plan = Plan(player, grants);
            CompleteIfPresent(service, player.Identity.Instance, DeliverArmor, "mission_555BE9F6_deliver_armor",
                "sarah-greene-force-complete", "SarahGreeneQuestRuntime");
            var stats = ApplyStanStats(tx, player, DeliverArmor, "captured-sarah-armor-turnin-xp-credits", 2229, 1280,
                "capture:20260721-sara:sarah-turnin-xp-credits", _now().Ticks);
            Accept(service, player.Identity.Instance, TalkVernon);
            ApplyRows(tx, plan, player, slot, armor);
            return () =>
            {
                plan.PublishAfterCommit(false);
                player.Inventory.Inventory.Content.Remove(slot.Instance);
                player.Session?.Send(new CharacterActionMessage { Identity = player.Identity, Action = CharacterActionType.DeleteItem, Target = slot });
                publishAcceptedTrade();
                PublishStats(player, stats);
                player.Session?.Send(new FormatFeedbackMessage { Identity = player.Identity, Unknown = 1,
                    FormattedMessage = "~&!!!\":$'O\"ui!!!;4i!!!0&~" });
                // The accepted unique-item branch still emitted this exact pair when already carried.
                SendOverflowGrant(player, reward);
                player.Session?.Send(new FeedbackMessage { Identity = player.Identity, Unknown = 1, CategoryId = 110, MessageId = 108871108 });
                AuthoredQuestJournal.Delete(player, unchecked((int)0x555BE9F6));
                AuthoredQuestJournal.Send(player, TalkVernon, _now());
            };
        });
    }
}
