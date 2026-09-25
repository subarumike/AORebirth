namespace ZoneEngine_New.Tests
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Reflection;
    using System.Threading;

    using AORebirth.Enums;

    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    using SmokeLounge.AOtomation.Messaging.GameData;
    using SmokeLounge.AOtomation.Messaging.Messages;

    using ZoneEngine_New.Core.Entities;
    using ZoneEngine_New.Core.GameData;
    using ZoneEngine_New.Core.Inventory;
    using ZoneEngine_New.Core.Network;
    using ZoneEngine_New.Core.Playfield;
    using ZoneEngine_New.Core.WorldSimulation;

    using Vector3 = AORebirth.Core.Vector.Vector3;

    [TestClass]
    public sealed class TeleportFunctionTests
    {
        const int SourcePlayfieldId = 100;
        const int WetlandsPlayfieldId = 4312;

        [TestMethod]
        public void PassageToTheWetlandsTeleportsWhenShadowlandsExpansionIsSet()
        {
            using var root = new TempPlayfieldRoot(WetlandsPlayfieldId);
            var destination = BlankPlayfield(WetlandsPlayfieldId, root.Path, manager: null);
            var manager = BlankManager(destination);
            var source = BlankPlayfield(SourcePlayfieldId, root.Path, manager);
            var session = new RecordingSession();
            Player player = PlayerOn(source, session, expansion: 2);
            player.Stats.Set(CharacterStat.ExternalPlayfieldInstance, 9, StatDetail.Base, dirty: true);
            player.Stats.Set(CharacterStat.ExternalDoorInstance, 8, StatDetail.Base, dirty: true);

            Assert.IsTrue(WetlandsTemplate().ExecuteOnUseSpells(
                player,
                new StubInventoryRepository(),
                new StubItemBuilder()));

            Assert.AreSame(destination, session.Destination);
            Assert.AreEqual(1402f, session.Landing.xf, 0.001f);
            Assert.AreEqual(25f, session.Landing.yf, 0.001f);
            Assert.AreEqual(1807f, session.Landing.zf, 0.001f);
            Assert.AreEqual(0, player.Stats.GetOrZero(CharacterStat.ExternalPlayfieldInstance));
            Assert.AreEqual(0, player.Stats.GetOrZero(CharacterStat.ExternalDoorInstance));
        }

        [TestMethod]
        public void PassageToTheWetlandsRefusesWithoutShadowlandsExpansion()
        {
            using var root = new TempPlayfieldRoot(WetlandsPlayfieldId);
            var destination = BlankPlayfield(WetlandsPlayfieldId, root.Path, manager: null);
            var source = BlankPlayfield(SourcePlayfieldId, root.Path, BlankManager(destination));
            var session = new RecordingSession();
            Player player = PlayerOn(source, session, expansion: 0);

            Assert.IsFalse(WetlandsTemplate().ExecuteOnUseSpells(
                player,
                new StubInventoryRepository(),
                new StubItemBuilder()));
            Assert.IsNull(session.Destination);
        }

        [TestMethod]
        public void TeleportRefusesAnUnknownPlayfield()
        {
            using var root = new TempPlayfieldRoot(WetlandsPlayfieldId);
            var source = BlankPlayfield(SourcePlayfieldId, root.Path, BlankManager(BlankPlayfield(WetlandsPlayfieldId, root.Path, null)));
            var session = new RecordingSession();
            Player player = PlayerOn(source, session, expansion: 2);
            ItemTemplate template = WetlandsTemplate();
            template.SpellList[EventType.OnUse][0].Arguments[3] = 999001;

            Assert.IsFalse(template.ExecuteOnUseSpells(
                player,
                new StubInventoryRepository(),
                new StubItemBuilder()));
            Assert.IsNull(session.Destination);
        }

        static ItemTemplate WetlandsTemplate()
        {
            return new ItemTemplate
            {
                Id = 244721,
                SpellList =
                {
                    [EventType.OnUse] =
                    [
                        new ItemSpell
                        {
                            FunctionType = (int)FunctionType.Teleport,
                            Arguments = new List<object> { 1402, 25, 1807, WetlandsPlayfieldId },
                            Requirements =
                            [
                                new ItemRequirement
                                {
                                    StatNumber = (int)CharacterStat.Expansion,
                                    Operator = (int)Operator.BitAnd,
                                    Value = 2
                                }
                            ]
                        }
                    ]
                }
            };
        }

        static Player PlayerOn(Playfield source, RecordingSession session, int expansion)
        {
            Player player = TestWorld.CreatePlayer(1);
            player.Session = session;
            session.BindPlayer(player);
            player.Playfield = source;
            player.Stats.Set(CharacterStat.Expansion, expansion, StatDetail.Base, dirty: true);
            return player;
        }

        static Playfield BlankPlayfield(int id, string root, PlayfieldManager? manager)
        {
            var playfield = (Playfield)System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject(typeof(Playfield));
            Set(playfield, "<Identity>k__BackingField", new Identity { Type = IdentityType.Playfield, Instance = id });
            IGameData data = new StubGameData(HashItemCatalog.Parse("{}"), rootPath: root);
            IServiceCollection services = new ServiceCollection()
                .AddSingleton(data)
                .AddSingleton(new WorldSimulationAccess());
            if (manager != null)
                services.AddSingleton(manager);
            Set(playfield, "_serviceProvider", services.BuildServiceProvider());
            return playfield;
        }

        static PlayfieldManager BlankManager(Playfield destination)
        {
            var manager = (PlayfieldManager)System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject(typeof(PlayfieldManager));
            Set(manager, "_sync", new Lock());
            Set(
                manager,
                "_playfields",
                new Dictionary<int, Playfield> { [destination.Identity.Instance] = destination });
            return manager;
        }

        static void Set(object target, string name, object value)
            => target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(target, value);

        sealed class TempPlayfieldRoot : IDisposable
        {
            public TempPlayfieldRoot(int playfieldId)
            {
                Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "aorebirth-teleport-" + Guid.NewGuid().ToString("N"));
                Directory.CreateDirectory(System.IO.Path.Combine(Path, "Playfields", playfieldId.ToString(System.Globalization.CultureInfo.InvariantCulture)));
            }

            public string Path { get; }

            public void Dispose()
            {
                if (Directory.Exists(Path))
                    Directory.Delete(Path, true);
            }
        }

        sealed class RecordingSession : IZoneSession
        {
            public SessionState State { get; set; } = SessionState.InPlay;

            public Player? Player { get; private set; }

            public Playfield? Destination { get; private set; }

            public Vector3 Landing { get; private set; }

            public void BindPlayer(Player player) => Player = player;

            public void UnbindPlayer() => Player = null;

            public void TransferToPlayfield(Playfield destination, Vector3 landing)
            {
                Destination = destination;
                Landing = landing;
            }

            public void Send(byte[] packet)
            {
            }

            public void Send(Message message)
            {
            }

            public void Send(MessageBody body)
            {
            }

            public void Send(MessageBody body, int sender, int receiver)
            {
            }

            public void SendInitiateCompression()
            {
            }

            public void Close() => State = SessionState.Closed;
        }
    }
}
