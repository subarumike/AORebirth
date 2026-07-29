using System;
using AORebirth.Core.Entities;
using AORebirth.Core.Playfields;
using AORebirth.Core.Statels;
using AORebirth.Core.Vector;
using AORebirth.Enums;
using AORebirth.Interfaces;
using AORebirth.Stats;
using MsgPack;
using SmokeLounge.AOtomation.Messaging.GameData;
using Utility;
using ZoneEngine.Core.Playfields;

namespace ZoneEngine.Core.Functions.GameFunctions;

internal class teleportproxy : FunctionPrototype
{
	private const FunctionType functionId = 53082;

	private const float ProxyEntryDoorClearance = 5f;

	public override FunctionType FunctionId => (FunctionType)53082;

	public override bool Execute(INamedEntity self, IEntity caller, IInstancedEntity target, MessagePackObject[] arguments)
	{
		//IL_0002: Unknown result type (might be due to invalid IL or missing references)
		//IL_0008: Expected O, but got Unknown
		//IL_003e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0043: Unknown result type (might be due to invalid IL or missing references)
		//IL_0067: Unknown result type (might be due to invalid IL or missing references)
		//IL_006c: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c4: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c9: Unknown result type (might be due to invalid IL or missing references)
		//IL_00db: Unknown result type (might be due to invalid IL or missing references)
		//IL_00e0: Unknown result type (might be due to invalid IL or missing references)
		//IL_017b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0186: Unknown result type (might be due to invalid IL or missing references)
		//IL_01b6: Unknown result type (might be due to invalid IL or missing references)
		//IL_01bc: Expected O, but got Unknown
		//IL_0238: Unknown result type (might be due to invalid IL or missing references)
		//IL_023f: Expected O, but got Unknown
		//IL_025f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0266: Expected O, but got Unknown
		//IL_027a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0281: Expected O, but got Unknown
		//IL_02d2: Unknown result type (might be due to invalid IL or missing references)
		//IL_02d7: Unknown result type (might be due to invalid IL or missing references)
		//IL_02e9: Unknown result type (might be due to invalid IL or missing references)
		//IL_02ee: Unknown result type (might be due to invalid IL or missing references)
		//IL_0339: Unknown result type (might be due to invalid IL or missing references)
		//IL_033e: Unknown result type (might be due to invalid IL or missing references)
		//IL_039d: Unknown result type (might be due to invalid IL or missing references)
		//IL_03a4: Unknown result type (might be due to invalid IL or missing references)
		//IL_03ad: Unknown result type (might be due to invalid IL or missing references)
		//IL_03dd: Unknown result type (might be due to invalid IL or missing references)
		//IL_03e3: Expected O, but got Unknown
		//IL_03e3: Expected O, but got Unknown
		ICharacter val = (ICharacter)self;
		int num = -1073741824 | ((MessagePackObject)(ref arguments[1])).AsInt32() | (((MessagePackObject)(ref arguments[2])).AsInt32() << 16);
		IStat obj = ((IStats)val).Stats[(StatIds)193];
		Identity val2 = caller.Identity;
		obj.BaseValue = (uint)((Identity)(ref val2)).Instance;
		IStat obj2 = ((IStats)val).Stats[(StatIds)192];
		val2 = ((IEntity)((IInstancedEntity)val).Playfield).Identity;
		obj2.BaseValue = (uint)((Identity)(ref val2)).Instance;
		if (((MessagePackObject)(ref arguments[1])).AsInt32() > 0)
		{
			if (SubwayTeleportProxyDestinationRules.TryResolveDestinationOverride(((MessagePackObject)(ref arguments[1])).AsInt32(), num, out var destination, out var heading))
			{
				object[] array = new object[9];
				val2 = caller.Identity;
				array[0] = ((Identity)(ref val2)).ToString(true);
				val2 = ((IEntity)((IInstancedEntity)val).Playfield).Identity;
				array[1] = ((Identity)(ref val2)).Instance;
				array[2] = ((IDynel)val).RawCoordinates.X;
				array[3] = ((IDynel)val).RawCoordinates.Y;
				array[4] = ((IDynel)val).RawCoordinates.Z;
				array[5] = ((MessagePackObject)(ref arguments[1])).AsInt32();
				array[6] = destination.x;
				array[7] = destination.y;
				array[8] = destination.z;
				LogUtil.Debug((DebugInfoDetail)64, string.Format("TeleportProxy caller={0} fromPf={1} from=({2:F2},{3:F2},{4:F2}) destDoor=SubwayEntranceOverride destPf={5} dest=({6:F2},{7:F2},{8:F2})", array));
				IPlayfield playfield = ((IInstancedEntity)val).Playfield;
				Dynel val3 = (Dynel)val;
				Coordinate obj3 = destination;
				Quaternion obj4 = heading;
				val2 = default(Identity);
				((Identity)(ref val2)).Type = (IdentityType)((MessagePackObject)(ref arguments[0])).AsInt32();
				((Identity)(ref val2)).Instance = ((MessagePackObject)(ref arguments[1])).AsInt32();
				playfield.Teleport(val3, obj3, (IQuaternion)(object)obj4, val2);
				return true;
			}
			StatelData door = PlayfieldLoader.PFData[((MessagePackObject)(ref arguments[1])).AsInt32()].GetDoor(num);
			if (door == null)
			{
				throw new Exception("Statel " + ((MessagePackObject)(ref arguments[3])).AsInt32().ToString("X") + " not found? Check the rdb dammit");
			}
			Vector3 val4 = new Vector3((double)door.X, (double)door.Y, (double)door.Z);
			Quaternion val5 = new Quaternion((double)door.HeadingX, (double)door.HeadingY, (double)door.HeadingZ, (double)door.HeadingW);
			Quaternion.Normalize((IQuaternion)(object)val5);
			Vector3 val6 = (Vector3)val5.RotateVector3((IVector3)(object)Vector3.AxisZ);
			val4.x += val6.x * 5.0;
			val4.z += val6.z * 5.0;
			object[] array2 = new object[10];
			val2 = caller.Identity;
			array2[0] = ((Identity)(ref val2)).ToString(true);
			val2 = ((IEntity)((IInstancedEntity)val).Playfield).Identity;
			array2[1] = ((Identity)(ref val2)).Instance;
			array2[2] = ((IDynel)val).RawCoordinates.X;
			array2[3] = ((IDynel)val).RawCoordinates.Y;
			array2[4] = ((IDynel)val).RawCoordinates.Z;
			val2 = door.Identity;
			array2[5] = ((Identity)(ref val2)).ToString(true);
			array2[6] = ((MessagePackObject)(ref arguments[1])).AsInt32();
			array2[7] = val4.x;
			array2[8] = val4.y;
			array2[9] = val4.z;
			LogUtil.Debug((DebugInfoDetail)64, string.Format("TeleportProxy caller={0} fromPf={1} from=({2:F2},{3:F2},{4:F2}) destDoor={5} destPf={6} dest=({7:F2},{8:F2},{9:F2})", array2));
			IPlayfield playfield2 = ((IInstancedEntity)val).Playfield;
			Dynel val7 = (Dynel)val;
			Coordinate val8 = new Coordinate(val4);
			val2 = default(Identity);
			((Identity)(ref val2)).Type = (IdentityType)((MessagePackObject)(ref arguments[0])).AsInt32();
			((Identity)(ref val2)).Instance = ((MessagePackObject)(ref arguments[1])).AsInt32();
			playfield2.Teleport(val7, val8, (IQuaternion)(object)val5, val2);
		}
		return true;
	}
}
