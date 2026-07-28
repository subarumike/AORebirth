namespace ZoneEngine.Core.Missions
{
    #region Usings ...

    using System;
    using System.Collections.Generic;

    #endregion

    internal sealed class MissionAcgAllocationService
    {
        internal const int MinimumLivePlayfield2 = 0x160000;

        internal const int MaximumLivePlayfield2 = 0x16FFFF;

        internal const int LegacySharedPlayfield2 = 1419349;

        internal const int AcceptedQuestIdentityType = 0xDAC3;

        internal const int MissionKeyIdentityType = 0xC76D;

        private const int MinimumAcceptedQuestInstance = 0x50000000;

        private const int MaximumAcceptedQuestInstance = 0x50FFFFFF;

        private const int MinimumMissionKeyInstance = 0x60000000;

        private const int MaximumMissionKeyInstance = 0x60FFFFFF;

        private readonly object gate = new object();

        private readonly HashSet<int> unavailablePlayfields = new HashSet<int>();

        private readonly HashSet<int> reservedPlayfields = new HashSet<int>();

        private readonly HashSet<int> acceptedQuestInstances = new HashSet<int>();

        private readonly HashSet<int> missionKeyInstances = new HashSet<int>();

        private int nextPlayfield = MinimumLivePlayfield2;

        private int nextAcceptedQuest = MinimumAcceptedQuestInstance;

        private int nextMissionKey = MinimumMissionKeyInstance;

        internal MissionAcgAllocationService(MissionAcgLayoutCatalog catalog)
        {
            if (catalog == null)
            {
                throw new ArgumentNullException("catalog");
            }

            this.unavailablePlayfields.Add(LegacySharedPlayfield2);
            for (int i = 0; i < catalog.Layouts.Count; i++)
            {
                this.unavailablePlayfields.Add(catalog.Layouts[i].SourcePlayfield2);
            }
        }

        internal bool TryRestore(
            IEnumerable<MissionAcgBindingRecord> records,
            out string failure)
        {
            failure = string.Empty;
            if (records == null)
            {
                failure = "Binding records are required.";
                return false;
            }

            lock (this.gate)
            {
                foreach (MissionAcgBindingRecord record in records)
                {
                    if (record == null)
                    {
                        failure = "Cannot restore a null binding record.";
                        return false;
                    }

                    MissionAcgInstanceBinding binding = record.Binding;
                    int playfield = binding.AllocatedLivePlayfield2;
                    if (!IsAllocatableRange(playfield)
                        || this.unavailablePlayfields.Contains(playfield))
                    {
                        failure =
                            "Accepted quest "
                            + IdentityKey(binding.AcceptedQuestIdentity)
                            + " has invalid allocated live PF2 "
                            + playfield
                            + ".";
                        return false;
                    }

                    if (!this.acceptedQuestInstances.Add(
                        binding.AcceptedQuestIdentity.Instance))
                    {
                        failure =
                            "Duplicate accepted quest identity "
                            + IdentityKey(binding.AcceptedQuestIdentity)
                            + ".";
                        return false;
                    }

                    if (!this.missionKeyInstances.Add(binding.MissionKeyIdentity.Instance))
                    {
                        failure =
                            "Duplicate mission key identity "
                            + IdentityKey(binding.MissionKeyIdentity)
                            + ".";
                        return false;
                    }

                    if (record.State.ReservesPlayfield
                        && !this.reservedPlayfields.Add(playfield))
                    {
                        failure = "Duplicate active PF2 reservation " + playfield + ".";
                        return false;
                    }

                    AdvanceRestoredCursors(binding);
                }
            }

            return true;
        }

        internal bool TryReservePlayfield(out int playfield2)
        {
            lock (this.gate)
            {
                int candidate = this.nextPlayfield;
                int attempts = MaximumLivePlayfield2 - MinimumLivePlayfield2 + 1;
                for (int i = 0; i < attempts; i++)
                {
                    if (candidate > MaximumLivePlayfield2)
                    {
                        candidate = MinimumLivePlayfield2;
                    }

                    if (!this.unavailablePlayfields.Contains(candidate)
                        && this.reservedPlayfields.Add(candidate))
                    {
                        playfield2 = candidate;
                        this.nextPlayfield = candidate + 1;
                        return true;
                    }

                    candidate++;
                }
            }

            playfield2 = 0;
            return false;
        }

        internal bool TryReserveAcceptedQuestIdentity(
            out MissionAcgIdentityRecord identity)
        {
            int instance;
            if (!TryReserveIdentity(
                this.acceptedQuestInstances,
                ref this.nextAcceptedQuest,
                MinimumAcceptedQuestInstance,
                MaximumAcceptedQuestInstance,
                out instance))
            {
                identity = null;
                return false;
            }

            identity = new MissionAcgIdentityRecord(AcceptedQuestIdentityType, instance);
            return true;
        }

        internal bool TryReserveMissionKeyIdentity(out MissionAcgIdentityRecord identity)
        {
            int instance;
            if (!TryReserveIdentity(
                this.missionKeyInstances,
                ref this.nextMissionKey,
                MinimumMissionKeyInstance,
                MaximumMissionKeyInstance,
                out instance))
            {
                identity = null;
                return false;
            }

            identity = new MissionAcgIdentityRecord(MissionKeyIdentityType, instance);
            return true;
        }

        internal void RollbackUnpersisted(
            MissionAcgIdentityRecord acceptedQuestIdentity,
            MissionAcgIdentityRecord missionKeyIdentity,
            int livePlayfield2)
        {
            lock (this.gate)
            {
                if (acceptedQuestIdentity != null)
                {
                    this.acceptedQuestInstances.Remove(acceptedQuestIdentity.Instance);
                }

                if (missionKeyIdentity != null)
                {
                    this.missionKeyInstances.Remove(missionKeyIdentity.Instance);
                }

                this.reservedPlayfields.Remove(livePlayfield2);
            }
        }

        internal bool ReleaseAfterCleanup(MissionAcgBindingRecord record)
        {
            if (record == null
                || record.State.LifecycleState != MissionAcgLifecycleState.Cleaned
                || record.State.CleanupState != MissionAcgCleanupState.Completed)
            {
                return false;
            }

            lock (this.gate)
            {
                return this.reservedPlayfields.Remove(
                    record.Binding.AllocatedLivePlayfield2);
            }
        }

        internal bool IsReserved(int livePlayfield2)
        {
            lock (this.gate)
            {
                return this.reservedPlayfields.Contains(livePlayfield2);
            }
        }

        internal static bool IsAllocatableRange(int playfield2)
        {
            return playfield2 >= MinimumLivePlayfield2
                   && playfield2 <= MaximumLivePlayfield2;
        }

        private bool TryReserveIdentity(
            ISet<int> reservations,
            ref int cursor,
            int minimum,
            int maximum,
            out int identity)
        {
            lock (this.gate)
            {
                int candidate = cursor;
                int attempts = maximum - minimum + 1;
                for (int i = 0; i < attempts; i++)
                {
                    if (candidate > maximum)
                    {
                        candidate = minimum;
                    }

                    if (reservations.Add(candidate))
                    {
                        identity = candidate;
                        cursor = candidate + 1;
                        return true;
                    }

                    candidate++;
                }
            }

            identity = 0;
            return false;
        }

        private void AdvanceRestoredCursors(MissionAcgInstanceBinding binding)
        {
            if (binding.AllocatedLivePlayfield2 >= this.nextPlayfield)
            {
                this.nextPlayfield = binding.AllocatedLivePlayfield2 + 1;
            }

            if (binding.AcceptedQuestIdentity.Instance >= this.nextAcceptedQuest
                && binding.AcceptedQuestIdentity.Instance < MaximumAcceptedQuestInstance)
            {
                this.nextAcceptedQuest = binding.AcceptedQuestIdentity.Instance + 1;
            }

            if (binding.MissionKeyIdentity.Instance >= this.nextMissionKey
                && binding.MissionKeyIdentity.Instance < MaximumMissionKeyInstance)
            {
                this.nextMissionKey = binding.MissionKeyIdentity.Instance + 1;
            }
        }

        private static string IdentityKey(MissionAcgIdentityRecord identity)
        {
            return identity.Type + ":" + identity.Instance;
        }
    }
}
