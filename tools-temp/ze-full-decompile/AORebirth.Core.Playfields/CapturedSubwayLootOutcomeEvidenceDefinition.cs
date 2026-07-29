namespace AORebirth.Core.Playfields;

internal sealed class CapturedSubwayLootOutcomeEvidenceDefinition
{
	public string Capture { get; private set; }

	public string CapturedUtc { get; private set; }

	public string CorpseIdentity { get; private set; }

	public string DeadNpcIdentity { get; private set; }

	public int MonsterData { get; private set; }

	public int Sequence { get; private set; }

	public int Slot { get; private set; }

	public int LowId { get; private set; }

	public int HighId { get; private set; }

	public int Quality { get; private set; }

	public CapturedSubwayLootOutcomeEvidenceDefinition(string capture, string capturedUtc, string corpseIdentity, string deadNpcIdentity, int monsterData, int sequence, int slot, int lowId, int highId, int quality)
	{
		Capture = capture;
		CapturedUtc = capturedUtc;
		CorpseIdentity = corpseIdentity;
		DeadNpcIdentity = deadNpcIdentity;
		MonsterData = monsterData;
		Sequence = sequence;
		Slot = slot;
		LowId = lowId;
		HighId = highId;
		Quality = quality;
	}
}
