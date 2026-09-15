namespace ZoneEngine.Core.Arete.Quests
{
    /// <summary>
    /// Capture-backed policy for B18C kill feedback (20260719-Rex-Markus-stone / 20260614-194454).
    /// Remaining-count FormatFeedback is sent for kills 1/5 through 4/5; kill 5/5 uses Feedback only.
    /// </summary>
    internal static class RexB18CFeedbackPolicy
    {
        internal static bool ShouldSendPerKillFeedback(int currentCount, int requiredCount)
        {
            return currentCount > 0 && currentCount <= requiredCount;
        }
    }
}
