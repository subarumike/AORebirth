namespace ZoneEngine.Core.Nascence.Quests
{
    #region Usings ...

    using System;

    using SmokeLounge.AOtomation.Messaging.GameData;

    #endregion

    /// <summary>
    /// Capture-backed Scientist Donna Red Aban garden-key quest start (20260822-224319, PF 4310).
    /// </summary>
    internal static class NascenceLifeDonnaRedInteractionRules
    {
        internal const int NascenseFrontierPlayfieldId = 4310;

        // Capture 20260822-224319 TemplateAction: Ancient Device / Analyzer seed item.
        internal const int AncientDeviceItemId = 214998;

        internal const string QuestId = "Mission:55ABAD4D";

        internal const string DeviceGrantedFlag = "nascence-life-donna-ancient-device-granted";

        internal const string DonnaName = "Scientist Donna Red";
        internal const int DonnaInstance = unchecked((int)0x7A18D4B1);
        internal const string DonnaIdentityText = "SimpleChar:7A18D4B1";

        // Capture path: donna_005 option 0 "Maybe I can help you?" assigns mission + grants device.
        internal const string QuestAcceptNodeId = "donna_005";

        // After accept / reopen lore hub.
        internal const string QuestHubNodeId = "donna_hub";

        internal static bool IsDonnaName(string name)
        {
            return string.Equals(name, DonnaName, StringComparison.OrdinalIgnoreCase);
        }

        internal static bool IsDonna(Identity identity)
        {
            return identity != null && identity.Instance == DonnaInstance;
        }

        internal static bool IsQuestPlayfield(int playfieldId)
        {
            return playfieldId == NascenseFrontierPlayfieldId
                   || playfieldId == 4311
                   || playfieldId == 4312
                   || playfieldId == 4313;
        }
    }
}
