namespace ZoneEngine.Core.Missions
{
    using System;
    using System.Collections.Generic;

    using AORebirth.Core.Entities;
    using AORebirth.Core.Network;

    using SmokeLounge.AOtomation.Messaging.GameData;

    internal static class MissionAcgLifecycleService
    {
        internal static void TryCleanupPendingForCharacter(
            IZoneClient client,
            ICharacter character)
        {
            if (client == null || character == null)
            {
                return;
            }

            MissionAcgExpiryRuntime.ProcessForCharacter(
                client,
                character);

            MissionAcgCompletionJournalService.ResumeForCharacter(
                client,
                character);

            IList<MissionAcgBindingRecord> work =
                MissionAcgBindingRuntime.GetOwnedCleanupWork(
                    character.Identity.Instance);
            for (int i = 0; i < work.Count; i++)
            {
                MissionAcgBindingRecord record = work[i];
                if (MissionAcgExpiryRuntime.OwnsCleanup(record))
                {
                    continue;
                }

                MissionAcgBindingRecord cleaned;
                string failure;
                if (!TryCleanupOwnedRecord(
                        client,
                        character,
                        record,
                        out cleaned,
                        out failure))
                {
                    MissionDiagnostics.Log(
                        "ACG-CLEANUP-FAIL accepted={0}:{1} path={2} reason={3}",
                        record.Binding.AcceptedQuestIdentity.Type,
                        record.Binding.AcceptedQuestIdentity.Instance,
                        record.RecordPath,
                        failure);
                }
            }
        }

        internal static bool TryCleanupOwnedRecord(
            IZoneClient client,
            ICharacter character,
            MissionAcgBindingRecord record,
            out MissionAcgBindingRecord cleaned,
            out string failure)
        {
            cleaned = null;
            failure = string.Empty;
            if (client == null
                || character == null
                || record == null
                || record.Binding.OwnerIdentity.Instance
                   != character.Identity.Instance)
            {
                failure = "Exact generated-mission cleanup ownership is required.";
                return false;
            }

            MissionAcgObjectiveRecord objective;
            if (!MissionAcgObjectiveRuntime.TryGetByAccepted(
                    character.Identity.Instance,
                    record.Binding.AcceptedQuestIdentity.Instance,
                    out objective))
            {
                failure = "Exact generated-mission objective record is unavailable.";
                return false;
            }

            if (MissionAcgLifecyclePolicy.IsCleanupComplete(
                record.State,
                objective.State))
            {
                MissionAcceptedStore.Remove(
                    character.Identity.Instance,
                    ToIdentity(record.Binding.AcceptedQuestIdentity));
                cleaned = record;
                return MissionAcgBindingRuntime.TryReleaseAfterDurableCleanup(
                    record,
                    objective,
                    out failure);
            }

            if (!MissionAcgCompletionJournalService.RemoveExactArtifacts(
                client,
                character,
                record.Binding,
                objective,
                out failure))
            {
                return false;
            }

            MissionAcceptedStore.Remove(
                character.Identity.Instance,
                ToIdentity(record.Binding.AcceptedQuestIdentity));

            MissionAcgBindingRecord pending = record;
            if (record.State.LifecycleState != MissionAcgLifecycleState.CleanupPending
                && record.State.LifecycleState != MissionAcgLifecycleState.Cleaned)
            {
                if (!MissionAcgBindingRuntime.TryTransition(
                    record,
                    MissionAcgLifecycleState.CleanupPending,
                    MissionAcgCleanupState.InstanceReleasePending,
                    DateTime.UtcNow,
                    out pending,
                    out failure))
                {
                    return false;
                }
            }

            if (!MissionAcgBindingRuntime.TryCompleteRuntimeCleanup(
                pending,
                out failure))
            {
                return false;
            }

            MissionAcgObjectiveRecord cleanedObjective = objective;
            if (!MissionAcgLifecyclePolicy.IsObjectiveCleanupComplete(
                objective.State))
            {
                if (!MissionAcgObjectiveRuntime.TryReplaceState(
                    objective,
                    objective.State.Copy(
                        lifecycle: MissionAcgObjectiveLifecycle.CleanupCompleted,
                        objectiveCleanupCompleted: true,
                        missionCleanupCompleted: true),
                    out cleanedObjective,
                    out failure))
                {
                    return false;
                }
            }

            cleaned = pending;
            if (pending.State.LifecycleState != MissionAcgLifecycleState.Cleaned)
            {
                if (!MissionAcgBindingRuntime.TryTransition(
                    pending,
                    MissionAcgLifecycleState.Cleaned,
                    MissionAcgCleanupState.Completed,
                    DateTime.UtcNow,
                    out cleaned,
                    out failure))
                {
                    return false;
                }
            }

            if (!MissionAcgLifecyclePolicy.IsCleanupComplete(
                cleaned.State,
                cleanedObjective.State))
            {
                failure = "Generated-mission cleanup did not reach its durable terminal state.";
                return false;
            }

            return MissionAcgBindingRuntime.TryReleaseAfterDurableCleanup(
                cleaned,
                cleanedObjective,
                out failure);
        }

        private static Identity ToIdentity(MissionAcgIdentityRecord identity)
        {
            return new Identity
            {
                Type = (IdentityType)identity.Type,
                Instance = identity.Instance
            };
        }
    }
}
