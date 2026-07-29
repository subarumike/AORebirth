namespace ZoneEngine.Core;

internal sealed class CapturedBureaucratPetProfile
{
	public string Name { get; private set; }

	public int Level { get; private set; }

	public int Health { get; private set; }

	public int MonsterData { get; private set; }

	public int MonsterScale { get; private set; }

	public int RunSpeed { get; private set; }

	public int HeadMesh { get; private set; }

	public int NpcFamily { get; private set; }

	public CapturedBureaucratPetProfile(string name, int level, int health, int monsterData, int monsterScale, int runSpeed, int headMesh = 0, int npcFamily = 95)
	{
		Name = name;
		Level = level;
		Health = health;
		MonsterData = monsterData;
		MonsterScale = monsterScale;
		RunSpeed = runSpeed;
		HeadMesh = headMesh;
		NpcFamily = npcFamily;
	}
}
