using SmokeLounge.AOtomation.Messaging.GameData;

namespace AORebirth.Core.Playfields;

internal sealed class WorldSpawnDefinition
{
	internal string SpawnKey { get; set; }

	internal string EnemyProfileKey { get; set; }

	internal Identity ConfiguredIdentity { get; set; }

	internal int PlayfieldId { get; set; }

	internal float X { get; set; }

	internal float Y { get; set; }

	internal float Z { get; set; }

	internal float OrientationX { get; set; }

	internal float OrientationY { get; set; }

	internal float OrientationZ { get; set; }

	internal float OrientationW { get; set; }

	internal int? ScaleOverride { get; set; }

	internal int? LevelOverride { get; set; }

	internal int? HealthOverride { get; set; }

	internal string MovementProfileOverride { get; set; }

	internal string AggressionProfileOverride { get; set; }

	internal string CombatProfileOverride { get; set; }

	internal string LootAssignmentOverride { get; set; }

	internal string SpawnGroupKey { get; set; }

	internal string RespawnPolicyKey { get; set; }

	internal string ZoneKey { get; set; }

	internal string SubzoneKey { get; set; }

	internal string CampKey { get; set; }

	internal WorldSpawnActivationPolicy ActivationPolicy { get; set; }

	internal bool Enabled { get; set; }

	internal bool Quarantined { get; set; }

	internal bool BossOrScripted { get; set; }

	internal bool OwnedSummon { get; set; }

	internal string Evidence { get; set; }

	internal string Confidence { get; set; }

	internal string Source { get; set; }

	internal WorldPopulationClassification Classification { get; set; }
}
