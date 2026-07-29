using AORebirth.Core.Entities;
using AORebirth.Enums;
using AORebirth.Interfaces;
using AORebirth.Stats;
using MsgPack;

namespace ZoneEngine.Core.Functions.GameFunctions;

internal class Function_set : FunctionPrototype
{
	private const FunctionType functionId = 53026;

	public override FunctionType FunctionId => (FunctionType)53026;

	public override bool Execute(INamedEntity self, IEntity caller, IInstancedEntity target, MessagePackObject[] arguments)
	{
		lock (target)
		{
			return FunctionExecute(self, caller, target, arguments);
		}
	}

	public bool FunctionExecute(INamedEntity Self, IEntity Caller, IInstancedEntity Target, MessagePackObject[] Arguments)
	{
		int num = ((MessagePackObject)(ref Arguments[0])).AsInt32();
		int num2 = ((MessagePackObject)(ref Arguments[1])).AsInt32();
		if (Target != null)
		{
			((IStats)Target).Stats[num].Set((uint)num2, false);
			return true;
		}
		return false;
	}
}
