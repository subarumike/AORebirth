using System;
using System.Collections.Generic;
using System.Linq;
using SmokeLounge.AOtomation.Messaging.GameData;

namespace AORebirth.Core.Playfields;

internal sealed class WorldRespawnScheduler
{
	private readonly Dictionary<string, WorldRespawnSchedule> scheduled = new Dictionary<string, WorldRespawnSchedule>(StringComparer.Ordinal);

	internal int Count => scheduled.Count;

	internal bool Schedule(WorldRespawnSchedule value)
	{
		if (value == null || string.IsNullOrWhiteSpace(value.SpawnKey) || value.Generation <= 0 || value.DueAtUtc == default(DateTime) || scheduled.ContainsKey(value.SpawnKey))
		{
			return false;
		}
		scheduled.Add(value.SpawnKey, value);
		return true;
	}

	internal bool Cancel(string spawnKey)
	{
		return scheduled.Remove(spawnKey);
	}

	internal void CancelPlayfield(int playfieldId)
	{
		string[] array = (from x in scheduled
			where x.Value.PlayfieldId == playfieldId
			select x.Key).ToArray();
		foreach (string key in array)
		{
			scheduled.Remove(key);
		}
	}

	internal WorldRespawnSchedule[] TakeDue(DateTime utcNow, int maximumWork)
	{
		WorldRespawnSchedule[] array = (from x in scheduled.Values
			where x.DueAtUtc <= utcNow
			orderby x.DueAtUtc, x.PlayfieldId
			select x).ThenBy((WorldRespawnSchedule x) => x.SpawnKey, StringComparer.Ordinal).Take(Math.Max(0, maximumWork)).ToArray();
		WorldRespawnSchedule[] array2 = array;
		foreach (WorldRespawnSchedule worldRespawnSchedule in array2)
		{
			scheduled.Remove(worldRespawnSchedule.SpawnKey);
		}
		return array;
	}

	internal bool Contains(string spawnKey)
	{
		return scheduled.ContainsKey(spawnKey);
	}

	internal static TimeSpan SelectDelay(RespawnPolicyDefinition policy, IPopulationRandomSource random)
	{
		if (!WorldRespawnPolicyValidator.IsSchedulable(policy))
		{
			throw new InvalidOperationException("Respawn policy cannot be scheduled: " + ((policy == null) ? "null" : policy.Mode.ToString()));
		}
		if (policy.Mode == WorldRespawnMode.FixedDelay)
		{
			return TimeSpan.FromSeconds(policy.FixedDelaySeconds.Value);
		}
		if (random == null)
		{
			throw new InvalidOperationException("Random respawn policy requires an explicit random source.");
		}
		double num = random.NextUnit();
		if (double.IsNaN(num) || double.IsInfinity(num) || num < 0.0 || num > 1.0)
		{
			throw new InvalidOperationException("Population random source must return a finite value in the inclusive range 0..1.");
		}
		return TimeSpan.FromSeconds(policy.MinimumDelaySeconds.Value + (policy.MaximumDelaySeconds.Value - policy.MinimumDelaySeconds.Value) * num);
	}

	internal static bool TryScheduleForLifecycle(WorldRespawnScheduler scheduler, PopulationRuntimeState state, RespawnPolicyDefinition policy, RespawnDelayStartsAt start, DateTime startedAtUtc, IPopulationRandomSource random = null)
	{
		if (scheduler == null || state == null || state.Generation <= 0 || !WorldRespawnPolicyValidator.IsSchedulable(policy) || (policy.Mode == WorldRespawnMode.RandomDelayRange && random == null) || policy.DelayStartsAt != start)
		{
			return false;
		}
		DateTime dateTime;
		try
		{
			dateTime = startedAtUtc.Add(SelectDelay(policy, random));
		}
		catch (ArgumentOutOfRangeException)
		{
			return false;
		}
		catch (InvalidOperationException)
		{
			return false;
		}
		if (!scheduler.Schedule(new WorldRespawnSchedule
		{
			SpawnKey = state.SpawnKey,
			GroupKey = state.SpawnGroupKey,
			PlayfieldId = state.PlayfieldId,
			DueAtUtc = dateTime,
			Generation = state.Generation
		}))
		{
			return false;
		}
		state.RespawnDueAt = dateTime;
		state.LifecycleState = PopulationLifecycleState.WaitingForRespawn;
		state.LastTransition = startedAtUtc;
		return true;
	}

	internal static bool IsCurrentGeneration(PopulationRuntimeState state, WorldRespawnSchedule due)
	{
		//IL_0016: Unknown result type (might be due to invalid IL or missing references)
		//IL_001b: Unknown result type (might be due to invalid IL or missing references)
		int result;
		if (state != null && due != null && state.Generation == due.Generation)
		{
			Identity currentRuntimeIdentity = state.CurrentRuntimeIdentity;
			result = ((((Identity)(ref currentRuntimeIdentity)).Instance == 0) ? 1 : 0);
		}
		else
		{
			result = 0;
		}
		return (byte)result != 0;
	}

	internal static bool TryResumePendingAfterRuntimeRelease(WorldRespawnScheduler scheduler, PopulationRuntimeState state, RespawnPolicyDefinition policy, DateTime releasedAtUtc)
	{
		//IL_0008: Unknown result type (might be due to invalid IL or missing references)
		//IL_000d: Unknown result type (might be due to invalid IL or missing references)
		if (scheduler != null && state != null)
		{
			Identity currentRuntimeIdentity = state.CurrentRuntimeIdentity;
			if (((Identity)(ref currentRuntimeIdentity)).Instance == 0 && state.RespawnDueAt.HasValue && !scheduler.Contains(state.SpawnKey) && WorldRespawnPolicyValidator.IsSchedulable(policy))
			{
				DateTime dateTime = ((state.RespawnDueAt.Value > releasedAtUtc) ? state.RespawnDueAt.Value : releasedAtUtc);
				if (!scheduler.Schedule(new WorldRespawnSchedule
				{
					SpawnKey = state.SpawnKey,
					GroupKey = state.SpawnGroupKey,
					PlayfieldId = state.PlayfieldId,
					DueAtUtc = dateTime,
					Generation = state.Generation
				}))
				{
					return false;
				}
				state.RespawnDueAt = dateTime;
				state.LifecycleState = PopulationLifecycleState.WaitingForRespawn;
				state.LastTransition = releasedAtUtc;
				return true;
			}
		}
		return false;
	}
}
