namespace ZoneEngine.Core.Nascence.Quests
{
    #region Usings ...

    using System;

    using SmokeLounge.AOtomation.Messaging.GameData;

    #endregion

    /// <summary>
    /// Capture-backed Joshua Falker silvertail / chimera kill quests (20260822-221109, PF 4310).
    /// </summary>
    internal static class NascenceLifeJoshuaFalkerInteractionRules
    {
        internal const int NascenseFrontierPlayfieldId = 4310;
        internal const int NascenseCavePlayfieldId = 4311;

        internal const int RequiredKills = 10;
        internal const int XpReward = 10080;

        internal const int SilvertailRewardLowItemId = 218351;
        internal const int SilvertailRewardHighItemId = 218352;
        internal const int SilvertailRewardQuality = 7;

        internal const int ChimeraRewardLowItemId = 218822;
        internal const int ChimeraRewardHighItemId = 218823;
        internal const int ChimeraRewardQuality = 7;

        internal const string SilvertailQuestId = "Mission:55ABAD28";
        internal const string ChimeraQuestId = "Mission:55ABAD29";

        internal const string SilvertailKillCountFlag = "nascence-life-falker-silvertail-kills";
        internal const string ChimeraKillCountFlag = "nascence-life-falker-chimera-kills";
        internal const string SilvertailRewardGrantedFlag = "nascence-life-falker-silvertail-reward-granted";
        internal const string ChimeraRewardGrantedFlag = "nascence-life-falker-chimera-reward-granted";

        internal const string FalkerName = "Joshua Falker";
        internal const int FalkerInstance = unchecked((int)0x7A18D424);
        internal const string FalkerIdentityText = "SimpleChar:7A18D424";

        internal const string SwiftSilvertailName = "Swift Silvertail";
        internal const string BarkingChimeraName = "Barking Chimera";

        internal const string QuestAcceptNodeId = "falker_001";

        internal static bool IsFalkerName(string name)
        {
            return string.Equals(name, FalkerName, StringComparison.OrdinalIgnoreCase);
        }

        internal static bool IsFalker(Identity identity)
        {
            return identity != null && identity.Instance == FalkerInstance;
        }

        internal static bool IsQuestPlayfield(int playfieldId)
        {
            return playfieldId == NascenseFrontierPlayfieldId
                   || playfieldId == NascenseCavePlayfieldId;
        }

        internal static bool IsSwiftSilvertailName(string name)
        {
            return string.Equals(name, SwiftSilvertailName, StringComparison.OrdinalIgnoreCase);
        }

        internal static bool IsBarkingChimeraName(string name)
        {
            return string.Equals(name, BarkingChimeraName, StringComparison.OrdinalIgnoreCase);
        }
    }
}
