using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Threading;

using AORebirth.Core.Components;
using AORebirth.Core.EventHandlers.Events;

using Cell.Core;

using LoginEngine.Component;
using LoginEngine.CoreClient;
using LoginEngine.CoreServer;
using LoginEngine.MessageHandlers;
using LoginEngine.Packets;

using SmokeLounge.AOtomation.Messaging.GameData;
using SmokeLounge.AOtomation.Messaging.Messages;
using SmokeLounge.AOtomation.Messaging.Messages.SystemMessages;

internal static class Stage7RuntimeFixtures
{
    internal static string Create()
    {
        var lines = new List<string>();
        VerifyInertTopology(lines);
        VerifyClientFramingAndReceive(lines);
        AddSerializerGoldens(lines);
        VerifySafeHandlers(lines);
        VerifyActiveDispatch(lines);
        return Normalize(string.Join("\n", lines) + "\n");
    }

    private static void VerifyInertTopology(ICollection<string> lines)
    {
        var serializer = new RecordingSerializer();
        var bus = new RecordingBus();
        var factory = new ClientFactory(serializer, bus);
        var server = new LoginServer(factory);
        Client client = null;
        try
        {
            Assert(!server.IsRunning, "LoginServer constructor started the server.");
            Assert(server.ClientCount == 0, "LoginServer constructor created clients.");
            Assert(server.TcpEndPoint == null, "LoginServer constructor assigned a TCP endpoint.");
            Assert(server.UdpEndPoint == null, "LoginServer constructor assigned a UDP endpoint.");
            Assert(server.MaximumPendingConnections == 100, "LoginServer default pending-connection limit changed.");
            Assert(GetInheritedField(server.GetType(), "_tcpListen").GetValue(server) == null, "LoginServer constructor created a TCP listener.");
            Assert(GetInheritedField(server.GetType(), "_udpListen").GetValue(server) == null, "LoginServer constructor created a UDP listener.");

            client = factory.Create(server);
            Assert(object.ReferenceEquals(client.Server, server), "ClientFactory returned a client bound to the wrong server.");
            Assert(!client.IsConnected, "ClientFactory returned a connected client.");
            Assert(client.ClientAddress == null, "Unconnected LoginEngine client has an address.");
            Assert(client.Port == -1, "Unconnected LoginEngine client has a port.");
            Assert(string.Equals(client.AccountName, string.Empty, StringComparison.Ordinal), "Client account default changed.");
            Assert(string.Equals(client.ClientVersion, string.Empty, StringComparison.Ordinal), "Client version default changed.");
            Assert(string.Equals(client.ServerSalt, string.Empty, StringComparison.Ordinal), "Client salt default changed.");
            AddLine(lines, "runtime.topology", "running=false", "clients=0", "tcp-endpoint=null", "udp-endpoint=null", "tcp-listener=null", "udp-listener=null", "pending=100");
        }
        finally
        {
            if (client != null) client.Dispose();
            server.Dispose();
        }
    }

    private static void VerifyClientFramingAndReceive(ICollection<string> lines)
    {
        var serializer = new RecordingSerializer
        {
            SerializedBytes = new byte[] { 0xAA, 0xBB, 0xCC, 0xDD, 0xEE }
        };
        var bus = new RecordingBus();
        var factory = new ClientFactory(serializer, bus);
        var server = new LoginServer(factory);
        var client = new CaptureClient(server, serializer, bus);
        try
        {
            var body = new SuggestNameMessage { Name = "StageSeven" };
            client.Send(0x10203040, body);
            client.Send(0x10203040, body);
            Assert(client.SentPackets.Count == 2, "Client did not emit two framed packets.");
            byte[] firstExpected = { 0x01, 0x00, 0xCC, 0xDD, 0xEE, 0x00, 0x00, 0x00 };
            byte[] secondExpected = { 0x02, 0x00, 0xCC, 0xDD, 0xEE, 0x00, 0x00, 0x00 };
            Assert(client.SentPackets[0].SequenceEqual(firstExpected), "Client first packet sequence/padding contract changed.");
            Assert(client.SentPackets[1].SequenceEqual(secondExpected), "Client second packet sequence/padding contract changed.");
            Assert(serializer.SerializedMessages.Count == 2, "Client did not serialize two message envelopes.");
            foreach (Message envelope in serializer.SerializedMessages)
            {
                Assert(object.ReferenceEquals(envelope.Body, body), "Client replaced the outbound message body.");
                Assert(envelope.Header.MessageId == 0xDFDF, "Client message id changed.");
                Assert(envelope.Header.PacketType == body.PacketType, "Client packet type changed.");
                Assert(envelope.Header.Unknown == 1, "Client header unknown field changed.");
                Assert(envelope.Header.Sender == 1, "Client header sender changed.");
                Assert(envelope.Header.Receiver == 0x10203040, "Client header receiver changed.");
            }

            byte[] numberFixture = new byte[20];
            numberFixture[16] = 0x12;
            numberFixture[17] = 0x34;
            numberFixture[18] = 0x56;
            numberFixture[19] = 0x78;
            Assert(client.ReadMessageNumber(numberFixture) == 0x12345678u, "Client byte-array message-number parsing changed.");
            BufferSegment numberSegment = BufferSegment.CreateSegment(numberFixture);
            Assert(client.ReadMessageNumber(numberSegment) == 0x12345678u, "Client BufferSegment message-number parsing changed.");

            var receivedMessage = new Message
            {
                Header = CreateHeader(new RandomNameRequestMessage(), 0x11111111),
                Body = new RandomNameRequestMessage { Profession = Profession.Soldier }
            };
            serializer.DeserializedMessage = receivedMessage;
            serializer.ThrowOnDeserialize = false;
            BufferSegment validSegment = BufferSegment.CreateSegment(numberFixture);
            Assert(client.Receive(validSegment, numberFixture.Length), "Client rejected a valid deserialized message.");
            Assert(validSegment.Uses == 1, "Client did not retain a valid receive segment.");
            Assert(bus.Published.Count == 1, "Client did not publish exactly one receive event.");
            var receivedEvent = bus.Published[0] as MessageReceivedEvent;
            Assert(receivedEvent != null, "Client published the wrong receive-event type.");
            Assert(object.ReferenceEquals(receivedEvent.Sender, client), "Receive event sender changed.");
            Assert(object.ReferenceEquals(receivedEvent.Message, receivedMessage), "Receive event message changed.");
            validSegment.DecrementUsage();

            bus.Published.Clear();
            serializer.DeserializedMessage = null;
            BufferSegment unknownSegment = BufferSegment.CreateSegment(numberFixture);
            Assert(!client.Receive(unknownSegment, numberFixture.Length), "Client accepted a null deserialization result.");
            Assert(unknownSegment.Uses == 1 && bus.Published.Count == 0, "Null deserialization usage/publish behavior changed.");
            unknownSegment.DecrementUsage();

            serializer.ThrowOnDeserialize = true;
            BufferSegment malformedSegment = BufferSegment.CreateSegment(numberFixture);
            Assert(!client.Receive(malformedSegment, numberFixture.Length), "Client accepted a malformed serialized message.");
            Assert(malformedSegment.Uses == 0 && bus.Published.Count == 0, "Malformed deserialization retained/published a packet.");

            AddLine(lines, "runtime.client-packet", "first", firstExpected.Length, ComputeSha256(firstExpected), Convert.ToBase64String(firstExpected));
            AddLine(lines, "runtime.client-packet", "second", secondExpected.Length, ComputeSha256(secondExpected), Convert.ToBase64String(secondExpected));
            AddLine(lines, "runtime.client-receive", "message-number=0x12345678", "valid=retain+publish", "null=retain+reject", "malformed=reject");
        }
        finally
        {
            client.Dispose();
            server.Dispose();
        }
    }

    private static void AddSerializerGoldens(ICollection<string> lines)
    {
        var serializer = new MessageSerializer();
        foreach (KeyValuePair<string, MessageBody> fixture in CreateWireBodies())
        {
            Message message = new Message
            {
                Header = CreateHeader(fixture.Value, unchecked((int)0xA1B2C3D4)),
                Body = fixture.Value
            };
            byte[] bytes = serializer.Serialize(message);
            Assert(bytes != null && bytes.Length >= 20, "Serializer returned an invalid " + fixture.Key + " packet.");
            Message roundTrip = serializer.Deserialize(bytes);
            Assert(roundTrip != null && roundTrip.Body != null, "Serializer could not deserialize its " + fixture.Key + " packet.");
            Assert(string.Equals(roundTrip.Body.GetType().FullName, fixture.Value.GetType().FullName, StringComparison.Ordinal), "Serializer changed the " + fixture.Key + " body type.");
            AddLine(lines, "runtime.wire", fixture.Key, bytes.Length, ComputeSha256(bytes), Convert.ToBase64String(bytes));
        }
    }

    private static IEnumerable<KeyValuePair<string, MessageBody>> CreateWireBodies()
    {
        byte[] salt = Enumerable.Range(1, 32).Select(value => (byte)value).ToArray();
        byte[] creationPrefix = Enumerable.Range(1, 49).Select(value => (byte)value).ToArray();
        var character = new LoginCharacterInfo
        {
            Unknown1 = 4,
            Id = 0x10203040,
            PlayfieldProxyVersion = 0x61,
            PlayfieldId = new Identity { Type = IdentityType.Playfield, Instance = 6553 },
            PlayfieldAttribute = 1,
            ExitDoor = 0,
            ExitDoorId = Identity.None,
            Unknown2 = 1,
            CharacterInfoVersion = 5,
            CharacterId = 0x10203040,
            Name = "StageSeven",
            Breed = Breed.Solitus,
            Gender = Gender.Female,
            Profession = Profession.Soldier,
            Level = 42,
            AreaName = "Arete",
            Status = CharacterStatus.Active
        };

        return new[]
        {
            Fixture("character-created", new CharacterCreatedMessage { CharacterId = 0x10203040 }),
            Fixture("character-deleted", new CharacterDeletedMessage { CharacterId = 0x10203040 }),
            Fixture("character-list", new CharacterListMessage { Characters = new[] { character }, AllowedCharacters = 8, Expansions = 2047 }),
            Fixture("create-character", new CreateCharacterMessage
            {
                Unknown1 = creationPrefix,
                Name = "StageSeven",
                Breed = Breed.Opifex,
                Gender = Gender.Female,
                Profession = Profession.Fixer,
                Level = 1,
                AreaName = "Rubi-Ka",
                Unknown2 = 7,
                Unknown3 = 9,
                HeadMesh = 40123,
                MonsterScale = 100,
                Fatness = Fatness.Normal,
                StarterArea = StarterArea.RubiKa
            }),
            Fixture("delete-character", new DeleteCharacterMessage { CharacterId = 0x10203040 }),
            Fixture("login-error", new LoginErrorMessage { Error = LoginError.InvalidUserNamePassword }),
            Fixture("name-in-use", new NameInUseMessage()),
            Fixture("random-name-request", new RandomNameRequestMessage { Profession = Profession.Engineer }),
            Fixture("select-character", new SelectCharacterMessage { CharacterId = 0x10203040 }),
            Fixture("server-salt", new ServerSaltMessage { ServerSalt = salt }),
            Fixture("suggest-name", new SuggestNameMessage { Name = "StageSeven" }),
            Fixture("user-credentials", new UserCredentialsMessage { UserName = "Stage7User", Credentials = "fixture-key|\u03bc\n" }),
            Fixture("user-login", new UserLoginMessage { UserName = "Stage7User", ClientVersion = "18.8.53_EP1" }),
            Fixture("zone-info", new ZoneInfoMessage
            {
                CharacterId = 0x10203040,
                ServerIpAddress = IPAddress.Parse("127.0.0.1"),
                ServerPort = 7501,
                Cookie1 = 0x11223344,
                Cookie2 = 0x55667788
            })
        };
    }

    private static void VerifySafeHandlers(ICollection<string> lines)
    {
        var serializer = new RecordingSerializer { SerializedBytes = new byte[] { 0xAA, 0xBB, 0xCC, 0xDD, 0xEE } };
        var bus = new RecordingBus();
        var factory = new ClientFactory(serializer, bus);
        var server = new LoginServer(factory);
        var client = new CaptureClient(server, serializer, bus);
        try
        {
            var randomHandler = new RandomNameRequestHandler();
            randomHandler.Handle(
                client,
                new Message
                {
                    Header = CreateHeader(new RandomNameRequestMessage(), 1),
                    Body = new RandomNameRequestMessage { Profession = Profession.Soldier }
                });
            Assert(serializer.SerializedMessages.Count == 1, "Random-name handler did not emit one response.");
            Assert(serializer.SerializedMessages[0].Header.Receiver == 0x0000FFFF, "Random-name response receiver changed.");
            var suggestion = serializer.SerializedMessages[0].Body as SuggestNameMessage;
            Assert(suggestion != null, "Random-name handler emitted the wrong response body.");
            VerifyRandomName(suggestion.Name);

            var generator = new CharacterName();
            for (int index = 0; index < 32; index++)
            {
                VerifyRandomName(generator.GetRandomName(Profession.Soldier));
            }

            AddLine(lines, "runtime.safe-handler", "random-name", "receiver=0x0000FFFF", "shape=verified", "exact-random-bytes=excluded");
        }
        finally
        {
            client.Dispose();
            server.Dispose();
        }
    }

    private static void VerifyActiveDispatch(ICollection<string> lines)
    {
        var serializer = new RecordingSerializer { SerializedBytes = new byte[] { 0xAA, 0xBB, 0xCC, 0xDD, 0xEE } };
        var passiveBus = new RecordingBus();
        var factory = new ClientFactory(serializer, passiveBus);
        var server = new LoginServer(factory);
        var client = new CaptureClient(server, serializer, passiveBus);
        try
        {
            var container = new MefContainer();
            IBus activeBus = container.GetInstance<IBus>();
            Assert(activeBus != null, "MEF did not compose an active IBus.");
            VerifyConcurrentPublish(activeBus);

            serializer.SerializedMessages.Clear();
            client.ResetSendSignal();
            var loginBody = new UserLoginMessage { UserName = "Stage7User", ClientVersion = "18.8.53_EP1" };
            var receivedEvent = new MessageReceivedEvent(
                client,
                new Message { Header = CreateHeader(loginBody, 1), Body = loginBody });
#if AOREBIRTH_LINUX
            activeBus.Publish(receivedEvent);
#else
            var publisher = new MessagePublisher(
                new IHandleMessage[]
                {
                    new CreateCharacterHandler(),
                    new DeleteCharacterHandler(),
                    new RandomNameRequestHandler(),
                    new SelectCharacterHandler(),
                    new UserCredentialsHandler(),
                    new UserLoginHandler()
                });
            publisher.Publish(receivedEvent.Sender, receivedEvent.Message);
#endif
            Assert(client.WaitForSend(TimeSpan.FromSeconds(5)), "Active dispatch did not reach UserLoginHandler.");
            Assert(serializer.SerializedMessages.Count == 1, "Active UserLogin dispatch emitted an unexpected response count.");
            Message response = serializer.SerializedMessages[0];
            Assert(response.Header.Receiver == 0x00002B3F, "Server-salt response receiver changed.");
            var serverSalt = response.Body as ServerSaltMessage;
            Assert(serverSalt != null && serverSalt.ServerSalt != null && serverSalt.ServerSalt.Length == 32, "Active UserLogin dispatch emitted an invalid salt.");
            Assert(serverSalt.ServerSalt.All(value => value != 0), "Active UserLogin dispatch emitted a zero salt byte.");
            string expectedHex = string.Concat(serverSalt.ServerSalt.Select(value => value.ToString("x2", CultureInfo.InvariantCulture)));
            Assert(string.Equals(client.AccountName, "Stage7User", StringComparison.Ordinal), "Active UserLogin dispatch did not assign the account name.");
            Assert(string.Equals(client.ClientVersion, "18.8.53_EP1", StringComparison.Ordinal), "Active UserLogin dispatch did not assign the client version.");
            Assert(string.Equals(client.ServerSalt, expectedHex, StringComparison.Ordinal), "Active UserLogin dispatch client salt does not match the response bytes.");
            AddLine(lines, "runtime.active-dispatch", "publish=nonblocking", "controlled-handlers=concurrent", "controlled-executions=2", "user-login=exactly-once", "response=ServerSaltMessage", "receiver=0x00002B3F", "salt-shape=verified", "exact-random-bytes=excluded");
        }
        finally
        {
            client.Dispose();
            server.Dispose();
        }
    }

    private static void VerifyConcurrentPublish(IBus activeBus)
    {
        int executions = 0;
        int activeHandlers = 0;
        int maximumActiveHandlers = 0;
        Exception publishException = null;
        using (var entered = new CountdownEvent(2))
        using (var completed = new CountdownEvent(2))
        using (var release = new ManualResetEventSlim(false))
        using (var publishReturned = new ManualResetEventSlim(false))
        using (IDisposable first = activeBus.Subscribe<DispatchProbe>(probe =>
        {
            RunControlledHandler(
                ref executions,
                ref activeHandlers,
                ref maximumActiveHandlers,
                entered,
                completed,
                release);
        }))
        using (IDisposable second = activeBus.Subscribe<DispatchProbe>(probe =>
        {
            RunControlledHandler(
                ref executions,
                ref activeHandlers,
                ref maximumActiveHandlers,
                entered,
                completed,
                release);
        }))
        {
            var publisher = new Thread(() =>
            {
                try
                {
                    activeBus.Publish(new DispatchProbe());
                }
                catch (Exception exception)
                {
                    publishException = exception;
                }
                finally
                {
                    publishReturned.Set();
                }
            });
            publisher.IsBackground = true;
            publisher.Start();

            bool returnedWhileHandlersWereHeld = publishReturned.Wait(TimeSpan.FromSeconds(2));
            bool bothHandlersEntered = entered.Wait(TimeSpan.FromSeconds(5));
            release.Set();
            bool bothHandlersCompleted = completed.Wait(TimeSpan.FromSeconds(5));
            publisher.Join(5000);

            Assert(publishException == null, "Active IBus Publish threw: " + (publishException == null ? string.Empty : publishException.Message));
            Assert(returnedWhileHandlersWereHeld, "Active IBus Publish blocked on controlled handlers.");
            Assert(bothHandlersEntered, "Active IBus did not begin both controlled handlers concurrently.");
            Assert(bothHandlersCompleted, "Active IBus controlled handlers did not complete.");
            Assert(executions == 2, "Active IBus did not execute each controlled handler exactly once.");
            Assert(maximumActiveHandlers >= 2, "Active IBus controlled handlers did not overlap.");
        }
    }

    private static void RunControlledHandler(
        ref int executions,
        ref int activeHandlers,
        ref int maximumActiveHandlers,
        CountdownEvent entered,
        CountdownEvent completed,
        ManualResetEventSlim release)
    {
        int execution = Interlocked.Increment(ref executions);
        int active = Interlocked.Increment(ref activeHandlers);
        UpdateMaximum(ref maximumActiveHandlers, active);
        if (execution <= 2)
        {
            entered.Signal();
        }

        release.Wait(TimeSpan.FromSeconds(10));
        Interlocked.Decrement(ref activeHandlers);
        if (execution <= 2)
        {
            completed.Signal();
        }
    }

    private static void UpdateMaximum(ref int target, int candidate)
    {
        int current;
        do
        {
            current = Volatile.Read(ref target);
            if (candidate <= current) return;
        }
        while (Interlocked.CompareExchange(ref target, candidate, current) != current);
    }

    private static Header CreateHeader(MessageBody body, int receiver)
    {
        return new Header
        {
            MessageId = 0xDFDF,
            PacketType = body.PacketType,
            Unknown = 1,
            Sender = 1,
            Receiver = receiver
        };
    }

    private static KeyValuePair<string, MessageBody> Fixture(string name, MessageBody body)
    {
        return new KeyValuePair<string, MessageBody>(name, body);
    }

    private static void VerifyRandomName(string name)
    {
        Assert(!string.IsNullOrEmpty(name), "Random-name generator returned an empty name.");
        Assert(name.Length >= 4 && name.Length <= 10, "Random-name length escaped the legacy 4-10 character range.");
        Assert(char.IsUpper(name[0]), "Random-name generator did not capitalize the first character.");
        const string allowed = "aiuevybcfghjqktdnpmrlwnstyz";
        for (int index = 0; index < name.Length; index++)
        {
            char value = char.ToLowerInvariant(name[index]);
            Assert(allowed.IndexOf(value) >= 0, "Random-name generator emitted an unexpected character.");
            if (index > 0) Assert(char.IsLower(name[index]), "Random-name generator emitted uppercase content after the first character.");
        }
    }

    private static System.Reflection.FieldInfo GetInheritedField(Type type, string name)
    {
        for (Type current = type; current != null; current = current.BaseType)
        {
            System.Reflection.FieldInfo field = current.GetField(
                name,
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.DeclaredOnly);
            if (field != null) return field;
        }

        throw new InvalidOperationException("Missing inherited field " + type.FullName + "." + name + ".");
    }

    private static string ComputeSha256(byte[] bytes)
    {
        using (SHA256 sha256 = SHA256.Create())
        {
            return string.Concat(sha256.ComputeHash(bytes).Select(value => value.ToString("x2", CultureInfo.InvariantCulture)));
        }
    }

    private static void AddLine(ICollection<string> lines, params object[] values)
    {
        lines.Add(string.Join("|", values.Select(value => Escape(Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty))));
    }

    private static string Escape(string value)
    {
        return value.Replace("%", "%25").Replace("|", "%7C").Replace("\r", "%0D").Replace("\n", "%0A");
    }

    private static string Normalize(string value)
    {
        return value.Replace("\r\n", "\n").Replace("\r", "\n").TrimEnd('\n') + "\n";
    }

    private static void Assert(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    private sealed class RecordingSerializer : IMessageSerializer
    {
        internal readonly List<Message> SerializedMessages = new List<Message>();
        internal byte[] SerializedBytes = new byte[] { 0, 0, 0, 0 };
        internal Message DeserializedMessage;
        internal bool ThrowOnDeserialize;

        public Message Deserialize(byte[] buffer)
        {
            if (this.ThrowOnDeserialize) throw new InvalidOperationException("Stage 7 malformed fixture.");
            return this.DeserializedMessage;
        }

        public byte[] Serialize(Message message)
        {
            this.SerializedMessages.Add(message);
            return (byte[])this.SerializedBytes.Clone();
        }
    }

    private sealed class RecordingBus : IBus
    {
        internal readonly List<object> Published = new List<object>();

        public void Publish(object message)
        {
            this.Published.Add(message);
        }

        public IDisposable Subscribe<T>(Action<T> action)
        {
            return new EmptySubscription();
        }
    }

    private sealed class EmptySubscription : IDisposable
    {
        public void Dispose()
        {
        }
    }

    private sealed class DispatchProbe
    {
    }

    private sealed class CaptureClient : Client
    {
        private readonly ManualResetEventSlim sendSignal = new ManualResetEventSlim(false);

        internal CaptureClient(ServerBase server, IMessageSerializer serializer, IBus bus)
            : base(server, serializer, bus)
        {
        }

        internal readonly List<byte[]> SentPackets = new List<byte[]>();

        public override void Send(byte[] packet, int offset, int length)
        {
            var copy = new byte[length];
            Array.Copy(packet, offset, copy, 0, length);
            lock (this.SentPackets)
            {
                this.SentPackets.Add(copy);
            }

            this.sendSignal.Set();
        }

        internal uint ReadMessageNumber(byte[] packet)
        {
            return this.GetMessageNumber(packet);
        }

        internal uint ReadMessageNumber(BufferSegment segment)
        {
            return this.GetMessageNumber(segment);
        }

        internal bool Receive(BufferSegment segment, int length)
        {
            this._remainingLength = length;
            return this.OnReceive(segment);
        }

        internal void ResetSendSignal()
        {
            this.sendSignal.Reset();
        }

        internal bool WaitForSend(TimeSpan timeout)
        {
            return this.sendSignal.Wait(timeout);
        }
    }
}
