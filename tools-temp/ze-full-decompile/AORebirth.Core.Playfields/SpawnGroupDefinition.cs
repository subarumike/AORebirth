namespace AORebirth.Core.Playfields;

internal sealed class SpawnGroupDefinition
{
	internal string SpawnGroupKey { get; set; }

	internal string DisplayName { get; set; }

	internal int PlayfieldId { get; set; }

	internal string ZoneKey { get; set; }

	internal string CampKey { get; set; }

	internal string[] SpawnKeys { get; set; }

	internal WorldSpawnActivationPolicy ActivationPolicy { get; set; }

	internal int MaximumAlive { get; set; }

	internal int MinimumAlive { get; set; }

	internal string SharedRespawnPolicyKey { get; set; }

	internal string ResetPolicy { get; set; }

	internal bool Enabled { get; set; }

	internal string Evidence { get; set; }

	internal string Confidence { get; set; }
}
