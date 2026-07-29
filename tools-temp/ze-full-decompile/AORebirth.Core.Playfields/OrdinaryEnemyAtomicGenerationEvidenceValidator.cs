using System;
using System.Collections.Generic;

namespace AORebirth.Core.Playfields;

internal static class OrdinaryEnemyAtomicGenerationEvidenceValidator
{
	internal static bool TryValidateSelectedVariant(int expectedMonsterData, int expectedSourceInstance, OrdinaryEnemySpawnVariant variant, CapturedSubwayGenerationVariantDefinition[] generationEvidence, out string failure)
	{
		failure = string.Empty;
		OrdinaryEnemySpawnWeaponLoadout ordinaryEnemySpawnWeaponLoadout = variant?.WeaponLoadout;
		if (expectedMonsterData <= 0 || expectedSourceInstance <= 0 || variant == null || !variant.IsValid || ordinaryEnemySpawnWeaponLoadout == null || !ordinaryEnemySpawnWeaponLoadout.IsValid || generationEvidence == null || generationEvidence.Length == 0)
		{
			failure = "Atomic generation selection or evidence is incomplete.";
			return false;
		}
		HashSet<string> hashSet = new HashSet<string>(StringComparer.Ordinal);
		int num = 0;
		foreach (CapturedSubwayGenerationVariantDefinition capturedSubwayGenerationVariantDefinition in generationEvidence)
		{
			if (capturedSubwayGenerationVariantDefinition == null || capturedSubwayGenerationVariantDefinition.MonsterData != expectedMonsterData || capturedSubwayGenerationVariantDefinition.SourceInstance != expectedSourceInstance || capturedSubwayGenerationVariantDefinition.Level <= 0 || capturedSubwayGenerationVariantDefinition.Health <= 0 || capturedSubwayGenerationVariantDefinition.HealthDamage < 0 || capturedSubwayGenerationVariantDefinition.HealthDamage >= capturedSubwayGenerationVariantDefinition.Health || capturedSubwayGenerationVariantDefinition.MonsterScale <= 0 || capturedSubwayGenerationVariantDefinition.RunSpeed <= 0 || capturedSubwayGenerationVariantDefinition.WeaponLowId <= 0 || capturedSubwayGenerationVariantDefinition.WeaponHighId <= 0 || capturedSubwayGenerationVariantDefinition.WeaponQuality <= 0 || string.IsNullOrWhiteSpace(capturedSubwayGenerationVariantDefinition.Evidence))
			{
				failure = "Atomic generation evidence contains an invalid or cross-source row.";
				return false;
			}
			string item = string.Join(":", capturedSubwayGenerationVariantDefinition.Level, capturedSubwayGenerationVariantDefinition.Health, capturedSubwayGenerationVariantDefinition.HealthDamage, capturedSubwayGenerationVariantDefinition.MonsterScale, capturedSubwayGenerationVariantDefinition.RunSpeed, capturedSubwayGenerationVariantDefinition.WeaponLowId, capturedSubwayGenerationVariantDefinition.WeaponHighId, capturedSubwayGenerationVariantDefinition.WeaponQuality);
			if (!hashSet.Add(item))
			{
				failure = "Atomic generation evidence contains a duplicate tuple.";
				return false;
			}
			if (capturedSubwayGenerationVariantDefinition.Level == variant.Level && capturedSubwayGenerationVariantDefinition.Health == variant.Health && capturedSubwayGenerationVariantDefinition.HealthDamage == variant.HealthDamage && capturedSubwayGenerationVariantDefinition.MonsterScale == variant.MonsterScale && capturedSubwayGenerationVariantDefinition.RunSpeed == variant.RunSpeed && capturedSubwayGenerationVariantDefinition.WeaponLowId == ordinaryEnemySpawnWeaponLoadout.LowId && capturedSubwayGenerationVariantDefinition.WeaponHighId == ordinaryEnemySpawnWeaponLoadout.HighId && capturedSubwayGenerationVariantDefinition.WeaponQuality == ordinaryEnemySpawnWeaponLoadout.Quality)
			{
				num++;
			}
		}
		if (num != 1)
		{
			failure = "Selected atomic generation is missing or conflicting.";
			return false;
		}
		return true;
	}
}
