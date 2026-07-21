namespace ZoneEngine.Core.Thrak.Quests
{
    #region Usings ...

    using SmokeLounge.AOtomation.Messaging.GameData;

    #endregion

    /// <summary>
    /// Capture 20260718-185306 Thrak garden key quest chain constants.
    /// </summary>
    internal static class ThrakGardenKeyInteractionRules
    {
        internal const int VeronicaPlayfieldId = 4310;
        internal const int ProphetPlayfieldId = 4311;
        internal const int HypnagogicPlayfieldId = 4677;
        internal const int SilvertailPlayfieldId = 4310;

        internal const int VeronicaInstance = unchecked((int)0x787B54B2);
        internal const int ProphetInstance = unchecked((int)0x78D280F6);
        internal const int HypnagogicInstance = unchecked((int)0x79758F3A);
        internal const int SilvertailInstanceA = unchecked((int)0x797652A0);
        internal const int SilvertailInstanceB = unchecked((int)0x797652F5);
        internal const int SilvertailInstanceC = unchecked((int)0x797652A7);

        internal const string VeronicaName = "Scientist Veronica Escobar";
        internal const string ProphetName = "Prophet Yutt Thrak";
        internal const string HypnagogicName = "Hypnagogic Urga-Lum Thrak";
        internal const string DreamingSilvertailName = "Dreaming Silvertail";
        internal const string CursedSilvertailName = "Cursed Silvertail";

        // Capture TemplateAction IDs
        internal const int AncientPatternAnalyzerItemId = 214998;
        internal const int InspectedAncientPatternAnalyzerItemId = 214783;
        internal const int InsigniaOfThrakItemId = 214789;
        internal const int FavoredAncientPatternAnalyzerItemId = 214785;
        internal const int SacredGardenKeyItemId = 226994;

        internal const string QuestVeronica = "Mission:5556893A";
        internal const string QuestInsignia = "Mission:55563C16";
        /// <summary>Client-only updated Veronica journal text after insignia handoff (capture QFU 55563C17).</summary>
        internal const string QuestVeronicaUpdated = "Mission:55563C17";
        internal const string QuestGarden = "Mission:55563C18";
        internal const string QuestSouls = "Mission:5556591A";
        /// <summary>Client mission id after claiming 1 soul (capture QFU 5556893B).</summary>
        internal const string QuestSouls1 = "Mission:5556893B";
        /// <summary>Client mission id after claiming 2 souls (capture QFU 5556893C).</summary>
        internal const string QuestSouls2 = "Mission:5556893C";
        internal const string QuestReturn = "Mission:5556893D";

        internal const string AccountKeyFlag = "thrak-garden-key";
        internal const string AnalyzerGrantedFlag = "thrak-analyzer-granted";
        internal const string InspectedAnalyzerGrantedFlag = "thrak-inspected-analyzer-granted";
        /// <summary>Set after Prophet inspects Ancient Device — gates insignia dialogue / trade.</summary>
        internal const string ProphetDeviceInspectedFlag = "thrak-prophet-device-inspected";
        internal const string InsigniaGrantedFlag = "thrak-insignia-granted";
        internal const string KeyGrantedFlag = "thrak-key-granted";
        internal const string SoulCountFlag = "thrak-soul-count";

        internal static bool IsSacredGardenKey(int itemId)
        {
            return itemId == SacredGardenKeyItemId;
        }

        internal static bool IsSacredGardenKeyItem(int lowId, int highId)
        {
            return IsSacredGardenKey(lowId) || IsSacredGardenKey(highId);
        }

        internal static bool IsVeronica(Identity identity)
        {
            return identity.Instance == VeronicaInstance;
        }

        internal static bool IsProphet(Identity identity)
        {
            return identity.Instance == ProphetInstance;
        }

        internal static bool IsHypnagogic(Identity identity)
        {
            return identity.Instance == HypnagogicInstance;
        }

        internal static bool IsDreamingSilvertail(Identity identity)
        {
            return identity.Instance == SilvertailInstanceA
                   || identity.Instance == SilvertailInstanceB
                   || identity.Instance == SilvertailInstanceC;
        }

        internal static bool IsThrakQuestNpcName(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                return false;
            }

            return string.Equals(name, VeronicaName, System.StringComparison.OrdinalIgnoreCase)
                   || string.Equals(name, ProphetName, System.StringComparison.OrdinalIgnoreCase)
                   || string.Equals(name, HypnagogicName, System.StringComparison.OrdinalIgnoreCase)
                   || string.Equals(name, DreamingSilvertailName, System.StringComparison.OrdinalIgnoreCase);
        }
    }
}
