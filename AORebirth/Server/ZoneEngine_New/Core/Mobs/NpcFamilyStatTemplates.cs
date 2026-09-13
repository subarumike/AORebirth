namespace ZoneEngine_New.Core.Mobs
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;

    /// <summary>
    /// JSON shape of one family entry in GameData/NpcFamilyStatTemplates.json.
    /// Referenced by MobTemplate.NpcFamily. <see cref="StatCurves"/> is keyed by stat id, then by level.
    /// </summary>
    public sealed class NpcFamilyStatTemplateData
    {
        public string Name { get; set; } = string.Empty;

        public Dictionary<int, Dictionary<int, int>> StatCurves { get; set; } = new();
    }

    /// <summary>
    /// One stat's level keypoints, sorted ascending and sampled by linear interpolation.
    /// Levels below the first or above the last keypoint clamp rather than extrapolate.
    /// </summary>
    public sealed class NpcStatCurve
    {
        private readonly int[] _levels;
        private readonly int[] _values;

        private NpcStatCurve(int[] levels, int[] values)
        {
            _levels = levels;
            _values = values;
        }

        public int KeypointCount => _levels.Length;

        public int FirstLevel => _levels[0];

        public int LastLevel => _levels[_levels.Length - 1];

        /// <summary>Returns null when <paramref name="points"/> has no usable keypoints.</summary>
        public static NpcStatCurve? Create(Dictionary<int, int>? points)
        {
            if (points == null || points.Count == 0)
                return null;

            int[] levels = new int[points.Count];
            points.Keys.CopyTo(levels, 0);
            Array.Sort(levels);

            int[] values = new int[levels.Length];
            for (int i = 0; i < levels.Length; i++)
                values[i] = points[levels[i]];

            return new NpcStatCurve(levels, values);
        }

        public int Sample(int level)
        {
            if (level <= _levels[0])
                return _values[0];

            int last = _levels.Length - 1;
            if (level >= _levels[last])
                return _values[last];

            int high = 1;
            while (_levels[high] < level)
                high++;

            int lowLevel = _levels[high - 1];
            int span = _levels[high] - lowLevel;
            if (span <= 0)
                return _values[high];

            int lowValue = _values[high - 1];
            double t = (double)(level - lowLevel) / span;
            double sampled = lowValue + (((long)_values[high] - lowValue) * t);
            int result = (int)Math.Round(sampled, MidpointRounding.AwayFromZero);
            return result < 0 ? 0 : result;
        }
    }

    /// <summary>Compiled family: every scaling stat with its own level curve.</summary>
    public sealed class NpcFamilyStatTemplate
    {
        public NpcFamilyStatTemplate(int family, string name, Dictionary<int, NpcStatCurve> curves)
        {
            Family = family;
            Name = name;
            Curves = curves;
        }

        public int Family { get; }

        public string Name { get; }

        public IReadOnlyDictionary<int, NpcStatCurve> Curves { get; }
    }

    /// <summary>
    /// Family id to compiled curves. Built once at load; <see cref="Build"/> reports malformed
    /// families through <paramref name="onError"/> and skips them rather than spawning bad stats.
    /// </summary>
    public sealed class NpcFamilyStatCatalog
    {
        private static readonly NpcFamilyStatCatalog EmptyCatalog =
            new(new Dictionary<int, NpcFamilyStatTemplate>());

        private readonly Dictionary<int, NpcFamilyStatTemplate> _families;

        private NpcFamilyStatCatalog(Dictionary<int, NpcFamilyStatTemplate> families)
        {
            _families = families;
        }

        public static NpcFamilyStatCatalog Empty => EmptyCatalog;

        public int Count => _families.Count;

        public static NpcFamilyStatCatalog Build(
            Dictionary<int, NpcFamilyStatTemplateData>? source,
            Action<string>? onError = null)
        {
            if (source == null || source.Count == 0)
                return EmptyCatalog;

            Dictionary<int, NpcFamilyStatTemplate> families = new(source.Count);
            foreach (KeyValuePair<int, NpcFamilyStatTemplateData> entry in source)
            {
                if (entry.Key < 0)
                {
                    Report(onError, "NPC family id {0} is negative; skipped", entry.Key);
                    continue;
                }

                NpcFamilyStatTemplateData data = entry.Value;
                if (data?.StatCurves == null || data.StatCurves.Count == 0)
                {
                    Report(onError, "NPC family {0} has no StatCurves; skipped", entry.Key);
                    continue;
                }

                Dictionary<int, NpcStatCurve> curves = new(data.StatCurves.Count);
                foreach (KeyValuePair<int, Dictionary<int, int>> curve in data.StatCurves)
                {
                    NpcStatCurve? compiled = NpcStatCurve.Create(curve.Value);
                    if (compiled == null)
                    {
                        Report(
                            onError,
                            "NPC family {0} stat {1} has no keypoints; stat skipped",
                            entry.Key,
                            curve.Key);
                        continue;
                    }

                    curves[curve.Key] = compiled;
                }

                if (curves.Count == 0)
                {
                    Report(onError, "NPC family {0} resolved to zero usable curves; skipped", entry.Key);
                    continue;
                }

                families[entry.Key] = new NpcFamilyStatTemplate(
                    entry.Key,
                    data.Name ?? string.Empty,
                    curves);
            }

            return families.Count == 0 ? EmptyCatalog : new NpcFamilyStatCatalog(families);
        }

        public bool TryGet(int family, out NpcFamilyStatTemplate template)
        {
            if (family < 0)
            {
                template = null!;
                return false;
            }

            return _families.TryGetValue(family, out template!);
        }

        /// <summary>
        /// Looks up <paramref name="family"/>; if missing, falls back to
        /// <see cref="MobTemplate.DefaultNpcFamilyId"/>.
        /// </summary>
        public bool TryResolve(int family, out NpcFamilyStatTemplate template)
        {
            if (TryGet(family, out template))
                return true;

            int fallback = MobTemplate.DefaultNpcFamilyId;
            if (family == fallback)
                return false;

            return TryGet(fallback, out template);
        }

        /// <summary>
        /// Reports families whose curves do not span a template's spawn range. Stats still resolve
        /// (the curve clamps), but the mob will not scale across the whole range.
        /// </summary>
        public void ValidateCoverage(
            int family,
            int minLevel,
            int maxLevel,
            string templateHash,
            Action<string> onWarning)
        {
            ArgumentNullException.ThrowIfNull(onWarning);

            if (minLevel <= 0 || maxLevel < minLevel)
                return;
            if (!TryGet(family, out NpcFamilyStatTemplate template))
                return;

            foreach (KeyValuePair<int, NpcStatCurve> curve in template.Curves)
            {
                if (curve.Value.KeypointCount < 2)
                    continue;
                if (curve.Value.FirstLevel <= minLevel && curve.Value.LastLevel >= maxLevel)
                    continue;

                onWarning(
                    string.Format(
                        CultureInfo.InvariantCulture,
                        "Mob template '{0}' spawns at levels {1}-{2} but family {3} stat {4} only covers {5}-{6}; values clamp outside that range",
                        templateHash,
                        minLevel,
                        maxLevel,
                        family,
                        curve.Key,
                        curve.Value.FirstLevel,
                        curve.Value.LastLevel));
            }
        }

        static void Report(Action<string>? onError, string format, params object[] args)
        {
            onError?.Invoke(string.Format(CultureInfo.InvariantCulture, format, args));
        }
    }
}
