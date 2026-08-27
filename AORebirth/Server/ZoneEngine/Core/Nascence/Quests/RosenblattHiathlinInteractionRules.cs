namespace ZoneEngine.Core.Nascence.Quests
{
    #region Usings ...

    using SmokeLounge.AOtomation.Messaging.GameData;

    #endregion

    /// <summary>
    /// Capture-backed Dr. Rosenblatt Hiathlin quest (20260822-070136, PF 4310).
    /// </summary>
    internal static class RosenblattHiathlinInteractionRules
    {
        internal const int NascenseFrontierPlayfieldId = 4310;
        internal const int NascenseCavePlayfieldId = 4311;

        internal const int HiathlinThighItemId = 259954;
        internal const int HiathlinPrimeThighItemId = 259955;
        internal const int LightBarRewardItemId = 252157;

        internal const int RequiredRegularKills = 5;
        internal const int RequiredPrimeKills = 1;
        internal const int RequiredRegularBodyParts = 5;
        internal const int RequiredPrimeBodyParts = 1;
        internal const int TradeSlotCount = 6;

        internal const string QuestAccept = "Mission:55AA388F";
        internal const string QuestKill4Remaining = "Mission:55AA3890";
        internal const string QuestKill3Remaining = "Mission:55AA3891";
        internal const string QuestKill2Remaining = "Mission:55AA3892";
        internal const string QuestKill1Remaining = "Mission:55AA3893";
        internal const string QuestRegularKillsComplete = "Mission:55AA3894";
        internal const string QuestTurnInReady = "Mission:55AA3895";

        internal const string RegularKillCountFlag = "rosenblatt-hiathlin-regular-kills";
        internal const string PrimeKilledFlag = "rosenblatt-hiathlin-prime-killed";
        internal const string RewardGrantedFlag = "rosenblatt-hiathlin-reward-granted";

        internal const string RosenblattName = "Dr. Rosenblatt";
        internal const int RosenblattInstance = unchecked((int)0x7A18D419);
        internal const string RosenblattIdentityText = "SimpleChar:7A18D419";

        internal const string RegularHiathlinName = "Hiathlin";
        internal const string HiathlinPrimeName = "Hiathlin Prime";

        internal const string QuestOfferNodeId = "rosenblatt_003";
        internal const string ReturnRootNodeId = "rosenblatt_return_001";
        internal const string TurnInTradeNodeId = "rosenblatt_turnin_trade";

        internal static readonly string[] ProgressiveClientQuestIds =
            {
                QuestAccept,
                QuestKill4Remaining,
                QuestKill3Remaining,
                QuestKill2Remaining,
                QuestKill1Remaining,
                QuestRegularKillsComplete,
                QuestTurnInReady
            };

        internal static bool IsRosenblatt(Identity identity)
        {
            return identity.Instance == RosenblattInstance;
        }

        internal static bool IsRosenblattName(string name)
        {
            return string.Equals(name, RosenblattName, System.StringComparison.OrdinalIgnoreCase);
        }

        internal static bool IsQuestPlayfield(int playfieldId)
        {
            return playfieldId == NascenseFrontierPlayfieldId
                   || playfieldId == NascenseCavePlayfieldId;
        }

        internal static bool IsRegularHiathlinName(string name)
        {
            return string.Equals(name, RegularHiathlinName, System.StringComparison.OrdinalIgnoreCase);
        }

        internal static bool IsHiathlinPrimeName(string name)
        {
            return string.Equals(name, HiathlinPrimeName, System.StringComparison.OrdinalIgnoreCase);
        }

        internal static bool IsHiathlinThighItem(int lowId, int highId)
        {
            return lowId == HiathlinThighItemId || highId == HiathlinThighItemId;
        }

        internal static bool IsHiathlinPrimeThighItem(int lowId, int highId)
        {
            return lowId == HiathlinPrimeThighItemId || highId == HiathlinPrimeThighItemId;
        }

        internal static bool IsBodyPartItem(int lowId, int highId)
        {
            return IsHiathlinThighItem(lowId, highId) || IsHiathlinPrimeThighItem(lowId, highId);
        }

        internal static string ResolveClientQuestId(int regularKillCount, bool primeKilled)
        {
            if (regularKillCount < 0)
            {
                regularKillCount = 0;
            }

            if (regularKillCount > RequiredRegularKills)
            {
                regularKillCount = RequiredRegularKills;
            }

            if (primeKilled && regularKillCount >= RequiredRegularKills)
            {
                return QuestTurnInReady;
            }

            if (regularKillCount >= RequiredRegularKills)
            {
                return QuestRegularKillsComplete;
            }

            return ProgressiveClientQuestIds[regularKillCount];
        }
    }
}
