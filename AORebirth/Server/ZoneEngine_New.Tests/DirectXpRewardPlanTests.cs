namespace ZoneEngine_New.Tests;

using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SmokeLounge.AOtomation.Messaging.GameData;
using ZoneEngine_New.Core.Entities;
using ZoneEngine_New.Core.Missions;

[TestClass]
public sealed class DirectXpRewardPlanTests
{
    [TestMethod]
    public void DirectRewardIsNotCappedAndPlansWholeLevelBeforePublication()
    {
        var player = TestWorld.CreatePlayer(51);
        player.Stats.Set(CharacterStat.Level, 1); player.Stats.Set(CharacterStat.IP, 1500);
        player.Stats.Set(CharacterStat.XP, 0); player.Stats.Set(CharacterStat.Health, 1);
        var plan = DirectXpRewardPlan.Create(player, 1500);
        Assert.AreEqual(1, player.Stats.GetOrOne(CharacterStat.Level));
        Assert.AreEqual(0, player.Stats.GetOrZero(CharacterStat.XP));
        Assert.AreEqual(1, player.Stats.GetOrZero(CharacterStat.Health));
        Assert.AreEqual(2, plan.LevelAfter); Assert.AreEqual(1500, plan.ExperienceAfter);
        Assert.AreEqual(50, plan.Stats[CharacterStat.UnsavedXP]);
        Assert.AreEqual(1500, plan.Stats[CharacterStat.LastXP]);
        Assert.AreEqual(2600, plan.Stats[CharacterStat.NextXP]);
        Assert.AreEqual(5500, plan.Stats[CharacterStat.IP]);
        Assert.IsTrue(plan.Stats[CharacterStat.Health] > 1);
        plan.PublishAfterCommit(player);
        Assert.AreEqual(2, player.Stats.GetOrOne(CharacterStat.Level));
        Assert.AreEqual(1500, player.Stats.GetOrZero(CharacterStat.XP));
        Assert.AreEqual(5500, player.Stats.GetOrZero(CharacterStat.IP));
    }

    [TestMethod]
    public void SubFloorLegacyProgressNormalizesAgainstSameAcceptedLevelTable()
    {
        var player = TestWorld.CreatePlayer(52);
        player.Stats.Set(CharacterStat.Level, 10); player.Stats.Set(CharacterStat.XP, 10);
        player.Stats.Set(CharacterStat.SavedXP, 38_650);
        var plan = DirectXpRewardPlan.Create(player, 100);
        Assert.AreEqual(38_760, plan.ExperienceAfter);
        Assert.AreEqual(110, plan.Stats[CharacterStat.UnsavedXP]);
        Assert.IsFalse(plan.Stats.ContainsKey(CharacterStat.SavedXP));
        Assert.AreEqual(10, player.Stats.GetOrZero(CharacterStat.XP));
    }

    [TestMethod]
    public void ShadowLevelUsesSkConversionAndPreservesXpWatermark()
    {
        var player = TestWorld.CreatePlayer(53);
        player.Stats.Set(CharacterStat.Level, 200); player.Stats.Set(CharacterStat.XP, 2_061_453_150);
        player.Stats.Set(CharacterStat.SK, 79_999); player.Stats.Set(CharacterStat.IP, 1);
        var plan = DirectXpRewardPlan.Create(player, 1000);
        Assert.AreEqual(201, plan.LevelAfter); Assert.AreEqual(80_000, plan.Stats[CharacterStat.SK]);
        Assert.AreEqual(96_000, plan.Stats[CharacterStat.NextSK]); Assert.AreEqual(0, plan.Stats[CharacterStat.NextXP]);
        Assert.AreEqual(2_061_453_150, plan.ExperienceAfter);
        Assert.IsFalse(plan.Stats.ContainsKey(CharacterStat.XP));
        Assert.AreEqual(200, player.Stats.GetOrOne(CharacterStat.Level));
    }

    [TestMethod]
    public void PlanningDoesNotTreatAnUnownedOldBonusAsARebasedContribution()
    {
        var baseline = TestWorld.CreatePlayer(54);
        baseline.Stats.Set(CharacterStat.Level, 1);
        var plain = DirectXpRewardPlan.Create(baseline, 1500);
        baseline.Stats.AddBonus(CharacterStat.MaxHealth, 40);
        var buffed = DirectXpRewardPlan.Create(baseline, 1500);
        Assert.AreEqual(plain.Stats[CharacterStat.MaxHealth], buffed.Stats[CharacterStat.MaxHealth]);
        Assert.AreEqual(plain.Stats[CharacterStat.Health], buffed.Stats[CharacterStat.Health]);
        Assert.AreEqual(40, baseline.Stats.GetOrZero(CharacterStat.MaxHealth, StatDetail.Bonus));
        buffed.PublishAfterCommit(baseline);
        Assert.AreEqual(buffed.Stats[CharacterStat.Health], baseline.Stats.GetOrZero(CharacterStat.Health, StatDetail.Base));
        Assert.AreEqual(baseline.Stats.GetOrZero(CharacterStat.MaxHealth), baseline.Stats.GetOrZero(CharacterStat.Health));
    }

    [TestMethod]
    public void PersistedFullBarRewardPreservesDistinctDeathPoolWithoutAwardingIt()
    {
        var player = TestWorld.CreatePlayer(57);
        player.Stats.Set(CharacterStat.Level, 2); player.Stats.Set(CharacterStat.XP, 1500);
        player.Stats.Set(CharacterStat.UnsavedXP, 5000); player.Stats.Set(CharacterStat.SavedXP, 1450);
        var persisted = DirectXpRewardPlan.CreatePersistedReward(player, 2600);
        Assert.AreEqual(3, persisted.LevelAfter); Assert.AreEqual(4100, persisted.ExperienceAfter);
        Assert.AreEqual(5000, persisted.Stats[CharacterStat.UnsavedXP]);
        Assert.IsFalse(persisted.Stats.ContainsKey(CharacterStat.SavedXP));
        Assert.AreEqual(1500, player.Stats.GetOrZero(CharacterStat.XP));
        var direct = DirectXpRewardPlan.Create(player, 2600);
        Assert.AreEqual(50, direct.Stats[CharacterStat.UnsavedXP]);
    }

    [TestMethod]
    public void ZeroRewardIsNoOpAndForeignPublicationFailsClosed()
    {
        var player = TestWorld.CreatePlayer(55);
        player.Stats.Set(CharacterStat.Level, 220);
        var plan = DirectXpRewardPlan.Create(player, 0);
        Assert.AreEqual(0, plan.Stats.Count);
        Assert.ThrowsException<InvalidOperationException>(() => plan.PublishAfterCommit(TestWorld.CreatePlayer(56)));
        player.Stats.Set(CharacterStat.XP, 2_061_453_150);
        var capped = DirectXpRewardPlan.Create(player, 1500);
        Assert.AreEqual(1500, capped.RequestedReward);
        Assert.AreEqual(0, capped.Stats.Count);
        Assert.AreEqual(2_061_453_150, capped.ExperienceAfter);
        capped.PublishAfterCommit(player);
        Assert.AreEqual(220, player.Stats.GetOrOne(CharacterStat.Level));
        Assert.AreEqual(2_061_453_150, player.Stats.GetOrZero(CharacterStat.XP));
    }
}
