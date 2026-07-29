using AORebirth.Core.Entities;
using AORebirth.Enums;
using AORebirth.Interfaces;
using AORebirth.ObjectManager;
using MsgPack;
using Utility;

namespace ZoneEngine.Core.Functions.GameFunctions;

internal class changeactionrestriction : FunctionPrototype
{
	private const int ImplantAccessFlag = 16;

	public override FunctionType FunctionId => (FunctionType)53067;

	public override bool Execute(INamedEntity self, IEntity caller, IInstancedEntity target, MessagePackObject[] arguments)
	{
		//IL_0089: Unknown result type (might be due to invalid IL or missing references)
		Character val = (Character)(object)((self is Character) ? self : null);
		if (val == null || arguments == null || arguments.Length < 2)
		{
			return false;
		}
		int num = ((MessagePackObject)(ref arguments[0])).AsInt32();
		int num2 = ((MessagePackObject)(ref arguments[1])).AsInt32();
		if ((num & 0x10) == 0)
		{
			LogUtil.Debug((DebugInfoDetail)256, $"ChangeActionRestriction ignored unsupported flags={num} duration={num2}");
			return true;
		}
		val.GrantImplantAccess(num2);
		LogUtil.Debug((DebugInfoDetail)256, $"ChangeActionRestriction implant access char={((PooledObject)val).Identity} duration={num2}");
		return true;
	}
}
