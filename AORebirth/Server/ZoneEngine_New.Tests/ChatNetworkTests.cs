using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Reflection;
using System.Text;
using System.Threading;
using AORebirth.World.Contracts.V1;
using Cell.Core;
using ChatEngine;
using ChatEngine.Channels;
using ChatEngine.CoreServer;
using ChatEngine.Lists;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using ChatClient = ChatEngine.CoreClient.Client;

namespace ZoneEngine_New.Tests;

[TestClass]
[DoNotParallelize]
[TestCategory("ChatNetwork")]
public sealed class ChatNetworkTests
{
    const uint SenderId = 91001;
    const uint RecipientId = 91002;

    [TestMethod]
    public void OrganizationJoinAfterRenamePublishesThePersistedName()
    {
        const int orgId = 185;
        string storedName = "Original Organization";
        using var world = new ChatWorld(id => id == orgId ? storedName : null);
        world.OrganizationCommand(SenderId, $"#aorebirth-org-join {orgId}");
        Assert.AreEqual("Original Organization", OrganizationNames(world.Sender, orgId).Single());

        storedName = "Renamed Organization";
        world.OrganizationCommand(RecipientId, $"#aorebirth-org-join {orgId}");

        Assert.AreEqual(storedName, OrganizationNames(world.Recipient, orgId).Single());
        CollectionAssert.AreEqual(new[] { "Original Organization", storedName }, OrganizationNames(world.Sender, orgId));
        Assert.AreEqual(1, world.OrganizationChannelCount(orgId));
    }

    [TestMethod]
    public void OrganizationRefreshUpdatesExistingMembersWithoutRejoiningThem()
    {
        const int orgId = 185;
        const int otherOrg = 186;
        string storedName = "Original Organization";
        using var world = new ChatWorld(id => id == orgId ? storedName : id == otherOrg ? "Other Organization" : null);
        using var unrelated = world.Client(91003, "OtherMember");
        world.OrganizationCommand(SenderId, $"#aorebirth-org-join {orgId}");
        world.OrganizationCommand(91003, $"#aorebirth-org-join {otherOrg}");

        storedName = "Renamed Organization";
        world.OrganizationCommand(SenderId, $"#aorebirth-org-refresh {orgId}");
        world.OrganizationCommand(RecipientId, $"#aorebirth-org-join {orgId}");
        Assert.AreEqual(storedName, OrganizationNames(world.Recipient, orgId).Single());

        storedName = "Final Organization";
        world.OrganizationCommand(SenderId, $"#aorebirth-org-refresh {orgId}");
        world.OrganizationCommand(SenderId, $"#aorebirth-org-refresh {orgId}");

        CollectionAssert.AreEqual(new[] { "Original Organization", "Renamed Organization", storedName },
            OrganizationNames(world.Sender, orgId));
        CollectionAssert.AreEqual(new[] { "Renamed Organization", storedName }, OrganizationNames(world.Recipient, orgId));
        Assert.AreEqual("Other Organization", OrganizationNames(unrelated, otherOrg).Single());
        Assert.AreEqual(1, world.OrganizationChannelCount(orgId));
        Assert.AreEqual(1, world.OrganizationChannelCount(otherOrg));
        Assert.AreEqual(1, world.Sender.Channels.Count(c => c.channelType == ChannelType.Organization));
        Assert.AreEqual(1, world.Recipient.Channels.Count(c => c.channelType == ChannelType.Organization));
        Assert.AreEqual(1, world.Sender.Sent.Count(packet => IsChannelJoin(packet, ChannelType.OrgMsg, 1)));
        Assert.AreEqual(1, world.Recipient.Sent.Count(packet => IsChannelJoin(packet, ChannelType.OrgMsg, 1)));
        Assert.IsFalse(world.Sender.Sent.Concat(world.Recipient.Sent)
            .Any(packet => BinaryPrimitives.ReadUInt16BigEndian(packet.AsSpan(0, 2)) == 61));
    }

    [TestMethod]
    public void OrganizationReconnectAndNewChatServerPublishThePersistedName()
    {
        const int orgId = 185;
        string storedName = "Original Organization";
        using (var world = new ChatWorld(id => id == orgId ? storedName : null))
        {
            world.OrganizationCommand(SenderId, $"#aorebirth-org-join {orgId}");
            world.OrganizationCommand(RecipientId, $"#aorebirth-org-join {orgId}");
            storedName = "Persisted Organization";

            using var reconnectedLeader = world.Replace(world.Sender);
            using var reconnectedMember = world.Replace(world.Recipient);
            world.OrganizationCommand(SenderId, $"#aorebirth-org-join {orgId}");
            world.OrganizationCommand(RecipientId, $"#aorebirth-org-join {orgId}");

            Assert.AreEqual(storedName, OrganizationNames(reconnectedLeader, orgId).Single());
            Assert.AreEqual(storedName, OrganizationNames(reconnectedMember, orgId).Single());
            Assert.AreEqual(1, world.OrganizationChannelCount(orgId));
        }

        using var restarted = new ChatWorld(id => id == orgId ? storedName : null);
        restarted.OrganizationCommand(SenderId, $"#aorebirth-org-join {orgId}");
        restarted.OrganizationCommand(RecipientId, $"#aorebirth-org-join {orgId}");
        Assert.AreEqual(storedName, OrganizationNames(restarted.Sender, orgId).Single());
        Assert.AreEqual(storedName, OrganizationNames(restarted.Recipient, orgId).Single());
        Assert.AreEqual(1, restarted.OrganizationChannelCount(orgId));
    }

    static string[] OrganizationNames(CapturingClient client, int orgId)
        => client.Sent.Where(packet => IsChannelJoin(packet, ChannelType.Organization, orgId))
            .Select(packet =>
            {
                int nameBytes = BinaryPrimitives.ReadUInt16BigEndian(packet.AsSpan(9, 2));
                Assert.AreEqual(17 + nameBytes, packet.Length);
                Assert.AreEqual((uint)(ChannelFlags.NoInternational | ChannelFlags.GroupOnName),
                    BinaryPrimitives.ReadUInt32BigEndian(packet.AsSpan(11 + nameBytes, 4)));
                Assert.AreEqual((ushort)0, BinaryPrimitives.ReadUInt16BigEndian(packet.AsSpan(15 + nameBytes, 2)));
                return Encoding.UTF8.GetString(packet, 11, nameBytes);
            })
            .ToArray();

    static bool IsChannelJoin(byte[] packet, ChannelType type, int channelId)
        => packet.Length >= 17
            && BinaryPrimitives.ReadUInt16BigEndian(packet.AsSpan(0, 2)) == 60
            && packet[4] == (byte)type
            && BinaryPrimitives.ReadInt32BigEndian(packet.AsSpan(5, 4)) == channelId;

    [TestMethod]
    public void CompleteHtmlTellUsesTheActualParserAndOutboundPacket()
    {
        using var world = new ChatWorld();
        string html = Html(7000);

        world.Sender.Feed(TellFrame(RecipientId, html));

        AssertTells(world, html);
    }

    [TestMethod]
    public void OneByteReceivesWaitForTheWholeHeaderAndBody()
    {
        using var world = new ChatWorld();
        const string html = "<a href=\"text://<font color='#00ff00'>modules</font>\">open</a>";
        byte[] frame = TellFrame(RecipientId, html);
        for (int index = 0; index < frame.Length; index++)
        {
            world.Sender.Feed(frame.AsSpan(index, 1));
            if (index + 1 < frame.Length)
            {
                Assert.AreEqual(0, world.Recipient.Sent.Count, "A partial frame was dispatched at byte " + index);
            }
        }

        AssertTells(world, html);
    }

    [TestMethod]
    public void SevenKilobyteHtmlSurvivesEveryTwoChunkBoundary()
    {
        using var world = new ChatWorld();
        string html = Html(7000);
        byte[] frame = TellFrame(RecipientId, html);
        byte[] expected = TellFrame(SenderId, html, new byte[] { 0 });
        // Reuse one real server and its clients rather than allocate thousands of fixture worlds.
        for (int split = 1; split < frame.Length; split++)
        {
            world.Recipient.Sent.Clear();
            world.Sender.Feed(frame.AsSpan(0, split));
            Assert.AreEqual(0, world.Recipient.Sent.Count, "Premature dispatch at split " + split);

            world.Sender.Feed(frame.AsSpan(split));

            Assert.AreEqual(1, world.Recipient.Sent.Count, "Wrong tell count at split " + split);
            CollectionAssert.AreEqual(expected, world.Recipient.Sent[0], "HTML changed at split " + split);
        }
    }

    [TestMethod]
    public void CoalescedFramesDispatchInOrderAndRetainPartialHeaderAndBodyTails()
    {
        using var world = new ChatWorld();
        string[] messages = { Html(2500), "second tell", "<a href=\"text://third\">tail</a>" };
        byte[] third = TellFrame(RecipientId, messages[2]);
        byte[] firstReceive = Join(TellFrame(RecipientId, messages[0]), TellFrame(RecipientId, messages[1]), third[..2]);

        world.Sender.Feed(firstReceive);
        AssertTells(world, messages[..2]);
        world.Sender.Feed(third.AsSpan(2, third.Length - 3));
        AssertTells(world, messages[..2]);
        world.Sender.Feed(third.AsSpan(third.Length - 1));

        AssertTells(world, messages);
    }

    [TestMethod]
    public void TellBurstCrossingReceiveSegmentsRetainsEveryMessage()
    {
        using var world = new ChatWorld();
        string[] messages = { Html(7000), "second:" + Html(7000), "third:" + Html(7000) };
        byte[] wire = Join(messages.Select(message => TellFrame(RecipientId, message)).ToArray());
        Assert.IsTrue(wire.Length > ClientBase.BufferSize * 2);

        world.Sender.FeedInReceiveSegments(wire);

        AssertTells(world, messages);
    }

    [TestMethod]
    public void ValidMultibyteUtf8IsDecodedOnlyAfterAllItsBytesArrive()
    {
        using var world = new ChatWorld();
        const string html = "<a href=\"text://é漢🙂<br>κόσμος\">módules</a>";
        byte[] frame = TellFrame(RecipientId, html);
        for (int index = 0; index < frame.Length; index++)
        {
            world.Sender.Feed(frame.AsSpan(index, 1));
        }

        AssertTells(world, html);
    }

    [TestMethod]
    public void NonzeroSegmentOffsetPreservesTheFrameBytes()
    {
        using var world = new ChatWorld();
        const string text = "a frame in a pooled segment";

        world.Sender.Feed(TellFrame(RecipientId, text), segmentOffset: 23);

        AssertTells(world, text);
    }

    [TestMethod]
    public void UnknownCompleteFrameDoesNotEatTheFollowingTell()
    {
        using var world = new ChatWorld();
        const string message = "after unknown opcode";

        world.Sender.Feed(Join(Frame(0x7FFE, new byte[] { 0xAA, 0xBB }), TellFrame(RecipientId, message)));

        AssertTells(world, message);
    }

    [TestMethod]
    public void PingRepliesRetainIndependentOwnedFrameBuffers()
    {
        using var world = new ChatWorld();
        world.Sender.RetainPacketBuffers = true;
        byte[] first = Frame(100, new byte[] { 1, 2, 3 });
        byte[] second = Frame(100, new byte[] { 4, 5, 6, 7 });
        byte[] received = Join(first, second);

        world.Sender.Feed(received);

        Assert.AreEqual(2, world.Sender.Sent.Count);
        Assert.AreNotSame(world.Sender.Sent[0], world.Sender.Sent[1]);
        Array.Fill(received, (byte)0xCC);
        world.Sender.Feed(Frame(100, new byte[] { 8, 9 }));
        CollectionAssert.AreEqual(first, world.Sender.Sent[0]);
        CollectionAssert.AreEqual(second, world.Sender.Sent[1]);
    }

    [TestMethod]
    public void AHandlerDisposingItsClientStopsTheCoalescedTail()
    {
        using var world = new ChatWorld();
        world.Sender.DisposeOnSend = true;
        byte[] first = Frame(100, new byte[] { 1, 2 });
        Assert.IsTrue(world.Sender.HasSocket);

        world.Sender.Feed(Join(first, Frame(100, new byte[] { 3, 4 })));

        Assert.AreEqual(1, world.Sender.Sent.Count);
        CollectionAssert.AreEqual(first, world.Sender.Sent[0]);
        Assert.IsFalse(world.Sender.HasSocket);
    }

    [TestMethod, Timeout(10000)]
    public void ActualReceiveDisposingPingHandlerClosesAndReleasesItsOperationSegment()
    {
        using var pair = new ClientSendTests.LoopbackPair();
        using var server = new ChatServer();
        using var receiveExited = new ManualResetEventSlim();
        using var client = new CapturingClient(server, pair.Sender)
        {
            DisposeOnSend = true,
            DisposedReceiveExited = receiveExited
        };
        byte[] first = Frame(100, new byte[] { 1, 2 });
        byte[] wire = Join(first, Frame(100, new byte[] { 3, 4 }));
        pair.Peer.SendTimeout = 2000;
        int sent = 0;
        while (sent < wire.Length)
        {
            int count = pair.Peer.Send(wire.AsSpan(sent), SocketFlags.None);
            Assert.IsTrue(count > 0, "The fixture socket closed before both Ping frames were queued.");
            sent += count;
        }

        client.BeginReceive();

        Assert.IsTrue(receiveExited.Wait(TimeSpan.FromSeconds(2)), "The actual receive path did not return from its disposing Ping handler.");
        Assert.AreEqual(1, client.Sent.Count);
        CollectionAssert.AreEqual(first, client.Sent[0]);
        Assert.IsNull(client.TcpSocket);
        BufferSegment received = client.ReceivedSegment!;
        Assert.IsNotNull(received);
        Assert.IsTrue(SpinWait.SpinUntil(() => received.Uses == 0, TimeSpan.FromSeconds(2)),
            "The actual receive callback retained its operation segment after disposal.");
    }

    [TestMethod]
    public void ZeroBodyLftUncheckAndFollowingTellBothReachTheirHandlers()
    {
        using var world = new ChatWorld();
        LftRegistry.Upsert(SenderId, "looking for a team");
        Assert.IsTrue(LftRegistry.IsListed(SenderId));

        world.Sender.Feed(Join(Frame(1501, Array.Empty<byte>()), TellFrame(RecipientId, "after LFT uncheck")));

        Assert.IsFalse(LftRegistry.IsListed(SenderId));
        AssertTells(world, "after LFT uncheck");
    }

    [TestMethod]
    public void MaximumUnsignedBodyLengthIsBufferedAcross8192ByteReceives()
    {
        using var world = new ChatWorld();
        byte[] frame = Frame(100, Enumerable.Repeat((byte)0xAB, ushort.MaxValue).ToArray());
        Assert.AreEqual(ushort.MaxValue + 4, frame.Length);
        for (int offset = 0; offset < frame.Length; offset += ClientBase.BufferSize)
        {
            int count = Math.Min(ClientBase.BufferSize, frame.Length - offset);
            world.Sender.Feed(frame.AsSpan(offset, count));
            if (offset + count < frame.Length)
            {
                Assert.AreEqual(0, world.Sender.Sent.Count, "An incomplete maximum-sized ping was echoed.");
            }
        }

        Assert.AreEqual(1, world.Sender.Sent.Count);
        CollectionAssert.AreEqual(frame, world.Sender.Sent[0]);
    }

    [TestMethod]
    public void MalformedTellStringCannotBeForwardedAsTruncatedHtml()
    {
        using var world = new ChatWorld();
        byte[] malformed = TellFrame(RecipientId, "<a>");
        BinaryPrimitives.WriteUInt16BigEndian(malformed.AsSpan(8, 2), 1000);

        Assert.ThrowsExactly<EndOfStreamException>(() => world.Sender.Feed(malformed));

        Assert.AreEqual(0, world.Recipient.Sent.Count);
    }

    [TestMethod]
    public void PacketReaderRejectsShortByteReads()
    {
        byte[] body = { 1, 2 };
        using var reader = new PacketReader(ref body);

        Assert.ThrowsExactly<EndOfStreamException>(() => reader.ReadBytes(3));
    }

    [TestMethod]
    public void PacketReaderRejectsStringsWhoseDeclaredBytesAreMissing()
    {
        byte[] body = { 0, 5, (byte)'a', (byte)'b' };
        using var reader = new PacketReader(ref body);

        Assert.ThrowsExactly<EndOfStreamException>(() => reader.ReadString());
    }

    [TestMethod]
    public void PacketReaderPreservesExistingInvalidUtf8ReplacementBehavior()
    {
        byte[] body = { 0, 2, 0xC3, 0x28 };
        using var reader = new PacketReader(ref body);

        Assert.AreEqual(Encoding.UTF8.GetString(body, 2, 2), reader.ReadString());
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void PacketWriterRejectsAStringWhoseUtf8ByteCountExceedsUInt16(bool multibyte)
    {
        string text = multibyte ? new string('é', 32768) : new string('a', ushort.MaxValue + 1);
        Assert.AreEqual(ushort.MaxValue + 1, Encoding.UTF8.GetByteCount(text));
        var writer = new PacketWriter(30);

        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => writer.WriteString(text));
    }

    [TestMethod]
    public void PacketWriterRejectsAnOversizedBodyInsteadOfWrappingItsLength()
    {
        var writer = new PacketWriter(100);
        writer.WriteBytes(new byte[ushort.MaxValue + 1]);

        Assert.ThrowsExactly<InvalidOperationException>(() => writer.Finish());
    }

    [TestMethod]
    public void PacketWriterAcceptsTheExactUnsignedBodyLimit()
    {
        var writer = new PacketWriter(100);
        writer.WriteBytes(new byte[ushort.MaxValue]);

        byte[] frame = writer.Finish();

        Assert.AreEqual(ushort.MaxValue + 4, frame.Length);
        Assert.AreEqual(ushort.MaxValue, (int)BinaryPrimitives.ReadUInt16BigEndian(frame.AsSpan(2, 2)));
    }

    [TestMethod]
    public void PacketWriterAndReaderRoundTripTheLargestStringThatFitsAWholeBody()
    {
        string text = new('x', ushort.MaxValue - 2);
        var writer = new PacketWriter(100);
        writer.WriteString(text);
        byte[] frame = writer.Finish();
        using var reader = new PacketReader(ref frame);
        Assert.AreEqual((ushort)100, reader.ReadUInt16());
        Assert.AreEqual(ushort.MaxValue, reader.ReadUInt16());

        Assert.AreEqual(text, reader.ReadString());
    }

    [TestMethod]
    [DataRow(null)]
    [DataRow("")]
    public void PacketWriterPreservesTheExistingEmptyStringEncoding(string? text)
    {
        var writer = new PacketWriter(100);
        writer.WriteString(text!);

        CollectionAssert.AreEqual(new byte[] { 0, 100, 0, 3, 0, 1, 0 }, writer.Finish());
    }

    static string Html(int utf8Bytes)
    {
        const string prefix = "<a href=\"text://<font color='#00ff00'>!modules</font><br>";
        const string suffix = "\">Modules</a>";
        return prefix + new string('m', utf8Bytes - Encoding.UTF8.GetByteCount(prefix + suffix)) + suffix;
    }

    static byte[] TellFrame(uint characterId, string message, byte[]? blob = null)
    {
        byte[] text = Encoding.UTF8.GetBytes(message);
        blob ??= Array.Empty<byte>();
        byte[] body = new byte[8 + text.Length + blob.Length];
        BinaryPrimitives.WriteUInt32BigEndian(body.AsSpan(0, 4), characterId);
        BinaryPrimitives.WriteUInt16BigEndian(body.AsSpan(4, 2), checked((ushort)text.Length));
        text.CopyTo(body, 6);
        BinaryPrimitives.WriteUInt16BigEndian(body.AsSpan(6 + text.Length, 2), checked((ushort)blob.Length));
        blob.CopyTo(body, 8 + text.Length);
        return Frame(30, body);
    }

    static byte[] Frame(ushort opcode, byte[] body)
    {
        byte[] frame = new byte[body.Length + 4];
        BinaryPrimitives.WriteUInt16BigEndian(frame.AsSpan(0, 2), opcode);
        BinaryPrimitives.WriteUInt16BigEndian(frame.AsSpan(2, 2), checked((ushort)body.Length));
        body.CopyTo(frame, 4);
        return frame;
    }

    static byte[] Join(params byte[][] parts)
    {
        byte[] joined = new byte[parts.Sum(part => part.Length)];
        int offset = 0;
        foreach (byte[] part in parts)
        {
            part.CopyTo(joined, offset);
            offset += part.Length;
        }
        return joined;
    }

    static void AssertTells(ChatWorld world, params string[] messages)
    {
        Assert.AreEqual(messages.Length, world.Recipient.Sent.Count);
        for (int index = 0; index < messages.Length; index++)
        {
            byte[] actual = world.Recipient.Sent[index];
            Assert.AreEqual(actual.Length - 4, (int)BinaryPrimitives.ReadUInt16BigEndian(actual.AsSpan(2, 2)));
            CollectionAssert.AreEqual(TellFrame(SenderId, messages[index], new byte[] { 0 }), actual);
        }
    }

    sealed class ChatWorld : IDisposable
    {
        readonly ChatServer server;
        public readonly CapturingClient Sender;
        public readonly CapturingClient Recipient;

        public ChatWorld(Func<int, string?>? readOrganizationName = null)
        {
            server = readOrganizationName == null ? new ChatServer() : (ChatServer)Activator.CreateInstance(
                typeof(ChatServer), BindingFlags.Instance | BindingFlags.NonPublic, null, [readOrganizationName], null)!;
            Sender = Client(SenderId, "HelpbotFixture");
            Recipient = Client(RecipientId, "ReceiverFixture");
            Recipient.KnownClients.Add(SenderId);
        }

        public void OrganizationCommand(uint characterId, string command)
        {
            var packet = new WorldToService
            {
                Chat = new ChatEvent
                {
                    ToCharacter = new CharacterChatCommand { CharacterId = (int)characterId, Command = command }
                }
            };
            typeof(ChatServer).GetMethod("OnWorldMessage", BindingFlags.Instance | BindingFlags.NonPublic)!
                .Invoke(server, [packet]);
        }

        public int OrganizationChannelCount(int orgId)
            => server.Channels.Count(channel => channel.channelType == ChannelType.Organization && channel.ChannelId == (uint)orgId);

        public CapturingClient Replace(CapturingClient previous)
        {
            CapturingClient replacement = Client(previous.Character.CharacterId, previous.Character.characterName);
            server.OnClientDisconnect(previous, false);
            return replacement;
        }

        public CapturingClient Client(uint id, string name)
        {
            var client = new CapturingClient(server);
            // Character(0) avoids the normal DAO name lookup; the fixture supplies authenticated-state fields.
            client.Character.CharacterId = id;
            client.Character.characterName = name;
            bool registered = (bool)typeof(ChatServer).GetMethod("RegisterClient", BindingFlags.Instance | BindingFlags.NonPublic)!
                .Invoke(server, new object[] { client })!;
            Assert.IsTrue(registered);
            return client;
        }

        public void Dispose()
        {
            LftRegistry.Remove(SenderId);
            Sender.Dispose();
            Recipient.Dispose();
        }
    }

    sealed class CapturingClient : ChatClient
    {
        byte[] pending = Array.Empty<byte>();
        public readonly List<byte[]> Sent = new();
        public bool RetainPacketBuffers;
        public bool DisposeOnSend;
        public bool HasSocket => _tcpSock != null;
        public BufferSegment? ReceivedSegment;
        public ManualResetEventSlim? DisposedReceiveExited;

        public CapturingClient(ChatServer server, Socket? socket = null) : base(server)
        {
            if (socket != null)
            {
                _tcpSock.Dispose();
                _tcpSock = socket;
            }
        }

        protected override bool OnReceive(BufferSegment buffer)
        {
            ReceivedSegment = buffer;
            try { return base.OnReceive(buffer); }
            finally
            {
                if (!HasSocket)
                {
                    DisposedReceiveExited?.Set();
                }
            }
        }

        public override void Send(byte[] packet, int offset, int length)
        {
            if (RetainPacketBuffers)
            {
                Assert.AreEqual(0, offset);
                Assert.AreEqual(packet.Length, length);
                Sent.Add(packet);
            }
            else
            {
                Sent.Add(packet.AsSpan(offset, length).ToArray());
            }
            if (DisposeOnSend)
            {
                Dispose();
            }
        }

        public void FeedInReceiveSegments(byte[] wire)
        {
            for (int offset = 0; offset < wire.Length; offset += ClientBase.BufferSize)
            {
                Feed(wire.AsSpan(offset, Math.Min(ClientBase.BufferSize, wire.Length - offset)));
            }
        }

        public void Feed(ReadOnlySpan<byte> chunk, int segmentOffset = 0)
        {
            byte[] received = new byte[pending.Length + chunk.Length];
            pending.CopyTo(received, 0);
            chunk.CopyTo(received.AsSpan(pending.Length));
            byte[] storage = new byte[segmentOffset + received.Length];
            received.CopyTo(storage, segmentOffset);
            _offset = 0;
            _remainingLength = received.Length;
            bool consumed = OnReceive(BufferSegment.CreateSegment(storage, segmentOffset, received.Length));
            pending = consumed ? Array.Empty<byte>() : received.AsSpan(_offset, _remainingLength).ToArray();
            if (consumed)
            {
                _remainingLength = 0;
            }
        }
    }
}
