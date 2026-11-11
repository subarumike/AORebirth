namespace ZoneEngine_New.Core.NpcEditor;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;

using SmokeLounge.AOtomation.Messaging.GameData;

using ZoneEngine_New.Core.Entities;
using ZoneEngine_New.Core.GameData;

internal sealed record NpcEditorStatsTarget(
    string TemplateHash, int BandIndex, string Signature, string? UnavailableReason, int? EstimatedAffectedNpcs = null,
    string Scope = "templateBand", string? FamilyHash = null, int? ResourcePlayfieldId = null, string? ScopeSignature = null,
    string[]? FamilyLeaves = null);

internal sealed record NpcEditorStatsSave(
    [property: JsonRequired] int ExpectedRevision,
    [property: JsonRequired] int StatId,
    [property: JsonRequired] int? Value);

internal sealed record NpcEditorStatsRestore(
    [property: JsonRequired] int ExpectedRevision, [property: JsonRequired] int Revision);

internal sealed record NpcEditorStatsState(
    string Scope, string Definition, string TemplateHash, int BandIndex, int? BandLevel, int? EstimatedAffectedNpcs,
    bool FutureSpawnsInherit, bool RequiresRespawn, string[] Overlaps, string? UnavailableReason,
    int Revision, NpcEditorConfiguredStatRow[] Rows, NpcEditorStatsRevisionView[] History,
    string? FamilyHash = null, int? ResourcePlayfieldId = null);

internal sealed record NpcEditorConfiguredStatRow(
    int StatId, string Name, int? AuthoredValue, int? ConfiguredValue, int? OverrideValue, string Source, bool Editable);

internal sealed record NpcEditorStatsValue(int StatId, int Value);

internal sealed record NpcEditorStatsRevisionView(
    int Revision, DateTimeOffset AtUtc, string Actor, string Action, NpcEditorStatsValue[] Before, NpcEditorStatsValue[] After);

internal sealed class NpcEditorStats(GameDataStore data)
{
    internal NpcEditorStatsState Describe(NpcEditorStatsTarget target) => View(target, data.GetEditorStats(target));

    internal NpcEditorStatsState Save(NpcEditorStatsTarget target, NpcEditorStatsSave edit, string actor)
        => View(target, data.ChangeEditorStats(target, edit.ExpectedRevision, actor, "save", current =>
        {
            if (!NpcEditorStatPolicy.Supports((CharacterStat)edit.StatId))
                throw new NpcEditorRuntimeException(400, "That stat does not support persistent editing.");
            var values = new Dictionary<int, int>(current.Values);
            if (edit.Value is int value)
                values[edit.StatId] = value;
            else
                values.Remove(edit.StatId);
            return values;
        }));

    internal NpcEditorStatsState Restore(NpcEditorStatsTarget target, NpcEditorStatsRestore edit, string actor)
        => View(target, data.ChangeEditorStats(target, edit.ExpectedRevision, actor, "restore", current =>
        {
            if (edit.Revision < 0 || edit.Revision > current.Revision)
                throw new NpcEditorRuntimeException(400, "Select a saved stat revision or the authored baseline.");
            return edit.Revision == 0 ? new() : new(current.History[edit.Revision - 1].Values);
        }));

    static NpcEditorStatsState View(NpcEditorStatsTarget target, NpcEditorStatsSnapshot snapshot)
    {
        if (snapshot.Band is not NpcLevelBand band)
            return new(target.Scope, string.Empty, target.TemplateHash, target.BandIndex, null, null, false, false, [],
                snapshot.UnavailableReason, 0, [], [], target.FamilyHash, target.ResourcePlayfieldId);

        Dictionary<int, int> authored = band.Stats ?? new();
        IReadOnlyDictionary<int, int> configured = snapshot.Effective ?? authored;
        string bandDefinition = $"{target.TemplateHash} / band {target.BandIndex + 1} (level {band.Level})";
        string definition = target.Scope switch
        {
            "family" => $"NpcTemplates.json / family {target.FamilyHash} / {target.FamilyLeaves?.Length ?? 0} member templates",
            "playfield" => $"Resource playfield {target.ResourcePlayfieldId} / {bandDefinition}",
            _ => $"NpcTemplates.json / {bandDefinition}"
        };
        NpcEditorConfiguredStatRow[] rows = authored.Keys
            .Union(NpcEditorStatPolicy.EditableStats.Select(stat => (int)stat)).Union(configured.Keys).Order()
            .Select(id => new NpcEditorConfiguredStatRow(id, ((CharacterStat)id).ToString(), Read(authored, id),
                Read(configured, id), Read(snapshot.Values, id), snapshot.Sources?.GetValueOrDefault(id)
                    ?? (authored.ContainsKey(id) ? "Authored NPC template band" : "No authored value"),
                snapshot.UnavailableReason == null && (target.Scope == "templateBand" || !NpcEditorStatPolicy.IsVital((CharacterStat)id))
                    && CanEditConfigured((CharacterStat)id, configured))).ToArray();

        IReadOnlyDictionary<int, int> previousValues = new Dictionary<int, int>();
        IReadOnlyDictionary<int, int> previousConfigured = authored;
        var history = new NpcEditorStatsRevisionView[snapshot.History.Length];
        for (int i = 0; i < history.Length; i++)
        {
            NpcEditorStatsRevision revision = snapshot.History[i];
            var next = new Dictionary<int, int>(authored);
            NpcEditorStatPolicy.TryApplyConfigured(next, revision.Values, null, out _);
            int[] changed = previousConfigured.Keys.Union(next.Keys).Union(previousValues.Keys).Union(revision.Values.Keys)
                .Where(id => Read(previousConfigured, id) != Read(next, id)
                    || Read(previousValues, id) != Read(revision.Values, id)).Order().ToArray();
            history[i] = new(revision.Revision, revision.AtUtc, revision.Actor, revision.Action,
                HistoryValues(changed, previousConfigured), HistoryValues(changed, next));
            previousValues = revision.Values;
            previousConfigured = next;
        }

        return new(target.Scope, definition, target.TemplateHash, target.BandIndex, band.Level, target.EstimatedAffectedNpcs,
            true, true, snapshot.Overlaps ?? [], snapshot.UnavailableReason, snapshot.Revision, rows, history,
            target.FamilyHash, target.ResourcePlayfieldId);
    }

    static int? Read(IReadOnlyDictionary<int, int> source, int id)
        => source.TryGetValue(id, out int value) && !StatCollection.IsUnset(value) ? value : null;

    static bool CanEditConfigured(CharacterStat stat, IReadOnlyDictionary<int, int> stats)
    {
        if (!NpcEditorStatPolicy.Supports(stat))
            return false;
        int? health = Read(stats, (int)CharacterStat.Health);
        int? maxHealth = Read(stats, (int)CharacterStat.MaxHealth);
        int? nano = Read(stats, (int)CharacterStat.CurrentNano);
        int? maxNano = Read(stats, (int)CharacterStat.MaxNanoEnergy);
        return stat switch
        {
            CharacterStat.Health => maxHealth is >= 1,
            CharacterStat.CurrentNano => maxNano is >= 0,
            CharacterStat.MaxHealth => health is >= 1 && maxHealth >= health,
            CharacterStat.MaxNanoEnergy => nano is >= 0 && maxNano >= nano,
            _ => true
        };
    }

    static NpcEditorStatsValue[] HistoryValues(IEnumerable<int> ids, IReadOnlyDictionary<int, int> configured)
        => ids.Where(id => Read(configured, id).HasValue)
            .Select(id => new NpcEditorStatsValue(id, configured[id])).ToArray();
}
