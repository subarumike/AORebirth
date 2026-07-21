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
        internal const string RexB196QuestId = "Mission:5514B196";
        internal const string RexFlintQuestId = "Mission:5514B198";
        internal const string RexB199QuestId = "Mission:5514B199";
        internal const string RexB19AQuestId = "Mission:5514B19A";
        internal const string RexFlintFindBioQuestId = "Mission:5514B19B";
        internal const string RexFlintDeliverBioQuestId = "Mission:5514B19C";
        internal const string RexFlintSurveillanceUplinkQuestId = "Mission:5514B19D";
        internal const string RexFlintPlantBugQuestId = "Mission:5514B19E";
        internal const string RexFlintDeliverHc12BillQuestId = "Mission:5514B19F";
        internal const string RexFlintKneecappingQuestId = "Mission:5514B1A0";
        internal const string RexFlintReportToAlexQuestId = "Mission:555B4365";
        internal const string RexFlintTalkToStanQuestId = "Mission:555B4366";
        internal const string RexFlintBuyLockpickQuestId = "Mission:555BD124";
        internal const string RexFlintStrongboxContentsQuestId = "Mission:555BE9C5";
        internal const string RexFlintDeliverAntonioFactoryQuestId = "Mission:555BE9F2";
        internal const string RexFlintTalkToSarahGreeneQuestId = "Mission:555BE9F3";
        internal const string RexFlintBuyNanoProgramsQuestId = "Mission:555BE9F4";
        internal const string RexFlintTradeskillNanoSensorQuestId = "Mission:555B4367";
        internal const string RexFlintTradeskillBasicBrainQuestId = "Mission:555B4368";
        internal const string RexFlintTradeskillPersonalizedBrainQuestId = "Mission:555B4369";
        internal const string RexFlintTradeskillShowBrainQuestId = "Mission:555B436A";
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

            // No hard prerequisites: Marcus fire handoff must still offer/accept when the client
            // already shows Talk to Marcus from packet projection even if B18E persistence lagged.
            AddHandoffDefinitionIfMissing(
                definitions,
                RexB18FQuestId,
                "talk_to_marcus",
                new string[0]);
            AddHandoffDefinitionIfMissing(
                definitions,
                RexB194QuestId,
                "captured_preview",
                new string[0]);
            AddHandoffDefinitionIfMissing(
                definitions,
                RexB196QuestId,
                "return_to_marcus",
                new string[0]);
            AddHandoffDefinitionIfMissing(
                definitions,
                RexFlintQuestId,
                "talk_to_flint_novak",
                new string[0]);
            AddHandoffDefinitionIfMissing(
                definitions,
                RexB199QuestId,
                "use_stim_wounded_dockworker",
                new string[0]);
            AddHandoffDefinitionIfMissing(
                definitions,
                RexB19AQuestId,
                "return_marcus_stim",
                new string[0]);
            AddHandoffDefinitionIfMissing(
                definitions,
                RexFlintFindBioQuestId,
                "kill_junkyard_robots",
                new string[0]);
            AddHandoffDefinitionIfMissing(
                definitions,
                RexFlintDeliverBioQuestId,
                "deliver_bio_com",
                new string[0]);
            AddHandoffDefinitionIfMissing(
                definitions,
                RexFlintSurveillanceUplinkQuestId,
                "use_sectec_monitor",
                new string[0]);
            AddHandoffDefinitionIfMissing(
                definitions,
                RexFlintPlantBugQuestId,
                "plant_rc_p_device",
                new string[0]);
            AddHandoffDefinitionIfMissing(
                definitions,
                RexFlintDeliverHc12BillQuestId,
                "deliver_hc12_bill",
                new string[0]);
            AddHandoffDefinitionIfMissing(
                definitions,
                RexFlintKneecappingQuestId,
                "kneecapping_tip",
                new string[0]);
            AddHandoffDefinitionIfMissing(
                definitions,
                RexFlintReportToAlexQuestId,
                "report_to_alex",
                new string[0]);
            AddHandoffDefinitionIfMissing(
                definitions,
                RexFlintTalkToStanQuestId,
                "talk_to_stan_goodman",
                new string[0]);
            AddHandoffDefinitionIfMissing(
                definitions,
                RexFlintBuyLockpickQuestId,
                "buy_a_lockpick",
                new string[0]);
            AddHandoffDefinitionIfMissing(
                definitions,
                RexFlintStrongboxContentsQuestId,
                "take_strongbox_contents",
                new string[0]);
            AddHandoffDefinitionIfMissing(
                definitions,
                RexFlintDeliverAntonioFactoryQuestId,
                "deliver_antonio_factory",
                new string[0]);
            AddHandoffDefinitionIfMissing(
                definitions,
                RexFlintTalkToSarahGreeneQuestId,
                "talk_to_sarah_greene",
                new string[0]);
            AddHandoffDefinitionIfMissing(
                definitions,
                RexFlintBuyNanoProgramsQuestId,
                "buy_nano_programs",
                new string[0]);
            AddHandoffDefinitionIfMissing(
                definitions,
                RexFlintTradeskillNanoSensorQuestId,
                "tradeskill_assemble_nano_sensor",
                new string[0]);
            AddHandoffDefinitionIfMissing(
                definitions,
                RexFlintTradeskillBasicBrainQuestId,
                "tradeskill_assemble_basic_brain",
                new string[0]);
            AddHandoffDefinitionIfMissing(
                definitions,
                RexFlintTradeskillPersonalizedBrainQuestId,
                "tradeskill_assemble_personalized_brain",
                new string[0]);
            AddHandoffDefinitionIfMissing(
                definitions,
                RexFlintTradeskillShowBrainQuestId,
                "tradeskill_show_brain_to_alex",
                new string[0]);

            return definitions;
        }

        private static int ResolveRequiredCount(string questId, QuestObjective objective)
        {
            if (objective != null && objective.RequiredCount > 0)
            {
                return objective.RequiredCount;
            }

            // These one-shot counts preserve the already-shipped, capture-backed Arete interaction contract.
            if (string.Equals(questId, RexB18DQuestId, StringComparison.OrdinalIgnoreCase)
                || string.Equals(questId, RexB18EQuestId, StringComparison.OrdinalIgnoreCase)
                || string.Equals(questId, RexB194QuestId, StringComparison.OrdinalIgnoreCase)
                || string.Equals(questId, RexB196QuestId, StringComparison.OrdinalIgnoreCase)
                || string.Equals(questId, RexB199QuestId, StringComparison.OrdinalIgnoreCase)
                || string.Equals(questId, RexB19AQuestId, StringComparison.OrdinalIgnoreCase)
                || string.Equals(questId, RexFlintDeliverBioQuestId, StringComparison.OrdinalIgnoreCase)
                || string.Equals(questId, RexFlintSurveillanceUplinkQuestId, StringComparison.OrdinalIgnoreCase)
                || string.Equals(questId, RexFlintPlantBugQuestId, StringComparison.OrdinalIgnoreCase)
                || string.Equals(questId, RexFlintDeliverHc12BillQuestId, StringComparison.OrdinalIgnoreCase)
                || string.Equals(questId, RexFlintKneecappingQuestId, StringComparison.OrdinalIgnoreCase)
                || string.Equals(questId, RexFlintReportToAlexQuestId, StringComparison.OrdinalIgnoreCase)
                || string.Equals(questId, RexFlintTalkToStanQuestId, StringComparison.OrdinalIgnoreCase)
                || string.Equals(questId, RexFlintBuyLockpickQuestId, StringComparison.OrdinalIgnoreCase)
                || string.Equals(questId, RexFlintStrongboxContentsQuestId, StringComparison.OrdinalIgnoreCase)
                || string.Equals(questId, RexFlintDeliverAntonioFactoryQuestId, StringComparison.OrdinalIgnoreCase)
                || string.Equals(questId, RexFlintTalkToSarahGreeneQuestId, StringComparison.OrdinalIgnoreCase)
                || string.Equals(questId, RexFlintBuyNanoProgramsQuestId, StringComparison.OrdinalIgnoreCase)
                || string.Equals(questId, RexFlintTradeskillNanoSensorQuestId, StringComparison.OrdinalIgnoreCase))
            {
                return 1;
            }

            if (string.Equals(questId, RexFlintFindBioQuestId, StringComparison.OrdinalIgnoreCase))
            {
                return 7;
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
                    Objectives = new[]
                                 {
                                     new MissionObjectiveDefinition
                                     {
                                         ObjectiveId = "mission_"
                                                       + questId.Replace("Mission:", string.Empty).ToLowerInvariant()
                                                       + "_objective_questfullupdate",
                                         StepId = initialStepId,
                                         RequiredCount = ResolveRequiredCount(questId, null),
                                         IsResolved = ResolveRequiredCount(questId, null) > 0
                                     }
                                 }
                });
        }
    }
}
