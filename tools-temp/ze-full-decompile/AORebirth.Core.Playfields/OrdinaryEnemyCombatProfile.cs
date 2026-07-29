using System;

namespace AORebirth.Core.Playfields;

internal sealed class OrdinaryEnemyCombatProfile
{
	private readonly Func<int, CapturedEnemyCombatContract> contractResolver;

	private readonly Func<int, int, CapturedEnemyCombatContract> sourceContractResolver;

	private readonly Func<int, OrdinaryEnemySpawnVariant, CapturedEnemyCombatContract> sourceVariantContractResolver;

	internal OrdinaryEnemyCombatMode Mode { get; private set; }

	internal OrdinaryEnemyDamageSource DamageSource { get; private set; }

	internal bool VisibleWeapon { get; private set; }

	internal CapturedEnemyCombatContract Contract { get; private set; }

	internal OrdinaryEnemyEvidenceState EvidenceState { get; private set; }

	internal double? HealthRegenIntervalSeconds { get; private set; }

	internal int? HealthRegenDelta { get; private set; }

	internal bool RegenerateHealthWhileInCombat { get; private set; }

	internal OrdinaryEnemyCombatProfile(OrdinaryEnemyCombatMode mode, OrdinaryEnemyDamageSource damageSource, bool visibleWeapon, CapturedEnemyCombatContract contract, OrdinaryEnemyEvidenceState evidenceState, double? healthRegenIntervalSeconds = null, int? healthRegenDelta = null, bool regenerateHealthWhileInCombat = false, Func<int, CapturedEnemyCombatContract> contractResolver = null, Func<int, int, CapturedEnemyCombatContract> sourceContractResolver = null, Func<int, OrdinaryEnemySpawnVariant, CapturedEnemyCombatContract> sourceVariantContractResolver = null)
	{
		Mode = mode;
		DamageSource = damageSource;
		VisibleWeapon = visibleWeapon;
		Contract = contract;
		EvidenceState = evidenceState;
		HealthRegenIntervalSeconds = healthRegenIntervalSeconds;
		HealthRegenDelta = healthRegenDelta;
		RegenerateHealthWhileInCombat = regenerateHealthWhileInCombat;
		this.contractResolver = contractResolver;
		this.sourceContractResolver = sourceContractResolver;
		this.sourceVariantContractResolver = sourceVariantContractResolver;
	}

	internal CapturedEnemyCombatContract ResolveContract(int level)
	{
		return (contractResolver == null) ? Contract : contractResolver(level);
	}

	internal CapturedEnemyCombatContract ResolveContract(int sourceIdentity, int level)
	{
		return (sourceContractResolver == null) ? ResolveContract(level) : sourceContractResolver(sourceIdentity, level);
	}

	internal CapturedEnemyCombatContract ResolveContract(int sourceIdentity, OrdinaryEnemySpawnVariant variant)
	{
		if (variant == null)
		{
			throw new ArgumentNullException("variant");
		}
		return (sourceVariantContractResolver == null) ? ResolveContract(sourceIdentity, variant.Level) : sourceVariantContractResolver(sourceIdentity, variant);
	}
}
