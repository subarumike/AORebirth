namespace ZoneEngine.Core.Perks;

public sealed class PerkDefinition
{
	public int PacketId { get; set; }

	public int Aoid { get; set; }

	public string Name { get; set; }

	public int? ActionTemplateId { get; set; }

	public int? ActionHash { get; set; }

	public int? ActionSlotIdOverride { get; set; }

	public bool GrantsPerkAction => ActionTemplateId.HasValue && ActionHash.HasValue;

	public int ActionSlotId
	{
		get
		{
			if (ActionSlotIdOverride.HasValue && ActionSlotIdOverride.Value > 0)
			{
				return ActionSlotIdOverride.Value;
			}
			return 10000 + PacketId;
		}
	}
}
