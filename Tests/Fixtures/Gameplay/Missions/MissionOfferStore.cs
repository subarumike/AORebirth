namespace ZoneEngine.Core.Missions
{
    #region Using declarations

    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.IO;
    using System.Text;

    using SmokeLounge.AOtomation.Messaging.GameData;
    using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;

    #endregion

    internal enum MissionOfferLifecycleState
    {
        Pending = 1,
        AcceptanceClaimed = 2,
        Accepted = 3,
        Expired = 4,
        Replaced = 5,
        Discarded = 6,
        Prepared = 7,
        FeeChargePending = 8
    }

    internal sealed class MissionOfferBatchHandle
    {
        internal Identity OwnerIdentity { get; set; }

        internal string BatchIdentity { get; set; }

        internal long LedgerRevision { get; set; }
    }

    internal sealed class MissionOfferRecord
    {
        internal Identity OwnerIdentity { get; set; }

        internal int OwnerInstance
        {
            get { return this.OwnerIdentity.Instance; }
        }

        internal QuestInfo Offer { get; set; }

        internal byte[] SerializedRollPayload { get; set; }

        internal string SerializedRollPayloadSha256 { get; set; }

        internal int OfferIndex { get; set; }

        internal string BatchIdentity { get; set; }

        internal int RollSeed { get; set; }

        internal int ResponseNonce { get; set; }

        internal int RollFee { get; set; }

        internal DateTime IssuedUtc { get; set; }

        internal DateTime ExpiresUtc { get; set; }

        internal DateTime UpdatedUtc { get; set; }

        internal int LevelSlider { get; set; }

        internal int GoodBadSlider { get; set; }

        internal int OrderChaosSlider { get; set; }

        internal int OpenHiddenSlider { get; set; }

        internal int PhysicalMysticalSlider { get; set; }

        internal int HeadOnStealthSlider { get; set; }

        internal int MoneyExperienceSlider { get; set; }

        internal MissionSliderEvidenceProfile SliderEvidenceProfile { get; set; }

        internal Identity IssuingTerminalIdentity { get; set; }

        internal MissionRollType MissionType { get; set; }

        internal int MissionIconId { get; set; }

        internal int MissionQuality { get; set; }

        internal string Title { get; set; }

        internal string Description { get; set; }

        internal int FrozenCashReward { get; set; }

        internal int FrozenExperienceReward { get; set; }

        internal int FrozenItemLowId { get; set; }

        internal int FrozenItemHighId { get; set; }

        internal int FrozenItemQuality { get; set; }

        internal int FrozenItemCount { get; set; }

        internal Identity ExteriorEntranceIdentity { get; set; }

        internal int ExteriorBuildingLow { get; set; }

        internal int ExteriorBuildingHigh { get; set; }

        internal float ExteriorX { get; set; }

        internal float ExteriorY { get; set; }

        internal float ExteriorZ { get; set; }

        internal MissionOfferLifecycleState LifecycleState { get; set; }

        internal long Revision { get; set; }

        internal Identity AcceptedQuestIdentity { get; set; }

        internal string TransitionReason { get; set; }

        internal bool Claimed
        {
            get
            {
                return this.LifecycleState == MissionOfferLifecycleState.AcceptanceClaimed
                       || this.LifecycleState == MissionOfferLifecycleState.Accepted;
            }
        }

        internal MissionOfferRecord Snapshot()
        {
            byte[] payload =
                this.SerializedRollPayload == null
                    ? null
                    : (byte[])this.SerializedRollPayload.Clone();
            QuestInfo offer = null;
            if (payload != null && payload.Length > 0)
            {
                QuestAlternativeMessage roll = MissionRollService.DeserializeBody(payload);
                offer = roll.QuestInfos[this.OfferIndex];
            }

            return new MissionOfferRecord
                   {
                       OwnerIdentity = MissionOfferStore.CopyIdentity(this.OwnerIdentity),
                       Offer = offer,
                       SerializedRollPayload = payload,
                       SerializedRollPayloadSha256 = this.SerializedRollPayloadSha256,
                       OfferIndex = this.OfferIndex,
                       BatchIdentity = this.BatchIdentity,
                       RollSeed = this.RollSeed,
                       ResponseNonce = this.ResponseNonce,
                       RollFee = this.RollFee,
                       IssuedUtc = this.IssuedUtc,
                       ExpiresUtc = this.ExpiresUtc,
                       UpdatedUtc = this.UpdatedUtc,
                       LevelSlider = this.LevelSlider,
                       GoodBadSlider = this.GoodBadSlider,
                       OrderChaosSlider = this.OrderChaosSlider,
                       OpenHiddenSlider = this.OpenHiddenSlider,
                       PhysicalMysticalSlider = this.PhysicalMysticalSlider,
                       HeadOnStealthSlider = this.HeadOnStealthSlider,
                       MoneyExperienceSlider = this.MoneyExperienceSlider,
                       SliderEvidenceProfile = this.SliderEvidenceProfile,
                       IssuingTerminalIdentity =
                           MissionOfferStore.CopyIdentity(this.IssuingTerminalIdentity),
                       MissionType = this.MissionType,
                       MissionIconId = this.MissionIconId,
                       MissionQuality = this.MissionQuality,
                       Title = this.Title,
                       Description = this.Description,
                       FrozenCashReward = this.FrozenCashReward,
                       FrozenExperienceReward = this.FrozenExperienceReward,
                       FrozenItemLowId = this.FrozenItemLowId,
                       FrozenItemHighId = this.FrozenItemHighId,
                       FrozenItemQuality = this.FrozenItemQuality,
                       FrozenItemCount = this.FrozenItemCount,
                       ExteriorEntranceIdentity =
                           MissionOfferStore.CopyIdentity(this.ExteriorEntranceIdentity),
                       ExteriorBuildingLow = this.ExteriorBuildingLow,
                       ExteriorBuildingHigh = this.ExteriorBuildingHigh,
                       ExteriorX = this.ExteriorX,
                       ExteriorY = this.ExteriorY,
                       ExteriorZ = this.ExteriorZ,
                       LifecycleState = this.LifecycleState,
                       Revision = this.Revision,
                       AcceptedQuestIdentity =
                           MissionOfferStore.CopyIdentity(this.AcceptedQuestIdentity),
                       TransitionReason = this.TransitionReason
                   };
        }
    }

    /// <summary>
    /// Durable pre-acceptance authority for generated terminal missions. One owner ledger contains
    /// the complete immutable roll projections plus monotonic offer lifecycle audit history, so a
    /// five-offer roll and replacement of its prior pending roll publish atomically.
    /// </summary>
    internal static class MissionOfferStore
    {
        internal const int CurrentFormatVersion = 1;

        internal const int OfferLifetimeSeconds = 48 * 60 * 60;

        private const string Header = "AORebirth-MissionOfferLedger";

        private const string FileExtension = ".offers";

        private static readonly object Sync = new object();

        private static readonly Dictionary<string, OwnerLedger> LedgersByOwner =
            new Dictionary<string, OwnerLedger>(StringComparer.Ordinal);

        private static readonly Dictionary<int, string> OwnerByOfferInstance =
            new Dictionary<int, string>();

        private static string missionStateDirectory;

        private static string ledgerDirectory;

        private static bool initialized;

        internal static object AuthorityGate
        {
            get { return Sync; }
        }

        internal static string DirectoryPath
        {
            get
            {
                lock (Sync)
                {
                    EnsureInitialized_NoLock();
                    return ledgerDirectory;
                }
            }
        }

        internal static void Initialize(string stateDirectory)
        {
            if (string.IsNullOrWhiteSpace(stateDirectory))
            {
                throw new ArgumentException(
                    "Mission state directory is required.",
                    "stateDirectory");
            }

            string normalized = Path.GetFullPath(stateDirectory);
            lock (Sync)
            {
                if (initialized)
                {
                    if (!string.Equals(
                            missionStateDirectory,
                            normalized,
                            StringComparison.OrdinalIgnoreCase))
                    {
                        throw new InvalidOperationException(
                            "Generated mission offer authority is already initialized for another state directory.");
                    }

                    return;
                }

                string candidateDirectory =
                    Path.Combine(normalized, "generated-offers");
                var loadedLedgers =
                    new Dictionary<string, OwnerLedger>(StringComparer.Ordinal);
                var loadedOfferOwners = new Dictionary<int, string>();
                var loadedAcceptedOffers = new Dictionary<int, int>();

                if (System.IO.Directory.Exists(candidateDirectory))
                {
                    string[] paths =
                        System.IO.Directory.GetFiles(
                            candidateDirectory,
                            "*" + FileExtension,
                            SearchOption.TopDirectoryOnly);
                    Array.Sort(paths, StringComparer.OrdinalIgnoreCase);
                    for (int i = 0; i < paths.Length; i++)
                    {
                        OwnerLedger ledger;
                        string diagnostic;
                        if (!TryReadLedger(paths[i], out ledger, out diagnostic))
                        {
                            throw new InvalidOperationException(
                                "Generated mission offer restoration failed closed for "
                                + paths[i]
                                + ": "
                                + diagnostic);
                        }

                        string ownerKey = OwnerKey(ledger.OwnerIdentity);
                        if (loadedLedgers.ContainsKey(ownerKey))
                        {
                            throw new InvalidOperationException(
                                "Generated mission offer restoration found duplicate owner ledger "
                                + ownerKey
                                + ".");
                        }

                        for (int j = 0; j < ledger.Records.Count; j++)
                        {
                            MissionOfferRecord record = ledger.Records[j];
                            if (!IdentityEquals(record.OwnerIdentity, ledger.OwnerIdentity))
                            {
                                throw new InvalidOperationException(
                                    "Generated mission offer restoration found a record detached from owner "
                                    + ownerKey
                                    + ".");
                            }

                            string existingOwner;
                            if (loadedOfferOwners.TryGetValue(
                                    record.Offer.QuestIdentity.Instance,
                                    out existingOwner))
                            {
                                throw new InvalidOperationException(
                                    "Generated mission offer restoration found duplicate offer identity "
                                    + record.Offer.QuestIdentity.Instance
                                    + " in owners "
                                    + existingOwner
                                    + " and "
                                    + ownerKey
                                    + ".");
                            }

                            loadedOfferOwners.Add(
                                record.Offer.QuestIdentity.Instance,
                                ownerKey);
                            if (record.LifecycleState
                                == MissionOfferLifecycleState.Accepted)
                            {
                                int acceptedQuestInstance =
                                    record.AcceptedQuestIdentity.Instance;
                                int existingOffer;
                                if (loadedAcceptedOffers.TryGetValue(
                                        acceptedQuestInstance,
                                        out existingOffer))
                                {
                                    throw new InvalidOperationException(
                                        "Generated mission offer restoration found accepted quest "
                                        + acceptedQuestInstance
                                        + " linked to offers "
                                        + existingOffer
                                        + " and "
                                        + record.Offer.QuestIdentity.Instance
                                        + ".");
                                }

                                loadedAcceptedOffers.Add(
                                    acceptedQuestInstance,
                                    record.Offer.QuestIdentity.Instance);
                            }
                        }

                        loadedLedgers.Add(ownerKey, ledger);
                    }
                }

                missionStateDirectory = normalized;
                ledgerDirectory = candidateDirectory;
                LedgersByOwner.Clear();
                OwnerByOfferInstance.Clear();
                foreach (KeyValuePair<string, OwnerLedger> entry in loadedLedgers)
                {
                    LedgersByOwner.Add(entry.Key, entry.Value);
                }

                foreach (KeyValuePair<int, string> entry in loadedOfferOwners)
                {
                    OwnerByOfferInstance.Add(entry.Key, entry.Value);
                }

                initialized = true;
            }
        }

        internal static void ResetForTests()
        {
            lock (Sync)
            {
                LedgersByOwner.Clear();
                OwnerByOfferInstance.Clear();
                missionStateDirectory = null;
                ledgerDirectory = null;
                initialized = false;
            }
        }

        internal static bool TryStoreRoll(
            Identity ownerIdentity,
            QuestAlternativeMessage response,
            QuestAlternativeMessage request,
            DateTime issuedUtc,
            int rollSeed,
            int responseNonce,
            byte[] serializedRollPayload,
            out MissionOfferBatchHandle batchHandle,
            out string failure)
        {
            batchHandle = null;
            failure = string.Empty;
            if (!IsValidIdentity(ownerIdentity)
                || response == null
                || request == null
                || response.QuestInfos == null
                || response.QuestInfos.Length == 0
                || serializedRollPayload == null
                || serializedRollPayload.Length == 0)
            {
                failure =
                    "Owner, complete mission roll, request, and serialized payload are required.";
                return false;
            }

            if (!IdentityEquals(response.Identity, ownerIdentity)
                || !IdentityEquals(
                    response.MissionTerminalIdentity,
                    request.MissionTerminalIdentity))
            {
                failure =
                    "Mission roll owner or terminal does not match the durable offer authority request.";
                return false;
            }

            MissionSliderProfile sliders;
            if (!MissionSliderProfile.TryCreate(request, out sliders, out failure))
            {
                return false;
            }

            if (issuedUtc.Kind != DateTimeKind.Utc)
            {
                issuedUtc = issuedUtc.ToUniversalTime();
            }

            byte[] payload = (byte[])serializedRollPayload.Clone();
            string payloadHash = MissionAcgHash.ComputeSha256(payload);
            string batchIdentity =
                ComputeBatchIdentity(
                    ownerIdentity,
                    rollSeed,
                    responseNonce,
                    issuedUtc,
                    payloadHash);

            var newRecords = new List<MissionOfferRecord>();
            var identities = new HashSet<int>();
            for (int i = 0; i < response.QuestInfos.Length; i++)
            {
                QuestInfo offer = response.QuestInfos[i];
                MissionOfferRecord record;
                if (!TryCreateRecord(
                        ownerIdentity,
                        response,
                        request,
                        sliders,
                        offer,
                        i,
                        issuedUtc,
                        rollSeed,
                        responseNonce,
                        batchIdentity,
                        payload,
                        payloadHash,
                        out record,
                        out failure))
                {
                    return false;
                }

                if (!identities.Add(record.Offer.QuestIdentity.Instance))
                {
                    failure = "Mission roll contains a duplicate offer identity.";
                    return false;
                }

                newRecords.Add(record);
            }

            lock (Sync)
            {
                EnsureInitialized_NoLock();
                for (int i = 0; i < newRecords.Count; i++)
                {
                    if (OwnerByOfferInstance.ContainsKey(
                            newRecords[i].Offer.QuestIdentity.Instance))
                    {
                        failure =
                            "Mission offer identity collides with durable offer history: "
                            + newRecords[i].Offer.QuestIdentity.Instance
                            + ".";
                        return false;
                    }
                }

                string ownerKey = OwnerKey(ownerIdentity);
                OwnerLedger current;
                if (!LedgersByOwner.TryGetValue(ownerKey, out current))
                {
                    current = new OwnerLedger
                              {
                                  OwnerIdentity = CopyIdentity(ownerIdentity),
                                  Revision = 0,
                                  Records = new List<MissionOfferRecord>()
                              };
                }

                OwnerLedger candidate = current.Clone();
                DateTime transitionUtc = issuedUtc;
                for (int i = 0; i < candidate.Records.Count; i++)
                {
                    MissionOfferRecord previous = candidate.Records[i];
                    if (previous.LifecycleState
                        == MissionOfferLifecycleState.FeeChargePending)
                    {
                        if (previous.ExpiresUtc <= transitionUtc)
                        {
                            Transition(
                                previous,
                                MissionOfferLifecycleState.Expired,
                                transitionUtc,
                                "FeeClaimExpiredBeforeReplacement",
                                null);
                        }
                        else
                        {
                            failure =
                                "An interrupted generated mission roll fee must be recovered before another roll.";
                            return false;
                        }
                    }

                    if (previous.LifecycleState == MissionOfferLifecycleState.Prepared)
                    {
                        Transition(
                            previous,
                            MissionOfferLifecycleState.Discarded,
                            transitionUtc,
                            "SupersededPreparation:" + batchIdentity,
                            null);
                    }
                }

                candidate.Records.AddRange(newRecords);
                candidate.Revision = checked(current.Revision + 1);
                if (!TryPersistLedger(
                        candidate,
                        current.Revision,
                        out failure))
                {
                    return false;
                }

                PublishLedger_NoLock(ownerKey, candidate);
                batchHandle =
                    new MissionOfferBatchHandle
                    {
                        OwnerIdentity = CopyIdentity(ownerIdentity),
                        BatchIdentity = batchIdentity,
                        LedgerRevision = candidate.Revision
                    };
                return true;
            }
        }

        internal static bool TryPublishBatch(
            MissionOfferBatchHandle batchHandle,
            DateTime nowUtc,
            out string failure)
        {
            failure = string.Empty;
            if (batchHandle == null
                || !IsValidIdentity(batchHandle.OwnerIdentity)
                || string.IsNullOrEmpty(batchHandle.BatchIdentity))
            {
                failure = "Exact fee-claimed mission offer batch is required.";
                return false;
            }

            lock (Sync)
            {
                EnsureInitialized_NoLock();
                string ownerKey = OwnerKey(batchHandle.OwnerIdentity);
                OwnerLedger current;
                if (!LedgersByOwner.TryGetValue(ownerKey, out current))
                {
                    failure = "Fee-claimed mission offer batch owner is not present.";
                    return false;
                }

                OwnerLedger candidate = current.Clone();
                DateTime transitionUtc = NormalizeUtc(nowUtc);
                int matched = 0;
                int feePending = 0;
                int pending = 0;
                for (int i = 0; i < candidate.Records.Count; i++)
                {
                    MissionOfferRecord record = candidate.Records[i];
                    if (!string.Equals(
                            record.BatchIdentity,
                            batchHandle.BatchIdentity,
                            StringComparison.Ordinal))
                    {
                        continue;
                    }

                    matched++;
                    if (record.LifecycleState == MissionOfferLifecycleState.FeeChargePending)
                    {
                        feePending++;
                    }
                    else if (record.LifecycleState == MissionOfferLifecycleState.Pending)
                    {
                        pending++;
                    }
                    else
                    {
                        failure =
                            "Fee-claimed mission offer batch has already reached a terminal or accepted state.";
                        return false;
                    }
                }

                if (matched == 0)
                {
                    failure = "Fee-claimed mission offer batch is absent.";
                    return false;
                }

                if (pending == matched)
                {
                    batchHandle.LedgerRevision = current.Revision;
                    return true;
                }

                if (feePending != matched)
                {
                    failure = "Mission offer batch has mixed publication state.";
                    return false;
                }

                for (int i = 0; i < candidate.Records.Count; i++)
                {
                    MissionOfferRecord record = candidate.Records[i];
                    bool isPublishingBatch = string.Equals(
                        record.BatchIdentity,
                        batchHandle.BatchIdentity,
                        StringComparison.Ordinal);
                    if (isPublishingBatch)
                    {
                        if (record.ExpiresUtc <= transitionUtc)
                        {
                            failure = "Fee-claimed mission offer batch expired before publication.";
                            return false;
                        }

                        Transition(
                            record,
                            MissionOfferLifecycleState.Pending,
                            transitionUtc,
                            "PublishedAfterRollFee",
                            null);
                    }
                    else if (record.LifecycleState == MissionOfferLifecycleState.Pending)
                    {
                        Transition(
                            record,
                            record.ExpiresUtc <= transitionUtc
                                ? MissionOfferLifecycleState.Expired
                                : MissionOfferLifecycleState.Replaced,
                            transitionUtc,
                            record.ExpiresUtc <= transitionUtc
                                ? "ExpiredBeforeReplacement"
                                : "ReplacedByBatch:" + batchHandle.BatchIdentity,
                            null);
                    }
                }

                candidate.Revision = checked(current.Revision + 1);
                if (!TryPersistLedger(candidate, current.Revision, out failure))
                {
                    return false;
                }

                PublishLedger_NoLock(ownerKey, candidate);
                batchHandle.LedgerRevision = candidate.Revision;
                return true;
            }
        }

        internal static bool TryBeginFeeCharge(
            MissionOfferBatchHandle batchHandle,
            int rollFee,
            DateTime nowUtc,
            out string failure)
        {
            failure = string.Empty;
            if (batchHandle == null
                || !IsValidIdentity(batchHandle.OwnerIdentity)
                || string.IsNullOrEmpty(batchHandle.BatchIdentity)
                || rollFee <= 0)
            {
                failure = "Exact prepared mission offer batch and positive roll fee are required.";
                return false;
            }

            lock (Sync)
            {
                EnsureInitialized_NoLock();
                string ownerKey = OwnerKey(batchHandle.OwnerIdentity);
                OwnerLedger current;
                if (!LedgersByOwner.TryGetValue(ownerKey, out current))
                {
                    failure = "Prepared mission offer batch owner is not present.";
                    return false;
                }

                OwnerLedger candidate = current.Clone();
                DateTime transitionUtc = NormalizeUtc(nowUtc);
                int matched = 0;
                int prepared = 0;
                int feePending = 0;
                for (int i = 0; i < candidate.Records.Count; i++)
                {
                    MissionOfferRecord record = candidate.Records[i];
                    if (!string.Equals(
                            record.BatchIdentity,
                            batchHandle.BatchIdentity,
                            StringComparison.Ordinal))
                    {
                        continue;
                    }

                    matched++;
                    if (record.LifecycleState == MissionOfferLifecycleState.Prepared)
                    {
                        if (record.ExpiresUtc <= transitionUtc)
                        {
                            failure = "Prepared mission offer batch expired before its roll-fee claim.";
                            return false;
                        }

                        prepared++;
                    }
                    else if (record.LifecycleState
                             == MissionOfferLifecycleState.FeeChargePending
                             && record.RollFee == rollFee)
                    {
                        feePending++;
                    }
                    else
                    {
                        failure = "Mission offer batch cannot begin this roll-fee claim.";
                        return false;
                    }
                }

                if (matched == 0)
                {
                    failure = "Prepared mission offer batch is absent.";
                    return false;
                }

                if (feePending == matched)
                {
                    batchHandle.LedgerRevision = current.Revision;
                    return true;
                }

                if (prepared != matched)
                {
                    failure = "Mission offer batch has mixed roll-fee claim state.";
                    return false;
                }

                for (int i = 0; i < candidate.Records.Count; i++)
                {
                    MissionOfferRecord record = candidate.Records[i];
                    if (!string.Equals(
                            record.BatchIdentity,
                            batchHandle.BatchIdentity,
                            StringComparison.Ordinal))
                    {
                        continue;
                    }

                    record.RollFee = rollFee;
                    Transition(
                        record,
                        MissionOfferLifecycleState.FeeChargePending,
                        transitionUtc,
                        "RollFeeChargePending",
                        null);
                }

                candidate.Revision = checked(current.Revision + 1);
                if (!TryPersistLedger(candidate, current.Revision, out failure))
                {
                    return false;
                }

                PublishLedger_NoLock(ownerKey, candidate);
                batchHandle.LedgerRevision = candidate.Revision;
                return true;
            }
        }

        internal static bool TryDiscardBatch(
            MissionOfferBatchHandle batchHandle,
            DateTime nowUtc,
            string reason,
            out string failure)
        {
            failure = string.Empty;
            if (batchHandle == null
                || !IsValidIdentity(batchHandle.OwnerIdentity)
                || string.IsNullOrEmpty(batchHandle.BatchIdentity))
            {
                failure = "Exact mission offer batch is required.";
                return false;
            }

            lock (Sync)
            {
                EnsureInitialized_NoLock();
                string ownerKey = OwnerKey(batchHandle.OwnerIdentity);
                OwnerLedger current;
                if (!LedgersByOwner.TryGetValue(ownerKey, out current))
                {
                    failure = "Mission offer batch owner is not present.";
                    return false;
                }

                OwnerLedger candidate = current.Clone();
                int changed = 0;
                int matched = 0;
                for (int i = 0; i < candidate.Records.Count; i++)
                {
                    MissionOfferRecord record = candidate.Records[i];
                    if (!string.Equals(
                            record.BatchIdentity,
                            batchHandle.BatchIdentity,
                            StringComparison.Ordinal))
                    {
                        continue;
                    }

                    matched++;

                    if (record.LifecycleState
                        == MissionOfferLifecycleState.Discarded)
                    {
                        continue;
                    }

                    if (record.LifecycleState != MissionOfferLifecycleState.Prepared
                        && record.LifecycleState != MissionOfferLifecycleState.FeeChargePending
                        && record.LifecycleState != MissionOfferLifecycleState.Pending)
                    {
                        failure =
                            "Mission offer batch is no longer wholly prepared or pending and cannot be discarded.";
                        return false;
                    }

                    Transition(
                        record,
                        MissionOfferLifecycleState.Discarded,
                        NormalizeUtc(nowUtc),
                        string.IsNullOrEmpty(reason) ? "Discarded" : reason,
                        null);
                    changed++;
                }

                if (matched == 0)
                {
                    failure = "Mission offer batch is absent.";
                    return false;
                }

                if (changed == 0)
                {
                    return true;
                }

                candidate.Revision = checked(current.Revision + 1);
                if (!TryPersistLedger(candidate, current.Revision, out failure))
                {
                    return false;
                }

                PublishLedger_NoLock(ownerKey, candidate);
                return true;
            }
        }

        internal static bool TryClaimForAcceptance(
            Identity ownerIdentity,
            Identity questIdentity,
            DateTime nowUtc,
            out MissionOfferRecord record,
            out string failure)
        {
            record = null;
            failure = string.Empty;
            if (!IsValidIdentity(ownerIdentity) || !IsValidIdentity(questIdentity))
            {
                failure = "Exact offer owner and identity are required.";
                return false;
            }

            lock (Sync)
            {
                EnsureInitialized_NoLock();
                string ownerKey = OwnerKey(ownerIdentity);
                OwnerLedger current;
                if (!LedgersByOwner.TryGetValue(ownerKey, out current))
                {
                    failure = "Offer is stale, belongs to another owner, or was replaced.";
                    return false;
                }

                int index = FindRecord(current, questIdentity);
                if (index < 0)
                {
                    failure = "Offer is stale, belongs to another owner, or was replaced.";
                    return false;
                }

                MissionOfferRecord currentRecord = current.Records[index];
                if (currentRecord.LifecycleState
                    == MissionOfferLifecycleState.AcceptanceClaimed)
                {
                    record = currentRecord.Snapshot();
                    return true;
                }

                if (currentRecord.LifecycleState != MissionOfferLifecycleState.Pending)
                {
                    failure = TerminalFailure(currentRecord.LifecycleState);
                    return false;
                }

                OwnerLedger candidate = current.Clone();
                MissionOfferRecord candidateRecord = candidate.Records[index];
                DateTime normalizedNow = NormalizeUtc(nowUtc);
                if (candidateRecord.ExpiresUtc <= normalizedNow)
                {
                    Transition(
                        candidateRecord,
                        MissionOfferLifecycleState.Expired,
                        normalizedNow,
                        "Expired",
                        null);
                    candidate.Revision = checked(current.Revision + 1);
                    if (!TryPersistLedger(candidate, current.Revision, out failure))
                    {
                        return false;
                    }

                    PublishLedger_NoLock(ownerKey, candidate);
                    failure = "Offer has expired.";
                    return false;
                }

                Transition(
                    candidateRecord,
                    MissionOfferLifecycleState.AcceptanceClaimed,
                    normalizedNow,
                    "AcceptanceClaimed",
                    null);
                candidate.Revision = checked(current.Revision + 1);
                if (!TryPersistLedger(candidate, current.Revision, out failure))
                {
                    return false;
                }

                PublishLedger_NoLock(ownerKey, candidate);
                record = candidateRecord.Snapshot();
                return true;
            }
        }

        internal static bool TryReleaseClaim(
            MissionOfferRecord expectedClaim,
            DateTime nowUtc,
            out string failure)
        {
            failure = string.Empty;
            if (expectedClaim == null
                || !IsValidIdentity(expectedClaim.OwnerIdentity)
                || expectedClaim.Offer == null
                || !IsValidIdentity(expectedClaim.Offer.QuestIdentity))
            {
                failure = "Exact claimed offer record is required.";
                return false;
            }

            lock (Sync)
            {
                EnsureInitialized_NoLock();
                string ownerKey = OwnerKey(expectedClaim.OwnerIdentity);
                OwnerLedger current;
                if (!LedgersByOwner.TryGetValue(ownerKey, out current))
                {
                    failure = "Claimed offer owner no longer exists.";
                    return false;
                }

                int index = FindRecord(current, expectedClaim.Offer.QuestIdentity);
                if (index < 0)
                {
                    failure = "Claimed offer no longer exists.";
                    return false;
                }

                MissionOfferRecord currentRecord = current.Records[index];
                if (currentRecord.Revision != expectedClaim.Revision
                    || currentRecord.LifecycleState
                       != MissionOfferLifecycleState.AcceptanceClaimed)
                {
                    failure = "Mission offer claim compare-and-swap conflict.";
                    return false;
                }

                OwnerLedger candidate = current.Clone();
                MissionOfferRecord candidateRecord = candidate.Records[index];
                DateTime normalizedNow = NormalizeUtc(nowUtc);
                MissionOfferLifecycleState next =
                    ResolveReleasedClaimState(candidate, candidateRecord, normalizedNow);
                Transition(
                    candidateRecord,
                    next,
                    normalizedNow,
                    next == MissionOfferLifecycleState.Expired
                        ? "ExpiredWhileClaimed"
                        : next == MissionOfferLifecycleState.Replaced
                            ? "ReplacedWhileClaimed"
                            : "AcceptanceClaimReleased",
                    null);
                candidate.Revision = checked(current.Revision + 1);
                if (!TryPersistLedger(candidate, current.Revision, out failure))
                {
                    return false;
                }

                PublishLedger_NoLock(ownerKey, candidate);
                return true;
            }
        }

        internal static bool TryRestoreUnprojectedClaim(
            MissionOfferRecord expectedClaim,
            DateTime nowUtc,
            out string failure)
        {
            failure = string.Empty;
            if (expectedClaim == null
                || expectedClaim.Offer == null
                || !IsValidIdentity(expectedClaim.OwnerIdentity)
                || !IsValidIdentity(expectedClaim.Offer.QuestIdentity))
            {
                failure = "Exact unprojected claim is required.";
                return false;
            }

            lock (Sync)
            {
                EnsureInitialized_NoLock();
                string ownerKey = OwnerKey(expectedClaim.OwnerIdentity);
                OwnerLedger current;
                if (!LedgersByOwner.TryGetValue(ownerKey, out current))
                {
                    failure = "Unprojected claim owner is absent.";
                    return false;
                }

                int index = FindRecord(current, expectedClaim.Offer.QuestIdentity);
                if (index < 0)
                {
                    failure = "Unprojected claim is absent.";
                    return false;
                }

                MissionOfferRecord currentRecord = current.Records[index];
                if (currentRecord.LifecycleState
                    != MissionOfferLifecycleState.AcceptanceClaimed)
                {
                    if (currentRecord.Revision == expectedClaim.Revision + 1
                        && (string.Equals(
                                currentRecord.TransitionReason,
                                "ExpiredDuringRestartRecovery",
                                StringComparison.Ordinal)
                            || string.Equals(
                                currentRecord.TransitionReason,
                                "ReplacedDuringRestartRecovery",
                                StringComparison.Ordinal)
                            || string.Equals(
                                currentRecord.TransitionReason,
                                "RestartRecoveredUnprojectedClaim",
                                StringComparison.Ordinal)))
                    {
                        return true;
                    }

                    failure = "Unprojected claim compare-and-swap conflict.";
                    return false;
                }

                if (currentRecord.Revision != expectedClaim.Revision)
                {
                    failure = "Unprojected claim compare-and-swap conflict.";
                    return false;
                }

                OwnerLedger candidate = current.Clone();
                MissionOfferRecord candidateRecord = candidate.Records[index];
                DateTime normalizedNow = NormalizeUtc(nowUtc);
                MissionOfferLifecycleState next =
                    ResolveReleasedClaimState(candidate, candidateRecord, normalizedNow);
                Transition(
                    candidateRecord,
                    next,
                    normalizedNow,
                    next == MissionOfferLifecycleState.Expired
                        ? "ExpiredDuringRestartRecovery"
                        : next == MissionOfferLifecycleState.Replaced
                            ? "ReplacedDuringRestartRecovery"
                            : "RestartRecoveredUnprojectedClaim",
                    null);
                candidate.Revision = checked(current.Revision + 1);
                if (!TryPersistLedger(candidate, current.Revision, out failure))
                {
                    return false;
                }

                PublishLedger_NoLock(ownerKey, candidate);
                return true;
            }
        }

        internal static bool TryMarkAccepted(
            MissionOfferRecord expectedClaim,
            MissionAcgIdentityRecord acceptedQuestIdentity,
            DateTime nowUtc,
            out string failure)
        {
            failure = string.Empty;
            if (expectedClaim == null
                || acceptedQuestIdentity == null
                || acceptedQuestIdentity.Instance <= 0)
            {
                failure = "Claimed offer and accepted quest identity are required.";
                return false;
            }

            return TryMarkAcceptedCore(
                expectedClaim.OwnerIdentity,
                expectedClaim.Offer.QuestIdentity,
                expectedClaim.Revision,
                acceptedQuestIdentity,
                nowUtc,
                false,
                out failure);
        }

        internal static bool TryReconcileAccepted(
            Identity ownerIdentity,
            Identity offerIdentity,
            MissionAcgIdentityRecord acceptedQuestIdentity,
            DateTime nowUtc,
            out bool offerRecordExists,
            out string failure)
        {
            offerRecordExists = false;
            failure = string.Empty;
            if (!IsValidIdentity(ownerIdentity)
                || !IsValidIdentity(offerIdentity)
                || acceptedQuestIdentity == null
                || acceptedQuestIdentity.Instance <= 0)
            {
                failure = "Exact accepted offer correlation is required.";
                return false;
            }

            lock (Sync)
            {
                EnsureInitialized_NoLock();
                OwnerLedger ledger;
                if (!LedgersByOwner.TryGetValue(OwnerKey(ownerIdentity), out ledger)
                    || FindRecord(ledger, offerIdentity) < 0)
                {
                    return true;
                }

                offerRecordExists = true;
            }

            return TryMarkAcceptedCore(
                ownerIdentity,
                offerIdentity,
                0,
                acceptedQuestIdentity,
                nowUtc,
                true,
                out failure);
        }

        internal static void DiscardPreparedOnRestoration(DateTime nowUtc)
        {
            lock (Sync)
            {
                EnsureInitialized_NoLock();
                var ownerKeys = new List<string>(LedgersByOwner.Keys);
                ownerKeys.Sort(StringComparer.Ordinal);
                for (int ownerIndex = 0; ownerIndex < ownerKeys.Count; ownerIndex++)
                {
                    string ownerKey = ownerKeys[ownerIndex];
                    OwnerLedger current = LedgersByOwner[ownerKey];
                    OwnerLedger candidate = current.Clone();
                    int changed = 0;
                    for (int i = 0; i < candidate.Records.Count; i++)
                    {
                        MissionOfferRecord record = candidate.Records[i];
                        if (record.LifecycleState == MissionOfferLifecycleState.Prepared)
                        {
                            Transition(
                                record,
                                MissionOfferLifecycleState.Discarded,
                                NormalizeUtc(nowUtc),
                                "UnpaidPreparationDiscardedOnRestoration",
                                null);
                            changed++;
                        }
                    }

                    if (changed == 0)
                    {
                        continue;
                    }

                    candidate.Revision = checked(current.Revision + 1);
                    string failure;
                    if (!TryPersistLedger(candidate, current.Revision, out failure))
                    {
                        throw new InvalidOperationException(
                            "Generated mission prepared-offer restoration failed for owner "
                            + ownerKey
                            + ": "
                            + failure);
                    }

                    PublishLedger_NoLock(ownerKey, candidate);
                }
            }
        }

        internal static void ExpirePending(DateTime nowUtc)
        {
            lock (Sync)
            {
                EnsureInitialized_NoLock();
                var ownerKeys = new List<string>(LedgersByOwner.Keys);
                ownerKeys.Sort(StringComparer.Ordinal);
                for (int ownerIndex = 0; ownerIndex < ownerKeys.Count; ownerIndex++)
                {
                    string ownerKey = ownerKeys[ownerIndex];
                    OwnerLedger current = LedgersByOwner[ownerKey];
                    OwnerLedger candidate = current.Clone();
                    int changed = 0;
                    for (int i = 0; i < candidate.Records.Count; i++)
                    {
                        MissionOfferRecord record = candidate.Records[i];
                        if ((record.LifecycleState == MissionOfferLifecycleState.Pending
                             || record.LifecycleState
                                == MissionOfferLifecycleState.FeeChargePending)
                            && record.ExpiresUtc <= NormalizeUtc(nowUtc))
                        {
                            Transition(
                                record,
                                MissionOfferLifecycleState.Expired,
                                NormalizeUtc(nowUtc),
                                "ExpiredOnRestoration",
                                null);
                            changed++;
                        }
                    }

                    if (changed == 0)
                    {
                        continue;
                    }

                    candidate.Revision = checked(current.Revision + 1);
                    string failure;
                    if (!TryPersistLedger(candidate, current.Revision, out failure))
                    {
                        throw new InvalidOperationException(
                            "Generated mission offer expiry restoration failed for owner "
                            + ownerKey
                            + ": "
                            + failure);
                    }

                    PublishLedger_NoLock(ownerKey, candidate);
                }
            }
        }

        internal static bool TryGetFeeChargePending(
            Identity ownerIdentity,
            DateTime nowUtc,
            out bool found,
            out MissionOfferBatchHandle batchHandle,
            out int rollFee,
            out QuestAlternativeMessage response,
            out string failure)
        {
            found = false;
            batchHandle = null;
            rollFee = 0;
            response = null;
            failure = string.Empty;
            if (!IsValidIdentity(ownerIdentity))
            {
                failure = "Exact generated mission offer owner is required.";
                return false;
            }

            ExpirePending(nowUtc);
            lock (Sync)
            {
                EnsureInitialized_NoLock();
                OwnerLedger ledger;
                if (!LedgersByOwner.TryGetValue(OwnerKey(ownerIdentity), out ledger))
                {
                    return true;
                }

                string batchIdentity = null;
                byte[] payload = null;
                string payloadHash = null;
                int matched = 0;
                for (int i = 0; i < ledger.Records.Count; i++)
                {
                    MissionOfferRecord record = ledger.Records[i];
                    if (record.LifecycleState
                        != MissionOfferLifecycleState.FeeChargePending)
                    {
                        continue;
                    }

                    if (batchIdentity == null)
                    {
                        batchIdentity = record.BatchIdentity;
                        rollFee = record.RollFee;
                        payload = record.SerializedRollPayload;
                        payloadHash = record.SerializedRollPayloadSha256;
                    }
                    else if (!string.Equals(
                                 batchIdentity,
                                 record.BatchIdentity,
                                 StringComparison.Ordinal)
                             || rollFee != record.RollFee
                             || !string.Equals(
                                 payloadHash,
                                 record.SerializedRollPayloadSha256,
                                 StringComparison.OrdinalIgnoreCase))
                    {
                        failure = "Generated mission owner has conflicting pending roll-fee claims.";
                        return false;
                    }

                    matched++;
                }

                if (matched == 0)
                {
                    return true;
                }

                try
                {
                    response = MissionRollService.DeserializeBody(payload);
                }
                catch (Exception ex)
                {
                    failure = "Pending roll-fee payload is malformed: " + ex.Message;
                    return false;
                }

                found = true;
                batchHandle =
                    new MissionOfferBatchHandle
                    {
                        OwnerIdentity = CopyIdentity(ownerIdentity),
                        BatchIdentity = batchIdentity,
                        LedgerRevision = ledger.Revision
                    };
                return true;
            }
        }

        internal static bool TryGetPendingRollForLogin(
            Identity ownerIdentity,
            DateTime nowUtc,
            out bool found,
            out MissionOfferBatchHandle batchHandle,
            out QuestAlternativeMessage response,
            out string failure)
        {
            found = false;
            batchHandle = null;
            response = null;
            failure = string.Empty;
            if (!IsValidIdentity(ownerIdentity))
            {
                failure = "Exact generated mission offer owner is required.";
                return false;
            }

            ExpirePending(nowUtc);
            lock (Sync)
            {
                OwnerLedger ledger;
                if (!LedgersByOwner.TryGetValue(OwnerKey(ownerIdentity), out ledger))
                {
                    return true;
                }

                string batchIdentity = null;
                byte[] payload = null;
                int pendingCount = 0;
                for (int i = 0; i < ledger.Records.Count; i++)
                {
                    MissionOfferRecord record = ledger.Records[i];
                    if (record.LifecycleState != MissionOfferLifecycleState.Pending)
                    {
                        continue;
                    }

                    if (batchIdentity == null)
                    {
                        batchIdentity = record.BatchIdentity;
                        payload = record.SerializedRollPayload;
                    }
                    else if (!string.Equals(
                                 batchIdentity,
                                 record.BatchIdentity,
                                 StringComparison.Ordinal))
                    {
                        failure = "Generated mission owner has multiple pending offer batches.";
                        return false;
                    }

                    pendingCount++;
                }

                if (pendingCount == 0)
                {
                    return true;
                }

                int batchRecordCount = 0;
                for (int i = 0; i < ledger.Records.Count; i++)
                {
                    if (string.Equals(
                            ledger.Records[i].BatchIdentity,
                            batchIdentity,
                            StringComparison.Ordinal))
                    {
                        batchRecordCount++;
                    }
                }

                if (batchRecordCount != pendingCount)
                {
                    return true;
                }

                try
                {
                    response = MissionRollService.DeserializeBody(payload);
                }
                catch (Exception ex)
                {
                    failure = "Pending mission offer payload is malformed: " + ex.Message;
                    return false;
                }

                found = true;
                batchHandle =
                    new MissionOfferBatchHandle
                    {
                        OwnerIdentity = CopyIdentity(ownerIdentity),
                        BatchIdentity = batchIdentity,
                        LedgerRevision = ledger.Revision
                    };
                return true;
            }
        }

        internal static bool TryGetOffer(
            Identity ownerIdentity,
            Identity questIdentity,
            DateTime nowUtc,
            out MissionOfferRecord record)
        {
            record = null;
            if (!IsValidIdentity(ownerIdentity) || !IsValidIdentity(questIdentity))
            {
                return false;
            }

            lock (Sync)
            {
                EnsureInitialized_NoLock();
                OwnerLedger ledger;
                if (!LedgersByOwner.TryGetValue(OwnerKey(ownerIdentity), out ledger))
                {
                    return false;
                }

                int index = FindRecord(ledger, questIdentity);
                if (index < 0)
                {
                    return false;
                }

                MissionOfferRecord candidate = ledger.Records[index];
                if (candidate.LifecycleState != MissionOfferLifecycleState.Pending)
                {
                    return false;
                }

                DateTime normalizedNow = NormalizeUtc(nowUtc);
                if (candidate.ExpiresUtc <= normalizedNow)
                {
                    OwnerLedger expiredLedger = ledger.Clone();
                    Transition(
                        expiredLedger.Records[index],
                        MissionOfferLifecycleState.Expired,
                        normalizedNow,
                        "ExpiredOnLookup",
                        null);
                    expiredLedger.Revision = checked(ledger.Revision + 1);
                    string persistenceFailure;
                    if (TryPersistLedger(
                            expiredLedger,
                            ledger.Revision,
                            out persistenceFailure))
                    {
                        PublishLedger_NoLock(OwnerKey(ownerIdentity), expiredLedger);
                    }

                    return false;
                }

                record = candidate.Snapshot();
                return true;
            }
        }

        internal static bool IsIdentityInUse(int questInstance)
        {
            lock (Sync)
            {
                EnsureInitialized_NoLock();
                return OwnerByOfferInstance.ContainsKey(questInstance);
            }
        }

        internal static List<MissionOfferRecord> Snapshot()
        {
            lock (Sync)
            {
                EnsureInitialized_NoLock();
                var result = new List<MissionOfferRecord>();
                foreach (OwnerLedger ledger in LedgersByOwner.Values)
                {
                    for (int i = 0; i < ledger.Records.Count; i++)
                    {
                        result.Add(ledger.Records[i].Snapshot());
                    }
                }

                result.Sort(
                    delegate(MissionOfferRecord left, MissionOfferRecord right)
                    {
                        return left.Offer.QuestIdentity.Instance.CompareTo(
                            right.Offer.QuestIdentity.Instance);
                    });
                return result;
            }
        }

        internal static string GetLedgerPathForTests(Identity ownerIdentity)
        {
            lock (Sync)
            {
                EnsureInitialized_NoLock();
                return LedgerPath(ownerIdentity);
            }
        }

        private static bool TryMarkAcceptedCore(
            Identity ownerIdentity,
            Identity offerIdentity,
            long expectedRecordRevision,
            MissionAcgIdentityRecord acceptedQuestIdentity,
            DateTime nowUtc,
            bool allowPendingReconciliation,
            out string failure)
        {
            failure = string.Empty;
            if (!IsValidIdentity(ownerIdentity) || !IsValidIdentity(offerIdentity))
            {
                failure = "Exact accepted offer correlation is required.";
                return false;
            }

            lock (Sync)
            {
                EnsureInitialized_NoLock();
                string ownerKey = OwnerKey(ownerIdentity);
                OwnerLedger current;
                if (!LedgersByOwner.TryGetValue(ownerKey, out current))
                {
                    failure = "Accepted offer record is absent.";
                    return false;
                }

                int index = FindRecord(current, offerIdentity);
                if (index < 0)
                {
                    failure = "Accepted offer record is absent.";
                    return false;
                }

                MissionOfferRecord currentRecord = current.Records[index];
                if (currentRecord.LifecycleState == MissionOfferLifecycleState.Accepted)
                {
                    if (IdentityEquals(
                            currentRecord.AcceptedQuestIdentity,
                            acceptedQuestIdentity))
                    {
                        return true;
                    }

                    failure = "Offer is already linked to another accepted quest.";
                    return false;
                }

                if ((!allowPendingReconciliation
                     && (currentRecord.Revision != expectedRecordRevision
                         || currentRecord.LifecycleState
                            != MissionOfferLifecycleState.AcceptanceClaimed))
                    || (allowPendingReconciliation
                        && currentRecord.LifecycleState
                           != MissionOfferLifecycleState.Pending
                        && currentRecord.LifecycleState
                           != MissionOfferLifecycleState.AcceptanceClaimed))
                {
                    failure = "Mission offer acceptance compare-and-swap conflict.";
                    return false;
                }

                foreach (OwnerLedger ledger in LedgersByOwner.Values)
                {
                    for (int i = 0; i < ledger.Records.Count; i++)
                    {
                        MissionOfferRecord other = ledger.Records[i];
                        if (other.LifecycleState == MissionOfferLifecycleState.Accepted
                            && IdentityEquals(
                                other.AcceptedQuestIdentity,
                                acceptedQuestIdentity)
                            && !IdentityEquals(
                                other.Offer.QuestIdentity,
                                offerIdentity))
                        {
                            failure =
                                "Accepted quest identity is already linked to another offer.";
                            return false;
                        }
                    }
                }

                OwnerLedger candidate = current.Clone();
                Transition(
                    candidate.Records[index],
                    MissionOfferLifecycleState.Accepted,
                    NormalizeUtc(nowUtc),
                    "Accepted",
                    acceptedQuestIdentity);
                candidate.Revision = checked(current.Revision + 1);
                if (!TryPersistLedger(candidate, current.Revision, out failure))
                {
                    return false;
                }

                PublishLedger_NoLock(ownerKey, candidate);
                return true;
            }
        }

        private static bool TryCreateRecord(
            Identity ownerIdentity,
            QuestAlternativeMessage response,
            QuestAlternativeMessage request,
            MissionSliderProfile sliders,
            QuestInfo offer,
            int offerIndex,
            DateTime issuedUtc,
            int rollSeed,
            int responseNonce,
            string batchIdentity,
            byte[] payload,
            string payloadHash,
            out MissionOfferRecord record,
            out string failure)
        {
            record = null;
            failure = string.Empty;
            if (offer == null || !IsValidIdentity(offer.QuestIdentity))
            {
                failure = "Rolled offer lacks its exact identity.";
                return false;
            }

            if (offer.QuestActions == null
                || offer.QuestActions.Length == 0
                || offer.QuestActions[0] == null)
            {
                failure = "Rolled offer lacks its exact exterior action.";
                return false;
            }

            if (!IsNonZeroIdentity(response.MissionTerminalIdentity))
            {
                failure = "Rolled offer lacks its exact issuing terminal.";
                return false;
            }

            MissionRollType missionType;
            if (!MissionTypeCatalog.TryTypeFromIcon(
                    offer.MissionIconId,
                    out missionType))
            {
                failure = "Rolled offer has an unsupported mission icon.";
                return false;
            }

            if (offer.ItemRewards != null && offer.ItemRewards.Length > 1)
            {
                failure = "Generated offer has more than one frozen item reward.";
                return false;
            }

            QuestItemShort item =
                offer.ItemRewards != null && offer.ItemRewards.Length == 1
                    ? offer.ItemRewards[0]
                    : null;
            QuestActionList action = offer.QuestActions[0];
            record =
                new MissionOfferRecord
                {
                    OwnerIdentity = CopyIdentity(ownerIdentity),
                    Offer = offer,
                    SerializedRollPayload = (byte[])payload.Clone(),
                    SerializedRollPayloadSha256 = payloadHash,
                    OfferIndex = offerIndex,
                    BatchIdentity = batchIdentity,
                    RollSeed = rollSeed,
                    ResponseNonce = responseNonce,
                    RollFee = 0,
                    IssuedUtc = issuedUtc,
                    ExpiresUtc = issuedUtc.AddSeconds(OfferLifetimeSeconds),
                    UpdatedUtc = issuedUtc,
                    LevelSlider = request.LevelSlider,
                    GoodBadSlider = sliders.GoodBad,
                    OrderChaosSlider = sliders.OrderChaos,
                    OpenHiddenSlider = sliders.OpenHidden,
                    PhysicalMysticalSlider = sliders.PhysicalMystical,
                    HeadOnStealthSlider = sliders.HeadOnStealth,
                    MoneyExperienceSlider = sliders.MoneyExperience,
                    SliderEvidenceProfile = sliders.EvidenceProfile,
                    IssuingTerminalIdentity =
                        CopyIdentity(response.MissionTerminalIdentity),
                    MissionType = missionType,
                    MissionIconId = offer.MissionIconId,
                    MissionQuality = offer.Quality,
                    Title = offer.ShortInfo ?? string.Empty,
                    Description = offer.Info ?? string.Empty,
                    FrozenCashReward = offer.CashReward,
                    FrozenExperienceReward = offer.ExperienceReward,
                    FrozenItemLowId = item == null ? 0 : item.LowId,
                    FrozenItemHighId = item == null ? 0 : item.HighId,
                    FrozenItemQuality = item == null ? 0 : item.Quality,
                    FrozenItemCount = item == null ? 0 : 1,
                    ExteriorEntranceIdentity = CopyIdentity(action.Playfield),
                    ExteriorBuildingLow = action.Unknown18,
                    ExteriorBuildingHigh = action.Unknown19,
                    ExteriorX = action.X,
                    ExteriorY = action.Y,
                    ExteriorZ = action.Z,
                    LifecycleState = MissionOfferLifecycleState.Prepared,
                    Revision = 1,
                    AcceptedQuestIdentity = new Identity(),
                    TransitionReason = "Rolled"
                };

            return ValidateRecord(record, out failure);
        }

        private static bool ValidateLedgerBatches(
            IList<MissionOfferRecord> records,
            out string failure)
        {
            failure = string.Empty;
            var firstByBatch =
                new Dictionary<string, MissionOfferRecord>(StringComparer.Ordinal);
            var indicesByBatch =
                new Dictionary<string, HashSet<int>>(StringComparer.Ordinal);
            var expectedCountByBatch =
                new Dictionary<string, int>(StringComparer.Ordinal);
            var preparedBatches = new HashSet<string>(StringComparer.Ordinal);
            var feePendingBatches = new HashSet<string>(StringComparer.Ordinal);
            var pendingBatches = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < records.Count; i++)
            {
                MissionOfferRecord record = records[i];
                MissionOfferRecord first;
                if (!firstByBatch.TryGetValue(record.BatchIdentity, out first))
                {
                    firstByBatch.Add(record.BatchIdentity, record);
                    indicesByBatch.Add(
                        record.BatchIdentity,
                        new HashSet<int>());
                    QuestAlternativeMessage roll =
                        MissionRollService.DeserializeBody(record.SerializedRollPayload);
                    expectedCountByBatch.Add(
                        record.BatchIdentity,
                        roll.QuestInfos.Length);
                }
                else if (record.RollFee != first.RollFee
                         || record.RollSeed != first.RollSeed
                         || record.ResponseNonce != first.ResponseNonce
                         || record.IssuedUtc != first.IssuedUtc
                         || record.ExpiresUtc != first.ExpiresUtc
                         || !string.Equals(
                             record.SerializedRollPayloadSha256,
                             first.SerializedRollPayloadSha256,
                             StringComparison.OrdinalIgnoreCase))
                {
                    failure =
                        "Mission offer ledger contains conflicting records for one roll batch.";
                    return false;
                }

                if (!indicesByBatch[record.BatchIdentity].Add(record.OfferIndex))
                {
                    failure = "Mission offer ledger contains a duplicate batch offer index.";
                    return false;
                }

                if (record.LifecycleState == MissionOfferLifecycleState.Prepared)
                {
                    preparedBatches.Add(record.BatchIdentity);
                }
                else if (record.LifecycleState
                         == MissionOfferLifecycleState.FeeChargePending)
                {
                    feePendingBatches.Add(record.BatchIdentity);
                }
                else if (record.LifecycleState == MissionOfferLifecycleState.Pending)
                {
                    pendingBatches.Add(record.BatchIdentity);
                }
            }

            foreach (KeyValuePair<string, HashSet<int>> batch in indicesByBatch)
            {
                if (batch.Value.Count != expectedCountByBatch[batch.Key])
                {
                    failure = "Mission offer ledger contains an incomplete roll batch.";
                    return false;
                }
            }

            if (preparedBatches.Count > 1
                || feePendingBatches.Count > 1
                || pendingBatches.Count > 1)
            {
                failure = "Mission offer ledger contains conflicting active roll batches.";
                return false;
            }

            return true;
        }

        private static bool ValidateRecord(
            MissionOfferRecord record,
            out string failure)
        {
            failure = string.Empty;
            if (record == null
                || !IsValidIdentity(record.OwnerIdentity)
                || record.SerializedRollPayload == null
                || record.SerializedRollPayload.Length == 0
                || string.IsNullOrEmpty(record.BatchIdentity)
                || record.Revision <= 0
                || record.ExpiresUtc <= record.IssuedUtc
                || record.UpdatedUtc < record.IssuedUtc
                || !IsFinite(record.ExteriorX)
                || !IsFinite(record.ExteriorY)
                || !IsFinite(record.ExteriorZ))
            {
                failure = "Generated offer record contains an invalid required field.";
                return false;
            }

            if (record.LifecycleState < MissionOfferLifecycleState.Pending
                || record.LifecycleState > MissionOfferLifecycleState.FeeChargePending)
            {
                failure = "Generated offer record has an unknown lifecycle state.";
                return false;
            }

            bool requiresFee =
                record.LifecycleState == MissionOfferLifecycleState.FeeChargePending
                || record.LifecycleState == MissionOfferLifecycleState.Pending
                || record.LifecycleState == MissionOfferLifecycleState.AcceptanceClaimed
                || record.LifecycleState == MissionOfferLifecycleState.Accepted
                || record.LifecycleState == MissionOfferLifecycleState.Expired
                || record.LifecycleState == MissionOfferLifecycleState.Replaced;
            if ((requiresFee && record.RollFee <= 0)
                || (record.LifecycleState == MissionOfferLifecycleState.Prepared
                    && record.RollFee != 0)
                || (record.LifecycleState == MissionOfferLifecycleState.Discarded
                    && record.RollFee < 0))
            {
                failure = "Generated offer record has an invalid durable roll-fee claim.";
                return false;
            }
            if (record.LevelSlider < 0
                || record.LevelSlider > 100
                || !IsSliderValue(record.GoodBadSlider)
                || !IsSliderValue(record.OrderChaosSlider)
                || !IsSliderValue(record.OpenHiddenSlider)
                || !IsSliderValue(record.PhysicalMysticalSlider)
                || !IsSliderValue(record.HeadOnStealthSlider)
                || !IsSliderValue(record.MoneyExperienceSlider)
                || ResolveEvidenceProfile(record) != record.SliderEvidenceProfile)
            {
                failure = "Generated offer record contains invalid persisted request sliders.";
                return false;
            }

            string payloadHash =
                MissionAcgHash.ComputeSha256(record.SerializedRollPayload);
            if (!string.Equals(
                    payloadHash,
                    record.SerializedRollPayloadSha256,
                    StringComparison.OrdinalIgnoreCase))
            {
                failure = "Generated offer roll payload SHA-256 mismatch.";
                return false;
            }

            QuestAlternativeMessage roll;
            try
            {
                roll = MissionRollService.DeserializeBody(record.SerializedRollPayload);
            }
            catch (Exception ex)
            {
                failure = "Generated offer roll payload is malformed: " + ex.Message;
                return false;
            }

            if (roll == null
                || roll.QuestInfos == null
                || record.OfferIndex < 0
                || record.OfferIndex >= roll.QuestInfos.Length
                || roll.QuestInfos[record.OfferIndex] == null)
            {
                failure = "Generated offer index is absent from its roll payload.";
                return false;
            }

            QuestInfo offer = roll.QuestInfos[record.OfferIndex];
            QuestActionList action =
                offer.QuestActions != null && offer.QuestActions.Length > 0
                    ? offer.QuestActions[0]
                    : null;
            QuestItemShort item =
                offer.ItemRewards != null && offer.ItemRewards.Length > 0
                    ? offer.ItemRewards[0]
                    : null;
            MissionRollType missionType;
            if (!MissionTypeCatalog.TryTypeFromIcon(
                    offer.MissionIconId,
                    out missionType)
                || !IdentityEquals(roll.Identity, record.OwnerIdentity)
                || !IdentityEquals(offer.QuestIdentity, record.Offer.QuestIdentity)
                || !IdentityEquals(roll.MissionTerminalIdentity, record.IssuingTerminalIdentity)
                || missionType != record.MissionType
                || offer.MissionIconId != record.MissionIconId
                || offer.Quality != record.MissionQuality
                || !string.Equals(
                    offer.ShortInfo ?? string.Empty,
                    record.Title ?? string.Empty,
                    StringComparison.Ordinal)
                || !string.Equals(
                    offer.Info ?? string.Empty,
                    record.Description ?? string.Empty,
                    StringComparison.Ordinal)
                || offer.CashReward != record.FrozenCashReward
                || offer.ExperienceReward != record.FrozenExperienceReward
                || action == null
                || !IdentityEquals(
                    action.Playfield,
                    record.ExteriorEntranceIdentity)
                || action.Unknown18 != record.ExteriorBuildingLow
                || action.Unknown19 != record.ExteriorBuildingHigh
                || !action.X.Equals(record.ExteriorX)
                || !action.Y.Equals(record.ExteriorY)
                || !action.Z.Equals(record.ExteriorZ))
            {
                failure =
                    "Generated offer semantic fields do not match its serialized roll payload.";
                return false;
            }

            if ((item == null && record.FrozenItemCount != 0)
                || (item != null
                    && (record.FrozenItemCount != 1
                        || item.LowId != record.FrozenItemLowId
                        || item.HighId != record.FrozenItemHighId
                        || item.Quality != record.FrozenItemQuality)))
            {
                failure = "Generated offer frozen item reward does not match its payload.";
                return false;
            }

            if (record.LifecycleState == MissionOfferLifecycleState.Accepted)
            {
                if (!IsValidIdentity(record.AcceptedQuestIdentity))
                {
                    failure = "Accepted offer lacks its accepted quest identity.";
                    return false;
                }
            }
            else if (record.AcceptedQuestIdentity.Instance != 0
                     || (int)record.AcceptedQuestIdentity.Type != 0)
            {
                failure = "Non-accepted offer unexpectedly names an accepted quest.";
                return false;
            }

            return true;
        }

        private static void Transition(
            MissionOfferRecord record,
            MissionOfferLifecycleState state,
            DateTime updatedUtc,
            string reason,
            MissionAcgIdentityRecord acceptedQuestIdentity)
        {
            record.LifecycleState = state;
            record.Revision = checked(record.Revision + 1);
            record.UpdatedUtc = NormalizeUtc(updatedUtc);
            record.TransitionReason = reason ?? string.Empty;
            record.AcceptedQuestIdentity =
                acceptedQuestIdentity == null
                    ? new Identity()
                    : new Identity
                      {
                          Type = (IdentityType)acceptedQuestIdentity.Type,
                          Instance = acceptedQuestIdentity.Instance
                      };
        }

        private static string TerminalFailure(MissionOfferLifecycleState state)
        {
            switch (state)
            {
                case MissionOfferLifecycleState.Accepted:
                    return "Offer has already been accepted.";
                case MissionOfferLifecycleState.Expired:
                    return "Offer has expired.";
                case MissionOfferLifecycleState.Replaced:
                    return "Offer was replaced by a newer roll.";
                case MissionOfferLifecycleState.Discarded:
                    return "Offer was explicitly discarded.";
                default:
                    return "Offer lifecycle does not permit acceptance.";
            }
        }

        private static MissionOfferLifecycleState ResolveReleasedClaimState(
            OwnerLedger ledger,
            MissionOfferRecord claimed,
            DateTime nowUtc)
        {
            if (claimed.ExpiresUtc <= nowUtc)
            {
                return MissionOfferLifecycleState.Expired;
            }

            for (int i = 0; i < ledger.Records.Count; i++)
            {
                MissionOfferRecord candidate = ledger.Records[i];
                if (string.Equals(
                        candidate.BatchIdentity,
                        claimed.BatchIdentity,
                        StringComparison.Ordinal)
                    || candidate.IssuedUtc <= claimed.IssuedUtc
                    || candidate.LifecycleState
                       == MissionOfferLifecycleState.Discarded)
                {
                    continue;
                }

                return MissionOfferLifecycleState.Replaced;
            }

            return MissionOfferLifecycleState.Pending;
        }

        private static int FindRecord(OwnerLedger ledger, Identity offerIdentity)
        {
            for (int i = 0; i < ledger.Records.Count; i++)
            {
                if (IdentityEquals(
                        ledger.Records[i].Offer.QuestIdentity,
                        offerIdentity))
                {
                    return i;
                }
            }

            return -1;
        }

        private static void PublishLedger_NoLock(
            string ownerKey,
            OwnerLedger ledger)
        {
            OwnerLedger previous;
            if (LedgersByOwner.TryGetValue(ownerKey, out previous))
            {
                for (int i = 0; i < previous.Records.Count; i++)
                {
                    OwnerByOfferInstance.Remove(
                        previous.Records[i].Offer.QuestIdentity.Instance);
                }
            }

            LedgersByOwner[ownerKey] = ledger;
            for (int i = 0; i < ledger.Records.Count; i++)
            {
                OwnerByOfferInstance[ledger.Records[i].Offer.QuestIdentity.Instance] =
                    ownerKey;
            }
        }

        private static bool TryPersistLedger(
            OwnerLedger ledger,
            long expectedRevision,
            out string failure)
        {
            failure = string.Empty;
            try
            {
                System.IO.Directory.CreateDirectory(ledgerDirectory);
                string path = LedgerPath(ledger.OwnerIdentity);
                if (File.Exists(path))
                {
                    OwnerLedger current;
                    string readFailure;
                    if (!TryReadLedger(path, out current, out readFailure))
                    {
                        failure =
                            "Existing mission offer ledger is invalid: "
                            + readFailure;
                        return false;
                    }

                    if (current.Revision != expectedRevision)
                    {
                        failure = "Mission offer ledger compare-and-swap conflict.";
                        return false;
                    }
                }
                else if (expectedRevision != 0)
                {
                    failure = "Mission offer ledger disappeared during compare-and-swap.";
                    return false;
                }

                string serialized = SerializeLedger(ledger);
                string temp =
                    path + "." + Guid.NewGuid().ToString("N") + ".tmp";
                string backup = path + ".bak";
                bool committed = false;
                try
                {
                    File.WriteAllText(temp, serialized, new UTF8Encoding(false));
                    using (FileStream stream =
                        new FileStream(
                            temp,
                            FileMode.Open,
                            FileAccess.ReadWrite,
                            FileShare.Read))
                    {
                        stream.Flush(true);
                    }

                    OwnerLedger readback;
                    string readbackFailure;
                    if (!TryReadLedger(temp, out readback, out readbackFailure)
                        || readback.Revision != ledger.Revision
                        || !IdentityEquals(
                            readback.OwnerIdentity,
                            ledger.OwnerIdentity))
                    {
                        failure =
                            "Mission offer ledger atomic readback failed: "
                            + readbackFailure;
                        return false;
                    }

                    if (File.Exists(path))
                    {
                        if (File.Exists(backup))
                        {
                            File.Delete(backup);
                        }

                        File.Replace(temp, path, backup, true);
                        committed = true;
                    }
                    else
                    {
                        File.Move(temp, path);
                        committed = true;
                    }

                    if (File.Exists(backup))
                    {
                        try
                        {
                            File.Delete(backup);
                        }
                        catch
                        {
                            // The atomic replacement is already committed. A cleanup-only
                            // failure must not leave memory behind the durable revision.
                        }
                    }
                }
                finally
                {
                    if (File.Exists(temp))
                    {
                        try
                        {
                            File.Delete(temp);
                        }
                        catch
                        {
                        }
                    }

                    if (committed && File.Exists(backup))
                    {
                        try
                        {
                            File.Delete(backup);
                        }
                        catch
                        {
                        }
                    }
                }

                return true;
            }
            catch (Exception ex)
            {
                failure = "Mission offer ledger persistence failed closed: " + ex.Message;
                return false;
            }
        }

        private static string SerializeLedger(OwnerLedger ledger)
        {
            var fields = new SortedDictionary<string, string>(StringComparer.Ordinal)
                         {
                             { "FormatVersion", CurrentFormatVersion.ToString(CultureInfo.InvariantCulture) },
                             { "LedgerRevision", ledger.Revision.ToString(CultureInfo.InvariantCulture) },
                             { "OwnerInstance", ledger.OwnerIdentity.Instance.ToString(CultureInfo.InvariantCulture) },
                             { "OwnerType", ((int)ledger.OwnerIdentity.Type).ToString(CultureInfo.InvariantCulture) },
                             { "RecordCount", ledger.Records.Count.ToString(CultureInfo.InvariantCulture) }
                         };
            for (int i = 0; i < ledger.Records.Count; i++)
            {
                MissionOfferRecord record = ledger.Records[i];
                string prefix = "Record." + i.ToString("D6", CultureInfo.InvariantCulture) + ".";
                AddRecordFields(fields, prefix, record);
            }

            string canonical = Canonical(fields);
            string hash =
                MissionAcgHash.ComputeSha256(
                    new UTF8Encoding(false).GetBytes(canonical));
            return Header
                   + "\r\n"
                   + canonical
                   + "LedgerSha256="
                   + hash
                   + "\r\n";
        }

        private static void AddRecordFields(
            IDictionary<string, string> fields,
            string prefix,
            MissionOfferRecord record)
        {
            fields.Add(prefix + "AcceptedQuestInstance", IdentityInstance(record.AcceptedQuestIdentity));
            fields.Add(prefix + "AcceptedQuestType", IdentityTypeValue(record.AcceptedQuestIdentity));
            fields.Add(prefix + "BatchIdentity", record.BatchIdentity);
            fields.Add(prefix + "DescriptionBase64", EncodeText(record.Description));
            fields.Add(prefix + "ExpiresUtcTicks", record.ExpiresUtc.Ticks.ToString(CultureInfo.InvariantCulture));
            fields.Add(prefix + "ExteriorBuildingHigh", record.ExteriorBuildingHigh.ToString(CultureInfo.InvariantCulture));
            fields.Add(prefix + "ExteriorBuildingLow", record.ExteriorBuildingLow.ToString(CultureInfo.InvariantCulture));
            fields.Add(prefix + "ExteriorEntranceInstance", record.ExteriorEntranceIdentity.Instance.ToString(CultureInfo.InvariantCulture));
            fields.Add(prefix + "ExteriorEntranceType", ((int)record.ExteriorEntranceIdentity.Type).ToString(CultureInfo.InvariantCulture));
            fields.Add(prefix + "ExteriorX", record.ExteriorX.ToString("R", CultureInfo.InvariantCulture));
            fields.Add(prefix + "ExteriorY", record.ExteriorY.ToString("R", CultureInfo.InvariantCulture));
            fields.Add(prefix + "ExteriorZ", record.ExteriorZ.ToString("R", CultureInfo.InvariantCulture));
            fields.Add(prefix + "FrozenCashReward", record.FrozenCashReward.ToString(CultureInfo.InvariantCulture));
            fields.Add(prefix + "FrozenExperienceReward", record.FrozenExperienceReward.ToString(CultureInfo.InvariantCulture));
            fields.Add(prefix + "FrozenItemCount", record.FrozenItemCount.ToString(CultureInfo.InvariantCulture));
            fields.Add(prefix + "FrozenItemHighId", record.FrozenItemHighId.ToString(CultureInfo.InvariantCulture));
            fields.Add(prefix + "FrozenItemLowId", record.FrozenItemLowId.ToString(CultureInfo.InvariantCulture));
            fields.Add(prefix + "FrozenItemQuality", record.FrozenItemQuality.ToString(CultureInfo.InvariantCulture));
            fields.Add(prefix + "GoodBadSlider", record.GoodBadSlider.ToString(CultureInfo.InvariantCulture));
            fields.Add(prefix + "HeadOnStealthSlider", record.HeadOnStealthSlider.ToString(CultureInfo.InvariantCulture));
            fields.Add(prefix + "IssuedUtcTicks", record.IssuedUtc.Ticks.ToString(CultureInfo.InvariantCulture));
            fields.Add(prefix + "IssuingTerminalInstance", record.IssuingTerminalIdentity.Instance.ToString(CultureInfo.InvariantCulture));
            fields.Add(prefix + "IssuingTerminalType", ((int)record.IssuingTerminalIdentity.Type).ToString(CultureInfo.InvariantCulture));
            fields.Add(prefix + "LevelSlider", record.LevelSlider.ToString(CultureInfo.InvariantCulture));
            fields.Add(prefix + "LifecycleState", ((int)record.LifecycleState).ToString(CultureInfo.InvariantCulture));
            fields.Add(prefix + "MissionIconId", record.MissionIconId.ToString(CultureInfo.InvariantCulture));
            fields.Add(prefix + "MissionQuality", record.MissionQuality.ToString(CultureInfo.InvariantCulture));
            fields.Add(prefix + "MissionType", ((int)record.MissionType).ToString(CultureInfo.InvariantCulture));
            fields.Add(prefix + "MoneyExperienceSlider", record.MoneyExperienceSlider.ToString(CultureInfo.InvariantCulture));
            fields.Add(prefix + "OfferIndex", record.OfferIndex.ToString(CultureInfo.InvariantCulture));
            fields.Add(prefix + "OfferInstance", record.Offer.QuestIdentity.Instance.ToString(CultureInfo.InvariantCulture));
            fields.Add(prefix + "OfferType", ((int)record.Offer.QuestIdentity.Type).ToString(CultureInfo.InvariantCulture));
            fields.Add(prefix + "OpenHiddenSlider", record.OpenHiddenSlider.ToString(CultureInfo.InvariantCulture));
            fields.Add(prefix + "OrderChaosSlider", record.OrderChaosSlider.ToString(CultureInfo.InvariantCulture));
            fields.Add(prefix + "OwnerInstance", record.OwnerIdentity.Instance.ToString(CultureInfo.InvariantCulture));
            fields.Add(prefix + "OwnerType", ((int)record.OwnerIdentity.Type).ToString(CultureInfo.InvariantCulture));
            fields.Add(prefix + "PhysicalMysticalSlider", record.PhysicalMysticalSlider.ToString(CultureInfo.InvariantCulture));
            fields.Add(prefix + "ResponseNonce", record.ResponseNonce.ToString(CultureInfo.InvariantCulture));
            fields.Add(prefix + "RollFee", record.RollFee.ToString(CultureInfo.InvariantCulture));
            fields.Add(prefix + "Revision", record.Revision.ToString(CultureInfo.InvariantCulture));
            fields.Add(prefix + "RollPayloadBase64", Convert.ToBase64String(record.SerializedRollPayload));
            fields.Add(prefix + "RollPayloadSha256", record.SerializedRollPayloadSha256);
            fields.Add(prefix + "RollSeed", record.RollSeed.ToString(CultureInfo.InvariantCulture));
            fields.Add(prefix + "SliderEvidenceProfile", ((int)record.SliderEvidenceProfile).ToString(CultureInfo.InvariantCulture));
            fields.Add(prefix + "TitleBase64", EncodeText(record.Title));
            fields.Add(prefix + "TransitionReasonBase64", EncodeText(record.TransitionReason));
            fields.Add(prefix + "UpdatedUtcTicks", record.UpdatedUtc.Ticks.ToString(CultureInfo.InvariantCulture));
        }

        private static bool TryReadLedger(
            string path,
            out OwnerLedger ledger,
            out string failure)
        {
            ledger = null;
            failure = string.Empty;
            try
            {
                string[] lines = File.ReadAllLines(path, Encoding.UTF8);
                if (lines.Length < 3 || !string.Equals(lines[0], Header, StringComparison.Ordinal))
                {
                    failure = "Missing or malformed mission offer ledger header.";
                    return false;
                }

                var fields = new Dictionary<string, string>(StringComparer.Ordinal);
                for (int i = 1; i < lines.Length; i++)
                {
                    if (lines[i].Length == 0)
                    {
                        failure = "Mission offer ledger contains an empty line.";
                        return false;
                    }

                    int equals = lines[i].IndexOf('=');
                    if (equals <= 0)
                    {
                        failure = "Mission offer ledger contains a malformed field.";
                        return false;
                    }

                    string key = lines[i].Substring(0, equals);
                    string value = lines[i].Substring(equals + 1);
                    if (fields.ContainsKey(key))
                    {
                        failure = "Mission offer ledger contains duplicate field " + key + ".";
                        return false;
                    }

                    fields.Add(key, value);
                }

                string expectedHash;
                if (!fields.TryGetValue("LedgerSha256", out expectedHash))
                {
                    failure = "Mission offer ledger lacks its SHA-256 field.";
                    return false;
                }

                fields.Remove("LedgerSha256");
                var sorted = new SortedDictionary<string, string>(fields, StringComparer.Ordinal);
                string actualHash =
                    MissionAcgHash.ComputeSha256(
                        new UTF8Encoding(false).GetBytes(Canonical(sorted)));
                if (!string.Equals(
                        expectedHash,
                        actualHash,
                        StringComparison.OrdinalIgnoreCase))
                {
                    failure = "Mission offer ledger SHA-256 mismatch.";
                    return false;
                }

                var consumed = new HashSet<string>(StringComparer.Ordinal);
                int version = ParseInt(Required(fields, consumed, "FormatVersion"), "FormatVersion");
                if (version != CurrentFormatVersion)
                {
                    failure = "Unknown mission offer ledger version " + version + ".";
                    return false;
                }

                var owner = new Identity
                            {
                                Type = (IdentityType)ParseInt(Required(fields, consumed, "OwnerType"), "OwnerType"),
                                Instance = ParseInt(Required(fields, consumed, "OwnerInstance"), "OwnerInstance")
                            };
                long revision = ParseLong(Required(fields, consumed, "LedgerRevision"), "LedgerRevision");
                int count = ParseInt(Required(fields, consumed, "RecordCount"), "RecordCount");
                if (!IsValidIdentity(owner) || revision <= 0 || count < 0 || count > 100000)
                {
                    failure = "Mission offer ledger owner, revision, or record count is invalid.";
                    return false;
                }

                var records = new List<MissionOfferRecord>(count);
                var identities = new HashSet<int>();
                for (int i = 0; i < count; i++)
                {
                    string prefix = "Record." + i.ToString("D6", CultureInfo.InvariantCulture) + ".";
                    MissionOfferRecord record = ParseRecord(fields, consumed, prefix, owner);
                    string recordFailure;
                    if (!ValidateRecord(record, out recordFailure))
                    {
                        failure = "Invalid mission offer record " + i + ": " + recordFailure;
                        return false;
                    }

                    if (!identities.Add(record.Offer.QuestIdentity.Instance))
                    {
                        failure = "Mission offer ledger contains duplicate offer identities.";
                        return false;
                    }

                    records.Add(record);
                }

                string batchFailure;
                if (!ValidateLedgerBatches(records, out batchFailure))
                {
                    failure = batchFailure;
                    return false;
                }

                if (consumed.Count != fields.Count)
                {
                    failure = "Mission offer ledger contains unexpected fields.";
                    return false;
                }

                ledger =
                    new OwnerLedger
                    {
                        OwnerIdentity = owner,
                        Revision = revision,
                        Records = records
                    };
                string expectedName = LedgerFileName(owner);
                if (path.EndsWith(FileExtension, StringComparison.OrdinalIgnoreCase)
                    && !string.Equals(
                        Path.GetFileName(path),
                        expectedName,
                        StringComparison.OrdinalIgnoreCase))
                {
                    failure = "Mission offer ledger filename does not match its exact owner.";
                    ledger = null;
                    return false;
                }

                return true;
            }
            catch (Exception ex)
            {
                failure = "Mission offer ledger is malformed or truncated: " + ex.Message;
                return false;
            }
        }

        private static MissionOfferRecord ParseRecord(
            IDictionary<string, string> fields,
            ISet<string> consumed,
            string prefix,
            Identity ledgerOwner)
        {
            int offerType = ParseInt(Required(fields, consumed, prefix + "OfferType"), prefix + "OfferType");
            int offerInstance = ParseInt(Required(fields, consumed, prefix + "OfferInstance"), prefix + "OfferInstance");
            byte[] payload = Convert.FromBase64String(Required(fields, consumed, prefix + "RollPayloadBase64"));
            int offerIndex = ParseInt(Required(fields, consumed, prefix + "OfferIndex"), prefix + "OfferIndex");
            QuestAlternativeMessage roll = MissionRollService.DeserializeBody(payload);
            QuestInfo offer = roll.QuestInfos[offerIndex];
            if ((int)offer.QuestIdentity.Type != offerType
                || offer.QuestIdentity.Instance != offerInstance)
            {
                throw new InvalidDataException(
                    "Offer identity does not match selected roll payload entry.");
            }

            int acceptedType = ParseInt(Required(fields, consumed, prefix + "AcceptedQuestType"), prefix + "AcceptedQuestType");
            int acceptedInstance = ParseInt(Required(fields, consumed, prefix + "AcceptedQuestInstance"), prefix + "AcceptedQuestInstance");
            Identity accepted =
                acceptedType == 0 && acceptedInstance == 0
                    ? new Identity()
                    : new Identity
                      {
                          Type = (IdentityType)acceptedType,
                          Instance = acceptedInstance
                      };

            return new MissionOfferRecord
                   {
                       OwnerIdentity =
                           new Identity
                           {
                               Type = (IdentityType)ParseInt(Required(fields, consumed, prefix + "OwnerType"), prefix + "OwnerType"),
                               Instance = ParseInt(Required(fields, consumed, prefix + "OwnerInstance"), prefix + "OwnerInstance")
                           },
                       Offer = offer,
                       SerializedRollPayload = payload,
                       SerializedRollPayloadSha256 = Required(fields, consumed, prefix + "RollPayloadSha256"),
                       OfferIndex = offerIndex,
                       BatchIdentity = Required(fields, consumed, prefix + "BatchIdentity"),
                       RollSeed = ParseInt(Required(fields, consumed, prefix + "RollSeed"), prefix + "RollSeed"),
                       ResponseNonce = ParseInt(Required(fields, consumed, prefix + "ResponseNonce"), prefix + "ResponseNonce"),
                       RollFee = ParseInt(Required(fields, consumed, prefix + "RollFee"), prefix + "RollFee"),
                       IssuedUtc = ParseUtc(Required(fields, consumed, prefix + "IssuedUtcTicks"), prefix + "IssuedUtcTicks"),
                       ExpiresUtc = ParseUtc(Required(fields, consumed, prefix + "ExpiresUtcTicks"), prefix + "ExpiresUtcTicks"),
                       UpdatedUtc = ParseUtc(Required(fields, consumed, prefix + "UpdatedUtcTicks"), prefix + "UpdatedUtcTicks"),
                       LevelSlider = ParseInt(Required(fields, consumed, prefix + "LevelSlider"), prefix + "LevelSlider"),
                       GoodBadSlider = ParseInt(Required(fields, consumed, prefix + "GoodBadSlider"), prefix + "GoodBadSlider"),
                       OrderChaosSlider = ParseInt(Required(fields, consumed, prefix + "OrderChaosSlider"), prefix + "OrderChaosSlider"),
                       OpenHiddenSlider = ParseInt(Required(fields, consumed, prefix + "OpenHiddenSlider"), prefix + "OpenHiddenSlider"),
                       PhysicalMysticalSlider = ParseInt(Required(fields, consumed, prefix + "PhysicalMysticalSlider"), prefix + "PhysicalMysticalSlider"),
                       HeadOnStealthSlider = ParseInt(Required(fields, consumed, prefix + "HeadOnStealthSlider"), prefix + "HeadOnStealthSlider"),
                       MoneyExperienceSlider = ParseInt(Required(fields, consumed, prefix + "MoneyExperienceSlider"), prefix + "MoneyExperienceSlider"),
                       SliderEvidenceProfile = (MissionSliderEvidenceProfile)ParseInt(Required(fields, consumed, prefix + "SliderEvidenceProfile"), prefix + "SliderEvidenceProfile"),
                       IssuingTerminalIdentity =
                           new Identity
                           {
                               Type = (IdentityType)ParseInt(Required(fields, consumed, prefix + "IssuingTerminalType"), prefix + "IssuingTerminalType"),
                               Instance = ParseInt(Required(fields, consumed, prefix + "IssuingTerminalInstance"), prefix + "IssuingTerminalInstance")
                           },
                       MissionType = (MissionRollType)ParseInt(Required(fields, consumed, prefix + "MissionType"), prefix + "MissionType"),
                       MissionIconId = ParseInt(Required(fields, consumed, prefix + "MissionIconId"), prefix + "MissionIconId"),
                       MissionQuality = ParseInt(Required(fields, consumed, prefix + "MissionQuality"), prefix + "MissionQuality"),
                       Title = DecodeText(Required(fields, consumed, prefix + "TitleBase64")),
                       Description = DecodeText(Required(fields, consumed, prefix + "DescriptionBase64")),
                       FrozenCashReward = ParseInt(Required(fields, consumed, prefix + "FrozenCashReward"), prefix + "FrozenCashReward"),
                       FrozenExperienceReward = ParseInt(Required(fields, consumed, prefix + "FrozenExperienceReward"), prefix + "FrozenExperienceReward"),
                       FrozenItemLowId = ParseInt(Required(fields, consumed, prefix + "FrozenItemLowId"), prefix + "FrozenItemLowId"),
                       FrozenItemHighId = ParseInt(Required(fields, consumed, prefix + "FrozenItemHighId"), prefix + "FrozenItemHighId"),
                       FrozenItemQuality = ParseInt(Required(fields, consumed, prefix + "FrozenItemQuality"), prefix + "FrozenItemQuality"),
                       FrozenItemCount = ParseInt(Required(fields, consumed, prefix + "FrozenItemCount"), prefix + "FrozenItemCount"),
                       ExteriorEntranceIdentity =
                           new Identity
                           {
                               Type = (IdentityType)ParseInt(Required(fields, consumed, prefix + "ExteriorEntranceType"), prefix + "ExteriorEntranceType"),
                               Instance = ParseInt(Required(fields, consumed, prefix + "ExteriorEntranceInstance"), prefix + "ExteriorEntranceInstance")
                           },
                       ExteriorBuildingLow = ParseInt(Required(fields, consumed, prefix + "ExteriorBuildingLow"), prefix + "ExteriorBuildingLow"),
                       ExteriorBuildingHigh = ParseInt(Required(fields, consumed, prefix + "ExteriorBuildingHigh"), prefix + "ExteriorBuildingHigh"),
                       ExteriorX = ParseFloat(Required(fields, consumed, prefix + "ExteriorX"), prefix + "ExteriorX"),
                       ExteriorY = ParseFloat(Required(fields, consumed, prefix + "ExteriorY"), prefix + "ExteriorY"),
                       ExteriorZ = ParseFloat(Required(fields, consumed, prefix + "ExteriorZ"), prefix + "ExteriorZ"),
                       LifecycleState = (MissionOfferLifecycleState)ParseInt(Required(fields, consumed, prefix + "LifecycleState"), prefix + "LifecycleState"),
                       Revision = ParseLong(Required(fields, consumed, prefix + "Revision"), prefix + "Revision"),
                       AcceptedQuestIdentity = accepted,
                       TransitionReason = DecodeText(Required(fields, consumed, prefix + "TransitionReasonBase64"))
                   };
        }

        private static string Required(
            IDictionary<string, string> fields,
            ISet<string> consumed,
            string key)
        {
            string value;
            if (!fields.TryGetValue(key, out value))
            {
                throw new InvalidDataException("Missing required field " + key + ".");
            }

            consumed.Add(key);
            return value;
        }

        private static string Canonical(
            IEnumerable<KeyValuePair<string, string>> fields)
        {
            var builder = new StringBuilder();
            foreach (KeyValuePair<string, string> field in fields)
            {
                builder.Append(field.Key);
                builder.Append('=');
                builder.Append(field.Value ?? string.Empty);
                builder.Append("\r\n");
            }

            return builder.ToString();
        }

        private static string ComputeBatchIdentity(
            Identity ownerIdentity,
            int rollSeed,
            int responseNonce,
            DateTime issuedUtc,
            string payloadHash)
        {
            string material =
                ((int)ownerIdentity.Type).ToString(CultureInfo.InvariantCulture)
                + "|"
                + ownerIdentity.Instance.ToString(CultureInfo.InvariantCulture)
                + "|"
                + rollSeed.ToString(CultureInfo.InvariantCulture)
                + "|"
                + responseNonce.ToString(CultureInfo.InvariantCulture)
                + "|"
                + issuedUtc.Ticks.ToString(CultureInfo.InvariantCulture)
                + "|"
                + payloadHash;
            return MissionAcgHash.ComputeSha256(
                new UTF8Encoding(false).GetBytes(material));
        }

        private static string EncodeText(string value)
        {
            return Convert.ToBase64String(
                new UTF8Encoding(false).GetBytes(value ?? string.Empty));
        }

        private static string DecodeText(string value)
        {
            return new UTF8Encoding(false).GetString(Convert.FromBase64String(value));
        }

        private static int ParseInt(string value, string field)
        {
            int parsed;
            if (!int.TryParse(
                    value,
                    NumberStyles.Integer,
                    CultureInfo.InvariantCulture,
                    out parsed))
            {
                throw new InvalidDataException("Invalid integer field " + field + ".");
            }

            return parsed;
        }

        private static long ParseLong(string value, string field)
        {
            long parsed;
            if (!long.TryParse(
                    value,
                    NumberStyles.Integer,
                    CultureInfo.InvariantCulture,
                    out parsed))
            {
                throw new InvalidDataException("Invalid long field " + field + ".");
            }

            return parsed;
        }

        private static float ParseFloat(string value, string field)
        {
            float parsed;
            if (!float.TryParse(
                    value,
                    NumberStyles.Float,
                    CultureInfo.InvariantCulture,
                    out parsed)
                || !IsFinite(parsed))
            {
                throw new InvalidDataException("Invalid float field " + field + ".");
            }

            return parsed;
        }

        private static DateTime ParseUtc(string value, string field)
        {
            long ticks = ParseLong(value, field);
            return new DateTime(ticks, DateTimeKind.Utc);
        }

        private static bool IsSliderValue(int value)
        {
            return value >= -100 && value <= 100;
        }

        private static MissionSliderEvidenceProfile ResolveEvidenceProfile(
            MissionOfferRecord record)
        {
            if (record.GoodBadSlider == 0
                && record.OrderChaosSlider == 0
                && record.OpenHiddenSlider == 0
                && record.PhysicalMysticalSlider == 0
                && record.HeadOnStealthSlider == 0
                && record.MoneyExperienceSlider == 0)
            {
                return MissionSliderEvidenceProfile.Neutral;
            }

            if (record.GoodBadSlider == -100
                && record.OrderChaosSlider == -100
                && record.OpenHiddenSlider == 0
                && record.PhysicalMysticalSlider == 0
                && record.HeadOnStealthSlider == 0
                && record.MoneyExperienceSlider == -100)
            {
                return MissionSliderEvidenceProfile.CapturedLeftGoodBadOrderChaosCreditsXp;
            }

            return MissionSliderEvidenceProfile.Unresolved;
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }

        private static DateTime NormalizeUtc(DateTime value)
        {
            return value.Kind == DateTimeKind.Utc ? value : value.ToUniversalTime();
        }

        private static bool IsValidIdentity(Identity identity)
        {
            return (int)identity.Type > 0
                   && identity.Instance > 0;
        }

        private static bool IsNonZeroIdentity(Identity identity)
        {
            return (int)identity.Type != 0 && identity.Instance != 0;
        }

        internal static Identity CopyIdentity(Identity identity)
        {
            return new Identity
                   {
                       Type = identity.Type,
                       Instance = identity.Instance
                   };
        }

        private static bool IdentityEquals(Identity left, Identity right)
        {
            return (int)left.Type == (int)right.Type
                   && left.Instance == right.Instance;
        }

        private static bool IdentityEquals(
            Identity left,
            MissionAcgIdentityRecord right)
        {
            return right != null
                   && (int)left.Type == right.Type
                   && left.Instance == right.Instance;
        }

        private static string IdentityTypeValue(Identity identity)
        {
            return ((int)identity.Type).ToString(CultureInfo.InvariantCulture);
        }

        private static string IdentityInstance(Identity identity)
        {
            return identity.Instance.ToString(CultureInfo.InvariantCulture);
        }

        private static string OwnerKey(Identity ownerIdentity)
        {
            return ((int)ownerIdentity.Type).ToString(CultureInfo.InvariantCulture)
                   + ":"
                   + ownerIdentity.Instance.ToString(CultureInfo.InvariantCulture);
        }

        private static string LedgerFileName(Identity ownerIdentity)
        {
            return "owner-"
                   + ((int)ownerIdentity.Type).ToString(CultureInfo.InvariantCulture)
                   + "-"
                   + ownerIdentity.Instance.ToString(CultureInfo.InvariantCulture)
                   + FileExtension;
        }

        private static string LedgerPath(Identity ownerIdentity)
        {
            return Path.Combine(ledgerDirectory, LedgerFileName(ownerIdentity));
        }

        private static void EnsureInitialized_NoLock()
        {
            if (initialized)
            {
                return;
            }

            Initialize(MissionStateDirectory.Resolve());
        }

        private sealed class OwnerLedger
        {
            internal Identity OwnerIdentity;

            internal long Revision;

            internal List<MissionOfferRecord> Records;

            internal OwnerLedger Clone()
            {
                var records = new List<MissionOfferRecord>(this.Records.Count);
                for (int i = 0; i < this.Records.Count; i++)
                {
                    records.Add(this.Records[i].Snapshot());
                }

                return new OwnerLedger
                       {
                           OwnerIdentity = CopyIdentity(this.OwnerIdentity),
                           Revision = this.Revision,
                           Records = records
                       };
            }
        }
    }
}
