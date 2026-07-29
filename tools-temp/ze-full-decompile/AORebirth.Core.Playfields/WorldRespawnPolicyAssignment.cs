namespace AORebirth.Core.Playfields;

internal sealed class WorldRespawnPolicyAssignment
{
	internal WorldRespawnPolicyAssignmentMode Mode { get; private set; }

	internal RespawnPolicyDefinition ExplicitPolicy { get; private set; }

	internal string PolicyKey { get; private set; }

	internal string Evidence { get; private set; }

	internal string Confidence { get; private set; }

	internal WorldRespawnPolicyAssignment(WorldRespawnPolicyAssignmentMode mode, RespawnPolicyDefinition explicitPolicy, string policyKey, string evidence, string confidence)
	{
		Mode = mode;
		ExplicitPolicy = explicitPolicy;
		PolicyKey = policyKey ?? string.Empty;
		Evidence = evidence ?? string.Empty;
		Confidence = confidence ?? string.Empty;
	}

	internal static WorldRespawnPolicyAssignment Inherit(string evidence)
	{
		return new WorldRespawnPolicyAssignment(WorldRespawnPolicyAssignmentMode.Inherit, null, null, evidence, "POLICY");
	}

	internal static WorldRespawnPolicyAssignment Explicit(RespawnPolicyDefinition policy)
	{
		return new WorldRespawnPolicyAssignment(WorldRespawnPolicyAssignmentMode.Explicit, policy, policy?.RespawnPolicyKey, (policy == null) ? string.Empty : policy.Evidence, (policy == null) ? string.Empty : policy.Confidence);
	}

	internal static WorldRespawnPolicyAssignment NoRespawn(string policyKey, string evidence, string confidence)
	{
		return new WorldRespawnPolicyAssignment(WorldRespawnPolicyAssignmentMode.NoRespawn, null, policyKey, evidence, confidence);
	}

	internal static WorldRespawnPolicyAssignment Unresolved(string evidence)
	{
		return new WorldRespawnPolicyAssignment(WorldRespawnPolicyAssignmentMode.Unresolved, null, null, evidence, "UNRESOLVED");
	}
}
