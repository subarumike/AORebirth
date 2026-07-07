namespace ZoneEngine.Core.Playfields
{
    #region Usings ...

    using System;

    using ZoneEngine.Core;

    #endregion

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

        internal void RunVisibilityPacketPairSequence(
            Action recordSimpleCharFullUpdate,
            Action sendSimpleCharFullUpdate,
            Action prepareCharInPlay,
            Action recordCharInPlay,
            Action sendCharInPlay)
        {
            this.packetSequencing.RunSimpleCharFullUpdateCharInPlaySequence(
                recordSimpleCharFullUpdate,
                sendSimpleCharFullUpdate,
                prepareCharInPlay,
                recordCharInPlay,
                sendCharInPlay);
        }

        internal void RunPlayfieldTransferBeginSequence(Action enterZoningPhase, Action sendTeleportPacket)
        {
            this.packetSequencing.RunPlayfieldTransferBeginSequence(enterZoningPhase, sendTeleportPacket);
        }
    }
}
