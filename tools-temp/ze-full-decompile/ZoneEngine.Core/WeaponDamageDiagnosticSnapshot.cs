using System.Collections.Generic;

namespace ZoneEngine.Core;

public sealed class WeaponDamageDiagnosticSnapshot
{
	public WeaponDamageRequestBuildResult RequestBuilderResult { get; set; }

	public DamageCalculationStrategyKind SelectedStrategy { get; set; }

	public int ActualLegacyResult { get; set; }

	public IList<WeaponDamageCandidateEvaluation> CandidateEvaluations { get; private set; }

	public IList<string> MissingInputs { get; private set; }

	public WeaponDamageDiagnosticSnapshot()
	{
		CandidateEvaluations = new List<WeaponDamageCandidateEvaluation>();
		MissingInputs = new List<string>();
	}
}
