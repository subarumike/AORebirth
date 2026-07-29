using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Net.Sockets;
using System.Threading;
using AORebirth.Core.Components;
using AORebirth.Core.Entities;
using AORebirth.Core.Network;
using AORebirth.Core.Playfields;
using AORebirth.Database.Dao;
using AORebirth.Database.Entities;
using AORebirth.Enums;
using AORebirth.Interfaces;
using AORebirth.ObjectManager;
using AORebirth.Stats;
using Cell.Core;
using Ionic.Zlib;
using MemBus;
using SmokeLounge.AOtomation.Messaging.GameData;
using SmokeLounge.AOtomation.Messaging.Messages;
using Utility;
using ZoneEngine.Core.Controllers;
using ZoneEngine.Core.MessageHandlers;
using ZoneEngine.Core.Missions;
using ZoneEngine.Core.Playfields;

namespace ZoneEngine.Core;

public class ZoneClient : ClientBase, IZoneClient, IClient, IDisposable
{
	private sealed class QueuedOutboundPacket
	{
		internal byte[] Buffer { get; private set; }

		internal bool TraceQuestNpcTransport { get; private set; }

		internal int QueueDepthAtEnqueue { get; set; }

		internal QueuedOutboundPacket(byte[] buffer, bool traceQuestNpcTransport)
		{
			Buffer = buffer;
			TraceQuestNpcTransport = traceQuestNpcTransport;
		}
	}

	public IPlayfield Playfield;

	private readonly ZoneServer server;

	private readonly IBus bus;

	private readonly ZoneClientSessionLifecycleCoordinator sessionLifecycle;

	private readonly PacketSequencingCoordinator packetSequencing;

	private IController controller;

	private readonly IMessageSerializer messageSerializer;

	private NetworkStream netStream;

	private readonly object locker = new object();

	private short packetNumber = 0;

	private ZlibStream zStream;

	private bool zStreamSetup;

	private bool disposed = false;

	private readonly Queue<QueuedOutboundPacket> sendQueue = new Queue<QueuedOutboundPacket>();

	private readonly string questNpcTransportDiagnosticSessionId = Guid.NewGuid().ToString("N");

	private Thread dispatcherThread;

	private bool stopDispatcher = false;

	public bool PreserveLogoutSitOnConnect { get; set; }

	public DateTime LastGameTimeSyncUtc { get; set; } = DateTime.UtcNow;


	public IController Controller
	{
		get
		{
			return controller;
		}
		set
		{
			controller = value;
		}
	}

	public ZoneClientSessionLifecycleCoordinator SessionLifecycle => sessionLifecycle;

	public PacketSequencingCoordinator PacketSequencing => packetSequencing;

	public ZoneClient(ZoneServer server, IMessageSerializer messageSerializer, IBus bus)
		: base((ServerBase)(object)server)
	{
		this.server = server;
		this.messageSerializer = messageSerializer;
		this.bus = bus;
		sessionLifecycle = new ZoneClientSessionLifecycleCoordinator();
		packetSequencing = new PacketSequencingCoordinator();
		dispatcherThread = new Thread(DispatchMessages);
		dispatcherThread.Start();
	}

	public void SendCompressed(MessageBody messageBody)
	{
		if (controller != null && controller.Character != null)
		{
			GridZoneInDiagnostics.LogOutboundMessage(this, messageBody);
			WorldEntrySummary.RecordOutboundMessage(this, messageBody);
			SendCompressed(messageBody, server.Id);
		}
	}

	public void SendCompressed(MessageBody messageBody, int sender)
	{
		//IL_0026: Unknown result type (might be due to invalid IL or missing references)
		//IL_002d: Expected O, but got Unknown
		//IL_0038: Unknown result type (might be due to invalid IL or missing references)
		//IL_003f: Expected O, but got Unknown
		//IL_0066: Unknown result type (might be due to invalid IL or missing references)
		//IL_0090: Unknown result type (might be due to invalid IL or missing references)
		//IL_0095: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ef: Unknown result type (might be due to invalid IL or missing references)
		//IL_011d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0122: Unknown result type (might be due to invalid IL or missing references)
		//IL_0140: Unknown result type (might be due to invalid IL or missing references)
		if (controller == null || controller.Character == null)
		{
			return;
		}
		Message val = new Message();
		val.Body = messageBody;
		Header val2 = new Header();
		val2.MessageId = BitConverter.ToUInt16(new byte[2] { 223, 223 }, 0);
		val2.PacketType = messageBody.PacketType;
		val2.Unknown = 1;
		val2.Sender = sender;
		Identity identity = ((IEntity)Controller.Character).Identity;
		val2.Receiver = ((Identity)(ref identity)).Instance;
		val.Header = val2;
		Message val3 = val;
		SubwayVisibilitySnapshotDiagnostics.OnSerializationStarted(messageBody);
		byte[] buffer;
		try
		{
			buffer = messageSerializer.Serialize(val3);
			SubwayVisibilitySnapshotDiagnostics.OnSerializationCompleted(messageBody, buffer);
		}
		catch (Exception exception)
		{
			SubwayVisibilitySnapshotDiagnostics.OnSerializationFailed(messageBody, exception);
			throw;
		}
		CombatStartPacketDiagnostics.LogSerializedOutbound("ZoneClient.SendCompressed", messageBody, sender, ((IEntity)Controller.Character).Identity, buffer);
		int num;
		if (((IInstancedEntity)Controller.Character).Playfield != null)
		{
			identity = ((IEntity)((IInstancedEntity)Controller.Character).Playfield).Identity;
			num = ((Identity)(ref identity)).Instance;
		}
		else
		{
			num = 0;
		}
		int playfieldId = num;
		bool flag = QuestNpcOutboundTransportDiagnostics.OnSerialized(questNpcTransportDiagnosticSessionId, ((IEntity)Controller.Character).Identity, ((INamedEntity)Controller.Character).Name, playfieldId, messageBody, buffer, EmitQuestNpcOutboundTransportDiagnostic);
		try
		{
			QueuedOutboundPacket queuedOutboundPacket = new QueuedOutboundPacket(buffer, flag);
			lock (sendQueue)
			{
				sendQueue.Enqueue(queuedOutboundPacket);
				queuedOutboundPacket.QueueDepthAtEnqueue = sendQueue.Count;
				if (flag)
				{
					QuestNpcOutboundTransportDiagnostics.MarkEnqueued(buffer);
				}
			}
		}
		catch (Exception exception2)
		{
			if (flag)
			{
				QuestNpcOutboundTransportDiagnostics.OnQueueFailed(buffer, exception2, EmitQuestNpcOutboundTransportDiagnostic);
			}
			throw;
		}
		LogUtil.Debug((DebugInfoDetail)2048, ((object)messageBody).GetType().ToString());
	}

	public void CreateCharacter(int charId)
	{
		//IL_0057: Unknown result type (might be due to invalid IL or missing references)
		//IL_0078: Unknown result type (might be due to invalid IL or missing references)
		//IL_0082: Unknown result type (might be due to invalid IL or missing references)
		//IL_009e: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a0: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a6: Unknown result type (might be due to invalid IL or missing references)
		//IL_0132: Unknown result type (might be due to invalid IL or missing references)
		//IL_0137: Unknown result type (might be due to invalid IL or missing references)
		//IL_013e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0148: Expected O, but got Unknown
		//IL_01e0: Unknown result type (might be due to invalid IL or missing references)
		//IL_01e6: Invalid comparison between Unknown and I4
		DBCharacter val = ((Dao<DBCharacter, CharacterDao>)(object)Dao<DBCharacter, CharacterDao>.Instance).Get(charId);
		if (val == null)
		{
			throw new Exception("Character " + charId + " not found.");
		}
		bool flag = SessionLifecycle.Phase == ZoneClientSessionPhase.Zoning;
		SessionLifecycle.EnterPlayfieldLoadingForCharacterLoadOrZoningExit();
		ZoneServer zoneServer = server;
		Identity val2 = default(Identity);
		((Identity)(ref val2)).Type = (IdentityType)51101;
		((Identity)(ref val2)).Instance = val.Playfield;
		IPlayfield val3 = zoneServer.PlayfieldById(val2);
		val2 = default(Identity);
		((Identity)(ref val2)).Type = (IdentityType)50000;
		((Identity)(ref val2)).Instance = charId;
		Identity val4 = val2;
		Character val5 = Pool.Instance.GetObject<Character>(val4);
		if (val5 != null && ((Dynel)val5).Controller is NPCController)
		{
			LogUtil.Debug((DebugInfoDetail)512, "Removing NPC/player identity collision for " + ((Identity)(ref val4)).ToString(true) + " while logging in character " + charId + ".");
			Pool.Instance.RemoveObject<Character>(val5);
			val5 = null;
		}
		if (val5 == null)
		{
			Controller.Character = (ICharacter)new Character(((IEntity)val3).Identity, val4, Controller);
			((IDatabaseObject)controller.Character).Read();
		}
		else
		{
			Controller.Character = (ICharacter)(object)val5;
			Controller.Character.Reconnect((IZoneClient)(object)this);
			LogUtil.Debug((DebugInfoDetail)128, "Reconnected to Character " + charId);
		}
		ICharacter character = Controller.Character;
		Character val6 = (Character)(object)((character is Character) ? character : null);
		if (val6 != null)
		{
			val6.ReloadTrainedPerksFromDatabase();
		}
		PreserveLogoutSitOnConnect = Controller.Character.InLogoutTimerPeriod() && (int)Controller.Character.MoveMode == 8;
		Controller.Character.StopLogoutTimer();
		((IInstancedEntity)Controller.Character).Playfield = val3;
		Playfield = val3;
		((IDatabaseObject)((IStats)Controller.Character).Stats).Read();
		if (val5 == null)
		{
			MissionRuntime.ReloadForLogin(charId);
		}
		else if (flag)
		{
			MissionRuntime.ReloadForZoning(charId);
		}
		else
		{
			MissionRuntime.ReloadForReconnect(charId);
		}
		ActiveNanoRuntimeService.Default.TryRestoreZoneTransferStats(Controller.Character);
		((IStats)controller.Character).Stats[(StatIds)368].BaseValue = (uint)((IStats)controller.Character).Stats[(StatIds)60].Value;
	}

	public void EnqueueOutboundCompressedBuffer(byte[] buffer)
	{
		if (buffer == null || buffer.Length == 0)
		{
			return;
		}
		byte[] array = new byte[buffer.Length];
		Buffer.BlockCopy(buffer, 0, array, 0, buffer.Length);
		lock (sendQueue)
		{
			sendQueue.Enqueue(new QueuedOutboundPacket(array, traceQuestNpcTransport: false));
		}
	}

	public void SendCompressed(byte[] buffer)
	{
		SendCompressed(buffer, QuestNpcOutboundTransportDiagnostics.IsTrackedBuffer(buffer));
	}

	private void SendCompressed(byte[] buffer, bool traceQuestNpcTransport)
	{
		if (buffer == null || buffer.Length < 2)
		{
			QuestNpcOutboundTransportDiagnostics.OnTransportUnavailable(buffer, "serialized buffer is shorter than the packet-number field", EmitQuestNpcOutboundTransportDiagnostic);
			return;
		}
		if (netStream == null || zStream == null)
		{
			SubwayVisibilitySnapshotDiagnostics.OnTransportUnavailable(buffer, "network or compression stream unavailable");
			if (traceQuestNpcTransport)
			{
				QuestNpcOutboundTransportDiagnostics.OnTransportUnavailable(buffer, "network or compression stream unavailable", EmitQuestNpcOutboundTransportDiagnostic);
			}
			return;
		}
		bool flag = false;
		bool flag2 = false;
		bool flag3 = false;
		string text = string.Empty;
		Exception ex = null;
		long zlibTotalIn = -1L;
		long zlibTotalOut = -1L;
		lock (locker)
		{
			if (netStream.CanWrite)
			{
				byte[] bytes = BitConverter.GetBytes(packetNumber++);
				buffer[0] = bytes[1];
				buffer[1] = bytes[0];
				if (traceQuestNpcTransport)
				{
					QuestNpcOutboundTransportDiagnostics.OnPacketNumberAssigned(buffer);
				}
				try
				{
					SubwayVisibilitySnapshotDiagnostics.OnTransportStarted(buffer);
					if (traceQuestNpcTransport)
					{
						QuestNpcOutboundTransportDiagnostics.OnWriteStarted(buffer);
					}
					((Stream)(object)zStream).Write(buffer, 0, buffer.Length);
					flag = true;
					if (traceQuestNpcTransport)
					{
						zlibTotalIn = ZlibTotalInOrUnavailable(zStream);
						zlibTotalOut = ZlibTotalOutOrUnavailable(zStream);
						QuestNpcOutboundTransportDiagnostics.OnWriteReturned(buffer, buffer.Length, zlibTotalIn, zlibTotalOut);
					}
					((Stream)(object)zStream).Flush();
					flag2 = true;
					if (traceQuestNpcTransport)
					{
						zlibTotalIn = ZlibTotalInOrUnavailable(zStream);
						zlibTotalOut = ZlibTotalOutOrUnavailable(zStream);
					}
					SubwayVisibilitySnapshotDiagnostics.OnTransportCompleted(buffer);
					if (ContainsTradeOpcode(buffer))
					{
						LogUtil.Debug((DebugInfoDetail)128, "OUT Trade wire len=" + buffer.Length.ToString(CultureInfo.InvariantCulture) + " hex=" + BitConverter.ToString(buffer).Replace("-", string.Empty));
					}
				}
				catch (Exception ex2)
				{
					ex = ex2;
					flag3 = true;
					if (traceQuestNpcTransport)
					{
						zlibTotalIn = ZlibTotalInOrUnavailable(zStream);
						zlibTotalOut = ZlibTotalOutOrUnavailable(zStream);
					}
					SubwayVisibilitySnapshotDiagnostics.OnTransportFailed(buffer, ex2);
					LogUtil.Debug((DebugInfoDetail)512, "Error writing to zStream");
					LogUtil.ErrorException(ex2);
				}
			}
			else
			{
				SubwayVisibilitySnapshotDiagnostics.OnTransportUnavailable(buffer, "network stream is not writable");
				text = "network stream is not writable";
			}
		}
		if (traceQuestNpcTransport && flag2)
		{
			QuestNpcOutboundTransportDiagnostics.OnFlushReturned(buffer, zlibTotalIn, zlibTotalOut, EmitQuestNpcOutboundTransportDiagnostic);
		}
		else if (traceQuestNpcTransport && ex != null)
		{
			if (flag)
			{
				QuestNpcOutboundTransportDiagnostics.OnFlushFailed(buffer, ex, zlibTotalIn, zlibTotalOut, EmitQuestNpcOutboundTransportDiagnostic);
			}
			else
			{
				QuestNpcOutboundTransportDiagnostics.OnWriteFailed(buffer, ex, zlibTotalIn, zlibTotalOut, EmitQuestNpcOutboundTransportDiagnostic);
			}
		}
		else if (traceQuestNpcTransport && !string.IsNullOrEmpty(text))
		{
			QuestNpcOutboundTransportDiagnostics.OnTransportUnavailable(buffer, text, EmitQuestNpcOutboundTransportDiagnostic);
		}
		if (flag3)
		{
			((ServerBase)server).DisconnectClient((IClient)(object)this);
		}
		LogUtil.Debug((DebugInfoDetail)4, HexOutput.Output(buffer));
	}

	private static bool ContainsTradeOpcode(byte[] buffer)
	{
		if (buffer == null || buffer.Length < 4)
		{
			return false;
		}
		for (int i = 0; i <= buffer.Length - 4; i++)
		{
			if (buffer[i] == 54 && buffer[i + 1] == 40 && buffer[i + 2] == 79 && buffer[i + 3] == 110)
			{
				return true;
			}
		}
		return false;
	}

	private static void EmitQuestNpcOutboundTransportDiagnostic(string message)
	{
		LogUtil.Debug((DebugInfoDetail)128, message);
	}

	private static long ZlibTotalInOrUnavailable(ZlibStream stream)
	{
		try
		{
			return (stream == null) ? (-1) : stream.TotalIn;
		}
		catch
		{
			return -1L;
		}
	}

	private static long ZlibTotalOutOrUnavailable(ZlibStream stream)
	{
		try
		{
			return (stream == null) ? (-1) : stream.TotalOut;
		}
		catch
		{
			return -1L;
		}
	}

	public void SendInitiateCompressionMessage(MessageBody messageBody)
	{
		//IL_004c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0056: Expected O, but got Unknown
		byte[] array = new byte[16]
		{
			223, 223, 127, 0, 0, 1, 0, 16, 1, 0,
			0, 0, 0, 0, 0, 0
		};
		((ClientBase)this).Send(array);
		packetNumber = 1;
		try
		{
			if (!zStreamSetup)
			{
				netStream = new NetworkStream(((ClientBase)this).TcpSocket);
				zStream = new ZlibStream((Stream)netStream, (CompressionMode)0, (CompressionLevel)1);
				zStream.FlushMode = (FlushType)2;
				zStreamSetup = true;
			}
		}
		catch (Exception ex)
		{
			LogUtil.ErrorException(ex);
		}
	}

	protected override void Dispose(bool disposing)
	{
		//IL_0081: Unknown result type (might be due to invalid IL or missing references)
		//IL_0086: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ad: Unknown result type (might be due to invalid IL or missing references)
		if (disposing && !disposed)
		{
			sessionLifecycle.EnterDisconnectingForSessionDispose();
			stopDispatcher = true;
			while (stopDispatcher)
			{
				Thread.Sleep(10);
			}
			QuestNpcOutboundTransportDiagnostics.OnSessionDisposed(questNpcTransportDiagnosticSessionId, EmitQuestNpcOutboundTransportDiagnostic);
			IController val = Controller;
			ICharacter val2 = ((val == null) ? null : val.Character);
			if (val2 != null)
			{
				Identity identity = ((IEntity)val2).Identity;
				int instance = ((Identity)(ref identity)).Instance;
				if (((IInstancedEntity)val2).Playfield is Playfield playfield)
				{
					playfield.ForgetVisibilityRecipient(((IEntity)val2).Identity);
				}
				bool preservePendingRestore = ActiveNanoRuntimeService.Default.HasZoneTransferStash(instance);
				PetRuntimeService.Default.OnCharacterDisconnected(val2, preservePendingRestore);
				if (!val2.InLogoutTimerPeriod() && !ActiveNanoRuntimeService.Default.HasZoneTransferStash(instance))
				{
					val2.EnterLogoutSitPosture();
					val.State = (CharacterState)0;
					val2.StartLogoutTimer(30000);
				}
			}
			CloseTransportStreamsQuietly();
			controller = null;
		}
		disposed = true;
		((ClientBase)this).Dispose(disposing);
	}

	private void CloseTransportStreamsQuietly()
	{
		try
		{
			if (zStream != null)
			{
				((Stream)(object)zStream).Close();
			}
		}
		catch (IOException)
		{
		}
		catch (SocketException)
		{
		}
		catch (ObjectDisposedException)
		{
		}
		finally
		{
			zStream = null;
		}
		try
		{
			if (netStream != null)
			{
				netStream.Close();
			}
		}
		catch (IOException)
		{
		}
		catch (SocketException)
		{
		}
		catch (ObjectDisposedException)
		{
		}
		finally
		{
			netStream = null;
		}
	}

	protected uint GetMessageNumber(BufferSegment segment)
	{
		byte[] array = new byte[4];
		array[3] = segment.SegmentData[16];
		array[2] = segment.SegmentData[17];
		array[1] = segment.SegmentData[18];
		array[0] = segment.SegmentData[19];
		return BitConverter.ToUInt32(array, 0);
	}

	protected uint GetMessageNumber(byte[] segment)
	{
		byte[] array = new byte[4];
		array[3] = segment[16];
		array[2] = segment[17];
		array[1] = segment[18];
		array[0] = segment[19];
		return BitConverter.ToUInt32(array, 0);
	}

	protected override bool OnReceive(BufferSegment buffer)
	{
		Message val = null;
		byte[] array = new byte[base._remainingLength];
		Array.Copy(buffer.SegmentData, array, base._remainingLength);
		LogUtil.Debug((DebugInfoDetail)128, string.Format(CultureInfo.InvariantCulture, "Zone receive: {0} bytes, message {1}", array.Length, GetMessageNumber(array)));
		LogUtil.Debug((DebugInfoDetail)4, "\r\nReceived: \r\n" + HexOutput.Output(array));
		base._remainingLength = 0;
		try
		{
			val = messageSerializer.Deserialize(array);
		}
		catch (Exception ex)
		{
			uint messageNumber = GetMessageNumber(array);
			((ClientBase)this).Server.Warning((IClient)(object)this, "Client sent malformed message {0}", new object[1] { messageNumber.ToString(CultureInfo.InvariantCulture) });
			LogUtil.ErrorException(ex, false, "Zone deserialize failed for message {0}", new object[1] { messageNumber });
			LogUtil.Debug((DebugInfoDetail)512, HexOutput.Output(array));
			return false;
		}
		buffer.IncrementUsage();
		if (val == null)
		{
			uint messageNumber2 = GetMessageNumber(array);
			((ClientBase)this).Server.Warning((IClient)(object)this, "Client sent unknown message {0}", new object[1] { messageNumber2.ToString(CultureInfo.InvariantCulture) });
			return false;
		}
		LogUtil.Debug((DebugInfoDetail)128, "Zone message decoded: " + ((object)val.Body).GetType().FullName);
		Type typeFromHandle = typeof(MessageWrapper<>);
		Type type = typeFromHandle.MakeGenericType(((object)val.Body).GetType());
		object obj = Activator.CreateInstance(type);
		obj.GetType().GetProperty("Client").SetValue(obj, this, null);
		obj.GetType().GetProperty("Message").SetValue(obj, val, null);
		obj.GetType().GetProperty("MessageBody").SetValue(obj, val.Body, null);
		((IPublisher)bus).Publish(obj);
		return true;
	}

	private void DispatchMessages()
	{
		while (!stopDispatcher)
		{
			QueuedOutboundPacket queuedOutboundPacket = null;
			int remainingQueueDepth = -1;
			lock (sendQueue)
			{
				if (sendQueue.Count > 0)
				{
					queuedOutboundPacket = sendQueue.Dequeue();
					remainingQueueDepth = sendQueue.Count;
				}
			}
			if (queuedOutboundPacket != null)
			{
				if (queuedOutboundPacket.TraceQuestNpcTransport)
				{
					QuestNpcOutboundTransportDiagnostics.EmitEnqueued(queuedOutboundPacket.Buffer, queuedOutboundPacket.QueueDepthAtEnqueue, EmitQuestNpcOutboundTransportDiagnostic);
					QuestNpcOutboundTransportDiagnostics.OnDequeued(queuedOutboundPacket.Buffer, remainingQueueDepth, EmitQuestNpcOutboundTransportDiagnostic);
				}
				SendCompressed(queuedOutboundPacket.Buffer, queuedOutboundPacket.TraceQuestNpcTransport);
			}
			else
			{
				Thread.Sleep(5);
			}
		}
		stopDispatcher = false;
	}
}
