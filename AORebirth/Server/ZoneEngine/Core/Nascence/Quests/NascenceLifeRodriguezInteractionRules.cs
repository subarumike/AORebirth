namespace ZoneEngine.Core.Nascence.Quests
{
    #region Usings ...

    using System;

    using SmokeLounge.AOtomation.Messaging.GameData;

    #endregion

    /// <summary>
    /// Capture-backed Scientist Drake Rodriguez bracer / Donna Red quest
    /// (20260822-221109 + proximity/bracer 20260825-155929, PF 4001).
    /// </summary>
    internal static class NascenceLifeRodriguezInteractionRules
    {
        internal const int JobeResearchPlayfieldId = 4001;

        // Capture 20260825-155929 TemplateAction ItemLowId/HighId=223762 (0x36A12).
        internal const int BracerItemId = 223762;

        internal const int BracerQuality = 1;

        // Capture 20260825-155929 ContainerAdd Overflow Slot=111 (0x6F).
        internal const int BracerOverflowSlot = 0x6F;

        internal const int BracerTemplateActionUnknown1 = 1;

        internal const int BracerTemplateActionUnknown2 = 87;

        internal const string QuestId = "Mission:55ABF001";

        internal const string BracerGrantedFlag = "nascence-life-rodriguez-bracer-granted";
        internal const string RewardGrantedFlag = "nascence-life-rodriguez-reward-granted";

        internal const string RodriguezName = "Scientist Drake Rodriguez";
        internal const int RodriguezInstance = unchecked((int)0x7A1E3C24);
        internal const string RodriguezIdentityText = "SimpleChar:7A1E3C24";

        internal const string QuestOfferNodeId = "drake_003";
        internal const string QuestOfferSourceNodeId = "drake_002";

        // Capture 20260825-155929: auto-open when walking up (~3m observed); Mike: 5m.
        internal const float RodriguezProximityRadiusMeters = 5f;

        internal static bool IsRodriguezName(string name)
        {
            return string.Equals(name, RodriguezName, StringComparison.OrdinalIgnoreCase);
        }

        internal static bool IsRodriguez(Identity identity)
        {
            return identity != null && identity.Instance == RodriguezInstance;
        }
    }
}
