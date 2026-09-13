namespace ZoneEngine_New.Core.Mobs
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;

    /// <summary>
    /// JSON shape of one overlay entry in GameData/NpcStatTemplateOverlays.json. Same curve layout as
    /// family templates; sampled on top of the family curves at spawn.
    /// </summary>
    public sealed class NpcStatTemplateData
    {
        public string Name { get; set; } = string.Empty;

        public Dictionary<int, Dictionary<int, int>> StatCurves { get; set; } = new();
    }

    /// <summary>Compiled optional NPC stat overlay: every scaling stat with its own level curve.</summary>
    public sealed class NpcStatTemplate
    {
        public NpcStatTemplate(int id, string name, Dictionary<int, NpcStatCurve> curves)
        {
            Id = id;
            Name = name;
            Curves = curves;
        }

        public int Id { get; }

        public string Name { get; }

        public IReadOnlyDictionary<int, NpcStatCurve> Curves { get; }
    }

    /// <summary>
    /// Overlay id to compiled curves. Built once at load; <see cref="Build"/> reports malformed
    /// entries through <paramref name="onError"/> and skips them.
    /// </summary>
    public sealed class NpcStatTemplateCatalog
    {
        private static readonly NpcStatTemplateCatalog EmptyCatalog =
            new(new Dictionary<int, NpcStatTemplate>());

        private readonly Dictionary<int, NpcStatTemplate> _templates;

        private NpcStatTemplateCatalog(Dictionary<int, NpcStatTemplate> templates)
        {
            _templates = templates;
        }

        public static NpcStatTemplateCatalog Empty => EmptyCatalog;

        public int Count => _templates.Count;

        public static NpcStatTemplateCatalog Build(
            Dictionary<int, NpcStatTemplateData>? source,
            Action<string>? onError = null)
        {
            if (source == null || source.Count == 0)
                return EmptyCatalog;

            Dictionary<int, NpcStatTemplate> templates = new(source.Count);
            foreach (KeyValuePair<int, NpcStatTemplateData> entry in source)
            {
                if (entry.Key <= 0)
                {
                    Report(onError, "NPC stat template id {0} is not positive; skipped", entry.Key);
                    continue;
                }

                NpcStatTemplateData data = entry.Value;
                if (data?.StatCurves == null || data.StatCurves.Count == 0)
                {
                    Report(onError, "NPC stat template {0} has no StatCurves; skipped", entry.Key);
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
                            "NPC stat template {0} stat {1} has no keypoints; stat skipped",
                            entry.Key,
                            curve.Key);
                        continue;
                    }

                    curves[curve.Key] = compiled;
                }

                if (curves.Count == 0)
                {
                    Report(
                        onError,
                        "NPC stat template {0} resolved to zero usable curves; skipped",
                        entry.Key);
                    continue;
                }

                templates[entry.Key] = new NpcStatTemplate(
                    entry.Key,
                    data.Name ?? string.Empty,
                    curves);
            }

            return templates.Count == 0 ? EmptyCatalog : new NpcStatTemplateCatalog(templates);
        }

        public bool TryGet(int id, out NpcStatTemplate template)
        {
            if (id <= 0)
            {
                template = null!;
                return false;
            }

            return _templates.TryGetValue(id, out template!);
        }

        /// <summary>
        /// Reports overlays whose curves do not span a template's spawn range. Stats still resolve
        /// (the curve clamps), but the mob will not scale across the whole range.
        /// </summary>
        public void ValidateCoverage(
            int id,
            int minLevel,
            int maxLevel,
            string templateHash,
            Action<string> onWarning)
        {
            ArgumentNullException.ThrowIfNull(onWarning);

            if (minLevel <= 0 || maxLevel < minLevel)
                return;
            if (!TryGet(id, out NpcStatTemplate template))
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
                        "Mob template '{0}' spawns at levels {1}-{2} but NpcStatTemplate {3} stat {4} only covers {5}-{6}; values clamp outside that range",
                        templateHash,
                        minLevel,
                        maxLevel,
                        id,
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
