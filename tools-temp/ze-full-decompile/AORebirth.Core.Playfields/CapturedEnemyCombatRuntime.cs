using System;
using AORebirth.Core.Entities;
using AORebirth.Core.Items;
using AORebirth.Enums;
using AORebirth.ObjectManager;
using AORebirth.Stats;
using SmokeLounge.AOtomation.Messaging.GameData;
using ZoneEngine.Core.Controllers;

namespace AORebirth.Core.Playfields;

internal static class CapturedEnemyCombatRuntime
{
	private const int MissingItemStatValue = 1234567890;

	internal static bool Prepare(Character character, NPCController controller, CapturedEnemyCombatContract contract, out string failure)
	{
		//IL_0037: Unknown result type (might be due to invalid IL or missing references)
		//IL_003c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0132: Unknown result type (might be due to invalid IL or missing references)
		//IL_0137: Unknown result type (might be due to invalid IL or missing references)
		//IL_00fd: Unknown result type (might be due to invalid IL or missing references)
		//IL_0102: Unknown result type (might be due to invalid IL or missing references)
		failure = string.Empty;
		if (character == null || controller == null || contract == null)
		{
			failure = "character, controller, or combat contract is null";
			return false;
		}
		Identity identity;
		if (!contract.IsCombatReady)
		{
			identity = ((PooledObject)character).Identity;
			CapturedEnemyCombatRuntimeRegistry.Register(((Identity)(ref identity)).Instance, contract);
			failure = "captured attack source is unresolved; evidence=" + contract.Evidence;
			return false;
		}
		controller.AiProfile = contract.AiProfile;
		if (contract.AttackModel == CapturedEnemyAttackModel.FixedAttackInfo)
		{
			SetMobStat((ICharacter)(object)character, (StatIds)286, contract.MinDamage);
			SetMobStat((ICharacter)(object)character, (StatIds)285, contract.MaxDamage);
			SetMobStat((ICharacter)(object)character, (StatIds)436, 91);
			SetMobStat((ICharacter)(object)character, (StatIds)339, 91);
			SetMobStat((ICharacter)(object)character, (StatIds)292, 0);
			SetMobStat((ICharacter)(object)character, (StatIds)1003, 0);
		}
		else if (contract.AttackModel == CapturedEnemyAttackModel.EquippedWeapon && !TryEquipCapturedWeapon(character, contract, out failure))
		{
			identity = ((PooledObject)character).Identity;
			CapturedEnemyCombatRuntimeRegistry.Register(((Identity)(ref identity)).Instance, CapturedEnemyCombatContract.Unresolved(contract.Evidence + "; runtime failure=" + failure, contract.Retaliates));
			return false;
		}
		identity = ((PooledObject)character).Identity;
		CapturedEnemyCombatRuntimeRegistry.Register(((Identity)(ref identity)).Instance, contract);
		return true;
	}

	private static bool TryEquipCapturedWeapon(Character character, CapturedEnemyCombatContract contract, out string failure)
	{
		//IL_00f0: Unknown result type (might be due to invalid IL or missing references)
		//IL_00f5: Unknown result type (might be due to invalid IL or missing references)
		//IL_00fe: Expected O, but got Unknown
		//IL_0106: Unknown result type (might be due to invalid IL or missing references)
		//IL_010b: Unknown result type (might be due to invalid IL or missing references)
		//IL_010c: Unknown result type (might be due to invalid IL or missing references)
		//IL_010e: Invalid comparison between Unknown and I4
		failure = string.Empty;
		if (!ItemLoader.ItemList.ContainsKey(contract.WeaponLowId) || !ItemLoader.ItemList.ContainsKey(contract.WeaponHighId))
		{
			failure = $"captured weapon template missing low={contract.WeaponLowId} high={contract.WeaponHighId}";
			return false;
		}
		if (((Dynel)character).BaseInventory == null || !((Dynel)character).BaseInventory.Pages.TryGetValue(101, out var value))
		{
			failure = "weapon inventory page is unavailable";
			return false;
		}
		if (!value.ValidSlot(contract.WeaponInventorySlot) || value[contract.WeaponInventorySlot] != null)
		{
			failure = "captured weapon slot is invalid or occupied: " + contract.WeaponInventorySlot;
			return false;
		}
		Item val = new Item(contract.WeaponQuality, contract.WeaponLowId, contract.WeaponHighId)
		{
			MultipleCount = 1
		};
		InventoryError val2 = value.Add(contract.WeaponInventorySlot, (IItem)(object)val);
		if ((int)val2 > 0)
		{
			failure = "captured weapon add failed: " + ((object)(InventoryError)(ref val2)).ToString();
			return false;
		}
		if (contract.HasCapturedEquippedAttackInfo)
		{
			ApplyCapturedEquippedAttackDisplayStats((ICharacter)(object)character, (IItem)(object)val);
		}
		return true;
	}

	private static void ApplyCapturedEquippedAttackDisplayStats(ICharacter character, IItem weapon)
	{
		ApplyWeaponStatIfPresent(character, weapon, (StatIds)292);
		ApplyWeaponStatIfPresent(character, weapon, (StatIds)436);
		ApplyWeaponStatIfPresent(character, weapon, (StatIds)1003);
	}

	private static void ApplyWeaponStatIfPresent(ICharacter character, IItem weapon, StatIds stat)
	{
		//IL_0002: Unknown result type (might be due to invalid IL or missing references)
		//IL_0008: Expected I4, but got Unknown
		//IL_0019: Unknown result type (might be due to invalid IL or missing references)
		int attribute = weapon.GetAttribute((int)stat);
		if (attribute != 1234567890)
		{
			SetMobStat(character, stat, attribute);
		}
	}

	private static void SetMobStat(ICharacter character, StatIds stat, int value)
	{
		//IL_0007: Unknown result type (might be due to invalid IL or missing references)
		//IL_0014: Expected I4, but got Unknown
		((IStats)character).Stats.SetBaseValueWithoutTriggering((int)stat, (uint)Math.Max(0, value));
	}
}
