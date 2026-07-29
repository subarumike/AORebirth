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
using ZoneEngine.Core.Playfields;

namespace ZoneEngine.Core.Functions.GameFunctions;

internal class teleportproxy2 : FunctionPrototype
{
	public override FunctionType FunctionId => (FunctionType)53083;

	public override bool Execute(INamedEntity self, IEntity caller, IInstancedEntity target, MessagePackObject[] arguments)
	{
		//IL_0002: Unknown result type (might be due to invalid IL or missing references)
		//IL_0008: Expected O, but got Unknown
		//IL_0098: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a2: Unknown result type (might be due to invalid IL or missing references)
		//IL_00d2: Unknown result type (might be due to invalid IL or missing references)
		//IL_00d9: Expected O, but got Unknown
		//IL_0155: Unknown result type (might be due to invalid IL or missing references)
		//IL_015c: Expected O, but got Unknown
		//IL_017c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0183: Expected O, but got Unknown
		//IL_0197: Unknown result type (might be due to invalid IL or missing references)
		//IL_019e: Expected O, but got Unknown
		//IL_01e5: Unknown result type (might be due to invalid IL or missing references)
		//IL_01ec: Unknown result type (might be due to invalid IL or missing references)
		//IL_01f5: Unknown result type (might be due to invalid IL or missing references)
		//IL_0225: Unknown result type (might be due to invalid IL or missing references)
		//IL_022c: Expected O, but got Unknown
		//IL_022c: Expected O, but got Unknown
		ICharacter val = (ICharacter)self;
		int num = -1073741824 | ((MessagePackObject)(ref arguments[1])).AsInt32() | (((MessagePackObject)(ref arguments[2])).AsInt32() << 16);
		((IStats)val).Stats[(StatIds)193].BaseValue = 0u;
		((IStats)val).Stats[(StatIds)192].BaseValue = 0u;
		if (((MessagePackObject)(ref arguments[1])).AsInt32() > 0)
		{
			Identity val3;
			if (SubwayTeleportProxyDestinationRules.TryResolveDestinationOverride(((MessagePackObject)(ref arguments[1])).AsInt32(), num, out var destination, out var heading))
			{
				IPlayfield playfield = ((IInstancedEntity)val).Playfield;
				Dynel val2 = (Dynel)val;
				Coordinate obj = destination;
				Quaternion obj2 = heading;
				val3 = default(Identity);
				((Identity)(ref val3)).Type = (IdentityType)((MessagePackObject)(ref arguments[0])).AsInt32();
				((Identity)(ref val3)).Instance = ((MessagePackObject)(ref arguments[1])).AsInt32();
				playfield.Teleport(val2, obj, (IQuaternion)(object)obj2, val3);
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
			val4.x += val6.x * 2.5;
			val4.z += val6.z * 2.5;
			IPlayfield playfield2 = ((IInstancedEntity)val).Playfield;
			Dynel val7 = (Dynel)val;
			Coordinate val8 = new Coordinate(val4);
			val3 = default(Identity);
			((Identity)(ref val3)).Type = (IdentityType)((MessagePackObject)(ref arguments[0])).AsInt32();
			((Identity)(ref val3)).Instance = ((MessagePackObject)(ref arguments[1])).AsInt32();
			playfield2.Teleport(val7, val8, (IQuaternion)(object)val5, val3);
		}
		return true;
	}
}
