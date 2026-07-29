namespace AORebirth.Core.Playfields;

internal sealed class CapturedSubwayStrictLootProfileDefinition
{
	public string Name { get; private set; }

	public int MonsterData { get; private set; }

	public int ObservedCompleteInventories { get; private set; }

	public int ObservedPositiveInventories { get; private set; }

	public int ObservedEmptyInventories { get; private set; }

	public bool ItemPoolComplete { get; private set; }

	public string[] EvidenceCaptures { get; private set; }

	public CapturedSubwayLootEvidenceDefinition[] Entries { get; private set; }

	public CapturedSubwayStrictLootProfileDefinition(string name, int monsterData, int observedCompleteInventories, int observedPositiveInventories, int observedEmptyInventories, bool itemPoolComplete, string[] evidenceCaptures, CapturedSubwayLootEvidenceDefinition[] entries)
	{
		Name = name;
		MonsterData = monsterData;
		ObservedCompleteInventories = observedCompleteInventories;
		ObservedPositiveInventories = observedPositiveInventories;
		ObservedEmptyInventories = observedEmptyInventories;
		ItemPoolComplete = itemPoolComplete;
		EvidenceCaptures = evidenceCaptures ?? new string[0];
		Entries = entries ?? new CapturedSubwayLootEvidenceDefinition[0];
	}
}
