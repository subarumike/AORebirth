using System;
using AORebirth.Core.Entities;
using AORebirth.Core.Playfields;
using AORebirth.Enums;
using AORebirth.Interfaces;
using AORebirth.Stats;
using SmokeLounge.AOtomation.Messaging.GameData;

namespace ZoneEngine.Core;

internal static class PetCombatRules
{
	public const int AttackPetLeftWeaponTemplate = 120634;

	public const int AttackPetRightWeaponTemplate = 120635;

	public const int AttackPetLeftWeaponHighTemplate = 120637;

	public const int AttackPetRightWeaponHighTemplate = 120638;

	private const int UnsetTemplateStatValue = 1234567890;

	public const int AttackPetLeftWeaponTag = 1296389938;

	public const int AttackPetRightWeaponTag = 1296389937;

	public const string AttackPetLeftWeaponName = "MEW2";

	public const string AttackPetRightWeaponName = "MEW1";

	public const int AttackPetSpecialAttackWeaponValue = 841;

	public const int AttackPetAttackInfoWeaponSlot = 1;

	public const int AttackPetAttackInfoUnk1 = 4;

	public const int AttackPetAttackInfoHitType = 1;

	public const double AttackPetRechargeSeconds = 2.0;

	public const double HealCastRange = 20.0;

	public const double HealCastRetrySeconds = 2.5;

	public const int HealingPetCapturedCurrentNano = 13184;

	public const int HealingPetCapturedMaxNano = 13184;

	public const int PetHealthRegenMaxLifeDivisor = 120;

	public const double PetHealthRegenIntervalSeconds = 1.0;

	public const int PetNanoRegenMaxNanoDivisor = 120;

	public const double PetNanoRegenIntervalSeconds = 1.0;

	public const int NpcHealthRegenMaxLifeDivisor = 2400;

	public const double NpcHealthRegenIntervalSeconds = 5.0;

	public static int ResolvePetHealthRegenDelta(int maxHealth)
	{
		return Math.Max(1, maxHealth / 120);
	}

	public static int ResolvePetNanoRegenDelta(int maxNano)
	{
		if (maxNano <= 0)
		{
			return 0;
		}
		return Math.Max(1, maxNano / 120);
	}

	public static int ResolveNpcHealthRegenDelta(int maxHealth)
	{
		return Math.Max(1, maxHealth / 2400);
	}

	public static int ResolveLevelEquivalentAttackPetMinDamage(int level)
	{
		int num = Math.Max(1, level);
		return Math.Max(3, num * 9 / 5 + 2);
	}

	public static int ResolveLevelEquivalentAttackPetMaxDamage(int level)
	{
		int num = Math.Max(1, level);
		int val = ResolveLevelEquivalentAttackPetMinDamage(num);
		int val2 = num * 13 / 5 + 6;
		return Math.Max(val, val2);
	}

	public static bool IsPlayerOwnedPet(ICharacter character)
	{
		if (character == null)
		{
			return false;
		}
		int value = ((IStats)character).Stats[(StatIds)196].Value;
		return value > 0 && value != 1234567890;
	}

	public static bool IsPlayerOwnedAttackPet(ICharacter pet)
	{
		//IL_0049: Unknown result type (might be due to invalid IL or missing references)
		//IL_004f: Unknown result type (might be due to invalid IL or missing references)
		if (!IsPlayerOwnedPet(pet) || ((IInstancedEntity)pet).Playfield == null)
		{
			return false;
		}
		ICharacter val = ResolvePetOwner(pet);
		if (val == null)
		{
			return false;
		}
		ICharacter activePetInStrain = PetRuntimeService.Default.GetActivePetInStrain(val, 1015);
		return activePetInStrain != null && ((IEntity)activePetInStrain).Identity == ((IEntity)pet).Identity;
	}

	public static bool IsPlayerOwnedBureaucratCompanionPet(ICharacter pet)
	{
		//IL_0049: Unknown result type (might be due to invalid IL or missing references)
		//IL_004f: Unknown result type (might be due to invalid IL or missing references)
		if (!IsPlayerOwnedPet(pet) || ((IInstancedEntity)pet).Playfield == null)
		{
			return false;
		}
		ICharacter val = ResolvePetOwner(pet);
		if (val == null)
		{
			return false;
		}
		ICharacter activePetInStrain = PetRuntimeService.Default.GetActivePetInStrain(val, 1017);
		return activePetInStrain != null && ((IEntity)activePetInStrain).Identity == ((IEntity)pet).Identity;
	}

	public static bool IsPlayerOwnedBureaucratGuardianPet(ICharacter pet)
	{
		return PetBureaucratGuardianAppearance.IsGuardianPet(pet);
	}

	public static bool IsPlayerOwnedMewAttackPet(ICharacter pet)
	{
		return IsPlayerOwnedAttackPet(pet) && !IsPlayerOwnedBureaucratGuardianPet(pet);
	}

	public static bool IsPlayerOwnedMeleeCombatPet(ICharacter pet)
	{
		return IsPlayerOwnedAttackPet(pet) || IsPlayerOwnedBureaucratCompanionPet(pet);
	}

	public static bool IsPlayerOwnedHealingPet(ICharacter pet)
	{
		//IL_0049: Unknown result type (might be due to invalid IL or missing references)
		//IL_004f: Unknown result type (might be due to invalid IL or missing references)
		if (!IsPlayerOwnedPet(pet) || ((IInstancedEntity)pet).Playfield == null)
		{
			return false;
		}
		ICharacter val = ResolvePetOwner(pet);
		if (val == null)
		{
			return false;
		}
		ICharacter activePetInStrain = PetRuntimeService.Default.GetActivePetInStrain(val, 1016);
		return activePetInStrain != null && ((IEntity)activePetInStrain).Identity == ((IEntity)pet).Identity;
	}

	public static ICharacter ResolvePetOwner(ICharacter pet)
	{
		//IL_001b: Unknown result type (might be due to invalid IL or missing references)
		//IL_004b: Unknown result type (might be due to invalid IL or missing references)
		if (!IsPlayerOwnedPet(pet))
		{
			return null;
		}
		IPlayfield playfield = ((IInstancedEntity)pet).Playfield;
		Identity val = default(Identity);
		((Identity)(ref val)).Type = (IdentityType)50000;
		((Identity)(ref val)).Instance = ((IStats)pet).Stats[(StatIds)196].Value;
		return playfield.FindByIdentity<ICharacter>(val);
	}
}
