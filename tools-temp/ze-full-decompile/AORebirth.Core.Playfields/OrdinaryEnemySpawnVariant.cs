namespace AORebirth.Core.Playfields;

internal sealed class OrdinaryEnemySpawnVariant
{
	internal int Level { get; private set; }

	internal int Health { get; private set; }

	internal int HealthDamage { get; private set; }

	internal int MonsterScale { get; private set; }

	internal int RunSpeed { get; private set; }

	internal string Evidence { get; private set; }

	internal OrdinaryEnemySpawnWeaponLoadout WeaponLoadout { get; private set; }

	internal bool IsValid => Level > 0 && Health > 0 && HealthDamage >= 0 && HealthDamage < Health && MonsterScale > 0 && RunSpeed > 0 && !string.IsNullOrWhiteSpace(Evidence) && (WeaponLoadout == null || WeaponLoadout.IsValid);

	internal OrdinaryEnemySpawnVariant(int level, int health, int healthDamage, int monsterScale, int runSpeed, string evidence, OrdinaryEnemySpawnWeaponLoadout weaponLoadout = null)
	{
		Level = level;
		Health = health;
		HealthDamage = healthDamage;
		MonsterScale = monsterScale;
		RunSpeed = runSpeed;
		Evidence = evidence ?? string.Empty;
		WeaponLoadout = weaponLoadout;
	}
}
