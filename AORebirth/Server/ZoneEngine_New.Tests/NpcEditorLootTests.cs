namespace ZoneEngine_New.Tests;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

using AORebirth.Core.GameData;
using AORebirth.Enums;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SmokeLounge.AOtomation.Messaging.GameData;
using ZoneEngine_New.Core.Entities;
using ZoneEngine_New.Core.GameData;
using ZoneEngine_New.Core.Inventory;
using ZoneEngine_New.Core.Mobs;
using ZoneEngine_New.Core.NpcEditor;

[TestClass]
public sealed class NpcEditorLootTests
{
    const string TargetHash = "MOB1";
    const string Actor = "TestGM";
    static readonly NpcEditorLootEntry Drop = new("ITM1", 100, 1, 5);

    [TestMethod]
    public void InspectReportsCarriedItemsEquipmentAndNestedHashRolls()
    {
        using var fixture = new Fixture();
        GameDataStore data = fixture.Open();
        var editor = new NpcEditorLoot(data, fixture.Items);
        NpcCharacter npc = fixture.Spawn(data, TargetHash);
        Assert.IsTrue(npc.TryCarryLoot(new Item
        {
            Definition = fixture.Items.Require(1001), LowId = 1001, HighId = 1001, Quality = 20, StackCount = 2
        }));
        Assert.IsTrue(npc.Equipment.Add(0, new Item
        {
            Definition = new ItemTemplate
            {
                Name = "Lootgiver fixture",
                SpellList = new()
                {
                    [EventType.OnWear] = [new ItemSpell { FunctionType = (int)FunctionType.SpawnItem, Arguments = ["ITM1", 20] }]
                }
            }
        }));

        NpcEditorLootView view = editor.Describe(npc);
        Assert.AreEqual(3, view.Rows.Count);
        Assert.AreEqual("Carried SpawnItem", view.Rows[0].Source);
        Assert.AreEqual(2, view.Rows[0].MinQuantity);
        Assert.AreEqual(2, editor.Describe(npc).Rows[0].MaxQuantity, "Inspection must not consume the item.");
        Assert.AreEqual("ITM1", view.Rows[1].Hash);
        Assert.AreEqual("POOL", view.Rows[2].Hash);
        Assert.AreEqual(0, view.Rows[2].Chance);
        Assert.AreEqual(18, view.Rows[2].MinQuality);
        Assert.AreEqual(22, view.Rows[2].MaxQuality);
        CollectionAssert.AreEqual(new[] { "ITM1", "ITM2" }, view.Rows[2].Items.Select(item => item.Hash).ToArray());
        Assert.IsTrue(view.Rows[2].Groups[0].SpawnAll);
        Assert.IsFalse(view.Rows[2].Groups[1].SpawnAll);
        Assert.AreEqual("ITM1", editor.SearchItemFamilies("1001").Single().Hash);
    }

    [TestMethod]
    public void OverlaySurvivesRestartAndUsesNativeRollWithoutChangingOtherSpawnsOrOriginalFiles()
    {
        using var fixture = new Fixture();
        GameDataStore data = fixture.Open();
        var editor = new NpcEditorLoot(data, fixture.Items);
        string original = File.ReadAllText(fixture.TemplateFile);
        NpcCharacter before = fixture.Spawn(data, TargetHash);
        NpcEditorLootTarget scope = editor.CaptureSaveTarget(before);

        NpcEditorLootState saved = editor.Save(scope, new(0, null, Drop), Actor);
        Assert.AreEqual(1, saved.Revision);
        Assert.AreEqual(Actor, saved.History.Single().Actor);
        Assert.AreEqual(1, editor.Describe(before).Rows.Count);
        Assert.AreEqual(0, data.RequireMobTemplate("MOB2").ItemTable.Count);
        Assert.AreEqual(original, File.ReadAllText(fixture.TemplateFile));

        GameDataStore restarted = fixture.Open();
        var future = new List<MobTemplate>();
        restarted.CollectMobSpawns("FAMY", 20, future);
        CollectionAssert.AreEqual(new[] { "POOL", "ITM1" }, future.Single().ItemTable.Select(entry => entry.Hash).ToArray());

        var minter = new HashItemMinter(restarted, fixture.Items, new StubItemBuilder());
        var roll = new List<(int LowId, int HighId, int Quality)>();
        LootableDynel.RollItemTable(future.Single().ItemTable, 20, minter, Corpse.LootCapacity, 0, roll);
        Assert.AreEqual((1001, 1001, 20), roll.Single());

        var restartedEditor = new NpcEditorLoot(restarted, fixture.Items);
        NpcEditorLootState replaced = restartedEditor.Save(scope, new(1, 0, new("ITM2", 30, 2, 10)), Actor);
        Assert.AreEqual(2, replaced.Revision);
        Assert.AreEqual("ITM2", replaced.Entries.Single().ItemHash);
        NpcEditorLootState removed = restartedEditor.Save(scope, new(2, 0, null), Actor);
        Assert.AreEqual(0, removed.Entries.Length);

        NpcEditorLootState reverted = restartedEditor.Revert(scope, new(3, 1), Actor);
        Assert.AreEqual(4, reverted.Revision);
        Assert.AreEqual("revert", reverted.History[^1].Action);
        Assert.AreEqual(Drop, reverted.Entries.Single());
        Assert.AreEqual("ITM1", fixture.Open().RequireMobTemplate(scope.TemplateHash).ItemTable[^1].Hash);
    }

    [TestMethod]
    public void InvalidStaleAndExternalWritesLeaveThePreviousOverlayIntact()
    {
        using var fixture = new Fixture();
        GameDataStore data = fixture.Open();
        var editor = new NpcEditorLoot(data, fixture.Items);
        GameDataStore otherData = fixture.Open();
        var otherProcessView = new NpcEditorLoot(otherData, fixture.Items);
        NpcEditorLootTarget target = editor.CaptureSaveTarget(fixture.Spawn(data, TargetHash));
        NpcEditorLootTarget otherTarget = otherProcessView.CaptureSaveTarget(fixture.Spawn(otherData, "MOB2"));

        Assert.ThrowsExactly<ArgumentException>(() => editor.Save(target, new(0, null, Drop with { Chance = 101 }), Actor));
        Assert.ThrowsExactly<ArgumentException>(() => editor.Save(target, new(0, null, Drop with { ItemHash = "POOL" }), Actor));
        Assert.ThrowsExactly<ArgumentException>(() => editor.Save(target, new(0, null, Drop with { ItemHash = "MISS" }), Actor));
        Assert.IsFalse(File.Exists(fixture.OverlayFile));

        editor.Save(target, new(0, null, Drop), Actor);
        string committed = File.ReadAllText(fixture.OverlayFile);
        Assert.ThrowsExactly<NpcEditorLootConflictException>(() => editor.Save(target, new(0, 0, null), Actor));
        Assert.ThrowsExactly<NpcEditorLootConflictException>(() => otherProcessView.Save(otherTarget, new(0, null, Drop), Actor));
        Assert.AreEqual(committed, File.ReadAllText(fixture.OverlayFile));
    }

    [TestMethod]
    public void InvalidOverlayReloadKeepsThePreviousCatalogAndRestartFallsBackToBase()
    {
        using var fixture = new Fixture();
        GameDataStore data = fixture.Open();
        var editor = new NpcEditorLoot(data, fixture.Items);
        editor.Save(editor.CaptureSaveTarget(fixture.Spawn(data, TargetHash)), new(0, null, Drop), Actor);

        File.WriteAllText(fixture.OverlayFile, "{invalid");
        Assert.IsFalse(data.ReloadNpcTemplates().Ok);
        Assert.AreEqual(2, data.RequireMobTemplate(TargetHash).ItemTable.Count);
        Assert.IsFalse(editor.Describe(fixture.Spawn(data, TargetHash)).Saved.CanSave);

        GameDataStore restarted = fixture.Open();
        Assert.AreEqual(1, restarted.RequireMobTemplate(TargetHash).ItemTable.Count);
        Assert.IsFalse(new NpcEditorLoot(restarted, fixture.Items).Describe(fixture.Spawn(restarted, TargetHash)).Saved.CanSave);
    }

    [TestMethod]
    public void SavedOverlayCannotSilentlyRetargetAChangedLeafOrWriteCanonicalGameData()
    {
        using var fixture = new Fixture();
        GameDataStore data = fixture.Open();
        var editor = new NpcEditorLoot(data, fixture.Items);
        NpcEditorLootTarget target = editor.CaptureSaveTarget(fixture.Spawn(data, TargetHash));
        editor.Save(target, new(0, null, Drop), Actor);

        string source = File.ReadAllText(fixture.TemplateFile);
        File.WriteAllText(fixture.TemplateFile, source.Replace("Mob One", "Changed NPC", StringComparison.Ordinal));
        Assert.IsFalse(data.ReloadNpcTemplates().Ok);
        Assert.AreEqual("Mob One", data.RequireMobTemplate(TargetHash).Name);

        var canonical = new GameDataStore(new StubLogger(), null, fixture.GameData, fixture.GameData);
        var restricted = new NpcEditorLoot(canonical, fixture.Items);
        NpcCharacter blocked = fixture.Spawn(canonical, TargetHash);
        Assert.IsFalse(restricted.Describe(blocked).Saved.CanSave);
        Assert.ThrowsExactly<InvalidOperationException>(() => restricted.CaptureSaveTarget(blocked));
        Assert.IsFalse(File.Exists(Path.Combine(fixture.GameData, NpcEditorLootJournalFile.FileName)));
    }

    [TestMethod]
    public void OldSpawnCannotEditAChangedAuthoredLeafAtRevisionZero()
    {
        using var fixture = new Fixture();
        GameDataStore data = fixture.Open();
        var editor = new NpcEditorLoot(data, fixture.Items);
        NpcCharacter oldSpawn = fixture.Spawn(data, TargetHash);
        NpcEditorLootTarget target = editor.CaptureSaveTarget(oldSpawn);

        string source = File.ReadAllText(fixture.TemplateFile);
        File.WriteAllText(fixture.TemplateFile, source.Replace("Mob One", "Changed NPC", StringComparison.Ordinal));
        Assert.IsTrue(data.ReloadNpcTemplates().Ok);
        Assert.IsFalse(editor.Describe(oldSpawn).Saved.CanSave);
        Assert.ThrowsExactly<InvalidOperationException>(() => editor.CaptureSaveTarget(oldSpawn));
        Assert.ThrowsExactly<NpcEditorLootConflictException>(() => editor.Save(target, new(0, null, Drop), Actor));
        Assert.IsFalse(File.Exists(fixture.OverlayFile));
        Assert.AreEqual(1, editor.Save(editor.CaptureSaveTarget(fixture.Spawn(data, TargetHash)), new(0, null, Drop), Actor).Revision);
    }

    sealed class Fixture : IDisposable
    {
        readonly string _root = Path.Combine(Path.GetTempPath(), "aorebirth-npc-editor-loot-" + Guid.NewGuid().ToString("N"));

        public Fixture()
        {
            Directory.CreateDirectory(Path.Combine(GameData, GameDataPaths.PlayfieldsFolderName));
            Directory.CreateDirectory(Overlay);
            File.WriteAllText(TemplateFile, """
                {
                  "FAMY": {"Children": ["MOB1"]},
                  "MOB1": {"Templates": [{"Name": "Mob One", "Level": 20, "LootTable": [
                    {"Hash": "POOL", "Repeats": 1, "Chance": 0, "LevelMod": 10}
                  ]}]},
                  "MOB2": {"Templates": [{"Name": "Mob Two", "Level": 20}]}
                }
                """);
            File.WriteAllText(Path.Combine(GameData, GameDataPaths.ItemTemplatesFileName), """
                {
                  "POOL": {"Children": ["ITM1", "PICK"], "SpawnAll": true},
                  "PICK": {"Children": ["ITM2"]},
                  "ITM1": {"Templates": [1001]},
                  "ITM2": {"Templates": [1002]},
                  "MISS": {"Templates": [1900]}
                }
                """);
        }

        internal string GameData => Path.Combine(_root, "GameData");
        internal string Overlay => Path.Combine(_root, "Overlay");
        internal string TemplateFile => Path.Combine(GameData, GameDataPaths.NpcTemplatesFileName);
        internal string OverlayFile => Path.Combine(Overlay, NpcEditorLootJournalFile.FileName);
        internal StubCatalog Items { get; } = new StubCatalog().Add(1001, 20).Add(1002, 20);

        internal GameDataStore Open() => new(new StubLogger(), null, GameData, Overlay);

        internal NpcCharacter Spawn(GameDataStore data, string hash)
        {
            var npc = new NpcCharacter(new Identity { Type = IdentityType.CanbeAffected, Instance = 77001 }, new StubItemBuilder())
            {
                MobTemplate = data.RequireMobTemplate(hash)
            };
            npc.Stats.Set(CharacterStat.Level, 20);
            return npc;
        }

        public void Dispose() => Directory.Delete(_root, recursive: true);
    }
}
