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
                MissionKeyGrantService.TryRemoveMissionKey(
                    client,
                    character,
                    record.Binding.MissionKeyIdentity.Instance);
                int ignoredKey;
                MissionKeyStore.TryTake(
                    character.Identity.Instance,
                    accepted,
                    out ignoredKey);
                int repairInstance;
                if (MissionKeyStore.TryTakeRepairKit(
                    character.Identity.Instance,
                    accepted,
                    out repairInstance))
                {
                    MissionKeyGrantService.TryRemoveRepairItem(
                        client,
                        character,
                        repairInstance);
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
                }
            }
        }
    }
}
