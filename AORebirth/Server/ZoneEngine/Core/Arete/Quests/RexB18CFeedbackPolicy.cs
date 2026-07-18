namespace ZoneEngine.Core.Arete.Quests
{
    /// <summary>
    /// Capture-backed policy boundary for B18C kill feedback. The current contract is deliberately fail-closed.
    /// </summary>
    internal static class RexB18CFeedbackPolicy
    {
        internal static bool ShouldSendPerKillFeedback(int currentCount, int requiredCount)
        {
            return false;
        }
    }
}
