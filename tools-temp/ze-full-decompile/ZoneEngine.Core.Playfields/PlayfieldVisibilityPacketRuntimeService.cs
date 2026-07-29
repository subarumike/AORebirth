using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using AORebirth.Core.Entities;
using AORebirth.Interfaces;
using AORebirth.ObjectManager;
using SmokeLounge.AOtomation.Messaging.GameData;
using SmokeLounge.AOtomation.Messaging.Messages;
using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;
using Utility;
using ZoneEngine.Core.Packets;

namespace ZoneEngine.Core.Playfields;

internal sealed class PlayfieldVisibilityPacketRuntimeService
{
	private readonly PlayfieldVisibilityFanoutRuntimeService visibilityFanout;

	private readonly PlayfieldPacketSequencingRuntimeService packetSequences;

	private readonly PlayfieldVisibilityInterestRuntimeService visibilityInterest;

	internal PlayfieldVisibilityPacketRuntimeService(PlayfieldVisibilityFanoutRuntimeService visibilityFanout, PlayfieldPacketSequencingRuntimeService packetSequences, PlayfieldVisibilityInterestRuntimeService visibilityInterest)
	{
		if (visibilityFanout == null)
		{
			throw new ArgumentNullException("visibilityFanout");
		}
		if (packetSequences == null)
		{
			throw new ArgumentNullException("packetSequences");
		}
		if (visibilityInterest == null)
		{
			throw new ArgumentNullException("visibilityInterest");
		}
		this.visibilityFanout = visibilityFanout;
		this.packetSequences = packetSequences;
		this.visibilityInterest = visibilityInterest;
	}

	internal void SendExistingCharacterVisibilityToClient(ICharacter recipient, IEnumerable<ICharacter> characters, Action<MessageBody> sendVisibilityMessage)
	{
		//IL_0071: Unknown result type (might be due to invalid IL or missing references)
		//IL_009a: Unknown result type (might be due to invalid IL or missing references)
		//IL_009f: Unknown result type (might be due to invalid IL or missing references)
		//IL_00eb: Unknown result type (might be due to invalid IL or missing references)
		//IL_0109: Unknown result type (might be due to invalid IL or missing references)
		//IL_0114: Unknown result type (might be due to invalid IL or missing references)
		Require(sendVisibilityMessage, "sendVisibilityMessage");
		List<ICharacter> list = characters.Where((ICharacter x) => x != null).ToList();
		visibilityInterest.Synchronize(list);
		visibilityInterest.ForgetRecipient(((IEntity)recipient).Identity);
		IList<ICharacter> list2 = visibilityInterest.SelectInitialCharacters(recipient);
		Identity playfieldIdentity = ((IEntity)((IInstancedEntity)recipient).Playfield).Identity;
		SubwayVisibilityDiagnosticSnapshot diagnosticSnapshot = SubwayVisibilitySnapshotDiagnostics.TryBeginSnapshot(recipient, 0);
		if (diagnosticSnapshot != null)
		{
			int num = 0;
			int num2 = 0;
			int num3 = 0;
			foreach (ICharacter item in list)
			{
				if (((IDynel)item).InPlayfield(playfieldIdentity))
				{
					num++;
					if (((IEntity)item).Identity != ((IEntity)recipient).Identity)
					{
						num3++;
					}
					Character val = (Character)(object)((item is Character) ? item : null);
					if (val != null && (((IDynel)item).Controller == null || ((IDynel)item).Controller.Client == null))
					{
						num2++;
					}
				}
			}
			diagnosticSnapshot.RecordSpatialInterestSelection(SubwayVisibilitySpatialInterestMetrics.ForInitialSnapshot(num, num2, num3, visibilityInterest.LastCandidateInspectionCount, list2.Count));
		}
		visibilityFanout.FanoutExistingCharactersForScfu(recipient, list2, delegate(ICharacter entity)
		{
			Character val3 = (Character)(object)((entity is Character) ? entity : null);
			return val3 != null && SendCharacterVisibilityEntry((ICharacter)(object)val3, recipient, sendVisibilityMessage, diagnosticSnapshot, joiningCharacter: false);
		}, delegate(ICharacter entity, bool senderEqualsRecipient, bool senderInRecipientPlayfield, bool sent)
		{
			//IL_0016: Unknown result type (might be due to invalid IL or missing references)
			//IL_000f: Unknown result type (might be due to invalid IL or missing references)
			//IL_001b: Unknown result type (might be due to invalid IL or missing references)
			//IL_0032: Unknown result type (might be due to invalid IL or missing references)
			//IL_004e: Unknown result type (might be due to invalid IL or missing references)
			//IL_0069: Unknown result type (might be due to invalid IL or missing references)
			//IL_0073: Unknown result type (might be due to invalid IL or missing references)
			Identity val2 = ((((IInstancedEntity)entity).Playfield == null) ? Identity.None : ((IEntity)((IInstancedEntity)entity).Playfield).Identity);
			LogUtil.Debug((DebugInfoDetail)16, string.Format(CultureInfo.InvariantCulture, "PlayerVisibilitySCFU sender={0}/{1} recipient={2}/{3} senderPf={4} recipientPf={5} self={6} inPlayfield={7} rangeRejected=False sent={8}", ((IEntity)entity).Identity, ((INamedEntity)entity).Name, ((IEntity)recipient).Identity, ((INamedEntity)recipient).Name, val2, playfieldIdentity, senderEqualsRecipient, senderInRecipientPlayfield, sent));
		});
		if (diagnosticSnapshot != null)
		{
			diagnosticSnapshot.MarkSnapshotEnqueueCompleted();
		}
		visibilityInterest.CompleteInitialRecipient(recipient);
	}

	internal void AnnounceJoiningCharacterVisibility(ICharacter character, Action<ICharacter, MessageBody> sendVisibilityMessage, Action<ICharacter, Identity> sendLeaveVisibility)
	{
		Require(sendVisibilityMessage, "sendVisibilityMessage");
		Require(sendLeaveVisibility, "sendLeaveVisibility");
		visibilityInterest.ReconcileInitializedRecipients(character, (ICharacter recipient, ICharacter source) => SendCharacterVisibilityEntry(source, recipient, delegate(MessageBody body)
		{
			sendVisibilityMessage(recipient, body);
		}, null, joiningCharacter: true), sendLeaveVisibility);
	}

	internal bool SendCharacterVisibilityEntry(ICharacter source, ICharacter recipient, Action<MessageBody> sendVisibilityMessage)
	{
		return SendCharacterVisibilityEntry(source, recipient, sendVisibilityMessage, null, joiningCharacter: false);
	}

	private bool SendCharacterVisibilityEntry(ICharacter source, ICharacter recipient, Action<MessageBody> sendVisibilityMessage, SubwayVisibilityDiagnosticSnapshot diagnosticSnapshot, bool joiningCharacter)
	{
		Require(sendVisibilityMessage, "sendVisibilityMessage");
		Character temp = (Character)(object)((source is Character) ? source : null);
		if (temp == null)
		{
			return false;
		}
		if (TrySendGuardianVisibilityScfu(temp, recipient, sendVisibilityMessage, diagnosticSnapshot, joiningCharacter))
		{
			return true;
		}
		if (TrySendSurveillanceDroidVisibilityScfu(temp, recipient, sendVisibilityMessage, diagnosticSnapshot, joiningCharacter))
		{
			return true;
		}
		SubwayVisibilityDiagnosticEnemy diagnosticEnemy = ((diagnosticSnapshot == null) ? null : diagnosticSnapshot.BeginEnemy(temp));
		try
		{
			SimpleCharFullUpdateMessage simpleCharFullUpdate = SimpleCharFullUpdate.ConstructMessage(temp);
			CharInPlayMessage charInPlay = null;
			packetSequences.RunVisibilityPacketPairSequence(delegate
			{
				//IL_0024: Unknown result type (might be due to invalid IL or missing references)
				//IL_0034: Unknown result type (might be due to invalid IL or missing references)
				//IL_0039: Unknown result type (might be due to invalid IL or missing references)
				string stage2 = (joiningCharacter ? "joining-character-simple-char-full-update-broadcast" : "existing-character-simple-char-full-update");
				Identity identity3 = ((PooledObject)temp).Identity;
				Identity identity4 = ((IEntity)recipient).Identity;
				PlayfieldLifecycleTrace.Record("same-playfield-visibility", stage2, "SimpleCharFullUpdate", identity3, "recipient=" + ((object)(Identity)(ref identity4)).ToString());
			}, delegate
			{
				using (SubwayVisibilitySnapshotDiagnostics.BeginPacket(diagnosticSnapshot, diagnosticEnemy, SubwayVisibilityDiagnosticPacketKind.SimpleCharFullUpdate, 0))
				{
					sendVisibilityMessage((MessageBody)(object)simpleCharFullUpdate);
				}
				SendWeaponDefinitionsForVisibility((ICharacter)(object)temp, recipient, sendVisibilityMessage, diagnosticSnapshot, diagnosticEnemy);
			}, delegate
			{
				//IL_0002: Unknown result type (might be due to invalid IL or missing references)
				//IL_0007: Unknown result type (might be due to invalid IL or missing references)
				//IL_0013: Unknown result type (might be due to invalid IL or missing references)
				//IL_001e: Unknown result type (might be due to invalid IL or missing references)
				//IL_002b: Expected O, but got Unknown
				charInPlay = new CharInPlayMessage
				{
					Identity = ((PooledObject)temp).Identity,
					Unknown = 0
				};
			}, delegate
			{
				//IL_0024: Unknown result type (might be due to invalid IL or missing references)
				//IL_0034: Unknown result type (might be due to invalid IL or missing references)
				//IL_0039: Unknown result type (might be due to invalid IL or missing references)
				string stage = (joiningCharacter ? "joining-character-char-in-play-broadcast" : "existing-character-char-in-play");
				Identity identity = ((PooledObject)temp).Identity;
				Identity identity2 = ((IEntity)recipient).Identity;
				PlayfieldLifecycleTrace.Record("same-playfield-visibility", stage, "CharInPlay", identity, "recipient=" + ((object)(Identity)(ref identity2)).ToString());
			}, delegate
			{
				using (SubwayVisibilitySnapshotDiagnostics.BeginPacket(diagnosticSnapshot, diagnosticEnemy, SubwayVisibilityDiagnosticPacketKind.CharInPlay, 0))
				{
					sendVisibilityMessage((MessageBody)(object)charInPlay);
				}
			});
			visibilityInterest.MarkVisibleEntry(recipient, source);
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

	private bool TrySendSurveillanceDroidVisibilityScfu(Character droid, ICharacter recipient, Action<MessageBody> sendVisibilityMessage, SubwayVisibilityDiagnosticSnapshot diagnosticSnapshot, bool joiningCharacter)
	{
		if (!SurveillanceDroidScfuWire.IsSurveillanceDroid(droid))
		{
			return false;
		}
		ZoneClient recipientClient = ((((IDynel)recipient).Controller != null) ? (((IDynel)recipient).Controller.Client as ZoneClient) : null);
		if (recipientClient == null)
		{
			return false;
		}
		SubwayVisibilityDiagnosticEnemy diagnosticEnemy = ((diagnosticSnapshot == null) ? null : diagnosticSnapshot.BeginEnemy(droid));
		try
		{
			CharInPlayMessage charInPlay = null;
			packetSequences.RunVisibilityPacketPairSequence(delegate
			{
				//IL_0024: Unknown result type (might be due to invalid IL or missing references)
				//IL_0034: Unknown result type (might be due to invalid IL or missing references)
				//IL_0039: Unknown result type (might be due to invalid IL or missing references)
				string stage2 = (joiningCharacter ? "joining-character-simple-char-full-update-broadcast" : "existing-character-simple-char-full-update");
				Identity identity3 = ((PooledObject)droid).Identity;
				Identity identity4 = ((IEntity)recipient).Identity;
				PlayfieldLifecycleTrace.Record("same-playfield-visibility", stage2, "SimpleCharFullUpdate", identity3, "recipient=" + ((object)(Identity)(ref identity4)).ToString() + " surveillanceDroidWire=true");
			}, delegate
			{
				using (SubwayVisibilitySnapshotDiagnostics.BeginPacket(diagnosticSnapshot, diagnosticEnemy, SubwayVisibilityDiagnosticPacketKind.SimpleCharFullUpdate, 0))
				{
					SurveillanceDroidScfuWire.SendToRecipient(recipientClient, droid);
				}
			}, delegate
			{
				//IL_0002: Unknown result type (might be due to invalid IL or missing references)
				//IL_0007: Unknown result type (might be due to invalid IL or missing references)
				//IL_0013: Unknown result type (might be due to invalid IL or missing references)
				//IL_001e: Unknown result type (might be due to invalid IL or missing references)
				//IL_002b: Expected O, but got Unknown
				charInPlay = new CharInPlayMessage
				{
					Identity = ((PooledObject)droid).Identity,
					Unknown = 0
				};
			}, delegate
			{
				//IL_0024: Unknown result type (might be due to invalid IL or missing references)
				//IL_0034: Unknown result type (might be due to invalid IL or missing references)
				//IL_0039: Unknown result type (might be due to invalid IL or missing references)
				string stage = (joiningCharacter ? "joining-character-char-in-play-broadcast" : "existing-character-char-in-play");
				Identity identity = ((PooledObject)droid).Identity;
				Identity identity2 = ((IEntity)recipient).Identity;
				PlayfieldLifecycleTrace.Record("same-playfield-visibility", stage, "CharInPlay", identity, "recipient=" + ((object)(Identity)(ref identity2)).ToString());
			}, delegate
			{
				using (SubwayVisibilitySnapshotDiagnostics.BeginPacket(diagnosticSnapshot, diagnosticEnemy, SubwayVisibilityDiagnosticPacketKind.CharInPlay, 0))
				{
					sendVisibilityMessage((MessageBody)(object)charInPlay);
				}
			});
			visibilityInterest.MarkVisibleEntry(recipient, (ICharacter)(object)droid);
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
				diagnosticSnapshot.RecordFailure(diagnosticEnemy, "surveillance_droid_visibility_sequence", exception);
			}
			throw;
		}
	}

	private bool TrySendGuardianVisibilityScfu(Character pet, ICharacter recipient, Action<MessageBody> sendVisibilityMessage, SubwayVisibilityDiagnosticSnapshot diagnosticSnapshot, bool joiningCharacter)
	{
		if (!PetBureaucratGuardianAppearance.IsGuardianPet((ICharacter)(object)pet))
		{
			return false;
		}
		int summonNanoId = PetBureaucratGuardianAppearance.ResolveSummonNanoId((ICharacter)(object)pet);
		ICharacter owner = PetCombatRules.ResolvePetOwner((ICharacter)(object)pet);
		ZoneClient recipientClient = ((((IDynel)recipient).Controller != null) ? (((IDynel)recipient).Controller.Client as ZoneClient) : null);
		if (summonNanoId <= 0 || owner == null || recipientClient == null)
		{
			return false;
		}
		SubwayVisibilityDiagnosticEnemy diagnosticEnemy = ((diagnosticSnapshot == null) ? null : diagnosticSnapshot.BeginEnemy(pet));
		try
		{
			CharInPlayMessage charInPlay = null;
			packetSequences.RunVisibilityPacketPairSequence(delegate
			{
				//IL_0024: Unknown result type (might be due to invalid IL or missing references)
				//IL_0034: Unknown result type (might be due to invalid IL or missing references)
				//IL_0039: Unknown result type (might be due to invalid IL or missing references)
				string stage2 = (joiningCharacter ? "joining-character-simple-char-full-update-broadcast" : "existing-character-simple-char-full-update");
				Identity identity3 = ((PooledObject)pet).Identity;
				Identity identity4 = ((IEntity)recipient).Identity;
				PlayfieldLifecycleTrace.Record("same-playfield-visibility", stage2, "SimpleCharFullUpdate", identity3, "recipient=" + ((object)(Identity)(ref identity4)).ToString() + " guardianWire=true");
			}, delegate
			{
				using (SubwayVisibilitySnapshotDiagnostics.BeginPacket(diagnosticSnapshot, diagnosticEnemy, SubwayVisibilityDiagnosticPacketKind.SimpleCharFullUpdate, 0))
				{
					PetBureaucratGuardianScfuWire.SendToRecipient(recipientClient, owner, pet, summonNanoId);
				}
				WeaponItemFullUpdateMessage val = WeaponItemFullUpdate.CreateRightHandWeaponDefinitionMessage((ICharacter)(object)pet);
				if (val != null)
				{
					using (SubwayVisibilitySnapshotDiagnostics.BeginPacket(diagnosticSnapshot, diagnosticEnemy, SubwayVisibilityDiagnosticPacketKind.WeaponDefinition, 1))
					{
						sendVisibilityMessage((MessageBody)(object)val);
					}
					WeaponItemFullUpdate.LogObserverWeaponDefinition((ICharacter)(object)pet, recipient, val);
				}
			}, delegate
			{
				//IL_0002: Unknown result type (might be due to invalid IL or missing references)
				//IL_0007: Unknown result type (might be due to invalid IL or missing references)
				//IL_0013: Unknown result type (might be due to invalid IL or missing references)
				//IL_001e: Unknown result type (might be due to invalid IL or missing references)
				//IL_002b: Expected O, but got Unknown
				charInPlay = new CharInPlayMessage
				{
					Identity = ((PooledObject)pet).Identity,
					Unknown = 0
				};
			}, delegate
			{
				//IL_0024: Unknown result type (might be due to invalid IL or missing references)
				//IL_0034: Unknown result type (might be due to invalid IL or missing references)
				//IL_0039: Unknown result type (might be due to invalid IL or missing references)
				string stage = (joiningCharacter ? "joining-character-char-in-play-broadcast" : "existing-character-char-in-play");
				Identity identity = ((PooledObject)pet).Identity;
				Identity identity2 = ((IEntity)recipient).Identity;
				PlayfieldLifecycleTrace.Record("same-playfield-visibility", stage, "CharInPlay", identity, "recipient=" + ((object)(Identity)(ref identity2)).ToString());
			}, delegate
			{
				using (SubwayVisibilitySnapshotDiagnostics.BeginPacket(diagnosticSnapshot, diagnosticEnemy, SubwayVisibilityDiagnosticPacketKind.CharInPlay, 0))
				{
					sendVisibilityMessage((MessageBody)(object)charInPlay);
				}
			});
			visibilityInterest.MarkVisibleEntry(recipient, (ICharacter)(object)pet);
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

	private void SendWeaponDefinitionsForVisibility(ICharacter owner, ICharacter recipient, Action<MessageBody> sendVisibilityMessage, SubwayVisibilityDiagnosticSnapshot diagnosticSnapshot, SubwayVisibilityDiagnosticEnemy diagnosticEnemy)
	{
		diagnosticSnapshot?.MarkWeaponPhaseStarted(diagnosticEnemy);
		int num = 0;
		WeaponItemFullUpdateMessage[] array = WeaponItemFullUpdate.CreateWeaponDefinitionMessages(owner);
		foreach (WeaponItemFullUpdateMessage val in array)
		{
			num++;
			using (SubwayVisibilitySnapshotDiagnostics.BeginPacket(diagnosticSnapshot, diagnosticEnemy, SubwayVisibilityDiagnosticPacketKind.WeaponDefinition, num))
			{
				sendVisibilityMessage((MessageBody)(object)val);
			}
			WeaponItemFullUpdate.LogObserverWeaponDefinition(owner, recipient, val);
		}
		diagnosticSnapshot?.MarkWeaponPhaseCompleted(diagnosticEnemy);
	}

	private static void Require(Delegate callback, string name)
	{
		if ((object)callback == null)
		{
			throw new ArgumentNullException(name);
		}
	}
}
