namespace ZoneEngine_New.Core.NpcEditor;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.Json.Serialization;

using AORebirth.Core.GameData;
using AORebirth.Core.Vector;
using ZoneEngine_New.Core.Entities;
using ZoneEngine_New.Core.GameData;
using ZoneEngine_New.Core.Playfield;
using ZoneEngine_New.Core.Playfield.Locality;

internal sealed record NpcEditorSpawnSettings(
    [property: JsonRequired] float X, [property: JsonRequired] float Y, [property: JsonRequired] float Z,
    [property: JsonRequired] int FacingDegrees, [property: JsonRequired] int FacingWidthDegrees,
    [property: JsonRequired] int MinLevel, [property: JsonRequired] int MaxLevel,
    [property: JsonRequired] int RespawnSeconds, [property: JsonRequired] int RespawnChance,
    [property: JsonRequired] string Hash)
{
    internal static NpcEditorSpawnSettings From(PlayfieldSpawnEntry source) => new(
        source.Position[0], source.Position[1], source.Position[2], source.Angle, source.AngleW,
        source.MinLevel, source.MaxLevel, source.RespawnTime, source.RespawnChance, source.HashText);

    internal static NpcEditorSpawnSettings From(HashSpawnPoint point)
    {
        SpawnSite site = point.Sites[0];
        return new(site.Centre.xf, site.Centre.yf, site.Centre.zf, site.FacingDegrees, site.WidthDegrees,
            point.MinLevel, point.MaxLevel, point.RespawnTimeSeconds, point.RespawnChance, point.HashText);
    }
}

internal sealed record NpcEditorSpawnRevision(int Revision, string Actor, DateTimeOffset AtUtc,
    string Action, NpcEditorSpawnSettings Before, NpcEditorSpawnSettings After);

internal sealed record NpcEditorSpawnSave([property: JsonRequired] int ExpectedRevision,
    [property: JsonRequired] NpcEditorSpawnSettings Settings);

internal sealed record NpcEditorSpawnRestore([property: JsonRequired] int ExpectedRevision, [property: JsonRequired] int Revision);

internal sealed record NpcEditorSpawnPreview([property: JsonRequired] float X, [property: JsonRequired] float Y,
    [property: JsonRequired] float Z)
{
    internal static NpcEditorSpawnPreview From(Vector3 at) => new(at.xf, at.yf, at.zf);
    internal Vector3 Position() => new(X, Y, Z);
}

internal sealed record NpcEditorSpawnUndo([property: JsonRequired] Guid ChangeId);

internal sealed record NpcEditorSpawnPreviewChange(Guid ChangeId, string Actor, DateTimeOffset AtUtc, string Action,
    NpcEditorSpawnPreview Before, NpcEditorSpawnPreview After);

internal sealed record NpcEditorSpawnPreviewView(bool CanApply, string? UnavailableReason, float X, float Y, float Z,
    Guid? ChangeId, NpcEditorSpawnPreviewChange[] History);

internal sealed record NpcEditorSpawnView(int PlayfieldId, int ResourcePlayfieldId, string? SpawnPointKey, string Source,
    string? Definition, int? AuthoredIndex, int? DistrictIndex, bool CanSave, string? UnavailableReason, int Revision,
    NpcEditorSpawnSettings? Original, NpcEditorSpawnSettings? Configured, NpcEditorSpawnSettings? Runtime,
    string State, DateTime? NextRespawnUtc, int LoadedNpcs, bool RequiresPlayfieldReload, int SiteCount,
    string[] ActivationConditions, NpcEditorSpawnRevision[] History, NpcEditorSpawnPreviewView? LivePreview,
    string? AuthoredSignature = null);

internal sealed record NpcEditorSpawnList(int Offset, int Limit, int Total, NpcEditorSpawnView[] Points);

internal sealed record NpcEditorSpawnTarget(int ResourcePlayfieldId, int AuthoredIndex, string AuthoredSignature,
    CellGrid Grid, NpcEditorSpawnView View);

internal sealed record NpcEditorSpawnRespawnPlan(int ExpectedRevision, NpcEditorSpawnSettings Before, NpcEditorSpawnSettings After);

/// <summary>Edits exact authored hash-spawn placements. Live position previews are scoped to the selected NPC.</summary>
internal sealed class NpcEditorSpawn(GameDataStore gameData)
{
    const int MaxPreviewHistory = 32;
    readonly ConditionalWeakTable<NpcCharacter, PreviewState> _previews = new();

    internal NpcEditorSpawnView Describe(NpcCharacter npc)
    {
        Playfield? field = npc.Playfield;
        int playfieldId = field?.Identity.Instance ?? 0;
        int resourceId = field?.ResourceId ?? 0;
        if (field == null || !TryPoint(npc, out HashSpawnPoint point))
        {
            return new(playfieldId, resourceId, npc.SpawnPointKey, npc.SpawnSource.ToString(), null, null, null,
                false, "This NPC is not owned by an authored hash-spawn point.", 0, null, null, null,
                npc.IsDead ? "Dead" : "Alive", null, 0, false, 0, [], [], PreviewView(npc, false));
        }

        return DescribePoint(field, point, npc);
    }

    internal NpcEditorSpawnView Describe(Playfield field, int authoredIndex)
        => DescribePoint(field, RequirePoint(field, authoredIndex), null);

    internal NpcEditorSpawnList List(Playfield field, int offset, int limit)
    {
        if (offset < 0 || limit is < 1 or > 200)
            throw new NpcEditorRuntimeException(400, "Use a nonnegative spawn offset and a page size from 1 to 200.");
        HashSpawnPoint[] points = field.GetService<HashSpawnSystem>()?.AuthoredNpcPoints().ToArray() ?? [];
        return new(offset, limit, points.Length, points.Skip(offset).Take(limit)
            .Select(point => DescribePoint(field, point, null, includeHistory: false)).ToArray());
    }

    NpcEditorSpawnView DescribePoint(Playfield field, HashSpawnPoint point, NpcCharacter? npc, bool includeHistory = true)
    {
        int resourceId = field.ResourceId;

        bool authored = IsEditable(npc, point);
        string? authoredSignature = authored ? NpcEditorSpawnStore.Signature(point.Source) : null;
        NpcEditorSpawnSettings runtime = NpcEditorSpawnSettings.From(point);
        NpcEditorSpawnSettings original = authored ? NpcEditorSpawnSettings.From(point.Source) : runtime;
        (NpcEditorSpawnRevision[] history, string? unavailable) = authored
            ? gameData.SpawnEditor.Describe(resourceId, point.AuthoredSpawnIndex, authoredSignature!)
            : ([], "This point comes from a generated, static, or scripted placement outside the authored spawn editor scope.");
        NpcEditorSpawnSettings configured = history.Length == 0 ? original : history[^1].After;
        string? definition = authored
            ? $"{GameDataPaths.PlayfieldSpawnsRelativePath(resourceId).Replace('\\', '/')} / entry {point.AuthoredSpawnIndex}"
            : null;
        return new(field.Identity.Instance, resourceId, point.Key, npc?.SpawnSource.ToString() ?? "HashSpawn", definition,
            authored ? point.AuthoredSpawnIndex : null, point.Source.DistrictIndex, unavailable == null, unavailable,
            history.Length, original, configured, runtime, point.Retired ? "Retired" : point.State.ToString(),
            point.State == HashSpawnState.Dead && !point.Retired ? point.NextSpawnTime : null,
            point.MemberCount, configured != runtime, point.Sites.Length, Conditions(point),
            includeHistory ? (NpcEditorSpawnRevision[])history.Clone() : [], npc == null ? null : PreviewView(npc, authored),
            authoredSignature);
    }

    internal NpcEditorSpawnTarget CaptureTarget(NpcCharacter npc)
    {
        NpcEditorSpawnView view = Describe(npc);
        if (!TryPoint(npc, out HashSpawnPoint point) || npc.Playfield is not Playfield field)
            throw new NpcEditorRuntimeException(409, view.UnavailableReason ?? "The authored spawn is unavailable.");
        return CaptureTarget(field, point, view);
    }

    internal NpcEditorSpawnTarget CaptureTarget(Playfield field, int authoredIndex)
    {
        HashSpawnPoint point = RequirePoint(field, authoredIndex);
        return CaptureTarget(field, point, DescribePoint(field, point, null));
    }

    static NpcEditorSpawnTarget CaptureTarget(Playfield field, HashSpawnPoint point, NpcEditorSpawnView view)
    {
        if (!view.CanSave || field.GetService<PlayfieldLocality>()?.Grid is not CellGrid grid)
        {
            throw new NpcEditorRuntimeException(409, view.UnavailableReason ?? "The authored spawn is unavailable.");
        }

        return new(view.ResourcePlayfieldId, point.AuthoredSpawnIndex, view.AuthoredSignature!, grid, view);
    }

    static HashSpawnPoint RequirePoint(Playfield field, int index)
        => field.GetService<HashSpawnSystem>()?.FindAuthoredNpcPoint(index)
            ?? throw new NpcEditorRuntimeException(404, "No authored NPC spawn point with that index is loaded in this playfield.");

    internal NpcEditorSpawnView Save(NpcEditorSpawnTarget target, NpcEditorSpawnSave request, string actor)
        => Change(target, request.ExpectedRevision, actor, request.Settings, null);

    internal NpcEditorSpawnView Restore(NpcEditorSpawnTarget target, NpcEditorSpawnRestore request, string actor)
        => Change(target, request.ExpectedRevision, actor, null, request.Revision);

    internal NpcEditorSpawnRespawnPlan PrepareRespawn(NpcEditorSpawnTarget target, double? respawnSeconds, double? respawnChance)
    {
        NpcEditorSpawnSettings before = target.View.Configured
            ?? throw new NpcEditorRuntimeException(409, "The configured spawn is unavailable.");
        var after = before with
        {
            RespawnSeconds = WholeNumber(respawnSeconds, before.RespawnSeconds, "Respawn seconds"),
            RespawnChance = WholeNumber(respawnChance, before.RespawnChance, "Respawn chance")
        };
        gameData.SpawnEditor.ValidateChange(target, before, after);
        return new(target.View.Revision, before, after);
    }

    static int WholeNumber(double? value, int fallback, string field)
    {
        if (value is not double number)
            return fallback;
        if (!double.IsFinite(number) || number < 0 || number > int.MaxValue || number != Math.Truncate(number))
            throw new NpcEditorRuntimeException(400, field + " must be a nonnegative whole number.");
        return (int)number;
    }

    NpcEditorSpawnView Change(NpcEditorSpawnTarget target, int expectedRevision, string actor,
        NpcEditorSpawnSettings? settings, int? restore)
    {
        NpcEditorSpawnRevision[] history = gameData.SpawnEditor.Change(target, expectedRevision, actor, settings, restore);
        NpcEditorSpawnSettings configured = history[^1].After;
        return target.View with
        {
            Revision = history.Length,
            Configured = configured,
            RequiresPlayfieldReload = configured != target.View.Runtime,
            History = (NpcEditorSpawnRevision[])history.Clone()
        };
    }

    internal NpcEditorSpawnView Preview(NpcCharacter npc, NpcEditorSpawnPreview destination, string actor)
    {
        RequirePreview(npc, destination, actor);
        NpcEditorSpawnPreview before = NpcEditorSpawnPreview.From(npc.Position);
        npc.Brain!.WarpTo(destination.Position());
        NpcEditorSpawnPreview after = NpcEditorSpawnPreview.From(npc.Position);
        var change = new NpcEditorSpawnPreviewChange(Guid.NewGuid(), actor, DateTimeOffset.UtcNow, "preview", before, after);
        PreviewState state = _previews.GetValue(npc, static _ => new());
        state.Undo = change;
        state.Record(change);
        return Describe(npc);
    }

    internal NpcEditorSpawnView UndoPreview(NpcCharacter npc, NpcEditorSpawnUndo request, string actor)
    {
        if (!_previews.TryGetValue(npc, out PreviewState? state) || state.Undo is not { } change
            || change.ChangeId != request.ChangeId || change.After != NpcEditorSpawnPreview.From(npc.Position))
        {
            throw new NpcEditorRuntimeException(409, "The selected NPC moved or the preview was replaced. Refresh before trying again.");
        }

        RequirePreview(npc, change.Before, actor);
        npc.Brain!.WarpTo(change.Before.Position());
        state.Undo = null;
        state.Record(new(Guid.NewGuid(), actor, DateTimeOffset.UtcNow, "undo", change.After, NpcEditorSpawnPreview.From(npc.Position)));
        return Describe(npc);
    }

    static void RequirePreview(NpcCharacter npc, NpcEditorSpawnPreview destination, string actor)
    {
        if (string.IsNullOrWhiteSpace(actor) || actor.Length > 128)
            throw new NpcEditorRuntimeException(400, "A valid GM actor is required.");
        if (!TryPoint(npc, out HashSpawnPoint point) || PreviewProblem(npc, IsEditable(npc, point)) != null)
            throw new NpcEditorRuntimeException(409, "Preview requires an idle, stationary NPC with a native brain and an authored spawn.");
        npc.Playfield!.RequireTick();
        if (!float.IsFinite(destination.X) || !float.IsFinite(destination.Y) || !float.IsFinite(destination.Z))
            throw new NpcEditorRuntimeException(400, "Preview coordinates must be finite.");
        Vector3 position = destination.Position();
        if (npc.Playfield?.GetService<PlayfieldLocality>()?.Grid is not CellGrid grid
            || !grid.ContainsWorldPosition(position.xf, position.zf)
            || !grid.TryGetCellId(position, out _, clampToGrid: false) || !npc.Brain!.CanWalkTo(position))
            throw new NpcEditorRuntimeException(400, "The preview destination is outside the reachable playfield space.");
    }

    NpcEditorSpawnPreviewView PreviewView(NpcCharacter npc, bool authored)
    {
        string? problem = PreviewProblem(npc, authored);
        NpcEditorSpawnPreview position = NpcEditorSpawnPreview.From(npc.Position);
        _previews.TryGetValue(npc, out PreviewState? state);
        Guid? undo = problem == null && state?.Undo is { } change && change.After == position ? change.ChangeId : null;
        return new(problem == null, problem, position.X, position.Y, position.Z, undo, state?.History.ToArray() ?? []);
    }

    static string? PreviewProblem(NpcCharacter npc, bool authored)
    {
        if (!authored || npc.Brain == null || npc.Scripts != null)
            return "Live position preview requires an authored spawn and an NPC brain without a running script.";
        if (npc.IsDead || npc.IsInCombat || npc.IsAiBusy || npc.Motor.HasPath)
            return "Wait for the NPC to stop combat or movement before previewing a position.";
        return null;
    }

    static bool TryPoint(NpcCharacter npc, out HashSpawnPoint point)
    {
        point = null!;
        return npc.Playfield?.GetService<HashSpawnSystem>()?.TryGetSpawnPoint(npc, out point) == true;
    }

    static bool IsEditable(NpcCharacter? npc, HashSpawnPoint point)
        => npc?.Pet == null && point.AuthoredSpawnIndex >= 0 && !point.IsStatic && point.FrozenTemplates == null;

    static string[] Conditions(HashSpawnPoint point)
    {
        var conditions = new List<string>();
        foreach (PlayfieldHashSpawnExtensionEvent row in point.Source.Extensions?.Events ?? [])
        {
            if (!string.IsNullOrEmpty(row?.Name))
                conditions.Add($"Event {row.Name}: {(row.Active ? "enabled in placement" : "disabled in placement")}");
        }

        conditions.Add(point.RespawnsAfterDeath ? "Playfield cell activation and native respawn scheduling" : "One-shot spawn");
        if (point.Sites.Length > 1)
            conditions.Add("The editor changes the primary site. Additional authored sites keep their positions and headings.");
        if (point.HoldsRoll)
            conditions.Add("This point currently holds its previous native spawn roll.");
        return [.. conditions];
    }

    sealed class PreviewState
    {
        internal NpcEditorSpawnPreviewChange? Undo { get; set; }
        internal List<NpcEditorSpawnPreviewChange> History { get; } = [];

        internal void Record(NpcEditorSpawnPreviewChange change)
        {
            History.Add(change);
            if (History.Count > MaxPreviewHistory)
                History.RemoveAt(0);
        }
    }
}
