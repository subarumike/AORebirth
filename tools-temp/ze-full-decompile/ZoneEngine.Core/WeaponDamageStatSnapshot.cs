namespace ZoneEngine.Core;

public sealed class WeaponDamageStatSnapshot
{
	public int StatId { get; set; }

	public int Value { get; set; }

	public string Source { get; set; }

	public WeaponDamageStatSnapshot()
	{
		Source = string.Empty;
	}
}
