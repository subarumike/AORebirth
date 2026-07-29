namespace AORebirth.Core.Playfields;

internal sealed class RespawnPolicyDefinition
{
	internal string RespawnPolicyKey { get; set; }

	internal WorldRespawnMode Mode { get; set; }

	internal double? FixedDelaySeconds { get; set; }

	internal double? MinimumDelaySeconds { get; set; }

	internal double? MaximumDelaySeconds { get; set; }

	internal bool SharedGroupTimer { get; set; }

	internal bool RespawnAtOriginalPosition { get; set; }

	internal bool ResetHealth { get; set; }

	internal bool ResetMovementState { get; set; }

	internal bool ResetAggressionState { get; set; }

	internal RespawnDelayStartsAt DelayStartsAt { get; set; }

	internal string Evidence { get; set; }

	internal string Confidence { get; set; }

	internal bool Enabled { get; set; }
}
