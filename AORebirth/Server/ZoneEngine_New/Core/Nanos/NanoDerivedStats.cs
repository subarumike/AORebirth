namespace ZoneEngine_New.Core.Nanos
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using SmokeLounge.AOtomation.Messaging.GameData;
    using ZoneEngine_New.Core.Entities;
    using ZoneEngine_New.Core.Helpers;

    /// <summary>
    /// Projects owned stat differences using the editable character rule data.
    /// Only nano-owned differences enter Bonus; baseline equipment/skill ownership is unchanged.
    /// </summary>
    internal static class NanoDerivedStats
    {
        internal static Dictionary<CharacterStat, int> Project(StatCollection current,
            IEnumerable<KeyValuePair<CharacterStat, int>> previousModifiers,
            IReadOnlyDictionary<CharacterStat, int> previousDerived,
            IEnumerable<KeyValuePair<CharacterStat, int>> nextModifiers)
        {
            var baseline = current.GetEntries().ToDictionary(e => e.Stat, e => checked(e.Base + e.Bonus));
            foreach (var modifier in previousModifiers) Add(baseline, modifier.Key, checked(-modifier.Value));
            foreach (var modifier in previousDerived) Add(baseline, modifier.Key, checked(-modifier.Value));
            var next = new Dictionary<CharacterStat, int>(baseline);
            foreach (var modifier in nextModifiers) Add(next, modifier.Key, modifier.Value);
            var result = new Dictionary<CharacterStat, int>();
            // These are the StatSkill IDs indexed by the accepted table. Trailing historical
            // non-skill rows are not additional skills and are not promoted by this adapter.
            for (int id = 100; id <= 167; id++)
            {
                var skill = (CharacterStat)id;
                int delta = checked(Trickle(next, skill) - Trickle(baseline, skill));
                if (delta == 0) continue;
                result[skill] = delta; Add(next, skill, delta);
            }
            if (baseline.GetValueOrDefault(CharacterStat.NPCFamily) <= 0)
            {
                SetResource(CharacterStat.MaxHealth, checked(MaxHealth(next) - MaxHealth(baseline)));
                SetResource(CharacterStat.MaxNanoEnergy, checked(MaxNano(next) - MaxNano(baseline)));
            }
            return result;
            void SetResource(CharacterStat stat, int delta) { if (delta != 0) result[stat] = delta; }
        }

        internal static int SkillDelta(StatCollection current, CharacterStat skill,
            IReadOnlyDictionary<CharacterStat, int> add, IReadOnlyDictionary<CharacterStat, int>? subtract)
        {
            var before = current.GetEntries().ToDictionary(e => e.Stat, e => checked(e.Base + e.Bonus));
            var after = new Dictionary<CharacterStat, int>(before);
            foreach (var modifier in add) Add(after, modifier.Key, modifier.Value);
            if (subtract != null) foreach (var modifier in subtract) Add(after, modifier.Key, checked(-modifier.Value));
            return checked(Trickle(after, skill) - Trickle(before, skill));
        }

        private static int Trickle(IReadOnlyDictionary<CharacterStat, int> stats, CharacterStat skill)
        => ZoneEngine_New.Core.GameData.CharacterRuleData.Current.SkillContribution(stats, skill);
        private static void Add(Dictionary<CharacterStat, int> values, CharacterStat stat, int amount)
            => values[stat] = checked(values.GetValueOrDefault(stat) + amount);
        private static int MaxHealth(IReadOnlyDictionary<CharacterStat, int> stats)
            => ZoneEngine_New.Core.GameData.CharacterRuleData.Current.ComputeVital("health", stats);
        private static int MaxNano(IReadOnlyDictionary<CharacterStat, int> stats)
            => ZoneEngine_New.Core.GameData.CharacterRuleData.Current.ComputeVital("nano", stats);
        private static int Positive(IReadOnlyDictionary<CharacterStat, int> stats, CharacterStat stat)
            => Math.Max(1, stats.GetValueOrDefault(stat));

        internal static int HealthDelta(Player player, int bodyDevelopmentDelta)
            => HealthDelta(player.Stats, bodyDevelopmentDelta);
        internal static int HealthDelta(StatCollection stats, int bodyDevelopmentDelta)
        {
            if (bodyDevelopmentDelta == 0 || stats.GetOrZero(CharacterStat.NPCFamily) > 0
                || stats.GetOrZero(CharacterStat.GmLevel) > 0) return 0;
            int body = stats.GetOrZero(CharacterStat.BodyDevelopment);
            int before = ZoneEngine_New.Core.GameData.CharacterRuleData.Current.ComputeVital("health", Value(stats, CharacterStat.Breed), Value(stats, CharacterStat.Profession),
                Value(stats, CharacterStat.TitleLevel), Value(stats, CharacterStat.Level), Math.Max(1, body));
            int after = ZoneEngine_New.Core.GameData.CharacterRuleData.Current.ComputeVital("health", Value(stats, CharacterStat.Breed), Value(stats, CharacterStat.Profession),
                Value(stats, CharacterStat.TitleLevel), Value(stats, CharacterStat.Level), Math.Max(1, checked(body + bodyDevelopmentDelta)));
            return checked(after - before);
        }
        internal static int NanoDelta(Player player, int nanoPoolDelta)
            => NanoDelta(player.Stats, nanoPoolDelta);
        internal static int NanoDelta(StatCollection stats, int nanoPoolDelta)
        {
            if (nanoPoolDelta == 0 || stats.GetOrZero(CharacterStat.NPCFamily) > 0) return 0;
            int pool = stats.GetOrZero(CharacterStat.NanoPool);
            int before = ZoneEngine_New.Core.GameData.CharacterRuleData.Current.ComputeVital("nano", Value(stats, CharacterStat.Breed), Value(stats, CharacterStat.Profession),
                Value(stats, CharacterStat.TitleLevel), Value(stats, CharacterStat.Level), Math.Max(1, pool));
            int after = ZoneEngine_New.Core.GameData.CharacterRuleData.Current.ComputeVital("nano", Value(stats, CharacterStat.Breed), Value(stats, CharacterStat.Profession),
                Value(stats, CharacterStat.TitleLevel), Value(stats, CharacterStat.Level), Math.Max(1, checked(pool + nanoPoolDelta)));
            return checked(after - before);
        }
        private static int Value(StatCollection stats, CharacterStat stat) => Math.Max(1, stats.GetOrZero(stat));
    }
}
