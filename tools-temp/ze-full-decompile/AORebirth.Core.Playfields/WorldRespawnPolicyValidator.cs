using System;
using System.Collections.Generic;

namespace AORebirth.Core.Playfields;

internal static class WorldRespawnPolicyValidator
{
	internal static bool IsValid(RespawnPolicyDefinition policy)
	{
		if (policy == null || string.IsNullOrWhiteSpace(policy.RespawnPolicyKey))
		{
			return false;
		}
		if (!Enum.IsDefined(typeof(WorldRespawnMode), policy.Mode) || !Enum.IsDefined(typeof(RespawnDelayStartsAt), policy.DelayStartsAt))
		{
			return false;
		}
		if (policy.SharedGroupTimer || policy.Mode == WorldRespawnMode.GroupSharedDelay)
		{
			return false;
		}
		if (policy.Mode == WorldRespawnMode.None)
		{
			return !policy.FixedDelaySeconds.HasValue && !policy.MinimumDelaySeconds.HasValue && !policy.MaximumDelaySeconds.HasValue;
		}
		if (policy.Mode == WorldRespawnMode.Scripted)
		{
			return policy.DelayStartsAt == RespawnDelayStartsAt.Scripted && !policy.FixedDelaySeconds.HasValue && !policy.MinimumDelaySeconds.HasValue && !policy.MaximumDelaySeconds.HasValue;
		}
		if (policy.DelayStartsAt == RespawnDelayStartsAt.Scripted || policy.DelayStartsAt == RespawnDelayStartsAt.Unresolved || policy.DelayStartsAt == RespawnDelayStartsAt.CorpseCreation)
		{
			return false;
		}
		if (policy.Mode == WorldRespawnMode.FixedDelay)
		{
			return IsUsableDelay(policy.FixedDelaySeconds) && !policy.MinimumDelaySeconds.HasValue && !policy.MaximumDelaySeconds.HasValue;
		}
		if (policy.Mode == WorldRespawnMode.RandomDelayRange)
		{
			return !policy.FixedDelaySeconds.HasValue && IsUsableDelay(policy.MinimumDelaySeconds) && IsUsableDelay(policy.MaximumDelaySeconds) && policy.MaximumDelaySeconds.Value >= policy.MinimumDelaySeconds.Value;
		}
		return false;
	}

	internal static bool IsSchedulable(RespawnPolicyDefinition policy)
	{
		return IsValid(policy) && policy.Enabled && (policy.Mode == WorldRespawnMode.FixedDelay || policy.Mode == WorldRespawnMode.RandomDelayRange);
	}

	internal static bool AreEquivalent(RespawnPolicyDefinition left, RespawnPolicyDefinition right)
	{
		return left != null && right != null && string.Equals(left.RespawnPolicyKey, right.RespawnPolicyKey, StringComparison.Ordinal) && left.Mode == right.Mode && left.FixedDelaySeconds == right.FixedDelaySeconds && left.MinimumDelaySeconds == right.MinimumDelaySeconds && left.MaximumDelaySeconds == right.MaximumDelaySeconds && left.SharedGroupTimer == right.SharedGroupTimer && left.RespawnAtOriginalPosition == right.RespawnAtOriginalPosition && left.ResetHealth == right.ResetHealth && left.ResetMovementState == right.ResetMovementState && left.ResetAggressionState == right.ResetAggressionState && left.DelayStartsAt == right.DelayStartsAt && string.Equals(left.Evidence, right.Evidence, StringComparison.Ordinal) && string.Equals(left.Confidence, right.Confidence, StringComparison.Ordinal) && left.Enabled == right.Enabled;
	}

	internal static void RegisterOrRejectConflict(IDictionary<string, RespawnPolicyDefinition> availablePolicies, RespawnPolicyDefinition policy)
	{
		if (availablePolicies == null || !IsValid(policy))
		{
			throw new InvalidOperationException("Invalid respawn policy registration.");
		}
		if (!availablePolicies.TryGetValue(policy.RespawnPolicyKey, out var value))
		{
			availablePolicies.Add(policy.RespawnPolicyKey, policy);
		}
		else if (!AreEquivalent(value, policy))
		{
			throw new InvalidOperationException("Conflicting respawn policy key: " + policy.RespawnPolicyKey);
		}
	}

	private static bool IsUsableDelay(double? seconds)
	{
		if (!seconds.HasValue || seconds.Value <= 0.0 || double.IsNaN(seconds.Value) || double.IsInfinity(seconds.Value))
		{
			return false;
		}
		try
		{
			TimeSpan.FromSeconds(seconds.Value);
			return true;
		}
		catch (OverflowException)
		{
			return false;
		}
	}
}
