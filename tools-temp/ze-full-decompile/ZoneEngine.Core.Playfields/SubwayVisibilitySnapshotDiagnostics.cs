using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using AORebirth.Core.Entities;
using AORebirth.Interfaces;
using SmokeLounge.AOtomation.Messaging.GameData;
using SmokeLounge.AOtomation.Messaging.Messages;

namespace ZoneEngine.Core.Playfields;

internal static class SubwayVisibilitySnapshotDiagnostics
{
	private sealed class PacketScope : IDisposable
	{
		private readonly SubwayVisibilityDiagnosticPacketContext previous;

		private bool disposed;

		internal PacketScope(SubwayVisibilityDiagnosticPacketContext previous)
		{
			this.previous = previous;
		}

		public void Dispose()
		{
			if (!disposed)
			{
				currentPacket = previous;
				disposed = true;
			}
		}
	}

	private sealed class EmptyScope : IDisposable
	{
		internal static readonly EmptyScope Instance = new EmptyScope();

		public void Dispose()
		{
		}
	}

	private sealed class ByteArrayReferenceComparer : IEqualityComparer<byte[]>
	{
		public bool Equals(byte[] left, byte[] right)
		{
			return left == right;
		}

		public int GetHashCode(byte[] value)
		{
			return RuntimeHelpers.GetHashCode(value);
		}
	}

	private static readonly object PendingSync = new object();

	private static readonly Dictionary<byte[], SubwayVisibilityDiagnosticPacketRecord> PendingPackets = new Dictionary<byte[], SubwayVisibilityDiagnosticPacketRecord>(new ByteArrayReferenceComparer());

	[ThreadStatic]
	private static SubwayVisibilityDiagnosticPacketContext currentPacket;

	internal static SubwayVisibilityDiagnosticSnapshot TryBeginSnapshot(ICharacter recipient, int candidateCharacters)
	{
		//IL_0020: Unknown result type (might be due to invalid IL or missing references)
		//IL_0025: Unknown result type (might be due to invalid IL or missing references)
		SubwayVisibilityDiagnosticConfiguration configuration = SubwayVisibilityDiagnosticSelection.Configuration;
		if (configuration.Enabled && recipient != null && ((IInstancedEntity)recipient).Playfield != null)
		{
			Identity identity = ((IEntity)((IInstancedEntity)recipient).Playfield).Identity;
			if (((Identity)(ref identity)).Instance == 127)
			{
				return new SubwayVisibilityDiagnosticSnapshot(configuration, recipient, candidateCharacters);
			}
		}
		return null;
	}

	internal static IDisposable BeginPacket(SubwayVisibilityDiagnosticSnapshot snapshot, SubwayVisibilityDiagnosticEnemy enemy, SubwayVisibilityDiagnosticPacketKind kind, int weaponIndex)
	{
		if (snapshot == null || enemy == null)
		{
			return EmptyScope.Instance;
		}
		SubwayVisibilityDiagnosticPacketContext previous = currentPacket;
		currentPacket = new SubwayVisibilityDiagnosticPacketContext(snapshot, enemy, kind, weaponIndex);
		return new PacketScope(previous);
	}

	internal static void OnSerializationStarted(MessageBody body)
	{
		SubwayVisibilityDiagnosticPacketContext subwayVisibilityDiagnosticPacketContext = currentPacket;
		subwayVisibilityDiagnosticPacketContext?.Snapshot.RecordPacketEvent(subwayVisibilityDiagnosticPacketContext, "SERIALIZATION_STARTED", body, 0, string.Empty);
	}

	internal static void OnSerializationCompleted(MessageBody body, byte[] buffer)
	{
		SubwayVisibilityDiagnosticPacketContext subwayVisibilityDiagnosticPacketContext = currentPacket;
		if (subwayVisibilityDiagnosticPacketContext == null)
		{
			return;
		}
		SubwayVisibilityDiagnosticPacketRecord subwayVisibilityDiagnosticPacketRecord = new SubwayVisibilityDiagnosticPacketRecord(subwayVisibilityDiagnosticPacketContext.Snapshot, subwayVisibilityDiagnosticPacketContext.Enemy, subwayVisibilityDiagnosticPacketContext.Kind, subwayVisibilityDiagnosticPacketContext.WeaponIndex, SubwayVisibilityPacketMeasurement.MeasureSerializedBytes(buffer));
		subwayVisibilityDiagnosticPacketContext.Snapshot.RecordSerializedPacket(subwayVisibilityDiagnosticPacketRecord, body);
		if (buffer == null)
		{
			return;
		}
		lock (PendingSync)
		{
			PendingPackets[buffer] = subwayVisibilityDiagnosticPacketRecord;
		}
	}

	internal static void OnSerializationFailed(MessageBody body, Exception exception)
	{
		SubwayVisibilityDiagnosticPacketContext subwayVisibilityDiagnosticPacketContext = currentPacket;
		if (subwayVisibilityDiagnosticPacketContext != null)
		{
			subwayVisibilityDiagnosticPacketContext.Snapshot.RecordPacketEvent(subwayVisibilityDiagnosticPacketContext, "SERIALIZATION_FAILED", body, 0, (exception == null) ? string.Empty : exception.ToString());
			subwayVisibilityDiagnosticPacketContext.Snapshot.RecordFailure(subwayVisibilityDiagnosticPacketContext.Enemy, "serialization", exception);
		}
	}

	internal static void OnTransportUnavailable(byte[] buffer, string reason)
	{
		SubwayVisibilityDiagnosticPacketRecord subwayVisibilityDiagnosticPacketRecord = TakePacket(buffer);
		if (subwayVisibilityDiagnosticPacketRecord != null)
		{
			subwayVisibilityDiagnosticPacketRecord.Snapshot.RecordTransportEvent(subwayVisibilityDiagnosticPacketRecord, "SEND_FAILED", reason);
			subwayVisibilityDiagnosticPacketRecord.Snapshot.RecordFailure(subwayVisibilityDiagnosticPacketRecord.Enemy, "transport", new IOException(reason));
		}
	}

	internal static void OnTransportStarted(byte[] buffer)
	{
		SubwayVisibilityDiagnosticPacketRecord subwayVisibilityDiagnosticPacketRecord = FindPacket(buffer);
		subwayVisibilityDiagnosticPacketRecord?.Snapshot.RecordTransportEvent(subwayVisibilityDiagnosticPacketRecord, "SEND_STARTED", string.Empty);
	}

	internal static void OnTransportCompleted(byte[] buffer)
	{
		SubwayVisibilityDiagnosticPacketRecord subwayVisibilityDiagnosticPacketRecord = TakePacket(buffer);
		if (subwayVisibilityDiagnosticPacketRecord != null)
		{
			subwayVisibilityDiagnosticPacketRecord.Snapshot.RecordTransportEvent(subwayVisibilityDiagnosticPacketRecord, "SEND_COMPLETED", string.Empty);
			subwayVisibilityDiagnosticPacketRecord.Snapshot.RecordPacketTransportCompleted(subwayVisibilityDiagnosticPacketRecord);
		}
	}

	internal static void OnTransportFailed(byte[] buffer, Exception exception)
	{
		SubwayVisibilityDiagnosticPacketRecord subwayVisibilityDiagnosticPacketRecord = TakePacket(buffer);
		if (subwayVisibilityDiagnosticPacketRecord != null)
		{
			subwayVisibilityDiagnosticPacketRecord.Snapshot.RecordTransportEvent(subwayVisibilityDiagnosticPacketRecord, "SEND_FAILED", (exception == null) ? string.Empty : exception.ToString());
			subwayVisibilityDiagnosticPacketRecord.Snapshot.RecordFailure(subwayVisibilityDiagnosticPacketRecord.Enemy, "transport", exception);
		}
	}

	private static SubwayVisibilityDiagnosticPacketRecord FindPacket(byte[] buffer)
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

	private static SubwayVisibilityDiagnosticPacketRecord TakePacket(byte[] buffer)
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
			return value;
		}
	}
}
