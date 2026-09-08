namespace ZoneEngine_New.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Reflection;
    using System.Runtime.CompilerServices;
    using System.Threading;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using SmokeLounge.AOtomation.Messaging.GameData;
    using SmokeLounge.AOtomation.Messaging.Messages;
    using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;
    using ZoneEngine_New.Core.Entities;
    using ZoneEngine_New.Core.Inventory;
    using ZoneEngine_New.Core.Nanos;
    using ZoneEngine_New.Core.Network;
    using ZoneEngine_New.Core.Playfield;
    using ZoneEngine_New.Core.Playfield.Locality;
    using ZoneEngine_New.Core.Teams;
    using Vector3 = AORebirth.Core.Vector.Vector3;

    [TestClass]
    public sealed class AmbientRestorationTests
    {
        static NanoDefinition Definition() => new(new ItemTemplate { Id = 302365 });

        [TestMethod]
        public void PrepareHasNoEffectsAndNoInventedNcuRow()
        {
            using var w = new World(); Player caster = w.Add(1);
            Assert.IsTrue(w.Aura.TryPrepare(caster, caster, Definition(), out var plan));
            Assert.IsFalse(plan.UsesActiveNano); Assert.AreEqual(0, plan.DurationCentiseconds);
            Assert.AreEqual(10, caster.Stats.GetOrZero(CharacterStat.Health));
            Assert.AreEqual(0, w.Session(caster).Messages.Count);
            Assert.IsFalse(w.Aura.TryPrepare(caster, w.Add(2), Definition(), out _));
            Assert.IsFalse(w.Aura.TryPrepare(caster, caster, new NanoDefinition(new ItemTemplate { Id = 1 }), out _));
        }

        [TestMethod]
        public void AppliedPulsesOnceThenEveryTwentySecondsWithoutCatchUpBurst()
        {
            using var w = new World(); Player caster = w.Add(1);
            w.Aura.Applied(caster, caster, Definition(), null);
            Assert.AreEqual(20, caster.Stats.GetOrZero(CharacterStat.Health));
            CollectionAssert.AreEqual(new[] { 302365, 300495 }, w.Session(caster).Messages
                .OfType<CastNanoSpellMessage>().Select(m => m.NanoId).ToArray());
            foreach (var cast in w.Session(caster).Messages.OfType<CastNanoSpellMessage>())
            {
                Assert.AreEqual(caster.Identity, cast.Identity); Assert.AreEqual(caster.Identity, cast.Caster);
                Assert.AreEqual(caster.Identity, cast.Target); Assert.AreEqual(1, cast.Unknown1);
            }
            w.Clock.Advance(19); w.Aura.Tick(caster);
            Assert.AreEqual(20, caster.Stats.GetOrZero(CharacterStat.Health));
            w.Clock.Advance(1); w.Aura.Tick(caster);
            Assert.AreEqual(30, caster.Stats.GetOrZero(CharacterStat.Health));
            w.Clock.Advance(61); w.Aura.Tick(caster);
            Assert.AreEqual(40, caster.Stats.GetOrZero(CharacterStat.Health));
            Assert.AreEqual(6, w.Session(caster).Messages.OfType<CastNanoSpellMessage>().Count());
        }

        [DataTestMethod]
        [DataRow(1, 300495, 10)] [DataRow(49, 300495, 10)]
        [DataRow(50, 300496, 53)] [DataRow(99, 300496, 53)]
        [DataRow(100, 300497, 143)] [DataRow(149, 300497, 143)]
        [DataRow(150, 300498, 243)] [DataRow(220, 300498, 243)]
        public void UsesExactlyOneAcceptedTier(int level, int child, int amount)
        {
            Assert.AreEqual((child, amount), AmbientRestorationNanoSpecialization.ResolveTier(level));
        }

        [TestMethod]
        public void PulseHealsOnlyLivingSamePlayfieldTeamAndStillShowsFullHealthVisual()
        {
            using var w = new World(); Player caster = w.Add(1), member = w.Add(2), outsider = w.Add(3);
            w.Teams.TryHandle(caster, new CharacterActionMessage
                { Identity = caster.Identity, Action = CharacterActionType.TeamRequestInvite, Target = member.Identity });
            w.Teams.TryHandle(member, new CharacterActionMessage
                { Identity = member.Identity, Action = CharacterActionType.ClientTeamInviteReply, Target = caster.Identity, Parameter2 = 1 });
            Assert.IsTrue(w.Teams.AreTeammates(caster, member));
            foreach (Player p in w.Players.Values) w.Session(p).Messages.Clear();
            caster.Stats.Set(CharacterStat.Health, 100);
            w.Aura.Applied(caster, caster, Definition(), null);
            Assert.AreEqual(100, caster.Stats.GetOrZero(CharacterStat.Health));
            Assert.AreEqual(20, member.Stats.GetOrZero(CharacterStat.Health));
            Assert.AreEqual(10, outsider.Stats.GetOrZero(CharacterStat.Health));
            Assert.AreEqual(1, w.Session(caster).Messages.OfType<SpellListMessage>().Count());
            member.Playfield = (Playfield)RuntimeHelpers.GetUninitializedObject(typeof(Playfield));
            w.Clock.Advance(20); w.Aura.Tick(caster);
            Assert.AreEqual(20, member.Stats.GetOrZero(CharacterStat.Health));
        }

        [TestMethod]
        public void ExactVisualFieldsMatchAcceptedSpellList()
        {
            using var w = new World(); Player player = w.Add(7);
            SpellListMessage visual = AmbientRestorationNanoSpecialization.BuildVisual(player);
            Assert.AreEqual(player.Identity, visual.Identity); Assert.AreEqual(player.Identity, visual.Character);
            Assert.AreEqual("Ambient Restoration", visual.NanoName);
            var effect = visual.NanoEffects.Single();
            Assert.AreEqual((IdentityType)0xCF4A, effect.Effect.Type); Assert.AreEqual(302365, effect.Effect.Instance);
            Assert.AreEqual(1, effect.CriterionCount); Assert.AreEqual(0x80, effect.Hits); Assert.AreEqual(0x90, effect.Delay);
            Assert.AreEqual(2, effect.GfxLife); Assert.AreEqual(9, effect.GfxSize); Assert.AreEqual(300495, effect.GfxRed);
            Assert.AreEqual((int)IdentityType.CanbeAffected, effect.GfxGreen); Assert.AreEqual(7, effect.GfxBlue);
        }

        [TestMethod]
        public void RemoveDisconnectQuarantineAndRestoreNeverSynthesizeMorePulses()
        {
            using var w = new World(); Player caster = w.Add(1);
            w.Aura.Applied(caster, caster, Definition(), null);
            w.Aura.Removed(caster, 302365); w.Clock.Advance(20); w.Aura.Tick(caster);
            Assert.AreEqual(20, caster.Stats.GetOrZero(CharacterStat.Health));
            w.Aura.Restored(caster, Definition(), new ActiveNanoRecord(302365, 1, 1, 1, 1));
            w.Aura.Tick(caster); Assert.AreEqual(20, caster.Stats.GetOrZero(CharacterStat.Health));
            w.Aura.Applied(caster, caster, Definition(), null);
            w.Aura.Detached(caster); w.Clock.Advance(20); w.Aura.Tick(caster);
            Assert.AreEqual(30, caster.Stats.GetOrZero(CharacterStat.Health));
            w.Aura.Applied(caster, caster, Definition(), null);
            caster.QuarantinePersistence(); w.Clock.Advance(20); w.Aura.Tick(caster);
            Assert.AreEqual(40, caster.Stats.GetOrZero(CharacterStat.Health));
        }

        sealed class World : IDisposable
        {
            public readonly Dictionary<int, Player> Players = new();
            // This fixture executes all playfield work on its single simulated owner thread.
            public readonly TeamService Teams = new(dispatchOnOwner: (_, action) => action());
            public readonly Clock Clock = new();
            public readonly AmbientRestorationNanoSpecialization Aura;
            readonly Playfield _playfield = (Playfield)RuntimeHelpers.GetUninitializedObject(typeof(Playfield));
            readonly ServiceProvider _services;
            public World()
            {
                _services = new ServiceCollection().AddSingleton(new PlayfieldLocality(4582, null)).BuildServiceProvider();
                typeof(Playfield).GetField("_serviceProvider", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(_playfield, _services);
                var manager = (PlayfieldManager)RuntimeHelpers.GetUninitializedObject(typeof(PlayfieldManager));
                typeof(PlayfieldManager).GetField("_sync", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(manager, new Lock());
                typeof(PlayfieldManager).GetField("_playersByCharacterId", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(manager, Players);
                Aura = new AmbientRestorationNanoSpecialization(Teams, new Lazy<PlayfieldManager>(() => manager)) { Clock = Clock };
            }
            public Player Add(int id)
            {
                Player player = TestWorld.CreatePlayer(id); player.Playfield = _playfield;
                player.Stats.Set(CharacterStat.Health, 10); player.Stats.Set(CharacterStat.MaxHealth, 100);
                player.Stats.Set(CharacterStat.Level, 1);
                var session = new Session(); player.Session = session; session.BindPlayer(player);
                Players.Add(id, player); Teams.AttachPlayer(player); session.Messages.Clear(); return player;
            }
            public Session Session(Player player) => (Session)player.Session!;
            public void Dispose() => _services.Dispose();
        }

        sealed class Clock : TimeProvider
        {
            DateTimeOffset _now = DateTimeOffset.UnixEpoch;
            public override DateTimeOffset GetUtcNow() => _now;
            public void Advance(int seconds) => _now = _now.AddSeconds(seconds);
        }
        sealed class Session : IZoneSession
        {
            public readonly List<MessageBody> Messages = new();
            public SessionState State { get; set; } = SessionState.InPlay;
            public Player? Player { get; private set; }
            public void BindPlayer(Player player) => Player = player;
            public void UnbindPlayer() => Player = null;
            public void TransferToPlayfield(Playfield destination, Vector3 landing) => throw new NotSupportedException();
            public void Send(byte[] packet) => throw new NotSupportedException();
            public void Send(Message message) => throw new NotSupportedException();
            public void Send(MessageBody message) => Messages.Add(message);
            public void Send(MessageBody message, int sender, int receiver) => Messages.Add(message);
            public void SendInitiateCompression() => throw new NotSupportedException();
            public void Close() => State = SessionState.Closed;
        }
    }
}
