namespace ZoneEngine.Core.Nascence.Quests
{
    #region Usings ...

    using System;

    #endregion

    /// <summary>
    /// Capture-backed Dr. Rosenblatt Spinetooth datadisc quest (20260822-083846, PF 4310).
    /// </summary>
    internal static class RosenblattSpinetoothInteractionRules
    {
        // InventoryUpdate 20260822-083846 Predator Striker / Stalking Predator: 0x3F76E.
        internal const int PredatorDatadiscItemId = 259950;

        internal const int CreditReward = 2000;

        internal const int XpReward = 2000;

        internal const string QuestId = "Mission:55AA38B6";

        internal const string RewardGrantedFlag = "rosenblatt-spinetooth-reward-granted";

        internal const string SpinetoothHatchlingName = "Spinetooth Hatchling";

        internal const string QuestOfferNodeId = "rosenblatt_spinetooth_offer";

        internal const int TradeSlotCount = 1;

        internal static bool IsPredatorDatadisc(int lowId, int highId)
        {
            return lowId == PredatorDatadiscItemId || highId == PredatorDatadiscItemId;
        }

        internal static bool IsSpinetoothHatchlingName(string name)
        {
            return string.Equals(name, SpinetoothHatchlingName, StringComparison.OrdinalIgnoreCase);
        }
    }
}
