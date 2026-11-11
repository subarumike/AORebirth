namespace ZoneEngine_New.Core.GameData;

using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Applies the existing fixture-domain resolver's scope specificity and priority ordering to authored runtime tables.
/// The native corpse generator consumes the same immutable projection exposed to the editor.
/// </summary>
internal sealed class NamedLootRegistry
{
    internal static readonly NamedLootRegistry Empty = new([], []);
    readonly Dictionary<string, NamedLootTable> _tables;
    readonly NamedLootAssignment[] _ordered;

    internal NamedLootRegistry(NamedLootTable[] tables, NamedLootAssignment[] assignments)
    {
        _tables = tables.ToDictionary(table => table.Id, StringComparer.Ordinal);
        _ordered = assignments.OrderBy(assignment => Specificity(assignment.Scope))
            .ThenBy(assignment => assignment.Priority).ThenBy(assignment => assignment.Id, StringComparer.Ordinal).ToArray();
        NeedsFamilies = _ordered.Any(assignment => assignment.Scope == "family");
        NeedsAuthoredPoint = _ordered.Any(assignment => assignment.Scope == "authoredPoint");
    }

    internal bool HasAssignments => _ordered.Length > 0;
    internal bool NeedsFamilies { get; }
    internal bool NeedsAuthoredPoint { get; }

    internal static bool Applies(NamedLootAssignment assignment, NamedLootContext context)
        => !context.IsOwnedSummon && Matches(assignment, context) && assignment.Condition?.Matches(context) != false;

    internal NamedLootResolution Resolve(NamedLootContext context)
    {
        if (!HasAssignments || context.IsOwnedSummon)
            return new(context, [], []);

        var groups = new Dictionary<string, NamedLootResolvedGroup>(StringComparer.Ordinal);
        var order = new List<string>();
        var trace = new List<NamedLootResolutionNote>();
        foreach (NamedLootAssignment assignment in _ordered)
        {
            if (!Matches(assignment, context))
                continue;
            if (assignment.Condition?.Matches(context) == false)
            {
                AddNote(assignment, "ineligible", assignment.TargetGroup, "The assignment condition did not match.");
                continue;
            }

            if (assignment.Merge == "append")
            {
                NamedLootTable table = _tables[assignment.TableId!];
                foreach (NamedLootGroup group in table.Groups)
                {
                    string id = QualifiedGroup(table.Id, group.Id);
                    if (group.Condition?.Matches(context) == false)
                    {
                        AddNote(assignment, "ineligible", id, "The group condition did not match.");
                        continue;
                    }
                    if (groups.TryAdd(id, new(id, table.Id, assignment.Id, group)))
                    {
                        order.Add(id);
                        AddNote(assignment, "append", id, "Added by matching " + assignment.Scope + " assignment.");
                    }
                    else
                        AddNote(assignment, "conflict", id, "An earlier assignment already supplied this group.");
                }
                continue;
            }

            string target = assignment.TargetGroup!;
            if (!groups.ContainsKey(target))
            {
                AddNote(assignment, "ineligible", target, "The inherited target group was not selected.");
                continue;
            }
            if (assignment.Merge == "suppress")
            {
                groups.Remove(target);
                order.Remove(target);
                AddNote(assignment, "suppress", target, "Suppressed by matching " + assignment.Scope + " assignment.");
                continue;
            }

            NamedLootTable source = _tables[assignment.TableId!];
            NamedLootGroup replacement = source.Groups.Single(group => group.Id == assignment.SourceGroupId);
            if (replacement.Condition?.Matches(context) == false)
            {
                AddNote(assignment, "ineligible", target, "The replacement group condition did not match.");
                continue;
            }
            groups[target] = new(target, source.Id, assignment.Id, replacement);
            AddNote(assignment, "replace", target, "Replaced with " + QualifiedGroup(source.Id, replacement.Id) + ".");
        }

        return new(context, order.Select(id => groups[id]).ToArray(), trace.ToArray());

        void AddNote(NamedLootAssignment assignment, string action, string? id, string reason)
            => trace.Add(new(assignment.Id, assignment.Scope, assignment.TableId, action, id, reason));
    }

    internal static string QualifiedGroup(string tableId, string groupId) => tableId + "/" + groupId;

    internal static int Specificity(string scope) => scope switch
    {
        "global" => 0,
        "family" => 10,
        "template" => 20,
        "templateBand" => 22,
        "playfield" => 25,
        "namedBoss" => 50,
        "authoredPoint" => 60,
        _ => -1
    };

    static bool Matches(NamedLootAssignment assignment, NamedLootContext context) => assignment.Scope switch
    {
        "global" => true,
        "family" => context.FamilyHashes?.Contains(assignment.FamilyHash!, StringComparer.Ordinal) == true,
        "template" => assignment.TemplateHash == context.TemplateHash,
        "templateBand" => assignment.TemplateHash == context.TemplateHash && assignment.BandIndex == context.BandIndex,
        "playfield" => assignment.PlayfieldId == context.PlayfieldId,
        "namedBoss" => context.IsBoss && assignment.TemplateHash == context.TemplateHash,
        "authoredPoint" => assignment.PlayfieldId == context.PlayfieldId && assignment.AuthoredIndex == context.AuthoredIndex
            && assignment.AuthoredSignature == context.AuthoredSignature,
        _ => false
    };
}
