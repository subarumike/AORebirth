namespace ZoneEngine_New.Tests;

using System;
using System.IO;
using System.Linq;
using AORebirth.Core.GameData;
using AORebirth.Enums;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SmokeLounge.AOtomation.Messaging.GameData;
using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;
using ZoneEngine_New.Core.Characters;
using ZoneEngine_New.Core.Data;
using ZoneEngine_New.Core.Entities;
using ZoneEngine_New.Core.GameData;
using ZoneEngine_New.Core.Inventory;
using ZoneEngine_New.Core.Nanos;

[TestClass]
public sealed class NotumRingNanoEligibilityTests
{
    const int NotumRingId = 204578;
    const int AdvancedAutomatonId = 45731;
    const int RequiredSkill = 69;
    static readonly DateTime Now = new(2026, 11, 23, 0, 0, 0, DateTimeKind.Utc);

    static readonly Lazy<ItemBuilder> RealItems = new(() =>
    {
        string root = GameDataPaths.ResolveRuntimeRoot();
        Assert.IsTrue(File.Exists(Path.Combine(root, "items.dat")), $"The item catalog is required at '{root}'.");
        var logger = new StubLogger();
        var data = new StubGameData(HashItemCatalog.Parse("{}"), rootPath: root);
        return new ItemBuilder(new ItemTemplateCatalog(data, logger), logger);
    });

    [TestMethod]
    public void EquippingAndRemovingTheRingChangesNanoEligibilityAtTheExactThreshold()
    {
        using var world = new InventoryActionTests.World();
        Player player = world.Player;
        Prepare(player, RequiredSkill - 2, RequiredSkill - 2);
        NanoSpell nano = AdvancedAutomaton();
        var moves = new InventoryMoveService(new StubLogger(), world.Flush, world.Actions);

        Item ring = RealItems.Value.Create(NotumRingId, NotumRingId, 1, ItemSource.Command, instanceId: 17);
        int inventorySlot = player.Inventory.Inventory.Offset;
        int fingerSlot = player.Inventory.Armor.Offset + (int)ArmorSlots.RightFinger - 1;
        Assert.IsTrue(player.Inventory.Inventory.Add(inventorySlot, ring));
        Assert.AreEqual(NanoCastRefusal.RequirementsNotMet, Preview(player, nano));

        Move(moves, player, IdentityType.Inventory, inventorySlot, fingerSlot);

        Assert.AreSame(ring, player.Inventory.Armor.Content[fingerSlot]);
        Assert.AreEqual(2, player.Stats.Get(CharacterStat.MaterialCreation, StatDetail.Bonus));
        Assert.AreEqual(2, player.Stats.Get(CharacterStat.SpaceTime, StatDetail.Bonus));
        AssertSkills(player, RequiredSkill, RequiredSkill);
        Assert.AreEqual(NanoCastRefusal.None, Preview(player, nano));

        Move(moves, player, IdentityType.ArmorPage, fingerSlot, inventorySlot);

        Assert.AreSame(ring, player.Inventory.Inventory.Content[inventorySlot]);
        AssertSkills(player, RequiredSkill - 2, RequiredSkill - 2);
        Assert.AreEqual(NanoCastRefusal.RequirementsNotMet, Preview(player, nano));
    }

    [TestMethod]
    public void RehydratedRingCombinesWithImplantAndBuffWithoutChangingBaseSkills()
    {
        Player original = TestWorld.CreatePlayer(111);
        Item ring = RealItems.Value.Create(NotumRingId, NotumRingId, 1, ItemSource.Command, instanceId: 18);
        int fingerSlot = original.Inventory.Armor.Offset + (int)ArmorSlots.LeftFinger - 1;
        Assert.IsTrue(original.Inventory.Armor.Add(fingerSlot, ring));
        ItemInstanceRecord ringRow = ring.ToRecord(original.Inventory.Armor.Identity, fingerSlot, ring.StackCount);

        Player reconnected = TestWorld.CreatePlayer(original.Identity.Instance);
        reconnected.Inventory.Apply(new CharacterHydrationResult { Items = [ringRow] },
            reconnected.Identity.Instance, RealItems.Value);
        Prepare(reconnected, RequiredSkill - 9, RequiredSkill - 8);
        Item implant = TestWorld.CreateItem(instanceId: 19);
        implant.SpellList[EventType.OnWear] =
        [
            SkillModifier(CharacterStat.MaterialCreation, 3),
            SkillModifier(CharacterStat.SpaceTime, 4)
        ];
        Assert.IsTrue(reconnected.Inventory.Implant.Add(reconnected.Inventory.Implant.Offset, implant));
        reconnected.Rebase();
        NanoSpell nano = AdvancedAutomaton();
        Assert.AreEqual(NotumRingId, reconnected.Inventory.Armor.Content[fingerSlot].LowId);
        AssertSkills(reconnected, RequiredSkill - 4, RequiredSkill - 2);
        Assert.AreEqual(NanoCastRefusal.RequirementsNotMet, Preview(reconnected, nano));

        NanoSpell skillBuff = TestNanos.Create(900001, modifiers:
        [
            SkillModifier(CharacterStat.MaterialCreation, 4),
            SkillModifier(CharacterStat.SpaceTime, 2)
        ]);
        Assert.AreEqual(BuffApplyDecision.Apply,
            reconnected.TryApplyBuff(skillBuff, reconnected.Identity, Now, out _, out _));
        reconnected.Rebase();

        Assert.AreEqual(RequiredSkill - 9, reconnected.Stats.Get(CharacterStat.MaterialCreation, StatDetail.Base));
        Assert.AreEqual(RequiredSkill - 8, reconnected.Stats.Get(CharacterStat.SpaceTime, StatDetail.Base));
        AssertSkills(reconnected, RequiredSkill, RequiredSkill);
        Assert.AreEqual(NanoCastRefusal.None, Preview(reconnected, nano));

        Assert.IsNotNull(reconnected.Inventory.Armor.Remove(fingerSlot));
        reconnected.Rebase();
        AssertSkills(reconnected, RequiredSkill - 2, RequiredSkill - 2);
        Assert.AreEqual(NanoCastRefusal.RequirementsNotMet, Preview(reconnected, nano));
    }

    static NanoSpell AdvancedAutomaton()
    {
        ItemTemplate template = RealItems.Value.CreateTemplate(AdvancedAutomatonId, AdvancedAutomatonId, 1);
        Assert.AreEqual("Advanced Automaton", template.Name);
        var criteria = template.Actions.Single(action => action.ActionType == (int)ActionType.ToUse).Requirements;
        foreach (CharacterStat stat in new[] { CharacterStat.MaterialCreation, CharacterStat.SpaceTime })
        {
            ItemRequirement requirement = criteria.Single(requirement => requirement.StatNumber == (int)stat);
            Assert.AreEqual(Operator.GreaterThan, (Operator)requirement.Operator);
            Assert.AreEqual(RequiredSkill - 1, requirement.Value);
        }
        return NanoSpell.From(template);
    }

    static void Prepare(Player player, int matterCreation, int spaceTime)
    {
        player.Stats.Set(CharacterStat.Level, 100);
        player.Stats.Set(CharacterStat.Profession, (int)Profession.Engineer);
        player.Stats.Set(CharacterStat.MaterialCreation, matterCreation);
        player.Stats.Set(CharacterStat.SpaceTime, spaceTime);
        player.Stats.Set(CharacterStat.Health, 100);
        player.Stats.Set(CharacterStat.CurrentNano, 1_000);
        player.Stats.Set(CharacterStat.MaxNCU, 60);
        Assert.IsTrue(player.TryAddUploadedNano(AdvancedAutomatonId));
        player.Rebase();
    }

    static NanoCastRefusal Preview(Player player, NanoSpell nano)
        => NanoRuntime.PreviewStartCast(player, nano, player, Now);

    static ItemSpell SkillModifier(CharacterStat skill, int amount) => new()
    {
        FunctionType = (int)FunctionType.Modify,
        Arguments = [(int)skill, amount]
    };

    static void AssertSkills(Player player, int matterCreation, int spaceTime)
    {
        Assert.AreEqual(matterCreation, player.Stats.Get(CharacterStat.MaterialCreation));
        Assert.AreEqual(spaceTime, player.Stats.Get(CharacterStat.SpaceTime));
    }

    static void Move(InventoryMoveService moves, Player player, IdentityType page, int source, int target)
    {
        moves.Handle(player, new ClientMoveItemToInventoryMessage
        {
            SourceContainer = new Identity { Type = page, Instance = source },
            TargetPlacement = target
        });
        moves.Tick(player.Playfield!, deltaTime: 30);
    }
}
