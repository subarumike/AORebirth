namespace ZoneEngine_New.Core.GameData;

using System.Collections.Generic;

using SmokeLounge.AOtomation.Messaging.GameData;
using ZoneEngine_New.Core.Mobs;

public sealed partial class GameDataStore
{
    internal NpcTemplateCatalog EditorLootCatalog => _npcTemplates;
}

public sealed partial class NpcTemplateCatalog
{
    internal IEnumerable<NpcLeaf> EditorLootLeaves => _leaves.Values;

    internal HashSet<NpcFeature> EditorLootFeatures(NpcLeaf leaf, NpcLevelBand band)
    {
        int? flags = band.CharacterFlags ?? band.Stats?.GetValueOrDefault((int)CharacterStat.Flags);
        return ResolveFeatures(RuleFeatures?.Invoke(leaf.Hash), flags, leaf.Hash == MobTemplate.FallbackHash);
    }

    internal IReadOnlyList<MobItemTableEntry> EditorLootBandRows(NpcLeaf leaf, int index)
    {
        NpcLevelBand band = leaf.Bands[index];
        return EditorLoot.Templates.TryGetValue(leaf.Hash, out NpcEditorLootHistory? history)
            ? NpcEditorLootJournal.MaterializeBand(band, index, history.Revisions[^1])
            : band.LootTable;
    }
}
