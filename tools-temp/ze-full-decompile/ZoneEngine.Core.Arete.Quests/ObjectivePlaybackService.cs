using System;
using System.Collections.Generic;
using System.Linq;

namespace ZoneEngine.Core.Arete.Quests;

public sealed class ObjectivePlaybackService
{
	private sealed class ObjectiveBinding
	{
		public QuestDefinition Quest { get; private set; }

		public QuestStep Step { get; private set; }

		public QuestObjective Objective { get; private set; }

		public QuestAction EvidenceAction { get; private set; }

		public ObjectiveBinding(QuestDefinition quest, QuestStep step, QuestObjective objective, QuestAction evidenceAction)
		{
			Quest = quest;
			Step = step;
			Objective = objective;
			EvidenceAction = evidenceAction;
		}
	}

	private const string KillCountObjectiveType = "CapturedKillCountObjective";

	private const string UseInteractObjectiveType = "CapturedUseInteractObjective";

	private const string TalkToNpcObjectiveType = "CapturedTalkToNpcObjective";

	private readonly Dictionary<string, ObjectiveProgressRecord> progressByKey = new Dictionary<string, ObjectiveProgressRecord>(StringComparer.OrdinalIgnoreCase);

	private readonly QuestContentRegistry registry;

	public ObjectivePlaybackService(QuestContentRegistry registry)
	{
		this.registry = registry;
	}

	public ObjectivePlaybackObservationResult Observe(int characterId, ObjectivePlaybackObservation observation)
	{
		AreteValidationResult areteValidationResult = new AreteValidationResult();
		List<ObjectiveProgressRecord> list = new List<ObjectiveProgressRecord>();
		List<ObjectiveProgressRecord> list2 = new List<ObjectiveProgressRecord>();
		if (characterId <= 0)
		{
			areteValidationResult.AddError("objectivePlayback", "stable character identity must be positive");
			return new ObjectivePlaybackObservationResult(observation, list, list2, areteValidationResult);
		}
		if (observation == null)
		{
			areteValidationResult.AddError("objectivePlayback", "observation is missing");
			return new ObjectivePlaybackObservationResult(observation, list, list2, areteValidationResult);
		}
		if (string.IsNullOrWhiteSpace(observation.ObservationType))
		{
			areteValidationResult.AddError("objectivePlayback", "observation type is missing");
			return new ObjectivePlaybackObservationResult(observation, list, list2, areteValidationResult);
		}
		foreach (ObjectiveBinding supportedObjectiveBinding in GetSupportedObjectiveBindings())
		{
			if (!IsRelevant(supportedObjectiveBinding.Objective, observation))
			{
				continue;
			}
			ObjectiveProgressRecord orCreateProgress = GetOrCreateProgress(characterId, supportedObjectiveBinding.Quest, supportedObjectiveBinding.Objective);
			if (Matches(supportedObjectiveBinding, observation))
			{
				orCreateProgress.MatchedEvidenceCount++;
				orCreateProgress.LastMatchedEvidenceReference = observation.EvidenceReference;
				if (!orCreateProgress.Completed)
				{
					orCreateProgress.CurrentCount = Math.Min(orCreateProgress.CurrentCount + 1, orCreateProgress.RequiredCount);
					orCreateProgress.Completed = orCreateProgress.CurrentCount >= orCreateProgress.RequiredCount;
				}
				list.Add(orCreateProgress);
			}
			else
			{
				orCreateProgress.IgnoredEvidenceCount++;
				list2.Add(orCreateProgress);
			}
		}
		return new ObjectivePlaybackObservationResult(observation, list, list2, areteValidationResult);
	}

	public ObjectivePlaybackReplayResult ReplayStoredObjectiveEvidence(int characterId)
	{
		AreteValidationResult areteValidationResult = new AreteValidationResult();
		List<ObjectivePlaybackObservationResult> list = new List<ObjectivePlaybackObservationResult>();
		foreach (ObjectiveBinding supportedObjectiveBinding in GetSupportedObjectiveBindings())
		{
			IList<ObjectivePlaybackObservation> list2 = CreateObservationsFromStoredEvidence(supportedObjectiveBinding, areteValidationResult);
			foreach (ObjectivePlaybackObservation item in list2)
			{
				ObjectivePlaybackObservationResult objectivePlaybackObservationResult = Observe(characterId, item);
				list.Add(objectivePlaybackObservationResult);
				areteValidationResult.AddErrors(objectivePlaybackObservationResult.Validation);
			}
		}
		return new ObjectivePlaybackReplayResult(list, GetAllProgress(characterId), areteValidationResult);
	}

	public ObjectiveProgressRecord GetProgress(int characterId, string missionId, string objectiveId)
	{
		if (characterId <= 0)
		{
			throw new ArgumentOutOfRangeException("characterId", "Stable character identity must be positive.");
		}
		if (progressByKey.TryGetValue(MakeKey(characterId, missionId, objectiveId), out var value))
		{
			return value;
		}
		if (TryFindObjective(missionId, objectiveId, out var quest, out var objective))
		{
			return GetOrCreateProgress(characterId, quest, objective);
		}
		return null;
	}

	public IList<ObjectiveProgressRecord> GetAllProgress(int characterId)
	{
		if (characterId <= 0)
		{
			throw new ArgumentOutOfRangeException("characterId", "Stable character identity must be positive.");
		}
		foreach (ObjectiveBinding supportedObjectiveBinding in GetSupportedObjectiveBindings())
		{
			GetOrCreateProgress(characterId, supportedObjectiveBinding.Quest, supportedObjectiveBinding.Objective);
		}
		return (from progress in progressByKey.Values
			where progress.CharacterId == characterId
			orderby progress.MissionId, progress.ObjectiveId
			select progress).ToList();
	}

	private IList<ObjectivePlaybackObservation> CreateObservationsFromStoredEvidence(ObjectiveBinding binding, AreteValidationResult validation)
	{
		List<ObjectivePlaybackObservation> list = new List<ObjectivePlaybackObservation>();
		IDictionary<string, string> parameters = binding.EvidenceAction?.Parameters;
		if (IsObjectiveType(binding.Objective, "CapturedKillCountObjective"))
		{
			string parameter = GetParameter(parameters, "targetName");
			string parameter2 = GetParameter(parameters, "deathSignal");
			IList<string> list2 = SplitEvidenceReferences(GetParameter(parameters, "observedDeathReferences"));
			if (list2.Count == 0)
			{
				validation.AddError(binding.Objective.ObjectiveId, "missing stored death evidence references");
				return list;
			}
			foreach (string item in list2)
			{
				list.Add(new ObjectivePlaybackObservation
				{
					ObservationType = "EnemyDeathObserved",
					EvidenceReference = item,
					TargetName = parameter,
					CapturedSignal = parameter2
				});
			}
		}
		else if (IsObjectiveType(binding.Objective, "CapturedUseInteractObjective"))
		{
			string text = FirstEvidenceReference(GetParameter(parameters, "usePacketReferences"));
			if (string.IsNullOrWhiteSpace(text))
			{
				validation.AddError(binding.Objective.ObjectiveId, "missing stored use-interaction evidence reference");
				return list;
			}
			list.Add(new ObjectivePlaybackObservation
			{
				ObservationType = "UseInteractionObserved",
				EvidenceReference = text,
				TargetName = GetParameter(parameters, "targetName"),
				TargetIdentity = GetParameter(parameters, "targetIdentityCandidate"),
				CapturedSignal = GetParameter(parameters, "useSignal"),
				ActionName = "Use"
			});
		}
		else if (IsObjectiveType(binding.Objective, "CapturedTalkToNpcObjective"))
		{
			string text2 = FirstEvidenceReference(GetParameter(parameters, "talkPacketReferences"));
			if (string.IsNullOrWhiteSpace(text2))
			{
				validation.AddError(binding.Objective.ObjectiveId, "missing stored NPC talk evidence reference");
				return list;
			}
			list.Add(new ObjectivePlaybackObservation
			{
				ObservationType = "NpcTalkObserved",
				EvidenceReference = text2,
				TargetName = GetParameter(parameters, "targetName"),
				TargetIdentity = FirstNonEmpty(GetParameter(parameters, "targetIdentity"), binding.Objective.TargetIdentity),
				CapturedSignal = GetParameter(parameters, "talkSignal")
			});
		}
		return list;
	}

	private IEnumerable<ObjectiveBinding> GetSupportedObjectiveBindings()
	{
		if (registry == null)
		{
			return Enumerable.Empty<ObjectiveBinding>();
		}
		List<ObjectiveBinding> list = new List<ObjectiveBinding>();
		foreach (QuestDefinition quest in registry.GetQuests())
		{
			if (quest == null)
			{
				continue;
			}
			IEnumerable<QuestStep> steps = quest.Steps;
			foreach (QuestStep item in steps ?? Enumerable.Empty<QuestStep>())
			{
				if (item == null)
				{
					continue;
				}
				IEnumerable<QuestObjective> objectives = item.Objectives;
				foreach (QuestObjective item2 in objectives ?? Enumerable.Empty<QuestObjective>())
				{
					if (item2 != null && IsSupportedObjective(item2))
					{
						list.Add(new ObjectiveBinding(quest, item, item2, FindObjectiveEvidenceAction(item, item2)));
					}
				}
			}
		}
		return list;
	}

	private bool Matches(ObjectiveBinding binding, ObjectivePlaybackObservation observation)
	{
		if (IsObjectiveType(binding.Objective, "CapturedKillCountObjective"))
		{
			string parameter = GetParameter((binding.EvidenceAction == null) ? null : binding.EvidenceAction.Parameters, "targetName");
			return string.Equals(parameter, observation.TargetName, StringComparison.OrdinalIgnoreCase);
		}
		if (IsObjectiveType(binding.Objective, "CapturedUseInteractObjective"))
		{
			return IsUseSignal(observation.CapturedSignal) || string.Equals(observation.ActionName, "Use", StringComparison.OrdinalIgnoreCase);
		}
		if (IsObjectiveType(binding.Objective, "CapturedTalkToNpcObjective"))
		{
			string a = FirstNonEmpty(binding.Objective.TargetIdentity, GetParameter((binding.EvidenceAction == null) ? null : binding.EvidenceAction.Parameters, "targetIdentity"));
			return string.Equals(a, observation.TargetIdentity, StringComparison.OrdinalIgnoreCase);
		}
		return false;
	}

	private ObjectiveProgressRecord GetOrCreateProgress(int characterId, QuestDefinition quest, QuestObjective objective)
	{
		string key = MakeKey(characterId, quest?.QuestId, objective?.ObjectiveId);
		if (progressByKey.TryGetValue(key, out var value))
		{
			return value;
		}
		value = new ObjectiveProgressRecord
		{
			CharacterId = characterId,
			MissionId = quest?.QuestId,
			ObjectiveId = objective?.ObjectiveId,
			ObjectiveType = objective?.Type,
			RequiredCount = EffectiveRequiredCount(objective)
		};
		progressByKey[key] = value;
		return value;
	}

	private bool TryFindObjective(string missionId, string objectiveId, out QuestDefinition quest, out QuestObjective objective)
	{
		quest = null;
		objective = null;
		if (registry == null || string.IsNullOrWhiteSpace(missionId) || string.IsNullOrWhiteSpace(objectiveId))
		{
			return false;
		}
		if (!registry.TryGetQuest(missionId, out quest) || quest == null)
		{
			return false;
		}
		IEnumerable<QuestStep> steps = quest.Steps;
		objective = (steps ?? Enumerable.Empty<QuestStep>()).Where((QuestStep step) => step != null).SelectMany(delegate(QuestStep step)
		{
			IEnumerable<QuestObjective> objectives = step.Objectives;
			return objectives ?? Enumerable.Empty<QuestObjective>();
		}).FirstOrDefault((QuestObjective candidate) => candidate != null && string.Equals(candidate.ObjectiveId, objectiveId, StringComparison.OrdinalIgnoreCase));
		return objective != null;
	}

	private static QuestAction FindObjectiveEvidenceAction(QuestStep step, QuestObjective objective)
	{
		if (step == null || objective == null)
		{
			return null;
		}
		IEnumerable<QuestAction> actions = step.Actions;
		return (actions ?? Enumerable.Empty<QuestAction>()).FirstOrDefault((QuestAction action) => action != null && string.Equals(GetParameter(action.Parameters, "eventKind"), "objective-trigger-evidence", StringComparison.OrdinalIgnoreCase) && string.Equals(GetParameter(action.Parameters, "objectiveId"), objective.ObjectiveId, StringComparison.OrdinalIgnoreCase));
	}

	private static bool IsRelevant(QuestObjective objective, ObjectivePlaybackObservation observation)
	{
		if (IsObjectiveType(objective, "CapturedKillCountObjective"))
		{
			return string.Equals(observation.ObservationType, "EnemyDeathObserved", StringComparison.OrdinalIgnoreCase);
		}
		if (IsObjectiveType(objective, "CapturedUseInteractObjective"))
		{
			return string.Equals(observation.ObservationType, "UseInteractionObserved", StringComparison.OrdinalIgnoreCase);
		}
		if (IsObjectiveType(objective, "CapturedTalkToNpcObjective"))
		{
			return string.Equals(observation.ObservationType, "NpcTalkObserved", StringComparison.OrdinalIgnoreCase);
		}
		return false;
	}

	private static bool IsSupportedObjective(QuestObjective objective)
	{
		return IsObjectiveType(objective, "CapturedKillCountObjective") || IsObjectiveType(objective, "CapturedUseInteractObjective") || IsObjectiveType(objective, "CapturedTalkToNpcObjective");
	}

	private static bool IsObjectiveType(QuestObjective objective, string objectiveType)
	{
		return objective != null && string.Equals(objective.Type, objectiveType, StringComparison.OrdinalIgnoreCase);
	}

	private static bool IsUseSignal(string capturedSignal)
	{
		return string.Equals(capturedSignal, "GenericCmd Action=Use", StringComparison.OrdinalIgnoreCase) || string.Equals(capturedSignal, "Use", StringComparison.OrdinalIgnoreCase);
	}

	private static int EffectiveRequiredCount(QuestObjective objective)
	{
		if (objective != null && objective.RequiredCount > 0)
		{
			return objective.RequiredCount;
		}
		return 1;
	}

	private static string GetParameter(IDictionary<string, string> parameters, string key)
	{
		if (parameters != null && !string.IsNullOrWhiteSpace(key) && parameters.TryGetValue(key, out var value))
		{
			return value;
		}
		return null;
	}

	private static IList<string> SplitEvidenceReferences(string value)
	{
		if (string.IsNullOrWhiteSpace(value))
		{
			return new List<string>();
		}
		string text = null;
		List<string> list = new List<string>();
		foreach (string item in from reference in value.Split(new char[2] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries)
			select reference.Trim() into reference
			where reference.Length > 0
			select reference)
		{
			string text2 = item;
			int num = text2.LastIndexOf(':');
			if (num > 0 && text2.Substring(0, num).IndexOf(".log", StringComparison.OrdinalIgnoreCase) >= 0)
			{
				text = text2.Substring(0, num + 1);
			}
			else if (text != null && IsLineNumberReference(text2))
			{
				text2 = text + text2;
			}
			list.Add(text2);
		}
		return list;
	}

	private static string FirstEvidenceReference(string value)
	{
		return SplitEvidenceReferences(value).FirstOrDefault();
	}

	private static bool IsLineNumberReference(string value)
	{
		int result;
		return int.TryParse(value, out result);
	}

	private static string FirstNonEmpty(params string[] values)
	{
		string[] array = values ?? new string[0];
		foreach (string text in array)
		{
			if (!string.IsNullOrWhiteSpace(text))
			{
				return text;
			}
		}
		return null;
	}

	private static string MakeKey(int characterId, string missionId, string objectiveId)
	{
		return characterId + "|" + (missionId ?? string.Empty) + "|" + (objectiveId ?? string.Empty);
	}
}
