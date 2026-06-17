namespace ZoneEngine.Core.Arete
{
    #region Usings ...

    using System;
    using System.Collections.Generic;
    using System.Linq;

    using ZoneEngine.Core.Arete.Dialogue;
    using ZoneEngine.Core.Arete.Quests;

    #endregion

    public static class AreteConditionReferenceValidator
    {
        private static readonly HashSet<string> SupportedConditionTypes =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "AlwaysTrue",
                "AlwaysFalse",
                "MissionOffered",
                "MissionActive",
                "MissionCompleted",
                "MissionNotStarted"
            };

        private static readonly HashSet<string> MissionConditionTypes =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "MissionOffered",
                "MissionActive",
                "MissionCompleted",
                "MissionNotStarted"
            };

        public static AreteValidationResult Validate(
            IEnumerable<DialogueContentPack> dialoguePacks,
            IEnumerable<QuestContentPack> questPacks,
            DialogueContentRegistry dialogueRegistry,
            QuestContentRegistry questRegistry)
        {
            var result = new AreteValidationResult();

            ValidateDialoguePacks(result, dialoguePacks, questRegistry);
            ValidateQuestPacks(result, questPacks, questRegistry);

            return result;
        }

        private static void ValidateDialoguePacks(
            AreteValidationResult result,
            IEnumerable<DialogueContentPack> dialoguePacks,
            QuestContentRegistry questRegistry)
        {
            int packIndex = 0;
            foreach (DialogueContentPack pack in dialoguePacks ?? Enumerable.Empty<DialogueContentPack>())
            {
                string packLocation = "dialoguePack[" + packIndex + "]";
                if (pack == null)
                {
                    packIndex++;
                    continue;
                }

                int npcIndex = 0;
                foreach (DialogueNpcEntry npc in pack.Npcs ?? Enumerable.Empty<DialogueNpcEntry>())
                {
                    string npcLocation = packLocation + ".npc[" + npcIndex + "]";
                    if (npc == null)
                    {
                        npcIndex++;
                        continue;
                    }

                    ValidateDialogueConditions(
                        result,
                        npc.Conditions,
                        npcLocation + ".condition",
                        questRegistry);

                    int nodeIndex = 0;
                    foreach (DialogueNode node in npc.Nodes ?? Enumerable.Empty<DialogueNode>())
                    {
                        string nodeLocation = npcLocation + ".node[" + nodeIndex + "]";
                        if (node == null)
                        {
                            nodeIndex++;
                            continue;
                        }

                        int optionIndex = 0;
                        foreach (DialogueOption option in node.Options ?? Enumerable.Empty<DialogueOption>())
                        {
                            string optionLocation = nodeLocation + ".option[" + optionIndex + "]";
                            if (option != null)
                            {
                                ValidateDialogueConditions(
                                    result,
                                    option.Conditions,
                                    optionLocation + ".condition",
                                    questRegistry);
                            }

                            optionIndex++;
                        }

                        nodeIndex++;
                    }

                    npcIndex++;
                }

                packIndex++;
            }
        }

        private static void ValidateQuestPacks(
            AreteValidationResult result,
            IEnumerable<QuestContentPack> questPacks,
            QuestContentRegistry questRegistry)
        {
            int packIndex = 0;
            foreach (QuestContentPack pack in questPacks ?? Enumerable.Empty<QuestContentPack>())
            {
                string packLocation = "questPack[" + packIndex + "]";
                if (pack == null)
                {
                    packIndex++;
                    continue;
                }

                int questIndex = 0;
                foreach (QuestDefinition quest in pack.Quests ?? Enumerable.Empty<QuestDefinition>())
                {
                    string questLocation = packLocation + ".quest[" + questIndex + "]";
                    if (quest == null)
                    {
                        questIndex++;
                        continue;
                    }

                    ValidateQuestConditions(
                        result,
                        quest.Conditions,
                        questLocation + ".condition",
                        questRegistry);

                    int stepIndex = 0;
                    foreach (QuestStep step in quest.Steps ?? Enumerable.Empty<QuestStep>())
                    {
                        string stepLocation = questLocation + ".step[" + stepIndex + "]";
                        if (step == null)
                        {
                            stepIndex++;
                            continue;
                        }

                        ValidateQuestConditions(
                            result,
                            step.Conditions,
                            stepLocation + ".condition",
                            questRegistry);

                        int objectiveIndex = 0;
                        foreach (QuestObjective objective in step.Objectives ?? Enumerable.Empty<QuestObjective>())
                        {
                            string objectiveLocation = stepLocation + ".objective[" + objectiveIndex + "]";
                            if (objective != null)
                            {
                                ValidateQuestConditions(
                                    result,
                                    objective.Conditions,
                                    objectiveLocation + ".condition",
                                    questRegistry);
                            }

                            objectiveIndex++;
                        }

                        stepIndex++;
                    }

                    questIndex++;
                }

                packIndex++;
            }
        }

        private static void ValidateDialogueConditions(
            AreteValidationResult result,
            IEnumerable<DialogueCondition> conditions,
            string locationPrefix,
            QuestContentRegistry questRegistry)
        {
            int conditionIndex = 0;
            foreach (DialogueCondition condition in conditions ?? Enumerable.Empty<DialogueCondition>())
            {
                string location = locationPrefix + "[" + conditionIndex + "]";
                if (condition == null)
                {
                    result.AddError(location, "dialogue condition is null");
                }
                else
                {
                    ValidateCondition(result, location, condition.Type, condition.QuestId, questRegistry);
                }

                conditionIndex++;
            }
        }

        private static void ValidateQuestConditions(
            AreteValidationResult result,
            IEnumerable<QuestCondition> conditions,
            string locationPrefix,
            QuestContentRegistry questRegistry)
        {
            int conditionIndex = 0;
            foreach (QuestCondition condition in conditions ?? Enumerable.Empty<QuestCondition>())
            {
                string location = locationPrefix + "[" + conditionIndex + "]";
                if (condition == null)
                {
                    result.AddError(location, "quest condition is null");
                }
                else
                {
                    ValidateCondition(result, location, condition.Type, condition.QuestId, questRegistry);
                }

                conditionIndex++;
            }
        }

        private static void ValidateCondition(
            AreteValidationResult result,
            string location,
            string conditionType,
            string questId,
            QuestContentRegistry questRegistry)
        {
            if (string.IsNullOrWhiteSpace(conditionType))
            {
                result.AddError(location, "missing condition type");
                return;
            }

            if (!SupportedConditionTypes.Contains(conditionType))
            {
                result.AddError(location, "unsupported condition type '" + conditionType + "'");
                return;
            }

            if (!MissionConditionTypes.Contains(conditionType))
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(questId))
            {
                result.AddError(location, "missing mission id for condition '" + conditionType + "'");
                return;
            }

            if (questRegistry == null)
            {
                result.AddError(location, "quest registry is missing");
                return;
            }

            QuestDefinition quest;
            if (!questRegistry.TryGetQuest(questId, out quest))
            {
                result.AddError(location, "mission id '" + questId + "' was not found");
            }
        }
    }
}
