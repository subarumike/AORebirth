using System.Collections.Generic;
using AORebirth.Core.Entities;
using AORebirth.Enums;
using AORebirth.Interfaces;
using AORebirth.ObjectManager;
using MsgPack;
using SmokeLounge.AOtomation.Messaging.GameData;

namespace ZoneEngine.Core.Functions.GameFunctions;

internal class teamcastnano : FunctionPrototype
{
	public override FunctionType FunctionId => (FunctionType)53066;

	public override bool Execute(INamedEntity self, IEntity caller, IInstancedEntity target, MessagePackObject[] arguments)
	{
		Character val = (Character)(object)((self is Character) ? self : null);
		if (val == null || arguments == null || arguments.Length < 1)
		{
			return false;
		}
		int nanoId = ((MessagePackObject)(ref arguments[0])).AsInt32();
		bool result = false;
		foreach (IInstancedEntity item in ResolveTeamCastTargets(val, target))
		{
			if (castnano.ApplyInstantNano(val, item, nanoId))
			{
				result = true;
			}
		}
		return result;
	}

	private static IEnumerable<IInstancedEntity> ResolveTeamCastTargets(Character character, IInstancedEntity functionTarget)
	{
		HashSet<int> yielded = new HashSet<int>();
		Identity identity;
		int num;
		if (functionTarget != null)
		{
			identity = ((IEntity)functionTarget).Identity;
			if (((Identity)(ref identity)).Instance != 0)
			{
				identity = ((IEntity)functionTarget).Identity;
				int instance = ((Identity)(ref identity)).Instance;
				identity = ((PooledObject)character).Identity;
				num = ((instance != ((Identity)(ref identity)).Instance) ? 1 : 0);
				goto IL_0090;
			}
		}
		num = 0;
		goto IL_0090;
		IL_0090:
		if (num != 0)
		{
			identity = ((IEntity)functionTarget).Identity;
			yielded.Add(((Identity)(ref identity)).Instance);
			yield return functionTarget;
		}
		int[] strains = new int[3] { 1015, 1016, 1017 };
		int[] array = strains;
		foreach (int strain in array)
		{
			ICharacter pet = PetRuntimeService.Default.GetActivePetInStrain((ICharacter)(object)character, strain);
			int num2;
			if (pet != null)
			{
				identity = ((IEntity)pet).Identity;
				num2 = (yielded.Contains(((Identity)(ref identity)).Instance) ? 1 : 0);
			}
			else
			{
				num2 = 1;
			}
			if (num2 == 0)
			{
				identity = ((IEntity)pet).Identity;
				yielded.Add(((Identity)(ref identity)).Instance);
				yield return (IInstancedEntity)(object)pet;
			}
		}
		if (yielded.Count == 0)
		{
			yield return (IInstancedEntity)(object)character;
		}
	}
}
