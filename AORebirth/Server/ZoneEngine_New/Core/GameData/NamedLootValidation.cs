namespace ZoneEngine_New.Core.GameData;

using System;
using System.Collections.Generic;
using System.Linq;

using ZoneEngine_New.Core.Entities;

internal static class NamedLootValidation
{
    internal const int MaxTables = 128;
    internal const int MaxAssignments = 256;
    const int MaxGroups = 16;
    const int MaxEntries = 64;
    const int MaxWeight = 1_000_000;
    const int MaxRollWork = 8_192;

    internal static string? Check(
        NamedLootTable[]? tables, NamedLootAssignment[]? assignments,
        Func<NamedLootEntry, bool> validItem, Func<NamedLootAssignment, bool> validTarget,
        Func<NamedLootAssignment, NamedLootAssignment, bool> mayOverlap)
    {
        if (tables == null || tables.Length > MaxTables || assignments == null || assignments.Length > MaxAssignments)
            return "The named loot registry exceeds its table or assignment limit.";

        var byId = new Dictionary<string, NamedLootTable>(StringComparer.Ordinal);
        var groupIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (NamedLootTable table in tables)
        {
            if (table == null || !ValidKey(table.Id) || !byId.TryAdd(table.Id, table)
                || string.IsNullOrWhiteSpace(table.Name) || table.Name.Length > 120
                || table.Description == null || table.Description.Length > 1000
                || !ValidCategory(table.Category) || table.EvidenceReference?.Length > 500
                || table.Groups == null || table.Groups.Length > MaxGroups)
                return "A named loot table has invalid fields, a duplicate ID or too many groups.";

            foreach (NamedLootGroup group in table.Groups)
            {
                if (group == null || !ValidKey(group.Id)
                    || !groupIds.Add(NamedLootRegistry.QualifiedGroup(table.Id, group.Id))
                    || !ValidGroup(group))
                    return "A loot group in " + table.Id + " has an invalid mode, count, chance, condition or duplicate ID.";
                var entries = new HashSet<string>(StringComparer.Ordinal);
                foreach (NamedLootEntry entry in group.Entries)
                {
                    if (entry?.EvidenceClassification == "GUARANTEED_PROVEN")
                        return "Verified guarantee linkage is unavailable. Save an explicit GM-authored rule without a guarantee evidence claim.";
                    if (entry == null || !ValidKey(entry.Id) || !entries.Add(entry.Id) || !ValidEntry(entry, group)
                        || !validItem(entry))
                        return "A loot entry in " + table.Id + "/" + group.Id + " has an invalid item, QL, quantity, evidence or roll value.";
                }
            }
        }

        var assignmentIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (NamedLootAssignment assignment in assignments)
        {
            if (assignment == null || !ValidKey(assignment.Id) || !assignmentIds.Add(assignment.Id)
                || assignment.Priority is < -10_000 or > 10_000 || !ValidCondition(assignment.Condition)
                || (assignment.PlayfieldId.HasValue && assignment.Condition?.PlayfieldId is int field
                    && assignment.PlayfieldId.Value != field)
                || !ValidScope(assignment) || !validTarget(assignment))
                return "A named loot assignment has invalid fields, duplicate ID or an unavailable authored target.";
            if (!ValidMerge(assignment, byId, groupIds))
                return "Assignment " + assignment.Id + " must reference an existing table and the groups required by its merge operation.";
        }

        for (int i = 0; i < assignments.Length; i++)
        {
            NamedLootAssignment left = assignments[i];
            int work = Cost(left);
            bool hasInheritedTarget = left.Merge == "append";
            for (int j = 0; j < assignments.Length; j++)
            {
                if (i == j)
                    continue;
                NamedLootAssignment right = assignments[j];
                if (!MayMatchSameContext(left, right) || !mayOverlap(left, right))
                    continue;
                work += Cost(right);
                if (left.Merge == "append" && right.Merge == "append" && left.TableId == right.TableId)
                    return "Overlapping append assignments would add the same named table twice. " + left.Id + " and " + right.Id;
                if (left.Merge != "append" && right.Merge != "append" && left.TargetGroup == right.TargetGroup
                    && Rank(left) == Rank(right))
                    return "Conflicting group overrides share the same precedence. " + left.Id + " and " + right.Id;
                if (!hasInheritedTarget && right.Merge == "append"
                    && left.TargetGroup!.StartsWith(right.TableId + "/", StringComparison.Ordinal) && Rank(right).CompareTo(Rank(left)) < 0)
                    hasInheritedTarget = true;
            }
            if (!hasInheritedTarget)
                return "Assignment " + left.Id + " needs an earlier, potentially applicable append of its inherited group.";
            if (work > MaxRollWork)
                return "Potentially overlapping assignments exceed the per-corpse named loot roll limit.";
        }
        return null;

        int Cost(NamedLootAssignment assignment)
            => assignment.Merge switch
            {
                "append" => byId[assignment.TableId!].Groups.Sum(group => 1 + group.RollCount * group.Entries.Length),
                "replace" => byId[assignment.TableId!].Groups.Where(group => group.Id == assignment.SourceGroupId)
                    .Sum(group => 1 + group.RollCount * group.Entries.Length),
                _ => 0
            };
    }

    internal static bool ValidKey(string? id)
        => id is { Length: > 0 and <= 64 } && id.All(ch =>
            ch is >= 'a' and <= 'z' or >= 'A' and <= 'Z' or >= '0' and <= '9' or '.' or '_' or '-');

    static (int Scope, int Priority) Rank(NamedLootAssignment assignment)
        => (NamedLootRegistry.Specificity(assignment.Scope), assignment.Priority);

    static bool ValidCategory(string? category) => category is
        "ordinaryNpc" or "family" or "namedBoss" or "dynaBoss" or "dungeon" or "mission" or "raid" or "quest" or "event" or "global";

    static bool ValidGroup(NamedLootGroup group)
    {
        bool weighted = group.Mode is "WEIGHTED_ONE" or "WEIGHTED_MANY";
        return (weighted || group.Mode is "INDEPENDENT" or "GUARANTEED_ALL")
            && group.RollCount is >= 1 and <= 16 && (group.Mode != "WEIGHTED_ONE" || group.RollCount == 1)
            && group.ChanceBasisPoints is >= 0 and <= 10_000 && ValidCondition(group.Condition)
            && (weighted ? group.EmptyWeight is >= 0 and <= MaxWeight : group.EmptyWeight == 0)
            && group.Entries is { Length: <= MaxEntries }
            && (group.Entries.Length > 0 || (weighted && group.EmptyWeight > 0));
    }

    static bool ValidEntry(NamedLootEntry entry, NamedLootGroup group)
    {
        bool weighted = group.Mode is "WEIGHTED_ONE" or "WEIGHTED_MANY";
        bool independent = group.Mode == "INDEPENDENT";
        return entry.LowId > 0 && entry.HighId > 0 && entry.MinQuality > 0
            && entry.MaxQuality >= entry.MinQuality && entry.MaxQuality <= ushort.MaxValue
            && entry.MinQuantity > 0 && entry.MaxQuantity >= entry.MinQuantity && entry.MaxQuantity <= Corpse.LootCapacity
            && (!entry.UniquePerCorpse || entry.MaxQuantity == 1) && ValidCondition(entry.Condition)
            && (independent ? entry.ChanceBasisPoints is >= 0 and <= 10_000 : entry.ChanceBasisPoints == 10_000)
            && (weighted ? entry.Weight is > 0 and <= MaxWeight : entry.Weight == 0)
            && entry.EvidenceReference?.Length is not > 500
            && ValidEvidence(entry, group);
    }

    static bool ValidEvidence(NamedLootEntry entry, NamedLootGroup group)
    {
        if (entry.EvidenceClassification == null)
            return true; // Explicit GM-authored configuration, independent of observed evidence.
        if (string.IsNullOrWhiteSpace(entry.EvidenceReference)
            || entry.EvidenceClassification is not ("OBSERVED_AVAILABLE_LOOT" or "COMMUNITY_DOCUMENTED" or "INFERRED"))
            return false;
        bool canGuarantee = group.Mode == "GUARANTEED_ALL"
            || (group.Mode == "INDEPENDENT" && entry.ChanceBasisPoints == 10_000)
            || (group.Mode is "WEIGHTED_ONE" or "WEIGHTED_MANY" && group.ChanceBasisPoints == 10_000 && group.EmptyWeight == 0);
        return !canGuarantee;
    }

    static bool ValidCondition(NamedLootCondition? condition)
        => condition == null || (condition.MinimumLevel is not (<= 0 or > ushort.MaxValue)
            && condition.MaximumLevel is not (<= 0 or > ushort.MaxValue)
            && (!condition.MinimumLevel.HasValue || !condition.MaximumLevel.HasValue
                || condition.MinimumLevel <= condition.MaximumLevel)
            && condition.PlayfieldId is not <= 0);

    static bool ValidScope(NamedLootAssignment row) => row.Scope switch
    {
        "global" => row is { FamilyHash: null, TemplateHash: null, BandIndex: null, PlayfieldId: null,
            AuthoredIndex: null, AuthoredSignature: null },
        "family" => row is { FamilyHash.Length: > 0 and <= 64, TemplateHash: null, BandIndex: null, PlayfieldId: null,
            AuthoredIndex: null, AuthoredSignature: null },
        "template" or "namedBoss" => row is { FamilyHash: null, TemplateHash.Length: > 0 and <= 64, BandIndex: null,
            PlayfieldId: null, AuthoredIndex: null, AuthoredSignature: null },
        "templateBand" => row is { FamilyHash: null, TemplateHash.Length: > 0 and <= 64, BandIndex: >= 0,
            PlayfieldId: null, AuthoredIndex: null, AuthoredSignature: null },
        "playfield" => row is { FamilyHash: null, TemplateHash: null, BandIndex: null, PlayfieldId: > 0,
            AuthoredIndex: null, AuthoredSignature: null },
        "authoredPoint" => row is { FamilyHash: null, TemplateHash: null, BandIndex: null, PlayfieldId: > 0,
            AuthoredIndex: >= 0, AuthoredSignature.Length: 64 },
        _ => false
    };

    static bool ValidMerge(
        NamedLootAssignment row, IReadOnlyDictionary<string, NamedLootTable> tables, HashSet<string> groups)
    {
        return row.Merge switch
        {
            "append" => row.TableId != null && tables.ContainsKey(row.TableId) && row.TargetGroup == null && row.SourceGroupId == null,
            "replace" => row.TableId != null && row.SourceGroupId != null && row.TargetGroup != null
                && groups.Contains(row.TargetGroup) && groups.Contains(NamedLootRegistry.QualifiedGroup(row.TableId, row.SourceGroupId)),
            "suppress" => row.TableId == null && row.TargetGroup != null && groups.Contains(row.TargetGroup) && row.SourceGroupId == null,
            _ => false
        };
    }

    static bool MayMatchSameContext(NamedLootAssignment a, NamedLootAssignment b)
    {
        int? firstField = a.PlayfieldId ?? a.Condition?.PlayfieldId;
        int? secondField = b.PlayfieldId ?? b.Condition?.PlayfieldId;
        if (firstField.HasValue && secondField.HasValue && firstField != secondField)
            return false;
        if (a.TemplateHash != null && b.TemplateHash != null && a.TemplateHash != b.TemplateHash)
            return false;
        if (a.BandIndex.HasValue && b.BandIndex.HasValue && a.BandIndex != b.BandIndex)
            return false;
        if (a.AuthoredIndex.HasValue && b.AuthoredIndex.HasValue && a.AuthoredIndex != b.AuthoredIndex)
            return false;
        return Math.Max(a.Condition?.MinimumLevel ?? 1, b.Condition?.MinimumLevel ?? 1)
            <= Math.Min(a.Condition?.MaximumLevel ?? int.MaxValue, b.Condition?.MaximumLevel ?? int.MaxValue);
    }
}
