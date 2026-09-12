namespace ZoneEngine_New.Core.Mobs
{
    using System;
    using System.Collections.Generic;

    using SmokeLounge.AOtomation.Messaging.GameData;

    /// <summary>
    /// Resolves a mob's stat snapshot in three layers: the family curve sampled at the spawn level,
    /// then <see cref="MobTemplate.Stats"/> overlaid on top, then the level stat itself. Template
    /// stats always beat the family, which is how bosses and other one-offs deviate from a family.
    /// </summary>
    public static class MobStatResolver
    {
        const int LevelStat = (int)CharacterStat.Level;

        public static Dictionary<int, int> Resolve(
            MobTemplate template,
            int? level,
            NpcFamilyStatTemplate? family = null)
        {
            ArgumentNullException.ThrowIfNull(template);

            int resolvedLevel = ResolveLevel(template, level);
            Dictionary<int, int> result = new();

            if (family != null)
            {
                foreach (KeyValuePair<int, NpcStatCurve> curve in family.Curves)
                    result[curve.Key] = curve.Value.Sample(resolvedLevel);
            }

            foreach (KeyValuePair<int, int> entry in template.Stats)
                result[entry.Key] = entry.Value;

            result[LevelStat] = resolvedLevel;
            return result;
        }

        static int ResolveLevel(MobTemplate template, int? level)
        {
            if (level.HasValue && level.Value > 0)
                return level.Value;
            if (template.MinLevel > 0)
                return template.MinLevel;
            if (template.Stats.TryGetValue(LevelStat, out int templateLevel) && templateLevel > 0)
                return templateLevel;

            return 1;
        }
    }
}
