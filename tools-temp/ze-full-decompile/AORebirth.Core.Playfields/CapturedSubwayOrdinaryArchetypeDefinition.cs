namespace AORebirth.Core.Playfields;

internal sealed class CapturedSubwayOrdinaryArchetypeDefinition
{
	public string Key { get; private set; }

	public string FamilyKey { get; private set; }

	public string Name { get; private set; }

	public int MonsterData { get; private set; }

	public int NpcFamily { get; private set; }

	public int NpcLosHeight { get; private set; }

	public int CharacterFlags { get; private set; }

	public int AccountFlags { get; private set; }

	public int Expansions { get; private set; }

	public int VisualFlags { get; private set; }

	public int VisibleTitle { get; private set; }

	public uint AppearanceValue { get; private set; }

	public int HeadMesh { get; private set; }

	public CapturedSubwayTextureDefinition[] Textures { get; private set; }

	public CapturedSubwayMeshDefinition[] Meshes { get; private set; }

	public CapturedSubwayCombatEvidenceDefinition Combat { get; private set; }

	public CapturedSubwayLootEvidenceDefinition[] LootEvidence { get; private set; }

	public CapturedSubwayLootOutcomeEvidenceDefinition[] LootOutcomeEvidence { get; private set; }

	public CapturedSubwayCorpseEvidenceDefinition[] CorpseEvidence { get; private set; }

	public string[] EvidenceCaptures { get; private set; }

	public CapturedSubwaySourceWeaponEvidenceDefinition[] SourceWeaponEvidence { get; private set; }

	public CapturedSubwayOrdinaryArchetypeDefinition(string key, string familyKey, string name, int monsterData, int npcFamily, int npcLosHeight, int characterFlags, int accountFlags, int expansions, int visualFlags, int visibleTitle, uint appearanceValue, int headMesh, CapturedSubwayTextureDefinition[] textures, CapturedSubwayMeshDefinition[] meshes, CapturedSubwayCombatEvidenceDefinition combat, CapturedSubwayLootEvidenceDefinition[] lootEvidence, CapturedSubwayLootOutcomeEvidenceDefinition[] lootOutcomeEvidence, CapturedSubwayCorpseEvidenceDefinition[] corpseEvidence, string[] evidenceCaptures, CapturedSubwaySourceWeaponEvidenceDefinition[] sourceWeaponEvidence = null)
	{
		Key = key;
		FamilyKey = familyKey;
		Name = name;
		MonsterData = monsterData;
		NpcFamily = npcFamily;
		NpcLosHeight = npcLosHeight;
		CharacterFlags = characterFlags;
		AccountFlags = accountFlags;
		Expansions = expansions;
		VisualFlags = visualFlags;
		VisibleTitle = visibleTitle;
		AppearanceValue = appearanceValue;
		HeadMesh = headMesh;
		Textures = textures ?? new CapturedSubwayTextureDefinition[0];
		Meshes = meshes ?? new CapturedSubwayMeshDefinition[0];
		Combat = combat;
		LootEvidence = lootEvidence ?? new CapturedSubwayLootEvidenceDefinition[0];
		LootOutcomeEvidence = lootOutcomeEvidence ?? new CapturedSubwayLootOutcomeEvidenceDefinition[0];
		CorpseEvidence = corpseEvidence ?? new CapturedSubwayCorpseEvidenceDefinition[0];
		EvidenceCaptures = evidenceCaptures ?? new string[0];
		SourceWeaponEvidence = sourceWeaponEvidence ?? new CapturedSubwaySourceWeaponEvidenceDefinition[0];
	}
}
