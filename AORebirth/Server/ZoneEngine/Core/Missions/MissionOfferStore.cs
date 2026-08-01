namespace ZoneEngine.Core.Missions
{
    #region Usings ...

    using System;
    using System.Collections.Generic;

    using SmokeLounge.AOtomation.Messaging.GameData;
    using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;

    #endregion

    internal sealed class MissionOfferRecord
    {
        internal int OwnerInstance { get; set; }

        internal QuestInfo Offer { get; set; }

        internal byte[] SerializedRollPayload { get; set; }

        internal int OfferIndex { get; set; }

        internal DateTime IssuedUtc { get; set; }

        internal DateTime ExpiresUtc { get; set; }

        internal int LevelSlider { get; set; }

        internal int GoodBadSlider { get; set; }

        internal int OrderChaosSlider { get; set; }

        internal int OpenHiddenSlider { get; set; }

        internal int PhysicalMysticalSlider { get; set; }

        internal int HeadOnStealthSlider { get; set; }

        internal int MoneyExperienceSlider { get; set; }

        internal MissionSliderEvidenceProfile SliderEvidenceProfile { get; set; }

        internal bool Claimed { get; set; }

        internal MissionOfferRecord Snapshot()
        {
            return new MissionOfferRecord
                   {
                       OwnerInstance = OwnerInstance,
                       Offer = Offer,
                       SerializedRollPayload =
                           SerializedRollPayload == null
                               ? null
                               : (byte[])SerializedRollPayload.Clone(),
                       OfferIndex = OfferIndex,
                       IssuedUtc = IssuedUtc,
                       ExpiresUtc = ExpiresUtc,
                       LevelSlider = LevelSlider,
                       GoodBadSlider = GoodBadSlider,
                       OrderChaosSlider = OrderChaosSlider,
                       OpenHiddenSlider = OpenHiddenSlider,
                       PhysicalMysticalSlider = PhysicalMysticalSlider,
                       HeadOnStealthSlider = HeadOnStealthSlider,
                       MoneyExperienceSlider = MoneyExperienceSlider,
                       SliderEvidenceProfile = SliderEvidenceProfile,
                       Claimed = Claimed
                   };
        }
    }

    /// <summary>
    /// Owns the current rolled offers until one exact offer is durably claimed. Offers are deliberately
    /// session-local, but their issuance time, expiry, sliders and exact serialized response are retained
    /// long enough for acceptance to freeze a complete durable projection.
    /// </summary>
    internal static class MissionOfferStore
    {
        internal const int OfferLifetimeSeconds = 48 * 60 * 60;

        private static readonly object Sync = new object();

        private static readonly Dictionary<int, MissionOfferRecord[]> OffersByCharacter =
            new Dictionary<int, MissionOfferRecord[]>();

        public static void StoreRoll(int characterInstance, QuestInfo[] offers)
        {
            DateTime now = DateTime.UtcNow;
            var records = new MissionOfferRecord[offers == null ? 0 : offers.Length];
            for (int i = 0; i < records.Length; i++)
            {
                records[i] =
                    new MissionOfferRecord
                    {
                        OwnerInstance = characterInstance,
                        Offer = offers[i],
                        SerializedRollPayload = null,
                        OfferIndex = i,
                        IssuedUtc = now,
                        ExpiresUtc = now.AddSeconds(OfferLifetimeSeconds),
                        SliderEvidenceProfile = MissionSliderEvidenceProfile.Unresolved
                    };
            }

            lock (Sync)
            {
                OffersByCharacter[characterInstance] = records;
            }
        }

        internal static bool TryStoreRoll(
            int characterInstance,
            QuestAlternativeMessage response,
            QuestAlternativeMessage request,
            DateTime issuedUtc,
            byte[] serializedRollPayload,
            out string failure)
        {
            failure = string.Empty;
            if (characterInstance <= 0
                || response == null
                || response.QuestInfos == null
                || response.QuestInfos.Length == 0
                || request == null
                || serializedRollPayload == null
                || serializedRollPayload.Length == 0
                || issuedUtc.Kind != DateTimeKind.Utc)
            {
                failure = "A complete serialized roll and UTC issuance time are required.";
                return false;
            }

            MissionSliderProfile sliders;
            if (!MissionSliderProfile.TryCreate(request, out sliders, out failure))
            {
                return false;
            }

            var records = new MissionOfferRecord[response.QuestInfos.Length];
            for (int i = 0; i < records.Length; i++)
            {
                QuestInfo offer = response.QuestInfos[i];
                if (offer == null
                    || offer.QuestIdentity == null
                    || offer.QuestIdentity.Instance <= 0)
                {
                    failure = "A rolled offer is missing its exact identity.";
                    return false;
                }

                records[i] =
                    new MissionOfferRecord
                    {
                        OwnerInstance = characterInstance,
                        Offer = offer,
                        SerializedRollPayload = (byte[])serializedRollPayload.Clone(),
                        OfferIndex = i,
                        IssuedUtc = issuedUtc,
                        ExpiresUtc = issuedUtc.AddSeconds(OfferLifetimeSeconds),
                        LevelSlider = request.LevelSlider,
                        GoodBadSlider = sliders.GoodBad,
                        OrderChaosSlider = sliders.OrderChaos,
                        OpenHiddenSlider = sliders.OpenHidden,
                        PhysicalMysticalSlider = sliders.PhysicalMystical,
                        HeadOnStealthSlider = sliders.HeadOnStealth,
                        MoneyExperienceSlider = sliders.MoneyExperience,
                        SliderEvidenceProfile = sliders.EvidenceProfile
                    };
            }

            lock (Sync)
            {
                OffersByCharacter[characterInstance] = records;
            }

            return true;
        }

        public static bool TryGetOffer(
            int characterInstance,
            Identity questIdentity,
            out QuestInfo offer)
        {
            offer = null;
            MissionOfferRecord record;
            if (!TryFind_NoClaim(characterInstance, questIdentity, out record))
            {
                return false;
            }

            offer = record.Offer;
            return offer != null;
        }

        internal static bool TryClaimForAcceptance(
            int characterInstance,
            Identity questIdentity,
            DateTime nowUtc,
            out MissionOfferRecord record,
            out string failure)
        {
            record = null;
            failure = string.Empty;
            if (characterInstance <= 0
                || questIdentity == null
                || questIdentity.Instance <= 0
                || nowUtc.Kind != DateTimeKind.Utc)
            {
                failure = "Exact owner, offer identity and UTC acceptance time are required.";
                return false;
            }

            lock (Sync)
            {
                MissionOfferRecord[] offers;
                if (!OffersByCharacter.TryGetValue(characterInstance, out offers)
                    || offers == null)
                {
                    failure = "Offer is stale or was not issued in this session.";
                    return false;
                }

                for (int i = 0; i < offers.Length; i++)
                {
                    MissionOfferRecord candidate = offers[i];
                    if (!Matches(candidate, questIdentity))
                    {
                        continue;
                    }

                    if (candidate.ExpiresUtc <= nowUtc)
                    {
                        failure = "Offer has expired.";
                        return false;
                    }

                    if (candidate.Claimed)
                    {
                        failure = "Offer acceptance is already in progress.";
                        return false;
                    }

                    if (candidate.SerializedRollPayload == null
                        || candidate.SerializedRollPayload.Length == 0)
                    {
                        failure = "Offer lacks the complete serialized roll projection.";
                        return false;
                    }

                    candidate.Claimed = true;
                    record = candidate.Snapshot();
                    return true;
                }

                failure = "Offer is stale or was replaced by a newer roll.";
                return false;
            }
        }

        internal static void ReleaseClaim(
            int characterInstance,
            Identity questIdentity)
        {
            lock (Sync)
            {
                MissionOfferRecord candidate;
                if (TryFind_NoLock(characterInstance, questIdentity, out candidate))
                {
                    candidate.Claimed = false;
                }
            }
        }

        internal static void MarkDurablyClaimed(
            int characterInstance,
            Identity questIdentity)
        {
            lock (Sync)
            {
                MissionOfferRecord candidate;
                if (TryFind_NoLock(characterInstance, questIdentity, out candidate))
                {
                    candidate.Claimed = true;
                }
            }
        }

        internal static bool IsIdentityInUse(int questInstance)
        {
            if (questInstance <= 0)
            {
                return false;
            }

            lock (Sync)
            {
                foreach (MissionOfferRecord[] records in OffersByCharacter.Values)
                {
                    if (records == null)
                    {
                        continue;
                    }

                    for (int i = 0; i < records.Length; i++)
                    {
                        QuestInfo offer = records[i] == null ? null : records[i].Offer;
                        if (offer != null
                            && offer.QuestIdentity != null
                            && offer.QuestIdentity.Instance == questInstance)
                        {
                            return true;
                        }
                    }
                }
            }

            return false;
        }

        internal static void DiscardRoll(int characterInstance)
        {
            lock (Sync)
            {
                OffersByCharacter.Remove(characterInstance);
            }
        }

        private static bool TryFind_NoClaim(
            int characterInstance,
            Identity questIdentity,
            out MissionOfferRecord record)
        {
            record = null;
            if (questIdentity == null || questIdentity.Instance <= 0)
            {
                return false;
            }

            lock (Sync)
            {
                return TryFind_NoLock(characterInstance, questIdentity, out record);
            }
        }

        private static bool TryFind_NoLock(
            int characterInstance,
            Identity questIdentity,
            out MissionOfferRecord record)
        {
            record = null;
            MissionOfferRecord[] offers;
            if (!OffersByCharacter.TryGetValue(characterInstance, out offers)
                || offers == null)
            {
                return false;
            }

            for (int i = 0; i < offers.Length; i++)
            {
                if (Matches(offers[i], questIdentity))
                {
                    record = offers[i];
                    return true;
                }
            }

            return false;
        }

        private static bool Matches(
            MissionOfferRecord record,
            Identity questIdentity)
        {
            return record != null
                   && record.Offer != null
                   && record.Offer.QuestIdentity != null
                   && questIdentity != null
                   && record.Offer.QuestIdentity.Type == questIdentity.Type
                   && record.Offer.QuestIdentity.Instance == questIdentity.Instance;
        }
    }
}
