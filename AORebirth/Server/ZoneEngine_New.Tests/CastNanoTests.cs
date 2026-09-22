namespace ZoneEngine_New.Tests
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Reflection;
    using System.Runtime.CompilerServices;
    using System.Threading;

    using AORebirth.Enums;

    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    using SmokeLounge.AOtomation.Messaging.GameData;
    using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;

    using ZoneEngine_New.Core.Data;
    using ZoneEngine_New.Core.Entities;
    using ZoneEngine_New.Core.Inventory;
    using ZoneEngine_New.Core.Nanos;
    using ZoneEngine_New.Core.Playfield;
    using ZoneEngine_New.Core.Teams;

    using Vector3 = AORebirth.Core.Vector.Vector3;

    [TestClass]
    public sealed class CastNanoTests
    {
        const int BuffId = 21001;
        const int InstantId = 21002;
        const int ChildId = 21003;
        const int HostileId = 21004;

        [TestMethod]
        public void OnUseCastNanoOccupiesNcuWithoutTouchingCastState()
        {
            var items = new StubItemBuilder().Add(Timed(BuffId));
            Player player = ReadyPlayer(1);
            player.BeginNanoCast(new PendingNanoCast(TestNanos.Create(9999, attackDelay: 200), player.Identity, 0, 200, DateTime.UtcNow));

            Assert.IsTrue(UseCast(player, items, FunctionType.CastNano, BuffId));
            Assert.AreEqual(1, player.Buffs.Count);
            Assert.AreEqual(BuffId, player.Buffs[0].Id);
            Assert.AreEqual(10, player.UsedNcu);
            Assert.IsNotNull(player.PendingCast);
            Assert.IsFalse(player.IsInNanoRecharge(DateTime.UtcNow));
        }

        [TestMethod]
        public void InstantCastNanoRunsOnUseAndDoesNotEnterNcu()
        {
            var items = new StubItemBuilder().Add(InstantHit(InstantId, heal: 12));
            Player player = ReadyPlayer(1);
            player.Stats.Set(CharacterStat.MaxHealth, 100);
            player.Stats.Set(CharacterStat.Health, 40);

            Assert.IsTrue(UseCast(player, items, FunctionType.CastNano, InstantId));
            Assert.AreEqual(0, player.Buffs.Count);
            Assert.AreEqual(52, player.Stats.GetOrZero(CharacterStat.Health));
        }

        [TestMethod]
        public void CastNanoLeavesAnExistingPendingCastUntouched()
        {
            var items = new StubItemBuilder().Add(Timed(BuffId));
            Player player = ReadyPlayer(1);
            var pending = new PendingNanoCast(TestNanos.Create(8888, attackDelay: 400), player.Identity, 15, 400, DateTime.UtcNow);
            player.BeginNanoCast(pending);

            Assert.IsTrue(UseCast(player, items, FunctionType.CastNano, BuffId));
            Assert.AreSame(pending, player.PendingCast);
        }

        [TestMethod]
        public void NcuRefusalDoesNotApply()
        {
            var items = new StubItemBuilder().Add(TestNanos.Create(BuffId, ncuCost: 40));
            Player player = ReadyPlayer(1);
            player.Stats.Set(CharacterStat.MaxNCU, 10);

            Assert.IsFalse(UseCast(player, items, FunctionType.CastNano, BuffId));
            Assert.AreEqual(0, player.Buffs.Count);
            Assert.AreEqual(0, player.UsedNcu);
        }

        [TestMethod]
        public void NestedCastNanoFromParentOnUseLandsTheChild()
        {
            NanoSpell parent = TestNanos.Create(
                BuffId,
                modifiers: [Cast(FunctionType.CastNano, ChildId)]);
            var items = new StubItemBuilder().Add(parent).Add(Timed(ChildId));
            Player player = ReadyPlayer(1);

            Assert.IsTrue(NanoRuntime.TryApplyImmediate(
                player,
                player,
                BuffId,
                items,
                new SilentInventory(),
                DateTime.UtcNow));
            Assert.AreEqual(2, player.Buffs.Count);
            Assert.IsTrue(player.TryGetBuff(BuffId, out _));
            Assert.IsTrue(player.TryGetBuff(ChildId, out _));
        }

        [TestMethod]
        public void AreaCastNanoUsesRadiusAndAttackableNotVendorSkip()
        {
            var items = new StubItemBuilder().Add(TestNanos.Create(HostileId, can: CanFlags.ApplyOnHostile));
            using var world = new FanoutWorld(items);
            Player caster = world.Player(1, 0, 0, 0);
            NpcCharacter nearVendor = world.Npc(2, 4, 0, 0, attackable: true);
            NpcCharacter far = world.Npc(3, 40, 0, 0, attackable: true);
            NpcCharacter nearSafe = world.Npc(4, 3, 0, 0, attackable: false);

            Assert.IsTrue(UseCast(caster, items, FunctionType.AreaCastNano, HostileId, radius: 10));
            Assert.AreEqual(0, caster.Buffs.Count);
            Assert.AreEqual(1, nearVendor.Buffs.Count);
            Assert.AreEqual(0, far.Buffs.Count);
            Assert.AreEqual(0, nearSafe.Buffs.Count);
        }

        [TestMethod]
        public void TeamCastNanoLandsOnTeammatesAndSoloSelf()
        {
            var items = new StubItemBuilder().Add(Timed(BuffId));
            using var world = new FanoutWorld(items);
            Player a = world.Player(1, 0, 0, 0);
            Player b = world.Player(2, 10, 0, 0);
            world.Join(a, b);

            Assert.IsTrue(UseCast(a, items, FunctionType.TeamCastNano, BuffId));
            Assert.AreEqual(1, a.Buffs.Count);
            Assert.AreEqual(1, b.Buffs.Count);

            Player solo = ReadyPlayer(9);
            Assert.IsTrue(UseCast(solo, items, FunctionType.TeamCastNano, BuffId));
            Assert.AreEqual(1, solo.Buffs.Count);
        }

        [TestMethod]
        public void PlayfieldNanoStaysOnTheCurrentPlayfield()
        {
            CanFlags can = CanFlags.ApplyOnSelf | CanFlags.ApplyOnFriendly | CanFlags.ApplyOnHostile;
            var items = new StubItemBuilder().Add(TestNanos.Create(BuffId, can: can));
            using var world = new FanoutWorld(items);
            Player caster = world.Player(1, 0, 0, 0);
            NpcCharacter local = world.Npc(2, 8, 0, 0, attackable: true);
            using var other = new FanoutWorld(items);
            Player stranger = other.Player(3, 0, 0, 0);

            Assert.IsTrue(UseCast(caster, items, FunctionType.PlayfieldNano, BuffId));
            Assert.AreEqual(1, caster.Buffs.Count);
            Assert.AreEqual(1, local.Buffs.Count);
            Assert.AreEqual(0, stranger.Buffs.Count);
        }

        [TestMethod]
        public void WearCastNanoLandsNpcControllerBuffThatExceedsMaxNcu()
        {
            NanoSpell controller = TestNanos.Create(205606, ncuCost: 999, durationCentiseconds: 72000000);
            var items = new StubItemBuilder().Add(controller);
            Item gear = WearItem(Cast(FunctionType.CastNano, 205606));
            var npc = new NpcCharacter(new Identity { Type = IdentityType.CanbeAffected, Instance = 81 }, items);
            npc.Stats.Set(CharacterStat.MaxNCU, 81);
            npc.Equipment.Add(0, gear);

            WearCastNano.ApplyContainer(npc, npc.Equipment, includeWield: true, items, new SilentInventory());
            Assert.AreEqual(1, npc.Buffs.Count);
            Assert.AreEqual(205606, npc.Buffs[0].Id);
        }

        [TestMethod]
        public void PlayerWearCastNanoStillRefusesWhenNcuDoesNotFit()
        {
            NanoSpell controller = TestNanos.Create(205606, ncuCost: 999, durationCentiseconds: 72000000);
            var items = new StubItemBuilder().Add(controller);
            Player player = ReadyPlayer(1);
            player.Stats.Set(CharacterStat.MaxNCU, 81);
            Item gear = WearItem(Cast(FunctionType.CastNano, 205606));

            WearCastNano.ApplyItem(player, gear, includeWield: false, items, new SilentInventory());
            Assert.AreEqual(0, player.Buffs.Count);
        }

        [TestMethod]
        public void WearCastNanoAppliesOnceAndRebaseDoesNotRecast()
        {
            NanoSpell nano = Timed(BuffId);
            var items = new StubItemBuilder().Add(nano);
            Item gear = WearItem(Cast(FunctionType.CastNano, BuffId));
            var npc = new NpcCharacter(new Identity { Type = IdentityType.CanbeAffected, Instance = 80 }, items);
            npc.Stats.Set(CharacterStat.MaxNCU, 60);
            npc.Equipment.Add(0, gear);

            WearCastNano.ApplyContainer(npc, npc.Equipment, includeWield: true, items, new SilentInventory());
            npc.Rebase();
            Assert.AreEqual(1, npc.Buffs.Count);
            npc.RebaseStats();
            Assert.AreEqual(1, npc.Buffs.Count);
        }

        [TestMethod]
        public void PlayerHydrationRebaseDoesNotCastWearNanos()
        {
            NanoSpell nano = Timed(BuffId);
            var items = new StubItemBuilder().Add(nano);
            Player player = ReadyPlayer(1);
            Item gear = WearItem(Cast(FunctionType.CastNano, BuffId));
            Assert.IsTrue(player.Inventory.Armor.Add(0x11, gear));

            player.Rebase();
            Assert.AreEqual(0, player.Buffs.Count);

            WearCastNano.ApplyItem(player, gear, includeWield: false, items, new SilentInventory());
            Assert.AreEqual(1, player.Buffs.Count);
            player.RebaseStats();
            Assert.AreEqual(1, player.Buffs.Count);
        }

        static bool UseCast(Player player, StubItemBuilder items, FunctionType function, int nanoId, int radius = 0)
        {
            var arguments = new List<object> { nanoId };
            if (function == FunctionType.AreaCastNano)
                arguments.Add(radius);

            var template = new ItemTemplate
            {
                Id = 9000,
                SpellList = new Dictionary<EventType, List<ItemSpell>>
                {
                    [EventType.OnUse] = [new ItemSpell { FunctionType = (int)function, Arguments = arguments }]
                }
            };
            return template.ExecuteOnUseSpells(player, new SilentInventory(), items);
        }

        static Player ReadyPlayer(int id)
        {
            Player player = TestWorld.CreatePlayer(id);
            player.Stats.Set(CharacterStat.MaxNCU, 60);
            return player;
        }

        static NanoSpell Timed(int id)
            => TestNanos.Create(id);

        static NanoSpell InstantHit(int id, int heal)
            => TestNanos.Create(
                id,
                durationCentiseconds: 0,
                modifiers:
                [
                    new ItemSpell
                    {
                        FunctionType = (int)FunctionType.Hit,
                        Arguments = [(int)CharacterStat.Health, heal, heal]
                    }
                ]);

        static ItemSpell Cast(FunctionType function, int nanoId)
            => new()
            {
                FunctionType = (int)function,
                Arguments = [nanoId]
            };

        static Item WearItem(ItemSpell spell)
        {
            Item item = TestWorld.CreateItem(lowId: 3000, highId: 3000, name: "WearCast");
            item.Definition.SpellList[EventType.OnWear] = [spell];
            return item;
        }

        sealed class FanoutWorld : IDisposable
        {
            readonly ServiceProvider _services;
            readonly DynelRegistry _registry;
            readonly TeamService _teams = new(dispatchOnOwner: (_, action) => action());
            readonly PlayfieldManager _manager;

            public Playfield Playfield { get; }

            public FanoutWorld(StubItemBuilder items)
            {
                _registry = new DynelRegistry();
                _manager = (PlayfieldManager)RuntimeHelpers.GetUninitializedObject(typeof(PlayfieldManager));
                typeof(PlayfieldManager).GetField("_sync", BindingFlags.Instance | BindingFlags.NonPublic)!
                    .SetValue(_manager, new Lock());
                typeof(PlayfieldManager).GetField("_playersByCharacterId", BindingFlags.Instance | BindingFlags.NonPublic)!
                    .SetValue(_manager, new Dictionary<int, Player>());

                Playfield = (Playfield)RuntimeHelpers.GetUninitializedObject(typeof(Playfield));
                _services = new ServiceCollection()
                    .AddSingleton(_registry)
                    .AddSingleton(_teams)
                    .AddSingleton(_manager)
                    .AddSingleton<IItemBuilder>(items)
                    .AddSingleton<IInventoryRepository>(new SilentInventory())
                    .BuildServiceProvider();
                typeof(Playfield).GetField("_serviceProvider", BindingFlags.Instance | BindingFlags.NonPublic)!
                    .SetValue(Playfield, _services);
                typeof(Playfield).GetField("_pendingStatRebases", BindingFlags.Instance | BindingFlags.NonPublic)!
                    .SetValue(Playfield, new ConcurrentDictionary<Character, byte>());
            }

            public Player Player(int id, double x, double y, double z)
            {
                Player player = ReadyPlayer(id);
                player.Stats.Set(CharacterStat.Level, 60);
                player.Stats.Set(CharacterStat.Profession, 3);
                var session = new InventoryActionTests.Session();
                session.BindPlayer(player);
                player.Session = session;
                player.Playfield = Playfield;
                player.Position = new Vector3(x, y, z);
                _registry.Register(player);
                _manager.RegisterPlayer(player);
                _teams.AttachPlayer(player);
                return player;
            }

            public NpcCharacter Npc(int id, double x, double y, double z, bool attackable)
            {
                var npc = new NpcCharacter(new Identity { Type = IdentityType.CanbeAffected, Instance = id }, new StubItemBuilder())
                {
                    Playfield = Playfield,
                    Position = new Vector3(x, y, z),
                    Attackable = attackable
                };
                npc.Stats.Set(CharacterStat.MaxNCU, 60);
                _registry.Register(npc);
                return npc;
            }

            public void Join(Player inviter, Player invitee)
            {
                _teams.TryHandle(inviter, new CharacterActionMessage
                {
                    Identity = inviter.Identity,
                    Action = CharacterActionType.TeamRequestInvite,
                    Target = invitee.Identity
                });
                _teams.TryHandle(invitee, new CharacterActionMessage
                {
                    Identity = invitee.Identity,
                    Action = CharacterActionType.TeamRequestReply,
                    Target = inviter.Identity,
                    Parameter2 = 1
                });
            }

            public void Dispose() => _services.Dispose();
        }

        sealed class SilentInventory : IInventoryRepository
        {
            public IReadOnlyList<ItemInstanceRecord> GetCarriedItems(int characterId) => [];
            public IReadOnlyList<ItemInstanceRecord> GetBankItems(int characterId) => [];
            public IReadOnlyList<ItemInstanceRecord> GetContainerItems(int containerInstanceId) => [];
            public int LeaseInstanceIdBlock(int count) => 1;
            public ItemInstanceRecord Insert(ItemInstanceRecord item) => item;
            public void UpdateLocation(int instanceId, int containerType, int containerInstance, int containerPlacement) { }
            public void UpdateLocations(IReadOnlyList<ItemLocationUpdate> locations) { }
            public void PersistNewAndUpdateLocations(
                IReadOnlyList<ItemInstanceRecord> inserts,
                IReadOnlyList<ItemLocationUpdate> updates)
            {
            }
        }
    }
}
