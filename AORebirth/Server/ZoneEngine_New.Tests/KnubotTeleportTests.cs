namespace ZoneEngine_New.Tests;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.VisualStudio.TestTools.UnitTesting;

using SmokeLounge.AOtomation.Messaging.GameData;

using ZoneEngine_New.Core.Entities;
using ZoneEngine_New.Core.GameData;
using ZoneEngine_New.Core.Knubot;
using ZoneEngine_New.Core.Network;
using ZoneEngine_New.Core.Playfield;

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
    public void EffectRequiresTheAuthoredSourceAndAvailableDestination()
    {
        string root = Path.Combine(Path.GetTempPath(), "aorebirth-knubot-transport-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            using ServiceProvider services = new ServiceCollection()
                .AddSingleton<IGameData>(new StubGameData(HashItemCatalog.Parse("{}"), rootPath: root))
                .BuildServiceProvider();
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
            Assert.IsFalse(effect.Check(context, new KnubotPlan(), null!), "A missing destination remains unavailable.");
            Directory.CreateDirectory(Path.Combine(root, "Playfields", "901"));
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
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    // This check needs the playfield identity and GameData only. No world thread or zone handoff is started.
    static Playfield TestPlayfield(int id, ServiceProvider services)
    {
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        var playfield = (Playfield)RuntimeHelpers.GetUninitializedObject(typeof(Playfield));
        typeof(Playfield).GetField("<Identity>k__BackingField", flags)!
            .SetValue(playfield, new Identity { Type = IdentityType.Playfield2, Instance = id });
        typeof(Playfield).GetField("_serviceProvider", flags)!.SetValue(playfield, services);
        return playfield;
    }
}
