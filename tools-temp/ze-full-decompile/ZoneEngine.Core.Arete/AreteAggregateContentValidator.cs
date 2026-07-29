using System.Collections.Generic;
using System.IO;
using System.Linq;
using ZoneEngine.Core.Arete.Dialogue;
using ZoneEngine.Core.Arete.Quests;

namespace ZoneEngine.Core.Arete;

public sealed class AreteAggregateContentValidator
{
	private static readonly string[] StageNames = new string[6] { "Load", "DialoguePack", "QuestPack", "Registry", "ActionReference", "ConditionReference" };

	public AreteValidationResult ValidateFiles(IEnumerable<string> dialogueFilePaths, IEnumerable<string> questFilePaths)
	{
		return ValidateFilesWithReport(dialogueFilePaths, questFilePaths).ValidationResult;
	}

	public AreteAggregateValidationReport ValidateFilesWithReport(IEnumerable<string> dialogueFilePaths, IEnumerable<string> questFilePaths)
	{
		AreteContentLoadResult<DialogueContentPack> dialogueLoadResult = AreteJsonContentFileLoader.LoadFiles<DialogueContentPack>(dialogueFilePaths, "dialogue", null);
		AreteContentLoadResult<QuestContentPack> questLoadResult = AreteJsonContentFileLoader.LoadFiles<QuestContentPack>(questFilePaths, "quest", null);
		return ValidateLoadedContentReport(dialogueLoadResult, questLoadResult);
	}

	public AreteValidationResult ValidateManifest(string manifestPath)
	{
		return ValidateManifestWithReport(manifestPath).ValidationResult;
	}

	public AreteAggregateValidationReport ValidateManifestWithReport(string manifestPath)
	{
		AreteContentManifestLoadResult areteContentManifestLoadResult = new AreteContentManifestLoader().Load(manifestPath);
		AreteContentLoadResult<DialogueContentPack> dialogueLoadResult = AreteJsonContentFileLoader.LoadFiles<DialogueContentPack>(areteContentManifestLoadResult.DialoguePackFiles, "dialogue", null);
		AreteContentLoadResult<QuestContentPack> questLoadResult = AreteJsonContentFileLoader.LoadFiles<QuestContentPack>(areteContentManifestLoadResult.QuestPackFiles, "quest", null);
		AreteAggregateValidationReport areteAggregateValidationReport = ValidateLoadedContentReport(dialogueLoadResult, questLoadResult);
		areteAggregateValidationReport.AddStageResult("Load", areteContentManifestLoadResult.Validation);
		return areteAggregateValidationReport;
	}

	public AreteValidationResult ValidateDirectory(string contentDirectory)
	{
		return ValidateDirectoryWithReport(contentDirectory).ValidationResult;
	}

	public AreteAggregateValidationReport ValidateDirectoryWithReport(string contentDirectory)
	{
		if (string.IsNullOrWhiteSpace(contentDirectory))
		{
			AreteAggregateValidationReport areteAggregateValidationReport = CreateReport();
			areteAggregateValidationReport.AddStageMessage("Load", "missing aggregate content directory path");
			return areteAggregateValidationReport;
		}
		return ValidateDirectoriesWithReport(Path.Combine(contentDirectory, "dialogue"), Path.Combine(contentDirectory, "quests"));
	}

	public AreteValidationResult ValidateDirectories(string dialogueDirectory, string questDirectory)
	{
		return ValidateDirectoriesWithReport(dialogueDirectory, questDirectory).ValidationResult;
	}

	public AreteAggregateValidationReport ValidateDirectoriesWithReport(string dialogueDirectory, string questDirectory)
	{
		AreteContentLoadResult<DialogueContentPack> dialogueLoadResult = AreteJsonContentFileLoader.LoadDirectory<DialogueContentPack>(dialogueDirectory, "dialogue", null);
		AreteContentLoadResult<QuestContentPack> questLoadResult = AreteJsonContentFileLoader.LoadDirectory<QuestContentPack>(questDirectory, "quest", null);
		return ValidateLoadedContentReport(dialogueLoadResult, questLoadResult);
	}

	private AreteAggregateValidationReport ValidateLoadedContentReport(AreteContentLoadResult<DialogueContentPack> dialogueLoadResult, AreteContentLoadResult<QuestContentPack> questLoadResult)
	{
		AreteAggregateValidationReport areteAggregateValidationReport = CreateReport();
		IList<DialogueContentPack> packs = GetPacks(dialogueLoadResult);
		IList<QuestContentPack> packs2 = GetPacks(questLoadResult);
		areteAggregateValidationReport.LoadedDialogueFileCount = packs.Count;
		areteAggregateValidationReport.LoadedQuestFileCount = packs2.Count;
		areteAggregateValidationReport.LoadedDialoguePackCount = packs.Count;
		areteAggregateValidationReport.LoadedQuestPackCount = packs2.Count;
		areteAggregateValidationReport.LoadedNpcEntryCount = CountNpcEntries(packs);
		areteAggregateValidationReport.LoadedQuestDefinitionCount = CountQuestDefinitions(packs2);
		areteAggregateValidationReport.ActionReferenceValidationCount = CountDialogueActions(packs);
		areteAggregateValidationReport.ConditionReferenceValidationCount = CountConditions(packs, packs2);
		areteAggregateValidationReport.AddStageResult("Load", dialogueLoadResult?.Validation);
		areteAggregateValidationReport.AddStageResult("Load", questLoadResult?.Validation);
		AreteValidationResult areteValidationResult = DialogueContentPackValidator.Validate(packs);
		AreteValidationResult areteValidationResult2 = QuestContentPackValidator.Validate(packs2);
		areteAggregateValidationReport.AddStageResult("DialoguePack", areteValidationResult);
		areteAggregateValidationReport.AddStageResult("QuestPack", areteValidationResult2);
		DialogueContentRegistry dialogueContentRegistry = new DialogueContentRegistry();
		QuestContentRegistry questContentRegistry = new QuestContentRegistry();
		bool flag = false;
		bool flag2 = false;
		if (areteValidationResult.IsValid)
		{
			AreteValidationResult areteValidationResult3 = dialogueContentRegistry.Load(packs);
			areteAggregateValidationReport.AddStageResult("Registry", areteValidationResult3);
			flag = areteValidationResult3.IsValid;
		}
		if (areteValidationResult2.IsValid)
		{
			AreteValidationResult areteValidationResult4 = questContentRegistry.Load(packs2);
			areteAggregateValidationReport.AddStageResult("Registry", areteValidationResult4);
			flag2 = areteValidationResult4.IsValid;
		}
		if (flag2)
		{
			AreteValidationResult validation = DialogueActionReferenceValidator.Validate(packs, questContentRegistry);
			areteAggregateValidationReport.AddStageResult("ActionReference", validation);
		}
		AreteValidationResult validation2 = AreteConditionReferenceValidator.Validate(packs, packs2, flag ? dialogueContentRegistry : null, flag2 ? questContentRegistry : null);
		areteAggregateValidationReport.AddStageResult("ConditionReference", validation2);
		return areteAggregateValidationReport;
	}

	private static AreteAggregateValidationReport CreateReport()
	{
		AreteAggregateValidationReport areteAggregateValidationReport = new AreteAggregateValidationReport();
		string[] stageNames = StageNames;
		foreach (string stageName in stageNames)
		{
			areteAggregateValidationReport.EnsureStage(stageName);
		}
		return areteAggregateValidationReport;
	}

	private static IList<TPack> GetPacks<TPack>(AreteContentLoadResult<TPack> loadResult)
	{
		if (loadResult == null)
		{
			return new List<TPack>();
		}
		IEnumerable<TPack> packs = loadResult.Packs;
		return new List<TPack>(packs ?? Enumerable.Empty<TPack>());
	}

	private static int CountNpcEntries(IEnumerable<DialogueContentPack> dialoguePacks)
	{
		return (dialoguePacks ?? Enumerable.Empty<DialogueContentPack>()).Where((DialogueContentPack pack) => pack != null).Sum(delegate(DialogueContentPack pack)
		{
			IEnumerable<DialogueNpcEntry> npcs = pack.Npcs;
			return (npcs ?? Enumerable.Empty<DialogueNpcEntry>()).Count((DialogueNpcEntry npc) => npc != null);
		});
	}

	private static int CountQuestDefinitions(IEnumerable<QuestContentPack> questPacks)
	{
		return (questPacks ?? Enumerable.Empty<QuestContentPack>()).Where((QuestContentPack pack) => pack != null).Sum(delegate(QuestContentPack pack)
		{
			IEnumerable<QuestDefinition> quests = pack.Quests;
			return (quests ?? Enumerable.Empty<QuestDefinition>()).Count((QuestDefinition quest) => quest != null);
		});
	}

	private static int CountDialogueActions(IEnumerable<DialogueContentPack> dialoguePacks)
	{
		int num = 0;
		foreach (DialogueContentPack item in dialoguePacks ?? Enumerable.Empty<DialogueContentPack>())
		{
			if (item == null)
			{
				continue;
			}
			IEnumerable<DialogueNpcEntry> npcs = item.Npcs;
			foreach (DialogueNpcEntry item2 in npcs ?? Enumerable.Empty<DialogueNpcEntry>())
			{
				if (item2 == null)
				{
					continue;
				}
				int num2 = num;
				IEnumerable<DialogueAction> actions = item2.Actions;
				num = num2 + (actions ?? Enumerable.Empty<DialogueAction>()).Count((DialogueAction action) => action != null);
				IEnumerable<DialogueNode> nodes = item2.Nodes;
				foreach (DialogueNode item3 in nodes ?? Enumerable.Empty<DialogueNode>())
				{
					if (item3 == null)
					{
						continue;
					}
					int num3 = num;
					actions = item3.EnterActions;
					num = num3 + (actions ?? Enumerable.Empty<DialogueAction>()).Count((DialogueAction action) => action != null);
					IEnumerable<DialogueOption> options = item3.Options;
					foreach (DialogueOption item4 in options ?? Enumerable.Empty<DialogueOption>())
					{
						if (item4 != null)
						{
							int num4 = num;
							actions = item4.Actions;
							num = num4 + (actions ?? Enumerable.Empty<DialogueAction>()).Count((DialogueAction action) => action != null);
						}
					}
				}
			}
		}
		return num;
	}

	private static int CountConditions(IEnumerable<DialogueContentPack> dialoguePacks, IEnumerable<QuestContentPack> questPacks)
	{
		return CountDialogueConditions(dialoguePacks) + CountQuestConditions(questPacks);
	}

	private static int CountDialogueConditions(IEnumerable<DialogueContentPack> dialoguePacks)
	{
		int num = 0;
		foreach (DialogueContentPack item in dialoguePacks ?? Enumerable.Empty<DialogueContentPack>())
		{
			if (item == null)
			{
				continue;
			}
			IEnumerable<DialogueNpcEntry> npcs = item.Npcs;
			foreach (DialogueNpcEntry item2 in npcs ?? Enumerable.Empty<DialogueNpcEntry>())
			{
				if (item2 == null)
				{
					continue;
				}
				int num2 = num;
				IEnumerable<DialogueCondition> conditions = item2.Conditions;
				num = num2 + (conditions ?? Enumerable.Empty<DialogueCondition>()).Count((DialogueCondition condition) => condition != null);
				IEnumerable<DialogueNode> nodes = item2.Nodes;
				foreach (DialogueNode item3 in nodes ?? Enumerable.Empty<DialogueNode>())
				{
					if (item3 == null)
					{
						continue;
					}
					IEnumerable<DialogueOption> options = item3.Options;
					foreach (DialogueOption item4 in options ?? Enumerable.Empty<DialogueOption>())
					{
						if (item4 != null)
						{
							int num3 = num;
							conditions = item4.Conditions;
							num = num3 + (conditions ?? Enumerable.Empty<DialogueCondition>()).Count((DialogueCondition condition) => condition != null);
						}
					}
				}
			}
		}
		return num;
	}

	private static int CountQuestConditions(IEnumerable<QuestContentPack> questPacks)
	{
		int num = 0;
		foreach (QuestContentPack item in questPacks ?? Enumerable.Empty<QuestContentPack>())
		{
			if (item == null)
			{
				continue;
			}
			IEnumerable<QuestDefinition> quests = item.Quests;
			foreach (QuestDefinition item2 in quests ?? Enumerable.Empty<QuestDefinition>())
			{
				if (item2 == null)
				{
					continue;
				}
				int num2 = num;
				IEnumerable<QuestCondition> conditions = item2.Conditions;
				num = num2 + (conditions ?? Enumerable.Empty<QuestCondition>()).Count((QuestCondition condition) => condition != null);
				IEnumerable<QuestStep> steps = item2.Steps;
				foreach (QuestStep item3 in steps ?? Enumerable.Empty<QuestStep>())
				{
					if (item3 == null)
					{
						continue;
					}
					int num3 = num;
					conditions = item3.Conditions;
					num = num3 + (conditions ?? Enumerable.Empty<QuestCondition>()).Count((QuestCondition condition) => condition != null);
					IEnumerable<QuestObjective> objectives = item3.Objectives;
					foreach (QuestObjective item4 in objectives ?? Enumerable.Empty<QuestObjective>())
					{
						if (item4 != null)
						{
							int num4 = num;
							conditions = item4.Conditions;
							num = num4 + (conditions ?? Enumerable.Empty<QuestCondition>()).Count((QuestCondition condition) => condition != null);
						}
					}
				}
			}
		}
		return num;
	}
}
