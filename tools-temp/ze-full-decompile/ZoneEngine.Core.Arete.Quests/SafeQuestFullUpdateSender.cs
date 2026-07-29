using System;
using AORebirth.Core.Entities;
using AORebirth.Interfaces;
using SmokeLounge.AOtomation.Messaging.GameData;
using SmokeLounge.AOtomation.Messaging.Messages;
using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;
using Utility;

namespace ZoneEngine.Core.Arete.Quests;

public static class SafeQuestFullUpdateSender
{
	private const int MissionIdentityType = 56003;

	private const int B18CInstance = 1427419532;

	private const int B18DInstance = 1427419533;

	private const int B18EInstance = 1427419534;

	private const int B18FInstance = 1427419535;

	private const int B194Instance = 1427419540;

	private const int B196Instance = 1427419542;

	private const int FlintInstance = 1427419544;

	private const int B199Instance = 1427419545;

	private const int B19AInstance = 1427419546;

	private const int FindBioInstance = 1427419547;

	private const int DeliverBioInstance = 1427419548;

	private const int SurveillanceUplinkInstance = 1431980617;

	private const int PlantBugInstance = 1431981627;

	private const int DeliverHc12BillInstance = 1431981628;

	private const int KneecappingInstance = 1431981629;

	private const int ReportToAlexInstance = 1432044389;

	private const int TalkToStanInstance = 1432044390;

	private const int TradeskillNanoSensorInstance = 1432044391;

	private const int LegacySurveillanceUplinkInstance = 1427419549;

	private const int LegacyPlantBugInstance = 1427419550;

	private const int LegacyDeliverHc12BillInstance = 1427419551;

	private const int LegacyKneecappingInstance = 1427419552;

	private const int RexLarssonInstance = 2016273768;

	private const int MarcusStoneInstance = 2016273767;

	private const int B18CUnknownActionIdType = 6553;

	private const int B18CUnknownActionIdInstance = 1296843589;

	private const int B18CUnknownActionId7Type = 54012;

	private const int B18CUnknownActionId7Instance = 475060430;

	private const int B18DUnknownActionId2Type = 70099;

	private const int B18DUnknownActionId2Instance = 105103;

	private const int B18DUnknownActionId7Type = 54001;

	private const int B18DUnknownActionId7Instance = 1293319993;

	private const int B18EUnknownActionId2Type = 70099;

	private const int B18EUnknownActionId2Instance = 1380273235;

	private const int B18EUnknownActionId7Type = 54001;

	private const int B18EUnknownActionId7Instance = 1293319994;

	private const int B18FUnknownActionId2Type = 70099;

	private const int B18FUnknownActionId2Instance = 105040;

	private const int B18FUnknownActionId7Type = 54001;

	private const int B18FUnknownActionId7Instance = 1293319995;

	private const int B194UnknownActionId2Type = 70099;

	private const int B194UnknownActionId2Instance = 104939;

	private const int B194UnknownActionId7Type = 54001;

	private const int B194UnknownActionId7Instance = 1293320000;

	private const string B18CShortInfo = "Terminate 5 Malfunctioning C...";

	private const string B18CLongInfo = "Terminate 5 Malfunctioning Cleaning Robots<BR><br><font color=\"#63ad63\">Identity Crisis:</font><BR>In order to leave Arete Landing and become a citizen of Rubi-Ka, you need an identity. Your mission is to create a fake ID Card to you can leave this place..<br><BR>Rex Larsson considers himself too lazy to clean up his cleaning business. Since you need his help, he wanted a favor in return. You have to terminate 5 of his Malfunctioning Cleaning Robots then open the package with brand new cleaning robots and set them to work.<BR><BR><font color=\"#FF0000\">Mission Objective:<BR>Kill 5 Malfunctining Cleaning Robots.</font>";

	private const string B18DShortInfo = "Open the Cargo Box";

	private const string B18DLongInfo = "Open the Cargo Box<BR><BR>Rex Larsson considers himself too lazy to clean up his cleaning business. Since you need his help, he wanted a favor in return. You have to terminate 5 of his Malfunctioning Cleaning Robots then open the Cargo Box with brand new cleaning robots and set them to work.<BR><BR><font color=\"#FF0000\">Mission Objective:<BR>Use (Right Click) the Cargo Box to open it.</font>";

	private const string B18EShortInfo = "Return to Rex Larsson";

	private const string B18ELongInfo = "Return to Rex Larsson<BR><BR>Return to Rex Larsson to inform him of the great cleaning success.<BR><BR><font color=\"#FF0000\">Mission Objective:<BR>Talk to Rex Larsson.</font>";

	private const string B18FShortInfo = "Talk to Marcus Stone";

	private const string B18FLongInfo = "Talk to Marcus Stone<BR><BR><font color=\"#63ad63\">Identity Crisis:</font><BR>In order to leave Arete Landing and become a citizen of Rubi-Ka, you need an identity. Your mission is to create a fake ID Card to you can leave this place..<BR><BR>Rex Larsson told you to spreak with Marcus Stone, an overseer for arriving cargo in the area, might be able to aid in getting your license issue settled.<BR><BR><font color=\"#FF0000\">Mission Objective:<BR>Talk to Marcus Stone.</font>";

	private const string B194ShortInfo = "Extinguish the Gas Fire";

	private const string B194LongInfo = "Extinguish the Gas Fire<BR><BR>Marcus Stone mentioned that he may be able to assist you with your lack of identity on Rubi-Ka, but at a price. A recent accident on one of his landing pads has left cargo damaged and people injured. Bodies can heal while cargo cannot. Extinguish one of the Gas Fires that has errupted on the landing pad.<BR><BR><font color=\"#FF0000\">Mission Objective:<BR>(Left Click) the <a href='itemref://296780/296780/1'>Compact Fire Suppressant Container</a> in your inventory to lift it up, then Left Click the Gas Fire to apply the fire suppressant.</font>";

	private const string B196ShortInfo = "Return to Marcus";

	private const string B196LongInfo = "Return to Marcus<BR><BR><font color=\"#FF0000\">Mission Objective:<BR>Talk to Marcus Stone and hand him the <a href='itemref://296780/296780/1'>Compact Fire Suppressant Container</a>.</font>";

	private const string FlintShortInfo = "Talk to Flint Novak";

	private const string FlintLongInfo = "Talk to Flint Novak<BR><BR><font color=\"#FF0000\">Mission Objective:<BR>Talk to Flint Novak.</font>";

	private const string FindBioShortInfo = "Find a Bio Analyzing Computer";

	private const string FindBioLongInfo = "Find a Bio Analyzing Computer<BR><BR>At the request of Flint Novak you must  find a Bio Analyzing Computer. You may find one of these computers by taking out the malfunctioning robots in the nearby junkyard.<BR><BR><font color=\"#FF0000\">Mission Objective:<BR>Kill 7 Robots in the junkyard.</font>";

	private const string DeliverBioShortInfo = "Deliver the Bio Analyzing Co...";

	private const string DeliverBioLongInfo = "Deliver the Bio Analyzing Computer to Alex Gibbs<BR><BR>After killing a few junk robots you finally found a Bio Analyzing Computer. Flint Novak told you to give this to Alex Gibbs, a local roboticist.<BR><BR><font color=\"#FF0000\">Mission Objective:<BR>Give the <a href='itemref://156020/156021/1'>Bio Analyzing Computer</a> to Alex Gibbs.</font>";

	private const string SurveillanceUplinkShortInfo = "Surveillance Uplink";

	private const string SurveillanceUplinkLongInfo = "Surveillance Uplink<BR><BR>Alex Gibbs has provided you with a contraption that will be able to hook into the video feed one of Desmond Calitri's Surveillance Droids.<BR><BR><font color=\"#FF0000\">Mission Objective:<BR>Target the Surveillance Droid and use (Right Click) the <a href='itemref://295800/295800/1'>Rebuilt HC-12 SecTec Monitor in your inventory.</a></font>";

	private const string PlantBugShortInfo = "Plant a Bug";

	private const string PlantBugLongInfo = "Plant a Bug<BR><BR>To further incriminate Desmond Calitri, a remote audio recording device is to be placed within his office.<BR><BR><font color=\"#FF0000\">Mission Objective:<BR>Find a suitable location in Desmond Calitri's office to hide the bug. Pick up (Left Click) the <a href='itemref://295801/295801/1'>RC-P Audio Recording Device</a> in your inventory and drop it (Left Click) in a suitable location.</font>";

	private const string DeliverHc12BillShortInfo = "Deliver the Rebuilt HC-12 Se...";

	private const string DeliverHc12BillLongInfo = "Deliver the Rebuilt HC-12 SecTec Monitor<BR><BR>With the Surveillance Droid feed uplink and a hidden audio recording device in Desmond Calitri's office, it is time to deliver this potential evidence to one of Alex's friend ICC Immigration Officer Bill.<BR><BR><font color=\"#FF0000\">Mission Objective:<BR>Give the <a href='itemref://295800/295800/1'>Rebuilt HC-12 SecTec Monitor</a> to ICC Immigration Officer Bill.</font>";

	private const string KneecappingShortInfo = "Kneecapping a Kneebreaker";

	private const string KneecappingLongInfo = "Kneecapping a Kneebreaker<BR><BR>While monitoring the audio and video feeds of Desmond Calitri, it became clear that he intends to send \"The Kneebreaker\", Alfonzo Rizzolo, to deal with an upstart Dockworker who is fighting for fair working conditions.<BR><BR><font color=\"#FF0000\">Mission Objective:<BR>Kill \"The Kneebreaker\".</font>";

	private const string ReportToAlexShortInfo = "Report to Alex";

	private const string ReportToAlexLongInfo = "Report to Alex<BR><BR>You have put a major dent in Demond Caltiri's plans. Since Bill doesn't want to talk to you about this matter, you decided to update Alex on your progress. She did promise you a reward for your efforts...<BR><BR><font color=\"#FF0000\">Mission Objective:<BR>Talk to Alex Gibbs.</font>";

	private const string TalkToStanShortInfo = "Talk to Stan Goodman";

	private const string TalkToStanLongInfo = "Talk to Stan Goodman<BR><BR><font color=\"#63ad63\">Identity Crisis:</font><BR>In order to leave Arete Landing and become a citizen of Rubi-Ka, you need an identity. Your mission is to create a fake ID Card to you can leave this place..<BR><BR>Alex told you to go talk to Stan Goodman, a local 'purveyer of recently used merchandise'. He should be able to help with aquiring more parts for your ID card.<BR><BR><font color=\"#FF0000\">Mission Objective:<BR>Talk to Stan Goodman.</font>";

	private const string TradeskillNanoSensorShortInfo = "Tradeskilling (1/4): Assembl...";

	private const string TradeskillNanoSensorLongInfo = "Tradeskilling (1/4): Assemble a Nano Sensor<BR><BR><font color=\"#FF0000\">WARNING: If you are interested in learning tradeskilling, this mission will help you learn the basics. However, only Engineers and Traders are equipped with profession tools to help them master the art of tradeskilling.</font><BR><BR>Alex Gibbs has provided you with the recipe for creating a <a href='itemref:// 156026/156027/1'>Personalized Basic Robot Brain</a>. Once this mission has been completed, allow her to inspect it. <BR><BR><font color=\"#FFFFFF\">1. Buy the following item from the <a href='itemref://297281/297281/1'>Junk Shop</a>:<BR><a href='itemref://150922/150922/1'><img src=\"rdb://151011\"> Screwdriver</a><BR><BR>2. Find <a href='itemref://42620/42620/1'>Robot Junk</a>.<BR>Do so by killing and looting a robot.<BR><BR>3. Modify the <a href='itemref://42620/42620/1'>Robot Junk</a> with the <a href='itemref://150922/150922/1'>Screwdriver</a> to create a <a href='itemref://150923/150924/1'>Nano Sensor</a>.<BR><a href='itemref://150922/150922/1'><img src=\"rdb://151011\"></a> + <a href='itemref://42620/42620/1'><img src=\"rdb://290417\"></a> = <a href='itemref://150923/150923/1'><img src=\"rdb://149940\"></a><BR></font><BR><font color=\"#FF0000\">Mission Objective: Open the Tradeskill Kit %{KEY:WINDOW_TS}%, place the <a href='itemref://150922/150922/1'>Screwdriver</a> as the Source and the <a href='itemref://42620/42620/1'>Robot Junk</a> as the Target, then press Build.</font>";

	private const string B199ShortInfo = "Use the Stim on a Wounded Do...";

	private const string B199LongInfo = "Use the Stim on a Wounded Dockworker<BR><BR>Marcus Stone's workers got damaged by the fire, he asked you to help him save their lives.<BR><BR> <font color=\"#FF0000\">Mission Objective:<BR>Target a Wounded Dockworker and use the <a href='itemref://297044/297044/1'>Health Regeneration Stim</a> (Right-Click).</font>";

	private const string B19AShortInfo = "Return to Marcus Stone";

	private const string B19ALongInfo = "Return to Marcus Stone<BR><BR>Marcus Stone's workers got damaged by the fire, he asked you to help him save their lives.<BR><BR><font color=\"#FF0000\">Mission Objective:<BR>Return to Marcus Stone and hand him the <a href='itemref://297044/297044/1'>Health Regeneration Stim</a>.</font>";

	private const long TipClientClockBaseSeconds = 1201445827L;

	private const int TipMissionDurationSeconds = 172800;

	public static RexQuestPreviewEmissionResult TrySendB18CPreview(ICharacter source)
	{
		//IL_0049: Unknown result type (might be due to invalid IL or missing references)
		//IL_004e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0052: Unknown result type (might be due to invalid IL or missing references)
		//IL_005c: Invalid comparison between Unknown and I4
		//IL_005f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0064: Unknown result type (might be due to invalid IL or missing references)
		//IL_008b: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a2: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a7: Unknown result type (might be due to invalid IL or missing references)
		if (source == null)
		{
			return RexQuestPreviewEmissionResult.Failed("B18C QuestFullUpdate preview failed: source character missing.");
		}
		if (((IDynel)source).Controller == null || ((IDynel)source).Controller.Client == null)
		{
			return RexQuestPreviewEmissionResult.Failed("B18C QuestFullUpdate preview failed: source client missing.");
		}
		Identity identity = ((IEntity)source).Identity;
		if ((int)((Identity)(ref identity)).Type == 50000)
		{
			identity = ((IEntity)source).Identity;
			if (((Identity)(ref identity)).Instance != 0)
			{
				try
				{
					QuestFullUpdateMessage val = CreateB18CPreviewMessage(((IEntity)source).Identity);
					identity = ((IEntity)source).Identity;
					LogUtil.Debug((DebugInfoDetail)128, "Arete Rex B18C QuestFullUpdate DTO preview sending character=" + ((Identity)(ref identity)).ToString(true) + " mission=Mission:5514B18C rawReplay=false noPersistence=true noRewards=true noQuestDelete=true noCompletion=true");
					((IDynel)source).Controller.Client.SendCompressed((MessageBody)(object)val);
					return RexQuestPreviewEmissionResult.Sent("B18C QuestFullUpdate preview sent using DTO serializer. mission=Mission:5514B18C rawReplay=false noPersistence=true noRewards=true noInventory=true noXpCredits=true noQuestDelete=true noCompletion=true");
				}
				catch (Exception ex)
				{
					LogUtil.Debug((DebugInfoDetail)512, "Arete Rex B18C QuestFullUpdate DTO preview failed: " + ex.Message);
					return RexQuestPreviewEmissionResult.Failed("B18C QuestFullUpdate preview failed during DTO serialization/send: " + ex.Message);
				}
			}
		}
		return RexQuestPreviewEmissionResult.Failed("B18C QuestFullUpdate preview failed: source identity is invalid.");
	}

	public static RexQuestPreviewEmissionResult TrySendB18DPreview(ICharacter source)
	{
		//IL_0049: Unknown result type (might be due to invalid IL or missing references)
		//IL_004e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0052: Unknown result type (might be due to invalid IL or missing references)
		//IL_005c: Invalid comparison between Unknown and I4
		//IL_005f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0064: Unknown result type (might be due to invalid IL or missing references)
		//IL_0093: Unknown result type (might be due to invalid IL or missing references)
		if (source == null)
		{
			return RexQuestPreviewEmissionResult.Failed("B18D QuestFullUpdate preview failed: source character missing.");
		}
		if (((IDynel)source).Controller == null || ((IDynel)source).Controller.Client == null)
		{
			return RexQuestPreviewEmissionResult.Failed("B18D QuestFullUpdate preview failed: source client missing.");
		}
		Identity identity = ((IEntity)source).Identity;
		if ((int)((Identity)(ref identity)).Type == 50000)
		{
			identity = ((IEntity)source).Identity;
			if (((Identity)(ref identity)).Instance != 0)
			{
				try
				{
					((IDynel)source).Controller.Client.SendCompressed((MessageBody)(object)CreateB18DPreviewMessage(((IEntity)source).Identity));
					return RexQuestPreviewEmissionResult.Sent("B18D QuestFullUpdate preview resent. mission=Mission:5514B18D");
				}
				catch (Exception ex)
				{
					return RexQuestPreviewEmissionResult.Failed("B18D QuestFullUpdate preview failed during DTO serialization/send: " + ex.Message);
				}
			}
		}
		return RexQuestPreviewEmissionResult.Failed("B18D QuestFullUpdate preview failed: source identity is invalid.");
	}

	public static bool TrySendB18CCompletionHandoff(ICharacter source)
	{
		//IL_0057: Unknown result type (might be due to invalid IL or missing references)
		//IL_005c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0060: Unknown result type (might be due to invalid IL or missing references)
		//IL_006a: Invalid comparison between Unknown and I4
		//IL_006d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0072: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ab: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c7: Unknown result type (might be due to invalid IL or missing references)
		//IL_00e3: Unknown result type (might be due to invalid IL or missing references)
		//IL_00fe: Unknown result type (might be due to invalid IL or missing references)
		//IL_0103: Unknown result type (might be due to invalid IL or missing references)
		if (source == null)
		{
			LogUtil.Debug((DebugInfoDetail)512, "Arete Rex B18C completion handoff skipped: source character missing.");
			return false;
		}
		if (((IDynel)source).Controller == null || ((IDynel)source).Controller.Client == null)
		{
			LogUtil.Debug((DebugInfoDetail)512, "Arete Rex B18C completion handoff skipped: source client missing.");
			return false;
		}
		Identity identity = ((IEntity)source).Identity;
		if ((int)((Identity)(ref identity)).Type == 50000)
		{
			identity = ((IEntity)source).Identity;
			if (((Identity)(ref identity)).Instance != 0)
			{
				try
				{
					((IDynel)source).Controller.Client.SendCompressed((MessageBody)(object)CreateB18CAction59Message(((IEntity)source).Identity));
					((IDynel)source).Controller.Client.SendCompressed((MessageBody)(object)CreateB18CQuestDeleteMessage(((IEntity)source).Identity));
					((IDynel)source).Controller.Client.SendCompressed((MessageBody)(object)CreateB18DPreviewMessage(((IEntity)source).Identity));
					identity = ((IEntity)source).Identity;
					LogUtil.Debug((DebugInfoDetail)128, "Arete Rex B18C completion handoff sent character=" + ((Identity)(ref identity)).ToString(true) + " action59=Mission:5514B18C questDelete=Mission:5514B18C nextQuestFullUpdate=Mission:5514B18D capture=20260614-194454/events.log:5919-5926 packetHandoffOnly=true noRewards=true noInventory=true noXpCredits=true noDbWrites=true noPersistence=true noCargoBox=true");
					return true;
				}
				catch (Exception ex)
				{
					LogUtil.Debug((DebugInfoDetail)512, "Arete Rex B18C completion handoff failed: " + ex.Message);
					return false;
				}
			}
		}
		LogUtil.Debug((DebugInfoDetail)512, "Arete Rex B18C completion handoff skipped: source identity is invalid.");
		return false;
	}

	public static RexQuestPreviewEmissionResult TrySendB18DQuestDelete(ICharacter source)
	{
		//IL_0049: Unknown result type (might be due to invalid IL or missing references)
		//IL_004e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0052: Unknown result type (might be due to invalid IL or missing references)
		//IL_005c: Invalid comparison between Unknown and I4
		//IL_005f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0064: Unknown result type (might be due to invalid IL or missing references)
		//IL_0095: Unknown result type (might be due to invalid IL or missing references)
		//IL_009a: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c0: Unknown result type (might be due to invalid IL or missing references)
		if (source == null)
		{
			return RexQuestPreviewEmissionResult.Failed("B18D Quest Delete skipped: source character missing.");
		}
		if (((IDynel)source).Controller == null || ((IDynel)source).Controller.Client == null)
		{
			return RexQuestPreviewEmissionResult.Failed("B18D Quest Delete skipped: source client missing.");
		}
		Identity identity = ((IEntity)source).Identity;
		if ((int)((Identity)(ref identity)).Type == 50000)
		{
			identity = ((IEntity)source).Identity;
			if (((Identity)(ref identity)).Instance != 0)
			{
				try
				{
					identity = ((IEntity)source).Identity;
					LogUtil.Debug((DebugInfoDetail)128, "Arete Rex B18D Quest Delete DTO cleanup sending character=" + ((Identity)(ref identity)).ToString(true) + " mission=Mission:5514B18D source=20260614-194454/packets.hex.log:5765 rawReplay=false noAction59=true b18dWindowCleanupOnly=true noCompletionSemantics=true noPersistence=true noRewards=true noInventory=true noXpCredits=true noB18ECompletion=true");
					((IDynel)source).Controller.Client.SendCompressed((MessageBody)(object)CreateB18DQuestDeleteMessage(((IEntity)source).Identity));
					return RexQuestPreviewEmissionResult.Sent("B18D Quest Delete sent using DTO serializer. mission=Mission:5514B18D source=20260614-194454/packets.hex.log:5765 rawReplay=false noAction59=true b18dWindowCleanupOnly=true noCompletionSemantics=true noPersistence=true noRewards=true noInventory=true noXpCredits=true noB18ECompletion=true");
				}
				catch (Exception ex)
				{
					LogUtil.Debug((DebugInfoDetail)512, "Arete Rex B18D Quest Delete DTO cleanup failed: " + ex.Message);
					return RexQuestPreviewEmissionResult.Failed("B18D Quest Delete failed during DTO serialization/send: " + ex.Message);
				}
			}
		}
		return RexQuestPreviewEmissionResult.Failed("B18D Quest Delete skipped: source identity is invalid.");
	}

	public static RexQuestPreviewEmissionResult TrySendB18EPreview(ICharacter source)
	{
		//IL_0049: Unknown result type (might be due to invalid IL or missing references)
		//IL_004e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0052: Unknown result type (might be due to invalid IL or missing references)
		//IL_005c: Invalid comparison between Unknown and I4
		//IL_005f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0064: Unknown result type (might be due to invalid IL or missing references)
		//IL_008b: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a2: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a7: Unknown result type (might be due to invalid IL or missing references)
		if (source == null)
		{
			return RexQuestPreviewEmissionResult.Failed("B18E QuestFullUpdate preview failed: source character missing.");
		}
		if (((IDynel)source).Controller == null || ((IDynel)source).Controller.Client == null)
		{
			return RexQuestPreviewEmissionResult.Failed("B18E QuestFullUpdate preview failed: source client missing.");
		}
		Identity identity = ((IEntity)source).Identity;
		if ((int)((Identity)(ref identity)).Type == 50000)
		{
			identity = ((IEntity)source).Identity;
			if (((Identity)(ref identity)).Instance != 0)
			{
				try
				{
					QuestFullUpdateMessage val = CreateB18EPreviewMessage(((IEntity)source).Identity);
					identity = ((IEntity)source).Identity;
					LogUtil.Debug((DebugInfoDetail)128, "Arete Rex B18E QuestFullUpdate DTO preview sending character=" + ((Identity)(ref identity)).ToString(true) + " mission=Mission:5514B18E source=20260614-194454/packets.hex.log:5767 rawReplay=false noAction59=true noQuestDelete=true noPersistence=true noRewards=true noInventory=true noXpCredits=true noCompletion=true");
					((IDynel)source).Controller.Client.SendCompressed((MessageBody)(object)val);
					return RexQuestPreviewEmissionResult.Sent("B18E QuestFullUpdate preview sent using DTO serializer. mission=Mission:5514B18E source=20260614-194454/packets.hex.log:5767 rawReplay=false noAction59=true noQuestDelete=true noPersistence=true noRewards=true noInventory=true noXpCredits=true noCompletion=true");
				}
				catch (Exception ex)
				{
					LogUtil.Debug((DebugInfoDetail)512, "Arete Rex B18E QuestFullUpdate DTO preview failed: " + ex.Message);
					return RexQuestPreviewEmissionResult.Failed("B18E QuestFullUpdate preview failed during DTO serialization/send: " + ex.Message);
				}
			}
		}
		return RexQuestPreviewEmissionResult.Failed("B18E QuestFullUpdate preview failed: source identity is invalid.");
	}

	public static RexQuestPreviewEmissionResult TrySendB18EQuestDelete(ICharacter source)
	{
		//IL_0049: Unknown result type (might be due to invalid IL or missing references)
		//IL_004e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0052: Unknown result type (might be due to invalid IL or missing references)
		//IL_005c: Invalid comparison between Unknown and I4
		//IL_005f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0064: Unknown result type (might be due to invalid IL or missing references)
		//IL_0095: Unknown result type (might be due to invalid IL or missing references)
		//IL_009a: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c0: Unknown result type (might be due to invalid IL or missing references)
		if (source == null)
		{
			return RexQuestPreviewEmissionResult.Failed("B18E Quest Delete skipped: source character missing.");
		}
		if (((IDynel)source).Controller == null || ((IDynel)source).Controller.Client == null)
		{
			return RexQuestPreviewEmissionResult.Failed("B18E Quest Delete skipped: source client missing.");
		}
		Identity identity = ((IEntity)source).Identity;
		if ((int)((Identity)(ref identity)).Type == 50000)
		{
			identity = ((IEntity)source).Identity;
			if (((Identity)(ref identity)).Instance != 0)
			{
				try
				{
					identity = ((IEntity)source).Identity;
					LogUtil.Debug((DebugInfoDetail)128, "Arete Rex B18E Quest Delete DTO cleanup sending character=" + ((Identity)(ref identity)).ToString(true) + " mission=Mission:5514B18E source=20260614-194454/packets.hex.log:5947 rawReplay=false noAction59=true b18eWindowCleanupOnly=true noCompletionSemantics=true noPersistence=true noCredits=true noItems=true noInventory=true noDbWrites=true");
					((IDynel)source).Controller.Client.SendCompressed((MessageBody)(object)CreateB18EQuestDeleteMessage(((IEntity)source).Identity));
					return RexQuestPreviewEmissionResult.Sent("B18E Quest Delete sent using DTO serializer. mission=Mission:5514B18E source=20260614-194454/packets.hex.log:5947 rawReplay=false noAction59=true b18eWindowCleanupOnly=true noCompletionSemantics=true noPersistence=true noCredits=true noItems=true noInventory=true noDbWrites=true");
				}
				catch (Exception ex)
				{
					LogUtil.Debug((DebugInfoDetail)512, "Arete Rex B18E Quest Delete DTO cleanup failed: " + ex.Message);
					return RexQuestPreviewEmissionResult.Failed("B18E Quest Delete failed during DTO serialization/send: " + ex.Message);
				}
			}
		}
		return RexQuestPreviewEmissionResult.Failed("B18E Quest Delete skipped: source identity is invalid.");
	}

	public static RexQuestPreviewEmissionResult TrySendB18EToB18FHandoff(ICharacter source)
	{
		//IL_0049: Unknown result type (might be due to invalid IL or missing references)
		//IL_004e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0052: Unknown result type (might be due to invalid IL or missing references)
		//IL_005c: Invalid comparison between Unknown and I4
		//IL_005f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0064: Unknown result type (might be due to invalid IL or missing references)
		//IL_0096: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b2: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ce: Unknown result type (might be due to invalid IL or missing references)
		if (source == null)
		{
			return RexQuestPreviewEmissionResult.Failed("B18E→B18F handoff skipped: source character missing.");
		}
		if (((IDynel)source).Controller == null || ((IDynel)source).Controller.Client == null)
		{
			return RexQuestPreviewEmissionResult.Failed("B18E→B18F handoff skipped: source client missing.");
		}
		Identity identity = ((IEntity)source).Identity;
		if ((int)((Identity)(ref identity)).Type == 50000)
		{
			identity = ((IEntity)source).Identity;
			if (((Identity)(ref identity)).Instance != 0)
			{
				try
				{
					((IDynel)source).Controller.Client.SendCompressed((MessageBody)(object)CreateB18EQuestDeleteMessage(((IEntity)source).Identity));
					((IDynel)source).Controller.Client.SendCompressed((MessageBody)(object)CreateB18EQuestDeleteMessage(((IEntity)source).Identity));
					((IDynel)source).Controller.Client.SendCompressed((MessageBody)(object)CreateB18FPreviewMessage(((IEntity)source).Identity));
					return RexQuestPreviewEmissionResult.Sent("B18E→B18F handoff sent delete+delete+Talk to Marcus Stone. source=20260614-194454/packets.hex.log:5947-5949");
				}
				catch (Exception ex)
				{
					LogUtil.Debug((DebugInfoDetail)512, "Arete Rex B18E→B18F handoff failed: " + ex.Message);
					return RexQuestPreviewEmissionResult.Failed("B18E→B18F handoff failed during DTO serialization/send: " + ex.Message);
				}
			}
		}
		return RexQuestPreviewEmissionResult.Failed("B18E→B18F handoff skipped: source identity is invalid.");
	}

	public static RexQuestPreviewEmissionResult TrySendB18FPreview(ICharacter source)
	{
		//IL_0049: Unknown result type (might be due to invalid IL or missing references)
		//IL_004e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0052: Unknown result type (might be due to invalid IL or missing references)
		//IL_005c: Invalid comparison between Unknown and I4
		//IL_005f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0064: Unknown result type (might be due to invalid IL or missing references)
		//IL_008b: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a2: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a7: Unknown result type (might be due to invalid IL or missing references)
		if (source == null)
		{
			return RexQuestPreviewEmissionResult.Failed("B18F QuestFullUpdate preview failed: source character missing.");
		}
		if (((IDynel)source).Controller == null || ((IDynel)source).Controller.Client == null)
		{
			return RexQuestPreviewEmissionResult.Failed("B18F QuestFullUpdate preview failed: source client missing.");
		}
		Identity identity = ((IEntity)source).Identity;
		if ((int)((Identity)(ref identity)).Type == 50000)
		{
			identity = ((IEntity)source).Identity;
			if (((Identity)(ref identity)).Instance != 0)
			{
				try
				{
					QuestFullUpdateMessage val = CreateB18FPreviewMessage(((IEntity)source).Identity);
					identity = ((IEntity)source).Identity;
					LogUtil.Debug((DebugInfoDetail)128, "Arete Rex B18F QuestFullUpdate DTO handoff sending character=" + ((Identity)(ref identity)).ToString(true) + " mission=Mission:5514B18F source=20260614-194454/packets.hex.log:5949 nextNpc=SimpleChar:782DE567 rawReplay=false noAction59=true noQuestDelete=true noPersistence=true noCredits=true noItems=true noInventory=true noMarcusStoneImplementation=true");
					((IDynel)source).Controller.Client.SendCompressed((MessageBody)(object)val);
					return RexQuestPreviewEmissionResult.Sent("B18F QuestFullUpdate sent using DTO serializer. mission=Mission:5514B18F source=20260614-194454/packets.hex.log:5949 title=\"Talk to Marcus Stone\" nextNpc=SimpleChar:782DE567 rawReplay=false noAction59=true noQuestDelete=true noPersistence=true noCredits=true noItems=true noInventory=true noMarcusStoneImplementation=true");
				}
				catch (Exception ex)
				{
					LogUtil.Debug((DebugInfoDetail)512, "Arete Rex B18F QuestFullUpdate DTO handoff failed: " + ex.Message);
					return RexQuestPreviewEmissionResult.Failed("B18F QuestFullUpdate failed during DTO serialization/send: " + ex.Message);
				}
			}
		}
		return RexQuestPreviewEmissionResult.Failed("B18F QuestFullUpdate preview failed: source identity is invalid.");
	}

	public static RexQuestPreviewEmissionResult TrySendB18FToB194Handoff(ICharacter source)
	{
		//IL_0049: Unknown result type (might be due to invalid IL or missing references)
		//IL_004e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0052: Unknown result type (might be due to invalid IL or missing references)
		//IL_005c: Invalid comparison between Unknown and I4
		//IL_005f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0064: Unknown result type (might be due to invalid IL or missing references)
		//IL_0096: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b2: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ce: Unknown result type (might be due to invalid IL or missing references)
		if (source == null)
		{
			return RexQuestPreviewEmissionResult.Failed("B18F→B194 handoff skipped: source character missing.");
		}
		if (((IDynel)source).Controller == null || ((IDynel)source).Controller.Client == null)
		{
			return RexQuestPreviewEmissionResult.Failed("B18F→B194 handoff skipped: source client missing.");
		}
		Identity identity = ((IEntity)source).Identity;
		if ((int)((Identity)(ref identity)).Type == 50000)
		{
			identity = ((IEntity)source).Identity;
			if (((Identity)(ref identity)).Instance != 0)
			{
				try
				{
					((IDynel)source).Controller.Client.SendCompressed((MessageBody)(object)CreateB18FAction59Message(((IEntity)source).Identity));
					((IDynel)source).Controller.Client.SendCompressed((MessageBody)(object)CreateB18FQuestDeleteMessage(((IEntity)source).Identity));
					((IDynel)source).Controller.Client.SendCompressed((MessageBody)(object)CreateB194PreviewMessage(((IEntity)source).Identity));
					return RexQuestPreviewEmissionResult.Sent("B18F→B194 handoff sent action59+delete+Extinguish the Gas Fire. source=20260719-Rex-Markus-stone/mission-flow.log:8-10");
				}
				catch (Exception ex)
				{
					LogUtil.Debug((DebugInfoDetail)512, "Arete Marcus B18F→B194 handoff failed: " + ex.Message);
					return RexQuestPreviewEmissionResult.Failed("B18F→B194 handoff failed during DTO serialization/send: " + ex.Message);
				}
			}
		}
		return RexQuestPreviewEmissionResult.Failed("B18F→B194 handoff skipped: source identity is invalid.");
	}

	public static RexQuestPreviewEmissionResult TrySendB18FQuestDelete(ICharacter source)
	{
		//IL_0049: Unknown result type (might be due to invalid IL or missing references)
		//IL_004e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0052: Unknown result type (might be due to invalid IL or missing references)
		//IL_005c: Invalid comparison between Unknown and I4
		//IL_005f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0064: Unknown result type (might be due to invalid IL or missing references)
		//IL_0095: Unknown result type (might be due to invalid IL or missing references)
		//IL_009a: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c0: Unknown result type (might be due to invalid IL or missing references)
		if (source == null)
		{
			return RexQuestPreviewEmissionResult.Failed("B18F Quest Delete skipped: source character missing.");
		}
		if (((IDynel)source).Controller == null || ((IDynel)source).Controller.Client == null)
		{
			return RexQuestPreviewEmissionResult.Failed("B18F Quest Delete skipped: source client missing.");
		}
		Identity identity = ((IEntity)source).Identity;
		if ((int)((Identity)(ref identity)).Type == 50000)
		{
			identity = ((IEntity)source).Identity;
			if (((Identity)(ref identity)).Instance != 0)
			{
				try
				{
					identity = ((IEntity)source).Identity;
					LogUtil.Debug((DebugInfoDetail)128, "Arete Marcus B18F Quest Delete DTO cleanup sending character=" + ((Identity)(ref identity)).ToString(true) + " mission=Mission:5514B18F source=20260614-195107/events.log:1645-1646 rawReplay=false b18fWindowCleanupOnly=true noCompletionSemantics=true noPersistence=true noRewards=true noInventory=true noXpCredits=true");
					((IDynel)source).Controller.Client.SendCompressed((MessageBody)(object)CreateB18FQuestDeleteMessage(((IEntity)source).Identity));
					return RexQuestPreviewEmissionResult.Sent("B18F Quest Delete sent using DTO serializer. mission=Mission:5514B18F source=20260614-195107/events.log:1645-1646 rawReplay=false b18fWindowCleanupOnly=true noCompletionSemantics=true noPersistence=true noRewards=true noInventory=true noXpCredits=true");
				}
				catch (Exception ex)
				{
					LogUtil.Debug((DebugInfoDetail)512, "Arete Marcus B18F Quest Delete DTO cleanup failed: " + ex.Message);
					return RexQuestPreviewEmissionResult.Failed("B18F Quest Delete failed during DTO serialization/send: " + ex.Message);
				}
			}
		}
		return RexQuestPreviewEmissionResult.Failed("B18F Quest Delete skipped: source identity is invalid.");
	}

	public static RexQuestPreviewEmissionResult TrySendB194Preview(ICharacter source)
	{
		//IL_0049: Unknown result type (might be due to invalid IL or missing references)
		//IL_004e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0052: Unknown result type (might be due to invalid IL or missing references)
		//IL_005c: Invalid comparison between Unknown and I4
		//IL_005f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0064: Unknown result type (might be due to invalid IL or missing references)
		//IL_008b: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a2: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a7: Unknown result type (might be due to invalid IL or missing references)
		if (source == null)
		{
			return RexQuestPreviewEmissionResult.Failed("B194 QuestFullUpdate preview failed: source character missing.");
		}
		if (((IDynel)source).Controller == null || ((IDynel)source).Controller.Client == null)
		{
			return RexQuestPreviewEmissionResult.Failed("B194 QuestFullUpdate preview failed: source client missing.");
		}
		Identity identity = ((IEntity)source).Identity;
		if ((int)((Identity)(ref identity)).Type == 50000)
		{
			identity = ((IEntity)source).Identity;
			if (((Identity)(ref identity)).Instance != 0)
			{
				try
				{
					QuestFullUpdateMessage val = CreateB194PreviewMessage(((IEntity)source).Identity);
					identity = ((IEntity)source).Identity;
					LogUtil.Debug((DebugInfoDetail)128, "Arete Marcus B194 QuestFullUpdate DTO preview sending character=" + ((Identity)(ref identity)).ToString(true) + " mission=Mission:5514B194 source=20260614-195107/packets.hex.log:1407 trigger=marcus_195107_b18f_002:0 rawReplay=false noPersistence=true noRewards=true noInventory=true item296780Deferred=true noFollowUpMission=true noTrade=true");
					((IDynel)source).Controller.Client.SendCompressed((MessageBody)(object)val);
					return RexQuestPreviewEmissionResult.Sent("B194 QuestFullUpdate preview sent using DTO serializer. mission=Mission:5514B194 source=20260614-195107/packets.hex.log:1407 title=\"Extinguish the Gas Fire\" rawReplay=false noPersistence=true noRewards=true noInventory=true item296780Deferred=true noFollowUpMission=true noTrade=true");
				}
				catch (Exception ex)
				{
					LogUtil.Debug((DebugInfoDetail)512, "Arete Marcus B194 QuestFullUpdate DTO preview failed: " + ex.Message);
					return RexQuestPreviewEmissionResult.Failed("B194 QuestFullUpdate preview failed during DTO serialization/send: " + ex.Message);
				}
			}
		}
		return RexQuestPreviewEmissionResult.Failed("B194 QuestFullUpdate preview failed: source identity is invalid.");
	}

	public static RexQuestPreviewEmissionResult TrySendB194QuestDelete(ICharacter source)
	{
		//IL_0049: Unknown result type (might be due to invalid IL or missing references)
		//IL_004e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0052: Unknown result type (might be due to invalid IL or missing references)
		//IL_005c: Invalid comparison between Unknown and I4
		//IL_005f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0064: Unknown result type (might be due to invalid IL or missing references)
		//IL_0093: Unknown result type (might be due to invalid IL or missing references)
		//IL_00af: Unknown result type (might be due to invalid IL or missing references)
		if (source == null)
		{
			return RexQuestPreviewEmissionResult.Failed("B194 Quest Delete skipped: source character missing.");
		}
		if (((IDynel)source).Controller == null || ((IDynel)source).Controller.Client == null)
		{
			return RexQuestPreviewEmissionResult.Failed("B194 Quest Delete skipped: source client missing.");
		}
		Identity identity = ((IEntity)source).Identity;
		if ((int)((Identity)(ref identity)).Type == 50000)
		{
			identity = ((IEntity)source).Identity;
			if (((Identity)(ref identity)).Instance != 0)
			{
				try
				{
					((IDynel)source).Controller.Client.SendCompressed((MessageBody)(object)CreateB194Action59Message(((IEntity)source).Identity));
					((IDynel)source).Controller.Client.SendCompressed((MessageBody)(object)CreateB194QuestDeleteMessage(((IEntity)source).Identity));
					return RexQuestPreviewEmissionResult.Sent("B194 Action59+Quest Delete sent. mission=Mission:5514B194 source=20260719-Rex-Markus-stone/events.log:11002-11006");
				}
				catch (Exception ex)
				{
					LogUtil.Debug((DebugInfoDetail)512, "Arete Marcus B194 Quest Delete DTO cleanup failed: " + ex.Message);
					return RexQuestPreviewEmissionResult.Failed("B194 Quest Delete failed during DTO serialization/send: " + ex.Message);
				}
			}
		}
		return RexQuestPreviewEmissionResult.Failed("B194 Quest Delete skipped: source identity is invalid.");
	}

	public static RexQuestPreviewEmissionResult TrySendB194ToB196Handoff(ICharacter source)
	{
		//IL_0049: Unknown result type (might be due to invalid IL or missing references)
		//IL_004e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0052: Unknown result type (might be due to invalid IL or missing references)
		//IL_005c: Invalid comparison between Unknown and I4
		//IL_005f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0064: Unknown result type (might be due to invalid IL or missing references)
		//IL_0096: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b2: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ce: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ea: Unknown result type (might be due to invalid IL or missing references)
		//IL_0106: Unknown result type (might be due to invalid IL or missing references)
		if (source == null)
		{
			return RexQuestPreviewEmissionResult.Failed("B194→B196 handoff skipped: source character missing.");
		}
		if (((IDynel)source).Controller == null || ((IDynel)source).Controller.Client == null)
		{
			return RexQuestPreviewEmissionResult.Failed("B194→B196 handoff skipped: source client missing.");
		}
		Identity identity = ((IEntity)source).Identity;
		if ((int)((Identity)(ref identity)).Type == 50000)
		{
			identity = ((IEntity)source).Identity;
			if (((Identity)(ref identity)).Instance != 0)
			{
				try
				{
					((IDynel)source).Controller.Client.SendCompressed((MessageBody)(object)CreateB194Action59Message(((IEntity)source).Identity));
					((IDynel)source).Controller.Client.SendCompressed((MessageBody)(object)CreateB194QuestDeleteMessage(((IEntity)source).Identity));
					((IDynel)source).Controller.Client.SendCompressed((MessageBody)(object)CreateB18FQuestDeleteMessage(((IEntity)source).Identity));
					((IDynel)source).Controller.Client.SendCompressed((MessageBody)(object)CreateB18EQuestDeleteMessage(((IEntity)source).Identity));
					((IDynel)source).Controller.Client.SendCompressed((MessageBody)(object)CreateB196PreviewMessage(((IEntity)source).Identity));
					return RexQuestPreviewEmissionResult.Sent("B194→B196 handoff sent action59+delete B194/B18F/B18E + Return to Marcus. source=20260719-Rex-Markus-stone/events.log:11002-11008");
				}
				catch (Exception ex)
				{
					LogUtil.Debug((DebugInfoDetail)512, "Arete Marcus B194→B196 handoff failed: " + ex.Message);
					return RexQuestPreviewEmissionResult.Failed("B194→B196 handoff failed during DTO serialization/send: " + ex.Message);
				}
			}
		}
		return RexQuestPreviewEmissionResult.Failed("B194→B196 handoff skipped: source identity is invalid.");
	}

	public static RexQuestPreviewEmissionResult TrySendB196Preview(ICharacter source)
	{
		//IL_0049: Unknown result type (might be due to invalid IL or missing references)
		//IL_004e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0052: Unknown result type (might be due to invalid IL or missing references)
		//IL_005c: Invalid comparison between Unknown and I4
		//IL_005f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0064: Unknown result type (might be due to invalid IL or missing references)
		//IL_0093: Unknown result type (might be due to invalid IL or missing references)
		if (source == null)
		{
			return RexQuestPreviewEmissionResult.Failed("B196 QuestFullUpdate preview failed: source character missing.");
		}
		if (((IDynel)source).Controller == null || ((IDynel)source).Controller.Client == null)
		{
			return RexQuestPreviewEmissionResult.Failed("B196 QuestFullUpdate preview failed: source client missing.");
		}
		Identity identity = ((IEntity)source).Identity;
		if ((int)((Identity)(ref identity)).Type == 50000)
		{
			identity = ((IEntity)source).Identity;
			if (((Identity)(ref identity)).Instance != 0)
			{
				try
				{
					((IDynel)source).Controller.Client.SendCompressed((MessageBody)(object)CreateB196PreviewMessage(((IEntity)source).Identity));
					return RexQuestPreviewEmissionResult.Sent("B196 QuestFullUpdate preview sent using DTO serializer. mission=Mission:5514B196 title=\"Return to Marcus\" source=20260614-195107/packets.hex.log:1773");
				}
				catch (Exception ex)
				{
					LogUtil.Debug((DebugInfoDetail)512, "Arete Marcus B196 QuestFullUpdate DTO preview failed: " + ex.Message);
					return RexQuestPreviewEmissionResult.Failed("B196 QuestFullUpdate preview failed during DTO serialization/send: " + ex.Message);
				}
			}
		}
		return RexQuestPreviewEmissionResult.Failed("B196 QuestFullUpdate preview failed: source identity is invalid.");
	}

	public static RexQuestPreviewEmissionResult TrySendB196QuestDelete(ICharacter source)
	{
		//IL_0049: Unknown result type (might be due to invalid IL or missing references)
		//IL_004e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0052: Unknown result type (might be due to invalid IL or missing references)
		//IL_005c: Invalid comparison between Unknown and I4
		//IL_005f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0064: Unknown result type (might be due to invalid IL or missing references)
		//IL_0093: Unknown result type (might be due to invalid IL or missing references)
		if (source == null)
		{
			return RexQuestPreviewEmissionResult.Failed("B196 Quest Delete skipped: source character missing.");
		}
		if (((IDynel)source).Controller == null || ((IDynel)source).Controller.Client == null)
		{
			return RexQuestPreviewEmissionResult.Failed("B196 Quest Delete skipped: source client missing.");
		}
		Identity identity = ((IEntity)source).Identity;
		if ((int)((Identity)(ref identity)).Type == 50000)
		{
			identity = ((IEntity)source).Identity;
			if (((Identity)(ref identity)).Instance != 0)
			{
				try
				{
					((IDynel)source).Controller.Client.SendCompressed((MessageBody)(object)CreateB196QuestDeleteMessage(((IEntity)source).Identity));
					return RexQuestPreviewEmissionResult.Sent("B196 Quest Delete sent. mission=Mission:5514B196");
				}
				catch (Exception ex)
				{
					LogUtil.Debug((DebugInfoDetail)512, "Arete Marcus B196 Quest Delete failed: " + ex.Message);
					return RexQuestPreviewEmissionResult.Failed("B196 Quest Delete failed during DTO serialization/send: " + ex.Message);
				}
			}
		}
		return RexQuestPreviewEmissionResult.Failed("B196 Quest Delete skipped: source identity is invalid.");
	}

	public static RexQuestPreviewEmissionResult TrySendB196CompletionCleanup(ICharacter source)
	{
		//IL_0049: Unknown result type (might be due to invalid IL or missing references)
		//IL_004e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0052: Unknown result type (might be due to invalid IL or missing references)
		//IL_005c: Invalid comparison between Unknown and I4
		//IL_005f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0064: Unknown result type (might be due to invalid IL or missing references)
		//IL_0096: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b2: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ce: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ea: Unknown result type (might be due to invalid IL or missing references)
		if (source == null)
		{
			return RexQuestPreviewEmissionResult.Failed("B196 cleanup skipped: source character missing.");
		}
		if (((IDynel)source).Controller == null || ((IDynel)source).Controller.Client == null)
		{
			return RexQuestPreviewEmissionResult.Failed("B196 cleanup skipped: source client missing.");
		}
		Identity identity = ((IEntity)source).Identity;
		if ((int)((Identity)(ref identity)).Type == 50000)
		{
			identity = ((IEntity)source).Identity;
			if (((Identity)(ref identity)).Instance != 0)
			{
				try
				{
					((IDynel)source).Controller.Client.SendCompressed((MessageBody)(object)CreateB196QuestDeleteMessage(((IEntity)source).Identity));
					((IDynel)source).Controller.Client.SendCompressed((MessageBody)(object)CreateB194QuestDeleteMessage(((IEntity)source).Identity));
					((IDynel)source).Controller.Client.SendCompressed((MessageBody)(object)CreateB18FQuestDeleteMessage(((IEntity)source).Identity));
					((IDynel)source).Controller.Client.SendCompressed((MessageBody)(object)CreateB18EQuestDeleteMessage(((IEntity)source).Identity));
					return RexQuestPreviewEmissionResult.Sent("B196 completion cleanup deleted B196/B194/B18F/B18E from mission window.");
				}
				catch (Exception ex)
				{
					LogUtil.Debug((DebugInfoDetail)512, "Arete Marcus B196 completion cleanup failed: " + ex.Message);
					return RexQuestPreviewEmissionResult.Failed("B196 completion cleanup failed during DTO serialization/send: " + ex.Message);
				}
			}
		}
		return RexQuestPreviewEmissionResult.Failed("B196 cleanup skipped: source identity is invalid.");
	}

	public static RexQuestPreviewEmissionResult TrySendB196ToFlintHandoff(ICharacter source)
	{
		//IL_0049: Unknown result type (might be due to invalid IL or missing references)
		//IL_004e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0052: Unknown result type (might be due to invalid IL or missing references)
		//IL_005c: Invalid comparison between Unknown and I4
		//IL_005f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0064: Unknown result type (might be due to invalid IL or missing references)
		//IL_0096: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b2: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ce: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ea: Unknown result type (might be due to invalid IL or missing references)
		//IL_0106: Unknown result type (might be due to invalid IL or missing references)
		//IL_0122: Unknown result type (might be due to invalid IL or missing references)
		if (source == null)
		{
			return RexQuestPreviewEmissionResult.Failed("B196→Flint handoff skipped: source character missing.");
		}
		if (((IDynel)source).Controller == null || ((IDynel)source).Controller.Client == null)
		{
			return RexQuestPreviewEmissionResult.Failed("B196→Flint handoff skipped: source client missing.");
		}
		Identity identity = ((IEntity)source).Identity;
		if ((int)((Identity)(ref identity)).Type == 50000)
		{
			identity = ((IEntity)source).Identity;
			if (((Identity)(ref identity)).Instance != 0)
			{
				try
				{
					((IDynel)source).Controller.Client.SendCompressed((MessageBody)(object)CreateB196Action59Message(((IEntity)source).Identity));
					((IDynel)source).Controller.Client.SendCompressed((MessageBody)(object)CreateB196QuestDeleteMessage(((IEntity)source).Identity));
					((IDynel)source).Controller.Client.SendCompressed((MessageBody)(object)CreateB194QuestDeleteMessage(((IEntity)source).Identity));
					((IDynel)source).Controller.Client.SendCompressed((MessageBody)(object)CreateB18FQuestDeleteMessage(((IEntity)source).Identity));
					((IDynel)source).Controller.Client.SendCompressed((MessageBody)(object)CreateB18EQuestDeleteMessage(((IEntity)source).Identity));
					((IDynel)source).Controller.Client.SendCompressed((MessageBody)(object)CreateFlintPreviewMessage(((IEntity)source).Identity));
					return RexQuestPreviewEmissionResult.Sent("B196→Flint handoff Action59+Delete leftovers + Talk to Flint Novak. mission=Mission:5514B198 source=20260719-185137/events.log:12204-12210");
				}
				catch (Exception ex)
				{
					LogUtil.Debug((DebugInfoDetail)512, "Arete Marcus B196→Flint handoff failed: " + ex.Message);
					return RexQuestPreviewEmissionResult.Failed("B196→Flint handoff failed during DTO serialization/send: " + ex.Message);
				}
			}
		}
		return RexQuestPreviewEmissionResult.Failed("B196→Flint handoff skipped: source identity is invalid.");
	}

	public static RexQuestPreviewEmissionResult TrySendFlintPreview(ICharacter source)
	{
		//IL_0049: Unknown result type (might be due to invalid IL or missing references)
		//IL_004e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0052: Unknown result type (might be due to invalid IL or missing references)
		//IL_005c: Invalid comparison between Unknown and I4
		//IL_005f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0064: Unknown result type (might be due to invalid IL or missing references)
		//IL_0093: Unknown result type (might be due to invalid IL or missing references)
		if (source == null)
		{
			return RexQuestPreviewEmissionResult.Failed("Flint QuestFullUpdate preview failed: source character missing.");
		}
		if (((IDynel)source).Controller == null || ((IDynel)source).Controller.Client == null)
		{
			return RexQuestPreviewEmissionResult.Failed("Flint QuestFullUpdate preview failed: source client missing.");
		}
		Identity identity = ((IEntity)source).Identity;
		if ((int)((Identity)(ref identity)).Type == 50000)
		{
			identity = ((IEntity)source).Identity;
			if (((Identity)(ref identity)).Instance != 0)
			{
				try
				{
					((IDynel)source).Controller.Client.SendCompressed((MessageBody)(object)CreateFlintPreviewMessage(((IEntity)source).Identity));
					return RexQuestPreviewEmissionResult.Sent("Flint QuestFullUpdate preview sent. mission=Mission:5514B198 title=\"Talk to Flint Novak\"");
				}
				catch (Exception ex)
				{
					LogUtil.Debug((DebugInfoDetail)512, "Arete Flint QuestFullUpdate DTO preview failed: " + ex.Message);
					return RexQuestPreviewEmissionResult.Failed("Flint QuestFullUpdate preview failed during DTO serialization/send: " + ex.Message);
				}
			}
		}
		return RexQuestPreviewEmissionResult.Failed("Flint QuestFullUpdate preview failed: source identity is invalid.");
	}

	public static RexQuestPreviewEmissionResult TrySendFlintToB199Handoff(ICharacter source)
	{
		return TrySendB199Preview(source);
	}

	public static RexQuestPreviewEmissionResult TrySendB199ToB19AHandoff(ICharacter source)
	{
		//IL_0049: Unknown result type (might be due to invalid IL or missing references)
		//IL_004e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0052: Unknown result type (might be due to invalid IL or missing references)
		//IL_005c: Invalid comparison between Unknown and I4
		//IL_005f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0064: Unknown result type (might be due to invalid IL or missing references)
		//IL_0096: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b2: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ce: Unknown result type (might be due to invalid IL or missing references)
		if (source == null)
		{
			return RexQuestPreviewEmissionResult.Failed("B199→B19A handoff skipped: source character missing.");
		}
		if (((IDynel)source).Controller == null || ((IDynel)source).Controller.Client == null)
		{
			return RexQuestPreviewEmissionResult.Failed("B199→B19A handoff skipped: source client missing.");
		}
		Identity identity = ((IEntity)source).Identity;
		if ((int)((Identity)(ref identity)).Type == 50000)
		{
			identity = ((IEntity)source).Identity;
			if (((Identity)(ref identity)).Instance != 0)
			{
				try
				{
					((IDynel)source).Controller.Client.SendCompressed((MessageBody)(object)CreateB199Action59Message(((IEntity)source).Identity));
					((IDynel)source).Controller.Client.SendCompressed((MessageBody)(object)CreateB199QuestDeleteMessage(((IEntity)source).Identity));
					((IDynel)source).Controller.Client.SendCompressed((MessageBody)(object)CreateB19APreviewMessage(((IEntity)source).Identity));
					return RexQuestPreviewEmissionResult.Sent("B199→B19A handoff Action59+Delete + Return to Marcus Stone. mission=Mission:5514B19A source=20260719-224226/events.log");
				}
				catch (Exception ex)
				{
					LogUtil.Debug((DebugInfoDetail)512, "Arete Marcus B199→B19A handoff failed: " + ex.Message);
					return RexQuestPreviewEmissionResult.Failed("B199→B19A handoff failed during DTO serialization/send: " + ex.Message);
				}
			}
		}
		return RexQuestPreviewEmissionResult.Failed("B199→B19A handoff skipped: source identity is invalid.");
	}

	public static RexQuestPreviewEmissionResult TrySendB19ACompletionCleanup(ICharacter source)
	{
		//IL_0049: Unknown result type (might be due to invalid IL or missing references)
		//IL_004e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0052: Unknown result type (might be due to invalid IL or missing references)
		//IL_005c: Invalid comparison between Unknown and I4
		//IL_005f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0064: Unknown result type (might be due to invalid IL or missing references)
		//IL_0093: Unknown result type (might be due to invalid IL or missing references)
		//IL_00af: Unknown result type (might be due to invalid IL or missing references)
		if (source == null)
		{
			return RexQuestPreviewEmissionResult.Failed("B19A cleanup skipped: source character missing.");
		}
		if (((IDynel)source).Controller == null || ((IDynel)source).Controller.Client == null)
		{
			return RexQuestPreviewEmissionResult.Failed("B19A cleanup skipped: source client missing.");
		}
		Identity identity = ((IEntity)source).Identity;
		if ((int)((Identity)(ref identity)).Type == 50000)
		{
			identity = ((IEntity)source).Identity;
			if (((Identity)(ref identity)).Instance != 0)
			{
				try
				{
					((IDynel)source).Controller.Client.SendCompressed((MessageBody)(object)CreateB19AQuestDeleteMessage(((IEntity)source).Identity));
					((IDynel)source).Controller.Client.SendCompressed((MessageBody)(object)CreateB19AQuestDeleteMessage(((IEntity)source).Identity));
					return RexQuestPreviewEmissionResult.Sent("B19A completion cleanup Delete×2 (no Action59). mission=Mission:5514B19A keepFlint=true");
				}
				catch (Exception ex)
				{
					LogUtil.Debug((DebugInfoDetail)512, "Arete Marcus B19A cleanup failed: " + ex.Message);
					return RexQuestPreviewEmissionResult.Failed("B19A cleanup failed during DTO serialization/send: " + ex.Message);
				}
			}
		}
		return RexQuestPreviewEmissionResult.Failed("B19A cleanup skipped: source identity is invalid.");
	}

	public static RexQuestPreviewEmissionResult TrySendFlintQuestDeleteOnly(ICharacter source)
	{
		//IL_0049: Unknown result type (might be due to invalid IL or missing references)
		//IL_004e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0052: Unknown result type (might be due to invalid IL or missing references)
		//IL_005c: Invalid comparison between Unknown and I4
		//IL_005f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0064: Unknown result type (might be due to invalid IL or missing references)
		//IL_0093: Unknown result type (might be due to invalid IL or missing references)
		if (source == null)
		{
			return RexQuestPreviewEmissionResult.Failed("Flint delete skipped: source character missing.");
		}
		if (((IDynel)source).Controller == null || ((IDynel)source).Controller.Client == null)
		{
			return RexQuestPreviewEmissionResult.Failed("Flint delete skipped: source client missing.");
		}
		Identity identity = ((IEntity)source).Identity;
		if ((int)((Identity)(ref identity)).Type == 50000)
		{
			identity = ((IEntity)source).Identity;
			if (((Identity)(ref identity)).Instance != 0)
			{
				try
				{
					((IDynel)source).Controller.Client.SendCompressed((MessageBody)(object)CreateFlintQuestDeleteMessage(((IEntity)source).Identity));
					return RexQuestPreviewEmissionResult.Sent("Flint Quest Delete only. mission=Mission:5514B198");
				}
				catch (Exception ex)
				{
					LogUtil.Debug((DebugInfoDetail)512, "Arete Marcus Flint delete failed: " + ex.Message);
					return RexQuestPreviewEmissionResult.Failed("Flint delete failed during DTO serialization/send: " + ex.Message);
				}
			}
		}
		return RexQuestPreviewEmissionResult.Failed("Flint delete skipped: source identity is invalid.");
	}

	public static RexQuestPreviewEmissionResult TrySendB199Preview(ICharacter source)
	{
		//IL_0049: Unknown result type (might be due to invalid IL or missing references)
		//IL_004e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0052: Unknown result type (might be due to invalid IL or missing references)
		//IL_005c: Invalid comparison between Unknown and I4
		//IL_005f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0064: Unknown result type (might be due to invalid IL or missing references)
		//IL_0093: Unknown result type (might be due to invalid IL or missing references)
		if (source == null)
		{
			return RexQuestPreviewEmissionResult.Failed("B199 preview failed: source character missing.");
		}
		if (((IDynel)source).Controller == null || ((IDynel)source).Controller.Client == null)
		{
			return RexQuestPreviewEmissionResult.Failed("B199 preview failed: source client missing.");
		}
		Identity identity = ((IEntity)source).Identity;
		if ((int)((Identity)(ref identity)).Type == 50000)
		{
			identity = ((IEntity)source).Identity;
			if (((Identity)(ref identity)).Instance != 0)
			{
				try
				{
					((IDynel)source).Controller.Client.SendCompressed((MessageBody)(object)CreateB199PreviewMessage(((IEntity)source).Identity));
					return RexQuestPreviewEmissionResult.Sent("B199 QuestFullUpdate preview sent. mission=Mission:5514B199 title=\"Use the Stim on a Wounded Dockworker\"");
				}
				catch (Exception ex)
				{
					LogUtil.Debug((DebugInfoDetail)512, "Arete Marcus B199 preview failed: " + ex.Message);
					return RexQuestPreviewEmissionResult.Failed("B199 preview failed during DTO serialization/send: " + ex.Message);
				}
			}
		}
		return RexQuestPreviewEmissionResult.Failed("B199 preview failed: source identity is invalid.");
	}

	public static RexQuestPreviewEmissionResult TrySendB19APreview(ICharacter source)
	{
		//IL_0049: Unknown result type (might be due to invalid IL or missing references)
		//IL_004e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0052: Unknown result type (might be due to invalid IL or missing references)
		//IL_005c: Invalid comparison between Unknown and I4
		//IL_005f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0064: Unknown result type (might be due to invalid IL or missing references)
		//IL_0093: Unknown result type (might be due to invalid IL or missing references)
		if (source == null)
		{
			return RexQuestPreviewEmissionResult.Failed("B19A preview failed: source character missing.");
		}
		if (((IDynel)source).Controller == null || ((IDynel)source).Controller.Client == null)
		{
			return RexQuestPreviewEmissionResult.Failed("B19A preview failed: source client missing.");
		}
		Identity identity = ((IEntity)source).Identity;
		if ((int)((Identity)(ref identity)).Type == 50000)
		{
			identity = ((IEntity)source).Identity;
			if (((Identity)(ref identity)).Instance != 0)
			{
				try
				{
					((IDynel)source).Controller.Client.SendCompressed((MessageBody)(object)CreateB19APreviewMessage(((IEntity)source).Identity));
					return RexQuestPreviewEmissionResult.Sent("B19A QuestFullUpdate preview sent. mission=Mission:5514B19A title=\"Return to Marcus Stone\"");
				}
				catch (Exception ex)
				{
					LogUtil.Debug((DebugInfoDetail)512, "Arete Marcus B19A preview failed: " + ex.Message);
					return RexQuestPreviewEmissionResult.Failed("B19A preview failed during DTO serialization/send: " + ex.Message);
				}
			}
		}
		return RexQuestPreviewEmissionResult.Failed("B19A preview failed: source identity is invalid.");
	}

	public static RexQuestPreviewEmissionResult TrySendB19AQuestDeleteOnly(ICharacter source)
	{
		//IL_0049: Unknown result type (might be due to invalid IL or missing references)
		//IL_004e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0052: Unknown result type (might be due to invalid IL or missing references)
		//IL_005c: Invalid comparison between Unknown and I4
		//IL_005f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0064: Unknown result type (might be due to invalid IL or missing references)
		//IL_0093: Unknown result type (might be due to invalid IL or missing references)
		if (source == null)
		{
			return RexQuestPreviewEmissionResult.Failed("B19A delete skipped: source character missing.");
		}
		if (((IDynel)source).Controller == null || ((IDynel)source).Controller.Client == null)
		{
			return RexQuestPreviewEmissionResult.Failed("B19A delete skipped: source client missing.");
		}
		Identity identity = ((IEntity)source).Identity;
		if ((int)((Identity)(ref identity)).Type == 50000)
		{
			identity = ((IEntity)source).Identity;
			if (((Identity)(ref identity)).Instance != 0)
			{
				try
				{
					((IDynel)source).Controller.Client.SendCompressed((MessageBody)(object)CreateB19AQuestDeleteMessage(((IEntity)source).Identity));
					return RexQuestPreviewEmissionResult.Sent("B19A Quest Delete only. mission=Mission:5514B19A");
				}
				catch (Exception ex)
				{
					LogUtil.Debug((DebugInfoDetail)512, "Arete Marcus B19A delete failed: " + ex.Message);
					return RexQuestPreviewEmissionResult.Failed("B19A delete failed during DTO serialization/send: " + ex.Message);
				}
			}
		}
		return RexQuestPreviewEmissionResult.Failed("B19A delete skipped: source identity is invalid.");
	}

	public static RexQuestPreviewEmissionResult TrySendFlintToFindBioHandoff(ICharacter source)
	{
		//IL_0049: Unknown result type (might be due to invalid IL or missing references)
		//IL_004e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0052: Unknown result type (might be due to invalid IL or missing references)
		//IL_005c: Invalid comparison between Unknown and I4
		//IL_005f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0064: Unknown result type (might be due to invalid IL or missing references)
		//IL_0096: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b2: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ce: Unknown result type (might be due to invalid IL or missing references)
		if (source == null)
		{
			return RexQuestPreviewEmissionResult.Failed("Flint→FindBio handoff skipped: source missing.");
		}
		if (((IDynel)source).Controller == null || ((IDynel)source).Controller.Client == null)
		{
			return RexQuestPreviewEmissionResult.Failed("Flint→FindBio handoff skipped: client missing.");
		}
		Identity identity = ((IEntity)source).Identity;
		if ((int)((Identity)(ref identity)).Type == 50000)
		{
			identity = ((IEntity)source).Identity;
			if (((Identity)(ref identity)).Instance != 0)
			{
				try
				{
					((IDynel)source).Controller.Client.SendCompressed((MessageBody)(object)CreateFlintAction59Message(((IEntity)source).Identity));
					((IDynel)source).Controller.Client.SendCompressed((MessageBody)(object)CreateFlintQuestDeleteMessage(((IEntity)source).Identity));
					((IDynel)source).Controller.Client.SendCompressed((MessageBody)(object)CreateFindBioPreviewMessage(((IEntity)source).Identity));
					return RexQuestPreviewEmissionResult.Sent("Flint→FindBio Action59+Delete + Find a Bio Analyzing Computer. mission=Mission:5514B19B source=20260720-072904/mission-flow.log:2-3");
				}
				catch (Exception ex)
				{
					LogUtil.Debug((DebugInfoDetail)512, "Arete Flint→FindBio handoff failed: " + ex.Message);
					return RexQuestPreviewEmissionResult.Failed("Flint→FindBio handoff failed: " + ex.Message);
				}
			}
		}
		return RexQuestPreviewEmissionResult.Failed("Flint→FindBio handoff skipped: identity invalid.");
	}

	public static RexQuestPreviewEmissionResult TrySendFindBioPreview(ICharacter source)
	{
		//IL_003d: Unknown result type (might be due to invalid IL or missing references)
		if (source == null || ((IDynel)source).Controller == null || ((IDynel)source).Controller.Client == null)
		{
			return RexQuestPreviewEmissionResult.Failed("FindBio preview skipped: source/client missing.");
		}
		try
		{
			((IDynel)source).Controller.Client.SendCompressed((MessageBody)(object)CreateFindBioPreviewMessage(((IEntity)source).Identity));
			return RexQuestPreviewEmissionResult.Sent("FindBio QuestFullUpdate preview sent. mission=Mission:5514B19B");
		}
		catch (Exception ex)
		{
			return RexQuestPreviewEmissionResult.Failed("FindBio preview failed: " + ex.Message);
		}
	}

	public static RexQuestPreviewEmissionResult TrySendFindBioToDeliverHandoff(ICharacter source)
	{
		//IL_0049: Unknown result type (might be due to invalid IL or missing references)
		//IL_004e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0052: Unknown result type (might be due to invalid IL or missing references)
		//IL_005c: Invalid comparison between Unknown and I4
		//IL_005f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0064: Unknown result type (might be due to invalid IL or missing references)
		//IL_0096: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b2: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ce: Unknown result type (might be due to invalid IL or missing references)
		if (source == null)
		{
			return RexQuestPreviewEmissionResult.Failed("FindBio→Deliver handoff skipped: source missing.");
		}
		if (((IDynel)source).Controller == null || ((IDynel)source).Controller.Client == null)
		{
			return RexQuestPreviewEmissionResult.Failed("FindBio→Deliver handoff skipped: client missing.");
		}
		Identity identity = ((IEntity)source).Identity;
		if ((int)((Identity)(ref identity)).Type == 50000)
		{
			identity = ((IEntity)source).Identity;
			if (((Identity)(ref identity)).Instance != 0)
			{
				try
				{
					((IDynel)source).Controller.Client.SendCompressed((MessageBody)(object)CreateFindBioAction59Message(((IEntity)source).Identity));
					((IDynel)source).Controller.Client.SendCompressed((MessageBody)(object)CreateFindBioQuestDeleteMessage(((IEntity)source).Identity));
					((IDynel)source).Controller.Client.SendCompressed((MessageBody)(object)CreateDeliverBioPreviewMessage(((IEntity)source).Identity));
					return RexQuestPreviewEmissionResult.Sent("FindBio→Deliver Action59+Delete + Deliver tip. mission=Mission:5514B19C source=20260720-072904/mission-flow.log:4-5");
				}
				catch (Exception ex)
				{
					LogUtil.Debug((DebugInfoDetail)512, "Arete FindBio→Deliver handoff failed: " + ex.Message);
					return RexQuestPreviewEmissionResult.Failed("FindBio→Deliver handoff failed: " + ex.Message);
				}
			}
		}
		return RexQuestPreviewEmissionResult.Failed("FindBio→Deliver handoff skipped: identity invalid.");
	}

	public static RexQuestPreviewEmissionResult TrySendDeliverBioPreview(ICharacter source)
	{
		//IL_003d: Unknown result type (might be due to invalid IL or missing references)
		if (source == null || ((IDynel)source).Controller == null || ((IDynel)source).Controller.Client == null)
		{
			return RexQuestPreviewEmissionResult.Failed("DeliverBio preview skipped: source/client missing.");
		}
		try
		{
			((IDynel)source).Controller.Client.SendCompressed((MessageBody)(object)CreateDeliverBioPreviewMessage(((IEntity)source).Identity));
			return RexQuestPreviewEmissionResult.Sent("DeliverBio QuestFullUpdate preview sent. mission=Mission:5514B19C");
		}
		catch (Exception ex)
		{
			return RexQuestPreviewEmissionResult.Failed("DeliverBio preview failed: " + ex.Message);
		}
	}

	public static RexQuestPreviewEmissionResult TrySendDeliverBioToSurveillanceUplinkHandoff(ICharacter source)
	{
		//IL_0049: Unknown result type (might be due to invalid IL or missing references)
		//IL_004e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0052: Unknown result type (might be due to invalid IL or missing references)
		//IL_005c: Invalid comparison between Unknown and I4
		//IL_005f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0064: Unknown result type (might be due to invalid IL or missing references)
		//IL_0096: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b2: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ce: Unknown result type (might be due to invalid IL or missing references)
		if (source == null)
		{
			return RexQuestPreviewEmissionResult.Failed("Deliver→Uplink handoff skipped: source missing.");
		}
		if (((IDynel)source).Controller == null || ((IDynel)source).Controller.Client == null)
		{
			return RexQuestPreviewEmissionResult.Failed("Deliver→Uplink handoff skipped: client missing.");
		}
		Identity identity = ((IEntity)source).Identity;
		if ((int)((Identity)(ref identity)).Type == 50000)
		{
			identity = ((IEntity)source).Identity;
			if (((Identity)(ref identity)).Instance != 0)
			{
				try
				{
					((IDynel)source).Controller.Client.SendCompressed((MessageBody)(object)CreateDeliverBioAction59Message(((IEntity)source).Identity));
					((IDynel)source).Controller.Client.SendCompressed((MessageBody)(object)CreateDeliverBioQuestDeleteMessage(((IEntity)source).Identity));
					((IDynel)source).Controller.Client.SendCompressed((MessageBody)(object)CreateSurveillanceUplinkPreviewMessage(((IEntity)source).Identity));
					return RexQuestPreviewEmissionResult.Sent("Deliver→SurveillanceUplink Action59+Delete + tip. mission=Mission:5514B19D source=20260720-074847/mission-flow.log");
				}
				catch (Exception ex)
				{
					LogUtil.Debug((DebugInfoDetail)512, "Arete Deliver→Uplink handoff failed: " + ex.Message);
					return RexQuestPreviewEmissionResult.Failed("Deliver→Uplink handoff failed: " + ex.Message);
				}
			}
		}
		return RexQuestPreviewEmissionResult.Failed("Deliver→Uplink handoff skipped: identity invalid.");
	}

	public static RexQuestPreviewEmissionResult TrySendSurveillanceUplinkPreview(ICharacter source)
	{
		//IL_003d: Unknown result type (might be due to invalid IL or missing references)
		if (source == null || ((IDynel)source).Controller == null || ((IDynel)source).Controller.Client == null)
		{
			return RexQuestPreviewEmissionResult.Failed("Uplink preview skipped: source/client missing.");
		}
		try
		{
			((IDynel)source).Controller.Client.SendCompressed((MessageBody)(object)CreateSurveillanceUplinkPreviewMessage(((IEntity)source).Identity));
			return RexQuestPreviewEmissionResult.Sent("SurveillanceUplink QuestFullUpdate preview sent. mission=Mission:5514B19D");
		}
		catch (Exception ex)
		{
			return RexQuestPreviewEmissionResult.Failed("Uplink preview failed: " + ex.Message);
		}
	}

	public static RexQuestPreviewEmissionResult TrySendUplinkToPlantBugHandoff(ICharacter source)
	{
		//IL_0054: Unknown result type (might be due to invalid IL or missing references)
		object obj;
		if (source == null)
		{
			obj = null;
		}
		else
		{
			IController controller = ((IDynel)source).Controller;
			obj = ((controller != null) ? controller.Client : null);
		}
		if (obj == null)
		{
			return RexQuestPreviewEmissionResult.Failed("Uplink→PlantBug handoff skipped: client missing.");
		}
		try
		{
			FlintKneecappingTipWire.TryDeleteTip(source, 1431980617);
			FlintKneecappingTipWire.TryDeleteTip(source, 1427419549);
			((IDynel)source).Controller.Client.SendCompressed((MessageBody)(object)CreatePlantBugPreviewMessage(((IEntity)source).Identity));
			return RexQuestPreviewEmissionResult.Sent("Uplink→PlantBug Action59+Delete + tip. mission=Mission:555A4E3B source=20260720-105157");
		}
		catch (Exception ex)
		{
			return RexQuestPreviewEmissionResult.Failed("Uplink→PlantBug handoff failed: " + ex.Message);
		}
	}

	public static RexQuestPreviewEmissionResult TrySendPlantBugToDeliverBillHandoff(ICharacter source)
	{
		//IL_006c: Unknown result type (might be due to invalid IL or missing references)
		object obj;
		if (source == null)
		{
			obj = null;
		}
		else
		{
			IController controller = ((IDynel)source).Controller;
			obj = ((controller != null) ? controller.Client : null);
		}
		if (obj == null)
		{
			return RexQuestPreviewEmissionResult.Failed("PlantBug→DeliverBill handoff skipped: client missing.");
		}
		try
		{
			FlintKneecappingTipWire.TryDeleteTip(source, 1431980617);
			FlintKneecappingTipWire.TryDeleteTip(source, 1427419549);
			FlintKneecappingTipWire.TryDeleteTip(source, 1431981627);
			FlintKneecappingTipWire.TryDeleteTip(source, 1427419550);
			((IDynel)source).Controller.Client.SendCompressed((MessageBody)(object)CreateDeliverHc12BillPreviewMessage(((IEntity)source).Identity));
			return RexQuestPreviewEmissionResult.Sent("PlantBug→DeliverBill Action59+Delete + tip. mission=Mission:555A4E3C source=20260720-105157");
		}
		catch (Exception ex)
		{
			return RexQuestPreviewEmissionResult.Failed("PlantBug→DeliverBill handoff failed: " + ex.Message);
		}
	}

	public static RexQuestPreviewEmissionResult TrySendDeliverBillToKneecappingHandoff(ICharacter source)
	{
		//IL_006e: Unknown result type (might be due to invalid IL or missing references)
		object obj;
		if (source == null)
		{
			obj = null;
		}
		else
		{
			IController controller = ((IDynel)source).Controller;
			obj = ((controller != null) ? controller.Client : null);
		}
		if (obj == null)
		{
			return RexQuestPreviewEmissionResult.Failed("DeliverBill→Kneecapping handoff skipped: client missing.");
		}
		try
		{
			int[] array = new int[8] { 1431980617, 1427419549, 1431981627, 1427419550, 1431981628, 1427419551, 1431981629, 1427419552 };
			for (int i = 0; i < array.Length; i++)
			{
				FlintKneecappingTipWire.TryDeleteTip(source, array[i]);
			}
			((IDynel)source).Controller.Client.SendCompressed((MessageBody)(object)CreateKneecappingPreviewMessage(((IEntity)source).Identity));
			return RexQuestPreviewEmissionResult.Sent("DeliverBill→Kneecapping Action59+Delete + tip. mission=Mission:555A4E3D source=20260720-105157");
		}
		catch (Exception ex)
		{
			return RexQuestPreviewEmissionResult.Failed("DeliverBill→Kneecapping handoff failed: " + ex.Message);
		}
	}

	public static RexQuestPreviewEmissionResult TrySendBillTurnInClearTips(ICharacter source)
	{
		return TrySendDeliverBillToKneecappingHandoff(source);
	}

	public static RexQuestPreviewEmissionResult TrySendKneecappingTip(ICharacter source)
	{
		return TrySendDeliverBillToKneecappingHandoff(source);
	}

	public static RexQuestPreviewEmissionResult TrySendKneecappingToReportAlexHandoff(ICharacter source)
	{
		//IL_008d: Unknown result type (might be due to invalid IL or missing references)
		object obj;
		if (source == null)
		{
			obj = null;
		}
		else
		{
			IController controller = ((IDynel)source).Controller;
			obj = ((controller != null) ? controller.Client : null);
		}
		if (obj == null)
		{
			return RexQuestPreviewEmissionResult.Failed("Kneecapping→ReportAlex handoff skipped: client missing.");
		}
		try
		{
			ZoneClient zoneClient = ((IDynel)source).Controller.Client as ZoneClient;
			Character val = (Character)(object)((source is Character) ? source : null);
			if (zoneClient != null && val != null)
			{
				FlintKneecappingTipWire.ClearChainTips(zoneClient, val);
			}
			else
			{
				SendTipAction59AndDelete(source, 1431981629);
				SendTipAction59AndDelete(source, 1427419552);
			}
			((IDynel)source).Controller.Client.SendCompressed((MessageBody)(object)CreateReportToAlexPreviewMessage(((IEntity)source).Identity));
			return RexQuestPreviewEmissionResult.Sent("Kneecapping→ReportAlex tip. mission=Mission:555B4365 source=20260720-171317");
		}
		catch (Exception ex)
		{
			return RexQuestPreviewEmissionResult.Failed("Kneecapping→ReportAlex handoff failed: " + ex.Message);
		}
	}

	public static RexQuestPreviewEmissionResult TrySendReportAlexToTalkStanHandoff(ICharacter source)
	{
		//IL_0081: Unknown result type (might be due to invalid IL or missing references)
		object obj;
		if (source == null)
		{
			obj = null;
		}
		else
		{
			IController controller = ((IDynel)source).Controller;
			obj = ((controller != null) ? controller.Client : null);
		}
		if (obj == null)
		{
			return RexQuestPreviewEmissionResult.Failed("ReportAlex→TalkStan handoff skipped: client missing.");
		}
		try
		{
			ZoneClient zoneClient = ((IDynel)source).Controller.Client as ZoneClient;
			Character val = (Character)(object)((source is Character) ? source : null);
			if (zoneClient != null && val != null)
			{
				FlintKneecappingTipWire.ClearChainTips(zoneClient, val);
			}
			else
			{
				SendTipAction59AndDelete(source, 1432044389);
			}
			((IDynel)source).Controller.Client.SendCompressed((MessageBody)(object)CreateTalkToStanPreviewMessage(((IEntity)source).Identity));
			return RexQuestPreviewEmissionResult.Sent("ReportAlex→TalkStan tip. mission=Mission:555B4366 source=20260720-171317");
		}
		catch (Exception ex)
		{
			return RexQuestPreviewEmissionResult.Failed("ReportAlex→TalkStan handoff failed: " + ex.Message);
		}
	}

	public static RexQuestPreviewEmissionResult TrySendTradeskillNanoSensorTip(ICharacter source)
	{
		//IL_003c: Unknown result type (might be due to invalid IL or missing references)
		object obj;
		if (source == null)
		{
			obj = null;
		}
		else
		{
			IController controller = ((IDynel)source).Controller;
			obj = ((controller != null) ? controller.Client : null);
		}
		if (obj == null)
		{
			return RexQuestPreviewEmissionResult.Failed("Tradeskill tip skipped: client missing.");
		}
		try
		{
			((IDynel)source).Controller.Client.SendCompressed((MessageBody)(object)CreateTradeskillNanoSensorPreviewMessage(((IEntity)source).Identity));
			return RexQuestPreviewEmissionResult.Sent("Tradeskill Nano Sensor tip. mission=Mission:555B4367 source=20260720-171317");
		}
		catch (Exception ex)
		{
			return RexQuestPreviewEmissionResult.Failed("Tradeskill tip failed: " + ex.Message);
		}
	}

	private static void SendTipAction59AndDelete(ICharacter source, int missionInstance)
	{
		//IL_0035: Unknown result type (might be due to invalid IL or missing references)
		object obj;
		if (source == null)
		{
			obj = null;
		}
		else
		{
			IController controller = ((IDynel)source).Controller;
			obj = ((controller != null) ? controller.Client : null);
		}
		if (obj != null && missionInstance != 0)
		{
			((IDynel)source).Controller.Client.SendCompressed((MessageBody)(object)CreateTipQuestDeleteMessage(((IEntity)source).Identity, missionInstance));
		}
	}

	private static CharacterActionMessage CreateTipAction59Message(Identity characterIdentity, int missionInstance)
	{
		//IL_0001: Unknown result type (might be due to invalid IL or missing references)
		//IL_0006: Unknown result type (might be due to invalid IL or missing references)
		//IL_0007: Unknown result type (might be due to invalid IL or missing references)
		//IL_000e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0016: Unknown result type (might be due to invalid IL or missing references)
		//IL_001f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0027: Unknown result type (might be due to invalid IL or missing references)
		//IL_002e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0039: Unknown result type (might be due to invalid IL or missing references)
		//IL_0045: Unknown result type (might be due to invalid IL or missing references)
		//IL_004d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0056: Expected O, but got Unknown
		return new CharacterActionMessage
		{
			Identity = characterIdentity,
			Unknown = 0,
			Action = (CharacterActionType)59,
			Unknown1 = 0,
			Target = IdentityFromRaw(56003, missionInstance),
			Parameter1 = 56003,
			Parameter2 = missionInstance,
			Unknown2 = 0
		};
	}

	private static QuestMessage CreateTipQuestDeleteMessage(Identity characterIdentity, int missionInstance)
	{
		//IL_0001: Unknown result type (might be due to invalid IL or missing references)
		//IL_0006: Unknown result type (might be due to invalid IL or missing references)
		//IL_0007: Unknown result type (might be due to invalid IL or missing references)
		//IL_000e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0016: Unknown result type (might be due to invalid IL or missing references)
		//IL_001e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0026: Unknown result type (might be due to invalid IL or missing references)
		//IL_002d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0038: Unknown result type (might be due to invalid IL or missing references)
		//IL_0040: Unknown result type (might be due to invalid IL or missing references)
		//IL_0049: Expected O, but got Unknown
		return new QuestMessage
		{
			Identity = characterIdentity,
			Unknown = 0,
			Action = (QuestAction)1,
			Unknown1 = 0,
			Mission = IdentityFromRaw(56003, missionInstance),
			Unknown2 = 0,
			Unknown3 = 0
		};
	}

	internal static QuestFullUpdateMessage CreateB18CPreviewMessage(Identity characterIdentity)
	{
		//IL_000b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0010: Unknown result type (might be due to invalid IL or missing references)
		//IL_0013: Unknown result type (might be due to invalid IL or missing references)
		//IL_0033: Unknown result type (might be due to invalid IL or missing references)
		//IL_0034: Unknown result type (might be due to invalid IL or missing references)
		//IL_0035: Unknown result type (might be due to invalid IL or missing references)
		//IL_003b: Expected O, but got Unknown
		//IL_003c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0054: Unknown result type (might be due to invalid IL or missing references)
		//IL_005b: Expected O, but got Unknown
		//IL_005d: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a5: Unknown result type (might be due to invalid IL or missing references)
		//IL_0153: Unknown result type (might be due to invalid IL or missing references)
		//IL_0183: Unknown result type (might be due to invalid IL or missing references)
		//IL_0188: Unknown result type (might be due to invalid IL or missing references)
		//IL_0191: Unknown result type (might be due to invalid IL or missing references)
		//IL_0192: Unknown result type (might be due to invalid IL or missing references)
		//IL_019d: Unknown result type (might be due to invalid IL or missing references)
		//IL_019e: Unknown result type (might be due to invalid IL or missing references)
		//IL_01a9: Unknown result type (might be due to invalid IL or missing references)
		//IL_01aa: Unknown result type (might be due to invalid IL or missing references)
		//IL_01b5: Unknown result type (might be due to invalid IL or missing references)
		//IL_01c0: Unknown result type (might be due to invalid IL or missing references)
		//IL_01cb: Unknown result type (might be due to invalid IL or missing references)
		//IL_01cc: Unknown result type (might be due to invalid IL or missing references)
		//IL_01d7: Unknown result type (might be due to invalid IL or missing references)
		//IL_01e3: Unknown result type (might be due to invalid IL or missing references)
		//IL_01ef: Unknown result type (might be due to invalid IL or missing references)
		//IL_01fb: Unknown result type (might be due to invalid IL or missing references)
		//IL_0207: Unknown result type (might be due to invalid IL or missing references)
		//IL_0208: Unknown result type (might be due to invalid IL or missing references)
		//IL_0213: Unknown result type (might be due to invalid IL or missing references)
		//IL_021f: Unknown result type (might be due to invalid IL or missing references)
		//IL_022b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0237: Unknown result type (might be due to invalid IL or missing references)
		//IL_0243: Unknown result type (might be due to invalid IL or missing references)
		//IL_0244: Unknown result type (might be due to invalid IL or missing references)
		//IL_024f: Unknown result type (might be due to invalid IL or missing references)
		//IL_025b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0263: Unknown result type (might be due to invalid IL or missing references)
		//IL_026e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0279: Unknown result type (might be due to invalid IL or missing references)
		//IL_027c: Unknown result type (might be due to invalid IL or missing references)
		//IL_029c: Unknown result type (might be due to invalid IL or missing references)
		//IL_02a3: Unknown result type (might be due to invalid IL or missing references)
		//IL_02af: Unknown result type (might be due to invalid IL or missing references)
		//IL_02bb: Unknown result type (might be due to invalid IL or missing references)
		//IL_02cb: Unknown result type (might be due to invalid IL or missing references)
		//IL_02d5: Expected O, but got Unknown
		//IL_02d7: Expected O, but got Unknown
		//IL_02e7: Unknown result type (might be due to invalid IL or missing references)
		//IL_02e8: Unknown result type (might be due to invalid IL or missing references)
		//IL_0338: Unknown result type (might be due to invalid IL or missing references)
		//IL_0339: Unknown result type (might be due to invalid IL or missing references)
		//IL_035c: Unknown result type (might be due to invalid IL or missing references)
		Identity questId = IdentityFromRaw(56003, 1427419532);
		Identity val = default(Identity);
		((Identity)(ref val)).Type = (IdentityType)50000;
		((Identity)(ref val)).Instance = 2016273768;
		Identity unknownId = val;
		QuestFullUpdateMessage val2 = new QuestFullUpdateMessage();
		((N3Message)val2).Identity = characterIdentity;
		((N3Message)val2).Unknown = 1;
		Quest[] array = new Quest[1];
		Quest val3 = new Quest();
		val3.QuestId = questId;
		val3.Unknown1 = 15;
		val3.Unknown2 = 0;
		val3.Unknown3 = 0;
		val3.Unknown4 = 2;
		val3.ShortInfo = "Terminate 5 Malfunctioning C...";
		val3.LongInfo = "Terminate 5 Malfunctioning Cleaning Robots<BR><br><font color=\"#63ad63\">Identity Crisis:</font><BR>In order to leave Arete Landing and become a citizen of Rubi-Ka, you need an identity. Your mission is to create a fake ID Card to you can leave this place..<br><BR>Rex Larsson considers himself too lazy to clean up his cleaning business. Since you need his help, he wanted a favor in return. You have to terminate 5 of his Malfunctioning Cleaning Robots then open the package with brand new cleaning robots and set them to work.<BR><BR><font color=\"#FF0000\">Mission Objective:<BR>Kill 5 Malfunctining Cleaning Robots.</font>";
		val3.UnknownId1 = unknownId;
		val3.Unknown5 = 6;
		val3.Unknown6 = 0;
		val3.Unknown7 = 0;
		val3.Unknown8 = 0;
		val3.Unknown9 = 1009;
		val3.Unknown10 = 1009;
		val3.MissionItemData = (MissionItemReward[])(object)new MissionItemReward[0];
		val3.Unknown11 = 1112496696;
		val3.Unknown12 = 0;
		val3.Unknown13 = 0;
		val3.UnknownHash1 = string.Empty;
		val3.Unknown14 = 0;
		val3.Unknown15 = 0;
		val3.Unknown16 = 0;
		val3.Unknown17 = 0;
		val3.Unknown18 = 0;
		val3.UnknownId2 = characterIdentity;
		val3.MissionIconId = 11330;
		val3.Unknown20 = 0;
		val3.Unknown21 = 0;
		QuestActionInfo[] array2 = new QuestActionInfo[1];
		QuestActionInfo val4 = new QuestActionInfo
		{
			Version = 20,
			Action = Identity.None,
			UnknownId1 = Identity.None,
			UnknownId2 = Identity.None,
			UnknownId3 = IdentityFromRaw(6553, 1296843589),
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
			UnknownHash1 = string.Empty,
			Unknown9 = 0,
			UnknownId7 = IdentityFromRaw(54012, 475060430)
		};
		val = default(Identity);
		((Identity)(ref val)).Type = (IdentityType)40016;
		((Identity)(ref val)).Instance = 6553;
		val4.PlayfieldId = val;
		val4.Unknown10 = 100000;
		val4.Unknown11 = 100000;
		val4.Position = new Vector3(3614f, 0f, 779f);
		array2[0] = val4;
		val3.QuestActions = (QuestActionInfo[])(object)array2;
		val3.PlayerIds = (Identity[])(object)new Identity[1] { characterIdentity };
		val3.UnknownArray1 = new int[1] { 72407246 };
		val3.UnknownArray2 = new int[0];
		val3.CharacterInfos = (CharacterInfo[])(object)new CharacterInfo[0];
		val3.Unknown22 = 6;
		val3.PlayerIds2 = (Identity[])(object)new Identity[1] { characterIdentity };
		val3.Unknown23 = 5;
		val3.Unknown24 = 105102;
		val3.UnknownId3 = Identity.None;
		val3.Unknown25 = 0;
		val3.Unknown26 = 0;
		val3.QuestIdentities = (QuestIdentity[])(object)new QuestIdentity[0];
		val3.Unknown27 = 7;
		val3.FactionInfos = (Identity[])(object)new Identity[0];
		val3.Unknown28 = 1;
		array[0] = val3;
		val2.Quests = (Quest[])(object)array;
		return val2;
	}

	internal static QuestFullUpdateMessage CreateB18DPreviewMessage(Identity characterIdentity)
	{
		//IL_000b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0010: Unknown result type (might be due to invalid IL or missing references)
		//IL_0013: Unknown result type (might be due to invalid IL or missing references)
		//IL_0033: Unknown result type (might be due to invalid IL or missing references)
		//IL_0034: Unknown result type (might be due to invalid IL or missing references)
		//IL_0035: Unknown result type (might be due to invalid IL or missing references)
		//IL_003b: Expected O, but got Unknown
		//IL_003c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0054: Unknown result type (might be due to invalid IL or missing references)
		//IL_005b: Expected O, but got Unknown
		//IL_005d: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a5: Unknown result type (might be due to invalid IL or missing references)
		//IL_0153: Unknown result type (might be due to invalid IL or missing references)
		//IL_0183: Unknown result type (might be due to invalid IL or missing references)
		//IL_0188: Unknown result type (might be due to invalid IL or missing references)
		//IL_0191: Unknown result type (might be due to invalid IL or missing references)
		//IL_0192: Unknown result type (might be due to invalid IL or missing references)
		//IL_019d: Unknown result type (might be due to invalid IL or missing references)
		//IL_019e: Unknown result type (might be due to invalid IL or missing references)
		//IL_01a9: Unknown result type (might be due to invalid IL or missing references)
		//IL_01b4: Unknown result type (might be due to invalid IL or missing references)
		//IL_01bf: Unknown result type (might be due to invalid IL or missing references)
		//IL_01c0: Unknown result type (might be due to invalid IL or missing references)
		//IL_01cb: Unknown result type (might be due to invalid IL or missing references)
		//IL_01cc: Unknown result type (might be due to invalid IL or missing references)
		//IL_01d7: Unknown result type (might be due to invalid IL or missing references)
		//IL_01e3: Unknown result type (might be due to invalid IL or missing references)
		//IL_01ef: Unknown result type (might be due to invalid IL or missing references)
		//IL_01fb: Unknown result type (might be due to invalid IL or missing references)
		//IL_0207: Unknown result type (might be due to invalid IL or missing references)
		//IL_0208: Unknown result type (might be due to invalid IL or missing references)
		//IL_0213: Unknown result type (might be due to invalid IL or missing references)
		//IL_021f: Unknown result type (might be due to invalid IL or missing references)
		//IL_022b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0237: Unknown result type (might be due to invalid IL or missing references)
		//IL_0243: Unknown result type (might be due to invalid IL or missing references)
		//IL_0244: Unknown result type (might be due to invalid IL or missing references)
		//IL_024f: Unknown result type (might be due to invalid IL or missing references)
		//IL_025b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0263: Unknown result type (might be due to invalid IL or missing references)
		//IL_026e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0279: Unknown result type (might be due to invalid IL or missing references)
		//IL_027c: Unknown result type (might be due to invalid IL or missing references)
		//IL_029c: Unknown result type (might be due to invalid IL or missing references)
		//IL_02a3: Unknown result type (might be due to invalid IL or missing references)
		//IL_02af: Unknown result type (might be due to invalid IL or missing references)
		//IL_02bb: Unknown result type (might be due to invalid IL or missing references)
		//IL_02cb: Unknown result type (might be due to invalid IL or missing references)
		//IL_02d5: Expected O, but got Unknown
		//IL_02d7: Expected O, but got Unknown
		//IL_02e7: Unknown result type (might be due to invalid IL or missing references)
		//IL_02e8: Unknown result type (might be due to invalid IL or missing references)
		//IL_0338: Unknown result type (might be due to invalid IL or missing references)
		//IL_0339: Unknown result type (might be due to invalid IL or missing references)
		//IL_035c: Unknown result type (might be due to invalid IL or missing references)
		Identity questId = IdentityFromRaw(56003, 1427419533);
		Identity val = default(Identity);
		((Identity)(ref val)).Type = (IdentityType)50000;
		((Identity)(ref val)).Instance = 2016273768;
		Identity unknownId = val;
		QuestFullUpdateMessage val2 = new QuestFullUpdateMessage();
		((N3Message)val2).Identity = characterIdentity;
		((N3Message)val2).Unknown = 1;
		Quest[] array = new Quest[1];
		Quest val3 = new Quest();
		val3.QuestId = questId;
		val3.Unknown1 = 15;
		val3.Unknown2 = 0;
		val3.Unknown3 = 0;
		val3.Unknown4 = 2;
		val3.ShortInfo = "Open the Cargo Box";
		val3.LongInfo = "Open the Cargo Box<BR><BR>Rex Larsson considers himself too lazy to clean up his cleaning business. Since you need his help, he wanted a favor in return. You have to terminate 5 of his Malfunctioning Cleaning Robots then open the Cargo Box with brand new cleaning robots and set them to work.<BR><BR><font color=\"#FF0000\">Mission Objective:<BR>Use (Right Click) the Cargo Box to open it.</font>";
		val3.UnknownId1 = unknownId;
		val3.Unknown5 = 6;
		val3.Unknown6 = 0;
		val3.Unknown7 = 0;
		val3.Unknown8 = 0;
		val3.Unknown9 = 1009;
		val3.Unknown10 = 1009;
		val3.MissionItemData = (MissionItemReward[])(object)new MissionItemReward[0];
		val3.Unknown11 = 1145587534;
		val3.Unknown12 = 0;
		val3.Unknown13 = 0;
		val3.UnknownHash1 = string.Empty;
		val3.Unknown14 = 0;
		val3.Unknown15 = 0;
		val3.Unknown16 = 0;
		val3.Unknown17 = 0;
		val3.Unknown18 = 0;
		val3.UnknownId2 = characterIdentity;
		val3.MissionIconId = 244818;
		val3.Unknown20 = 0;
		val3.Unknown21 = 0;
		QuestActionInfo[] array2 = new QuestActionInfo[1];
		QuestActionInfo val4 = new QuestActionInfo
		{
			Version = 24,
			Action = Identity.None,
			UnknownId1 = Identity.None,
			UnknownId2 = IdentityFromRaw(70099, 105103),
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
			UnknownHash1 = string.Empty,
			Unknown9 = 0,
			UnknownId7 = IdentityFromRaw(54001, 1293319993)
		};
		val = default(Identity);
		((Identity)(ref val)).Type = (IdentityType)40016;
		((Identity)(ref val)).Instance = 6553;
		val4.PlayfieldId = val;
		val4.Unknown10 = 100000;
		val4.Unknown11 = 100000;
		val4.Position = new Vector3(3621f, 0f, 782f);
		array2[0] = val4;
		val3.QuestActions = (QuestActionInfo[])(object)array2;
		val3.PlayerIds = (Identity[])(object)new Identity[1] { characterIdentity };
		val3.UnknownArray1 = new int[1] { 85360441 };
		val3.UnknownArray2 = new int[0];
		val3.CharacterInfos = (CharacterInfo[])(object)new CharacterInfo[0];
		val3.Unknown22 = 6;
		val3.PlayerIds2 = (Identity[])(object)new Identity[1] { characterIdentity };
		val3.Unknown23 = 0;
		val3.Unknown24 = 105103;
		val3.UnknownId3 = Identity.None;
		val3.Unknown25 = 0;
		val3.Unknown26 = 0;
		val3.QuestIdentities = (QuestIdentity[])(object)new QuestIdentity[0];
		val3.Unknown27 = 7;
		val3.FactionInfos = (Identity[])(object)new Identity[0];
		val3.Unknown28 = 1;
		array[0] = val3;
		val2.Quests = (Quest[])(object)array;
		return val2;
	}

	internal static QuestFullUpdateMessage CreateB18EPreviewMessage(Identity characterIdentity)
	{
		//IL_000b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0010: Unknown result type (might be due to invalid IL or missing references)
		//IL_0013: Unknown result type (might be due to invalid IL or missing references)
		//IL_0033: Unknown result type (might be due to invalid IL or missing references)
		//IL_0034: Unknown result type (might be due to invalid IL or missing references)
		//IL_0035: Unknown result type (might be due to invalid IL or missing references)
		//IL_003b: Expected O, but got Unknown
		//IL_003c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0054: Unknown result type (might be due to invalid IL or missing references)
		//IL_005b: Expected O, but got Unknown
		//IL_005d: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a5: Unknown result type (might be due to invalid IL or missing references)
		//IL_015b: Unknown result type (might be due to invalid IL or missing references)
		//IL_018b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0190: Unknown result type (might be due to invalid IL or missing references)
		//IL_0199: Unknown result type (might be due to invalid IL or missing references)
		//IL_019a: Unknown result type (might be due to invalid IL or missing references)
		//IL_01a5: Unknown result type (might be due to invalid IL or missing references)
		//IL_01a6: Unknown result type (might be due to invalid IL or missing references)
		//IL_01b1: Unknown result type (might be due to invalid IL or missing references)
		//IL_01bc: Unknown result type (might be due to invalid IL or missing references)
		//IL_01c7: Unknown result type (might be due to invalid IL or missing references)
		//IL_01c8: Unknown result type (might be due to invalid IL or missing references)
		//IL_01d3: Unknown result type (might be due to invalid IL or missing references)
		//IL_01d4: Unknown result type (might be due to invalid IL or missing references)
		//IL_01df: Unknown result type (might be due to invalid IL or missing references)
		//IL_01eb: Unknown result type (might be due to invalid IL or missing references)
		//IL_01f7: Unknown result type (might be due to invalid IL or missing references)
		//IL_0203: Unknown result type (might be due to invalid IL or missing references)
		//IL_020f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0210: Unknown result type (might be due to invalid IL or missing references)
		//IL_021b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0227: Unknown result type (might be due to invalid IL or missing references)
		//IL_0233: Unknown result type (might be due to invalid IL or missing references)
		//IL_023f: Unknown result type (might be due to invalid IL or missing references)
		//IL_024b: Unknown result type (might be due to invalid IL or missing references)
		//IL_024c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0257: Unknown result type (might be due to invalid IL or missing references)
		//IL_0263: Unknown result type (might be due to invalid IL or missing references)
		//IL_026b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0276: Unknown result type (might be due to invalid IL or missing references)
		//IL_0281: Unknown result type (might be due to invalid IL or missing references)
		//IL_0284: Unknown result type (might be due to invalid IL or missing references)
		//IL_02a4: Unknown result type (might be due to invalid IL or missing references)
		//IL_02ab: Unknown result type (might be due to invalid IL or missing references)
		//IL_02b7: Unknown result type (might be due to invalid IL or missing references)
		//IL_02c3: Unknown result type (might be due to invalid IL or missing references)
		//IL_02d3: Unknown result type (might be due to invalid IL or missing references)
		//IL_02dd: Expected O, but got Unknown
		//IL_02df: Expected O, but got Unknown
		//IL_02ef: Unknown result type (might be due to invalid IL or missing references)
		//IL_02f0: Unknown result type (might be due to invalid IL or missing references)
		//IL_0340: Unknown result type (might be due to invalid IL or missing references)
		//IL_0341: Unknown result type (might be due to invalid IL or missing references)
		//IL_0364: Unknown result type (might be due to invalid IL or missing references)
		Identity questId = IdentityFromRaw(56003, 1427419534);
		Identity val = default(Identity);
		((Identity)(ref val)).Type = (IdentityType)50000;
		((Identity)(ref val)).Instance = 2016273768;
		Identity unknownId = val;
		QuestFullUpdateMessage val2 = new QuestFullUpdateMessage();
		((N3Message)val2).Identity = characterIdentity;
		((N3Message)val2).Unknown = 1;
		Quest[] array = new Quest[1];
		Quest val3 = new Quest();
		val3.QuestId = questId;
		val3.Unknown1 = 15;
		val3.Unknown2 = 0;
		val3.Unknown3 = 0;
		val3.Unknown4 = 2;
		val3.ShortInfo = "Return to Rex Larsson";
		val3.LongInfo = "Return to Rex Larsson<BR><BR>Return to Rex Larsson to inform him of the great cleaning success.<BR><BR><font color=\"#FF0000\">Mission Objective:<BR>Talk to Rex Larsson.</font>";
		val3.UnknownId1 = unknownId;
		val3.Unknown5 = 6;
		val3.Unknown6 = 1040;
		val3.Unknown7 = 0;
		val3.Unknown8 = 1281;
		val3.Unknown9 = 1009;
		val3.Unknown10 = 1009;
		val3.MissionItemData = (MissionItemReward[])(object)new MissionItemReward[0];
		val3.Unknown11 = 861490233;
		val3.Unknown12 = 0;
		val3.Unknown13 = 0;
		val3.UnknownHash1 = "5UFZ";
		val3.Unknown14 = 1;
		val3.Unknown15 = 0;
		val3.Unknown16 = 0;
		val3.Unknown17 = 0;
		val3.Unknown18 = 0;
		val3.UnknownId2 = characterIdentity;
		val3.MissionIconId = 244818;
		val3.Unknown20 = 0;
		val3.Unknown21 = 0;
		QuestActionInfo[] array2 = new QuestActionInfo[1];
		QuestActionInfo val4 = new QuestActionInfo
		{
			Version = 23,
			Action = Identity.None,
			UnknownId1 = Identity.None,
			UnknownId2 = IdentityFromRaw(70099, 1380273235),
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
			UnknownHash1 = string.Empty,
			Unknown9 = 0,
			UnknownId7 = IdentityFromRaw(54001, 1293319994)
		};
		val = default(Identity);
		((Identity)(ref val)).Type = (IdentityType)40016;
		((Identity)(ref val)).Instance = 6553;
		val4.PlayfieldId = val;
		val4.Unknown10 = 100000;
		val4.Unknown11 = 100000;
		val4.Position = new Vector3(3621f, 0f, 790f);
		array2[0] = val4;
		val3.QuestActions = (QuestActionInfo[])(object)array2;
		val3.PlayerIds = (Identity[])(object)new Identity[1] { characterIdentity };
		val3.UnknownArray1 = new int[1] { 85360442 };
		val3.UnknownArray2 = new int[0];
		val3.CharacterInfos = (CharacterInfo[])(object)new CharacterInfo[0];
		val3.Unknown22 = 6;
		val3.PlayerIds2 = (Identity[])(object)new Identity[1] { characterIdentity };
		val3.Unknown23 = 0;
		val3.Unknown24 = 105104;
		val3.UnknownId3 = Identity.None;
		val3.Unknown25 = 0;
		val3.Unknown26 = 0;
		val3.QuestIdentities = (QuestIdentity[])(object)new QuestIdentity[0];
		val3.Unknown27 = 7;
		val3.FactionInfos = (Identity[])(object)new Identity[0];
		val3.Unknown28 = 1;
		array[0] = val3;
		val2.Quests = (Quest[])(object)array;
		return val2;
	}

	internal static QuestFullUpdateMessage CreateB18FPreviewMessage(Identity characterIdentity)
	{
		//IL_000b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0010: Unknown result type (might be due to invalid IL or missing references)
		//IL_0013: Unknown result type (might be due to invalid IL or missing references)
		//IL_0033: Unknown result type (might be due to invalid IL or missing references)
		//IL_0034: Unknown result type (might be due to invalid IL or missing references)
		//IL_0035: Unknown result type (might be due to invalid IL or missing references)
		//IL_003b: Expected O, but got Unknown
		//IL_003c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0054: Unknown result type (might be due to invalid IL or missing references)
		//IL_005b: Expected O, but got Unknown
		//IL_005d: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a5: Unknown result type (might be due to invalid IL or missing references)
		//IL_0153: Unknown result type (might be due to invalid IL or missing references)
		//IL_0183: Unknown result type (might be due to invalid IL or missing references)
		//IL_0188: Unknown result type (might be due to invalid IL or missing references)
		//IL_0191: Unknown result type (might be due to invalid IL or missing references)
		//IL_0192: Unknown result type (might be due to invalid IL or missing references)
		//IL_019d: Unknown result type (might be due to invalid IL or missing references)
		//IL_019e: Unknown result type (might be due to invalid IL or missing references)
		//IL_01a9: Unknown result type (might be due to invalid IL or missing references)
		//IL_01b4: Unknown result type (might be due to invalid IL or missing references)
		//IL_01bf: Unknown result type (might be due to invalid IL or missing references)
		//IL_01c0: Unknown result type (might be due to invalid IL or missing references)
		//IL_01cb: Unknown result type (might be due to invalid IL or missing references)
		//IL_01cc: Unknown result type (might be due to invalid IL or missing references)
		//IL_01d7: Unknown result type (might be due to invalid IL or missing references)
		//IL_01e3: Unknown result type (might be due to invalid IL or missing references)
		//IL_01ef: Unknown result type (might be due to invalid IL or missing references)
		//IL_01fb: Unknown result type (might be due to invalid IL or missing references)
		//IL_0207: Unknown result type (might be due to invalid IL or missing references)
		//IL_0208: Unknown result type (might be due to invalid IL or missing references)
		//IL_0213: Unknown result type (might be due to invalid IL or missing references)
		//IL_021f: Unknown result type (might be due to invalid IL or missing references)
		//IL_022b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0237: Unknown result type (might be due to invalid IL or missing references)
		//IL_0243: Unknown result type (might be due to invalid IL or missing references)
		//IL_0244: Unknown result type (might be due to invalid IL or missing references)
		//IL_024f: Unknown result type (might be due to invalid IL or missing references)
		//IL_025b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0263: Unknown result type (might be due to invalid IL or missing references)
		//IL_026e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0279: Unknown result type (might be due to invalid IL or missing references)
		//IL_027c: Unknown result type (might be due to invalid IL or missing references)
		//IL_029c: Unknown result type (might be due to invalid IL or missing references)
		//IL_02a3: Unknown result type (might be due to invalid IL or missing references)
		//IL_02af: Unknown result type (might be due to invalid IL or missing references)
		//IL_02bb: Unknown result type (might be due to invalid IL or missing references)
		//IL_02cb: Unknown result type (might be due to invalid IL or missing references)
		//IL_02d5: Expected O, but got Unknown
		//IL_02d7: Expected O, but got Unknown
		//IL_02e7: Unknown result type (might be due to invalid IL or missing references)
		//IL_02e8: Unknown result type (might be due to invalid IL or missing references)
		//IL_0338: Unknown result type (might be due to invalid IL or missing references)
		//IL_0339: Unknown result type (might be due to invalid IL or missing references)
		//IL_035c: Unknown result type (might be due to invalid IL or missing references)
		Identity questId = IdentityFromRaw(56003, 1427419535);
		Identity val = default(Identity);
		((Identity)(ref val)).Type = (IdentityType)50000;
		((Identity)(ref val)).Instance = 2016273768;
		Identity unknownId = val;
		QuestFullUpdateMessage val2 = new QuestFullUpdateMessage();
		((N3Message)val2).Identity = characterIdentity;
		((N3Message)val2).Unknown = 1;
		Quest[] array = new Quest[1];
		Quest val3 = new Quest();
		val3.QuestId = questId;
		val3.Unknown1 = 15;
		val3.Unknown2 = 0;
		val3.Unknown3 = 0;
		val3.Unknown4 = 2;
		val3.ShortInfo = "Talk to Marcus Stone";
		val3.LongInfo = "Talk to Marcus Stone<BR><BR><font color=\"#63ad63\">Identity Crisis:</font><BR>In order to leave Arete Landing and become a citizen of Rubi-Ka, you need an identity. Your mission is to create a fake ID Card to you can leave this place..<BR><BR>Rex Larsson told you to spreak with Marcus Stone, an overseer for arriving cargo in the area, might be able to aid in getting your license issue settled.<BR><BR><font color=\"#FF0000\">Mission Objective:<BR>Talk to Marcus Stone.</font>";
		val3.UnknownId1 = unknownId;
		val3.Unknown5 = 6;
		val3.Unknown6 = 0;
		val3.Unknown7 = 0;
		val3.Unknown8 = 0;
		val3.Unknown9 = 1009;
		val3.Unknown10 = 1009;
		val3.MissionItemData = (MissionItemReward[])(object)new MissionItemReward[0];
		val3.Unknown11 = 1212436295;
		val3.Unknown12 = 0;
		val3.Unknown13 = 0;
		val3.UnknownHash1 = string.Empty;
		val3.Unknown14 = 0;
		val3.Unknown15 = 0;
		val3.Unknown16 = 0;
		val3.Unknown17 = 0;
		val3.Unknown18 = 0;
		val3.UnknownId2 = characterIdentity;
		val3.MissionIconId = 244818;
		val3.Unknown20 = 0;
		val3.Unknown21 = 0;
		QuestActionInfo[] array2 = new QuestActionInfo[1];
		QuestActionInfo val4 = new QuestActionInfo
		{
			Version = 24,
			Action = Identity.None,
			UnknownId1 = Identity.None,
			UnknownId2 = IdentityFromRaw(70099, 105040),
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
			UnknownHash1 = string.Empty,
			Unknown9 = 0,
			UnknownId7 = IdentityFromRaw(54001, 1293319995)
		};
		val = default(Identity);
		((Identity)(ref val)).Type = (IdentityType)40016;
		((Identity)(ref val)).Instance = 6553;
		val4.PlayfieldId = val;
		val4.Unknown10 = 100000;
		val4.Unknown11 = 100000;
		val4.Position = new Vector3(3638f, 0f, 830f);
		array2[0] = val4;
		val3.QuestActions = (QuestActionInfo[])(object)array2;
		val3.PlayerIds = (Identity[])(object)new Identity[1] { characterIdentity };
		val3.UnknownArray1 = new int[1] { 85360443 };
		val3.UnknownArray2 = new int[0];
		val3.CharacterInfos = (CharacterInfo[])(object)new CharacterInfo[0];
		val3.Unknown22 = 6;
		val3.PlayerIds2 = (Identity[])(object)new Identity[1] { characterIdentity };
		val3.Unknown23 = 0;
		val3.Unknown24 = 105040;
		val3.UnknownId3 = Identity.None;
		val3.Unknown25 = 0;
		val3.Unknown26 = 0;
		val3.QuestIdentities = (QuestIdentity[])(object)new QuestIdentity[0];
		val3.Unknown27 = 7;
		val3.FactionInfos = (Identity[])(object)new Identity[0];
		val3.Unknown28 = 1;
		array[0] = val3;
		val2.Quests = (Quest[])(object)array;
		return val2;
	}

	internal static QuestFullUpdateMessage CreateB194PreviewMessage(Identity characterIdentity)
	{
		//IL_000b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0010: Unknown result type (might be due to invalid IL or missing references)
		//IL_0013: Unknown result type (might be due to invalid IL or missing references)
		//IL_0033: Unknown result type (might be due to invalid IL or missing references)
		//IL_0034: Unknown result type (might be due to invalid IL or missing references)
		//IL_0035: Unknown result type (might be due to invalid IL or missing references)
		//IL_003b: Expected O, but got Unknown
		//IL_003c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0054: Unknown result type (might be due to invalid IL or missing references)
		//IL_005b: Expected O, but got Unknown
		//IL_005d: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a5: Unknown result type (might be due to invalid IL or missing references)
		//IL_0153: Unknown result type (might be due to invalid IL or missing references)
		//IL_0183: Unknown result type (might be due to invalid IL or missing references)
		//IL_0188: Unknown result type (might be due to invalid IL or missing references)
		//IL_0191: Unknown result type (might be due to invalid IL or missing references)
		//IL_0192: Unknown result type (might be due to invalid IL or missing references)
		//IL_019d: Unknown result type (might be due to invalid IL or missing references)
		//IL_019e: Unknown result type (might be due to invalid IL or missing references)
		//IL_01a9: Unknown result type (might be due to invalid IL or missing references)
		//IL_01b4: Unknown result type (might be due to invalid IL or missing references)
		//IL_01bf: Unknown result type (might be due to invalid IL or missing references)
		//IL_01c0: Unknown result type (might be due to invalid IL or missing references)
		//IL_01cb: Unknown result type (might be due to invalid IL or missing references)
		//IL_01cc: Unknown result type (might be due to invalid IL or missing references)
		//IL_01d7: Unknown result type (might be due to invalid IL or missing references)
		//IL_01e3: Unknown result type (might be due to invalid IL or missing references)
		//IL_01ef: Unknown result type (might be due to invalid IL or missing references)
		//IL_01fb: Unknown result type (might be due to invalid IL or missing references)
		//IL_0207: Unknown result type (might be due to invalid IL or missing references)
		//IL_0208: Unknown result type (might be due to invalid IL or missing references)
		//IL_0213: Unknown result type (might be due to invalid IL or missing references)
		//IL_021f: Unknown result type (might be due to invalid IL or missing references)
		//IL_022b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0237: Unknown result type (might be due to invalid IL or missing references)
		//IL_0243: Unknown result type (might be due to invalid IL or missing references)
		//IL_0244: Unknown result type (might be due to invalid IL or missing references)
		//IL_024f: Unknown result type (might be due to invalid IL or missing references)
		//IL_025b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0263: Unknown result type (might be due to invalid IL or missing references)
		//IL_026e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0279: Unknown result type (might be due to invalid IL or missing references)
		//IL_027c: Unknown result type (might be due to invalid IL or missing references)
		//IL_029c: Unknown result type (might be due to invalid IL or missing references)
		//IL_02a3: Unknown result type (might be due to invalid IL or missing references)
		//IL_02af: Unknown result type (might be due to invalid IL or missing references)
		//IL_02bb: Unknown result type (might be due to invalid IL or missing references)
		//IL_02cb: Unknown result type (might be due to invalid IL or missing references)
		//IL_02d5: Expected O, but got Unknown
		//IL_02d7: Expected O, but got Unknown
		//IL_02e7: Unknown result type (might be due to invalid IL or missing references)
		//IL_02e8: Unknown result type (might be due to invalid IL or missing references)
		//IL_0338: Unknown result type (might be due to invalid IL or missing references)
		//IL_0339: Unknown result type (might be due to invalid IL or missing references)
		//IL_035c: Unknown result type (might be due to invalid IL or missing references)
		Identity questId = IdentityFromRaw(56003, 1427419540);
		Identity val = default(Identity);
		((Identity)(ref val)).Type = (IdentityType)50000;
		((Identity)(ref val)).Instance = 2016273768;
		Identity unknownId = val;
		QuestFullUpdateMessage val2 = new QuestFullUpdateMessage();
		((N3Message)val2).Identity = characterIdentity;
		((N3Message)val2).Unknown = 1;
		Quest[] array = new Quest[1];
		Quest val3 = new Quest();
		val3.QuestId = questId;
		val3.Unknown1 = 15;
		val3.Unknown2 = 0;
		val3.Unknown3 = 0;
		val3.Unknown4 = 2;
		val3.ShortInfo = "Extinguish the Gas Fire";
		val3.LongInfo = "Extinguish the Gas Fire<BR><BR>Marcus Stone mentioned that he may be able to assist you with your lack of identity on Rubi-Ka, but at a price. A recent accident on one of his landing pads has left cargo damaged and people injured. Bodies can heal while cargo cannot. Extinguish one of the Gas Fires that has errupted on the landing pad.<BR><BR><font color=\"#FF0000\">Mission Objective:<BR>(Left Click) the <a href='itemref://296780/296780/1'>Compact Fire Suppressant Container</a> in your inventory to lift it up, then Left Click the Gas Fire to apply the fire suppressant.</font>";
		val3.UnknownId1 = unknownId;
		val3.Unknown5 = 6;
		val3.Unknown6 = 0;
		val3.Unknown7 = 0;
		val3.Unknown8 = 0;
		val3.Unknown9 = 1009;
		val3.Unknown10 = 1009;
		val3.MissionItemData = (MissionItemReward[])(object)new MissionItemReward[0];
		val3.Unknown11 = 1229076054;
		val3.Unknown12 = 0;
		val3.Unknown13 = 0;
		val3.UnknownHash1 = string.Empty;
		val3.Unknown14 = 0;
		val3.Unknown15 = 0;
		val3.Unknown16 = 0;
		val3.Unknown17 = 0;
		val3.Unknown18 = 0;
		val3.UnknownId2 = characterIdentity;
		val3.MissionIconId = 244818;
		val3.Unknown20 = 0;
		val3.Unknown21 = 0;
		QuestActionInfo[] array2 = new QuestActionInfo[1];
		QuestActionInfo val4 = new QuestActionInfo
		{
			Version = 24,
			Action = Identity.None,
			UnknownId1 = Identity.None,
			UnknownId2 = IdentityFromRaw(70099, 104939),
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
			UnknownHash1 = string.Empty,
			Unknown9 = 0,
			UnknownId7 = IdentityFromRaw(54001, 1293320000)
		};
		val = default(Identity);
		((Identity)(ref val)).Type = (IdentityType)40016;
		((Identity)(ref val)).Instance = 6553;
		val4.PlayfieldId = val;
		val4.Unknown10 = 100000;
		val4.Unknown11 = 100000;
		val4.Position = new Vector3(3604f, 0f, 833f);
		array2[0] = val4;
		val3.QuestActions = (QuestActionInfo[])(object)array2;
		val3.PlayerIds = (Identity[])(object)new Identity[1] { characterIdentity };
		val3.UnknownArray1 = new int[1] { 85360448 };
		val3.UnknownArray2 = new int[0];
		val3.CharacterInfos = (CharacterInfo[])(object)new CharacterInfo[0];
		val3.Unknown22 = 6;
		val3.PlayerIds2 = (Identity[])(object)new Identity[1] { characterIdentity };
		val3.Unknown23 = 0;
		val3.Unknown24 = 104939;
		val3.UnknownId3 = Identity.None;
		val3.Unknown25 = 0;
		val3.Unknown26 = 0;
		val3.QuestIdentities = (QuestIdentity[])(object)new QuestIdentity[0];
		val3.Unknown27 = 7;
		val3.FactionInfos = (Identity[])(object)new Identity[0];
		val3.Unknown28 = 1;
		array[0] = val3;
		val2.Quests = (Quest[])(object)array;
		return val2;
	}

	internal static CharacterActionMessage CreateB18FAction59Message(Identity characterIdentity)
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
			Target = IdentityFromRaw(56003, 1427419535),
			Parameter1 = 56003,
			Parameter2 = 1427419535,
			Unknown2 = 0
		};
	}

	internal static CharacterActionMessage CreateB18CAction59Message(Identity characterIdentity)
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
			Target = IdentityFromRaw(56003, 1427419532),
			Parameter1 = 56003,
			Parameter2 = 1427419532,
			Unknown2 = 0
		};
	}

	internal static QuestMessage CreateB18CQuestDeleteMessage(Identity characterIdentity)
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
			Mission = IdentityFromRaw(56003, 1427419532),
			Unknown2 = 0,
			Unknown3 = 0
		};
	}

	internal static QuestMessage CreateB18DQuestDeleteMessage(Identity characterIdentity)
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
			Mission = IdentityFromRaw(56003, 1427419533),
			Unknown2 = 0,
			Unknown3 = 0
		};
	}

	internal static QuestMessage CreateB18EQuestDeleteMessage(Identity characterIdentity)
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
			Mission = IdentityFromRaw(56003, 1427419534),
			Unknown2 = 0,
			Unknown3 = 0
		};
	}

	internal static QuestMessage CreateB18FQuestDeleteMessage(Identity characterIdentity)
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
			Mission = IdentityFromRaw(56003, 1427419535),
			Unknown2 = 0,
			Unknown3 = 0
		};
	}

	internal static CharacterActionMessage CreateB194Action59Message(Identity characterIdentity)
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
			Target = IdentityFromRaw(56003, 1427419540),
			Parameter1 = 56003,
			Parameter2 = 1427419540,
			Unknown2 = 0
		};
	}

	internal static QuestMessage CreateB194QuestDeleteMessage(Identity characterIdentity)
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
			Mission = IdentityFromRaw(56003, 1427419540),
			Unknown2 = 0,
			Unknown3 = 0
		};
	}

	internal static CharacterActionMessage CreateB196Action59Message(Identity characterIdentity)
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
			Target = IdentityFromRaw(56003, 1427419542),
			Parameter1 = 56003,
			Parameter2 = 1427419542,
			Unknown2 = 0
		};
	}

	internal static QuestMessage CreateB196QuestDeleteMessage(Identity characterIdentity)
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
			Mission = IdentityFromRaw(56003, 1427419542),
			Unknown2 = 0,
			Unknown3 = 0
		};
	}

	internal static QuestFullUpdateMessage CreateB196PreviewMessage(Identity characterIdentity)
	{
		//IL_000b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0010: Unknown result type (might be due to invalid IL or missing references)
		//IL_0013: Unknown result type (might be due to invalid IL or missing references)
		//IL_0033: Unknown result type (might be due to invalid IL or missing references)
		//IL_0034: Unknown result type (might be due to invalid IL or missing references)
		//IL_0035: Unknown result type (might be due to invalid IL or missing references)
		//IL_003b: Expected O, but got Unknown
		//IL_003c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0054: Unknown result type (might be due to invalid IL or missing references)
		//IL_005b: Expected O, but got Unknown
		//IL_005d: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a5: Unknown result type (might be due to invalid IL or missing references)
		//IL_0153: Unknown result type (might be due to invalid IL or missing references)
		//IL_0191: Unknown result type (might be due to invalid IL or missing references)
		//IL_0192: Unknown result type (might be due to invalid IL or missing references)
		//IL_01e2: Unknown result type (might be due to invalid IL or missing references)
		//IL_01e3: Unknown result type (might be due to invalid IL or missing references)
		//IL_0206: Unknown result type (might be due to invalid IL or missing references)
		Identity questId = IdentityFromRaw(56003, 1427419542);
		Identity val = default(Identity);
		((Identity)(ref val)).Type = (IdentityType)50000;
		((Identity)(ref val)).Instance = 2016273767;
		Identity unknownId = val;
		QuestFullUpdateMessage val2 = new QuestFullUpdateMessage();
		((N3Message)val2).Identity = characterIdentity;
		((N3Message)val2).Unknown = 1;
		Quest[] array = new Quest[1];
		Quest val3 = new Quest();
		val3.QuestId = questId;
		val3.Unknown1 = 15;
		val3.Unknown2 = 0;
		val3.Unknown3 = 0;
		val3.Unknown4 = 2;
		val3.ShortInfo = "Return to Marcus";
		val3.LongInfo = "Return to Marcus<BR><BR><font color=\"#FF0000\">Mission Objective:<BR>Talk to Marcus Stone and hand him the <a href='itemref://296780/296780/1'>Compact Fire Suppressant Container</a>.</font>";
		val3.UnknownId1 = unknownId;
		val3.Unknown5 = 6;
		val3.Unknown6 = 0;
		val3.Unknown7 = 0;
		val3.Unknown8 = 0;
		val3.Unknown9 = 1009;
		val3.Unknown10 = 1009;
		val3.MissionItemData = (MissionItemReward[])(object)new MissionItemReward[0];
		val3.Unknown11 = 1229076054;
		val3.Unknown12 = 0;
		val3.Unknown13 = 0;
		val3.UnknownHash1 = string.Empty;
		val3.Unknown14 = 0;
		val3.Unknown15 = 0;
		val3.Unknown16 = 0;
		val3.Unknown17 = 0;
		val3.Unknown18 = 0;
		val3.UnknownId2 = characterIdentity;
		val3.MissionIconId = 158429;
		val3.Unknown20 = 0;
		val3.Unknown21 = 0;
		val3.QuestActions = (QuestActionInfo[])(object)new QuestActionInfo[0];
		val3.PlayerIds = (Identity[])(object)new Identity[1] { characterIdentity };
		val3.UnknownArray1 = new int[1] { 85360448 };
		val3.UnknownArray2 = new int[0];
		val3.CharacterInfos = (CharacterInfo[])(object)new CharacterInfo[0];
		val3.Unknown22 = 6;
		val3.PlayerIds2 = (Identity[])(object)new Identity[1] { characterIdentity };
		val3.Unknown23 = 0;
		val3.Unknown24 = 104939;
		val3.UnknownId3 = Identity.None;
		val3.Unknown25 = 0;
		val3.Unknown26 = 0;
		val3.QuestIdentities = (QuestIdentity[])(object)new QuestIdentity[0];
		val3.Unknown27 = 7;
		val3.FactionInfos = (Identity[])(object)new Identity[0];
		val3.Unknown28 = 1;
		array[0] = val3;
		val2.Quests = (Quest[])(object)array;
		return val2;
	}

	internal static QuestFullUpdateMessage CreateFlintPreviewMessage(Identity characterIdentity)
	{
		//IL_000b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0010: Unknown result type (might be due to invalid IL or missing references)
		//IL_0013: Unknown result type (might be due to invalid IL or missing references)
		//IL_0033: Unknown result type (might be due to invalid IL or missing references)
		//IL_0034: Unknown result type (might be due to invalid IL or missing references)
		//IL_003b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0042: Expected O, but got Unknown
		//IL_0044: Unknown result type (might be due to invalid IL or missing references)
		//IL_005e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0065: Expected O, but got Unknown
		//IL_0067: Unknown result type (might be due to invalid IL or missing references)
		//IL_00af: Unknown result type (might be due to invalid IL or missing references)
		//IL_0159: Unknown result type (might be due to invalid IL or missing references)
		//IL_0191: Unknown result type (might be due to invalid IL or missing references)
		//IL_0196: Unknown result type (might be due to invalid IL or missing references)
		//IL_019f: Unknown result type (might be due to invalid IL or missing references)
		//IL_01a0: Unknown result type (might be due to invalid IL or missing references)
		//IL_01ab: Unknown result type (might be due to invalid IL or missing references)
		//IL_01ac: Unknown result type (might be due to invalid IL or missing references)
		//IL_01b7: Unknown result type (might be due to invalid IL or missing references)
		//IL_01c2: Unknown result type (might be due to invalid IL or missing references)
		//IL_01cd: Unknown result type (might be due to invalid IL or missing references)
		//IL_01ce: Unknown result type (might be due to invalid IL or missing references)
		//IL_01d9: Unknown result type (might be due to invalid IL or missing references)
		//IL_01da: Unknown result type (might be due to invalid IL or missing references)
		//IL_01e5: Unknown result type (might be due to invalid IL or missing references)
		//IL_01f1: Unknown result type (might be due to invalid IL or missing references)
		//IL_01fd: Unknown result type (might be due to invalid IL or missing references)
		//IL_0209: Unknown result type (might be due to invalid IL or missing references)
		//IL_0215: Unknown result type (might be due to invalid IL or missing references)
		//IL_0216: Unknown result type (might be due to invalid IL or missing references)
		//IL_0221: Unknown result type (might be due to invalid IL or missing references)
		//IL_022d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0239: Unknown result type (might be due to invalid IL or missing references)
		//IL_0245: Unknown result type (might be due to invalid IL or missing references)
		//IL_0251: Unknown result type (might be due to invalid IL or missing references)
		//IL_0252: Unknown result type (might be due to invalid IL or missing references)
		//IL_025d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0269: Unknown result type (might be due to invalid IL or missing references)
		//IL_0271: Unknown result type (might be due to invalid IL or missing references)
		//IL_027c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0287: Unknown result type (might be due to invalid IL or missing references)
		//IL_028a: Unknown result type (might be due to invalid IL or missing references)
		//IL_02aa: Unknown result type (might be due to invalid IL or missing references)
		//IL_02b1: Unknown result type (might be due to invalid IL or missing references)
		//IL_02bd: Unknown result type (might be due to invalid IL or missing references)
		//IL_02c9: Unknown result type (might be due to invalid IL or missing references)
		//IL_02d9: Unknown result type (might be due to invalid IL or missing references)
		//IL_02e3: Expected O, but got Unknown
		//IL_02e5: Expected O, but got Unknown
		//IL_02f5: Unknown result type (might be due to invalid IL or missing references)
		//IL_02f6: Unknown result type (might be due to invalid IL or missing references)
		//IL_0346: Unknown result type (might be due to invalid IL or missing references)
		//IL_0347: Unknown result type (might be due to invalid IL or missing references)
		//IL_036a: Unknown result type (might be due to invalid IL or missing references)
		Identity questId = IdentityFromRaw(56003, 1427419544);
		Identity val = default(Identity);
		((Identity)(ref val)).Type = (IdentityType)50000;
		((Identity)(ref val)).Instance = 2016273767;
		Identity unknownId = val;
		int unknown = 1201618627;
		QuestFullUpdateMessage val2 = new QuestFullUpdateMessage();
		((N3Message)val2).Identity = characterIdentity;
		((N3Message)val2).Unknown = 1;
		Quest[] array = new Quest[1];
		Quest val3 = new Quest();
		val3.QuestId = questId;
		val3.Unknown1 = 15;
		val3.Unknown2 = 0;
		val3.Unknown3 = 0;
		val3.Unknown4 = 2;
		val3.ShortInfo = "Talk to Flint Novak";
		val3.LongInfo = "Talk to Flint Novak<BR><BR><font color=\"#FF0000\">Mission Objective:<BR>Talk to Flint Novak.</font>";
		val3.UnknownId1 = unknownId;
		val3.Unknown5 = 6;
		val3.Unknown6 = 0;
		val3.Unknown7 = 0;
		val3.Unknown8 = 0;
		val3.Unknown9 = 1009;
		val3.Unknown10 = 1009;
		val3.MissionItemData = (MissionItemReward[])(object)new MissionItemReward[0];
		val3.Unknown11 = unknown;
		val3.Unknown12 = 0;
		val3.Unknown13 = 0;
		val3.UnknownHash1 = string.Empty;
		val3.Unknown14 = 0;
		val3.Unknown15 = 0;
		val3.Unknown16 = 0;
		val3.Unknown17 = 0;
		val3.Unknown18 = 0;
		val3.UnknownId2 = characterIdentity;
		val3.MissionIconId = 244818;
		val3.Unknown20 = 172800;
		val3.Unknown21 = 172800;
		QuestActionInfo[] array2 = new QuestActionInfo[1];
		QuestActionInfo val4 = new QuestActionInfo
		{
			Version = 24,
			Action = Identity.None,
			UnknownId1 = Identity.None,
			UnknownId2 = IdentityFromRaw(70099, 105042),
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
			UnknownHash1 = string.Empty,
			Unknown9 = 0,
			UnknownId7 = IdentityFromRaw(54001, 1293319996)
		};
		val = default(Identity);
		((Identity)(ref val)).Type = (IdentityType)40016;
		((Identity)(ref val)).Instance = 6553;
		val4.PlayfieldId = val;
		val4.Unknown10 = 100000;
		val4.Unknown11 = 100000;
		val4.Position = new Vector3(3598f, 0f, 863f);
		array2[0] = val4;
		val3.QuestActions = (QuestActionInfo[])(object)array2;
		val3.PlayerIds = (Identity[])(object)new Identity[1] { characterIdentity };
		val3.UnknownArray1 = new int[1] { 85360450 };
		val3.UnknownArray2 = new int[0];
		val3.CharacterInfos = (CharacterInfo[])(object)new CharacterInfo[0];
		val3.Unknown22 = 6;
		val3.PlayerIds2 = (Identity[])(object)new Identity[1] { characterIdentity };
		val3.Unknown23 = 0;
		val3.Unknown24 = 105040;
		val3.UnknownId3 = Identity.None;
		val3.Unknown25 = 0;
		val3.Unknown26 = 0;
		val3.QuestIdentities = (QuestIdentity[])(object)new QuestIdentity[0];
		val3.Unknown27 = 7;
		val3.FactionInfos = (Identity[])(object)new Identity[0];
		val3.Unknown28 = 1;
		array[0] = val3;
		val2.Quests = (Quest[])(object)array;
		return val2;
	}

	internal static QuestFullUpdateMessage CreateFindBioPreviewMessage(Identity characterIdentity)
	{
		//IL_000b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0010: Unknown result type (might be due to invalid IL or missing references)
		//IL_0013: Unknown result type (might be due to invalid IL or missing references)
		//IL_0033: Unknown result type (might be due to invalid IL or missing references)
		//IL_0034: Unknown result type (might be due to invalid IL or missing references)
		//IL_003b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0042: Expected O, but got Unknown
		//IL_0044: Unknown result type (might be due to invalid IL or missing references)
		//IL_005e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0065: Expected O, but got Unknown
		//IL_0067: Unknown result type (might be due to invalid IL or missing references)
		//IL_00af: Unknown result type (might be due to invalid IL or missing references)
		//IL_0159: Unknown result type (might be due to invalid IL or missing references)
		//IL_0191: Unknown result type (might be due to invalid IL or missing references)
		//IL_0196: Unknown result type (might be due to invalid IL or missing references)
		//IL_019f: Unknown result type (might be due to invalid IL or missing references)
		//IL_01a0: Unknown result type (might be due to invalid IL or missing references)
		//IL_01ab: Unknown result type (might be due to invalid IL or missing references)
		//IL_01ac: Unknown result type (might be due to invalid IL or missing references)
		//IL_01b7: Unknown result type (might be due to invalid IL or missing references)
		//IL_01b8: Unknown result type (might be due to invalid IL or missing references)
		//IL_01c3: Unknown result type (might be due to invalid IL or missing references)
		//IL_01ce: Unknown result type (might be due to invalid IL or missing references)
		//IL_01d9: Unknown result type (might be due to invalid IL or missing references)
		//IL_01da: Unknown result type (might be due to invalid IL or missing references)
		//IL_01e5: Unknown result type (might be due to invalid IL or missing references)
		//IL_01f1: Unknown result type (might be due to invalid IL or missing references)
		//IL_01fd: Unknown result type (might be due to invalid IL or missing references)
		//IL_0209: Unknown result type (might be due to invalid IL or missing references)
		//IL_0215: Unknown result type (might be due to invalid IL or missing references)
		//IL_0216: Unknown result type (might be due to invalid IL or missing references)
		//IL_0221: Unknown result type (might be due to invalid IL or missing references)
		//IL_022d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0239: Unknown result type (might be due to invalid IL or missing references)
		//IL_0245: Unknown result type (might be due to invalid IL or missing references)
		//IL_0251: Unknown result type (might be due to invalid IL or missing references)
		//IL_0252: Unknown result type (might be due to invalid IL or missing references)
		//IL_025d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0269: Unknown result type (might be due to invalid IL or missing references)
		//IL_0271: Unknown result type (might be due to invalid IL or missing references)
		//IL_027c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0287: Unknown result type (might be due to invalid IL or missing references)
		//IL_028a: Unknown result type (might be due to invalid IL or missing references)
		//IL_02aa: Unknown result type (might be due to invalid IL or missing references)
		//IL_02b1: Unknown result type (might be due to invalid IL or missing references)
		//IL_02bd: Unknown result type (might be due to invalid IL or missing references)
		//IL_02c9: Unknown result type (might be due to invalid IL or missing references)
		//IL_02d9: Unknown result type (might be due to invalid IL or missing references)
		//IL_02e3: Expected O, but got Unknown
		//IL_02e5: Expected O, but got Unknown
		//IL_02f5: Unknown result type (might be due to invalid IL or missing references)
		//IL_02f6: Unknown result type (might be due to invalid IL or missing references)
		//IL_0346: Unknown result type (might be due to invalid IL or missing references)
		//IL_0347: Unknown result type (might be due to invalid IL or missing references)
		//IL_036a: Unknown result type (might be due to invalid IL or missing references)
		Identity questId = IdentityFromRaw(56003, 1427419547);
		Identity val = default(Identity);
		((Identity)(ref val)).Type = (IdentityType)50000;
		((Identity)(ref val)).Instance = 2028010596;
		Identity unknownId = val;
		int unknown = 1201618627;
		QuestFullUpdateMessage val2 = new QuestFullUpdateMessage();
		((N3Message)val2).Identity = characterIdentity;
		((N3Message)val2).Unknown = 1;
		Quest[] array = new Quest[1];
		Quest val3 = new Quest();
		val3.QuestId = questId;
		val3.Unknown1 = 15;
		val3.Unknown2 = 0;
		val3.Unknown3 = 0;
		val3.Unknown4 = 2;
		val3.ShortInfo = "Find a Bio Analyzing Computer";
		val3.LongInfo = "Find a Bio Analyzing Computer<BR><BR>At the request of Flint Novak you must  find a Bio Analyzing Computer. You may find one of these computers by taking out the malfunctioning robots in the nearby junkyard.<BR><BR><font color=\"#FF0000\">Mission Objective:<BR>Kill 7 Robots in the junkyard.</font>";
		val3.UnknownId1 = unknownId;
		val3.Unknown5 = 6;
		val3.Unknown6 = 0;
		val3.Unknown7 = 0;
		val3.Unknown8 = 0;
		val3.Unknown9 = 1009;
		val3.Unknown10 = 1009;
		val3.MissionItemData = (MissionItemReward[])(object)new MissionItemReward[0];
		val3.Unknown11 = unknown;
		val3.Unknown12 = 0;
		val3.Unknown13 = 0;
		val3.UnknownHash1 = string.Empty;
		val3.Unknown14 = 0;
		val3.Unknown15 = 0;
		val3.Unknown16 = 0;
		val3.Unknown17 = 0;
		val3.Unknown18 = 0;
		val3.UnknownId2 = characterIdentity;
		val3.MissionIconId = 11330;
		val3.Unknown20 = 172800;
		val3.Unknown21 = 172800;
		QuestActionInfo[] array2 = new QuestActionInfo[1];
		QuestActionInfo val4 = new QuestActionInfo
		{
			Version = 20,
			Action = Identity.None,
			UnknownId1 = Identity.None,
			UnknownId2 = Identity.None,
			UnknownId3 = IdentityFromRaw(6553, 1296189783),
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
			UnknownHash1 = string.Empty,
			Unknown9 = 0,
			UnknownId7 = IdentityFromRaw(54012, 476692210)
		};
		val = default(Identity);
		((Identity)(ref val)).Type = (IdentityType)40016;
		((Identity)(ref val)).Instance = 6553;
		val4.PlayfieldId = val;
		val4.Unknown10 = 100000;
		val4.Unknown11 = 100000;
		val4.Position = new Vector3(3598f, 0f, 863f);
		array2[0] = val4;
		val3.QuestActions = (QuestActionInfo[])(object)array2;
		val3.PlayerIds = (Identity[])(object)new Identity[1] { characterIdentity };
		val3.UnknownArray1 = new int[1] { 85360450 };
		val3.UnknownArray2 = new int[0];
		val3.CharacterInfos = (CharacterInfo[])(object)new CharacterInfo[0];
		val3.Unknown22 = 6;
		val3.PlayerIds2 = (Identity[])(object)new Identity[1] { characterIdentity };
		val3.Unknown23 = 0;
		val3.Unknown24 = 105040;
		val3.UnknownId3 = Identity.None;
		val3.Unknown25 = 0;
		val3.Unknown26 = 0;
		val3.QuestIdentities = (QuestIdentity[])(object)new QuestIdentity[0];
		val3.Unknown27 = 7;
		val3.FactionInfos = (Identity[])(object)new Identity[0];
		val3.Unknown28 = 1;
		array[0] = val3;
		val2.Quests = (Quest[])(object)array;
		return val2;
	}

	internal static QuestFullUpdateMessage CreateDeliverBioPreviewMessage(Identity characterIdentity)
	{
		//IL_000b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0010: Unknown result type (might be due to invalid IL or missing references)
		//IL_0013: Unknown result type (might be due to invalid IL or missing references)
		//IL_0033: Unknown result type (might be due to invalid IL or missing references)
		//IL_0034: Unknown result type (might be due to invalid IL or missing references)
		//IL_003b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0042: Expected O, but got Unknown
		//IL_0044: Unknown result type (might be due to invalid IL or missing references)
		//IL_005e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0065: Expected O, but got Unknown
		//IL_0067: Unknown result type (might be due to invalid IL or missing references)
		//IL_00af: Unknown result type (might be due to invalid IL or missing references)
		//IL_0161: Unknown result type (might be due to invalid IL or missing references)
		//IL_0199: Unknown result type (might be due to invalid IL or missing references)
		//IL_019e: Unknown result type (might be due to invalid IL or missing references)
		//IL_01a6: Unknown result type (might be due to invalid IL or missing references)
		//IL_01b1: Unknown result type (might be due to invalid IL or missing references)
		//IL_01bc: Unknown result type (might be due to invalid IL or missing references)
		//IL_01bd: Unknown result type (might be due to invalid IL or missing references)
		//IL_01c8: Unknown result type (might be due to invalid IL or missing references)
		//IL_01d3: Unknown result type (might be due to invalid IL or missing references)
		//IL_01de: Unknown result type (might be due to invalid IL or missing references)
		//IL_01df: Unknown result type (might be due to invalid IL or missing references)
		//IL_01ea: Unknown result type (might be due to invalid IL or missing references)
		//IL_01eb: Unknown result type (might be due to invalid IL or missing references)
		//IL_01f6: Unknown result type (might be due to invalid IL or missing references)
		//IL_0202: Unknown result type (might be due to invalid IL or missing references)
		//IL_020e: Unknown result type (might be due to invalid IL or missing references)
		//IL_021a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0226: Unknown result type (might be due to invalid IL or missing references)
		//IL_0227: Unknown result type (might be due to invalid IL or missing references)
		//IL_0232: Unknown result type (might be due to invalid IL or missing references)
		//IL_023e: Unknown result type (might be due to invalid IL or missing references)
		//IL_024a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0256: Unknown result type (might be due to invalid IL or missing references)
		//IL_0262: Unknown result type (might be due to invalid IL or missing references)
		//IL_0263: Unknown result type (might be due to invalid IL or missing references)
		//IL_026e: Unknown result type (might be due to invalid IL or missing references)
		//IL_027a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0282: Unknown result type (might be due to invalid IL or missing references)
		//IL_028d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0298: Unknown result type (might be due to invalid IL or missing references)
		//IL_029b: Unknown result type (might be due to invalid IL or missing references)
		//IL_02bb: Unknown result type (might be due to invalid IL or missing references)
		//IL_02c2: Unknown result type (might be due to invalid IL or missing references)
		//IL_02ce: Unknown result type (might be due to invalid IL or missing references)
		//IL_02da: Unknown result type (might be due to invalid IL or missing references)
		//IL_02ea: Unknown result type (might be due to invalid IL or missing references)
		//IL_02f4: Expected O, but got Unknown
		//IL_02f6: Expected O, but got Unknown
		//IL_0306: Unknown result type (might be due to invalid IL or missing references)
		//IL_0307: Unknown result type (might be due to invalid IL or missing references)
		//IL_0357: Unknown result type (might be due to invalid IL or missing references)
		//IL_0358: Unknown result type (might be due to invalid IL or missing references)
		//IL_037b: Unknown result type (might be due to invalid IL or missing references)
		Identity questId = IdentityFromRaw(56003, 1427419548);
		Identity val = default(Identity);
		((Identity)(ref val)).Type = (IdentityType)50000;
		((Identity)(ref val)).Instance = 2028010593;
		Identity unknownId = val;
		int unknown = 1201618627;
		QuestFullUpdateMessage val2 = new QuestFullUpdateMessage();
		((N3Message)val2).Identity = characterIdentity;
		((N3Message)val2).Unknown = 1;
		Quest[] array = new Quest[1];
		Quest val3 = new Quest();
		val3.QuestId = questId;
		val3.Unknown1 = 15;
		val3.Unknown2 = 0;
		val3.Unknown3 = 0;
		val3.Unknown4 = 2;
		val3.ShortInfo = "Deliver the Bio Analyzing Co...";
		val3.LongInfo = "Deliver the Bio Analyzing Computer to Alex Gibbs<BR><BR>After killing a few junk robots you finally found a Bio Analyzing Computer. Flint Novak told you to give this to Alex Gibbs, a local roboticist.<BR><BR><font color=\"#FF0000\">Mission Objective:<BR>Give the <a href='itemref://156020/156021/1'>Bio Analyzing Computer</a> to Alex Gibbs.</font>";
		val3.UnknownId1 = unknownId;
		val3.Unknown5 = 6;
		val3.Unknown6 = 1120;
		val3.Unknown7 = 0;
		val3.Unknown8 = 2076;
		val3.Unknown9 = 1009;
		val3.Unknown10 = 1009;
		val3.MissionItemData = (MissionItemReward[])(object)new MissionItemReward[0];
		val3.Unknown11 = unknown;
		val3.Unknown12 = 0;
		val3.Unknown13 = 0;
		val3.UnknownHash1 = string.Empty;
		val3.Unknown14 = 0;
		val3.Unknown15 = 0;
		val3.Unknown16 = 0;
		val3.Unknown17 = 0;
		val3.Unknown18 = 0;
		val3.UnknownId2 = characterIdentity;
		val3.MissionIconId = 158429;
		val3.Unknown20 = 172800;
		val3.Unknown21 = 172800;
		QuestActionInfo[] array2 = new QuestActionInfo[1];
		QuestActionInfo val4 = new QuestActionInfo
		{
			Version = 6,
			Action = IdentityFromRaw(70099, 1112097102),
			UnknownId1 = Identity.None,
			UnknownId2 = IdentityFromRaw(70099, 1095518025),
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
			UnknownHash1 = string.Empty,
			Unknown9 = 0,
			UnknownId7 = IdentityFromRaw(54001, 1297475317)
		};
		val = default(Identity);
		((Identity)(ref val)).Type = (IdentityType)40016;
		((Identity)(ref val)).Instance = 6553;
		val4.PlayfieldId = val;
		val4.Unknown10 = 100000;
		val4.Unknown11 = 100000;
		val4.Position = new Vector3(3521f, 0f, 857f);
		array2[0] = val4;
		val3.QuestActions = (QuestActionInfo[])(object)array2;
		val3.PlayerIds = (Identity[])(object)new Identity[1] { characterIdentity };
		val3.UnknownArray1 = new int[1] { 85360450 };
		val3.UnknownArray2 = new int[0];
		val3.CharacterInfos = (CharacterInfo[])(object)new CharacterInfo[0];
		val3.Unknown22 = 6;
		val3.PlayerIds2 = (Identity[])(object)new Identity[1] { characterIdentity };
		val3.Unknown23 = 0;
		val3.Unknown24 = 105040;
		val3.UnknownId3 = Identity.None;
		val3.Unknown25 = 0;
		val3.Unknown26 = 0;
		val3.QuestIdentities = (QuestIdentity[])(object)new QuestIdentity[0];
		val3.Unknown27 = 0;
		val3.FactionInfos = (Identity[])(object)new Identity[0];
		val3.Unknown28 = 1;
		array[0] = val3;
		val2.Quests = (Quest[])(object)array;
		return val2;
	}

	internal static CharacterActionMessage CreateFindBioAction59Message(Identity characterIdentity)
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
			Target = IdentityFromRaw(56003, 1427419547),
			Parameter1 = 56003,
			Parameter2 = 1427419547,
			Unknown2 = 0
		};
	}

	internal static QuestMessage CreateFindBioQuestDeleteMessage(Identity characterIdentity)
	{
		//IL_0001: Unknown result type (might be due to invalid IL or missing references)
		//IL_0006: Unknown result type (might be due to invalid IL or missing references)
		//IL_0007: Unknown result type (might be due to invalid IL or missing references)
		//IL_000e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0016: Unknown result type (might be due to invalid IL or missing references)
		//IL_001e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0029: Unknown result type (might be due to invalid IL or missing references)
		//IL_0035: Expected O, but got Unknown
		return new QuestMessage
		{
			Identity = characterIdentity,
			Unknown = 0,
			Action = (QuestAction)1,
			Mission = IdentityFromRaw(56003, 1427419547)
		};
	}

	internal static CharacterActionMessage CreateDeliverBioAction59Message(Identity characterIdentity)
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
			Target = IdentityFromRaw(56003, 1427419548),
			Parameter1 = 56003,
			Parameter2 = 1427419548,
			Unknown2 = 0
		};
	}

	internal static QuestMessage CreateDeliverBioQuestDeleteMessage(Identity characterIdentity)
	{
		//IL_0001: Unknown result type (might be due to invalid IL or missing references)
		//IL_0006: Unknown result type (might be due to invalid IL or missing references)
		//IL_0007: Unknown result type (might be due to invalid IL or missing references)
		//IL_000e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0016: Unknown result type (might be due to invalid IL or missing references)
		//IL_001e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0029: Unknown result type (might be due to invalid IL or missing references)
		//IL_0035: Expected O, but got Unknown
		return new QuestMessage
		{
			Identity = characterIdentity,
			Unknown = 0,
			Action = (QuestAction)1,
			Mission = IdentityFromRaw(56003, 1427419548)
		};
	}

	internal static QuestFullUpdateMessage CreateSurveillanceUplinkPreviewMessage(Identity characterIdentity)
	{
		//IL_000b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0010: Unknown result type (might be due to invalid IL or missing references)
		//IL_0013: Unknown result type (might be due to invalid IL or missing references)
		//IL_0033: Unknown result type (might be due to invalid IL or missing references)
		//IL_0034: Unknown result type (might be due to invalid IL or missing references)
		//IL_003b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0042: Expected O, but got Unknown
		//IL_0044: Unknown result type (might be due to invalid IL or missing references)
		//IL_005e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0065: Expected O, but got Unknown
		//IL_0067: Unknown result type (might be due to invalid IL or missing references)
		//IL_00af: Unknown result type (might be due to invalid IL or missing references)
		//IL_0159: Unknown result type (might be due to invalid IL or missing references)
		//IL_0191: Unknown result type (might be due to invalid IL or missing references)
		//IL_0196: Unknown result type (might be due to invalid IL or missing references)
		//IL_019f: Unknown result type (might be due to invalid IL or missing references)
		//IL_01a0: Unknown result type (might be due to invalid IL or missing references)
		//IL_01ab: Unknown result type (might be due to invalid IL or missing references)
		//IL_01ac: Unknown result type (might be due to invalid IL or missing references)
		//IL_01b7: Unknown result type (might be due to invalid IL or missing references)
		//IL_01c2: Unknown result type (might be due to invalid IL or missing references)
		//IL_01cd: Unknown result type (might be due to invalid IL or missing references)
		//IL_01ce: Unknown result type (might be due to invalid IL or missing references)
		//IL_01d9: Unknown result type (might be due to invalid IL or missing references)
		//IL_01da: Unknown result type (might be due to invalid IL or missing references)
		//IL_01e5: Unknown result type (might be due to invalid IL or missing references)
		//IL_01f1: Unknown result type (might be due to invalid IL or missing references)
		//IL_01fd: Unknown result type (might be due to invalid IL or missing references)
		//IL_0209: Unknown result type (might be due to invalid IL or missing references)
		//IL_0215: Unknown result type (might be due to invalid IL or missing references)
		//IL_0216: Unknown result type (might be due to invalid IL or missing references)
		//IL_0221: Unknown result type (might be due to invalid IL or missing references)
		//IL_022d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0239: Unknown result type (might be due to invalid IL or missing references)
		//IL_0245: Unknown result type (might be due to invalid IL or missing references)
		//IL_0251: Unknown result type (might be due to invalid IL or missing references)
		//IL_0252: Unknown result type (might be due to invalid IL or missing references)
		//IL_025d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0269: Unknown result type (might be due to invalid IL or missing references)
		//IL_0271: Unknown result type (might be due to invalid IL or missing references)
		//IL_027c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0287: Unknown result type (might be due to invalid IL or missing references)
		//IL_028a: Unknown result type (might be due to invalid IL or missing references)
		//IL_02aa: Unknown result type (might be due to invalid IL or missing references)
		//IL_02b1: Unknown result type (might be due to invalid IL or missing references)
		//IL_02bd: Unknown result type (might be due to invalid IL or missing references)
		//IL_02c9: Unknown result type (might be due to invalid IL or missing references)
		//IL_02d9: Unknown result type (might be due to invalid IL or missing references)
		//IL_02e3: Expected O, but got Unknown
		//IL_02e5: Expected O, but got Unknown
		//IL_02f5: Unknown result type (might be due to invalid IL or missing references)
		//IL_02f6: Unknown result type (might be due to invalid IL or missing references)
		//IL_0346: Unknown result type (might be due to invalid IL or missing references)
		//IL_0347: Unknown result type (might be due to invalid IL or missing references)
		//IL_036a: Unknown result type (might be due to invalid IL or missing references)
		Identity questId = IdentityFromRaw(56003, 1431980617);
		Identity val = default(Identity);
		((Identity)(ref val)).Type = (IdentityType)50000;
		((Identity)(ref val)).Instance = 2028010593;
		Identity unknownId = val;
		int unknown = 1201618627;
		QuestFullUpdateMessage val2 = new QuestFullUpdateMessage();
		((N3Message)val2).Identity = characterIdentity;
		((N3Message)val2).Unknown = 1;
		Quest[] array = new Quest[1];
		Quest val3 = new Quest();
		val3.QuestId = questId;
		val3.Unknown1 = 15;
		val3.Unknown2 = 0;
		val3.Unknown3 = 0;
		val3.Unknown4 = 2;
		val3.ShortInfo = "Surveillance Uplink";
		val3.LongInfo = "Surveillance Uplink<BR><BR>Alex Gibbs has provided you with a contraption that will be able to hook into the video feed one of Desmond Calitri's Surveillance Droids.<BR><BR><font color=\"#FF0000\">Mission Objective:<BR>Target the Surveillance Droid and use (Right Click) the <a href='itemref://295800/295800/1'>Rebuilt HC-12 SecTec Monitor in your inventory.</a></font>";
		val3.UnknownId1 = unknownId;
		val3.Unknown5 = 6;
		val3.Unknown6 = 0;
		val3.Unknown7 = 0;
		val3.Unknown8 = 0;
		val3.Unknown9 = 1009;
		val3.Unknown10 = 1009;
		val3.MissionItemData = (MissionItemReward[])(object)new MissionItemReward[0];
		val3.Unknown11 = unknown;
		val3.Unknown12 = 0;
		val3.Unknown13 = 0;
		val3.UnknownHash1 = string.Empty;
		val3.Unknown14 = 0;
		val3.Unknown15 = 0;
		val3.Unknown16 = 0;
		val3.Unknown17 = 0;
		val3.Unknown18 = 0;
		val3.UnknownId2 = characterIdentity;
		val3.MissionIconId = 244818;
		val3.Unknown20 = 172800;
		val3.Unknown21 = 172800;
		QuestActionInfo[] array2 = new QuestActionInfo[1];
		QuestActionInfo val4 = new QuestActionInfo
		{
			Version = 24,
			Action = Identity.None,
			UnknownId1 = Identity.None,
			UnknownId2 = IdentityFromRaw(70099, 104915),
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
			UnknownHash1 = string.Empty,
			Unknown9 = 0,
			UnknownId7 = IdentityFromRaw(54001, 1297475562)
		};
		val = default(Identity);
		((Identity)(ref val)).Type = (IdentityType)40016;
		((Identity)(ref val)).Instance = 6553;
		val4.PlayfieldId = val;
		val4.Unknown10 = 100000;
		val4.Unknown11 = 100000;
		val4.Position = new Vector3(3521f, 0f, 857f);
		array2[0] = val4;
		val3.QuestActions = (QuestActionInfo[])(object)array2;
		val3.PlayerIds = (Identity[])(object)new Identity[1] { characterIdentity };
		val3.UnknownArray1 = new int[1] { 85360450 };
		val3.UnknownArray2 = new int[0];
		val3.CharacterInfos = (CharacterInfo[])(object)new CharacterInfo[0];
		val3.Unknown22 = 6;
		val3.PlayerIds2 = (Identity[])(object)new Identity[1] { characterIdentity };
		val3.Unknown23 = 0;
		val3.Unknown24 = 105040;
		val3.UnknownId3 = Identity.None;
		val3.Unknown25 = 0;
		val3.Unknown26 = 0;
		val3.QuestIdentities = (QuestIdentity[])(object)new QuestIdentity[0];
		val3.Unknown27 = 0;
		val3.FactionInfos = (Identity[])(object)new Identity[0];
		val3.Unknown28 = 1;
		array[0] = val3;
		val2.Quests = (Quest[])(object)array;
		return val2;
	}

	internal static CharacterActionMessage CreateSurveillanceUplinkAction59Message(Identity characterIdentity)
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
			Target = IdentityFromRaw(56003, 1431980617),
			Parameter1 = 56003,
			Parameter2 = 1431980617,
			Unknown2 = 0
		};
	}

	internal static QuestMessage CreateSurveillanceUplinkQuestDeleteMessage(Identity characterIdentity)
	{
		//IL_0001: Unknown result type (might be due to invalid IL or missing references)
		//IL_0006: Unknown result type (might be due to invalid IL or missing references)
		//IL_0007: Unknown result type (might be due to invalid IL or missing references)
		//IL_000e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0016: Unknown result type (might be due to invalid IL or missing references)
		//IL_001e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0029: Unknown result type (might be due to invalid IL or missing references)
		//IL_0035: Expected O, but got Unknown
		return new QuestMessage
		{
			Identity = characterIdentity,
			Unknown = 0,
			Action = (QuestAction)1,
			Mission = IdentityFromRaw(56003, 1431980617)
		};
	}

	internal static QuestFullUpdateMessage CreatePlantBugPreviewMessage(Identity characterIdentity)
	{
		//IL_000b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0010: Unknown result type (might be due to invalid IL or missing references)
		//IL_0013: Unknown result type (might be due to invalid IL or missing references)
		//IL_0033: Unknown result type (might be due to invalid IL or missing references)
		//IL_0034: Unknown result type (might be due to invalid IL or missing references)
		//IL_003b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0042: Expected O, but got Unknown
		//IL_0044: Unknown result type (might be due to invalid IL or missing references)
		//IL_005e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0065: Expected O, but got Unknown
		//IL_0067: Unknown result type (might be due to invalid IL or missing references)
		//IL_00af: Unknown result type (might be due to invalid IL or missing references)
		//IL_0159: Unknown result type (might be due to invalid IL or missing references)
		//IL_019f: Unknown result type (might be due to invalid IL or missing references)
		//IL_01a0: Unknown result type (might be due to invalid IL or missing references)
		//IL_01f0: Unknown result type (might be due to invalid IL or missing references)
		//IL_01f1: Unknown result type (might be due to invalid IL or missing references)
		//IL_0214: Unknown result type (might be due to invalid IL or missing references)
		Identity questId = IdentityFromRaw(56003, 1431981627);
		Identity val = default(Identity);
		((Identity)(ref val)).Type = (IdentityType)50000;
		((Identity)(ref val)).Instance = 2028010634;
		Identity unknownId = val;
		int unknown = 1201618627;
		QuestFullUpdateMessage val2 = new QuestFullUpdateMessage();
		((N3Message)val2).Identity = characterIdentity;
		((N3Message)val2).Unknown = 1;
		Quest[] array = new Quest[1];
		Quest val3 = new Quest();
		val3.QuestId = questId;
		val3.Unknown1 = 15;
		val3.Unknown2 = 0;
		val3.Unknown3 = 0;
		val3.Unknown4 = 2;
		val3.ShortInfo = "Plant a Bug";
		val3.LongInfo = "Plant a Bug<BR><BR>To further incriminate Desmond Calitri, a remote audio recording device is to be placed within his office.<BR><BR><font color=\"#FF0000\">Mission Objective:<BR>Find a suitable location in Desmond Calitri's office to hide the bug. Pick up (Left Click) the <a href='itemref://295801/295801/1'>RC-P Audio Recording Device</a> in your inventory and drop it (Left Click) in a suitable location.</font>";
		val3.UnknownId1 = unknownId;
		val3.Unknown5 = 6;
		val3.Unknown6 = 0;
		val3.Unknown7 = 0;
		val3.Unknown8 = 0;
		val3.Unknown9 = 1009;
		val3.Unknown10 = 1009;
		val3.MissionItemData = (MissionItemReward[])(object)new MissionItemReward[0];
		val3.Unknown11 = unknown;
		val3.Unknown12 = 0;
		val3.Unknown13 = 0;
		val3.UnknownHash1 = string.Empty;
		val3.Unknown14 = 0;
		val3.Unknown15 = 0;
		val3.Unknown16 = 0;
		val3.Unknown17 = 0;
		val3.Unknown18 = 0;
		val3.UnknownId2 = characterIdentity;
		val3.MissionIconId = 11342;
		val3.Unknown20 = 172800;
		val3.Unknown21 = 172800;
		val3.QuestActions = (QuestActionInfo[])(object)new QuestActionInfo[0];
		val3.PlayerIds = (Identity[])(object)new Identity[1] { characterIdentity };
		val3.UnknownArray1 = new int[1] { 85360450 };
		val3.UnknownArray2 = new int[0];
		val3.CharacterInfos = (CharacterInfo[])(object)new CharacterInfo[0];
		val3.Unknown22 = 6;
		val3.PlayerIds2 = (Identity[])(object)new Identity[1] { characterIdentity };
		val3.Unknown23 = 0;
		val3.Unknown24 = 105040;
		val3.UnknownId3 = Identity.None;
		val3.Unknown25 = 0;
		val3.Unknown26 = 0;
		val3.QuestIdentities = (QuestIdentity[])(object)new QuestIdentity[0];
		val3.Unknown27 = 0;
		val3.FactionInfos = (Identity[])(object)new Identity[0];
		val3.Unknown28 = 1;
		array[0] = val3;
		val2.Quests = (Quest[])(object)array;
		return val2;
	}

	internal static CharacterActionMessage CreatePlantBugAction59Message(Identity characterIdentity)
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
			Target = IdentityFromRaw(56003, 1431981627),
			Parameter1 = 56003,
			Parameter2 = 1431981627,
			Unknown2 = 0
		};
	}

	internal static QuestMessage CreatePlantBugQuestDeleteMessage(Identity characterIdentity)
	{
		//IL_0001: Unknown result type (might be due to invalid IL or missing references)
		//IL_0006: Unknown result type (might be due to invalid IL or missing references)
		//IL_0007: Unknown result type (might be due to invalid IL or missing references)
		//IL_000e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0016: Unknown result type (might be due to invalid IL or missing references)
		//IL_001e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0029: Unknown result type (might be due to invalid IL or missing references)
		//IL_0035: Expected O, but got Unknown
		return new QuestMessage
		{
			Identity = characterIdentity,
			Unknown = 0,
			Action = (QuestAction)1,
			Mission = IdentityFromRaw(56003, 1431981627)
		};
	}

	internal static QuestFullUpdateMessage CreateDeliverHc12BillPreviewMessage(Identity characterIdentity)
	{
		//IL_000b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0010: Unknown result type (might be due to invalid IL or missing references)
		//IL_0013: Unknown result type (might be due to invalid IL or missing references)
		//IL_0033: Unknown result type (might be due to invalid IL or missing references)
		//IL_0034: Unknown result type (might be due to invalid IL or missing references)
		//IL_003b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0042: Expected O, but got Unknown
		//IL_0044: Unknown result type (might be due to invalid IL or missing references)
		//IL_005e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0065: Expected O, but got Unknown
		//IL_0067: Unknown result type (might be due to invalid IL or missing references)
		//IL_00af: Unknown result type (might be due to invalid IL or missing references)
		//IL_0159: Unknown result type (might be due to invalid IL or missing references)
		//IL_019f: Unknown result type (might be due to invalid IL or missing references)
		//IL_01a0: Unknown result type (might be due to invalid IL or missing references)
		//IL_01f0: Unknown result type (might be due to invalid IL or missing references)
		//IL_01f1: Unknown result type (might be due to invalid IL or missing references)
		//IL_0214: Unknown result type (might be due to invalid IL or missing references)
		Identity questId = IdentityFromRaw(56003, 1431981628);
		Identity val = default(Identity);
		((Identity)(ref val)).Type = (IdentityType)50000;
		((Identity)(ref val)).Instance = 2028010598;
		Identity unknownId = val;
		int unknown = 1201618627;
		QuestFullUpdateMessage val2 = new QuestFullUpdateMessage();
		((N3Message)val2).Identity = characterIdentity;
		((N3Message)val2).Unknown = 1;
		Quest[] array = new Quest[1];
		Quest val3 = new Quest();
		val3.QuestId = questId;
		val3.Unknown1 = 15;
		val3.Unknown2 = 0;
		val3.Unknown3 = 0;
		val3.Unknown4 = 2;
		val3.ShortInfo = "Deliver the Rebuilt HC-12 Se...";
		val3.LongInfo = "Deliver the Rebuilt HC-12 SecTec Monitor<BR><BR>With the Surveillance Droid feed uplink and a hidden audio recording device in Desmond Calitri's office, it is time to deliver this potential evidence to one of Alex's friend ICC Immigration Officer Bill.<BR><BR><font color=\"#FF0000\">Mission Objective:<BR>Give the <a href='itemref://295800/295800/1'>Rebuilt HC-12 SecTec Monitor</a> to ICC Immigration Officer Bill.</font>";
		val3.UnknownId1 = unknownId;
		val3.Unknown5 = 6;
		val3.Unknown6 = 0;
		val3.Unknown7 = 0;
		val3.Unknown8 = 0;
		val3.Unknown9 = 1009;
		val3.Unknown10 = 1009;
		val3.MissionItemData = (MissionItemReward[])(object)new MissionItemReward[0];
		val3.Unknown11 = unknown;
		val3.Unknown12 = 0;
		val3.Unknown13 = 0;
		val3.UnknownHash1 = string.Empty;
		val3.Unknown14 = 0;
		val3.Unknown15 = 0;
		val3.Unknown16 = 0;
		val3.Unknown17 = 0;
		val3.Unknown18 = 0;
		val3.UnknownId2 = characterIdentity;
		val3.MissionIconId = 158429;
		val3.Unknown20 = 172800;
		val3.Unknown21 = 172800;
		val3.QuestActions = (QuestActionInfo[])(object)new QuestActionInfo[0];
		val3.PlayerIds = (Identity[])(object)new Identity[1] { characterIdentity };
		val3.UnknownArray1 = new int[1] { 85360450 };
		val3.UnknownArray2 = new int[0];
		val3.CharacterInfos = (CharacterInfo[])(object)new CharacterInfo[0];
		val3.Unknown22 = 6;
		val3.PlayerIds2 = (Identity[])(object)new Identity[1] { characterIdentity };
		val3.Unknown23 = 0;
		val3.Unknown24 = 105040;
		val3.UnknownId3 = Identity.None;
		val3.Unknown25 = 0;
		val3.Unknown26 = 0;
		val3.QuestIdentities = (QuestIdentity[])(object)new QuestIdentity[0];
		val3.Unknown27 = 0;
		val3.FactionInfos = (Identity[])(object)new Identity[0];
		val3.Unknown28 = 1;
		array[0] = val3;
		val2.Quests = (Quest[])(object)array;
		return val2;
	}

	internal static CharacterActionMessage CreateDeliverHc12BillAction59Message(Identity characterIdentity)
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
			Target = IdentityFromRaw(56003, 1431981628),
			Parameter1 = 56003,
			Parameter2 = 1431981628,
			Unknown2 = 0
		};
	}

	internal static QuestMessage CreateDeliverHc12BillQuestDeleteMessage(Identity characterIdentity)
	{
		//IL_0001: Unknown result type (might be due to invalid IL or missing references)
		//IL_0006: Unknown result type (might be due to invalid IL or missing references)
		//IL_0007: Unknown result type (might be due to invalid IL or missing references)
		//IL_000e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0016: Unknown result type (might be due to invalid IL or missing references)
		//IL_001e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0029: Unknown result type (might be due to invalid IL or missing references)
		//IL_0035: Expected O, but got Unknown
		return new QuestMessage
		{
			Identity = characterIdentity,
			Unknown = 0,
			Action = (QuestAction)1,
			Mission = IdentityFromRaw(56003, 1431981628)
		};
	}

	internal static QuestFullUpdateMessage CreateKneecappingPreviewMessage(Identity characterIdentity)
	{
		//IL_000b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0010: Unknown result type (might be due to invalid IL or missing references)
		//IL_0013: Unknown result type (might be due to invalid IL or missing references)
		//IL_0033: Unknown result type (might be due to invalid IL or missing references)
		//IL_0034: Unknown result type (might be due to invalid IL or missing references)
		//IL_003b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0042: Expected O, but got Unknown
		//IL_0044: Unknown result type (might be due to invalid IL or missing references)
		//IL_005e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0065: Expected O, but got Unknown
		//IL_0067: Unknown result type (might be due to invalid IL or missing references)
		//IL_00af: Unknown result type (might be due to invalid IL or missing references)
		//IL_0159: Unknown result type (might be due to invalid IL or missing references)
		//IL_019f: Unknown result type (might be due to invalid IL or missing references)
		//IL_01a0: Unknown result type (might be due to invalid IL or missing references)
		//IL_01f0: Unknown result type (might be due to invalid IL or missing references)
		//IL_01f1: Unknown result type (might be due to invalid IL or missing references)
		//IL_0214: Unknown result type (might be due to invalid IL or missing references)
		Identity questId = IdentityFromRaw(56003, 1431981629);
		Identity val = default(Identity);
		((Identity)(ref val)).Type = (IdentityType)50000;
		((Identity)(ref val)).Instance = 2028010595;
		Identity unknownId = val;
		int unknown = 1201618627;
		QuestFullUpdateMessage val2 = new QuestFullUpdateMessage();
		((N3Message)val2).Identity = characterIdentity;
		((N3Message)val2).Unknown = 1;
		Quest[] array = new Quest[1];
		Quest val3 = new Quest();
		val3.QuestId = questId;
		val3.Unknown1 = 15;
		val3.Unknown2 = 0;
		val3.Unknown3 = 0;
		val3.Unknown4 = 2;
		val3.ShortInfo = "Kneecapping a Kneebreaker";
		val3.LongInfo = "Kneecapping a Kneebreaker<BR><BR>While monitoring the audio and video feeds of Desmond Calitri, it became clear that he intends to send \"The Kneebreaker\", Alfonzo Rizzolo, to deal with an upstart Dockworker who is fighting for fair working conditions.<BR><BR><font color=\"#FF0000\">Mission Objective:<BR>Kill \"The Kneebreaker\".</font>";
		val3.UnknownId1 = unknownId;
		val3.Unknown5 = 6;
		val3.Unknown6 = 0;
		val3.Unknown7 = 0;
		val3.Unknown8 = 0;
		val3.Unknown9 = 1009;
		val3.Unknown10 = 1009;
		val3.MissionItemData = (MissionItemReward[])(object)new MissionItemReward[0];
		val3.Unknown11 = unknown;
		val3.Unknown12 = 0;
		val3.Unknown13 = 0;
		val3.UnknownHash1 = string.Empty;
		val3.Unknown14 = 0;
		val3.Unknown15 = 0;
		val3.Unknown16 = 0;
		val3.Unknown17 = 0;
		val3.Unknown18 = 0;
		val3.UnknownId2 = characterIdentity;
		val3.MissionIconId = 11330;
		val3.Unknown20 = 172800;
		val3.Unknown21 = 172800;
		val3.QuestActions = (QuestActionInfo[])(object)new QuestActionInfo[0];
		val3.PlayerIds = (Identity[])(object)new Identity[1] { characterIdentity };
		val3.UnknownArray1 = new int[1] { 85360450 };
		val3.UnknownArray2 = new int[0];
		val3.CharacterInfos = (CharacterInfo[])(object)new CharacterInfo[0];
		val3.Unknown22 = 6;
		val3.PlayerIds2 = (Identity[])(object)new Identity[1] { characterIdentity };
		val3.Unknown23 = 0;
		val3.Unknown24 = 105040;
		val3.UnknownId3 = Identity.None;
		val3.Unknown25 = 0;
		val3.Unknown26 = 0;
		val3.QuestIdentities = (QuestIdentity[])(object)new QuestIdentity[0];
		val3.Unknown27 = 0;
		val3.FactionInfos = (Identity[])(object)new Identity[0];
		val3.Unknown28 = 1;
		array[0] = val3;
		val2.Quests = (Quest[])(object)array;
		return val2;
	}

	internal static QuestFullUpdateMessage CreateReportToAlexPreviewMessage(Identity characterIdentity)
	{
		//IL_000b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0010: Unknown result type (might be due to invalid IL or missing references)
		//IL_0013: Unknown result type (might be due to invalid IL or missing references)
		//IL_0033: Unknown result type (might be due to invalid IL or missing references)
		//IL_0034: Unknown result type (might be due to invalid IL or missing references)
		//IL_003b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0042: Expected O, but got Unknown
		//IL_0044: Unknown result type (might be due to invalid IL or missing references)
		//IL_005e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0065: Expected O, but got Unknown
		//IL_0067: Unknown result type (might be due to invalid IL or missing references)
		//IL_00af: Unknown result type (might be due to invalid IL or missing references)
		//IL_0159: Unknown result type (might be due to invalid IL or missing references)
		//IL_019f: Unknown result type (might be due to invalid IL or missing references)
		//IL_01a0: Unknown result type (might be due to invalid IL or missing references)
		//IL_01f0: Unknown result type (might be due to invalid IL or missing references)
		//IL_01f1: Unknown result type (might be due to invalid IL or missing references)
		//IL_0214: Unknown result type (might be due to invalid IL or missing references)
		Identity questId = IdentityFromRaw(56003, 1432044389);
		Identity val = default(Identity);
		((Identity)(ref val)).Type = (IdentityType)50000;
		((Identity)(ref val)).Instance = 2028010593;
		Identity unknownId = val;
		int unknown = 1201618627;
		QuestFullUpdateMessage val2 = new QuestFullUpdateMessage();
		((N3Message)val2).Identity = characterIdentity;
		((N3Message)val2).Unknown = 1;
		Quest[] array = new Quest[1];
		Quest val3 = new Quest();
		val3.QuestId = questId;
		val3.Unknown1 = 15;
		val3.Unknown2 = 0;
		val3.Unknown3 = 0;
		val3.Unknown4 = 2;
		val3.ShortInfo = "Report to Alex";
		val3.LongInfo = "Report to Alex<BR><BR>You have put a major dent in Demond Caltiri's plans. Since Bill doesn't want to talk to you about this matter, you decided to update Alex on your progress. She did promise you a reward for your efforts...<BR><BR><font color=\"#FF0000\">Mission Objective:<BR>Talk to Alex Gibbs.</font>";
		val3.UnknownId1 = unknownId;
		val3.Unknown5 = 6;
		val3.Unknown6 = 0;
		val3.Unknown7 = 0;
		val3.Unknown8 = 0;
		val3.Unknown9 = 1009;
		val3.Unknown10 = 1009;
		val3.MissionItemData = (MissionItemReward[])(object)new MissionItemReward[0];
		val3.Unknown11 = unknown;
		val3.Unknown12 = 0;
		val3.Unknown13 = 0;
		val3.UnknownHash1 = string.Empty;
		val3.Unknown14 = 0;
		val3.Unknown15 = 0;
		val3.Unknown16 = 0;
		val3.Unknown17 = 0;
		val3.Unknown18 = 0;
		val3.UnknownId2 = characterIdentity;
		val3.MissionIconId = 244818;
		val3.Unknown20 = 172800;
		val3.Unknown21 = 172800;
		val3.QuestActions = (QuestActionInfo[])(object)new QuestActionInfo[0];
		val3.PlayerIds = (Identity[])(object)new Identity[1] { characterIdentity };
		val3.UnknownArray1 = new int[1] { 85360450 };
		val3.UnknownArray2 = new int[0];
		val3.CharacterInfos = (CharacterInfo[])(object)new CharacterInfo[0];
		val3.Unknown22 = 6;
		val3.PlayerIds2 = (Identity[])(object)new Identity[1] { characterIdentity };
		val3.Unknown23 = 0;
		val3.Unknown24 = 105040;
		val3.UnknownId3 = Identity.None;
		val3.Unknown25 = 0;
		val3.Unknown26 = 0;
		val3.QuestIdentities = (QuestIdentity[])(object)new QuestIdentity[0];
		val3.Unknown27 = 0;
		val3.FactionInfos = (Identity[])(object)new Identity[0];
		val3.Unknown28 = 1;
		array[0] = val3;
		val2.Quests = (Quest[])(object)array;
		return val2;
	}

	internal static QuestFullUpdateMessage CreateTalkToStanPreviewMessage(Identity characterIdentity)
	{
		//IL_000b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0010: Unknown result type (might be due to invalid IL or missing references)
		//IL_0013: Unknown result type (might be due to invalid IL or missing references)
		//IL_0033: Unknown result type (might be due to invalid IL or missing references)
		//IL_0034: Unknown result type (might be due to invalid IL or missing references)
		//IL_003b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0042: Expected O, but got Unknown
		//IL_0044: Unknown result type (might be due to invalid IL or missing references)
		//IL_005e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0065: Expected O, but got Unknown
		//IL_0067: Unknown result type (might be due to invalid IL or missing references)
		//IL_00af: Unknown result type (might be due to invalid IL or missing references)
		//IL_0159: Unknown result type (might be due to invalid IL or missing references)
		//IL_019f: Unknown result type (might be due to invalid IL or missing references)
		//IL_01a0: Unknown result type (might be due to invalid IL or missing references)
		//IL_01f0: Unknown result type (might be due to invalid IL or missing references)
		//IL_01f1: Unknown result type (might be due to invalid IL or missing references)
		//IL_0214: Unknown result type (might be due to invalid IL or missing references)
		Identity questId = IdentityFromRaw(56003, 1432044390);
		Identity val = default(Identity);
		((Identity)(ref val)).Type = (IdentityType)50000;
		((Identity)(ref val)).Instance = 2028010595;
		Identity unknownId = val;
		int unknown = 1201618627;
		QuestFullUpdateMessage val2 = new QuestFullUpdateMessage();
		((N3Message)val2).Identity = characterIdentity;
		((N3Message)val2).Unknown = 1;
		Quest[] array = new Quest[1];
		Quest val3 = new Quest();
		val3.QuestId = questId;
		val3.Unknown1 = 15;
		val3.Unknown2 = 0;
		val3.Unknown3 = 0;
		val3.Unknown4 = 2;
		val3.ShortInfo = "Talk to Stan Goodman";
		val3.LongInfo = "Talk to Stan Goodman<BR><BR><font color=\"#63ad63\">Identity Crisis:</font><BR>In order to leave Arete Landing and become a citizen of Rubi-Ka, you need an identity. Your mission is to create a fake ID Card to you can leave this place..<BR><BR>Alex told you to go talk to Stan Goodman, a local 'purveyer of recently used merchandise'. He should be able to help with aquiring more parts for your ID card.<BR><BR><font color=\"#FF0000\">Mission Objective:<BR>Talk to Stan Goodman.</font>";
		val3.UnknownId1 = unknownId;
		val3.Unknown5 = 6;
		val3.Unknown6 = 0;
		val3.Unknown7 = 0;
		val3.Unknown8 = 0;
		val3.Unknown9 = 1009;
		val3.Unknown10 = 1009;
		val3.MissionItemData = (MissionItemReward[])(object)new MissionItemReward[0];
		val3.Unknown11 = unknown;
		val3.Unknown12 = 0;
		val3.Unknown13 = 0;
		val3.UnknownHash1 = string.Empty;
		val3.Unknown14 = 0;
		val3.Unknown15 = 0;
		val3.Unknown16 = 0;
		val3.Unknown17 = 0;
		val3.Unknown18 = 0;
		val3.UnknownId2 = characterIdentity;
		val3.MissionIconId = 244818;
		val3.Unknown20 = 172800;
		val3.Unknown21 = 172800;
		val3.QuestActions = (QuestActionInfo[])(object)new QuestActionInfo[0];
		val3.PlayerIds = (Identity[])(object)new Identity[1] { characterIdentity };
		val3.UnknownArray1 = new int[1] { 85360450 };
		val3.UnknownArray2 = new int[0];
		val3.CharacterInfos = (CharacterInfo[])(object)new CharacterInfo[0];
		val3.Unknown22 = 6;
		val3.PlayerIds2 = (Identity[])(object)new Identity[1] { characterIdentity };
		val3.Unknown23 = 0;
		val3.Unknown24 = 105040;
		val3.UnknownId3 = Identity.None;
		val3.Unknown25 = 0;
		val3.Unknown26 = 0;
		val3.QuestIdentities = (QuestIdentity[])(object)new QuestIdentity[0];
		val3.Unknown27 = 0;
		val3.FactionInfos = (Identity[])(object)new Identity[0];
		val3.Unknown28 = 1;
		array[0] = val3;
		val2.Quests = (Quest[])(object)array;
		return val2;
	}

	internal static QuestFullUpdateMessage CreateTradeskillNanoSensorPreviewMessage(Identity characterIdentity)
	{
		//IL_000b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0010: Unknown result type (might be due to invalid IL or missing references)
		//IL_0013: Unknown result type (might be due to invalid IL or missing references)
		//IL_0033: Unknown result type (might be due to invalid IL or missing references)
		//IL_0034: Unknown result type (might be due to invalid IL or missing references)
		//IL_003b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0042: Expected O, but got Unknown
		//IL_0044: Unknown result type (might be due to invalid IL or missing references)
		//IL_005e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0065: Expected O, but got Unknown
		//IL_0067: Unknown result type (might be due to invalid IL or missing references)
		//IL_00af: Unknown result type (might be due to invalid IL or missing references)
		//IL_0159: Unknown result type (might be due to invalid IL or missing references)
		//IL_019f: Unknown result type (might be due to invalid IL or missing references)
		//IL_01a0: Unknown result type (might be due to invalid IL or missing references)
		//IL_01f0: Unknown result type (might be due to invalid IL or missing references)
		//IL_01f1: Unknown result type (might be due to invalid IL or missing references)
		//IL_0214: Unknown result type (might be due to invalid IL or missing references)
		Identity questId = IdentityFromRaw(56003, 1432044391);
		Identity val = default(Identity);
		((Identity)(ref val)).Type = (IdentityType)50000;
		((Identity)(ref val)).Instance = 2028010593;
		Identity unknownId = val;
		int unknown = 1201618627;
		QuestFullUpdateMessage val2 = new QuestFullUpdateMessage();
		((N3Message)val2).Identity = characterIdentity;
		((N3Message)val2).Unknown = 1;
		Quest[] array = new Quest[1];
		Quest val3 = new Quest();
		val3.QuestId = questId;
		val3.Unknown1 = 15;
		val3.Unknown2 = 0;
		val3.Unknown3 = 0;
		val3.Unknown4 = 2;
		val3.ShortInfo = "Tradeskilling (1/4): Assembl...";
		val3.LongInfo = "Tradeskilling (1/4): Assemble a Nano Sensor<BR><BR><font color=\"#FF0000\">WARNING: If you are interested in learning tradeskilling, this mission will help you learn the basics. However, only Engineers and Traders are equipped with profession tools to help them master the art of tradeskilling.</font><BR><BR>Alex Gibbs has provided you with the recipe for creating a <a href='itemref:// 156026/156027/1'>Personalized Basic Robot Brain</a>. Once this mission has been completed, allow her to inspect it. <BR><BR><font color=\"#FFFFFF\">1. Buy the following item from the <a href='itemref://297281/297281/1'>Junk Shop</a>:<BR><a href='itemref://150922/150922/1'><img src=\"rdb://151011\"> Screwdriver</a><BR><BR>2. Find <a href='itemref://42620/42620/1'>Robot Junk</a>.<BR>Do so by killing and looting a robot.<BR><BR>3. Modify the <a href='itemref://42620/42620/1'>Robot Junk</a> with the <a href='itemref://150922/150922/1'>Screwdriver</a> to create a <a href='itemref://150923/150924/1'>Nano Sensor</a>.<BR><a href='itemref://150922/150922/1'><img src=\"rdb://151011\"></a> + <a href='itemref://42620/42620/1'><img src=\"rdb://290417\"></a> = <a href='itemref://150923/150923/1'><img src=\"rdb://149940\"></a><BR></font><BR><font color=\"#FF0000\">Mission Objective: Open the Tradeskill Kit %{KEY:WINDOW_TS}%, place the <a href='itemref://150922/150922/1'>Screwdriver</a> as the Source and the <a href='itemref://42620/42620/1'>Robot Junk</a> as the Target, then press Build.</font>";
		val3.UnknownId1 = unknownId;
		val3.Unknown5 = 6;
		val3.Unknown6 = 0;
		val3.Unknown7 = 0;
		val3.Unknown8 = 0;
		val3.Unknown9 = 1009;
		val3.Unknown10 = 1009;
		val3.MissionItemData = (MissionItemReward[])(object)new MissionItemReward[0];
		val3.Unknown11 = unknown;
		val3.Unknown12 = 0;
		val3.Unknown13 = 0;
		val3.UnknownHash1 = string.Empty;
		val3.Unknown14 = 0;
		val3.Unknown15 = 0;
		val3.Unknown16 = 0;
		val3.Unknown17 = 0;
		val3.Unknown18 = 0;
		val3.UnknownId2 = characterIdentity;
		val3.MissionIconId = 11340;
		val3.Unknown20 = 172800;
		val3.Unknown21 = 172800;
		val3.QuestActions = (QuestActionInfo[])(object)new QuestActionInfo[0];
		val3.PlayerIds = (Identity[])(object)new Identity[1] { characterIdentity };
		val3.UnknownArray1 = new int[1] { 85360450 };
		val3.UnknownArray2 = new int[0];
		val3.CharacterInfos = (CharacterInfo[])(object)new CharacterInfo[0];
		val3.Unknown22 = 6;
		val3.PlayerIds2 = (Identity[])(object)new Identity[1] { characterIdentity };
		val3.Unknown23 = 0;
		val3.Unknown24 = 105040;
		val3.UnknownId3 = Identity.None;
		val3.Unknown25 = 0;
		val3.Unknown26 = 0;
		val3.QuestIdentities = (QuestIdentity[])(object)new QuestIdentity[0];
		val3.Unknown27 = 0;
		val3.FactionInfos = (Identity[])(object)new Identity[0];
		val3.Unknown28 = 1;
		array[0] = val3;
		val2.Quests = (Quest[])(object)array;
		return val2;
	}

	internal static CharacterActionMessage CreateFlintAction59Message(Identity characterIdentity)
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
			Target = IdentityFromRaw(56003, 1427419544),
			Parameter1 = 56003,
			Parameter2 = 1427419544,
			Unknown2 = 0
		};
	}

	internal static QuestMessage CreateFlintQuestDeleteMessage(Identity characterIdentity)
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
			Mission = IdentityFromRaw(56003, 1427419544),
			Unknown2 = 0,
			Unknown3 = 0
		};
	}

	internal static CharacterActionMessage CreateB199Action59Message(Identity characterIdentity)
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
			Target = IdentityFromRaw(56003, 1427419545),
			Parameter1 = 56003,
			Parameter2 = 1427419545,
			Unknown2 = 0
		};
	}

	internal static QuestMessage CreateB199QuestDeleteMessage(Identity characterIdentity)
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
			Mission = IdentityFromRaw(56003, 1427419545),
			Unknown2 = 0,
			Unknown3 = 0
		};
	}

	internal static QuestFullUpdateMessage CreateB199PreviewMessage(Identity characterIdentity)
	{
		//IL_000b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0010: Unknown result type (might be due to invalid IL or missing references)
		//IL_0013: Unknown result type (might be due to invalid IL or missing references)
		//IL_0033: Unknown result type (might be due to invalid IL or missing references)
		//IL_0034: Unknown result type (might be due to invalid IL or missing references)
		//IL_0035: Unknown result type (might be due to invalid IL or missing references)
		//IL_003b: Expected O, but got Unknown
		//IL_003c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0054: Unknown result type (might be due to invalid IL or missing references)
		//IL_005b: Expected O, but got Unknown
		//IL_005d: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a5: Unknown result type (might be due to invalid IL or missing references)
		//IL_0153: Unknown result type (might be due to invalid IL or missing references)
		//IL_0191: Unknown result type (might be due to invalid IL or missing references)
		//IL_0192: Unknown result type (might be due to invalid IL or missing references)
		//IL_01e2: Unknown result type (might be due to invalid IL or missing references)
		//IL_01e3: Unknown result type (might be due to invalid IL or missing references)
		//IL_0206: Unknown result type (might be due to invalid IL or missing references)
		Identity questId = IdentityFromRaw(56003, 1427419545);
		Identity val = default(Identity);
		((Identity)(ref val)).Type = (IdentityType)50000;
		((Identity)(ref val)).Instance = 2016273767;
		Identity unknownId = val;
		QuestFullUpdateMessage val2 = new QuestFullUpdateMessage();
		((N3Message)val2).Identity = characterIdentity;
		((N3Message)val2).Unknown = 1;
		Quest[] array = new Quest[1];
		Quest val3 = new Quest();
		val3.QuestId = questId;
		val3.Unknown1 = 15;
		val3.Unknown2 = 0;
		val3.Unknown3 = 0;
		val3.Unknown4 = 2;
		val3.ShortInfo = "Use the Stim on a Wounded Do...";
		val3.LongInfo = "Use the Stim on a Wounded Dockworker<BR><BR>Marcus Stone's workers got damaged by the fire, he asked you to help him save their lives.<BR><BR> <font color=\"#FF0000\">Mission Objective:<BR>Target a Wounded Dockworker and use the <a href='itemref://297044/297044/1'>Health Regeneration Stim</a> (Right-Click).</font>";
		val3.UnknownId1 = unknownId;
		val3.Unknown5 = 6;
		val3.Unknown6 = 0;
		val3.Unknown7 = 0;
		val3.Unknown8 = 0;
		val3.Unknown9 = 1009;
		val3.Unknown10 = 1009;
		val3.MissionItemData = (MissionItemReward[])(object)new MissionItemReward[0];
		val3.Unknown11 = 1229076059;
		val3.Unknown12 = 0;
		val3.Unknown13 = 0;
		val3.UnknownHash1 = string.Empty;
		val3.Unknown14 = 0;
		val3.Unknown15 = 0;
		val3.Unknown16 = 0;
		val3.Unknown17 = 0;
		val3.Unknown18 = 0;
		val3.UnknownId2 = characterIdentity;
		val3.MissionIconId = 244818;
		val3.Unknown20 = 0;
		val3.Unknown21 = 0;
		val3.QuestActions = (QuestActionInfo[])(object)new QuestActionInfo[0];
		val3.PlayerIds = (Identity[])(object)new Identity[1] { characterIdentity };
		val3.UnknownArray1 = new int[1] { 85360450 };
		val3.UnknownArray2 = new int[0];
		val3.CharacterInfos = (CharacterInfo[])(object)new CharacterInfo[0];
		val3.Unknown22 = 6;
		val3.PlayerIds2 = (Identity[])(object)new Identity[1] { characterIdentity };
		val3.Unknown23 = 0;
		val3.Unknown24 = 104939;
		val3.UnknownId3 = Identity.None;
		val3.Unknown25 = 0;
		val3.Unknown26 = 0;
		val3.QuestIdentities = (QuestIdentity[])(object)new QuestIdentity[0];
		val3.Unknown27 = 7;
		val3.FactionInfos = (Identity[])(object)new Identity[0];
		val3.Unknown28 = 1;
		array[0] = val3;
		val2.Quests = (Quest[])(object)array;
		return val2;
	}

	internal static CharacterActionMessage CreateB19AAction59Message(Identity characterIdentity)
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
			Target = IdentityFromRaw(56003, 1427419546),
			Parameter1 = 56003,
			Parameter2 = 1427419546,
			Unknown2 = 0
		};
	}

	internal static QuestMessage CreateB19AQuestDeleteMessage(Identity characterIdentity)
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
			Mission = IdentityFromRaw(56003, 1427419546),
			Unknown2 = 0,
			Unknown3 = 0
		};
	}

	internal static QuestFullUpdateMessage CreateB19APreviewMessage(Identity characterIdentity)
	{
		//IL_000b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0010: Unknown result type (might be due to invalid IL or missing references)
		//IL_0013: Unknown result type (might be due to invalid IL or missing references)
		//IL_0033: Unknown result type (might be due to invalid IL or missing references)
		//IL_0034: Unknown result type (might be due to invalid IL or missing references)
		//IL_0035: Unknown result type (might be due to invalid IL or missing references)
		//IL_003b: Expected O, but got Unknown
		//IL_003c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0054: Unknown result type (might be due to invalid IL or missing references)
		//IL_005b: Expected O, but got Unknown
		//IL_005d: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a5: Unknown result type (might be due to invalid IL or missing references)
		//IL_0153: Unknown result type (might be due to invalid IL or missing references)
		//IL_0191: Unknown result type (might be due to invalid IL or missing references)
		//IL_0192: Unknown result type (might be due to invalid IL or missing references)
		//IL_01e2: Unknown result type (might be due to invalid IL or missing references)
		//IL_01e3: Unknown result type (might be due to invalid IL or missing references)
		//IL_0206: Unknown result type (might be due to invalid IL or missing references)
		Identity questId = IdentityFromRaw(56003, 1427419546);
		Identity val = default(Identity);
		((Identity)(ref val)).Type = (IdentityType)50000;
		((Identity)(ref val)).Instance = 2016273767;
		Identity unknownId = val;
		QuestFullUpdateMessage val2 = new QuestFullUpdateMessage();
		((N3Message)val2).Identity = characterIdentity;
		((N3Message)val2).Unknown = 1;
		Quest[] array = new Quest[1];
		Quest val3 = new Quest();
		val3.QuestId = questId;
		val3.Unknown1 = 15;
		val3.Unknown2 = 0;
		val3.Unknown3 = 0;
		val3.Unknown4 = 2;
		val3.ShortInfo = "Return to Marcus Stone";
		val3.LongInfo = "Return to Marcus Stone<BR><BR>Marcus Stone's workers got damaged by the fire, he asked you to help him save their lives.<BR><BR><font color=\"#FF0000\">Mission Objective:<BR>Return to Marcus Stone and hand him the <a href='itemref://297044/297044/1'>Health Regeneration Stim</a>.</font>";
		val3.UnknownId1 = unknownId;
		val3.Unknown5 = 6;
		val3.Unknown6 = 0;
		val3.Unknown7 = 0;
		val3.Unknown8 = 0;
		val3.Unknown9 = 1009;
		val3.Unknown10 = 1009;
		val3.MissionItemData = (MissionItemReward[])(object)new MissionItemReward[0];
		val3.Unknown11 = 1229076060;
		val3.Unknown12 = 0;
		val3.Unknown13 = 0;
		val3.UnknownHash1 = string.Empty;
		val3.Unknown14 = 0;
		val3.Unknown15 = 0;
		val3.Unknown16 = 0;
		val3.Unknown17 = 0;
		val3.Unknown18 = 0;
		val3.UnknownId2 = characterIdentity;
		val3.MissionIconId = 158429;
		val3.Unknown20 = 0;
		val3.Unknown21 = 0;
		val3.QuestActions = (QuestActionInfo[])(object)new QuestActionInfo[0];
		val3.PlayerIds = (Identity[])(object)new Identity[1] { characterIdentity };
		val3.UnknownArray1 = new int[1] { 85360450 };
		val3.UnknownArray2 = new int[0];
		val3.CharacterInfos = (CharacterInfo[])(object)new CharacterInfo[0];
		val3.Unknown22 = 6;
		val3.PlayerIds2 = (Identity[])(object)new Identity[1] { characterIdentity };
		val3.Unknown23 = 0;
		val3.Unknown24 = 104939;
		val3.UnknownId3 = Identity.None;
		val3.Unknown25 = 0;
		val3.Unknown26 = 0;
		val3.QuestIdentities = (QuestIdentity[])(object)new QuestIdentity[0];
		val3.Unknown27 = 7;
		val3.FactionInfos = (Identity[])(object)new Identity[0];
		val3.Unknown28 = 1;
		array[0] = val3;
		val2.Quests = (Quest[])(object)array;
		return val2;
	}

	private static Identity IdentityFromRaw(int type, int instance)
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
