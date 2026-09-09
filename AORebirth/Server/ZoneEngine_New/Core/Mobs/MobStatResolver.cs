namespace ZoneEngine_New.Core.Mobs
{
    using System;
    using System.Collections.Generic;

    using SmokeLounge.AOtomation.Messaging.GameData;

    /// <summary>
    /// Resolves a mob's stat snapshot from root <see cref="MobTemplate.Stats"/> plus optional
    /// <see cref="MobTemplate.StatBands"/>. One or zero bands is a constant overlay; two or more
    /// lerp between the surrounding keypoints and extrapolate past the first/last band.
    /// </summary>
    public static class MobStatResolver
    {
        const int LevelStat = (int)CharacterStat.Level;

        public static Dictionary<int, int> Resolve(MobTemplate template, int? level)
        {
            ArgumentNullException.ThrowIfNull(template);

            Dictionary<int, int> result = Copy(template.Stats);
            List<MobStatBand> bands = CollectBands(template.StatBands);
            if (bands.Count == 0)
            {
                ApplySpawnLevel(result, level);
                return result;
            }

            if (bands.Count == 1)
            {
                Overlay(result, bands[0].Stats);
                ApplySpawnLevel(result, level);
                return result;
            }

            int resolvedLevel = level ?? DefaultLevel(template, bands);
            OverlayAtLevel(result, bands, resolvedLevel);
            result[LevelStat] = resolvedLevel;
            return result;
        }

        static List<MobStatBand> CollectBands(List<MobStatBand>? bands)
        {
            List<MobStatBand> usable = new();
            if (bands == null)
                return usable;

            for (int i = 0; i < bands.Count; i++)
            {
                MobStatBand? band = bands[i];
                if (band?.Stats == null || band.Stats.Count == 0)
                    continue;
                usable.Add(band);
            }

            usable.Sort(static (a, b) => a.Level.CompareTo(b.Level));

            int write = 0;
            for (int read = 0; read < usable.Count; read++)
            {
                if (write > 0 && usable[read].Level == usable[write - 1].Level)
                {
                    usable[write - 1] = usable[read];
                    continue;
                }

                usable[write++] = usable[read];
            }

            if (write < usable.Count)
                usable.RemoveRange(write, usable.Count - write);

            return usable;
        }

        static int DefaultLevel(MobTemplate template, List<MobStatBand> bands)
        {
            if (template.MinLevel > 0)
                return template.MinLevel;
            return bands[0].Level;
        }

        static void OverlayAtLevel(Dictionary<int, int> result, List<MobStatBand> bands, int level)
        {
            MobStatBand first = bands[0];
            MobStatBand second = bands[1];
            if (level <= first.Level)
            {
                OverlaySegment(result, first, second, level);
                return;
            }

            MobStatBand last = bands[bands.Count - 1];
            MobStatBand previous = bands[bands.Count - 2];
            if (level >= last.Level)
            {
                OverlaySegment(result, previous, last, level);
                return;
            }

            int highIndex = 1;
            while (highIndex < bands.Count && bands[highIndex].Level < level)
                highIndex++;

            OverlaySegment(result, bands[highIndex - 1], bands[highIndex], level);
        }

        static void OverlaySegment(Dictionary<int, int> result, MobStatBand low, MobStatBand high, int level)
        {
            if (high.Level <= low.Level + 1)
            {
                Overlay(result, level >= high.Level ? high.Stats : low.Stats);
                return;
            }

            OverlayLerp(result, low, high, level);
        }

        static void OverlayLerp(Dictionary<int, int> result, MobStatBand low, MobStatBand high, int level)
        {
            HashSet<int> keys = new(low.Stats.Keys);
            foreach (int key in high.Stats.Keys)
                keys.Add(key);

            foreach (int key in keys)
            {
                bool hasLow = low.Stats.TryGetValue(key, out int lowValue);
                bool hasHigh = high.Stats.TryGetValue(key, out int highValue);
                if (hasLow && hasHigh)
                {
                    result[key] = Lerp(lowValue, highValue, low.Level, high.Level, level);
                    continue;
                }

                result[key] = hasLow ? lowValue : highValue;
            }
        }

        static int Lerp(int lowValue, int highValue, int lowLevel, int highLevel, int level)
        {
            int span = highLevel - lowLevel;
            if (span <= 0)
                return lowValue;

            double t = (double)(level - lowLevel) / span;
            int value = (int)Math.Round(lowValue + ((long)highValue - lowValue) * t, MidpointRounding.AwayFromZero);
            return value < 0 ? 0 : value;
        }

        static void Overlay(Dictionary<int, int> result, Dictionary<int, int> overlay)
        {
            foreach (KeyValuePair<int, int> entry in overlay)
                result[entry.Key] = entry.Value;
        }

        static void ApplySpawnLevel(Dictionary<int, int> result, int? level)
        {
            if (level.HasValue)
                result[LevelStat] = level.Value;
        }

        static Dictionary<int, int> Copy(Dictionary<int, int>? source)
        {
            if (source == null || source.Count == 0)
                return new Dictionary<int, int>();

            return new Dictionary<int, int>(source);
        }
    }
}
