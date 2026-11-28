namespace ZoneEngine_New.Tests;

using System.IO;
using System.Linq;
using AORebirth.Core.GameData;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SmokeLounge.AOtomation.Messaging.GameData;
using ZoneEngine_New.Core.Characters;
using ZoneEngine_New.Core.Data;
using ZoneEngine_New.Core.Entities;
using ZoneEngine_New.Core.GameData;
using ZoneEngine_New.Core.Missions;

[TestClass]
public sealed class RewardXpDurabilityTests
{
    const int FirstTier = 10;
    const int SecondTier = 11;

    [TestMethod]
    public void SuppressedXpCommitsDeathPoolAndItsWireProjection()
    {
        var player = TestWorld.CreatePlayer(61);
        player.SetXpGainOptions(16);
        player.Stats.Set(CharacterStat.DeathXpPool, int.MaxValue - 4);
        player.Stats.Set(CharacterStat.UnsavedXP, 17);

        var plan = DirectXpRewardPlan.CreatePersistedReward(player, 20);

        Assert.IsTrue(plan.NormalXpSuppressed);
        Assert.AreEqual(20, plan.PooledXp);
        Assert.AreEqual(int.MaxValue, plan.Stats[CharacterStat.DeathXpPool]);
        Assert.AreEqual(int.MaxValue, plan.Stats[CharacterStat.UnsavedXP]);
        Assert.AreEqual(17, player.Stats.GetOrZero(CharacterStat.UnsavedXP));
        Assert.IsFalse(plan.Stats.ContainsKey(CharacterStat.XP));

        plan.PublishAfterCommit(player);
        plan.PublishAfterCommit(player);
        Assert.AreEqual(int.MaxValue, player.Stats.GetOrZero(CharacterStat.DeathXpPool));
        Assert.AreEqual(int.MaxValue, player.Stats.GetOrZero(CharacterStat.UnsavedXP));
    }

    [TestMethod]
    public void ResearchProgressPublishesTheCommittedRemainingValue()
    {
        var player = CreateResearcher(remaining: 25);
        var plan = DirectXpRewardPlan.CreatePersistedReward(player, 20);
        var research = plan.ResearchMutation!;

        Assert.AreEqual(10L, plan.ResearchXp);
        Assert.AreEqual(10, plan.ExperienceAfter);
        Assert.AreEqual(FirstTier, research.ResearchId);
        Assert.AreEqual(25, research.ExpectedRemainingXp.GetValueOrDefault());
        Assert.AreEqual(15, research.RemainingXp);
        Assert.IsTrue(player.Research.TryGetRemaining(FirstTier, out int before));
        Assert.AreEqual(25, before);

        plan.PublishAfterCommit(player);
        plan.PublishAfterCommit(player);
        Assert.IsTrue(player.Research.TryGetRemaining(FirstTier, out int after));
        Assert.AreEqual(15, after);
        Assert.IsFalse(player.Research.IsDirty);
    }

    [TestMethod]
    public void FreshHydrationRecoversFromCommittedServerDeathPoolStat()
    {
        var before = TestWorld.CreatePlayer(62);
        before.SetXpGainOptions(16);
        var plan = DirectXpRewardPlan.CreatePersistedReward(before, 20);
        var identity = new Identity { Type = IdentityType.CanbeAffected, Instance = 62 };
        var items = new StubItemBuilder();
        var fresh = new Player(identity, new StubLogger(), items);
        var data = new StubGameData(HashItemCatalog.Parse("{}")) { AllowMissingXpLevels = true };
        new PlayerHydrator(items, data).Apply(fresh, new CharacterHydrationResult
        {
            Character = new CharacterRecord { Id = identity.Instance, Name = "Xp test", Playfield = 4582, HeadingW = 1 },
            Stats = CharacterHydrationValidator.RequiredSpawnStats
                .Where(stat => !plan.Stats.ContainsKey(stat))
                .Select(stat => new StatRecord { StatId = (int)stat, StatValue = 1 })
                .Concat(plan.Stats.Select(pair => new StatRecord { StatId = (int)pair.Key, StatValue = pair.Value }))
                .ToArray()
        });

        Assert.AreEqual(20, fresh.Stats.GetOrZero(CharacterStat.DeathXpPool));
        int recovered = fresh.TakeDeathPoolRecovery(10);
        Assert.IsTrue(recovered > 0);
        Assert.AreEqual(20 - recovered, fresh.Stats.GetOrZero(CharacterStat.DeathXpPool));
        Assert.AreEqual(20 - recovered, fresh.Stats.GetOrZero(CharacterStat.UnsavedXP));
        Assert.IsTrue(StatCollection.IsServerOnly(CharacterStat.DeathXpPool));
    }

    [TestMethod]
    public void ResearchCompletionPlansTierAndNextGoalBeforePublication()
    {
        var player = CreateResearcher(remaining: 5);
        var plan = DirectXpRewardPlan.CreatePersistedReward(player, 20);

        Assert.AreEqual(0, plan.ResearchMutation!.RemainingXp);
        Assert.AreEqual(5L, plan.ResearchXp);
        Assert.AreEqual(15, plan.ExperienceAfter);
        Assert.AreEqual(SecondTier, plan.Stats[CharacterStat.PersonalResearchGoal]);
        Assert.IsFalse(player.TrainedPerks.Contains(FirstTier));
        Assert.AreEqual(FirstTier, player.Stats.GetOrZero(CharacterStat.PersonalResearchGoal));

        plan.PublishAfterCommit(player);
        Assert.IsTrue(player.TrainedPerks.Contains(FirstTier));
        Assert.IsFalse(player.Research.TryGetRemaining(FirstTier, out _));
        Assert.AreEqual(SecondTier, player.Stats.GetOrZero(CharacterStat.PersonalResearchGoal));
        Assert.IsFalse(player.TrainedPerks.IsDirty);
        Assert.IsFalse(player.Research.IsDirty);
    }

    static Player CreateResearcher(int remaining)
    {
        DirectoryInfo directory = Directory.CreateTempSubdirectory("aorebirth-reward-research-");
        string path = Path.Combine(directory.FullName, GameDataPaths.ResearchFileName);
        try
        {
            File.WriteAllText(path, """
                {"PersonalResearch":[{"Name":"Test","Tiers":[
                  {"Id":10,"Tier":1,"Xp":50000,"MinLevel":1},
                  {"Id":11,"Tier":2,"Xp":50000,"MinLevel":1}
                ]}]}
                """);
            var player = new Player(new Identity { Type = IdentityType.CanbeAffected, Instance = 61 },
                new StubLogger(), new StubItemBuilder()) { ResearchCatalog = ResearchCatalog.Load(directory.FullName) };
            player.Stats.Set(CharacterStat.Level, 1);
            player.Stats.Set(CharacterStat.PersonalResearchGoal, FirstTier);
            player.SetPersonalResearchShare(50);
            player.LoadResearch([new ResearchProgressRecord(FirstTier, remaining)]);
            return player;
        }
        finally
        {
            File.Delete(path);
            directory.Delete();
        }
    }
}
