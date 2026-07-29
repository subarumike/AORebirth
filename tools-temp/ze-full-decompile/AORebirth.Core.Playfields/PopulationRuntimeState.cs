using System;
using SmokeLounge.AOtomation.Messaging.GameData;

namespace AORebirth.Core.Playfields;

internal sealed class PopulationRuntimeState
{
	internal string SpawnKey { get; set; }

	internal string SpawnGroupKey { get; set; }

	internal string EnemyProfileKey { get; set; }

	internal Identity ConfiguredIdentity { get; set; }

	internal Identity CurrentRuntimeIdentity { get; set; }

	internal int PlayfieldId { get; set; }

	internal PopulationLifecycleState LifecycleState { get; set; }

	internal DateTime? SpawnedAt { get; set; }

	internal DateTime? DiedAt { get; set; }

	internal Identity CorpseIdentity { get; set; }

	internal DateTime? RespawnDueAt { get; set; }

	internal int Generation { get; set; }

	internal int? SelectedLevel { get; set; }

	internal DateTime LastTransition { get; set; }

	internal string FailureState { get; set; }
}
