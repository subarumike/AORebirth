using ZoneEngine.Core;

namespace AORebirth.Core.Playfields;

internal sealed class CapturedEnemyCombatContract
{
	internal string Evidence { get; private set; }

	internal bool Retaliates { get; private set; }

	internal NpcAiProfile AiProfile { get; private set; }

	internal CapturedEnemyAttackModel AttackModel { get; private set; }

	internal int MinDamage { get; private set; }

	internal int MaxDamage { get; private set; }

	internal double RechargeSeconds { get; private set; }

	internal int AttackInfoWeaponSlot { get; private set; }

	internal int AttackInfoUnknown { get; private set; }

	internal int AttackInfoWeaponInstance { get; private set; }

	internal int WeaponLowId { get; private set; }

	internal int WeaponHighId { get; private set; }

	internal int WeaponQuality { get; private set; }

	internal int WeaponInventorySlot { get; private set; }

	internal bool HasEmptySpecialAttackWeaponContext { get; private set; }

	internal bool HasCapturedAttackStartContext { get; private set; }

	internal bool HasCapturedEquippedAttackInfo { get; private set; }

	internal bool HasCapturedCombatStopSequence { get; private set; }

	internal int AttackInfoAmmoCount { get; private set; }

	internal int SpecialAttackWeaponUnknown1 { get; private set; }

	internal int SpecialAttackWeaponUnknown2 { get; private set; }

	internal int SpecialAttackWeaponUnknown3 { get; private set; }

	internal int SpecialAttackWeaponUnknown4 { get; private set; }

	internal int SpecialAttackWeaponUnknown5 { get; private set; }

	internal double AttackStartDelaySeconds { get; private set; }

	internal double MovementTransitionDelaySeconds { get; private set; }

	internal double FirstHitDelaySeconds { get; private set; }

	internal bool SendStopFightOnDeath { get; private set; }

	internal bool RequiresDamageLineOfSight { get; private set; }

	internal CapturedEnemySpecialAttackSequenceDefinition SpecialAttackSequence { get; private set; }

	internal CapturedEnemyParallelAttackSequenceDefinition ParallelAttackSequence { get; private set; }

	internal bool IsCombatReady
	{
		get
		{
			if (!Retaliates)
			{
				return false;
			}
			return AttackModel switch
			{
				CapturedEnemyAttackModel.FixedAttackInfo => MinDamage > 0 && MaxDamage >= MinDamage, 
				CapturedEnemyAttackModel.EquippedWeapon => WeaponLowId > 0 && WeaponHighId > 0 && WeaponQuality > 0 && WeaponInventorySlot > 0, 
				CapturedEnemyAttackModel.Specialized => (SpecialAttackSequence != null && SpecialAttackSequence.IsValid) || (ParallelAttackSequence != null && ParallelAttackSequence.IsValid), 
				_ => false, 
			};
		}
	}

	private CapturedEnemyCombatContract()
	{
	}

	internal static CapturedEnemyCombatContract FixedAttack(string evidence, int minDamage, int maxDamage, double rechargeSeconds, int weaponSlot, int attackInfoUnknown, int weaponInstance, int attackInfoAmmoCount = 0)
	{
		return new CapturedEnemyCombatContract
		{
			Evidence = evidence,
			Retaliates = true,
			AiProfile = NpcAiProfile.Passive,
			AttackModel = CapturedEnemyAttackModel.FixedAttackInfo,
			MinDamage = minDamage,
			MaxDamage = maxDamage,
			RechargeSeconds = rechargeSeconds,
			AttackInfoAmmoCount = attackInfoAmmoCount,
			AttackInfoWeaponSlot = weaponSlot,
			AttackInfoUnknown = attackInfoUnknown,
			AttackInfoWeaponInstance = weaponInstance
		};
	}

	internal static CapturedEnemyCombatContract FixedAttackOnSight(string evidence, int minDamage, int maxDamage, double rechargeSeconds, int weaponSlot, int attackInfoUnknown, int weaponInstance)
	{
		return new CapturedEnemyCombatContract
		{
			Evidence = evidence,
			Retaliates = true,
			AiProfile = NpcAiProfile.Aggressive,
			AttackModel = CapturedEnemyAttackModel.FixedAttackInfo,
			MinDamage = minDamage,
			MaxDamage = maxDamage,
			RechargeSeconds = rechargeSeconds,
			AttackInfoWeaponSlot = weaponSlot,
			AttackInfoUnknown = attackInfoUnknown,
			AttackInfoWeaponInstance = weaponInstance,
			HasCapturedAttackStartContext = true,
			HasEmptySpecialAttackWeaponContext = true
		};
	}

	internal static CapturedEnemyCombatContract EquippedWeapon(string evidence, int lowId, int highId, int quality, int inventorySlot)
	{
		return new CapturedEnemyCombatContract
		{
			Evidence = evidence,
			Retaliates = true,
			AiProfile = NpcAiProfile.Passive,
			AttackModel = CapturedEnemyAttackModel.EquippedWeapon,
			WeaponLowId = lowId,
			WeaponHighId = highId,
			WeaponQuality = quality,
			WeaponInventorySlot = inventorySlot
		};
	}

	internal static CapturedEnemyCombatContract EquippedWeaponWithCapturedAttackInfo(string evidence, int lowId, int highId, int quality, int inventorySlot, int attackInfoAmmoCount, int attackInfoWeaponSlot, int attackInfoUnknown, int attackInfoWeaponInstance)
	{
		CapturedEnemyCombatContract capturedEnemyCombatContract = EquippedWeapon(evidence, lowId, highId, quality, inventorySlot);
		capturedEnemyCombatContract.HasCapturedEquippedAttackInfo = true;
		capturedEnemyCombatContract.AttackInfoAmmoCount = attackInfoAmmoCount;
		capturedEnemyCombatContract.AttackInfoWeaponSlot = attackInfoWeaponSlot;
		capturedEnemyCombatContract.AttackInfoUnknown = attackInfoUnknown;
		capturedEnemyCombatContract.AttackInfoWeaponInstance = attackInfoWeaponInstance;
		return capturedEnemyCombatContract;
	}

	internal static CapturedEnemyCombatContract EquippedWeaponWithEmptySpecialAttackContext(string evidence, int lowId, int highId, int quality, int inventorySlot, int minDamage, int maxDamage, double attackStartDelaySeconds, double movementTransitionDelaySeconds, double firstHitDelaySeconds, double rechargeSeconds, bool sendStopFightOnDeath, int attackInfoAmmoCount, int attackInfoUnknown, int unknown1, int unknown2, int unknown3, int unknown4, int unknown5, bool requiresDamageLineOfSight = false)
	{
		CapturedEnemyCombatContract capturedEnemyCombatContract = EquippedWeapon(evidence, lowId, highId, quality, inventorySlot);
		capturedEnemyCombatContract.HasEmptySpecialAttackWeaponContext = true;
		capturedEnemyCombatContract.HasCapturedAttackStartContext = true;
		capturedEnemyCombatContract.HasCapturedEquippedAttackInfo = true;
		capturedEnemyCombatContract.HasCapturedCombatStopSequence = true;
		capturedEnemyCombatContract.AttackInfoAmmoCount = attackInfoAmmoCount;
		capturedEnemyCombatContract.AttackInfoWeaponSlot = inventorySlot;
		capturedEnemyCombatContract.AttackInfoUnknown = attackInfoUnknown;
		capturedEnemyCombatContract.AttackInfoWeaponInstance = 0;
		capturedEnemyCombatContract.MinDamage = minDamage;
		capturedEnemyCombatContract.MaxDamage = maxDamage;
		capturedEnemyCombatContract.AttackStartDelaySeconds = attackStartDelaySeconds;
		capturedEnemyCombatContract.MovementTransitionDelaySeconds = movementTransitionDelaySeconds;
		capturedEnemyCombatContract.FirstHitDelaySeconds = firstHitDelaySeconds;
		capturedEnemyCombatContract.RechargeSeconds = rechargeSeconds;
		capturedEnemyCombatContract.SendStopFightOnDeath = sendStopFightOnDeath;
		capturedEnemyCombatContract.SpecialAttackWeaponUnknown1 = unknown1;
		capturedEnemyCombatContract.SpecialAttackWeaponUnknown2 = unknown2;
		capturedEnemyCombatContract.SpecialAttackWeaponUnknown3 = unknown3;
		capturedEnemyCombatContract.SpecialAttackWeaponUnknown4 = unknown4;
		capturedEnemyCombatContract.SpecialAttackWeaponUnknown5 = unknown5;
		capturedEnemyCombatContract.RequiresDamageLineOfSight = requiresDamageLineOfSight;
		return capturedEnemyCombatContract;
	}

	internal static CapturedEnemyCombatContract CapturedSpecialSequence(string evidence, CapturedEnemySpecialAttackSequenceDefinition specialAttackSequence)
	{
		return new CapturedEnemyCombatContract
		{
			Evidence = evidence,
			Retaliates = true,
			AiProfile = NpcAiProfile.Passive,
			AttackModel = CapturedEnemyAttackModel.Specialized,
			SpecialAttackSequence = specialAttackSequence
		};
	}

	internal static CapturedEnemyCombatContract CapturedParallelAttackSequence(string evidence, CapturedEnemyParallelAttackSequenceDefinition parallelAttackSequence, bool requiresDamageLineOfSight = false)
	{
		return new CapturedEnemyCombatContract
		{
			Evidence = evidence,
			Retaliates = true,
			AiProfile = NpcAiProfile.Passive,
			AttackModel = CapturedEnemyAttackModel.Specialized,
			ParallelAttackSequence = parallelAttackSequence,
			RequiresDamageLineOfSight = requiresDamageLineOfSight
		};
	}

	internal static CapturedEnemyCombatContract Unresolved(string evidence, bool retaliationObserved)
	{
		return new CapturedEnemyCombatContract
		{
			Evidence = evidence,
			Retaliates = retaliationObserved,
			AiProfile = NpcAiProfile.Passive,
			AttackModel = CapturedEnemyAttackModel.Unresolved
		};
	}
}
