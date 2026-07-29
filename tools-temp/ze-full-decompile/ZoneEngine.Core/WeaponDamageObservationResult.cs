namespace ZoneEngine.Core;

public sealed class WeaponDamageObservationResult
{
	public WeaponDamageHitKind HitKind { get; set; }

	public int? ObservedDamage { get; set; }

	public int? TargetHealthBefore { get; set; }

	public int? TargetHealthAfter { get; set; }

	public WeaponDamageObservationResult()
	{
		HitKind = WeaponDamageHitKind.UnknownHitKind;
	}
}
