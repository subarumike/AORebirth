using AORebirth.Core.Entities;
using AORebirth.Enums;
using AORebirth.Interfaces;
using AORebirth.Stats;
using SmokeLounge.AOtomation.Messaging.GameData;
using ZoneEngine.Core.Controllers;
using ZoneEngine.Core.Playfields;

namespace ZoneEngine.Core;

internal static class PlayerVersusPlayerCombatRules
{
	private const int PvpFlaggedVisualFlagBit = 64;

	private const int DefaultSuppressionGasPercent = 75;

	internal static bool IsPlayerCharacter(ICharacter character)
	{
		return character != null && ((IDynel)character).Controller is PlayerController;
	}

	internal static bool IsPlayerOwnedPetTarget(ICharacter character)
	{
		return PetCombatRules.IsPlayerOwnedPet(character);
	}

	internal static bool IsProtectedPlayerVersusPlayerTarget(ICharacter target)
	{
		return IsPlayerCharacter(target) || IsPlayerOwnedPetTarget(target);
	}

	internal static bool IsPlayerControlledCombatant(ICharacter character)
	{
		return IsPlayerCharacter(character) || PetCombatRules.IsPlayerOwnedPet(character);
	}

	internal static bool CanEngagePlayerVersusPlayerCombat(ICharacter attacker, ICharacter target)
	{
		if (attacker == null || target == null)
		{
			return false;
		}
		if (!IsPlayerControlledCombatant(attacker) || !IsProtectedPlayerVersusPlayerTarget(target))
		{
			return true;
		}
		int suppressionGas = ResolveSuppressionGas(attacker);
		int suppressionGas2 = ResolveSuppressionGas(target);
		if (IsLowSuppressionGasZone(suppressionGas) || IsLowSuppressionGasZone(suppressionGas2))
		{
			return true;
		}
		return IsPvpFlagged(attacker) || IsPvpFlagged(target);
	}

	internal static bool IsPvpFlagged(ICharacter character)
	{
		ICharacter val = ResolveAuthorizationSubject(character);
		if (val == null)
		{
			return false;
		}
		return (((IStats)val).Stats[(StatIds)673].Value & 0x40) != 0;
	}

	internal static int ResolveSuppressionGas(ICharacter character)
	{
		//IL_0027: Unknown result type (might be due to invalid IL or missing references)
		//IL_002c: Unknown result type (might be due to invalid IL or missing references)
		ICharacter val = ResolveAuthorizationSubject(character);
		if (val == null || ((IInstancedEntity)val).Playfield == null)
		{
			return 75;
		}
		Identity identity = ((IEntity)((IInstancedEntity)val).Playfield).Identity;
		return ZoneEngine.Core.Playfields.Playfields.ResolveSuppressionGasPercent(((Identity)(ref identity)).Instance);
	}

	private static bool IsLowSuppressionGasZone(int suppressionGas)
	{
		return suppressionGas == 5 || suppressionGas == 25;
	}

	private static ICharacter ResolveAuthorizationSubject(ICharacter character)
	{
		if (character == null)
		{
			return null;
		}
		if (PetCombatRules.IsPlayerOwnedPet(character))
		{
			ICharacter val = PetCombatRules.ResolvePetOwner(character);
			if (val != null)
			{
				return val;
			}
		}
		return character;
	}
}
