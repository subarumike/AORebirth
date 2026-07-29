using AORebirth.Core.Entities;
using AORebirth.Enums;
using AORebirth.Interfaces;
using MsgPack;

namespace ZoneEngine.Core.Functions.GameFunctions;

internal class Function_changebodymesh : FunctionPrototype
{
	private const FunctionType functionId = 53054;

	public override FunctionType FunctionId => (FunctionType)53054;

	public override bool Execute(INamedEntity self, IEntity caller, IInstancedEntity target, MessagePackObject[] arguments)
	{
		lock (target)
		{
			return FunctionExecute(self, caller, target, arguments);
		}
	}

	public bool FunctionExecute(INamedEntity Self, IEntity Caller, IInstancedEntity Target, MessagePackObject[] Arguments)
	{
		//IL_0002: Unknown result type (might be due to invalid IL or missing references)
		//IL_0008: Expected O, but got Unknown
		Character val = (Character)Self;
		string text = ((MessagePackObject)(ref Arguments[0])).AsString();
		string text2 = text;
		if (text2 == "robe")
		{
			((Dynel)val).Stats[(StatIds)12].Value = 1;
		}
		else
		{
			((Dynel)val).Stats[(StatIds)12].Value = 0;
		}
		((Dynel)val).ChangedAppearance = true;
		return true;
	}
}
