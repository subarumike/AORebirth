using AORebirth.Core.Playfields;

namespace ZoneEngine.Core.Playfields;

internal sealed class CapturedSubwaySpawnDefinition
{
	public int SourceInstance { get; private set; }

	public string ContentSection { get; internal set; }

	public string TemplateHash { get; private set; }

	public string Name { get; private set; }

	public int MonsterData { get; private set; }

	public int Level { get; private set; }

	public int Health { get; private set; }

	public int HealthDamage { get; private set; }

	public int MonsterScale { get; private set; }

	public int HeadMesh { get; private set; }

	public int RunSpeed { get; private set; }

	public int NpcFamily { get; private set; }

	public int CharacterFlags { get; private set; }

	public int Breed { get; private set; }

	public int Sex { get; private set; }

	public float X { get; private set; }

	public float Y { get; private set; }

	public float Z { get; private set; }

	public float? PatrolX { get; private set; }

	public float? PatrolY { get; private set; }

	public float? PatrolZ { get; private set; }

	public bool UseSpawnAsPatrolStart { get; private set; }

	public double? RespawnDelaySeconds { get; private set; }

	public CapturedEnemyCombatContract Combat { get; private set; }

	public bool HasPatrolWaypoint => PatrolX.HasValue && PatrolY.HasValue && PatrolZ.HasValue;

	public bool HasRespawnDelay => RespawnDelaySeconds.HasValue && RespawnDelaySeconds.Value > 0.0;

	public CapturedSubwaySpawnDefinition(int sourceInstance, string templateHash, string name, int monsterData, int level, int health, int monsterScale, int headMesh, int runSpeed, int npcFamily, int characterFlags, int breed, int sex, float x, float y, float z, float? patrolX = null, float? patrolY = null, float? patrolZ = null, bool useSpawnAsPatrolStart = false, double? respawnDelaySeconds = null, int healthDamage = 0)
	{
		SourceInstance = sourceInstance;
		ContentSection = "CapturedPopulation";
		TemplateHash = templateHash;
		Name = name;
		MonsterData = monsterData;
		Level = level;
		Health = health;
		HealthDamage = healthDamage;
		MonsterScale = monsterScale;
		HeadMesh = headMesh;
		RunSpeed = runSpeed;
		NpcFamily = npcFamily;
		CharacterFlags = characterFlags;
		Breed = breed;
		Sex = sex;
		X = x;
		Y = y;
		Z = z;
		PatrolX = patrolX;
		PatrolY = patrolY;
		PatrolZ = patrolZ;
		UseSpawnAsPatrolStart = useSpawnAsPatrolStart;
		RespawnDelaySeconds = respawnDelaySeconds;
		Combat = CapturedSubwayCombatCatalog.For(name, monsterData, level);
	}
}
