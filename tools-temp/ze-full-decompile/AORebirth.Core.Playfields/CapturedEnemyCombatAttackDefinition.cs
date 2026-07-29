namespace AORebirth.Core.Playfields;

internal sealed class CapturedEnemyCombatAttackDefinition
{
	internal int MinDamage { get; private set; }

	internal int MaxDamage { get; private set; }

	internal int DamageBonus { get; private set; }

	internal double Range { get; private set; }

	internal double RechargeSeconds { get; private set; }

	internal bool UsesEquippedWeapon { get; private set; }

	internal int AttackInfoAmmoCount { get; private set; }

	internal int AttackInfoWeaponSlot { get; private set; }

	internal int AttackInfoUnknown { get; private set; }

	internal int AttackInfoHitType { get; private set; }

	internal int AttackInfoWeaponInstance { get; private set; }

	internal bool SendAttackInfo { get; private set; }

	internal bool IsValid => MinDamage > 0 && MaxDamage >= MinDamage && Range > 0.0 && RechargeSeconds > 0.0;

	internal CapturedEnemyCombatAttackDefinition(int minDamage, int maxDamage, int damageBonus, double range, double rechargeSeconds, bool usesEquippedWeapon, int attackInfoAmmoCount, int attackInfoWeaponSlot, int attackInfoUnknown, int attackInfoHitType, int attackInfoWeaponInstance, bool sendAttackInfo)
	{
		MinDamage = minDamage;
		MaxDamage = maxDamage;
		DamageBonus = damageBonus;
		Range = range;
		RechargeSeconds = rechargeSeconds;
		UsesEquippedWeapon = usesEquippedWeapon;
		AttackInfoAmmoCount = attackInfoAmmoCount;
		AttackInfoWeaponSlot = attackInfoWeaponSlot;
		AttackInfoUnknown = attackInfoUnknown;
		AttackInfoHitType = attackInfoHitType;
		AttackInfoWeaponInstance = attackInfoWeaponInstance;
		SendAttackInfo = sendAttackInfo;
	}
}
