namespace ZoneEngine_New.Tests;

using Microsoft.VisualStudio.TestTools.UnitTesting;
using SmokeLounge.AOtomation.Messaging.GameData;
using ZoneEngine.Core.Missions;

[TestClass]
public sealed class GeneratedMissionTokenPolicyTests
{
    [TestMethod]
    public void ProgressUsesExactAmbientPopulationAndNeverTheOlderEightySixPercentThreshold()
    {
        Assert.AreEqual(100, MissionAcgTokenRewardPolicy.CalculatePercent(0, 0));
        Assert.AreEqual(66, MissionAcgTokenRewardPolicy.CalculatePercent(2, 3));
        foreach (int percent in new[] { 0, 66, 86, 99 })
        {
            Assert.IsTrue(MissionAcgTokenRewardPolicy.TryResolve(percent, 220, Side.Clan, out var data, out string reason), reason);
            Assert.AreEqual(0, data.Disposition, "Below-full progress must remain explicitly unresolved, never a random reward policy.");
            Assert.AreEqual(0, data.Count);
        }
    }

    [TestMethod]
    public void FullProgressPreservesEveryOfficialLevelTokenCellAndExactFactionPairs()
    {
        for (int level = 1; level <= 220; level++)
        {
            Assert.IsTrue(MissionLevelTable.TryGetTokenReward(level, out int expected, out string reason), reason);
            foreach (Side side in new[] { Side.Clan, Side.Omni })
            {
                Assert.IsTrue(MissionAcgTokenRewardPolicy.TryResolve(100, level, side, out var data, out reason), reason);
                Assert.AreEqual(2, data.Disposition); Assert.AreEqual(expected, data.Count);
                Assert.AreEqual(side == Side.Clan ? 103910 : 103908, data.LowId); Assert.AreEqual(data.LowId + 1, data.HighId);
            }
        }
        Assert.IsTrue(MissionAcgTokenRewardPolicy.TryResolve(100, 220, Side.Neutral, out var neutral, out _));
        Assert.AreEqual(1, neutral.Disposition); Assert.AreEqual(0, neutral.Count);
        Assert.IsFalse(MissionAcgTokenRewardPolicy.TryResolve(100, 220, Side.Monster, out _, out _));
    }
}
