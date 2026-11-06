namespace ZoneEngine_New.Core.Missions;

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using AORebirth.Stats.SpecialStats;
using SmokeLounge.AOtomation.Messaging.GameData;
using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;
using ZoneEngine_New.Core.Configuration;
using ZoneEngine_New.Core.Entities;
using ZoneEngine_New.Core.Helpers;

/// <summary>
/// Plan-first adapter of CombatXpRuntimeService.AwardDirectXp/AwardDirectSk.
/// Direct mission rewards are NOT the New generic 20%-of-bar AwardXp path.
/// The caller persists this complete projection with its reward ledger transaction,
/// then publishes it once. No XP, level, IP or refill is changed while planning.
/// </summary>
public sealed class DirectXpRewardPlan
{
    readonly Dictionary<CharacterStat, int> _stats;
    readonly Identity _owner;
    bool _questStyle;
    public IReadOnlyDictionary<CharacterStat, int> Stats { get; }
    public int LevelBefore { get; }
    public int LevelAfter { get; }
    public int ExperienceAfter { get; }
    public int RequestedReward { get; }
    public bool IsShadowKnowledge { get; }

    /// <summary>The XP-off preference captured while planning; research and independent rewards still apply.</summary>
    public bool NormalXpSuppressed { get; private init; }

    /// <summary>
    /// The reward's personal research share (Player.PlanResearchXp), already taken off its XP. Put into the research goal
    /// once the reward is committed, by <see cref="PublishAfterCommit"/>.
    /// </summary>
    public long ResearchXp { get; private init; }

    /// <summary>
    /// With XP gain turned off, the XP the reward would have given (after research), added to the death pool once the
    /// reward is committed, by <see cref="PublishAfterCommit"/>. The pool is saved with the character, not in the reward's
    /// transaction.
    /// </summary>
    public int PooledXp { get; private init; }

    bool _pooledPublished;

    bool _researchPublished;

    DirectXpRewardPlan(Player player, int reward, int before, int after, bool shadow, Dictionary<CharacterStat, int> stats)
    {
        _owner = player.Identity; RequestedReward = reward; LevelBefore = before; LevelAfter = after;
        IsShadowKnowledge = shadow; _stats = stats; Stats = new ReadOnlyDictionary<CharacterStat, int>(stats);
        ExperienceAfter = stats.TryGetValue(CharacterStat.XP, out var xp) ? xp : player.Stats.GetOrZero(CharacterStat.XP, StatDetail.Base);
    }

    public static DirectXpRewardPlan Create(Player player, int reward)
        => CreateCore(player, reward, false);

    /// <summary>Plan the current Character.AwardXp(Quest) rules without publishing or mutating the actor.</summary>
    public static DirectXpRewardPlan CreateQuestReward(Player player, int reward)
    {
        ArgumentNullException.ThrowIfNull(player);
        if (reward < 0) throw new ArgumentOutOfRangeException(nameof(reward));
        int before = player.Stats.GetOrOne(CharacterStat.Level), level = before;
        var values = new Dictionary<CharacterStat, int>();
        if (reward == 0) return new(player, 0, before, before, false, values) { _questStyle = true };
        var data = player.Playfield?.GetRequiredService<ZoneEngine_New.Core.GameData.IGameData>()
            ?? throw new InvalidOperationException("Quest XP requires the current GameData owner.");
        if (!data.TryGetXpLevel(level, out var current))
            throw new InvalidOperationException("Quest XP has no level data; reward remains pending.");
        int amount = player.ApplySideXpBonus(player.ApplyXpRateModifier(reward));
        // As Character.AwardXp: personal research takes its share of the whole reward, before the cap; only the XP
        // that remains is capped.
        long research = player.PlanResearchXp(amount);
        if (player.IsNormalXpSuppressed)
        {
            // XP gain turned off: what research does not take goes into the death pool (capped as the XP would be).
            long pooled = amount - research;
            if (current.NextLevelXp > 0)
                pooled = Math.Min(pooled, GameplayXpCaps.Of(player.XpCaps.Quest, current.NextLevelXp));
            return new(player, 0, before, before, false, values)
            {
                _questStyle = true, ResearchXp = research, NormalXpSuppressed = true, PooledXp = (int)Math.Max(0, pooled)
            };
        }
        amount = checked(amount - (int)research);
        if (current.NextLevelXp > 0)
            amount = (int)Math.Min(amount, GameplayXpCaps.Of(player.XpCaps.Quest, current.NextLevelXp));
        int xp = checked(player.Stats.GetOrZero(CharacterStat.XP) + amount);
        while (level < Character.MaxXpLevel && current.NextLevelXp > 0 && (long)xp >= (long)current.FloorXp + current.NextLevelXp)
        {
            if (!data.TryGetXpLevel(level + 1, out var next)) break;
            level = next.Level;
            current = next;
        }
        values[CharacterStat.XP] = xp;
        values[CharacterStat.Level] = level;
        values[CharacterStat.LastXP] = current.FloorXp;
        values[CharacterStat.NextXP] = current.NextLevelXp > 0 ? checked(current.FloorXp + current.NextLevelXp) : 0;
        if (level > before)
        {
            values[CharacterStat.TitleLevel] = Character.TitleLevelFor(level);
            var projected = player.ProjectLevelUpStats(values);
            PlanQuestVital(CharacterStat.Health, CharacterStat.MaxHealth, CharacterStat.PercentRemainingHealth);
            PlanQuestVital(CharacterStat.CurrentNano, CharacterStat.MaxNanoEnergy, CharacterStat.PercentRemainingNano);

            void PlanQuestVital(CharacterStat currentStat, CharacterStat maxStat, CharacterStat percentStat)
            {
                int maximum = projected.GetOrZero(maxStat);
                if (maximum <= 0) return;
                values[maxStat] = projected.GetOrZero(maxStat, StatDetail.Base);
                // ApplyLevelVitals writes the full maximum to Base; Character's vital-change
                // handler then caps a positive current-vital bonus back to that same maximum.
                int bonus = projected.GetOrZero(currentStat, StatDetail.Bonus);
                values[currentStat] = checked(maximum - Math.Max(0, bonus));
                values[percentStat] = (int)Math.Clamp((long)(maximum + Math.Min(0, bonus)) * 100 / maximum, 0, 100);
            }
        }
        return new(player, amount, before, level, false, values) { _questStyle = true, ResearchXp = research };
    }

    /// <summary>
    /// DOJA's accepted path adds a reward to cumulative XP, then normalizes the
    /// persisted total while preserving a distinct UnsavedXP death pool. It is
    /// deliberately separate from AwardDirectXp's immediate progress rewrite.
    /// </summary>
    public static DirectXpRewardPlan CreatePersistedReward(Player player, int reward)
        => CreateCore(player, reward, true);

    static DirectXpRewardPlan CreateCore(Player player, int reward, bool persistedTotal)
    {
        ArgumentNullException.ThrowIfNull(player);
        if (reward < 0) throw new ArgumentOutOfRangeException(nameof(reward));
        int before = player.Stats.GetOrOne(CharacterStat.Level), level = before;
        var values = new Dictionary<CharacterStat, int>();
        if (before < 1 || before > 220) throw new InvalidOperationException("Invalid progression level.");
        // Legacy declines the XP component at the level cap; it does not cancel
        // the enclosing mission's independent cash/item/token reward transaction.
        if (reward == 0 || before == 220) return new(player, reward, before, before, before >= 200, values);
        // Gameplay.json xp.globalModifier (and xp.shadowlandsModifier in the Shadowlands), as Character.AwardXp.
        reward = player.ApplyXpRateModifier(reward);
        if (reward == 0) return new(player, 0, before, before, before >= 200, values);
        bool shadow = before >= 200;
        if (shadow && persistedTotal) throw new InvalidOperationException("Persisted DOJA reward normalization is an RK progression path.");
        // Personal research takes its share of an XP reward (not of Shadowlands SK); the rest is the XP the plan adds.
        long research = shadow ? 0 : player.PlanResearchXp(reward);
        int requested = reward;
        if (!shadow && player.IsNormalXpSuppressed)
            return new(player, 0, before, before, false, values)
            {
                ResearchXp = research, NormalXpSuppressed = true, PooledXp = (int)Math.Max(0, reward - research)
            };
        reward = checked(reward - (int)research);
        if (shadow)
        {
            uint raw = Raw(player, CharacterStat.SK), floor = SkFloor(before);
            uint progress = raw >= floor ? raw - floor : raw;
            uint total = checked(floor + AddClamped(progress, Math.Max(1, reward / 1000)));
            for (int guard = 0; guard < 20 && level < 220 && total - SkFloor(level) >= SkNext(level); guard++) level++;
            values[CharacterStat.SK] = checked((int)total);
            values[CharacterStat.NextSK] = checked((int)SkNext(level));
            if (level > before) values[CharacterStat.NextXP] = 0;
        }
        else
        {
            uint raw = Raw(player, CharacterStat.XP), floor = XpFloor(before);
            // Exact Legacy GetBarProgress: an UnsavedXP value differing from current
            // bar progress is a death pool, not another source of spendable XP.
            uint progress = raw >= floor ? raw - floor : raw;
            uint total = persistedTotal ? AddClamped(raw, reward) : checked(floor + AddClamped(progress, reward));
            if (persistedTotal && total < floor) total = floor;
            uint finalProgress = total - floor;
            uint unsaved = Raw(player, CharacterStat.UnsavedXP);
            uint preservedDeathPool = persistedTotal && unsaved > 0 && unsaved != finalProgress ? unsaved : 0;
            values[CharacterStat.LastXP] = reward;
            values[CharacterStat.LastSaveXP] = 0; // Not SavedXP, the insurance watermark.
            for (int guard = 0; guard < 20 && level < 200 && finalProgress >= XpNext(level); guard++)
            {
                finalProgress -= XpNext(level);
                level++;
                total = checked(XpFloor(level) + finalProgress);
            }
            values[CharacterStat.XP] = checked((int)total);
            values[CharacterStat.UnsavedXP] = checked((int)(preservedDeathPool > 0 ? preservedDeathPool : finalProgress));
            values[CharacterStat.NextXP] = checked((int)XpNext(level));
        }
        if (level > before)
        {
            values[CharacterStat.Level] = level;
            values[CharacterStat.TitleLevel] = Character.TitleLevelFor(level);
            PlanRefill(player, values);
        }
        return new(player, requested, before, level, shadow, values) { ResearchXp = research };
    }

    static void PlanRefill(Player player, Dictionary<CharacterStat, int> values)
    {
        // Reproduce Player.Rebase's equipment-only base calculation without touching
        // the live actor. Nano-owned contributions remain Bonus and are never saved
        // into the new maximum's Base field.
        var equipment = new StatCollection();
        foreach (var entry in player.Stats.GetEntries()) equipment.Set(entry.Stat, entry.Base);
        foreach (var pair in values) equipment.Set(pair.Key, pair.Value);
        if (player.Inventory.IsHydrated) player.Inventory.ApplyWearBonuses(equipment);
        player.SkillCatalog.ApplyTrickle(equipment);
        bool healthKnown = MaxHealthCalculator.TryCompute(equipment, out int health);
        bool nanoKnown = MaxNanoCalculator.TryCompute(equipment, out int nano);
        if (healthKnown) equipment.Set(CharacterStat.MaxHealth, health);
        if (nanoKnown) equipment.Set(CharacterStat.MaxNanoEnergy, nano);
        if (healthKnown)
        {
            values[CharacterStat.MaxHealth] = health;
            int refill = checked(equipment.GetOrZero(CharacterStat.MaxHealth) - equipment.GetOrZero(CharacterStat.Health, StatDetail.Bonus));
            if (refill < 0) throw new InvalidOperationException("Prospective health contribution cannot be represented by a nonnegative durable base.");
            values[CharacterStat.Health] = refill;
            values[CharacterStat.PercentRemainingHealth] = 100;
        }
        if (nanoKnown)
        {
            values[CharacterStat.MaxNanoEnergy] = nano;
            int refill = checked(equipment.GetOrZero(CharacterStat.MaxNanoEnergy) - equipment.GetOrZero(CharacterStat.CurrentNano, StatDetail.Bonus));
            if (refill < 0) throw new InvalidOperationException("Prospective nano contribution cannot be represented by a nonnegative durable base.");
            values[CharacterStat.CurrentNano] = refill;
            values[CharacterStat.PercentRemainingNano] = 100;
        }
    }

    public void PublishAfterCommit(Player player)
    {
        if (player.Identity != _owner) throw new InvalidOperationException("XP projection owner changed.");
        // Once, after the commit. Anything the goal no longer takes (it finished or changed meanwhile) is not refunded
        // as XP: the committed XP stands as planned.
        if (ResearchXp > 0 && !_researchPublished)
        {
            _researchPublished = true;
            player.ApplyResearchXp(ResearchXp);
        }
        if (PooledXp > 0 && !_pooledPublished)
        {
            _pooledPublished = true;
            player.AddToDeathXpPool(PooledXp);
        }
        if (_stats.Count == 0) return;
        foreach (var pair in _stats) player.Stats.Set(pair.Key, pair.Value, StatDetail.Base, dirty: _questStyle);
        if (_questStyle)
        {
            if (LevelAfter > LevelBefore)
            {
                player.Stats.MarkDirty(CharacterStat.IP);
                player.ApplyLevelVitals();
                var data = player.Playfield!.GetRequiredService<ZoneEngine_New.Core.GameData.IGameData>();
                for (int level = LevelBefore + 1; level <= LevelAfter; level++)
                {
                    data.TryGetXpLevel(level, out var entry);
                    player.Session?.Send(new NewLevelMessage
                    {
                        Identity = player.Identity, Level = level, Ip = Math.Max(0, player.AvailableIp),
                        Xp = ExperienceAfter, LastSaveXp = entry.FloorXp,
                        NextLevelXp = entry.NextLevelXp > 0 ? checked(entry.FloorXp + entry.NextLevelXp) : 0,
                        Unknown2 = 4, LastXp = RequestedReward
                    });
                }
            }
            player.FlushDirtyStats();
            return;
        }
        if (LevelAfter > LevelBefore)
        {
            player.Rebase();
            SendStat(player, CharacterStat.MaxHealth, player.Stats.GetOrZero(CharacterStat.MaxHealth, StatDetail.Base), 0);
            SendStat(player, CharacterStat.MaxNanoEnergy, player.Stats.GetOrZero(CharacterStat.MaxNanoEnergy, StatDetail.Base), 0);
            SendStat(player, CharacterStat.CurrentNano, player.Stats.GetOrZero(CharacterStat.CurrentNano, StatDetail.Base), 0);
            for (int level = LevelBefore + 1; level <= LevelAfter; level++)
                player.Session?.Send(new NewLevelMessage
                {
                    Identity = player.Identity, Unknown = 0, Level = level, Ip = Math.Max(0, player.AvailableIp),
                    Xp = ExperienceAfter, LastSaveXp = checked((int)XpFloor(level)),
                    NextLevelXp = level >= 200 ? 0 : checked((int)XpFloor(level + 1)), Unknown1 = 0, Unknown2 = 4,
                    LastXp = Math.Max(0, player.Stats.GetOrZero(CharacterStat.LastXP))
                });
            SendStat(player, CharacterStat.LastSaveXP, checked((int)XpFloor(LevelAfter)), 1);
            SendStat(player, CharacterStat.SocialStatus, 0, 1);
            SendStat(player, CharacterStat.XP, ExperienceAfter, 0);
            ClientFeedback.Send(player, ClientFeedback.CanLootRemains);
        }
        else if (RequestedReward > 0 && !IsShadowKnowledge) SendStat(player, CharacterStat.XP, ExperienceAfter, 0);
        if (RequestedReward > 0 && IsShadowKnowledge)
        {
            SendStat(player, CharacterStat.SK, player.Stats.GetOrZero(CharacterStat.SK), 0);
            SendStat(player, CharacterStat.NextSK, checked((int)SkNext(LevelAfter)), 0);
        }
    }

    static void SendStat(Player player, CharacterStat stat, int value, byte unknown)
        => player.Session?.Send(new StatMessage { Identity = player.Identity, Unknown = unknown,
            Stats = [new() { Value1 = stat, Value2 = unchecked((uint)value) }] });
    static uint Raw(Player player, CharacterStat stat) => unchecked((uint)player.Stats.GetOrZero(stat, StatDetail.Base));
    static uint AddClamped(uint value, int amount) => value > uint.MaxValue - (uint)amount ? uint.MaxValue : value + (uint)amount;
    internal static uint XpFloor(int level) => level <= 1 ? 0 : (uint)XPTable.TableRKXP[Math.Min(level, 200) - 1, 1];
    internal static uint XpNext(int level) => level < 1 || level >= 200 ? 0 : (uint)XPTable.TableRKXP[level - 1, 2];
    internal static uint SkFloor(int level) => level < 200 ? 0 : (uint)XPTable.TableShadowLandsSK[Math.Min(level, 220) - 200, 1];
    internal static uint SkNext(int level) => level < 200 || level >= 220 ? 0 : (uint)XPTable.TableShadowLandsSK[level - 200, 2];
}
