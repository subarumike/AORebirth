using System;
using System.Collections.Generic;
using System.Linq;

namespace ZoneEngine.Core.Arete;

public sealed class AreteAggregateValidationReport
{
	private readonly Dictionary<string, AreteAggregateValidationStageReport> stagesByName = new Dictionary<string, AreteAggregateValidationStageReport>(StringComparer.OrdinalIgnoreCase);

	private readonly List<AreteAggregateValidationStageReport> stages = new List<AreteAggregateValidationStageReport>();

	private readonly List<string> validationStagesExecuted = new List<string>();

	public AreteValidationResult ValidationResult { get; private set; }

	public IEnumerable<AreteAggregateValidationStageReport> Stages => stages;

	public IEnumerable<string> ValidationStagesExecuted => validationStagesExecuted;

	public bool IsValid => ValidationResult.IsValid;

	public int TotalErrorCount => ValidationResult.ErrorCount;

	public int TotalWarningCount => stages.Sum((AreteAggregateValidationStageReport stage) => stage.WarningCount);

	public int LoadedDialogueFileCount { get; set; }

	public int LoadedQuestFileCount { get; set; }

	public int LoadedDialoguePackCount { get; set; }

	public int LoadedQuestPackCount { get; set; }

	public int LoadedNpcEntryCount { get; set; }

	public int LoadedQuestDefinitionCount { get; set; }

	public int ActionReferenceValidationCount { get; set; }

	public int ConditionReferenceValidationCount { get; set; }

	public AreteAggregateValidationReport()
	{
		ValidationResult = new AreteValidationResult();
	}

	public AreteAggregateValidationStageReport EnsureStage(string stageName)
	{
		if (string.IsNullOrWhiteSpace(stageName))
		{
			stageName = "Unknown";
		}
		if (stagesByName.TryGetValue(stageName, out var value))
		{
			return value;
		}
		value = new AreteAggregateValidationStageReport(stageName);
		stagesByName.Add(stageName, value);
		stages.Add(value);
		return value;
	}

	public AreteAggregateValidationStageReport GetStage(string stageName)
	{
		return EnsureStage(stageName);
	}

	public void MarkStageExecuted(string stageName)
	{
		AreteAggregateValidationStageReport stage = EnsureStage(stageName);
		stage.MarkExecuted();
		if (!validationStagesExecuted.Any((string existing) => string.Equals(existing, stage.Name, StringComparison.OrdinalIgnoreCase)))
		{
			validationStagesExecuted.Add(stage.Name);
		}
	}

	public void AddStageResult(string stageName, AreteValidationResult validation)
	{
		MarkStageExecuted(stageName);
		if (validation == null)
		{
			return;
		}
		foreach (string error in validation.Errors)
		{
			AddStageMessage(stageName, error);
		}
	}

	public void AddStageMessage(string stageName, string message)
	{
		MarkStageExecuted(stageName);
		GetStage(stageName).AddMessage(message);
		ValidationResult.AddError(stageName, message);
	}
}
