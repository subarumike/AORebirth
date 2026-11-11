namespace ZoneEngine_New.Core.NpcEditor;

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;

using SmokeLounge.AOtomation.Messaging.GameData;

using ZoneEngine_New.Core.Ai;
using ZoneEngine_New.Core.Entities;
using ZoneEngine_New.Core.Mobs;
using ZoneEngine_New.Core.NpcScripts;
using ZoneEngine_New.Core.Playfield.Locality;

using Vector3 = AORebirth.Core.Vector.Vector3;

internal sealed record NpcEditorBehaviorEdit([property: JsonRequired] string ExpectedVersion,
    int? BreedHostility, bool? PatrolEnabled, bool? PatrolRun, NpcEditorPoint[]? Waypoints);
internal sealed record NpcEditorBehaviorUndo([property: JsonRequired] string ExpectedVersion,
    [property: JsonRequired] Guid ChangeId);
internal sealed record NpcEditorBehaviorSetting(string Name, string Runtime, string? Configured, string Source, bool Editable);
internal sealed record NpcEditorBehaviorChange(Guid ChangeId, string Actor, DateTimeOffset AtUtc, string Before,
    string After, bool Reverted, bool Revertible);
internal sealed record NpcEditorBehaviorView(string Version, NpcEditorBehaviorSetting[] Settings, int? BreedHostility,
    bool PatrolEnabled, bool PatrolRun, NpcEditorPoint[] Waypoints, bool CanEditAggression, bool CanEditPatrol,
    string? UnavailableReason, NpcEditorBehaviorChange[] History);

internal sealed partial class NpcEditorRuntime
{
    const int MaxPatrolWaypoints = 64;
    readonly ConditionalWeakTable<NpcCharacter, List<BehaviorRevision>> _behaviorChanges = new();

    internal Task<NpcEditorBehaviorView> ReadBehaviorAsync(NpcEditorIdentity identity, CancellationToken ct)
        => WithNpcAsync(identity, DescribeBehavior, ct);

    internal Task<NpcEditorBehaviorView> ApplyBehaviorAsync(NpcEditorIdentity identity, NpcEditorBehaviorEdit edit,
        string actor, CancellationToken ct)
        => WithNpcAsync(identity, npc =>
        {
            BehaviorValues before = ReadBehavior(npc);
            RequireBehaviorVersion(before, edit.ExpectedVersion);
            BehaviorValues after = before with
            {
                BreedHostility = edit.BreedHostility ?? before.BreedHostility,
                PatrolEnabled = edit.PatrolEnabled ?? before.PatrolEnabled,
                PatrolRun = edit.PatrolRun ?? before.PatrolRun,
                Waypoints = edit.Waypoints ?? before.Waypoints
            };
            if (BehaviorVersion(after) == edit.ExpectedVersion)
                throw new NpcEditorRuntimeException(400, "The NPC already has these behavior settings.");

            ApplyBehavior(npc, before, after, restoring: false);
            List<BehaviorRevision> history = _behaviorChanges.GetValue(npc, static _ => []);
            if (history.Count == MaxHistory)
                history.RemoveAt(0);
            history.Add(new(Guid.NewGuid(), actor, DateTimeOffset.UtcNow, before, after));
            return DescribeBehavior(npc);
        }, ct);

    internal Task<NpcEditorBehaviorView> UndoBehaviorAsync(NpcEditorIdentity identity, NpcEditorBehaviorUndo edit,
        string actor, CancellationToken ct)
        => WithNpcAsync(identity, npc =>
        {
            BehaviorValues current = ReadBehavior(npc);
            RequireBehaviorVersion(current, edit.ExpectedVersion);
            BehaviorRevision? last = LastBehaviorChange(npc);
            if (last == null || last.Id != edit.ChangeId || BehaviorVersion(last.After) != edit.ExpectedVersion)
                throw new NpcEditorRuntimeException(409, "Only the most recent matching behavior change can be undone.");

            ApplyBehavior(npc, current, last.Before, restoring: true);
            last.RevertedBy = actor;
            return DescribeBehavior(npc);
        }, ct);

    NpcEditorBehaviorView DescribeBehavior(NpcCharacter npc)
    {
        BehaviorValues current = ReadBehavior(npc);
        string version = BehaviorVersion(current);
        NpcBrain? brain = npc.Brain;
        bool canAggro = BehaviorIdle(npc) && npc.Attackable && current.BreedHostility.HasValue
            && StatValidationError(npc, CharacterStat.BreedHostility, current.BreedHostility.Value, restoring: true) == null;
        bool canPatrol = BehaviorIdle(npc) && CanEditPatrol(npc);
        var field = npc.Playfield!;
        bool outdoor = field.GetService<PlayfieldLocality>()?.Grid.IsOutdoor == true;
        CharacterStat range = outdoor ? CharacterStat.ProximityRangeOutdoors : CharacterStat.ProximityRangeIndoors;
        var script = field.GetService<NpcScriptCatalog>()?.Resolve(npc.SpawnHash ?? npc.MobTemplate?.Hash,
            field.ResourceId, npc.SpawnPointKey, npcKey: null);
        string? StatsConfigured(CharacterStat stat)
        {
            int id = (int)stat;
            if (npc.EditorScopedStats?.TryGetValue(id, out var scoped) == true)
                return Text(scoped.Value);
            return npc.MobTemplate?.Stats.TryGetValue(id, out int value) == true ? Text(value) : null;
        }
        string StatSource(CharacterStat stat, string fallback) =>
            npc.EditorScopedStats?.TryGetValue((int)stat, out var scoped) == true
                ? scoped.Source
                : npc.MobTemplate?.EditorStatSources?.GetValueOrDefault((int)stat) ?? fallback;
        string? patrolSource = script?.Patrol != null ? "NpcScripts movement binding" : "Native brain profile";
        string? attackable = npc.MobTemplate is { } template ? (!template.Has(NpcFeature.NoCombat)).ToString() : null;
        NpcEditorBehaviorSetting[] settings =
        [
            new("Attackable", npc.Attackable.ToString(), attackable, "Spawn template features", false),
            new("Breed hostility", Text(current.BreedHostility), StatsConfigured(CharacterStat.BreedHostility),
                StatSource(CharacterStat.BreedHostility, "Spawn template, with native faction, guard and target rules"), canAggro),
            new("Detection distance", Text(NpcAiRules.ProximityAggroRangeFor(npc.Stats, outdoor)), StatsConfigured(range),
                StatSource(range, $"{range} or the native {NpcAiRules.ProximityAggroRange} m fallback"), false),
            new("Engagement distance", Text(NpcAiRules.EngageRange(false)), null, "Native new-target rule", false),
            new("Current-target limit", Text(NpcAiRules.EngageRange(true)), null, "Native pursuit rule", false),
            new("Attack range", Text(npc.Weapons.Values.Select(weapon => weapon.GetAttackRange()).DefaultIfEmpty(0).Max()),
                null, "Active NPC weapons", false),
            new("Return home", brain?.Home is { } home ? FormatPoint(Point(home)) : "Unavailable", null, "Native brain home and leash", false),
            new("Movement", brain?.ScriptHoldsMovement == true ? "Scripted" : current.PatrolEnabled ? "Patrol" :
                brain?.WanderEnabled == true ? "Wander" : npc.Motor.State.ToString(), null, "Native movement controller", false),
            new("Patrol enabled", current.PatrolEnabled.ToString(), (script?.Patrol != null || NpcAiProfiles.Default.PatrolEnabled).ToString(),
                patrolSource, canPatrol),
            new("Patrol pace", current.PatrolRun ? "Run" : "Walk", script?.Patrol?.Run == true ? "Run" : "Walk", patrolSource, canPatrol),
            new("Patrol waypoints", Text(current.Waypoints.Length), Text(script?.Patrol?.Waypoints.Count() ?? 0), patrolSource, canPatrol),
            new("Combat nanos", string.Join(", ", npc.UploadedNanoIds.Select(id => $"{ItemName(id)} ({id})")), null,
                "Native uploaded nano list", false),
            new("Behavior features", string.Join(", ", npc.MobTemplate?.Features.Order() ?? Enumerable.Empty<NpcFeature>()),
                null, "Materialized template and combat rules", false),
            new("Script", npc.Scripts?.Program.Id ?? "None", script?.Program?.Id, "NpcScripts binding", false)
        ];
        BehaviorRevision? last = LastBehaviorChange(npc);
        NpcEditorBehaviorChange[] history = _behaviorChanges.TryGetValue(npc, out List<BehaviorRevision>? changes)
            ? changes.AsEnumerable().Reverse().Select(change => new NpcEditorBehaviorChange(change.Id, change.Actor,
                change.AtUtc, FormatBehavior(change.Before), FormatBehavior(change.After), change.RevertedBy != null,
                ReferenceEquals(change, last) && BehaviorVersion(change.After) == version && BehaviorIdle(npc))).ToArray()
            : [];
        string? unavailable = canAggro || canPatrol ? null : "Live behavior editing requires an eligible idle NPC with a native brain.";
        return new(version, settings, current.BreedHostility, current.PatrolEnabled, current.PatrolRun, current.Waypoints,
            canAggro, canPatrol, unavailable, history);
    }

    internal static void ApplyBehavior(NpcCharacter npc, BehaviorValues before, BehaviorValues after, bool restoring)
    {
        if (!BehaviorIdle(npc))
            throw new NpcEditorRuntimeException(409, "Wait for this NPC to finish combat, leashing or scripted movement.");

        bool aggressionChanged = after.BreedHostility != before.BreedHostility;
        bool patrolChanged = before.PatrolEnabled != after.PatrolEnabled || before.PatrolRun != after.PatrolRun
            || !before.Waypoints.SequenceEqual(after.Waypoints);
        if (aggressionChanged && (after.BreedHostility is not int value || !npc.Attackable
            || StatValidationError(npc, CharacterStat.BreedHostility, value, restoring) != null))
            throw new NpcEditorRuntimeException(400, "Breed hostility requires an editable base value and a value from 0 to 100.");

        if (patrolChanged)
        {
            if (!CanEditPatrol(npc))
                throw new NpcEditorRuntimeException(409, "Patrol editing requires a movable NPC without a running script or other movement path.");
            if (after.Waypoints.Length > MaxPatrolWaypoints || after.PatrolEnabled && after.Waypoints.Length == 0
                || after.Waypoints.Any(point => !ValidWaypoint(npc, point)))
                throw new NpcEditorRuntimeException(400, "Supply up to 64 finite waypoints inside the playfield and at least one to enable patrol.");
        }

        if (aggressionChanged)
            SetStat(npc, CharacterStat.BreedHostility, after.BreedHostility!.Value, restoring);
        if (patrolChanged)
            npc.Brain!.ConfigurePatrol(after.Waypoints.Select(point => new Vector3(point.X, point.Y, point.Z)).ToArray(),
                after.PatrolEnabled, after.PatrolRun);
    }

    static bool BehaviorIdle(NpcCharacter npc) => !npc.IsDead && npc.Pet == null && npc.Shop == null && !npc.IsInCombat
        && npc.Brain is { IsBusy: false, ScriptHoldsMovement: false, IsHeldByConversation: false };

    static bool CanEditPatrol(NpcCharacter npc) => !npc.IsImmobile && npc.Scripts == null
        && npc.Brain is { } brain && (!npc.Motor.HasPath || brain.PatrolAnchor is not null);

    static bool ValidWaypoint(NpcCharacter npc, NpcEditorPoint? point)
        => point != null && double.IsFinite(point.X) && double.IsFinite(point.Y) && double.IsFinite(point.Z)
            && Math.Abs(point.X) <= 100_000 && Math.Abs(point.Y) <= 100_000 && Math.Abs(point.Z) <= 100_000
            && (npc.Playfield?.GetService<PlayfieldLocality>()?.Grid is not { } grid
                || grid.TryGetCellId(new Vector3(point.X, point.Y, point.Z), out _, clampToGrid: false));

    static void RequireBehaviorVersion(BehaviorValues current, string expected)
    {
        if (!string.Equals(BehaviorVersion(current), expected, StringComparison.Ordinal))
            throw new NpcEditorRuntimeException(409, "The NPC behavior changed. Refresh before editing.");
    }

    static string BehaviorVersion(BehaviorValues values) => Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(values)));
    static BehaviorValues ReadBehavior(NpcCharacter npc) => new(ReadStat(npc, CharacterStat.BreedHostility, StatDetail.Base),
        npc.Brain?.PatrolEnabled == true, npc.Brain?.PatrolRun == true, npc.Brain?.PatrolWaypoints.Select(Point).ToArray() ?? []);
    static NpcEditorPoint Point(Vector3 position) => new(position.x, position.y, position.z);
    static string Text(object? value) => Convert.ToString(value, CultureInfo.InvariantCulture) is { Length: > 0 } text ? text : "Unavailable";
    static string FormatPoint(NpcEditorPoint point) => FormattableString.Invariant($"{point.X:F2}, {point.Y:F2}, {point.Z:F2}");
    static string FormatBehavior(BehaviorValues value) => $"Breed hostility {Text(value.BreedHostility)}. Patrol {value.PatrolEnabled}. "
        + (value.PatrolRun ? "Run. " : "Walk. ") + string.Join(" / ", value.Waypoints.Select(FormatPoint));

    BehaviorRevision? LastBehaviorChange(NpcCharacter npc) => _behaviorChanges.TryGetValue(npc, out List<BehaviorRevision>? history)
        ? history.FindLast(change => change.RevertedBy == null) : null;

    internal sealed record BehaviorValues(int? BreedHostility, bool PatrolEnabled, bool PatrolRun, NpcEditorPoint[] Waypoints);
    sealed record BehaviorRevision(Guid Id, string Actor, DateTimeOffset AtUtc, BehaviorValues Before, BehaviorValues After)
    {
        internal string? RevertedBy { get; set; }
    }
}
