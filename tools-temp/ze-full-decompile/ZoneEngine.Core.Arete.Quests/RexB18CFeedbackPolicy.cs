namespace ZoneEngine.Core.Arete.Quests;

internal static class RexB18CFeedbackPolicy
{
	internal static bool ShouldSendPerKillFeedback(int currentCount, int requiredCount)
	{
		return currentCount > 0 && currentCount <= requiredCount;
	}
}
