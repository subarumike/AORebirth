using System;
using System.Linq;

namespace AORebirth.Core.Playfields;

internal sealed class LootAssignmentResolver
{
	internal ResolvedLootAssignment[] Resolve(LootTableRegistry registry, LootGenerationContext context)
	{
		if (registry == null)
		{
			throw new ArgumentNullException("registry");
		}
		if (context == null)
		{
			throw new ArgumentNullException("context");
		}
		if (context.IsOwnedSummon)
		{
			return new ResolvedLootAssignment[0];
		}
		return (from x in registry.Assignments()
			where x.Enabled && Matches(x, context)
			select new ResolvedLootAssignment
			{
				Assignment = x,
				Table = registry.GetTable(x.LootTableKey),
				Specificity = SpecificityFor(x.TargetType)
			} into x
			where x.Table.Enabled
			orderby x.Specificity, x.Assignment.Priority
			select x).ThenBy((ResolvedLootAssignment x) => x.Assignment.AssignmentKey, StringComparer.Ordinal).ToArray();
	}

	private static bool Matches(LootAssignmentDefinition assignment, LootGenerationContext context)
	{
		if (assignment.PlayfieldId.HasValue && assignment.PlayfieldId.Value != context.PlayfieldId)
		{
			return false;
		}
		if (assignment.MinimumLevel.HasValue && context.Level < assignment.MinimumLevel.Value)
		{
			return false;
		}
		if (assignment.MaximumLevel.HasValue && context.Level > assignment.MaximumLevel.Value)
		{
			return false;
		}
		return assignment.TargetType switch
		{
			LootAssignmentTargetType.Global => true, 
			LootAssignmentTargetType.Family => Same(assignment.TargetKey, context.FamilyKey), 
			LootAssignmentTargetType.EnemyType => Same(assignment.TargetKey, context.EnemyProfileKey), 
			LootAssignmentTargetType.Spawn => Same(assignment.TargetKey, context.SpawnKey), 
			LootAssignmentTargetType.Boss => context.IsBoss && Same(assignment.TargetKey, context.EnemyProfileKey), 
			LootAssignmentTargetType.DynaGlobal => context.IsDyna, 
			LootAssignmentTargetType.DynaLevelBand => context.IsDyna && Same(assignment.TargetKey, context.DynaLevelBandKey), 
			LootAssignmentTargetType.DynaFamily => context.IsDyna && Same(assignment.TargetKey, context.DynaFamilyKey), 
			LootAssignmentTargetType.Encounter => Same(assignment.TargetKey, context.EncounterKey), 
			LootAssignmentTargetType.Event => Same(assignment.TargetKey, context.EventKey), 
			_ => false, 
		};
	}

	private static bool Same(string left, string right)
	{
		return !string.IsNullOrWhiteSpace(left) && string.Equals(left, right, StringComparison.OrdinalIgnoreCase);
	}

	private static int SpecificityFor(LootAssignmentTargetType type)
	{
		switch (type)
		{
		case LootAssignmentTargetType.Global:
			return 0;
		case LootAssignmentTargetType.Family:
			return 10;
		case LootAssignmentTargetType.EnemyType:
			return 20;
		case LootAssignmentTargetType.Mission:
		case LootAssignmentTargetType.Dungeon:
			return 25;
		case LootAssignmentTargetType.DynaGlobal:
			return 30;
		case LootAssignmentTargetType.DynaLevelBand:
			return 35;
		case LootAssignmentTargetType.DynaFamily:
			return 40;
		case LootAssignmentTargetType.Boss:
			return 50;
		case LootAssignmentTargetType.Spawn:
			return 60;
		case LootAssignmentTargetType.Encounter:
			return 70;
		case LootAssignmentTargetType.Event:
			return 80;
		default:
			return 25;
		}
	}
}
