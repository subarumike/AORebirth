namespace AORebirth.Core.Playfields;

internal sealed class CapturedSubwayGenerationVariantDefinition
{
	public int MonsterData { get; private set; }

	public int SourceInstance { get; private set; }

	public int Level { get; private set; }

	public int Health { get; private set; }

	public int HealthDamage { get; private set; }

	public int MonsterScale { get; private set; }

	public int RunSpeed { get; private set; }

	public int WeaponLowId { get; private set; }

	public int WeaponHighId { get; private set; }

	public int WeaponQuality { get; private set; }

	public string Evidence { get; private set; }

	public CapturedSubwayGenerationVariantDefinition(int monsterData, int sourceInstance, int level, int health, int healthDamage, int monsterScale, int runSpeed, int weaponLowId, int weaponHighId, int weaponQuality, string evidence)
	{
		MonsterData = monsterData;
		SourceInstance = sourceInstance;
		Level = level;
		Health = health;
		HealthDamage = healthDamage;
		MonsterScale = monsterScale;
		RunSpeed = runSpeed;
		WeaponLowId = weaponLowId;
		WeaponHighId = weaponHighId;
		WeaponQuality = weaponQuality;
		Evidence = evidence;
	}
}
