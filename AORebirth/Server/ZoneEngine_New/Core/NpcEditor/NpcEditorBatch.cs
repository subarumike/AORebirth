namespace ZoneEngine_New.Core.NpcEditor;

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.Extensions.Logging;
using MySqlConnector;
using SmokeLounge.AOtomation.Messaging.GameData;

internal sealed record NpcEditorBatchRequest(string Operation, NpcEditorIdentity[] Targets, int? StatId,
    int? Value, int? ItemId, int? Quality, NpcEditorLootEntry? LootEntry, double? RespawnSeconds, double? RespawnChance);
internal sealed record NpcEditorBatchApply(Guid PlanId);
internal sealed record NpcEditorBatchUndo(Guid BatchId);
internal sealed record NpcEditorBatchRow(NpcEditorIdentity Identity, string Name, string Definition,
    string Before, string After, string? Error = null, bool Applied = false);
internal sealed record NpcEditorBatchPreview(Guid? PlanId, string Scope, NpcEditorBatchRow[] Rows, DateTimeOffset? ExpiresAt);
internal sealed record NpcEditorBatchResult(Guid BatchId, NpcEditorBatchRow[] Rows, string TransactionBoundary, bool CanUndo);
internal delegate Task NpcEditorBatchRollback(string actor, CancellationToken ct);
internal sealed record NpcEditorBatchTarget(NpcEditorBatchRow Row,
    Func<string, CancellationToken, Task<NpcEditorBatchRollback>> Apply, (int Resource, int Index)? SpawnKey = null);

internal sealed class NpcEditorBatch(NpcEditorRuntime runtime, NpcEditorLoot loot, NpcEditorSpawn spawn, ILogger logger)
{
    const int MaxTargets = 20;
    const int MaxRetained = 32;
    const string Boundary = "One NPC or authored spawn per transaction. Review each result. Undo checks for intervening changes.";
    static readonly TimeSpan PreviewLifetime = TimeSpan.FromMinutes(2);
    static readonly TimeSpan UndoLifetime = TimeSpan.FromMinutes(10);
    readonly Lock _sync = new();
    readonly Dictionary<Guid, Batch> _batches = [];

    internal async Task<NpcEditorBatchPreview> PreviewAsync(NpcEditorBatchRequest edit, NpcEditorSessions.Session owner,
        CancellationToken ct)
    {
        if (edit.Operation is not ("stat" or "aggression" or "armor" or "loot" or "respawn"))
            throw new NpcEditorRuntimeException(400, "Select a supported batch operation.");
        if (edit.Targets == null || edit.Targets.Length is < 1 or > MaxTargets
            || edit.Targets.Any(target => target == null)
            || edit.Targets.Select(target => (target.PlayfieldId, target.IdentityType, target.InstanceId)).Distinct().Count()
                != edit.Targets.Length || edit.Targets.Any(target => target.PlayfieldId != edit.Targets[0].PlayfieldId))
            throw new NpcEditorRuntimeException(400, "Select 1 to 20 distinct NPCs in one loaded playfield.");

        bool saves = edit.Operation == "respawn";
        string scope = saves ? "Exact authored spawn points in one resource playfield. Future copies inherit saved settings."
            : "Selected live instances in one loaded playfield.";
        var rows = new List<NpcEditorBatchRow>();
        var targets = new List<NpcEditorBatchTarget>();
        var spawnKeys = new HashSet<(int, int)>();
        foreach (NpcEditorIdentity identity in edit.Targets)
        {
            try
            {
                NpcEditorBatchTarget target = saves
                    ? await PrepareRespawn(identity, edit, ct)
                    : await runtime.PrepareLiveBatchAsync(identity, edit, loot, ct);
                if (target.SpawnKey is { } key && !spawnKeys.Add(key))
                {
                    rows.Add(target.Row with { Error = "Select only one member per authored spawn point." });
                    continue;
                }
                rows.Add(target.Row);
                targets.Add(target);
            }
            catch (Exception error) when (ExpectedFailure(error))
            {
                rows.Add(new(identity, $"NPC {identity.InstanceId}", scope, "", "", FailureMessage(error)));
            }
        }
        if (targets.Count != rows.Count)
            return new(null, scope, [.. rows], null);

        Guid id = Guid.NewGuid();
        DateTimeOffset expiry = DateTimeOffset.UtcNow + PreviewLifetime;
        lock (_sync)
        {
            foreach (Guid expired in _batches.Where(pair => !pair.Value.Running && pair.Value.ExpiresAt <= DateTimeOffset.UtcNow)
                         .Select(pair => pair.Key).ToArray())
                _batches.Remove(expired);
            if (_batches.Count >= MaxRetained)
                throw new NpcEditorRuntimeException(429, "Too many NPC batch plans are retained. Try again after a plan expires.");
            _batches.Add(id, new(owner, saves, expiry, [.. targets]));
        }
        return new(id, scope, [.. rows], expiry);
    }

    internal Task<NpcEditorBatchResult> ApplyAsync(Guid id, NpcEditorSessions.Session owner, Func<bool, bool> authorize, CancellationToken ct)
        => ExecuteAsync(id, owner, authorize, undo: false, ct);

    internal Task<NpcEditorBatchResult> UndoAsync(Guid id, NpcEditorSessions.Session owner, Func<bool, bool> authorize, CancellationToken ct)
        => ExecuteAsync(id, owner, authorize, undo: true, ct);

    async Task<NpcEditorBatchResult> ExecuteAsync(Guid id, NpcEditorSessions.Session owner,
        Func<bool, bool> authorize, bool undo, CancellationToken ct)
    {
        Batch batch;
        lock (_sync)
        {
            if (!_batches.TryGetValue(id, out Batch? found) || !ReferenceEquals(found.Owner, owner)
                || found.ExpiresAt <= DateTimeOffset.UtcNow)
                throw new NpcEditorRuntimeException(404, "The batch is expired or unavailable in this session.");
            batch = found;
            if (batch.Running || undo && batch.Applied == null)
                throw new NpcEditorRuntimeException(409, "The batch is still running or has not been applied.");
            batch.Running = true;
        }

        try
        {
            if (!authorize(batch.Saves))
                throw new NpcEditorRuntimeException(403, "This session cannot change the requested batch scope.");
            if (!undo && batch.Applied is { } previous)
                return previous with { CanUndo = batch.Rollbacks.Any(rollback => rollback != null) };

            var rows = new NpcEditorBatchRow[batch.Targets.Length];
            using var deadline = CancellationTokenSource.CreateLinkedTokenSource(ct);
            deadline.CancelAfter(TimeSpan.FromSeconds(45));
            for (int step = 0; step < rows.Length; step++)
            {
                int index = undo ? rows.Length - 1 - step : step;
                NpcEditorBatchTarget target = batch.Targets[index];
                NpcEditorBatchRow row = undo
                    ? target.Row with { Before = target.Row.After, After = target.Row.Before }
                    : target.Row;
                try
                {
                    if (undo && batch.Rollbacks[index] == null)
                    {
                        rows[index] = row with { Error = "No change remains to undo." };
                        continue;
                    }
                    deadline.Token.ThrowIfCancellationRequested();
                    if (!authorize(batch.Saves))
                        throw new NpcEditorRuntimeException(403, "Permission to change this scope was revoked.");
                    if (undo)
                    {
                        await batch.Rollbacks[index]!(owner.Username, deadline.Token);
                        batch.Rollbacks[index] = null;
                    }
                    else
                    {
                        batch.Rollbacks[index] = await target.Apply(owner.Username, deadline.Token);
                    }
                    rows[index] = row with { Applied = true };
                }
                catch (OperationCanceledException) when (deadline.IsCancellationRequested)
                {
                    rows[index] = row with { Error = "Canceled before this target was changed." };
                }
                catch (Exception error) when (ExpectedFailure(error))
                {
                    rows[index] = row with { Error = FailureMessage(error) };
                }
                catch (Exception error)
                {
                    logger.LogError(error, "NPC editor batch {BatchId} failed for target {Target}.", id, row.Identity);
                    rows[index] = row with { Error = "The server could not confirm this change. Refresh the target before retrying." };
                }
            }
            var result = new NpcEditorBatchResult(id, rows, Boundary, batch.Rollbacks.Any(rollback => rollback != null));
            if (!undo)
            {
                batch.Applied = result;
                batch.ExpiresAt = DateTimeOffset.UtcNow + UndoLifetime;
            }
            return result;
        }
        finally
        {
            lock (_sync)
                batch.Running = false;
        }
    }

    async Task<NpcEditorBatchTarget> PrepareRespawn(NpcEditorIdentity identity, NpcEditorBatchRequest edit, CancellationToken ct)
    {
        var (target, name) = await runtime.WithNpcAsync(identity, npc => (spawn.CaptureTarget(npc), npc.Name), ct);
        if (!runtime.CanBatchSpawnInPlayfield(identity.PlayfieldId, target.ResourcePlayfieldId))
            throw new NpcEditorRuntimeException(409, "The same spawn definition is loaded in another playfield instance.");
        NpcEditorSpawnRespawnPlan plan = spawn.PrepareRespawn(target, edit.RespawnSeconds, edit.RespawnChance);
        var row = new NpcEditorBatchRow(identity, name ?? "", target.View.Definition!,
            DescribeRespawn(plan.Before), DescribeRespawn(plan.After));
        return new(row, async (actor, token) =>
        {
            NpcEditorSpawnTarget current = await runtime.WithNpcAsync(identity, spawn.CaptureTarget, token);
            token.ThrowIfCancellationRequested();
            if (current.AuthoredSignature != target.AuthoredSignature || current.AuthoredIndex != target.AuthoredIndex
                || !runtime.CanBatchSpawnInPlayfield(identity.PlayfieldId, target.ResourcePlayfieldId))
                throw new NpcEditorRuntimeException(409, "The spawn scope changed. Preview again.");
            NpcEditorSpawnView result = spawn.Save(current, new(plan.ExpectedRevision, plan.After), actor);
            return (undoActor, undoToken) =>
            {
                if (!runtime.CanBatchSpawnInPlayfield(identity.PlayfieldId, target.ResourcePlayfieldId))
                    throw new NpcEditorRuntimeException(409, "The spawn definition is loaded in another playfield instance.");
                undoToken.ThrowIfCancellationRequested();
                spawn.Restore(current, new(result.Revision, plan.ExpectedRevision), undoActor);
                return Task.CompletedTask;
            };
        }, (target.ResourcePlayfieldId, target.AuthoredIndex));
    }

    static string DescribeRespawn(NpcEditorSpawnSettings settings)
        => string.Create(CultureInfo.InvariantCulture, $"{settings.RespawnSeconds} s / {settings.RespawnChance}% chance");

    static bool ExpectedFailure(Exception error)
        => error is NpcEditorRuntimeException or ArgumentException or InvalidOperationException or IOException
            or UnauthorizedAccessException or MySqlException;

    static string FailureMessage(Exception error) => error switch
    {
        IOException or UnauthorizedAccessException => "The overlay could not be saved or verified.",
        MySqlException => "Account authorization was unavailable.",
        _ => error.Message
    };

    sealed class Batch(NpcEditorSessions.Session owner, bool saves, DateTimeOffset expiresAt, NpcEditorBatchTarget[] targets)
    {
        internal NpcEditorSessions.Session Owner { get; } = owner;
        internal bool Saves { get; } = saves;
        internal DateTimeOffset ExpiresAt { get; set; } = expiresAt;
        internal NpcEditorBatchTarget[] Targets { get; } = targets;
        internal NpcEditorBatchRollback?[] Rollbacks { get; } = new NpcEditorBatchRollback?[targets.Length];
        internal NpcEditorBatchResult? Applied { get; set; }
        internal bool Running { get; set; }
    }
}
