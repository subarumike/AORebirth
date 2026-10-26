using System.Globalization;
using System.Text;

namespace AORebirth.Tools.ItemExpansionCatalog;

internal static class HistoricalReport
{
    internal static string Create(HistoricalSummary summary, HistoricalRecord[] records,
        HistoricalEvidenceBundle bundle, BaselineState baseline)
    {
        var text = new StringBuilder();
        text.AppendLine("# AO Item Patch History and Loot Classification");
        text.AppendLine();
        text.AppendLine($"Reconstruction status: **{Escape(summary.ReconstructionStatus)}**. " +
            $"The inventory covers {summary.TotalInputRecords:N0} current input definitions and " +
            $"{summary.DistinctTemplateIds:N0} distinct template IDs. Historical and acquisition conclusions " +
            "are limited to the exact evidence recovered. Missing evidence remains Unknown.");
        text.AppendLine();
        text.AppendLine("This is historical research and offline catalog generation. Runtime loot selection, NPC loot tables, " +
            "mission chests, player inventories and production databases are unchanged. Nothing is merged or deployed by this tool. " +
            "The previous classification files remain a hash-bound baseline.");

        Heading(text, "Before and after classification");
        text.AppendLine("The baseline classifies explicit item expansion requirements. The reconstructed primary files classify " +
            "historical content associations. These are different axes: moving from one table to the other is not a count of " +
            "corrected entitlement checks. RequiredExpansionAssociations and RequirementEvidence remain separately attached to every item.");
        text.AppendLine();
        text.AppendLine("| Classification | Previous requirement association | Reconstructed content association |");
        text.AppendLine("| --- | ---: | ---: |");
        string[] categories = new[] { "RubiKa", "NotumWars", "Shadowlands", "AlienInvasion", "LostEden", "Xan", "OtherContent", "MultipleExpansions", "Unknown" }
            .Concat(summary.BeforeClassificationCounts.Keys).Concat(summary.ContentClassificationCounts.Keys)
            .Distinct(StringComparer.Ordinal).ToArray();
        foreach (string category in categories)
            text.AppendLine($"| {Escape(category)} | {summary.BeforeClassificationCounts.GetValueOrDefault(category):N0} | " +
                $"{summary.ContentClassificationCounts.GetValueOrDefault(category):N0} |");
        text.AppendLine($"| TOTAL | {summary.BeforeClassificationCounts.Values.Sum():N0} | {summary.ContentClassificationCounts.Values.Sum():N0} |");
        text.AppendLine();
        text.AppendLine($"- Current definitions with recovered historical observations: {summary.ItemsWithHistoricalObservations:N0}.");
        text.AppendLine($"- Current definitions with a recorded addition observation: {summary.ItemsWithRecordedAddition:N0}.");
        text.AppendLine($"- Historical template IDs absent from the selected current database: {summary.HistoricalOnlyTemplateIds:N0}; retained as history rather than fabricated current items.");
        text.AppendLine($"- Items with verified RK acquisition/access evidence: {summary.VerifiedRkAccessible:N0}.");
        text.AppendLine($"- Items positively supported as ordinary RK mission chest loot: {summary.MissionChestEligibleRk:N0}.");
        text.AppendLine($"- Items with a verified special-purpose exclusion: {summary.VerifiedSpecialPurposeExcluded:N0}.");
        text.AppendLine($"- Items with unverified chest eligibility: {summary.UnverifiedChestEligibility:N0}.");
        text.AppendLine();
        text.AppendLine("| Historical content evidence confidence | Current definitions |");
        text.AppendLine("| --- | ---: |");
        foreach (var group in records.GroupBy(record => record.ContentConfidence).OrderBy(group => group.Key, StringComparer.Ordinal))
            text.AppendLine($"| {Escape(group.Key)} | {group.Count():N0} |");

        Heading(text, "Evidence and classification rules");
        text.AppendLine("1. Exact template identities join current definitions to recovered database histories and acquisition documentation. " +
            "Names, template ID ranges, QL and appearance never infer expansion, introduction date or loot eligibility.");
        text.AppendLine("2. FirstKnownPatch is the earliest verified observation available to this reconstruction. " +
            "FirstRecordedAdditionPatch records a publisher/database addition claim when available. Neither automatically proves " +
            "the item's actual first introduction: earlier snapshots may be missing, and a database's initial import is not a release change log.");
        text.AppendLine("3. Content association, current account/expansion requirement, accessible playfield, acquisition source and " +
            "ordinary mission-chest eligibility are retained independently. A post-Shadowlands release date does not imply Shadowlands content. " +
            "A Rubi-Ka content association does not imply RK accessibility or random-chest eligibility.");
        text.AppendLine("4. Exact primary Auno11.0 roster identities support qualified initial base-client database membership. " +
            "Independently matched AOItems roster identities retain stronger confidence; a second identity source is not required for every primary-source record. " +
            "This means membership in that initial base-client item dataset only. The AOItems initial label 11.0 carries build 11.0 " +
            "dated 2001-06-01; its patch label 12.0 carries build 11.5 dated 2001-06-26. These are distinct label/build identities. " +
            "The original retail launch build is not authenticated by either row. Initial database membership does not prove a " +
            "retail acquisition source, that a current version is unchanged, or that a GM/test/placeholder item is player loot.");
        text.AppendLine("5. Acquisition assertions require an exact identity bridge and retain their source URLs, temporal scope, " +
            "confidence and exclusivity. A documented source is not automatically the only source. An item is not marked " +
            "historically absent from chests merely because another acquisition route or a modern exclusion is documented.");
        text.AppendLine("6. An explicit item requirement can prove an entitlement association under the previous classification policy, " +
            "but it does not by itself identify original content, a garden/sanctuary key or a chest source. Conflicting or missing " +
            "content evidence remains explicit rather than being resolved from an entitlement or a release date.");
        text.AppendLine("7. A guide's explicit introduction statement is retained separately in DocumentedIntroductionPatch, " +
            "with its source and scope. It is not silently substituted for a recovered database delta, build date or release date. " +
            "Miy's armor is a concrete post-Shadowlands patch17.5 example whose documented RK acquisition establishes RK content independently of the patch era.");
        text.AppendLine();
        text.AppendLine("| Initial base-client evidence grade | Current definitions with this evidence |");
        text.AppendLine("| --- | ---: |");
        foreach (var grade in records.SelectMany(record => record.ClassificationEvidence
                     .Where(assertion => assertion.Confidence is "CORROBORATED_PRIMARY_INITIAL_BASE_DATABASE_TAG"
                         or "CORROBORATED_INDEPENDENT_INITIAL_ROSTER_INTERSECTION")
                     .Select(assertion => (record.SourceRecordIndex, assertion.Confidence)))
                     .GroupBy(value => value.Confidence).OrderBy(group => group.Key, StringComparer.Ordinal))
            text.AppendLine($"| {Escape(grade.Key)} | {grade.Select(value => value.SourceRecordIndex).Distinct().Count():N0} |");

        Heading(text, "Recovered patch chronology");
        text.AppendLine($"The recovered index contains {bundle.Patches.Length:N0} observed source version labels " +
            $"({summary.ObservedPatchLabels:N0} recorded by the summary). This is the complete recovered index for this evidence bundle, " +
            "not proof that every released patch was recovered. Series headings, raw historical spelling and database build aliases " +
            "remain distinguishable; they must not be counted automatically as separate public releases. BuildDate is never copied to ReleaseDates.");
        text.AppendLine("Dated rows are ordered by documented database build date where available, otherwise an unambiguous release date. " +
            "Undated or conflicting-date-only labels follow in source-version order. Opaque build text such as12.52 is not treated as semantic patch12.52.");
        text.AppendLine();
        text.AppendLine("A missing note or inaccessible route does not imply zero item changes. An explicit zero client-template delta " +
            "is scoped to database templates; server-side acquisition or loot changes may still have occurred. " +
            "Source-reported counters are summaries until exact changed identities are recovered.");
        text.AppendLine();
        text.AppendLine("| Source version label | Database build | Database build date | Documented release date(s) | New / modified / deleted templates | Availability | Item-change evidence | Sources |");
        text.AppendLine("| --- | --- | --- | --- | --- | --- | --- | --- |");
        foreach (HistoricalPatch patch in bundle.Patches)
            text.AppendLine($"| {Escape(patch.Version)} | {Escape(patch.DatabaseBuild ?? "UNKNOWN")} | " +
                $"{Escape(patch.BuildDate ?? "UNKNOWN")} | {Escape(patch.ReleaseDates.Length == 0 ? "UNKNOWN" : string.Join("; ", patch.ReleaseDates))} | " +
                $"{Number(patch.NewTemplateCount)} / {Number(patch.ModifiedTemplateCount)} / {Number(patch.DeletedTemplateCount)} | " +
                $"{Escape(patch.Availability)} | {Escape(patch.ItemChangesStatus)}" +
                $"{(patch.ExplicitNoTemplateChanges ? "; EXPLICIT NO TEMPLATE CHANGES" : "")} | {Links(patch.SourceUrls)} |");
        text.AppendLine();
        foreach (HistoricalPatch patch in bundle.Patches.Where(patch => patch.Notes.Length > 0))
            text.AppendLine($"- {Escape(patch.Version)}: {Escape(string.Join("; ", patch.Notes))}");

        Heading(text, "Recovered additions, removals and modifications");
        text.AppendLine("The following totals count recovered historical observations by their explicit kind. " +
            "An item may have several observations, sources or patches. These are not disjoint current-item counts, " +
            "and an initial baseline import must not be relabelled as that many retail additions on its build date.");
        text.AppendLine();
        text.AppendLine("A concrete source difference illustrates why a database addition tag is not an exact introduction date: " +
            "Auno places 342 additions under 18.8.55, while the archived AOItems publisher index reports 343 new templates " +
            "under 18.8.54 and no new templates under 18.8.55 (43 modified). The publisher builds are 18.8.54.8 " +
            "dated 2020-10-26 and 18.8.55.0 dated 2020-10-29. The Auno tag may cover an import interval rather than " +
            "the precise game patch. Preserve both source observations and the disagreement instead of silently choosing an introduction patch.");
        text.AppendLine();
        text.AppendLine("| Observation kind | Recovered observations |");
        text.AppendLine("| --- | ---: |");
        foreach (var pair in summary.ObservationsByKind.OrderBy(pair => pair.Key, StringComparer.Ordinal))
            text.AppendLine($"| {Escape(pair.Key)} | {pair.Value:N0} |");
        text.AppendLine();
        text.AppendLine("ADDED/DELETED/MODIFIED identity sets establish the corresponding database observation only. " +
            "A MODIFIED record alone does not reveal whether its name, QL, requirements, identity relationship or acquisition changed. " +
            "A rename or replacement requires explicit before/after evidence; none is inferred from similar names. " +
            "Current StoredQuality and explicit family ranges are preserved, but are not substitutes for historical full template snapshots. " +
            "Unrecovered past QL ranges, old requirements, replacement IDs and source changes remain unresolved.");

        Heading(text, "Acquisition sources and chest decisions");
        var acquisitions = records.SelectMany(record => record.AcquisitionSources.Select(source => (Record: record, Source: source))).ToArray();
        text.AppendLine("| Documented acquisition kind | Current definitions with evidence | Definitions with an explicit exclusive-source claim | Temporal scope(s) |");
        text.AppendLine("| --- | ---: | ---: | --- |");
        foreach (var group in acquisitions.GroupBy(value => value.Source.Kind).OrderBy(group => group.Key, StringComparer.Ordinal))
            text.AppendLine($"| {Escape(group.Key)} | {group.Select(value => value.Record.SourceRecordIndex).Distinct().Count():N0} | " +
                $"{group.Where(value => value.Source.Exclusive).Select(value => value.Record.SourceRecordIndex).Distinct().Count():N0} | " +
                $"{Escape(string.Join("; ", group.Select(value => value.Source.TemporalScope).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal)))} |");
        text.AppendLine();
        text.AppendLine("One item may appear under multiple valid acquisition kinds. These counts are not additive. " +
            "Historical chest availability and current documentation are kept separate. A guide's update date is not an item introduction date. " +
            "A low/high-Ql link or a source's stated QL does not verify every intermediate current variant or every other item in its family.");
        text.AppendLine();
        text.AppendLine("| Chest disposition | Current definitions |");
        text.AppendLine("| --- | ---: |");
        foreach (var group in records.GroupBy(record => record.ChestDisposition).OrderBy(group => group.Key, StringComparer.Ordinal))
            text.AppendLine($"| {Escape(group.Key)} | {group.Count():N0} |");
        text.AppendLine();
        text.AppendLine("MissionChestEligibleRK.json contains positive ordinary RK mission-chest evidence only. " +
            "MissionChestExcluded.json is an explicit exclusion/review inventory; an unverified item is not thereby proven never to have dropped. " +
            "Verified special-purpose exclusions and absence-of-positive-evidence cases retain distinct fields and reasons. " +
            "Garden/sanctuary keys, progression rewards, GM/test items and similar roles are identified only where exact evidence establishes the role. " +
            "Items with unknown roles are not guessed from their names or entitlement flags.");
        text.AppendLine();
        text.AppendLine("| Verified special-purpose role | Current definitions |");
        text.AppendLine("| --- | ---: |");
        foreach (var group in records.SelectMany(record => record.SpecialPurposeRoles.Select(role => (record.SourceRecordIndex, Role: role)))
                     .GroupBy(value => value.Role).OrderBy(group => group.Key, StringComparer.Ordinal))
            text.AppendLine($"| {Escape(group.Key)} | {group.Select(value => value.SourceRecordIndex).Distinct().Count():N0} |");

        Heading(text, "Representative reconciled records");
        text.AppendLine("These are actual current input records, selected deterministically from populated classifications, " +
            "chest dispositions and documented roles. Names are display metadata only. Unknown first appearance, " +
            "content origin or acquisition remains Unknown even when another axis has positive evidence.");
        text.AppendLine();
        text.AppendLine("| Template ID | Stored QL | Stored name | Historical content | Current required expansion associations | First known patch | Chest disposition | Sources |");
        text.AppendLine("| ---: | ---: | --- | --- | --- | --- | --- | --- |");
        IEnumerable<HistoricalRecord> examples = records.GroupBy(record => record.PrimaryClassification)
            .OrderBy(group => group.Key, StringComparer.Ordinal).SelectMany(group => group.Take(2))
            .Concat(records.GroupBy(record => record.ChestDisposition).OrderBy(group => group.Key, StringComparer.Ordinal).SelectMany(group => group.Take(2)))
            .Concat(records.Where(record => record.DocumentedIntroductionPatch != null).Take(5))
            .Concat(records.Where(record => record.VerifiedSpecialPurposeExclusion).Take(4));
        foreach (HistoricalRecord record in examples.DistinctBy(record => record.SourceRecordIndex)
                     .OrderBy(record => record.TemplateId).ThenBy(record => record.StoredQuality).ThenBy(record => record.SourceRecordIndex))
            text.AppendLine($"| {record.TemplateId} | {record.StoredQuality} | {Escape(record.Name ?? "[missing]")} | " +
                $"{Escape(record.PrimaryClassification + ": " + string.Join(", ", record.ContentAssociations))} | " +
                $"{Escape(record.RequiredExpansionAssociations.Length == 0 ? "UNKNOWN / no proven requirement association" : string.Join(", ", record.RequiredExpansionAssociations))} | " +
                $"{Escape(record.FirstKnownPatch ?? "UNKNOWN")} | {Escape(record.ChestDisposition)} | {Links(record.SourceUrls)} |");
        text.AppendLine();
        text.AppendLine("| Explicit guide introduction claim | Template ID | Earliest recovered database patch | Qualification |");
        text.AppendLine("| --- | ---: | --- | --- |");
        foreach (HistoricalRecord record in records.Where(record => record.DocumentedIntroductionPatch != null))
            text.AppendLine($"| {Escape(record.DocumentedIntroductionPatch!)} | {record.TemplateId} | {Escape(record.FirstKnownPatch ?? "UNKNOWN")} | {Escape(record.ActualIntroductionStatus)} |");

        Heading(text, "Source availability and evidence boundaries");
        text.AppendLine($"The bundle records {bundle.Sources.Length:N0} source routes, with {summary.AvailableSources:N0} " +
            $"available sources and {summary.UnavailableSourceRoutes:N0} unavailable routes under its source-status rules. " +
            "A failed route is not proof a document never existed: archived snapshots and the public MediaWiki API can recover " +
            "content when current pages fail. AOItems current-domain availability or repurposing is distinguished from original " +
            "publisher content recovered through Wayback. Auno access can differ between a direct request and indexed/archived views; " +
            "only the actual recovered response is cited as evidence. Unavailable pages are not silently counted as successful research.");
        text.AppendLine();
        text.AppendLine("| Source ID | Status | Source date / scope | Source URL | Cached source SHA256 |");
        text.AppendLine("| --- | --- | --- | --- | --- |");
        foreach (HistoricalSource source in bundle.Sources.OrderBy(source => source.Id, StringComparer.Ordinal))
            text.AppendLine($"| {Escape(source.Id)} | {Escape(source.Status)} | " +
                $"{Escape((source.SourceDate ?? "date unknown") + "; " + (source.Scope ?? source.Title))} | " +
                $"{Links([source.Url])} | {Escape(source.Sha256 ?? "UNAVAILABLE")} |");

        Heading(text, "Unresolved coverage and remaining limitations");
        text.AppendLine("| Stored original-file ItemClass | Current definitions | Unknown historical content | No verified acquisition source |");
        text.AppendLine("| --- | ---: | ---: | ---: |");
        foreach (var group in records.GroupBy(record => record.Category?.FileItemClass is int itemClass
                     ? itemClass.ToString(CultureInfo.InvariantCulture) + " " + (record.Category.FileItemClassName ?? "UNDEFINED")
                     : "MISSING").OrderBy(group => group.Key, StringComparer.Ordinal))
            text.AppendLine($"| {Escape(group.Key)} | {group.Count():N0} | " +
                $"{group.Count(record => record.PrimaryClassification == "Unknown"):N0} | " +
                $"{group.Count(record => record.AcquisitionSources.Length == 0):N0} |");
        text.AppendLine();
        text.AppendLine("Original-file category values are preserved without inventing a category for missing values. " +
            "Keys, quest objects, tradeskill parts, implants, utilities, nanos and other special roles are not excluded from " +
            "coverage merely because the raw class is absent or broad. No documented role means unknown role, not ordinary loot.");
        text.AppendLine();
        foreach (string gap in bundle.Gaps.Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal))
            text.AppendLine("- " + Escape(gap));
        foreach (var group in records.SelectMany(record => record.Unresolved.Select(reason => (record.SourceRecordIndex, Reason: reason)))
                     .GroupBy(value => value.Reason).OrderBy(group => group.Key, StringComparer.Ordinal))
            text.AppendLine($"- {Escape(group.Key)}: {group.Select(value => value.SourceRecordIndex).Distinct().Count():N0} current definitions.");
        text.AppendLine();
        text.AppendLine("The reconstruction is not a complete historical server loot table. Published client databases do not " +
            "contain a proof of every server drop source, and patch notes omit many record-level changes. " +
            "Undocumented patches, missing snapshots, uncertain release dates, source conflicts and historical-only identities " +
            "are retained for resumable research. No item is assigned an expansion or chest source simply to reduce Unknown totals.");

        Heading(text, "Validation, baseline preservation and reproducibility");
        text.AppendLine($"- Coverage: {Escape(summary.Coverage)}; {summary.TotalInputRecords:N0} decoded input records, exact original template identities, names and stored QLs retained.");
        text.AppendLine($"- Baseline preservation: {Escape(summary.BaselinePreservation)}. Baseline items.dat SHA256: `{baseline.ItemsDatSha256}`.");
        text.AppendLine($"- Selected items.dat SHA256: `{summary.ItemsDatSha256}`.");
        text.AppendLine($"- ItemTemplates.json SHA256: `{summary.ItemTemplatesSha256 ?? "MISSING"}`.");
        text.AppendLine($"- Evidence bundle SHA256: `{summary.EvidenceBundleSha256}`.");
        text.AppendLine($"- Historical observations SHA256: `{summary.ObservationsSha256}`.");
        text.AppendLine($"- Tool assembly SHA256: `{summary.ToolAssemblySha256}`.");
        text.AppendLine("- Published ordering and content are deterministic for these frozen inputs. Actual repeated-generation validation " +
            "belongs to its measured verification result; it is not assumed from this report's existence.");
        text.AppendLine();
        text.AppendLine("| Preserved baseline file | SHA256 | Bytes |");
        text.AppendLine("| --- | --- | ---: |");
        foreach (BaselineFile file in baseline.Files.OrderBy(file => file.File, StringComparer.Ordinal))
            text.AppendLine($"| {Escape(file.File)} | {file.Sha256} | {file.Bytes:N0} |");
        text.AppendLine();
        text.AppendLine("| Generated primary classification file | Item count | SHA256 |");
        text.AppendLine("| --- | ---: | --- |");
        foreach (OutputIdentity file in summary.PrimaryFiles.OrderBy(file => file.File, StringComparer.Ordinal))
            text.AppendLine($"| {Escape(file.File)} | {file.ItemCount:N0} | {file.Sha256} |");

        Heading(text, "Recommendations for integration");
        text.AppendLine($"Readiness: **{Escape(summary.LootIntegrationReadiness)}**. Review the positive ordinary-RK-chest " +
            "records and each source's historical/current scope before any runtime integration. Preserve exact identities and " +
            "documented QL boundaries; do not expand an item-level source to a whole named category or family without evidence.");
        text.AppendLine();
        text.AppendLine("Keep primary historical content, current entitlement requirements, acquisition and chest eligibility as " +
            "separate inputs. Unknown is not Rubi-Ka and is not permission to roll an item. A base-client association, an RK shop " +
            "source or an RK quest reward is not ordinary mission-chest evidence. Keep verified special-purpose exclusions explicit, " +
            "and preserve contradictions for review. Resume from the hash-bound source caches and missing-patch inventory; " +
            "do not repeatedly download entire databases. Any eventual runtime change requires a separate scoped implementation " +
            "and normal Windows acceptance/release workflow. This task performs no such integration.");
        return text.ToString().Replace("\r\n", "\n", StringComparison.Ordinal);
    }

    static void Heading(StringBuilder text, string title)
    {
        text.AppendLine();
        text.AppendLine("## " + title);
        text.AppendLine();
    }

    static string Number(int? value) => value?.ToString("N0", CultureInfo.InvariantCulture) ?? "UNKNOWN";

    static string Escape(string value) => value.Replace("\\", "\\\\", StringComparison.Ordinal)
        .Replace("|", "\\|", StringComparison.Ordinal).Replace("[", "\\[", StringComparison.Ordinal)
        .Replace("]", "\\]", StringComparison.Ordinal).Replace("<", "&lt;", StringComparison.Ordinal)
        .Replace(">", "&gt;", StringComparison.Ordinal).Replace("\r", " ", StringComparison.Ordinal)
        .Replace("\n", " ", StringComparison.Ordinal);

    static string Links(IEnumerable<string> urls) => string.Join("; ", urls.Distinct(StringComparer.Ordinal)
        .Order(StringComparer.Ordinal).Select(url => Uri.TryCreate(url, UriKind.Absolute, out Uri? parsed)
            && parsed.Scheme is "http" or "https"
                ? "[" + Escape(parsed.Host) + "](<" + url.Replace("\r", "", StringComparison.Ordinal).Replace("\n", "", StringComparison.Ordinal) + ">)"
                : Escape(url)));
}
