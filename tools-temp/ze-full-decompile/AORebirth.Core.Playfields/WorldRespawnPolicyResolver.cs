using System;
using System.Collections.Generic;

namespace AORebirth.Core.Playfields;

internal static class WorldRespawnPolicyResolver
{
	internal static void ApplyGroupConfiguration(SpawnGroupDefinition group, IDictionary<string, RespawnPolicyDefinition> configuredByGroupKey, IDictionary<string, RespawnPolicyDefinition> availablePolicies)
	{
		if (group == null)
		{
			throw new ArgumentNullException("group");
		}
		if (configuredByGroupKey != null && configuredByGroupKey.TryGetValue(group.SpawnGroupKey, out var value))
		{
			WorldRespawnPolicyValidator.RegisterOrRejectConflict(availablePolicies, value);
			group.SharedRespawnPolicyKey = value.RespawnPolicyKey;
		}
	}

	internal static WorldRespawnPolicyAssignment ResolveGroupAssignment(SpawnGroupDefinition group, IDictionary<string, RespawnPolicyDefinition> availablePolicies)
	{
		if (group == null || string.IsNullOrWhiteSpace(group.SharedRespawnPolicyKey))
		{
			return null;
		}
		if (availablePolicies == null || !availablePolicies.TryGetValue(group.SharedRespawnPolicyKey, out var value))
		{
			return WorldRespawnPolicyAssignment.Unresolved("missing-group-policy:" + group.SharedRespawnPolicyKey);
		}
		return WorldRespawnPolicyAssignment.Explicit(value);
	}

	internal static WorldRespawnPolicyResolution Resolve(WorldPopulationClassification classification, WorldRespawnPolicyAssignment spawnOrArchetype, WorldRespawnPolicyAssignment group, RespawnPolicyDefinition ordinaryDefault)
	{
		if (!Enum.IsDefined(typeof(WorldPopulationClassification), classification) || classification == WorldPopulationClassification.Unsupported)
		{
			return Unresolved("unsupported-classification");
		}
		WorldRespawnPolicyResolution worldRespawnPolicyResolution = ResolveAssignment(spawnOrArchetype, WorldRespawnPolicyResolutionSource.ExplicitSpawnOrArchetype);
		if (worldRespawnPolicyResolution != null)
		{
			return worldRespawnPolicyResolution;
		}
		WorldRespawnPolicyResolution worldRespawnPolicyResolution2 = ResolveAssignment(group, WorldRespawnPolicyResolutionSource.ExplicitGroup);
		if (worldRespawnPolicyResolution2 != null)
		{
			return worldRespawnPolicyResolution2;
		}
		if (classification == WorldPopulationClassification.OrdinaryEnemy)
		{
			return WorldRespawnPolicyValidator.IsSchedulable(ordinaryDefault) ? new WorldRespawnPolicyResolution(ordinaryDefault, WorldRespawnPolicyResolutionSource.OrdinaryDefault) : Unresolved("invalid-ordinary-default");
		}
		if (IsExcludedFromOrdinaryDefault(classification))
		{
			return NoRespawn("excluded." + classification, "classification-excluded-from-ordinary-default", "POLICY", WorldRespawnPolicyResolutionSource.ExcludedClassification);
		}
		return Unresolved("invalid-classification");
	}

	internal static bool IsExcludedFromOrdinaryDefault(WorldPopulationClassification classification)
	{
		return classification == WorldPopulationClassification.NamedEnemy || classification == WorldPopulationClassification.Boss || classification == WorldPopulationClassification.ScriptedEncounter || classification == WorldPopulationClassification.Summon || classification == WorldPopulationClassification.Pet || classification == WorldPopulationClassification.TemporaryEncounterAdd || classification == WorldPopulationClassification.Vendor || classification == WorldPopulationClassification.StaticObject || classification == WorldPopulationClassification.Container || classification == WorldPopulationClassification.QuestOwned;
	}

	private static WorldRespawnPolicyResolution ResolveAssignment(WorldRespawnPolicyAssignment assignment, WorldRespawnPolicyResolutionSource explicitSource)
	{
		if (assignment == null || assignment.Mode == WorldRespawnPolicyAssignmentMode.Inherit)
		{
			return null;
		}
		if (assignment.Mode == WorldRespawnPolicyAssignmentMode.NoRespawn)
		{
			if (string.IsNullOrWhiteSpace(assignment.PolicyKey))
			{
				return Unresolved("missing-no-respawn-policy-key");
			}
			return NoRespawn(assignment.PolicyKey, assignment.Evidence, assignment.Confidence, WorldRespawnPolicyResolutionSource.ExplicitNoRespawn);
		}
		if (assignment.Mode == WorldRespawnPolicyAssignmentMode.Explicit && (WorldRespawnPolicyValidator.IsSchedulable(assignment.ExplicitPolicy) || (WorldRespawnPolicyValidator.IsValid(assignment.ExplicitPolicy) && assignment.ExplicitPolicy.Enabled && assignment.ExplicitPolicy.Mode == WorldRespawnMode.None)))
		{
			return new WorldRespawnPolicyResolution(assignment.ExplicitPolicy, explicitSource);
		}
		return Unresolved((assignment.Mode == WorldRespawnPolicyAssignmentMode.Unresolved) ? assignment.Evidence : "invalid-policy-assignment");
	}

	private static WorldRespawnPolicyResolution NoRespawn(string key, string evidence, string confidence, WorldRespawnPolicyResolutionSource source)
	{
		return new WorldRespawnPolicyResolution(new RespawnPolicyDefinition
		{
			RespawnPolicyKey = key,
			Mode = WorldRespawnMode.None,
			DelayStartsAt = RespawnDelayStartsAt.Unresolved,
			Evidence = evidence,
			Confidence = confidence,
			Enabled = true
		}, source);
	}

	private static WorldRespawnPolicyResolution Unresolved(string evidence)
	{
		return new WorldRespawnPolicyResolution(new RespawnPolicyDefinition
		{
			RespawnPolicyKey = "respawn.unresolved",
			Mode = WorldRespawnMode.Unresolved,
			DelayStartsAt = RespawnDelayStartsAt.Unresolved,
			Evidence = evidence,
			Confidence = "UNRESOLVED",
			Enabled = false
		}, WorldRespawnPolicyResolutionSource.Unresolved);
	}
}
