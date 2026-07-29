using AORebirth.Core.Entities;
using AORebirth.Enums;
using AORebirth.Interfaces;
using MsgPack;

namespace ZoneEngine.Core.Functions.GameFunctions;

internal class summonpets : FunctionPrototype
{
	public override FunctionType FunctionId => (FunctionType)53181;

	public override bool Execute(INamedEntity self, IEntity caller, IInstancedEntity target, MessagePackObject[] arguments)
	{
		ICharacter val = (ICharacter)(object)((self is ICharacter) ? self : null);
		if (val == null)
		{
			return false;
		}
		if (arguments != null && arguments.Length >= 2)
		{
			return PetRuntimeService.Default.SummonPet(val, ((MessagePackObject)(ref arguments[0])).AsString(), ((MessagePackObject)(ref arguments[1])).AsInt32());
		}
		foreach (IActiveNano value in val.ActiveNanos.Values)
		{
			if (value == null || !PetSummonNanoCatalog.TryResolve(val, value.ID, out var summonParams))
			{
				continue;
			}
			return PetRuntimeService.Default.SummonPet(val, summonParams.PetHash, summonParams.PetTypeId);
		}
		return false;
	}
}
