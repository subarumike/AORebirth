namespace ZoneEngine.Core.Nascence.Quests
{
    #region Usings ...

    using System;

    using SmokeLounge.AOtomation.Messaging.GameData;

    #endregion

    /// <summary>
    /// Capture-backed Ecclesiast Aban Fala garden-key chain (20260822-224319, PF 4312).
    /// </summary>
    internal static class NascenceAbanFalaInteractionRules
    {
        internal const int RedeemedVillagePlayfieldId = 4312;
        internal const int GardenPlayfieldId = 4676;

        internal const int AncientDeviceItemId = 214998;
        internal const int InsigniaOfAbanItemId = 214788;
        internal const int InspectedAncientPatternAnalyzerItemId = 214783;
        internal const int FavoredAncientPatternAnalyzerItemId = 214784;
        internal const int GardenKeyItemId = 226824;

        internal const string QuestInsigniaTask = "Mission:55AAB052";
        internal const string QuestDeviceInfo = "Mission:55AAB053";
        internal const string QuestGarden = "Mission:55AAB054";
        internal const string QuestSouls = "Mission:55ABF806";
        internal const string QuestSoulsOne = "Mission:55ABAD58";
        internal const string QuestSoulsTwo = "Mission:55ABAD5A";
        internal const string QuestSoulsReturn = "Mission:55ABAD60";
        internal const string QuestDonnaDevice = "Mission:55ABAD4D";

        internal const string DreamingSilvertailName = "Dreaming Silvertail";
        internal const string CursedSilvertailName = "Cursed Silvertail";

        internal const string DeviceInspectedFlag = "nascence-aban-fala-device-inspected";
        internal const string LuxWeiDeviceShownFlag = "nascence-lux-wei-device-shown";
        internal const string LuxWeiKeyGrantedFlag = "nascence-lux-wei-key-granted";
        internal const string LuxWeiActivationGrantsFlag = "nascence-lux-wei-activation-grants";
        internal const string SoulCountFlag = "nascence-aban-fala-soul-count";

        internal const string FalaName = "Ecclesiast Aban Fala";
        internal const int FalaInstance = unchecked((int)0x7A1B033F);
        internal const string FalaIdentityText = "SimpleChar:7A1B033F";

        internal const string LuxWeiName = "Sipius Aban Lux-Wei";
        internal const int LuxWeiInstance = unchecked((int)0x7A2013BC);
        internal const string LuxWeiIdentityText = "SimpleChar:7A2013BC";

        internal const int TradeSlotCount = 1;

        internal const string JourneyNodeId = "fala_001";
        internal const string RedemptionNodeId = "fala_002";
        internal const string ArtifactOfferNodeId = "fala_003";
        internal const string DeviceTradeHoldNodeId = "fala_device_trade_hold";
        internal const string InsigniaTradeHoldNodeId = "fala_insignia_trade_hold";
        internal const string InsigniaTurnInNodeId = "fala_turnin";
        internal const string ReopenHubNodeId = "fala_hub";

        internal const string LuxWeiRootNodeId = "lux_wei_001";
        internal const string LuxWeiDeviceTradeHoldNodeId = "lux_wei_device_trade_hold";
        internal const string LuxWeiActivatedTradeHoldNodeId = "lux_wei_activated_trade_hold";
        internal const string LuxWeiActivationNodeId = "lux_wei_002";
        internal const string LuxWeiHubNodeId = "lux_wei_hub";
        internal const string LuxWeiSoulsInProgressNodeId = "lux_wei_souls_busy";
        internal const string LuxWeiFarewellNodeId = "lux_wei_farewell";

        internal static readonly string[] AllClientQuestIds =
            {
                QuestDonnaDevice,
                QuestInsigniaTask,
                QuestDeviceInfo,
                QuestGarden,
                QuestSouls,
                QuestSoulsOne,
                QuestSoulsTwo,
                QuestSoulsReturn
            };

        internal static bool IsLuxWeiName(string name)
        {
            return string.Equals(name, LuxWeiName, StringComparison.OrdinalIgnoreCase);
        }

        internal static bool IsLuxWei(Identity identity)
        {
            return identity != null && identity.Instance == LuxWeiInstance;
        }

        internal static bool IsFalaName(string name)
        {
            return string.Equals(name, FalaName, StringComparison.OrdinalIgnoreCase);
        }

        internal static bool IsFala(Identity identity)
        {
            return identity != null && identity.Instance == FalaInstance;
        }

        internal static bool IsDreamingSilvertailName(string name)
        {
            return string.Equals(name, DreamingSilvertailName, StringComparison.OrdinalIgnoreCase);
        }

        internal static bool IsGardenKeyItem(int itemId)
        {
            return itemId == GardenKeyItemId;
        }

        internal static bool IsQuestPlayfield(int playfieldId)
        {
            return playfieldId == RedeemedVillagePlayfieldId
                   || playfieldId == GardenPlayfieldId
                   || playfieldId == 4310
                   || playfieldId == 4311
                   || playfieldId == 4313;
        }

        internal static bool TryResolveQuestId(int missionInstance, out string questId)
        {
            switch (missionInstance)
            {
                case unchecked((int)0x55ABAD4D):
                    questId = QuestDonnaDevice;
                    return true;
                case unchecked((int)0x55AAB052):
                    questId = QuestInsigniaTask;
                    return true;
                case unchecked((int)0x55AAB053):
                    questId = QuestDeviceInfo;
                    return true;
                case unchecked((int)0x55AAB054):
                    questId = QuestGarden;
                    return true;
                case unchecked((int)0x55ABF806):
                    questId = QuestSouls;
                    return true;
                case unchecked((int)0x55ABAD58):
                    questId = QuestSoulsOne;
                    return true;
                case unchecked((int)0x55ABAD5A):
                    questId = QuestSoulsTwo;
                    return true;
                case unchecked((int)0x55ABAD60):
                    questId = QuestSoulsReturn;
                    return true;
                default:
                    questId = null;
                    return false;
            }
        }

        internal static bool IsSoulsQuestId(string questId)
        {
            return string.Equals(questId, QuestSouls, StringComparison.OrdinalIgnoreCase)
                   || string.Equals(questId, QuestSoulsOne, StringComparison.OrdinalIgnoreCase)
                   || string.Equals(questId, QuestSoulsTwo, StringComparison.OrdinalIgnoreCase)
                   || string.Equals(questId, QuestSoulsReturn, StringComparison.OrdinalIgnoreCase);
        }
    }

}
