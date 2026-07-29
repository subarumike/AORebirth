using System;
using System.Collections.Generic;
using System.Threading;
using AORebirth.Core.Components;
using AORebirth.Core.Entities;
using AORebirth.Core.Inventory;
using AORebirth.Core.Items;
using AORebirth.Core.Network;
using AORebirth.Enums;
using AORebirth.Interfaces;
using AORebirth.ObjectManager;
using AORebirth.Stats;
using SmokeLounge.AOtomation.Messaging.GameData;
using SmokeLounge.AOtomation.Messaging.Messages;
using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;
using Utility;
using ZoneEngine.Core.Arete.Dialogue;
using ZoneEngine.Core.Controllers;
using ZoneEngine.Core.MessageHandlers;
using ZoneEngine.Core.Missions;

namespace ZoneEngine.Core.Arete.Quests;

public static class RexMarcusChainCoordinator
{
	public enum MarcusTradeKind
	{
		Suppressant,
		Stim
	}

	private sealed class MarcusTradeSession
	{
		public Identity NpcIdentity { get; set; }

		public Identity StagedContainer { get; set; }

		public bool HasSuppressant { get; set; }

		public bool HasStim { get; set; }

		public MarcusTradeKind Kind { get; set; }
	}

	public const string RexReturnNodeId = "rex_194454_006";

	public const string MarcusFireRootNodeId = "marcus_195107_b18f_001";

	public const string MarcusReturnNodeId = "marcus_return_001";

	public const string MarcusReturnTradeNodeId = "marcus_return_trade";

	public const string MarcusPostCompleteNodeId = "marcus_return_003";

	public const string MarcusHealReturnNodeId = "marcus_heal_001";

	public const string MarcusHealTradeNodeId = "marcus_heal_trade";

	private const int AreteLandingPlayfieldId = 6553;

	private const int RexLarssonInstance = 2016273768;

	private const int MarcusStoneInstance = 2016273767;

	private const int CargoBoxInstance = 1457108143;

	private const int CompactFireSuppressantItemId = 296780;

	private const int MarcusReturnIdentityCardItemId = 296569;

	private const int MarcusReturnXpReward = 1281;

	private const int MarcusReturnCreditReward = 1080;

	private const string MarcusReturnRewardFeedback = "~&!!!\":$'O\"ui!!!0'i!!!-]~";

	private const string CargoRejectFeedback = "~&!!!\":!o[Im";

	private const string MarcusReturnCardGrantedFlag = "marcus-return-item-296569";

	private static readonly Dictionary<int, MarcusTradeSession> TradeSessionsByCharacter = new Dictionary<int, MarcusTradeSession>();

	private static readonly HashSet<int> TurnInInFlightByCharacter = new HashSet<int>();

	private static readonly object TradeSyncRoot = new object();

	public static RexMarcusChainPhase GetPhase(ICharacter character)
	{
		//IL_000f: Unknown result type (might be due to invalid IL or missing references)
		if (character == null)
		{
			return RexMarcusChainPhase.None;
		}
		return GetPhase(((IEntity)character).Identity);
	}

	public static bool TryResendActiveTipsForLogin(ICharacter source)
	{
		if (source == null || !MissionRuntime.IsInitialized)
		{
			return false;
		}
		bool result = ResendActiveTipsNow(source, "login-immediate");
		ICharacter captured = source;
		ThreadPool.QueueUserWorkItem(delegate
		{
			try
			{
				Thread.Sleep(900);
				ResendActiveTipsNow(captured, "login-delayed");
			}
			catch (Exception ex)
			{
				Log("login tip delayed resync failed: " + ex.Message);
			}
		});
		return result;
	}

	private static bool ResendActiveTipsNow(ICharacter source, string trigger)
	{
		//IL_0034: Unknown result type (might be due to invalid IL or missing references)
		//IL_0039: Unknown result type (might be due to invalid IL or missing references)
		//IL_0143: Unknown result type (might be due to invalid IL or missing references)
		//IL_0148: Unknown result type (might be due to invalid IL or missing references)
		if (source == null || ((IDynel)source).Controller == null || ((IDynel)source).Controller.Client == null || !MissionRuntime.IsInitialized)
		{
			return false;
		}
		Identity identity = ((IEntity)source).Identity;
		int instance = ((Identity)(ref identity)).Instance;
		EnsureFlintPersistenceForChain(source);
		ZoneEngine.Core.Missions.MissionStateRecord mission = MissionRuntime.Service.GetMission(instance, "Mission:5514B198");
		ZoneEngine.Core.Missions.MissionStateRecord mission2 = MissionRuntime.Service.GetMission(instance, "Mission:5514B199");
		ZoneEngine.Core.Missions.MissionStateRecord mission3 = MissionRuntime.Service.GetMission(instance, "Mission:5514B19A");
		bool flag = false;
		if (FlintBioComQuestRuntime.TryResendActiveTip(source))
		{
			flag = true;
		}
		else if (IsActiveOrOffered(mission))
		{
			ReanchorGameTimeForTipJournal(source);
			RexQuestPreviewEmissionResult rexQuestPreviewEmissionResult = SafeQuestFullUpdateSender.TrySendFlintPreview(source);
			flag |= rexQuestPreviewEmissionResult?.Emitted ?? false;
		}
		if (IsActiveOrOffered(mission3) && !MarcusWoundedWorkersQuestRuntime.HasCompletedStimReturn(source))
		{
			RexQuestPreviewEmissionResult rexQuestPreviewEmissionResult2 = SafeQuestFullUpdateSender.TrySendB19APreview(source);
			flag |= rexQuestPreviewEmissionResult2?.Emitted ?? false;
		}
		else if (IsActiveOrOffered(mission2))
		{
			RexQuestPreviewEmissionResult rexQuestPreviewEmissionResult3 = SafeQuestFullUpdateSender.TrySendB199Preview(source);
			flag |= rexQuestPreviewEmissionResult3?.Emitted ?? false;
		}
		string[] obj = new string[12]
		{
			"tip resync trigger=", trigger, " character=", null, null, null, null, null, null, null,
			null, null
		};
		identity = ((IEntity)source).Identity;
		obj[3] = ((Identity)(ref identity)).ToString(true);
		obj[4] = " flintState=";
		obj[5] = ((mission == null) ? "missing" : mission.State.ToString());
		obj[6] = " b199=";
		obj[7] = IsActiveOrOffered(mission2).ToString();
		obj[8] = " b19a=";
		obj[9] = IsActiveOrOffered(mission3).ToString();
		obj[10] = " sent=";
		obj[11] = flag.ToString();
		Log(string.Concat(obj));
		return flag;
	}

	private static void EnsureFlintPersistenceForChain(ICharacter source)
	{
		//IL_001c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0021: Unknown result type (might be due to invalid IL or missing references)
		if (source == null || !MissionRuntime.IsInitialized)
		{
			return;
		}
		Identity identity = ((IEntity)source).Identity;
		int instance = ((Identity)(ref identity)).Instance;
		ZoneEngine.Core.Missions.MissionStateRecord mission = MissionRuntime.Service.GetMission(instance, "Mission:5514B198");
		if (IsCompleted(mission) || IsActiveOrOffered(mission))
		{
			if (mission != null && mission.State == MissionLifecycleState.Offered)
			{
				MissionRuntime.Service.AcceptMission(instance, "Mission:5514B198");
			}
			return;
		}
		ZoneEngine.Core.Missions.MissionStateRecord mission2 = MissionRuntime.Service.GetMission(instance, "Mission:5514B196");
		if (!IsCompleted(mission2) && GetPhase(source) != RexMarcusChainPhase.Flint)
		{
			return;
		}
		try
		{
			if (mission == null)
			{
				MissionOperationResult missionOperationResult = MissionRuntime.Service.OfferMission(instance, "Mission:5514B198");
				Log("ensure Flint Offer status=" + ((missionOperationResult == null) ? "null" : missionOperationResult.Status.ToString()));
			}
			MissionOperationResult missionOperationResult2 = MissionRuntime.Service.AcceptMission(instance, "Mission:5514B198");
			Log("ensure Flint Accept status=" + ((missionOperationResult2 == null) ? "null" : missionOperationResult2.Status.ToString()));
		}
		catch (Exception ex)
		{
			Log("ensure Flint persistence failed: " + ex.Message);
		}
	}

	private static void ReanchorGameTimeForTipJournal(ICharacter source)
	{
		//IL_0032: Unknown result type (might be due to invalid IL or missing references)
		//IL_0037: Unknown result type (might be due to invalid IL or missing references)
		//IL_003a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0050: Unknown result type (might be due to invalid IL or missing references)
		//IL_0055: Unknown result type (might be due to invalid IL or missing references)
		//IL_0063: Unknown result type (might be due to invalid IL or missing references)
		//IL_006a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0076: Unknown result type (might be due to invalid IL or missing references)
		//IL_0082: Unknown result type (might be due to invalid IL or missing references)
		//IL_0093: Expected O, but got Unknown
		ZoneClient zoneClient = ((source != null && ((IDynel)source).Controller != null) ? (((IDynel)source).Controller.Client as ZoneClient) : null);
		if (zoneClient != null && source != null)
		{
			GameTimeMessage val = new GameTimeMessage();
			Identity identity = default(Identity);
			((Identity)(ref identity)).Type = (IdentityType)50000;
			Identity identity2 = ((IEntity)source).Identity;
			((Identity)(ref identity)).Instance = ((Identity)(ref identity2)).Instance;
			((N3Message)val).Identity = identity;
			val.Unknown1 = 30024f;
			val.Unknown3 = 185408;
			val.Unknown4 = 80183.31f;
			zoneClient.SendCompressed((MessageBody)val);
			zoneClient.LastGameTimeSyncUtc = DateTime.UtcNow;
		}
	}

	public static RexMarcusChainPhase GetPhase(Identity identity)
	{
		//IL_000a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0014: Invalid comparison between Unknown and I4
		if (!MissionRuntime.IsInitialized || (int)((Identity)(ref identity)).Type != 50000 || ((Identity)(ref identity)).Instance == 0)
		{
			return RexMarcusChainPhase.None;
		}
		int instance = ((Identity)(ref identity)).Instance;
		ZoneEngine.Core.Missions.MissionStateRecord mission = MissionRuntime.Service.GetMission(instance, "Mission:5514B19A");
		ZoneEngine.Core.Missions.MissionStateRecord mission2 = MissionRuntime.Service.GetMission(instance, "Mission:5514B199");
		ZoneEngine.Core.Missions.MissionStateRecord mission3 = MissionRuntime.Service.GetMission(instance, "Mission:5514B198");
		ZoneEngine.Core.Missions.MissionStateRecord mission4 = MissionRuntime.Service.GetMission(instance, "Mission:5514B196");
		ZoneEngine.Core.Missions.MissionStateRecord mission5 = MissionRuntime.Service.GetMission(instance, "Mission:5514B194");
		ZoneEngine.Core.Missions.MissionStateRecord mission6 = MissionRuntime.Service.GetMission(instance, "Mission:5514B18F");
		ZoneEngine.Core.Missions.MissionStateRecord mission7 = MissionRuntime.Service.GetMission(instance, "Mission:5514B18E");
		ZoneEngine.Core.Missions.MissionStateRecord mission8 = MissionRuntime.Service.GetMission(instance, "Mission:5514B18D");
		ZoneEngine.Core.Missions.MissionStateRecord mission9 = MissionRuntime.Service.GetMission(instance, "Mission:5514B18C");
		if (IsActiveOrOffered(mission))
		{
			return RexMarcusChainPhase.ReturnMarcusStim;
		}
		if (IsActiveOrOffered(mission2))
		{
			return RexMarcusChainPhase.HealWorkers;
		}
		if (IsActiveOrOffered(mission3))
		{
			return RexMarcusChainPhase.Flint;
		}
		if (IsActiveOrOffered(mission4))
		{
			return RexMarcusChainPhase.ReturnMarcus;
		}
		if (IsActiveOrOffered(mission5))
		{
			return RexMarcusChainPhase.Extinguish;
		}
		if (IsActiveOrOffered(mission6))
		{
			return RexMarcusChainPhase.TalkMarcus;
		}
		if (IsActiveOrOffered(mission7))
		{
			return RexMarcusChainPhase.ReturnRex;
		}
		if (IsActiveOrOffered(mission8))
		{
			return RexMarcusChainPhase.Cargo;
		}
		if (IsActiveOrOffered(mission9))
		{
			return RexMarcusChainPhase.Robots;
		}
		if (IsCompleted(mission3))
		{
			return RexMarcusChainPhase.Done;
		}
		if (IsCompleted(mission4))
		{
			return RexMarcusChainPhase.Flint;
		}
		if (IsCompleted(mission5))
		{
			return RexMarcusChainPhase.ReturnMarcus;
		}
		if (IsCompleted(mission6))
		{
			return RexMarcusChainPhase.Extinguish;
		}
		if (IsCompleted(mission7))
		{
			return RexMarcusChainPhase.TalkMarcus;
		}
		if (IsCompleted(mission8))
		{
			return RexMarcusChainPhase.ReturnRex;
		}
		if (IsCompleted(mission9))
		{
			return RexMarcusChainPhase.Cargo;
		}
		return RexMarcusChainPhase.None;
	}

	public static string ResolveRexStartNodeId(ICharacter source)
	{
		RexMarcusChainPhase phase = GetPhase(source);
		if (phase == RexMarcusChainPhase.ReturnRex)
		{
			return "rex_194454_006";
		}
		return null;
	}

	public static string ResolveMarcusStartNodeId(ICharacter source)
	{
		CleanupStaleMarcusClientTips(source);
		EnsureReturnMarcusPersistence(source);
		switch (GetPhase(source))
		{
		case RexMarcusChainPhase.ReturnMarcus:
			return "marcus_return_001";
		case RexMarcusChainPhase.TalkMarcus:
			return "marcus_195107_b18f_001";
		case RexMarcusChainPhase.ReturnMarcusStim:
			return "marcus_heal_001";
		case RexMarcusChainPhase.HealWorkers:
		case RexMarcusChainPhase.Flint:
		case RexMarcusChainPhase.Done:
			return "marcus_return_003";
		case RexMarcusChainPhase.Extinguish:
			return null;
		default:
			return null;
		}
	}

	public static void OnRexOpen(ICharacter source, bool dialogueGateEnabled)
	{
		//IL_0047: Unknown result type (might be due to invalid IL or missing references)
		//IL_0067: Unknown result type (might be due to invalid IL or missing references)
		if (source != null)
		{
			RexMarcusChainPhase phase = GetPhase(source);
			Log("rex-open phase=" + phase.ToString() + " character=" + IdentityText(source));
			if (phase == RexMarcusChainPhase.ReturnRex)
			{
				Identity npcIdentity = default(Identity);
				((Identity)(ref npcIdentity)).Type = (IdentityType)50000;
				((Identity)(ref npcIdentity)).Instance = 2016273768;
				RexB18ECompletionHandler.TryCompleteOnReturn(source, npcIdentity, dialogueGateEnabled);
			}
			else if (phase >= RexMarcusChainPhase.TalkMarcus)
			{
				SafeQuestFullUpdateSender.TrySendB18EToB18FHandoff(source);
			}
		}
	}

	public static void OnMarcusOpen(ICharacter source)
	{
		//IL_0076: Unknown result type (might be due to invalid IL or missing references)
		//IL_0096: Unknown result type (might be due to invalid IL or missing references)
		//IL_0097: Unknown result type (might be due to invalid IL or missing references)
		RexMarcusChainPhase phase = GetPhase(source);
		Log("marcus-open phase=" + phase.ToString() + " character=" + IdentityText(source));
		if (phase >= RexMarcusChainPhase.TalkMarcus)
		{
			SafeQuestFullUpdateSender.TrySendB18EQuestDelete(source);
		}
		EnsureReturnMarcusPersistence(source);
		CleanupStaleMarcusClientTips(source);
		if (ShouldRepairStolenSuppressantTurnIn(source))
		{
			Log("marcus-open stolen-suppressant repair character=" + IdentityText(source));
			Identity marcusTarget = default(Identity);
			((Identity)(ref marcusTarget)).Type = (IdentityType)50000;
			((Identity)(ref marcusTarget)).Instance = 2016273767;
			ApplyMarcusTradeTurnIn(source, marcusTarget, Identity.None, "MarcusOpenRepair");
		}
	}

	private static void CleanupStaleMarcusClientTips(ICharacter source)
	{
		//IL_001c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0021: Unknown result type (might be due to invalid IL or missing references)
		if (source == null || !MissionRuntime.IsInitialized)
		{
			return;
		}
		Identity identity = ((IEntity)source).Identity;
		int instance = ((Identity)(ref identity)).Instance;
		ZoneEngine.Core.Missions.MissionStateRecord mission = MissionRuntime.Service.GetMission(instance, "Mission:5514B196");
		ZoneEngine.Core.Missions.MissionStateRecord mission2 = MissionRuntime.Service.GetMission(instance, "Mission:5514B19A");
		ZoneEngine.Core.Missions.MissionStateRecord mission3 = MissionRuntime.Service.GetMission(instance, "Mission:5514B199");
		RexMarcusChainPhase phase = GetPhase(source);
		if (phase == RexMarcusChainPhase.Flint || phase == RexMarcusChainPhase.HealWorkers || phase == RexMarcusChainPhase.ReturnMarcusStim || phase == RexMarcusChainPhase.Done || IsActiveOrOffered(mission3) || IsActiveOrOffered(mission2) || IsCompleted(mission3) || IsCompleted(mission2) || IsCompleted(mission))
		{
			if (IsActiveOrOffered(mission) && (IsActiveOrOffered(mission3) || IsActiveOrOffered(mission2) || IsCompleted(mission3) || IsCompleted(mission2) || phase == RexMarcusChainPhase.Flint || phase == RexMarcusChainPhase.HealWorkers || phase == RexMarcusChainPhase.ReturnMarcusStim || phase == RexMarcusChainPhase.Done))
			{
				try
				{
					MissionRuntime.Service.CompleteMission(instance, "Mission:5514B196");
				}
				catch (Exception ex)
				{
					Log("cleanup stale B196 complete failed: " + ex.Message);
				}
			}
			SafeQuestFullUpdateSender.TrySendB196QuestDelete(source);
		}
		if (MarcusWoundedWorkersQuestRuntime.HasCompletedStimReturn(source) || IsCompleted(mission2))
		{
			if (IsActiveOrOffered(mission2))
			{
				try
				{
					MissionRuntime.Service.AbandonMission(instance, "Mission:5514B19A");
				}
				catch (Exception ex2)
				{
					Log("cleanup finished B19A abandon failed: " + ex2.Message);
				}
			}
			SafeQuestFullUpdateSender.TrySendB19ACompletionCleanup(source);
			SafeQuestFullUpdateSender.TrySendB19AQuestDeleteOnly(source);
		}
		else if (phase == RexMarcusChainPhase.HealWorkers || IsActiveOrOffered(mission3))
		{
			if (IsActiveOrOffered(mission2))
			{
				try
				{
					MissionRuntime.Service.AbandonMission(instance, "Mission:5514B19A");
				}
				catch (Exception ex3)
				{
					Log("cleanup premature B19A abandon failed: " + ex3.Message);
				}
			}
			SafeQuestFullUpdateSender.TrySendB19AQuestDeleteOnly(source);
			SafeQuestFullUpdateSender.TrySendB199Preview(source);
		}
		else if (phase == RexMarcusChainPhase.ReturnMarcusStim || IsActiveOrOffered(mission2))
		{
			SafeQuestFullUpdateSender.TrySendB19APreview(source);
		}
	}

	private static void EnsureReturnMarcusPersistence(ICharacter source)
	{
		//IL_001c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0021: Unknown result type (might be due to invalid IL or missing references)
		if (source == null || !MissionRuntime.IsInitialized)
		{
			return;
		}
		Identity identity = ((IEntity)source).Identity;
		int instance = ((Identity)(ref identity)).Instance;
		ZoneEngine.Core.Missions.MissionStateRecord mission = MissionRuntime.Service.GetMission(instance, "Mission:5514B196");
		if (IsActiveOrOffered(mission) || IsCompleted(mission))
		{
			return;
		}
		ZoneEngine.Core.Missions.MissionStateRecord mission2 = MissionRuntime.Service.GetMission(instance, "Mission:5514B198");
		ZoneEngine.Core.Missions.MissionStateRecord mission3 = MissionRuntime.Service.GetMission(instance, "Mission:5514B199");
		ZoneEngine.Core.Missions.MissionStateRecord mission4 = MissionRuntime.Service.GetMission(instance, "Mission:5514B19A");
		if (!IsActiveOrOffered(mission2) && !IsCompleted(mission2) && !IsActiveOrOffered(mission3) && !IsCompleted(mission3) && !IsActiveOrOffered(mission4) && !IsCompleted(mission4))
		{
			ZoneEngine.Core.Missions.MissionStateRecord mission5 = MissionRuntime.Service.GetMission(instance, "Mission:5514B194");
			if (IsCompleted(mission5))
			{
				MissionRuntime.Service.OfferMission(instance, "Mission:5514B196");
				MissionRuntime.Service.AcceptMission(instance, "Mission:5514B196");
				SafeQuestFullUpdateSender.TrySendB194ToB196Handoff(source);
				Log("ensure-return-marcus character=" + IdentityText(source));
			}
		}
	}

	public static bool OnRexAnswer(ICharacter source, string previousNodeId, int answerIndex, bool dialogueGateEnabled)
	{
		//IL_002e: Unknown result type (might be due to invalid IL or missing references)
		//IL_004e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0098: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b8: Unknown result type (might be due to invalid IL or missing references)
		if (source == null)
		{
			return false;
		}
		Identity npcIdentity;
		if (string.Equals(previousNodeId, "rex_194454_004", StringComparison.OrdinalIgnoreCase) && answerIndex == 0)
		{
			npcIdentity = default(Identity);
			((Identity)(ref npcIdentity)).Type = (IdentityType)50000;
			((Identity)(ref npcIdentity)).Instance = 2016273768;
			return RexQuestPreviewEmitter.TryEmitB18CPreview(source, npcIdentity, previousNodeId, answerIndex, dialogueGateEnabled)?.Emitted ?? false;
		}
		if (IsRexReturnPathNode(previousNodeId) && answerIndex == 0)
		{
			RexMarcusChainPhase phase = GetPhase(source);
			if (phase == RexMarcusChainPhase.ReturnRex)
			{
				npcIdentity = default(Identity);
				((Identity)(ref npcIdentity)).Type = (IdentityType)50000;
				((Identity)(ref npcIdentity)).Instance = 2016273768;
				return RexB18ECompletionHandler.TryCompleteOnReturn(source, npcIdentity, dialogueGateEnabled)?.Completed ?? false;
			}
			if (phase >= RexMarcusChainPhase.TalkMarcus)
			{
				return SafeQuestFullUpdateSender.TrySendB18EToB18FHandoff(source)?.Emitted ?? false;
			}
		}
		return false;
	}

	private static bool IsRexReturnPathNode(string nodeId)
	{
		return string.Equals(nodeId, "rex_194454_006", StringComparison.OrdinalIgnoreCase) || string.Equals(nodeId, "rex_194454_007", StringComparison.OrdinalIgnoreCase) || string.Equals(nodeId, "rex_194454_008", StringComparison.OrdinalIgnoreCase);
	}

	public static bool OnMarcusAnswer(ICharacter source, string previousNodeId, int answerIndex, string optionText, bool dialogueGateEnabled)
	{
		//IL_003d: Unknown result type (might be due to invalid IL or missing references)
		//IL_005d: Unknown result type (might be due to invalid IL or missing references)
		if (source == null)
		{
			return false;
		}
		if (MarcusWoundedWorkersQuestRuntime.TryHandleDialogueAnswer(source, previousNodeId, answerIndex))
		{
			return true;
		}
		RexMarcusChainPhase phase = GetPhase(source);
		if (phase != RexMarcusChainPhase.TalkMarcus)
		{
			return false;
		}
		Identity npcIdentity = default(Identity);
		((Identity)(ref npcIdentity)).Type = (IdentityType)50000;
		((Identity)(ref npcIdentity)).Instance = 2016273767;
		return MarcusB18FCompletionHandler.TryCompleteFromDialogue(source, npcIdentity, previousNodeId, answerIndex, optionText, dialogueGateEnabled)?.Completed ?? false;
	}

	public static bool TryBeginMarcusReturnTrade(ICharacter source, Identity marcusIdentity)
	{
		//IL_0013: Unknown result type (might be due to invalid IL or missing references)
		//IL_001d: Invalid comparison between Unknown and I4
		//IL_0058: Unknown result type (might be due to invalid IL or missing references)
		//IL_0066: Unknown result type (might be due to invalid IL or missing references)
		//IL_0033: Unknown result type (might be due to invalid IL or missing references)
		//IL_0053: Unknown result type (might be due to invalid IL or missing references)
		//IL_0054: Unknown result type (might be due to invalid IL or missing references)
		if (source == null)
		{
			return false;
		}
		if ((int)((Identity)(ref marcusIdentity)).Type != 50000 || ((Identity)(ref marcusIdentity)).Instance == 0)
		{
			Identity val = default(Identity);
			((Identity)(ref val)).Type = (IdentityType)50000;
			((Identity)(ref val)).Instance = 2016273767;
			marcusIdentity = val;
		}
		BeginMarcusTrade(source, marcusIdentity, MarcusTradeKind.Suppressant);
		BaseMessageHandler<KnuBotStartTradeMessage, KnuBotStartTradeMessageHandler>.Default.Send(source, marcusIdentity, "Drag and drop the item(s) you want to give to Marcus Stone into one of the slots available and press \"accept\"", 1);
		Log("marcus-return-trade-opened character=" + IdentityText(source) + " target=" + ((Identity)(ref marcusIdentity)).ToString(true) + " phase=" + GetPhase(source));
		return true;
	}

	public static void BeginMarcusTradeSession(ICharacter source, Identity marcusIdentity, MarcusTradeKind kind)
	{
		//IL_0002: Unknown result type (might be due to invalid IL or missing references)
		BeginMarcusTrade(source, marcusIdentity, kind);
	}

	public static bool OnCargoUse(ICharacter source, Identity target)
	{
		//IL_0001: Unknown result type (might be due to invalid IL or missing references)
		//IL_002f: Unknown result type (might be due to invalid IL or missing references)
		if (!IsCargoBoxTarget(target) || source == null)
		{
			return false;
		}
		if (GetPhase(source) != RexMarcusChainPhase.Cargo)
		{
			return false;
		}
		RexB18DBoxProgressTracker.TryObserveBoxUse(source, target);
		return true;
	}

	public static bool TryRejectCargoWithoutQuest(IZoneClient client, GenericCmdMessage message, Identity target)
	{
		//IL_014e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0153: Unknown result type (might be due to invalid IL or missing references)
		//IL_0007: Unknown result type (might be due to invalid IL or missing references)
		//IL_0069: Unknown result type (might be due to invalid IL or missing references)
		//IL_006e: Unknown result type (might be due to invalid IL or missing references)
		//IL_00cd: Unknown result type (might be due to invalid IL or missing references)
		//IL_00d2: Unknown result type (might be due to invalid IL or missing references)
		//IL_00d4: Unknown result type (might be due to invalid IL or missing references)
		//IL_00df: Unknown result type (might be due to invalid IL or missing references)
		//IL_00e7: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ef: Unknown result type (might be due to invalid IL or missing references)
		//IL_00fb: Unknown result type (might be due to invalid IL or missing references)
		//IL_0108: Expected O, but got Unknown
		if (client == null || message == null || !IsCargoBoxTarget(target))
		{
			return false;
		}
		ICharacter val = ((client.Controller != null) ? client.Controller.Character : null);
		if (val == null || !(((IDynel)val).Controller is PlayerController))
		{
			return false;
		}
		if (((IInstancedEntity)val).Playfield != null)
		{
			Identity identity = ((IEntity)((IInstancedEntity)val).Playfield).Identity;
			if (((Identity)(ref identity)).Instance == 6553)
			{
				RexMarcusChainPhase phase = GetPhase(val);
				if (phase == RexMarcusChainPhase.Cargo)
				{
					return false;
				}
				try
				{
					if (((IDynel)val).Controller.Client != null)
					{
						((IDynel)val).Controller.Client.SendCompressed((MessageBody)new FormatFeedbackMessage
						{
							Identity = ((IEntity)val).Identity,
							Unknown = 1,
							Unknown1 = 0,
							FormattedMessage = "~&!!!\":!o[Im",
							Unknown2 = 0
						});
					}
				}
				catch (Exception ex)
				{
					LogUtil.Debug((DebugInfoDetail)512, "ARETE_REX_CARGO_REJECT feedback failed: " + ex.Message);
				}
				BaseMessageHandler<GenericCmdMessage, GenericCmdMessageHandler>.Default.AcknowledgeDenied(val, message);
				string[] obj = new string[7] { "cargo-reject-without-quest character=", null, null, null, null, null, null };
				identity = ((IEntity)val).Identity;
				obj[1] = ((Identity)(ref identity)).ToString(true);
				obj[2] = " phase=";
				obj[3] = phase.ToString();
				obj[4] = " target=";
				obj[5] = ((Identity)(ref target)).ToString(true);
				obj[6] = " feedback=\"~&!!!\":!o[Im\"";
				Log(string.Concat(obj));
				return true;
			}
		}
		return false;
	}

	public static bool TryStageMarcusTradeItem(ICharacter source, KnuBotTradeMessage message)
	{
		//IL_001c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0046: Unknown result type (might be due to invalid IL or missing references)
		//IL_0113: Unknown result type (might be due to invalid IL or missing references)
		//IL_00f9: Unknown result type (might be due to invalid IL or missing references)
		//IL_01e9: Unknown result type (might be due to invalid IL or missing references)
		//IL_01ee: Unknown result type (might be due to invalid IL or missing references)
		//IL_0156: Unknown result type (might be due to invalid IL or missing references)
		//IL_0163: Unknown result type (might be due to invalid IL or missing references)
		//IL_0314: Unknown result type (might be due to invalid IL or missing references)
		//IL_031a: Unknown result type (might be due to invalid IL or missing references)
		if (message == null || source == null)
		{
			return false;
		}
		bool flag = IsMarcusStoneNpc(source, message.Target);
		MarcusTradeSession tradeSession = GetTradeSession(source);
		bool flag2 = IsMarcusReturnTip(source);
		bool flag3 = MarcusWoundedWorkersQuestRuntime.IsStimReturnTip(source);
		bool flag4 = ShouldRepairStolenSuppressantTurnIn(source);
		IItem val = TryGetTradeContainerItem(source, message.Container);
		bool flag5 = IsSuppressantItem(val);
		bool flag6 = MarcusWoundedWorkersQuestRuntime.IsHealthRegenStim(val);
		if (!flag && tradeSession == null && !flag2 && !flag3 && !flag4 && !flag5 && !flag6)
		{
			return false;
		}
		if (!flag && tradeSession == null && !flag2 && !flag3 && !flag4 && !flag5 && !flag6)
		{
			return false;
		}
		MarcusTradeKind marcusTradeKind = ((flag3 || flag6) ? MarcusTradeKind.Stim : MarcusTradeKind.Suppressant);
		if (tradeSession != null && tradeSession.Kind == MarcusTradeKind.Stim)
		{
			marcusTradeKind = MarcusTradeKind.Stim;
		}
		if (tradeSession == null)
		{
			BeginMarcusTrade(source, message.Target, marcusTradeKind);
			tradeSession = GetTradeSession(source);
		}
		else
		{
			tradeSession.NpcIdentity = message.Target;
			if (marcusTradeKind == MarcusTradeKind.Stim)
			{
				tradeSession.Kind = MarcusTradeKind.Stim;
			}
		}
		if (tradeSession != null)
		{
			lock (TradeSyncRoot)
			{
				tradeSession.StagedContainer = message.Container;
				tradeSession.NpcIdentity = message.Target;
				if (flag5 || val != null || flag2 || flag4)
				{
					tradeSession.HasSuppressant = true;
				}
				if (flag6 || flag3)
				{
					tradeSession.HasStim = true;
				}
			}
		}
		string[] obj = new string[22]
		{
			"marcus-trade-stage character=",
			IdentityText(source),
			" marcus=",
			flag.ToString(),
			" container=",
			null,
			null,
			null,
			null,
			null,
			null,
			null,
			null,
			null,
			null,
			null,
			null,
			null,
			null,
			null,
			null,
			null
		};
		Identity container = message.Container;
		obj[5] = ((Identity)(ref container)).ToString(true);
		obj[6] = " item=";
		obj[7] = ((val == null) ? "<null>" : (val.LowID + "/" + val.HighID));
		obj[8] = " suppressant=";
		obj[9] = flag5.ToString();
		obj[10] = " stim=";
		obj[11] = flag6.ToString();
		obj[12] = " kind=";
		obj[13] = marcusTradeKind.ToString();
		obj[14] = " returnTip=";
		obj[15] = flag2.ToString();
		obj[16] = " stimReturnTip=";
		obj[17] = flag3.ToString();
		obj[18] = " stolenRepair=";
		obj[19] = flag4.ToString();
		obj[20] = " phase=";
		obj[21] = GetPhase(source).ToString();
		Log(string.Concat(obj));
		if (marcusTradeKind == MarcusTradeKind.Stim || flag3 || flag6)
		{
			return true;
		}
		if (flag || tradeSession != null || flag2 || flag4 || flag5)
		{
			ApplyMarcusTradeTurnIn(source, message.Target, message.Container, "KnuBotTrade");
		}
		return true;
	}

	public static bool TryFinishMarcusTrade(ICharacter source, KnuBotFinishTradeMessage message)
	{
		//IL_001c: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a7: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c7: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c0: Unknown result type (might be due to invalid IL or missing references)
		//IL_00cc: Unknown result type (might be due to invalid IL or missing references)
		//IL_00f6: Unknown result type (might be due to invalid IL or missing references)
		//IL_00fb: Unknown result type (might be due to invalid IL or missing references)
		//IL_00de: Unknown result type (might be due to invalid IL or missing references)
		//IL_00e3: Unknown result type (might be due to invalid IL or missing references)
		if (message == null || source == null)
		{
			return false;
		}
		bool flag = IsMarcusStoneNpc(source, message.Target);
		MarcusTradeSession tradeSession = GetTradeSession(source);
		bool flag2 = IsMarcusReturnTip(source);
		bool flag3 = MarcusWoundedWorkersQuestRuntime.IsStimReturnTip(source);
		bool flag4 = ShouldRepairStolenSuppressantTurnIn(source);
		if (!flag && tradeSession == null && !flag2 && !flag3 && !flag4)
		{
			return false;
		}
		if (message.Decline != 0)
		{
			ForgetTradeSession(source);
			return true;
		}
		MarcusTradeKind marcusTradeKind = (MarcusTradeKind)(((int?)tradeSession?.Kind) ?? (flag3 ? 1 : 0));
		if (tradeSession == null)
		{
			BeginMarcusTrade(source, message.Target, marcusTradeKind);
			tradeSession = GetTradeSession(source);
		}
		Identity stagedContainer = tradeSession?.StagedContainer ?? Identity.None;
		if (marcusTradeKind == MarcusTradeKind.Stim || flag3)
		{
			ApplyStimTradeTurnIn(source, message.Target, stagedContainer, "KnuBotFinishTrade");
		}
		else
		{
			ApplyMarcusTradeTurnIn(source, message.Target, stagedContainer, "KnuBotFinishTrade");
		}
		return true;
	}

	private static bool ShouldRepairStolenSuppressantTurnIn(ICharacter source)
	{
		//IL_004a: Unknown result type (might be due to invalid IL or missing references)
		//IL_004f: Unknown result type (might be due to invalid IL or missing references)
		if (source == null || !MissionRuntime.IsInitialized)
		{
			return false;
		}
		RexMarcusChainPhase phase = GetPhase(source);
		if (phase == RexMarcusChainPhase.Flint || phase == RexMarcusChainPhase.Done || phase == RexMarcusChainPhase.HealWorkers || phase == RexMarcusChainPhase.ReturnMarcusStim)
		{
			return false;
		}
		Identity identity = ((IEntity)source).Identity;
		int instance = ((Identity)(ref identity)).Instance;
		ZoneEngine.Core.Missions.MissionStateRecord mission = MissionRuntime.Service.GetMission(instance, "Mission:5514B196");
		if (IsCompleted(mission))
		{
			return false;
		}
		ZoneEngine.Core.Missions.MissionStateRecord mission2 = MissionRuntime.Service.GetMission(instance, "Mission:5514B199");
		ZoneEngine.Core.Missions.MissionStateRecord mission3 = MissionRuntime.Service.GetMission(instance, "Mission:5514B19A");
		if (IsActiveOrOffered(mission2) || IsCompleted(mission2) || IsActiveOrOffered(mission3) || IsCompleted(mission3))
		{
			return false;
		}
		ZoneEngine.Core.Missions.MissionStateRecord mission4 = MissionRuntime.Service.GetMission(instance, "Mission:5514B194");
		if (!IsCompleted(mission4) && phase < RexMarcusChainPhase.ReturnMarcus)
		{
			return false;
		}
		ZoneEngine.Core.Missions.MissionStateRecord mission5 = MissionRuntime.Service.GetMission(instance, "Mission:5514B198");
		if (IsActiveOrOffered(mission5) || IsCompleted(mission5))
		{
			return false;
		}
		if (InventoryContainerRuntimeService.Default.CharacterHasItemInCarriedInventory(source, 296569))
		{
			return false;
		}
		if (MissionRuntime.Service.GetFlag(instance, "Mission:5514B196", "marcus-return-item-296569") != null)
		{
			return false;
		}
		return !InventoryContainerRuntimeService.Default.CharacterHasItemInCarriedInventory(source, 296780);
	}

	private static bool IsMarcusReturnTip(ICharacter source)
	{
		//IL_0043: Unknown result type (might be due to invalid IL or missing references)
		//IL_0048: Unknown result type (might be due to invalid IL or missing references)
		if (source == null)
		{
			return false;
		}
		RexMarcusChainPhase phase = GetPhase(source);
		if (phase == RexMarcusChainPhase.ReturnMarcus)
		{
			return true;
		}
		if (!MissionRuntime.IsInitialized)
		{
			return false;
		}
		Identity identity = ((IEntity)source).Identity;
		int instance = ((Identity)(ref identity)).Instance;
		if (IsActiveOrOffered(MissionRuntime.Service.GetMission(instance, "Mission:5514B196")))
		{
			return true;
		}
		ZoneEngine.Core.Missions.MissionStateRecord mission = MissionRuntime.Service.GetMission(instance, "Mission:5514B194");
		ZoneEngine.Core.Missions.MissionStateRecord mission2 = MissionRuntime.Service.GetMission(instance, "Mission:5514B198");
		return IsCompleted(mission) && !IsActiveOrOffered(mission2) && !IsCompleted(mission2);
	}

	private static bool IsSuppressantItem(IItem item)
	{
		return item != null && (item.LowID == 296780 || item.HighID == 296780);
	}

	private static IItem TryGetTradeContainerItem(ICharacter source, Identity container)
	{
		//IL_000e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0014: Invalid comparison between Unknown and I4
		//IL_0031: Unknown result type (might be due to invalid IL or missing references)
		//IL_003d: Expected I4, but got Unknown
		if (source == null || ((IItemContainer)source).BaseInventory == null || (int)((Identity)(ref container)).Type == 0)
		{
			return null;
		}
		try
		{
			if (!((IItemContainer)source).BaseInventory.Pages.TryGetValue((int)((Identity)(ref container)).Type, out var value) || value == null)
			{
				return null;
			}
			return value[((Identity)(ref container)).Instance];
		}
		catch (Exception)
		{
			return null;
		}
	}

	private static void ApplyMarcusTradeTurnIn(ICharacter source, Identity marcusTarget, Identity stagedContainer, string trigger)
	{
		//IL_0010: Unknown result type (might be due to invalid IL or missing references)
		//IL_0015: Unknown result type (might be due to invalid IL or missing references)
		//IL_011a: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ce: Unknown result type (might be due to invalid IL or missing references)
		//IL_00dc: Unknown result type (might be due to invalid IL or missing references)
		if (source == null)
		{
			return;
		}
		Identity identity = ((IEntity)source).Identity;
		int instance = ((Identity)(ref identity)).Instance;
		lock (TradeSyncRoot)
		{
			if (TurnInInFlightByCharacter.Contains(instance))
			{
				return;
			}
			TurnInInFlightByCharacter.Add(instance);
		}
		try
		{
			EnsureReturnMarcusPersistence(source);
			RexMarcusChainPhase phase = GetPhase(source);
			Log("marcus-trade-turnin begin character=" + IdentityText(source) + " trigger=" + trigger + " phase=" + phase.ToString() + " target=" + ((Identity)(ref marcusTarget)).ToString(true));
			TryConsumeSuppressant(source, stagedContainer);
			try
			{
				BaseMessageHandler<KnuBotRejectedItemsMessage, KnuBotRejectedItemsMessageHandler>.Default.Send(source, marcusTarget, (IEnumerable<Item>)(object)new Item[0], 0);
			}
			catch (Exception ex)
			{
				Log("marcus-trade-rejecteditems failed: " + ex.Message);
			}
			CompleteMarcusReturnAndHandoffFlint(source);
			ForgetTradeSession(source);
			try
			{
				ContentDrivenNpcDialogueRouter.TryResumeAfterNpcTrade(source, marcusTarget);
			}
			catch (Exception ex2)
			{
				Log("marcus-trade-resume-dialogue failed: " + ex2.Message);
			}
			Log("marcus-trade-turnin done character=" + IdentityText(source) + " trigger=" + trigger + " phaseNow=" + GetPhase(source));
		}
		catch (Exception ex3)
		{
			Log("marcus-trade-turnin EXCEPTION: " + ex3);
			try
			{
				SafeQuestFullUpdateSender.TrySendB196ToFlintHandoff(source);
				TryGrantMarcusReturnIdentityCard(source);
			}
			catch (Exception ex4)
			{
				Log("marcus-trade-turnin recovery failed: " + ex4.Message);
			}
		}
		finally
		{
			lock (TradeSyncRoot)
			{
				TurnInInFlightByCharacter.Remove(instance);
			}
		}
	}

	public static bool IsCargoBoxTarget(Identity target)
	{
		//IL_0003: Unknown result type (might be due to invalid IL or missing references)
		//IL_000d: Invalid comparison between Unknown and I4
		return (int)((Identity)(ref target)).Type == 51005 && ((Identity)(ref target)).Instance == 1457108143;
	}

	private static void CompleteMarcusReturnAndHandoffFlint(ICharacter source)
	{
		//IL_001e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0023: Unknown result type (might be due to invalid IL or missing references)
		//IL_022c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0231: Unknown result type (might be due to invalid IL or missing references)
		//IL_010e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0113: Unknown result type (might be due to invalid IL or missing references)
		if (source == null)
		{
			return;
		}
		Identity identity;
		if (MissionRuntime.IsInitialized)
		{
			try
			{
				identity = ((IEntity)source).Identity;
				int instance = ((Identity)(ref identity)).Instance;
				string questId = "Mission:5514B196";
				ZoneEngine.Core.Missions.MissionStateRecord mission = MissionRuntime.Service.GetMission(instance, questId);
				if ((mission == null || mission.State == MissionLifecycleState.Offered || mission.State == MissionLifecycleState.Completed) && (mission == null || mission.State == MissionLifecycleState.Offered))
				{
					MissionRuntime.Service.OfferMission(instance, questId);
					MissionRuntime.Service.AcceptMission(instance, questId);
					mission = MissionRuntime.Service.GetMission(instance, questId);
				}
				if (mission != null && mission.State == MissionLifecycleState.Active)
				{
					PersistentMissionService service = MissionRuntime.Service;
					MissionObjectiveObservation obj = new MissionObjectiveObservation
					{
						CharacterId = instance,
						QuestId = questId,
						ObjectiveId = "mission_5514b196_objective_questfullupdate",
						ObservationKey = "marcus-trade-suppressant",
						Amount = 1,
						EventType = "KnuBotFinishTrade"
					};
					identity = ((IEntity)source).Identity;
					obj.SourceIdentity = ((Identity)(ref identity)).ToString(true);
					obj.TargetIdentity = "SimpleChar:782DE567";
					service.ObserveObjective(obj);
					MissionRuntime.Service.CompleteMission(instance, questId);
				}
				else if (mission != null && mission.State != MissionLifecycleState.Completed)
				{
					MissionRuntime.Service.CompleteMission(instance, questId);
				}
				ForceCompleteIfNeeded(instance, "Mission:5514B18F");
				ForceCompleteIfNeeded(instance, "Mission:5514B194");
				ForceCompleteIfNeeded(instance, "Mission:5514B18E");
				ApplyMarcusReturnRewards(source);
				SendMarcusReturnRewardFeedback(source);
				MissionRuntime.Service.OfferMission(instance, "Mission:5514B198");
				MissionRuntime.Service.AcceptMission(instance, "Mission:5514B198");
				EnsureFlintPersistenceForChain(source);
			}
			catch (Exception ex)
			{
				Log("marcus-return persistence failed: " + ex.Message);
			}
		}
		TryGrantMarcusReturnIdentityCard(source);
		RexQuestPreviewEmissionResult rexQuestPreviewEmissionResult = SafeQuestFullUpdateSender.TrySendB196ToFlintHandoff(source);
		if (rexQuestPreviewEmissionResult == null || !rexQuestPreviewEmissionResult.Emitted)
		{
			SafeQuestFullUpdateSender.TrySendB196QuestDelete(source);
			SafeQuestFullUpdateSender.TrySendB196CompletionCleanup(source);
			SafeQuestFullUpdateSender.TrySendFlintPreview(source);
		}
		identity = ((IEntity)source).Identity;
		Log("marcus-return-trade-complete character=" + ((Identity)(ref identity)).ToString(true) + " flintProjected=" + (rexQuestPreviewEmissionResult?.Emitted ?? false));
	}

	private static void TryGrantMarcusReturnIdentityCard(ICharacter source)
	{
		//IL_001a: Unknown result type (might be due to invalid IL or missing references)
		//IL_001f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0102: Unknown result type (might be due to invalid IL or missing references)
		//IL_0108: Expected O, but got Unknown
		if (source == null || !MissionRuntime.IsInitialized)
		{
			return;
		}
		Identity identity = ((IEntity)source).Identity;
		int instance = ((Identity)(ref identity)).Instance;
		if (MissionRuntime.Service.GetFlag(instance, "Mission:5514B196", "marcus-return-item-296569") != null)
		{
			return;
		}
		if (InventoryContainerRuntimeService.Default.CharacterHasItemInCarriedInventory(source, 296569))
		{
			MissionRuntime.Service.SetFlag(instance, "Mission:5514B196", "marcus-return-item-296569", "item:" + 296569);
			return;
		}
		if (!InventoryContainerRuntimeService.Default.HasCharacterInventory(source) || ((IDynel)source).Controller == null || ((IDynel)source).Controller.Client == null || !ItemLoader.ItemList.ContainsKey(296569))
		{
			Log("marcus-return-card-grant skipped character=" + IdentityText(source) + " reason=inventory-or-itemloader");
			return;
		}
		Item val;
		try
		{
			val = new Item(1, 296569, 296569);
			if (val.MultipleCount < 1)
			{
				val.MultipleCount = 1;
			}
		}
		catch (Exception ex)
		{
			Log("marcus-return-card-create failed: " + ex.Message);
			return;
		}
		QuestRewardInventoryGrantResult questRewardInventoryGrantResult = InventoryContainerRuntimeService.Default.TryGrantQuestRewardItem(source, val);
		if (questRewardInventoryGrantResult.Status != 0)
		{
			Log("marcus-return-card-grant failed character=" + IdentityText(source) + " status=" + questRewardInventoryGrantResult.Status);
			return;
		}
		SendMarcusReturnIdentityCardPackets(source, 296569);
		MissionRuntime.Service.SetFlag(instance, "Mission:5514B196", "marcus-return-item-296569", "item:" + 296569);
		Log("marcus-return-card-granted character=" + IdentityText(source) + " item=" + 296569);
	}

	private static void SendMarcusReturnIdentityCardPackets(ICharacter source, int itemId)
	{
		//IL_0028: Unknown result type (might be due to invalid IL or missing references)
		//IL_002d: Unknown result type (might be due to invalid IL or missing references)
		//IL_002f: Unknown result type (might be due to invalid IL or missing references)
		//IL_003a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0042: Unknown result type (might be due to invalid IL or missing references)
		//IL_004a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0052: Unknown result type (might be due to invalid IL or missing references)
		//IL_005a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0062: Unknown result type (might be due to invalid IL or missing references)
		//IL_006b: Unknown result type (might be due to invalid IL or missing references)
		//IL_006e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0087: Unknown result type (might be due to invalid IL or missing references)
		//IL_008e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0096: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a4: Expected O, but got Unknown
		//IL_00a6: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ab: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ad: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b8: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c0: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c3: Unknown result type (might be due to invalid IL or missing references)
		//IL_00dc: Unknown result type (might be due to invalid IL or missing references)
		//IL_00e3: Unknown result type (might be due to invalid IL or missing references)
		//IL_00e6: Unknown result type (might be due to invalid IL or missing references)
		//IL_00f9: Unknown result type (might be due to invalid IL or missing references)
		//IL_00fe: Unknown result type (might be due to invalid IL or missing references)
		//IL_010c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0113: Unknown result type (might be due to invalid IL or missing references)
		//IL_0122: Expected O, but got Unknown
		if (source != null && ((IDynel)source).Controller != null && ((IDynel)source).Controller.Client != null)
		{
			TemplateActionMessage val = new TemplateActionMessage
			{
				Identity = ((IEntity)source).Identity,
				Unknown = 0,
				ItemLowId = itemId,
				ItemHighId = itemId,
				Quality = 1,
				Unknown1 = 1,
				Unknown2 = 87
			};
			Identity val2 = default(Identity);
			((Identity)(ref val2)).Type = (IdentityType)110;
			((Identity)(ref val2)).Instance = 0;
			val.Placement = val2;
			val.Unknown3 = 0;
			val.Unknown4 = 0;
			((IDynel)source).Send((MessageBody)val, false);
			ContainerAddItemMessage val3 = new ContainerAddItemMessage
			{
				Identity = ((IEntity)source).Identity,
				Unknown = 0
			};
			val2 = default(Identity);
			((Identity)(ref val2)).Type = (IdentityType)110;
			((Identity)(ref val2)).Instance = 0;
			val3.SourceContainer = val2;
			val2 = default(Identity);
			((Identity)(ref val2)).Type = (IdentityType)110;
			Identity identity = ((IEntity)source).Identity;
			((Identity)(ref val2)).Instance = ((Identity)(ref identity)).Instance;
			val3.Target = val2;
			val3.TargetPlacement = 111;
			((IDynel)source).Send((MessageBody)val3, false);
			BaseMessageHandler<FeedbackMessage, FeedbackMessageHandler>.Default.Send(source, 110, 108871108);
		}
	}

	private static void ApplyMarcusReturnRewards(ICharacter source)
	{
		//IL_014f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0154: Unknown result type (might be due to invalid IL or missing references)
		MissionRewardDefinition missionRewardDefinition = new MissionRewardDefinition();
		missionRewardDefinition.RewardKey = "captured-marcus-return-xp-credits";
		missionRewardDefinition.RewardType = "character-stats";
		missionRewardDefinition.IsResolved = true;
		missionRewardDefinition.StatMutations = new MissionCharacterStatMutation[4]
		{
			new MissionCharacterStatMutation
			{
				StatIdentityType = 50000,
				StatId = 61,
				Kind = MissionStatMutationKind.AddClamped,
				Value = 1080L,
				MinimumValue = 0L,
				MaximumValue = 4294967295L
			},
			new MissionCharacterStatMutation
			{
				StatIdentityType = 50000,
				StatId = 52,
				Kind = MissionStatMutationKind.AddClamped,
				Value = 1281L,
				MinimumValue = 0L,
				MaximumValue = 4294967295L
			},
			new MissionCharacterStatMutation
			{
				StatIdentityType = 50000,
				StatId = 592,
				Kind = MissionStatMutationKind.AddClamped,
				Value = 1281L,
				MinimumValue = 0L,
				MaximumValue = 4294967295L
			},
			new MissionCharacterStatMutation
			{
				StatIdentityType = 50000,
				StatId = 57,
				Kind = MissionStatMutationKind.Set,
				Value = 1281L,
				MinimumValue = 0L,
				MaximumValue = 4294967295L
			}
		};
		MissionRewardDefinition definition = missionRewardDefinition;
		MissionRewardCoordinator rewards = MissionRuntime.Rewards;
		Identity identity = ((IEntity)source).Identity;
		MissionRewardExecutionResult missionRewardExecutionResult = rewards.ExecuteAtomicCharacterStats(((Identity)(ref identity)).Instance, "Mission:5514B196", definition, "capture:20260719-Rex-Markus-stone:marcus-b196-xp-credits");
		if (!missionRewardExecutionResult.Succeeded || missionRewardExecutionResult.StatValues == null)
		{
			return;
		}
		foreach (MissionCharacterStatValue statValue in missionRewardExecutionResult.StatValues)
		{
			uint num = (uint)((statValue.Value > 0) ? Math.Min(statValue.Value, 4294967295L) : 0u);
			((IStats)source).Stats[(StatIds)statValue.StatId].Set(num, false);
		}
		BaseMessageHandler<StatMessage, StatMessageHandler>.Default.SendChanged(source);
	}

	private static void SendMarcusReturnRewardFeedback(ICharacter source)
	{
		//IL_0031: Unknown result type (might be due to invalid IL or missing references)
		//IL_0036: Unknown result type (might be due to invalid IL or missing references)
		//IL_0038: Unknown result type (might be due to invalid IL or missing references)
		//IL_0043: Unknown result type (might be due to invalid IL or missing references)
		//IL_004b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0053: Unknown result type (might be due to invalid IL or missing references)
		//IL_005f: Unknown result type (might be due to invalid IL or missing references)
		//IL_006c: Expected O, but got Unknown
		if (source == null || ((IDynel)source).Controller == null || ((IDynel)source).Controller.Client == null)
		{
			return;
		}
		try
		{
			((IDynel)source).Controller.Client.SendCompressed((MessageBody)new FormatFeedbackMessage
			{
				Identity = ((IEntity)source).Identity,
				Unknown = 1,
				Unknown1 = 0,
				FormattedMessage = "~&!!!\":$'O\"ui!!!0'i!!!-]~",
				Unknown2 = 0
			});
		}
		catch (Exception ex)
		{
			LogUtil.Debug((DebugInfoDetail)512, "ARETE_MARCUS_RETURN reward feedback failed: " + ex.Message);
		}
	}

	private static void TryConsumeSuppressant(ICharacter source, Identity stagedContainer)
	{
		//IL_00f0: Unknown result type (might be due to invalid IL or missing references)
		//IL_01e9: Unknown result type (might be due to invalid IL or missing references)
		//IL_001c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0045: Unknown result type (might be due to invalid IL or missing references)
		//IL_0051: Expected I4, but got Unknown
		//IL_00c7: Unknown result type (might be due to invalid IL or missing references)
		//IL_00d8: Expected I4, but got Unknown
		if (source == null || ((IItemContainer)source).BaseInventory == null)
		{
			return;
		}
		if ((int)((Identity)(ref stagedContainer)).Type != 0 && ((Identity)(ref stagedContainer)).Instance > 0 && ((IItemContainer)source).BaseInventory.Pages.TryGetValue((int)((Identity)(ref stagedContainer)).Type, out var value) && value != null)
		{
			IItem val = value[((Identity)(ref stagedContainer)).Instance];
			if (val != null && (val.LowID == 296780 || val.HighID == 296780))
			{
				value.Remove(((Identity)(ref stagedContainer)).Instance);
				try
				{
					if (((IItemContainer)source).BaseInventory.Write())
					{
						BaseMessageHandler<CharacterActionMessage, CharacterActionMessageHandler>.Default.SendDeleteItem(source, (int)((Identity)(ref stagedContainer)).Type, ((Identity)(ref stagedContainer)).Instance);
						return;
					}
				}
				catch (Exception)
				{
				}
				value.Add(((Identity)(ref stagedContainer)).Instance, val);
			}
		}
		foreach (KeyValuePair<int, IInventoryPage> page in ((IItemContainer)source).BaseInventory.Pages)
		{
			IInventoryPage value2 = page.Value;
			if (value2 == null)
			{
				continue;
			}
			foreach (KeyValuePair<int, IItem> item in value2.List())
			{
				IItem value3 = item.Value;
				if (value3 == null || (value3.LowID != 296780 && value3.HighID != 296780))
				{
					continue;
				}
				value2.Remove(item.Key);
				try
				{
					if (((IItemContainer)source).BaseInventory.Write())
					{
						BaseMessageHandler<CharacterActionMessage, CharacterActionMessageHandler>.Default.SendDeleteItem(source, page.Key, item.Key);
						return;
					}
				}
				catch (Exception)
				{
				}
				value2.Add(item.Key, value3);
			}
		}
	}

	private static void ApplyStimTradeTurnIn(ICharacter source, Identity marcusTarget, Identity stagedContainer, string trigger)
	{
		//IL_0010: Unknown result type (might be due to invalid IL or missing references)
		//IL_0015: Unknown result type (might be due to invalid IL or missing references)
		//IL_0063: Unknown result type (might be due to invalid IL or missing references)
		//IL_0064: Unknown result type (might be due to invalid IL or missing references)
		if (source == null)
		{
			return;
		}
		Identity identity = ((IEntity)source).Identity;
		int instance = ((Identity)(ref identity)).Instance;
		lock (TradeSyncRoot)
		{
			if (TurnInInFlightByCharacter.Contains(instance))
			{
				return;
			}
			TurnInInFlightByCharacter.Add(instance);
		}
		try
		{
			MarcusWoundedWorkersQuestRuntime.CompleteStimReturnTurnIn(source, marcusTarget, stagedContainer, trigger);
			ForgetTradeSession(source);
		}
		finally
		{
			lock (TradeSyncRoot)
			{
				TurnInInFlightByCharacter.Remove(instance);
			}
		}
	}

	private static void BeginMarcusTrade(ICharacter source, Identity marcusIdentity, MarcusTradeKind kind)
	{
		//IL_0005: Unknown result type (might be due to invalid IL or missing references)
		//IL_000a: Unknown result type (might be due to invalid IL or missing references)
		//IL_003a: Unknown result type (might be due to invalid IL or missing references)
		//IL_003f: Unknown result type (might be due to invalid IL or missing references)
		//IL_004d: Unknown result type (might be due to invalid IL or missing references)
		if (source == null)
		{
			return;
		}
		Identity identity = ((IEntity)source).Identity;
		if (((Identity)(ref identity)).Instance <= 0)
		{
			return;
		}
		lock (TradeSyncRoot)
		{
			Dictionary<int, MarcusTradeSession> tradeSessionsByCharacter = TradeSessionsByCharacter;
			identity = ((IEntity)source).Identity;
			tradeSessionsByCharacter[((Identity)(ref identity)).Instance] = new MarcusTradeSession
			{
				NpcIdentity = marcusIdentity,
				Kind = kind
			};
		}
	}

	private static MarcusTradeSession GetTradeSession(ICharacter source)
	{
		//IL_0027: Unknown result type (might be due to invalid IL or missing references)
		//IL_002c: Unknown result type (might be due to invalid IL or missing references)
		if (source == null)
		{
			return null;
		}
		MarcusTradeSession value;
		lock (TradeSyncRoot)
		{
			Dictionary<int, MarcusTradeSession> tradeSessionsByCharacter = TradeSessionsByCharacter;
			Identity identity = ((IEntity)source).Identity;
			tradeSessionsByCharacter.TryGetValue(((Identity)(ref identity)).Instance, out value);
		}
		return value;
	}

	private static void ForgetTradeSession(ICharacter source)
	{
		//IL_0024: Unknown result type (might be due to invalid IL or missing references)
		//IL_0029: Unknown result type (might be due to invalid IL or missing references)
		if (source == null)
		{
			return;
		}
		lock (TradeSyncRoot)
		{
			Dictionary<int, MarcusTradeSession> tradeSessionsByCharacter = TradeSessionsByCharacter;
			Identity identity = ((IEntity)source).Identity;
			tradeSessionsByCharacter.Remove(((Identity)(ref identity)).Instance);
		}
	}

	private static void ForceCompleteIfNeeded(int characterId, string questId)
	{
		ZoneEngine.Core.Missions.MissionStateRecord mission = MissionRuntime.Service.GetMission(characterId, questId);
		if (mission != null && mission.State != MissionLifecycleState.Completed)
		{
			if (mission.State == MissionLifecycleState.Offered)
			{
				MissionRuntime.Service.AcceptMission(characterId, questId);
				mission = MissionRuntime.Service.GetMission(characterId, questId);
			}
			if (mission != null && mission.State == MissionLifecycleState.Active)
			{
				string objectiveId = "mission_" + questId.Replace("Mission:", string.Empty).ToLowerInvariant() + "_objective_questfullupdate";
				MissionRuntime.Service.ObserveObjective(new MissionObjectiveObservation
				{
					CharacterId = characterId,
					QuestId = questId,
					ObjectiveId = objectiveId,
					ObservationKey = "rex-marcus-chain-force-complete",
					Amount = 1,
					EventType = "ChainCoordinator",
					SourceIdentity = string.Empty,
					TargetIdentity = string.Empty
				});
				MissionRuntime.Service.CompleteMission(characterId, questId);
			}
		}
	}

	private static bool IsActiveOrOffered(ZoneEngine.Core.Missions.MissionStateRecord mission)
	{
		return mission != null && (mission.State == MissionLifecycleState.Offered || mission.State == MissionLifecycleState.Active);
	}

	private static bool IsCompleted(ZoneEngine.Core.Missions.MissionStateRecord mission)
	{
		return mission != null && mission.State == MissionLifecycleState.Completed;
	}

	private static bool IsMarcusStoneNpc(ICharacter source, Identity identity)
	{
		//IL_0003: Unknown result type (might be due to invalid IL or missing references)
		//IL_000d: Invalid comparison between Unknown and I4
		//IL_0061: Unknown result type (might be due to invalid IL or missing references)
		//IL_0066: Unknown result type (might be due to invalid IL or missing references)
		if ((int)((Identity)(ref identity)).Type != 50000 || ((Identity)(ref identity)).Instance == 0)
		{
			return false;
		}
		if (((Identity)(ref identity)).Instance == 2016273767)
		{
			return true;
		}
		if (source == null || ((IInstancedEntity)source).Playfield == null)
		{
			return false;
		}
		try
		{
			ICharacter @object = Pool.Instance.GetObject<ICharacter>(((IEntity)((IInstancedEntity)source).Playfield).Identity, identity);
			return @object != null && !string.IsNullOrWhiteSpace(((INamedEntity)@object).Name) && ((INamedEntity)@object).Name.IndexOf("Marcus Stone", StringComparison.OrdinalIgnoreCase) >= 0;
		}
		catch (Exception)
		{
			return false;
		}
	}

	private static string IdentityText(ICharacter source)
	{
		//IL_0005: Unknown result type (might be due to invalid IL or missing references)
		//IL_000a: Unknown result type (might be due to invalid IL or missing references)
		object result;
		if (source != null)
		{
			Identity identity = ((IEntity)source).Identity;
			result = ((Identity)(ref identity)).ToString(true);
		}
		else
		{
			result = "<null>";
		}
		return (string)result;
	}

	private static void Log(string message)
	{
		LogUtil.Debug((DebugInfoDetail)512, "ARETE_REX_MARCUS_CHAIN " + message);
	}
}
