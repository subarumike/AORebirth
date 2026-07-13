namespace ZoneEngine.Core.Playfields
{
    #region Usings

    using System;
    using System.Collections.Generic;
    using System.Globalization;

    using AORebirth.Core.Entities;
    using AORebirth.Interfaces;

    using SmokeLounge.AOtomation.Messaging.GameData;
    using SmokeLounge.AOtomation.Messaging.Messages;
    using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;

    using Utility;

    using ZoneEngine.Core.Packets;

    #endregion

    internal sealed class PlayfieldVisibilityPacketRuntimeService
    {
        private readonly PlayfieldVisibilityFanoutRuntimeService visibilityFanout;

        private readonly PlayfieldPacketSequencingRuntimeService packetSequences;

        internal PlayfieldVisibilityPacketRuntimeService(
            PlayfieldVisibilityFanoutRuntimeService visibilityFanout,
            PlayfieldPacketSequencingRuntimeService packetSequences)
        {
            if (visibilityFanout == null)
            {
                throw new ArgumentNullException("visibilityFanout");
            }

            if (packetSequences == null)
            {
                throw new ArgumentNullException("packetSequences");
            }

            this.visibilityFanout = visibilityFanout;
            this.packetSequences = packetSequences;
        }

        internal void SendExistingCharacterVisibilityToClient(
            ICharacter recipient,
            IEnumerable<ICharacter> characters,
            Action<MessageBody> sendVisibilityMessage)
        {
            Require(sendVisibilityMessage, "sendVisibilityMessage");

            Identity playfieldIdentity = recipient.Playfield.Identity;
            SubwayVisibilityDiagnosticSnapshot diagnosticSnapshot =
                SubwayVisibilitySnapshotDiagnostics.TryBeginSnapshot(recipient, 0);
            if (diagnosticSnapshot != null)
            {
                int candidateNpcs = 0;
                foreach (ICharacter candidate in characters)
                {
                    Character candidateCharacter = candidate as Character;
                    if (candidateCharacter != null
                        && candidate.Identity != recipient.Identity
                        && candidate.InPlayfield(playfieldIdentity)
                        && (candidate.Controller == null || candidate.Controller.Client == null))
                    {
                        candidateNpcs++;
                    }
                }

                diagnosticSnapshot.SetTotalCandidateNpcs(candidateNpcs);
            }

            this.visibilityFanout.FanoutExistingCharactersForScfu(
                recipient,
                characters,
                entity =>
                    {
                        Character temp = entity as Character;
                        if (temp == null)
                        {
                            return false;
                        }

                        SubwayVisibilityDiagnosticEnemy diagnosticEnemy =
                            diagnosticSnapshot == null ? null : diagnosticSnapshot.BeginEnemy(temp);
                        try
                        {
                            SimpleCharFullUpdateMessage simpleCharFullUpdate = SimpleCharFullUpdate.ConstructMessage(temp);
                            CharInPlayMessage charInPlay = null;
                            this.packetSequences.RunVisibilityPacketPairSequence(
                                () => PlayfieldLifecycleTrace.Record(
                                    PlayfieldLifecycleTrace.FlowSamePlayfieldVisibility,
                                    PlayfieldLifecycleTrace.StageExistingCharacterSimpleCharFullUpdate,
                                    PlayfieldLifecycleTrace.MessageSimpleCharFullUpdate,
                                    temp.Identity,
                                    "recipient=" + recipient.Identity),
                                () =>
                                {
                                    using (SubwayVisibilitySnapshotDiagnostics.BeginPacket(
                                        diagnosticSnapshot,
                                        diagnosticEnemy,
                                        SubwayVisibilityDiagnosticPacketKind.SimpleCharFullUpdate,
                                        0))
                                    {
                                        sendVisibilityMessage(simpleCharFullUpdate);
                                    }

                                    this.SendWeaponDefinitionsForVisibility(
                                        temp,
                                        recipient,
                                        sendVisibilityMessage,
                                        diagnosticSnapshot,
                                        diagnosticEnemy);
                                },
                                () => { charInPlay = new CharInPlayMessage { Identity = temp.Identity, Unknown = 0x00 }; },
                                () => PlayfieldLifecycleTrace.Record(
                                    PlayfieldLifecycleTrace.FlowSamePlayfieldVisibility,
                                    PlayfieldLifecycleTrace.StageExistingCharacterCharInPlay,
                                    PlayfieldLifecycleTrace.MessageCharInPlay,
                                    temp.Identity,
                                    "recipient=" + recipient.Identity),
                                () =>
                                {
                                    using (SubwayVisibilitySnapshotDiagnostics.BeginPacket(
                                        diagnosticSnapshot,
                                        diagnosticEnemy,
                                        SubwayVisibilityDiagnosticPacketKind.CharInPlay,
                                        0))
                                    {
                                        sendVisibilityMessage(charInPlay);
                                    }
                                });
                            if (diagnosticSnapshot != null)
                            {
                                diagnosticSnapshot.MarkEnemyQueued(diagnosticEnemy);
                            }

                            return true;
                        }
                        catch (Exception exception)
                        {
                            if (diagnosticSnapshot != null)
                            {
                                diagnosticSnapshot.RecordFailure(diagnosticEnemy, "enemy_visibility_sequence", exception);
                            }

                            throw;
                        }
                    },
                (entity, senderEqualsRecipient, senderInRecipientPlayfield, sent) =>
                    {
                        Identity senderPlayfield = entity.Playfield == null ? Identity.None : entity.Playfield.Identity;
                        LogUtil.Debug(
                            DebugInfoDetail.NetworkMessages,
                            string.Format(
                                CultureInfo.InvariantCulture,
                                "PlayerVisibilitySCFU sender={0}/{1} recipient={2}/{3} senderPf={4} recipientPf={5} self={6} inPlayfield={7} rangeRejected=False sent={8}",
                                entity.Identity,
                                entity.Name,
                                recipient.Identity,
                                recipient.Name,
                                senderPlayfield,
                                playfieldIdentity,
                                senderEqualsRecipient,
                                senderInRecipientPlayfield,
                                sent));
                    });
            if (diagnosticSnapshot != null)
            {
                diagnosticSnapshot.MarkSnapshotEnqueueCompleted();
            }
        }

        internal void AnnounceJoiningCharacterVisibility(ICharacter character, Action<MessageBody> announceVisibilityMessage)
        {
            Require(announceVisibilityMessage, "announceVisibilityMessage");

            Character temp = character as Character;
            if (temp == null)
            {
                return;
            }

            CharInPlayMessage charInPlay = null;
            this.packetSequences.RunVisibilityPacketPairSequence(
                () => PlayfieldLifecycleTrace.Record(
                    PlayfieldLifecycleTrace.FlowSamePlayfieldVisibility,
                    PlayfieldLifecycleTrace.StageJoiningCharacterSimpleCharFullUpdateBroadcast,
                    PlayfieldLifecycleTrace.MessageSimpleCharFullUpdate,
                    temp.Identity),
                () =>
                {
                    announceVisibilityMessage(SimpleCharFullUpdate.ConstructMessage(temp));
                    this.SendWeaponDefinitionsForVisibility(
                        temp,
                        null,
                        announceVisibilityMessage,
                        null,
                        null);
                },
                () => { charInPlay = new CharInPlayMessage { Identity = temp.Identity, Unknown = 0x00 }; },
                () => PlayfieldLifecycleTrace.Record(
                    PlayfieldLifecycleTrace.FlowSamePlayfieldVisibility,
                    PlayfieldLifecycleTrace.StageJoiningCharacterCharInPlayBroadcast,
                    PlayfieldLifecycleTrace.MessageCharInPlay,
                    temp.Identity),
                () => announceVisibilityMessage(charInPlay));
        }

        private void SendWeaponDefinitionsForVisibility(
            ICharacter owner,
            ICharacter recipient,
            Action<MessageBody> sendVisibilityMessage,
            SubwayVisibilityDiagnosticSnapshot diagnosticSnapshot,
            SubwayVisibilityDiagnosticEnemy diagnosticEnemy)
        {
            if (diagnosticSnapshot != null)
            {
                diagnosticSnapshot.MarkWeaponPhaseStarted(diagnosticEnemy);
            }

            int weaponIndex = 0;
            foreach (WeaponItemFullUpdateMessage message in WeaponItemFullUpdate.CreateWeaponDefinitionMessages(owner))
            {
                weaponIndex++;
                using (SubwayVisibilitySnapshotDiagnostics.BeginPacket(
                    diagnosticSnapshot,
                    diagnosticEnemy,
                    SubwayVisibilityDiagnosticPacketKind.WeaponDefinition,
                    weaponIndex))
                {
                    sendVisibilityMessage(message);
                }

                WeaponItemFullUpdate.LogObserverWeaponDefinition(owner, recipient, message);
            }

            if (diagnosticSnapshot != null)
            {
                diagnosticSnapshot.MarkWeaponPhaseCompleted(diagnosticEnemy);
            }
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
