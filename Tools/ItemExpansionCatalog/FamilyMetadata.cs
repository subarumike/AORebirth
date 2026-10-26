namespace AORebirth.Tools.ItemExpansionCatalog;

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;

using ZoneEngine_New.Core.GameData;
using ZoneEngine_New.Core.Inventory.Dat;

public sealed record FamilyQualityEvidence(
    string FamilyHash,
    int? MinimumQuality,
    int? MaximumQuality,
    int[] TemplateIds,
    string Status,
    string SourceField);

public sealed class FamilyMetadata
{
    readonly Dictionary<int, FamilyQualityEvidence[]> _byTemplate;

    FamilyMetadata(Dictionary<int, FamilyQualityEvidence[]> byTemplate, int familyCount,
        int rangeFamilyCount, int[] missingTemplateIds, string[] warnings)
    {
        _byTemplate = byTemplate;
        FamilyCount = familyCount;
        RangeFamilyCount = rangeFamilyCount;
        MissingTemplateIds = missingTemplateIds;
        Warnings = warnings;
    }

    public int FamilyCount { get; }

    public int RangeFamilyCount { get; }

    public int LinkedTemplateCount => _byTemplate.Count - MissingTemplateIds.Length;

    public int ReferencedTemplateCount => _byTemplate.Count;

    public int[] MissingTemplateIds { get; }

    public string[] Warnings { get; }

    public FamilyQualityEvidence[] ForTemplate(int id)
        => _byTemplate.TryGetValue(id, out FamilyQualityEvidence[]? evidence) ? evidence : [];

    public static FamilyMetadata Build(IReadOnlyList<DatItemTemplate> templates, string? familyJson)
    {
        ArgumentNullException.ThrowIfNull(templates);
        if (string.IsNullOrWhiteSpace(familyJson))
            return new FamilyMetadata(new(), 0, 0, [],
                ["ItemTemplates.json is unavailable or empty; no family-supported QL ranges are asserted."]);

        var qualitiesById = templates.GroupBy(template => template.ID)
            .ToDictionary(group => group.Key, group => group.Select(template => template.Quality).Distinct().Order().ToArray());
        HashInstance[] families = HashItemCatalog.Parse(familyJson).Instances
            .OrderBy(family => family.Hash, StringComparer.Ordinal).ToArray();
        var byTemplate = new Dictionary<int, List<FamilyQualityEvidence>>();
        var missingIds = new SortedSet<int>();
        var diagnosticCounts = new SortedDictionary<string, int>(StringComparer.Ordinal);
        int rangeFamilyCount = 0;

        foreach (HashInstance family in families)
        {
            int[] ids = family.TemplateIds.Distinct().Order().ToArray();
            var reasons = new List<string>();
            bool missing = false;
            bool ambiguousQuality = false;
            bool invalidQuality = false;
            var endpointQualities = new List<int>();
            foreach (int id in ids)
            {
                if (!qualitiesById.TryGetValue(id, out int[]? qualities))
                {
                    missingIds.Add(id);
                    missing = true;
                    continue;
                }
                if (qualities.Length != 1)
                {
                    ambiguousQuality = true;
                    continue;
                }
                if (qualities[0] < 1)
                    invalidQuality = true;
                endpointQualities.Add(qualities[0]);
            }

            if (missing)
                reasons.Add("MissingTemplateReferences");
            if (ambiguousQuality)
                reasons.Add("AmbiguousTemplateQualities");
            if (invalidQuality)
                reasons.Add("InvalidStoredQuality");
            if (endpointQualities.Distinct().Count() != endpointQualities.Count)
                reasons.Add("SameQualityVariants");

            int? minimum = null;
            int? maximum = null;
            string status;
            if (reasons.Count > 0)
            {
                status = string.Join(";", reasons);
                foreach (string reason in reasons)
                    diagnosticCounts[reason] = diagnosticCounts.GetValueOrDefault(reason) + 1;
            }
            else if (ids.Length < 2)
            {
                status = "ExplicitSingleTemplateLink";
            }
            else
            {
                minimum = endpointQualities.Min();
                maximum = endpointQualities.Max();
                status = "ExplicitInterpolationFamily";
                rangeFamilyCount++;
            }

            var evidence = new FamilyQualityEvidence(family.Hash, minimum, maximum, ids, status,
                "ItemTemplates.json[" + JsonSerializer.Serialize(family.Hash) + "].Templates; items.dat.Quality");
            foreach (int id in ids)
            {
                if (!byTemplate.TryGetValue(id, out List<FamilyQualityEvidence>? memberships))
                {
                    memberships = [];
                    byTemplate.Add(id, memberships);
                }
                memberships.Add(evidence);
            }
        }

        var warnings = diagnosticCounts.Select(pair => pair.Key + ": "
            + pair.Value.ToString(CultureInfo.InvariantCulture) + " explicit families; QL ranges remain null.").ToList();
        if (families.Length == 0)
            warnings.Add("ItemTemplates.json contains no parsed leaf families; no family-supported QL ranges are asserted.");
        if (missingIds.Count > 0)
            warnings.Add("Explicit families reference " + missingIds.Count.ToString(CultureInfo.InvariantCulture)
                + " template IDs absent from items.dat; missing identities are retained in family evidence only.");

        return new FamilyMetadata(byTemplate.ToDictionary(pair => pair.Key, pair => pair.Value.ToArray()),
            families.Length, rangeFamilyCount, missingIds.ToArray(), warnings.ToArray());
    }
}
