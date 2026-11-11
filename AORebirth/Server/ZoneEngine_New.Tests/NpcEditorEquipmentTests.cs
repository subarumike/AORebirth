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
public sealed class NpcEditorEquipmentTests
{
    [TestMethod]
    public void EquipmentOverrideSurvivesNewSpawnsAndRestartAndRestoresAnEarlierRevision()
    {
        using var fixture = new Fixture();
        GameDataStore data = fixture.Open();
        var editor = new NpcEditorEquipment(data, fixture.Catalog);
        string original = File.ReadAllText(fixture.TemplateFile);
        NpcCharacter initial = fixture.Spawn(data, 24);
        NpcEditorEquipmentTarget target = NpcEditorRuntime.CaptureEquipmentTarget(initial);
        Assert.AreEqual(0, target.BandIndex);
        Assert.AreEqual(10, initial.VisualRightHand!.LowId);
        Assert.AreEqual(50, initial.Weapons[WeaponSlot.Npc0].Item!.LowId);
        Assert.IsNull(editor.Describe(target).UnavailableReason);

        NpcEditorEquipmentState replaced = editor.Save(target, new(0, 0, 10, 10, 11, 25), "TestGM");
        Assert.AreEqual(1, replaced.Revision);
        Assert.AreEqual(10, replaced.History[0].Before[0].LowId);
        Assert.AreEqual(11, replaced.History[0].After[0].LowId);
        Assert.AreEqual("TestGM", replaced.History[0].Actor);
        Assert.AreEqual(10, initial.VisualRightHand.LowId, "Saving must not change a spawned NPC.");
        Assert.AreEqual(11, fixture.Spawn(data, 24).VisualRightHand!.LowId);
        Assert.AreEqual(10, fixture.Spawn(data, 39).VisualRightHand!.LowId);

        NpcEditorEquipmentState armored = editor.Save(target, new(1, -1, 0, 0, 30, null), "TestGM");
        Assert.AreEqual(2, armored.Revision);
        GameDataStore restarted = fixture.Open();
        NpcCharacter equipped = fixture.Spawn(restarted, 24);
        Assert.AreEqual(11, equipped.VisualRightHand!.LowId);
        Assert.AreEqual(25, equipped.VisualRightHand.Quality);
        Assert.AreEqual(50, equipped.Weapons[WeaponSlot.Npc0].Item!.LowId);
        Assert.AreEqual((byte)1, equipped.BuildAppearanceUpdateMessage().Unknown1);
        Assert.AreEqual((byte)0, fixture.Spawn(restarted, 39).BuildAppearanceUpdateMessage().Unknown1);

        var reopened = new NpcEditorEquipment(restarted, fixture.Catalog);
        NpcEditorEquipmentState removed = reopened.Save(target, new(2, 0, 11, 11, null, null), "TestGM");
        Assert.AreEqual(3, removed.Revision);
        NpcCharacter unarmedHand = fixture.Spawn(fixture.Open(), 24);
        Assert.IsNull(unarmedHand.VisualRightHand);
        Assert.AreEqual(50, unarmedHand.Weapons[WeaponSlot.Npc0].Item!.LowId);
        Assert.AreEqual((byte)1, unarmedHand.BuildAppearanceUpdateMessage().Unknown1);

        NpcEditorEquipmentState restored = reopened.Restore(target, new(3, 1), "RollbackGM");
        Assert.AreEqual("restore", restored.History[^1].Action);
        Assert.AreEqual("RollbackGM", restored.History[^1].Actor);
        NpcCharacter recovered = fixture.Spawn(fixture.Open(), 24);
        Assert.AreEqual(11, recovered.VisualRightHand!.LowId);
        Assert.AreEqual((byte)0, recovered.BuildAppearanceUpdateMessage().Unknown1);
        Assert.AreEqual(original, File.ReadAllText(fixture.TemplateFile));
    }

    [TestMethod]
    public void EquipmentOverrideRejectsProtectedRowsStaleVersionsAndCanonicalDestination()
    {
        using var fixture = new Fixture();
        GameDataStore data = fixture.Open();
        GameDataStore secondProcess = fixture.Open();
        var editor = new NpcEditorEquipment(data, fixture.Catalog);
        var other = new NpcEditorEquipment(secondProcess, fixture.Catalog);
        NpcEditorEquipmentTarget target = NpcEditorRuntime.CaptureEquipmentTarget(fixture.Spawn(data, 20));
        NpcEditorEquipmentTarget otherBand = NpcEditorRuntime.CaptureEquipmentTarget(fixture.Spawn(secondProcess, 40));

        Assert.AreEqual(400, Assert.ThrowsExactly<NpcEditorRuntimeException>(
            () => editor.Save(target, new(0, 1, 20, 20, null, null), "TestGM")).StatusCode);
        Assert.AreEqual(400, Assert.ThrowsExactly<NpcEditorRuntimeException>(
            () => editor.Save(target, new(0, 0, 10, 10, 30, null), "TestGM")).StatusCode);
        Assert.IsFalse(File.Exists(fixture.OverrideFile));

        editor.Save(target, new(0, 0, 10, 10, 11, null), "TestGM");
        string committed = File.ReadAllText(fixture.OverrideFile);
        Assert.AreEqual(409, Assert.ThrowsExactly<NpcEditorRuntimeException>(
            () => editor.Save(target, new(0, 0, 11, 11, null, null), "TestGM")).StatusCode);
        Assert.AreEqual(409, Assert.ThrowsExactly<NpcEditorRuntimeException>(
            () => other.Save(otherBand, new(0, 0, 10, 10, 11, null), "TestGM")).StatusCode);
        Assert.AreEqual(committed, File.ReadAllText(fixture.OverrideFile));

        var canonical = new GameDataStore(new StubLogger(), null, fixture.GameData, fixture.GameData);
        var blocked = new NpcEditorEquipment(canonical, fixture.Catalog);
        Assert.IsNotNull(blocked.Describe(target).UnavailableReason);
        Assert.IsFalse(File.Exists(Path.Combine(fixture.GameData, NpcEditorEquipmentJournalFile.FileName)));

        File.WriteAllText(fixture.OverrideFile, "{invalid");
        Assert.IsFalse(data.ReloadNpcTemplates().Ok);
        Assert.IsNotNull(editor.Describe(target).UnavailableReason);
        Assert.AreEqual(11, fixture.Spawn(data, 20).VisualRightHand!.LowId);
        Assert.AreEqual(10, fixture.Spawn(fixture.Open(), 20).VisualRightHand!.LowId);
    }

    sealed class Fixture : IDisposable
    {
        readonly string _root = Path.Combine(Path.GetTempPath(), "aorebirth-npc-editor-equipment-" + Guid.NewGuid().ToString("N"));
        readonly ItemBuilder _items;

        public Fixture()
        {
            Directory.CreateDirectory(Path.Combine(GameData, GameDataPaths.PlayfieldsFolderName));
            Directory.CreateDirectory(Overlay);
            File.WriteAllText(TemplateFile, """
                {
                  "FAMY": {"Children": ["MOB1"]},
                  "MOB1": {"Templates": [
                    {"Name": "Equipment fixture", "Level": 20, "Equipment": [[10,10],[20,20]]},
                    {"Name": "Equipment fixture", "Level": 40, "Equipment": [[10,10],[20,20]]}
                  ]}
                }
                """);
            File.WriteAllText(Path.Combine(GameData, GameDataPaths.MonsterWeaponsFileName), """{"WPN1":[50,50]}""");
            foreach (int id in new[] { 10, 11, 50 })
            {
                ItemTemplate item = Catalog.Require(id);
                item.Stats[CharacterStat.ItemClass] = (int)ItemClass.Weapon;
                item.Stats[CharacterStat.InitiativeType] = (int)CharacterStat.MeleeInit;
                item.Stats[CharacterStat.AttackRange] = 3;
                item.Stats[CharacterStat.WeaponMesh] = 500 + id;
            }
            Catalog.Require(20).Stats[CharacterStat.ItemClass] = (int)ItemClass.Npc;
            Catalog.Require(20).SpellList[EventType.OnWear] =
                [new ItemSpell { FunctionType = (int)FunctionType.EquipMonsterWeapon, Arguments = ["WPN1"] }];
            Catalog.Require(30).Stats[CharacterStat.ItemClass] = (int)ItemClass.Armor;
            Catalog.Require(30).Stats[CharacterStat.DefaultPos] = 8;
            Catalog.Require(30).SpellList[EventType.OnWear] =
                [new ItemSpell { FunctionType = (int)FunctionType.ChangeBodyMesh, Arguments = ["robe"] }];
            _items = new ItemBuilder(Catalog, new StubInstanceIdAllocator(), new StubLogger());
        }

        internal string GameData => Path.Combine(_root, "GameData");
        string Overlay => Path.Combine(_root, "Overlay");
        internal string TemplateFile => Path.Combine(GameData, GameDataPaths.NpcTemplatesFileName);
        internal string OverrideFile => Path.Combine(Overlay, NpcEditorEquipmentJournalFile.FileName);
        internal StubCatalog Catalog { get; } = new StubCatalog().Add(10, 25).Add(11, 25).Add(20, 25).Add(30, 25).Add(50, 25);

        internal GameDataStore Open() => new(new StubLogger(), null, GameData, Overlay);

        internal NpcCharacter Spawn(GameDataStore data, int level)
        {
            var branches = new List<MobTemplate>();
            data.CollectMobSpawns("FAMY", level, branches);
            var npc = new NpcCharacter(new Identity { Type = IdentityType.CanbeAffected, Instance = 77001 }, _items)
            {
                MobTemplate = branches.Single()
            };
            npc.Stats.Set(CharacterStat.Level, level);
            npc.FillEquipment(data, new StubLogger(), Catalog);
            npc.Rebase();
            return npc;
        }

        public void Dispose() => Directory.Delete(_root, recursive: true);
    }
}
