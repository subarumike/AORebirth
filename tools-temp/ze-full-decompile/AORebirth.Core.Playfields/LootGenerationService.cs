using System;
using System.Linq;

namespace AORebirth.Core.Playfields;

internal sealed class LootGenerationService
{
	private readonly LootTableRegistry registry;

	private readonly LootAssignmentResolver resolver;

	internal LootGenerationService(LootTableRegistry registry, LootAssignmentResolver resolver)
	{
		if (registry == null)
		{
			throw new ArgumentNullException("registry");
		}
		if (resolver == null)
		{
			throw new ArgumentNullException("resolver");
		}
		this.registry = registry;
		this.resolver = resolver;
	}

	internal LootGenerationResult Generate(LootGenerationContext context, ILootRandomSource random)
	{
		if (context == null)
		{
			throw new ArgumentNullException("context");
		}
		if (random == null)
		{
			throw new ArgumentNullException("random");
		}
		LootGenerationResult lootGenerationResult = new LootGenerationResult
		{
			Seed = context.Seed,
			RegistryVersion = registry.Version
		};
		ResolvedLootAssignment[] array = resolver.Resolve(registry, context);
		if (array.Length == 0)
		{
			lootGenerationResult.LootUnresolved = !context.IsOwnedSummon;
			return lootGenerationResult;
		}
		ResolvedLootAssignment[] array2 = array;
		foreach (ResolvedLootAssignment resolvedLootAssignment in array2)
		{
			LootTableDefinition table = resolvedLootAssignment.Table;
			lootGenerationResult.AppliedAssignmentKeys.Add(resolvedLootAssignment.Assignment.AssignmentKey);
			lootGenerationResult.AppliedTableKeys.Add(table.LootTableKey);
			lootGenerationResult.LootUnresolved |= table.ItemPoolUnresolved;
			if (table.ObservedCorpseSnapshots.Length != 0)
			{
				RollObservedCorpseSnapshot(lootGenerationResult, table, context, random);
				continue;
			}
			foreach (LootGroupDefinition item in table.RollGroups.OrderBy((LootGroupDefinition x) => x.LootGroupKey, StringComparer.Ordinal))
			{
				RollGroup(lootGenerationResult, table, item, context, random);
			}
			ApplyCredits(lootGenerationResult, table.CreditsPolicy, random);
		}
		return lootGenerationResult;
	}

	private void RollObservedCorpseSnapshot(LootGenerationResult result, LootTableDefinition table, LootGenerationContext context, ILootRandomSource random)
	{
		ObservedCorpseSnapshotDefinition observedCorpseSnapshotDefinition = table.ObservedCorpseSnapshots[random.Next(table.ObservedCorpseSnapshots.Length)];
		string text = "observed.corpse.snapshot." + observedCorpseSnapshotDefinition.SnapshotKey;
		LootGroupDefinition lootGroupDefinition = new LootGroupDefinition();
		lootGroupDefinition.LootGroupKey = text;
		lootGroupDefinition.RollMode = LootRollMode.ObservedSnapshot;
		lootGroupDefinition.RollCount = 1;
		lootGroupDefinition.EmptyWeight = 0;
		lootGroupDefinition.DropChanceBasisPoints = 0;
		lootGroupDefinition.Entries = observedCorpseSnapshotDefinition.Entries;
		lootGroupDefinition.Conditions = new string[0];
		LootGroupDefinition group = lootGroupDefinition;
		result.LootUnresolved = true;
		result.Credits = observedCorpseSnapshotDefinition.Credits;
		result.CreditsUnresolved = true;
		result.RollEvidence.Add(new LootRollEvidence
		{
			TableKey = table.LootTableKey,
			GroupKey = text,
			EntryTemplateId = 0,
			Outcome = "snapshot-selected:" + observedCorpseSnapshotDefinition.SnapshotKey + "; probability-unresolved"
		});
		LootEntryDefinition[] entries = observedCorpseSnapshotDefinition.Entries;
		foreach (LootEntryDefinition entry in entries)
		{
			TryGenerate(result, table, group, entry, context, random, selected: true);
		}
	}

	private void RollGroup(LootGenerationResult result, LootTableDefinition table, LootGroupDefinition group, LootGenerationContext context, ILootRandomSource random)
	{
		if (group.DropChanceBasisPoints > 0 && group.DropChanceBasisPoints < 10000 && random.Next(10000) >= group.DropChanceBasisPoints)
		{
			result.RollEvidence.Add(new LootRollEvidence
			{
				TableKey = table.LootTableKey,
				GroupKey = group.LootGroupKey,
				EntryTemplateId = 0,
				Outcome = "group-not-selected"
			});
			return;
		}
		LootEntryDefinition[] array = (from x in @group.Entries
			orderby x.ItemTemplateId, x.MinimumQuality
			select x).ToArray();
		switch (group.RollMode)
		{
		case LootRollMode.All:
		case LootRollMode.Guaranteed:
		{
			LootEntryDefinition[] array3 = array;
			foreach (LootEntryDefinition entry2 in array3)
			{
				TryGenerate(result, table, group, entry2, context, random, selected: true);
			}
			break;
		}
		case LootRollMode.ObservedSnapshot:
		{
			result.LootUnresolved = true;
			LootEntryDefinition[] array4 = array;
			foreach (LootEntryDefinition entry3 in array4)
			{
				TryGenerate(result, table, group, entry3, context, random, selected: true);
			}
			break;
		}
		case LootRollMode.Independent:
		{
			LootEntryDefinition[] array2 = array;
			foreach (LootEntryDefinition entry in array2)
			{
				TryGenerate(result, table, group, entry, context, random, selected: false);
			}
			break;
		}
		case LootRollMode.WeightedOne:
			RollWeighted(result, table, group, array, context, random, 1);
			break;
		case LootRollMode.WeightedMany:
			RollWeighted(result, table, group, array, context, random, Math.Max(0, group.RollCount));
			break;
		}
	}

	private void RollWeighted(LootGenerationResult result, LootTableDefinition table, LootGroupDefinition group, LootEntryDefinition[] entries, LootGenerationContext context, ILootRandomSource random, int rolls)
	{
		for (int i = 0; i < rolls; i++)
		{
			var array = (from x in entries.Where(CanRoll).GroupBy((LootEntryDefinition x) => string.IsNullOrWhiteSpace(x.SelectionKey) ? ("item:" + x.ItemTemplateId) : x.SelectionKey, StringComparer.Ordinal)
				select new
				{
					Key = x.Key,
					Weight = x.Max((LootEntryDefinition y) => y.Weight),
					Entries = x.ToArray()
				}).OrderBy(x => x.Key, StringComparer.Ordinal).ToArray();
			int num = group.EmptyWeight + array.Sum(x => x.Weight);
			if (num <= 0)
			{
				break;
			}
			int num2 = random.Next(num);
			if (num2 < group.EmptyWeight)
			{
				result.RollEvidence.Add(new LootRollEvidence
				{
					TableKey = table.LootTableKey,
					GroupKey = group.LootGroupKey,
					EntryTemplateId = 0,
					Outcome = "empty"
				});
				continue;
			}
			num2 -= group.EmptyWeight;
			var array2 = array;
			foreach (var anon in array2)
			{
				if (num2 < anon.Weight)
				{
					LootEntryDefinition[] entries2 = anon.Entries;
					foreach (LootEntryDefinition entry in entries2)
					{
						TryGenerate(result, table, group, entry, context, random, selected: true);
					}
					break;
				}
				num2 -= anon.Weight;
			}
		}
	}

	private void TryGenerate(LootGenerationResult result, LootTableDefinition table, LootGroupDefinition group, LootEntryDefinition entry, LootGenerationContext context, ILootRandomSource random, bool selected)
	{
		if (!CanRoll(entry))
		{
			result.LootUnresolved |= entry.Semantics == LootSemantics.Unresolved;
			result.SkippedEntries.Add(table.LootTableKey + ":" + group.LootGroupKey + ":" + entry.ItemTemplateId);
			return;
		}
		bool flag = selected || entry.DropChanceBasisPoints >= 10000 || (entry.DropChanceBasisPoints > 0 && random.Next(10000) < entry.DropChanceBasisPoints);
		result.RollEvidence.Add(new LootRollEvidence
		{
			TableKey = table.LootTableKey,
			GroupKey = group.LootGroupKey,
			EntryTemplateId = entry.ItemTemplateId,
			Outcome = (flag ? "generated" : "not-selected")
		});
		if (flag && (!entry.UniquePerCorpse || !result.Items.Any((GeneratedLootItem x) => x.Definition.ItemTemplateId == entry.ItemTemplateId)))
		{
			int quality = entry.FixedQuality ?? NextInclusive(random, entry.MinimumQuality, entry.MaximumQuality);
			int quantity = NextInclusive(random, entry.MinimumQuantity, entry.MaximumQuantity);
			result.Items.Add(new GeneratedLootItem
			{
				Definition = entry,
				ItemTemplateId = entry.ItemTemplateId,
				HighItemTemplateId = entry.HighItemTemplateId,
				Quality = quality,
				Quantity = quantity,
				TableKey = table.LootTableKey,
				GroupKey = group.LootGroupKey
			});
		}
	}

	private static bool CanRoll(LootEntryDefinition entry)
	{
		return entry.Semantics != LootSemantics.Unresolved && entry.Semantics != LootSemantics.NoneProven && entry.Evidence != LootEvidenceConfidence.Unresolved && !string.IsNullOrWhiteSpace(entry.EvidenceReference);
	}

	private static void ApplyCredits(LootGenerationResult result, CreditsPolicyDefinition policy, ILootRandomSource random)
	{
		switch (policy.Mode)
		{
		case CreditsPolicyMode.None:
			result.Credits = 0;
			result.CreditsUnresolved = false;
			break;
		case CreditsPolicyMode.Fixed:
			result.Credits = policy.MinimumCredits;
			result.CreditsUnresolved = false;
			break;
		case CreditsPolicyMode.Range:
			result.Credits = NextInclusive(random, policy.MinimumCredits, policy.MaximumCredits);
			result.CreditsUnresolved = false;
			break;
		case CreditsPolicyMode.ObservedSet:
		case CreditsPolicyMode.ObservedSamples:
		{
			int[] array = policy.ObservedCredits ?? new int[0];
			result.Credits = ((array.Length != 0) ? array[random.Next(array.Length)] : 0);
			result.CreditsUnresolved = true;
			break;
		}
		case CreditsPolicyMode.Unresolved:
			result.CreditsUnresolved = true;
			break;
		}
	}

	private static int NextInclusive(ILootRandomSource random, int minimum, int maximum)
	{
		return (maximum <= minimum) ? minimum : (minimum + random.Next(maximum - minimum + 1));
	}
}
