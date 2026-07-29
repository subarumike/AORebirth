using System.Linq;
using AORebirth.Core.Entities;
using AORebirth.Core.Vector;
using AORebirth.Enums;
using AORebirth.Interfaces;
using MsgPack;
using SmokeLounge.AOtomation.Messaging.GameData;
using ZoneEngine.Core.Playfields;

namespace ZoneEngine.Core.Functions.GameFunctions;

internal class lineteleport : FunctionPrototype
{
	public override FunctionType FunctionId => (FunctionType)53059;

	public override bool Execute(INamedEntity self, IEntity caller, IInstancedEntity target, MessagePackObject[] arguments)
	{
		//IL_006f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0074: Unknown result type (might be due to invalid IL or missing references)
		//IL_018e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0195: Expected O, but got Unknown
		//IL_01a0: Unknown result type (might be due to invalid IL or missing references)
		//IL_01bc: Unknown result type (might be due to invalid IL or missing references)
		if (arguments.Count() != 3)
		{
			return false;
		}
		ICharacter val = (ICharacter)(object)((self is ICharacter) ? self : null);
		if (val == null || ((IInstancedEntity)val).Playfield == null)
		{
			return false;
		}
		uint num = ((MessagePackObject)(ref arguments[1])).AsUInt32();
		int num2 = ((MessagePackObject)(ref arguments[2])).AsInt32();
		Identity val2;
		if (num2 == 0)
		{
			val2 = ((IEntity)((IInstancedEntity)val).Playfield).Identity;
			num2 = ((Identity)(ref val2)).Instance;
		}
		byte key = (byte)(num >> 16);
		if (!PlayfieldLoader.PFData.TryGetValue(num2, out var value))
		{
			return false;
		}
		if (!value.Destinations.TryGetValue(key, out var value2))
		{
			return false;
		}
		float num3 = (value2.EndX - value2.StartX) * 0.5f + value2.StartX;
		float num4 = (value2.EndZ - value2.StartZ) * 0.5f + value2.StartZ;
		float num5 = WallCollision.Distance(value2.StartX, value2.StartZ, value2.EndX, value2.EndZ);
		if (num5 <= 0f)
		{
			return false;
		}
		float num6 = (value2.EndX - value2.StartX) / num5;
		float num7 = (value2.EndZ - value2.StartZ) / num5;
		num3 -= num7 * 4f;
		num4 += num6 * 4f;
		Coordinate val3 = new Coordinate(num3, value2.EndY, num4);
		Quaternion heading = ((IDynel)val).Heading;
		val2 = default(Identity);
		((Identity)(ref val2)).Type = (IdentityType)51101;
		((Identity)(ref val2)).Instance = num2;
		val.Teleport(val3, (IQuaternion)(object)heading, val2);
		return true;
	}
}
