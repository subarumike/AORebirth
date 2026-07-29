using System.Collections.Generic;

namespace ZoneEngine.Core;

public sealed class WeaponDamageParityReport
{
	public IList<string> CandidatesMatchingEveryObservation { get; private set; }

	public IList<string> CandidatesMatchingOnlySubsets { get; private set; }

	public IList<string> UnderdeterminedObservations { get; private set; }

	public IList<string> ContradictoryObservations { get; private set; }

	public IList<string> MissingObservationsNeeded { get; private set; }

	public IList<string> PossibleRoundingBoundaries { get; private set; }

	public IList<string> PossibleHiddenModifiers { get; private set; }

	public bool FormulaProven => CandidatesMatchingEveryObservation.Count == 1 && CandidatesMatchingOnlySubsets.Count == 0 && UnderdeterminedObservations.Count == 0 && ContradictoryObservations.Count == 0 && MissingObservationsNeeded.Count == 0;

	public WeaponDamageParityReport()
	{
		CandidatesMatchingEveryObservation = new List<string>();
		CandidatesMatchingOnlySubsets = new List<string>();
		UnderdeterminedObservations = new List<string>();
		ContradictoryObservations = new List<string>();
		MissingObservationsNeeded = new List<string>();
		PossibleRoundingBoundaries = new List<string>();
		PossibleHiddenModifiers = new List<string>();
	}
}
