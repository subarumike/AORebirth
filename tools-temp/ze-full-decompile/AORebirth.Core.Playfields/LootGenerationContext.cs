namespace AORebirth.Core.Playfields;

internal sealed class LootGenerationContext
{
	internal string EnemyProfileKey { get; set; }

	internal int EnemyIdentityInstance { get; set; }

	internal int MonsterData { get; set; }

	internal string FamilyKey { get; set; }

	internal int Level { get; set; }

	internal int PlayfieldId { get; set; }

	internal string SpawnKey { get; set; }

	internal string EncounterKey { get; set; }

	internal bool IsBoss { get; set; }

	internal bool IsDyna { get; set; }

	internal bool IsOwnedSummon { get; set; }

	internal string DynaLevelBandKey { get; set; }

	internal string DynaFamilyKey { get; set; }

	internal string EventKey { get; set; }

	internal int Seed { get; set; }
}
