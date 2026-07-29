namespace AORebirth.Core.Playfields;

internal sealed class OrdinaryEnemyAggressionProfile
{
	internal OrdinaryEnemyAggressionMode Mode { get; private set; }

	internal double? AutomaticAggroRadius { get; private set; }

	internal bool Chase { get; private set; }

	internal bool ReturnToSpawn { get; private set; }

	internal OrdinaryEnemyEvidenceState EvidenceState { get; private set; }

	internal OrdinaryEnemyAggressionProfile(OrdinaryEnemyAggressionMode mode, double? automaticAggroRadius, bool chase, bool returnToSpawn, OrdinaryEnemyEvidenceState evidenceState)
	{
		Mode = mode;
		AutomaticAggroRadius = automaticAggroRadius;
		Chase = chase;
		ReturnToSpawn = returnToSpawn;
		EvidenceState = evidenceState;
	}
}
