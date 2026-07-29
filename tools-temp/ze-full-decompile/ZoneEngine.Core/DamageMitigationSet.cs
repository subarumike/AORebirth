namespace ZoneEngine.Core;

public sealed class DamageMitigationSet
{
	public int MatchingArmor { get; set; }

	public bool HasMatchingArmor { get; set; }

	public int ReflectPercentage { get; set; }

	public int ReflectCap { get; set; }

	public int TypedAbsorbPool { get; set; }

	public int UniversalAbsorbPool { get; set; }

	public int DamageShield { get; set; }

	public bool Immune { get; set; }

	public bool Invulnerable { get; set; }
}
