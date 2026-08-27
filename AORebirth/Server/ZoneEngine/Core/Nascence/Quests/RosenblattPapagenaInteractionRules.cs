namespace ZoneEngine.Core.Nascence.Quests
{
    #region Usings ...

    using System;

    #endregion

    /// <summary>
    /// Capture-backed Dr. Rosenblatt Papagena datadisc quest (20260822-082554, PF 4310).
    /// </summary>
    internal static class RosenblattPapagenaInteractionRules
    {
        // InventoryUpdate 20260822-082554 / 20260822-221109 Remains of Swift Silvertail: 0x3F76D.
        internal const int SwiftSilvertailDatadiscItemId = 259949;

        internal const int CreditReward = 1000;

        internal const string QuestId = "Mission:55AA38B0";

        internal const string RewardGrantedFlag = "rosenblatt-papagena-reward-granted";

        internal const string PapagenaName = "Papagena";

        internal const string DiscLookNodeId = "rosenblatt_disc_look";
        internal const string QuestOfferNodeId = "rosenblatt_papagena_offer";

        internal const int TradeSlotCount = 1;

        internal static bool IsSwiftSilvertailDatadisc(int lowId, int highId)
        {
            return lowId == SwiftSilvertailDatadiscItemId
                   || highId == SwiftSilvertailDatadiscItemId;
        }

        internal static bool IsPapagenaName(string name)
        {
            return string.Equals(name, PapagenaName, StringComparison.OrdinalIgnoreCase);
        }

        internal static bool IsDiscTradeEntryNode(string nodeId)
        {
            return string.Equals(nodeId, DiscLookNodeId, StringComparison.OrdinalIgnoreCase);
        }

        internal static bool IsDiscTradeSourceNode(string nodeId)
        {
            return string.Equals(nodeId, "rosenblatt_001", StringComparison.OrdinalIgnoreCase)
                   || string.Equals(nodeId, "rosenblatt_003", StringComparison.OrdinalIgnoreCase)
                   || string.Equals(nodeId, "rosenblatt_return_001", StringComparison.OrdinalIgnoreCase)
                   || string.Equals(nodeId, "rosenblatt_turnin_done", StringComparison.OrdinalIgnoreCase);
        }

        internal static bool IsDiscTradeSourceAnswer(string nodeId, int answerIndex)
        {
            if (string.Equals(nodeId, "rosenblatt_001", StringComparison.OrdinalIgnoreCase))
            {
                return answerIndex == 1;
            }

            if (string.Equals(nodeId, "rosenblatt_003", StringComparison.OrdinalIgnoreCase))
            {
                return answerIndex == 1;
            }

            if (string.Equals(nodeId, "rosenblatt_return_001", StringComparison.OrdinalIgnoreCase))
            {
                return answerIndex == 0;
            }

            if (string.Equals(nodeId, "rosenblatt_turnin_done", StringComparison.OrdinalIgnoreCase))
            {
                return answerIndex == 0;
            }

            return false;
        }
    }
}
