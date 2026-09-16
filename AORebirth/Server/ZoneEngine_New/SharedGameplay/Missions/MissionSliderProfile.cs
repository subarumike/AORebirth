namespace ZoneEngine.Core.Missions
{
    using System;

    using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;

    internal enum MissionSliderEvidenceProfile
    {
        Neutral = 0,
        CapturedLeftGoodBadOrderChaosCreditsXp = 1,
        Unresolved = 2
    }

    /// <summary>
    /// Decodes the six continuous mission sliders from their signed-byte wire representation.
    /// Finalized requests prove raw 0 = center and raw 156 = signed -100. Values outside the
    /// captured/protocol range -100..100 are rejected instead of silently clamped.
    /// </summary>
    internal sealed class MissionSliderProfile
    {
        private MissionSliderProfile(
            int goodBad,
            int orderChaos,
            int openHidden,
            int physicalMystical,
            int headOnStealth,
            int moneyExperience)
        {
            GoodBad = goodBad;
            OrderChaos = orderChaos;
            OpenHidden = openHidden;
            PhysicalMystical = physicalMystical;
            HeadOnStealth = headOnStealth;
            MoneyExperience = moneyExperience;
            EvidenceProfile = ResolveEvidenceProfile();
        }

        internal int GoodBad { get; private set; }

        internal int OrderChaos { get; private set; }

        internal int OpenHidden { get; private set; }

        internal int PhysicalMystical { get; private set; }

        internal int HeadOnStealth { get; private set; }

        internal int MoneyExperience { get; private set; }

        internal MissionSliderEvidenceProfile EvidenceProfile { get; private set; }

        internal static bool TryCreate(
            QuestAlternativeMessage request,
            out MissionSliderProfile profile,
            out string error)
        {
            profile = null;
            error = null;
            if (request == null)
            {
                error = "Mission roll request was null.";
                return false;
            }

            int goodBad;
            int orderChaos;
            int openHidden;
            int physicalMystical;
            int headOnStealth;
            int moneyExperience;
            if (!TryDecodeSignedPercent(request.GoodBadSlider, out goodBad)
                || !TryDecodeSignedPercent(request.OrderChaosSlider, out orderChaos)
                || !TryDecodeSignedPercent(request.OpenHiddenSlider, out openHidden)
                || !TryDecodeSignedPercent(request.PhysicalMysticalSlider, out physicalMystical)
                || !TryDecodeSignedPercent(request.HeadOnStealthSlider, out headOnStealth)
                || !TryDecodeSignedPercent(request.MoneyExperienceSlider, out moneyExperience))
            {
                error = "Mission slider bytes must decode to signed values from -100 through 100.";
                return false;
            }

            profile = new MissionSliderProfile(
                goodBad,
                orderChaos,
                openHidden,
                physicalMystical,
                headOnStealth,
                moneyExperience);
            return true;
        }

        internal static bool TryDecodeSignedPercent(byte wireValue, out int signedPercent)
        {
            signedPercent = unchecked((sbyte)wireValue);
            return signedPercent >= -100 && signedPercent <= 100;
        }

        internal int SemanticDistance(
            int goodBad,
            int orderChaos,
            int openHidden,
            int physicalMystical,
            int headOnStealth,
            int moneyExperience)
        {
            bool candidateIsNeutral = IsProfile(
                goodBad,
                orderChaos,
                openHidden,
                physicalMystical,
                headOnStealth,
                moneyExperience,
                0,
                0,
                0,
                0,
                0,
                0);
            bool candidateIsCapturedLeft = IsProfile(
                goodBad,
                orderChaos,
                openHidden,
                physicalMystical,
                headOnStealth,
                moneyExperience,
                -100,
                -100,
                0,
                0,
                0,
                -100);

            if (EvidenceProfile == MissionSliderEvidenceProfile.CapturedLeftGoodBadOrderChaosCreditsXp)
            {
                return candidateIsCapturedLeft ? 0 : candidateIsNeutral ? 1 : 2;
            }

            // No finalized capture isolates a partial position or any Open/Hidden,
            // Physical/Mystical, or Head-on/Stealth value. Unsupported combinations therefore
            // rank against neutral evidence explicitly instead of inventing equal slider weights.
            return candidateIsNeutral ? 0 : candidateIsCapturedLeft ? 1 : 2;
        }

        internal bool Matches(
            int goodBad,
            int orderChaos,
            int openHidden,
            int physicalMystical,
            int headOnStealth,
            int moneyExperience)
        {
            return GoodBad == goodBad
                   && OrderChaos == orderChaos
                   && OpenHidden == openHidden
                   && PhysicalMystical == physicalMystical
                   && HeadOnStealth == headOnStealth
                   && MoneyExperience == moneyExperience;
        }

        private MissionSliderEvidenceProfile ResolveEvidenceProfile()
        {
            if (Matches(0, 0, 0, 0, 0, 0))
            {
                return MissionSliderEvidenceProfile.Neutral;
            }

            if (Matches(-100, -100, 0, 0, 0, -100))
            {
                return MissionSliderEvidenceProfile.CapturedLeftGoodBadOrderChaosCreditsXp;
            }

            return MissionSliderEvidenceProfile.Unresolved;
        }

        private static bool IsProfile(
            int goodBad,
            int orderChaos,
            int openHidden,
            int physicalMystical,
            int headOnStealth,
            int moneyExperience,
            int expectedGoodBad,
            int expectedOrderChaos,
            int expectedOpenHidden,
            int expectedPhysicalMystical,
            int expectedHeadOnStealth,
            int expectedMoneyExperience)
        {
            return goodBad == expectedGoodBad
                   && orderChaos == expectedOrderChaos
                   && openHidden == expectedOpenHidden
                   && physicalMystical == expectedPhysicalMystical
                   && headOnStealth == expectedHeadOnStealth
                   && moneyExperience == expectedMoneyExperience;
        }
    }
}
