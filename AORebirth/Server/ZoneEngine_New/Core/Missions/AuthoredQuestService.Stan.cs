namespace ZoneEngine_New.Core.Missions;

using System;
using System.Linq;
using AORebirth.Interfaces.Persistence.Missions;
using SmokeLounge.AOtomation.Messaging.GameData;
using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;
using ZoneEngine_New.Core.Entities;
using ZoneEngine_New.Core.Inventory;

public sealed partial class AuthoredQuestService
{
    /// <summary>Called only after a real nearby Merchant's Strongbox target has been resolved by the playfield owner.</summary>
    public bool TryUseLockpickOnStrongbox(Player player, Identity slot, Item lockpick)
    {
        if (player.Playfield?.Identity.Instance != 6553 || (lockpick.LowId != 95577 && lockpick.HighId != 95577)) return false;
        return Mutate(player, null, (tx, service) =>
        {
            RequireSource(player, slot, lockpick);
            var grants = HasCarried(player, 248306) ? Array.Empty<Item>() : new[] { CreateItem(248306, 1) };
            var plan = Plan(player, grants);
            CompleteIfPresent(service, player.Identity.Instance, Strongbox, "mission_555BE9C5_strongbox");
            Accept(service, player.Identity.Instance, DeliverFactory);
            if (tx is not IMissionInventoryMutationTransaction inventory) throw new InvalidOperationException("Mission DAO lacks atomic item effects.");
            inventory.ApplyInventoryMutation(plan.Rows.Select(ToMissionItem).ToArray(), []);
            return () =>
            {
                plan.PublishAfterCommit(false);
                foreach (var grant in grants) SendOverflowGrant(player, grant);
                player.Session?.Send(new FormatFeedbackMessage { Identity = player.Identity, Unknown = 1,
                    FormattedMessage = "~&!!!\":!!!)<sOYou successfully picked this lock and obtained the Antonio's Adaption Factory." });
                AuthoredQuestJournal.Delete(player, unchecked((int)0x555BE9C5));
                AuthoredQuestJournal.Send(player, DeliverFactory, _now());
            };
        });
    }

    /// <summary>
    /// Trusted Stan trade completion. The handler owns exact NPC/session/staged-item validation;
    /// its accepted RejectedItems publication runs here only after the complete durable commit.
    /// </summary>
    public bool TryTurnInFactory(Player player, Identity slot, Item factory, Action publishAcceptedTrade)
    {
        if (player.Playfield?.Identity.Instance != 6553 || (factory.LowId != 248306 && factory.HighId != 248306)) return false;
        ArgumentNullException.ThrowIfNull(publishAcceptedTrade);
        return Mutate(player, null, (tx, service) =>
        {
            RequireSource(player, slot, factory);
            if (tx.GetMission(new(player.Identity.Instance, DeliverFactory))?.State is not (MissionLifecycleState.Active or MissionLifecycleState.Offered))
                throw new InvalidOperationException("Factory reward requires an unfinished accepted mission.");
            var grants = HasCarried(player, 296572) ? Array.Empty<Item>() : new[] { CreateItem(296572, 1) };
            var plan = Plan(player, grants);
            CompleteIfPresent(service, player.Identity.Instance, DeliverFactory, "mission_555BE9F2_deliver_factory");
            var stats = ApplyStanStats(tx, player, DeliverFactory, "captured-stan-factory-turnin-xp-credits", 2596, 1240,
                "capture:20260721-afgter-dog-lockpick-goodman:stan-turnin-xp-credits", _now().Ticks);
            Accept(service, player.Identity.Instance, TalkSarah);
            Accept(service, player.Identity.Instance, BuyNano);
            ApplyRows(tx, plan, player, slot, factory);
            return () =>
            {
                plan.PublishAfterCommit(false);
                player.Inventory.Inventory.Content.Remove(slot.Instance);
                // Stan trade consumption uses DeleteItem only, unlike package Use.
                player.Session?.Send(new CharacterActionMessage { Identity = player.Identity, Action = CharacterActionType.DeleteItem, Target = slot });
                publishAcceptedTrade();
                PublishStats(player, stats);
                player.Session?.Send(new FormatFeedbackMessage { Identity = player.Identity, Unknown = 1,
                    FormattedMessage = "~&!!!\":$'O\"ui!!!?Oi!!!/S~" });
                foreach (var grant in grants) SendOverflowGrant(player, grant);
                player.Session?.Send(new FeedbackMessage { Identity = player.Identity, Unknown = 1, CategoryId = 110, MessageId = 108871108 });
                AuthoredQuestJournal.Delete(player, unchecked((int)0x555BE9F2));
                AuthoredQuestJournal.Send(player, TalkSarah, _now());
                AuthoredQuestJournal.Send(player, BuyNano, _now());
            };
        });
    }
}
