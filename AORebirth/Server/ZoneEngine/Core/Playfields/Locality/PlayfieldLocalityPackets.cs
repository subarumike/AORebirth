namespace ZoneEngine.Core.Playfields.Locality
{
    #region Usings

    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Linq;

    using AORebirth.Core.Entities;
    using AORebirth.Enums;
    using AORebirth.Interfaces;

    using SmokeLounge.AOtomation.Messaging.GameData;
    using SmokeLounge.AOtomation.Messaging.Messages;
    using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;

    using Utility;

    using ZoneEngine.Core;
    using ZoneEngine.Core.Controllers;
    using ZoneEngine.Core.Packets;

    #endregion

    internal sealed class PlayfieldLocalityPackets
    {
        private readonly PlayfieldVisibilityFanoutRuntimeService visibilityFanout;
        private readonly PlayfieldPacketSequencingRuntimeService packetSequences;
        private readonly PlayfieldLocalityVisibility visibility;

        internal PlayfieldLocalityPackets(
            PlayfieldVisibilityFanoutRuntimeService visibilityFanout,
            PlayfieldPacketSequencingRuntimeService packetSequences,
            PlayfieldLocalityVisibility visibility)
        {
            this.visibilityFanout = visibilityFanout ?? throw new ArgumentNullException("visibilityFanout");
            this.packetSequences = packetSequences ?? throw new ArgumentNullException("packetSequences");
            this.visibility = visibility ?? throw new ArgumentNullException("visibility");
        }

        internal void SendExistingCharacterVisibilityToClient(
            ICharacter recipient,
            IEnumerable<ICharacter> characters,
            Action<MessageBody> sendVisibilityMessage)
        {
            List<ICharacter> characterSnapshot = characters.Where(x => x != null).ToList();
            this.visibility.Synchronize(characterSnapshot);
            this.visibility.ForgetRecipient(recipient.Identity);
            IList<ICharacter> selectedCharacters = this.visibility.SelectInitialCharacters(recipient);
            Identity playfieldIdentity = recipient.Playfield.Identity;
            SubwayVisibilityDiagnosticSnapshot diagnosticSnapshot =
                SubwayVisibilitySnapshotDiagnostics.TryBeginSnapshot(recipient, 0);
            if (diagnosticSnapshot != null)
            {
                int totalPlayfieldCharacters = 0;
                int totalPlayfieldNpcs = 0;
                int visibilityEligibleCharacters = 0;
                foreach (ICharacter candidate in characterSnapshot)
                {
                    if (!candidate.InPlayfield(playfieldIdentity))
                    {
                        continue;
                    }

                    totalPlayfieldCharacters++;
                    if (candidate.Identity != recipient.Identity)
                    {
                        visibilityEligibleCharacters++;
                    }

                    Character candidateCharacter = candidate as Character;
                    if (candidateCharacter != null
                        && (candidate.Controller == null || candidate.Controller.Client == null))
                    {
                        totalPlayfieldNpcs++;
                    }
                }

                diagnosticSnapshot.RecordSpatialInterestSelection(
                    SubwayVisibilitySpatialInterestMetrics.ForInitialSnapshot(
                        totalPlayfieldCharacters,
                        totalPlayfieldNpcs,
                        visibilityEligibleCharacters,
                        this.visibility.LastCandidateCount,
                        selectedCharacters.Count));
            }

            this.visibilityFanout.FanoutExistingCharactersForScfu(
                recipient,
                selectedCharacters,
                entity =>
                    {
                        Character temp = entity as Character;
                        return temp != null
                               && this.SendCharacterVisibilityEntry(
                                   temp,
                                   recipient,
                                   sendVisibilityMessage,
                                   diagnosticSnapshot,
                                   false);
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

            this.visibility.CompleteInitialRecipient(recipient);
        }

        internal void AnnounceJoiningCharacterVisibility(
            ICharacter character,
            Action<ICharacter, MessageBody> sendVisibilityMessage,
            Action<ICharacter, Identity> sendLeaveVisibility)
        {
            this.visibility.Reconcile(
                character,
                (recipient, source) => this.SendCharacterVisibilityEntry(
                    source,
                    recipient,
                    body => sendVisibilityMessage(recipient, body),
                    null,
                    true),
                sendLeaveVisibility);
        }

        internal bool SendCharacterVisibilityEntry(
            ICharacter source,
            ICharacter recipient,
            Action<MessageBody> sendVisibilityMessage)
        {
            return this.SendCharacterVisibilityEntry(source, recipient, sendVisibilityMessage, null, false);
        }

        private bool SendCharacterVisibilityEntry(
            ICharacter source,
            ICharacter recipient,
            Action<MessageBody> sendVisibilityMessage,
            SubwayVisibilityDiagnosticSnapshot diagnosticSnapshot,
            bool joiningCharacter)
        {
            Character temp = source as Character;
            if (temp == null)
            {
                return false;
            }

            if (temp.Stats[StatIds.health].Value <= 0
                && temp.Controller is NPCController)
            {
                return false;
            }

            if (this.TrySendGuardianVisibilityScfu(
                    temp,
                    recipient,
                    sendVisibilityMessage,
                    diagnosticSnapshot,
                    joiningCharacter))
            {
                return true;
            }

            if (this.TrySendHavarisVisibilityScfu(
                    temp,
                    recipient,
                    sendVisibilityMessage,
                    diagnosticSnapshot,
                    joiningCharacter))
            {
                return true;
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
                        joiningCharacter
                            ? PlayfieldLifecycleTrace.StageJoiningCharacterSimpleCharFullUpdateBroadcast
                            : PlayfieldLifecycleTrace.StageExistingCharacterSimpleCharFullUpdate,
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

                        if (temp.Stats[StatIds.npcfamily].Value == 0)
                        {
                            int wireLevel = CombatXpRuntimeService.ResolveWireLevel(temp);
                            sendVisibilityMessage(
                                new StatMessage
                                {
                                    Identity = temp.Identity,
                                    Unknown = 0,
                                    Stats = new[]
                                              {
                                                  new GameTuple<CharacterStat, uint>
                                                  {
                                                      Value1 = (CharacterStat)(int)StatIds.level,
                                                      Value2 = (uint)wireLevel
                                                  }
                                              }
                                });
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
                        joiningCharacter
                            ? PlayfieldLifecycleTrace.StageJoiningCharacterCharInPlayBroadcast
                            : PlayfieldLifecycleTrace.StageExistingCharacterCharInPlay,
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

                this.visibility.MarkVisibleEntry(recipient, source);
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
        }

        private bool TrySendGuardianVisibilityScfu(
            Character pet,
            ICharacter recipient,
            Action<MessageBody> sendVisibilityMessage,
            SubwayVisibilityDiagnosticSnapshot diagnosticSnapshot,
            bool joiningCharacter)
        {
            if (!PetBureaucratGuardianAppearance.IsGuardianPet(pet))
            {
                return false;
            }

            int summonNanoId = PetBureaucratGuardianAppearance.ResolveSummonNanoId(pet);
            ICharacter owner = PetCombatRules.ResolvePetOwner(pet);
            ZoneClient recipientClient = recipient.Controller != null
                ? recipient.Controller.Client as ZoneClient
                : null;
            if (summonNanoId <= 0 || owner == null || recipientClient == null)
            {
                return false;
            }

            SubwayVisibilityDiagnosticEnemy diagnosticEnemy =
                diagnosticSnapshot == null ? null : diagnosticSnapshot.BeginEnemy(pet);
            try
            {
                CharInPlayMessage charInPlay = null;
                this.packetSequences.RunVisibilityPacketPairSequence(
                    () => PlayfieldLifecycleTrace.Record(
                        PlayfieldLifecycleTrace.FlowSamePlayfieldVisibility,
                        joiningCharacter
                            ? PlayfieldLifecycleTrace.StageJoiningCharacterSimpleCharFullUpdateBroadcast
                            : PlayfieldLifecycleTrace.StageExistingCharacterSimpleCharFullUpdate,
                        PlayfieldLifecycleTrace.MessageSimpleCharFullUpdate,
                        pet.Identity,
                        "recipient=" + recipient.Identity + " guardianWire=true"),
                    () =>
                    {
                        using (SubwayVisibilitySnapshotDiagnostics.BeginPacket(
                            diagnosticSnapshot,
                            diagnosticEnemy,
                            SubwayVisibilityDiagnosticPacketKind.SimpleCharFullUpdate,
                            0))
                        {
                            PetBureaucratGuardianScfuWire.SendToRecipient(
                                recipientClient,
                                owner,
                                pet,
                                summonNanoId);
                        }

                        WeaponItemFullUpdateMessage weaponMessage =
                            WeaponItemFullUpdate.CreateRightHandWeaponDefinitionMessage(pet);
                        if (weaponMessage != null)
                        {
                            using (SubwayVisibilitySnapshotDiagnostics.BeginPacket(
                                diagnosticSnapshot,
                                diagnosticEnemy,
                                SubwayVisibilityDiagnosticPacketKind.WeaponDefinition,
                                1))
                            {
                                sendVisibilityMessage(weaponMessage);
                            }

                            WeaponItemFullUpdate.LogObserverWeaponDefinition(pet, recipient, weaponMessage);
                        }
                    },
                    () => { charInPlay = new CharInPlayMessage { Identity = pet.Identity, Unknown = 0x00 }; },
                    () => PlayfieldLifecycleTrace.Record(
                        PlayfieldLifecycleTrace.FlowSamePlayfieldVisibility,
                        joiningCharacter
                            ? PlayfieldLifecycleTrace.StageJoiningCharacterCharInPlayBroadcast
                            : PlayfieldLifecycleTrace.StageExistingCharacterCharInPlay,
                        PlayfieldLifecycleTrace.MessageCharInPlay,
                        pet.Identity,
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

                this.visibility.MarkVisibleEntry(recipient, pet);
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
                    diagnosticSnapshot.RecordFailure(diagnosticEnemy, "guardian_visibility_sequence", exception);
                }

                throw;
            }
        }

        private bool TrySendHavarisVisibilityScfu(
            Character npc,
            ICharacter recipient,
            Action<MessageBody> sendVisibilityMessage,
            SubwayVisibilityDiagnosticSnapshot diagnosticSnapshot,
            bool joiningCharacter)
        {
            if (!NascenceDungeon1HavarisScfuWire.IsHavaris(npc)
                && !NascenceDungeon2HavarisScfuWire.IsHavaris(npc)
                && !NascenceDungeon3HavarisScfuWire.IsHavaris(npc)
                && !NascenceDungeon4HavarisScfuWire.IsHavaris(npc))
            {
                return false;
            }

            ZoneClient recipientClient = recipient.Controller != null
                ? recipient.Controller.Client as ZoneClient
                : null;
            if (recipientClient == null)
            {
                return false;
            }

            SubwayVisibilityDiagnosticEnemy diagnosticEnemy =
                diagnosticSnapshot == null ? null : diagnosticSnapshot.BeginEnemy(npc);
            try
            {
                CharInPlayMessage charInPlay = null;
                this.packetSequences.RunVisibilityPacketPairSequence(
                    () => PlayfieldLifecycleTrace.Record(
                        PlayfieldLifecycleTrace.FlowSamePlayfieldVisibility,
                        joiningCharacter
                            ? PlayfieldLifecycleTrace.StageJoiningCharacterSimpleCharFullUpdateBroadcast
                            : PlayfieldLifecycleTrace.StageExistingCharacterSimpleCharFullUpdate,
                        PlayfieldLifecycleTrace.MessageSimpleCharFullUpdate,
                        npc.Identity,
                        "recipient=" + recipient.Identity + " havarisWire=true"),
                    () =>
                    {
                        using (SubwayVisibilitySnapshotDiagnostics.BeginPacket(
                            diagnosticSnapshot,
                            diagnosticEnemy,
                            SubwayVisibilityDiagnosticPacketKind.SimpleCharFullUpdate,
                            0))
                        {
                            if (NascenceDungeon4HavarisScfuWire.IsHavaris(npc))
                            {
                                NascenceDungeon4HavarisScfuWire.SendToRecipient(recipientClient, npc);
                            }
                            else if (NascenceDungeon3HavarisScfuWire.IsHavaris(npc))
                            {
                                NascenceDungeon3HavarisScfuWire.SendToRecipient(recipientClient, npc);
                            }
                            else if (NascenceDungeon2HavarisScfuWire.IsHavaris(npc))
                            {
                                NascenceDungeon2HavarisScfuWire.SendToRecipient(recipientClient, npc);
                            }
                            else
                            {
                                NascenceDungeon1HavarisScfuWire.SendToRecipient(recipientClient, npc);
                            }
                        }

                        this.SendWeaponDefinitionsForVisibility(
                            npc,
                            recipient,
                            sendVisibilityMessage,
                            diagnosticSnapshot,
                            diagnosticEnemy);
                    },
                    () => { charInPlay = new CharInPlayMessage { Identity = npc.Identity, Unknown = 0x00 }; },
                    () => PlayfieldLifecycleTrace.Record(
                        PlayfieldLifecycleTrace.FlowSamePlayfieldVisibility,
                        joiningCharacter
                            ? PlayfieldLifecycleTrace.StageJoiningCharacterCharInPlayBroadcast
                            : PlayfieldLifecycleTrace.StageExistingCharacterCharInPlay,
                        PlayfieldLifecycleTrace.MessageCharInPlay,
                        npc.Identity,
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

                this.visibility.MarkVisibleEntry(recipient, npc);
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
                    diagnosticSnapshot.RecordFailure(diagnosticEnemy, "havaris_visibility_sequence", exception);
                }

                throw;
            }
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
    }
}
