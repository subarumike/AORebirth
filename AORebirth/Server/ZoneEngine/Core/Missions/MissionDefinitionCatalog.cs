namespace ZoneEngine.Core.Missions
{
    #region Usings ...

    using System;
    using System.Collections.Generic;
    using System.Linq;

    using ZoneEngine.Core.Arete.Quests;

    #endregion

    internal static class MissionDefinitionCatalog
    {
        internal const string RexB18CQuestId = "Mission:5514B18C";
        internal const string RexB18DQuestId = "Mission:5514B18D";
        internal const string RexB18EQuestId = "Mission:5514B18E";
        internal const string RexB18FQuestId = "Mission:5514B18F";
        internal const string RexB194QuestId = "Mission:5514B194";
        internal const string WindcallerKarrecQuestId = "Mission:55579381";

        internal static IList<MissionDefinition> Build(QuestContentRegistry questRegistry)
        {
            if (questRegistry == null)
            {
                throw new ArgumentNullException("questRegistry");
            }

            var definitions = new List<MissionDefinition>();
            foreach (QuestDefinition quest in questRegistry.GetQuests())
            {
                if (quest == null || string.IsNullOrWhiteSpace(quest.QuestId))
                {
                    continue;
                }

                IList<string> stepIds = (quest.Steps ?? new QuestStep[0])
                    .Where(step => step != null && !string.IsNullOrWhiteSpace(step.StepId))
                    .Select(step => step.StepId.Trim())
                    .ToList();
                IList<MissionObjectiveDefinition> objectives = (quest.Steps ?? new QuestStep[0])
                    .Where(step => step != null)
                    .SelectMany(
                        step => (step.Objectives ?? new QuestObjective[0])
                            .Where(objective => objective != null && !string.IsNullOrWhiteSpace(objective.ObjectiveId))
                            .Select(
                                objective => new MissionObjectiveDefinition
                                             {
                                                 ObjectiveId = objective.ObjectiveId.Trim(),
                                                 StepId = step.StepId == null ? null : step.StepId.Trim(),
                                                 RequiredCount = ResolveRequiredCount(quest.QuestId, objective),
                                                 IsResolved = ResolveRequiredCount(quest.QuestId, objective) > 0
                                             }))
                    .ToList();

                definitions.Add(
                    new MissionDefinition
                    {
                        QuestId = quest.QuestId.Trim(),
                        InitialStepId = quest.InitialStepId == null ? null : quest.InitialStepId.Trim(),
                        IsResolved = stepIds.Count > 0 && objectives.All(objective => objective.IsResolved),
                        StepIds = stepIds,
                        PrerequisiteQuestIds = ResolvePrerequisites(quest.QuestId),
                        Objectives = objectives
                    });
            }

            AddHandoffDefinitionIfMissing(
                definitions,
                RexB18FQuestId,
                "talk_to_marcus",
                new[] { RexB18EQuestId });
            AddHandoffDefinitionIfMissing(
                definitions,
                RexB194QuestId,
                "captured_preview",
                new[] { RexB18FQuestId });

            return definitions;
        }

        private static int ResolveRequiredCount(string questId, QuestObjective objective)
        {
            if (objective.RequiredCount > 0)
            {
                return objective.RequiredCount;
            }

            // These one-shot counts preserve the already-shipped, capture-backed Arete interaction contract.
            if (string.Equals(questId, RexB18DQuestId, StringComparison.OrdinalIgnoreCase)
                || string.Equals(questId, RexB18EQuestId, StringComparison.OrdinalIgnoreCase))
            {
                return 1;
            }

            return 0;
        }

        private static IList<string> ResolvePrerequisites(string questId)
        {
            if (string.Equals(questId, RexB18DQuestId, StringComparison.OrdinalIgnoreCase))
            {
                return new[] { RexB18CQuestId };
            }

            if (string.Equals(questId, RexB18EQuestId, StringComparison.OrdinalIgnoreCase))
            {
                return new[] { RexB18DQuestId };
            }

            return new string[0];
        }

        private static void AddHandoffDefinitionIfMissing(
            ICollection<MissionDefinition> definitions,
            string questId,
            string initialStepId,
            IList<string> prerequisiteQuestIds)
        {
            if (definitions.Any(
                    definition => string.Equals(definition.QuestId, questId, StringComparison.OrdinalIgnoreCase)))
            {
                return;
            }

            definitions.Add(
                new MissionDefinition
                {
                    QuestId = questId,
                    InitialStepId = initialStepId,
                    IsResolved = true,
                    StepIds = new[] { initialStepId },
                    PrerequisiteQuestIds = prerequisiteQuestIds,
                    Objectives = new MissionObjectiveDefinition[0]
                });
        }
    }
}
