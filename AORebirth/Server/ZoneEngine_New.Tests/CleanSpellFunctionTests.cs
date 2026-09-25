namespace ZoneEngine_New.Tests
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Linq;
    using System.Reflection;
    using System.Runtime.CompilerServices;

    using AORebirth.Enums;

    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    using SmokeLounge.AOtomation.Messaging.GameData;
    using SmokeLounge.AOtomation.Messaging.Messages;
    using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;

    using ZoneEngine_New.Core.Ai;
    using ZoneEngine_New.Core.Data;
    using ZoneEngine_New.Core.Entities;
    using ZoneEngine_New.Core.GameData;
    using ZoneEngine_New.Core.Inventory;
    using ZoneEngine_New.Core.Nanos;
    using ZoneEngine_New.Core.Network;
    using ZoneEngine_New.Core.Playfield;

    using Vector3 = AORebirth.Core.Vector.Vector3;

    [TestClass]
    public sealed class CleanSpellFunctionTests
    {
        const CharacterStat FlagStat = (CharacterStat)80;

        [TestMethod]
        public void ToggleFlagFlipsOneBitAndRejectsAnOutOfRangeIndex()
        {
            Player player = TestWorld.CreatePlayer(1);
            Assert.IsTrue(Run(player, FunctionType.ToggleFlag, (int)FlagStat, 0));
            Assert.AreEqual(1, player.Stats.GetOrZero(FlagStat, StatDetail.Base));
            Assert.IsTrue(Run(player, FunctionType.ToggleFlag, (int)FlagStat, 0));
            Assert.AreEqual(0, player.Stats.GetOrZero(FlagStat, StatDetail.Base));
            Assert.IsFalse(Run(player, FunctionType.ToggleFlag, (int)FlagStat, 32));
            Assert.AreEqual(0, player.Stats.GetOrZero(FlagStat, StatDetail.Base));
        }

        [TestMethod]
        public void CastNanoIfPossibleSucceedsWhenTheChildCannotLand()
        {
            Player player = TestWorld.CreatePlayer(2);
            player.Stats.Set(CharacterStat.MaxNCU, 60);
            player.OnDeath();

            Assert.IsFalse(Run(player, FunctionType.CastNano, 21001));
            Assert.IsTrue(Run(player, FunctionType.CastNanoIfPossible, 21001));
            Assert.AreEqual(0, player.Buffs.Count);
        }

        [TestMethod]
        public void CastOnFightTargetLandsOnTheFightingCharacterOnly()
        {
            var items = new StubItemBuilder().Add(TestNanos.Create(21011, strain: 4));
            Player caster = TestWorld.CreatePlayer(3);
            Player opponent = TestWorld.CreatePlayer(4);
            caster.Stats.Set(CharacterStat.MaxNCU, 60);
            opponent.Stats.Set(CharacterStat.MaxNCU, 60);
            var registry = new DynelRegistry();
            Playfield playfield = PlayfieldWith(registry);
            caster.Playfield = playfield;
            opponent.Playfield = playfield;
            registry.Register(caster);
            registry.Register(opponent);
            caster.SetFightingTarget(opponent.Identity);

            Assert.IsTrue(Run(caster, FunctionType.CastNanoIfPossibleOnFightTarget, items, 21011));
            Assert.AreEqual(0, caster.Buffs.Count);
            Assert.AreEqual(1, opponent.Buffs.Count);

            caster.SetFightingTarget(Identity.None);
            Assert.IsTrue(Run(caster, FunctionType.NpcCastNanoIfPossibleOnFightTarget, items, 21012));
            Assert.AreEqual(1, opponent.Buffs.Count);
        }

        [TestMethod]
        public void StripRemovesUncancellableBuffsByIdStrainAndAll()
        {
            Player player = Ready(5);
            var items = new StubItemBuilder()
                .Add(TestNanos.Create(31, strain: 6, canCancel: false))
                .Add(TestNanos.Create(32, strain: 7, canCancel: false))
                .Add(TestNanos.Create(33, strain: 0, canCancel: false));
            Assert.IsTrue(NanoRuntime.TryApplyImmediate(player, player, 31, items, new SilentInventory(), DateTime.UtcNow));
            Assert.IsTrue(NanoRuntime.TryApplyImmediate(player, player, 32, items, new SilentInventory(), DateTime.UtcNow));
            Assert.IsTrue(NanoRuntime.TryApplyImmediate(player, player, 33, items, new SilentInventory(), DateTime.UtcNow));
            Assert.AreEqual(BuffRemovalOutcome.NotCancellable, NanoRuntime.TryCancelBuff(player, 31));

            Assert.IsFalse(Run(player, FunctionType.RemoveNanoStrain, 0));
            Assert.AreEqual(3, player.Buffs.Count);
            Assert.IsTrue(Run(player, FunctionType.RemoveNano, 31));
            Assert.IsFalse(player.TryGetBuff(31, out _));
            Assert.IsTrue(Run(player, FunctionType.RemoveNanoStrain, 7));
            Assert.IsFalse(player.TryGetBuff(32, out _));
            Assert.IsTrue(player.TryGetBuff(33, out _));
            Assert.IsTrue(Run(player, FunctionType.RemoveBuffs));
            Assert.AreEqual(0, player.Buffs.Count);
        }

        [TestMethod]
        public void SpawnItemGrantsOneCatalogItemAndRefusesAnUnknownMode()
        {
            Player player = TestWorld.CreatePlayer(6);
            var sent = new RecordingSession();
            player.Session = sent;
            HashItemCatalog catalog = HashItemCatalog.Parse(
                """
                { "ENAU": { "Templates": [1] } }
                """);
            var builder = new StubItemBuilder();
            player.Playfield = PlayfieldWith(
                new HashItemMinter(new StubGameData(catalog), new StubCatalog(), builder),
                builder);

            Assert.IsFalse(Run(player, FunctionType.SpawnItem, "ENAU", 10, 2));
            Assert.AreEqual(0, player.Inventory.Inventory.Content.Count);

            Assert.IsTrue(Run(player, FunctionType.SpawnItem, "ENAU", 10, 0));
            Assert.AreEqual(1, player.Inventory.Inventory.Content.Count);
            AddTemplateMessage granted = sent.Bodies.OfType<AddTemplateMessage>().Single();
            Assert.AreEqual(1, granted.LowId);
            Assert.AreEqual(10, granted.Quality);
            Assert.AreEqual(1, granted.Count);
        }

        [TestMethod]
        public void NpcSocialAnimSendsTheAnimationId()
        {
            Player player = TestWorld.CreatePlayer(7);
            var sent = new RecordingSession();
            player.Session = sent;

            Assert.IsFalse(Run(player, FunctionType.NpcSocialAnim, 0));
            Assert.IsTrue(Run(player, FunctionType.NpcSocialAnim, 65));

            CharacterActionMessage anim = sent.Bodies.OfType<CharacterActionMessage>().Single();
            Assert.AreEqual(CharacterActionType.NpcSocialAnim, anim.Action);
            Assert.AreEqual(65, anim.Target.Instance);
        }

        [TestMethod]
        public void NpcControlUsesBrainHomeHateAndTheSelectedOpponent()
        {
            NpcCharacter npc = new(
                new Identity { Type = IdentityType.CanbeAffected, Instance = 1001 },
                new StubItemBuilder());
            var home = new Vector3(4f, 5f, 6f);
            NpcBrain brain = NpcBrain.Create(npc, home);
            Player player = TestWorld.CreatePlayer(8);
            brain.AddThreat(player.Identity, 5f);
            npc.StartFighting(player.Identity, 0);
            npc.Position = new Vector3(20f, 0f, 0f);

            Assert.IsTrue(Run(npc, FunctionType.NpcWipeHateList));
            Assert.AreEqual(0f, brain.Hate.ThreatOf(player.Identity));
            Assert.AreEqual(Identity.None, npc.FightingTarget);

            Assert.IsTrue(Run(npc, FunctionType.NpcStopMoving));
            Assert.IsTrue(Run(npc, player, FunctionType.NpcFightSelected));
            Assert.AreEqual(player.Identity, npc.FightingTarget);
            Assert.AreEqual(NpcAiRules.ProximityHate, brain.Hate.ThreatOf(player.Identity));

            Assert.IsTrue(Run(npc, FunctionType.NpcTeleportToSpawnPoint));
            Assert.AreEqual(home.xf, npc.Position.xf);
            Assert.AreEqual(home.yf, npc.Position.yf);
            Assert.AreEqual(home.zf, npc.Position.zf);

            NpcCharacter homeless = new(
                new Identity { Type = IdentityType.CanbeAffected, Instance = 1002 },
                new StubItemBuilder());
            NpcBrain.Create(homeless, home: null);
            Assert.IsFalse(Run(homeless, FunctionType.NpcTeleportToSpawnPoint));
            Assert.IsFalse(Run(player, FunctionType.NpcWipeHateList));
        }

        static Player Ready(int id)
        {
            Player player = TestWorld.CreatePlayer(id);
            player.Stats.Set(CharacterStat.MaxNCU, 60);
            return player;
        }

        static bool Run(Character target, FunctionType function, params object[] arguments)
            => Run(target, source: null, function, new StubItemBuilder(), arguments);

        static bool Run(Character target, FunctionType function, StubItemBuilder items, params object[] arguments)
            => Run(target, source: null, function, items, arguments);

        static bool Run(Character target, Character source, FunctionType function)
            => Run(target, source, function, new StubItemBuilder(), []);

        static bool Run(
            Character target,
            Character? source,
            FunctionType function,
            StubItemBuilder items,
            object[] arguments)
        {
            var spell = new ItemSpell
            {
                FunctionType = (int)function,
                Arguments = new List<object>(arguments)
            };
            return ItemUseFunctions.TryExecute(1, target, source, spell, new SilentInventory(), items);
        }

        static Playfield PlayfieldWith(params object[] services)
        {
            var collection = new ServiceCollection();
            foreach (object service in services)
                collection.AddSingleton(service.GetType(), service);
            ServiceProvider provider = collection.BuildServiceProvider();
            var playfield = (Playfield)RuntimeHelpers.GetUninitializedObject(typeof(Playfield));
            typeof(Playfield).GetField("_serviceProvider", BindingFlags.Instance | BindingFlags.NonPublic)!
                .SetValue(playfield, provider);
            typeof(Playfield).GetField("_pendingStatRebases", BindingFlags.Instance | BindingFlags.NonPublic)!
                .SetValue(playfield, new ConcurrentDictionary<Character, byte>());
            return playfield;
        }

        sealed class RecordingSession : IZoneSession
        {
            public List<MessageBody> Bodies { get; } = [];

            public SessionState State { get; set; } = SessionState.InPlay;

            public Player? Player { get; private set; }

            public void BindPlayer(Player player) => Player = player;

            public void UnbindPlayer() => Player = null;

            public void TransferToPlayfield(Playfield destination, Vector3 landing)
            {
            }

            public void Send(byte[] packet)
            {
            }

            public void Send(Message message)
            {
            }

            public void Send(MessageBody body) => Bodies.Add(body);

            public void Send(MessageBody body, int sender, int receiver) => Bodies.Add(body);

            public void SendInitiateCompression()
            {
            }

            public void Close() => State = SessionState.Closed;
        }

        sealed class SilentInventory : IInventoryRepository
        {
            public IReadOnlyList<ItemInstanceRecord> GetCarriedItems(int characterId) => [];

            public IReadOnlyList<ItemInstanceRecord> GetBankItems(int characterId) => [];

            public IReadOnlyList<ItemInstanceRecord> GetContainerItems(int containerInstanceId) => [];

            public int LeaseInstanceIdBlock(int count) => 1;

            public ItemInstanceRecord Insert(ItemInstanceRecord item) => item;

            public void UpdateLocation(int instanceId, int containerType, int containerInstance, int containerPlacement)
            {
            }

            public void UpdateLocations(IReadOnlyList<ItemLocationUpdate> locations)
            {
            }

            public void PersistNewAndUpdateLocations(
                IReadOnlyList<ItemInstanceRecord> inserts,
                IReadOnlyList<ItemLocationUpdate> updates)
            {
            }
        }
    }
}
