using System;

namespace ZoneEngine.Core.Playfields;

internal sealed class PlayfieldPacketSequencingRuntimeService
{
	private readonly PacketSequencingCoordinator packetSequencing;

	internal PlayfieldPacketSequencingRuntimeService(PacketSequencingCoordinator packetSequencing)
	{
		if (packetSequencing == null)
		{
			throw new ArgumentNullException("packetSequencing");
		}
		this.packetSequencing = packetSequencing;
	}

	internal void RunVisibilityPacketPairSequence(Action recordSimpleCharFullUpdate, Action sendSimpleCharFullUpdate, Action prepareCharInPlay, Action recordCharInPlay, Action sendCharInPlay)
	{
		packetSequencing.RunSimpleCharFullUpdateCharInPlaySequence(recordSimpleCharFullUpdate, sendSimpleCharFullUpdate, prepareCharInPlay, recordCharInPlay, sendCharInPlay);
	}

	internal void RunPlayfieldTransferBeginSequence(Action enterZoningPhase, Action sendTeleportPacket)
	{
		packetSequencing.RunPlayfieldTransferBeginSequence(enterZoningPhase, sendTeleportPacket);
	}
}
