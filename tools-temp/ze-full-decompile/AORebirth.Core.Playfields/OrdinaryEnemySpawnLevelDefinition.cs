using System;
using System.Linq;

namespace AORebirth.Core.Playfields;

internal sealed class OrdinaryEnemySpawnLevelDefinition
{
	private readonly OrdinaryEnemySpawnVariant[] explicitVariants;

	internal OrdinaryEnemySpawnLevelMode Mode { get; private set; }

	internal int MinimumLevel { get; private set; }

	internal int MaximumLevel { get; private set; }

	internal int ReferenceLevel { get; private set; }

	internal int ReferenceHealth { get; private set; }

	internal int HealthPerLevel { get; private set; }

	internal int HealthDamage { get; private set; }

	internal int MonsterScale { get; private set; }

	internal int ReferenceRunSpeed { get; private set; }

	internal int RunSpeedPerLevel { get; private set; }

	internal OrdinaryEnemyLevelRerollPolicy RerollPolicy { get; private set; }

	internal OrdinaryEnemyEvidenceState EvidenceState { get; private set; }

	internal string Evidence { get; private set; }

	internal bool IsValid
	{
		get
		{
			if (Mode == OrdinaryEnemySpawnLevelMode.ExplicitObservedVariants)
			{
				if (explicitVariants.Length == 0 || RerollPolicy != OrdinaryEnemyLevelRerollPolicy.NewPopulationGeneration || EvidenceState != OrdinaryEnemyEvidenceState.Policy || string.IsNullOrWhiteSpace(Evidence) || explicitVariants.Any((OrdinaryEnemySpawnVariant value) => value == null || !value.IsValid) || MinimumLevel != explicitVariants.Min((OrdinaryEnemySpawnVariant value) => value.Level) || MaximumLevel != explicitVariants.Max((OrdinaryEnemySpawnVariant value) => value.Level))
				{
					return false;
				}
				bool hasWeaponLoadout = explicitVariants[0].WeaponLoadout != null;
				if (explicitVariants.Any((OrdinaryEnemySpawnVariant value) => value.WeaponLoadout != null != hasWeaponLoadout))
				{
					return false;
				}
				return explicitVariants.Select(VariantSignature).Distinct(StringComparer.Ordinal).Count() == explicitVariants.Length;
			}
			if ((Mode != OrdinaryEnemySpawnLevelMode.Fixed && Mode != OrdinaryEnemySpawnLevelMode.InclusiveRange) || MinimumLevel <= 0 || MaximumLevel < MinimumLevel || ReferenceLevel < MinimumLevel || ReferenceLevel > MaximumLevel || ReferenceHealth <= 0 || HealthPerLevel < 0 || HealthDamage < 0 || MonsterScale <= 0 || ReferenceRunSpeed <= 0 || RunSpeedPerLevel < 0 || (RerollPolicy != OrdinaryEnemyLevelRerollPolicy.Never && RerollPolicy != OrdinaryEnemyLevelRerollPolicy.NewPopulationGeneration) || (EvidenceState != OrdinaryEnemyEvidenceState.Observed && EvidenceState != OrdinaryEnemyEvidenceState.Policy) || string.IsNullOrWhiteSpace(Evidence))
			{
				return false;
			}
			if (explicitVariants.Length != 0)
			{
				return false;
			}
			if (Mode == OrdinaryEnemySpawnLevelMode.Fixed && (MinimumLevel != MaximumLevel || ReferenceLevel != MinimumLevel || HealthPerLevel != 0 || RunSpeedPerLevel != 0 || RerollPolicy != OrdinaryEnemyLevelRerollPolicy.Never))
			{
				return false;
			}
			if (Mode == OrdinaryEnemySpawnLevelMode.InclusiveRange && MaximumLevel == MinimumLevel)
			{
				return false;
			}
			long num = HealthAt(MinimumLevel);
			long num2 = HealthAt(MaximumLevel);
			long num3 = RunSpeedAt(MinimumLevel);
			long num4 = RunSpeedAt(MaximumLevel);
			return num > 0 && num <= int.MaxValue && HealthDamage < num && num2 > 0 && num2 <= int.MaxValue && HealthDamage < num2 && num3 > 0 && num3 <= int.MaxValue && num4 > 0 && num4 <= int.MaxValue;
		}
	}

	internal OrdinaryEnemySpawnLevelDefinition(OrdinaryEnemySpawnLevelMode mode, int minimumLevel, int maximumLevel, int referenceLevel, int referenceHealth, int healthPerLevel, int healthDamage, int monsterScale, int referenceRunSpeed, int runSpeedPerLevel, OrdinaryEnemyLevelRerollPolicy rerollPolicy, OrdinaryEnemyEvidenceState evidenceState, string evidence, OrdinaryEnemySpawnVariant[] explicitVariants = null)
	{
		Mode = mode;
		MinimumLevel = minimumLevel;
		MaximumLevel = maximumLevel;
		ReferenceLevel = referenceLevel;
		ReferenceHealth = referenceHealth;
		HealthPerLevel = healthPerLevel;
		HealthDamage = healthDamage;
		MonsterScale = monsterScale;
		ReferenceRunSpeed = referenceRunSpeed;
		RunSpeedPerLevel = runSpeedPerLevel;
		RerollPolicy = rerollPolicy;
		EvidenceState = evidenceState;
		Evidence = evidence ?? string.Empty;
		this.explicitVariants = ((explicitVariants == null) ? new OrdinaryEnemySpawnVariant[0] : ((OrdinaryEnemySpawnVariant[])explicitVariants.Clone()));
	}

	internal static OrdinaryEnemySpawnLevelDefinition Fixed(OrdinaryEnemySpawnVariant variant, OrdinaryEnemyEvidenceState evidenceState, string evidence)
	{
		if (variant == null)
		{
			throw new ArgumentNullException("variant");
		}
		return new OrdinaryEnemySpawnLevelDefinition(OrdinaryEnemySpawnLevelMode.Fixed, variant.Level, variant.Level, variant.Level, variant.Health, 0, variant.HealthDamage, variant.MonsterScale, variant.RunSpeed, 0, OrdinaryEnemyLevelRerollPolicy.Never, evidenceState, evidence);
	}

	internal static OrdinaryEnemySpawnLevelDefinition ExplicitObservedVariants(OrdinaryEnemySpawnVariant[] variants, string evidence)
	{
		if (variants == null || variants.Length == 0)
		{
			throw new ArgumentException("At least one explicit observed ordinary enemy variant is required.", "variants");
		}
		OrdinaryEnemySpawnVariant ordinaryEnemySpawnVariant = variants[0];
		if (ordinaryEnemySpawnVariant == null)
		{
			throw new ArgumentException("Explicit observed ordinary enemy variants cannot contain null.", "variants");
		}
		return new OrdinaryEnemySpawnLevelDefinition(OrdinaryEnemySpawnLevelMode.ExplicitObservedVariants, variants.Min((OrdinaryEnemySpawnVariant value) => value?.Level ?? int.MaxValue), variants.Max((OrdinaryEnemySpawnVariant value) => value?.Level ?? int.MinValue), ordinaryEnemySpawnVariant.Level, ordinaryEnemySpawnVariant.Health, 0, ordinaryEnemySpawnVariant.HealthDamage, ordinaryEnemySpawnVariant.MonsterScale, ordinaryEnemySpawnVariant.RunSpeed, 0, OrdinaryEnemyLevelRerollPolicy.NewPopulationGeneration, OrdinaryEnemyEvidenceState.Policy, evidence, variants);
	}

	internal OrdinaryEnemySpawnVariant SelectVariant(Func<int, int> nextRandom)
	{
		if (!IsValid)
		{
			throw new InvalidOperationException("Ordinary enemy spawn level definition is invalid.");
		}
		if (Mode == OrdinaryEnemySpawnLevelMode.Fixed)
		{
			return Resolve(MinimumLevel);
		}
		if (Mode == OrdinaryEnemySpawnLevelMode.ExplicitObservedVariants)
		{
			if (nextRandom == null)
			{
				throw new ArgumentNullException("nextRandom");
			}
			int num = nextRandom(explicitVariants.Length);
			if (num < 0 || num >= explicitVariants.Length)
			{
				throw new ArgumentOutOfRangeException("nextRandom");
			}
			return explicitVariants[num];
		}
		if (nextRandom == null)
		{
			throw new ArgumentNullException("nextRandom");
		}
		int num2 = MaximumLevel - MinimumLevel + 1;
		int num3 = nextRandom(num2);
		if (num3 < 0 || num3 >= num2)
		{
			throw new ArgumentOutOfRangeException("nextRandom");
		}
		return Resolve(MinimumLevel + num3);
	}

	internal OrdinaryEnemySpawnVariant Resolve(int level)
	{
		if (!IsValid)
		{
			throw new InvalidOperationException("Ordinary enemy spawn level definition is invalid.");
		}
		if (level < MinimumLevel || level > MaximumLevel)
		{
			throw new ArgumentOutOfRangeException("level");
		}
		if (Mode == OrdinaryEnemySpawnLevelMode.ExplicitObservedVariants)
		{
			OrdinaryEnemySpawnVariant[] array = explicitVariants.Where((OrdinaryEnemySpawnVariant value) => value.Level == level).ToArray();
			if (array.Length != 1)
			{
				throw new InvalidOperationException("An explicit observed level must resolve to exactly one atomic variant.");
			}
			return array[0];
		}
		return new OrdinaryEnemySpawnVariant(level, (int)HealthAt(level), HealthDamage, MonsterScale, (int)RunSpeedAt(level), Evidence);
	}

	internal OrdinaryEnemySpawnVariant[] GetExplicitVariants()
	{
		return (OrdinaryEnemySpawnVariant[])explicitVariants.Clone();
	}

	internal bool ContainsSourceRow(int level, int health, int healthDamage, int monsterScale, int runSpeed)
	{
		if (!IsValid)
		{
			return false;
		}
		if (Mode == OrdinaryEnemySpawnLevelMode.ExplicitObservedVariants)
		{
			return explicitVariants.Any((OrdinaryEnemySpawnVariant value) => value.Level == level && value.Health == health && value.HealthDamage == healthDamage && value.MonsterScale == monsterScale && value.RunSpeed == runSpeed);
		}
		OrdinaryEnemySpawnVariant ordinaryEnemySpawnVariant = ((level >= MinimumLevel && level <= MaximumLevel) ? Resolve(level) : null);
		return ordinaryEnemySpawnVariant != null && ordinaryEnemySpawnVariant.Health == health && ordinaryEnemySpawnVariant.HealthDamage == healthDamage && ordinaryEnemySpawnVariant.MonsterScale == monsterScale && ordinaryEnemySpawnVariant.RunSpeed == runSpeed;
	}

	private static string VariantSignature(OrdinaryEnemySpawnVariant variant)
	{
		OrdinaryEnemySpawnWeaponLoadout weaponLoadout = variant.WeaponLoadout;
		return string.Join(":", variant.Level, variant.Health, variant.HealthDamage, variant.MonsterScale, variant.RunSpeed, weaponLoadout?.LowId ?? 0, weaponLoadout?.HighId ?? 0, weaponLoadout?.Quality ?? 0);
	}

	private long HealthAt(int level)
	{
		return ReferenceHealth + (long)(level - ReferenceLevel) * (long)HealthPerLevel;
	}

	private long RunSpeedAt(int level)
	{
		return ReferenceRunSpeed + (long)(level - ReferenceLevel) * (long)RunSpeedPerLevel;
	}
}
