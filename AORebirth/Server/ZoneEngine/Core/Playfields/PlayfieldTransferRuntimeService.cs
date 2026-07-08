namespace ZoneEngine.Core.Playfields
{
    #region Usings ...

    using System;

    using AORebirth.Core.Entities;
    using AORebirth.Core.Playfields;
    using AORebirth.Core.Vector;
    using AORebirth.Interfaces;

    using SmokeLounge.AOtomation.Messaging.GameData;

    using ZoneEngine.Core;

    #endregion

    internal sealed class PlayfieldTransferRuntimeService
    {
        private readonly PlayfieldLifecycleRuntimeService lifecycle;
        private readonly PlayfieldPacketSequencingRuntimeService packetSequences;

        internal PlayfieldTransferRuntimeService(
            PlayfieldLifecycleRuntimeService lifecycle,
            PlayfieldPacketSequencingRuntimeService packetSequences)
        {
            if (lifecycle == null)
            {
                throw new ArgumentNullException("lifecycle");
            }

            if (packetSequences == null)
            {
                throw new ArgumentNullException("packetSequences");
            }

            this.lifecycle = lifecycle;
            this.packetSequences = packetSequences;
        }

        internal void TransferToPlayfield(
            Dynel dynel,
            Coordinate destination,
            IQuaternion heading,
            Identity playfield,
            Action<int> clearTransferContactState,
            Action<Dynel> disableTimers,
            Func<Dynel, Action> captureEnterZoningPhase,
            Action sendTeleportPacket,
            Action<Dynel> announceDespawn,
            Action<Dynel, Coordinate, IQuaternion> applyTransferState,
            Func<Dynel, ZoneClient> captureClient,
            Func<Identity, IPlayfield> resolveDestinationPlayfield,
            Action<Dynel, IPlayfield> finalizeTransferDispose,
            Action<ZoneClient> sendRedirect)
        {
            Require(clearTransferContactState, "clearTransferContactState");
            Require(disableTimers, "disableTimers");
            Require(captureEnterZoningPhase, "captureEnterZoningPhase");
            Require(sendTeleportPacket, "sendTeleportPacket");

            this.lifecycle.PreparePlayfieldTransfer(
                dynel,
                clearTransferContactState,
                disableTimers);

            Action enterZoningPhase = captureEnterZoningPhase(dynel);
            if (enterZoningPhase != null)
            {
                this.packetSequences.RunPlayfieldTransferBeginSequence(
                    enterZoningPhase,
                    sendTeleportPacket);
            }
            else
            {
                sendTeleportPacket();
            }

            this.CompletePlayfieldTransfer(
                dynel,
                destination,
                heading,
                playfield,
                announceDespawn,
                applyTransferState,
                captureClient,
                resolveDestinationPlayfield,
                finalizeTransferDispose,
                sendRedirect);
        }

        internal void CompletePlayfieldTransfer(
            Dynel dynel,
            Coordinate destination,
            IQuaternion heading,
            Identity playfield,
            Action<Dynel> announceDespawn,
            Action<Dynel, Coordinate, IQuaternion> applyTransferState,
            Func<Dynel, ZoneClient> captureClient,
            Func<Identity, IPlayfield> resolveDestinationPlayfield,
            Action<Dynel, IPlayfield> finalizeTransferDispose,
            Action<ZoneClient> sendRedirect)
        {
            Require(announceDespawn, "announceDespawn");
            Require(applyTransferState, "applyTransferState");
            Require(captureClient, "captureClient");
            Require(resolveDestinationPlayfield, "resolveDestinationPlayfield");
            Require(finalizeTransferDispose, "finalizeTransferDispose");
            Require(sendRedirect, "sendRedirect");

            announceDespawn(dynel);
            applyTransferState(dynel, destination, heading);

            ZoneClient client = captureClient(dynel);
            IPlayfield newPlayfield = resolveDestinationPlayfield(playfield);

            finalizeTransferDispose(dynel, newPlayfield);
            sendRedirect(client);
        }

        private static void Require(Delegate callback, string name)
        {
            if (callback == null)
            {
                throw new ArgumentNullException(name);
            }
        }
    }
}
