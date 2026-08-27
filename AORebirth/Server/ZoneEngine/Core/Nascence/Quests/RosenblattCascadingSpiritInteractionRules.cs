namespace ZoneEngine.Core.Nascence.Quests
{
    #region Usings ...

    using System;

    #endregion

    /// <summary>
    /// Capture-backed Dr. Rosenblatt Cascading Spirit quest (20260822-083345, PF 4310).
    /// </summary>
    internal static class RosenblattCascadingSpiritInteractionRules
    {
        internal const int BarkingChimeraDatadiscItemId = 259951;

        internal const int EssenceOfTheHauntedItemId = 259956;

        internal const int CreditReward = 1000;

        internal const int XpReward = 1009;

        internal const string QuestId = "Mission:55AA38B5";

        internal const string RewardGrantedFlag = "rosenblatt-cascading-reward-granted";

        internal const string SpiritKilledFlag = "rosenblatt-cascading-spirit-killed";

        internal const string ChimeraDiscTradedFlag = "rosenblatt-cascading-chimera-disc-traded";

        internal const string CascadingSpiritName = "Cascading Spirit";

        internal const string DiscLookNodeId = "rosenblatt_chimera_disc_look";

        internal const string QuestOfferNodeId = "rosenblatt_cascading_offer";

        internal const string TurnInTradeNodeId = "rosenblatt_cascading_turnin_trade";

        internal const string TurnInDoneNodeId = "rosenblatt_cascading_turnin_done";

        internal const int TradeSlotCount = 1;

        internal static bool IsBarkingChimeraDatadisc(int lowId, int highId)
        {
            return lowId == BarkingChimeraDatadiscItemId
                   || highId == BarkingChimeraDatadiscItemId;
        }

        internal static bool IsEssenceOfTheHaunted(int lowId, int highId)
        {
            return lowId == EssenceOfTheHauntedItemId
                   || highId == EssenceOfTheHauntedItemId;
        }

        internal static bool IsCascadingSpiritName(string name)
        {
            return string.Equals(name, CascadingSpiritName, StringComparison.OrdinalIgnoreCase);
        }

        internal static bool IsDiscTradeSourceNode(string nodeId)
        {
            return string.Equals(nodeId, "rosenblatt_001", StringComparison.OrdinalIgnoreCase)
                   || string.Equals(nodeId, "rosenblatt_003", StringComparison.OrdinalIgnoreCase)
                   || string.Equals(nodeId, "rosenblatt_return_001", StringComparison.OrdinalIgnoreCase)
                   || string.Equals(nodeId, "rosenblatt_turnin_done", StringComparison.OrdinalIgnoreCase)
                   || string.Equals(nodeId, TurnInDoneNodeId, StringComparison.OrdinalIgnoreCase);
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

            if (string.Equals(nodeId, TurnInDoneNodeId, StringComparison.OrdinalIgnoreCase))
            {
                return answerIndex == 0;
            }

            return false;
        }
    }
}
