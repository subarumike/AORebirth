namespace ZoneEngine_New.Core.Helpers
{
    using System;
    using AORebirth.Stats;
    using SmokeLounge.AOtomation.Messaging.GameData;
    using ZoneEngine_New.Core.Entities;

    internal static class VitalSkillTrickle
    {
        // The same table and double-addition order as StatSkill.Trickle. Persisted
        // skill rows are base values; equipment adds bonuses before this rebase.
        internal static int Effective(StatCollection stats, CharacterStat skill)
        {
            int row = (int)skill - 100;
            if (skill is not (CharacterStat.BodyDevelopment or CharacterStat.NanoPool)
                || SkillTrickleTable.table[row, 0] != (int)skill)
                throw new InvalidOperationException("Vital skill trickle identity changed.");
            int trickle = checked((int)Math.Floor((
                SkillTrickleTable.table[row, 1] * stats.GetOrZero(CharacterStat.Strength)
                + SkillTrickleTable.table[row, 3] * stats.GetOrZero(CharacterStat.Stamina)
                + SkillTrickleTable.table[row, 5] * stats.GetOrZero(CharacterStat.Sense)
                + SkillTrickleTable.table[row, 2] * stats.GetOrZero(CharacterStat.Agility)
                + SkillTrickleTable.table[row, 4] * stats.GetOrZero(CharacterStat.Intelligence)
                + SkillTrickleTable.table[row, 6] * stats.GetOrZero(CharacterStat.Psychic)) / 4));
            return Math.Max(1, checked(stats.GetOrZero(skill) + trickle));
        }
    }
}
