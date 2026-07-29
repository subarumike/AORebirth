using System;
using System.Collections.Generic;
using AORebirth.Core.Entities;
using AORebirth.Core.Network;
using AORebirth.Core.Playfields;
using AORebirth.Core.Vector;
using AORebirth.Interfaces;
using SmokeLounge.AOtomation.Messaging.GameData;
using Utility;

namespace ZoneEngine.Core.Missions;

internal static class MissionInstanceService
{
	internal const bool EntryEnabled = true;

	internal const int InstancePlayfieldId = 1413198;

	internal const int RomeBluePlayfieldInstance = 735;

	internal const float SpawnX = 1.8f;

	internal const float SpawnY = 5.01f;

	internal const float SpawnZ = 85.01f;

	internal static readonly int[] RomeEntranceDoorInstances = new int[3] { -1073544481, -1073610017, -1073675553 };

	internal static readonly float[][] RomeEntranceSpots = new float[3][]
	{
		new float[3] { 582.7546f, 22.25639f, 348.7862f },
		new float[3] { 582.5867f, 22.25661f, 279.4357f },
		new float[3] { 608.6941f, 22.25724f, 279.2319f }
	};

	private static readonly object ObjectiveGate = new object();

	private static readonly Dictionary<int, MissionRollType> ObjectiveByPlayfield = new Dictionary<int, MissionRollType>();

	private const float OutdoorExitMarkerStandoff = 12f;

	internal static bool IsMissionInstancePlayfield(int playfieldInstance)
	{
		if (playfieldInstance == 1413198 || playfieldInstance == 1419307)
		{
			return true;
		}
		if (MissionInstanceShapeCatalog.IsCapturedShapePlayfield(playfieldInstance))
		{
			return true;
		}
		return playfieldInstance >= 1376256 && playfieldInstance <= 1507327;
	}

	internal static void StampObjective(int playfieldInstance, MissionRollType type)
	{
		lock (ObjectiveGate)
		{
			ObjectiveByPlayfield[playfieldInstance] = type;
		}
	}

	internal static bool TryGetStampedObjective(int playfieldInstance, out MissionRollType type)
	{
		lock (ObjectiveGate)
		{
			return ObjectiveByPlayfield.TryGetValue(playfieldInstance, out type);
		}
	}

	internal static bool IsRomeEntranceDoor(int doorInstance)
	{
		for (int i = 0; i < RomeEntranceDoorInstances.Length; i++)
		{
			if (RomeEntranceDoorInstances[i] == doorInstance)
			{
				return true;
			}
		}
		return false;
	}

	internal static bool IsMissionEntranceTarget(Identity target)
	{
		//IL_0003: Unknown result type (might be due to invalid IL or missing references)
		//IL_000d: Invalid comparison between Unknown and I4
		if ((int)((Identity)(ref target)).Type == 56006)
		{
			return true;
		}
		if (IsRomeEntranceDoor(((Identity)(ref target)).Instance))
		{
			return true;
		}
		return false;
	}

	internal static bool IsAcceptedMissionEntranceUse(ICharacter character, Identity target)
	{
		//IL_0011: Unknown result type (might be due to invalid IL or missing references)
		//IL_0025: Unknown result type (might be due to invalid IL or missing references)
		//IL_002f: Invalid comparison between Unknown and I4
		//IL_0033: Unknown result type (might be due to invalid IL or missing references)
		//IL_003d: Invalid comparison between Unknown and I4
		//IL_0082: Unknown result type (might be due to invalid IL or missing references)
		//IL_0087: Unknown result type (might be due to invalid IL or missing references)
		if (character == null)
		{
			return false;
		}
		if (IsMissionEntranceTarget(target))
		{
			return true;
		}
		if ((int)((Identity)(ref target)).Type != 51016 && (int)((Identity)(ref target)).Type != 56006)
		{
			if (((IInstancedEntity)character).Playfield == null)
			{
				return false;
			}
			return IsNearAcceptedMarker(character, 10.0, 14.0);
		}
		Identity identity = ((IEntity)character).Identity;
		List<MissionAcceptedStore.AcceptedMission> all = MissionAcceptedStore.GetAll(((Identity)(ref identity)).Instance);
		for (int i = 0; i < all.Count; i++)
		{
			MissionAcceptedStore.AcceptedMission acceptedMission = all[i];
			if (acceptedMission != null)
			{
				if (acceptedMission.EntranceLow != 0 && (((Identity)(ref target)).Instance == acceptedMission.EntranceLow || (((Identity)(ref target)).Instance & 0xFFFF) == (acceptedMission.EntranceLow & 0xFFFF)))
				{
					return true;
				}
				if (acceptedMission.EntranceHigh != 0 && (((Identity)(ref target)).Instance == acceptedMission.EntranceHigh || (((Identity)(ref target)).Instance & 0xFFFF) == (acceptedMission.EntranceHigh & 0xFFFF)))
				{
					return true;
				}
			}
		}
		return IsNearAcceptedMarker(character, 10.0, 14.0);
	}

	internal static bool IsNearAcceptedMarker(ICharacter character, double horizontalRadius, double verticalRadius)
	{
		//IL_0025: Unknown result type (might be due to invalid IL or missing references)
		//IL_002a: Unknown result type (might be due to invalid IL or missing references)
		//IL_005e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0063: Unknown result type (might be due to invalid IL or missing references)
		if (character == null || ((IInstancedEntity)character).Playfield == null)
		{
			return false;
		}
		Identity identity = ((IEntity)((IInstancedEntity)character).Playfield).Identity;
		int instance = ((Identity)(ref identity)).Instance;
		float x = ((IDynel)character).RawCoordinates.X;
		float y = ((IDynel)character).RawCoordinates.Y;
		float z = ((IDynel)character).RawCoordinates.Z;
		double num = horizontalRadius * horizontalRadius;
		identity = ((IEntity)character).Identity;
		List<MissionAcceptedStore.AcceptedMission> all = MissionAcceptedStore.GetAll(((Identity)(ref identity)).Instance);
		for (int i = 0; i < all.Count; i++)
		{
			MissionAcceptedStore.AcceptedMission acceptedMission = all[i];
			if (acceptedMission != null && acceptedMission.MarkerPlayfield != 0 && acceptedMission.MarkerPlayfield == instance)
			{
				double num2 = x - acceptedMission.MarkerX;
				double num3 = z - acceptedMission.MarkerZ;
				if (num2 * num2 + num3 * num3 <= num && (double)Math.Abs(y - acceptedMission.MarkerY) <= verticalRadius)
				{
					return true;
				}
			}
		}
		return false;
	}

	internal static int ResolveInstancePlayfieldId(ICharacter character)
	{
		//IL_002a: Unknown result type (might be due to invalid IL or missing references)
		//IL_002f: Unknown result type (might be due to invalid IL or missing references)
		MissionShape[] shapes = MissionInstanceShapeCatalog.Shapes;
		if (shapes == null || shapes.Length == 0)
		{
			return 1413198;
		}
		int num;
		if (character == null)
		{
			num = Environment.TickCount;
		}
		else
		{
			Identity identity = ((IEntity)character).Identity;
			num = ((Identity)(ref identity)).Instance ^ Environment.TickCount;
		}
		int value = num;
		return shapes[Math.Abs(value) % shapes.Length].CapturedPlayfieldId;
	}

	internal static MissionRollType ResolveCharacterObjective(ICharacter character)
	{
		//IL_000f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0014: Unknown result type (might be due to invalid IL or missing references)
		if (character == null)
		{
			return MissionRollType.KillPerson;
		}
		Identity identity = ((IEntity)character).Identity;
		List<MissionAcceptedStore.AcceptedMission> all = MissionAcceptedStore.GetAll(((Identity)(ref identity)).Instance);
		if (all == null || all.Count == 0)
		{
			return MissionRollType.KillPerson;
		}
		MissionAcceptedStore.AcceptedMission acceptedMission = all[all.Count - 1];
		if (acceptedMission == null)
		{
			return MissionRollType.KillPerson;
		}
		return MissionTypeCatalog.TypeFromIcon(acceptedMission.MissionIconId);
	}

	internal static bool TryEnterMissionInstance(IZoneClient client)
	{
		//IL_004f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0054: Unknown result type (might be due to invalid IL or missing references)
		//IL_008f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0094: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c3: Unknown result type (might be due to invalid IL or missing references)
		//IL_00df: Unknown result type (might be due to invalid IL or missing references)
		//IL_00e1: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ec: Unknown result type (might be due to invalid IL or missing references)
		//IL_00f1: Unknown result type (might be due to invalid IL or missing references)
		//IL_00fa: Unknown result type (might be due to invalid IL or missing references)
		//IL_0103: Unknown result type (might be due to invalid IL or missing references)
		//IL_0112: Unknown result type (might be due to invalid IL or missing references)
		//IL_0119: Expected O, but got Unknown
		//IL_012f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0134: Unknown result type (might be due to invalid IL or missing references)
		//IL_0198: Unknown result type (might be due to invalid IL or missing references)
		//IL_019d: Unknown result type (might be due to invalid IL or missing references)
		if (client == null || client.Controller == null)
		{
			return false;
		}
		ICharacter character = client.Controller.Character;
		if (character == null || ((IInstancedEntity)character).Playfield == null)
		{
			return false;
		}
		Identity val = ((IEntity)((IInstancedEntity)character).Playfield).Identity;
		if (IsMissionInstancePlayfield(((Identity)(ref val)).Instance))
		{
			return false;
		}
		if (!MissionKeyGrantService.HasMissionKey(character))
		{
			return false;
		}
		val = ((IEntity)((IInstancedEntity)character).Playfield).Identity;
		int instance = ((Identity)(ref val)).Instance;
		int num = ResolveInstancePlayfieldId(character);
		MissionRollType type = ResolveCharacterObjective(character);
		StampObjective(num, type);
		ResolveInteriorSpawn(num, out var x, out var y, out var z);
		val = default(Identity);
		((Identity)(ref val)).Type = (IdentityType)51101;
		((Identity)(ref val)).Instance = num;
		Identity val2 = val;
		((IInstancedEntity)character).DoNotDoTimers = false;
		character.Teleport(new Coordinate
		{
			x = x,
			y = y,
			z = z
		}, (IQuaternion)(object)((IDynel)character).Heading, val2);
		Playfield.ArmPostZoneCollisionGrace(character);
		object[] array = new object[7];
		val = ((IEntity)character).Identity;
		array[0] = ((Identity)(ref val)).Instance;
		array[1] = instance;
		array[2] = num;
		array[3] = MissionTypeCatalog.TypeName(type);
		array[4] = x;
		array[5] = y;
		array[6] = z;
		MissionDiagnostics.Log("ENTRY-TELEPORT char={0} fromPf={1} destPf={2} objective={3} spawn=({4},{5},{6})", array);
		string[] obj = new string[6] { "MissionInstance enter char=", null, null, null, null, null };
		val = ((IEntity)character).Identity;
		obj[1] = ((object)(Identity)(ref val)).ToString();
		obj[2] = " pf=";
		obj[3] = num.ToString();
		obj[4] = " objective=";
		obj[5] = MissionTypeCatalog.TypeName(type);
		LogUtil.Debug((DebugInfoDetail)128, string.Concat(obj));
		return true;
	}

	internal static bool TryExitMissionInstance(IZoneClient client)
	{
		//IL_004f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0054: Unknown result type (might be due to invalid IL or missing references)
		//IL_0085: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a1: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a3: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ae: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b3: Unknown result type (might be due to invalid IL or missing references)
		//IL_00bb: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c3: Unknown result type (might be due to invalid IL or missing references)
		//IL_00d2: Unknown result type (might be due to invalid IL or missing references)
		//IL_00d9: Expected O, but got Unknown
		//IL_00ef: Unknown result type (might be due to invalid IL or missing references)
		//IL_00f4: Unknown result type (might be due to invalid IL or missing references)
		if (client == null || client.Controller == null)
		{
			return false;
		}
		ICharacter character = client.Controller.Character;
		if (character == null || ((IInstancedEntity)character).Playfield == null)
		{
			return false;
		}
		Identity val = ((IEntity)((IInstancedEntity)character).Playfield).Identity;
		if (!IsMissionInstancePlayfield(((Identity)(ref val)).Instance))
		{
			return false;
		}
		ResolveOutdoorExitDestination(character, out var destPf, out var destX, out var destY, out var destZ);
		val = default(Identity);
		((Identity)(ref val)).Type = (IdentityType)51101;
		((Identity)(ref val)).Instance = destPf;
		Identity val2 = val;
		((IInstancedEntity)character).DoNotDoTimers = false;
		character.Teleport(new Coordinate
		{
			x = destX,
			y = destY,
			z = destZ
		}, (IQuaternion)(object)((IDynel)character).Heading, val2);
		Playfield.ArmPostZoneCollisionGrace(character);
		object[] array = new object[5];
		val = ((IEntity)character).Identity;
		array[0] = ((Identity)(ref val)).Instance;
		array[1] = destPf;
		array[2] = destX;
		array[3] = destY;
		array[4] = destZ;
		MissionDiagnostics.Log("EXIT-TELEPORT char={0} destPf={1} dest=({2},{3},{4})", array);
		return true;
	}

	internal static bool IsMissionExitDoorTarget(Identity target)
	{
		//IL_0003: Unknown result type (might be due to invalid IL or missing references)
		//IL_000d: Invalid comparison between Unknown and I4
		//IL_0011: Unknown result type (might be due to invalid IL or missing references)
		//IL_001b: Invalid comparison between Unknown and I4
		return (int)((Identity)(ref target)).Type == 51016 || (int)((Identity)(ref target)).Type == 56006;
	}

	internal static void ResolveInteriorExitDoor(int playfieldId, out float x, out float y, out float z)
	{
		x = 1.8f;
		y = 5.01f;
		z = 85.01f;
		MissionShape missionShape = MissionInstanceShapeCatalog.PickShape(playfieldId, null);
		if (missionShape != null)
		{
			x = missionShape.SpawnX;
			y = missionShape.SpawnY;
			z = missionShape.SpawnZ;
		}
	}

	internal static bool IsNearInteriorExitDoor(ICharacter character, double horizontalRadius, double verticalRadius)
	{
		//IL_0022: Unknown result type (might be due to invalid IL or missing references)
		//IL_0027: Unknown result type (might be due to invalid IL or missing references)
		if (character == null || ((IInstancedEntity)character).Playfield == null)
		{
			return false;
		}
		Identity identity = ((IEntity)((IInstancedEntity)character).Playfield).Identity;
		ResolveInteriorExitDoor(((Identity)(ref identity)).Instance, out var x, out var y, out var z);
		double num = ((IDynel)character).RawCoordinates.X - x;
		double num2 = ((IDynel)character).RawCoordinates.Z - z;
		double num3 = Math.Abs(((IDynel)character).RawCoordinates.Y - y);
		return num * num + num2 * num2 <= horizontalRadius * horizontalRadius && num3 <= verticalRadius;
	}

	internal static void ResolveInteriorSpawn(int playfieldId, out float x, out float y, out float z)
	{
		x = 1.8f;
		y = 5.01f;
		z = 85.01f;
		MissionShape missionShape = MissionInstanceShapeCatalog.PickShape(playfieldId, null);
		if (missionShape != null)
		{
			x = missionShape.SpawnX;
			y = missionShape.SpawnY;
			z = missionShape.SpawnZ;
			ApplySpawnDoorClearance(missionShape.CapturedPlayfieldId, ref x, ref z);
		}
		else
		{
			z += 12f;
		}
	}

	private static void ApplySpawnDoorClearance(int capturedPlayfieldId, ref float x, ref float z)
	{
		switch (capturedPlayfieldId)
		{
		case 1419310:
		case 1419335:
			x -= 12f;
			break;
		case 1419382:
			x += 12f;
			z -= 8f;
			break;
		default:
			z += 12f;
			break;
		}
	}

	internal static void ResolveOutdoorExitDestination(ICharacter character, out int destPf, out float destX, out float destY, out float destZ)
	{
		//IL_0034: Unknown result type (might be due to invalid IL or missing references)
		//IL_0039: Unknown result type (might be due to invalid IL or missing references)
		destPf = 735;
		destX = RomeEntranceSpots[0][0];
		destY = RomeEntranceSpots[0][1];
		destZ = RomeEntranceSpots[0][2];
		if (character != null)
		{
			Identity identity = ((IEntity)character).Identity;
			List<MissionAcceptedStore.AcceptedMission> all = MissionAcceptedStore.GetAll(((Identity)(ref identity)).Instance);
			for (int num = all.Count - 1; num >= 0; num--)
			{
				MissionAcceptedStore.AcceptedMission acceptedMission = all[num];
				if (acceptedMission != null && acceptedMission.MarkerPlayfield != 0)
				{
					destPf = acceptedMission.MarkerPlayfield;
					destX = acceptedMission.MarkerX;
					destY = acceptedMission.MarkerY;
					destZ = acceptedMission.MarkerZ;
					break;
				}
			}
		}
		destX += 12f;
	}
}
