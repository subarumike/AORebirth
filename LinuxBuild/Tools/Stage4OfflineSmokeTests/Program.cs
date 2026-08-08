using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using AORebirth.Communication;
using AORebirth.Communication.ISComV2Client;
using AORebirth.Communication.ISComV2Server;
using AORebirth.Communication.Messages;

using MemBus;
using MemBus.Configurators;

using MsgPack.Serialization;

internal static class Program
{
    private static readonly TimeSpan NetworkTimeout = TimeSpan.FromSeconds(3);
    private static readonly TimeSpan CallbackOverlapProbe = TimeSpan.FromMilliseconds(150);

    private static int checks;

    private static int Main(string[] args)
    {
        try
        {
            Run("MemBus compatibility identity and inert API", VerifyMemBusCompatibility);
            Run("Communication identity and defaults", VerifyCommunicationIdentityAndDefaults);
            Run("DynamicMessage pure roundtrip", VerifyDynamicMessageRoundTrip);
            Run("Disconnected send is a bounded no-op", VerifyDisconnectedSend);
            Run("IPv4 loopback framing and keepalive", VerifyLoopbackFramingAndKeepAlive);

            string mode = args.Length == 0 ? "source" : args[0];
            Console.WriteLine("PASS: Stage 4 offline smoke ({0}, {1} checks)", mode, checks);
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine("FAIL: Stage 4 offline smoke: {0}", ex);
            return 1;
        }
    }

    private static void Run(string name, Action test)
    {
        test();
        checks++;
        Console.WriteLine("PASS: {0}", name);
    }

    private static void VerifyMemBusCompatibility()
    {
        VerifyAssemblyIdentity(typeof(IBus), "MemBus", new Version(2, 0, 2, 0));
        Equal(0, typeof(IBus).GetMethods().Length, "The compatibility IBus must remain inert.");
        Equal(0, typeof(IBus).GetProperties().Length, "The compatibility IBus must expose no properties.");
        Equal(0, typeof(IBus).GetEvents().Length, "The compatibility IBus must expose no callbacks.");

        IBus bus = BusSetup.StartWith<AsyncConfiguration>().Construct();
        NotNull(bus, "Construct must return an inert bus instance.");

        ISComV2ClientHandler handler = new ISComV2ClientHandler(null, bus, 37);
        try
        {
            Equal(37, handler.GetID(), "The public handler must retain its client number.");

            FieldInfo busField = typeof(ISComV2ClientHandler).GetField(
                "bus",
                BindingFlags.Instance | BindingFlags.NonPublic);
            NotNull(busField, "The handler bus field must remain available.");
            Same(bus, busField.GetValue(handler), "The handler must retain the exact constructed bus reference.");

            FieldInfo callbackField = typeof(ISComV2ClientHandler).GetField(
                "DataReceived",
                BindingFlags.Instance | BindingFlags.NonPublic);
            NotNull(callbackField, "The handler callback backing field must be discoverable.");
            Null(callbackField.GetValue(handler), "Construction must not install or invoke callbacks.");
        }
        finally
        {
            handler.Dispose();
            handler.Dispose();
        }
    }

    private static void VerifyCommunicationIdentityAndDefaults()
    {
        VerifyAssemblyIdentity(
            typeof(ISComV2ClientBase),
            "AORebirth.Communication",
            new Version(1, 0, 0, 0));

        LoopbackClient client = new LoopbackClient();
        try
        {
            False(client.IsConnected, "A new Communication client must be disconnected.");
            Equal(0u, client.ReceivedBytes, "A new client must have zero received bytes.");
            Equal(0u, client.SentBytes, "A new client must have zero sent bytes.");
            NotNull(client.TcpSocket, "A new client must own an IPv4 socket.");
            Equal(AddressFamily.InterNetwork, client.TcpSocket.AddressFamily, "The default socket must be IPv4.");
            False(client.TcpSocket.Connected, "The default socket must not be connected.");
            Equal("<disconnected client>", client.ToString(), "The disconnected display value must remain stable.");

            DynamicMessage dynamicMessage = new DynamicMessage();
            Null(dynamicMessage.DataObject, "DynamicMessage data must default to null.");
            Null(dynamicMessage.TypeName, "DynamicMessage type name must default to null.");
            Equal("0", new Ping().dummy, "Ping's legacy default value must remain stable.");
            Null(new OnDataReceivedArgs().dataBytes, "Received-data arguments must default to null data.");
        }
        finally
        {
            client.Dispose();
            client.Dispose();
        }
    }

    private static void VerifyDynamicMessageRoundTrip()
    {
        Ping payload = new Ping { dummy = "stage4-roundtrip" };
        DynamicMessage input = new DynamicMessage { DataObject = payload };
        Equal(typeof(Ping).ToString(), input.TypeName, "DynamicMessage must capture the payload type name.");

        MessagePackSerializer<DynamicMessage> serializer = MessagePackSerializer.Create<DynamicMessage>();
        byte[] packed = serializer.PackSingleObject(input);
        True(packed.Length > 0, "DynamicMessage serialization must produce bytes.");

        DynamicMessage output = serializer.UnpackSingleObject(packed);
        NotNull(output, "DynamicMessage deserialization must return an object.");
        Equal(typeof(Ping).ToString(), output.TypeName, "DynamicMessage must preserve its type name.");

        Ping roundTrip = output.DataObject as Ping;
        NotNull(roundTrip, "DynamicMessage must resolve Ping from the Communication assembly.");
        Equal(payload.dummy, roundTrip.dummy, "DynamicMessage must preserve its payload.");
    }

    private static void VerifyDisconnectedSend()
    {
        LoopbackClient client = new LoopbackClient();
        try
        {
            client.Send(new byte[] { 0x01, 0x02, 0x03 });
            Equal(0u, client.SentBytes, "Disconnected Send must remain a no-op.");
            False(client.IsConnected, "Disconnected Send must not create a connection.");
        }
        finally
        {
            CompleteWithin(
                Task.Run(
                    delegate
                    {
                        client.Dispose();
                        client.Dispose();
                    }),
                NetworkTimeout,
                "Disconnected idempotent disposal timed out.");
        }
    }

    private static void VerifyLoopbackFramingAndKeepAlive()
    {
        TcpListener listener = new TcpListener(IPAddress.Loopback, 0);
        Socket serverSocket = null;
        LoopbackClient client = new LoopbackClient();
        ManualResetEventSlim firstCallbackEntered = new ManualResetEventSlim(false);
        ManualResetEventSlim releaseFirstCallback = new ManualResetEventSlim(false);
        ManualResetEventSlim secondCallbackCompleted = new ManualResetEventSlim(false);
        object callbackGate = new object();
        List<byte[]> callbackPayloads = new List<byte[]>();
        Exception callbackFailure = null;
        int activeCallbacks = 0;

        client.ReceivedData += delegate(object sender, OnDataReceivedArgs e)
        {
            int callbackNumber = 0;
            bool signalSecondCompleted = false;

            try
            {
                if (Interlocked.Increment(ref activeCallbacks) != 1)
                {
                    RecordCallbackFailure(
                        ref callbackFailure,
                        new InvalidOperationException("ReceivedData callbacks overlapped."));
                }

                lock (callbackGate)
                {
                    callbackPayloads.Add(e.dataBytes);
                    callbackNumber = callbackPayloads.Count;
                }

                if (callbackNumber == 1)
                {
                    firstCallbackEntered.Set();
                    if (!releaseFirstCallback.Wait(NetworkTimeout))
                    {
                        RecordCallbackFailure(
                            ref callbackFailure,
                            new TimeoutException("The first callback was not released in time."));
                    }
                }
                else if (callbackNumber == 2)
                {
                    signalSecondCompleted = true;
                }
                else
                {
                    RecordCallbackFailure(
                        ref callbackFailure,
                        new InvalidOperationException("Unexpected callback count: " + callbackNumber));
                }
            }
            catch (Exception ex)
            {
                RecordCallbackFailure(ref callbackFailure, ex);
                signalSecondCompleted = true;
            }
            finally
            {
                Interlocked.Decrement(ref activeCallbacks);
                if (signalSecondCompleted)
                {
                    secondCallbackCompleted.Set();
                }
            }
        };

        try
        {
            listener.Start(1);
            IPEndPoint boundEndpoint = (IPEndPoint)listener.LocalEndpoint;
            Equal(IPAddress.Loopback, boundEndpoint.Address, "The listener must bind only to IPv4 loopback.");
            True(boundEndpoint.Port > 0, "The listener must use an ephemeral port.");

            Task<Socket> acceptTask = listener.AcceptSocketAsync();
            CompleteWithin(
                Task.Run(delegate { client.Connect(IPAddress.Loopback, boundEndpoint.Port); }),
                NetworkTimeout,
                "Loopback Connect timed out.");
            serverSocket = CompleteWithin(acceptTask, NetworkTimeout, "Loopback accept timed out.");
            listener.Stop();

            serverSocket.SendTimeout = (int)NetworkTimeout.TotalMilliseconds;
            serverSocket.ReceiveTimeout = (int)NetworkTimeout.TotalMilliseconds;

            True(client.IsConnected, "The client must report the loopback connection.");
            True(client.TcpSocket.Connected, "The client socket must be connected.");
            True(client.TcpSocket.NoDelay, "The client socket must enable NoDelay.");
            Equal(
                1,
                Convert.ToInt32(
                    client.TcpSocket.GetSocketOption(SocketOptionLevel.Socket, SocketOptionName.KeepAlive)),
                "The client socket must enable KeepAlive.");

            byte[] firstInboundPayload = Encoding.ASCII.GetBytes("first-inbound");
            byte[] secondInboundPayload = Encoding.ASCII.GetBytes("second-inbound");
            byte[] inboundFrames = Concat(BuildFrame(firstInboundPayload), BuildFrame(secondInboundPayload));
            SendAll(serverSocket, inboundFrames);

            True(firstCallbackEntered.Wait(NetworkTimeout), "The first ReceivedData callback timed out.");
            False(
                secondCallbackCompleted.Wait(CallbackOverlapProbe),
                "The second callback must not overtake a blocked first callback.");
            releaseFirstCallback.Set();
            True(secondCallbackCompleted.Wait(NetworkTimeout), "The second ReceivedData callback timed out.");

            Exception observedCallbackFailure = Volatile.Read(ref callbackFailure);
            if (observedCallbackFailure != null)
            {
                throw new InvalidOperationException("ReceivedData callback validation failed.", observedCallbackFailure);
            }

            lock (callbackGate)
            {
                Equal(2, callbackPayloads.Count, "Exactly two callback payloads must be received.");
                SequenceEqual(firstInboundPayload, callbackPayloads[0], "The first callback payload must remain FIFO.");
                SequenceEqual(secondInboundPayload, callbackPayloads[1], "The second callback payload must remain FIFO.");
            }

            Equal((uint)inboundFrames.Length, client.ReceivedBytes, "Received byte accounting must include exact frames.");

            byte[] firstOutboundFrame = BuildFrame(Encoding.ASCII.GetBytes("first-outbound"));
            byte[] secondOutboundFrame = BuildFrame(Encoding.ASCII.GetBytes("second-outbound"));

            client.Send(firstOutboundFrame);
            SequenceEqual(
                firstOutboundFrame,
                ReceiveExact(serverSocket, firstOutboundFrame.Length),
                "The first outbound frame must arrive exactly.");

            client.Send(secondOutboundFrame);
            SequenceEqual(
                secondOutboundFrame,
                ReceiveExact(serverSocket, secondOutboundFrame.Length),
                "The second outbound frame must arrive exactly and sequentially.");

            Equal(
                (uint)(firstOutboundFrame.Length + secondOutboundFrame.Length),
                client.SentBytes,
                "Sent byte accounting must include the exact sequential frames.");

            CompleteWithin(
                Task.Run(
                    delegate
                    {
                        client.Dispose();
                        client.Dispose();
                    }),
                NetworkTimeout,
                "Connected idempotent disposal timed out.");
        }
        finally
        {
            releaseFirstCallback.Set();
            CompleteWithin(
                Task.Run(
                    delegate
                    {
                        client.Dispose();
                        client.Dispose();
                    }),
                NetworkTimeout,
                "Loopback cleanup disposal timed out.");

            if (serverSocket != null)
            {
                serverSocket.Dispose();
            }

            listener.Stop();
            firstCallbackEntered.Dispose();
            releaseFirstCallback.Dispose();
            secondCallbackCompleted.Dispose();
        }
    }

    private static byte[] BuildFrame(byte[] payload)
    {
        byte[] frame = new byte[8 + payload.Length];
        BitConverter.GetBytes(0x00FF55AA).CopyTo(frame, 0);
        BitConverter.GetBytes(payload.Length).CopyTo(frame, 4);
        Buffer.BlockCopy(payload, 0, frame, 8, payload.Length);
        return frame;
    }

    private static byte[] Concat(byte[] first, byte[] second)
    {
        byte[] result = new byte[first.Length + second.Length];
        Buffer.BlockCopy(first, 0, result, 0, first.Length);
        Buffer.BlockCopy(second, 0, result, first.Length, second.Length);
        return result;
    }

    private static void SendAll(Socket socket, byte[] data)
    {
        int offset = 0;
        while (offset < data.Length)
        {
            int sent = socket.Send(data, offset, data.Length - offset, SocketFlags.None);
            if (sent <= 0)
            {
                throw new IOException("Loopback send returned zero bytes.");
            }

            offset += sent;
        }
    }

    private static byte[] ReceiveExact(Socket socket, int length)
    {
        byte[] result = new byte[length];
        int offset = 0;
        while (offset < result.Length)
        {
            int received = socket.Receive(result, offset, result.Length - offset, SocketFlags.None);
            if (received <= 0)
            {
                throw new EndOfStreamException("Loopback receive ended before the exact frame arrived.");
            }

            offset += received;
        }

        return result;
    }

    private static void VerifyAssemblyIdentity(Type marker, string expectedName, Version expectedVersion)
    {
        AssemblyName identity = marker.Assembly.GetName();
        Equal(expectedName, identity.Name, "Assembly simple name mismatch.");
        Equal(expectedVersion, identity.Version, "Assembly version mismatch for " + expectedName + ".");
        True(string.IsNullOrEmpty(identity.CultureName), expectedName + " must remain culture-neutral.");

        byte[] token = identity.GetPublicKeyToken();
        True(token == null || token.Length == 0, expectedName + " must remain unsigned.");
    }

    private static void RecordCallbackFailure(ref Exception target, Exception failure)
    {
        Interlocked.CompareExchange(ref target, failure, null);
    }

    private static void CompleteWithin(Task task, TimeSpan timeout, string timeoutMessage)
    {
        int completed = Task.WaitAny(new[] { task }, timeout);
        True(completed == 0, timeoutMessage);
        task.GetAwaiter().GetResult();
    }

    private static T CompleteWithin<T>(Task<T> task, TimeSpan timeout, string timeoutMessage)
    {
        int completed = Task.WaitAny(new Task[] { task }, timeout);
        True(completed == 0, timeoutMessage);
        return task.GetAwaiter().GetResult();
    }

    private static void True(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }

    private static void False(bool condition, string message)
    {
        True(!condition, message);
    }

    private static void Null(object value, string message)
    {
        True(value == null, message);
    }

    private static void NotNull(object value, string message)
    {
        True(value != null, message);
    }

    private static void Same(object expected, object actual, string message)
    {
        True(object.ReferenceEquals(expected, actual), message);
    }

    private static void Equal<T>(T expected, T actual, string message)
    {
        if (!object.Equals(expected, actual))
        {
            throw new InvalidOperationException(
                message + " Expected: " + expected + "; actual: " + actual + ".");
        }
    }

    private static void SequenceEqual(byte[] expected, byte[] actual, string message)
    {
        if (expected == null || actual == null || expected.Length != actual.Length)
        {
            throw new InvalidOperationException(message + " Byte lengths differ.");
        }

        for (int i = 0; i < expected.Length; i++)
        {
            if (expected[i] != actual[i])
            {
                throw new InvalidOperationException(message + " Byte mismatch at index " + i + ".");
            }
        }
    }

    private sealed class LoopbackClient : ISComV2ClientBase
    {
    }
}
