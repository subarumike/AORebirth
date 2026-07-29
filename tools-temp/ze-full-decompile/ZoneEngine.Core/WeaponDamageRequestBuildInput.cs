namespace ZoneEngine.Core;

public sealed class WeaponDamageRequestBuildInput
{
	public string CallerName { get; set; }

	public bool IsFixedCapturedDamage { get; set; }

	public int FixedCapturedDamage { get; set; }

	public WeaponDamageWeaponSnapshot Weapon { get; set; }

	public WeaponDamageActorSnapshot Attacker { get; set; }

	public WeaponDamageActorSnapshot Target { get; set; }

	public bool HasCriticalState { get; set; }

	public bool IsCritical { get; set; }

	public bool HasUniversalAddDamageSource { get; set; }

	public int UniversalAddDamage { get; set; }

	public WeaponDamageRequestBuildInput()
	{
		CallerName = string.Empty;
		Weapon = new WeaponDamageWeaponSnapshot();
		Attacker = new WeaponDamageActorSnapshot();
		Target = new WeaponDamageActorSnapshot();
	}
}
