using System;
using System.Globalization;

namespace AORebirth.Core.Playfields;

internal sealed class OrdinaryEnemySpawnDefinition
{
	internal string SpawnKey { get; private set; }

	internal int SourceIdentity { get; private set; }

	internal string ProfileKey { get; private set; }

	internal int PlayfieldInstance { get; private set; }

	internal int Level { get; private set; }

	internal int Health { get; private set; }

	internal int HealthDamage { get; private set; }

	internal int MonsterScale { get; private set; }

	internal int RunSpeed { get; private set; }

	internal float X { get; private set; }

	internal float Y { get; private set; }

	internal float Z { get; private set; }

	internal float HeadingX { get; private set; }

	internal float HeadingY { get; private set; }

	internal float HeadingZ { get; private set; }

	internal float HeadingW { get; private set; }

	internal OrdinaryEnemyMovementMode MovementMode { get; private set; }

	internal OrdinaryEnemyWaypoint[] Waypoints { get; private set; }

	internal bool UseCapturedPatrolReplay { get; private set; }

	internal bool UseSpawnAsPatrolStart { get; private set; }

	internal bool HasCapturedScfuOverride { get; private set; }

	internal uint CapturedScfuFlags { get; private set; }

	internal int CapturedScfuFlags2 { get; private set; }

	internal byte[] CapturedScfuUnknown1 { get; private set; }

	internal int CapturedScfuUnknown2 { get; private set; }

	internal OrdinaryEnemyEvidenceState RespawnEvidence { get; private set; }

	internal double? RespawnDelaySeconds { get; private set; }

	internal OrdinaryEnemyRuntimeDisposition Disposition { get; private set; }

	internal string SourceOwnerIdentity { get; private set; }

	internal string SourceCapture { get; private set; }

	internal string SourceTimestamp { get; private set; }

	internal OrdinaryEnemySpawnLevelDefinition LevelDefinition { get; private set; }

	internal WorldRespawnPolicyAssignment RespawnPolicy { get; private set; }

	private OrdinaryEnemySpawnVariant DefaultVariant { get; set; }

	internal bool HasRespawnDelay => (RespawnEvidence == OrdinaryEnemyEvidenceState.Observed || RespawnEvidence == OrdinaryEnemyEvidenceState.Policy) && RespawnDelaySeconds.HasValue && RespawnDelaySeconds.Value > 0.0;

	internal OrdinaryEnemySpawnDefinition(string spawnKey, int sourceIdentity, string profileKey, int playfieldInstance, int level, int health, int healthDamage, int monsterScale, int runSpeed, float x, float y, float z, float headingX, float headingY, float headingZ, float headingW, OrdinaryEnemyMovementMode movementMode, OrdinaryEnemyWaypoint[] waypoints, bool useCapturedPatrolReplay, bool useSpawnAsPatrolStart, bool hasCapturedScfuOverride, uint capturedScfuFlags, int capturedScfuFlags2, byte[] capturedScfuUnknown1, int capturedScfuUnknown2, OrdinaryEnemyEvidenceState respawnEvidence, double? respawnDelaySeconds, OrdinaryEnemyRuntimeDisposition disposition, string sourceOwnerIdentity, string sourceCapture, string sourceTimestamp, OrdinaryEnemySpawnLevelDefinition levelDefinition = null, WorldRespawnPolicyAssignment respawnPolicy = null)
	{
		SpawnKey = spawnKey;
		SourceIdentity = sourceIdentity;
		ProfileKey = profileKey;
		PlayfieldInstance = playfieldInstance;
		Level = level;
		Health = health;
		HealthDamage = healthDamage;
		MonsterScale = monsterScale;
		RunSpeed = runSpeed;
		X = x;
		Y = y;
		Z = z;
		HeadingX = headingX;
		HeadingY = headingY;
		HeadingZ = headingZ;
		HeadingW = headingW;
		MovementMode = movementMode;
		Waypoints = waypoints ?? new OrdinaryEnemyWaypoint[0];
		UseCapturedPatrolReplay = useCapturedPatrolReplay;
		UseSpawnAsPatrolStart = useSpawnAsPatrolStart;
		HasCapturedScfuOverride = hasCapturedScfuOverride;
		CapturedScfuFlags = capturedScfuFlags;
		CapturedScfuFlags2 = capturedScfuFlags2;
		CapturedScfuUnknown1 = capturedScfuUnknown1 ?? new byte[0];
		CapturedScfuUnknown2 = capturedScfuUnknown2;
		RespawnEvidence = respawnEvidence;
		RespawnDelaySeconds = respawnDelaySeconds;
		Disposition = disposition;
		SourceOwnerIdentity = sourceOwnerIdentity;
		SourceCapture = sourceCapture;
		SourceTimestamp = sourceTimestamp;
		DefaultVariant = new OrdinaryEnemySpawnVariant(level, health, healthDamage, monsterScale, runSpeed, sourceCapture);
		LevelDefinition = levelDefinition ?? OrdinaryEnemySpawnLevelDefinition.Fixed(DefaultVariant, OrdinaryEnemyEvidenceState.Observed, string.IsNullOrWhiteSpace(sourceCapture) ? ("captured-fixed:" + spawnKey) : sourceCapture);
		RespawnPolicy = respawnPolicy ?? BuildCompatibilityRespawnPolicy(spawnKey, sourceIdentity, respawnEvidence, respawnDelaySeconds, sourceCapture);
	}

	internal OrdinaryEnemySpawnVariant SelectVariant(Func<int, int> nextRandom)
	{
		return LevelDefinition.SelectVariant(nextRandom);
	}

	private static WorldRespawnPolicyAssignment BuildCompatibilityRespawnPolicy(string spawnKey, int sourceIdentity, OrdinaryEnemyEvidenceState respawnEvidence, double? respawnDelaySeconds, string sourceCapture)
	{
		if (respawnEvidence == OrdinaryEnemyEvidenceState.Observed || respawnEvidence == OrdinaryEnemyEvidenceState.Policy)
		{
			return WorldRespawnPolicyAssignment.Explicit(new RespawnPolicyDefinition
			{
				RespawnPolicyKey = "ordinary.explicit." + sourceIdentity.ToString(CultureInfo.InvariantCulture),
				Mode = WorldRespawnMode.FixedDelay,
				FixedDelaySeconds = respawnDelaySeconds,
				RespawnAtOriginalPosition = true,
				ResetHealth = true,
				ResetMovementState = true,
				ResetAggressionState = true,
				DelayStartsAt = RespawnDelayStartsAt.NpcDespawn,
				Evidence = sourceCapture,
				Confidence = respawnEvidence.ToString(),
				Enabled = true
			});
		}
		return WorldRespawnPolicyAssignment.Inherit(string.IsNullOrWhiteSpace(sourceCapture) ? ("ordinary-default:" + spawnKey) : sourceCapture);
	}
}
