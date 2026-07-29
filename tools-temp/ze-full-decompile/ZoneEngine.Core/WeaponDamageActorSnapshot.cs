using System.Collections.Generic;

namespace ZoneEngine.Core;

public sealed class WeaponDamageActorSnapshot
{
	public string Identity { get; set; }

	public DamageSourceCategory Category { get; set; }

	public WeaponDamageAttackerReadiness Readiness { get; set; }

	public IList<WeaponDamageStatSnapshot> Stats { get; private set; }

	public WeaponDamageActorSnapshot()
	{
		Identity = string.Empty;
		Stats = new List<WeaponDamageStatSnapshot>();
		Category = DamageSourceCategory.Unknown;
		Readiness = WeaponDamageAttackerReadiness.PartialStatProvenance;
	}
}
