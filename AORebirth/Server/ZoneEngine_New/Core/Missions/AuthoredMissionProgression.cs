namespace ZoneEngine_New.Core.Missions;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using AORebirth.Interfaces.Persistence.Missions;

// These are editable content schema, not another persistence model. Durable records use the DAO types.
public sealed class MissionDefinition
{
    public string QuestId { get; set; } = string.Empty;
    public string InitialStepId { get; set; } = string.Empty;
    public bool IsResolved { get; set; }
    public IList<string> StepIds { get; set; } = [];
    public IList<string> PrerequisiteQuestIds { get; set; } = [];
    public IList<MissionObjectiveDefinition> Objectives { get; set; } = [];
}

public sealed class MissionObjectiveDefinition
{
    public string ObjectiveId { get; set; } = string.Empty;
    public string StepId { get; set; } = string.Empty;
    public int RequiredCount { get; set; }
    public bool IsResolved { get; set; }
}

/// <summary>Authored action transitions inside the caller's mission/inventory/reward transaction.</summary>
public static class AuthoredMissionProgression
{
    public static void Validate(IReadOnlyCollection<MissionDefinition> definitions)
    {
        var names = definitions.Select(d => d.QuestId).ToHashSet(StringComparer.OrdinalIgnoreCase);
        bool Unique(IEnumerable<string> values) => values.All(v => !string.IsNullOrWhiteSpace(v) && v == v.Trim())
            && values.Distinct(StringComparer.OrdinalIgnoreCase).Count() == values.Count();
        if (names.Count != definitions.Count || !Unique(names)) throw new InvalidDataException("Invalid authored quest identities.");
        foreach (var d in definitions)
        {
            if (!Unique(d.StepIds) || !Unique(d.PrerequisiteQuestIds) || !Unique(d.Objectives.Select(o => o.ObjectiveId))
                || d.PrerequisiteQuestIds.Any(p => !names.Contains(p) || string.Equals(p, d.QuestId, StringComparison.OrdinalIgnoreCase))
                || d.IsResolved && !d.StepIds.Contains(d.InitialStepId, StringComparer.OrdinalIgnoreCase)
                || d.Objectives.Any(o => o.IsResolved && (o.RequiredCount <= 0 || !d.StepIds.Contains(o.StepId, StringComparer.OrdinalIgnoreCase))))
                throw new InvalidDataException("Invalid authored quest definition: " + d.QuestId);
        }
    }

    static MissionKeyData Key(IMissionDaoTransaction tx, MissionDefinition definition, long now)
    {
        if (tx.CharacterId <= 0 || now <= 0 || !definition.IsResolved || string.IsNullOrWhiteSpace(definition.QuestId))
            throw new InvalidOperationException("Authored transition requires a resolved definition, stable owner, and UTC clock.");
        return new(tx.CharacterId, definition.QuestId);
    }

    public static bool Offer(IMissionDaoTransaction tx, MissionDefinition definition, long now)
    {
        var key = Key(tx, definition, now);
        var existing = tx.GetMission(key);
        if (existing != null)
            return existing.State is MissionLifecycleState.Offered or MissionLifecycleState.Active or MissionLifecycleState.Completed
                ? false : throw new InvalidOperationException("Ended authored quests cannot be offered again.");
        if (definition.PrerequisiteQuestIds.Any(p => tx.GetMission(new(tx.CharacterId, p))?.State != MissionLifecycleState.Completed))
            throw new InvalidOperationException("An authored quest prerequisite is incomplete.");
        tx.SaveMission(key, new()
        {
            CharacterId = tx.CharacterId, QuestId = definition.QuestId, State = MissionLifecycleState.Offered,
            CurrentStepId = definition.InitialStepId, OfferedAtUtcTicks = now, CreatedAtUtcTicks = now, UpdatedAtUtcTicks = now
        });
        foreach (var objective in definition.Objectives.Where(o => o.IsResolved))
            tx.SaveObjective(new(key, objective.ObjectiveId), new()
            {
                CharacterId = tx.CharacterId, QuestId = definition.QuestId, ObjectiveId = objective.ObjectiveId,
                RequiredCount = objective.RequiredCount, CreatedAtUtcTicks = now, UpdatedAtUtcTicks = now
            });
        return true;
    }

    public static bool Accept(IMissionDaoTransaction tx, MissionDefinition definition, long now)
    {
        var key = Key(tx, definition, now);
        var mission = tx.GetMission(key) ?? throw new InvalidOperationException("Authored quest has not been offered.");
        switch (mission.State)
        {
            case MissionLifecycleState.Active:
            case MissionLifecycleState.Completed: return false;
            case MissionLifecycleState.Offered:
                mission.State = MissionLifecycleState.Active;
                mission.AcceptedAtUtcTicks = now;
                mission.UpdatedAtUtcTicks = now;
                tx.SaveMission(key, mission);
                return true;
            default: throw new InvalidOperationException("Ended authored quests cannot be accepted again.");
        }
    }

    public static bool Complete(IMissionDaoTransaction tx, MissionDefinition definition, string objectiveId,
        string observationKey, string eventType, string source, string target, long now)
    {
        var key = Key(tx, definition, now);
        var mission = tx.GetMission(key) ?? throw new InvalidOperationException("Authored quest does not exist.");
        if (mission.State == MissionLifecycleState.Completed) return false;
        if (mission.State != MissionLifecycleState.Active) throw new InvalidOperationException("Authored quest is not active.");
        var objective = definition.Objectives.SingleOrDefault(o => string.Equals(o.ObjectiveId, objectiveId, StringComparison.OrdinalIgnoreCase));
        if (objective is not { IsResolved: true, RequiredCount: > 0 }
            || !string.Equals(objective.StepId, mission.CurrentStepId, StringComparison.OrdinalIgnoreCase)
            || string.IsNullOrWhiteSpace(observationKey) || string.IsNullOrWhiteSpace(eventType))
            throw new InvalidOperationException("The observation does not identify a resolved objective on the active step.");
        var progressKey = new MissionObjectiveKeyData(key, objective.ObjectiveId);
        var progress = tx.GetObjective(progressKey) ?? throw new InvalidOperationException("Authored objective is not initialized.");
        if (progress.RequiredCount != objective.RequiredCount)
        {
            progress.RequiredCount = objective.RequiredCount;
            progress.UpdatedAtUtcTicks = now;
            tx.SaveObjective(progressKey, progress);
        }
        if (progress.Progress < progress.RequiredCount)
        {
            if (!tx.TryAddObservation(new()
            {
                CharacterId = tx.CharacterId, QuestId = definition.QuestId, ObjectiveId = objective.ObjectiveId,
                ObservationKey = observationKey.Trim(), EventType = eventType.Trim(), SourceIdentity = source,
                TargetIdentity = target, ObservedAtUtcTicks = now
            })) throw new InvalidOperationException("Duplicate authored objective observation.");
            progress.Progress = Math.Min(progress.RequiredCount, checked(progress.Progress + 1));
            progress.LastObservationKey = observationKey.Trim();
            progress.UpdatedAtUtcTicks = now;
            tx.SaveObjective(progressKey, progress);
        }
        if (definition.Objectives.Any(o => !o.IsResolved || o.RequiredCount <= 0
            || tx.GetObjective(new(key, o.ObjectiveId)) is not { } row || row.Progress < row.RequiredCount))
            throw new InvalidOperationException("Authored quest has unresolved or incomplete objectives.");
        mission.State = MissionLifecycleState.Completed;
        mission.CompletedAtUtcTicks = now;
        mission.UpdatedAtUtcTicks = now;
        tx.SaveMission(key, mission);
        return true;
    }

    public static bool SetFlag(IMissionDaoTransaction tx, MissionDefinition definition, string name, string value, long now)
    {
        var key = Key(tx, definition, now);
        if (string.IsNullOrWhiteSpace(name) || tx.GetMission(key) == null)
            throw new InvalidOperationException("Authored flag requires an existing quest and a name.");
        name = name.Trim();
        var flag = tx.GetFlag(key, name);
        if (flag?.Value == value) return false;
        flag ??= new() { CharacterId = tx.CharacterId, QuestId = definition.QuestId, FlagKey = name, CreatedAtUtcTicks = now };
        flag.Value = value;
        flag.UpdatedAtUtcTicks = now;
        tx.SaveFlag(key, flag);
        return true;
    }
}
