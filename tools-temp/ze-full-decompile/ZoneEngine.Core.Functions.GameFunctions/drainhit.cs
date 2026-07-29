using AORebirth.Core.Entities;
using AORebirth.Enums;
using AORebirth.Interfaces;
using MsgPack;

namespace ZoneEngine.Core.Functions.GameFunctions;

internal class drainhit : FunctionPrototype
{
	public override FunctionType FunctionId => (FunctionType)53185;

	public override bool Execute(INamedEntity self, IEntity caller, IInstancedEntity target, MessagePackObject[] arguments)
	{
		return new hit().Execute(self, caller, target, arguments);
	}
}
