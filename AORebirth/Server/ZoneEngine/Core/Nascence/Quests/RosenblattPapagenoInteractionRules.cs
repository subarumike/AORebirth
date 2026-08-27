namespace ZoneEngine.Core.Nascence.Quests
{
    #region Usings ...

    using System;

    using AORebirth.Core.Entities;
    using AORebirth.Enums;

    using SmokeLounge.AOtomation.Messaging.GameData;

    #endregion

    /// <summary>
    /// Capture-backed Dr. Rosenblatt Remove Papageno quest for Clan players
    /// (Swift Silvertail Compact Message Datadisc → Mission:55B0B8A3, capture 20260825-204815).
    /// Omni players get Remove Papagena (Mission:55AA38B0) from the same disc.
    /// </summary>
    internal static class RosenblattPapagenoInteractionRules
    {
        // Same Silvertail Compact Message Datadisc as Papagena Omni quest.
        internal const int SwiftSilvertailDatadiscItemId =
            RosenblattPapagenaInteractionRules.SwiftSilvertailDatadiscItemId;

        internal const int CreditReward = 1000;

        internal const int XpReward = 1000;

        internal const string QuestId = "Mission:55B0B8A3";

        internal const string RewardGrantedFlag = "rosenblatt-papageno-reward-granted";

        internal const string PapagenoName = "Papageno";

        internal const string QuestOfferNodeId = "rosenblatt_papageno_offer";

        internal const int TradeSlotCount = 1;

        internal static bool IsPapagenoName(string name)
        {
            return string.Equals(name, PapagenoName, StringComparison.OrdinalIgnoreCase);
        }

        internal static bool IsClanPlayer(ICharacter source)
        {
            if (source == null || source.Stats == null)
            {
                return false;
            }

            try
            {
                return source.Stats[StatIds.side].Value == (int)Side.Clan;
            }
            catch
            {
                return false;
            }
        }

        internal static bool IsOmniPlayer(ICharacter source)
        {
            if (source == null || source.Stats == null)
            {
                return false;
            }

            try
            {
                return source.Stats[StatIds.side].Value == (int)Side.Omni;
            }
            catch
            {
                return false;
            }
        }
    }
}
