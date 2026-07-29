namespace AORebirth.Core.Playfields;

internal sealed class OrdinaryEnemyCorpseProfile
{
	internal OrdinaryEnemyCorpsePacketProfile PacketProfile { get; private set; }

	internal double EmptyLifetimeSeconds { get; private set; }

	internal double UnlootedLifetimeSeconds { get; private set; }

	internal double LootedCleanupSeconds { get; private set; }

	internal int? CapturedCatMesh { get; private set; }

	internal string VisualEvidence { get; private set; }

	internal OrdinaryEnemyCorpseProfile(OrdinaryEnemyCorpsePacketProfile packetProfile, double emptyLifetimeSeconds, double unlootedLifetimeSeconds, double lootedCleanupSeconds)
		: this(packetProfile, emptyLifetimeSeconds, unlootedLifetimeSeconds, lootedCleanupSeconds, null, string.Empty)
	{
	}

	internal OrdinaryEnemyCorpseProfile(OrdinaryEnemyCorpsePacketProfile packetProfile, double emptyLifetimeSeconds, double unlootedLifetimeSeconds, double lootedCleanupSeconds, int? capturedCatMesh, string visualEvidence)
	{
		PacketProfile = packetProfile;
		EmptyLifetimeSeconds = emptyLifetimeSeconds;
		UnlootedLifetimeSeconds = unlootedLifetimeSeconds;
		LootedCleanupSeconds = lootedCleanupSeconds;
		CapturedCatMesh = capturedCatMesh;
		VisualEvidence = visualEvidence ?? string.Empty;
	}
}
