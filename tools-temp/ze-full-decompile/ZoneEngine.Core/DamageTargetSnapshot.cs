namespace ZoneEngine.Core;

public sealed class DamageTargetSnapshot
{
	public string Identity { get; set; }

	public DamageTargetCategory Category { get; set; }

	public int CurrentHealth { get; set; }

	public int MaximumHealth { get; set; }

	public int AddAllDef { get; set; }

	public DamageTargetSnapshot()
	{
		Identity = string.Empty;
		Category = DamageTargetCategory.Unknown;
	}
}
