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

        private readonly Dictionary<int, int> acceptedQuestByPlayfield =
            new Dictionary<int, int>();

        private readonly Dictionary<int, int> playfieldByAcceptedQuest =
            new Dictionary<int, int>();

        private readonly Dictionary<int, int> releasedAcceptedQuestByPlayfield =
            new Dictionary<int, int>();

        private readonly HashSet<int> releaseConfirmationPendingPlayfields =
            new HashSet<int>();

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
                var orderedRecords = new List<MissionAcgBindingRecord>();
                foreach (MissionAcgBindingRecord record in records)
                {
                    if (record == null)
                    {
                        failure = "Cannot restore a null binding record.";
                        return false;
                    }

                    orderedRecords.Add(record);
                }

                orderedRecords.Sort(CompareByAcceptedQuest);

                var restoredAcceptedQuestInstances =
                    new HashSet<int>(this.acceptedQuestInstances);
                var restoredMissionKeyInstances =
                    new HashSet<int>(this.missionKeyInstances);
                var restoredAcceptedQuestByPlayfield =
                    new Dictionary<int, int>(this.acceptedQuestByPlayfield);
                var restoredPlayfieldByAcceptedQuest =
                    new Dictionary<int, int>(this.playfieldByAcceptedQuest);
                int restoredNextPlayfield = this.nextPlayfield;
                int restoredNextAcceptedQuest = this.nextAcceptedQuest;
                int restoredNextMissionKey = this.nextMissionKey;

                for (int i = 0; i < orderedRecords.Count; i++)
                {
                    MissionAcgBindingRecord record = orderedRecords[i];
                    MissionAcgInstanceBinding binding = record.Binding;
                    if (!IsAcceptedQuestIdentity(binding.AcceptedQuestIdentity))
                    {
                        failure =
                            "Binding has invalid accepted quest identity "
                            + IdentityKey(binding.AcceptedQuestIdentity)
                            + ".";
                        return false;
                    }

                    if (!IsMissionKeyIdentity(binding.MissionKeyIdentity))
                    {
                        failure =
                            "Accepted quest "
                            + IdentityKey(binding.AcceptedQuestIdentity)
                            + " has invalid mission key identity "
                            + IdentityKey(binding.MissionKeyIdentity)
                            + ".";
                        return false;
                    }

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

                    if (!restoredAcceptedQuestInstances.Add(
                        binding.AcceptedQuestIdentity.Instance))
                    {
                        failure =
                            "Duplicate accepted quest identity "
                            + IdentityKey(binding.AcceptedQuestIdentity)
                            + ".";
                        return false;
                    }

                    if (!restoredMissionKeyInstances.Add(
                        binding.MissionKeyIdentity.Instance))
                    {
                        failure =
                            "Duplicate mission key identity "
                            + IdentityKey(binding.MissionKeyIdentity)
                            + ".";
                        return false;
                    }

                    if (record.State.ReservesPlayfield)
                    {
                        int currentOwner;
                        if (restoredAcceptedQuestByPlayfield.TryGetValue(
                            playfield,
                            out currentOwner))
                        {
                            failure =
                                "Duplicate active PF2 reservation "
                                + playfield
                                + " for accepted quests "
                                + currentOwner
                                + " and "
                                + binding.AcceptedQuestIdentity.Instance
                                + ".";
                            return false;
                        }

                        int currentPlayfield;
                        if (restoredPlayfieldByAcceptedQuest.TryGetValue(
                            binding.AcceptedQuestIdentity.Instance,
                            out currentPlayfield))
                        {
                            failure =
                                "Accepted quest "
                                + IdentityKey(binding.AcceptedQuestIdentity)
                                + " already owns active PF2 "
                                + currentPlayfield
                                + ".";
                            return false;
                        }

                        restoredAcceptedQuestByPlayfield.Add(
                            playfield,
                            binding.AcceptedQuestIdentity.Instance);
                        restoredPlayfieldByAcceptedQuest.Add(
                            binding.AcceptedQuestIdentity.Instance,
                            playfield);
                    }

                    AdvanceRestoredCursors(
                        binding,
                        ref restoredNextPlayfield,
                        ref restoredNextAcceptedQuest,
                        ref restoredNextMissionKey);
                }

                ReplaceSet(
                    this.acceptedQuestInstances,
                    restoredAcceptedQuestInstances);
                ReplaceSet(this.missionKeyInstances, restoredMissionKeyInstances);
                ReplaceDictionary(
                    this.acceptedQuestByPlayfield,
                    restoredAcceptedQuestByPlayfield);
                ReplaceDictionary(
                    this.playfieldByAcceptedQuest,
                    restoredPlayfieldByAcceptedQuest);
                foreach (int playfield in restoredAcceptedQuestByPlayfield.Keys)
                {
                    this.releasedAcceptedQuestByPlayfield.Remove(playfield);
                    this.releaseConfirmationPendingPlayfields.Remove(playfield);
                }

                this.nextPlayfield = restoredNextPlayfield;
                this.nextAcceptedQuest = restoredNextAcceptedQuest;
                this.nextMissionKey = restoredNextMissionKey;
            }

            return true;
        }

        internal bool TryReservePlayfield(
            MissionAcgIdentityRecord acceptedQuestIdentity,
            out int playfield2)
        {
            if (!IsAcceptedQuestIdentity(acceptedQuestIdentity))
            {
                playfield2 = 0;
                return false;
            }

            lock (this.gate)
            {
                if (!this.acceptedQuestInstances.Contains(
                    acceptedQuestIdentity.Instance))
                {
                    playfield2 = 0;
                    return false;
                }

                int existingPlayfield;
                if (this.playfieldByAcceptedQuest.TryGetValue(
                    acceptedQuestIdentity.Instance,
                    out existingPlayfield))
                {
                    playfield2 = existingPlayfield;
                    return true;
                }
            }

            return this.TryReservePlayfieldForOwner(
                acceptedQuestIdentity.Instance,
                out playfield2);
        }

        private bool TryReservePlayfieldForOwner(
            int acceptedQuestInstance,
            out int playfield2)
        {
            lock (this.gate)
            {
                int existingPlayfield;
                if (this.playfieldByAcceptedQuest.TryGetValue(
                        acceptedQuestInstance,
                        out existingPlayfield))
                {
                    playfield2 = existingPlayfield;
                    return true;
                }

                if (!this.acceptedQuestInstances.Contains(
                        acceptedQuestInstance))
                {
                    playfield2 = 0;
                    return false;
                }

                int candidate = this.nextPlayfield;
                int attempts = MaximumLivePlayfield2 - MinimumLivePlayfield2 + 1;
                for (int i = 0; i < attempts; i++)
                {
                    if (candidate > MaximumLivePlayfield2)
                    {
                        candidate = MinimumLivePlayfield2;
                    }

                    if (!this.unavailablePlayfields.Contains(candidate)
                        && !this.acceptedQuestByPlayfield.ContainsKey(candidate)
                        && !this.releaseConfirmationPendingPlayfields.Contains(
                            candidate))
                    {
                        this.acceptedQuestByPlayfield.Add(
                            candidate,
                            acceptedQuestInstance);
                        this.playfieldByAcceptedQuest.Add(
                            acceptedQuestInstance,
                            candidate);

                        this.releasedAcceptedQuestByPlayfield.Remove(candidate);
                        this.releaseConfirmationPendingPlayfields.Remove(
                            candidate);
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
                if (IsAcceptedQuestIdentity(acceptedQuestIdentity))
                {
                    int ownedPlayfield;
                    if (this.playfieldByAcceptedQuest.TryGetValue(
                        acceptedQuestIdentity.Instance,
                        out ownedPlayfield)
                        && ownedPlayfield == livePlayfield2)
                    {
                        int owner;
                        if (this.acceptedQuestByPlayfield.TryGetValue(
                            livePlayfield2,
                            out owner)
                            && owner == acceptedQuestIdentity.Instance)
                        {
                            this.acceptedQuestByPlayfield.Remove(livePlayfield2);
                            this.playfieldByAcceptedQuest.Remove(
                                acceptedQuestIdentity.Instance);
                        }
                    }

                    if (!this.playfieldByAcceptedQuest.ContainsKey(
                        acceptedQuestIdentity.Instance))
                    {
                        this.acceptedQuestInstances.Remove(
                            acceptedQuestIdentity.Instance);
                    }
                }

                if (missionKeyIdentity != null)
                {
                    this.missionKeyInstances.Remove(missionKeyIdentity.Instance);
                }
            }
        }

        internal bool ReleaseAfterCleanup(
            MissionAcgBindingRecord record,
            bool holdForDurableJournalConfirmation = false)
        {
            if (record == null
                || record.State.LifecycleState != MissionAcgLifecycleState.Cleaned
                || record.State.CleanupState != MissionAcgCleanupState.Completed
                || !IsAcceptedQuestIdentity(
                    record.Binding.AcceptedQuestIdentity)
                || !IsAllocatableRange(
                    record.Binding.AllocatedLivePlayfield2)
                || this.unavailablePlayfields.Contains(
                    record.Binding.AllocatedLivePlayfield2))
            {
                return false;
            }

            lock (this.gate)
            {
                int playfield = record.Binding.AllocatedLivePlayfield2;
                int acceptedQuest = record.Binding.AcceptedQuestIdentity.Instance;
                int currentOwner;
                if (this.acceptedQuestByPlayfield.TryGetValue(
                    playfield,
                    out currentOwner))
                {
                    if (currentOwner != acceptedQuest)
                    {
                        return false;
                    }

                    int currentPlayfield;
                    if (!this.playfieldByAcceptedQuest.TryGetValue(
                        acceptedQuest,
                        out currentPlayfield)
                        || currentPlayfield != playfield)
                    {
                        return false;
                    }

                    this.acceptedQuestByPlayfield.Remove(playfield);
                    this.playfieldByAcceptedQuest.Remove(acceptedQuest);
                    this.releasedAcceptedQuestByPlayfield[playfield] =
                        acceptedQuest;
                    if (holdForDurableJournalConfirmation)
                    {
                        this.releaseConfirmationPendingPlayfields.Add(
                            playfield);
                    }
                    else
                    {
                        this.releaseConfirmationPendingPlayfields.Remove(
                            playfield);
                    }

                    return true;
                }

                int releasedOwner;
                if (!this.releasedAcceptedQuestByPlayfield.TryGetValue(
                    playfield,
                    out releasedOwner)
                    || releasedOwner != acceptedQuest)
                {
                    return false;
                }

                if (holdForDurableJournalConfirmation)
                {
                    this.releaseConfirmationPendingPlayfields.Add(playfield);
                }
                else
                {
                    this.releaseConfirmationPendingPlayfields.Remove(playfield);
                }

                return true;
            }
        }

        internal bool TryRestoreReleasePendingJournalConfirmation(
            MissionAcgBindingRecord record,
            out string failure)
        {
            failure = string.Empty;
            if (record == null
                || record.State.LifecycleState
                   != MissionAcgLifecycleState.Cleaned
                || record.State.CleanupState
                   != MissionAcgCleanupState.Completed
                || !IsAcceptedQuestIdentity(
                    record.Binding.AcceptedQuestIdentity)
                || !IsAllocatableRange(
                    record.Binding.AllocatedLivePlayfield2)
                || this.unavailablePlayfields.Contains(
                    record.Binding.AllocatedLivePlayfield2))
            {
                failure =
                    "A cleaned exact-owner binding is required for release hold restoration.";
                return false;
            }

            lock (this.gate)
            {
                int playfield = record.Binding.AllocatedLivePlayfield2;
                int acceptedQuest =
                    record.Binding.AcceptedQuestIdentity.Instance;
                int activeOwner;
                if (this.acceptedQuestByPlayfield.TryGetValue(
                    playfield,
                    out activeOwner))
                {
                    failure =
                        "PF2 "
                        + playfield
                        + " is actively reserved by accepted quest "
                        + activeOwner
                        + ".";
                    return false;
                }

                int activePlayfield;
                if (this.playfieldByAcceptedQuest.TryGetValue(
                    acceptedQuest,
                    out activePlayfield))
                {
                    failure =
                        "Accepted quest "
                        + acceptedQuest
                        + " still actively reserves PF2 "
                        + activePlayfield
                        + ".";
                    return false;
                }

                foreach (KeyValuePair<int, int> released
                    in this.releasedAcceptedQuestByPlayfield)
                {
                    if (released.Value == acceptedQuest
                        && released.Key != playfield)
                    {
                        failure =
                            "Accepted quest "
                            + acceptedQuest
                            + " has conflicting release holds for PF2 "
                            + released.Key
                            + " and "
                            + playfield
                            + ".";
                        return false;
                    }
                }

                int releasedOwner;
                if (this.releasedAcceptedQuestByPlayfield.TryGetValue(
                        playfield,
                        out releasedOwner)
                    && releasedOwner != acceptedQuest)
                {
                    failure =
                        "PF2 "
                        + playfield
                        + " release hold belongs to accepted quest "
                        + releasedOwner
                        + ".";
                    return false;
                }

                this.releasedAcceptedQuestByPlayfield[playfield] =
                    acceptedQuest;
                this.releaseConfirmationPendingPlayfields.Add(playfield);
                return true;
            }
        }

        internal bool IsReserved(int livePlayfield2)
        {
            lock (this.gate)
            {
                return this.acceptedQuestByPlayfield.ContainsKey(livePlayfield2);
            }
        }

        internal bool ConfirmReleaseAfterDurableJournal(
            MissionAcgBindingRecord record)
        {
            if (record == null
                || record.State.LifecycleState
                   != MissionAcgLifecycleState.Cleaned
                || record.State.CleanupState
                   != MissionAcgCleanupState.Completed
                || !IsAcceptedQuestIdentity(
                    record.Binding.AcceptedQuestIdentity)
                || !IsAllocatableRange(
                    record.Binding.AllocatedLivePlayfield2))
            {
                return false;
            }

            lock (this.gate)
            {
                int playfield = record.Binding.AllocatedLivePlayfield2;
                int acceptedQuest =
                    record.Binding.AcceptedQuestIdentity.Instance;
                if (this.acceptedQuestByPlayfield.ContainsKey(playfield)
                    || this.playfieldByAcceptedQuest.ContainsKey(acceptedQuest))
                {
                    return false;
                }

                int releasedOwner;
                if (!this.releasedAcceptedQuestByPlayfield.TryGetValue(
                    playfield,
                    out releasedOwner))
                {
                    // Tombstones are process-local. A restart after allocator release
                    // restores the durable attempted journal before new allocations.
                    this.releaseConfirmationPendingPlayfields.Remove(playfield);
                    return true;
                }

                if (releasedOwner != acceptedQuest)
                {
                    return false;
                }

                this.releasedAcceptedQuestByPlayfield.Remove(playfield);
                this.releaseConfirmationPendingPlayfields.Remove(playfield);
                return true;
            }
        }

        internal bool IsReleasePendingJournalConfirmation(int livePlayfield2)
        {
            lock (this.gate)
            {
                return this.releaseConfirmationPendingPlayfields.Contains(
                    livePlayfield2);
            }
        }

        internal bool IsReservedBy(
            int livePlayfield2,
            MissionAcgIdentityRecord acceptedQuestIdentity)
        {
            if (!IsAcceptedQuestIdentity(acceptedQuestIdentity))
            {
                return false;
            }

            lock (this.gate)
            {
                int owner;
                return this.acceptedQuestByPlayfield.TryGetValue(
                           livePlayfield2,
                           out owner)
                       && owner == acceptedQuestIdentity.Instance;
            }
        }

        internal bool TryGetReservationOwner(
            int livePlayfield2,
            out MissionAcgIdentityRecord acceptedQuestIdentity)
        {
            lock (this.gate)
            {
                int owner;
                if (this.acceptedQuestByPlayfield.TryGetValue(
                    livePlayfield2,
                    out owner)
                    && owner != 0)
                {
                    acceptedQuestIdentity =
                        new MissionAcgIdentityRecord(
                            AcceptedQuestIdentityType,
                            owner);
                    return true;
                }
            }

            acceptedQuestIdentity = null;
            return false;
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

        private static void AdvanceRestoredCursors(
            MissionAcgInstanceBinding binding,
            ref int nextPlayfield,
            ref int nextAcceptedQuest,
            ref int nextMissionKey)
        {
            if (binding.AllocatedLivePlayfield2 >= nextPlayfield)
            {
                nextPlayfield = binding.AllocatedLivePlayfield2 + 1;
            }

            if (binding.AcceptedQuestIdentity.Instance >= nextAcceptedQuest
                && binding.AcceptedQuestIdentity.Instance < MaximumAcceptedQuestInstance)
            {
                nextAcceptedQuest = binding.AcceptedQuestIdentity.Instance + 1;
            }

            if (binding.MissionKeyIdentity.Instance >= nextMissionKey
                && binding.MissionKeyIdentity.Instance < MaximumMissionKeyInstance)
            {
                nextMissionKey = binding.MissionKeyIdentity.Instance + 1;
            }
        }

        private static int CompareByAcceptedQuest(
            MissionAcgBindingRecord left,
            MissionAcgBindingRecord right)
        {
            int typeComparison =
                left.Binding.AcceptedQuestIdentity.Type.CompareTo(
                    right.Binding.AcceptedQuestIdentity.Type);
            return typeComparison != 0
                       ? typeComparison
                       : left.Binding.AcceptedQuestIdentity.Instance.CompareTo(
                           right.Binding.AcceptedQuestIdentity.Instance);
        }

        private static bool IsAcceptedQuestIdentity(
            MissionAcgIdentityRecord identity)
        {
            return identity != null
                   && identity.Type == AcceptedQuestIdentityType
                   && identity.Instance >= MinimumAcceptedQuestInstance
                   && identity.Instance <= MaximumAcceptedQuestInstance;
        }

        private static bool IsMissionKeyIdentity(
            MissionAcgIdentityRecord identity)
        {
            return identity != null
                   && identity.Type == MissionKeyIdentityType
                   && identity.Instance >= MinimumMissionKeyInstance
                   && identity.Instance <= MaximumMissionKeyInstance;
        }

        private static void ReplaceSet(
            ISet<int> target,
            IEnumerable<int> source)
        {
            target.Clear();
            foreach (int value in source)
            {
                target.Add(value);
            }
        }

        private static void ReplaceDictionary(
            IDictionary<int, int> target,
            IEnumerable<KeyValuePair<int, int>> source)
        {
            target.Clear();
            foreach (KeyValuePair<int, int> pair in source)
            {
                target.Add(pair.Key, pair.Value);
            }
        }

        private static string IdentityKey(MissionAcgIdentityRecord identity)
        {
            return identity == null
                       ? "<null>"
                       : identity.Type + ":" + identity.Instance;
        }
    }
}
