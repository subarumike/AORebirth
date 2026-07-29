using AORebirth.Core.Entities;
using AORebirth.Core.Vector;
using AORebirth.Enums;
using AORebirth.Interfaces;
using MsgPack;
using SmokeLounge.AOtomation.Messaging.GameData;

namespace ZoneEngine.Core.Functions.GameFunctions;

internal class teleport : FunctionPrototype
{
	private const FunctionType functionId = 53016;

	public override FunctionType FunctionId => (FunctionType)53016;

	public override bool Execute(INamedEntity self, IEntity caller, IInstancedEntity target, MessagePackObject[] arguments)
	{
		lock (target)
		{
			return FunctionExecute(self, caller, target, arguments);
		}
	}

	public bool FunctionExecute(INamedEntity Self, IEntity Caller, IInstancedEntity Target, MessagePackObject[] Arguments)
	{
		//IL_002b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0031: Expected O, but got Unknown
		//IL_0032: Unknown result type (might be due to invalid IL or missing references)
		//IL_0073: Unknown result type (might be due to invalid IL or missing references)
		//IL_009b: Unknown result type (might be due to invalid IL or missing references)
		//IL_009d: Unknown result type (might be due to invalid IL or missing references)
		//IL_006a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0070: Expected O, but got Unknown
		//IL_00bd: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c4: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b5: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ba: Unknown result type (might be due to invalid IL or missing references)
		Coordinate val = new Coordinate((float)((MessagePackObject)(ref Arguments[0])).AsInt32(), (float)((MessagePackObject)(ref Arguments[1])).AsInt32(), (float)((MessagePackObject)(ref Arguments[2])).AsInt32());
		IQuaternion val2 = (IQuaternion)(object)((Dynel)(Character)Self).Heading;
		if (val2 == null)
		{
			val2 = (IQuaternion)new Quaternion(0.0, 0.0, 0.0, 1.0);
		}
		Identity val3 = default(Identity);
		((Identity)(ref val3)).Type = (IdentityType)51101;
		((Identity)(ref val3)).Instance = ((MessagePackObject)(ref Arguments[3])).AsInt32();
		Identity val4 = val3;
		if (((Identity)(ref val4)).Instance == 0)
		{
			val4 = ((IEntity)((IInstancedEntity)Self).Playfield).Identity;
		}
		((Dynel)(Character)Self).Teleport(val, val2, val4);
		return true;
	}
}
