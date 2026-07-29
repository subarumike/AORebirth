using AORebirth.Core.Entities;
using AORebirth.Core.Nanos;
using AORebirth.Enums;
using AORebirth.Interfaces;
using AORebirth.ObjectManager;
using MsgPack;
using SmokeLounge.AOtomation.Messaging.GameData;
using Utility;

namespace ZoneEngine.Core.Functions.GameFunctions;

internal class undefined : FunctionPrototype
{
	public override FunctionType FunctionId => (FunctionType)53240;

	public override bool Execute(INamedEntity self, IEntity caller, IInstancedEntity target, MessagePackObject[] arguments)
	{
		//IL_0094: Unknown result type (might be due to invalid IL or missing references)
		Character val = (Character)(object)((self is Character) ? self : null);
		if (val == null || arguments == null || arguments.Length < 1)
		{
			return false;
		}
		int num = ((MessagePackObject)(ref arguments[0])).AsInt32();
		if (!NanoLoader.NanoList.TryGetValue(num, out var _))
		{
			return castnano.ApplyInstantNano(val, target, num);
		}
		IInstancedEntity val2 = ResolvePerkPetTarget(val, target);
		if (val2 == null)
		{
			LogUtil.Debug((DebugInfoDetail)256, "Undefined/ChannelRage-style nano=" + num + " needs a pet target");
			return false;
		}
		val.SetTarget(((IEntity)val2).Identity);
		return castnano.ApplyInstantNano(val, val2, num);
	}

	internal static IInstancedEntity ResolvePerkPetTarget(Character owner, IInstancedEntity functionTarget)
	{
		//IL_003d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0061: Unknown result type (might be due to invalid IL or missing references)
		if (owner == null || ((Dynel)owner).Playfield == null)
		{
			return null;
		}
		ICharacter val = (ICharacter)(object)((functionTarget is ICharacter) ? functionTarget : null);
		if (IsOwnedPet(owner, val))
		{
			return (IInstancedEntity)(object)val;
		}
		val = ((Dynel)owner).Playfield.FindByIdentity<ICharacter>(owner.SelectedTarget);
		if (IsOwnedPet(owner, val))
		{
			return (IInstancedEntity)(object)val;
		}
		val = ((Dynel)owner).Playfield.FindByIdentity<ICharacter>(owner.FightingTarget);
		if (IsOwnedPet(owner, val))
		{
			return (IInstancedEntity)(object)val;
		}
		val = PetRuntimeService.Default.GetActivePetInStrain((ICharacter)(object)owner, 1015);
		if (val != null)
		{
			return (IInstancedEntity)(object)val;
		}
		val = PetRuntimeService.Default.GetActivePetInStrain((ICharacter)(object)owner, 1016);
		if (val != null)
		{
			return (IInstancedEntity)(object)val;
		}
		return (IInstancedEntity)(object)PetRuntimeService.Default.GetActivePetInStrain((ICharacter)(object)owner, 1017);
	}

	private static bool IsOwnedPet(Character owner, ICharacter candidate)
	{
		//IL_0008: Unknown result type (might be due to invalid IL or missing references)
		//IL_000d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0016: Unknown result type (might be due to invalid IL or missing references)
		//IL_001b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0047: Unknown result type (might be due to invalid IL or missing references)
		//IL_004c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0055: Unknown result type (might be due to invalid IL or missing references)
		//IL_005a: Unknown result type (might be due to invalid IL or missing references)
		if (owner != null && candidate != null)
		{
			Identity identity = ((IEntity)candidate).Identity;
			int instance = ((Identity)(ref identity)).Instance;
			identity = ((PooledObject)owner).Identity;
			if (instance != ((Identity)(ref identity)).Instance)
			{
				int result;
				if (PetCombatRules.IsPlayerOwnedPet(candidate) && PetCombatRules.ResolvePetOwner(candidate) != null)
				{
					identity = ((IEntity)PetCombatRules.ResolvePetOwner(candidate)).Identity;
					int instance2 = ((Identity)(ref identity)).Instance;
					identity = ((PooledObject)owner).Identity;
					result = ((instance2 == ((Identity)(ref identity)).Instance) ? 1 : 0);
				}
				else
				{
					result = 0;
				}
				return (byte)result != 0;
			}
		}
		return false;
	}
}
