namespace ZoneEngine_New.Core.GameData;

using System;
using System.Collections.Generic;

using ZoneEngine_New.Core.Mobs;
using ZoneEngine_New.Core.NpcEditor;

public sealed partial class NpcTemplateCatalog
{
    internal NpcEditorStatsJournal EditorStats { get; private init; } = NpcEditorStatsJournal.Empty;

    internal NpcTemplateCatalog WithEditorStats(NpcEditorStatsJournal journal)
        => new(_families, _leaves, _random, _spawnAll)
        {
            RuleFeatures = RuleFeatures,
            EditorLoot = EditorLoot,
            EditorEquipment = EditorEquipment,
            EditorStats = journal
        };

    internal bool TryGetStatsBand(string hash, int bandIndex, out NpcLeaf leaf)
    {
        if (TryGetEditableLootLeaf(hash, out leaf) && bandIndex >= 0 && bandIndex < leaf.Bands.Length)
            return true;
        leaf = null!;
        return false;
    }

    internal static Dictionary<int, int>? AuthoredStatsForBand(NpcLeaf leaf, int bandIndex, int level)
    {
        SelectBands(leaf.Bands, level, out NpcLevelBand low, out NpcLevelBand high, out double fraction);
        return ReferenceEquals(fraction <= 0.5 ? low : high, leaf.Bands[bandIndex])
            ? InterpolateStats(low, high, fraction)
            : null;
    }

    void ApplyEditorStats(MobTemplate template, NpcLeaf leaf)
    {
        if (EditorStats.Bands.Count == 0 || template.AuthoredBandIndex < 0)
            return;
        NpcEditorStatsHistory? history = EditorStats.Find(leaf.Hash, template.AuthoredBandIndex);
        if (history == null || history.Signature != leaf.EditorLootTemplateSignature || history.Revisions[^1].Values.Count == 0)
            return;

        var sources = new Dictionary<int, string>();
        if (NpcEditorStatPolicy.TryApplyConfigured(template.Stats, history.Revisions[^1].Values, sources, out _))
            template.EditorStatSources = sources;
    }
}
