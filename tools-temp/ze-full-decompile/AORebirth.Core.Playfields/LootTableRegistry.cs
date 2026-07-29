using System;
using System.Collections.Generic;
using System.Linq;

namespace AORebirth.Core.Playfields;

internal sealed class LootTableRegistry
{
	private readonly Dictionary<string, LootTableDefinition> tables = new Dictionary<string, LootTableDefinition>(StringComparer.OrdinalIgnoreCase);

	private readonly Dictionary<string, LootAssignmentDefinition> assignments = new Dictionary<string, LootAssignmentDefinition>(StringComparer.OrdinalIgnoreCase);

	private readonly Func<int, bool> itemTemplateExists;

	internal string Version { get; private set; }

	internal LootTableRegistry(Func<int, bool> itemTemplateExists)
	{
		this.itemTemplateExists = itemTemplateExists ?? ((Func<int, bool>)((int value) => value > 0));
	}

	internal void RegisterTable(LootTableDefinition table)
	{
		ValidateTable(table);
		if (tables.ContainsKey(table.LootTableKey))
		{
			throw new LootDefinitionValidationException("Duplicate loot table key: " + table.LootTableKey);
		}
		tables.Add(table.LootTableKey, table);
		RefreshVersion();
	}

	internal void RegisterAssignment(LootAssignmentDefinition assignment)
	{
		ValidateAssignment(assignment);
		if (assignments.ContainsKey(assignment.AssignmentKey))
		{
			throw new LootDefinitionValidationException("Duplicate loot assignment key: " + assignment.AssignmentKey);
		}
		if (!tables.ContainsKey(assignment.LootTableKey))
		{
			throw new LootDefinitionValidationException("Assignment references missing table: " + assignment.LootTableKey);
		}
		ValidateActiveAssignmentOwner(assignment);
		assignments.Add(assignment.AssignmentKey, assignment);
		RefreshVersion();
	}

	internal void RegisterTableAndAssignment(LootTableDefinition table, LootAssignmentDefinition assignment)
	{
		ValidateTable(table);
		ValidateAssignment(assignment);
		if (tables.ContainsKey(table.LootTableKey))
		{
			throw new LootDefinitionValidationException("Duplicate loot table key: " + table.LootTableKey);
		}
		if (assignments.ContainsKey(assignment.AssignmentKey))
		{
			throw new LootDefinitionValidationException("Duplicate loot assignment key: " + assignment.AssignmentKey);
		}
		if (!string.Equals(table.LootTableKey, assignment.LootTableKey, StringComparison.OrdinalIgnoreCase))
		{
			throw new LootDefinitionValidationException("Atomic loot registration requires the assignment to reference its table: " + assignment.AssignmentKey);
		}
		ValidateActiveAssignmentOwner(assignment);
		tables.Add(table.LootTableKey, table);
		assignments.Add(assignment.AssignmentKey, assignment);
		RefreshVersion();
	}

	internal bool ContainsTable(string key)
	{
		return tables.ContainsKey(key);
	}

	internal bool ContainsAssignment(string key)
	{
		return assignments.ContainsKey(key);
	}

	internal LootTableDefinition GetTable(string key)
	{
		return tables[key];
	}

	internal LootAssignmentDefinition[] Assignments()
	{
		return assignments.Values.ToArray();
	}

	private void ValidateTable(LootTableDefinition table)
	{
		if (table == null || string.IsNullOrWhiteSpace(table.LootTableKey))
		{
			throw new LootDefinitionValidationException("Loot table key is required.");
		}
		if (table.RollGroups == null)
		{
			table.RollGroups = new LootGroupDefinition[0];
		}
		if (table.ObservedCorpseSnapshots == null)
		{
			table.ObservedCorpseSnapshots = new ObservedCorpseSnapshotDefinition[0];
		}
		if (table.CreditsPolicy == null)
		{
			throw new LootDefinitionValidationException("Credits policy is required for " + table.LootTableKey);
		}
		ValidateCredits(table.CreditsPolicy, table.LootTableKey);
		ValidateObservedCorpseSnapshots(table);
		HashSet<string> hashSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		LootGroupDefinition[] rollGroups = table.RollGroups;
		foreach (LootGroupDefinition lootGroupDefinition in rollGroups)
		{
			if (lootGroupDefinition == null || string.IsNullOrWhiteSpace(lootGroupDefinition.LootGroupKey) || !hashSet.Add(lootGroupDefinition.LootGroupKey))
			{
				throw new LootDefinitionValidationException("Invalid or duplicate loot group in " + table.LootTableKey);
			}
			if (lootGroupDefinition.RollCount < 0 || lootGroupDefinition.EmptyWeight < 0 || lootGroupDefinition.DropChanceBasisPoints < 0 || lootGroupDefinition.DropChanceBasisPoints > 10000 || lootGroupDefinition.Entries == null)
			{
				throw new LootDefinitionValidationException("Invalid roll count or entries in " + lootGroupDefinition.LootGroupKey);
			}
			LootEntryDefinition[] entries = lootGroupDefinition.Entries;
			foreach (LootEntryDefinition entry in entries)
			{
				ValidateEntry(entry, lootGroupDefinition.LootGroupKey, table.Enabled);
			}
		}
	}

	private void ValidateObservedCorpseSnapshots(LootTableDefinition table)
	{
		if (table.ObservedCorpseSnapshots.Length == 0)
		{
			return;
		}
		if (table.RollGroups.Length != 0)
		{
			throw new LootDefinitionValidationException("Observed corpse snapshots cannot be combined with independent roll groups in " + table.LootTableKey);
		}
		if (!table.ItemPoolUnresolved)
		{
			throw new LootDefinitionValidationException("Observed corpse snapshots require an unresolved wider item pool in " + table.LootTableKey);
		}
		if (table.CreditsPolicy.Mode != CreditsPolicyMode.Unresolved || table.CreditsPolicy.Evidence != LootEvidenceConfidence.Unresolved)
		{
			throw new LootDefinitionValidationException("Observed corpse snapshots require unresolved independent credit probability in " + table.LootTableKey);
		}
		HashSet<string> hashSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		ObservedCorpseSnapshotDefinition[] observedCorpseSnapshots = table.ObservedCorpseSnapshots;
		foreach (ObservedCorpseSnapshotDefinition observedCorpseSnapshotDefinition in observedCorpseSnapshots)
		{
			if (observedCorpseSnapshotDefinition == null || string.IsNullOrWhiteSpace(observedCorpseSnapshotDefinition.SnapshotKey) || !hashSet.Add(observedCorpseSnapshotDefinition.SnapshotKey))
			{
				throw new LootDefinitionValidationException("Invalid or duplicate observed corpse snapshot in " + table.LootTableKey);
			}
			if (observedCorpseSnapshotDefinition.Credits < 0 || observedCorpseSnapshotDefinition.Entries == null || observedCorpseSnapshotDefinition.Entries.Length == 0 || observedCorpseSnapshotDefinition.Evidence == LootEvidenceConfidence.Unresolved || observedCorpseSnapshotDefinition.SelectionProbabilityEvidence != LootEvidenceConfidence.Unresolved || string.IsNullOrWhiteSpace(observedCorpseSnapshotDefinition.EvidenceReference))
			{
				throw new LootDefinitionValidationException("Observed corpse snapshot evidence is incomplete in " + observedCorpseSnapshotDefinition.SnapshotKey);
			}
			LootEntryDefinition[] entries = observedCorpseSnapshotDefinition.Entries;
			foreach (LootEntryDefinition lootEntryDefinition in entries)
			{
				ValidateEntry(lootEntryDefinition, observedCorpseSnapshotDefinition.SnapshotKey, table.Enabled);
				if (!lootEntryDefinition.FixedQuality.HasValue || lootEntryDefinition.MinimumQuality != lootEntryDefinition.FixedQuality.Value || lootEntryDefinition.MaximumQuality != lootEntryDefinition.FixedQuality.Value || lootEntryDefinition.MinimumQuantity != lootEntryDefinition.MaximumQuantity || lootEntryDefinition.Weight != 0 || lootEntryDefinition.DropChanceBasisPoints != 0 || lootEntryDefinition.Semantics != LootSemantics.ObservedAvailable)
				{
					throw new LootDefinitionValidationException("Observed corpse snapshot entries must preserve exact non-probabilistic values in " + observedCorpseSnapshotDefinition.SnapshotKey);
				}
			}
		}
	}

	private void ValidateCredits(CreditsPolicyDefinition policy, string tableKey)
	{
		if (policy.MinimumCredits < 0 || policy.MaximumCredits < policy.MinimumCredits)
		{
			throw new LootDefinitionValidationException("Invalid credits range in " + tableKey);
		}
		if (policy.Mode == CreditsPolicyMode.Fixed && policy.MinimumCredits != policy.MaximumCredits)
		{
			throw new LootDefinitionValidationException("Fixed credits require equal bounds in " + tableKey);
		}
		if (policy.Mode == CreditsPolicyMode.ObservedSet || policy.Mode == CreditsPolicyMode.ObservedSamples)
		{
			IEnumerable<int> source = policy.ObservedCredits ?? new int[0];
			if (policy.Mode == CreditsPolicyMode.ObservedSet)
			{
				source = source.Distinct();
			}
			int[] array = source.OrderBy((int value) => value).ToArray();
			if (array.Length == 0 || array.Any((int value) => value < 0))
			{
				throw new LootDefinitionValidationException("Observed credits require at least one non-negative outcome in " + tableKey);
			}
			if (policy.MinimumCredits != array[0] || policy.MaximumCredits != array[array.Length - 1])
			{
				throw new LootDefinitionValidationException("Observed credit bounds must match the captured outcome set in " + tableKey);
			}
			policy.ObservedCredits = array;
		}
	}

	private void ValidateEntry(LootEntryDefinition entry, string groupKey, bool active)
	{
		if (entry == null || entry.ItemTemplateId <= 0)
		{
			throw new LootDefinitionValidationException("Invalid item template in " + groupKey);
		}
		if (active && !itemTemplateExists(entry.ItemTemplateId))
		{
			throw new LootDefinitionValidationException("Unknown active item template: " + entry.ItemTemplateId);
		}
		if (active && entry.HighItemTemplateId > 0 && !itemTemplateExists(entry.HighItemTemplateId))
		{
			throw new LootDefinitionValidationException("Unknown active high item template: " + entry.HighItemTemplateId);
		}
		if (entry.FixedQuality.HasValue && entry.FixedQuality.Value < 1)
		{
			throw new LootDefinitionValidationException("Invalid fixed quality in " + groupKey);
		}
		if (entry.MinimumQuality < 1 || entry.MaximumQuality < entry.MinimumQuality)
		{
			throw new LootDefinitionValidationException("Invalid quality range in " + groupKey);
		}
		if (entry.FixedQuality.HasValue && (entry.FixedQuality.Value < entry.MinimumQuality || entry.FixedQuality.Value > entry.MaximumQuality))
		{
			throw new LootDefinitionValidationException("Fixed quality is outside the declared range in " + groupKey);
		}
		if (entry.MinimumQuantity < 1 || entry.MaximumQuantity < entry.MinimumQuantity)
		{
			throw new LootDefinitionValidationException("Invalid quantity range in " + groupKey);
		}
		if (entry.Weight < 0 || entry.DropChanceBasisPoints < 0 || entry.DropChanceBasisPoints > 10000)
		{
			throw new LootDefinitionValidationException("Invalid weight or probability in " + groupKey);
		}
		bool flag = entry.Semantics != LootSemantics.Unresolved && entry.Semantics != LootSemantics.NoneProven;
		if (active && flag && entry.Evidence == LootEvidenceConfidence.Unresolved)
		{
			throw new LootDefinitionValidationException("Rollable active loot requires resolved evidence in " + groupKey);
		}
		if (active && flag && string.IsNullOrWhiteSpace(entry.EvidenceReference))
		{
			throw new LootDefinitionValidationException("Rollable active loot requires an evidence reference in " + groupKey);
		}
		if (entry.Semantics == LootSemantics.GuaranteedProven && entry.Evidence == LootEvidenceConfidence.Unresolved)
		{
			throw new LootDefinitionValidationException("Unresolved item cannot be guaranteed in " + groupKey);
		}
		if (entry.Semantics == LootSemantics.ObservedAvailable && entry.DropChanceBasisPoints >= 10000)
		{
			throw new LootDefinitionValidationException("Observed-only item cannot become guaranteed in " + groupKey);
		}
	}

	private static void ValidateAssignment(LootAssignmentDefinition assignment)
	{
		if (assignment == null || string.IsNullOrWhiteSpace(assignment.AssignmentKey) || string.IsNullOrWhiteSpace(assignment.LootTableKey))
		{
			throw new LootDefinitionValidationException("Assignment key and table key are required.");
		}
		if (assignment.MinimumLevel.HasValue && assignment.MaximumLevel.HasValue && assignment.MinimumLevel.Value > assignment.MaximumLevel.Value)
		{
			throw new LootDefinitionValidationException("Invalid assignment level range: " + assignment.AssignmentKey);
		}
		if (assignment.TargetType != 0 && string.IsNullOrWhiteSpace(assignment.TargetKey))
		{
			throw new LootDefinitionValidationException("Assignment target key is required: " + assignment.AssignmentKey);
		}
	}

	private void ValidateActiveAssignmentOwner(LootAssignmentDefinition candidate)
	{
		if (candidate.Enabled)
		{
			LootAssignmentDefinition lootAssignmentDefinition = assignments.Values.FirstOrDefault((LootAssignmentDefinition existing) => existing.Enabled && existing.TargetType == candidate.TargetType && SameTarget(existing.TargetKey, candidate.TargetKey) && ScopesOverlap(existing.PlayfieldId, candidate.PlayfieldId) && RangesOverlap(existing.MinimumLevel, existing.MaximumLevel, candidate.MinimumLevel, candidate.MaximumLevel));
			if (lootAssignmentDefinition != null)
			{
				throw new LootDefinitionValidationException("Duplicate overlapping active loot assignment owner: " + lootAssignmentDefinition.AssignmentKey + " / " + candidate.AssignmentKey);
			}
		}
	}

	private static bool SameTarget(string left, string right)
	{
		return string.Equals(left ?? string.Empty, right ?? string.Empty, StringComparison.OrdinalIgnoreCase);
	}

	private static bool ScopesOverlap(int? left, int? right)
	{
		return !left.HasValue || !right.HasValue || left.Value == right.Value;
	}

	private static bool RangesOverlap(int? leftMinimum, int? leftMaximum, int? rightMinimum, int? rightMaximum)
	{
		int num = leftMinimum ?? int.MinValue;
		int num2 = leftMaximum ?? int.MaxValue;
		int num3 = rightMinimum ?? int.MinValue;
		int num4 = rightMaximum ?? int.MaxValue;
		return num <= num4 && num3 <= num2;
	}

	private void RefreshVersion()
	{
		Version = string.Join("|", tables.Keys.OrderBy((string x) => x, StringComparer.Ordinal).Concat(assignments.Keys.OrderBy((string x) => x, StringComparer.Ordinal)));
	}
}
