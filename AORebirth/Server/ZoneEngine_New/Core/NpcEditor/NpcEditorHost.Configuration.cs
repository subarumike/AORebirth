namespace ZoneEngine_New.Core.NpcEditor;

using System;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

internal sealed partial class NpcEditorHost
{
    static void MapStatsRoutes(RouteGroupBuilder npc, NpcEditorRuntime runtime, NpcEditorStats stats)
    {
        npc.MapGet("/stats/saved", async (int playfieldId, int identityType, int instanceId, Guid generation,
            string? scope, string? familyHash, HttpContext ctx) =>
            Results.Ok(stats.Describe(await runtime.CaptureStatsAsync(
                new(playfieldId, identityType, instanceId, generation), ctx.RequestAborted, scope: scope, familyHash: familyHash))));
        npc.MapPost("/stats/save", (int playfieldId, int identityType, int instanceId, Guid generation,
            string? scope, string? familyHash, NpcEditorStatsSave edit, HttpContext ctx) => SaveOverlay(ctx,
                ct => runtime.CaptureStatsAsync(new(playfieldId, identityType, instanceId, generation), ct,
                    estimate: false, scope: scope, familyHash: familyHash),
                (target, actor) => stats.Save(target, edit, actor)));
        npc.MapPost("/stats/restore", (int playfieldId, int identityType, int instanceId, Guid generation,
            string? scope, string? familyHash, NpcEditorStatsRestore edit, HttpContext ctx) => SaveOverlay(ctx,
                ct => runtime.CaptureStatsAsync(new(playfieldId, identityType, instanceId, generation), ct,
                    estimate: false, scope: scope, familyHash: familyHash),
                (target, actor) => stats.Restore(target, edit, actor)));
    }

    static void MapBehaviorRoutes(RouteGroupBuilder npc, NpcEditorRuntime runtime)
    {
        npc.MapGet("/behavior", async (int playfieldId, int identityType, int instanceId, Guid generation, HttpContext ctx) =>
            Results.Ok(await runtime.ReadBehaviorAsync(new(playfieldId, identityType, instanceId, generation), ctx.RequestAborted)));
        npc.MapPost("/behavior", async (int playfieldId, int identityType, int instanceId, Guid generation,
            NpcEditorBehaviorEdit edit, HttpContext ctx) => CanMutate(ctx)
                ? Results.Ok(await runtime.ApplyBehaviorAsync(new(playfieldId, identityType, instanceId, generation), edit,
                    Auth(ctx).Session.Username, ctx.RequestAborted))
                : WriteDenied());
        npc.MapPost("/behavior/undo", async (int playfieldId, int identityType, int instanceId, Guid generation,
            NpcEditorBehaviorUndo edit, HttpContext ctx) => CanMutate(ctx)
                ? Results.Ok(await runtime.UndoBehaviorAsync(new(playfieldId, identityType, instanceId, generation), edit,
                    Auth(ctx).Session.Username, ctx.RequestAborted))
                : WriteDenied());
    }

    static void MapSpawnRoutes(RouteGroupBuilder api, RouteGroupBuilder npc, NpcEditorRuntime runtime, NpcEditorSpawn spawn)
    {
        npc.MapGet("/spawn", async (int playfieldId, int identityType, int instanceId, Guid generation, HttpContext ctx) =>
            Results.Ok(await runtime.EstimateSpawnImpactAsync(await runtime.WithNpcAsync(
                new(playfieldId, identityType, instanceId, generation), spawn.Describe, ctx.RequestAborted), ctx.RequestAborted)));
        npc.MapPost("/spawn/save", (int playfieldId, int identityType, int instanceId, Guid generation,
            NpcEditorSpawnSave edit, HttpContext ctx) => SaveOverlay(ctx,
                ct => runtime.WithNpcAsync(new(playfieldId, identityType, instanceId, generation), spawn.CaptureTarget, ct),
                (target, actor) => spawn.Save(target, edit, actor)));
        npc.MapPost("/spawn/restore", (int playfieldId, int identityType, int instanceId, Guid generation,
            NpcEditorSpawnRestore edit, HttpContext ctx) => SaveOverlay(ctx,
                ct => runtime.WithNpcAsync(new(playfieldId, identityType, instanceId, generation), spawn.CaptureTarget, ct),
                (target, actor) => spawn.Restore(target, edit, actor)));
        npc.MapPost("/spawn/preview", async (int playfieldId, int identityType, int instanceId, Guid generation,
            NpcEditorSpawnPreview edit, HttpContext ctx) => CanMutate(ctx)
                ? Results.Ok(await runtime.WithNpcAsync(new(playfieldId, identityType, instanceId, generation),
                    mob => spawn.Preview(mob, edit, Auth(ctx).Session.Username), ctx.RequestAborted))
                : WriteDenied());
        npc.MapPost("/spawn/preview/revert", async (int playfieldId, int identityType, int instanceId, Guid generation,
            NpcEditorSpawnUndo edit, HttpContext ctx) => CanMutate(ctx)
                ? Results.Ok(await runtime.WithNpcAsync(new(playfieldId, identityType, instanceId, generation),
                    mob => spawn.UndoPreview(mob, edit, Auth(ctx).Session.Username), ctx.RequestAborted))
                : WriteDenied());

        var points = api.MapGroup("/playfields/{playfieldId:int}/spawns");
        points.MapGet("", async (int playfieldId, int? offset, int? limit, HttpContext ctx) =>
            Results.Ok(await runtime.WithPlayfieldAsync(playfieldId, field => spawn.List(field, offset ?? 0, limit ?? 40),
                ctx.RequestAborted)));
        points.MapGet("/{authoredIndex:int}", async (int playfieldId, int authoredIndex, HttpContext ctx) =>
            Results.Ok(await runtime.EstimateSpawnImpactAsync(await runtime.WithPlayfieldAsync(playfieldId,
                field => spawn.Describe(field, authoredIndex), ctx.RequestAborted), ctx.RequestAborted)));
        points.MapPost("/{authoredIndex:int}/save", (int playfieldId, int authoredIndex, AuthoredSpawnSave edit, HttpContext ctx) =>
            SaveOverlay(ctx, ct => CaptureAuthoredSpawn(playfieldId, authoredIndex, edit.AuthoredSignature, ct),
                (target, actor) => spawn.Save(target, new(edit.ExpectedRevision, edit.Settings), actor)));
        points.MapPost("/{authoredIndex:int}/restore", (int playfieldId, int authoredIndex, AuthoredSpawnRestore edit, HttpContext ctx) =>
            SaveOverlay(ctx, ct => CaptureAuthoredSpawn(playfieldId, authoredIndex, edit.AuthoredSignature, ct),
                (target, actor) => spawn.Restore(target, new(edit.ExpectedRevision, edit.Revision), actor)));

        Task<NpcEditorSpawnTarget> CaptureAuthoredSpawn(int playfieldId, int index, string signature, CancellationToken ct)
            => runtime.WithPlayfieldAsync(playfieldId, field =>
            {
                NpcEditorSpawnTarget target = spawn.CaptureTarget(field, index);
                if (!string.Equals(target.AuthoredSignature, signature, StringComparison.Ordinal))
                    throw new NpcEditorRuntimeException(409, "The authored placement changed. Refresh before editing.");
                return target;
            }, ct);
    }

    sealed record AuthoredSpawnSave([property: JsonRequired] string AuthoredSignature,
        [property: JsonRequired] int ExpectedRevision, [property: JsonRequired] NpcEditorSpawnSettings Settings);
    sealed record AuthoredSpawnRestore([property: JsonRequired] string AuthoredSignature,
        [property: JsonRequired] int ExpectedRevision, [property: JsonRequired] int Revision);

    static void MapBatchRoutes(RouteGroupBuilder api, NpcEditorBatch batch)
    {
        api.MapPost("/batch/preview", async (NpcEditorBatchRequest edit, HttpContext ctx) =>
            CanMutate(ctx, save: edit.Operation == "respawn")
                ? Results.Ok(await batch.PreviewAsync(edit, Auth(ctx).Session, ctx.RequestAborted))
                : WriteDenied());
        api.MapPost("/batch/apply", async (NpcEditorBatchApply edit, HttpContext ctx) =>
            Results.Ok(await batch.ApplyAsync(edit.PlanId, Auth(ctx).Session,
                save => CanMutate(ctx, save), ctx.RequestAborted)));
        api.MapPost("/batch/undo", async (NpcEditorBatchUndo edit, HttpContext ctx) =>
            Results.Ok(await batch.UndoAsync(edit.BatchId, Auth(ctx).Session,
                save => CanMutate(ctx, save), ctx.RequestAborted)));
    }
}
