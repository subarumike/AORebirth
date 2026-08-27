namespace ZoneEngine.Core.Nascence.Quests
{
    #region Usings ...

    using System;

    using AORebirth.Core.Entities;
    using AORebirth.Enums;

    #endregion

    /// <summary>
    /// Capture-backed Dr. Rosenblatt Demonic Subjugator datadisc quest (20260822-084957, PF 4310).
    /// </summary>
    internal static class RosenblattDemonicInteractionRules
    {
        // InventoryUpdate 20260822-083846 / 084957 Weaver of Malice: 0x3F770.
        internal const int WeaverDatadiscItemId = 259952;

        internal const int CreditReward = 2000;

        internal const int XpReward = 2000;

        internal const string QuestId = "Mission:55AA38B7";

        internal const string RewardGrantedFlag = "rosenblatt-demonic-reward-granted";

        // Live spawn name (NascenceLifeSpawn) includes "The "; QFU text says "Demonic Subjugator".
        internal const string DemonicSubjugatorName = "Demonic Subjugator";

        internal const string DemonicSubjugatorSpawnName = "The Demonic Subjugator";

        // Capture / spawn MonsterData for The Demonic Subjugator.
        internal const int DemonicSubjugatorMonsterData = 223690;

        internal const string QuestOfferNodeId = "rosenblatt_demonic_offer";

        internal const int TradeSlotCount = 1;

        internal static bool IsWeaverDatadisc(int lowId, int highId)
        {
            return lowId == WeaverDatadiscItemId || highId == WeaverDatadiscItemId;
        }

        internal static bool IsDemonicSubjugatorName(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                return false;
            }

            if (string.Equals(name, DemonicSubjugatorName, StringComparison.OrdinalIgnoreCase)
                || string.Equals(name, DemonicSubjugatorSpawnName, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            // Tolerate leading "The " from SCFU / spawn naming.
            string trimmed = name.Trim();
            if (trimmed.StartsWith("The ", StringComparison.OrdinalIgnoreCase))
            {
                trimmed = trimmed.Substring(4).Trim();
            }

            return string.Equals(trimmed, DemonicSubjugatorName, StringComparison.OrdinalIgnoreCase);
        }

        internal static bool IsDemonicSubjugator(ICharacter target)
        {
            if (target == null)
            {
                return false;
            }

            if (IsDemonicSubjugatorName(target.Name))
            {
                return true;
            }

            try
            {
                return target.Stats != null
                       && target.Stats[StatIds.monsterdata] != null
                       && target.Stats[StatIds.monsterdata].Value == DemonicSubjugatorMonsterData;
            }
            catch
            {
                return false;
            }
        }
    }
}
