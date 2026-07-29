using System.Collections.Generic;
using System.Threading;
using AORebirth.Core.Components;
using AORebirth.Core.Entities;
using AORebirth.Core.Network;
using AORebirth.Core.Playfields;
using AORebirth.Enums;
using AORebirth.Interfaces;
using AORebirth.Stats;
using Cell.Core;
using SmokeLounge.AOtomation.Messaging.GameData;
using SmokeLounge.AOtomation.Messaging.Messages;
using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;
using Utility;
using ZoneEngine.Core.InternalMessages;
using ZoneEngine.Core.PacketHandlers;
using ZoneEngine.Core.Packets;
using ZoneEngine.Core.Perks;
using ZoneEngine.Core.Thrak.Quests;

namespace ZoneEngine.Core.MessageHandlers;

[MessageHandler(/*Could not decode attribute arguments.*/)]
public class CharacterActionMessageHandler : BaseMessageHandler<CharacterActionMessage, CharacterActionMessageHandler>
{
	private const int CompatSitDownActionCode = 286;

	private const int CompatStandUpActionCode = 87;

	private const int LiveDeathRespawnDelayMilliseconds = 2700;

	public CharacterActionMessageHandler()
	{
		base.UpdateCharacterStatsOnReceive = true;
	}

	protected override void Read(CharacterActionMessage message, IZoneClient client)
	{
		//IL_0023: Unknown result type (might be due to invalid IL or missing references)
		//IL_0031: Unknown result type (might be due to invalid IL or missing references)
		//IL_003b: Expected I4, but got Unknown
		//IL_003f: Unknown result type (might be due to invalid IL or missing references)
		//IL_009b: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a0: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a2: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a4: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a6: Unknown result type (might be due to invalid IL or missing references)
		//IL_00aa: Invalid comparison between Unknown and I4
		//IL_0157: Unknown result type (might be due to invalid IL or missing references)
		//IL_015e: Invalid comparison between Unknown and I4
		//IL_00af: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b3: Invalid comparison between Unknown and I4
		//IL_01b3: Unknown result type (might be due to invalid IL or missing references)
		//IL_01ba: Invalid comparison between Unknown and I4
		//IL_0160: Unknown result type (might be due to invalid IL or missing references)
		//IL_0164: Invalid comparison between Unknown and I4
		//IL_011f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0123: Invalid comparison between Unknown and I4
		//IL_00b5: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b9: Invalid comparison between Unknown and I4
		//IL_01db: Unknown result type (might be due to invalid IL or missing references)
		//IL_01e2: Unknown result type (might be due to invalid IL or missing references)
		//IL_01f4: Expected I4, but got Unknown
		//IL_01bc: Unknown result type (might be due to invalid IL or missing references)
		//IL_01c3: Invalid comparison between Unknown and I4
		//IL_017f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0186: Invalid comparison between Unknown and I4
		//IL_0166: Unknown result type (might be due to invalid IL or missing references)
		//IL_016a: Invalid comparison between Unknown and I4
		//IL_013e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0142: Invalid comparison between Unknown and I4
		//IL_0125: Unknown result type (might be due to invalid IL or missing references)
		//IL_0129: Invalid comparison between Unknown and I4
		//IL_0106: Unknown result type (might be due to invalid IL or missing references)
		//IL_010a: Invalid comparison between Unknown and I4
		//IL_00bb: Unknown result type (might be due to invalid IL or missing references)
		//IL_00bf: Unknown result type (might be due to invalid IL or missing references)
		//IL_00e5: Expected I4, but got Unknown
		//IL_0799: Unknown result type (might be due to invalid IL or missing references)
		//IL_079e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0363: Unknown result type (might be due to invalid IL or missing references)
		//IL_0265: Unknown result type (might be due to invalid IL or missing references)
		//IL_0222: Unknown result type (might be due to invalid IL or missing references)
		//IL_0599: Unknown result type (might be due to invalid IL or missing references)
		//IL_0532: Unknown result type (might be due to invalid IL or missing references)
		//IL_055e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0577: Unknown result type (might be due to invalid IL or missing references)
		//IL_01f6: Unknown result type (might be due to invalid IL or missing references)
		//IL_01fd: Invalid comparison between Unknown and I4
		//IL_01ca: Unknown result type (might be due to invalid IL or missing references)
		//IL_01d1: Invalid comparison between Unknown and I4
		//IL_018d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0194: Unknown result type (might be due to invalid IL or missing references)
		//IL_01ae: Expected I4, but got Unknown
		//IL_0171: Unknown result type (might be due to invalid IL or missing references)
		//IL_0175: Invalid comparison between Unknown and I4
		//IL_0149: Unknown result type (might be due to invalid IL or missing references)
		//IL_014d: Invalid comparison between Unknown and I4
		//IL_0130: Unknown result type (might be due to invalid IL or missing references)
		//IL_0134: Invalid comparison between Unknown and I4
		//IL_0111: Unknown result type (might be due to invalid IL or missing references)
		//IL_0115: Invalid comparison between Unknown and I4
		//IL_00e7: Unknown result type (might be due to invalid IL or missing references)
		//IL_00eb: Unknown result type (might be due to invalid IL or missing references)
		//IL_0101: Expected I4, but got Unknown
		//IL_02b4: Unknown result type (might be due to invalid IL or missing references)
		//IL_066e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0673: Unknown result type (might be due to invalid IL or missing references)
		//IL_0677: Unknown result type (might be due to invalid IL or missing references)
		//IL_0699: Unknown result type (might be due to invalid IL or missing references)
		//IL_069b: Unknown result type (might be due to invalid IL or missing references)
		//IL_06ab: Unknown result type (might be due to invalid IL or missing references)
		//IL_06bc: Expected I4, but got Unknown
		//IL_06b7: Unknown result type (might be due to invalid IL or missing references)
		//IL_06c1: Expected O, but got Unknown
		//IL_06d0: Unknown result type (might be due to invalid IL or missing references)
		//IL_06e1: Expected I4, but got Unknown
		//IL_06dc: Unknown result type (might be due to invalid IL or missing references)
		//IL_06e6: Expected O, but got Unknown
		//IL_0204: Unknown result type (might be due to invalid IL or missing references)
		//IL_020b: Invalid comparison between Unknown and I4
		//IL_03c8: Unknown result type (might be due to invalid IL or missing references)
		//IL_03cd: Unknown result type (might be due to invalid IL or missing references)
		//IL_04a9: Unknown result type (might be due to invalid IL or missing references)
		//IL_04af: Invalid comparison between Unknown and I4
		//IL_04bc: Unknown result type (might be due to invalid IL or missing references)
		//IL_04c3: Invalid comparison between Unknown and I4
		//IL_04d0: Unknown result type (might be due to invalid IL or missing references)
		//IL_04d7: Invalid comparison between Unknown and I4
		LogUtil.Debug((DebugInfoDetail)16, "Reading CharacterActionMessage");
		((IClient)client).Server.Info((IClient)(object)client, "CharacterAction action={0}({1}) target={2} p1={3} p2={4} u1={5} u2={6}", new object[7]
		{
			message.Action,
			(int)message.Action,
			message.Target,
			message.Parameter1,
			message.Parameter2,
			message.Unknown1,
			message.Unknown2
		});
		if (TryHandleCompatPostureAction(message, client))
		{
			return;
		}
		CharacterActionType action = message.Action;
		CharacterActionType val = action;
		Identity val2;
		if ((int)val <= 112)
		{
			if ((int)val <= 81)
			{
				if ((int)val <= 35)
				{
					switch (val - 19)
					{
					default:
						switch (val - 32)
						{
						case 0:
							client.Controller.TeamLeave();
							return;
						case 2:
							InventoryContainerRuntimeService.Default.SplitInventoryItemStackAction(client.Controller.Character, message);
							return;
						case 3:
							InventoryContainerRuntimeService.Default.MergeInventoryItemStackAction(client.Controller.Character, message);
							Acknowledge(client.Controller.Character, message);
							return;
						}
						break;
					case 0:
						client.Controller.CastNano(message.Parameter2, message.Target);
						return;
					case 3:
						client.Controller.TeamKickMember(message.Target);
						return;
					case 6:
						client.Controller.TransferTeamLeadership(message.Target);
						return;
					case 7:
						client.Controller.TeamJoinRequest(message.Target);
						return;
					case 2:
						client.Controller.TeamJoinReply(message.Parameter1 != 0, message.Target);
						return;
					case 1:
					case 4:
					case 5:
						break;
					}
				}
				else
				{
					if ((int)val == 65)
					{
						ActiveNanoRuntimeService.Default.TryHandleRemoveFriendlyNano(client, message);
						return;
					}
					if ((int)val == 81)
					{
						Identity target = message.Target;
						val2 = default(Identity);
						((Identity)(ref val2)).Type = (IdentityType)message.Parameter1;
						((Identity)(ref val2)).Instance = message.Parameter2;
						Identity val3 = val2;
						client.Controller.Character.TradeSkillSource = new TradeSkillInfo(0, (int)((Identity)(ref target)).Type, ((Identity)(ref target)).Instance);
						client.Controller.Character.TradeSkillTarget = new TradeSkillInfo(1, (int)((Identity)(ref val3)).Type, ((Identity)(ref val3)).Instance);
						TradeSkillReceiver.TradeSkillBuildPressed(client, 300);
						return;
					}
				}
			}
			else if ((int)val <= 102)
			{
				if ((int)val == 87)
				{
					ApplyStand(client);
					if (client.Controller.Character.InLogoutTimerPeriod())
					{
						((AbstractMessageHandler<CharacterActionMessage>)(object)this).Send(client.Controller.Character, StopLogout(client.Controller.Character), true);
						client.Controller.Character.StopLogoutTimer();
					}
					return;
				}
				if ((int)val == 102)
				{
					BaseMessageHandler<FeedbackMessage, FeedbackMessageHandler>.Default.Send(client.Controller.Character, 110, 136744723);
					return;
				}
			}
			else
			{
				if ((int)val == 105)
				{
					IInstancedEntity val4 = ((IInstancedEntity)client.Controller.Character).Playfield.FindByIdentity(message.Target);
					Character val5 = (Character)(object)((val4 is Character) ? val4 : null);
					if (val5 != null)
					{
						BaseMessageHandler<InfoPacketMessage, CharacterInfoPacketMessageHandler>.Default.Send(client.Controller.Character, (ICharacter)(object)val5);
					}
					return;
				}
				if ((int)val == 112)
				{
					if (!InventoryContainerRuntimeService.Default.DeleteInventoryItemAction(client.Controller.Character, message))
					{
						ThrakGardenKeyQuestRuntime.TryForceReturnGardenKey(client.Controller.Character);
					}
					else
					{
						AcknowledgeDelete(client.Controller.Character, message);
					}
					return;
				}
			}
		}
		else if ((int)val <= 167)
		{
			if ((int)val <= 122)
			{
				if ((int)val == 120)
				{
					ApplyLogoutSit(client);
					SendOwnerLogoutSitAction(client);
					SendStartLogout(client.Controller.Character);
					SendLogoutMovementModeStat(client);
					client.Controller.Character.StartLogoutTimer(30000);
					return;
				}
				if ((int)val == 122)
				{
					ApplyStand(client);
					return;
				}
			}
			else
			{
				if ((int)val == 152)
				{
					ServerBase server = ((IClient)client).Server;
					object[] obj = new object[3]
					{
						((IEntity)client.Controller.Character).Identity,
						(((IDynel)client.Controller.Character).Controller == null) ? "null" : ((object)((IDynel)client.Controller.Character).Controller).GetType().FullName,
						null
					};
					object obj2;
					if (((IInstancedEntity)client.Controller.Character).Playfield != null)
					{
						val2 = ((IEntity)((IInstancedEntity)client.Controller.Character).Playfield).Identity;
						obj2 = ((object)(Identity)(ref val2)).ToString();
					}
					else
					{
						obj2 = "null";
					}
					obj[2] = obj2;
					server.Info((IClient)(object)client, "Player death action received. character={0} controller={1} playfield={2}", obj);
					if (((IInstancedEntity)client.Controller.Character).Playfield is Playfield playfield)
					{
						Thread.Sleep(2700);
						playfield.RespawnPlayer(client.Controller.Character);
					}
					else
					{
						LogUtil.Debug((DebugInfoDetail)4, "Player death respawn deferred because current playfield is not a ZoneEngine playfield.");
					}
					return;
				}
				switch (val - 163)
				{
				case 4:
					if (message.Parameter1 == 0)
					{
						ApplySit(client);
					}
					else
					{
						ApplyStand(client);
					}
					return;
				case 0:
					((AbstractMessageHandler<CharacterActionMessage>)(object)this).Send(client.Controller.Character, Sneak(client.Controller.Character), true);
					return;
				case 3:
					((IStats)client.Controller.Character).Stats[(StatIds)673].Value = message.Parameter2;
					BaseMessageHandler<ChatTextMessage, ChatTextMessageHandler>.Default.Send(client.Controller.Character, "Setting Visual Flag to " + message.Parameter2, 0, 0);
					BaseMessageHandler<AppearanceUpdateMessage, AppearanceUpdateMessageHandler>.Default.Send(client.Controller.Character);
					return;
				}
			}
		}
		else if ((int)val <= 187)
		{
			if ((int)val == 179)
			{
				PerkRuntimeService.Default.TryHandleUsePerk(client, message);
				return;
			}
			if ((int)val == 187)
			{
				PerkRuntimeService.Default.TryHandleTrainPerk(client, message);
				return;
			}
		}
		else
		{
			switch (val - 220)
			{
			case 0:
				TradeSkillReceiver.TradeSkillSourceChanged(client, message.Parameter1, message.Parameter2);
				return;
			case 1:
				TradeSkillReceiver.TradeSkillTargetChanged(client, message.Parameter1, message.Parameter2);
				return;
			case 2:
				val2 = message.Target;
				TradeSkillReceiver.TradeSkillBuildPressed(client, ((Identity)(ref val2)).Instance);
				return;
			}
			if ((int)val == 261)
			{
				IInstancedEntity val6 = ((IInstancedEntity)client.Controller.Character).Playfield.FindByIdentity(message.Target);
				Character val7 = (Character)(object)((val6 is Character) ? val6 : null);
				if (val7 != null)
				{
					BaseMessageHandler<InspectMessage, InspectMessageHandler>.Default.Send(client.Controller.Character, (ICharacter)(object)val7);
				}
				return;
			}
			if ((int)val == 263)
			{
				if ((int)client.Controller.Character.MoveMode == 8 || (int)client.Controller.Character.MoveMode == 11 || (int)client.Controller.Character.MoveMode == 12)
				{
					ApplyStand(client);
				}
				else
				{
					ApplySit(client);
				}
				return;
			}
		}
		((IInstancedEntity)client.Controller.Character).Playfield.Announce((MessageBody)(object)message);
	}

	public void FinishNanoCasting(ICharacter character, CharacterActionType actionType, Identity target, int unknown1, int unknown2)
	{
		//IL_0005: Unknown result type (might be due to invalid IL or missing references)
		((AbstractMessageHandler<CharacterActionMessage>)(object)this).Send(character, ConstructFinishNanoCasting(character, target, unknown1, unknown2), true);
	}

	public void SendPetNanoExecutedWithinOwnerNcu(ICharacter owner, ICharacter pet, int healRoll)
	{
		((AbstractMessageHandler<CharacterActionMessage>)(object)this).Send(owner, (MessageDataFiller<CharacterActionMessage>)delegate(CharacterActionMessage x)
		{
			//IL_0008: Unknown result type (might be due to invalid IL or missing references)
			//IL_0036: Unknown result type (might be due to invalid IL or missing references)
			((N3Message)x).Identity = ((IEntity)owner).Identity;
			((N3Message)x).Unknown = 0;
			x.Action = (CharacterActionType)129;
			x.Unknown1 = 0;
			x.Target = ((IEntity)pet).Identity;
			x.Parameter1 = 0;
			x.Parameter2 = healRoll;
			x.Unknown2 = 0;
		}, true);
	}

	private MessageDataFiller<CharacterActionMessage> ConstructFinishNanoCasting(ICharacter character, Identity target, int unknown1, int unknown2)
	{
		return delegate(CharacterActionMessage x)
		{
			//IL_0008: Unknown result type (might be due to invalid IL or missing references)
			//IL_002d: Unknown result type (might be due to invalid IL or missing references)
			((N3Message)x).Identity = ((IEntity)character).Identity;
			((N3Message)x).Unknown = 0;
			x.Action = (CharacterActionType)107;
			x.Unknown1 = 0;
			x.Target = Identity.None;
			x.Parameter1 = unknown1;
			x.Parameter2 = unknown2;
			x.Unknown2 = 0;
		};
	}

	private MessageDataFiller<CharacterActionMessage> ConstructSetNanoDuration(ICharacter character, Identity target, int unknown1, int duration = 150000)
	{
		//IL_0007: Unknown result type (might be due to invalid IL or missing references)
		//IL_0008: Unknown result type (might be due to invalid IL or missing references)
		return delegate(CharacterActionMessage x)
		{
			//IL_0003: Unknown result type (might be due to invalid IL or missing references)
			//IL_002a: Unknown result type (might be due to invalid IL or missing references)
			//IL_004b: Unknown result type (might be due to invalid IL or missing references)
			//IL_0059: Unknown result type (might be due to invalid IL or missing references)
			//IL_005e: Unknown result type (might be due to invalid IL or missing references)
			((N3Message)x).Identity = target;
			((N3Message)x).Unknown = 0;
			x.Action = (CharacterActionType)98;
			x.Unknown1 = 0;
			Identity target2 = default(Identity);
			((Identity)(ref target2)).Type = (IdentityType)53019;
			((Identity)(ref target2)).Instance = unknown1;
			x.Target = target2;
			target2 = ((IEntity)character).Identity;
			x.Parameter1 = ((Identity)(ref target2)).Instance;
			x.Parameter2 = duration;
			x.Unknown2 = 0;
		};
	}

	public void SetNanoDuration(ICharacter character, Identity target, int unknown1, int duration = 150000)
	{
		//IL_003f: Unknown result type (might be due to invalid IL or missing references)
		//IL_001f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0024: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ec: Unknown result type (might be due to invalid IL or missing references)
		//IL_008d: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a2: Unknown result type (might be due to invalid IL or missing references)
		ICharacter character2 = character;
		if (character != null && ((IInstancedEntity)character).Playfield != null && ((Identity)(ref target)).Instance != 0)
		{
			int instance = ((Identity)(ref target)).Instance;
			Identity identity = ((IEntity)character).Identity;
			if (instance != ((Identity)(ref identity)).Instance)
			{
				ICharacter val = ((IInstancedEntity)character).Playfield.FindByIdentity<ICharacter>(target);
				if (val != null)
				{
					character2 = val;
				}
			}
		}
		int num = ActiveNanoRuntimeService.Default.ResolveNanoStrain(character2, unknown1);
		if (duration > 0 && !ActiveNanoRuntimeService.Default.HasActiveNanoInStrain(character2, unknown1, num))
		{
			if (!ActiveNanoRuntimeService.Default.ApplyActiveNano(character2, unknown1, duration, target, num))
			{
				((AbstractMessageHandler<CharacterActionMessage>)(object)this).Send(character, ConstructSetNanoDuration(character, target, unknown1, duration), false);
				return;
			}
			if (((IDynel)character).Controller != null && ((IDynel)character).Controller.Client != null)
			{
				SimpleCharFullUpdate.SendToOne(character, ((IDynel)character).Controller.Client);
			}
		}
		((AbstractMessageHandler<CharacterActionMessage>)(object)this).Send(character, ConstructSetNanoDuration(character, target, unknown1, duration), false);
	}

	public void NotifyActiveNanoDuration(ICharacter character, Identity target, int nanoId, int duration)
	{
		//IL_0005: Unknown result type (might be due to invalid IL or missing references)
		((AbstractMessageHandler<CharacterActionMessage>)(object)this).Send(character, ConstructSetNanoDuration(character, target, nanoId, duration), false);
	}

	public void NotifyActiveNanoDurationToPlayfield(ICharacter character, Identity target, int nanoId, int duration)
	{
		//IL_0005: Unknown result type (might be due to invalid IL or missing references)
		((AbstractMessageHandler<CharacterActionMessage>)(object)this).Send(character, ConstructSetNanoDuration(character, target, nanoId, duration), true);
	}

	public void SendActiveNanoDuration(ICharacter character, Identity target, int nanoId, int duration)
	{
		//IL_0003: Unknown result type (might be due to invalid IL or missing references)
		NotifyActiveNanoDuration(character, target, nanoId, duration);
	}

	public void AcknowledgeRemoveFriendlyNano(ICharacter character, CharacterActionMessage message, int nanoId)
	{
		if (nanoId > 0)
		{
			BaseMessageHandler<BuffMessage, BuffMessageHandler>.Default.SendRemoveNanoBuff(character, nanoId);
		}
	}

	public void CompleteFriendlyNanoRemoval(ICharacter character, CharacterActionMessage message, List<ActiveNanoRuntimeService.ActiveNanoRemovalTarget> removalTargets)
	{
		if (removalTargets == null)
		{
			return;
		}
		IZoneClient val = ((((IDynel)character).Controller != null) ? ((IDynel)character).Controller.Client : null);
		foreach (ActiveNanoRuntimeService.ActiveNanoRemovalTarget removalTarget in removalTargets)
		{
			BaseMessageHandler<BuffMessage, BuffMessageHandler>.Default.SendRemoveNanoBuff(character, removalTarget.NanoId);
			if (val != null)
			{
				((IClient)val).Server.Info((IClient)(object)val, "RemoveFriendlyNano outbound Buff remove nanoId={0} instance={1}", new object[2] { removalTarget.NanoId, removalTarget.NanoInstance });
			}
		}
	}

	public void CompleteFriendlyNanoRemoval(ICharacter character, int nanoId, Identity identity, int nanoInstance)
	{
		BaseMessageHandler<BuffMessage, BuffMessageHandler>.Default.SendRemoveNanoBuff(character, nanoId);
	}

	private MessageDataFiller<CharacterActionMessage> SkillUnavailableAction(ICharacter character, int statId, int durationSeconds)
	{
		return delegate(CharacterActionMessage x)
		{
			//IL_0008: Unknown result type (might be due to invalid IL or missing references)
			//IL_0030: Unknown result type (might be due to invalid IL or missing references)
			((N3Message)x).Identity = ((IEntity)character).Identity;
			((N3Message)x).Unknown = 0;
			x.Action = (CharacterActionType)132;
			x.Unknown1 = 0;
			x.Target = Identity.None;
			x.Parameter1 = statId;
			x.Parameter2 = durationSeconds;
			x.Unknown2 = 0;
		};
	}

	public void SendSkillUnavailable(ICharacter character, int statId, int durationSeconds)
	{
		((AbstractMessageHandler<CharacterActionMessage>)(object)this).Send(character, SkillUnavailableAction(character, statId, durationSeconds), false);
	}

	private MessageDataFiller<CharacterActionMessage> SkillAvailableAction(ICharacter character, int statId)
	{
		return delegate(CharacterActionMessage x)
		{
			//IL_0008: Unknown result type (might be due to invalid IL or missing references)
			//IL_0030: Unknown result type (might be due to invalid IL or missing references)
			((N3Message)x).Identity = ((IEntity)character).Identity;
			((N3Message)x).Unknown = 0;
			x.Action = (CharacterActionType)164;
			x.Unknown1 = 0;
			x.Target = Identity.None;
			x.Parameter1 = 0;
			x.Parameter2 = statId;
			x.Unknown2 = 0;
		};
	}

	public void SendSkillAvailable(ICharacter character, int statId)
	{
		((AbstractMessageHandler<CharacterActionMessage>)(object)this).Send(character, SkillAvailableAction(character, statId), false);
	}

	private MessageDataFiller<CharacterActionMessage> DeleteItemAction(ICharacter character, int container, int placement)
	{
		return delegate(CharacterActionMessage x)
		{
			//IL_0008: Unknown result type (might be due to invalid IL or missing references)
			//IL_001f: Unknown result type (might be due to invalid IL or missing references)
			//IL_0041: Unknown result type (might be due to invalid IL or missing references)
			((N3Message)x).Identity = ((IEntity)character).Identity;
			x.Action = (CharacterActionType)112;
			Identity target = default(Identity);
			((Identity)(ref target)).Type = (IdentityType)container;
			((Identity)(ref target)).Instance = placement;
			x.Target = target;
		};
	}

	public void SendDeleteItem(ICharacter character, int container, int placement)
	{
		((AbstractMessageHandler<CharacterActionMessage>)(object)this).Send(character, DeleteItemAction(character, container, placement), false);
	}

	private MessageDataFiller<CharacterActionMessage> Sneak(ICharacter character)
	{
		return delegate(CharacterActionMessage x)
		{
			//IL_0008: Unknown result type (might be due to invalid IL or missing references)
			//IL_0030: Unknown result type (might be due to invalid IL or missing references)
			((N3Message)x).Identity = ((IEntity)character).Identity;
			((N3Message)x).Unknown = 0;
			x.Action = (CharacterActionType)162;
			x.Unknown1 = 0;
			x.Target = Identity.None;
			x.Parameter1 = 0;
			x.Parameter2 = 0;
			x.Unknown2 = 0;
		};
	}

	private void Acknowledge(ICharacter character, CharacterActionMessage message)
	{
		((AbstractMessageHandler<CharacterActionMessage>)(object)this).Send(character, Reply(message), false);
	}

	private MessageDataFiller<CharacterActionMessage> Reply(CharacterActionMessage message)
	{
		return delegate(CharacterActionMessage x)
		{
			//IL_0008: Unknown result type (might be due to invalid IL or missing references)
			//IL_001a: Unknown result type (might be due to invalid IL or missing references)
			//IL_0050: Unknown result type (might be due to invalid IL or missing references)
			x.Action = message.Action;
			((N3Message)x).Identity = ((N3Message)message).Identity;
			x.Parameter1 = message.Parameter1;
			x.Parameter2 = message.Parameter2;
			x.Target = message.Target;
			x.Unknown1 = message.Unknown1;
			x.Unknown2 = message.Unknown2;
			((N3Message)x).Unknown = ((N3Message)message).Unknown;
		};
	}

	private void AcknowledgeDelete(ICharacter character, CharacterActionMessage message)
	{
		((AbstractMessageHandler<CharacterActionMessage>)(object)this).Send(character, ReplyWithoutParameters(message), false);
	}

	private MessageDataFiller<CharacterActionMessage> ReplyWithoutParameters(CharacterActionMessage message)
	{
		return delegate(CharacterActionMessage x)
		{
			//IL_0008: Unknown result type (might be due to invalid IL or missing references)
			//IL_001a: Unknown result type (might be due to invalid IL or missing references)
			//IL_003c: Unknown result type (might be due to invalid IL or missing references)
			x.Action = message.Action;
			((N3Message)x).Identity = ((N3Message)message).Identity;
			x.Parameter1 = 0;
			x.Parameter2 = 0;
			x.Target = message.Target;
			x.Unknown1 = message.Unknown1;
			x.Unknown2 = message.Unknown2;
			((N3Message)x).Unknown = ((N3Message)message).Unknown;
		};
	}

	private bool TryHandleCompatPostureAction(CharacterActionMessage message, IZoneClient client)
	{
		//IL_0002: Unknown result type (might be due to invalid IL or missing references)
		//IL_0008: Expected I4, but got Unknown
		int num = (int)message.Action;
		bool flag = num == 286 || message.Parameter1 == 286 || message.Parameter2 == 286;
		bool flag2 = num == 87 || message.Parameter1 == 87 || message.Parameter2 == 87;
		if (flag)
		{
			ApplySit(client);
			return true;
		}
		if (flag2)
		{
			ApplyStand(client);
			return true;
		}
		return false;
	}

	private void ApplySit(IZoneClient client)
	{
		ICharacter character = client.Controller.Character;
		character.EnterLogoutSitPosture();
		client.Controller.State = (CharacterState)0;
		SendPostureMove(character, 30);
		SimpleCharFullUpdate.SendToPlayfield(client.Controller.Client);
	}

	private void ApplyLogoutSit(IZoneClient client)
	{
		//IL_002c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0036: Expected O, but got Unknown
		//IL_004f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0062: Unknown result type (might be due to invalid IL or missing references)
		ICharacter character = client.Controller.Character;
		character.EnterLogoutSitPosture();
		client.Controller.State = (CharacterState)0;
		CharDCMoveMessage val = CreatePostureMove(character, 30);
		SimpleCharFullUpdateMessage val2 = SimpleCharFullUpdate.ConstructMessage((Character)character);
		client.SendCompressed((MessageBody)(object)val);
		client.SendCompressed((MessageBody)(object)val2);
		((IInstancedEntity)character).Playfield.AnnounceOthers((MessageBody)(object)val, ((IEntity)character).Identity);
		((IInstancedEntity)character).Playfield.AnnounceOthers((MessageBody)(object)val2, ((IEntity)character).Identity);
	}

	private void SendOwnerLogoutSitAction(IZoneClient client)
	{
		//IL_000e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0013: Unknown result type (might be due to invalid IL or missing references)
		//IL_0015: Unknown result type (might be due to invalid IL or missing references)
		//IL_0020: Unknown result type (might be due to invalid IL or missing references)
		//IL_0028: Unknown result type (might be due to invalid IL or missing references)
		//IL_0034: Unknown result type (might be due to invalid IL or missing references)
		//IL_003c: Unknown result type (might be due to invalid IL or missing references)
		//IL_003d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0048: Unknown result type (might be due to invalid IL or missing references)
		//IL_0050: Unknown result type (might be due to invalid IL or missing references)
		//IL_0058: Unknown result type (might be due to invalid IL or missing references)
		//IL_0065: Expected O, but got Unknown
		ICharacter character = client.Controller.Character;
		client.SendCompressed((MessageBody)new CharacterActionMessage
		{
			Identity = ((IEntity)character).Identity,
			Unknown = 0,
			Action = (CharacterActionType)167,
			Unknown1 = 0,
			Target = Identity.None,
			Parameter1 = 0,
			Parameter2 = 0,
			Unknown2 = 0
		});
	}

	private void ApplyStand(IZoneClient client)
	{
		//IL_001c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0021: Unknown result type (might be due to invalid IL or missing references)
		//IL_0023: Unknown result type (might be due to invalid IL or missing references)
		//IL_002e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0036: Unknown result type (might be due to invalid IL or missing references)
		//IL_003f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0047: Unknown result type (might be due to invalid IL or missing references)
		//IL_0048: Unknown result type (might be due to invalid IL or missing references)
		//IL_0053: Unknown result type (might be due to invalid IL or missing references)
		//IL_005b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0063: Unknown result type (might be due to invalid IL or missing references)
		//IL_0070: Expected O, but got Unknown
		ICharacter character = client.Controller.Character;
		character.UpdateMoveType((byte)37);
		((IInstancedEntity)character).Playfield.Announce((MessageBody)new CharacterActionMessage
		{
			Identity = ((IEntity)character).Identity,
			Unknown = 0,
			Action = (CharacterActionType)87,
			Unknown1 = 0,
			Target = Identity.None,
			Parameter1 = 0,
			Parameter2 = 0,
			Unknown2 = 0
		});
		SendPostureMove(character, 37);
		if (character.InLogoutTimerPeriod())
		{
			SendStopLogout(character);
			((AbstractMessageHandler<CharacterActionMessage>)(object)this).Send(character, StopLogout(character), true);
			character.StopLogoutTimer();
		}
	}

	private void SendStartLogout(ICharacter character)
	{
		//IL_000c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0011: Unknown result type (might be due to invalid IL or missing references)
		//IL_0013: Unknown result type (might be due to invalid IL or missing references)
		//IL_0023: Expected O, but got Unknown
		((IDynel)character).Controller.Client.SendCompressed((MessageBody)new StartLogoutMessage
		{
			Identity = ((IEntity)character).Identity
		});
	}

	private void SendStopLogout(ICharacter character)
	{
		//IL_000c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0011: Unknown result type (might be due to invalid IL or missing references)
		//IL_0013: Unknown result type (might be due to invalid IL or missing references)
		//IL_0023: Expected O, but got Unknown
		((IDynel)character).Controller.Client.SendCompressed((MessageBody)new StopLogoutMessage
		{
			Identity = ((IEntity)character).Identity
		});
	}

	private void SendLogoutMovementModeStat(IZoneClient client)
	{
		//IL_000e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0014: Expected O, but got Unknown
		//IL_0016: Unknown result type (might be due to invalid IL or missing references)
		ICharacter character = client.Controller.Character;
		StatMessage val = new StatMessage();
		((N3Message)val).Identity = ((IEntity)character).Identity;
		((N3Message)val).Unknown = 1;
		val.Stats = new GameTuple<CharacterStat, uint>[1]
		{
			new GameTuple<CharacterStat, uint>
			{
				Value1 = (CharacterStat)173,
				Value2 = (uint)((IStats)character).Stats[(StatIds)173].Value
			}
		};
		client.SendCompressed((MessageBody)(object)val);
	}

	private void SendPostureMove(ICharacter character, byte moveType)
	{
		CharDCMoveMessage body = CreatePostureMove(character, moveType);
		((IInstancedEntity)character).Playfield.Publish((object)new IMSendAOtomationMessageToPlayfield
		{
			Body = (MessageBody)(object)body
		});
	}

	private CharDCMoveMessage CreatePostureMove(ICharacter character, byte moveType)
	{
		//IL_0001: Unknown result type (might be due to invalid IL or missing references)
		//IL_0006: Unknown result type (might be due to invalid IL or missing references)
		//IL_0008: Unknown result type (might be due to invalid IL or missing references)
		//IL_0013: Unknown result type (might be due to invalid IL or missing references)
		//IL_001b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0023: Unknown result type (might be due to invalid IL or missing references)
		//IL_0024: Unknown result type (might be due to invalid IL or missing references)
		//IL_0029: Unknown result type (might be due to invalid IL or missing references)
		//IL_003b: Unknown result type (might be due to invalid IL or missing references)
		//IL_004d: Unknown result type (might be due to invalid IL or missing references)
		//IL_005f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0076: Expected O, but got Unknown
		//IL_0077: Unknown result type (might be due to invalid IL or missing references)
		//IL_0078: Unknown result type (might be due to invalid IL or missing references)
		//IL_007d: Unknown result type (might be due to invalid IL or missing references)
		//IL_008f: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a1: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b8: Expected O, but got Unknown
		//IL_00b9: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c1: Unknown result type (might be due to invalid IL or missing references)
		//IL_00cd: Unknown result type (might be due to invalid IL or missing references)
		//IL_00da: Expected O, but got Unknown
		return new CharDCMoveMessage
		{
			Identity = ((IEntity)character).Identity,
			Unknown = 0,
			MoveType = moveType,
			Heading = new Quaternion
			{
				X = ((IDynel)character).Heading.xf,
				Y = ((IDynel)character).Heading.yf,
				Z = ((IDynel)character).Heading.zf,
				W = ((IDynel)character).Heading.wf
			},
			Coordinates = new Vector3
			{
				X = ((IDynel)character).RawCoordinates.X,
				Y = ((IDynel)character).RawCoordinates.Y,
				Z = ((IDynel)character).RawCoordinates.Z
			},
			Unknown1 = 0,
			Unknown2 = 0f,
			Unknown3 = 0f
		};
	}

	private MessageDataFiller<CharacterActionMessage> StopLogout(ICharacter character)
	{
		return delegate(CharacterActionMessage x)
		{
			//IL_0011: Unknown result type (might be due to invalid IL or missing references)
			x.Action = (CharacterActionType)122;
			((N3Message)x).Identity = ((IEntity)character).Identity;
		};
	}
}
