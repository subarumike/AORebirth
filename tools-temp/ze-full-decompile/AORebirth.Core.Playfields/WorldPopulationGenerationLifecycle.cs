using System;
using SmokeLounge.AOtomation.Messaging.GameData;

namespace AORebirth.Core.Playfields;

internal static class WorldPopulationGenerationLifecycle
{
	internal static void ApplySpawnSuccess(PopulationRuntimeState state, Identity runtimeIdentity, OrdinaryEnemySpawnGeneration generation, DateTime spawnedAtUtc)
	{
		//IL_0044: Unknown result type (might be due to invalid IL or missing references)
		//IL_008d: Unknown result type (might be due to invalid IL or missing references)
		if (state == null || ((Identity)(ref runtimeIdentity)).Instance <= 0 || generation == null || state.Generation == int.MaxValue || generation.Number != state.Generation + 1)
		{
			throw new InvalidOperationException("Spawn success must advance exactly one population generation.");
		}
		state.CurrentRuntimeIdentity = runtimeIdentity;
		state.Generation = generation.Number;
		state.SelectedLevel = generation.SelectedVariant.Level;
		state.SpawnedAt = spawnedAtUtc;
		state.DiedAt = null;
		state.CorpseIdentity = Identity.None;
		state.RespawnDueAt = null;
		state.LifecycleState = PopulationLifecycleState.Alive;
		state.LastTransition = spawnedAtUtc;
		state.FailureState = null;
	}

	internal static void ClearRuntime(PopulationRuntimeState state, DateTime clearedAtUtc)
	{
		//IL_0015: Unknown result type (might be due to invalid IL or missing references)
		//IL_0051: Unknown result type (might be due to invalid IL or missing references)
		if (state == null)
		{
			throw new ArgumentNullException("state");
		}
		state.CurrentRuntimeIdentity = Identity.None;
		state.SelectedLevel = null;
		state.SpawnedAt = null;
		state.DiedAt = null;
		state.CorpseIdentity = Identity.None;
		state.RespawnDueAt = null;
		state.LifecycleState = PopulationLifecycleState.Despawned;
		state.LastTransition = clearedAtUtc;
		state.FailureState = null;
	}
}
