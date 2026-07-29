using System;
using AORebirth.Core.Entities;
using AORebirth.Core.Playfields;
using AORebirth.Core.Vector;
using AORebirth.Interfaces;
using SmokeLounge.AOtomation.Messaging.GameData;

namespace ZoneEngine.Core.Playfields;

internal sealed class PlayfieldTransferRuntimeService
{
	private readonly PlayfieldLifecycleRuntimeService lifecycle;

	private readonly PlayfieldPacketSequencingRuntimeService packetSequences;

	internal PlayfieldTransferRuntimeService(PlayfieldLifecycleRuntimeService lifecycle, PlayfieldPacketSequencingRuntimeService packetSequences)
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

	internal void TransferToPlayfield(Dynel dynel, Coordinate destination, IQuaternion heading, Identity playfield, Action<int> clearTransferContactState, Action<Dynel> disableTimers, Func<Dynel, Action> captureEnterZoningPhase, Action sendTeleportPacket, Action<Dynel> announceDespawn, Action<Dynel, Coordinate, IQuaternion> applyTransferState, Func<Dynel, ZoneClient> captureClient, Func<Identity, IPlayfield> resolveDestinationPlayfield, Action<Dynel, IPlayfield> finalizeTransferDispose, Action<ZoneClient> sendRedirect)
	{
		//IL_0078: Unknown result type (might be due to invalid IL or missing references)
		Require(clearTransferContactState, "clearTransferContactState");
		Require(disableTimers, "disableTimers");
		Require(captureEnterZoningPhase, "captureEnterZoningPhase");
		Require(sendTeleportPacket, "sendTeleportPacket");
		lifecycle.PreparePlayfieldTransfer(dynel, clearTransferContactState, disableTimers);
		Action action = captureEnterZoningPhase(dynel);
		if (action != null)
		{
			packetSequences.RunPlayfieldTransferBeginSequence(action, sendTeleportPacket);
		}
		else
		{
			sendTeleportPacket();
		}
		CompletePlayfieldTransfer(dynel, destination, heading, playfield, announceDespawn, applyTransferState, captureClient, resolveDestinationPlayfield, finalizeTransferDispose, sendRedirect);
	}

	internal void CompletePlayfieldTransfer(Dynel dynel, Coordinate destination, IQuaternion heading, Identity playfield, Action<Dynel> announceDespawn, Action<Dynel, Coordinate, IQuaternion> applyTransferState, Func<Dynel, ZoneClient> captureClient, Func<Identity, IPlayfield> resolveDestinationPlayfield, Action<Dynel, IPlayfield> finalizeTransferDispose, Action<ZoneClient> sendRedirect)
	{
		//IL_006e: Unknown result type (might be due to invalid IL or missing references)
		Require(announceDespawn, "announceDespawn");
		Require(applyTransferState, "applyTransferState");
		Require(captureClient, "captureClient");
		Require(resolveDestinationPlayfield, "resolveDestinationPlayfield");
		Require(finalizeTransferDispose, "finalizeTransferDispose");
		Require(sendRedirect, "sendRedirect");
		announceDespawn(dynel);
		applyTransferState(dynel, destination, heading);
		ZoneClient obj = captureClient(dynel);
		IPlayfield arg = resolveDestinationPlayfield(playfield);
		finalizeTransferDispose(dynel, arg);
		sendRedirect(obj);
	}

	private static void Require(Delegate callback, string name)
	{
		if ((object)callback == null)
		{
			throw new ArgumentNullException(name);
		}
	}
}
