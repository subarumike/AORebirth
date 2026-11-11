namespace ZoneEngine_New.Tests;

using System;
using System.Linq;
using System.Runtime.CompilerServices;

using AORebirth.Enums;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SmokeLounge.AOtomation.Messaging.GameData;

using ZoneEngine_New.Core.Entities;
using ZoneEngine_New.Core.Inventory;
using ZoneEngine_New.Core.Mobs;
using ZoneEngine_New.Core.NpcEditor;
using ZoneEngine_New.Core.Playfield;

[TestClass]
public sealed class NpcEditorRuntimeTests
{
    [TestMethod]
    public void TypedResolutionRejectsPlayersMovedNpcsAndRecycledGenerations()
    {
        var field = (Playfield)RuntimeHelpers.GetUninitializedObject(typeof(Playfield));
        var registry = new DynelRegistry();
        var npc = MakeNpc(100);
        npc.Playfield = field;
        registry.Register(npc);
        registry.Register(TestWorld.CreatePlayer(101));

        Assert.AreSame(npc, NpcEditorRuntime.Find(registry, field, (int)IdentityType.CanbeAffected, 100));
        Assert.IsNull(NpcEditorRuntime.Find(registry, field, (int)IdentityType.Corpse, 100));
        Assert.IsNull(NpcEditorRuntime.Find(registry, field, (int)IdentityType.CanbeAffected, 101));

        npc.Playfield = null;
        Assert.IsNull(NpcEditorRuntime.Find(registry, field, (int)IdentityType.CanbeAffected, 100));

        var handle = new NpcEditorIdentity(1, (int)IdentityType.CanbeAffected, 100, Guid.NewGuid());
        NpcEditorRuntime.RequireGeneration(handle, handle.Generation);
        Assert.AreEqual(409, Assert.ThrowsExactly<NpcEditorRuntimeException>(
            () => NpcEditorRuntime.RequireGeneration(handle, Guid.NewGuid())).StatusCode);
    }

    [TestMethod]
    public void LiveHealthUsesVitalPercentAndLeavesAnotherInstanceAndTemplateAlone()
    {
        var first = MakeNpc(100);
        var second = MakeNpc(101);
        second.MobTemplate = first.MobTemplate;

        NpcEditorRuntime.ApplyStat(first, CharacterStat.Health, 100, 75);

        Assert.AreEqual(75, first.Stats.Get(CharacterStat.Health, StatDetail.Base));
        Assert.AreEqual(75, first.Stats.Get(CharacterStat.PercentRemainingHealth));
        Assert.AreEqual(100, second.Stats.Get(CharacterStat.Health));
        Assert.AreEqual(100, first.MobTemplate!.Stats[(int)CharacterStat.Health]);

        Assert.AreEqual(409, Assert.ThrowsExactly<NpcEditorRuntimeException>(
            () => NpcEditorRuntime.ApplyStat(first, CharacterStat.Health, 100, 50)).StatusCode);
        Assert.AreEqual(400, Assert.ThrowsExactly<NpcEditorRuntimeException>(
            () => NpcEditorRuntime.ApplyStat(first, CharacterStat.Health, 75, 0)).StatusCode);
        Assert.AreEqual(400, Assert.ThrowsExactly<NpcEditorRuntimeException>(
            () => NpcEditorRuntime.ApplyStat(first, CharacterStat.Health, 75, 101)).StatusCode);
        Assert.AreEqual(75, first.Stats.Get(CharacterStat.Health));
        Assert.IsFalse(first.IsDead);

        NpcEditorRuntime.SetStat(first, CharacterStat.Health, 100, restoring: true);
        Assert.AreEqual(100, first.Stats.Get(CharacterStat.PercentRemainingHealth));
    }

    [TestMethod]
    public void ArmorEditsUseNpcPositionsAndRejectStaleOrConflictingSlots()
    {
        var catalog = new StubCatalog().Add(10, 25).Add(11, 25);
        catalog.Require(10).Stats[CharacterStat.ItemClass] = (int)ItemClass.Armor;
        catalog.Require(10).Stats[CharacterStat.DefaultPos] = 2;
        catalog.Require(10).SpellList[EventType.OnWear] =
            [new ItemSpell { FunctionType = (int)FunctionType.ChangeBodyMesh, Arguments = ["robe"] }];
        catalog.Require(11).Stats[CharacterStat.ItemClass] = (int)ItemClass.Armor;
        catalog.Require(11).Stats[CharacterStat.DefaultPos] = 2;
        var items = new ItemBuilder(catalog, new StubInstanceIdAllocator(), new StubLogger());
        var npc = MakeNpc(100, items);

        var change = NpcEditorRuntime.EditEquipment(npc, catalog, items, new(-1, 0, 10, 25));
        Assert.IsNotNull(change.After);
        Assert.AreSame(change.After, npc.Equipment.Content[change.Slot]);
        Assert.AreEqual((byte)1, npc.BuildAppearanceUpdateMessage().Unknown1);
        Assert.AreEqual(400, Assert.ThrowsExactly<NpcEditorRuntimeException>(
            () => NpcEditorRuntime.EditEquipment(npc, catalog, items, new(-1, 0, 11, null))).StatusCode);
        Assert.AreEqual(409, Assert.ThrowsExactly<NpcEditorRuntimeException>(
            () => NpcEditorRuntime.EditEquipment(npc, catalog, items, new(change.Slot, 0, null, null))).StatusCode);

        var replaced = NpcEditorRuntime.EditEquipment(npc, catalog, items, new(change.Slot, change.After!.InstanceId, 11, null));
        Assert.AreEqual(11, npc.Equipment.Content[change.Slot].LowId);
        Assert.AreEqual(10, replaced.Before!.LowId);
        Assert.AreEqual((byte)0, npc.BuildAppearanceUpdateMessage().Unknown1);
        NpcEditorRuntime.EditEquipment(npc, catalog, items, new(change.Slot, replaced.After!.InstanceId, null, null));
        Assert.AreEqual(0, npc.Equipment.Content.Count);
        Assert.AreEqual(0, npc.MobTemplate!.Equipment.Count);
    }

    [TestMethod]
    public void VisibleWeaponSwapKeepsTheActualMonsterCombatWeapon()
    {
        var catalog = new StubCatalog().Add(10, 25).Add(11, 25).Add(12, 25);
        for (int id = 10; id <= 12; id++)
        {
            ItemTemplate template = catalog.Require(id);
            template.Stats[CharacterStat.ItemClass] = (int)ItemClass.Weapon;
            template.Stats[CharacterStat.InitiativeType] = (int)CharacterStat.MeleeInit;
            template.Stats[CharacterStat.AttackRange] = 3;
            template.Stats[CharacterStat.WeaponMesh] = 500 + id;
        }
        var items = new ItemBuilder(catalog, new StubInstanceIdAllocator(), new StubLogger());
        var npc = MakeNpc(100, items);
        Item hand = items.CreateWithNewInstance(10, 10, 25, ItemSource.Other);
        Item monster = items.CreateWithNewInstance(11, 11, 25, ItemSource.Other);
        Assert.IsTrue(npc.Equipment.Add(0, hand));
        Assert.IsTrue(npc.EquipCombatWeapon(monster));
        npc.RebaseWeapons();

        Assert.AreSame(hand, npc.VisualRightHand);
        NpcEditorRuntime.EditEquipment(npc, catalog, items, new(0, hand.InstanceId, 12, null));
        Assert.AreEqual(12, npc.VisualRightHand!.LowId);
        Assert.AreSame(monster, npc.Equipment.Content[1]);
        Assert.AreSame(monster, npc.Weapons[WeaponSlot.Npc0].Item);
        Assert.AreEqual(400, Assert.ThrowsExactly<NpcEditorRuntimeException>(
            () => NpcEditorRuntime.EditEquipment(npc, catalog, items, new(1, monster.InstanceId, 10, null))).StatusCode);

        Item visible = npc.VisualRightHand;
        var removal = NpcEditorRuntime.EditEquipment(npc, catalog, items, new(0, visible.InstanceId, null, null));
        Assert.IsNull(npc.VisualRightHand);
        Assert.AreEqual(0, npc.BuildWeaponInstanceMessages().Count);
        Assert.IsFalse(npc.BuildAppearanceUpdateMessage().Meshes.Any(mesh => mesh.Position == 1));
        Assert.AreSame(monster, npc.Weapons[WeaponSlot.Npc0].Item);
        Assert.IsNull(npc.Weapons[WeaponSlot.Npc0].VisualHand);

        NpcEditorRuntime.UndoEquipment(npc, removal);
        Assert.AreSame(visible, npc.VisualRightHand);
        Assert.IsTrue(npc.BuildAppearanceUpdateMessage().Meshes.Any(mesh => mesh.Position == 1 && mesh.Id == 512));
        Assert.AreSame(monster, npc.Weapons[WeaponSlot.Npc0].Item);
    }

    static NpcCharacter MakeNpc(int instance, IItemBuilder? items = null)
    {
        var npc = new NpcCharacter(new Identity { Type = IdentityType.CanbeAffected, Instance = instance }, items ?? new StubItemBuilder())
        {
            MobTemplate = new MobTemplate { Hash = "TEST", Stats = { [(int)CharacterStat.Health] = 100 } }
        };
        npc.Stats.Set(CharacterStat.MaxHealth, 100);
        npc.Stats.Set(CharacterStat.Health, 100);
        return npc;
    }
}
