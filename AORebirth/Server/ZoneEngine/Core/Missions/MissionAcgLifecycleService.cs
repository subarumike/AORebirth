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

            MissionAcgCompletionJournalService.ResumeForCharacter(
                client,
                character);

            IList<MissionAcgBindingRecord> work =
                MissionAcgBindingRuntime.GetOwnedCleanupWork(
                    character.Identity.Instance);
            for (int i = 0; i < work.Count; i++)
            {
                MissionAcgBindingRecord record = work[i];
                var accepted =
                    new Identity
                    {
                        Type =
                            (IdentityType)record.Binding.AcceptedQuestIdentity.Type,
                        Instance =
                            record.Binding.AcceptedQuestIdentity.Instance
                    };
                MissionAcgObjectiveRecord objective;
                if (MissionAcgObjectiveRuntime.TryGetByAccepted(
                    character.Identity.Instance,
                    record.Binding.AcceptedQuestIdentity.Instance,
                    out objective))
                {
                    string artifactFailure;
                    if (!MissionAcgCompletionJournalService.RemoveExactArtifacts(
                        client,
                        character,
                        record.Binding,
                        objective,
                        out artifactFailure))
                    {
                        MissionDiagnostics.Log(
                            "ACG-CLEANUP-FAIL accepted={0}:{1} path={2} reason={3}",
                            record.Binding.AcceptedQuestIdentity.Type,
                            record.Binding.AcceptedQuestIdentity.Instance,
                            objective.RecordPath,
                            artifactFailure);
                        continue;
                    }
                }

                MissionAcceptedStore.Remove(
                    character.Identity.Instance,
                    accepted);

                MissionAcgBindingRecord pending = record;
                string failure;
                if (record.State.LifecycleState
                    != MissionAcgLifecycleState.CleanupPending)
                {
                    if (!MissionAcgBindingRuntime.TryTransition(
                        record,
                        MissionAcgLifecycleState.CleanupPending,
                        MissionAcgCleanupState.InstanceReleasePending,
                        DateTime.UtcNow,
                        out pending,
                        out failure))
                    {
                        MissionDiagnostics.Log(
                            "ACG-CLEANUP-FAIL accepted={0}:{1} path={2} reason={3}",
                            record.Binding.AcceptedQuestIdentity.Type,
                            record.Binding.AcceptedQuestIdentity.Instance,
                            record.RecordPath,
                            failure);
                        continue;
                    }
                }

                MissionAcgBindingRecord cleaned;
                if (!MissionAcgBindingRuntime.TryTransition(
                    pending,
                    MissionAcgLifecycleState.Cleaned,
                    MissionAcgCleanupState.Completed,
                    DateTime.UtcNow,
                    out cleaned,
                    out failure))
                {
                    MissionDiagnostics.Log(
                        "ACG-CLEANUP-FAIL accepted={0}:{1} path={2} reason={3}",
                        record.Binding.AcceptedQuestIdentity.Type,
                        record.Binding.AcceptedQuestIdentity.Instance,
                        record.RecordPath,
                        failure);
                    continue;
                }

                if (objective != null)
                {
                    MissionAcgObjectiveRecord cleanedObjective;
                    if (!MissionAcgObjectiveRuntime.TryReplaceState(
                        objective,
                        objective.State.Copy(
                            lifecycle: MissionAcgObjectiveLifecycle.CleanupCompleted,
                            objectiveCleanupCompleted: true,
                            missionCleanupCompleted: true),
                        out cleanedObjective,
                        out failure))
                    {
                        MissionDiagnostics.Log(
                            "ACG-CLEANUP-FAIL accepted={0}:{1} path={2} reason={3}",
                            record.Binding.AcceptedQuestIdentity.Type,
                            record.Binding.AcceptedQuestIdentity.Instance,
                            objective.RecordPath,
                            failure);
                    }
                }
            }
        }
    }
}
