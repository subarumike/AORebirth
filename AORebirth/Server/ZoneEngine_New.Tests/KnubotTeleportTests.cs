namespace ZoneEngine_New.Tests;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Reflection;
using System.Runtime.CompilerServices;

using AORebirth.Core.GameData;
using Google.Protobuf;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.VisualStudio.TestTools.UnitTesting;

using SmokeLounge.AOtomation.Messaging.GameData;

using ZoneEngine_New.Core.Entities;
using ZoneEngine_New.Core.GameData;
using ZoneEngine_New.Core.Knubot;
using ZoneEngine_New.Core.Network;
using ZoneEngine_New.Core.Playfield;
using ZoneEngine_New.Core.Quests;
using ZoneEngine_New.Core.World;
using ZoneEngine_New.Core.WorldSimulation;

using ArrivalKind = AORebirth.World.Contracts.V1.ArrivalKind;
using TransferEnvelope = AORebirth.World.Contracts.V1.TransferEnvelope;
using Vector3 = AORebirth.Core.Vector.Vector3;

[TestClass]
public sealed class KnubotTeleportTests
{
    const string Directive = "{teleport 800 901 12.5 4.25 6.75 0 0.6 0 0.8}";

    [TestMethod]
    public void DirectiveRetainsTheAuthoredFractionalLandingAndHeading()
    {
        var errors = new List<string>();
        KnubotParser.Parsed parsed = KnubotParser.Parse(Directive, false, null!, errors);
        Assert.AreEqual(0, errors.Count);
        var effect = (KnubotTeleport)parsed.Effects.Single();
        Assert.AreEqual(800, effect.SourceId);
        Assert.AreEqual(901, effect.DestinationId);
        Assert.AreEqual(12.5f, effect.Landing.xf);
        Assert.AreEqual(4.25f, effect.Landing.yf);
        Assert.AreEqual(6.75f, effect.Landing.zf);
        Assert.AreEqual(0.6f, effect.Heading.yf);
        Assert.AreEqual(0.8f, effect.Heading.wf);
    }

    [TestMethod]
    [DataRow("{teleport 800 800 1 2 3 0 0 0 1}")]
    [DataRow("{teleport 800 1441792 1 2 3 0 0 0 1}")]
    [DataRow("{teleport 800 901 NaN 2 3 0 0 0 1}")]
    [DataRow("{teleport 800 901 1 2 3 0 0 Infinity 1}")]
    [DataRow("{teleport 800 901 1 2 3 0 0 0 0}")]
    [DataRow("{teleport 800 901 1 2 3 0 0 0}")]
    public void InvalidDestinationOrTransformCannotBecomeAnEffect(string directive)
    {
        var errors = new List<string>();
        Assert.AreEqual(0, KnubotParser.Parse(directive, false, null!, errors).Effects.Count);
        Assert.IsTrue(errors.Count > 0);
    }

    [TestMethod]
    public void RepliesCannotExecuteTransport()
    {
        var errors = new List<string>();
        Assert.AreEqual(0, KnubotParser.Parse(Directive, true, null!, errors).Effects.Count);
        Assert.IsTrue(errors.Count > 0);
    }

    [TestMethod]
    [DataRow(false, true)]
    [DataRow(true, false)]
    [DataRow(true, true)]
    public void CatalogRequiresKnownPlayfieldReferences(bool hasSource, bool hasDestination)
    {
        DirectoryInfo root = Directory.CreateTempSubdirectory("aorebirth-knubot-transport-");
        try
        {
            var metadata = new Dictionary<int, PlayfieldMetaData>();
            if (hasSource)
                metadata.Add(800, new PlayfieldMetaData { PlayfieldId = 800 });
            if (hasDestination)
                metadata.Add(901, new PlayfieldMetaData { PlayfieldId = 901 });
            var data = new StubGameData(HashItemCatalog.Parse("{}"), rootPath: root.FullName) { PlayfieldMetadata = metadata };
            var logger = new StubLogger();
            var catalog = new KnubotCatalog(data, new QuestCatalog(data, logger), new StubCatalog(), logger);
            string script = $$"""
                {
                  "Npcs": ["TEST"],
                  "Openers": [{ "Say": "go" }],
                  "Lines": {
                    "go": { "Text": "{{Directive}}" }
                  }
                }
                """;
            IReadOnlyList<string> errors = catalog.Check("route", script);
            Assert.AreEqual(hasSource, !errors.Any(error => error.Contains("line 'go': teleport source playfield 800")));
            Assert.AreEqual(hasDestination, !errors.Any(error => error.Contains("line 'go': teleport destination playfield 901")));
            if (hasSource && hasDestination)
                Assert.AreEqual(0, errors.Count);
        }
        finally
        {
            root.Delete();
        }
    }

    [TestMethod]
    public void EffectRequiresTheAuthoredSourceAndCurrentSession()
    {
        using ServiceProvider services = new ServiceCollection().BuildServiceProvider();
        Playfield source = TestPlayfield(800, services);
        Player player = TestWorld.CreatePlayer(1);
        player.Playfield = source;
        player.EnterOnline(new RecordingZoneSession { State = SessionState.InPlay });
        var npc = new NpcCharacter(new Identity { Type = IdentityType.CanbeAffected, Instance = 99 }, new StubItemBuilder())
        {
            Playfield = source,
            Position = player.Position
        };
        npc.Stats.Set(CharacterStat.Health, 100);
        npc.Stats.Set(CharacterStat.MaxHealth, 100);
        var context = new KnubotContext(player, npc, null!, false, null!);
        var errors = new List<string>();
        var effect = (KnubotTeleport)KnubotParser.Parse(Directive, false, null!, errors).Effects.Single();
        Assert.IsTrue(effect.Check(context, new KnubotPlan(), null!));
        Assert.IsFalse((effect with { SourceId = 801 }).Check(context, new KnubotPlan(), null!));
        player.Session!.State = SessionState.Loading;
        Assert.IsFalse(effect.Check(context, new KnubotPlan(), null!), "An existing transfer must retain ownership.");
        player.Session.State = SessionState.InPlay;
        player.Session.UnbindPlayer();
        Assert.IsFalse(effect.Check(context, new KnubotPlan(), null!), "A stale session cannot apply a route.");
        player.Session.BindPlayer(player);
        player.Playfield = TestPlayfield(801, services);
        Assert.IsFalse(effect.Check(context, new KnubotPlan(), null!), "A player who left the NPC cannot use the route.");
        player.Playfield = source;
        player.QuarantinePersistence();
        Assert.IsFalse(effect.Check(context, new KnubotPlan(), null!), "Quarantined characters cannot transfer.");
    }

    [TestMethod]
    public void RefusedLocalTransferKeepsTheReturnDoor()
    {
        using ServiceProvider services = new ServiceCollection().AddSingleton<WorldSimulationAccess>().BuildServiceProvider();
        Playfield source = TestPlayfield(800, services);
        Playfield destination = TestPlayfield(901, services);
        Player player = TestWorld.CreatePlayer(1);
        player.Playfield = source;
        player.Stats.Set(CharacterStat.ExternalPlayfieldInstance, 800);
        player.Stats.Set(CharacterStat.ExternalDoorInstance, 99);
        using var socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        var dispatcher = (ZoneMessageDispatcher)RuntimeHelpers.GetUninitializedObject(typeof(ZoneMessageDispatcher));
        var session = new ZoneSession(Guid.NewGuid(), socket, new ZoneMessageCodec(), dispatcher, new StubLogger());
        try
        {
            player.EnterOnline(session);
            session.State = SessionState.InPlay;
            Assert.IsFalse(session.TransferToPlayfield(destination, new Vector3(1, 2, 3), player.Rotation,
                stillAuthorized: null, clearReturnDoor: true));
            Assert.AreSame(source, player.Playfield);
            Assert.AreEqual(SessionState.InPlay, session.State);
            Assert.AreEqual(800, player.Stats.GetOrZero(CharacterStat.ExternalPlayfieldInstance));
            Assert.AreEqual(99, player.Stats.GetOrZero(CharacterStat.ExternalDoorInstance));
        }
        finally
        {
            session.UnbindPlayer();
            session.Close();
        }
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void RemoteTransferCarriesReturnDoorPolicyWithoutChangingTheSource(bool clearReturnDoor)
    {
        Player player = TestWorld.CreatePlayer(1);
        player.Stats.Set(CharacterStat.ExternalPlayfieldInstance, 800);
        player.Stats.Set(CharacterStat.ExternalDoorInstance, 99);
        TransferEnvelope envelope = CrossInstanceTransfer.BuildEnvelope(player, 901, new Vector3(1, 2, 3), player.Rotation,
            ArrivalKind.ArrivalTeleport, exitProxyDoor: 0, clearReturnDoor: clearReturnDoor);
        Assert.AreEqual(clearReturnDoor, TransferEnvelope.Parser.ParseFrom(envelope.ToByteArray()).ClearReturnDoor);
        Assert.AreEqual(800, player.Stats.GetOrZero(CharacterStat.ExternalPlayfieldInstance));
        Assert.AreEqual(99, player.Stats.GetOrZero(CharacterStat.ExternalDoorInstance));
    }

    // The checks use playfield identity and refuse collision-free fixtures before scheduling any handoff.
    static Playfield TestPlayfield(int id, ServiceProvider services)
    {
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        var playfield = (Playfield)RuntimeHelpers.GetUninitializedObject(typeof(Playfield));
        typeof(Playfield).GetField("<Identity>k__BackingField", flags)!
            .SetValue(playfield, new Identity { Type = IdentityType.Playfield2, Instance = id });
        typeof(Playfield).GetField("_serviceProvider", flags)!.SetValue(playfield, services);
        typeof(Playfield).GetField("_logger", flags)!.SetValue(playfield, new StubLogger());
        return playfield;
    }
}
