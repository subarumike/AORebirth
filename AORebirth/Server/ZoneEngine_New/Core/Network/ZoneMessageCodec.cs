namespace ZoneEngine_New.Core.Network
{
    using System;
    using System.IO;

    using SmokeLounge.AOtomation.Messaging.Messages;
    using SmokeLounge.AOtomation.Messaging.Serialization;

    using MessagingStreamWriter = SmokeLounge.AOtomation.Messaging.Serialization.StreamWriter;

    /// <summary>
    /// Thin wrapper around AOtomation MessageSerializer.
    /// </summary>
    public sealed class ZoneMessageCodec
    {
        private readonly MessageSerializer _serializer = new MessageSerializer();
        private readonly SerializerResolver _bodyResolver = new SerializerResolverBuilder<MessageBody>().Build();
        private readonly SmokeLounge.AOtomation.Messaging.Serialization.Serializers.HeaderSerializer _headerSerializer =
            new SmokeLounge.AOtomation.Messaging.Serialization.Serializers.HeaderSerializer();

        public Message? Deserialize(byte[] buffer)
        {
            ArgumentNullException.ThrowIfNull(buffer);

            using MemoryStream stream = new MemoryStream(buffer);
            return _serializer.Deserialize(stream);
        }

        public byte[] Serialize(Message message)
        {
            ArgumentNullException.ThrowIfNull(message);

            using MemoryStream stream = new MemoryStream();
            _serializer.Serialize(stream, message);
            return stream.ToArray();
        }

        public byte[] Serialize(MessageBody body, int sender, int receiver)
        {
            ArgumentNullException.ThrowIfNull(body);

            Message message = new Message
            {
                Body = body,
                Header = new Header
                {
                    MessageId = 0xDFDF,
                    PacketType = body.PacketType,
                    Unknown = 0x0001,
                    Sender = sender,
                    Receiver = receiver
                }
            };

            return Serialize(message);
        }
    }
}
