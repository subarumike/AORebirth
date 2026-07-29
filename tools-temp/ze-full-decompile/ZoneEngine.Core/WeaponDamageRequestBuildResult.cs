using System.Collections.Generic;
using System.Linq;

namespace ZoneEngine.Core;

public sealed class WeaponDamageRequestBuildResult
{
	public WeaponDamageRequestBuildClassification Classification { get; set; }

	public DamageCalculationStrategyKind ExpectedActiveStrategy { get; set; }

	public DamageCalculationRequest Request { get; set; }

	public IList<WeaponDamageInputProvenance> Provenance { get; private set; }

	public IList<WeaponDamageInputIssue> Issues { get; private set; }

	public WeaponDamageRequestBuildResult()
	{
		Provenance = new List<WeaponDamageInputProvenance>();
		Issues = new List<WeaponDamageInputIssue>();
		Request = new DamageCalculationRequest();
		ExpectedActiveStrategy = DamageCalculationStrategyKind.LegacyFallback;
	}

	public bool HasIssue(WeaponDamageInputIssueKind kind)
	{
		return Issues.Any((WeaponDamageInputIssue x) => x.Kind == kind);
	}
}
