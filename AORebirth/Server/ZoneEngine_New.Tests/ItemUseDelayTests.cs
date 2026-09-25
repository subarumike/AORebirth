using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using AORebirth.Enums;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SmokeLounge.AOtomation.Messaging.GameData;
using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;
using ZoneEngine_New.Core.Entities;
using ZoneEngine_New.Core.Inventory;
using ZoneEngine_New.Core.Playfield;
using ZoneEngine_New.Core.Playfield.Locality;

namespace ZoneEngine_New.Tests;

[TestClass]
public sealed class ItemUseDelayTests
{
    const int Heal = 30;

    [TestMethod]
    public void ZeroDelayExecutesImmediately()
    {
        using var h = new Harness();
        Item item = h.Potion(11, delay: 0);
        Assert.AreEqual(ItemUseStart.Executed, h.Uses.TryBegin(h.Player, Slot(64), item));
        Assert.AreEqual(20 + Heal, h.Health);
        Assert.IsFalse(item.Locked);
        Assert.IsFalse(h.Uses.HasPending(h.Player.Identity.Instance));
    }

    [TestMethod]
    public void DelayLocksItemAndExecutesOnlyAfterTheDelay()
    {
        using var h = new Harness();
        Item item = h.Potion(11, delay: 150);
        Assert.AreEqual(ItemUseStart.Started, h.Uses.TryBegin(h.Player, Slot(64), item));
        Assert.IsTrue(item.Locked);
        Assert.AreEqual(20, h.Health);

        h.Uses.Tick(1.0);
        Assert.AreEqual(20, h.Health);
        Assert.IsTrue(item.Locked);

        h.Uses.Tick(0.6);
        Assert.AreEqual(20 + Heal, h.Health);
        Assert.IsFalse(item.Locked);
        Assert.IsFalse(h.Uses.HasPending(h.Player.Identity.Instance));
    }

    [TestMethod]
    public void SecondUseWhilePendingIsRejectedAndConsumesOneCharge()
    {
        using var h = new Harness();
        Item item = h.Potion(11, delay: 100, count: 3);
        Item other = h.Potion(12, delay: 0, slot: 65);
        Assert.AreEqual(ItemUseStart.Started, h.Uses.TryBegin(h.Player, Slot(64), item));
        Assert.AreEqual(ItemUseStart.Rejected, h.Uses.TryBegin(h.Player, Slot(64), item));
        Assert.AreEqual(ItemUseStart.Rejected, h.Uses.TryBegin(h.Player, Slot(65), other));

        h.Uses.Tick(2.0);
        h.Uses.Tick(2.0);
        Assert.AreEqual(20 + Heal, h.Health);
        Assert.AreEqual(2, item.StackCount);
        Assert.AreSame(item, h.Player.Inventory.Inventory.Content[64]);
    }

    [TestMethod]
    public void LeavePlayfieldCancelsAndUnlocksWithoutExecuting()
    {
        using var h = new Harness();
        Item item = h.Potion(11, delay: 100);
        h.Uses.TryBegin(h.Player, Slot(64), item);
        h.Player.InterruptTimedActions(TimedActionInterrupt.LeavePlayfield);
        Assert.IsFalse(item.Locked);
        Assert.IsFalse(h.Uses.HasPending(h.Player.Identity.Instance));
        h.Uses.Tick(2.0);
        Assert.AreEqual(20, h.Health);
    }

    [TestMethod]
    public void JumpAndMovementDoNotCancel()
    {
        using var h = new Harness();
        Item item = h.Potion(11, delay: 100);
        h.Uses.TryBegin(h.Player, Slot(64), item);
        h.Player.InterruptTimedActions(TimedActionInterrupt.Jump);
        h.Player.InterruptTimedActions(TimedActionInterrupt.Movement);
        Assert.IsTrue(h.Uses.HasPending(h.Player.Identity.Instance));
        h.Uses.Tick(2.0);
        Assert.AreEqual(20 + Heal, h.Health);
    }

    [TestMethod]
    public void ChangedSlotAbortsAtCompletion()
    {
        using var h = new Harness();
        Item item = h.Potion(11, delay: 100);
        h.Uses.TryBegin(h.Player, Slot(64), item);
        h.Player.Inventory.Inventory.Content.Remove(64);
        h.Player.Inventory.Inventory.Add(64, TestWorld.CreateItem(instanceId: 99));
        h.Uses.Tick(2.0);
        Assert.AreEqual(20, h.Health);
        Assert.IsFalse(item.Locked);
        Assert.AreEqual(1, h.Session.Messages.OfType<ChatTextMessage>().Count());
    }

    [TestMethod]
    public void LostRequirementsAbortAtCompletion()
    {
        using var h = new Harness();
        Item item = h.Potion(11, delay: 100);
        item.Definition.Actions.Add(new ItemAction
        {
            ActionType = (int)ActionType.ToUse,
            Requirements = new List<ItemRequirement>
            {
                new() { StatNumber = (int)CharacterStat.Strength, Operator = (int)Operator.GreaterThan, Value = 10 }
            }
        });
        h.Player.Stats.Set(CharacterStat.Strength, 50);
        Assert.AreEqual(ItemUseStart.Started, h.Uses.TryBegin(h.Player, Slot(64), item));
        h.Player.Stats.Set(CharacterStat.Strength, 5);
        h.Uses.Tick(2.0);
        Assert.AreEqual(20, h.Health);
        Assert.AreEqual(1, item.StackCount);
    }

    [TestMethod]
    public void DeadPlayerAtCompletionDoesNotExecute()
    {
        using var h = new Harness();
        Item item = h.Potion(11, delay: 100);
        h.Uses.TryBegin(h.Player, Slot(64), item);
        h.Session.Close();
        h.Uses.Tick(2.0);
        Assert.AreEqual(20, h.Health);
        Assert.IsFalse(item.Locked);
    }

    [TestMethod]
    public void EquipAndDelayedUseCannotOverlap()
    {
        using var h = new Harness();
        Item potion = h.Potion(11, delay: 100);
        Item armor = h.Armor(12, slot: 65);
        var move = new ClientMoveItemToInventoryMessage
        {
            SourceContainer = Slot(65),
            TargetPlacement = h.Player.Inventory.Armor.Offset
        };

        h.Uses.TryBegin(h.Player, Slot(64), potion);
        h.Moves.Handle(h.Player, move);
        Assert.IsFalse(h.Moves.HasPending(h.Player.Identity.Instance));
        Assert.IsFalse(armor.Locked);

        h.Uses.Tick(2.0);
        h.Moves.Handle(h.Player, move);
        Assert.IsTrue(h.Moves.HasPending(h.Player.Identity.Instance));
        Assert.AreEqual(ItemUseStart.Rejected, h.Uses.TryBegin(h.Player, Slot(64), potion));
    }

    [TestMethod]
    public void TemplateDelayIsClamped()
    {
        Item item = TestWorld.CreateItem();
        item.Definition.Stats[CharacterStat.AttackDelay] = 1_000_000;
        Assert.AreEqual(ItemUseService.MaxDelayCentiseconds, ItemUseService.ResolveDelayCentiseconds(item));
        item.Definition.Stats[CharacterStat.AttackDelay] = -5;
        Assert.AreEqual(0, ItemUseService.ResolveDelayCentiseconds(item));
    }

    static Identity Slot(int placement) => new() { Type = IdentityType.Inventory, Instance = placement };

    sealed class Harness : System.IDisposable
    {
        readonly InventoryActionTests.World _world = new();
        readonly ServiceProvider _services;

        public Harness()
        {
            Moves = new InventoryMoveService(new StubLogger(), _world.Flush, _world.Actions);
            Uses = new ItemUseService(Player.Playfield!, new StubLogger(), Moves, new StubInventoryRepository(), new StubItemBuilder());
            _services = new ServiceCollection()
                .AddSingleton(new PlayfieldLocality(4582, null))
                .AddSingleton(_world.Flush)
                .AddSingleton(Moves)
                .AddSingleton(Uses)
                .BuildServiceProvider();
            typeof(Playfield).GetField("_serviceProvider", BindingFlags.Instance | BindingFlags.NonPublic)!
                .SetValue(Player.Playfield, _services);
            Player.Stats.Set(CharacterStat.Health, 20);
            Player.Stats.Set(CharacterStat.MaxHealth, 100);
        }

        public Player Player => _world.Player;

        public InventoryActionTests.Session Session => _world.Session;

        public InventoryMoveService Moves { get; }

        public ItemUseService Uses { get; }

        public int Health => Player.Stats.GetOrZero(CharacterStat.Health);

        public Item Potion(int id, int delay, int count = 1, int slot = 64)
        {
            Item item = _world.Add(id, count, slot);
            item.Definition.Stats[CharacterStat.Can] = (int)(CanFlags.Use | CanFlags.Consume | CanFlags.Stackable);
            item.Definition.Stats[CharacterStat.AttackDelay] = delay;
            item.SpellList[EventType.OnUse] = new List<ItemSpell>
            {
                new() { FunctionType = (int)FunctionType.Hit, Target = (int)ItemTarget.User,
                    Arguments = new List<object> { (int)CharacterStat.Health, Heal } }
            };
            return item;
        }

        public Item Armor(int id, int slot)
        {
            Item item = _world.Add(id, 1, slot);
            item.Definition.Stats[CharacterStat.ItemClass] = (int)ItemClass.Armor;
            item.Definition.Stats[CharacterStat.Slot] = 1 << 1;
            return item;
        }

        public void Dispose()
        {
            _services.Dispose();
            _world.Dispose();
        }
    }
}
