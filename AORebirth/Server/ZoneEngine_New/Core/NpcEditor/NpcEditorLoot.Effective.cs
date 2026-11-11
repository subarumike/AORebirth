namespace ZoneEngine_New.Core.NpcEditor;

using System.Collections.Generic;
using System.Linq;

using SmokeLounge.AOtomation.Messaging.GameData;

using ZoneEngine_New.Core.Entities;
using ZoneEngine_New.Core.GameData;

internal sealed record NamedLootCandidate(string QualifiedGroupId, string EntryId, int LowId, int HighId, string Name,
    int? MinQuality, int? MaxQuality, bool Eligible, string Reason, int Weight, double? ConditionalProbabilityPercent);
internal sealed record NpcEditorEffectiveLootView(IReadOnlyList<NpcEditorLootRow> NativeRows, NamedLootResolution Resolution,
    NamedLootCandidate[] Candidates);
internal sealed record LootComparisonInput(string TemplateHash, string Name, int? MonsterData, int Level, int PlayfieldId,
    IReadOnlyList<NpcEditorLootRow> ConfiguredRows, NamedLootCandidate[] Candidates);

public sealed partial class NpcEditorLoot
{
    internal NpcEditorEffectiveLootView DescribeEffective(NpcCharacter npc)
    {
        NpcEditorLootView native = Describe(npc);
        NamedLootResolution named = gameData.ResolveNamedLoot(npc, includeAuthoringContext: true);
        return new(native.Rows, named, DescribeCandidates(named));
    }

    internal LootComparisonInput CaptureComparison(NpcCharacter npc)
    {
        NpcEditorLootView native = Describe(npc);
        NamedLootResolution named = gameData.ResolveNamedLoot(npc);
        int monsterData = npc.Stats.GetOrZero(CharacterStat.MonsterData);
        return new(native.TemplateHash, npc.Name ?? "", monsterData > 0 ? monsterData : null,
            named.Context.Level, named.Context.PlayfieldId, native.ConfiguredRows, DescribeCandidates(named));
    }

    NamedLootCandidate[] DescribeCandidates(NamedLootResolution resolution)
    {
        var candidates = new List<NamedLootCandidate>();
        foreach (NamedLootResolvedGroup row in resolution.Groups)
        {
            NamedLootGroup group = row.Group;
            bool groupMatches = group.Condition?.Matches(resolution.Context) != false;
            var entries = group.Entries.Select(entry =>
            {
                int min = 0;
                int max = 0;
                bool eligible = groupMatches && minter != null
                    && NamedLootRoller.TryQualityWindow(entry, resolution.Context, minter, out min, out max);
                return (Entry: entry, Eligible: eligible, Min: min, Max: max);
            }).ToArray();
            long totalWeight = group.EmptyWeight + entries.Where(entry => entry.Eligible).Sum(entry => (long)entry.Entry.Weight);
            foreach (var rowEntry in entries)
            {
                NamedLootEntry entry = rowEntry.Entry;
                double? probability = rowEntry.Eligible ? group.Mode switch
                {
                    "INDEPENDENT" => entry.ChanceBasisPoints / 100.0,
                    "GUARANTEED_ALL" => 100,
                    "WEIGHTED_ONE" or "WEIGHTED_MANY" when totalWeight > 0 => entry.Weight * 100.0 / totalWeight,
                    _ => null
                } : null;
                string name = items.TryGet(entry.LowId, out var template) ? template.Name : "Unresolved item";
                string reason = rowEntry.Eligible ? "Eligible before capacity and unique checks."
                    : groupMatches ? "Entry conditions or RDB quality window exclude this item." : "The group condition does not match.";
                candidates.Add(new(row.QualifiedId, entry.Id, entry.LowId, entry.HighId, name,
                    rowEntry.Eligible ? rowEntry.Min : null, rowEntry.Eligible ? rowEntry.Max : null,
                    rowEntry.Eligible, reason, entry.Weight, probability));
            }
        }
        return [.. candidates];
    }
}
