namespace ZoneEngine.Core.Arete
{
    #region Usings ...

    using System;
    using System.Collections.Generic;
    using System.Linq;

    #endregion

    public sealed class AreteAggregateValidationStageReport
    {
        private readonly List<string> messages = new List<string>();

        public AreteAggregateValidationStageReport(string name)
        {
            this.Name = name;
        }

        public string Name { get; private set; }

        public bool Executed { get; private set; }

        public IEnumerable<string> Messages
        {
            get
            {
                return this.messages;
            }
        }

        public int ErrorCount
        {
            get
            {
                return this.messages.Count;
            }
        }

        public int WarningCount
        {
            get
            {
                return 0;
            }
        }

        public bool IsValid
        {
            get
            {
                return this.ErrorCount == 0;
            }
        }

        public void MarkExecuted()
        {
            this.Executed = true;
        }

        public void AddMessage(string message)
        {
            this.MarkExecuted();

            if (string.IsNullOrWhiteSpace(message))
            {
                message = "validation error";
            }

            this.messages.Add(message);
        }
    }

    public sealed class AreteAggregateValidationReport
    {
        private readonly Dictionary<string, AreteAggregateValidationStageReport> stagesByName =
            new Dictionary<string, AreteAggregateValidationStageReport>(StringComparer.OrdinalIgnoreCase);

        private readonly List<AreteAggregateValidationStageReport> stages =
            new List<AreteAggregateValidationStageReport>();

        private readonly List<string> validationStagesExecuted = new List<string>();

        public AreteAggregateValidationReport()
        {
            this.ValidationResult = new AreteValidationResult();
        }

        public AreteValidationResult ValidationResult { get; private set; }

        public IEnumerable<AreteAggregateValidationStageReport> Stages
        {
            get
            {
                return this.stages;
            }
        }

        public IEnumerable<string> ValidationStagesExecuted
        {
            get
            {
                return this.validationStagesExecuted;
            }
        }

        public bool IsValid
        {
            get
            {
                return this.ValidationResult.IsValid;
            }
        }

        public int TotalErrorCount
        {
            get
            {
                return this.ValidationResult.ErrorCount;
            }
        }

        public int TotalWarningCount
        {
            get
            {
                return this.stages.Sum(stage => stage.WarningCount);
            }
        }

        public int LoadedDialogueFileCount { get; set; }

        public int LoadedQuestFileCount { get; set; }

        public int LoadedDialoguePackCount { get; set; }

        public int LoadedQuestPackCount { get; set; }

        public int LoadedNpcEntryCount { get; set; }

        public int LoadedQuestDefinitionCount { get; set; }

        public int ActionReferenceValidationCount { get; set; }

        public int ConditionReferenceValidationCount { get; set; }

        public AreteAggregateValidationStageReport EnsureStage(string stageName)
        {
            if (string.IsNullOrWhiteSpace(stageName))
            {
                stageName = "Unknown";
            }

            AreteAggregateValidationStageReport stage;
            if (this.stagesByName.TryGetValue(stageName, out stage))
            {
                return stage;
            }

            stage = new AreteAggregateValidationStageReport(stageName);
            this.stagesByName.Add(stageName, stage);
            this.stages.Add(stage);
            return stage;
        }

        public AreteAggregateValidationStageReport GetStage(string stageName)
        {
            return this.EnsureStage(stageName);
        }

        public void MarkStageExecuted(string stageName)
        {
            AreteAggregateValidationStageReport stage = this.EnsureStage(stageName);
            stage.MarkExecuted();

            if (!this.validationStagesExecuted.Any(
                existing => string.Equals(existing, stage.Name, StringComparison.OrdinalIgnoreCase)))
            {
                this.validationStagesExecuted.Add(stage.Name);
            }
        }

        public void AddStageResult(string stageName, AreteValidationResult validation)
        {
            this.MarkStageExecuted(stageName);

            if (validation == null)
            {
                return;
            }

            foreach (string message in validation.Errors)
            {
                this.AddStageMessage(stageName, message);
            }
        }

        public void AddStageMessage(string stageName, string message)
        {
            this.MarkStageExecuted(stageName);
            this.GetStage(stageName).AddMessage(message);
            this.ValidationResult.AddError(stageName, message);
        }
    }
}
