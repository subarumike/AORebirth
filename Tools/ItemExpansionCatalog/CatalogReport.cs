using System.Globalization;
using System.Text;

namespace AORebirth.Tools.ItemExpansionCatalog;

internal static class CatalogReport
{
    internal static string Create(CatalogSummary summary, CatalogItem[] records)
    {
        var text = new StringBuilder();
        text.AppendLine("# Item Expansion Classification Report");
        text.AppendLine();
        text.AppendLine("This complete record inventory uses explicit positive item expansion requirements as " +
            "verified expansion associations, following Mike's classification rule. For example, an item's " +
            "User Expansion check requiring Lost Eden supports LostEden classification. A standalone account " +
            "entitlement value is not automatically an item classification. Missing or ambiguous metadata stays Unknown.");
        text.AppendLine();
        text.AppendLine("This is an offline classification inventory. It changes no runtime selection, drop chances, " +
            "missions, ownership, NPC tables or live data. It is not an item database replacement.");
        text.AppendLine();
        text.AppendLine("## Coverage");
        text.AppendLine();
        text.AppendLine("| Classification | Item count | Percentage |");
        text.AppendLine("| --- | ---: | ---: |");
        foreach ((string bucket, int count) in summary.ClassificationCounts)
            text.AppendLine($"| {Display(bucket)} | {count:N0} | {Percent(count, summary.TotalInputRecords)}% |");
        text.AppendLine($"| TOTAL | {summary.TotalInputRecords:N0} | 100% |");
        text.AppendLine();
        text.AppendLine("Every decoded record appears once. SourceRecordIndex distinguishes original definitions " +
            "even if an input repeats an identity/QL. Primary files sort by TemplateId, StoredQuality, then SourceRecordIndex. " +
            "No category, flags, name, QL or loot eligibility excludes a record.");
        text.AppendLine();
        text.AppendLine($"- Distinct template IDs: {summary.DistinctTemplateIds:N0}.");
        text.AppendLine($"- Repeated template/QL keys in input: {summary.RepeatedTemplateQualityKeys:N0}; retained without overwriting.");
        text.AppendLine($"- Records with an Rdb metadata object available: {summary.RecordsWithRdb:N0}.");
        text.AppendLine($"- Records with missing or empty stored name: {summary.RecordsWithMissingName:N0}; no name fabricated.");
        text.AppendLine($"- Records missing expansion/context metadata: {summary.MissingExpansionInformation:N0}.");
        text.AppendLine($"- Records with unsupported expansion entitlement values: {summary.UnsupportedExpansionValues:N0}.");
        text.AppendLine($"- Records with conflicting mapped/original expansion evidence: {summary.ConflictingExpansionEvidence:N0}.");
        text.AppendLine($"- Records with absent/incomplete RDB metadata: {summary.IncompleteRdbMetadata:N0}.");
        text.AppendLine();
        text.AppendLine("Diagnostic counts can overlap. Expansion/context metadata is broader than accepted classification evidence: " +
            "an observation may be an account-style value, a negative check, an alternative requirement or a playfield context. " +
            "Only accepted positive item requirements establish the associations used in the primary files.");
        text.AppendLine();
        text.AppendLine("| Primary UNKNOWN reason | Records |");
        text.AppendLine("| --- | ---: |");
        foreach (var group in records.Where(record => record.ExpansionClassification == "Unknown")
                     .GroupBy(record => record.ClassificationReason).OrderBy(group => group.Key, StringComparer.Ordinal))
            text.AppendLine($"| {group.Key} | {group.Count():N0} |");
        text.AppendLine();
        text.AppendLine("## Fields and classification rules");
        text.AppendLine();
        text.AppendLine("1. An explicit positive item User Expansion requirement establishes an expansion association under " +
            "Mike's accepted rule. Supported associations are Notum Wars, Shadowlands, Alien Invasion, Lost Eden, " +
            "Legacy of the Xan and PMV Obsidian Edition. One verified association selects its primary file; " +
            "multiple verified associations select MultipleExpansions.json and preserve every association in VerifiedExpansions. " +
            "This is requirement-backed association evidence, not a claim to reconstruct the item's original release date.");
        text.AppendLine("2. Preserve exact values of CharacterStat.Expansion (389) and ExpansionPlayfield (531) " +
            "from DatItemTemplate.Stats, Rdb.LiveStats, Rdb.Stats/Stats18/Stats2B, action requirements " +
            "and spell criteria/expressions. Each observation carries its source path, operator, value and decoded " +
            "entitlement bits. Source paths are relative to the original item definition in items.dat. " +
            "Full ordered requirement expressions remain available whenever they contain relevant terms; " +
            "packed dynamic expressions retain lossless MessagePack bytes.");
        text.AppendLine("3. Evaluate the complete ordered item action requirement expression rather than searching for a bit name. " +
            "Positive CharacterStat.Expansion BitAnd (operator 22), or EqualTo a nonzero known mask, in known item actions, including " +
            "ToUse, ToWear and ToWield, supply accepted association evidence. User/self selectors, action boundaries, " +
            "postfix And/Or/Not and legacy ChildOperator links affect the result. AND can combine an expansion check " +
            "with unrelated requirements. OR is accepted only when every possible branch supplies a positive known " +
            "expansion association: SL OR AI preserves both alternatives in MultipleExpansions, while SL OR a " +
            "non-expansion skill check remains Unknown. Negated expansion checks or malformed/unsupported expressions " +
            "cannot invent a positive association. BitAnd tests overlap; multi-bit operands preserve the named " +
            "association alternatives and their ANY semantics, not a claim that every bit is simultaneously required. " +
            "EqualTo preserves the exact known mask's associations and its equality semantics. " +
            "The complete expression and accepted source paths remain traceable.");
        text.AppendLine("4. ExpansionPlayfield531 describes location context. ItemFlags describes behavior such as " +
            "NoDrop/Unique/Special. ItemType, ItemClass, DynelType, descriptions and ItemTemplates family hashes " +
            "do not independently establish expansion association. Standalone Stats/Rdb.LiveStats expansion values " +
            "and spell/playfield context are retained without automatically becoming primary assignments. " +
            "Names, ID ranges, QL and appearance are never classification rules. Zero or missing expansion requirements " +
            "never become Rubi-Ka. Explicitly named preorder bits map to their named parent expansion association " +
            "while retaining the original preorder mask; Mail alone establishes no supported expansion association.");
        text.AppendLine("5. Raw original stat blocks are kept separate from class-default/live values. " +
            "A conflict means an incompatible mapped/original mirror, not simply different raw block entries or action conditions. " +
            "Unsupported values, conflicting evidence, incomplete reads and missing information remain visible with separate reasons. " +
            "An incomplete present RDB record is not accepted as complete evidence. A legacy record without an Rdb " +
            "object can still be classified from its explicit supported item action requirements. " +
            "When a supported classification cannot be established safely, the primary assignment remains Unknown. " +
            "ClassificationReason and ClassificationConfidence describe the actual assignment; Evidence.PrimaryReason " +
            "describes the raw metadata diagnostics and is not itself the assignment rule.");
        text.AppendLine();
        text.AppendLine("### Existing expansion and entitlement bit values");
        text.AppendLine();
        text.AppendLine("| Value | Existing name |");
        text.AppendLine("| ---: | --- |");
        foreach (AORebirth.Enums.Expansions flag in Enum.GetValues<AORebirth.Enums.Expansions>()
                     .Where(flag => flag != AORebirth.Enums.Expansions.All).OrderBy(flag => (int)flag))
            text.AppendLine($"| {(int)flag} | {flag} |");
        text.AppendLine();
        text.AppendLine("NotumWars and PMVObsidianEdition have separate primary output lists when supported item requirements " +
            "prove those associations. Preorder labels retain their exact values and map only to the expansion " +
            "explicitly named by that label. Mail is retained as entitlement evidence without an expansion assignment. " +
            "An empty list means no item was classified there " +
            "from the selected evidence, not that the expansion has no items.");
        text.AppendLine();
        text.AppendLine("### Observed raw expansion values");
        text.AppendLine();
        text.AppendLine("Counts below are distinct records with an observation; mirrored fields can report the same fact. " +
            "These are evidence diagnostics, not classified expansion totals.");
        text.AppendLine();
        text.AppendLine("| Stat | Raw value | Records |");
        text.AppendLine("| ---: | ---: | ---: |");
        foreach (var group in records.SelectMany(record => record.Evidence.Observations
                     .Select(observation => (observation.StatId, observation.Value)).Distinct())
                     .GroupBy(pair => pair).OrderBy(group => group.Key.StatId).ThenBy(group => group.Key.Value))
            text.AppendLine($"| {group.Key.StatId} | {group.Key.Value} | {group.Count():N0} |");
        text.AppendLine();
        text.AppendLine("## Quality and category preservation");
        text.AppendLine();
        text.AppendLine($"ItemTemplates.json contributes {summary.FamilyCount:N0} explicit leaf families, " +
            $"{summary.RangeFamilyCount:N0} valid interpolation ranges and {summary.FamilyLinkedTemplateCount:N0} linked input IDs. " +
            "Each original template and stored QL is retained separately. Minimum/maximum supported QL is provided " +
            "only for a complete explicit family with distinct valid endpoint qualities. " +
            "All original family memberships remain in QualityRangeEvidence; missing, singleton or same-QL links do not invent ranges. " +
            "The top-level minimum/maximum bounds are the extremes of valid linked ranges, not a claim that different families form one continuous range.");
        text.AppendLine();
        foreach (string warning in summary.FamilyWarnings)
            text.AppendLine("- " + warning);
        if (summary.MissingFamilyTemplateIds.Length > 0)
            text.AppendLine("- Exact absent family IDs are retained in ClassificationSummary.json.");
        text.AppendLine();
        text.AppendLine("Category retains raw ItemType, DynelType and ItemFlags, plus separate original-file and " +
            "default-applied/live ItemClass values. The exporter leaves raw ItemType at its default in some data; " +
            "zero is preserved rather than reinterpreted as a proven category. Undefined values stay numeric.");
        text.AppendLine();
        text.AppendLine("| Original-file ItemClass | Records |");
        text.AppendLine("| --- | ---: |");
        foreach (var group in records.GroupBy(record => record.Category.FileItemClass.HasValue
                     ? record.Category.FileItemClass.Value.ToString(CultureInfo.InvariantCulture) + " " + (record.Category.FileItemClassName ?? "UNDEFINED")
                     : "MISSING").OrderBy(group => group.Key, StringComparer.Ordinal))
            text.AppendLine($"| {group.Key} | {group.Count():N0} |");
        text.AppendLine();
        text.AppendLine("## Representative records");
        text.AppendLine();
        text.AppendLine("The following actual input records represent every populated primary classification. " +
            "Names identify records only and played no part in classification. Source paths refer to the exact " +
            "item's stored requirement evidence; the JSON retains complete expressions and original numeric values.");
        text.AppendLine();
        text.AppendLine("| Classification | Template ID | Stored QL | Stored name | Verified associations | Reason | Supporting field |");
        text.AppendLine("| --- | ---: | ---: | --- | --- | --- | --- |");
        foreach (string bucket in Program.Buckets)
        {
            foreach (CatalogItem record in records.Where(record => record.ExpansionClassification == bucket)
                         .OrderBy(record => record.TemplateId).ThenBy(record => record.StoredQuality)
                         .ThenBy(record => record.SourceRecordIndex).Take(2))
                text.AppendLine($"| {Display(bucket)} | {record.TemplateId} | {record.StoredQuality} | " +
                    $"{Escape(record.Name ?? "[missing]")} | {Escape(string.Join(", ", record.VerifiedExpansions))} | " +
                    $"{Escape(record.ClassificationReason)} | {Escape(record.SourceField ?? "[no verified field]")} |");
        }
        text.AppendLine();
        text.AppendLine("## Garden/sanctuary keys and mission chest integration");
        text.AppendLine();
        text.AppendLine("UNVERIFIED: no dedicated key-kind or garden/sanctuary provenance field was established. " +
            "A verified Shadowlands requirement can classify an item's expansion association, but does not by itself " +
            "prove the item is a garden or sanctuary key. A key-like name, quest reward link or playfield check " +
            "does not establish that structural item-kind classification. All item definitions remain in the inventory; " +
            "no name-based key exclusion or loot eligibility decision is applied.");
        text.AppendLine();
        int rubiKaCount = summary.ClassificationCounts.GetValueOrDefault("RubiKa");
        text.AppendLine($"{summary.RkOnlyMissionChestIntegration}: the selected evidence establishes {rubiKaCount:N0} " +
            "verified Rubi-Ka records. Missing requirements are not proof of Rubi-Ka membership. " +
            "Treating Unknown as Rubi-Ka would admit unclassified expansion items. " +
            (rubiKaCount == 0 ? "A filter accepting only verified Rubi-Ka currently has no candidates. " : "") +
            "Expansion associations can support later exclusion or expansion-specific selection, but do not establish " +
            "normal chest-drop eligibility or reliable garden/sanctuary key identification. A verified RK membership " +
            "source and a separate loot-eligibility policy are still required for RK-only chests. No live selection was changed.");
        text.AppendLine();
        text.AppendLine("## Validation and provenance");
        text.AppendLine();
        text.AppendLine("- PASS: decoded input/output counts, unique original-record assignments and exact ID/name/stored-QL preservation.");
        text.AppendLine("- Classification evidence: positive assignments follow supported explicit item expansion requirements; " +
            "every primary record preserves its actual classification reason and the available raw evidence. " +
            "Unknown records are never promoted to Rubi-Ka by absence of a requirement.");
        text.AppendLine("- PASS: deterministic ordering and generation without timestamps or random values in published files.");
        text.AppendLine("- Repeat verification: run the generator again with --verify to independently read the same inputs, " +
            "regenerate all files and require byte-identical hashes. It refuses changed or missing outputs; " +
            "the actual result is recorded in ../ExpansionClassification.verify.json only after success, " +
            "bound to these inputs, the tool and every output hash. It must not be assumed from this report.");
        text.AppendLine("- Source item/family identities are checked again before publication. The tool reads each item dataset " +
            "once per invocation; second-run verification is the only requested repeated full parse.");
        text.AppendLine();
        text.AppendLine($"- items.dat SHA256: `{summary.ItemsDatSha256}`");
        text.AppendLine($"- ItemTemplates.json SHA256: `{summary.ItemTemplatesSha256 ?? "MISSING"}`");
        text.AppendLine($"- Tool assembly SHA256: `{summary.ToolAssemblySha256}`");
        text.AppendLine("- Existing reader/definitions: AORebirth/Server/ZoneEngine_New/Core/Inventory/Dat/ItemsDatReader.cs and DatItemTemplate.cs.");
        text.AppendLine("- Original mapper: Tools/RDBDataExtractor/ItemRdbMapper.cs; file stats order is Stats18, Stats2B, Stats.");
        text.AppendLine("- Field and flag semantics: CharacterStat.cs, AORebirth.Enums/Expansions.cs, ItemFlags.cs, ItemClass.cs; Core/Inventory/ItemTemplate.cs requirement evaluator.");
        text.AppendLine("- Assignment policy: explicit positive item User Expansion requirements are accepted as association evidence by Mike; " +
            "RequirementExpansionClassifier.cs applies the supported boolean/target/operator rules without name or ID inference.");
        text.AppendLine("- Explicit QL family metadata: Core/GameData/HashItemCatalog.cs and HashInstance.cs.");
        text.AppendLine("- Detailed record counts, diagnostics, primary file hashes and byte sizes: ClassificationSummary.json.");
        return text.ToString().Replace("\r\n", "\n", StringComparison.Ordinal);
    }

    static string Percent(int count, int total)
        => (100m * count / total).ToString("0.0000", CultureInfo.InvariantCulture);

    static string Escape(string value)
        => value.Replace("|", "\\|", StringComparison.Ordinal).Replace("\r", " ", StringComparison.Ordinal)
            .Replace("\n", " ", StringComparison.Ordinal);

    static string Display(string bucket) => bucket switch
    {
        "RubiKa" => "Rubi-Ka",
        "AlienInvasion" => "Alien Invasion",
        "LostEden" => "Lost Eden",
        "Xan" => "Legacy of the Xan",
        "NotumWars" => "Notum Wars",
        "PMVObsidianEdition" => "PMV Obsidian Edition",
        "MultipleExpansions" => "Multiple Expansions",
        _ => bucket
    };
}
