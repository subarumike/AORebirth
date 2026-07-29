namespace ZoneEngine.Core;

public sealed class CombatLootItemTemplate
{
	public int LowId { get; set; }

	public int HighId { get; set; }

	public int MinQuality { get; set; }

	public int MaxQuality { get; set; }

	public int RangeCheck { get; set; }

	public string DropGroupHash { get; set; }
}
