namespace ZoneEngine.Core.Missions
{
    #region Usings ...

    using System;
    using System.Globalization;

    using AORebirth.Stats.SpecialStats;

    #endregion

    /// <summary>
    /// Shared progression rules used by the Daily Mission XP Reward item (285612).
    /// The item grants two side tokens for every mission-token level tier reached.
    /// </summary>
    internal static class DailyMissionRewardRules
    {
        private const string CompletionSnapshotPrefix = "item:285612:completion:v1:";
        private const string FullLevelXpEffectReferencePrefix = "item:285612:full-level-xp:";
        private const string SideTokenEffectReferencePrefix = "item:285612:side-token:";

        internal const int ClanSideTokenStatId = 62;
        internal const int NoSideTokenStatId = -1;
        internal const int OmniSideTokenStatId = 75;
        internal const int SideTokensPerTier = 2;
        internal const string LegacyOmniSideTokenEffectReference = "capture:20260717-223626:stat-75-plus-2";

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
            out DailyMissionRewardSnapshot snapshot)
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

            snapshot = new DailyMissionRewardSnapshot(level, xpReward, statId, sideTokenReward);
            return true;
        }

        internal static string SerializeCompletionSnapshot(DailyMissionRewardSnapshot snapshot)
        {
            if (!IsValidCompletionSnapshot(snapshot))
            {
                throw new ArgumentException("Daily mission reward snapshot is unresolved.", "snapshot");
            }

            return CompletionSnapshotPrefix
                   + snapshot.LevelBefore.ToString(CultureInfo.InvariantCulture) + ":"
                   + snapshot.XpReward.ToString(CultureInfo.InvariantCulture) + ":"
                   + snapshot.SideTokenStatId.ToString(CultureInfo.InvariantCulture) + ":"
                   + snapshot.SideTokenReward.ToString(CultureInfo.InvariantCulture);
        }

        internal static bool TryParseCompletionSnapshot(
            string value,
            out DailyMissionRewardSnapshot snapshot)
        {
            snapshot = null;
            if (string.IsNullOrWhiteSpace(value)
                || !value.StartsWith(CompletionSnapshotPrefix, StringComparison.Ordinal))
            {
                return false;
            }

            string[] values = value.Substring(CompletionSnapshotPrefix.Length).Split(':');
            int level;
            int xpReward;
            int statId;
            int sideTokenReward;
            if (values.Length != 4
                || !int.TryParse(values[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out level)
                || !int.TryParse(values[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out xpReward)
                || !int.TryParse(values[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out statId)
                || !int.TryParse(values[3], NumberStyles.Integer, CultureInfo.InvariantCulture, out sideTokenReward))
            {
                return false;
            }

            var parsed = new DailyMissionRewardSnapshot(level, xpReward, statId, sideTokenReward);
            if (!IsValidCompletionSnapshot(parsed))
            {
                return false;
            }

            snapshot = parsed;
            return true;
        }

        internal static string CreateSideTokenEffectReference(int statId, int reward)
        {
            if (!IsValidSideTokenResolution(statId, reward))
            {
                throw new ArgumentOutOfRangeException("reward");
            }

            return SideTokenEffectReferencePrefix
                   + statId.ToString(CultureInfo.InvariantCulture) + ":"
                   + reward.ToString(CultureInfo.InvariantCulture);
        }

        internal static bool TryParseSideTokenEffectReference(
            string effectReference,
            out int statId,
            out int reward)
        {
            statId = NoSideTokenStatId;
            reward = 0;
            if (string.IsNullOrWhiteSpace(effectReference)
                || !effectReference.StartsWith(SideTokenEffectReferencePrefix, StringComparison.Ordinal))
            {
                return false;
            }

            string[] values = effectReference.Substring(SideTokenEffectReferencePrefix.Length).Split(':');
            return values.Length == 2
                   && int.TryParse(values[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out statId)
                   && int.TryParse(values[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out reward)
                   && IsValidSideTokenResolution(statId, reward);
        }

        internal static bool TryResolveAppliedSideTokenEffectReference(
            string effectReference,
            out int statId,
            out int reward)
        {
            if (TryParseSideTokenEffectReference(effectReference, out statId, out reward))
            {
                return true;
            }

            if (string.Equals(
                effectReference,
                LegacyOmniSideTokenEffectReference,
                StringComparison.Ordinal))
            {
                statId = OmniSideTokenStatId;
                reward = SideTokensPerTier;
                return true;
            }

            statId = NoSideTokenStatId;
            reward = 0;
            return false;
        }

        internal static bool TryResolveAppliedSideTokenForSnapshot(
            DailyMissionRewardSnapshot snapshot,
            string effectReference,
            out int statId,
            out int reward)
        {
            statId = NoSideTokenStatId;
            reward = 0;
            if (!IsValidCompletionSnapshot(snapshot)
                || !TryResolveAppliedSideTokenEffectReference(effectReference, out statId, out reward))
            {
                return false;
            }

            return string.Equals(
                       effectReference,
                       LegacyOmniSideTokenEffectReference,
                       StringComparison.Ordinal)
                   || (statId == snapshot.SideTokenStatId && reward == snapshot.SideTokenReward);
        }

        internal static string CreateFullLevelXpEffectReference(int level, int reward)
        {
            if (reward <= 0 || reward != GetFullRubikaLevelXpReward(level))
            {
                throw new ArgumentOutOfRangeException("reward");
            }

            return FullLevelXpEffectReferencePrefix
                   + level.ToString(CultureInfo.InvariantCulture) + ":"
                   + reward.ToString(CultureInfo.InvariantCulture);
        }

        internal static bool TryParseFullLevelXpEffectReference(
            string effectReference,
            out int level,
            out int reward)
        {
            level = 0;
            reward = 0;
            if (string.IsNullOrWhiteSpace(effectReference)
                || !effectReference.StartsWith(FullLevelXpEffectReferencePrefix, StringComparison.Ordinal))
            {
                return false;
            }

            string[] values = effectReference.Substring(FullLevelXpEffectReferencePrefix.Length).Split(':');
            return values.Length == 2
                   && int.TryParse(values[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out level)
                   && int.TryParse(values[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out reward)
                   && reward > 0
                   && reward == GetFullRubikaLevelXpReward(level);
        }

        private static bool IsValidCompletionSnapshot(DailyMissionRewardSnapshot snapshot)
        {
            if (snapshot == null
                || snapshot.XpReward <= 0
                || snapshot.XpReward != GetFullRubikaLevelXpReward(snapshot.LevelBefore))
            {
                return false;
            }

            int expectedTokenReward = SideTokensPerTier * GetMissionTokenTierCount(snapshot.LevelBefore);
            return (snapshot.SideTokenStatId == NoSideTokenStatId && snapshot.SideTokenReward == 0)
                   || ((snapshot.SideTokenStatId == ClanSideTokenStatId
                        || snapshot.SideTokenStatId == OmniSideTokenStatId)
                       && snapshot.SideTokenReward == expectedTokenReward);
        }

        private static bool IsValidSideTokenResolution(int statId, int reward)
        {
            return (statId == NoSideTokenStatId && reward == 0)
                   || ((statId == ClanSideTokenStatId || statId == OmniSideTokenStatId)
                       && reward > 0
                       && reward <= SideTokensPerTier * 9
                       && reward % SideTokensPerTier == 0);
        }
    }

    internal sealed class DailyMissionRewardSnapshot
    {
        internal DailyMissionRewardSnapshot(
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
