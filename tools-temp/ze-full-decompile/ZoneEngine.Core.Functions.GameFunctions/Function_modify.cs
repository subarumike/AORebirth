using AORebirth.Core.Entities;
using AORebirth.Enums;
using AORebirth.Interfaces;
using AORebirth.Stats;
using MsgPack;

namespace ZoneEngine.Core.Functions.GameFunctions;

internal class Function_modify : FunctionPrototype
{
	private const FunctionType functionId = 53045;

	public override FunctionType FunctionId => (FunctionType)53045;

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
		if (num == 61)
		{
			return true;
		}
		Character val = (Character)(object)((Target is Character) ? Target : null);
		if (val == null)
		{
			val = (Character)(object)((Self is Character) ? Self : null);
		}
		if (val == null)
		{
			return false;
		}
		IStat obj = ((Dynel)val).Stats[num];
		obj.Modifier += ((MessagePackObject)(ref Arguments[1])).AsInt32();
		return true;
	}
}
