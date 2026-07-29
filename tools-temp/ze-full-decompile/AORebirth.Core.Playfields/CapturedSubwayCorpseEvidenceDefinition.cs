namespace AORebirth.Core.Playfields;

internal sealed class CapturedSubwayCorpseEvidenceDefinition
{
	public string Capture { get; private set; }

	public string CapturedUtc { get; private set; }

	public string CorpseIdentity { get; private set; }

	public string DeadNpcIdentity { get; private set; }

	public int EnemyLevel { get; private set; }

	public int MonsterData { get; private set; }

	public int CatMesh { get; private set; }

	public int Credits { get; private set; }

	public CapturedSubwayCorpseEvidenceDefinition(string capture, string capturedUtc, string corpseIdentity, string deadNpcIdentity, int enemyLevel, int monsterData, int catMesh, int credits)
	{
		Capture = capture;
		CapturedUtc = capturedUtc;
		CorpseIdentity = corpseIdentity;
		DeadNpcIdentity = deadNpcIdentity;
		EnemyLevel = enemyLevel;
		MonsterData = monsterData;
		CatMesh = catMesh;
		Credits = credits;
	}
}
