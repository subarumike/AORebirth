namespace ZoneEngine_New.Tests;

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SmokeLounge.AOtomation.Messaging.GameData;
using ZoneEngine_New.Core.Ai;
using ZoneEngine_New.Core.Configuration;
using ZoneEngine_New.Core.Entities;
using ZoneEngine_New.Core.GameData;
using ZoneEngine_New.Core.Helpers;
using ZoneEngine_New.Core.Mobs;
using ZoneEngine_New.Core.Playfield;
using ZoneEngine_New.Core.Playfield.Locality;
using ZoneEngine_New.Core.WorldSimulation;

using Vector3 = AORebirth.Core.Vector.Vector3;

[TestClass]
public sealed class MedusaProximityAggroTests
{
    public static IEnumerable<object[]> MedusaHashes()
    {
        foreach (string hash in new[] { "ME03", "ME04", "ME15", "ME16", "ME19", "ME20" })
            yield return [hash];
    }

    [TestMethod]
    [DynamicData(nameof(MedusaHashes), DynamicDataSourceType.Method)]
    public void AuthoredMedusaRuleBlocksInitialHateForAllFemaleBreeds(string hash)
    {
        using var world = new AggroWorld();
        NpcBrain brain = world.AddNpc(hash);

        foreach (Breed breed in new[] { Breed.Solitus, Breed.Opifex, Breed.Nanomage })
        {
            Player player = world.AddPlayer(breed, Gender.Female);
            brain.TryProximityAggro(player);
            Assert.IsFalse(brain.Hate.Contains(player.Identity), $"Unprovoked {breed} female");
        }

        Assert.IsTrue(brain.Hate.IsEmpty);
    }

    [TestMethod]
    [DynamicData(nameof(MedusaHashes), DynamicDataSourceType.Method)]
    public void MaleAndAtroxStillPassThroughRangeSightAndLevelChecks(string hash)
    {
        using var world = new AggroWorld();
        NpcBrain brain = world.AddNpc(hash);

        foreach ((Breed breed, Gender sex) in new[] { (Breed.Solitus, Gender.Male), (Breed.Atrox, Gender.Neutral) })
        {
            Player player = world.AddPlayer(breed, sex);
            player.Position = new Vector3(80, 0, 0);
            brain.TryProximityAggro(player);
            Assert.IsFalse(brain.Hate.Contains(player.Identity), $"{breed} out of range");

            player.Position = new Vector3(2, 0, 0);
            player.Playfield = null;
            brain.TryProximityAggro(player);
            Assert.IsFalse(brain.Hate.Contains(player.Identity), $"{breed} out of sight");

            player.Playfield = world.Playfield;
            brain.Npc.Stats.Set(CharacterStat.Level, 1);
            brain.TryProximityAggro(player);
            Assert.IsFalse(brain.Hate.Contains(player.Identity), $"{breed} sees a gray Medusa");

            brain.Npc.Stats.Set(CharacterStat.Level, 175);
            brain.TryProximityAggro(player);
            Assert.AreEqual(NpcAiRules.ProximityHate, brain.Hate.ThreatOf(player.Identity), $"{breed} eligible");
        }
    }

    [TestMethod]
    [DynamicData(nameof(MedusaHashes), DynamicDataSourceType.Method)]
    public void ProximityScanEvaluatesEachPlayersActualSex(string hash)
    {
        using var world = new AggroWorld();
        NpcBrain brain = world.AddNpc(hash);
        Player woman = world.AddPlayer(Breed.Solitus, Gender.Female);
        Player man = world.AddPlayer(Breed.Opifex, Gender.Male);
        Player atrox = world.AddPlayer(Breed.Atrox, Gender.Neutral);
        woman.Stats.Set(CharacterStat.VisualSex, (int)Gender.Male);
        man.Stats.Set(CharacterStat.VisualSex, (int)Gender.Female);

        brain.ScanProximity();

        Assert.IsFalse(brain.Hate.Contains(woman.Identity));
        Assert.AreEqual(NpcAiRules.ProximityHate, brain.Hate.ThreatOf(man.Identity));
        Assert.AreEqual(NpcAiRules.ProximityHate, brain.Hate.ThreatOf(atrox.Identity));
    }

    [TestMethod]
    [DynamicData(nameof(MedusaHashes), DynamicDataSourceType.Method)]
    public void AProvokingFemaleCharacterCanDealAndReceiveDamage(string hash)
    {
        using var world = new AggroWorld();
        NpcBrain brain = world.AddNpc(hash);
        Player player = world.AddPlayer(Breed.Solitus, Gender.Female);
        brain.TryProximityAggro(player);
        Assert.IsTrue(brain.Hate.IsEmpty);

        Assert.IsTrue(CombatRules.CanAttack(player, brain.Npc));
        brain.Npc.ApplyDamage(player, 12, HitType.Normal);

        Assert.AreEqual(88, brain.Npc.Stats.GetOrZero(CharacterStat.Health));
        Assert.AreEqual(12f, brain.Hate.ThreatOf(player.Identity));
        Assert.IsTrue(CombatRules.CanAttack(brain.Npc, player));
        player.ApplyDamage(brain.Npc, 9, HitType.Normal);
        Assert.AreEqual(91, player.Stats.GetOrZero(CharacterStat.Health));
    }

    [TestMethod]
    [DynamicData(nameof(MedusaHashes), DynamicDataSourceType.Method)]
    public void ProximityScanKeepsAnExistingFemaleHateTarget(string hash)
    {
        using var world = new AggroWorld();
        NpcBrain brain = world.AddNpc(hash);
        Player player = world.AddPlayer(Breed.Opifex, Gender.Female);
        brain.AddThreatFrom(player, 15f);

        brain.ScanProximity();

        Assert.AreEqual(15f, brain.Hate.ThreatOf(player.Identity));
        Assert.IsTrue(brain.HasNearbyHate());
    }

    [TestMethod]
    [DynamicData(nameof(MedusaHashes), DynamicDataSourceType.Method)]
    public void MedusasCanAssistInLegitimateCombatAgainstFemaleAttackers(string hash)
    {
        using var world = new AggroWorld();
        NpcBrain victim = world.AddNpc(hash);
        NpcBrain helper = world.AddNpc(hash, 5);
        Player player = world.AddPlayer(Breed.Nanomage, Gender.Female);
        helper.ScanProximity();
        Assert.IsTrue(helper.Hate.IsEmpty);

        victim.Npc.ApplyDamage(player, 12, HitType.Normal);

        Assert.AreEqual(12f, victim.Hate.ThreatOf(player.Identity));
        Assert.AreEqual(NpcAiRules.ProximityHate, helper.Hate.ThreatOf(player.Identity));
    }

    [TestMethod]
    [DynamicData(nameof(MedusaHashes), DynamicDataSourceType.Method)]
    public void ALeashResetRestoresNormalSightAndRetaliationRules(string hash)
    {
        using var world = new AggroWorld();
        NpcBrain brain = world.AddNpc(hash);
        Player woman = world.AddPlayer(Breed.Nanomage, Gender.Female);
        brain.Npc.ApplyDamage(woman, 12, HitType.Normal);
        Assert.IsTrue(brain.Hate.Contains(woman.Identity));

        woman.Position = new Vector3(250, 0, 0);
        brain.Tick(NpcAiRules.BrainThinkSeconds);

        Assert.IsFalse(brain.IsEvading);
        Assert.IsTrue(brain.Hate.IsEmpty);
        Assert.AreEqual(100, brain.Npc.Stats.GetOrZero(CharacterStat.Health));

        woman.Position = new Vector3(2, 0, 0);
        brain.TryProximityAggro(woman);
        Assert.IsTrue(brain.Hate.IsEmpty);
        Player man = world.AddPlayer(Breed.Solitus, Gender.Male);
        brain.TryProximityAggro(man);
        Assert.AreEqual(NpcAiRules.ProximityHate, brain.Hate.ThreatOf(man.Identity));

        brain.Npc.ApplyDamage(woman, 12, HitType.Normal);
        Assert.AreEqual(12f, brain.Hate.ThreatOf(woman.Identity));
    }

    [TestMethod]
    public void UnrelatedAggressiveNpcOfTheSameFamilyStillAggrosAFemalePlayer()
    {
        using var world = new AggroWorld();
        NpcBrain brain = world.AddNpc("EV01");
        Player player = world.AddPlayer(Breed.Solitus, Gender.Female);

        brain.TryProximityAggro(player);

        Assert.AreEqual(NpcAiRules.ProximityHate, brain.Hate.ThreatOf(player.Identity));
    }

    [TestMethod]
    [DynamicData(nameof(MedusaHashes), DynamicDataSourceType.Method)]
    public void TemplateRuleAppliesWithoutASpawnSpecificRule(string hash)
    {
        using var world = new AggroWorld();
        NpcBrain brain = world.AddNpc(hash, useTemplateRule: true);
        Player player = world.AddPlayer(Breed.Solitus, Gender.Female);

        brain.TryProximityAggro(player);

        Assert.IsTrue(brain.Hate.IsEmpty);
    }

    sealed class AggroWorld : IDisposable
    {
        readonly ServiceProvider _services;
        readonly DynelRegistry _registry = new();
        readonly PlayfieldLocality _locality = new(620, null);
        int _nextInstance = 1_000;

        public Playfield Playfield { get; } = (Playfield)RuntimeHelpers.GetUninitializedObject(typeof(Playfield));

        public AggroWorld()
        {
            var settings = new GameplaySettings { SocialAggro = new GameplaySocialAggro { DefaultRadius = 12f } };
            var data = new GameDataStore(new StubLogger(), null, Path.Combine(AppContext.BaseDirectory, "Fixtures"));
            _services = new ServiceCollection()
                .AddSingleton(_registry)
                .AddSingleton(_locality)
                .AddSingleton<IGameData>(data)
                .AddSingleton(new WorldSimulationAccess())
                .AddSingleton(settings)
                .BuildServiceProvider();

            SetPlayfieldField("_serviceProvider", _services);
            SetPlayfieldField("_lifecycleSync", new Lock());
            SetPlayfieldField("_pendingStatRebases", new ConcurrentDictionary<Character, byte>());
            SetPlayfieldField("<Identity>k__BackingField", new Identity { Type = IdentityType.Playfield2, Instance = 620 });
            SetPlayfieldField("<FightModes>k__BackingField", DistrictFightModes.None);
        }

        public NpcBrain AddNpc(string hash, double x = 0, bool useTemplateRule = false)
        {
            var npc = new NpcCharacter(new Identity { Type = IdentityType.CanbeAffected, Instance = ++_nextInstance },
                new StubItemBuilder())
            {
                SpawnHash = useTemplateRule ? null : hash,
                MobTemplate = new MobTemplate { Hash = hash },
                Position = new Vector3(x, 0, 0),
            };
            npc.Stats.Set(CharacterStat.Level, 175);
            npc.Stats.Set(CharacterStat.Side, (int)Side.Monster);
            npc.Stats.Set(CharacterStat.NPCFamily, 41);
            npc.Stats.Set(CharacterStat.BreedHostility, 60);
            npc.Stats.Set(CharacterStat.ProximityRangeOutdoors, 25);
            npc.Stats.Set(CharacterStat.ProximityRangeIndoors, 25);
            npc.Stats.Set(CharacterStat.MaxHealth, 100);
            npc.Stats.Set(CharacterStat.Health, 100);
            Place(npc);
            return NpcBrain.Create(npc, npc.Position);
        }

        public Player AddPlayer(Breed breed, Gender sex)
        {
            Player player = TestWorld.CreatePlayer(++_nextInstance);
            player.Position = new Vector3(2, 0, 0);
            player.Stats.Set(CharacterStat.Level, 175);
            player.Stats.Set(CharacterStat.Breed, (int)breed);
            player.Stats.Set(CharacterStat.Sex, (int)sex);
            player.Stats.Set(CharacterStat.MaxHealth, 100);
            player.Stats.Set(CharacterStat.Health, 100);
            Place(player);
            return player;
        }

        void Place(Character character)
        {
            character.Playfield = Playfield;
            _registry.Register(character);
            _locality.RegisterDynel(character);
        }

        void SetPlayfieldField(string name, object value)
            => typeof(Playfield).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(Playfield, value);

        public void Dispose() => _services.Dispose();
    }
}
