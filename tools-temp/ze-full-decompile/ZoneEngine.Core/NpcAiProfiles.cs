namespace ZoneEngine.Core;

public static class NpcAiProfiles
{
	public static bool CanRetaliate(NpcAiProfile profile)
	{
		return profile == NpcAiProfile.Passive || profile == NpcAiProfile.Aggressive;
	}

	public static bool CanProximityAggro(NpcAiProfile profile)
	{
		return profile == NpcAiProfile.Aggressive;
	}
}
