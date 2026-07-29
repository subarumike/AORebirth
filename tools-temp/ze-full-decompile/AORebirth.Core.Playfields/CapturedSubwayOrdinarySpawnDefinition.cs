using System;
using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;

namespace AORebirth.Core.Playfields;

internal sealed class CapturedSubwayOrdinarySpawnDefinition
{
	public int SourceInstance { get; private set; }

	public string ArchetypeKey { get; private set; }

	public int Level { get; private set; }

	public int Health { get; private set; }

	public int HealthDamage { get; private set; }

	public int MonsterScale { get; private set; }

	public int RunSpeed { get; private set; }

	public float X { get; private set; }

	public float Y { get; private set; }

	public float Z { get; private set; }

	public float HeadingX { get; private set; }

	public float HeadingY { get; private set; }

	public float HeadingZ { get; private set; }

	public float HeadingW { get; private set; }

	public SimpleCharFullUpdateFlags CapturedFlags { get; private set; }

	public int CapturedFlags2 { get; private set; }

	public byte[] Unknown1 { get; private set; }

	public int Unknown2 { get; private set; }

	public CapturedSubwayWaypointDefinition[] Waypoints { get; private set; }

	public string SourceOwnerIdentity { get; private set; }

	public string EvidenceCapture { get; private set; }

	public string EvidenceTimestamp { get; private set; }

	public CapturedSubwayOrdinarySpawnDefinition(int sourceInstance, string archetypeKey, int level, int health, int healthDamage, int monsterScale, int runSpeed, float x, float y, float z, float headingX, float headingY, float headingZ, float headingW, SimpleCharFullUpdateFlags capturedFlags, int capturedFlags2, string unknown1Hex, int unknown2, CapturedSubwayWaypointDefinition[] waypoints, string sourceOwnerIdentity, string evidenceCapture, string evidenceTimestamp)
	{
		//IL_0084: Unknown result type (might be due to invalid IL or missing references)
		SourceInstance = sourceInstance;
		ArchetypeKey = archetypeKey;
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
		CapturedFlags = capturedFlags;
		CapturedFlags2 = capturedFlags2;
		Unknown1 = HexToBytes(unknown1Hex);
		Unknown2 = unknown2;
		Waypoints = waypoints ?? new CapturedSubwayWaypointDefinition[0];
		SourceOwnerIdentity = sourceOwnerIdentity;
		EvidenceCapture = evidenceCapture;
		EvidenceTimestamp = evidenceTimestamp;
	}

	private static byte[] HexToBytes(string value)
	{
		if (string.IsNullOrEmpty(value))
		{
			return new byte[0];
		}
		byte[] array = new byte[value.Length / 2];
		for (int i = 0; i < array.Length; i++)
		{
			array[i] = Convert.ToByte(value.Substring(i * 2, 2), 16);
		}
		return array;
	}
}
