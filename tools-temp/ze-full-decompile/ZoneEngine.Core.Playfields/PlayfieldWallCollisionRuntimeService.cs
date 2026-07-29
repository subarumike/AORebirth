using System;
using System.Collections.Generic;
using System.Globalization;
using AORebirth.Core.Entities;
using AORebirth.Core.Playfields;
using AORebirth.Core.Vector;
using AORebirth.Interfaces;
using SmokeLounge.AOtomation.Messaging.GameData;
using Utility;

namespace ZoneEngine.Core.Playfields;

internal sealed class PlayfieldWallCollisionRuntimeService
{
	internal void CheckWallCollision(ICharacter dynel, Func<ICharacter, bool> isPostZoneCollisionGraceActive, Action<Dynel, Coordinate, Quaternion, int> teleportToPlayfield)
	{
		//IL_001d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0022: Unknown result type (might be due to invalid IL or missing references)
		//IL_0048: Unknown result type (might be due to invalid IL or missing references)
		//IL_004d: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b9: Unknown result type (might be due to invalid IL or missing references)
		//IL_00be: Unknown result type (might be due to invalid IL or missing references)
		//IL_00cd: Unknown result type (might be due to invalid IL or missing references)
		//IL_00d2: Unknown result type (might be due to invalid IL or missing references)
		//IL_02ab: Unknown result type (might be due to invalid IL or missing references)
		//IL_02b2: Expected O, but got Unknown
		//IL_02cb: Unknown result type (might be due to invalid IL or missing references)
		//IL_02d0: Unknown result type (might be due to invalid IL or missing references)
		//IL_02e2: Unknown result type (might be due to invalid IL or missing references)
		//IL_02e7: Unknown result type (might be due to invalid IL or missing references)
		//IL_0372: Unknown result type (might be due to invalid IL or missing references)
		//IL_0386: Expected O, but got Unknown
		//IL_014d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0152: Unknown result type (might be due to invalid IL or missing references)
		//IL_0164: Unknown result type (might be due to invalid IL or missing references)
		//IL_0169: Unknown result type (might be due to invalid IL or missing references)
		if (isPostZoneCollisionGraceActive(dynel))
		{
			return;
		}
		Dictionary<int, PlayfieldData> pFData = PlayfieldLoader.PFData;
		Identity identity = ((IEntity)((IInstancedEntity)dynel).Playfield).Identity;
		if (!pFData.ContainsKey(((Identity)(ref identity)).Instance))
		{
			return;
		}
		Coordinate c = ((IDynel)dynel).Coordinates();
		identity = ((IEntity)((IInstancedEntity)dynel).Playfield).Identity;
		WallCollisionResult wallCollisionResult = WallCollision.CheckCollision(c, ((Identity)(ref identity)).Instance);
		if (wallCollisionResult == null)
		{
			return;
		}
		int destinationPlayfield = wallCollisionResult.SecondWall.DestinationPlayfield;
		if (destinationPlayfield <= 0)
		{
			return;
		}
		LogUtil.Debug((DebugInfoDetail)64, wallCollisionResult.ToString());
		if (!PlayfieldLoader.PFData.ContainsKey(destinationPlayfield))
		{
			CultureInfo invariantCulture = CultureInfo.InvariantCulture;
			identity = ((IEntity)dynel).Identity;
			string arg = ((Identity)(ref identity)).ToString(true);
			identity = ((IEntity)((IInstancedEntity)dynel).Playfield).Identity;
			LogUtil.Debug((DebugInfoDetail)128, string.Format(invariantCulture, "Wall collision ignored character={0} fromPlayfield={1} missingDestinationPlayfield={2}", arg, ((Identity)(ref identity)).Instance, destinationPlayfield));
			return;
		}
		PlayfieldData val = PlayfieldLoader.PFData[destinationPlayfield];
		byte destinationIndex = wallCollisionResult.SecondWall.DestinationIndex;
		if (!val.Destinations.TryGetValue(destinationIndex, out var value) || value == null)
		{
			CultureInfo invariantCulture2 = CultureInfo.InvariantCulture;
			object[] array = new object[8];
			identity = ((IEntity)dynel).Identity;
			array[0] = ((Identity)(ref identity)).ToString(true);
			identity = ((IEntity)((IInstancedEntity)dynel).Playfield).Identity;
			array[1] = ((Identity)(ref identity)).Instance;
			array[2] = ((IDynel)dynel).RawCoordinates.X;
			array[3] = ((IDynel)dynel).RawCoordinates.Y;
			array[4] = ((IDynel)dynel).RawCoordinates.Z;
			array[5] = destinationPlayfield;
			array[6] = destinationIndex;
			array[7] = val.Destinations.Count;
			LogUtil.Debug((DebugInfoDetail)128, string.Format(invariantCulture2, "Wall collision ignored character={0} fromPlayfield={1} fromCoords={2:F1},{3:F1},{4:F1} toPlayfield={5} missingDestinationIndex={6} destinationCount={7}", array));
		}
		else
		{
			LogUtil.Debug((DebugInfoDetail)64, ((object)value).ToString());
			float num = (value.EndX - value.StartX) * wallCollisionResult.Factor + value.StartX;
			float num2 = (value.EndZ - value.StartZ) * wallCollisionResult.Factor + value.StartZ;
			float num3 = WallCollision.Distance(value.StartX, value.StartZ, value.EndX, value.EndZ);
			float num4 = (value.EndX - value.StartX) / num3;
			float num5 = (value.EndZ - value.StartZ) / num3;
			num -= num5 * 8f;
			num2 += num4 * 8f;
			Coordinate val2 = new Coordinate(num, ((IDynel)dynel).RawCoordinates.Y, num2);
			CultureInfo invariantCulture3 = CultureInfo.InvariantCulture;
			object[] array2 = new object[9];
			identity = ((IEntity)dynel).Identity;
			array2[0] = ((Identity)(ref identity)).ToString(true);
			identity = ((IEntity)((IInstancedEntity)dynel).Playfield).Identity;
			array2[1] = ((Identity)(ref identity)).Instance;
			array2[2] = ((IDynel)dynel).RawCoordinates.X;
			array2[3] = ((IDynel)dynel).RawCoordinates.Y;
			array2[4] = ((IDynel)dynel).RawCoordinates.Z;
			array2[5] = destinationPlayfield;
			array2[6] = val2.x;
			array2[7] = val2.y;
			array2[8] = val2.z;
			LogUtil.Debug((DebugInfoDetail)128, string.Format(invariantCulture3, "Wall collision zoning character={0} fromPlayfield={1} fromCoords={2:F1},{3:F1},{4:F1} toPlayfield={5} toCoords={6:F1},{7:F1},{8:F1}", array2));
			teleportToPlayfield((Dynel)dynel, val2, ((IDynel)dynel).RawHeading, destinationPlayfield);
		}
	}
}
