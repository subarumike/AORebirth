using System.Collections.Generic;

namespace ZoneEngine.Core;

public sealed class WeaponDamageCandidateEvaluation
{
	public string FormulaName { get; set; }

	public bool Evaluable { get; set; }

	public int? PredictedDamage { get; set; }

	public int? DifferenceFromObservation { get; set; }

	public bool ExactMatch { get; set; }

	public bool MultipleCandidatesAlsoMatched { get; set; }

	public IList<WeaponDamageCandidateStage> Stages { get; private set; }

	public IList<string> Assumptions { get; private set; }

	public IList<string> UnknownInputs { get; private set; }

	public WeaponDamageCandidateEvaluation()
	{
		FormulaName = string.Empty;
		Stages = new List<WeaponDamageCandidateStage>();
		Assumptions = new List<string>();
		UnknownInputs = new List<string>();
	}
}
