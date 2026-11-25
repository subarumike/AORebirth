namespace ZoneEngine_New.Tests;

using System.IO;
using System.Linq;

using Microsoft.VisualStudio.TestTools.UnitTesting;
using SmokeLounge.AOtomation.Messaging.GameData;
using SmokeLounge.AOtomation.Messaging.Messages;
using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;
using SmokeLounge.AOtomation.Messaging.Serialization;
using SmokeLounge.AOtomation.Messaging.Serialization.Serializers.Custom;
using ZoneEngine_New.Core.Entities;
using ZoneEngine_New.Core.MessageHandlers;

[TestClass]
public sealed class BossSpawnHealthTests
{
    [TestMethod]
    [DataRow(65534, 0)]
    [DataRow(65534, 256)]
    [DataRow(65535, 0)]
    [DataRow(65535, 255)]
    [DataRow(65536, 0)]
    [DataRow(65536, 256)]
    [DataRow(100000, 0)]
    [DataRow(100000, 14099)]
    [DataRow(200000, 0)]
    [DataRow(200000, 91231)]
    public void SpawnAndInspectionAgreeOnFullBossVitals(int maximum, int missing)
    {
        using var world = new TriggerTestWorld();
        var boss = new NpcCharacter(new Identity { Type = IdentityType.CanbeAffected, Instance = 96381 }, new StubItemBuilder())
        {
            Name = "Fixture Boss",
            Playfield = world.Playfield
        };
        boss.Stats.Set(CharacterStat.Level, 220);
        boss.Stats.Set(CharacterStat.MaxHealth, maximum);
        boss.Stats.Set(CharacterStat.Health, maximum - missing);
        world.Registry.Register(boss);
        world.Locality.RegisterDynel(boss);
        world.Locality.ActivatePlayerVisibility(world.First);

        SimpleCharFullUpdateMessage sent = world.FirstSession.Sent.OfType<SimpleCharFullUpdateMessage>()
            .Single(packet => packet.Identity == boss.Identity);
        var serialization = new SerializationContext(new SerializerResolverBuilder<MessageBody>().Build());
        var serializer = new SimpleCharFullUpdateSerializer();
        using var stream = new MemoryStream();
        using var writer = new SmokeLounge.AOtomation.Messaging.Serialization.StreamWriter(stream);
        const int headerLength = 16;
        writer.Position = headerLength;
        serializer.Serialize(writer, serialization, sent);
        using var reader = new SmokeLounge.AOtomation.Messaging.Serialization.StreamReader(stream);
        reader.Position = headerLength;
        var decoded = (SimpleCharFullUpdateMessage)serializer.Deserialize(reader, serialization);

        Assert.AreEqual(maximum, decoded.Health);
        Assert.AreEqual(missing, decoded.HealthDamage);
        Assert.AreEqual(maximum <= ushort.MaxValue, decoded.Flags.HasFlag(SimpleCharFullUpdateFlags.HasSmallHealth));
        Assert.AreEqual(missing <= byte.MaxValue, decoded.Flags.HasFlag(SimpleCharFullUpdateFlags.HasSmallHealthDamage));

        new LookAtMessageHandler().Handle(new LookAtMessage { Target = boss.Identity }, world.FirstSession);
        var inspection = (CharacterInfoPacket)world.FirstSession.Sent.OfType<InfoPacketMessage>().Last().Info;
        Assert.AreEqual(decoded.Health, inspection.MaxHealth);
        Assert.AreEqual(decoded.Health - decoded.HealthDamage, inspection.Health);
    }
}
