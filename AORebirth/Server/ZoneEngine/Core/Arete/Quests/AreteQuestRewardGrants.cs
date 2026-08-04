namespace ZoneEngine.Core.Arete.Quests
{
    #region Usings ...

    using System;

    using AORebirth.Core.Entities;
    using AORebirth.Enums;

    using Utility;

    using ZoneEngine.Core;
    using ZoneEngine.Core.MessageHandlers;
    using ZoneEngine.Core.Missions;

    #endregion

    /// <summary>
    /// Live Arete tip turn-ins announce credits in FormatFeedback but historically relied on
    /// MissionRuntime.Rewards.ExecuteAtomicCharacterStats, which rejects unless the mission is
    /// already Completed — most turn-ins grant before Complete, so cash never landed.
    /// XP already used CombatXpRuntimeService.AwardDirectXp; credits must use the same live path.
    /// </summary>
    internal static class AreteQuestRewardGrants
    {
        internal static void GrantCredits(ICharacter source, int credits)
        {
            if (source == null || credits <= 0 || source.Stats == null)
            {
                return;
            }

            MissionCompleteService.GrantCredits(source, credits);
            try
            {
                source.Stats.Write();
            }
            catch (Exception ex)
            {
                LogUtil.Debug(
                    DebugInfoDetail.Engine,
                    "AreteQuestRewardGrants cash Write failed: " + ex.Message);
            }
        }

        /// <summary>
        /// Idempotent credit grant keyed by mission flag (same pattern as AwardDirectXp flags).
        /// </summary>
        internal static void GrantCreditsOnce(
            ICharacter source,
            string questId,
            string awardedFlagKey,
            int credits)
        {
            if (source == null || credits <= 0 || string.IsNullOrEmpty(questId)
                || string.IsNullOrEmpty(awardedFlagKey))
            {
                return;
            }

            int characterId = source.Identity.Instance;
            if (MissionRuntime.IsInitialized
                && MissionRuntime.Service.GetFlag(characterId, questId, awardedFlagKey) != null)
            {
                return;
            }

            GrantCredits(source, credits);
            if (MissionRuntime.IsInitialized)
            {
                MissionRuntime.Service.SetFlag(characterId, questId, awardedFlagKey, "true");
            }
        }

        internal static void GrantXpOnce(
            ICharacter source,
            string questId,
            string awardedFlagKey,
            int xp,
            string awardSource)
        {
            if (source == null || xp <= 0 || string.IsNullOrEmpty(questId)
                || string.IsNullOrEmpty(awardedFlagKey))
            {
                return;
            }

            int characterId = source.Identity.Instance;
            if (MissionRuntime.IsInitialized
                && MissionRuntime.Service.GetFlag(characterId, questId, awardedFlagKey) != null)
            {
                return;
            }

            if (!CombatXpRuntimeService.AwardDirectXp(source, xp, awardSource))
            {
                return;
            }

            if (MissionRuntime.IsInitialized)
            {
                MissionRuntime.Service.SetFlag(characterId, questId, awardedFlagKey, "true");
            }
        }

        /// <summary>
        /// Preferred Arete turn-in pair: live credits + live XP (FormatFeedback stays caller-owned).
        /// </summary>
        internal static void GrantCreditsAndXpOnce(
            ICharacter source,
            string questId,
            string creditsFlagKey,
            int credits,
            string xpFlagKey,
            int xp,
            string xpAwardSource)
        {
            GrantCreditsOnce(source, questId, creditsFlagKey, credits);
            GrantXpOnce(source, questId, xpFlagKey, xp, xpAwardSource);
        }
    }
}
