namespace ZoneEngine_New.Core.GameData;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

using ZoneEngine_New.Core.Inventory;
using ZoneEngine_New.Core.NpcEditor;

internal sealed record NpcEditorEquipmentSnapshot(
    NpcLevelBand? Band, string? UnavailableReason, NpcEditorEquipmentRevision[] History)
{
    internal int Revision => History.Length;
    internal NpcEditorEquipmentEntry[] Entries => History.Length > 0
        ? History[^1].Entries
        : Band == null ? [] : NpcEditorEquipmentJournal.Authored(Band);
}

public sealed partial class GameDataStore
{
    readonly NpcEditorEquipmentJournalFile _editorEquipmentFile;

    NpcTemplateCatalog WithLoadedEditorEquipment(NpcTemplateCatalog catalog)
    {
        try
        {
            return catalog.WithEditorEquipment(_editorEquipmentFile.Read(catalog));
        }
        catch (Exception error) when (error is IOException or InvalidDataException or UnauthorizedAccessException or JsonException)
        {
            const string reason = "The NPC equipment overlay failed validation. See the ZoneEngine log and reload NpcTemplates after repair.";
            _logger.Error(error, reason);
            return catalog.WithEditorEquipment(_npcTemplates.EditorEquipment with { LoadError = reason });
        }
    }

    internal NpcEditorEquipmentSnapshot GetEditorEquipment(NpcEditorEquipmentTarget target)
        => GetEditorEquipment(_npcTemplates, target);

    NpcEditorEquipmentSnapshot GetEditorEquipment(NpcTemplateCatalog catalog, NpcEditorEquipmentTarget target)
    {
        if (!catalog.TryGetEquipmentBand(target.TemplateHash, target.BandIndex, out NpcLevelBand band))
            return new(null, target.UnavailableReason ?? "Select an NPC with an authored template band.", []);
        string? unavailable = target.UnavailableReason ?? _editorEquipmentFile.ConfigurationProblem ?? catalog.EditorEquipment.LoadError;
        if (target.Signature != NpcEditorEquipmentJournalFile.Signature(band))
            return new(band, unavailable ?? "The authored NPC band changed. Select a fresh spawn to edit its equipment.", []);
        NpcEditorEquipmentHistory? history = catalog.EditorEquipment.Find(target.TemplateHash, target.BandIndex);
        return new(band, unavailable, history?.Signature == target.Signature ? history.Revisions : []);
    }

    internal NpcEditorEquipmentSnapshot ChangeEditorEquipment(
        NpcEditorEquipmentTarget target, int expectedRevision, string actor, string action,
        IItemTemplateCatalog items, Func<NpcLevelBand, NpcEditorEquipmentSnapshot, NpcEditorEquipmentEntry[]> edit)
    {
        if (string.IsNullOrWhiteSpace(actor) || actor.Length > 128)
            throw new ArgumentException("A valid GM actor is required.", nameof(actor));

        lock (_lootCatalogSync)
        {
            NpcTemplateCatalog catalog = _npcTemplates;
            NpcEditorEquipmentSnapshot current = GetEditorEquipment(catalog, target);
            if (current.Band is not NpcLevelBand band || current.UnavailableReason != null)
                throw new NpcEditorRuntimeException(409, current.UnavailableReason ?? "The authored NPC band is unavailable.");
            if (current.Revision != expectedRevision)
                throw new NpcEditorRuntimeException(409, "The equipment revision changed. Refresh before saving.");
            if (current.Revision >= NpcEditorEquipmentJournalFile.MaxRevisionsPerBand)
                throw new NpcEditorRuntimeException(409, "The equipment history reached its revision limit.");

            NpcEditorEquipmentEntry[] edited = edit(band, current);
            if (current.Entries.SequenceEqual(edited))
                throw new NpcEditorRuntimeException(400, "The equipment already matches that configuration.");
            if (!NpcEditorEquipmentPolicy.TryResolve(band, edited, items, out _, out string? error))
                throw new NpcEditorRuntimeException(400, error!);

            string key = NpcEditorEquipmentJournal.Key(target.TemplateHash, target.BandIndex);
            NpcEditorEquipmentJournal existing = catalog.EditorEquipment;
            var bands = new Dictionary<string, NpcEditorEquipmentHistory>(existing.Bands, StringComparer.Ordinal)
            {
                [key] = new(target.TemplateHash, target.BandIndex, target.Signature,
                    [.. current.History, new(current.Revision + 1, DateTimeOffset.UtcNow, actor, action, edited)])
            };
            NpcEditorEquipmentJournal updated = _editorEquipmentFile.Write(existing, existing with { Bands = bands });
            NpcTemplateCatalog revised = catalog.WithEditorEquipment(updated);
            _npcTemplates = revised;
            return GetEditorEquipment(revised, target);
        }
    }
}
