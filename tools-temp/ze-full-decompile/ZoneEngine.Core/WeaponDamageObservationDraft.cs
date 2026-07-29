namespace ZoneEngine.Core;

public sealed class WeaponDamageObservationDraft
{
	public WeaponDamageObservationSource Source { get; set; }

	public WeaponDamageObservationInput Input { get; set; }

	public WeaponDamageObservationResult Result { get; set; }

	public WeaponDamageObservationDraft()
	{
		Source = new WeaponDamageObservationSource();
		Input = new WeaponDamageObservationInput();
		Result = new WeaponDamageObservationResult();
	}
}
