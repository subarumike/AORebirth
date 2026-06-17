namespace ZoneEngine.Core.Arete
{
    #region Usings ...

    using System.Collections.Generic;
    using System.IO;
    using System.Linq;

    using ZoneEngine.Core.Arete.Dialogue;
    using ZoneEngine.Core.Arete.Quests;

    #endregion

    public sealed class AreteAggregateContentValidator
    {
        private static readonly string[] StageNames =
        {
            "Load",
            "DialoguePack",
            "QuestPack",
            "Registry",
            "ActionReference",
            "ConditionReference"
        };

        public AreteValidationResult ValidateFiles(
            IEnumerable<string> dialogueFilePaths,
            IEnumerable<string> questFilePaths)
        {
            return this.ValidateFilesWithReport(dialogueFilePaths, questFilePaths).ValidationResult;
        }

        public AreteAggregateValidationReport ValidateFilesWithReport(
            IEnumerable<string> dialogueFilePaths,
            IEnumerable<string> questFilePaths)
        {
            AreteContentLoadResult<DialogueContentPack> dialogueLoadResult =
                AreteJsonContentFileLoader.LoadFiles<DialogueContentPack>(
                    dialogueFilePaths,
                    "dialogue",
                    null);

            AreteContentLoadResult<QuestContentPack> questLoadResult =
                AreteJsonContentFileLoader.LoadFiles<QuestContentPack>(
                    questFilePaths,
                    "quest",
                    null);

            return this.ValidateLoadedContentReport(dialogueLoadResult, questLoadResult);
        }

        public AreteValidationResult ValidateManifest(string manifestPath)
        {
            return this.ValidateManifestWithReport(manifestPath).ValidationResult;
        }

        public AreteAggregateValidationReport ValidateManifestWithReport(string manifestPath)
        {
            AreteContentManifestLoadResult manifestResult = new AreteContentManifestLoader().Load(manifestPath);
            AreteContentLoadResult<DialogueContentPack> dialogueLoadResult =
                AreteJsonContentFileLoader.LoadFiles<DialogueContentPack>(
                    manifestResult.DialoguePackFiles,
                    "dialogue",
                    null);

            AreteContentLoadResult<QuestContentPack> questLoadResult =
                AreteJsonContentFileLoader.LoadFiles<QuestContentPack>(
                    manifestResult.QuestPackFiles,
                    "quest",
                    null);

            AreteAggregateValidationReport report =
                this.ValidateLoadedContentReport(dialogueLoadResult, questLoadResult);

            report.AddStageResult("Load", manifestResult.Validation);
            return report;
        }

        public AreteValidationResult ValidateDirectory(string contentDirectory)
        {
            return this.ValidateDirectoryWithReport(contentDirectory).ValidationResult;
        }

        public AreteAggregateValidationReport ValidateDirectoryWithReport(string contentDirectory)
        {
            if (string.IsNullOrWhiteSpace(contentDirectory))
            {
                AreteAggregateValidationReport report = CreateReport();
                report.AddStageMessage("Load", "missing aggregate content directory path");
                return report;
            }

            return this.ValidateDirectoriesWithReport(
                Path.Combine(contentDirectory, "dialogue"),
                Path.Combine(contentDirectory, "quests"));
        }

        public AreteValidationResult ValidateDirectories(string dialogueDirectory, string questDirectory)
        {
            return this.ValidateDirectoriesWithReport(dialogueDirectory, questDirectory).ValidationResult;
        }

        public AreteAggregateValidationReport ValidateDirectoriesWithReport(string dialogueDirectory, string questDirectory)
        {
            AreteContentLoadResult<DialogueContentPack> dialogueLoadResult =
                AreteJsonContentFileLoader.LoadDirectory<DialogueContentPack>(
                    dialogueDirectory,
                    "dialogue",
                    null);

            AreteContentLoadResult<QuestContentPack> questLoadResult =
                AreteJsonContentFileLoader.LoadDirectory<QuestContentPack>(
                    questDirectory,
                    "quest",
                    null);

            return this.ValidateLoadedContentReport(dialogueLoadResult, questLoadResult);
        }

        private AreteAggregateValidationReport ValidateLoadedContentReport(
            AreteContentLoadResult<DialogueContentPack> dialogueLoadResult,
            AreteContentLoadResult<QuestContentPack> questLoadResult)
        {
            AreteAggregateValidationReport report = CreateReport();
            IList<DialogueContentPack> dialoguePacks = GetPacks(dialogueLoadResult);
            IList<QuestContentPack> questPacks = GetPacks(questLoadResult);

            report.LoadedDialogueFileCount = dialoguePacks.Count;
            report.LoadedQuestFileCount = questPacks.Count;
            report.LoadedDialoguePackCount = dialoguePacks.Count;
            report.LoadedQuestPackCount = questPacks.Count;
            report.LoadedNpcEntryCount = CountNpcEntries(dialoguePacks);
            report.LoadedQuestDefinitionCount = CountQuestDefinitions(questPacks);
            report.ActionReferenceValidationCount = CountDialogueActions(dialoguePacks);
            report.ConditionReferenceValidationCount = CountConditions(dialoguePacks, questPacks);

            report.AddStageResult("Load", dialogueLoadResult == null ? null : dialogueLoadResult.Validation);
            report.AddStageResult("Load", questLoadResult == null ? null : questLoadResult.Validation);

            AreteValidationResult dialoguePackValidation = DialogueContentPackValidator.Validate(dialoguePacks);
            AreteValidationResult questPackValidation = QuestContentPackValidator.Validate(questPacks);

            report.AddStageResult("DialoguePack", dialoguePackValidation);
            report.AddStageResult("QuestPack", questPackValidation);

            var dialogueRegistry = new DialogueContentRegistry();
            var questRegistry = new QuestContentRegistry();
            bool dialogueRegistryValid = false;
            bool questRegistryValid = false;

            if (dialoguePackValidation.IsValid)
            {
                AreteValidationResult dialogueRegistryValidation = dialogueRegistry.Load(dialoguePacks);
                report.AddStageResult("Registry", dialogueRegistryValidation);
                dialogueRegistryValid = dialogueRegistryValidation.IsValid;
            }

            if (questPackValidation.IsValid)
            {
                AreteValidationResult questRegistryValidation = questRegistry.Load(questPacks);
                report.AddStageResult("Registry", questRegistryValidation);
                questRegistryValid = questRegistryValidation.IsValid;
            }

            if (questRegistryValid)
            {
                AreteValidationResult actionReferenceValidation =
                    DialogueActionReferenceValidator.Validate(dialoguePacks, questRegistry);

                report.AddStageResult("ActionReference", actionReferenceValidation);
            }

            AreteValidationResult conditionReferenceValidation =
                AreteConditionReferenceValidator.Validate(
                    dialoguePacks,
                    questPacks,
                    dialogueRegistryValid ? dialogueRegistry : null,
                    questRegistryValid ? questRegistry : null);

            report.AddStageResult("ConditionReference", conditionReferenceValidation);
            return report;
        }

        private static AreteAggregateValidationReport CreateReport()
        {
            var report = new AreteAggregateValidationReport();

            foreach (string stageName in StageNames)
            {
                report.EnsureStage(stageName);
            }

            return report;
        }

        private static IList<TPack> GetPacks<TPack>(AreteContentLoadResult<TPack> loadResult)
        {
            if (loadResult == null)
            {
                return new List<TPack>();
            }

            return new List<TPack>(loadResult.Packs ?? Enumerable.Empty<TPack>());
        }

        private static int CountNpcEntries(IEnumerable<DialogueContentPack> dialoguePacks)
        {
            return (dialoguePacks ?? Enumerable.Empty<DialogueContentPack>())
                .Where(pack => pack != null)
                .Sum(pack => (pack.Npcs ?? Enumerable.Empty<DialogueNpcEntry>()).Count(npc => npc != null));
        }

        private static int CountQuestDefinitions(IEnumerable<QuestContentPack> questPacks)
        {
            return (questPacks ?? Enumerable.Empty<QuestContentPack>())
                .Where(pack => pack != null)
                .Sum(pack => (pack.Quests ?? Enumerable.Empty<QuestDefinition>()).Count(quest => quest != null));
        }

        private static int CountDialogueActions(IEnumerable<DialogueContentPack> dialoguePacks)
        {
            int count = 0;

            foreach (DialogueContentPack pack in dialoguePacks ?? Enumerable.Empty<DialogueContentPack>())
            {
                if (pack == null)
                {
                    continue;
                }

                foreach (DialogueNpcEntry npc in pack.Npcs ?? Enumerable.Empty<DialogueNpcEntry>())
                {
                    if (npc == null)
                    {
                        continue;
                    }

                    count += (npc.Actions ?? Enumerable.Empty<DialogueAction>()).Count(action => action != null);

                    foreach (DialogueNode node in npc.Nodes ?? Enumerable.Empty<DialogueNode>())
                    {
                        if (node == null)
                        {
                            continue;
                        }

                        count += (node.EnterActions ?? Enumerable.Empty<DialogueAction>()).Count(action => action != null);

                        foreach (DialogueOption option in node.Options ?? Enumerable.Empty<DialogueOption>())
                        {
                            if (option == null)
                            {
                                continue;
                            }

                            count += (option.Actions ?? Enumerable.Empty<DialogueAction>()).Count(action => action != null);
                        }
                    }
                }
            }

            return count;
        }

        private static int CountConditions(
            IEnumerable<DialogueContentPack> dialoguePacks,
            IEnumerable<QuestContentPack> questPacks)
        {
            return CountDialogueConditions(dialoguePacks) + CountQuestConditions(questPacks);
        }

        private static int CountDialogueConditions(IEnumerable<DialogueContentPack> dialoguePacks)
        {
            int count = 0;

            foreach (DialogueContentPack pack in dialoguePacks ?? Enumerable.Empty<DialogueContentPack>())
            {
                if (pack == null)
                {
                    continue;
                }

                foreach (DialogueNpcEntry npc in pack.Npcs ?? Enumerable.Empty<DialogueNpcEntry>())
                {
                    if (npc == null)
                    {
                        continue;
                    }

                    count += (npc.Conditions ?? Enumerable.Empty<DialogueCondition>()).Count(condition => condition != null);

                    foreach (DialogueNode node in npc.Nodes ?? Enumerable.Empty<DialogueNode>())
                    {
                        if (node == null)
                        {
                            continue;
                        }

                        foreach (DialogueOption option in node.Options ?? Enumerable.Empty<DialogueOption>())
                        {
                            if (option == null)
                            {
                                continue;
                            }

                            count += (option.Conditions ?? Enumerable.Empty<DialogueCondition>()).Count(
                                condition => condition != null);
                        }
                    }
                }
            }

            return count;
        }

        private static int CountQuestConditions(IEnumerable<QuestContentPack> questPacks)
        {
            int count = 0;

            foreach (QuestContentPack pack in questPacks ?? Enumerable.Empty<QuestContentPack>())
            {
                if (pack == null)
                {
                    continue;
                }

                foreach (QuestDefinition quest in pack.Quests ?? Enumerable.Empty<QuestDefinition>())
                {
                    if (quest == null)
                    {
                        continue;
                    }

                    count += (quest.Conditions ?? Enumerable.Empty<QuestCondition>()).Count(condition => condition != null);

                    foreach (QuestStep step in quest.Steps ?? Enumerable.Empty<QuestStep>())
                    {
                        if (step == null)
                        {
                            continue;
                        }

                        count += (step.Conditions ?? Enumerable.Empty<QuestCondition>()).Count(condition => condition != null);

                        foreach (QuestObjective objective in step.Objectives ?? Enumerable.Empty<QuestObjective>())
                        {
                            if (objective == null)
                            {
                                continue;
                            }

                            count += (objective.Conditions ?? Enumerable.Empty<QuestCondition>()).Count(
                                condition => condition != null);
                        }
                    }
                }
            }

            return count;
        }
    }
}
