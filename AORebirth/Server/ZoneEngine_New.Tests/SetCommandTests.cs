namespace ZoneEngine_New.Tests
{
    using System;
    using System.Linq;
    using System.Reflection;
    using System.Runtime.CompilerServices;

    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    using SmokeLounge.AOtomation.Messaging.GameData;
    using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;

    using ZoneEngine_New.Core.Commands;
    using ZoneEngine_New.Core.Entities;
    using ZoneEngine_New.Core.Helpers;
    using ZoneEngine_New.Core.Playfield;
    using ZoneEngine_New.Core.Playfield.Locality;

    [TestClass]
    public sealed class SetCommandTests
    {
        [TestMethod]
        public void Set_WithoutTarget_ChangesIssuer()
        {
            Player gm = TestWorld.CreatePlayer(1, "Gm");
            gm.Name = "Gm";
            var session = new RecordingZoneSession();

            new SetCommand().Execute(new GmCommandContext(session, gm, ["health", "80"]));

            Assert.AreEqual(80, gm.Stats.Get(CharacterStat.Health));
            Assert.IsTrue(LastText(session).Contains("[Gm]", StringComparison.Ordinal));
        }

        [TestMethod]
        public void Set_MaxHealth_OnPlayer_IsReplacedByCalculatedMax()
        {
            Player gm = TestWorld.CreatePlayer(1, "Gm");
            gm.Stats.Set(CharacterStat.Breed, (int)Breed.Solitus);
            gm.Stats.Set(CharacterStat.Profession, (int)Profession.Soldier);
            gm.Stats.Set(CharacterStat.Level, 10);
            gm.Stats.Set(CharacterStat.TitleLevel, 1);
            gm.Stats.Set(CharacterStat.BodyDevelopment, 40);
            int expected = MaxHealthCalculator.Compute((int)Breed.Solitus, (int)Profession.Soldier, 1, 10, 40);

            new SetCommand().Execute(new GmCommandContext(new RecordingZoneSession(), gm, ["1", "1840"]));

            Assert.AreEqual(expected, gm.Stats.Get(CharacterStat.MaxHealth));

            gm.Stats.Set(CharacterStat.NPCFamily, 5);
            new SetCommand().Execute(new GmCommandContext(new RecordingZoneSession(), gm, ["1", "999999"]));

            Assert.AreEqual(expected, gm.Stats.Get(CharacterStat.MaxHealth, StatDetail.Base));
        }

        [TestMethod]
        public void Set_Health_And_Nano_AboveMax_AreCappedAtMax()
        {
            Player gm = TestWorld.CreatePlayer(1, "Gm");
            gm.Stats.Set(CharacterStat.MaxHealth, 500);
            gm.Stats.Set(CharacterStat.MaxNanoEnergy, 300);
            gm.Stats.Set(CharacterStat.Health, 400);
            gm.Stats.Set(CharacterStat.CurrentNano, 200);

            new SetCommand().Execute(new GmCommandContext(new RecordingZoneSession(), gm, ["health", "9999"]));
            new SetCommand().Execute(new GmCommandContext(new RecordingZoneSession(), gm, ["currentnano", "9999"]));

            Assert.AreEqual(500, gm.Stats.Get(CharacterStat.Health));
            Assert.AreEqual(300, gm.Stats.Get(CharacterStat.CurrentNano));

            new SetCommand().Execute(new GmCommandContext(new RecordingZoneSession(), gm, ["health", "120"]));
            Assert.AreEqual(120, gm.Stats.Get(CharacterStat.Health));
        }

        [TestMethod]
        public void Set_WithPlayerTarget_ChangesTargetNotIssuer()
        {
            using var world = new CommandWorld();
            Player gm = world.Player(1, "Gm");
            Player target = world.Player(2, "Target");
            gm.SetTarget(target.Identity);
            var session = new RecordingZoneSession();

            new SetCommand().Execute(new GmCommandContext(session, gm, ["health", "250"]));

            Assert.AreEqual(250, target.Stats.Get(CharacterStat.Health));
            Assert.IsFalse(gm.Stats.TryGetValue(CharacterStat.Health, out _));
            Assert.IsTrue(LastText(session).Contains("[Target]", StringComparison.Ordinal));
        }

        [TestMethod]
        public void Set_WithNpcTarget_ChangesNpcStat()
        {
            using var world = new CommandWorld();
            Player gm = world.Player(1, "Gm");
            NpcCharacter npc = world.Npc(9, "Leet");
            gm.SetTarget(npc.Identity);

            new SetCommand().Execute(new GmCommandContext(new RecordingZoneSession(), gm, ["level", "42"]));

            Assert.AreEqual(42, npc.Stats.Get(CharacterStat.Level));
            Assert.IsFalse(gm.Stats.TryGetValue(CharacterStat.Level, out _));
        }

        static string LastText(RecordingZoneSession session)
            => session.Sent.OfType<ChatTextMessage>().Last().Text ?? string.Empty;

        sealed class CommandWorld : IDisposable
        {
            readonly ServiceProvider _services;
            readonly DynelRegistry _registry = new();

            public Playfield Playfield { get; }

            public CommandWorld()
            {
                Playfield = (Playfield)RuntimeHelpers.GetUninitializedObject(typeof(Playfield));
                _services = new ServiceCollection()
                    .AddSingleton(_registry)
                    .AddSingleton(new PlayfieldLocality(1, null))
                    .BuildServiceProvider();
                typeof(Playfield).GetField("_serviceProvider", BindingFlags.Instance | BindingFlags.NonPublic)!
                    .SetValue(Playfield, _services);
            }

            public Player Player(int id, string name)
            {
                Player player = TestWorld.CreatePlayer(id, name);
                player.Name = name;
                player.Playfield = Playfield;
                _registry.Register(player);
                return player;
            }

            public NpcCharacter Npc(int id, string name)
            {
                var npc = new NpcCharacter(
                    new Identity { Type = IdentityType.CanbeAffected, Instance = id },
                    new StubItemBuilder())
                {
                    Name = name,
                    Playfield = Playfield
                };
                _registry.Register(npc);
                return npc;
            }

            public void Dispose() => _services.Dispose();
        }
    }
}
