namespace ZoneEngine.Core.Missions
{
    #region Usings ...

    using System.Collections.Generic;

    using SmokeLounge.AOtomation.Messaging.GameData;
    using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;

    #endregion

    /// <summary>
    /// Remembers the mission offers most recently rolled for each character so that a following
    /// mission-accept (CreateQuest) can look the chosen offer up by its quest identity. This is kept
    /// in memory only (no persistence): a roll is short lived and only meaningful for the session in
    /// which it was produced.
    /// </summary>
    internal static class MissionOfferStore
    {
        private static readonly object Sync = new object();

        private static readonly Dictionary<int, QuestInfo[]> OffersByCharacter =
            new Dictionary<int, QuestInfo[]>();

        /// <summary>
        /// Records the offers just sent to a character, replacing any previous roll.
        /// </summary>
        public static void StoreRoll(int characterInstance, QuestInfo[] offers)
        {
            lock (Sync)
            {
                OffersByCharacter[characterInstance] = offers ?? new QuestInfo[0];
            }
        }

        /// <summary>
        /// Finds the offer whose quest identity matches the one the client accepted.
        /// </summary>
        public static bool TryGetOffer(int characterInstance, Identity questIdentity, out QuestInfo offer)
        {
            offer = null;

            QuestInfo[] offers;
            lock (Sync)
            {
                if (!OffersByCharacter.TryGetValue(characterInstance, out offers) || offers == null)
                {
                    return false;
                }
            }

            foreach (QuestInfo candidate in offers)
            {
                if (candidate == null)
                {
                    continue;
                }

                if (candidate.QuestIdentity.Instance == questIdentity.Instance
                    && candidate.QuestIdentity.Type == questIdentity.Type)
                {
                    offer = candidate;
                    return true;
                }
            }

            return false;
        }
    }
}
