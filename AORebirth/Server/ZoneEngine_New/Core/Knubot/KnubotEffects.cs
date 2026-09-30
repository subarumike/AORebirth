namespace ZoneEngine_New.Core.Knubot;

using System;
using System.Collections.Generic;

using AORebirth.Enums;

using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;

using ZoneEngine_New.Core.Entities;
using ZoneEngine_New.Core.Inventory;
using ZoneEngine_New.Core.Quests;
using ZoneEngine_New.Core.Trade;

/// <summary>Services a line's effects are applied through.</summary>
public sealed class KnubotEffectServices(QuestService quests, HashItemMinter minter, InventoryFlushService flush, TradeService trades)
{
    public QuestService Quests { get; } = quests;
    public HashItemMinter Minter { get; } = minter;
    public InventoryFlushService Flush { get; } = flush;
    public TradeService Trades { get; } = trades;
}

/// <summary>What a line's effects would do, built before anything is applied.</summary>
public sealed class KnubotPlan
{
    public Dictionary<string, QuestState?> Quests { get; } = new(StringComparer.Ordinal);
    public int SlotsNeeded { get; set; }

    public QuestState? QuestState(KnubotContext context, string hash)
    {
        if (Quests.TryGetValue(hash, out QuestState? planned))
            return planned;

        return context.Quests.GetLog(context.Player).Quests.TryGetValue(hash, out PlayerQuest? quest) ? quest.State : null;
    }
}

/// <summary>
/// A side effect written inline in a line's text. All of a line's effects are checked together against one
/// <see cref="KnubotPlan"/> before the line starts, so a line that cannot finish goes to its Fail line up front.
/// Each is then applied when the text reaches it, after one more check against the player's state at that moment.
/// </summary>
public abstract record KnubotEffect
{
    public abstract bool Check(KnubotContext context, KnubotPlan plan, KnubotEffectServices services);

    public abstract void Apply(KnubotContext context, KnubotEffectServices services);

    public static bool CanApplyAll(KnubotEffect[] effects, KnubotContext context, KnubotEffectServices services)
    {
        if (effects.Length == 0)
            return true;

        Player player = context.Player;
        if (!player.Inventory.IsHydrated)
            return false;

        var plan = new KnubotPlan();
        foreach (KnubotEffect effect in effects)
        {
            if (!effect.Check(context, plan, services))
                return false;
        }

        return KnubotItems.FreeSlots(player) >= plan.SlotsNeeded;
    }

    /// <summary>Applies one effect if it can still be applied now.</summary>
    public static bool TryApply(KnubotEffect effect, KnubotContext context, KnubotEffectServices services)
    {
        if (!CanApplyAll([effect], context, services))
            return false;

        effect.Apply(context, services);
        return true;
    }
}

/// <summary>
/// <c>{givequest HASH}</c>: gives a Quests.json quest. A quest the player already holds active is skipped,
/// so the rest of the line still plays.
/// </summary>
public sealed record KnubotAcceptQuest(string Hash) : KnubotEffect
{
    public override bool Check(KnubotContext context, KnubotPlan plan, KnubotEffectServices services)
    {
        plan.Quests[Hash] = QuestState.Active;
        return true;
    }

    public override void Apply(KnubotContext context, KnubotEffectServices services)
    {
        if (context.Quests.GetLog(context.Player).Quests.TryGetValue(Hash, out PlayerQuest? quest) && quest.State == QuestState.Active)
            return;

        services.Quests.TryAssignTemplate(context.Player, Hash, out _);
    }
}

/// <summary><c>{completequest HASH}</c>: completes an active quest, granting its reward and next step.</summary>
public sealed record KnubotCompleteQuest(string Hash) : KnubotEffect
{
    public override bool Check(KnubotContext context, KnubotPlan plan, KnubotEffectServices services)
    {
        if (plan.QuestState(context, Hash) != QuestState.Active)
            return false;

        plan.Quests[Hash] = QuestState.Completed;
        if (services.Quests.Catalog.TryGet(Hash, out QuestTemplate template) && template.Reward?.Records is { Count: > 0 } records)
        {
            int most = 0;
            foreach (List<QuestRewardItem> record in records)
                most = Math.Max(most, record.Count);

            plan.SlotsNeeded += most;
        }

        return true;
    }

    public override void Apply(KnubotContext context, KnubotEffectServices services)
        => services.Quests.TryComplete(context.Player, Hash, out _);
}

/// <summary><c>{spawn ITEMHASH QL [count]}</c>: mints the item into the player's inventory.</summary>
public sealed record KnubotSpawnItem(string Hash, int Quality, int Count) : KnubotEffect
{
    public override bool Check(KnubotContext context, KnubotPlan plan, KnubotEffectServices services)
    {
        if (!services.Minter.TryRollIds(Hash, Quality, out _, out _, out _))
            return false;

        plan.SlotsNeeded += Count;
        return true;
    }

    public override void Apply(KnubotContext context, KnubotEffectServices services)
    {
        for (int i = 0; i < Count; i++)
        {
            if (services.Minter.TryMint(Hash, Quality, ItemSource.Quest, out Item item))
                KnubotItems.Give(context.Player, item, services.Flush);
        }
    }
}

/// <summary><c>{shop}</c>: opens the NPC's vendor window.</summary>
public sealed record KnubotOpenShop : KnubotEffect
{
    public override bool Check(KnubotContext context, KnubotPlan plan, KnubotEffectServices services) => context.Npc.Shop != null;

    public override void Apply(KnubotContext context, KnubotEffectServices services)
        => services.Trades.TryOpenShop(context.Player, context.Npc.Shop!);
}

/// <summary><c>{opentrade}</c>: opens the NPC's Give Item window for the script's Trades.</summary>
public sealed record KnubotOpenTrade : KnubotEffect
{
    public override bool Check(KnubotContext context, KnubotPlan plan, KnubotEffectServices services) => context.Script.Trades.Length != 0;

    public override void Apply(KnubotContext context, KnubotEffectServices services)
        => services.Trades.TryOpenKnubot(context.Player, context.Npc, KnubotService.TradeText(context.Npc));
}

/// <summary>Main-inventory item helpers shared by conditions and effects.</summary>
public static class KnubotItems
{
    public static int CountCarried(Player player, HashSet<int> templateIds)
    {
        if (!player.Inventory.IsHydrated)
            return 0;

        int count = 0;
        foreach (Item item in player.Inventory.Inventory.Content.Values)
        {
            if (item != null && (templateIds.Contains(item.LowId) || templateIds.Contains(item.HighId)))
                count += Math.Max(1, item.StackCount);
        }

        return count;
    }

    public static int FreeSlots(Player player)
    {
        Container page = player.Inventory.Inventory;
        int used = 0;
        foreach (int slot in page.Content.Keys)
        {
            if (slot >= page.Offset && slot < page.Offset + page.Capacity)
                used++;
        }

        return page.Capacity - used;
    }

    public static void Give(Player player, Item item, InventoryFlushService flush)
    {
        Container page = player.Inventory.Inventory;
        int slot = page.FindFreeSlot();
        if (slot < 0 || !page.Add(slot, item))
            return;

        player.Inventory.MarkDirty(item, page, slot);
        flush.NotifyDirty(player);
        player.Session?.Send(new AddTemplateMessage
        {
            Identity = player.Identity,
            HighId = item.HighId,
            LowId = item.LowId,
            Quality = item.Quality,
            Count = item.StackCount
        });
    }
}
