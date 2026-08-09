using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Threading;

using AORebirth.Core.Components;
using AORebirth.Core.EventHandlers.Events;
using AORebirth.Core.EventHandlers.Handlers;

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
        VerifyAuthenticationSecurity(lines);
        VerifyMessagePublisherOrdering(lines);
        VerifyAdapterOrdering(lines);
        VerifyActiveDispatch(lines);
#if AOREBIRTH_LINUX
        VerifyLinuxBoundedDrain();
#endif
        AddLine(
            lines,
            "runtime.shutdown-drain",
            "linux-only=held-message-rejected-and-drained",
            "actual-handler=MessageReceivedHandler",
            "wait=bounded");
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

    private static void VerifyAuthenticationSecurity(ICollection<string> lines)
    {
        var encryption = new AO.Core.Encryption.LoginEncryption();
        Assert(encryption.i_Enable, "Login encryption is disabled in the active build configuration.");
        encryption.i_Enable = false;
        Assert(
            !encryption.IsValidLogin("invalid-login-key", "invalid-salt", "Stage7User", "invalid-password-hash"),
            "Disabled four-argument login validation bypassed invalid credentials.");
        Assert(
            !encryption.IsValidLogin("invalid-login-key", "invalid-salt", "Stage7User"),
            "Disabled three-argument login validation bypassed invalid credentials.");

        VerifyAnonymousHandler(
            new CreateCharacterHandler(),
            new CreateCharacterMessage(),
            "create-character");
        VerifyAnonymousHandler(
            new SelectCharacterHandler(),
            new SelectCharacterMessage { CharacterId = 1 },
            "select-character");
        VerifyAnonymousHandler(
            new DeleteCharacterHandler(),
            new DeleteCharacterMessage { CharacterId = 1 },
            "delete-character");
        VerifyAuthenticationStateMachine();

        AddLine(
            lines,
            "runtime.authentication-security",
            "debug-encryption=enabled",
            "disabled-bypass=fail-closed",
            "anonymous-create=reject-before-dao",
            "anonymous-select=reject-before-dao",
            "anonymous-delete=reject-before-dao",
            "challenge-reset=verified",
            "challenge-replay=rejected");
    }

    private static void VerifyAnonymousHandler(
        IHandleMessage handler,
        MessageBody body,
        string description)
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
            handler.Handle(
                client,
                new Message { Header = CreateHeader(body, 1), Body = body });

            Assert(
                serializer.SerializedMessages.Count == 1,
                "Anonymous " + description + " did not emit exactly one rejection.");
            Message response = serializer.SerializedMessages[0];
            Assert(
                response.Header.Receiver == 0x00001F83,
                "Anonymous " + description + " rejection receiver changed.");
            var loginError = response.Body as LoginErrorMessage;
            Assert(
                loginError != null && loginError.Error == LoginError.InvalidUserNamePassword,
                "Anonymous " + description + " emitted a non-authentication response.");
            Assert(
                client.SentPackets.Count == 1,
                "Anonymous " + description + " emitted an unexpected packet count.");
        }
        finally
        {
            client.Dispose();
            server.Dispose();
        }
    }

    private static void VerifyAuthenticationStateMachine()
    {
        var serializer = new RecordingSerializer();
        var bus = new RecordingBus();
        var factory = new ClientFactory(serializer, bus);
        var server = new LoginServer(factory);
        var client = new CaptureClient(server, serializer, bus);
        try
        {
            object[] unauthenticated = { string.Empty };
            Assert(
                !(bool)InvokeRequired(client, "TryGetAuthenticatedAccountName", unauthenticated),
                "Fresh LoginEngine client reported an authenticated account.");

            Assert(
                (bool)InvokeRequired(
                    client,
                    "BeginAuthentication",
                    new object[] { "Stage7User", "18.8.53_EP1", "stage7-salt-one" }),
                "LoginEngine client did not issue its first authentication challenge.");

            object[] wrongAttempt = { "WrongUser", string.Empty, string.Empty, 0L };
            Assert(
                !(bool)InvokeRequired(client, "TryBeginAuthenticationAttempt", wrongAttempt),
                "Authentication challenge accepted a different account name.");

            object[] firstAttempt = { "Stage7User", string.Empty, string.Empty, 0L };
            Assert(
                (bool)InvokeRequired(client, "TryBeginAuthenticationAttempt", firstAttempt),
                "LoginEngine client did not consume its first authentication challenge.");
            Assert(
                string.Equals((string)firstAttempt[1], "Stage7User", StringComparison.Ordinal)
                && string.Equals((string)firstAttempt[2], "stage7-salt-one", StringComparison.Ordinal),
                "Authentication attempt did not return the challenged identity and salt.");
            long firstGeneration = (long)firstAttempt[3];

            Assert(
                (bool)InvokeRequired(
                    client,
                    "BeginAuthentication",
                    new object[] { "Stage7Replacement", "18.8.53_EP1", "stage7-salt-two" }),
                "LoginEngine client did not replace an in-flight authentication challenge.");
            Assert(
                !(bool)InvokeRequired(
                    client,
                    "CompleteAuthentication",
                    new object[] { "Stage7User", firstGeneration }),
                "Replaced authentication challenge completed with a stale generation.");

            object[] replacementAttempt = { "Stage7Replacement", string.Empty, string.Empty, 0L };
            Assert(
                (bool)InvokeRequired(client, "TryBeginAuthenticationAttempt", replacementAttempt),
                "Replacement authentication challenge could not begin.");
            long replacementGeneration = (long)replacementAttempt[3];
            Assert(
                replacementGeneration != firstGeneration,
                "Replacement authentication challenge reused its generation.");
            Assert(
                (bool)InvokeRequired(
                    client,
                    "CompleteAuthentication",
                    new object[] { "Stage7Replacement", replacementGeneration }),
                "Replacement authentication challenge could not complete.");
            Assert(
                !(bool)InvokeRequired(
                    client,
                    "CompleteAuthentication",
                    new object[] { "Stage7Replacement", replacementGeneration }),
                "Completed authentication challenge was replayable.");
            Assert(
                string.Equals(client.ServerSalt, string.Empty, StringComparison.Ordinal),
                "Successful authentication retained the server challenge salt.");

            object[] authenticated = { string.Empty };
            Assert(
                (bool)InvokeRequired(client, "TryGetAuthenticatedAccountName", authenticated)
                && string.Equals((string)authenticated[0], "Stage7Replacement", StringComparison.Ordinal),
                "Completed authentication did not expose the authenticated identity.");

            Assert(
                (bool)InvokeRequired(
                    client,
                    "BeginAuthentication",
                    new object[] { "Stage7Final", "18.8.53_EP1", "stage7-salt-three" }),
                "LoginEngine client did not reset an authenticated session for a new challenge.");
            object[] resetAuthentication = { string.Empty };
            Assert(
                !(bool)InvokeRequired(client, "TryGetAuthenticatedAccountName", resetAuthentication),
                "A new challenge retained the prior authenticated identity.");
        }
        finally
        {
            client.Dispose();
            server.Dispose();
        }
    }

    private static void VerifyMessagePublisherOrdering(ICollection<string> lines)
    {
        using (var handler = new SequencingMessageHandler())
        {
            var publisher = new MessagePublisher(new IHandleMessage[] { handler });
            var sameSender = new object();
            var firstFailure = new ThreadFailure();
            var secondFailure = new ThreadFailure();
            Thread first = StartPublishThread(
                publisher,
                sameSender,
                "same-first",
                null,
                firstFailure);
            Thread second = null;
            try
            {
                Assert(
                    handler.SameFirstEntered.Wait(TimeSpan.FromSeconds(5)),
                    "Same-sender first dispatch did not enter its handler.");
                second = StartPublishThread(
                    publisher,
                    sameSender,
                    "same-second",
                    handler.SameSecondStarted,
                    secondFailure);
                Assert(
                    handler.SameSecondStarted.Wait(TimeSpan.FromSeconds(5)),
                    "Same-sender second dispatch thread did not start.");
                Assert(
                    !handler.SameSecondEntered.Wait(TimeSpan.FromMilliseconds(250)),
                    "Same-sender second dispatch overtook a held first dispatch.");

                handler.ReleaseSame.Set();
                Assert(first.Join(5000), "Same-sender first dispatch did not complete.");
                Assert(second.Join(5000), "Same-sender second dispatch did not complete.");
                Assert(firstFailure.Value == null, "Same-sender first dispatch threw: " + FormatException(firstFailure.Value));
                Assert(secondFailure.Value == null, "Same-sender second dispatch threw: " + FormatException(secondFailure.Value));
                Assert(
                    handler.SameOrder.SequenceEqual(new[] { "same-first", "same-second" }),
                    "Same-sender dispatch order was not FIFO.");

                var differentFirstFailure = new ThreadFailure();
                var differentSecondFailure = new ThreadFailure();
                Thread differentFirst = StartPublishThread(
                    publisher,
                    new object(),
                    "different-first",
                    null,
                    differentFirstFailure);
                Thread differentSecond = StartPublishThread(
                    publisher,
                    new object(),
                    "different-second",
                    null,
                    differentSecondFailure);
                try
                {
                    Assert(
                        handler.DifferentEntered.Wait(TimeSpan.FromSeconds(5)),
                        "Different-sender dispatches did not enter concurrently.");
                    Assert(
                        handler.MaximumDifferentActive >= 2,
                        "Different-sender dispatches were serialized.");
                }
                finally
                {
                    handler.ReleaseDifferent.Set();
                }

                Assert(differentFirst.Join(5000), "Different-sender first dispatch did not complete.");
                Assert(differentSecond.Join(5000), "Different-sender second dispatch did not complete.");
                Assert(differentFirstFailure.Value == null, "Different-sender first dispatch threw: " + FormatException(differentFirstFailure.Value));
                Assert(differentSecondFailure.Value == null, "Different-sender second dispatch threw: " + FormatException(differentSecondFailure.Value));
            }
            finally
            {
                handler.ReleaseSame.Set();
                handler.ReleaseDifferent.Set();
                if (first != null && first.IsAlive) first.Join(5000);
                if (second != null && second.IsAlive) second.Join(5000);
            }
        }

        AddLine(
            lines,
            "runtime.message-publisher-defense",
            "same-sender=fifo",
            "different-sender=concurrent",
            "different-sender-overlap=2");
    }

    private static void VerifyAdapterOrdering(ICollection<string> lines)
    {
        using (var publisher = new AdapterSequencingPublisher())
        {
            var actualHandler = new MessageReceivedHandler(publisher);
            IBus adapter = CreateMemBusAdapter(new SingleInstanceContainer(actualHandler));
            var sameSender = new object();
            try
            {
                adapter.Publish(CreateReceivedEvent(sameSender, "adapter-same-first"));
                Assert(
                    publisher.SameFirstEntered.Wait(TimeSpan.FromSeconds(5)),
                    "Adapter same-sender first dispatch did not enter its handler.");
                adapter.Publish(CreateReceivedEvent(sameSender, "adapter-same-second"));
                Assert(
                    !publisher.SameSecondEntered.Wait(TimeSpan.FromMilliseconds(250)),
                    "Adapter dispatched a same-sender message before its predecessor completed.");

                publisher.ReleaseSame.Set();
                Assert(
                    publisher.SameSecondCompleted.Wait(TimeSpan.FromSeconds(5)),
                    "Adapter same-sender queue did not advance after completion.");
                Assert(
                    publisher.SameOrder.SequenceEqual(new[] { "adapter-same-first", "adapter-same-second" }),
                    "Adapter same-sender dispatch order was not FIFO.");

                adapter.Publish(CreateReceivedEvent(new object(), "adapter-different-first"));
                adapter.Publish(CreateReceivedEvent(new object(), "adapter-different-second"));
                Assert(
                    publisher.DifferentEntered.Wait(TimeSpan.FromSeconds(5)),
                    "Adapter different-sender dispatches did not enter concurrently.");
                Assert(
                    publisher.MaximumDifferentActive >= 2,
                    "Adapter serialized dispatches from different senders.");
            }
            finally
            {
                publisher.ReleaseSame.Set();
                publisher.ReleaseDifferent.Set();
            }

            Assert(
                publisher.DifferentCompleted.Wait(TimeSpan.FromSeconds(5)),
                "Adapter different-sender dispatches did not complete.");
        }

        AddLine(
            lines,
            "runtime.dispatch-ordering",
            "adapter=MemBusAdapter",
            "actual-handler=MessageReceivedHandler",
            "same-sender=fifo",
            "different-sender=concurrent",
            "different-sender-overlap=2");
    }

    private static MessageReceivedEvent CreateReceivedEvent(object sender, string marker)
    {
        var body = new UserLoginMessage { UserName = marker, ClientVersion = "stage7.1" };
        return new MessageReceivedEvent(
            sender,
            new Message { Header = CreateHeader(body, 1), Body = body });
    }

#if AOREBIRTH_LINUX
    private static void VerifyLinuxBoundedDrain()
    {
        using (var holdingPublisher = new HoldingMessagePublisher())
        {
            var actualHandler = new MessageReceivedHandler(holdingPublisher);
            var container = new SingleInstanceContainer(actualHandler);
            IBus adapter = CreateMemBusAdapter(container);
            var body = new UserLoginMessage { UserName = "drain-held", ClientVersion = "stage7.1" };
            var receivedEvent = new MessageReceivedEvent(
                new object(),
                new Message { Header = CreateHeader(body, 1), Body = body });
            try
            {
                adapter.Publish(receivedEvent);
                Assert(
                    holdingPublisher.Entered.Wait(TimeSpan.FromSeconds(5)),
                    "Linux drain fixture did not enter the actual MessageReceivedHandler.");

                InvokeRequired(adapter, "StopAcceptingMessages", new object[0]);
                Assert(
                    !(bool)InvokeRequired(
                        adapter,
                        "WaitForIdle",
                        new object[] { TimeSpan.FromMilliseconds(150) }),
                    "Linux drain reported idle while an actual MessageReceivedHandler was held.");

                bool rejected = false;
                try
                {
                    adapter.Publish(
                        new MessageReceivedEvent(
                            new object(),
                            new Message { Header = CreateHeader(body, 1), Body = body }));
                }
                catch (InvalidOperationException)
                {
                    rejected = true;
                }

                Assert(rejected, "Linux drain accepted a message after shutdown began.");
            }
            finally
            {
                holdingPublisher.Release.Set();
            }

            Assert(
                (bool)InvokeRequired(
                    adapter,
                    "WaitForIdle",
                    new object[] { TimeSpan.FromSeconds(5) }),
                "Linux drain did not become idle after the held handler completed.");
            Assert(
                holdingPublisher.Executions == 1,
                "Linux drain did not execute the held MessageReceivedHandler exactly once.");
        }
    }
#endif

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

    private static object InvokeRequired(object target, string methodName, object[] arguments)
    {
        MethodInfo[] methods = target.GetType()
            .GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
            .Where(method => string.Equals(method.Name, methodName, StringComparison.Ordinal)
                && method.GetParameters().Length == arguments.Length)
            .ToArray();
        Assert(
            methods.Length == 1,
            "Expected exactly one " + target.GetType().FullName + "." + methodName + " method.");
        return methods[0].Invoke(target, arguments);
    }

    private static IBus CreateMemBusAdapter(IContainer container)
    {
        object iocAdapter = Activator.CreateInstance(
            typeof(MemBusIoCAdapter),
            new object[] { container });
        object adapter = Activator.CreateInstance(
            typeof(MemBusAdapter),
            new[] { iocAdapter });
        var bus = adapter as IBus;
        Assert(bus != null, "Stage 7.1 fixture could not construct MemBusAdapter.");
        return bus;
    }

    private static Thread StartPublishThread(
        MessagePublisher publisher,
        object sender,
        string marker,
        ManualResetEventSlim started,
        ThreadFailure failure)
    {
        var thread = new Thread(() =>
        {
            try
            {
                if (started != null) started.Set();
                var body = new UserLoginMessage { UserName = marker, ClientVersion = "stage7.1" };
                publisher.Publish(
                    sender,
                    new Message { Header = CreateHeader(body, 1), Body = body });
            }
            catch (Exception exception)
            {
                failure.Value = exception;
            }
        });
        thread.IsBackground = true;
        thread.Start();
        return thread;
    }

    private static string FormatException(Exception exception)
    {
        return exception == null ? string.Empty : exception.GetType().FullName + ": " + exception.Message;
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

    private sealed class ThreadFailure
    {
        internal Exception Value;
    }

    private sealed class SequencingMessageHandler : IHandleMessage<UserLoginMessage>, IDisposable
    {
        private readonly object sameOrderSync = new object();
        private readonly List<string> sameOrder = new List<string>();
        private int differentActive;
        private int maximumDifferentActive;

        internal readonly ManualResetEventSlim SameFirstEntered = new ManualResetEventSlim(false);
        internal readonly ManualResetEventSlim SameSecondStarted = new ManualResetEventSlim(false);
        internal readonly ManualResetEventSlim SameSecondEntered = new ManualResetEventSlim(false);
        internal readonly ManualResetEventSlim ReleaseSame = new ManualResetEventSlim(false);
        internal readonly CountdownEvent DifferentEntered = new CountdownEvent(2);
        internal readonly ManualResetEventSlim ReleaseDifferent = new ManualResetEventSlim(false);

        internal IEnumerable<string> SameOrder
        {
            get
            {
                lock (this.sameOrderSync)
                {
                    return this.sameOrder.ToArray();
                }
            }
        }

        internal int MaximumDifferentActive
        {
            get
            {
                return Volatile.Read(ref this.maximumDifferentActive);
            }
        }

        public void Handle(object sender, Message message)
        {
            var body = (UserLoginMessage)message.Body;
            string marker = body.UserName;
            if (marker.StartsWith("same-", StringComparison.Ordinal))
            {
                lock (this.sameOrderSync)
                {
                    this.sameOrder.Add(marker);
                }

                if (string.Equals(marker, "same-first", StringComparison.Ordinal))
                {
                    this.SameFirstEntered.Set();
                    this.ReleaseSame.Wait(TimeSpan.FromSeconds(10));
                }
                else
                {
                    this.SameSecondEntered.Set();
                }

                return;
            }

            int active = Interlocked.Increment(ref this.differentActive);
            UpdateMaximum(ref this.maximumDifferentActive, active);
            try
            {
                this.DifferentEntered.Signal();
                this.ReleaseDifferent.Wait(TimeSpan.FromSeconds(10));
            }
            finally
            {
                Interlocked.Decrement(ref this.differentActive);
            }
        }

        public void Dispose()
        {
            this.SameFirstEntered.Dispose();
            this.SameSecondStarted.Dispose();
            this.SameSecondEntered.Dispose();
            this.ReleaseSame.Dispose();
            this.DifferentEntered.Dispose();
            this.ReleaseDifferent.Dispose();
        }
    }

    private sealed class AdapterSequencingPublisher : IMessagePublisher, IDisposable
    {
        private readonly object sameOrderSync = new object();
        private readonly List<string> sameOrder = new List<string>();
        private int differentActive;
        private int maximumDifferentActive;

        internal readonly ManualResetEventSlim SameFirstEntered = new ManualResetEventSlim(false);
        internal readonly ManualResetEventSlim SameSecondEntered = new ManualResetEventSlim(false);
        internal readonly ManualResetEventSlim SameSecondCompleted = new ManualResetEventSlim(false);
        internal readonly ManualResetEventSlim ReleaseSame = new ManualResetEventSlim(false);
        internal readonly CountdownEvent DifferentEntered = new CountdownEvent(2);
        internal readonly CountdownEvent DifferentCompleted = new CountdownEvent(2);
        internal readonly ManualResetEventSlim ReleaseDifferent = new ManualResetEventSlim(false);

        internal IEnumerable<string> SameOrder
        {
            get
            {
                lock (this.sameOrderSync)
                {
                    return this.sameOrder.ToArray();
                }
            }
        }

        internal int MaximumDifferentActive
        {
            get
            {
                return Volatile.Read(ref this.maximumDifferentActive);
            }
        }

        public void Publish(object sender, Message message)
        {
            var body = (UserLoginMessage)message.Body;
            string marker = body.UserName;
            if (marker.StartsWith("adapter-same-", StringComparison.Ordinal))
            {
                lock (this.sameOrderSync)
                {
                    this.sameOrder.Add(marker);
                }

                if (string.Equals(marker, "adapter-same-first", StringComparison.Ordinal))
                {
                    this.SameFirstEntered.Set();
                    this.ReleaseSame.Wait(TimeSpan.FromSeconds(10));
                }
                else
                {
                    this.SameSecondEntered.Set();
                    this.SameSecondCompleted.Set();
                }

                return;
            }

            int active = Interlocked.Increment(ref this.differentActive);
            UpdateMaximum(ref this.maximumDifferentActive, active);
            try
            {
                this.DifferentEntered.Signal();
                this.ReleaseDifferent.Wait(TimeSpan.FromSeconds(10));
            }
            finally
            {
                Interlocked.Decrement(ref this.differentActive);
                this.DifferentCompleted.Signal();
            }
        }

        public void Dispose()
        {
            this.SameFirstEntered.Dispose();
            this.SameSecondEntered.Dispose();
            this.SameSecondCompleted.Dispose();
            this.ReleaseSame.Dispose();
            this.DifferentEntered.Dispose();
            this.DifferentCompleted.Dispose();
            this.ReleaseDifferent.Dispose();
        }
    }

#if AOREBIRTH_LINUX
    private sealed class HoldingMessagePublisher : IMessagePublisher, IDisposable
    {
        private int executions;

        internal readonly ManualResetEventSlim Entered = new ManualResetEventSlim(false);
        internal readonly ManualResetEventSlim Release = new ManualResetEventSlim(false);

        internal int Executions
        {
            get
            {
                return Volatile.Read(ref this.executions);
            }
        }

        public void Publish(object sender, Message message)
        {
            Interlocked.Increment(ref this.executions);
            this.Entered.Set();
            this.Release.Wait(TimeSpan.FromSeconds(10));
        }

        public void Dispose()
        {
            this.Entered.Dispose();
            this.Release.Dispose();
        }
    }
#endif

    private sealed class SingleInstanceContainer : IContainer
    {
        private readonly object instance;

        internal SingleInstanceContainer(object instance)
        {
            this.instance = instance;
        }

        public IEnumerable<object> GetAllInstances(Type serviceType)
        {
            return serviceType.IsInstanceOfType(this.instance)
                ? new[] { this.instance }
                : new object[0];
        }

        public object GetInstance(Type serviceType, string key = null)
        {
            if (serviceType.IsInstanceOfType(this.instance)) return this.instance;
            throw new InvalidOperationException("No Stage 7.1 fixture instance for " + serviceType.FullName + ".");
        }

        public T GetInstance<T>(string key = null)
        {
            return (T)this.GetInstance(typeof(T), key);
        }
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
