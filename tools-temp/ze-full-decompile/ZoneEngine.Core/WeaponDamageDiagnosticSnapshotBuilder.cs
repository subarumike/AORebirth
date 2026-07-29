using System.Collections.Generic;

namespace ZoneEngine.Core;

public static class WeaponDamageDiagnosticSnapshotBuilder
{
	public static WeaponDamageDiagnosticSnapshot Build(bool enabled, WeaponDamageRequestBuildResult requestBuilderResult, DamageCalculationResult productionResult, WeaponDamageObservation observation, IEnumerable<WeaponDamageCandidateFormula> formulas)
	{
		if (!enabled)
		{
			return null;
		}
		WeaponDamageDiagnosticSnapshot weaponDamageDiagnosticSnapshot = new WeaponDamageDiagnosticSnapshot
		{
			RequestBuilderResult = requestBuilderResult,
			SelectedStrategy = (productionResult?.Strategy ?? DamageCalculationStrategyKind.LegacyFallback),
			ActualLegacyResult = (productionResult?.FinalTargetDamage ?? 0)
		};
		if (requestBuilderResult != null)
		{
			foreach (WeaponDamageInputIssue issue in requestBuilderResult.Issues)
			{
				weaponDamageDiagnosticSnapshot.MissingInputs.Add(issue.Kind.ToString() + ": " + issue.InputName);
			}
		}
		if (observation != null)
		{
			foreach (WeaponDamageCandidateEvaluation item in WeaponDamageCandidateEvaluator.EvaluateAll(observation, formulas))
			{
				weaponDamageDiagnosticSnapshot.CandidateEvaluations.Add(item);
			}
		}
		return weaponDamageDiagnosticSnapshot;
	}
}
