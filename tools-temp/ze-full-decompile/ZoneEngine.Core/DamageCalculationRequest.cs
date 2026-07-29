namespace ZoneEngine.Core;

public sealed class DamageCalculationRequest
{
	public DamageCalculationContext Context { get; set; }

	public DamageSourceSnapshot Source { get; set; }

	public DamageTargetSnapshot Target { get; set; }

	public DamageDefinition Definition { get; set; }

	public DamageModifierSet Modifiers { get; set; }

	public DamageMitigationSet Mitigation { get; set; }

	public DamageCalculationPolicy Policy { get; set; }

	public DamageEvidenceClassification EvidenceClassification { get; set; }

	public DamageHitOutcome HitOutcome { get; set; }

	public DamageType DamageTypeOverride { get; set; }

	public DamageCalculationRequest()
	{
		Context = new DamageCalculationContext();
		Source = new DamageSourceSnapshot();
		Target = new DamageTargetSnapshot();
		Definition = new DamageDefinition();
		Modifiers = new DamageModifierSet();
		Mitigation = new DamageMitigationSet();
		Policy = DamageCalculationPolicy.RepositoryLegacyNormalHit(isPlayer: false);
		EvidenceClassification = DamageEvidenceClassification.Unknown;
		HitOutcome = DamageHitOutcome.Hit;
	}
}
