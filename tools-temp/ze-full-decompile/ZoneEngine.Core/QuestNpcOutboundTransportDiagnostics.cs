using System;
using System.Collections.Generic;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;
using SmokeLounge.AOtomation.Messaging.GameData;
using SmokeLounge.AOtomation.Messaging.Messages;
using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;

namespace ZoneEngine.Core;

internal static class QuestNpcOutboundTransportDiagnostics
{
	private sealed class TrackedPacket
	{
		internal string SessionId { get; private set; }

		internal int ClientIdentityType { get; private set; }

		internal int ClientIdentityInstance { get; private set; }

		internal string ClientName { get; private set; }

		internal int PlayfieldId { get; private set; }

		internal int TargetIdentityType { get; private set; }

		internal int TargetIdentityInstance { get; private set; }

		internal string NpcName { get; private set; }

		internal string MessageType { get; private set; }

		internal byte[] Buffer { get; private set; }

		internal string QueueResult { get; set; }

		internal int PacketNumber { get; set; }

		internal bool TransportWriteCallStarted { get; set; }

		internal bool SocketWriteReached { get; set; }

		internal string TransportCall { get; set; }

		internal int TransportBytesAccepted { get; set; }

		internal string TransportBytesKind { get; set; }

		internal long ZlibTotalIn { get; set; }

		internal long ZlibTotalOut { get; set; }

		internal TrackedPacket(string sessionId, Identity clientIdentity, string clientName, int playfieldId, Identity targetIdentity, string npcName, string messageType, byte[] buffer)
		{
			//IL_001c: Unknown result type (might be due to invalid IL or missing references)
			//IL_0026: Expected I4, but got Unknown
			//IL_0052: Unknown result type (might be due to invalid IL or missing references)
			//IL_005c: Expected I4, but got Unknown
			SessionId = sessionId ?? string.Empty;
			ClientIdentityType = (int)((Identity)(ref clientIdentity)).Type;
			ClientIdentityInstance = ((Identity)(ref clientIdentity)).Instance;
			ClientName = clientName ?? string.Empty;
			PlayfieldId = playfieldId;
			TargetIdentityType = (int)((Identity)(ref targetIdentity)).Type;
			TargetIdentityInstance = ((Identity)(ref targetIdentity)).Instance;
			NpcName = npcName ?? string.Empty;
			MessageType = messageType ?? string.Empty;
			Buffer = buffer;
			QueueResult = "PENDING";
			PacketNumber = -1;
			TransportCall = string.Empty;
			TransportBytesKind = string.Empty;
			TransportBytesAccepted = 0;
			ZlibTotalIn = -1L;
			ZlibTotalOut = -1L;
		}
	}

	private sealed class ByteArrayReferenceComparer : IEqualityComparer<byte[]>
	{
		public bool Equals(byte[] first, byte[] second)
		{
			return first == second;
		}

		public int GetHashCode(byte[] value)
		{
			return RuntimeHelpers.GetHashCode(value);
		}
	}

	internal const int QuestNpcPlayfieldId = 655;

	internal const int KarrecRuntimeInstance = 1000000;

	internal const int AnnoyingDudeRuntimeInstance = 1000001;

	internal const int MaddyCardileRuntimeInstance = 1000002;

	internal const int TestClientRuntimeInstance = 22;

	private const int MessageHeaderLength = 16;

	private const int MaximumPendingPackets = 64;

	private static readonly object PendingSync = new object();

	private static readonly Dictionary<byte[], TrackedPacket> PendingPackets = new Dictionary<byte[], TrackedPacket>(new ByteArrayReferenceComparer());

	private static bool capacityExhaustionReported;

	internal static int PendingCount
	{
		get
		{
			lock (PendingSync)
			{
				return PendingPackets.Count;
			}
		}
	}

	internal static bool IsTrackedMessage(MessageBody body, int playfieldId, Identity clientIdentity)
	{
		//IL_000b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0015: Invalid comparison between Unknown and I4
		//IL_004c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0051: Unknown result type (might be due to invalid IL or missing references)
		//IL_0055: Unknown result type (might be due to invalid IL or missing references)
		//IL_005f: Invalid comparison between Unknown and I4
		//IL_0071: Unknown result type (might be due to invalid IL or missing references)
		//IL_0076: Unknown result type (might be due to invalid IL or missing references)
		if (playfieldId != 655 || (int)((Identity)(ref clientIdentity)).Type != 50000 || ((Identity)(ref clientIdentity)).Instance != 22)
		{
			return false;
		}
		N3Message val = (N3Message)(object)((body is N3Message) ? body : null);
		if (val != null && (body is SimpleCharFullUpdateMessage || body is CharInPlayMessage))
		{
			Identity identity = val.Identity;
			if ((int)((Identity)(ref identity)).Type == 50000)
			{
				identity = val.Identity;
				return IsTrackedRuntimeInstance(((Identity)(ref identity)).Instance);
			}
		}
		return false;
	}

	internal static bool IsTrackedBuffer(byte[] buffer)
	{
		if (buffer == null || buffer.Length < 29)
		{
			return false;
		}
		int num = ReadInt32BigEndian(buffer, 16);
		if (num != 656095851 && num != 1460412473)
		{
			return false;
		}
		return ReadInt32BigEndian(buffer, 12) == 22 && ReadInt32BigEndian(buffer, 20) == 50000 && IsTrackedRuntimeInstance(ReadInt32BigEndian(buffer, 24));
	}

	internal static string NameForRuntimeInstance(int runtimeInstance)
	{
		return runtimeInstance switch
		{
			1000000 => "Windcaller Karrec", 
			1000001 => "Annoying Dude", 
			1000002 => "Maddy Cardile", 
			_ => string.Empty, 
		};
	}

	internal static bool OnSerialized(string sessionId, Identity clientIdentity, string clientName, int playfieldId, MessageBody body, byte[] buffer, Action<string> emit)
	{
		//IL_0005: Unknown result type (might be due to invalid IL or missing references)
		//IL_001f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0025: Expected O, but got Unknown
		//IL_0026: Unknown result type (might be due to invalid IL or missing references)
		//IL_002a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0030: Unknown result type (might be due to invalid IL or missing references)
		//IL_0035: Unknown result type (might be due to invalid IL or missing references)
		try
		{
			if (!IsTrackedMessage(body, playfieldId, clientIdentity))
			{
				return false;
			}
			N3Message val = (N3Message)body;
			Identity identity = val.Identity;
			Identity identity2 = val.Identity;
			TrackedPacket trackedPacket = new TrackedPacket(sessionId, clientIdentity, clientName, playfieldId, identity, NameForRuntimeInstance(((Identity)(ref identity2)).Instance), ((object)body).GetType().Name, buffer ?? new byte[0]);
			if (buffer == null || buffer.Length < 29)
			{
				trackedPacket.QueueResult = "DROPPED";
				WriteEvent(trackedPacket, "DROPPED_INVALID_BUFFER", -1, "serialized message is shorter than the minimum tracked wrapper", emit);
				return false;
			}
			bool flag;
			bool flag2;
			lock (PendingSync)
			{
				flag = PendingPackets.Count >= 64;
				flag2 = flag && !capacityExhaustionReported;
				if (flag2)
				{
					capacityExhaustionReported = true;
				}
				if (!flag)
				{
					PendingPackets[buffer] = trackedPacket;
				}
			}
			if (flag)
			{
				if (flag2)
				{
					trackedPacket.QueueResult = "NOT_TRACKED";
					WriteEvent(trackedPacket, "TRACKING_CAPACITY_EXHAUSTED", -1, "pending diagnostic capacity is 64 packets", emit);
				}
				return false;
			}
			WriteEvent(trackedPacket, "SERIALIZED", -1, string.Empty, emit);
			return true;
		}
		catch
		{
			return false;
		}
	}

	internal static void OnEnqueued(byte[] buffer, int queueDepth, Action<string> emit)
	{
		MarkEnqueued(buffer);
		EmitEnqueued(buffer, queueDepth, emit);
	}

	internal static void MarkEnqueued(byte[] buffer)
	{
		TrackedPacket trackedPacket = Find(buffer);
		if (trackedPacket != null)
		{
			trackedPacket.QueueResult = "ENQUEUED";
		}
	}

	internal static void EmitEnqueued(byte[] buffer, int queueDepth, Action<string> emit)
	{
		TrackedPacket trackedPacket = Find(buffer);
		if (trackedPacket != null)
		{
			WriteEvent(trackedPacket, "ENQUEUED", queueDepth, string.Empty, emit);
		}
	}

	internal static void OnQueueFailed(byte[] buffer, Exception exception, Action<string> emit)
	{
		TrackedPacket trackedPacket = Take(buffer);
		if (trackedPacket != null)
		{
			trackedPacket.QueueResult = "FAILED";
			WriteEvent(trackedPacket, "QUEUE_FAILED", -1, ExceptionText(exception), emit);
		}
	}

	internal static void OnDequeued(byte[] buffer, int remainingQueueDepth, Action<string> emit)
	{
		TrackedPacket trackedPacket = Find(buffer);
		if (trackedPacket != null)
		{
			WriteEvent(trackedPacket, "DEQUEUED", remainingQueueDepth, string.Empty, emit);
		}
	}

	internal static void OnPacketNumberAssigned(byte[] buffer)
	{
		TrackedPacket trackedPacket = Find(buffer);
		if (trackedPacket != null)
		{
			trackedPacket.PacketNumber = ReadUInt16BigEndian(buffer, 0);
		}
	}

	internal static void OnWriteStarted(byte[] buffer)
	{
		TrackedPacket trackedPacket = Find(buffer);
		if (trackedPacket != null)
		{
			trackedPacket.TransportWriteCallStarted = true;
			trackedPacket.TransportCall = "ZlibStream.Write";
		}
	}

	internal static void OnWriteReturned(byte[] buffer, int bytesAccepted, long zlibTotalIn, long zlibTotalOut)
	{
		TrackedPacket trackedPacket = Find(buffer);
		if (trackedPacket != null)
		{
			trackedPacket.TransportBytesAccepted = bytesAccepted;
			trackedPacket.TransportBytesKind = "uncompressed_input_to_ZlibStream.Write";
			trackedPacket.ZlibTotalIn = zlibTotalIn;
			trackedPacket.ZlibTotalOut = zlibTotalOut;
		}
	}

	internal static void OnFlushReturned(byte[] buffer, long zlibTotalIn, long zlibTotalOut, Action<string> emit)
	{
		TrackedPacket trackedPacket = Take(buffer);
		if (trackedPacket != null)
		{
			trackedPacket.SocketWriteReached = true;
			trackedPacket.ZlibTotalIn = zlibTotalIn;
			trackedPacket.ZlibTotalOut = zlibTotalOut;
			WriteEvent(trackedPacket, "FLUSH_RETURNED", -1, string.Empty, emit);
		}
	}

	internal static void OnTransportUnavailable(byte[] buffer, string reason, Action<string> emit)
	{
		TrackedPacket trackedPacket = Take(buffer);
		if (trackedPacket != null)
		{
			WriteEvent(trackedPacket, "DROPPED", -1, reason ?? string.Empty, emit);
		}
	}

	internal static void OnWriteFailed(byte[] buffer, Exception exception, long zlibTotalIn, long zlibTotalOut, Action<string> emit)
	{
		TrackedPacket trackedPacket = Take(buffer);
		if (trackedPacket != null)
		{
			trackedPacket.ZlibTotalIn = zlibTotalIn;
			trackedPacket.ZlibTotalOut = zlibTotalOut;
			WriteEvent(trackedPacket, "WRITE_FAILED", -1, ExceptionText(exception), emit);
		}
	}

	internal static void OnFlushFailed(byte[] buffer, Exception exception, long zlibTotalIn, long zlibTotalOut, Action<string> emit)
	{
		TrackedPacket trackedPacket = Take(buffer);
		if (trackedPacket != null)
		{
			trackedPacket.ZlibTotalIn = zlibTotalIn;
			trackedPacket.ZlibTotalOut = zlibTotalOut;
			WriteEvent(trackedPacket, "FLUSH_FAILED", -1, ExceptionText(exception), emit);
		}
	}

	internal static void OnSessionDisposed(string sessionId, Action<string> emit)
	{
		List<TrackedPacket> list = new List<TrackedPacket>();
		List<byte[]> list2 = new List<byte[]>();
		lock (PendingSync)
		{
			foreach (KeyValuePair<byte[], TrackedPacket> pendingPacket in PendingPackets)
			{
				if (string.Equals(pendingPacket.Value.SessionId, sessionId, StringComparison.Ordinal))
				{
					list2.Add(pendingPacket.Key);
					list.Add(pendingPacket.Value);
				}
			}
			foreach (byte[] item in list2)
			{
				PendingPackets.Remove(item);
			}
			if (PendingPackets.Count < 64)
			{
				capacityExhaustionReported = false;
			}
		}
		foreach (TrackedPacket item2 in list)
		{
			item2.QueueResult = "DROPPED";
			WriteEvent(item2, "SESSION_DISPOSED_DROP", -1, "client session disposed before transport completion", emit);
		}
	}

	internal static void Reset()
	{
		lock (PendingSync)
		{
			PendingPackets.Clear();
			capacityExhaustionReported = false;
		}
	}

	private static bool IsTrackedRuntimeInstance(int runtimeInstance)
	{
		return runtimeInstance == 1000000 || runtimeInstance == 1000001 || runtimeInstance == 1000002;
	}

	private static TrackedPacket Find(byte[] buffer)
	{
		if (buffer == null)
		{
			return null;
		}
		lock (PendingSync)
		{
			PendingPackets.TryGetValue(buffer, out var value);
			return value;
		}
	}

	private static TrackedPacket Take(byte[] buffer)
	{
		if (buffer == null)
		{
			return null;
		}
		lock (PendingSync)
		{
			if (!PendingPackets.TryGetValue(buffer, out var value))
			{
				return null;
			}
			PendingPackets.Remove(buffer);
			if (PendingPackets.Count < 64)
			{
				capacityExhaustionReported = false;
			}
			return value;
		}
	}

	private static void WriteEvent(TrackedPacket record, string eventName, int queueDepth, string exception, Action<string> emit)
	{
		try
		{
			StringBuilder stringBuilder = new StringBuilder();
			stringBuilder.Append("QUEST_NPC_OUTBOUND ");
			stringBuilder.Append('{');
			AppendJson(stringBuilder, "timestamp_utc", DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture), first: true);
			AppendJson(stringBuilder, "session_id", record.SessionId, first: false);
			AppendJson(stringBuilder, "client_identity_type", record.ClientIdentityType, first: false);
			AppendJson(stringBuilder, "client_identity_instance", record.ClientIdentityInstance, first: false);
			AppendJson(stringBuilder, "client_name", record.ClientName, first: false);
			AppendJson(stringBuilder, "playfield_id", record.PlayfieldId, first: false);
			AppendJson(stringBuilder, "target_identity_type", record.TargetIdentityType, first: false);
			AppendJson(stringBuilder, "target_identity_instance", record.TargetIdentityInstance, first: false);
			AppendJson(stringBuilder, "npc_name", record.NpcName, first: false);
			AppendJson(stringBuilder, "message_type", record.MessageType, first: false);
			AppendJson(stringBuilder, "message_opcode", ReadUInt32BigEndianHex(record.Buffer, 16), first: false);
			AppendJson(stringBuilder, "total_serialized_length", record.Buffer.Length, first: false);
			AppendJson(stringBuilder, "declared_length", ReadUInt16BigEndian(record.Buffer, 6), first: false);
			AppendJson(stringBuilder, "body_length", Math.Max(0, record.Buffer.Length - 16), first: false);
			AppendJson(stringBuilder, "header_receiver", ReadInt32BigEndian(record.Buffer, 12), first: false);
			AppendJson(stringBuilder, "event", eventName, first: false);
			AppendJson(stringBuilder, "queue_result", record.QueueResult, first: false);
			AppendJson(stringBuilder, "queue_depth", queueDepth, first: false);
			AppendJson(stringBuilder, "packet_number", record.PacketNumber, first: false);
			AppendJson(stringBuilder, "transport_write_call_started", record.TransportWriteCallStarted, first: false);
			AppendJson(stringBuilder, "socket_write_reached", record.SocketWriteReached, first: false);
			AppendJson(stringBuilder, "transport_call", record.TransportCall, first: false);
			AppendJson(stringBuilder, "transport_bytes_accepted", record.TransportBytesAccepted, first: false);
			AppendJson(stringBuilder, "transport_bytes_kind", record.TransportBytesKind, first: false);
			AppendJson(stringBuilder, "zlib_total_in", record.ZlibTotalIn, first: false);
			AppendJson(stringBuilder, "zlib_total_out", record.ZlibTotalOut, first: false);
			AppendJson(stringBuilder, "exception", exception ?? string.Empty, first: false);
			if (IncludesPayload(eventName))
			{
				AppendJson(stringBuilder, "sha256", Sha256(record.Buffer), first: false);
				AppendJson(stringBuilder, "full_hex", BitConverter.ToString(record.Buffer).Replace("-", string.Empty), first: false);
			}
			stringBuilder.Append('}');
			SafeEmit(emit, stringBuilder.ToString());
		}
		catch
		{
		}
	}

	private static bool IncludesPayload(string eventName)
	{
		int result;
		switch (eventName)
		{
		default:
			result = ((eventName == "SESSION_DISPOSED_DROP") ? 1 : 0);
			break;
		case "SERIALIZED":
		case "FLUSH_RETURNED":
		case "WRITE_FAILED":
		case "FLUSH_FAILED":
		case "QUEUE_FAILED":
		case "DROPPED":
		case "DROPPED_INVALID_BUFFER":
		case "TRACKING_CAPACITY_EXHAUSTED":
			result = 1;
			break;
		}
		return (byte)result != 0;
	}

	private static string ExceptionText(Exception exception)
	{
		return (exception == null) ? string.Empty : exception.ToString();
	}

	private static string Sha256(byte[] buffer)
	{
		using SHA256 sHA = SHA256.Create();
		return BitConverter.ToString(sHA.ComputeHash(buffer)).Replace("-", string.Empty);
	}

	private static ushort ReadUInt16BigEndian(byte[] buffer, int offset)
	{
		if (buffer == null || offset < 0 || buffer.Length < offset + 2)
		{
			return 0;
		}
		return (ushort)((buffer[offset] << 8) | buffer[offset + 1]);
	}

	private static int ReadInt32BigEndian(byte[] buffer, int offset)
	{
		if (buffer == null || offset < 0 || buffer.Length < offset + 4)
		{
			return 0;
		}
		return (buffer[offset] << 24) | (buffer[offset + 1] << 16) | (buffer[offset + 2] << 8) | buffer[offset + 3];
	}

	private static string ReadUInt32BigEndianHex(byte[] buffer, int offset)
	{
		if (buffer == null || offset < 0 || buffer.Length < offset + 4)
		{
			return string.Empty;
		}
		return "0x" + ((uint)((buffer[offset] << 24) | (buffer[offset + 1] << 16) | (buffer[offset + 2] << 8) | buffer[offset + 3])).ToString("X8", CultureInfo.InvariantCulture);
	}

	private static void SafeEmit(Action<string> emit, string message)
	{
		if (emit == null)
		{
			return;
		}
		try
		{
			emit(message);
		}
		catch
		{
		}
	}

	private static void AppendJson(StringBuilder builder, string name, string value, bool first)
	{
		AppendName(builder, name, first);
		builder.Append('"');
		builder.Append(EscapeJson(value ?? string.Empty));
		builder.Append('"');
	}

	private static void AppendJson(StringBuilder builder, string name, int value, bool first)
	{
		AppendName(builder, name, first);
		builder.Append(value.ToString(CultureInfo.InvariantCulture));
	}

	private static void AppendJson(StringBuilder builder, string name, long value, bool first)
	{
		AppendName(builder, name, first);
		builder.Append(value.ToString(CultureInfo.InvariantCulture));
	}

	private static void AppendJson(StringBuilder builder, string name, bool value, bool first)
	{
		AppendName(builder, name, first);
		builder.Append(value ? "true" : "false");
	}

	private static void AppendName(StringBuilder builder, string name, bool first)
	{
		if (!first)
		{
			builder.Append(',');
		}
		builder.Append('"');
		builder.Append(name);
		builder.Append("\":");
	}

	private static string EscapeJson(string value)
	{
		return value.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\r", "\\r")
			.Replace("\n", "\\n");
	}
}
