using System;
using System.Collections.ObjectModel;
using System.Globalization;

namespace ZoneEngine.Core.Subway.Quests;

internal sealed class WindcallerKarrecNpcDefinition
{
	internal int SourceNpcInstance { get; private set; }

	internal string SourceNpcIdentity => "SimpleChar:" + SourceNpcInstance.ToString("X8", CultureInfo.InvariantCulture);

	internal string DisplayName { get; private set; }

	internal int PlayfieldId => 655;

	internal float X { get; private set; }

	internal float Y { get; private set; }

	internal float Z { get; private set; }

	internal float HeadingX { get; private set; }

	internal float HeadingY { get; private set; }

	internal float HeadingZ { get; private set; }

	internal float HeadingW { get; private set; }

	internal int AppearanceValue { get; private set; }

	internal int Side { get; private set; }

	internal int Fatness { get; private set; }

	internal int Breed { get; private set; }

	internal int Sex { get; private set; }

	internal int Race { get; private set; }

	internal int MonsterData { get; private set; }

	internal int MonsterScale { get; private set; }

	internal int HeadMesh { get; private set; }

	internal int NpcFamily { get; private set; }

	internal int NpcLosHeight { get; private set; }

	internal int Level { get; private set; }

	internal int Health { get; private set; }

	internal int RunSpeed { get; private set; }

	internal int CharacterFlags { get; private set; }

	internal int VisualFlags { get; private set; }

	internal int VisibleTitle { get; private set; }

	internal uint CapturedScfuFlags { get; private set; }

	internal ReadOnlyCollection<byte> CapturedScfuUnknown1 { get; private set; }

	internal ReadOnlyCollection<WindcallerKarrecNpcTextureDefinition> Textures { get; private set; }

	internal ReadOnlyCollection<WindcallerKarrecNpcMeshDefinition> Meshes { get; private set; }

	internal ReadOnlyCollection<WindcallerKarrecNpcWaypointDefinition> ScfuWaypoints { get; private set; }

	internal ReadOnlyCollection<WindcallerKarrecNpcPatrolSegment> PatrolSegments { get; private set; }

	internal ReadOnlyCollection<WindcallerKarrecNpcActiveNanoDefinition> ActiveNanos { get; private set; }

	internal bool HasPatrol => PatrolSegments.Count > 0;

	internal string Evidence { get; private set; }

	internal WindcallerKarrecNpcDefinition(int sourceNpcInstance, string displayName, float x, float y, float z, float headingX, float headingY, float headingZ, float headingW, int appearanceValue, int side, int fatness, int breed, int sex, int race, int monsterData, int monsterScale, int headMesh, int npcFamily, int npcLosHeight, int level, int health, int runSpeed, int characterFlags, int visualFlags, int visibleTitle, uint capturedScfuFlags, byte[] capturedScfuUnknown1, WindcallerKarrecNpcTextureDefinition[] textures, WindcallerKarrecNpcMeshDefinition[] meshes, WindcallerKarrecNpcWaypointDefinition[] scfuWaypoints, WindcallerKarrecNpcPatrolSegment[] patrolSegments, WindcallerKarrecNpcActiveNanoDefinition[] activeNanos, string evidence)
	{
		SourceNpcInstance = sourceNpcInstance;
		DisplayName = displayName;
		X = x;
		Y = y;
		Z = z;
		HeadingX = headingX;
		HeadingY = headingY;
		HeadingZ = headingZ;
		HeadingW = headingW;
		AppearanceValue = appearanceValue;
		Side = side;
		Fatness = fatness;
		Breed = breed;
		Sex = sex;
		Race = race;
		MonsterData = monsterData;
		MonsterScale = monsterScale;
		HeadMesh = headMesh;
		NpcFamily = npcFamily;
		NpcLosHeight = npcLosHeight;
		Level = level;
		Health = health;
		RunSpeed = runSpeed;
		CharacterFlags = characterFlags;
		VisualFlags = visualFlags;
		VisibleTitle = visibleTitle;
		CapturedScfuFlags = capturedScfuFlags;
		CapturedScfuUnknown1 = Array.AsReadOnly((byte[])capturedScfuUnknown1.Clone());
		Textures = Array.AsReadOnly((WindcallerKarrecNpcTextureDefinition[])textures.Clone());
		Meshes = Array.AsReadOnly((WindcallerKarrecNpcMeshDefinition[])meshes.Clone());
		ScfuWaypoints = Array.AsReadOnly((WindcallerKarrecNpcWaypointDefinition[])scfuWaypoints.Clone());
		PatrolSegments = Array.AsReadOnly((WindcallerKarrecNpcPatrolSegment[])patrolSegments.Clone());
		ActiveNanos = Array.AsReadOnly((WindcallerKarrecNpcActiveNanoDefinition[])activeNanos.Clone());
		Evidence = evidence;
	}

	internal WindcallerKarrecNpcWaypointDefinition ResolveScfuCoordinates(bool hasActivePatrolDestination, float existingX, float existingY, float existingZ, float currentX, float currentY, float currentZ)
	{
		return (hasActivePatrolDestination && HasPatrol) ? new WindcallerKarrecNpcWaypointDefinition(currentX, currentY, currentZ) : new WindcallerKarrecNpcWaypointDefinition(existingX, existingY, existingZ);
	}

	internal WindcallerKarrecNpcWaypointDefinition[] ResolveScfuWaypoints(bool hasActivePatrolDestination, float currentX, float currentY, float currentZ, float destinationX, float destinationY, float destinationZ)
	{
		if (hasActivePatrolDestination && HasPatrol)
		{
			return new WindcallerKarrecNpcWaypointDefinition[2]
			{
				new WindcallerKarrecNpcWaypointDefinition(currentX, currentY, currentZ),
				new WindcallerKarrecNpcWaypointDefinition(destinationX, destinationY, destinationZ)
			};
		}
		WindcallerKarrecNpcWaypointDefinition[] array = new WindcallerKarrecNpcWaypointDefinition[ScfuWaypoints.Count];
		for (int i = 0; i < array.Length; i++)
		{
			array[i] = ScfuWaypoints[i];
		}
		return array;
	}
}
