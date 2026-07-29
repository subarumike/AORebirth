using System.Collections.Generic;

namespace ZoneEngine.Core;

public sealed class WeaponDamageEvidenceSet
{
	public IList<WeaponDamageObservation> Observations { get; private set; }

	public IList<WeaponDamageCandidateFormula> CandidateFormulas { get; private set; }

	public WeaponDamageEvidenceSet()
	{
		Observations = new List<WeaponDamageObservation>();
		CandidateFormulas = new List<WeaponDamageCandidateFormula>();
	}
}
