namespace ZoneEngine.Core.Missions
{
    using System;
    using SmokeLounge.AOtomation.Messaging.GameData;

    // Shared exact generated policy. Below full progress is unresolved, never a probability roll.
    internal static class MissionAcgTokenRewardPolicy
    {
        internal static int CalculatePercent(int killed, int total)
        {
            if (killed < 0 || total < 0 || killed > total) throw new ArgumentOutOfRangeException("killed");
            return total == 0 ? 100 : (int)Math.Min(100L, (long)killed * 100L / total);
        }

        internal static bool TryResolve(int percent, int level, Side side, out MissionAcgTokenRewardData result, out string failure)
        {
            result = null; failure = string.Empty;
            if (percent < 0 || percent > 100) { failure = "Invalid durable token progress."; return false; }
            if (percent < 100) { result = new MissionAcgTokenRewardData(0, 0, 0, 0, string.Empty); return true; }
            if (side == Side.Neutral) { result = new MissionAcgTokenRewardData(1, 0, 0, 0, string.Empty); return true; }
            int low, high; string name;
            if (side == Side.Clan) { low = 103910; high = 103911; name = "Clan Token"; }
            else if (side == Side.Omni) { low = 103908; high = 103909; name = "Omni Token"; }
            else { failure = "Generated token claim side is not a supported player faction."; return false; }
            int count;
            if (!MissionLevelTable.TryGetTokenReward(level, out count, out failure)) return false;
            result = new MissionAcgTokenRewardData(2, low, high, count, name);
            return true;
        }
    }

    internal sealed class MissionAcgTokenRewardData
    {
        internal MissionAcgTokenRewardData(int disposition, int lowId, int highId, int count, string name)
        { Disposition = disposition; LowId = lowId; HighId = highId; Count = count; Name = name; }
        internal int Disposition { get; private set; }
        internal int LowId { get; private set; }
        internal int HighId { get; private set; }
        internal int Count { get; private set; }
        internal string Name { get; private set; }
    }
}
