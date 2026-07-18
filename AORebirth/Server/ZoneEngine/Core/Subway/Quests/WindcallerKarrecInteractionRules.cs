namespace ZoneEngine.Core.Subway.Quests
{
    #region Usings ...

    using System.Collections.Generic;
    using System.Linq;

    using SmokeLounge.AOtomation.Messaging.GameData;

    #endregion

    internal enum KarrecTradeEligibility
    {
        Eligible = 0,
        WrongNpc = 1,
        InvalidPlayer = 2,
        WrongPlayfield = 3,
        MissionNotActive = 4,
        MissingOrWrongOfferings = 5
    }

    internal static class WindcallerKarrecInteractionRules
    {
        internal const int PlayfieldId = WindcallerKarrecNpcContent.PlayfieldId;
        internal const int KarrecInstance = WindcallerKarrecNpcContent.KarrecSourceInstance;
        internal const int BurgerItemId = 297042;
        internal const int CreditCardItemId = 297043;
        internal const int GatewayInstance = unchecked((int)0xC004028F);

        internal static bool IsKarrec(Identity identity)
        {
            return identity.Type == IdentityType.CanbeAffected
                   && identity.Instance == KarrecInstance;
        }

        internal static bool IsGateway(Identity identity)
        {
            return identity.Type == IdentityType.Terminal
                   && identity.Instance == GatewayInstance;
        }

        internal static bool AreCapturedPerkUpdateFieldsResolved()
        {
            return false;
        }

        internal static KarrecTradeEligibility EvaluateTrade(
            int characterId,
            int playfieldId,
            Identity npcIdentity,
            bool missionActive,
            IEnumerable<int> itemIds)
        {
            if (!IsKarrec(npcIdentity))
            {
                return KarrecTradeEligibility.WrongNpc;
            }

            if (characterId <= 0)
            {
                return KarrecTradeEligibility.InvalidPlayer;
            }

            if (playfieldId != PlayfieldId)
            {
                return KarrecTradeEligibility.WrongPlayfield;
            }

            if (!missionActive)
            {
                return KarrecTradeEligibility.MissionNotActive;
            }

            int[] offerings = (itemIds ?? new int[0]).ToArray();
            return HasExactOfferings(offerings, offerings.Length, false)
                       ? KarrecTradeEligibility.Eligible
                       : KarrecTradeEligibility.MissingOrWrongOfferings;
        }

        internal static bool HasExactOfferings(
            IEnumerable<int> itemIds,
            int stagedSlotCount,
            bool containsUnrecognizedItem)
        {
            int[] offerings = (itemIds ?? new int[0]).OrderBy(value => value).ToArray();
            return !containsUnrecognizedItem
                   && stagedSlotCount == 2
                   && offerings.Length == 2
                   && offerings[0] == BurgerItemId
                   && offerings[1] == CreditCardItemId;
        }
    }
}
