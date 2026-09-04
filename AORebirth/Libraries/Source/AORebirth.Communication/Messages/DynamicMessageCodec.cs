namespace AORebirth.Communication.Messages
{
    using System;
    using System.IO;

    using MsgPack;

    /// <summary>
    /// Packs/unpacks <see cref="DynamicMessage"/> via its IPackable/IUnpackable contract.
    /// MsgPack.Cli's reflection serializers try to serialize <see cref="MessageBase"/> by type
    /// (no members) and fail; Mono's IPackable path is the wire format ChatEngine expects.
    /// </summary>
    public static class DynamicMessageCodec
    {
        public static byte[] Pack(DynamicMessage message)
        {
            if (message == null)
            {
                throw new ArgumentNullException("message");
            }

            using (MemoryStream stream = new MemoryStream())
            {
                using (Packer packer = Packer.Create(stream, false))
                {
                    message.PackToMessage(packer, null);
                }

                return stream.ToArray();
            }
        }

        public static DynamicMessage Unpack(byte[] buffer)
        {
            if (buffer == null)
            {
                throw new ArgumentNullException("buffer");
            }

            using (MemoryStream stream = new MemoryStream(buffer))
            {
                using (Unpacker unpacker = Unpacker.Create(stream))
                {
                    if (!unpacker.Read())
                    {
                        throw new InvalidOperationException("Empty DynamicMessage payload.");
                    }

                    DynamicMessage message = new DynamicMessage();
                    message.UnpackFromMessage(unpacker);
                    return message;
                }
            }
        }
    }
}
