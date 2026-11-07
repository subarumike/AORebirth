namespace ZoneEngine_New.Core.Missions;

using System;
using System.IO;
using SmokeLounge.AOtomation.Messaging.Messages;
using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;
using SmokeLounge.AOtomation.Messaging.Serialization;
using WireReader = SmokeLounge.AOtomation.Messaging.Serialization.StreamReader;
using WireWriter = SmokeLounge.AOtomation.Messaging.Serialization.StreamWriter;

/// <summary>One codec for frozen DAO offers and accepted packet fixtures. Never constructs game content.</summary>
internal static class GeneratedMissionWire
{
    static readonly SerializerResolver Resolver = new SerializerResolverBuilder<MessageBody>().Build();
    internal static QuestAlternativeMessage Read(byte[] body)
    {
        ArgumentNullException.ThrowIfNull(body);
        if (body.Length == 0) throw new ArgumentException("A frozen mission packet is required.", nameof(body));
        using var stream = new MemoryStream(body, writable: false);
        using var reader = new WireReader(stream);
        var response = (QuestAlternativeMessage)Resolver.GetSerializer(typeof(QuestAlternativeMessage)).Deserialize(reader, new SerializationContext(Resolver));
        // The AO string reader drops terminators; QuestInfo's serializer writes its string literally.
        foreach (var offer in response.QuestInfos ?? [])
            if (offer?.Info is { } text && !text.EndsWith('\0')) offer.Info = text + '\0';
        return response;
    }
    internal static byte[] Write(QuestAlternativeMessage message)
    {
        using var stream = new MemoryStream();
        using var writer = new WireWriter(stream);
        Resolver.GetSerializer(typeof(QuestAlternativeMessage)).Serialize(writer, new SerializationContext(Resolver), message);
        return stream.ToArray();
    }
    /// <summary>The client's now: the server seconds its last GameTime carried plus the real seconds since.</summary>
    internal static int ClientClock(ZoneEngine_New.Core.Network.IGameTimeSession clock, DateTime now)
        => checked(clock.GameTimeServerSeconds + (int)Math.Max(0, (now - (clock.GameTimeSynchronizedAtUtc ?? now)).TotalSeconds));
    internal static int ClientExpiry(ZoneEngine_New.Core.Network.IGameTimeSession clock, DateTime now, DateTime expires)
        => expires <= now ? 0 : checked(ClientClock(clock, now) + (int)Math.Ceiling((expires - now).TotalSeconds));
    internal static string ExpiryField(int value)
    {
        Span<byte> bytes = stackalloc byte[sizeof(int)];
        System.Buffers.Binary.BinaryPrimitives.WriteInt32BigEndian(bytes, value);
        return System.Text.Encoding.Latin1.GetString(bytes);
    }
}
