namespace ZoneEngine_New.Core.NpcEditor;

using System;
using System.Globalization;
using System.Linq;
using System.Text.Json.Serialization;

using SmokeLounge.AOtomation.Messaging.GameData;

using ZoneEngine_New.Core.Entities;
using ZoneEngine_New.Core.GameData;
using ZoneEngine_New.Core.Inventory;

internal sealed record NamedLootTableSave(
    [property: JsonRequired] int ExpectedRevision, [property: JsonRequired] NamedLootTable Table);

internal sealed record NamedLootAssignmentSave(
    [property: JsonRequired] int ExpectedRevision, [property: JsonRequired] NamedLootAssignment Assignment);

internal sealed record NamedLootDelete(
    [property: JsonRequired] int ExpectedRevision, [property: JsonRequired] string Id);

internal sealed record NamedLootRestore(
    [property: JsonRequired] int ExpectedRevision, [property: JsonRequired] int Revision);

internal sealed class NpcEditorNamedLoot
{
    readonly GameDataStore _data;

    internal NpcEditorNamedLoot(GameDataStore data, IItemTemplateCatalog items)
    {
        _data = data;
        _data.InitializeNamedLoot(items);
    }

    internal NamedLootRegistryState Describe() => _data.DescribeNamedLoot();
    internal NamedLootResolution DescribeContext(NpcCharacter npc) => _data.ResolveNamedLoot(npc, includeAuthoringContext: true);
    internal static NamedLootContext CaptureContext(NpcCharacter npc)
        => npc.Playfield?.GetService<IGameData>() is GameDataStore data
            ? data.CaptureNamedLootContext(npc)
            : new(npc.Stats.GetOrOne(CharacterStat.Level), npc.Playfield?.ResourceId ?? 0,
                IsOwnedSummon: npc.Pet != null || npc.SpawnSource == SpawnSource.Summoned);

    internal NamedLootRegistryState SaveTable(NamedLootTableSave edit, string actor)
    {
        if (edit.Table == null)
            throw new NpcEditorRuntimeException(400, "Supply a named loot table.");
        return _data.ChangeNamedLoot(edit.ExpectedRevision, actor, "saveTable", edit.Table.Id, journal =>
        {
            var latest = journal.Latest;
            NamedLootTable[] tables = [.. (latest?.Tables ?? []).Where(table => table.Id != edit.Table.Id), edit.Table];
            return (tables.OrderBy(table => table.Id, StringComparer.Ordinal).ToArray(), latest?.Assignments ?? []);
        });
    }

    internal NamedLootRegistryState DeleteTable(NamedLootDelete edit, string actor)
        => _data.ChangeNamedLoot(edit.ExpectedRevision, actor, "deleteTable", edit.Id, journal =>
        {
            NamedLootRevision? latest = journal.Latest;
            if (latest == null || !latest.Tables.Any(table => table.Id == edit.Id))
                throw new NpcEditorRuntimeException(404, "The named loot table is unavailable.");
            if (latest.Assignments.Any(row => row.TableId == edit.Id
                || row.TargetGroup?.StartsWith(edit.Id + "/", StringComparison.Ordinal) == true))
                throw new NpcEditorRuntimeException(409, "Remove or reassign active references before deleting this table.");
            return (latest.Tables.Where(table => table.Id != edit.Id).ToArray(), latest.Assignments);
        });

    internal NamedLootRegistryState SaveAssignment(NamedLootAssignmentSave edit, string actor)
    {
        if (edit.Assignment == null)
            throw new NpcEditorRuntimeException(400, "Supply a named loot assignment.");
        return _data.ChangeNamedLoot(edit.ExpectedRevision, actor, "saveAssignment", edit.Assignment.Id,
            journal => (journal.Latest?.Tables ?? [], UpsertAssignment(journal, edit.Assignment)));
    }

    internal NamedLootAssignment PreviewAssignment(NamedLootAssignmentSave edit)
    {
        if (edit.Assignment == null)
            throw new NpcEditorRuntimeException(400, "Supply a named loot assignment.");
        _data.PreviewNamedLoot(edit.ExpectedRevision, journal => UpsertAssignment(journal, edit.Assignment));
        return edit.Assignment;
    }

    internal NamedLootRegistryState DeleteAssignment(NamedLootDelete edit, string actor)
        => _data.ChangeNamedLoot(edit.ExpectedRevision, actor, "deleteAssignment", edit.Id, journal =>
        {
            NamedLootRevision? latest = journal.Latest;
            if (latest == null || !latest.Assignments.Any(row => row.Id == edit.Id))
                throw new NpcEditorRuntimeException(404, "The named loot assignment is unavailable.");
            return (latest.Tables, latest.Assignments.Where(row => row.Id != edit.Id).ToArray());
        });

    internal NamedLootRegistryState Restore(NamedLootRestore edit, string actor)
        => _data.ChangeNamedLoot(edit.ExpectedRevision, actor, "restore", edit.Revision.ToString(CultureInfo.InvariantCulture), journal =>
        {
            if (edit.Revision < 0 || edit.Revision >= journal.Revisions.Length)
                throw new NpcEditorRuntimeException(400, "Select an earlier named loot revision or revision zero.");
            NamedLootRevision? previous = edit.Revision > 0 ? journal.Revisions[edit.Revision - 1] : null;
            return (previous?.Tables ?? [], previous?.Assignments ?? []);
        });

    static NamedLootAssignment[] UpsertAssignment(NamedLootJournal journal, NamedLootAssignment assignment)
        => (journal.Latest?.Assignments ?? []).Where(row => row.Id != assignment.Id).Append(assignment)
            .OrderBy(row => row.Id, StringComparer.Ordinal).ToArray();
}
