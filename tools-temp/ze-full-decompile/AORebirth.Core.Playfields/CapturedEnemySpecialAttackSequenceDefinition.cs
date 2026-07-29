namespace AORebirth.Core.Playfields;

internal sealed class CapturedEnemySpecialAttackSequenceDefinition
{
	internal double InitialAttackDelaySeconds { get; private set; }

	internal CapturedEnemyCombatAttackDefinition OpeningAttack { get; private set; }

	internal CapturedEnemyCombatAttackDefinition RepeatingAttack { get; private set; }

	internal CapturedEnemySpecialAttackDefinition[] SpecialAttacks { get; private set; }

	internal int SpecialAttackWeaponUnknown1 { get; private set; }

	internal int SpecialAttackWeaponUnknown2 { get; private set; }

	internal int SpecialAttackWeaponUnknown3 { get; private set; }

	internal int SpecialAttackWeaponUnknown4 { get; private set; }

	internal int SpecialAttackWeaponUnknown5 { get; private set; }

	internal bool IsValid => InitialAttackDelaySeconds >= 0.0 && (OpeningAttack == null || OpeningAttack.IsValid) && RepeatingAttack != null && RepeatingAttack.IsValid;

	internal CapturedEnemySpecialAttackSequenceDefinition(double initialAttackDelaySeconds, CapturedEnemyCombatAttackDefinition openingAttack, CapturedEnemyCombatAttackDefinition repeatingAttack, CapturedEnemySpecialAttackDefinition[] specialAttacks, int specialAttackWeaponUnknown1, int specialAttackWeaponUnknown2, int specialAttackWeaponUnknown3, int specialAttackWeaponUnknown4, int specialAttackWeaponUnknown5)
	{
		InitialAttackDelaySeconds = initialAttackDelaySeconds;
		OpeningAttack = openingAttack;
		RepeatingAttack = repeatingAttack;
		SpecialAttacks = specialAttacks ?? new CapturedEnemySpecialAttackDefinition[0];
		SpecialAttackWeaponUnknown1 = specialAttackWeaponUnknown1;
		SpecialAttackWeaponUnknown2 = specialAttackWeaponUnknown2;
		SpecialAttackWeaponUnknown3 = specialAttackWeaponUnknown3;
		SpecialAttackWeaponUnknown4 = specialAttackWeaponUnknown4;
		SpecialAttackWeaponUnknown5 = specialAttackWeaponUnknown5;
	}
}
