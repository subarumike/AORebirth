namespace ZoneEngine_New.Core.Nanos
{
    using System;
    using SmokeLounge.AOtomation.Messaging.GameData;
    using ZoneEngine_New.Core.Entities;
    using ZoneEngine_New.Core.Helpers;

    /// <summary>
    /// The two already implemented New maximum-resource formulas, reused for nano-owned
    /// BodyDevelopment/NanoPool contributions. Derived deltas stay in Bonus, not snapshot Base.
    /// This is not a replacement for the still-missing general Legacy skill-trickle graph.
    /// </summary>
    internal static class NanoDerivedStats
    {
        internal static int HealthDelta(Player player, int bodyDevelopmentDelta)
            => HealthDelta(player.Stats, bodyDevelopmentDelta);
        internal static int HealthDelta(StatCollection stats, int bodyDevelopmentDelta)
        {
            if (bodyDevelopmentDelta == 0 || stats.GetOrZero(CharacterStat.NPCFamily) > 0) return 0;
            int body = stats.GetOrZero(CharacterStat.BodyDevelopment);
            int before = MaxHealthCalculator.Compute(Value(stats, CharacterStat.Breed), Value(stats, CharacterStat.Profession),
                Value(stats, CharacterStat.TitleLevel), Value(stats, CharacterStat.Level), Math.Max(1, body));
            int after = MaxHealthCalculator.Compute(Value(stats, CharacterStat.Breed), Value(stats, CharacterStat.Profession),
                Value(stats, CharacterStat.TitleLevel), Value(stats, CharacterStat.Level), Math.Max(1, checked(body + bodyDevelopmentDelta)));
            return checked(after - before);
        }
        internal static int NanoDelta(Player player, int nanoPoolDelta)
            => NanoDelta(player.Stats, nanoPoolDelta);
        internal static int NanoDelta(StatCollection stats, int nanoPoolDelta)
        {
            if (nanoPoolDelta == 0 || stats.GetOrZero(CharacterStat.NPCFamily) > 0) return 0;
            int pool = stats.GetOrZero(CharacterStat.NanoPool);
            int before = MaxNanoCalculator.Compute(Value(stats, CharacterStat.Breed), Value(stats, CharacterStat.Profession),
                Value(stats, CharacterStat.TitleLevel), Value(stats, CharacterStat.Level), Math.Max(1, pool));
            int after = MaxNanoCalculator.Compute(Value(stats, CharacterStat.Breed), Value(stats, CharacterStat.Profession),
                Value(stats, CharacterStat.TitleLevel), Value(stats, CharacterStat.Level), Math.Max(1, checked(pool + nanoPoolDelta)));
            return checked(after - before);
        }
        private static int Value(StatCollection stats, CharacterStat stat) => Math.Max(1, stats.GetOrZero(stat));
    }
}
