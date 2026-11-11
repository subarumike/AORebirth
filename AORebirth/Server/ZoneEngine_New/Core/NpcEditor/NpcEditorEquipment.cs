namespace ZoneEngine_New.Core.NpcEditor;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;

using ZoneEngine_New.Core.Entities;
using ZoneEngine_New.Core.GameData;
using ZoneEngine_New.Core.Inventory;

internal sealed record NpcEditorEquipmentTarget(
    string TemplateHash, int BandIndex, string Signature, string? UnavailableReason, int? EstimatedAffectedNpcs = null);

internal sealed record NpcEditorEquipmentSave(
    [property: JsonRequired] int ExpectedRevision,
    [property: JsonRequired] int Index,
    [property: JsonRequired] int ExpectedLowId,
    [property: JsonRequired] int ExpectedHighId,
    [property: JsonRequired] int? ItemId,
    int? Quality);

internal sealed record NpcEditorEquipmentRestore(
    [property: JsonRequired] int ExpectedRevision, [property: JsonRequired] int Revision);

internal sealed record NpcEditorEquipmentState(
    string Scope, string Definition, string TemplateHash, int? BandLevel, int? EstimatedAffectedNpcs,
    string? UnavailableReason, int Revision, bool CanAddArmor, NpcEditorEquipmentRow[] Entries,
    NpcEditorEquipmentRevisionView[] History);

internal sealed record NpcEditorEquipmentRow(
    int Index, int LowId, int HighId, string Name, string Role, int DefaultPosition, bool Replaceable, bool Removable);

internal sealed record NpcEditorEquipmentRevisionView(
    int Revision, DateTimeOffset AtUtc, string Actor, string Action,
    NpcEditorEquipmentRow[] Before, NpcEditorEquipmentRow[] After);

internal sealed class NpcEditorEquipment(GameDataStore gameData, IItemTemplateCatalog catalog)
{
    internal NpcEditorEquipmentState Describe(NpcEditorEquipmentTarget target)
        => View(target, gameData.GetEditorEquipment(target));

    internal NpcEditorEquipmentState Save(NpcEditorEquipmentTarget target, NpcEditorEquipmentSave edit, string actor)
        => View(target, gameData.ChangeEditorEquipment(target, edit.ExpectedRevision, actor, "save", catalog,
            (band, current) => ChangeEntries(band, current.Entries, edit)));

    internal NpcEditorEquipmentState Restore(NpcEditorEquipmentTarget target, NpcEditorEquipmentRestore edit, string actor)
        => View(target, gameData.ChangeEditorEquipment(target, edit.ExpectedRevision, actor, "restore", catalog,
            (band, current) =>
            {
                if (edit.Revision < 0 || edit.Revision > current.Revision)
                    throw new NpcEditorRuntimeException(400, "Select a saved equipment revision or the authored baseline.");
                return edit.Revision == 0 ? NpcEditorEquipmentJournal.Authored(band) : current.History[edit.Revision - 1].Entries;
            }));

    NpcEditorEquipmentEntry[] ChangeEntries(
        NpcLevelBand band, NpcEditorEquipmentEntry[] entries, NpcEditorEquipmentSave edit)
    {
        if (edit.ItemId is int id && (!catalog.TryGet(id, out ItemTemplate template) || template.Quality <= 0
            || (edit.Quality.HasValue && template.Quality != edit.Quality)))
            throw new NpcEditorRuntimeException(400, "Select an existing item at its native catalog quality.");
        if (edit.Index == -1)
        {
            if (edit.ItemId == null || edit.ExpectedLowId != 0 || edit.ExpectedHighId != 0)
                throw new NpcEditorRuntimeException(400, "Choose an armor item to add to this configuration.");
            return [.. entries, new(null, edit.ItemId)];
        }
        if (edit.Index < 0 || edit.Index >= entries.Length)
            throw new NpcEditorRuntimeException(409, "The configured equipment row changed. Refresh before saving.");

        NpcEditorEquipmentEntry previous = entries[edit.Index];
        (int lowId, int highId) = NpcEditorEquipmentPolicy.ReadIds(band, previous);
        if (edit.ExpectedLowId != lowId || edit.ExpectedHighId != highId)
            throw new NpcEditorRuntimeException(409, "The configured equipment row changed. Refresh before saving.");
        if (!NpcEditorEquipmentPolicy.Classify(catalog, lowId, highId).Editable)
            throw new NpcEditorRuntimeException(400, "This configured equipment role is read-only.");

        var edited = new List<NpcEditorEquipmentEntry>(entries);
        if (edit.ItemId != null)
        {
            if (edit.ItemId == lowId && edit.ItemId == highId)
                throw new NpcEditorRuntimeException(400, "Select a different item.");
            edited[edit.Index] = previous with { ItemId = edit.ItemId };
        }
        else
        {
            if (edit.Quality.HasValue)
                throw new NpcEditorRuntimeException(400, "Remove requires an empty replacement item and quality.");
            edited.RemoveAt(edit.Index);
        }
        return edited.ToArray();
    }

    NpcEditorEquipmentState View(NpcEditorEquipmentTarget target, NpcEditorEquipmentSnapshot snapshot)
    {
        string? unavailable = snapshot.UnavailableReason;
        if (snapshot.Band is not NpcLevelBand band)
            return new("templateBand", string.Empty, target.TemplateHash, null, null, unavailable, 0, false, [], []);
        if (unavailable == null && snapshot.Revision > 0
            && !NpcEditorEquipmentPolicy.TryResolve(band, snapshot.Entries, catalog, out _, out string? error))
            unavailable = error;

        NpcEditorEquipmentRow[] initial = Rows(band, NpcEditorEquipmentJournal.Authored(band), editable: false);
        NpcEditorEquipmentRow[] previous = initial;
        var history = new NpcEditorEquipmentRevisionView[snapshot.History.Length];
        for (int i = 0; i < snapshot.History.Length; i++)
        {
            NpcEditorEquipmentRevision revision = snapshot.History[i];
            NpcEditorEquipmentRow[] after = Rows(band, revision.Entries, editable: false);
            history[i] = new(revision.Revision, revision.AtUtc, revision.Actor, revision.Action, previous, after);
            previous = after;
        }

        string definition = $"NpcTemplates.json / {target.TemplateHash} / band {target.BandIndex + 1} (level {band.Level})";
        return new("templateBand", definition, target.TemplateHash, band.Level, target.EstimatedAffectedNpcs, unavailable,
            snapshot.Revision, unavailable == null && snapshot.Entries.Length < NpcCharacter.EquipmentCapacity,
            Rows(band, snapshot.Entries, unavailable == null), history);
    }

    NpcEditorEquipmentRow[] Rows(NpcLevelBand band, NpcEditorEquipmentEntry[] entries, bool editable)
        => entries.Select((entry, index) =>
        {
            (int lowId, int highId) = NpcEditorEquipmentPolicy.ReadIds(band, entry);
            NpcEditorEquipmentPolicy.Role role = NpcEditorEquipmentPolicy.Classify(catalog, lowId, highId);
            bool canEdit = editable && role.Editable;
            string name = catalog.TryGet(lowId, out ItemTemplate item) ? item.Name : string.Empty;
            return new NpcEditorEquipmentRow(index, lowId, highId, name, role.Description, role.Position, canEdit, canEdit);
        }).ToArray();
}
