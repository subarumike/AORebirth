namespace ZoneEngine_New.Core.NpcEditor;

using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

using ZoneEngine_New.Core.GameData;

internal sealed partial class NpcEditorHost
{
    static void MapNamedLootRoutes(RouteGroupBuilder api, RouteGroupBuilder npc, NpcEditorRuntime runtime,
        NpcEditorLoot loot, NpcEditorNamedLoot named, NpcEditorLootCoverage coverage)
    {
        var tables = api.MapGroup("/loot-tables");
        tables.MapGet("", () => Results.Ok(named.Describe()));
        tables.MapGet("/targets", (string? q, int? playfieldId, HttpContext ctx) =>
            Results.Ok(coverage.Targets(q, playfieldId, ctx.RequestAborted)));
        tables.MapPost("/save", (NamedLootTableSave edit, HttpContext ctx) => SaveDefinition(ctx, edit, named.SaveTable));
        tables.MapPost("/delete", (NamedLootDelete edit, HttpContext ctx) => SaveDefinition(ctx, edit, named.DeleteTable));
        tables.MapPost("/restore", (NamedLootRestore edit, HttpContext ctx) => SaveDefinition(ctx, edit, named.Restore));
        tables.MapPost("/assignments/save", (NamedLootAssignmentSave edit, HttpContext ctx) =>
            SaveDefinition(ctx, edit, named.SaveAssignment));
        tables.MapPost("/assignments/delete", (NamedLootDelete edit, HttpContext ctx) =>
            SaveDefinition(ctx, edit, named.DeleteAssignment));
        tables.MapPost("/assignments/preview", async (NamedLootAssignmentSave edit, HttpContext ctx) =>
        {
            if (!CanMutate(ctx, save: true))
                return WriteDenied();
            NamedLootAssignment candidate = named.PreviewAssignment(edit);
            NamedLootAssignment? previous = named.Describe().Assignments.FirstOrDefault(row => row.Id == candidate.Id);
            int? matches = await runtime.EstimateLootAssignmentAsync(candidate, previous, ctx.RequestAborted);
            return Results.Ok(new
            {
                definition = NpcEditorLootCoverage.AssignmentDefinition(candidate),
                loadedMatches = matches,
                affectedTable = candidate.TableId,
                description = $"{candidate.Merge} at priority {candidate.Priority}. The next eligible death uses the saved registry."
            });
        });
        npc.MapGet("/loot/effective", async (int playfieldId, int identityType, int instanceId, Guid generation, HttpContext ctx) =>
            Results.Ok(await runtime.WithNpcAsync(new(playfieldId, identityType, instanceId, generation),
                loot.DescribeEffective, ctx.RequestAborted)));
    }

    static void MapLootEvidenceRoutes(RouteGroupBuilder api, RouteGroupBuilder npc, NpcEditorRuntime runtime,
        NpcEditorLoot loot, NpcEditorLootEvidence evidence, NpcEditorLootCoverage coverage)
    {
        var observations = api.MapGroup("/loot-evidence");
        observations.MapGet("", (string? q, string? classification, string? review, int? offset, int? limit) =>
            Results.Ok(evidence.Read(q, classification, review, offset ?? 0, limit ?? 50)));
        observations.MapPost("/import", (LootEvidenceImport edit, HttpContext ctx) => SaveDefinition(ctx, edit, evidence.Import));
        observations.MapPost("/review", (LootEvidenceReview edit, HttpContext ctx) => SaveDefinition(ctx, edit, evidence.Review));
        api.MapGet("/loot-coverage", (string? q, int? playfieldId, string? family, int? minLevel, int? maxLevel,
            string? boss, string? category, string? status, int? offset, int? limit, bool? export, HttpContext ctx) =>
            Results.Ok(coverage.Read(new(q, playfieldId, family, minLevel, maxLevel, boss, category, status,
                offset ?? 0, limit ?? 50), ctx.RequestAborted, export == true)));
        npc.MapGet("/loot/comparison", async (int playfieldId, int identityType, int instanceId, Guid generation, HttpContext ctx) =>
        {
            LootComparisonInput input = await runtime.WithNpcAsync(new(playfieldId, identityType, instanceId, generation),
                loot.CaptureComparison, ctx.RequestAborted);
            return Results.Ok(coverage.Compare(input));
        });
    }

    static Task<IResult> SaveDefinition<TEdit, TResponse>(HttpContext ctx, TEdit edit, Func<TEdit, string, TResponse> save)
        => SaveOverlay(ctx, _ => Task.FromResult(edit), save);
}
