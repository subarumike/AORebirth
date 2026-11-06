using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Cell.Core;
using ChatEngine.Packets;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ZoneEngine_New.Tests;

[TestClass]
[DoNotParallelize]
[TestCategory("ClientSend")]
public sealed class ClientSendTests
{
    [TestMethod, Timeout(10000)]
    public void OnlyOneWriteIsInFlightAndPartialCompletionsRetainTheFifoTail()
    {
        using var world = new SendWorld();
        byte[] first = { 1, 2, 3, 4, 5, 6 };
        byte[] second = { 7, 8, 9 };
        long globalBefore = ClientBase.TotalBytesSent;

        world.Client.Send(first);
        world.Client.Send(second);

        Assert.AreEqual(1, world.Client.Calls.Count);
        Assert.AreEqual(1, world.Client.Pending.Count);
        Assert.AreEqual(0u, world.Client.SentBytes);
        world.Client.CompleteNext(2);
        Assert.AreEqual(2u, world.Client.SentBytes);
        Assert.AreEqual(2, world.Client.Calls.Count);
        CollectionAssert.AreEqual(first[2..], world.Client.Calls[1].Bytes);
        world.Client.CompleteNext(4);
        Assert.AreEqual(6u, world.Client.SentBytes);
        Assert.AreEqual(3, world.Client.Calls.Count);
        CollectionAssert.AreEqual(second, world.Client.Calls[2].Bytes);
        world.Client.CompleteNext(3);

        CollectionAssert.AreEqual(first.Concat(second).ToArray(), world.Client.Accepted.ToArray());
        Assert.AreEqual(9u, world.Client.SentBytes);
        Assert.AreEqual(9L, ClientBase.TotalBytesSent - globalBefore);
        Assert.AreEqual(0, world.Client.Pending.Count);
    }

    [TestMethod, Timeout(10000)]
    public async Task ConcurrentProducerDuringPartialCompletionKeepsOneOrderedWriteInFlight()
    {
        using var world = new SendWorld();
        using var tailEntered = new ManualResetEventSlim();
        using var releaseTail = new ManualResetEventSlim();
        using var producerAttempted = new ManualResetEventSlim();
        byte[] first = { 1, 2, 3, 4, 5, 6 };
        byte[] second = { 7, 8, 9 };
        Task? completion = null;
        Task? producer = null;
        world.Client.OnSendStarted = call =>
        {
            if (call != 2)
                return;
            tailEntered.Set();
            Assert.IsTrue(releaseTail.Wait(TimeSpan.FromSeconds(5)), "The controlled partial-send barrier was not released.");
        };
        try
        {
            world.Client.Send(first);
            completion = Task.Run(() => world.Client.CompleteNext(2));
            Assert.IsTrue(tailEntered.Wait(TimeSpan.FromSeconds(2)), "Completion did not start the remaining first-packet bytes.");
            producer = Task.Run(() =>
            {
                producerAttempted.Set();
                world.Client.Send(second);
            });
            Assert.IsTrue(producerAttempted.Wait(TimeSpan.FromSeconds(2)), "The concurrent producer did not attempt its write.");

            releaseTail.Set();
            await Task.WhenAll(completion, producer).WaitAsync(TimeSpan.FromSeconds(2));
            Assert.AreEqual(2, world.Client.Calls.Count);
            Assert.AreEqual(1, world.Client.Pending.Count);
            CollectionAssert.AreEqual(first[2..], world.Client.Calls[1].Bytes);
            Assert.AreEqual(2u, world.Client.SentBytes);
            world.Client.CompleteAll();

            CollectionAssert.AreEqual(first.Concat(second).ToArray(), world.Client.Accepted.ToArray());
            Assert.AreEqual(1, world.Client.MaximumPendingCount);
            Assert.AreEqual(9u, world.Client.SentBytes);
            Assert.AreEqual(3, world.Client.Calls.Count);
            Assert.AreEqual(0, world.Client.Pending.Count);
        }
        finally
        {
            releaseTail.Set();
            Task[] workers = new[] { completion, producer }.OfType<Task>().ToArray();
            await Task.WhenAll(workers).WaitAsync(TimeSpan.FromSeconds(2));
        }
    }

    [TestMethod, Timeout(10000)]
    public void SevenKilobytesOfSynchronousThreeByteCompletionsUseAnIterativePump()
    {
        using var world = new SendWorld();
        world.Client.Mode = CompletionMode.Synchronous;
        world.Client.MaximumCompletionBytes = 3;
        byte[] payload = Encoding.UTF8.GetBytes(Html(7000));

        world.Client.Send(payload);

        CollectionAssert.AreEqual(payload, world.Client.Accepted.ToArray());
        Assert.AreEqual((uint)payload.Length, world.Client.SentBytes);
        Assert.AreEqual((payload.Length + 2) / 3, world.Client.Calls.Count);
        Assert.AreEqual(1, world.Client.MaximumDepth);
    }

    [TestMethod, Timeout(10000)]
    public void CompletionBeforeSendAsyncReturnsDoesNotRecurseLoseOrRepeatBytes()
    {
        using var world = new SendWorld();
        world.Client.Mode = CompletionMode.Reentrant;
        world.Client.MaximumCompletionBytes = 3;
        byte[] first = Encoding.UTF8.GetBytes(Html(7000));
        byte[] second = Encoding.UTF8.GetBytes("tail");

        world.Client.Send(first);
        world.Client.Send(second);

        CollectionAssert.AreEqual(first.Concat(second).ToArray(), world.Client.Accepted.ToArray());
        Assert.AreEqual((uint)(first.Length + second.Length), world.Client.SentBytes);
        Assert.AreEqual(1, world.Client.MaximumDepth);
        Assert.AreEqual(0, world.Client.Pending.Count);
    }

    [TestMethod, Timeout(10000)]
    [DataRow(SocketError.ConnectionReset)]
    [DataRow(SocketError.Success)]
    public void SocketErrorOrZeroCompletionFailsClosedWithoutSendingQueuedData(SocketError error)
    {
        using var world = new SendWorld();
        long globalBefore = ClientBase.TotalBytesSent;
        world.Client.Send(new byte[] { 1, 2, 3 });
        world.Client.Send(new byte[] { 4, 5, 6 });

        world.Client.CompleteNext(0, error);

        Assert.IsFalse(world.Client.IsConnected);
        Assert.AreEqual(1, world.Server.Disconnects);
        Assert.AreEqual(1, world.Client.Calls.Count);
        Assert.AreEqual(0, world.Client.Accepted.Count);
        Assert.AreEqual(0u, world.Client.SentBytes);
        Assert.AreEqual(0L, ClientBase.TotalBytesSent - globalBefore);
        world.Client.Send(new byte[] { 7 });
        Assert.AreEqual(1, world.Client.Calls.Count);
    }

    [TestMethod, Timeout(10000)]
    public void ThrownSocketErrorFailsClosedAndDoesNotClaimUnsentBytes()
    {
        using var world = new SendWorld();
        world.Client.ThrowOnCall = 1;
        long globalBefore = ClientBase.TotalBytesSent;

        AllowSocketException(() => world.Client.Send(new byte[] { 1, 2, 3 }));

        Assert.IsFalse(world.Client.IsConnected);
        Assert.AreEqual(1, world.Server.Disconnects);
        Assert.AreEqual(0u, world.Client.SentBytes);
        Assert.AreEqual(0L, ClientBase.TotalBytesSent - globalBefore);
        world.Client.Send(new byte[] { 4 });
        Assert.AreEqual(1, world.Client.Calls.Count);
        Assert.AreEqual(0, world.Client.Pending.Count);
    }

    [TestMethod, Timeout(10000)]
    public void ThrowWhileStartingAQueuedWritePreservesEarlierCompletedBytesOnly()
    {
        using var world = new SendWorld();
        world.Client.ThrowOnCall = 2;
        byte[] first = { 1, 2, 3 };
        world.Client.Send(first);
        AllowSocketException(() => world.Client.Send(new byte[] { 4, 5 }));
        AllowSocketException(() => world.Client.Send(new byte[] { 6, 7 }));

        AllowSocketException(() => world.Client.CompleteNext(first.Length));

        Assert.IsFalse(world.Client.IsConnected);
        Assert.AreEqual(1, world.Server.Disconnects);
        Assert.AreEqual(2, world.Client.Calls.Count);
        CollectionAssert.AreEqual(first, world.Client.Accepted.ToArray());
        Assert.AreEqual((uint)first.Length, world.Client.SentBytes);
    }

    [TestMethod, Timeout(10000)]
    public void DisposeRetainsInFlightOwnershipUntilItsCallbackThenClearsContext()
    {
        using var world = new SendWorld();
        world.Client.Send(new byte[] { 1, 2, 3 });
        world.Client.Send(new byte[] { 4, 5 });
        SocketAsyncEventArgs pending = world.Client.Pending[0];
        Assert.AreSame(world.Client, pending.UserToken);

        world.Client.Dispose();

        Assert.AreSame(world.Client, pending.UserToken);
        Assert.IsTrue(pending.Count > 0);
        world.Client.CompleteNext(0, SocketError.OperationAborted);
        Assert.IsNull(pending.UserToken);
        Assert.IsNull(pending.Buffer);
        Assert.IsTrue(pending.MemoryBuffer.IsEmpty);
        Assert.AreEqual(0, pending.Count);
        Assert.AreEqual(1, world.Client.Calls.Count);
        Assert.AreEqual(0u, world.Client.SentBytes);
    }

    [TestMethod, Timeout(10000)]
    public void SuccessfulCompletionClearsOwnershipAndBufferBeforePoolReuse()
    {
        using var world = new SendWorld();
        world.Client.Send(new byte[] { 1, 2, 3 });
        SocketAsyncEventArgs pending = world.Client.Pending[0];

        world.Client.CompleteNext(3);

        Assert.IsNull(pending.UserToken);
        Assert.IsNull(pending.Buffer);
        Assert.IsTrue(pending.MemoryBuffer.IsEmpty);
        Assert.AreEqual(0, pending.Count);
        Assert.AreEqual(3u, world.Client.SentBytes);
    }

    [TestMethod, Timeout(10000)]
    public void SendCopyKeepsItsSnapshotAfterTheCallerMutatesTheOriginal()
    {
        using var world = new SendWorld();
        byte[] prefix = { 1 };
        byte[] payload = Encoding.UTF8.GetBytes(Html(7000));
        byte[] expected = prefix.Concat(payload).ToArray();
        world.Client.Send(prefix);
        world.Client.SendCopy(payload);
        Array.Fill(payload, (byte)0xCC);

        world.Client.CompleteAll();

        CollectionAssert.AreEqual(expected, world.Client.Accepted.ToArray());
    }

    [TestMethod, Timeout(10000)]
    public void QueuedArrayRangeSendsOnlyTheRequestedBytes()
    {
        using var world = new SendWorld();
        byte[] source = { 90, 91, 1, 2, 3, 4, 92, 93 };
        world.Client.Send(new byte[] { 0 });
        world.Client.Send(source, 2, 4);
        // Raw Send borrows the requested range; only SendCopy promises a snapshot.
        source[0] = source[1] = source[6] = source[7] = 0xCC;

        world.Client.CompleteAll();

        CollectionAssert.AreEqual(new byte[] { 0, 1, 2, 3, 4 }, world.Client.Accepted.ToArray());
    }

    [TestMethod, Timeout(10000)]
    public void QueuedPooledSegmentKeepsItsBoundedSnapshotAfterReleaseAndReuse()
    {
        using var world = new SendWorld();
        var pool = new BufferManager(1, 64);
        BufferSegment segment = pool.CheckOut();
        byte[] payload = { 1, 2, 3, 4, 5 };
        payload.CopyTo(segment.Buffer.Array, segment.Offset);
        world.Client.Send(new byte[] { 0 });
        try
        {
            world.Client.Send(segment, payload.Length);
        }
        finally
        {
            segment.DecrementUsage();
        }
        BufferSegment reused = pool.CheckOut();
        try
        {
            Assert.AreSame(segment, reused);
            Array.Fill(reused.Buffer.Array, (byte)0xCC, reused.Offset, reused.Length);
        }
        finally
        {
            reused.DecrementUsage();
        }

        world.Client.CompleteAll();

        CollectionAssert.AreEqual(new byte[] { 0, 1, 2, 3, 4, 5 }, world.Client.Accepted.ToArray());
    }

    [TestMethod, Timeout(10000)]
    public void SegmentLengthCannotReadBytesOutsideThatSegmentsBoundary()
    {
        using var world = new SendWorld();
        BufferSegment segment = BufferSegment.CreateSegment(new byte[128], 17, 32);

        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => world.Client.Send(segment, 33));

        Assert.AreEqual(0, world.Client.Calls.Count);
    }

    [TestMethod, Timeout(10000)]
    public async Task RealLoopbackReceivesAllHtmlFramesInSubmissionOrder()
    {
        using var pair = new LoopbackPair();
        using var server = new RecordingServer();
        using var client = new RealClient(server, pair.Sender);
        byte[][] packets =
        {
            MsgPrivateGroup.Create(91001, Html(7000), string.Empty),
            MsgPrivateGroup.Create(91001, "second:" + Html(7000), string.Empty),
            MsgPrivateGroup.Create(91001, "third:é漢🙂" + Html(7000), string.Empty)
        };
        byte[] expected = packets.SelectMany(packet => packet).ToArray();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        foreach (byte[] packet in packets)
        {
            client.Send(packet);
        }
        byte[] actual = new byte[expected.Length];
        int received = 0;
        while (received < actual.Length)
        {
            int count = await pair.Peer.ReceiveAsync(actual.AsMemory(received), SocketFlags.None, timeout.Token);
            Assert.IsTrue(count > 0, "Connection ended before all HTML bytes arrived.");
            received += count;
        }

        CollectionAssert.AreEqual(expected, actual);
        Assert.AreEqual(0, server.Disconnects);
    }

    [TestMethod, Timeout(10000)]
    public void DisposeCannotRecycleAReceiveSegmentWhileItsHandlerStillOwnsTheLease()
    {
        using var pair = new LoopbackPair();
        using var server = new RecordingServer();
        using var entered = new ManualResetEventSlim();
        using var release = new ManualResetEventSlim();
        using var exited = new ManualResetEventSlim();
        using var client = new ReceiveLeaseClient(server, pair.Sender, entered, release, exited);
        bool handlerEntered = false;
        BufferSegment? held = null;
        try
        {
            client.BeginReceive();
            Assert.AreEqual(1, pair.Peer.Send(new byte[] { 0x41 }));
            handlerEntered = entered.Wait(TimeSpan.FromSeconds(2));
            Assert.IsTrue(handlerEntered, "The receive callback did not enter its controlled handler.");
            held = client.Held!;

            client.Dispose();

            Assert.AreEqual(1, held.Uses, "Dispose recycled a segment still used by the receive callback.");
        }
        finally
        {
            release.Set();
            client.Dispose();
            if (handlerEntered)
            {
                Assert.IsTrue(exited.Wait(TimeSpan.FromSeconds(2)), "The receive handler did not leave its barrier.");
                Assert.IsTrue(SpinWait.SpinUntil(() => held!.Uses == 0, TimeSpan.FromSeconds(2)),
                    "The completed receive operation did not release its segment lease.");
                Assert.IsFalse(client.BarrierTimedOut);
            }
        }
    }

    static string Html(int length)
    {
        const string prefix = "<a href=\"text://<font color='#00ff00'>modules</font><br>";
        const string suffix = "\">Modules</a>";
        return prefix + new string('m', length - prefix.Length - suffix.Length) + suffix;
    }

    static void AllowSocketException(Action action)
    {
        try { action(); }
        catch (SocketException) { }
    }

    enum CompletionMode { Asynchronous, Synchronous, Reentrant }

    sealed record SendCall(SocketAsyncEventArgs Args, byte[] Bytes);

    sealed class ControlledClient : ClientBase
    {
        static readonly MethodInfo SetResults = typeof(SocketAsyncEventArgs).GetMethod("SetResults",
            BindingFlags.Instance | BindingFlags.NonPublic, null,
            new[] { typeof(SocketError), typeof(int), typeof(SocketFlags) }, null)!;
        static readonly MethodInfo OnCompleted = typeof(SocketAsyncEventArgs).GetMethod("OnCompleted",
            BindingFlags.Instance | BindingFlags.NonPublic, null, new[] { typeof(SocketAsyncEventArgs) }, null)!;
        public readonly List<SendCall> Calls = new();
        public readonly List<SocketAsyncEventArgs> Pending = new();
        public readonly List<byte> Accepted = new();
        public CompletionMode Mode;
        public int MaximumCompletionBytes = int.MaxValue;
        public int ThrowOnCall;
        public int MaximumDepth;
        public int MaximumPendingCount;
        public Action<int>? OnSendStarted;
        int depth;

        public ControlledClient(RecordingServer server, Socket socket) : base(server)
        {
            _tcpSock.Dispose();
            _tcpSock = socket;
        }

        protected override bool OnReceive(BufferSegment buffer) => true;

        protected override bool SendAsync(SocketAsyncEventArgs args)
        {
            depth++;
            MaximumDepth = Math.Max(MaximumDepth, depth);
            try
            {
                if (depth > 32) throw new InvalidOperationException("Send pump recursed instead of iterating.");
                Calls.Add(new SendCall(args, Bytes(args).ToArray()));
                OnSendStarted?.Invoke(Calls.Count);
                if (Calls.Count == ThrowOnCall) throw new SocketException((int)SocketError.ConnectionReset);
                if (Mode == CompletionMode.Asynchronous)
                {
                    Pending.Add(args);
                    MaximumPendingCount = Math.Max(MaximumPendingCount, Pending.Count);
                    return true;
                }
                int count = Math.Min(MaximumCompletionBytes, args.Count);
                Accept(args, count);
                Results(args, SocketError.Success, count);
                if (Mode == CompletionMode.Reentrant)
                {
                    Raise(args);
                    return true;
                }
                return false;
            }
            finally { depth--; }
        }

        public void CompleteNext(int count, SocketError error = SocketError.Success)
        {
            Assert.IsTrue(Pending.Count > 0, "No controlled async write is pending.");
            SocketAsyncEventArgs args = Pending[0];
            Pending.RemoveAt(0);
            if (error == SocketError.Success && count > 0)
            {
                Accept(args, count);
            }
            Results(args, error, count);
            Raise(args);
        }

        public void CompleteAll()
        {
            int completions = 0;
            while (Pending.Count > 0)
            {
                if (++completions > 64) throw new InvalidOperationException("Unbounded send completion loop.");
                CompleteNext(Pending[0].Count);
            }
        }

        public void AbortPending()
        {
            int completions = 0;
            Exception? failure = null;
            while (Pending.Count > 0)
            {
                if (++completions > 64) throw new InvalidOperationException("Unbounded cleanup completion loop.");
                try { CompleteNext(0, SocketError.OperationAborted); }
                catch (Exception exception) { failure ??= exception; }
            }
            if (failure != null) ExceptionDispatchInfo.Capture(failure).Throw();
        }

        void Accept(SocketAsyncEventArgs args, int count)
        {
            Assert.IsTrue(count <= args.Count);
            Accepted.AddRange(Bytes(args)[..count].ToArray());
        }

        static ReadOnlySpan<byte> Bytes(SocketAsyncEventArgs args)
            => args.Buffer != null ? args.Buffer.AsSpan(args.Offset, args.Count) : args.MemoryBuffer.Span;

        static void Results(SocketAsyncEventArgs args, SocketError error, int count)
            => SetResults.Invoke(args, new object[] { error, count, SocketFlags.None });

        static void Raise(SocketAsyncEventArgs args)
        {
            try { OnCompleted.Invoke(args, new object[] { args }); }
            catch (TargetInvocationException exception) when (exception.InnerException != null)
            {
                ExceptionDispatchInfo.Capture(exception.InnerException).Throw();
            }
        }
    }

    sealed class RealClient : ClientBase
    {
        public RealClient(RecordingServer server, Socket socket) : base(server)
        {
            _tcpSock.Dispose();
            _tcpSock = socket;
        }
        protected override bool OnReceive(BufferSegment buffer) => true;
    }

    sealed class ReceiveLeaseClient : ClientBase
    {
        readonly ManualResetEventSlim entered;
        readonly ManualResetEventSlim release;
        readonly ManualResetEventSlim exited;
        public BufferSegment? Held;
        public bool BarrierTimedOut;

        public ReceiveLeaseClient(RecordingServer server, Socket socket, ManualResetEventSlim entered,
            ManualResetEventSlim release, ManualResetEventSlim exited) : base(server)
        {
            _tcpSock.Dispose();
            _tcpSock = socket;
            this.entered = entered;
            this.release = release;
            this.exited = exited;
        }

        protected override bool OnReceive(BufferSegment buffer)
        {
            Held = buffer;
            entered.Set();
            try
            {
                BarrierTimedOut = !release.Wait(TimeSpan.FromSeconds(5));
                return true;
            }
            finally { exited.Set(); }
        }
    }

    sealed class SendWorld : IDisposable
    {
        readonly LoopbackPair pair = new();
        public readonly RecordingServer Server = new();
        public readonly ControlledClient Client;

        public SendWorld() => Client = new ControlledClient(Server, pair.Sender);

        public void Dispose()
        {
            try { Client.AbortPending(); }
            finally
            {
                Client.Dispose();
                pair.Dispose();
                Server.Dispose();
            }
        }
    }

    sealed class RecordingServer : ServerBase
    {
        public int Disconnects;
        protected override IClient CreateClient(IPAddress address) => throw new NotSupportedException();
        protected override void OnReceiveUDP(int numBytes, byte[] buf, IPEndPoint ip) => throw new NotSupportedException();
        protected override void OnSendTo(IPEndPoint clientIP, int numBytes) => throw new NotSupportedException();
        protected override void OnClientDisconnected(IClient client, bool forced)
        {
            Disconnects++;
            client.Dispose();
        }
    }

    internal sealed class LoopbackPair : IDisposable
    {
        public readonly Socket Sender = new(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        public readonly Socket Peer;

        public LoopbackPair()
        {
            using var listener = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            listener.Bind(new IPEndPoint(IPAddress.Loopback, 0));
            listener.Listen(1);
            Task<Socket> accept = listener.AcceptAsync(timeout.Token).AsTask();
            Sender.ConnectAsync(listener.LocalEndPoint!, timeout.Token).GetAwaiter().GetResult();
            Peer = accept.GetAwaiter().GetResult();
            Sender.NoDelay = Peer.NoDelay = true;
        }

        public void Dispose()
        {
            Sender.Dispose();
            Peer.Dispose();
        }
    }
}
