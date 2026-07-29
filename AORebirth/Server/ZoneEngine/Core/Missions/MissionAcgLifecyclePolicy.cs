namespace ZoneEngine.Core.Missions
{
    /// <summary>
    /// Pure lifecycle gates shared by generated-mission cleanup and restart recovery.
    /// Side effects remain with their existing runtime and persistence owners.
    /// </summary>
    internal static class MissionAcgLifecyclePolicy
    {
        internal static bool IsCompletionResumeEligible(MissionAcgObjectiveState state)
        {
            return state != null
                   && state.Phase >= MissionAcgCompletionPhase.ObjectiveVerified
                   && state.Phase < MissionAcgCompletionPhase.MissionCleanupCompleted
                   && state.Lifecycle != MissionAcgObjectiveLifecycle.Abandoned
                   && state.Lifecycle != MissionAcgObjectiveLifecycle.Expired
                   && state.Lifecycle != MissionAcgObjectiveLifecycle.CleanupCompleted
                   && state.Lifecycle != MissionAcgObjectiveLifecycle.Invalid;
        }

        internal static bool RequiresVerifiedRuntimeCleanup(
            MissionAcgLifecycleState lifecycle,
            MissionAcgCleanupState cleanup)
        {
            return lifecycle == MissionAcgLifecycleState.Cleaned
                   && cleanup == MissionAcgCleanupState.Completed;
        }

        internal static bool IsSameBindingStateVersion(
            MissionAcgInstanceState current,
            MissionAcgInstanceState supplied)
        {
            return current != null
                   && supplied != null
                   && current.LifecycleState == supplied.LifecycleState
                   && current.CleanupState == supplied.CleanupState
                   && current.LastUpdatedUtc == supplied.LastUpdatedUtc
                   && current.CleanupStartedUtc == supplied.CleanupStartedUtc;
        }

        internal static bool IsCleanupComplete(
            MissionAcgInstanceState bindingState,
            MissionAcgObjectiveState objectiveState)
        {
            return bindingState != null
                   && bindingState.LifecycleState == MissionAcgLifecycleState.Cleaned
                   && bindingState.CleanupState == MissionAcgCleanupState.Completed
                   && IsObjectiveCleanupComplete(objectiveState);
        }

        internal static bool IsObjectiveCleanupComplete(
            MissionAcgObjectiveState objectiveState)
        {
            return objectiveState != null
                   && objectiveState.Lifecycle
                      == MissionAcgObjectiveLifecycle.CleanupCompleted
                   && objectiveState.ObjectiveCleanupCompleted
                   && objectiveState.MissionCleanupCompleted;
        }
    }
}
