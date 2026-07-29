namespace AORebirth.Core.Playfields;

internal sealed class WorldRespawnPolicyResolution
{
	internal RespawnPolicyDefinition Policy { get; private set; }

	internal WorldRespawnPolicyResolutionSource Source { get; private set; }

	internal bool IsValid => WorldRespawnPolicyValidator.IsValid(Policy);

	internal WorldRespawnPolicyResolution(RespawnPolicyDefinition policy, WorldRespawnPolicyResolutionSource source)
	{
		Policy = policy;
		Source = source;
	}
}
