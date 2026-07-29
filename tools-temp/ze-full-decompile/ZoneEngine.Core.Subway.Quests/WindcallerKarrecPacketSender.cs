using System;
using AORebirth.Core.Components;
using AORebirth.Core.Entities;
using AORebirth.Interfaces;
using SmokeLounge.AOtomation.Messaging.GameData;
using SmokeLounge.AOtomation.Messaging.Messages;
using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;
using Utility;
using ZoneEngine.Core.MessageHandlers;

namespace ZoneEngine.Core.Subway.Quests;

internal static class WindcallerKarrecPacketSender
{
	internal const int MissionInstance = 1431802753;

	private const int MissionIdentityType = 56003;

	private const int KarrecInstance = 2036555963;

	private const string ShortInfo = "The Windcaller's requests";

	private const string LongInfo = "The Windcaller's requests<BR><BR>Windcaller Karrec told you to get him a hamburger from an annoying individual. He also told you to get a woman named Maddy Cardile to donate money to his temple.<BR><BR><font color=\"#FF0000\">Mission Objective:<BR>Give Windcaller Karrec a Bronto Burger and Maddy's Credit Card.</font>";

	internal static bool TrySendQuestFullUpdate(ICharacter character, Identity karrecIdentity)
	{
		//IL_0021: Unknown result type (might be due to invalid IL or missing references)
		//IL_0026: Unknown result type (might be due to invalid IL or missing references)
		//IL_0027: Unknown result type (might be due to invalid IL or missing references)
		if (!CanSend(character))
		{
			return false;
		}
		try
		{
			((IDynel)character).Controller.Client.SendCompressed((MessageBody)(object)CreateQuestFullUpdate(((IEntity)character).Identity, ResolveKarrecIdentity(karrecIdentity)));
			return true;
		}
		catch (Exception ex)
		{
			LogUtil.Debug((DebugInfoDetail)512, "SUBWAY_KARREC QuestFullUpdate send failed: " + ex.Message);
			return false;
		}
	}

	internal static bool TrySendCompletionAndDelete(ICharacter character)
	{
		//IL_0021: Unknown result type (might be due to invalid IL or missing references)
		//IL_003d: Unknown result type (might be due to invalid IL or missing references)
		if (!CanSend(character))
		{
			return false;
		}
		try
		{
			((IDynel)character).Controller.Client.SendCompressed((MessageBody)(object)CreateAction59(((IEntity)character).Identity));
			((IDynel)character).Controller.Client.SendCompressed((MessageBody)(object)CreateQuestDelete(((IEntity)character).Identity));
			return true;
		}
		catch (Exception ex)
		{
			LogUtil.Debug((DebugInfoDetail)512, "SUBWAY_KARREC completion packet send failed: " + ex.Message);
			return false;
		}
	}

	internal static bool TrySendPersonalResearchFeedback(ICharacter character)
	{
		//IL_0020: Unknown result type (might be due to invalid IL or missing references)
		//IL_0025: Unknown result type (might be due to invalid IL or missing references)
		//IL_0027: Unknown result type (might be due to invalid IL or missing references)
		//IL_0032: Unknown result type (might be due to invalid IL or missing references)
		//IL_003a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0046: Unknown result type (might be due to invalid IL or missing references)
		//IL_0052: Unknown result type (might be due to invalid IL or missing references)
		//IL_005f: Expected O, but got Unknown
		if (!CanSend(character))
		{
			return false;
		}
		try
		{
			((IDynel)character).Controller.Client.SendCompressed((MessageBody)new FormatFeedbackMessage
			{
				Identity = ((IEntity)character).Identity,
				Unknown = 1,
				Unknown1 = 1107296284,
				FormattedMessage = "~&!!!\":!)90Fi!!![g~",
				Unknown2 = 0
			});
			return true;
		}
		catch (Exception ex)
		{
			LogUtil.Debug((DebugInfoDetail)512, "SUBWAY_KARREC_QUEST personal research feedback failed: " + ex.Message);
			return false;
		}
	}

	internal static bool TrySendSideTokenProjection(ICharacter character, long sideTokenValue)
	{
		//IL_0023: Unknown result type (might be due to invalid IL or missing references)
		//IL_0028: Unknown result type (might be due to invalid IL or missing references)
		//IL_002a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0035: Unknown result type (might be due to invalid IL or missing references)
		//IL_003d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0045: Unknown result type (might be due to invalid IL or missing references)
		//IL_0062: Unknown result type (might be due to invalid IL or missing references)
		//IL_006f: Expected O, but got Unknown
		if (!CanSend(character))
		{
			return false;
		}
		try
		{
			((IDynel)character).Controller.Client.SendCompressed((MessageBody)new FormatFeedbackMessage
			{
				Identity = ((IEntity)character).Identity,
				Unknown = 1,
				Unknown1 = 0,
				FormattedMessage = "Side tokens collected: " + sideTokenValue + ".",
				Unknown2 = 0
			});
			BaseMessageHandler<FeedbackMessage, FeedbackMessageHandler>.Default.Send(character, 110, 108871108);
			return true;
		}
		catch (Exception ex)
		{
			LogUtil.Debug((DebugInfoDetail)512, "SUBWAY_KARREC_QUEST side token projection failed: " + ex.Message);
			return false;
		}
	}

	internal static QuestFullUpdateMessage CreateQuestFullUpdate(Identity characterIdentity, Identity karrecIdentity)
	{
		//IL_000b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0010: Unknown result type (might be due to invalid IL or missing references)
		//IL_0011: Unknown result type (might be due to invalid IL or missing references)
		//IL_0017: Expected O, but got Unknown
		//IL_0018: Unknown result type (might be due to invalid IL or missing references)
		//IL_0030: Unknown result type (might be due to invalid IL or missing references)
		//IL_0036: Expected O, but got Unknown
		//IL_0037: Unknown result type (might be due to invalid IL or missing references)
		//IL_0078: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c0: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c5: Unknown result type (might be due to invalid IL or missing references)
		//IL_00d1: Unknown result type (might be due to invalid IL or missing references)
		//IL_00dd: Unknown result type (might be due to invalid IL or missing references)
		//IL_00e5: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ee: Expected O, but got Unknown
		//IL_0145: Unknown result type (might be due to invalid IL or missing references)
		//IL_0173: Unknown result type (might be due to invalid IL or missing references)
		//IL_0178: Unknown result type (might be due to invalid IL or missing references)
		//IL_0181: Unknown result type (might be due to invalid IL or missing references)
		//IL_0182: Unknown result type (might be due to invalid IL or missing references)
		//IL_018d: Unknown result type (might be due to invalid IL or missing references)
		//IL_018e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0199: Unknown result type (might be due to invalid IL or missing references)
		//IL_01a4: Unknown result type (might be due to invalid IL or missing references)
		//IL_01af: Unknown result type (might be due to invalid IL or missing references)
		//IL_01b0: Unknown result type (might be due to invalid IL or missing references)
		//IL_01bb: Unknown result type (might be due to invalid IL or missing references)
		//IL_01bc: Unknown result type (might be due to invalid IL or missing references)
		//IL_01c7: Unknown result type (might be due to invalid IL or missing references)
		//IL_01d3: Unknown result type (might be due to invalid IL or missing references)
		//IL_01df: Unknown result type (might be due to invalid IL or missing references)
		//IL_01eb: Unknown result type (might be due to invalid IL or missing references)
		//IL_01f7: Unknown result type (might be due to invalid IL or missing references)
		//IL_01f8: Unknown result type (might be due to invalid IL or missing references)
		//IL_0203: Unknown result type (might be due to invalid IL or missing references)
		//IL_020f: Unknown result type (might be due to invalid IL or missing references)
		//IL_021b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0227: Unknown result type (might be due to invalid IL or missing references)
		//IL_0233: Unknown result type (might be due to invalid IL or missing references)
		//IL_0234: Unknown result type (might be due to invalid IL or missing references)
		//IL_023f: Unknown result type (might be due to invalid IL or missing references)
		//IL_024b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0253: Unknown result type (might be due to invalid IL or missing references)
		//IL_025e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0269: Unknown result type (might be due to invalid IL or missing references)
		//IL_026a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0275: Unknown result type (might be due to invalid IL or missing references)
		//IL_027d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0285: Unknown result type (might be due to invalid IL or missing references)
		//IL_0295: Unknown result type (might be due to invalid IL or missing references)
		//IL_029f: Expected O, but got Unknown
		//IL_02a1: Expected O, but got Unknown
		//IL_02b0: Unknown result type (might be due to invalid IL or missing references)
		//IL_02b1: Unknown result type (might be due to invalid IL or missing references)
		//IL_02fc: Unknown result type (might be due to invalid IL or missing references)
		//IL_02fd: Unknown result type (might be due to invalid IL or missing references)
		//IL_031d: Unknown result type (might be due to invalid IL or missing references)
		Identity questId = RawIdentity(56003, 1431802753);
		QuestFullUpdateMessage val = new QuestFullUpdateMessage();
		((N3Message)val).Identity = characterIdentity;
		((N3Message)val).Unknown = 1;
		Quest[] array = new Quest[1];
		Quest val2 = new Quest();
		val2.QuestId = questId;
		val2.Unknown1 = 15;
		val2.Unknown2 = 0;
		val2.Unknown3 = 0;
		val2.Unknown4 = 2;
		val2.ShortInfo = "The Windcaller's requests";
		val2.LongInfo = "The Windcaller's requests<BR><BR>Windcaller Karrec told you to get him a hamburger from an annoying individual. He also told you to get a woman named Maddy Cardile to donate money to his temple.<BR><BR><font color=\"#FF0000\">Mission Objective:<BR>Give Windcaller Karrec a Bronto Burger and Maddy's Credit Card.</font>";
		val2.UnknownId1 = karrecIdentity;
		val2.Unknown5 = 6;
		val2.Unknown6 = 0;
		val2.Unknown7 = 0;
		val2.Unknown8 = 0;
		val2.Unknown9 = 1009;
		val2.Unknown10 = 1009;
		val2.MissionItemData = (MissionItemReward[])(object)new MissionItemReward[1]
		{
			new MissionItemReward
			{
				LowId = 285612,
				HighId = 285612,
				Ql = 1,
				Unknown = 0
			}
		};
		val2.Unknown11 = 1110716998;
		val2.Unknown12 = 0;
		val2.Unknown13 = 0;
		val2.UnknownHash1 = "00000000";
		val2.Unknown14 = 0;
		val2.Unknown15 = 0;
		val2.Unknown16 = 0;
		val2.Unknown17 = 0;
		val2.Unknown18 = 0;
		val2.UnknownId2 = characterIdentity;
		val2.MissionIconId = 244818;
		val2.Unknown20 = 60;
		val2.Unknown21 = 60;
		val2.QuestActions = (QuestActionInfo[])(object)new QuestActionInfo[1]
		{
			new QuestActionInfo
			{
				Version = 24,
				Action = Identity.None,
				UnknownId1 = Identity.None,
				UnknownId2 = RawIdentity(70099, 105201),
				UnknownId3 = Identity.None,
				UnknownId4 = Identity.None,
				Unknown1 = 0f,
				Unknown2 = 0f,
				Unknown3 = 0f,
				Unknown4 = 0f,
				UnknownId5 = Identity.None,
				Unknown5 = 0f,
				Unknown6 = 0f,
				Unknown7 = 0f,
				Unknown8 = 0f,
				UnknownId6 = Identity.None,
				UnknownHash1 = "6A5B02D9",
				Unknown9 = 0,
				UnknownId7 = RawIdentity(54001, 1297226293),
				PlayfieldId = Identity.None,
				Unknown10 = 0,
				Unknown11 = 0,
				Position = new Vector3(0f, 0f, 0f)
			}
		};
		val2.PlayerIds = (Identity[])(object)new Identity[1] { characterIdentity };
		val2.UnknownArray1 = new int[1] { 89266741 };
		val2.UnknownArray2 = new int[0];
		val2.CharacterInfos = (CharacterInfo[])(object)new CharacterInfo[0];
		val2.Unknown22 = 6;
		val2.PlayerIds2 = (Identity[])(object)new Identity[1] { characterIdentity };
		val2.Unknown23 = 0;
		val2.Unknown24 = 105201;
		val2.UnknownId3 = Identity.None;
		val2.Unknown25 = 0;
		val2.Unknown26 = 0;
		val2.QuestIdentities = (QuestIdentity[])(object)new QuestIdentity[0];
		val2.Unknown27 = 7;
		val2.FactionInfos = (Identity[])(object)new Identity[0];
		val2.Unknown28 = 1;
		array[0] = val2;
		val.Quests = (Quest[])(object)array;
		return val;
	}

	private static Identity ResolveKarrecIdentity(Identity identity)
	{
		//IL_0003: Unknown result type (might be due to invalid IL or missing references)
		//IL_000d: Invalid comparison between Unknown and I4
		//IL_001a: Unknown result type (might be due to invalid IL or missing references)
		//IL_003a: Unknown result type (might be due to invalid IL or missing references)
		//IL_003e: Unknown result type (might be due to invalid IL or missing references)
		//IL_003d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0041: Unknown result type (might be due to invalid IL or missing references)
		Identity result;
		if ((int)((Identity)(ref identity)).Type != 50000 || ((Identity)(ref identity)).Instance == 0)
		{
			Identity val = default(Identity);
			((Identity)(ref val)).Type = (IdentityType)50000;
			((Identity)(ref val)).Instance = 2036555963;
			result = val;
		}
		else
		{
			result = identity;
		}
		return result;
	}

	internal static CharacterActionMessage CreateAction59(Identity characterIdentity)
	{
		//IL_0001: Unknown result type (might be due to invalid IL or missing references)
		//IL_0006: Unknown result type (might be due to invalid IL or missing references)
		//IL_0007: Unknown result type (might be due to invalid IL or missing references)
		//IL_000e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0016: Unknown result type (might be due to invalid IL or missing references)
		//IL_001f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0027: Unknown result type (might be due to invalid IL or missing references)
		//IL_0032: Unknown result type (might be due to invalid IL or missing references)
		//IL_003d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0049: Unknown result type (might be due to invalid IL or missing references)
		//IL_0055: Unknown result type (might be due to invalid IL or missing references)
		//IL_005e: Expected O, but got Unknown
		return new CharacterActionMessage
		{
			Identity = characterIdentity,
			Unknown = 0,
			Action = (CharacterActionType)59,
			Unknown1 = 0,
			Target = RawIdentity(56003, 1431802753),
			Parameter1 = 56003,
			Parameter2 = 1431802753,
			Unknown2 = 0
		};
	}

	internal static QuestMessage CreateQuestDelete(Identity characterIdentity)
	{
		//IL_0001: Unknown result type (might be due to invalid IL or missing references)
		//IL_0006: Unknown result type (might be due to invalid IL or missing references)
		//IL_0007: Unknown result type (might be due to invalid IL or missing references)
		//IL_000e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0016: Unknown result type (might be due to invalid IL or missing references)
		//IL_001e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0026: Unknown result type (might be due to invalid IL or missing references)
		//IL_0031: Unknown result type (might be due to invalid IL or missing references)
		//IL_003c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0044: Unknown result type (might be due to invalid IL or missing references)
		//IL_004d: Expected O, but got Unknown
		return new QuestMessage
		{
			Identity = characterIdentity,
			Unknown = 0,
			Action = (QuestAction)1,
			Unknown1 = 0,
			Mission = RawIdentity(56003, 1431802753),
			Unknown2 = 0,
			Unknown3 = 0
		};
	}

	private static bool CanSend(ICharacter character)
	{
		//IL_001a: Unknown result type (might be due to invalid IL or missing references)
		//IL_001f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0022: Unknown result type (might be due to invalid IL or missing references)
		//IL_002c: Invalid comparison between Unknown and I4
		//IL_002f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0034: Unknown result type (might be due to invalid IL or missing references)
		int result;
		if (character != null && ((IDynel)character).Controller != null && ((IDynel)character).Controller.Client != null)
		{
			Identity identity = ((IEntity)character).Identity;
			if ((int)((Identity)(ref identity)).Type == 50000)
			{
				identity = ((IEntity)character).Identity;
				result = ((((Identity)(ref identity)).Instance > 0) ? 1 : 0);
				goto IL_0042;
			}
		}
		result = 0;
		goto IL_0042;
		IL_0042:
		return (byte)result != 0;
	}

	private static Identity RawIdentity(int type, int instance)
	{
		//IL_0003: Unknown result type (might be due to invalid IL or missing references)
		//IL_001b: Unknown result type (might be due to invalid IL or missing references)
		//IL_001c: Unknown result type (might be due to invalid IL or missing references)
		//IL_001f: Unknown result type (might be due to invalid IL or missing references)
		Identity result = default(Identity);
		((Identity)(ref result)).Type = (IdentityType)type;
		((Identity)(ref result)).Instance = instance;
		return result;
	}
}
