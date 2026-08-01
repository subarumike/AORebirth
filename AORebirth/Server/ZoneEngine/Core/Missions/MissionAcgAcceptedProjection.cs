namespace ZoneEngine.Core.Missions
{
    #region Usings ...

    using System;

    using SmokeLounge.AOtomation.Messaging.GameData;
    using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;

    #endregion

    /// <summary>
    /// Durable checkpoints for generated-mission acceptance. The persisted projection is written
    /// before each externally visible acceptance effect so an interrupted acceptance can resume
    /// without allocating a second quest, key, artifact, or PF2.
    /// </summary>
    internal enum MissionAcgAcceptancePhase
    {
        OfferClaimed = 1,
        BindingPersisted = 2,
        ObjectivePersisted = 3,
        KeyGrantPending = 4,
        KeyGranted = 5,
        ArtifactGrantPending = 6,
        ArtifactsGranted = 7,
        ObjectiveExposed = 8,
        AcceptanceCommitted = 9,
        QfuPending = 10,
        QfuSent = 11
    }

    /// <summary>
    /// Immutable accepted generated-mission projection. The complete selected roll body is retained
    /// as the authoritative source of every QuestInfo field, including opaque action fields. Explicit
    /// semantic fields are cross-checked against that body when the record is constructed or loaded.
    /// </summary>
    internal sealed class MissionAcgAcceptedProjection
    {
        internal const int CurrentFormatVersion = 1;

        private readonly byte[] selectedRollBody;

        internal MissionAcgAcceptedProjection(
            int formatVersion,
            MissionAcgInstanceBinding binding,
            byte[] selectedRollBody,
            string selectedRollBodySha256,
            int selectedOfferIndex,
            byte rawLevelSlider,
            int goodBadSlider,
            int orderChaosSlider,
            int openHiddenSlider,
            int physicalMysticalSlider,
            int headOnStealthSlider,
            int moneyExperienceSlider,
            DateTime offeredUtc,
            DateTime offerExpiryUtc,
            int missionIconId,
            string title,
            string description,
            int frozenCashReward,
            int frozenExperienceReward,
            int frozenItemLowId,
            int frozenItemHighId,
            int frozenItemQuality,
            int frozenItemCount,
            int qfuVersion,
            int qfuQuestIdentityFlag,
            MissionAcgAcceptancePhase acceptancePhase,
            MissionAcgIdentityRecord runtimeObjectiveIdentity,
            MissionAcgIdentityRecord missionArtifactIdentity,
            int repairArtifactLowId,
            int repairArtifactHighId,
            MissionAcgLifecycleState lifecycleState,
            MissionAcgCleanupState cleanupState,
            DateTime updatedUtc)
        {
            if (formatVersion != CurrentFormatVersion)
            {
                throw new ArgumentOutOfRangeException("formatVersion");
            }

            if (binding == null)
            {
                throw new ArgumentNullException("binding");
            }

            if (selectedRollBody == null || selectedRollBody.Length == 0)
            {
                throw new ArgumentException(
                    "The exact selected mission-roll body is required.",
                    "selectedRollBody");
            }

            if (!IsSha256(selectedRollBodySha256))
            {
                throw new ArgumentException(
                    "The selected mission-roll SHA-256 is required.",
                    "selectedRollBodySha256");
            }

            if (!string.Equals(
                    MissionAcgHash.ComputeSha256(selectedRollBody),
                    selectedRollBodySha256,
                    StringComparison.OrdinalIgnoreCase))
            {
                throw new ArgumentException(
                    "The selected mission-roll body does not match its SHA-256.",
                    "selectedRollBodySha256");
            }

            if (selectedOfferIndex < 0)
            {
                throw new ArgumentOutOfRangeException("selectedOfferIndex");
            }

            RequireSignedSlider(goodBadSlider, "goodBadSlider");
            RequireSignedSlider(orderChaosSlider, "orderChaosSlider");
            RequireSignedSlider(openHiddenSlider, "openHiddenSlider");
            RequireSignedSlider(physicalMysticalSlider, "physicalMysticalSlider");
            RequireSignedSlider(headOnStealthSlider, "headOnStealthSlider");
            RequireSignedSlider(moneyExperienceSlider, "moneyExperienceSlider");

            DateTime offered = RequireUtc(offeredUtc, "offeredUtc");
            DateTime offerExpiry = RequireUtc(offerExpiryUtc, "offerExpiryUtc");
            DateTime updated = RequireUtc(updatedUtc, "updatedUtc");
            if (offerExpiry <= offered || binding.AcceptedUtc < offered
                || binding.AcceptedUtc >= offerExpiry)
            {
                throw new ArgumentException(
                    "The accepted mission must be claimed during the exact offer lifetime.",
                    "offerExpiryUtc");
            }

            if (!Enum.IsDefined(typeof(MissionAcgAcceptancePhase), acceptancePhase)
                || !Enum.IsDefined(typeof(MissionAcgLifecycleState), lifecycleState)
                || !Enum.IsDefined(typeof(MissionAcgCleanupState), cleanupState))
            {
                throw new ArgumentException("Accepted mission state is invalid.");
            }

            if (runtimeObjectiveIdentity != null)
            {
                RequireIdentity(runtimeObjectiveIdentity, "runtimeObjectiveIdentity");
                int encodedPlayfield;
                int ordinal;
                if (!MissionAcgRuntimeMaterializer.TryReverseRuntimeInstance(
                        runtimeObjectiveIdentity.Instance,
                        out encodedPlayfield,
                        out ordinal)
                    || encodedPlayfield != binding.AllocatedLivePlayfield2
                    || ordinal <= 0)
                {
                    throw new ArgumentException(
                        "Runtime objective identity does not belong to the allocated PF2.",
                        "runtimeObjectiveIdentity");
                }
            }

            if (missionArtifactIdentity != null)
            {
                RequireIdentity(missionArtifactIdentity, "missionArtifactIdentity");
            }

            if ((int)acceptancePhase >= (int)MissionAcgAcceptancePhase.ObjectivePersisted
                && runtimeObjectiveIdentity == null)
            {
                throw new ArgumentException(
                    "An objective-persisted acceptance requires its exact runtime objective.",
                    "runtimeObjectiveIdentity");
            }

            if (binding.MissionType == MissionRollType.RepairMachine
                && (int)acceptancePhase >= (int)MissionAcgAcceptancePhase.ArtifactsGranted
                && missionArtifactIdentity == null)
            {
                throw new ArgumentException(
                    "A repair acceptance with granted artifacts requires the exact component identity.",
                    "missionArtifactIdentity");
            }

            if (binding.MissionType == MissionRollType.RepairMachine)
            {
                if (repairArtifactLowId <= 0 || repairArtifactHighId <= 0)
                {
                    throw new ArgumentOutOfRangeException(
                        "A repair acceptance requires positive frozen component template identities.");
                }
            }
            else if (repairArtifactLowId != 0 || repairArtifactHighId != 0)
            {
                throw new ArgumentException(
                    "Only repair acceptances may contain frozen component template identities.");
            }

            if (frozenCashReward < 0 || frozenExperienceReward < 0
                || frozenItemLowId < 0 || frozenItemHighId < 0
                || frozenItemQuality < 0 || frozenItemCount < 0
                || frozenItemCount > 1)
            {
                throw new ArgumentOutOfRangeException(
                    "Frozen accepted reward values are invalid.");
            }

            if (frozenItemCount == 0
                && (frozenItemLowId != 0 || frozenItemHighId != 0 || frozenItemQuality != 0))
            {
                throw new ArgumentException(
                    "An explicit no-item reward cannot contain item values.");
            }

            this.FormatVersion = formatVersion;
            this.Binding = binding;
            this.selectedRollBody = (byte[])selectedRollBody.Clone();
            this.SelectedRollBodySha256 = selectedRollBodySha256.ToLowerInvariant();
            this.SelectedOfferIndex = selectedOfferIndex;
            this.RawLevelSlider = rawLevelSlider;
            this.GoodBadSlider = goodBadSlider;
            this.OrderChaosSlider = orderChaosSlider;
            this.OpenHiddenSlider = openHiddenSlider;
            this.PhysicalMysticalSlider = physicalMysticalSlider;
            this.HeadOnStealthSlider = headOnStealthSlider;
            this.MoneyExperienceSlider = moneyExperienceSlider;
            this.OfferedUtc = offered;
            this.OfferExpiryUtc = offerExpiry;
            this.MissionIconId = missionIconId;
            this.Title = title ?? string.Empty;
            this.Description = description ?? string.Empty;
            this.FrozenCashReward = frozenCashReward;
            this.FrozenExperienceReward = frozenExperienceReward;
            this.FrozenItemLowId = frozenItemLowId;
            this.FrozenItemHighId = frozenItemHighId;
            this.FrozenItemQuality = frozenItemQuality;
            this.FrozenItemCount = frozenItemCount;
            this.QfuVersion = qfuVersion;
            this.QfuQuestIdentityFlag = qfuQuestIdentityFlag;
            this.AcceptancePhase = acceptancePhase;
            this.RuntimeObjectiveIdentity = runtimeObjectiveIdentity;
            this.MissionArtifactIdentity = missionArtifactIdentity;
            this.RepairArtifactLowId = repairArtifactLowId;
            this.RepairArtifactHighId = repairArtifactHighId;
            this.LifecycleState = lifecycleState;
            this.CleanupState = cleanupState;
            this.UpdatedUtc = updated;

            this.ValidateSourceProjection();
        }

        internal int FormatVersion { get; private set; }

        internal MissionAcgInstanceBinding Binding { get; private set; }

        internal byte[] SelectedRollBody
        {
            get { return (byte[])this.selectedRollBody.Clone(); }
        }

        internal string SelectedRollBodySha256 { get; private set; }

        internal int SelectedOfferIndex { get; private set; }

        internal byte RawLevelSlider { get; private set; }

        internal int GoodBadSlider { get; private set; }

        internal int OrderChaosSlider { get; private set; }

        internal int OpenHiddenSlider { get; private set; }

        internal int PhysicalMysticalSlider { get; private set; }

        internal int HeadOnStealthSlider { get; private set; }

        internal int MoneyExperienceSlider { get; private set; }

        internal DateTime OfferedUtc { get; private set; }

        internal DateTime OfferExpiryUtc { get; private set; }

        internal int MissionIconId { get; private set; }

        internal string Title { get; private set; }

        internal string Description { get; private set; }

        internal int FrozenCashReward { get; private set; }

        internal int FrozenExperienceReward { get; private set; }

        internal int FrozenItemLowId { get; private set; }

        internal int FrozenItemHighId { get; private set; }

        internal int FrozenItemQuality { get; private set; }

        internal int FrozenItemCount { get; private set; }

        internal int QfuVersion { get; private set; }

        internal int QfuQuestIdentityFlag { get; private set; }

        internal MissionAcgAcceptancePhase AcceptancePhase { get; private set; }

        internal MissionAcgIdentityRecord RuntimeObjectiveIdentity { get; private set; }

        internal MissionAcgIdentityRecord MissionArtifactIdentity { get; private set; }

        internal int RepairArtifactLowId { get; private set; }

        internal int RepairArtifactHighId { get; private set; }

        internal MissionAcgLifecycleState LifecycleState { get; private set; }

        internal MissionAcgCleanupState CleanupState { get; private set; }

        internal DateTime UpdatedUtc { get; private set; }

        internal static MissionAcgAcceptedProjection Create(
            MissionAcgInstanceBinding binding,
            byte[] selectedRollBody,
            int selectedOfferIndex,
            byte rawLevelSlider,
            int goodBadSlider,
            int orderChaosSlider,
            int openHiddenSlider,
            int physicalMysticalSlider,
            int headOnStealthSlider,
            int moneyExperienceSlider,
            DateTime offeredUtc,
            DateTime offerExpiryUtc,
            int qfuVersion,
            int qfuQuestIdentityFlag,
            MissionAcgAcceptancePhase acceptancePhase,
            MissionAcgIdentityRecord runtimeObjectiveIdentity,
            MissionAcgIdentityRecord missionArtifactIdentity,
            int repairArtifactLowId,
            int repairArtifactHighId,
            MissionAcgLifecycleState lifecycleState,
            MissionAcgCleanupState cleanupState,
            DateTime updatedUtc)
        {
            QuestInfo offer = ResolveOffer(selectedRollBody, selectedOfferIndex);
            QuestItemShort reward =
                offer.ItemRewards != null && offer.ItemRewards.Length > 0
                    ? offer.ItemRewards[0]
                    : null;
            return new MissionAcgAcceptedProjection(
                CurrentFormatVersion,
                binding,
                selectedRollBody,
                    MissionAcgHash.ComputeSha256(selectedRollBody),
                selectedOfferIndex,
                rawLevelSlider,
                goodBadSlider,
                orderChaosSlider,
                openHiddenSlider,
                physicalMysticalSlider,
                headOnStealthSlider,
                moneyExperienceSlider,
                offeredUtc,
                offerExpiryUtc,
                offer.MissionIconId,
                offer.ShortInfo,
                offer.Info,
                offer.CashReward,
                offer.ExperienceReward,
                reward == null ? 0 : reward.LowId,
                reward == null ? 0 : reward.HighId,
                reward == null ? 0 : reward.Quality,
                reward == null ? 0 : 1,
                qfuVersion,
                qfuQuestIdentityFlag,
                acceptancePhase,
                runtimeObjectiveIdentity,
                missionArtifactIdentity,
                repairArtifactLowId,
                repairArtifactHighId,
                lifecycleState,
                cleanupState,
                updatedUtc);
        }

        internal QuestInfo ReconstructOffer()
        {
            return ResolveOffer(this.selectedRollBody, this.SelectedOfferIndex);
        }

        internal MissionAcgAcceptedProjection WithPhase(
            MissionAcgAcceptancePhase phase,
            DateTime updatedUtc)
        {
            if ((int)phase < (int)this.AcceptancePhase)
            {
                throw new InvalidOperationException(
                    "Acceptance checkpoints cannot move backwards.");
            }

            return this.Copy(
                phase,
                this.RuntimeObjectiveIdentity,
                this.MissionArtifactIdentity,
                this.LifecycleState,
                this.CleanupState,
                updatedUtc);
        }

        internal MissionAcgAcceptedProjection WithObjective(
            MissionAcgIdentityRecord runtimeObjectiveIdentity,
            DateTime updatedUtc)
        {
            RequireIdentity(runtimeObjectiveIdentity, "runtimeObjectiveIdentity");
            if (this.RuntimeObjectiveIdentity != null
                && !this.RuntimeObjectiveIdentity.Equals(runtimeObjectiveIdentity))
            {
                throw new InvalidOperationException(
                    "A persisted acceptance objective cannot be replaced.");
            }

            return this.Copy(
                this.AcceptancePhase,
                runtimeObjectiveIdentity,
                this.MissionArtifactIdentity,
                this.LifecycleState,
                this.CleanupState,
                updatedUtc);
        }

        internal MissionAcgAcceptedProjection WithArtifact(
            MissionAcgIdentityRecord missionArtifactIdentity,
            DateTime updatedUtc)
        {
            RequireIdentity(missionArtifactIdentity, "missionArtifactIdentity");
            if (this.MissionArtifactIdentity != null
                && !this.MissionArtifactIdentity.Equals(missionArtifactIdentity))
            {
                throw new InvalidOperationException(
                    "A persisted acceptance artifact cannot be replaced.");
            }

            return this.Copy(
                this.AcceptancePhase,
                this.RuntimeObjectiveIdentity,
                missionArtifactIdentity,
                this.LifecycleState,
                this.CleanupState,
                updatedUtc);
        }

        internal MissionAcgAcceptedProjection WithLifecycle(
            MissionAcgLifecycleState lifecycleState,
            MissionAcgCleanupState cleanupState,
            DateTime updatedUtc)
        {
            return this.Copy(
                this.AcceptancePhase,
                this.RuntimeObjectiveIdentity,
                this.MissionArtifactIdentity,
                lifecycleState,
                cleanupState,
                updatedUtc);
        }

        private MissionAcgAcceptedProjection Copy(
            MissionAcgAcceptancePhase phase,
            MissionAcgIdentityRecord runtimeObjectiveIdentity,
            MissionAcgIdentityRecord missionArtifactIdentity,
            MissionAcgLifecycleState lifecycleState,
            MissionAcgCleanupState cleanupState,
            DateTime updatedUtc)
        {
            return new MissionAcgAcceptedProjection(
                this.FormatVersion,
                this.Binding,
                this.selectedRollBody,
                this.SelectedRollBodySha256,
                this.SelectedOfferIndex,
                this.RawLevelSlider,
                this.GoodBadSlider,
                this.OrderChaosSlider,
                this.OpenHiddenSlider,
                this.PhysicalMysticalSlider,
                this.HeadOnStealthSlider,
                this.MoneyExperienceSlider,
                this.OfferedUtc,
                this.OfferExpiryUtc,
                this.MissionIconId,
                this.Title,
                this.Description,
                this.FrozenCashReward,
                this.FrozenExperienceReward,
                this.FrozenItemLowId,
                this.FrozenItemHighId,
                this.FrozenItemQuality,
                this.FrozenItemCount,
                this.QfuVersion,
                this.QfuQuestIdentityFlag,
                phase,
                runtimeObjectiveIdentity,
                missionArtifactIdentity,
                this.RepairArtifactLowId,
                this.RepairArtifactHighId,
                lifecycleState,
                cleanupState,
                updatedUtc);
        }

        private void ValidateSourceProjection()
        {
            QuestAlternativeMessage roll =
                MissionRollService.DeserializeBody(this.selectedRollBody);
            if (roll.QuestInfos == null
                || this.SelectedOfferIndex >= roll.QuestInfos.Length
                || roll.QuestInfos[this.SelectedOfferIndex] == null)
            {
                throw new ArgumentException(
                    "Selected offer index is absent from the persisted mission-roll body.");
            }

            QuestInfo offer = roll.QuestInfos[this.SelectedOfferIndex];
            if (roll.LevelSlider != this.RawLevelSlider
                || DecodeSigned(roll.GoodBadSlider) != this.GoodBadSlider
                || DecodeSigned(roll.OrderChaosSlider) != this.OrderChaosSlider
                || DecodeSigned(roll.OpenHiddenSlider) != this.OpenHiddenSlider
                || DecodeSigned(roll.PhysicalMysticalSlider) != this.PhysicalMysticalSlider
                || DecodeSigned(roll.HeadOnStealthSlider) != this.HeadOnStealthSlider
                || DecodeSigned(roll.MoneyExperienceSlider) != this.MoneyExperienceSlider)
            {
                throw new ArgumentException(
                    "Persisted slider semantics do not match the selected mission-roll body.");
            }

            if (!IdentityEquals(offer.QuestIdentity, this.Binding.OriginalOfferIdentity)
                || MissionTypeCatalog.TypeFromIcon(offer.MissionIconId)
                    != this.Binding.MissionType
                || offer.MissionIconId != this.MissionIconId
                || offer.Quality != this.Binding.MissionQuality
                || !string.Equals(offer.ShortInfo ?? string.Empty, this.Title, StringComparison.Ordinal)
                || !string.Equals(offer.Info ?? string.Empty, this.Description, StringComparison.Ordinal)
                || offer.CashReward != this.FrozenCashReward
                || offer.ExperienceReward != this.FrozenExperienceReward)
            {
                throw new ArgumentException(
                    "Persisted accepted fields do not match the selected QuestInfo.");
            }

            QuestItemShort reward =
                offer.ItemRewards != null && offer.ItemRewards.Length > 0
                    ? offer.ItemRewards[0]
                    : null;
            if ((reward == null && this.FrozenItemCount != 0)
                || (reward != null
                    && (this.FrozenItemCount != 1
                        || reward.LowId != this.FrozenItemLowId
                        || reward.HighId != this.FrozenItemHighId
                        || reward.Quality != this.FrozenItemQuality)))
            {
                throw new ArgumentException(
                    "Persisted frozen item reward does not match the selected QuestInfo.");
            }

            if (!IdentityEquals(offer.Unknown5, this.Binding.IssuingTerminalIdentity)
                || !IdentityEquals(roll.MissionTerminalIdentity, this.Binding.IssuingTerminalIdentity))
            {
                throw new ArgumentException(
                    "Persisted issuing terminal does not match the selected mission roll.");
            }

            QuestActionList action =
                offer.QuestActions != null && offer.QuestActions.Length > 0
                    ? offer.QuestActions[0]
                    : null;
            if (action == null
                || !IdentityEquals(action.Playfield, this.Binding.ExteriorEntranceIdentity)
                || action.Unknown18 != this.Binding.ExteriorEntranceLow
                || action.Unknown19 != this.Binding.ExteriorEntranceHigh
                || !action.X.Equals(this.Binding.ExteriorX)
                || !action.Y.Equals(this.Binding.ExteriorY)
                || !action.Z.Equals(this.Binding.ExteriorZ))
            {
                throw new ArgumentException(
                    "Persisted exterior action does not match the accepted binding.");
            }

            int expectedVersion;
            int expectedFlag;
            ResolveQfuContract(this.Binding.MissionType, out expectedVersion, out expectedFlag);
            if (this.QfuVersion != expectedVersion
                || this.QfuQuestIdentityFlag != expectedFlag)
            {
                throw new ArgumentException(
                    "Persisted QFU contract does not match the accepted mission type.");
            }
        }

        private static QuestInfo ResolveOffer(byte[] body, int selectedOfferIndex)
        {
            QuestAlternativeMessage roll = MissionRollService.DeserializeBody(body);
            if (roll.QuestInfos == null
                || selectedOfferIndex < 0
                || selectedOfferIndex >= roll.QuestInfos.Length
                || roll.QuestInfos[selectedOfferIndex] == null)
            {
                throw new ArgumentOutOfRangeException(
                    "selectedOfferIndex",
                    "Selected offer is absent from the mission-roll body.");
            }

            return roll.QuestInfos[selectedOfferIndex];
        }

        private static void ResolveQfuContract(
            MissionRollType missionType,
            out int version,
            out int questIdentityFlag)
        {
            questIdentityFlag = 0;
            switch (missionType)
            {
                case MissionRollType.KillPerson:
                    version = 16;
                    return;
                case MissionRollType.FindPerson:
                    version = 16;
                    questIdentityFlag = 64;
                    return;
                case MissionRollType.FindItem:
                    version = 15;
                    return;
                case MissionRollType.FindItemReturn:
                    version = 8;
                    return;
                case MissionRollType.RepairMachine:
                    version = 16;
                    return;
                default:
                    throw new ArgumentOutOfRangeException("missionType");
            }
        }

        private static bool IdentityEquals(
            Identity identity,
            MissionAcgIdentityRecord record)
        {
            return identity != null
                   && record != null
                   && (int)identity.Type == record.Type
                   && identity.Instance == record.Instance;
        }

        private static int DecodeSigned(byte value)
        {
            int decoded;
            if (!MissionSliderProfile.TryDecodeSignedPercent(value, out decoded))
            {
                throw new ArgumentOutOfRangeException(
                    "value",
                    "Mission slider is outside the captured protocol range.");
            }

            return decoded;
        }

        private static void RequireSignedSlider(int value, string parameterName)
        {
            if (value < -100 || value > 100)
            {
                throw new ArgumentOutOfRangeException(parameterName);
            }
        }

        private static void RequireIdentity(
            MissionAcgIdentityRecord identity,
            string parameterName)
        {
            if (identity == null || identity.Type == 0 || identity.Instance == 0)
            {
                throw new ArgumentException(
                    "A concrete identity is required.",
                    parameterName);
            }
        }

        private static DateTime RequireUtc(DateTime value, string parameterName)
        {
            if (value == DateTime.MinValue)
            {
                throw new ArgumentOutOfRangeException(parameterName);
            }

            return value.Kind == DateTimeKind.Utc ? value : value.ToUniversalTime();
        }

        private static bool IsSha256(string value)
        {
            if (string.IsNullOrWhiteSpace(value) || value.Length != 64)
            {
                return false;
            }

            try
            {
                return MissionAcgHash.ParseHex(value, "value").Length == 32;
            }
            catch (ArgumentException)
            {
                return false;
            }
        }
    }
}
