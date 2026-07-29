using AORebirth.Core.Entities;
using AORebirth.Enums;
using AORebirth.Interfaces;
using AORebirth.Stats;
using MsgPack;

namespace ZoneEngine.Core.Functions.GameFunctions;

internal class Function_modifypercentage : FunctionPrototype
{
	private const FunctionType functionId = 53184;

	public override FunctionType FunctionId => (FunctionType)53184;

	public override bool Execute(INamedEntity self, IEntity caller, IInstancedEntity target, MessagePackObject[] arguments)
	{
		lock (target)
		{
			return FunctionExecute(self, caller, target, arguments);
		}
	}

	public bool FunctionExecute(INamedEntity Self, IEntity Caller, IInstancedEntity Target, MessagePackObject[] Arguments)
	{
		//IL_001e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0024: Expected O, but got Unknown
		int num = ((MessagePackObject)(ref Arguments[0])).AsInt32();
		if (num == 61)
		{
			return true;
		}
		Character val = (Character)Self;
		IStat obj = ((Dynel)val).Stats[num];
		obj.PercentageModifier += ((MessagePackObject)(ref Arguments[1])).AsInt32();
		return true;
	}
}
