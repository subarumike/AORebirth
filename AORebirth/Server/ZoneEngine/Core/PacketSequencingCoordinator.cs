#region License

// Copyright (c) 2015-2025 The WCell Core Contributors
//
// This file is part of WCell.
//
// WCell is free software: you can redistribute it and/or modify it under the
// terms of the GNU Lesser General Public License as published by the Free
// Software Foundation, either version 3 of the License, or (at your option)
// any later version.
//
// WCell is distributed in the hope that it will be useful, but WITHOUT ANY
// WARRANTY; without even the implied warranty of MERCHANTABILITY or FITNESS
// FOR A PARTICULAR PURPOSE. See the GNU Lesser General Public License for more
// details.
//
// You should have received a copy of the GNU Lesser General Public License
// along with WCell. If not, see <http://www.gnu.org/licenses/>.

#endregion

namespace ZoneEngine.Core
{
    using System;

    public sealed class PacketSequencingCoordinator
    {
        public void BeginSessionReadyBlock(Action enterReadyBlock)
        {
            Execute(enterReadyBlock, "enterReadyBlock");
        }

        public void RunSessionReadyFullCharacterSequence(
            Action recordReadyBlockBegin,
            Action recordSimpleCharFullUpdate,
            Action sendSimpleCharFullUpdate,
            Action prepareFullCharacterState,
            Action sendPreFullCharacterReadyBlock,
            Action recordFullCharacter,
            Action enterFullCharacterBoundary,
            Action sendFullCharacter,
            Action sendPlayfieldReadyBlock,
            Action recordReadyBlockEnd)
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

        public void RunVisibilityInitializationSequence(
            Action recordJoinerReady,
            Action enterCharInPlay,
            Action announceJoiningCharacter,
            Action sendExistingCharacterSnapshots)
        {
            Execute(recordJoinerReady, "recordJoinerReady");
            Execute(enterCharInPlay, "enterCharInPlay");
            Execute(announceJoiningCharacter, "announceJoiningCharacter");
            Execute(sendExistingCharacterSnapshots, "sendExistingCharacterSnapshots");
        }

        public void RunSimpleCharFullUpdateCharInPlaySequence(
            Action recordSimpleCharFullUpdate,
            Action sendSimpleCharFullUpdate,
            Action prepareCharInPlay,
            Action recordCharInPlay,
            Action sendCharInPlay)
        {
            Execute(recordSimpleCharFullUpdate, "recordSimpleCharFullUpdate");
            Execute(sendSimpleCharFullUpdate, "sendSimpleCharFullUpdate");
            Execute(prepareCharInPlay, "prepareCharInPlay");
            Execute(recordCharInPlay, "recordCharInPlay");
            Execute(sendCharInPlay, "sendCharInPlay");
        }

        public void RunPrivateCityPreFullCharacterOrgInitSequence(
            Action sendOrgInfoPacket,
            Action sendInitialSocialStatus,
            Action sendOrganizationId,
            Action sendOrganizationRank,
            Action sendSocialStatusRepeat1,
            Action sendSocialStatusRepeat2,
            Action sendSocialStatusRepeat3,
            Action recordOrgInitSent)
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

        public void RunPrivateCityPlayfieldReadyBlockSequence(
            Action sendPlayfieldAllTowers,
            Action recordPlayfieldAllTowers,
            Action sendPlayfieldAllCities,
            Action recordPlayfieldAllCities,
            Action recordTowersCitiesSent)
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
}
