namespace ZoneEngine_New.Core.Knubot;

using System;
using System.Collections.Generic;
using System.Linq;

using AORebirth.Enums;

using SmokeLounge.AOtomation.Messaging.GameData;
using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;

using ZoneEngine_New.Core.Entities;
using ZoneEngine_New.Core.GameData;
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
        => CanApplyAll(effects, context, services, out _);

    /// <summary>
    /// <paramref name="lacksSpace"/> is true when every effect could apply but the main inventory has too few free
    /// slots for what the line hands over.
    /// </summary>
    public static bool CanApplyAll(KnubotEffect[] effects, KnubotContext context, KnubotEffectServices services, out bool lacksSpace)
    {
        lacksSpace = false;
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

        lacksSpace = KnubotItems.FreeSlots(player) < plan.SlotsNeeded;
        return !lacksSpace;
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

/// <summary>
/// <c>{failquest HASH}</c>: drops an active quest that is no longer an option, with no reward (Leonora Marty takes
/// the card back: Steal the credits... ends). Nothing to do when it is not held.
/// </summary>
public sealed record KnubotFailQuest(string Hash) : KnubotEffect
{
    public override bool Check(KnubotContext context, KnubotPlan plan, KnubotEffectServices services)
    {
        if (plan.QuestState(context, Hash) == QuestState.Active)
            plan.Quests[Hash] = QuestState.Failed;
        return true;
    }

    public override void Apply(KnubotContext context, KnubotEffectServices services)
    {
        if (context.Quests.GetLog(context.Player).Quests.TryGetValue(Hash, out PlayerQuest? quest))
            services.Quests.TryFail(context.Player, quest);
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
/// <c>{take ITEMHASH [count]}</c>: the NPC takes items the player carries in the main inventory, with no trade window
/// (Fia Lou unwraps the Gift from Fia Lou and keeps the paper before handing over the NCU chips). Each item taken whole
/// frees its slot for what the line gives after it. Written before the effect that needs the room.
/// </summary>
public sealed record KnubotTakeItem(string Hash, int Count) : KnubotEffect
{
    public override bool Check(KnubotContext context, KnubotPlan plan, KnubotEffectServices services)
    {
        int left = Count;
        foreach ((_, Item item) in Carried(context, services))
        {
            int stack = Math.Max(1, item.StackCount);
            if (stack <= left)
                plan.SlotsNeeded--;
            left -= Math.Min(stack, left);
            if (left == 0)
                return true;
        }

        return false;
    }

    public override void Apply(KnubotContext context, KnubotEffectServices services)
    {
        Player player = context.Player;
        Container page = player.Inventory.Inventory;
        int left = Count;
        foreach ((int placement, Item item) in Carried(context, services))
        {
            var slot = new Identity { Type = page.Identity.Type, Instance = placement };
            while (left > 0 && item.DestroyOne(player, slot))
            {
                left--;
                if (item.StackCount <= 0)
                    break;
            }

            if (left == 0)
                return;
        }
    }

    /// <summary>The main-inventory items the hash covers, with their slots, in slot order.</summary>
    List<(int Placement, Item Item)> Carried(KnubotContext context, KnubotEffectServices services)
    {
        var leaves = new List<HashInstance>();
        services.Minter.CollectLeafInstances(Hash, leaves);
        var ids = new HashSet<int>();
        foreach (HashInstance leaf in leaves)
            ids.UnionWith(leaf.TemplateIds);

        var found = new List<(int Placement, Item Item)>();
        Container page = context.Player.Inventory.Inventory;
        foreach (KeyValuePair<int, Item> entry in page.Content.OrderBy(pair => pair.Key))
        {
            if (entry.Key >= page.Offset && entry.Key < page.Offset + page.Capacity && entry.Value != null && !entry.Value.Locked
                && (ids.Contains(entry.Value.LowId) || ids.Contains(entry.Value.HighId)))
                found.Add((entry.Key, entry.Value));
        }

        return found;
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

/// <summary>
/// <c>{attack}</c>: the NPC turns on the player it is talking to (Kneebreaker Alfonzo Rizzolo, after "I have come to
/// put your plans to an end.": "...take your best shot." and he attacks, live 2026-10-07). An NPC with no brain or one
/// the player cannot fight does nothing.
/// </summary>
public sealed record KnubotAttack : KnubotEffect
{
    public override bool Check(KnubotContext context, KnubotPlan plan, KnubotEffectServices services) => true;

    public override void Apply(KnubotContext context, KnubotEffectServices services)
    {
        NpcCharacter npc = context.Npc;
        Player player = context.Player;
        if (npc.Brain == null || npc.IsDead || player.IsDead || !ReferenceEquals(npc.Playfield, player.Playfield)
            || !ZoneEngine_New.Core.Helpers.CombatRules.CanAttack(npc, player))
            return;

        npc.Brain.AddThreat(player.Identity, ZoneEngine_New.Core.Ai.NpcAiRules.ProximityHate);
        npc.StartFighting(player.Identity, 0);
    }
}

/// <summary>
/// <c>{kill}</c>: the NPC kills the player it is talking to outright, fight or not (Patrick Sun after "Excellent! Now,
/// relax. This won't hurt a bit.": live 2026-07-25 sent one HealthDamage from Patrick for the player's whole health
/// 2.95 s later, then the death; the player wakes at the insurance beacon). Written after a <c>{delay}</c>.
/// </summary>
public sealed record KnubotKill : KnubotEffect
{
    public override bool Check(KnubotContext context, KnubotPlan plan, KnubotEffectServices services) => true;

    public override void Apply(KnubotContext context, KnubotEffectServices services)
    {
        NpcCharacter npc = context.Npc;
        Player player = context.Player;
        if (npc.IsDead || player.IsDead || !ReferenceEquals(npc.Playfield, player.Playfield))
            return;

        int health = Math.Max(1, player.Stats.GetOrZero(CharacterStat.Health));
        player.AnnounceHealthDamage(npc, 0, -health, 0);
        // A friendly NPC cannot attack, so the hit is the player's own: ApplyDamage's death path without combat rules.
        player.ApplyDamage(player, health, HitType.Normal);
    }
}

/// <summary>
/// <c>{despawn}</c>: the NPC leaves as if it had died, and its spawn point brings it back on its usual timer (Lolly the
/// Reet, picked up into the player's inventory after eating the cookie).
/// </summary>
public sealed record KnubotDespawn : KnubotEffect
{
    public override bool Check(KnubotContext context, KnubotPlan plan, KnubotEffectServices services) => true;

    public override void Apply(KnubotContext context, KnubotEffectServices services)
    {
        if (!context.Npc.IsDead && context.Npc.Playfield != null)
            context.Npc.DespawnAsDeath();
    }
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
