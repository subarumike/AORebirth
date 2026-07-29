using System;

namespace ZoneEngine.Core;

public sealed class PacketSequencingCoordinator
{
	public void BeginSessionReadyBlock(Action enterReadyBlock)
	{
		Execute(enterReadyBlock, "enterReadyBlock");
	}

	public void RunSessionReadyFullCharacterSequence(Action recordReadyBlockBegin, Action recordSimpleCharFullUpdate, Action sendSimpleCharFullUpdate, Action prepareFullCharacterState, Action sendPreFullCharacterReadyBlock, Action recordFullCharacter, Action enterFullCharacterBoundary, Action sendFullCharacter, Action sendPlayfieldReadyBlock, Action recordReadyBlockEnd)
	{
		Execute(recordReadyBlockBegin, "recordReadyBlockBegin");
		Execute(recordSimpleCharFullUpdate, "recordSimpleCharFullUpdate");
		Execute(sendSimpleCharFullUpdate, "sendSimpleCharFullUpdate");
		Execute(prepareFullCharacterState, "prepareFullCharacterState");
		Execute(sendPreFullCharacterReadyBlock, "sendPreFullCharacterReadyBlock");
		Execute(recordFullCharacter, "recordFullCharacter");
		Execute(enterFullCharacterBoundary, "enterFullCharacterBoundary");
		Execute(sendFullCharacter, "sendFullCharacter");
		Execute(sendPlayfieldReadyBlock, "sendPlayfieldReadyBlock");
		Execute(recordReadyBlockEnd, "recordReadyBlockEnd");
	}

	public void RunVisibilityInitializationSequence(Action recordJoinerReady, Action enterCharInPlay, Action announceJoiningCharacter, Action sendExistingCharacterSnapshots)
	{
		Execute(recordJoinerReady, "recordJoinerReady");
		Execute(enterCharInPlay, "enterCharInPlay");
		Execute(announceJoiningCharacter, "announceJoiningCharacter");
		Execute(sendExistingCharacterSnapshots, "sendExistingCharacterSnapshots");
	}

	public void RunSimpleCharFullUpdateCharInPlaySequence(Action recordSimpleCharFullUpdate, Action sendSimpleCharFullUpdate, Action prepareCharInPlay, Action recordCharInPlay, Action sendCharInPlay)
	{
		Execute(recordSimpleCharFullUpdate, "recordSimpleCharFullUpdate");
		Execute(sendSimpleCharFullUpdate, "sendSimpleCharFullUpdate");
		Execute(prepareCharInPlay, "prepareCharInPlay");
		Execute(recordCharInPlay, "recordCharInPlay");
		Execute(sendCharInPlay, "sendCharInPlay");
	}

	public void RunPrivateCityPreFullCharacterOrgInitSequence(Action sendOrgInfoPacket, Action sendInitialSocialStatus, Action sendOrganizationId, Action sendOrganizationRank, Action sendSocialStatusRepeat1, Action sendSocialStatusRepeat2, Action sendSocialStatusRepeat3, Action recordOrgInitSent)
	{
		Execute(sendOrgInfoPacket, "sendOrgInfoPacket");
		Execute(sendInitialSocialStatus, "sendInitialSocialStatus");
		Execute(sendOrganizationId, "sendOrganizationId");
		Execute(sendOrganizationRank, "sendOrganizationRank");
		Execute(sendSocialStatusRepeat1, "sendSocialStatusRepeat1");
		Execute(sendSocialStatusRepeat2, "sendSocialStatusRepeat2");
		Execute(sendSocialStatusRepeat3, "sendSocialStatusRepeat3");
		Execute(recordOrgInitSent, "recordOrgInitSent");
	}

	public void RunPrivateCityPlayfieldReadyBlockSequence(Action sendPlayfieldAllTowers, Action recordPlayfieldAllTowers, Action sendPlayfieldAllCities, Action recordPlayfieldAllCities, Action recordTowersCitiesSent)
	{
		Execute(sendPlayfieldAllTowers, "sendPlayfieldAllTowers");
		Execute(recordPlayfieldAllTowers, "recordPlayfieldAllTowers");
		Execute(sendPlayfieldAllCities, "sendPlayfieldAllCities");
		Execute(recordPlayfieldAllCities, "recordPlayfieldAllCities");
		Execute(recordTowersCitiesSent, "recordTowersCitiesSent");
	}

	public void RunPlayfieldTransferBeginSequence(Action enterZoningPhase, Action sendTeleportPacket)
	{
		Execute(enterZoningPhase, "enterZoningPhase");
		Execute(sendTeleportPacket, "sendTeleportPacket");
	}

	public void CompleteSessionInitialization(Action completeInPlay)
	{
		Execute(completeInPlay, "completeInPlay");
	}

	private static void Execute(Action action, string argumentName)
	{
		if (action == null)
		{
			throw new ArgumentNullException(argumentName);
		}
		action();
	}
}
