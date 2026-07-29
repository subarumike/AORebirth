using System.Collections.Generic;

namespace ZoneEngine.Core;

public sealed class DamageCalculationResult
{
	public DamageCalculationStrategyKind Strategy { get; set; }

	public string StrategyReason { get; set; }

	public DamageHitOutcome HitOutcome { get; set; }

	public bool CriticalOutcome { get; set; }

	public DamageType SelectedDamageType { get; set; }

	public int BaseRoll { get; set; }

	public int EffectiveAttackRating { get; set; }

	public int AttackRatingCapResult { get; set; }

	public int Pre1000AttackRatingContribution { get; set; }

	public int Post1000AttackRatingContribution { get; set; }

	public int FinalAttackRatingMultiplierBasisPoints { get; set; }

	public int ScaledBaseDamage { get; set; }

	public int CriticalContribution { get; set; }

	public int ArmorReduction { get; set; }

	public int MinimumDamageFloor { get; set; }

	public int FlatAddDamageContribution { get; set; }

	public int LegacyDamageBonusContribution { get; set; }

	public int TypeSpecificAddDamageContribution { get; set; }

	public int UniversalAddDamageContribution { get; set; }

	public IList<DamageCalculationResult> SubHitResults { get; private set; }

	public int AggregateSpecialDamage { get; set; }

	public int SpecialCompression { get; set; }

	public int SpecialCap { get; set; }

	public int PvPConversion { get; set; }

	public int PvPHealthCap { get; set; }

	public int ReflectPrevention { get; set; }

	public int ReflectReturn { get; set; }

	public int TypedAbsorbConsumption { get; set; }

	public int UniversalAbsorbConsumption { get; set; }

	public int DamageShieldReturn { get; set; }

	public int FinalTargetDamage { get; set; }

	public int FinalAttackerDamage { get; set; }

	public IList<string> Clamps { get; private set; }

	public DamageEvidenceClassification EvidenceClassification { get; set; }

	public DamageCalculationTrace Trace { get; private set; }

	public DamageCalculationResult()
	{
		SelectedDamageType = DamageType.Unknown;
		HitOutcome = DamageHitOutcome.Hit;
		EvidenceClassification = DamageEvidenceClassification.Unknown;
		Trace = new DamageCalculationTrace();
		SubHitResults = new List<DamageCalculationResult>();
		Clamps = new List<string>();
		Strategy = DamageCalculationStrategyKind.LegacyFallback;
		StrategyReason = string.Empty;
	}
}
