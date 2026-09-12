namespace ZoneEngine_New.Core.Missions;

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using AORebirth.Stats.SpecialStats;
using SmokeLounge.AOtomation.Messaging.GameData;
using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;
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
    public IReadOnlyDictionary<CharacterStat, int> Stats { get; }
    public int LevelBefore { get; }
    public int LevelAfter { get; }
    public int ExperienceAfter { get; }
    public int RequestedReward { get; }
    public bool IsShadowKnowledge { get; }

    DirectXpRewardPlan(Player player, int reward, int before, int after, bool shadow, Dictionary<CharacterStat, int> stats)
    {
        _owner = player.Identity; RequestedReward = reward; LevelBefore = before; LevelAfter = after;
        IsShadowKnowledge = shadow; _stats = stats; Stats = new ReadOnlyDictionary<CharacterStat, int>(stats);
        ExperienceAfter = stats.TryGetValue(CharacterStat.XP, out var xp) ? xp : player.Stats.GetOrZero(CharacterStat.XP, StatDetail.Base);
    }

    public static DirectXpRewardPlan Create(Player player, int reward)
        => CreateCore(player, reward, false);

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
        bool shadow = before >= 200;
        if (shadow && persistedTotal) throw new InvalidOperationException("Persisted DOJA reward normalization is an RK progression path.");
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
            values[CharacterStat.IP] = checked(player.Stats.GetOrZero(CharacterStat.IP, StatDetail.Base)
                + Character.TotalIpEarnedAtLevel(level) - Character.TotalIpEarnedAtLevel(before));
            PlanRefill(player, values);
        }
        return new(player, reward, before, level, shadow, values);
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
        bool healthKnown = MaxHealthCalculator.TryCompute(equipment, out int health);
        bool nanoKnown = MaxNanoCalculator.TryCompute(equipment, out int nano);
        if (healthKnown) equipment.Set(CharacterStat.MaxHealth, health);
        if (nanoKnown) equipment.Set(CharacterStat.MaxNanoEnergy, nano);
        player.NanoRuntime?.ProjectBonusesAfterRebase(player, equipment);
        if (healthKnown)
        {
            values[CharacterStat.MaxHealth] = health;
            int refill = checked(equipment.GetOrZero(CharacterStat.MaxHealth) - equipment.GetOrZero(CharacterStat.Health, StatDetail.Bonus));
            if (refill < 0) throw new InvalidOperationException("Prospective health contribution cannot be represented by a nonnegative durable base.");
            values[CharacterStat.Health] = refill;
        }
        if (nanoKnown)
        {
            values[CharacterStat.MaxNanoEnergy] = nano;
            int refill = checked(equipment.GetOrZero(CharacterStat.MaxNanoEnergy) - equipment.GetOrZero(CharacterStat.CurrentNano, StatDetail.Bonus));
            if (refill < 0) throw new InvalidOperationException("Prospective nano contribution cannot be represented by a nonnegative durable base.");
            values[CharacterStat.CurrentNano] = refill;
        }
    }

    public void PublishAfterCommit(Player player)
    {
        if (player.Identity != _owner) throw new InvalidOperationException("XP projection owner changed.");
        if (_stats.Count == 0) return;
        foreach (var pair in _stats) player.Stats.Set(pair.Key, pair.Value, StatDetail.Base, dirty: false);
        if (LevelAfter > LevelBefore)
        {
            player.Rebase();
            SendStat(player, CharacterStat.MaxHealth, player.Stats.GetOrZero(CharacterStat.MaxHealth), 0);
            SendStat(player, CharacterStat.MaxNanoEnergy, player.Stats.GetOrZero(CharacterStat.MaxNanoEnergy), 0);
            SendStat(player, CharacterStat.CurrentNano, player.Stats.GetOrZero(CharacterStat.CurrentNano), 0);
            for (int level = LevelBefore + 1; level <= LevelAfter; level++)
                player.Session?.Send(new NewLevelMessage
                {
                    Identity = player.Identity, Unknown = 0, Level = level, Ip = player.Stats.GetOrZero(CharacterStat.IP),
                    Xp = ExperienceAfter, LastSaveXp = checked((int)XpFloor(level)),
                    NextLevelXp = level >= 200 ? 0 : checked((int)XpFloor(level + 1)), Unknown1 = 0, Unknown2 = 4,
                    LastXp = Math.Max(0, player.Stats.GetOrZero(CharacterStat.LastXP))
                });
            SendStat(player, CharacterStat.LastSaveXP, checked((int)XpFloor(LevelAfter)), 1);
            SendStat(player, CharacterStat.SocialStatus, 0, 1);
            SendStat(player, CharacterStat.XP, ExperienceAfter, 0);
            player.Session?.Send(new FeedbackMessage { Identity = player.Identity, Unknown = 1, Unknown1 = 0,
                CategoryId = 110, MessageId = 249817907 });
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
