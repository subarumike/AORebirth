namespace ZoneEngine_New.Tests;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

using AORebirth.Core.GameData;
using AORebirth.Enums;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SmokeLounge.AOtomation.Messaging.GameData;

using ZoneEngine_New.Core.Ai;
using ZoneEngine_New.Core.Entities;
using ZoneEngine_New.Core.GameData;
using ZoneEngine_New.Core.Inventory;
using ZoneEngine_New.Core.Mobs;
using ZoneEngine_New.Core.Nanos;
using ZoneEngine_New.Core.NpcEditor;

[TestClass]
public sealed class NpcEditorStatsTests
{
    [TestMethod]
    public void SavedStatsMaterializeOnlyInTheSelectedBandAndSurviveReloadRestartAndRestore()
    {
        using var fixture = new Fixture();
        GameDataStore data = fixture.Open();
        var editor = new NpcEditorStats(data);
        NpcCharacter oldSpawn = fixture.Spawn(data, 20);
        NpcEditorStatsTarget target = NpcEditorRuntime.CaptureStatsTarget(oldSpawn);
        string original = File.ReadAllText(fixture.TemplateFile);
        Assert.AreEqual(0, target.BandIndex);

        var first = editor.Save(target, new(0, (int)CharacterStat.MaxHealth, 50), "StatsGM");
        Assert.AreEqual(1, first.Revision);
        Assert.AreEqual(100, first.History.Single().Before.Single(row => row.StatId == (int)CharacterStat.MaxHealth).Value);
        Assert.AreEqual(50, first.History.Single().After.Single(row => row.StatId == (int)CharacterStat.MaxHealth).Value);
        Assert.AreEqual("StatsGM", first.History.Single().Actor);
        Assert.AreEqual(100, oldSpawn.Stats.Get(CharacterStat.MaxHealth));
        Assert.AreEqual(40, fixture.Spawn(data, 20).Stats.Get(CharacterStat.Health));
        Assert.AreEqual(200, fixture.Spawn(data, 40).Stats.Get(CharacterStat.MaxHealth));
        Assert.AreEqual(50, fixture.Spawn(data, 30).Stats.Get(CharacterStat.MaxHealth));
        Assert.AreEqual(155, fixture.Spawn(data, 31).Stats.Get(CharacterStat.MaxHealth));

        var second = editor.Save(target, new(1, (int)CharacterStat.MaxNanoEnergy, 0), "StatsGM");
        Assert.AreEqual(2, second.Revision);
        Assert.IsTrue(data.ReloadNpcTemplates().Ok);
        Assert.AreEqual(0, fixture.Spawn(data, 24).Stats.Get(CharacterStat.CurrentNano));
        GameDataStore reopened = fixture.Open();
        NpcCharacter future = fixture.Spawn(reopened, 24);
        Assert.AreEqual(50, future.Stats.Get(CharacterStat.MaxHealth));
        Assert.AreEqual(43, future.Stats.Get(CharacterStat.Health));
        Assert.AreEqual(0, future.Stats.Get(CharacterStat.PercentRemainingNano));
        Assert.AreEqual("NPC editor template band override", future.MobTemplate!.EditorStatSources![(int)CharacterStat.MaxHealth]);
        Assert.AreEqual("Derived from the template band maximum override", future.MobTemplate.EditorStatSources[(int)CharacterStat.Health]);

        var restored = new NpcEditorStats(reopened).Restore(target, new(2, 1), "RollbackGM");
        Assert.AreEqual("restore", restored.History[^1].Action);
        Assert.AreEqual("RollbackGM", restored.History[^1].Actor);
        NpcCharacter recovered = fixture.Spawn(fixture.Open(), 20);
        Assert.AreEqual(50, recovered.Stats.Get(CharacterStat.MaxHealth));
        Assert.AreEqual(80, recovered.Stats.Get(CharacterStat.MaxNanoEnergy));
        Assert.AreEqual(original, File.ReadAllText(fixture.TemplateFile));
    }

    [TestMethod]
    public void PersistentEditsRejectDisallowedStatsOutOfBandVitalsAndStaleWrites()
    {
        using var fixture = new Fixture();
        GameDataStore data = fixture.Open();
        GameDataStore stale = fixture.Open();
        var editor = new NpcEditorStats(data);
        NpcEditorStatsTarget lower = NpcEditorRuntime.CaptureStatsTarget(fixture.Spawn(data, 20));
        NpcEditorStatsTarget upper = NpcEditorRuntime.CaptureStatsTarget(fixture.Spawn(data, 40));
        Assert.AreEqual(400, Assert.ThrowsExactly<NpcEditorRuntimeException>(() =>
            editor.Save(lower, new(0, (int)CharacterStat.Level, 300), "GM")).StatusCode);
        Assert.AreEqual(400, Assert.ThrowsExactly<NpcEditorRuntimeException>(() =>
            editor.Save(upper, new(0, (int)CharacterStat.Health, 190), "GM")).StatusCode);
        Assert.IsFalse(File.Exists(fixture.OverrideFile));

        editor.Save(lower, new(0, (int)CharacterStat.AMSModifier, 65), "GM");
        Assert.AreEqual(65, fixture.Spawn(data, 22).Stats.Get(CharacterStat.AMSModifier));
        Assert.AreEqual(409, Assert.ThrowsExactly<NpcEditorRuntimeException>(() =>
            editor.Save(lower, new(0, (int)CharacterStat.AMSModifier, 15), "GM")).StatusCode);
        Assert.AreEqual(409, Assert.ThrowsExactly<NpcEditorRuntimeException>(() =>
            new NpcEditorStats(stale).Save(upper, new(0, (int)CharacterStat.AMSModifier, 80), "OtherGM")).StatusCode);
        Assert.AreEqual(0, fixture.Spawn(fixture.Open(), 40).Stats.GetOrZero(CharacterStat.AMSModifier));
    }

    [TestMethod]
    public void LiveMaximumChangesPreserveNativeGearAndNanoBonusesAndValidCurrentValues()
    {
        using var fixture = new Fixture();
        NpcCharacter npc = fixture.Spawn(fixture.Open(), 20);
        npc.Stats.Set(CharacterStat.Health, 60);
        npc.Stats.Set(CharacterStat.CurrentNano, 30);
        npc.Equipment.Add(0, new Item
        {
            Definition = new ItemTemplate
            {
                Name = "Fixture lootgiver",
                SpellList = new()
                {
                    [EventType.OnWear] =
                    [TestNanos.Modify(CharacterStat.MaxHealth, 20), TestNanos.Modify(CharacterStat.MeleeAC, 5)]
                }
            }
        });
        NanoSpell nano = TestNanos.Create(1234, modifiers:
        [
            TestNanos.Modify(CharacterStat.MaxNanoEnergy, 20), TestNanos.Modify(CharacterStat.CurrentNano, 10),
            TestNanos.Modify(CharacterStat.AMSModifier, 10)
        ]);
        Assert.AreEqual(BuffApplyDecision.Apply, npc.TryApplyBuff(nano, npc.Identity, DateTime.UtcNow, out _, out _));
        Assert.AreEqual(120, npc.Stats.Get(CharacterStat.MaxHealth));
        Assert.AreEqual(60, npc.Stats.Get(CharacterStat.Health));
        Assert.AreEqual(100, npc.Stats.Get(CharacterStat.MaxNanoEnergy));
        Assert.AreEqual(40, npc.Stats.Get(CharacterStat.CurrentNano));

        NpcEditorRuntime.ApplyStat(npc, CharacterStat.MaxHealth, 100, 220);
        Assert.AreEqual(240, npc.Stats.Get(CharacterStat.MaxHealth));
        Assert.AreEqual(120, npc.Stats.Get(CharacterStat.Health));
        Assert.AreEqual(120, npc.Stats.Get(CharacterStat.Health, StatDetail.Base));
        Assert.AreEqual(50, npc.Stats.Get(CharacterStat.PercentRemainingHealth));
        Assert.AreEqual(20, npc.Stats.Get(CharacterStat.MaxHealth, StatDetail.Bonus));
        Assert.AreEqual(5, npc.Stats.Get(CharacterStat.MeleeAC, StatDetail.Bonus));

        NpcEditorRuntime.ApplyStat(npc, CharacterStat.MaxNanoEnergy, 80, 30);
        Assert.AreEqual(50, npc.Stats.Get(CharacterStat.MaxNanoEnergy));
        Assert.AreEqual(20, npc.Stats.Get(CharacterStat.CurrentNano));
        Assert.AreEqual(40, npc.Stats.Get(CharacterStat.PercentRemainingNano));
        Assert.AreEqual(20, npc.Stats.Get(CharacterStat.MaxNanoEnergy, StatDetail.Bonus));
        Assert.AreEqual(10, npc.Stats.Get(CharacterStat.CurrentNano, StatDetail.Bonus));
        Assert.AreEqual(400, Assert.ThrowsExactly<NpcEditorRuntimeException>(() =>
            NpcEditorRuntime.ApplyStat(npc, CharacterStat.MaxNanoEnergy, 30, 0)).StatusCode);
        Assert.AreEqual(50, npc.Stats.Get(CharacterStat.MaxNanoEnergy));
        Assert.AreEqual(20, npc.Stats.Get(CharacterStat.CurrentNano));

        NpcEditorRuntime.ApplyStat(npc, CharacterStat.AMSModifier, 0, 55);
        Assert.AreEqual(65, npc.Stats.Get(CharacterStat.AMSModifier));
        Assert.AreEqual(10, npc.Stats.Get(CharacterStat.AMSModifier, StatDetail.Bonus));
        Assert.AreEqual(1, npc.Buffs.Count);

        NpcEditorRuntime.SetStat(npc, CharacterStat.MaxHealth, 100, restoring: true, restoreDependent: 60);
        Assert.AreEqual(60, npc.Stats.Get(CharacterStat.Health));
        Assert.IsFalse(npc.IsDead);
    }

    [TestMethod]
    public void BehaviorStatsUseTheNativeAggressionAndDetectionInputs()
    {
        using var fixture = new Fixture();
        GameDataStore data = fixture.Open();
        NpcCharacter npc = fixture.Spawn(data, 20);
        Assert.IsFalse(NpcAiRules.IsProximityHostile(npc.Stats.GetOrZero(CharacterStat.BreedHostility)));
        NpcEditorRuntime.ApplyStat(npc, CharacterStat.BreedHostility, 0, 1);
        NpcEditorRuntime.ApplyStat(npc, CharacterStat.ProximityRangeIndoors, 0, 22);
        Assert.IsTrue(NpcAiRules.IsProximityHostile(npc.Stats.GetOrZero(CharacterStat.BreedHostility)));
        Assert.AreEqual(22f, NpcAiRules.ProximityAggroRangeFor(npc.Stats, outdoor: false));
        Assert.AreEqual(NpcAiRules.ProximityAggroRange, NpcAiRules.ProximityAggroRangeFor(npc.Stats, outdoor: true));
        Assert.AreEqual(400, Assert.ThrowsExactly<NpcEditorRuntimeException>(() =>
            NpcEditorRuntime.ApplyStat(npc, CharacterStat.ProximityRangeIndoors, 22, 100)).StatusCode);

        var editor = new NpcEditorStats(data);
        editor.Save(NpcEditorRuntime.CaptureStatsTarget(npc), new(0, (int)CharacterStat.BreedHostility, 1), "GM");
        Assert.AreEqual(1, fixture.Spawn(fixture.Open(), 20).Stats.Get(CharacterStat.BreedHostility));
        Assert.AreEqual(0, fixture.Spawn(fixture.Open(), 40).Stats.Get(CharacterStat.BreedHostility));
    }

    [TestMethod]
    public void LiveEditsRejectActiveBaseDependentEquipmentAndNanoPercentages()
    {
        using var fixture = new Fixture();
        GameDataStore data = fixture.Open();
        NpcCharacter npc = fixture.Spawn(data, 20);
        npc.Equipment.Add(0, new Item
        {
            Definition = new ItemTemplate
            {
                Name = "Fixture lootgiver",
                SpellList = new()
                {
                    [EventType.OnWear] = [Percent(CharacterStat.MaxHealth, 20)]
                }
            }
        });
        NanoSpell nano = TestNanos.Create(1234, modifiers: [Percent(CharacterStat.CurrentNano, 50)]);
        Assert.AreEqual(BuffApplyDecision.Apply, npc.TryApplyBuff(nano, npc.Identity, DateTime.UtcNow, out _, out _));
        Assert.AreEqual(120, npc.Stats.Get(CharacterStat.MaxHealth));
        Assert.AreEqual(60, npc.Stats.Get(CharacterStat.CurrentNano));

        Assert.AreEqual(400, Assert.ThrowsExactly<NpcEditorRuntimeException>(() =>
            NpcEditorRuntime.ApplyStat(npc, CharacterStat.MaxHealth, 100, 200)).StatusCode);
        Assert.AreEqual(400, Assert.ThrowsExactly<NpcEditorRuntimeException>(() =>
            NpcEditorRuntime.ApplyStat(npc, CharacterStat.MaxNanoEnergy, 80, 60)).StatusCode);
        Assert.AreEqual(400, Assert.ThrowsExactly<NpcEditorRuntimeException>(() =>
            NpcEditorRuntime.ApplyStat(npc, CharacterStat.CurrentNano, 40, 30)).StatusCode);
        Assert.AreEqual(120, npc.Stats.Get(CharacterStat.MaxHealth));
        Assert.AreEqual(60, npc.Stats.Get(CharacterStat.CurrentNano));

        var editor = new NpcEditorStats(data);
        var saved = editor.Save(NpcEditorRuntime.CaptureStatsTarget(npc), new(0, (int)CharacterStat.MaxHealth, 200), "GM");
        Assert.AreEqual(1, saved.Revision);
        Assert.AreEqual(200, fixture.Spawn(data, 20).Stats.Get(CharacterStat.MaxHealth));

        static ItemSpell Percent(CharacterStat stat, int percentage) => new()
        {
            FunctionType = (int)FunctionType.ModifyPercentage,
            Arguments = [(int)stat, percentage]
        };
    }

    [TestMethod]
    public void FamilyAndResourceScopesPersistWithExplicitPrecedenceAndRejectOverlappingFamilies()
    {
        using var fixture = new Fixture();
        GameDataStore data = fixture.Open();
        var editor = new NpcEditorStats(data);
        NpcEditorStatsTarget exact = NpcEditorRuntime.CaptureStatsTarget(fixture.Spawn(data, 20));
        NpcEditorStatsTarget family = Family("FAM1");
        NpcEditorStatsTarget overlapping = Family("FAM2");
        NpcEditorStatsTarget resource = exact with { Scope = "playfield", ResourcePlayfieldId = 777 };
        int ams = (int)CharacterStat.AMSModifier;

        editor.Save(family, new(0, ams, 40), "FamilyGM");
        Assert.AreEqual(409, Assert.ThrowsExactly<NpcEditorRuntimeException>(() =>
            editor.Save(overlapping, new(0, ams, 50), "OtherGM")).StatusCode);
        Assert.AreEqual(400, Assert.ThrowsExactly<NpcEditorRuntimeException>(() =>
            editor.Save(family, new(1, (int)CharacterStat.MaxHealth, 50), "FamilyGM")).StatusCode);
        editor.Save(exact, new(0, ams, 65), "TemplateGM");
        NpcEditorStatsState saved = editor.Save(resource, new(0, ams, 90), "ResourceGM");
        Assert.AreEqual("playfield", saved.Scope);
        Assert.AreEqual(777, saved.ResourcePlayfieldId);
        Assert.AreEqual("NPC editor resource playfield 777 band override", saved.Rows.Single(row => row.StatId == ams).Source);
        CollectionAssert.AreEquivalent(new[] { "Family FAM1", "Template band NPC1/1" }, saved.Overlaps);

        GameDataStore restarted = fixture.Open();
        Assert.AreEqual(90, fixture.Spawn(restarted, 20, resourceId: 777).Stats.Get(CharacterStat.AMSModifier));
        Assert.AreEqual(65, fixture.Spawn(restarted, 20, resourceId: 778).Stats.Get(CharacterStat.AMSModifier));
        Assert.AreEqual(40, fixture.Spawn(restarted, 40, resourceId: 777).Stats.Get(CharacterStat.AMSModifier));
        Assert.AreEqual(40, fixture.Spawn(restarted, 20, hash: "NPC2", resourceId: 777).Stats.Get(CharacterStat.AMSModifier));
        Assert.AreEqual(0, fixture.Spawn(restarted, 20, hash: "NPC3", resourceId: 777).Stats.GetOrZero(CharacterStat.AMSModifier));

        new NpcEditorStats(restarted).Restore(family, new(1, 0), "UndoGM");
        restarted = fixture.Open();
        Assert.AreEqual(0, fixture.Spawn(restarted, 20, hash: "NPC2", resourceId: 777).Stats.GetOrZero(CharacterStat.AMSModifier));
        Assert.AreEqual(90, fixture.Spawn(restarted, 20, resourceId: 777).Stats.Get(CharacterStat.AMSModifier));

        NpcEditorStatsTarget Family(string hash)
        {
            NpcEditorStatFamilyBinding binding = data.EditorStatsFamily(hash)!;
            return exact with { Scope = "family", FamilyHash = hash, ScopeSignature = binding.Signature, FamilyLeaves = binding.Leaves };
        }
    }

    sealed class Fixture : IDisposable
    {
        readonly string _root = Directory.CreateTempSubdirectory("aorebirth-npc-editor-stats-").FullName;

        internal Fixture()
        {
            Directory.CreateDirectory(Path.Combine(GameData, GameDataPaths.PlayfieldsFolderName));
            Directory.CreateDirectory(Overlay);
            File.WriteAllText(TemplateFile, $$$"""
                {"FAM1":{"Children":["NPC1","NPC2"]},"FAM2":{"Children":["NPC1","NPC3"]},
                 "NPC2":{"Templates":[{"Name":"Sibling","Level":20}]},
                 "NPC3":{"Templates":[{"Name":"Other Family","Level":20}]},
                 "NPC1":{"Templates":[{"Name":"Stats Fixture","Level":20,"Stats":{
                  "{{{(int)CharacterStat.MaxHealth}}}":100,"{{{(int)CharacterStat.Health}}}":80,
                  "{{{(int)CharacterStat.MaxNanoEnergy}}}":80,"{{{(int)CharacterStat.CurrentNano}}}":40,
                  "{{{(int)CharacterStat.BreedHostility}}}":0,"{{{(int)CharacterStat.ProximityRangeIndoors}}}":0}},
                 {"Name":"Stats Fixture","Level":40,"Stats":{
                  "{{{(int)CharacterStat.MaxHealth}}}":200,"{{{(int)CharacterStat.Health}}}":200,
                  "{{{(int)CharacterStat.MaxNanoEnergy}}}":100,"{{{(int)CharacterStat.CurrentNano}}}":50,
                  "{{{(int)CharacterStat.BreedHostility}}}":0,"{{{(int)CharacterStat.ProximityRangeIndoors}}}":0}}]}}
                """);
        }

        string GameData => Path.Combine(_root, "GameData");
        string Overlay => Path.Combine(_root, "Overlay");
        internal string TemplateFile => Path.Combine(GameData, GameDataPaths.NpcTemplatesFileName);
        internal string OverrideFile => Path.Combine(Overlay, NpcEditorStatsJournalFile.FileName);
        internal GameDataStore Open() => new(new StubLogger(), null, GameData, Overlay);

        internal NpcCharacter Spawn(GameDataStore data, int level, string hash = "NPC1", int resourceId = 0)
        {
            var branches = new List<MobTemplate>();
            data.CollectMobSpawns(hash, level, branches);
            MobTemplate template = branches.Single();
            var npc = new NpcCharacter(new Identity { Type = IdentityType.CanbeAffected, Instance = 77001 }, new StubItemBuilder())
            {
                MobTemplate = template
            };
            Dictionary<int, int> stats = data.ComposeNpcStats(template, level);
            data.ApplyScopedEditorStats(template, resourceId, stats);
            foreach ((int id, int value) in stats)
                npc.Stats.Set((CharacterStat)id, value);
            return npc;
        }

        public void Dispose() => Directory.Delete(_root, recursive: true);
    }
}
