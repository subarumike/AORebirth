namespace ZoneEngine_New.Core.Knubot;

using System;
using System.Collections.Generic;
using System.Linq;

using AORebirth.Enums;
using AORebirth.Modules.Events;

using SmokeLounge.AOtomation.Messaging.GameData;
using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;

using ZoneEngine_New.Core.Entities;
using ZoneEngine_New.Core.Inventory;
using ZoneEngine_New.Core.Modules;
using ZoneEngine_New.Core.Quests;
using ZoneEngine_New.Core.Trade;

/// <summary>Services a line's effects are applied through.</summary>
public sealed class KnubotEffectServices(QuestService quests, HashItemMinter minter, InventoryFlushService flush, TradeService trades,
    InventoryActionService inventoryActions)
{
    public QuestService Quests { get; } = quests;
    public HashItemMinter Minter { get; } = minter;
    public InventoryFlushService Flush { get; } = flush;
    public TradeService Trades { get; } = trades;
    public InventoryActionService InventoryActions { get; } = inventoryActions;
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

        // No inventory space is reserved: the quest service delivers a reward that does not fit into overflow, so a
        // full inventory never stops the dialog that completes it.
        plan.Quests[Hash] = QuestState.Completed;
        return true;
    }

    public override void Apply(KnubotContext context, KnubotEffectServices services)
        => services.Quests.TryComplete(context.Player, Hash, out _);
}

/// <summary><c>{spawn ITEMHASH QL [count]}</c>: mints the item into the player's inventory.</summary>
public sealed record KnubotSpawnItem(string Hash, int? Quality, int Count) : KnubotEffect
{
    /// <summary>The written QL, or the player's level when <see cref="Quality"/> is null (<c>level</c>).</summary>
    int QualityFor(KnubotContext context) => Quality ?? context.Player.Stats.GetOrOne(CharacterStat.Level);

    public override bool Check(KnubotContext context, KnubotPlan plan, KnubotEffectServices services)
    {
        if (!services.Minter.TryRollIds(Hash, QualityFor(context), out _, out _, out _))
            return false;

        plan.SlotsNeeded += Count;
        return true;
    }

    public override void Apply(KnubotContext context, KnubotEffectServices services)
    {
        for (int i = 0; i < Count; i++)
        {
            if (!services.Minter.TryMint(Hash, QualityFor(context), ItemSource.Quest, out Item item))
                continue;
            if (UniqueItems.WouldDuplicate(context.Player, item, out string uniqueName))
            {
                Helpers.ClientFeedback.SendFormatted(context.Player, Helpers.ClientFeedback.AlreadyHaveUniqueItem, uniqueName);
                continue;
            }

            KnubotItems.Give(context.Player, item, services.Flush);
        }
    }
}

/// <summary>
/// <c>{take ITEMHASH [count]}</c>: takes carried main-inventory items minted from the hash, any QL (Windcaller Karrec's
/// "Give it to me!" takes the Bronto Burger and Maddy's Credit Card without a Give Item window). The line fails up
/// front when the player does not carry enough.
/// </summary>
public sealed record KnubotTakeItem(HashSet<int> TemplateIds, int Count) : KnubotEffect
{
    public override bool Check(KnubotContext context, KnubotPlan plan, KnubotEffectServices services)
        => KnubotItems.CountCarried(context.Player, TemplateIds) >= Count;

    public override void Apply(KnubotContext context, KnubotEffectServices services)
    {
        Player player = context.Player;
        Container page = player.Inventory.Inventory;
        int remaining = Count;
        foreach (var (placement, item) in page.Content.ToArray())
        {
            if (remaining <= 0)
                break;
            if (item == null || item.Locked || (!TemplateIds.Contains(item.LowId) && !TemplateIds.Contains(item.HighId)))
                continue;

            int stack = Math.Max(1, item.StackCount);
            if (stack > remaining)
            {
                if (item.TakeFromStack(player, page, placement, remaining))
                    remaining = 0;
                continue;
            }

            var slot = new Identity { Type = page.Identity.Type, Instance = placement };
            if (!services.InventoryActions.TryDelete(player, slot, item.InstanceId))
                continue;

            remaining -= stack;
            player.Session?.Send(new CharacterActionMessage
            {
                Identity = player.Identity, Action = CharacterActionType.DeleteItem, Target = slot
            });
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
        => services.Trades.TryOpenKnubot(context.Player, context.Npc, KnubotService.TradeText(context.Npc), context.Script.TradeSlots);
}

/// <summary>
/// <c>{givebuff NANOID}</c>: the NPC lands the nano on the player at once, as a service (no cast bar, nano cost or
/// recharge; the Portal Technician's Shadowlands charge 239825).
/// </summary>
public sealed record KnubotGiveBuff(int NanoId) : KnubotEffect
{
    public override bool Check(KnubotContext context, KnubotPlan plan, KnubotEffectServices services)
        => !context.Player.IsDead && context.Player.Playfield != null;

    public override void Apply(KnubotContext context, KnubotEffectServices services)
    {
        Playfield.Playfield playfield = context.Player.Playfield!;
        Nanos.NanoRuntime.TryApplyImmediate(
            context.Npc,
            context.Player,
            NanoId,
            playfield.GetRequiredService<IItemBuilder>(),
            playfield.GetRequiredService<Data.IInventoryRepository>(),
            DateTime.UtcNow);
    }
}

/// <summary>
/// <c>{useitem ITEMHASH}</c> or <c>{useitem ITEMID}</c>: the item's OnUse runs on the player, nothing goes to the inventory. For live's virtual
/// items (Null flag) that apply a service: Specialization Item - First..Fourth Spec 229611-229613/229614 set
/// Specialization for Professor Dedlock.
/// </summary>
public sealed record KnubotUseItem(int ItemId) : KnubotEffect
{
    public override bool Check(KnubotContext context, KnubotPlan plan, KnubotEffectServices services)
        => !context.Player.IsDead && context.Player.Playfield != null;

    public override void Apply(KnubotContext context, KnubotEffectServices services)
    {
        Playfield.Playfield playfield = context.Player.Playfield!;
        IItemBuilder items = playfield.GetRequiredService<IItemBuilder>();
        Item item = items.Create(ItemId, ItemId, 1, ItemSource.Quest);
        item.Definition.ExecuteOnUseSpells(context.Player, playfield.GetRequiredService<Data.IInventoryRepository>(), items, source: context.Player);
        context.Player.FlushDirtyStats();
    }
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
        if (InventoryStacking.TryGrantOntoStack(player, item, flush))
            return;

        // The first free inventory slot, else overflow (which keeps only its newest items).
        if (!player.Inventory.TryPlace(item, out Container page, out int slot))
            return;

        player.Inventory.MarkDirty(item, page, slot);
        flush.NotifyDirty(player);
        ModuleHooks.ItemMoved(player, item, ModuleHooks.Place(ItemPlaceKind.None), ModuleHooks.Place(ItemPlaceKind.Inventory));
        if (ReferenceEquals(page, player.Inventory.Overflow))
        {
            OverflowGrants.Send(player, item);
            OverflowGrants.Trim(player);
            return;
        }

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
