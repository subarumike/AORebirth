using System.Linq;
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

internal class exitproxyplayfield : FunctionPrototype
{
	private const float DefaultExitDoorOffset = 2.5f;

	public override FunctionType FunctionId => (FunctionType)100000;

	public override bool Execute(INamedEntity self, IEntity caller, IInstancedEntity target, MessagePackObject[] arguments)
	{
		//IL_01bc: Unknown result type (might be due to invalid IL or missing references)
		//IL_01c3: Expected O, but got Unknown
		//IL_01df: Unknown result type (might be due to invalid IL or missing references)
		//IL_01e6: Expected O, but got Unknown
		//IL_01fa: Unknown result type (might be due to invalid IL or missing references)
		//IL_0201: Expected O, but got Unknown
		//IL_024d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0252: Unknown result type (might be due to invalid IL or missing references)
		//IL_0260: Unknown result type (might be due to invalid IL or missing references)
		//IL_0265: Unknown result type (might be due to invalid IL or missing references)
		//IL_0278: Unknown result type (might be due to invalid IL or missing references)
		//IL_027d: Unknown result type (might be due to invalid IL or missing references)
		//IL_028f: Unknown result type (might be due to invalid IL or missing references)
		//IL_02a7: Unknown result type (might be due to invalid IL or missing references)
		//IL_02bf: Unknown result type (might be due to invalid IL or missing references)
		//IL_0337: Unknown result type (might be due to invalid IL or missing references)
		//IL_033e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0347: Unknown result type (might be due to invalid IL or missing references)
		//IL_0363: Unknown result type (might be due to invalid IL or missing references)
		//IL_036a: Expected O, but got Unknown
		//IL_036a: Expected O, but got Unknown
		//IL_0091: Unknown result type (might be due to invalid IL or missing references)
		//IL_0096: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a4: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a9: Unknown result type (might be due to invalid IL or missing references)
		//IL_00bc: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c1: Unknown result type (might be due to invalid IL or missing references)
		//IL_00d3: Unknown result type (might be due to invalid IL or missing references)
		//IL_00eb: Unknown result type (might be due to invalid IL or missing references)
		//IL_0103: Unknown result type (might be due to invalid IL or missing references)
		//IL_0170: Unknown result type (might be due to invalid IL or missing references)
		//IL_017b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0197: Unknown result type (might be due to invalid IL or missing references)
		//IL_019e: Expected O, but got Unknown
		uint externalDoorInstance = ((IStats)self).Stats[(StatIds)193].BaseValue;
		int value = ((IStats)self).Stats[(StatIds)192].Value;
		StatelData val = PlayfieldLoader.PFData[value].Statels.FirstOrDefault(delegate(StatelData x)
		{
			//IL_0001: Unknown result type (might be due to invalid IL or missing references)
			//IL_0006: Unknown result type (might be due to invalid IL or missing references)
			//IL_0017: Unknown result type (might be due to invalid IL or missing references)
			//IL_001c: Unknown result type (might be due to invalid IL or missing references)
			//IL_001f: Unknown result type (might be due to invalid IL or missing references)
			//IL_0029: Invalid comparison between Unknown and I4
			Identity identity = x.Identity;
			int result;
			if (((Identity)(ref identity)).Instance == (int)externalDoorInstance)
			{
				identity = x.Identity;
				result = (((int)((Identity)(ref identity)).Type == 51016) ? 1 : 0);
			}
			else
			{
				result = 0;
			}
			return (byte)result != 0;
		});
		if (val != null)
		{
			Identity val2;
			if (SubwayTeleportProxyDestinationRules.TryResolveMainExitOverride(value, externalDoorInstance, out var destination, out var heading))
			{
				object[] array = new object[11];
				val2 = caller.Identity;
				array[0] = ((Identity)(ref val2)).ToString(true);
				val2 = ((IEntity)self).Identity;
				array[1] = ((Identity)(ref val2)).ToString(true);
				val2 = ((IEntity)((IInstancedEntity)self).Playfield).Identity;
				array[2] = ((Identity)(ref val2)).Instance;
				array[3] = ((Dynel)self).RawCoordinates.X;
				array[4] = ((Dynel)self).RawCoordinates.Y;
				array[5] = ((Dynel)self).RawCoordinates.Z;
				array[6] = externalDoorInstance;
				array[7] = value;
				array[8] = destination.x;
				array[9] = destination.y;
				array[10] = destination.z;
				LogUtil.Debug((DebugInfoDetail)64, string.Format("ExitProxyPlayfield caller={0} internal={1} currentPf={2} current=({3:F2},{4:F2},{5:F2}) externalDoor={6:X8} externalPf={7} dest=({8:F3},{9:F3},{10:F3}) evidence=official_live_subway_main_exit", array));
				IPlayfield playfield = ((IInstancedEntity)self).Playfield;
				Dynel val3 = (Dynel)self;
				Coordinate obj = destination;
				Quaternion obj2 = heading;
				val2 = default(Identity);
				((Identity)(ref val2)).Type = (IdentityType)51101;
				((Identity)(ref val2)).Instance = value;
				playfield.Teleport(val3, obj, (IQuaternion)(object)obj2, val2);
				return true;
			}
			Vector3 val4 = new Vector3((double)val.X, (double)val.Y, (double)val.Z);
			Quaternion val5 = new Quaternion((double)val.HeadingX, (double)val.HeadingY, (double)val.HeadingZ, (double)val.HeadingW);
			Quaternion.Normalize((IQuaternion)(object)val5);
			Vector3 val6 = (Vector3)val5.RotateVector3((IVector3)(object)Vector3.AxisZ);
			float num = 2.5f;
			val4.x += val6.x * (double)num;
			val4.z += val6.z * (double)num;
			object[] array2 = new object[12];
			val2 = caller.Identity;
			array2[0] = ((Identity)(ref val2)).ToString(true);
			val2 = ((IEntity)self).Identity;
			array2[1] = ((Identity)(ref val2)).ToString(true);
			val2 = ((IEntity)((IInstancedEntity)self).Playfield).Identity;
			array2[2] = ((Identity)(ref val2)).Instance;
			array2[3] = ((Dynel)self).RawCoordinates.X;
			array2[4] = ((Dynel)self).RawCoordinates.Y;
			array2[5] = ((Dynel)self).RawCoordinates.Z;
			array2[6] = externalDoorInstance;
			array2[7] = value;
			array2[8] = val4.x;
			array2[9] = val4.y;
			array2[10] = val4.z;
			array2[11] = num;
			LogUtil.Debug((DebugInfoDetail)64, string.Format("ExitProxyPlayfield caller={0} internal={1} currentPf={2} current=({3:F2},{4:F2},{5:F2}) externalDoor={6:X8} externalPf={7} dest=({8:F2},{9:F2},{10:F2}) offset={11:F2}", array2));
			IPlayfield playfield2 = ((IInstancedEntity)self).Playfield;
			Dynel val7 = (Dynel)self;
			Coordinate val8 = new Coordinate(val4);
			val2 = default(Identity);
			((Identity)(ref val2)).Type = (IdentityType)51101;
			((Identity)(ref val2)).Instance = value;
			playfield2.Teleport(val7, val8, (IQuaternion)(object)val5, val2);
		}
		return val != null;
	}
}
