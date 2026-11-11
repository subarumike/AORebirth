namespace ZoneEngine_New.Core.GameData;

using ZoneEngine_New.Core.Mobs;

public sealed partial class NpcTemplateCatalog
{
    internal NpcEditorEquipmentJournal EditorEquipment { get; private init; } = NpcEditorEquipmentJournal.Empty;

    internal NpcTemplateCatalog WithEditorEquipment(NpcEditorEquipmentJournal journal)
        => new(_families, _leaves, _random, _spawnAll)
        {
            RuleFeatures = RuleFeatures,
            EditorLoot = EditorLoot,
            EditorEquipment = journal,
            EditorStats = EditorStats
        };

    internal bool TryGetEquipmentBand(string hash, int bandIndex, out NpcLevelBand band)
    {
        band = null!;
        if (hash == MobTemplate.FallbackHash || !TryGetLeaf(hash, out NpcLeaf leaf)
            || bandIndex < 0 || bandIndex >= leaf.Bands.Length)
            return false;
        band = leaf.Bands[bandIndex];
        return true;
    }

    void ApplyEditorEquipment(MobTemplate template, NpcLeaf leaf)
    {
        if (template.AuthoredBandIndex < 0 || EditorEquipment.Bands.Count == 0)
            return;
        NpcEditorEquipmentHistory? history = EditorEquipment.Find(leaf.Hash, template.AuthoredBandIndex);
        if (history == null
            || history.Signature != NpcEditorEquipmentJournalFile.Signature(leaf.Bands[template.AuthoredBandIndex]))
            return;
        template.EditorEquipmentEntries = history.Revisions[^1].Entries;
    }
}
