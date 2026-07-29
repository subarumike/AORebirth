using System.Linq;

namespace AORebirth.Core.Playfields;

internal sealed class CapturedEncounterRuntimeDefinition
{
	internal string ProfileKey { get; private set; }

	internal string SpawnKey { get; private set; }

	internal string EncounterKey { get; private set; }

	internal string DisplayName { get; private set; }

	internal int MonsterData { get; private set; }

	internal bool IsBoss { get; private set; }

	internal bool IsEncounterSummon { get; private set; }

	internal int Level { get; private set; }

	internal int Health { get; private set; }

	internal int MonsterScale { get; private set; }

	internal int RunSpeed { get; private set; }

	internal int CapturedScfuRunSpeedBase { get; private set; }

	internal int CapturedScfuNpcUnknownData { get; private set; }

	internal int Side { get; private set; }

	internal int NpcFamily { get; private set; }

	internal int NpcLosHeight { get; private set; }

	internal int Fatness { get; private set; }

	internal int Breed { get; private set; }

	internal int Sex { get; private set; }

	internal int Race { get; private set; }

	internal int HeadMesh { get; private set; }

	internal CapturedSubwayTextureDefinition[] Textures { get; private set; }

	internal CapturedSubwayMeshDefinition[] Meshes { get; private set; }

	internal CapturedSubwayWaypointDefinition[] Waypoints { get; private set; }

	internal float X { get; private set; }

	internal float Y { get; private set; }

	internal float Z { get; private set; }

	internal float HeadingX { get; private set; }

	internal float HeadingY { get; private set; }

	internal float HeadingZ { get; private set; }

	internal float HeadingW { get; private set; }

	internal uint AppearanceValue { get; private set; }

	internal int CapturedScfuFlags { get; private set; }

	internal int CapturedScfuFlags2 { get; private set; }

	internal byte[] CapturedScfuUnknown1 { get; private set; }

	internal int CapturedScfuUnknown2 { get; private set; }

	internal int CorpseCatMesh { get; private set; }

	internal double UnlootedCorpseLifetimeSeconds { get; private set; }

	internal double LootedCleanupSeconds { get; private set; }

	internal string Evidence { get; private set; }

	internal double? MaximumNpcLeashDistanceFromHome { get; private set; }

	internal CapturedEncounterRuntimeDefinition(string profileKey, string spawnKey, string encounterKey, string displayName, int monsterData, bool isBoss, bool isEncounterSummon, int level, int health, int monsterScale, int runSpeed, int capturedScfuRunSpeedBase, int capturedScfuNpcUnknownData, int side, float x, float y, float z, float headingX, float headingY, float headingZ, float headingW, uint appearanceValue, int capturedScfuFlags, int capturedScfuFlags2, byte[] capturedScfuUnknown1, int capturedScfuUnknown2, int corpseCatMesh, double unlootedCorpseLifetimeSeconds, double lootedCleanupSeconds, string evidence, int npcFamily = 150, int npcLosHeight = 0, int fatness = 1, int breed = 6, int sex = 0, int race = 1, int headMesh = 0, CapturedSubwayTextureDefinition[] textures = null, CapturedSubwayMeshDefinition[] meshes = null, CapturedSubwayWaypointDefinition[] waypoints = null, double? maximumNpcLeashDistanceFromHome = null)
	{
		ProfileKey = profileKey;
		SpawnKey = spawnKey;
		EncounterKey = encounterKey;
		DisplayName = displayName;
		MonsterData = monsterData;
		IsBoss = isBoss;
		IsEncounterSummon = isEncounterSummon;
		Level = level;
		Health = health;
		MonsterScale = monsterScale;
		RunSpeed = runSpeed;
		CapturedScfuRunSpeedBase = capturedScfuRunSpeedBase;
		CapturedScfuNpcUnknownData = capturedScfuNpcUnknownData;
		Side = side;
		NpcFamily = npcFamily;
		NpcLosHeight = npcLosHeight;
		Fatness = fatness;
		Breed = breed;
		Sex = sex;
		Race = race;
		HeadMesh = headMesh;
		Textures = textures ?? CreateDefaultTextures();
		Meshes = meshes ?? new CapturedSubwayMeshDefinition[0];
		Waypoints = waypoints ?? new CapturedSubwayWaypointDefinition[0];
		X = x;
		Y = y;
		Z = z;
		HeadingX = headingX;
		HeadingY = headingY;
		HeadingZ = headingZ;
		HeadingW = headingW;
		AppearanceValue = appearanceValue;
		CapturedScfuFlags = capturedScfuFlags;
		CapturedScfuFlags2 = capturedScfuFlags2;
		CapturedScfuUnknown1 = capturedScfuUnknown1 ?? new byte[0];
		CapturedScfuUnknown2 = capturedScfuUnknown2;
		CorpseCatMesh = corpseCatMesh;
		UnlootedCorpseLifetimeSeconds = unlootedCorpseLifetimeSeconds;
		LootedCleanupSeconds = lootedCleanupSeconds;
		Evidence = evidence;
		MaximumNpcLeashDistanceFromHome = maximumNpcLeashDistanceFromHome;
	}

	private static CapturedSubwayTextureDefinition[] CreateDefaultTextures()
	{
		return (from place in Enumerable.Range(0, 5)
			select new CapturedSubwayTextureDefinition(place, 0, 0)).ToArray();
	}
}
