namespace AORebirth.Core.Playfields;

internal sealed class CapturedSubwayCombatEvidenceDefinition
{
	public bool Observed { get; private set; }

	public bool RuntimeReady { get; private set; }

	public int MinDamage { get; private set; }

	public int MaxDamage { get; private set; }

	public double RechargeSeconds { get; private set; }

	public int WeaponSlot { get; private set; }

	public int AttackInfoUnknown { get; private set; }

	public int WeaponInstance { get; private set; }

	public int ObservedRows { get; private set; }

	public CapturedSubwayCombatEvidenceDefinition(bool observed, int minDamage, int maxDamage, double rechargeSeconds, int weaponSlot, int attackInfoUnknown, int weaponInstance, int observedRows)
		: this(observed, observed, minDamage, maxDamage, rechargeSeconds, weaponSlot, attackInfoUnknown, weaponInstance, observedRows)
	{
	}

	public CapturedSubwayCombatEvidenceDefinition(bool observed, bool runtimeReady, int minDamage, int maxDamage, double rechargeSeconds, int weaponSlot, int attackInfoUnknown, int weaponInstance, int observedRows)
	{
		Observed = observed;
		RuntimeReady = runtimeReady;
		MinDamage = minDamage;
		MaxDamage = maxDamage;
		RechargeSeconds = rechargeSeconds;
		WeaponSlot = weaponSlot;
		AttackInfoUnknown = attackInfoUnknown;
		WeaponInstance = weaponInstance;
		ObservedRows = observedRows;
	}
}
