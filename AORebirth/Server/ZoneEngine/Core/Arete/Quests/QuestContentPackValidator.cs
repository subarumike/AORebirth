namespace ZoneEngine.Core.Arete.Quests
{
    #region Usings ...

    using System;
    using System.Collections.Generic;
    using System.Linq;

    using ZoneEngine.Core.Arete;

    #endregion

    public static class QuestContentPackValidator
    {
        public static AreteValidationResult Validate(IEnumerable<QuestContentPack> packs)
        {
            var result = new AreteValidationResult();
            var packList = new List<QuestContentPack>(packs ?? Enumerable.Empty<QuestContentPack>());
            var packIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var questIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var stepsByQuest = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);
            int packIndex = 0;

            foreach (QuestContentPack pack in packList)
            {
                string packLocation = "questPack[" + packIndex + "]";
                string packId = GetPackId(pack);

                if (string.IsNullOrWhiteSpace(packId))
                {
                    result.AddError(packLocation, "missing quest content pack id");
                }
                else if (!packIds.Add(packId))
                {
                    result.AddError(packLocation, "duplicate quest content pack id '" + packId + "'");
                }

                ValidateQuests(result, pack, packLocation, questIds, stepsByQuest);
                packIndex++;
            }

            ValidateLinks(result, packList, questIds, stepsByQuest);
            return result;
        }

        private static void ValidateQuests(
            AreteValidationResult result,
            QuestContentPack pack,
            string packLocation,
            HashSet<string> questIds,
            Dictionary<string, HashSet<string>> stepsByQuest)
        {
            if (pack == null)
            {
                result.AddError(packLocation, "content pack is null");
                return;
            }

            int questIndex = 0;
            foreach (QuestDefinition quest in pack.Quests ?? Enumerable.Empty<QuestDefinition>())
            {
                string questLocation = packLocation + ".quest[" + questIndex + "]";
                if (quest == null)
                {
                    result.AddError(questLocation, "quest definition is null");
                    questIndex++;
                    continue;
                }

                if (string.IsNullOrWhiteSpace(quest.QuestId))
                {
                    result.AddError(questLocation, "missing quest id");
                }
                else if (!questIds.Add(quest.QuestId))
                {
                    result.AddError(questLocation, "duplicate quest id '" + quest.QuestId + "'");
                }

                ValidateSteps(result, quest, questLocation, stepsByQuest);
                questIndex++;
            }
        }

        private static void ValidateSteps(
            AreteValidationResult result,
            QuestDefinition quest,
            string questLocation,
            Dictionary<string, HashSet<string>> stepsByQuest)
        {
            var stepIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            int stepIndex = 0;

            foreach (QuestStep step in quest.Steps ?? Enumerable.Empty<QuestStep>())
            {
                string stepLocation = questLocation + ".step[" + stepIndex + "]";
                if (step == null)
                {
                    result.AddError(stepLocation, "quest step is null");
                    stepIndex++;
                    continue;
                }

                if (string.IsNullOrWhiteSpace(step.StepId))
                {
                    result.AddError(stepLocation, "missing quest step id");
                }
                else if (!stepIds.Add(step.StepId))
                {
                    result.AddError(stepLocation, "duplicate quest step id '" + step.StepId + "'");
                }

                ValidateObjectives(result, step, stepLocation);
                stepIndex++;
            }

            if (!string.IsNullOrWhiteSpace(quest.QuestId))
            {
                stepsByQuest[quest.QuestId] = stepIds;
            }

            if (!string.IsNullOrWhiteSpace(quest.InitialStepId) && !stepIds.Contains(quest.InitialStepId))
            {
                result.AddError(questLocation, "initial quest step id '" + quest.InitialStepId + "' was not found");
            }
        }

        private static void ValidateObjectives(AreteValidationResult result, QuestStep step, string stepLocation)
        {
            var objectiveIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            int objectiveIndex = 0;

            foreach (QuestObjective objective in step.Objectives ?? Enumerable.Empty<QuestObjective>())
            {
                string objectiveLocation = stepLocation + ".objective[" + objectiveIndex + "]";
                if (objective == null)
                {
                    result.AddError(objectiveLocation, "quest objective is null");
                    objectiveIndex++;
                    continue;
                }

                if (!string.IsNullOrWhiteSpace(objective.ObjectiveId) && !objectiveIds.Add(objective.ObjectiveId))
                {
                    result.AddError(objectiveLocation, "duplicate quest objective id '" + objective.ObjectiveId + "'");
                }

                objectiveIndex++;
            }
        }

        private static void ValidateLinks(
            AreteValidationResult result,
            IEnumerable<QuestContentPack> packs,
            HashSet<string> questIds,
            Dictionary<string, HashSet<string>> stepsByQuest)
        {
            int packIndex = 0;
            foreach (QuestContentPack pack in packs ?? Enumerable.Empty<QuestContentPack>())
            {
                if (pack == null)
                {
                    packIndex++;
                    continue;
                }

                int linkIndex = 0;
                foreach (QuestChainLinkMetadata link in pack.Links ?? Enumerable.Empty<QuestChainLinkMetadata>())
                {
                    string linkLocation = "questPack[" + packIndex + "].link[" + linkIndex + "]";
                    ValidateLinkEndpoint(result, link, linkLocation, "from", questIds, stepsByQuest);
                    ValidateLinkEndpoint(result, link, linkLocation, "to", questIds, stepsByQuest);
                    linkIndex++;
                }

                packIndex++;
            }
        }

        private static void ValidateLinkEndpoint(
            AreteValidationResult result,
            QuestChainLinkMetadata link,
            string linkLocation,
            string side,
            HashSet<string> questIds,
            Dictionary<string, HashSet<string>> stepsByQuest)
        {
            if (link == null)
            {
                result.AddError(linkLocation, "quest chain link is null");
                return;
            }

            string questId = side == "from" ? link.FromQuestId : link.ToQuestId;
            string stepId = side == "from" ? link.FromStepId : link.ToStepId;

            if (string.IsNullOrWhiteSpace(questId))
            {
                result.AddError(linkLocation, "missing " + side + " quest id");
                return;
            }

            if (!questIds.Contains(questId))
            {
                result.AddError(linkLocation, side + " quest id '" + questId + "' was not found");
                return;
            }

            if (!string.IsNullOrWhiteSpace(stepId)
                && (!stepsByQuest.ContainsKey(questId) || !stepsByQuest[questId].Contains(stepId)))
            {
                result.AddError(linkLocation, side + " quest step id '" + stepId + "' was not found");
            }
        }

        private static string GetPackId(QuestContentPack pack)
        {
            if (pack == null || pack.Identity == null)
            {
                return string.Empty;
            }

            return pack.Identity.Id;
        }
    }
}
