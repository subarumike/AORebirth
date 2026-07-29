using System.Collections.Generic;
using System.Linq;

namespace ZoneEngine.Core;

public static class WeaponDamageParityReporter
{
	private static readonly string[] RequiredObservationTags = new string[13]
	{
		"base-roll-variation", "attack-rating-variation", "target-ac-variation", "minimum-floor-boundary", "critical-versus-normal", "type-specific-add-damage", "universal-add-damage", "amscap-boundary", "single-skill-weapon", "multi-skill-weapon",
		"ar-below-1000", "ar-exactly-1000", "ar-above-1000"
	};

	public static WeaponDamageParityReport Generate(WeaponDamageEvidenceSet evidenceSet)
	{
		WeaponDamageParityReport weaponDamageParityReport = new WeaponDamageParityReport();
		if (evidenceSet == null)
		{
			string[] requiredObservationTags = RequiredObservationTags;
			foreach (string item in requiredObservationTags)
			{
				weaponDamageParityReport.MissingObservationsNeeded.Add(item);
			}
			return weaponDamageParityReport;
		}
		IList<WeaponDamageObservation> list = evidenceSet.Observations.Where((WeaponDamageObservation x) => x.ValidationStatus == WeaponDamageObservationValidationStatus.Complete).ToList();
		foreach (WeaponDamageCandidateFormula formula in evidenceSet.CandidateFormulas)
		{
			int num = list.Count((WeaponDamageObservation x) => WeaponDamageCandidateEvaluator.Evaluate(x, formula).ExactMatch);
			if (list.Count > 0 && num == list.Count)
			{
				weaponDamageParityReport.CandidatesMatchingEveryObservation.Add(formula.Name);
			}
			else if (num > 0)
			{
				weaponDamageParityReport.CandidatesMatchingOnlySubsets.Add(formula.Name);
			}
		}
		foreach (WeaponDamageObservation item2 in list)
		{
			IList<WeaponDamageCandidateEvaluation> source = WeaponDamageCandidateEvaluator.EvaluateAll(item2, evidenceSet.CandidateFormulas);
			int num2 = source.Count((WeaponDamageCandidateEvaluation x) => x.ExactMatch);
			if (num2 == 0)
			{
				weaponDamageParityReport.ContradictoryObservations.Add(item2.Source.ObservationId);
				weaponDamageParityReport.PossibleHiddenModifiers.Add(item2.Source.ObservationId);
			}
			else if (num2 > 1)
			{
				weaponDamageParityReport.UnderdeterminedObservations.Add(item2.Source.ObservationId);
			}
			if (source.Any((WeaponDamageCandidateEvaluation x) => x.Stages.Any((WeaponDamageCandidateStage y) => y.Assumption.Contains("truncate"))))
			{
				weaponDamageParityReport.PossibleRoundingBoundaries.Add(item2.Source.ObservationId);
			}
		}
		string[] requiredObservationTags2 = RequiredObservationTags;
		foreach (string tag in requiredObservationTags2)
		{
			if (!evidenceSet.Observations.Any((WeaponDamageObservation x) => x.Input.KnownUncertainties.Contains(tag)))
			{
				weaponDamageParityReport.MissingObservationsNeeded.Add(tag);
			}
		}
		return weaponDamageParityReport;
	}
}
