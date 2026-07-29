using AORebirth.Core.Entities;
using AORebirth.Enums;
using AORebirth.Interfaces;
using MsgPack;

namespace ZoneEngine.Core.Functions.GameFunctions;

internal class summonpet : FunctionPrototype
{
	public override FunctionType FunctionId => (FunctionType)53167;

	public override bool Execute(INamedEntity self, IEntity caller, IInstancedEntity target, MessagePackObject[] arguments)
	{
		ICharacter val = (ICharacter)(object)((self is ICharacter) ? self : null);
		if (val == null || arguments == null || arguments.Length < 2)
		{
			return false;
		}
		string petHash = ((MessagePackObject)(ref arguments[0])).AsString();
		int petTypeId = ((MessagePackObject)(ref arguments[1])).AsInt32();
		return PetRuntimeService.Default.SummonPet(val, petHash, petTypeId);
	}
}
