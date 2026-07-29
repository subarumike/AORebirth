namespace AORebirth.Core.Playfields;

internal sealed class SubwayLootPoolSelectionPlan
{
	internal SubwayLootRollContext Context { get; private set; }

	internal SubwayLootPoolReference[] Pools { get; private set; }

	internal SubwayLootPoolSelectionPlan(SubwayLootRollContext context, SubwayLootPoolReference[] pools)
	{
		Context = context;
		Pools = pools ?? new SubwayLootPoolReference[0];
	}
}
