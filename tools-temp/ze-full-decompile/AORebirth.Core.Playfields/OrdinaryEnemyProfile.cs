namespace AORebirth.Core.Playfields;

internal sealed class OrdinaryEnemyProfile
{
	internal string ProfileKey { get; private set; }

	internal string FamilyKey { get; private set; }

	internal string DisplayName { get; private set; }

	internal int MonsterData { get; private set; }

	internal OrdinaryEnemyConstructionMode ConstructionMode { get; private set; }

	internal string TemplateHash { get; private set; }

	internal OrdinaryEnemyAppearanceProfile Appearance { get; private set; }

	internal OrdinaryEnemyAggressionProfile Aggression { get; private set; }

	internal OrdinaryEnemyCombatProfile Combat { get; private set; }

	internal OrdinaryEnemyLootProfile Loot { get; private set; }

	internal OrdinaryEnemyCorpseProfile Corpse { get; private set; }

	internal string[] Evidence { get; private set; }

	internal bool BossOrScripted { get; private set; }

	internal bool OwnedSummon { get; private set; }

	internal OrdinaryEnemySupportNanoProfile SupportNano { get; private set; }

	internal OrdinaryEnemyProfile(string profileKey, string familyKey, string displayName, int monsterData, OrdinaryEnemyConstructionMode constructionMode, string templateHash, OrdinaryEnemyAppearanceProfile appearance, OrdinaryEnemyAggressionProfile aggression, OrdinaryEnemyCombatProfile combat, OrdinaryEnemyLootProfile loot, OrdinaryEnemyCorpseProfile corpse, string[] evidence, bool bossOrScripted, bool ownedSummon, OrdinaryEnemySupportNanoProfile supportNano = null)
	{
		ProfileKey = profileKey;
		FamilyKey = familyKey;
		DisplayName = displayName;
		MonsterData = monsterData;
		ConstructionMode = constructionMode;
		TemplateHash = templateHash;
		Appearance = appearance;
		Aggression = aggression;
		Combat = combat;
		Loot = loot;
		Corpse = corpse;
		Evidence = evidence ?? new string[0];
		BossOrScripted = bossOrScripted;
		OwnedSummon = ownedSummon;
		SupportNano = supportNano;
	}
}
