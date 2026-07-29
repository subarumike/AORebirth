using System.Collections.Generic;
using AORebirth.Core.Entities;
using AORebirth.Core.Nanos;
using AORebirth.Core.Playfields;
using AORebirth.Enums;
using AORebirth.Interfaces;
using AORebirth.ObjectManager;
using MsgPack;
using Utility;
using ZoneEngine.Core.Controllers;

namespace ZoneEngine.Core.Functions.GameFunctions;

internal class areacastnano : FunctionPrototype
{
	private const float DefaultRadiusMeters = 20f;

	public override FunctionType FunctionId => (FunctionType)53087;

	public override bool Execute(INamedEntity self, IEntity caller, IInstancedEntity target, MessagePackObject[] arguments)
	{
		//IL_0173: Unknown result type (might be due to invalid IL or missing references)
		Character val = (Character)(object)((self is Character) ? self : null);
		if (val == null || arguments == null || arguments.Length < 1)
		{
			return false;
		}
		int num = ((MessagePackObject)(ref arguments[0])).AsInt32();
		float num2 = 20f;
		NanoFormula value;
		if (arguments.Length >= 2)
		{
			int num3 = ((MessagePackObject)(ref arguments[1])).AsInt32();
			if (num3 > 0)
			{
				num2 = num3;
			}
		}
		else if (NanoLoader.NanoList.TryGetValue(num, out value))
		{
			int itemAttribute = value.getItemAttribute(287);
			if (itemAttribute > 0 && itemAttribute != 1234567890)
			{
				num2 = itemAttribute;
			}
		}
		if (!(((Dynel)val).Playfield is Playfield playfield))
		{
			return castnano.ApplyInstantNano(val, (IInstancedEntity)(object)val, num);
		}
		IList<ICharacter> list = playfield.FindCharacterInRange((IDynel)(object)val, num2);
		int num4 = 0;
		foreach (ICharacter item in list)
		{
			Character val2 = (Character)(object)((item is Character) ? item : null);
			if (val2 != null && val2 != val && IsValidAreaCastTarget(val, val2) && castnano.ApplyInstantNano(val, (IInstancedEntity)(object)val2, num))
			{
				num4++;
			}
		}
		LogUtil.Debug((DebugInfoDetail)256, $"AreaCastNano caster={((PooledObject)val).Identity} nested={num} radius={num2} hits={num4}");
		return true;
	}

	private static bool IsValidAreaCastTarget(Character caster, Character other)
	{
		if (((Dynel)other).Stats[(StatIds)455].BaseValue != 0 || ((Dynel)other).Stats[(StatIds)359].BaseValue != 0 || ((Dynel)other).Controller is NPCController)
		{
			return true;
		}
		if (!PlayerVersusPlayerCombatRules.IsProtectedPlayerVersusPlayerTarget((ICharacter)(object)other))
		{
			return false;
		}
		return PlayerVersusPlayerCombatRules.CanEngagePlayerVersusPlayerCombat((ICharacter)(object)caster, (ICharacter)(object)other);
	}
}
