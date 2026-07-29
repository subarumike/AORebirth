using System;
using System.Collections.ObjectModel;

namespace ZoneEngine.Core.Playfields;

internal sealed class CapturedSubwayVendorDefinition
{
	internal int SourceNpcInstance { get; private set; }

	internal int SourceVendorInstance { get; private set; }

	internal string DisplayName { get; private set; }

	internal int VendorTemplateId { get; private set; }

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

	internal int Level { get; private set; }

	internal int Health { get; private set; }

	internal int RunSpeed { get; private set; }

	internal int CharacterFlags { get; private set; }

	internal int VisualFlags { get; private set; }

	internal uint CapturedScfuFlags { get; private set; }

	internal ReadOnlyCollection<byte> CapturedScfuUnknown1 { get; private set; }

	internal ReadOnlyCollection<CapturedSubwayVendorTextureDefinition> Textures { get; private set; }

	internal ReadOnlyCollection<CapturedSubwayVendorMeshDefinition> Meshes { get; private set; }

	internal ReadOnlyCollection<CapturedSubwayVendorWaypointDefinition> Waypoints { get; private set; }

	internal ReadOnlyCollection<CapturedSubwayVendorStockDefinition> Stock { get; private set; }

	internal bool HasCapturedStock { get; private set; }

	internal string Evidence { get; private set; }

	internal string StockEvidence { get; private set; }

	internal CapturedSubwayVendorDefinition(int sourceNpcInstance, int sourceVendorInstance, string displayName, int vendorTemplateId, float x, float y, float z, float headingX, float headingY, float headingZ, float headingW, int appearanceValue, int side, int fatness, int breed, int sex, int race, int monsterData, int monsterScale, int headMesh, int level, int health, int runSpeed, int characterFlags, int visualFlags, uint capturedScfuFlags, byte[] capturedScfuUnknown1, CapturedSubwayVendorTextureDefinition[] textures, CapturedSubwayVendorMeshDefinition[] meshes, CapturedSubwayVendorWaypointDefinition[] waypoints, CapturedSubwayVendorStockDefinition[] stock, string evidence, string stockEvidence)
	{
		SourceNpcInstance = sourceNpcInstance;
		SourceVendorInstance = sourceVendorInstance;
		DisplayName = displayName;
		VendorTemplateId = vendorTemplateId;
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
		Level = level;
		Health = health;
		RunSpeed = runSpeed;
		CharacterFlags = characterFlags;
		VisualFlags = visualFlags;
		CapturedScfuFlags = capturedScfuFlags;
		CapturedScfuUnknown1 = Array.AsReadOnly((byte[])capturedScfuUnknown1.Clone());
		Textures = Array.AsReadOnly((CapturedSubwayVendorTextureDefinition[])textures.Clone());
		Meshes = Array.AsReadOnly((CapturedSubwayVendorMeshDefinition[])meshes.Clone());
		Waypoints = Array.AsReadOnly((CapturedSubwayVendorWaypointDefinition[])waypoints.Clone());
		Stock = Array.AsReadOnly((stock == null) ? new CapturedSubwayVendorStockDefinition[0] : ((CapturedSubwayVendorStockDefinition[])stock.Clone()));
		HasCapturedStock = stock != null;
		Evidence = evidence;
		StockEvidence = stockEvidence ?? string.Empty;
	}
}
