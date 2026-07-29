using AORebirth.Core.Entities;
using AORebirth.Enums;
using AORebirth.Interfaces;
using MsgPack;

namespace ZoneEngine.Core.Functions.GameFunctions;

internal class openbank : FunctionPrototype
{
	public override FunctionType FunctionId => (FunctionType)53092;

	public override bool Execute(INamedEntity self, IEntity caller, IInstancedEntity target, MessagePackObject[] arguments)
	{
		//IL_0007: Unknown result type (might be due to invalid IL or missing references)
		//IL_0011: Expected O, but got Unknown
		InventoryContainerRuntimeService.Default.OpenBank((ICharacter)self);
		return true;
	}
}
