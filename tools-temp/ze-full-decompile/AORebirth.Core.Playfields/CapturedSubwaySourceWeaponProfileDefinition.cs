namespace AORebirth.Core.Playfields;

internal sealed class CapturedSubwaySourceWeaponProfileDefinition
{
	public string Name { get; private set; }

	public int MonsterData { get; private set; }

	public CapturedSubwaySourceWeaponEvidenceDefinition[] SourceWeaponEvidence { get; private set; }

	public CapturedSubwaySourceWeaponProfileDefinition(string name, int monsterData, CapturedSubwaySourceWeaponEvidenceDefinition[] sourceWeaponEvidence)
	{
		Name = name;
		MonsterData = monsterData;
		SourceWeaponEvidence = sourceWeaponEvidence ?? new CapturedSubwaySourceWeaponEvidenceDefinition[0];
	}
}
