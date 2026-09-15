namespace ZoneEngine_New.Core.Missions
{
    #region Usings ...

    using System;
    using System.Globalization;

    using AORebirth.Stats.SpecialStats;

    #endregion

    /// <summary>
    /// Shared progression rules used by the Daily Mission XP Reward mechanic.
    /// The item grants two side tokens for every mission-token level tier reached.
    /// </summary>
    internal static class ProgressionRewardRules
    {
        private const string CompletionSnapshotPrefix = "progression:completion:v1:";
        private const string FullLevelXpEffectReferencePrefix = "progression:full-level-xp:";
        private const string SideTokenEffectReferencePrefix = "progression:side-token:";

        internal const int ClanSideTokenStatId = 62;
        internal const int NoSideTokenStatId = -1;
        internal const int OmniSideTokenStatId = 75;
        internal const int SideTokensPerTier = 2;

        internal static int GetFullRubikaLevelXpReward(int level)
        {
            if (level < 1 || level >= 200)
            {
                return 0;
            }

            return Convert.ToInt32(XPTable.TableRKXP[level - 1, 2]);
        }

        internal static int GetMissionTokenTierCount(int level)
        {
            if (level < 1 || level > 220)
            {
                return 0;
            }

            if (level >= 190)
            {
                return 9;
            }

            if (level >= 175)
            {
                return 8;
            }

            if (level >= 150)
            {
                return 7;
            }

            if (level >= 125)
            {
                return 6;
            }

            if (level >= 100)
            {
                return 5;
            }

            if (level >= 75)
            {
                return 4;
            }

            if (level >= 50)
            {
                return 3;
            }

            return level >= 15 ? 2 : 1;
        }

        internal static int GetSideTokenReward(int level, int side)
        {
            int statId;
            return TryGetSideTokenStatId(side, out statId)
                       ? SideTokensPerTier * GetMissionTokenTierCount(level)
                       : 0;
        }

        internal static bool TryGetSideTokenStatId(int side, out int statId)
        {
            if (side == 1)
            {
                statId = ClanSideTokenStatId;
                return true;
            }

            if (side == 2)
            {
                statId = OmniSideTokenStatId;
                return true;
            }

            statId = NoSideTokenStatId;
            return false;
        }

        internal static bool TryCreateCompletionSnapshot(
            int level,
            int side,
            out ProgressionRewardSnapshot snapshot)
        {
            snapshot = null;
            int xpReward = GetFullRubikaLevelXpReward(level);
            if (xpReward <= 0)
            {
                return false;
            }

            int statId;
            int sideTokenReward;
            if (side == 0)
            {
                statId = NoSideTokenStatId;
                sideTokenReward = 0;
            }
            else if (TryGetSideTokenStatId(side, out statId))
            {
                sideTokenReward = GetSideTokenReward(level, side);
            }
            else
            {
                return false;
            }

            snapshot = new ProgressionRewardSnapshot(level, xpReward, statId, sideTokenReward);
            return true;
        }

        internal static string CreateSideTokenEffectReference(int statId, int reward) => "progression:side-token:" + statId + ":" + reward;
        internal static string CreateFullLevelXpEffectReference(int level, int reward) => "progression:full-level-xp:" + level + ":" + reward;
    }

    internal sealed class ProgressionRewardSnapshot
    {
        internal ProgressionRewardSnapshot(
            int levelBefore,
            int xpReward,
            int sideTokenStatId,
            int sideTokenReward)
        {
            this.LevelBefore = levelBefore;
            this.XpReward = xpReward;
            this.SideTokenStatId = sideTokenStatId;
            this.SideTokenReward = sideTokenReward;
        }

        internal int LevelBefore { get; private set; }

        internal int SideTokenReward { get; private set; }

        internal int SideTokenStatId { get; private set; }

        internal int XpReward { get; private set; }
    }
}
