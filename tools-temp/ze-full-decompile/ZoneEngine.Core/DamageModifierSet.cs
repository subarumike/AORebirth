namespace ZoneEngine.Core;

public sealed class DamageModifierSet
{
	public int FlatAddDamage { get; set; }

	public int LegacyDamageBonus { get; set; }

	public int TypeSpecificAddDamage { get; set; }

	public int UniversalAddDamage { get; set; }

	public int CriticalModifier { get; set; }

	public int AddNanoDamage { get; set; }
}
