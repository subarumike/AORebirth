namespace AORebirth.Core.Playfields;

internal enum PopulationLifecycleState
{
	Disabled,
	Ready,
	Spawning,
	Alive,
	DeadCorpseActive,
	WaitingForRespawn,
	Respawning,
	Despawned,
	Quarantined,
	Failed
}
