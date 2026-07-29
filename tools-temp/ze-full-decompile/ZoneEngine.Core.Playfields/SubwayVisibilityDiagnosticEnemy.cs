using System;
using System.Globalization;
using SmokeLounge.AOtomation.Messaging.GameData;

namespace ZoneEngine.Core.Playfields;

internal sealed class SubwayVisibilityDiagnosticEnemy
{
	private readonly object sync = new object();

	internal int SendOrdinal { get; private set; }

	internal Identity RuntimeIdentity { get; private set; }

	internal string Name { get; private set; }

	internal string Family { get; private set; }

	internal string SourceGroup { get; private set; }

	internal int ManifestOrdinal { get; private set; }

	internal int SourceInstance { get; private set; }

	internal string SourceCapture { get; private set; }

	internal string PositionText { get; private set; }

	internal int Level { get; private set; }

	internal DateTime StartedUtc { get; private set; }

	internal DateTime? CompletedUtc { get; set; }

	internal int ScfuBytes { get; private set; }

	internal int WeaponCount { get; private set; }

	internal int WeaponBytes { get; private set; }

	internal int CharInPlayBytes { get; private set; }

	internal int PacketCount { get; private set; }

	internal int TotalBytes { get; private set; }

	internal int CumulativeNpcCount { get; set; }

	internal int CumulativePacketCount { get; set; }

	internal long CumulativeBytes { get; set; }

	internal bool LedgerWritten { get; set; }

	internal SubwayVisibilityDiagnosticEnemy(int sendOrdinal, Identity runtimeIdentity, string name, string family, string sourceGroup, int manifestOrdinal, int sourceInstance, string sourceCapture, float x, float y, float z, int level, DateTime startedUtc)
	{
		//IL_001c: Unknown result type (might be due to invalid IL or missing references)
		SendOrdinal = sendOrdinal;
		RuntimeIdentity = runtimeIdentity;
		Name = name ?? string.Empty;
		Family = family ?? string.Empty;
		SourceGroup = sourceGroup ?? string.Empty;
		ManifestOrdinal = manifestOrdinal;
		SourceInstance = sourceInstance;
		SourceCapture = sourceCapture ?? string.Empty;
		PositionText = string.Format(CultureInfo.InvariantCulture, "{0:0.######}|{1:0.######}|{2:0.######}", x, y, z);
		Level = level;
		StartedUtc = startedUtc;
	}

	internal void AddPacket(SubwayVisibilityDiagnosticPacketKind kind, int size)
	{
		lock (sync)
		{
			PacketCount++;
			TotalBytes += size;
			switch (kind)
			{
			case SubwayVisibilityDiagnosticPacketKind.SimpleCharFullUpdate:
				ScfuBytes = size;
				break;
			case SubwayVisibilityDiagnosticPacketKind.WeaponDefinition:
				WeaponCount++;
				WeaponBytes += size;
				break;
			default:
				CharInPlayBytes = size;
				break;
			}
		}
	}
}
