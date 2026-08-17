namespace AORebirth.BotService
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Net;
    using System.Net.Sockets;
    using System.Security.Cryptography;
    using System.Text;
    using System.Threading;

    public sealed class BotPrivateTcpClient : IBotChatGateway
    {
        private readonly IPEndPoint endpoint;
        private readonly byte[] serviceKey;

        public BotPrivateTcpClient(IPEndPoint endpoint, byte[] serviceKey)
        {
            BotPrivateProtocol.ValidateLoopbackEndpoint(endpoint);
            BotPrivateProtocol.ValidateServiceKey(serviceKey);
            this.endpoint = endpoint;
            this.serviceKey = (byte[])serviceKey.Clone();
        }

        public BotOperationResult Execute(BotSession session, BotChatRequest request)
        {
            byte[] payload = BotPrivateProtocol.SerializeRequest(
                Guid.NewGuid(),
                DateTime.UtcNow,
                session,
                request);
            using (TcpClient client = new TcpClient(AddressFamily.InterNetwork))
            {
                client.ReceiveTimeout = 5000;
                client.SendTimeout = 5000;
                client.Connect(this.endpoint);
                using (NetworkStream stream = client.GetStream())
                {
                    BotPrivateProtocol.WriteAuthenticatedFrame(stream, payload, this.serviceKey);
                    byte[] response = BotPrivateProtocol.ReadAuthenticatedFrame(stream, this.serviceKey);
                    return BotPrivateProtocol.DeserializeResponse(response);
                }
            }
        }
    }

    public sealed class BotPrivateTcpServer : IDisposable
    {
        private readonly IPEndPoint endpoint;
        private readonly byte[] serviceKey;
        private readonly IBotChatRequestHandler handler;
        private readonly object replaySync = new object();
        private readonly Dictionary<Guid, DateTime> replayWindow = new Dictionary<Guid, DateTime>();
        private TcpListener listener;
        private Thread listenerThread;
        private volatile bool running;

        public BotPrivateTcpServer(IPEndPoint endpoint, byte[] serviceKey, IBotChatRequestHandler handler)
        {
            BotPrivateProtocol.ValidateLoopbackEndpoint(endpoint);
            BotPrivateProtocol.ValidateServiceKey(serviceKey);
            this.endpoint = endpoint;
            this.serviceKey = (byte[])serviceKey.Clone();
            this.handler = handler ?? throw new ArgumentNullException("handler");
        }

        public IPEndPoint BoundEndpoint { get; private set; }

        public bool IsRunning
        {
            get { return this.running; }
        }

        public void Start()
        {
            if (this.running)
            {
                throw new InvalidOperationException("The private bot listener is already running.");
            }

            this.listener = new TcpListener(this.endpoint);
            this.listener.Start(16);
            this.BoundEndpoint = (IPEndPoint)this.listener.LocalEndpoint;
            this.running = true;
            this.listenerThread = new Thread(this.AcceptLoop)
            {
                IsBackground = true,
                Name = "AORebirth BotService private listener"
            };
            this.listenerThread.Start();
        }

        public void Stop()
        {
            this.running = false;
            if (this.listener != null)
            {
                this.listener.Stop();
            }

            if (this.listenerThread != null && this.listenerThread != Thread.CurrentThread)
            {
                this.listenerThread.Join(2000);
            }

            this.listenerThread = null;
            this.listener = null;
        }

        public void Dispose()
        {
            this.Stop();
        }

        private void AcceptLoop()
        {
            while (this.running)
            {
                try
                {
                    TcpClient client = this.listener.AcceptTcpClient();
                    ThreadPool.QueueUserWorkItem(this.HandleClient, client);
                }
                catch (SocketException)
                {
                    if (this.running)
                    {
                        this.running = false;
                    }
                }
                catch (ObjectDisposedException)
                {
                    return;
                }
            }
        }

        private void HandleClient(object state)
        {
            using (TcpClient client = (TcpClient)state)
            {
                client.ReceiveTimeout = 5000;
                client.SendTimeout = 5000;
                try
                {
                    using (NetworkStream stream = client.GetStream())
                    {
                        byte[] frame = BotPrivateProtocol.ReadAuthenticatedFrame(stream, this.serviceKey);
                        BotPrivateProtocol.RequestEnvelope envelope = BotPrivateProtocol.DeserializeRequest(frame);
                        BotOperationResult replayResult = this.AcceptRequest(envelope.RequestId, envelope.TimestampUtc);
                        BotOperationResult result = replayResult
                            ?? this.handler.Handle(envelope.Session, envelope.Request)
                            ?? BotOperationResult.Denied("CHATENGINE_HANDLER_EMPTY");
                        BotPrivateProtocol.WriteAuthenticatedFrame(
                            stream,
                            BotPrivateProtocol.SerializeResponse(result),
                            this.serviceKey);
                    }
                }
                catch
                {
                    // The boundary fails closed. No request, credential, key, or payload is logged here.
                }
            }
        }

        private BotOperationResult AcceptRequest(Guid requestId, DateTime timestampUtc)
        {
            DateTime now = DateTime.UtcNow;
            if (timestampUtc < now.AddMinutes(-2) || timestampUtc > now.AddMinutes(2))
            {
                return BotOperationResult.Denied("PRIVATE_REQUEST_STALE");
            }

            lock (this.replaySync)
            {
                List<Guid> expired = new List<Guid>();
                foreach (KeyValuePair<Guid, DateTime> observation in this.replayWindow)
                {
                    if (observation.Value < now.AddMinutes(-2))
                    {
                        expired.Add(observation.Key);
                    }
                }

                foreach (Guid expiredId in expired)
                {
                    this.replayWindow.Remove(expiredId);
                }

                if (this.replayWindow.ContainsKey(requestId))
                {
                    return BotOperationResult.Denied("PRIVATE_REQUEST_REPLAYED");
                }

                this.replayWindow[requestId] = now;
                return null;
            }
        }
    }

    internal static class BotPrivateProtocol
    {
        private const int Magic = 0x414F5242;
        private const int Version = 1;
        private const int MaximumPayloadLength = 65536;
        private const int MacLength = 32;

        internal sealed class RequestEnvelope
        {
            public Guid RequestId { get; set; }

            public DateTime TimestampUtc { get; set; }

            public BotSession Session { get; set; }

            public BotChatRequest Request { get; set; }
        }

        public static void ValidateLoopbackEndpoint(IPEndPoint endpoint)
        {
            if (endpoint == null || !IPAddress.IsLoopback(endpoint.Address))
            {
                throw new ArgumentException("The private BotService endpoint must be loopback-only.", "endpoint");
            }
        }

        public static void ValidateServiceKey(byte[] serviceKey)
        {
            if (serviceKey == null || serviceKey.Length < 32)
            {
                throw new ArgumentException("The private service key must contain at least 256 bits.", "serviceKey");
            }
        }

        public static byte[] SerializeRequest(
            Guid requestId,
            DateTime timestampUtc,
            BotSession session,
            BotChatRequest request)
        {
            if (session == null || request == null)
            {
                throw new ArgumentNullException(session == null ? "session" : "request");
            }

            using (MemoryStream stream = new MemoryStream())
            using (BinaryWriter writer = new BinaryWriter(stream, Encoding.UTF8))
            {
                writer.Write(requestId.ToByteArray());
                writer.Write(timestampUtc.ToUniversalTime().Ticks);
                WriteGuid(writer, session.SessionId);
                WriteGuid(writer, session.BotId);
                WriteBoundedString(writer, session.DisplayName, 64);
                writer.Write(session.OwningAccountId);
                WriteNullableInt64(writer, session.OrganizationId);
                WriteBoundedString(writer, session.PublicCredentialId, 64);
                writer.Write(session.CredentialVersion);
                writer.Write((long)session.GrantedScopes);
                WriteBoundedString(writer, session.RateLimitProfile, 64);
                WriteBoundedString(writer, session.AuditIdentity, 128);
                writer.Write(session.CreatedAtUtc.ToUniversalTime().Ticks);
                writer.Write(session.EnabledSnapshot);
                writer.Write((int)request.Operation);
                writer.Write(request.TargetCharacterId);
                writer.Write(request.ChannelType);
                writer.Write(request.ChannelId);
                WriteNullableInt64(writer, request.OrganizationId);
                WriteBoundedString(writer, request.Text, 4096);
                writer.Flush();
                return stream.ToArray();
            }
        }

        public static RequestEnvelope DeserializeRequest(byte[] payload)
        {
            using (MemoryStream stream = new MemoryStream(payload, false))
            using (BinaryReader reader = new BinaryReader(stream, Encoding.UTF8))
            {
                Guid requestId = ReadGuid(reader);
                DateTime timestampUtc = new DateTime(reader.ReadInt64(), DateTimeKind.Utc);
                BotSession session = new BotSession
                {
                    SessionId = ReadGuid(reader),
                    BotId = ReadGuid(reader),
                    DisplayName = ReadBoundedString(reader, 64),
                    OwningAccountId = reader.ReadInt64(),
                    OrganizationId = ReadNullableInt64(reader),
                    PublicCredentialId = ReadBoundedString(reader, 64),
                    CredentialVersion = reader.ReadInt32(),
                    GrantedScopes = (BotScope)reader.ReadInt64(),
                    RateLimitProfile = ReadBoundedString(reader, 64),
                    AuditIdentity = ReadBoundedString(reader, 128),
                    CreatedAtUtc = new DateTime(reader.ReadInt64(), DateTimeKind.Utc),
                    EnabledSnapshot = reader.ReadBoolean()
                };
                BotChatRequest request = new BotChatRequest
                {
                    Operation = (BotOperation)reader.ReadInt32(),
                    TargetCharacterId = reader.ReadUInt32(),
                    ChannelType = reader.ReadByte(),
                    ChannelId = reader.ReadUInt32(),
                    OrganizationId = ReadNullableInt64(reader),
                    Text = ReadBoundedString(reader, 4096)
                };
                if (stream.Position != stream.Length)
                {
                    throw new InvalidDataException("The private bot request has trailing data.");
                }

                return new RequestEnvelope
                {
                    RequestId = requestId,
                    TimestampUtc = timestampUtc,
                    Session = session,
                    Request = request
                };
            }
        }

        public static byte[] SerializeResponse(BotOperationResult response)
        {
            using (MemoryStream stream = new MemoryStream())
            using (BinaryWriter writer = new BinaryWriter(stream, Encoding.UTF8))
            {
                writer.Write(response.Succeeded);
                WriteBoundedString(writer, response.ReasonCode, 128);
                WriteBoundedString(writer, response.Detail, 1024);
                writer.Flush();
                return stream.ToArray();
            }
        }

        public static BotOperationResult DeserializeResponse(byte[] payload)
        {
            using (MemoryStream stream = new MemoryStream(payload, false))
            using (BinaryReader reader = new BinaryReader(stream, Encoding.UTF8))
            {
                BotOperationResult result = new BotOperationResult
                {
                    Succeeded = reader.ReadBoolean(),
                    ReasonCode = ReadBoundedString(reader, 128),
                    Detail = ReadBoundedString(reader, 1024)
                };
                if (stream.Position != stream.Length)
                {
                    throw new InvalidDataException("The private bot response has trailing data.");
                }

                return result;
            }
        }

        public static void WriteAuthenticatedFrame(Stream stream, byte[] payload, byte[] key)
        {
            if (payload == null || payload.Length > MaximumPayloadLength)
            {
                throw new InvalidDataException("The private bot payload is invalid.");
            }

            byte[] mac;
            using (HMACSHA256 hmac = new HMACSHA256(key))
            {
                mac = hmac.ComputeHash(payload);
            }

            using (BinaryWriter writer = new BinaryWriter(stream, Encoding.UTF8, true))
            {
                writer.Write(Magic);
                writer.Write(Version);
                writer.Write(payload.Length);
                writer.Write(payload);
                writer.Write(mac.Length);
                writer.Write(mac);
                writer.Flush();
            }
        }

        public static byte[] ReadAuthenticatedFrame(Stream stream, byte[] key)
        {
            using (BinaryReader reader = new BinaryReader(stream, Encoding.UTF8, true))
            {
                if (reader.ReadInt32() != Magic || reader.ReadInt32() != Version)
                {
                    throw new InvalidDataException("The private bot protocol header is invalid.");
                }

                int payloadLength = reader.ReadInt32();
                if (payloadLength < 0 || payloadLength > MaximumPayloadLength)
                {
                    throw new InvalidDataException("The private bot payload length is invalid.");
                }

                byte[] payload = ReadExact(reader, payloadLength);
                int macLength = reader.ReadInt32();
                if (macLength != MacLength)
                {
                    throw new InvalidDataException("The private bot message authenticator is invalid.");
                }

                byte[] suppliedMac = ReadExact(reader, macLength);
                byte[] expectedMac;
                using (HMACSHA256 hmac = new HMACSHA256(key))
                {
                    expectedMac = hmac.ComputeHash(payload);
                }

                if (!BotCredentialManager.FixedTimeEquals(suppliedMac, expectedMac))
                {
                    throw new InvalidDataException("The private bot message authentication failed.");
                }

                return payload;
            }
        }

        private static void WriteGuid(BinaryWriter writer, Guid value)
        {
            writer.Write(value.ToByteArray());
        }

        private static Guid ReadGuid(BinaryReader reader)
        {
            return new Guid(ReadExact(reader, 16));
        }

        private static void WriteNullableInt64(BinaryWriter writer, long? value)
        {
            writer.Write(value.HasValue);
            if (value.HasValue)
            {
                writer.Write(value.Value);
            }
        }

        private static long? ReadNullableInt64(BinaryReader reader)
        {
            return reader.ReadBoolean() ? (long?)reader.ReadInt64() : null;
        }

        private static void WriteBoundedString(BinaryWriter writer, string value, int maximumCharacters)
        {
            value = value ?? string.Empty;
            if (value.Length > maximumCharacters)
            {
                throw new InvalidDataException("A private bot string exceeds its protocol limit.");
            }

            writer.Write(value);
        }

        private static string ReadBoundedString(BinaryReader reader, int maximumCharacters)
        {
            string value = reader.ReadString();
            if (value.Length > maximumCharacters)
            {
                throw new InvalidDataException("A private bot string exceeds its protocol limit.");
            }

            return value;
        }

        private static byte[] ReadExact(BinaryReader reader, int length)
        {
            byte[] bytes = reader.ReadBytes(length);
            if (bytes.Length != length)
            {
                throw new EndOfStreamException("The private bot frame ended early.");
            }

            return bytes;
        }
    }
}
