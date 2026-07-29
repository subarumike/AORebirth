using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using AORebirth.Core.Components;
using AORebirth.Core.Entities;
using AORebirth.Interfaces;
using AORebirth.ObjectManager;
using SmokeLounge.AOtomation.Messaging.GameData;
using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;
using Utility;
using ZoneEngine.Core.Arete.Quests;
using ZoneEngine.Core.Controllers;
using ZoneEngine.Core.MessageHandlers;
using ZoneEngine.Core.Missions;
using ZoneEngine.Core.Playfields;
using ZoneEngine.Core.Subway.Quests;
using ZoneEngine.Core.Thrak.Quests;
using ZoneEngine.Core.Thrak.Vendors;

namespace ZoneEngine.Core.Arete.Dialogue;

public static class ContentDrivenNpcDialogueRouter
{
	private sealed class ContentDrivenNpcDialogueRegistration
	{
		public string Name { get; set; }

		public string ExpectedNpcName { get; set; }

		public Identity NpcIdentity { get; set; }

		public string NpcIdentityText { get; set; }

		public int? PlayfieldId { get; set; }

		public string GateEnvironmentVariableName { get; set; }

		public string LogPrefix { get; set; }
	}

	private sealed class DialogueSessionRecord
	{
		public ContentDrivenNpcDialogueRegistration Registration { get; set; }

		public DialogueSession Session { get; set; }
	}

	public const string RexLarssonGateEnvironmentVariableName = "AO_REBIRTH_ENABLE_ARETE_REX_DIALOGUE_ROUTING";

	public const string SubwayTailorGateEnvironmentVariableName = "AO_REBIRTH_ENABLE_SUBWAY_TAILOR_DIALOGUE_ROUTING";

	private const int AreteLandingPlayfieldId = 6553;

	private const int RexLarssonInstance = 2016273768;

	private const int MarcusStoneInstance = 2016273767;

	private const int FlintNovakInstance = 2028010596;

	private const int AlexGibbsInstance = 2028010593;

	private const int BillInstance = 2028010598;

	private const string RexLarssonNpcIdentity = "SimpleChar:782DE568";

	private const string MarcusStoneNpcIdentity = "SimpleChar:782DE567";

	private const string FlintNovakNpcIdentity = "SimpleChar:78E0FC64";

	private const string AlexGibbsNpcIdentity = "SimpleChar:78E0FC61";

	private const string RexB18EReturnNodeId = "rex_194454_006";

	private const int KnuBotPacketPacingMilliseconds = 20;

	private static readonly ContentDrivenNpcDialogueRegistration RexLarssonRegistration;

	private static readonly ContentDrivenNpcDialogueRegistration MarcusStoneRegistration;

	private static readonly ContentDrivenNpcDialogueRegistration FlintNovakRegistration;

	private static readonly ContentDrivenNpcDialogueRegistration AlexGibbsRegistration;

	private static readonly ContentDrivenNpcDialogueRegistration BillRegistration;

	private static readonly ContentDrivenNpcDialogueRegistration WindcallerKarrecRegistration;

	private static readonly ContentDrivenNpcDialogueRegistration AnnoyingDudeRegistration;

	private static readonly ContentDrivenNpcDialogueRegistration MaddyCardileRegistration;

	private static readonly ContentDrivenNpcDialogueRegistration SubwayTailorRegistration;

	private static readonly ContentDrivenNpcDialogueRegistration VeronicaEscobarRegistration;

	private static readonly ContentDrivenNpcDialogueRegistration ProphetYuttRegistration;

	private static readonly ContentDrivenNpcDialogueRegistration HypnagogicUrgaLumRegistration;

	private static readonly ContentDrivenNpcDialogueRegistration DreamingSilvertailRegistration;

	private static readonly ContentDrivenNpcDialogueRegistration CraigOrFuriousFistsRegistration;

	private static readonly ContentDrivenNpcDialogueRegistration CraigOrPreservationRegistration;

	private static readonly ContentDrivenNpcDialogueRegistration CraigOrFlamingBarrelsRegistration;

	private static readonly ContentDrivenNpcDialogueRegistration CraigOrGearAndAmmoRegistration;

	private static readonly ContentDrivenNpcDialogueRegistration CraigOrProtectionRegistration;

	private static readonly ContentDrivenNpcDialogueRegistration SonLenRegistration;

	private static readonly ContentDrivenNpcDialogueRegistration[] Registrations;

	private static readonly Dictionary<string, DialogueSessionRecord> SessionsByCharacter;

	private static readonly ConditionalWeakTable<ICharacter, object> TailorOpenHistoryByCharacter;

	private static readonly object SyncRoot;

	private static DialogueSessionService sharedDialogueSessionService;

	public static bool IsRexLarssonRoutingEnabled => IsRegistrationEnabled(RexLarssonRegistration);

	private static ContentDrivenNpcDialogueRegistration CreateWindcallerRegistration(WindcallerKarrecNpcDefinition definition)
	{
		//IL_0023: Unknown result type (might be due to invalid IL or missing references)
		//IL_0044: Unknown result type (might be due to invalid IL or missing references)
		ContentDrivenNpcDialogueRegistration obj = new ContentDrivenNpcDialogueRegistration
		{
			Name = definition.DisplayName,
			ExpectedNpcName = definition.DisplayName
		};
		Identity npcIdentity = default(Identity);
		((Identity)(ref npcIdentity)).Type = (IdentityType)50000;
		((Identity)(ref npcIdentity)).Instance = definition.SourceNpcInstance;
		obj.NpcIdentity = npcIdentity;
		obj.NpcIdentityText = definition.SourceNpcIdentity;
		obj.PlayfieldId = definition.PlayfieldId;
		obj.GateEnvironmentVariableName = null;
		obj.LogPrefix = "SUBWAY_KARREC_DIALOGUE";
		return obj;
	}

	private static ContentDrivenNpcDialogueRegistration CreateThrakGardenVendorRegistration(string displayName, int sourceNpcInstance, string npcIdentityText)
	{
		//IL_0018: Unknown result type (might be due to invalid IL or missing references)
		return new ContentDrivenNpcDialogueRegistration
		{
			Name = displayName,
			ExpectedNpcName = displayName,
			NpcIdentity = ThrakGardenVendorInteractionRules.CreateIdentity(sourceNpcInstance),
			NpcIdentityText = npcIdentityText,
			PlayfieldId = 4677,
			GateEnvironmentVariableName = null,
			LogPrefix = "THRAK_GARDEN_VENDOR"
		};
	}

	public static bool TryStartDialogue(ICharacter npc, Identity sourceIdentity)
	{
		//IL_0070: Unknown result type (might be due to invalid IL or missing references)
		ContentDrivenNpcDialogueRegistration contentDrivenNpcDialogueRegistration = FindRegistration(npc);
		if (contentDrivenNpcDialogueRegistration == null)
		{
			return false;
		}
		if (!IsRegistrationEnabled(contentDrivenNpcDialogueRegistration))
		{
			return false;
		}
		if (!IsExpectedPlayfield(npc, contentDrivenNpcDialogueRegistration))
		{
			LogSkipped(contentDrivenNpcDialogueRegistration, "routing skipped because NPC is not in expected playfield " + contentDrivenNpcDialogueRegistration.PlayfieldId + ".");
			return false;
		}
		ICharacter val = ResolveCharacter(npc, sourceIdentity);
		if (val == null)
		{
			LogSkipped(contentDrivenNpcDialogueRegistration, "routing skipped because source character was not found.");
			return false;
		}
		if (!IsExpectedPlayfield(val, contentDrivenNpcDialogueRegistration))
		{
			LogSkipped(contentDrivenNpcDialogueRegistration, "routing skipped because source character is not in expected playfield " + contentDrivenNpcDialogueRegistration.PlayfieldId + ".");
			return false;
		}
		return TryStartDialogueForSource(val, npc, contentDrivenNpcDialogueRegistration);
	}

	public static bool TryStartDialogueForTarget(ICharacter source, Identity targetIdentity)
	{
		//IL_0027: Unknown result type (might be due to invalid IL or missing references)
		//IL_002c: Unknown result type (might be due to invalid IL or missing references)
		//IL_003d: Unknown result type (might be due to invalid IL or missing references)
		if (source == null || ((IInstancedEntity)source).Playfield == null)
		{
			return false;
		}
		ICharacter @object = Pool.Instance.GetObject<ICharacter>(((IEntity)((IInstancedEntity)source).Playfield).Identity, targetIdentity);
		ContentDrivenNpcDialogueRegistration contentDrivenNpcDialogueRegistration = FindRegistration(@object) ?? FindRegistration(targetIdentity);
		if (contentDrivenNpcDialogueRegistration == null)
		{
			return false;
		}
		if (!IsRegistrationEnabled(contentDrivenNpcDialogueRegistration))
		{
			LogSkipped(contentDrivenNpcDialogueRegistration, "direct trade routing skipped because " + contentDrivenNpcDialogueRegistration.GateEnvironmentVariableName + " is not enabled.");
			return false;
		}
		if (!IsExpectedPlayfield(source, contentDrivenNpcDialogueRegistration))
		{
			return false;
		}
		if (!IsRegisteredNpc(@object, contentDrivenNpcDialogueRegistration) || !IsExpectedPlayfield(@object, contentDrivenNpcDialogueRegistration))
		{
			LogSkipped(contentDrivenNpcDialogueRegistration, "direct trade routing skipped because registered target was not found in expected playfield.");
			return false;
		}
		return TryStartDialogueForSource(source, @object, contentDrivenNpcDialogueRegistration);
	}

	public static bool TryResumeAfterNpcTrade(ICharacter source, Identity npcIdentity)
	{
		//IL_0026: Unknown result type (might be due to invalid IL or missing references)
		//IL_0080: Unknown result type (might be due to invalid IL or missing references)
		//IL_01ac: Unknown result type (might be due to invalid IL or missing references)
		//IL_01b1: Unknown result type (might be due to invalid IL or missing references)
		if (source == null)
		{
			return false;
		}
		ContentDrivenNpcDialogueRegistration contentDrivenNpcDialogueRegistration = FindActiveSessionRegistration(source);
		if (contentDrivenNpcDialogueRegistration == null)
		{
			contentDrivenNpcDialogueRegistration = FindRegistration(npcIdentity);
		}
		if (contentDrivenNpcDialogueRegistration == null || !IsRegistrationEnabled(contentDrivenNpcDialogueRegistration))
		{
			return false;
		}
		if (!IsExpectedPlayfield(source, contentDrivenNpcDialogueRegistration))
		{
			return false;
		}
		if (!TryGetSessionService(contentDrivenNpcDialogueRegistration, out var service))
		{
			return false;
		}
		string text = CreateSessionKey(((IEntity)source).Identity, contentDrivenNpcDialogueRegistration);
		DialogueSessionRecord value;
		lock (SyncRoot)
		{
			SessionsByCharacter.TryGetValue(text, out value);
		}
		if (value == null || value.Session == null || !value.Session.IsActive)
		{
			return false;
		}
		DialogueSessionResult dialogueSessionResult = service.SelectOption(value.Session, 0);
		if (!dialogueSessionResult.IsValid)
		{
			LogValidation(contentDrivenNpcDialogueRegistration, "post-trade dialogue advance failed", dialogueSessionResult.Validation);
			return false;
		}
		if (dialogueSessionResult.Session == null || !dialogueSessionResult.Session.IsActive)
		{
			CloseSession(source, text, contentDrivenNpcDialogueRegistration, sendClose: true);
			return true;
		}
		lock (SyncRoot)
		{
			SessionsByCharacter[text] = new DialogueSessionRecord
			{
				Registration = contentDrivenNpcDialogueRegistration,
				Session = dialogueSessionResult.Session
			};
		}
		ContentDrivenNpcDialogueRegistration registration = contentDrivenNpcDialogueRegistration;
		Identity identity = ((IEntity)source).Identity;
		LogDialogue(registration, "post-trade advanced character=" + ((Identity)(ref identity)).ToString(true) + " to=" + dialogueSessionResult.Session.CurrentNodeId);
		if (IsRegistration(contentDrivenNpcDialogueRegistration, BillRegistration))
		{
			SafeQuestFullUpdateSender.TrySendDeliverBillToKneecappingHandoff(source);
		}
		SendDialogueNode(source, dialogueSessionResult, contentDrivenNpcDialogueRegistration);
		return true;
	}

	public static bool ShouldSuppressCombat(ICharacter target)
	{
		ContentDrivenNpcDialogueRegistration contentDrivenNpcDialogueRegistration = FindRegistration(target);
		return contentDrivenNpcDialogueRegistration != null && IsRegistrationEnabled(contentDrivenNpcDialogueRegistration) && IsExpectedPlayfield(target, contentDrivenNpcDialogueRegistration);
	}

	public static bool TryHandleAnswer(ICharacter source, Identity targetIdentity, int answerIndex)
	{
		//IL_0015: Unknown result type (might be due to invalid IL or missing references)
		//IL_0016: Unknown result type (might be due to invalid IL or missing references)
		//IL_0051: Unknown result type (might be due to invalid IL or missing references)
		//IL_0057: Unknown result type (might be due to invalid IL or missing references)
		//IL_0071: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ee: Unknown result type (might be due to invalid IL or missing references)
		//IL_014d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0254: Unknown result type (might be due to invalid IL or missing references)
		//IL_0259: Unknown result type (might be due to invalid IL or missing references)
		//IL_01cb: Unknown result type (might be due to invalid IL or missing references)
		//IL_01d0: Unknown result type (might be due to invalid IL or missing references)
		//IL_0381: Unknown result type (might be due to invalid IL or missing references)
		//IL_0386: Unknown result type (might be due to invalid IL or missing references)
		//IL_044e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0453: Unknown result type (might be due to invalid IL or missing references)
		if (source == null)
		{
			return false;
		}
		ICharacter npc = ((((IInstancedEntity)source).Playfield == null) ? null : Pool.Instance.GetObject<ICharacter>(((IEntity)((IInstancedEntity)source).Playfield).Identity, targetIdentity));
		ContentDrivenNpcDialogueRegistration registration = FindRegistration(npc) ?? FindRegistration(targetIdentity);
		if (registration == null)
		{
			registration = FindActiveSessionRegistration(source);
			if (registration == null)
			{
				return false;
			}
		}
		if (!IsRegistrationEnabled(registration) || !IsExpectedPlayfield(source, registration))
		{
			return false;
		}
		if (!IsRegisteredIdentity(targetIdentity, registration) && !HasActiveSession(source, registration))
		{
			return false;
		}
		if (!TryGetSessionService(registration, out var service))
		{
			return false;
		}
		string text = CreateSessionKey(((IEntity)source).Identity, registration);
		DialogueSession dialogueSession;
		lock (SyncRoot)
		{
			SessionsByCharacter.TryGetValue(text, out var value);
			dialogueSession = value?.Session;
		}
		Identity identity;
		if (dialogueSession == null)
		{
			ContentDrivenNpcDialogueRegistration registration2 = registration;
			string[] obj = new string[6] { "answer ignored because no routed session exists for character=", null, null, null, null, null };
			identity = ((IEntity)source).Identity;
			obj[1] = ((Identity)(ref identity)).ToString(true);
			obj[2] = " target=";
			obj[3] = ((Identity)(ref targetIdentity)).ToString(true);
			obj[4] = " answer=";
			obj[5] = answerIndex.ToString();
			LogDialogue(registration2, string.Concat(obj));
			return false;
		}
		string previousNodeId = dialogueSession.CurrentNodeId;
		string optionText = ResolveSelectedOptionText(service, dialogueSession, answerIndex);
		ContentDrivenNpcDialogueRegistration registration3 = registration;
		string[] obj2 = new string[8] { "answer received character=", null, null, null, null, null, null, null };
		identity = ((IEntity)source).Identity;
		obj2[1] = ((Identity)(ref identity)).ToString(true);
		obj2[2] = " target=";
		obj2[3] = ((Identity)(ref targetIdentity)).ToString(true);
		obj2[4] = " answer=";
		obj2[5] = answerIndex.ToString();
		obj2[6] = " node=";
		obj2[7] = previousNodeId;
		LogDialogue(registration3, string.Concat(obj2));
		DialogueSessionResult dialogueSessionResult = service.SelectOption(dialogueSession, answerIndex);
		if (!dialogueSessionResult.IsValid)
		{
			LogValidation(registration, "dialogue option failed", dialogueSessionResult.Validation);
			CloseSession(source, text, registration, sendClose: true);
			return true;
		}
		LogRecordedActions(source, dialogueSessionResult, registration);
		TryHandleTailorMeasurementGrant(source, registration, previousNodeId, answerIndex);
		Func<bool> afterPromptBeforeOptions = () => TryHandleWindcallerSideEffect(source, registration, previousNodeId, answerIndex) || TryHandleThrakGardenKeySideEffect(source, registration, previousNodeId, answerIndex) || TryHandleThrakGardenVendorSideEffect(source, registration, previousNodeId, answerIndex) || TryHandleRexMarcusTradeHoldSideEffect(source, registration, previousNodeId, answerIndex, targetIdentity) || TryHandleAlexTradeHoldSideEffect(source, registration, previousNodeId, answerIndex, targetIdentity) || TryHandleBillTradeHoldSideEffect(source, registration, previousNodeId, answerIndex, targetIdentity);
		if (dialogueSessionResult.Session == null || !dialogueSessionResult.Session.IsActive)
		{
			ContentDrivenNpcDialogueRegistration registration4 = registration;
			string[] obj3 = new string[6] { "answer closed session character=", null, null, null, null, null };
			identity = ((IEntity)source).Identity;
			obj3[1] = ((Identity)(ref identity)).ToString(true);
			obj3[2] = " previousNode=";
			obj3[3] = previousNodeId;
			obj3[4] = " answer=";
			obj3[5] = answerIndex.ToString();
			LogDialogue(registration4, string.Concat(obj3));
			CloseSession(source, text, registration, sendClose: true);
			return true;
		}
		lock (SyncRoot)
		{
			SessionsByCharacter[text] = new DialogueSessionRecord
			{
				Registration = registration,
				Session = dialogueSessionResult.Session
			};
		}
		ContentDrivenNpcDialogueRegistration registration5 = registration;
		string[] obj4 = new string[8] { "answer advanced character=", null, null, null, null, null, null, null };
		identity = ((IEntity)source).Identity;
		obj4[1] = ((Identity)(ref identity)).ToString(true);
		obj4[2] = " from=";
		obj4[3] = previousNodeId;
		obj4[4] = " to=";
		obj4[5] = dialogueSessionResult.Session.CurrentNodeId;
		obj4[6] = " answer=";
		obj4[7] = answerIndex.ToString();
		LogDialogue(registration5, string.Concat(obj4));
		if (IsRegistration(registration, MarcusStoneRegistration))
		{
			RexMarcusChainCoordinator.OnMarcusAnswer(source, previousNodeId, answerIndex, optionText, IsRegistrationEnabled(registration));
		}
		if (IsRegistration(registration, FlintNovakRegistration))
		{
			FlintBioComQuestRuntime.TryHandleDialogueAnswer(source, previousNodeId, answerIndex);
		}
		if (IsRegistration(registration, AlexGibbsRegistration))
		{
			KneecappingQuestRuntime.TryHandleAlexDialogueAnswer(source, previousNodeId, answerIndex);
		}
		if (IsRegistration(registration, BillRegistration))
		{
			SurveillanceUplinkQuestRuntime.TryHandleBillDialogueAnswer(source, previousNodeId, answerIndex);
		}
		PaceKnuBotPackets();
		SendDialogueNode(source, dialogueSessionResult, registration, afterPromptBeforeOptions);
		if (IsRegistration(registration, RexLarssonRegistration))
		{
			RexMarcusChainCoordinator.OnRexAnswer(source, previousNodeId, answerIndex, IsRegistrationEnabled(registration));
		}
		return true;
	}

	private static void TryHandleTailorMeasurementGrant(ICharacter source, ContentDrivenNpcDialogueRegistration registration, string previousNodeId, int answerIndex)
	{
		//IL_0062: Unknown result type (might be due to invalid IL or missing references)
		//IL_0067: Unknown result type (might be due to invalid IL or missing references)
		if (IsRegistration(registration, SubwayTailorRegistration) && string.Equals(previousNodeId, "tailor_parts", StringComparison.OrdinalIgnoreCase) && answerIndex >= 0 && answerIndex <= 7)
		{
			bool flag = CapturedSubwayTailorDialogueRuntime.TryGrantMeasurementItem(source, answerIndex);
			string[] obj = new string[6]
			{
				"measurement item grant ",
				flag ? "succeeded" : "failed",
				" character=",
				null,
				null,
				null
			};
			Identity identity = ((IEntity)source).Identity;
			obj[3] = ((Identity)(ref identity)).ToString(true);
			obj[4] = " answer=";
			obj[5] = answerIndex.ToString();
			LogDialogue(registration, string.Concat(obj));
		}
	}

	public static bool TryHandleClose(ICharacter source, Identity targetIdentity)
	{
		//IL_0012: Unknown result type (might be due to invalid IL or missing references)
		//IL_0063: Unknown result type (might be due to invalid IL or missing references)
		//IL_0086: Unknown result type (might be due to invalid IL or missing references)
		//IL_00d2: Unknown result type (might be due to invalid IL or missing references)
		//IL_00d7: Unknown result type (might be due to invalid IL or missing references)
		if (source == null)
		{
			return false;
		}
		ContentDrivenNpcDialogueRegistration contentDrivenNpcDialogueRegistration = FindRegistration(targetIdentity);
		if (contentDrivenNpcDialogueRegistration == null)
		{
			contentDrivenNpcDialogueRegistration = FindActiveSessionRegistration(source);
			if (contentDrivenNpcDialogueRegistration == null)
			{
				return false;
			}
		}
		if (!IsRegistrationEnabled(contentDrivenNpcDialogueRegistration) || !IsExpectedPlayfield(source, contentDrivenNpcDialogueRegistration))
		{
			return false;
		}
		if (!IsRegisteredIdentity(targetIdentity, contentDrivenNpcDialogueRegistration) && !HasActiveSession(source, contentDrivenNpcDialogueRegistration))
		{
			return false;
		}
		string key = CreateSessionKey(((IEntity)source).Identity, contentDrivenNpcDialogueRegistration);
		bool flag;
		lock (SyncRoot)
		{
			flag = SessionsByCharacter.Remove(key);
		}
		if (flag)
		{
			ContentDrivenNpcDialogueRegistration registration = contentDrivenNpcDialogueRegistration;
			Identity identity = ((IEntity)source).Identity;
			LogDialogue(registration, "session closed by client character=" + ((Identity)(ref identity)).ToString(true));
			return true;
		}
		return false;
	}

	private static bool TryStartDialogueForSource(ICharacter source, ICharacter npc, ContentDrivenNpcDialogueRegistration registration)
	{
		//IL_0013: Unknown result type (might be due to invalid IL or missing references)
		//IL_0057: Unknown result type (might be due to invalid IL or missing references)
		//IL_01cd: Unknown result type (might be due to invalid IL or missing references)
		//IL_014b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0169: Unknown result type (might be due to invalid IL or missing references)
		//IL_016e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0276: Unknown result type (might be due to invalid IL or missing references)
		//IL_027b: Unknown result type (might be due to invalid IL or missing references)
		if (IsRegistration(registration, WindcallerKarrecRegistration))
		{
			WindcallerKarrecTradeAdapter.TryResumeDurableCompletion(source, registration.NpcIdentity);
			if (WindcallerKarrecQuestRuntime.IsCompleted(source))
			{
				return CloseRegisteredDialogueSafely(source, npc, registration);
			}
			if (WindcallerKarrecQuestRuntime.IsActive(source) && !WindcallerKarrecQuestRuntime.HasBothOfferingItems(source))
			{
				WindcallerKarrecPacketSender.TrySendQuestFullUpdate(source, registration.NpcIdentity);
				return CloseRegisteredDialogueSafely(source, npc, registration);
			}
		}
		else if ((IsRegistration(registration, AnnoyingDudeRegistration) || IsRegistration(registration, MaddyCardileRegistration)) && !WindcallerKarrecQuestRuntime.IsActive(source))
		{
			return CloseRegisteredDialogueSafely(source, npc, registration);
		}
		if (!TryGetSessionService(registration, out var service))
		{
			return false;
		}
		string text = ResolveRequestedStartNodeId(source, registration);
		DialogueSessionResult dialogueSessionResult = (string.IsNullOrWhiteSpace(text) ? service.StartSession(registration.NpcIdentityText) : service.StartSessionAtNode(registration.NpcIdentityText, text));
		Identity identity;
		if (!dialogueSessionResult.IsValid || dialogueSessionResult.Session == null)
		{
			LogValidation(registration, "dialogue start failed", dialogueSessionResult.Validation);
			if (!string.IsNullOrWhiteSpace(text))
			{
				SendOpenChatWindow(source, registration);
				PaceKnuBotPackets();
				BaseMessageHandler<KnuBotCloseChatWindowMessage, KnuBotCloseChatWindowMessageHandler>.Default.Send(source, registration.NpcIdentity);
				string[] obj = new string[6] { "return-state start node unavailable; closed safely character=", null, null, null, null, null };
				identity = ((IEntity)source).Identity;
				obj[1] = ((Identity)(ref identity)).ToString(true);
				obj[2] = " requestedNode=";
				obj[3] = text;
				obj[4] = " chainState=";
				obj[5] = DescribeChainState(source, registration);
				LogDialogue(registration, string.Concat(obj));
				return true;
			}
			return false;
		}
		lock (SyncRoot)
		{
			SessionsByCharacter[CreateSessionKey(((IEntity)source).Identity, registration)] = new DialogueSessionRecord
			{
				Registration = registration,
				Session = dialogueSessionResult.Session
			};
		}
		FaceNpcTowardSource(npc, source);
		SendOpenChatWindow(source, registration);
		PaceKnuBotPackets();
		SendDialogueNode(source, dialogueSessionResult, registration);
		if (IsRegistration(registration, RexLarssonRegistration))
		{
			RexMarcusChainCoordinator.OnRexOpen(source, IsRegistrationEnabled(registration));
		}
		else if (IsRegistration(registration, MarcusStoneRegistration))
		{
			RexMarcusChainCoordinator.OnMarcusOpen(source);
		}
		string[] obj2 = new string[8] { "started character=", null, null, null, null, null, null, null };
		identity = ((IEntity)source).Identity;
		obj2[1] = ((Identity)(ref identity)).ToString(true);
		obj2[2] = " node=";
		obj2[3] = dialogueSessionResult.Session.CurrentNodeId;
		obj2[4] = " requestedStartNode=";
		obj2[5] = (string.IsNullOrWhiteSpace(text) ? "<default>" : text);
		obj2[6] = " chainState=";
		obj2[7] = DescribeChainState(source, registration);
		LogDialogue(registration, string.Concat(obj2));
		return true;
	}

	private static bool TryHandleRexMarcusTradeHoldSideEffect(ICharacter source, ContentDrivenNpcDialogueRegistration registration, string previousNodeId, int answerIndex, Identity liveMarcusIdentity)
	{
		//IL_001e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0020: Unknown result type (might be due to invalid IL or missing references)
		//IL_0023: Unknown result type (might be due to invalid IL or missing references)
		//IL_002d: Invalid comparison between Unknown and I4
		//IL_0042: Unknown result type (might be due to invalid IL or missing references)
		//IL_0047: Unknown result type (might be due to invalid IL or missing references)
		//IL_005d: Unknown result type (might be due to invalid IL or missing references)
		//IL_007a: Unknown result type (might be due to invalid IL or missing references)
		if (!IsRegistration(registration, MarcusStoneRegistration) || answerIndex != 0)
		{
			return false;
		}
		Identity marcusIdentity = liveMarcusIdentity;
		if ((int)((Identity)(ref marcusIdentity)).Type != 50000 || ((Identity)(ref marcusIdentity)).Instance == 0)
		{
			marcusIdentity = registration.NpcIdentity;
		}
		if (string.Equals(previousNodeId, "marcus_return_001", StringComparison.OrdinalIgnoreCase))
		{
			return RexMarcusChainCoordinator.TryBeginMarcusReturnTrade(source, marcusIdentity);
		}
		if (string.Equals(previousNodeId, "marcus_heal_001", StringComparison.OrdinalIgnoreCase))
		{
			return MarcusWoundedWorkersQuestRuntime.TryBeginStimReturnTrade(source, marcusIdentity);
		}
		return false;
	}

	private static bool TryHandleAlexTradeHoldSideEffect(ICharacter source, ContentDrivenNpcDialogueRegistration registration, string previousNodeId, int answerIndex, Identity liveAlexIdentity)
	{
		//IL_0036: Unknown result type (might be due to invalid IL or missing references)
		//IL_0038: Unknown result type (might be due to invalid IL or missing references)
		//IL_003b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0045: Invalid comparison between Unknown and I4
		//IL_0064: Unknown result type (might be due to invalid IL or missing references)
		//IL_005c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0061: Unknown result type (might be due to invalid IL or missing references)
		if (!IsRegistration(registration, AlexGibbsRegistration) || answerIndex != 0)
		{
			return false;
		}
		if (!string.Equals(previousNodeId, "alex_074847_001", StringComparison.OrdinalIgnoreCase))
		{
			return false;
		}
		Identity alexIdentity = liveAlexIdentity;
		if ((int)((Identity)(ref alexIdentity)).Type != 50000 || ((Identity)(ref alexIdentity)).Instance == 0)
		{
			alexIdentity = registration.NpcIdentity;
		}
		return FlintBioComQuestRuntime.TryBeginAlexTrade(source, alexIdentity);
	}

	private static bool TryHandleBillTradeHoldSideEffect(ICharacter source, ContentDrivenNpcDialogueRegistration registration, string previousNodeId, int answerIndex, Identity liveBillIdentity)
	{
		//IL_0036: Unknown result type (might be due to invalid IL or missing references)
		//IL_0038: Unknown result type (might be due to invalid IL or missing references)
		//IL_003b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0045: Invalid comparison between Unknown and I4
		//IL_0064: Unknown result type (might be due to invalid IL or missing references)
		//IL_005c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0061: Unknown result type (might be due to invalid IL or missing references)
		if (!IsRegistration(registration, BillRegistration) || answerIndex != 0)
		{
			return false;
		}
		if (!string.Equals(previousNodeId, "bill_105157_001", StringComparison.OrdinalIgnoreCase))
		{
			return false;
		}
		Identity billIdentity = liveBillIdentity;
		if ((int)((Identity)(ref billIdentity)).Type != 50000 || ((Identity)(ref billIdentity)).Instance == 0)
		{
			billIdentity = registration.NpcIdentity;
		}
		return SurveillanceUplinkQuestRuntime.TryBeginBillTrade(source, billIdentity);
	}

	private static RexQuestPreviewEmissionResult TryHandleDialogueSideEffect(ICharacter source, ContentDrivenNpcDialogueRegistration registration, string previousNodeId, int answerIndex)
	{
		return RexQuestPreviewEmissionResult.NotApplicable();
	}

	private static MarcusB18FCompletionResult TryHandleMarcusB18FCompletion(ICharacter source, ContentDrivenNpcDialogueRegistration registration, string previousNodeId, int answerIndex, string optionText)
	{
		return MarcusB18FCompletionResult.NotApplicable();
	}

	private static bool TryHandleWindcallerSideEffect(ICharacter source, ContentDrivenNpcDialogueRegistration registration, string previousNodeId, int answerIndex)
	{
		//IL_0089: Unknown result type (might be due to invalid IL or missing references)
		//IL_009b: Unknown result type (might be due to invalid IL or missing references)
		//IL_004b: Unknown result type (might be due to invalid IL or missing references)
		if (IsRegistration(registration, WindcallerKarrecRegistration) && string.Equals(previousNodeId, "karrec_223626_005", StringComparison.OrdinalIgnoreCase) && answerIndex == 0)
		{
			MissionOperationResult missionOperationResult = WindcallerKarrecQuestRuntime.Accept(source);
			if (missionOperationResult.Status == MissionOperationStatus.Applied || missionOperationResult.Status == MissionOperationStatus.AlreadyApplied)
			{
				WindcallerKarrecPacketSender.TrySendQuestFullUpdate(source, registration.NpcIdentity);
			}
			return false;
		}
		if (IsRegistration(registration, WindcallerKarrecRegistration) && string.Equals(previousNodeId, "karrec_223626_return_offer", StringComparison.OrdinalIgnoreCase) && answerIndex == 0)
		{
			WindcallerKarrecTradeAdapter.BeginTrade(source, registration.NpcIdentity);
			BaseMessageHandler<KnuBotStartTradeMessage, KnuBotStartTradeMessageHandler>.Default.Send(source, registration.NpcIdentity, "Move the items you want to give to Windcaller Karrec into the available slots in the Give Item Tab on the right side of this window and press \"Accept'.", 2);
			return true;
		}
		if (IsRegistration(registration, AnnoyingDudeRegistration) && string.Equals(previousNodeId, "annoying_223626_006", StringComparison.OrdinalIgnoreCase) && answerIndex == 0)
		{
			WindcallerKarrecQuestRuntime.TryGrantBurger(source);
			return false;
		}
		if (IsRegistration(registration, MaddyCardileRegistration) && string.Equals(previousNodeId, "maddy_223626_004", StringComparison.OrdinalIgnoreCase) && answerIndex == 0)
		{
			WindcallerKarrecQuestRuntime.TryGrantCreditCard(source);
		}
		return false;
	}

	private static bool TryHandleThrakGardenKeySideEffect(ICharacter source, ContentDrivenNpcDialogueRegistration registration, string previousNodeId, int answerIndex)
	{
		//IL_006b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0082: Unknown result type (might be due to invalid IL or missing references)
		//IL_01a6: Unknown result type (might be due to invalid IL or missing references)
		//IL_01bd: Unknown result type (might be due to invalid IL or missing references)
		//IL_0134: Unknown result type (might be due to invalid IL or missing references)
		//IL_014b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0200: Unknown result type (might be due to invalid IL or missing references)
		//IL_0217: Unknown result type (might be due to invalid IL or missing references)
		//IL_0257: Unknown result type (might be due to invalid IL or missing references)
		//IL_026e: Unknown result type (might be due to invalid IL or missing references)
		if (IsRegistration(registration, VeronicaEscobarRegistration) && string.Equals(previousNodeId, "veronica_004", StringComparison.OrdinalIgnoreCase) && answerIndex == 0)
		{
			ThrakGardenKeyQuestRuntime.AcceptQuest(source, "Mission:5556893A");
			ThrakGardenKeyQuestRuntime.TryGrantAnalyzer(source);
			return false;
		}
		if (IsRegistration(registration, ProphetYuttRegistration) && string.Equals(previousNodeId, "prophet_001", StringComparison.OrdinalIgnoreCase) && answerIndex == 0)
		{
			ThrakGardenKeyTradeAdapter.BeginTrade(source, registration.NpcIdentity, "ProphetDevice");
			BaseMessageHandler<KnuBotStartTradeMessage, KnuBotStartTradeMessageHandler>.Default.Send(source, registration.NpcIdentity, "Drag and drop the item(s) you want to give to Prophet Yutt Thrak into one of the slots available and press \"accept\"", 1);
			return true;
		}
		if (IsRegistration(registration, ProphetYuttRegistration) && string.Equals(previousNodeId, "prophet_004", StringComparison.OrdinalIgnoreCase) && answerIndex == 0)
		{
			ThrakGardenKeyQuestRuntime.AcceptQuest(source, "Mission:55563C16");
			return false;
		}
		if (IsRegistration(registration, ProphetYuttRegistration) && (string.Equals(previousNodeId, "prophet_005", StringComparison.OrdinalIgnoreCase) || string.Equals(previousNodeId, "prophet_need_insignia", StringComparison.OrdinalIgnoreCase)) && answerIndex == 0)
		{
			if (!ThrakGardenKeyQuestRuntime.IsMissionActive(source, "Mission:55563C16") || !ThrakGardenKeyQuestRuntime.HasProphetDeviceInspected(source))
			{
				return false;
			}
			ThrakGardenKeyTradeAdapter.BeginTrade(source, registration.NpcIdentity, "ProphetInsignia");
			BaseMessageHandler<KnuBotStartTradeMessage, KnuBotStartTradeMessageHandler>.Default.Send(source, registration.NpcIdentity, "Drag and drop the item(s) you want to give to Prophet Yutt Thrak into one of the slots available and press \"accept\"", 1);
			return true;
		}
		if (IsRegistration(registration, HypnagogicUrgaLumRegistration) && ((string.Equals(previousNodeId, "hyp_001", StringComparison.OrdinalIgnoreCase) && answerIndex == 1) || (string.Equals(previousNodeId, "hyp_002", StringComparison.OrdinalIgnoreCase) && answerIndex == 0)))
		{
			ThrakGardenKeyTradeAdapter.BeginTrade(source, registration.NpcIdentity, "HypAnalyzer");
			BaseMessageHandler<KnuBotStartTradeMessage, KnuBotStartTradeMessageHandler>.Default.Send(source, registration.NpcIdentity, "Drag and drop the item(s) you want to give to Hypnagogic Urga-Lum Thrak into one of the slots available and press \"accept\"", 1);
			return true;
		}
		if (IsRegistration(registration, HypnagogicUrgaLumRegistration) && string.Equals(previousNodeId, "hyp_return", StringComparison.OrdinalIgnoreCase) && answerIndex == 0)
		{
			ThrakGardenKeyTradeAdapter.BeginTrade(source, registration.NpcIdentity, "HypReturn");
			BaseMessageHandler<KnuBotStartTradeMessage, KnuBotStartTradeMessageHandler>.Default.Send(source, registration.NpcIdentity, "Drag and drop the item(s) you want to give to Hypnagogic Urga-Lum Thrak into one of the slots available and press \"accept\"", 1);
			return true;
		}
		if (IsRegistration(registration, DreamingSilvertailRegistration) && string.Equals(previousNodeId, "silver_001", StringComparison.OrdinalIgnoreCase) && answerIndex == 0)
		{
			ThrakGardenKeyTradeAdapter.BeginTrade(source, registration.NpcIdentity, "Silvertail");
			BaseMessageHandler<KnuBotStartTradeMessage, KnuBotStartTradeMessageHandler>.Default.Send(source, registration.NpcIdentity, "Drag and drop the item(s) you want to give to Dreaming Silvertail into one of the slots available and press \"accept\"", 1);
			return true;
		}
		return false;
	}

	private static bool TryHandleThrakGardenVendorSideEffect(ICharacter source, ContentDrivenNpcDialogueRegistration registration, string previousNodeId, int answerIndex)
	{
		//IL_002e: Unknown result type (might be due to invalid IL or missing references)
		if (!IsThrakGardenCraigOrRegistration(registration) || !string.Equals(previousNodeId, "craig_or_001", StringComparison.OrdinalIgnoreCase) || answerIndex != 0)
		{
			return false;
		}
		CapturedThrakGardenVendorInteractionHandler.Default.TryOpenShop(source, registration.NpcIdentity);
		return false;
	}

	private static bool IsThrakGardenCraigOrRegistration(ContentDrivenNpcDialogueRegistration registration)
	{
		return IsRegistration(registration, CraigOrFuriousFistsRegistration) || IsRegistration(registration, CraigOrPreservationRegistration) || IsRegistration(registration, CraigOrFlamingBarrelsRegistration) || IsRegistration(registration, CraigOrGearAndAmmoRegistration) || IsRegistration(registration, CraigOrProtectionRegistration);
	}

	private static string ResolveRequestedStartNodeId(ICharacter source, ContentDrivenNpcDialogueRegistration registration)
	{
		if (IsRegistration(registration, SubwayTailorRegistration))
		{
			lock (SyncRoot)
			{
				object value;
				bool flag = TailorOpenHistoryByCharacter.TryGetValue(source, out value);
				if (!flag)
				{
					TailorOpenHistoryByCharacter.Add(source, new object());
				}
				return CapturedSubwayTailorDialogueContent.ResolveRootNodeId(flag);
			}
		}
		if (IsRegistration(registration, WindcallerKarrecRegistration))
		{
			return WindcallerKarrecQuestRuntime.HasBothOfferingItems(source) ? "karrec_223626_return_offer" : null;
		}
		if (IsRegistration(registration, AlexGibbsRegistration))
		{
			return KneecappingQuestRuntime.ResolveAlexStartNodeId(source);
		}
		if (IsRegistration(registration, ProphetYuttRegistration))
		{
			if (!ThrakGardenKeyQuestRuntime.HasProphetDeviceInspected(source))
			{
				return null;
			}
			if (ThrakGardenKeyQuestRuntime.IsMissionActive(source, "Mission:55563C16"))
			{
				ThrakGardenKeyQuestRuntime.ApplyInsigniaCommitmentHandoff(source);
				return "prophet_need_insignia";
			}
			return null;
		}
		if (IsRegistration(registration, HypnagogicUrgaLumRegistration))
		{
			if (ThrakGardenKeyQuestRuntime.IsMissionActive(source, "Mission:5556591A") || ThrakGardenKeyQuestRuntime.IsMissionActive(source, "Mission:5556893D") || ThrakGardenKeyQuestRuntime.IsMissionCompleted(source, "Mission:55563C18"))
			{
				ThrakGardenKeyQuestRuntime.TryForceReturnAncientDevice(source);
			}
			if (ThrakGardenKeyQuestRuntime.GetSoulCount(source) >= 3 || ThrakGardenKeyQuestRuntime.IsMissionActive(source, "Mission:5556893D") || ThrakGardenKeyQuestRuntime.IsMissionCompleted(source, "Mission:5556591A"))
			{
				return "hyp_return";
			}
			return null;
		}
		if (!IsRegistration(registration, RexLarssonRegistration) && !IsRegistration(registration, MarcusStoneRegistration))
		{
			return null;
		}
		if (IsRegistration(registration, MarcusStoneRegistration))
		{
			return RexMarcusChainCoordinator.ResolveMarcusStartNodeId(source);
		}
		return RexMarcusChainCoordinator.ResolveRexStartNodeId(source);
	}

	private static string DescribeChainState(ICharacter source, ContentDrivenNpcDialogueRegistration registration)
	{
		if (IsRegistration(registration, RexLarssonRegistration) || IsRegistration(registration, MarcusStoneRegistration))
		{
			return RexMarcusChainCoordinator.GetPhase(source).ToString();
		}
		if (IsRegistration(registration, WindcallerKarrecRegistration) || IsRegistration(registration, AnnoyingDudeRegistration) || IsRegistration(registration, MaddyCardileRegistration))
		{
			return WindcallerKarrecQuestRuntime.IsCompleted(source) ? "Completed" : (WindcallerKarrecQuestRuntime.IsActive(source) ? "Active" : "NotStarted");
		}
		return "<none>";
	}

	private static string ResolveSelectedOptionText(DialogueSessionService service, DialogueSession session, int answerIndex)
	{
		if (service == null || session == null)
		{
			return null;
		}
		return service.ListAvailableOptions(session).FirstOrDefault((DialogueOption option) => option != null && option.Index == answerIndex)?.Text;
	}

	private static void FaceNpcTowardSource(ICharacter npc, ICharacter source)
	{
		((npc == null) ? null : (((IDynel)npc).Controller as NPCController))?.FaceDialoguePartner(source);
	}

	private static bool HasActiveSession(ICharacter source, ContentDrivenNpcDialogueRegistration registration)
	{
		//IL_002d: Unknown result type (might be due to invalid IL or missing references)
		if (source == null || registration == null)
		{
			return false;
		}
		DialogueSessionRecord value;
		lock (SyncRoot)
		{
			SessionsByCharacter.TryGetValue(CreateSessionKey(((IEntity)source).Identity, registration), out value);
		}
		return value != null && value.Session != null && value.Session.IsActive;
	}

	private static ContentDrivenNpcDialogueRegistration FindActiveSessionRegistration(ICharacter source)
	{
		//IL_003d: Unknown result type (might be due to invalid IL or missing references)
		if (source == null)
		{
			return null;
		}
		ContentDrivenNpcDialogueRegistration[] registrations = Registrations;
		foreach (ContentDrivenNpcDialogueRegistration registration in registrations)
		{
			DialogueSessionRecord value;
			lock (SyncRoot)
			{
				SessionsByCharacter.TryGetValue(CreateSessionKey(((IEntity)source).Identity, registration), out value);
			}
			if (value != null && value.Session != null && value.Session.IsActive)
			{
				return value.Registration;
			}
		}
		return null;
	}

	private static bool TryGetSessionService(ContentDrivenNpcDialogueRegistration registration, out DialogueSessionService service)
	{
		lock (SyncRoot)
		{
			if (sharedDialogueSessionService == null)
			{
				AreteFrameworkRegistries current;
				try
				{
					current = AreteFrameworkBootstrap.Current;
				}
				catch (Exception ex)
				{
					LogSkipped(registration, "central content bootstrap failed: " + ex.Message);
					service = null;
					return false;
				}
				if (current == null || !current.IsValid)
				{
					service = null;
					return false;
				}
				sharedDialogueSessionService = new DialogueSessionService(current.DialogueRegistry);
			}
			service = sharedDialogueSessionService;
			return service != null;
		}
	}

	private static void SendDialogueNode(ICharacter source, DialogueSessionResult result, ContentDrivenNpcDialogueRegistration registration, Func<bool> afterPromptBeforeOptions = null)
	{
		//IL_00e7: Unknown result type (might be due to invalid IL or missing references)
		//IL_007d: Unknown result type (might be due to invalid IL or missing references)
		//IL_01c8: Unknown result type (might be due to invalid IL or missing references)
		//IL_01ab: Unknown result type (might be due to invalid IL or missing references)
		//IL_0229: Unknown result type (might be due to invalid IL or missing references)
		//IL_022e: Unknown result type (might be due to invalid IL or missing references)
		DialogueNode currentNode = result.CurrentNode;
		bool flag = false;
		if (currentNode != null && currentNode.PromptSegments != null && currentNode.PromptSegments.Count > 0)
		{
			foreach (DialoguePromptSegment promptSegment in currentNode.PromptSegments)
			{
				if (promptSegment != null && promptSegment.Text != null)
				{
					BaseMessageHandler<KnuBotAppendTextMessage, KnuBotAppendTextMessageHandler>.Default.Send(source, registration.NpcIdentity, NormalizeDialoguePromptText(promptSegment.Text), promptSegment.Unknown2);
					PaceKnuBotPackets();
					flag = true;
				}
			}
		}
		if (!flag && currentNode != null && !string.IsNullOrWhiteSpace(currentNode.PromptText))
		{
			BaseMessageHandler<KnuBotAppendTextMessage, KnuBotAppendTextMessageHandler>.Default.Send(source, registration.NpcIdentity, NormalizeDialoguePromptText(currentNode.PromptText));
			PaceKnuBotPackets();
		}
		bool flag2 = false;
		if (afterPromptBeforeOptions != null)
		{
			flag2 = afterPromptBeforeOptions();
			PaceKnuBotPackets();
		}
		if (!flag2)
		{
			string[] array = (from option in result.AvailableOptions
				orderby option.Index
				select FormatDialogueOptionText(source, option.Text) into text
				where !string.IsNullOrWhiteSpace(text)
				select text).ToArray();
			if (array.Length == 0)
			{
				BaseMessageHandler<KnuBotCloseChatWindowMessage, KnuBotCloseChatWindowMessageHandler>.Default.Send(source, registration.NpcIdentity);
				return;
			}
			BaseMessageHandler<KnuBotAnswerListMessage, KnuBotAnswerListMessageHandler>.Default.Send(source, registration.NpcIdentity, array);
			string[] obj = new string[6]
			{
				"sent node=",
				(result.CurrentNode == null) ? "<none>" : result.CurrentNode.Id,
				" options=",
				array.Length.ToString(),
				" character=",
				null
			};
			Identity identity = ((IEntity)source).Identity;
			obj[5] = ((Identity)(ref identity)).ToString(true);
			LogDialogue(registration, string.Concat(obj));
		}
	}

	private static bool CloseRegisteredDialogueSafely(ICharacter source, ICharacter npc, ContentDrivenNpcDialogueRegistration registration)
	{
		//IL_001e: Unknown result type (might be due to invalid IL or missing references)
		FaceNpcTowardSource(npc, source);
		SendOpenChatWindow(source, registration);
		PaceKnuBotPackets();
		BaseMessageHandler<KnuBotCloseChatWindowMessage, KnuBotCloseChatWindowMessageHandler>.Default.Send(source, registration.NpcIdentity);
		return true;
	}

	private static void SendOpenChatWindow(ICharacter source, ContentDrivenNpcDialogueRegistration registration)
	{
		//IL_002d: Unknown result type (might be due to invalid IL or missing references)
		int unknown = 1;
		if (IsRegistration(registration, SubwayTailorRegistration) || IsRegistration(registration, RexLarssonRegistration))
		{
			unknown = 0;
		}
		BaseMessageHandler<KnuBotOpenChatWindowMessage, KnuBotOpenChatWindowMessageHandler>.Default.Send(source, registration.NpcIdentity, unknown);
	}

	private static void CloseSession(ICharacter source, string sessionKey, ContentDrivenNpcDialogueRegistration registration, bool sendClose)
	{
		//IL_004e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0053: Unknown result type (might be due to invalid IL or missing references)
		//IL_003a: Unknown result type (might be due to invalid IL or missing references)
		lock (SyncRoot)
		{
			SessionsByCharacter.Remove(sessionKey);
		}
		if (sendClose)
		{
			BaseMessageHandler<KnuBotCloseChatWindowMessage, KnuBotCloseChatWindowMessageHandler>.Default.Send(source, registration.NpcIdentity);
		}
		Identity identity = ((IEntity)source).Identity;
		LogDialogue(registration, "session ended character=" + ((Identity)(ref identity)).ToString(true));
	}

	private static void LogRecordedActions(ICharacter source, DialogueSessionResult result, ContentDrivenNpcDialogueRegistration registration)
	{
		//IL_0036: Unknown result type (might be due to invalid IL or missing references)
		//IL_003b: Unknown result type (might be due to invalid IL or missing references)
		int num = ((result.RecordedActions != null) ? result.RecordedActions.Count : 0);
		if (num != 0)
		{
			string text = num.ToString();
			Identity identity = ((IEntity)source).Identity;
			LogDialogue(registration, "recorded " + text + " no-op action(s) for character=" + ((Identity)(ref identity)).ToString(true));
		}
	}

	private static void LogQuestPreviewResult(RexQuestPreviewEmissionResult result, ContentDrivenNpcDialogueRegistration registration)
	{
		if (result != null && result.IsApplicable && !string.IsNullOrWhiteSpace(result.Message))
		{
			LogDialogue(registration, result.Message);
		}
	}

	private static void LogB18ECompletionResult(RexB18ECompletionResult result, ContentDrivenNpcDialogueRegistration registration)
	{
		if (result != null && result.IsApplicable && !string.IsNullOrWhiteSpace(result.Message))
		{
			LogDialogue(registration, result.Message);
		}
	}

	private static void LogMarcusB18FCompletionResult(MarcusB18FCompletionResult result, ContentDrivenNpcDialogueRegistration registration)
	{
		if (result != null && result.IsApplicable && !string.IsNullOrWhiteSpace(result.Message))
		{
			LogDialogue(registration, result.Message);
		}
	}

	private static void LogMarcusB196CompletionResult(MarcusB196CompletionResult result, ContentDrivenNpcDialogueRegistration registration)
	{
		if (result != null && result.IsApplicable && !string.IsNullOrWhiteSpace(result.Message))
		{
			LogDialogue(registration, result.Message);
		}
	}

	private static void PaceKnuBotPackets()
	{
		Thread.Sleep(20);
	}

	private static string NormalizeDialoguePromptText(string text)
	{
		if (string.IsNullOrEmpty(text))
		{
			return text;
		}
		return text.Replace("\\n", "\n");
	}

	private static string FormatDialogueOptionText(ICharacter source, string text)
	{
		if (string.IsNullOrEmpty(text) || text.IndexOf("{player}", StringComparison.OrdinalIgnoreCase) < 0)
		{
			return text;
		}
		string text2 = ((source == null) ? null : ((INamedEntity)source).Name);
		if (string.IsNullOrWhiteSpace(text2))
		{
			text2 = "stranger";
		}
		return text.Replace("{player}", text2);
	}

	private static void LogDialogue(ContentDrivenNpcDialogueRegistration registration, string message)
	{
		LogUtil.Debug((DebugInfoDetail)128, registration.LogPrefix + " " + message);
	}

	private static void LogSkipped(ContentDrivenNpcDialogueRegistration registration, string message)
	{
		LogUtil.Debug((DebugInfoDetail)4096, "Content-driven NPC dialogue for " + registration.Name + " " + message);
	}

	private static void LogValidation(ContentDrivenNpcDialogueRegistration registration, string prefix, AreteValidationResult validation)
	{
		if (validation == null)
		{
			LogUtil.Debug((DebugInfoDetail)4096, "Content-driven NPC dialogue for " + registration.Name + " " + prefix + ": validation result was missing.");
			return;
		}
		foreach (string error in validation.Errors)
		{
			LogUtil.Debug((DebugInfoDetail)4096, "Content-driven NPC dialogue for " + registration.Name + " " + prefix + ": " + error);
		}
	}

	private static ICharacter ResolveCharacter(ICharacter npc, Identity sourceIdentity)
	{
		//IL_0024: Unknown result type (might be due to invalid IL or missing references)
		//IL_0029: Unknown result type (might be due to invalid IL or missing references)
		if (npc == null || ((IInstancedEntity)npc).Playfield == null)
		{
			return null;
		}
		return Pool.Instance.GetObject<ICharacter>(((IEntity)((IInstancedEntity)npc).Playfield).Identity, sourceIdentity);
	}

	private static ContentDrivenNpcDialogueRegistration FindRegistration(ICharacter npc)
	{
		//IL_0054: Unknown result type (might be due to invalid IL or missing references)
		//IL_0090: Unknown result type (might be due to invalid IL or missing references)
		if (npc == null)
		{
			return null;
		}
		ContentDrivenNpcDialogueRegistration contentDrivenNpcDialogueRegistration = FindCapturedSubwayVendorRuntimeRegistration(npc) ?? FindWindcallerRuntimeRegistration(npc);
		if (contentDrivenNpcDialogueRegistration != null)
		{
			return contentDrivenNpcDialogueRegistration;
		}
		ContentDrivenNpcDialogueRegistration contentDrivenNpcDialogueRegistration2 = FindRegistration(((IEntity)npc).Identity);
		if (contentDrivenNpcDialogueRegistration2 != null)
		{
			return contentDrivenNpcDialogueRegistration2;
		}
		ContentDrivenNpcDialogueRegistration contentDrivenNpcDialogueRegistration3 = Registrations.FirstOrDefault((ContentDrivenNpcDialogueRegistration registration) => !IsRuntimeBoundRegistration(registration) && !string.IsNullOrWhiteSpace(registration.ExpectedNpcName) && string.Equals(((INamedEntity)npc).Name, registration.ExpectedNpcName, StringComparison.OrdinalIgnoreCase));
		return (contentDrivenNpcDialogueRegistration3 == null) ? null : BindRegistration(contentDrivenNpcDialogueRegistration3, ((IEntity)npc).Identity);
	}

	private static ContentDrivenNpcDialogueRegistration FindRegistration(Identity identity)
	{
		//IL_0060: Unknown result type (might be due to invalid IL or missing references)
		//IL_0084: Unknown result type (might be due to invalid IL or missing references)
		if (WindcallerKarrecNpcRuntimeRegistry.TryGet(((Identity)(ref identity)).Instance, out var runtime) && runtime != null && runtime.Content != null)
		{
			ContentDrivenNpcDialogueRegistration contentDrivenNpcDialogueRegistration = Registrations.FirstOrDefault(delegate(ContentDrivenNpcDialogueRegistration candidate)
			{
				//IL_0009: Unknown result type (might be due to invalid IL or missing references)
				//IL_000e: Unknown result type (might be due to invalid IL or missing references)
				int result;
				if (IsWindcallerQuestRegistration(candidate))
				{
					Identity npcIdentity = candidate.NpcIdentity;
					result = ((((Identity)(ref npcIdentity)).Instance == runtime.Content.SourceNpcInstance) ? 1 : 0);
				}
				else
				{
					result = 0;
				}
				return (byte)result != 0;
			});
			if (contentDrivenNpcDialogueRegistration != null)
			{
				return BindRegistration(contentDrivenNpcDialogueRegistration, runtime.NpcIdentity);
			}
		}
		ContentDrivenNpcDialogueRegistration[] registrations = Registrations;
		foreach (ContentDrivenNpcDialogueRegistration contentDrivenNpcDialogueRegistration2 in registrations)
		{
			if (IsRegisteredIdentity(identity, contentDrivenNpcDialogueRegistration2))
			{
				return contentDrivenNpcDialogueRegistration2;
			}
		}
		return null;
	}

	private static bool IsRegisteredNpc(ICharacter npc, ContentDrivenNpcDialogueRegistration registration)
	{
		//IL_0005: Unknown result type (might be due to invalid IL or missing references)
		return npc != null && (IsRegisteredIdentity(((IEntity)npc).Identity, registration) || (!IsRuntimeBoundRegistration(registration) && !string.IsNullOrWhiteSpace(registration.ExpectedNpcName) && string.Equals(((INamedEntity)npc).Name, registration.ExpectedNpcName, StringComparison.OrdinalIgnoreCase)));
	}

	private static ContentDrivenNpcDialogueRegistration FindCapturedSubwayVendorRuntimeRegistration(ICharacter npc)
	{
		//IL_001a: Unknown result type (might be due to invalid IL or missing references)
		//IL_001f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0079: Unknown result type (might be due to invalid IL or missing references)
		//IL_004f: Unknown result type (might be due to invalid IL or missing references)
		//IL_005a: Unknown result type (might be due to invalid IL or missing references)
		if (npc == null || ((IInstancedEntity)npc).Playfield == null)
		{
			return null;
		}
		Identity identity = ((IEntity)npc).Identity;
		if (!CapturedSubwayVendorRuntimeRegistry.TryGet(((Identity)(ref identity)).Instance, out var runtime) || runtime == null || runtime.Content == null || runtime.Content.SourceNpcInstance != 2031312721 || !CapturedSubwayVendorRuntimeRegistry.Same(runtime.PlayfieldIdentity, ((IEntity)((IInstancedEntity)npc).Playfield).Identity))
		{
			return null;
		}
		return BindRegistration(SubwayTailorRegistration, runtime.NpcIdentity);
	}

	private static ContentDrivenNpcDialogueRegistration FindWindcallerRuntimeRegistration(ICharacter npc)
	{
		//IL_0025: Unknown result type (might be due to invalid IL or missing references)
		//IL_002b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0082: Unknown result type (might be due to invalid IL or missing references)
		if (npc == null || ((IInstancedEntity)npc).Playfield == null)
		{
			return null;
		}
		if (!WindcallerKarrecNpcRuntimeRegistry.TryGet(((IEntity)((IInstancedEntity)npc).Playfield).Identity, ((IEntity)npc).Identity, out var runtime) || runtime == null || runtime.Content == null)
		{
			return null;
		}
		ContentDrivenNpcDialogueRegistration contentDrivenNpcDialogueRegistration = Registrations.FirstOrDefault(delegate(ContentDrivenNpcDialogueRegistration candidate)
		{
			//IL_0009: Unknown result type (might be due to invalid IL or missing references)
			//IL_000e: Unknown result type (might be due to invalid IL or missing references)
			int result;
			if (IsWindcallerQuestRegistration(candidate))
			{
				Identity npcIdentity = candidate.NpcIdentity;
				result = ((((Identity)(ref npcIdentity)).Instance == runtime.Content.SourceNpcInstance) ? 1 : 0);
			}
			else
			{
				result = 0;
			}
			return (byte)result != 0;
		});
		return (contentDrivenNpcDialogueRegistration == null) ? null : BindRegistration(contentDrivenNpcDialogueRegistration, runtime.NpcIdentity);
	}

	private static ContentDrivenNpcDialogueRegistration BindRegistration(ContentDrivenNpcDialogueRegistration registration, Identity npcIdentity)
	{
		//IL_0021: Unknown result type (might be due to invalid IL or missing references)
		return new ContentDrivenNpcDialogueRegistration
		{
			Name = registration.Name,
			ExpectedNpcName = registration.ExpectedNpcName,
			NpcIdentity = npcIdentity,
			NpcIdentityText = registration.NpcIdentityText,
			PlayfieldId = registration.PlayfieldId,
			GateEnvironmentVariableName = registration.GateEnvironmentVariableName,
			LogPrefix = registration.LogPrefix
		};
	}

	private static bool IsWindcallerQuestRegistration(ContentDrivenNpcDialogueRegistration registration)
	{
		return IsRegistration(registration, WindcallerKarrecRegistration) || IsRegistration(registration, AnnoyingDudeRegistration) || IsRegistration(registration, MaddyCardileRegistration);
	}

	private static bool IsRuntimeBoundRegistration(ContentDrivenNpcDialogueRegistration registration)
	{
		return IsWindcallerQuestRegistration(registration) || IsRegistration(registration, SubwayTailorRegistration);
	}

	private static bool IsRegistration(ContentDrivenNpcDialogueRegistration registration, ContentDrivenNpcDialogueRegistration expected)
	{
		return registration != null && expected != null && (registration == expected || string.Equals(registration.NpcIdentityText, expected.NpcIdentityText, StringComparison.OrdinalIgnoreCase));
	}

	private static bool IsRegisteredIdentity(Identity identity, ContentDrivenNpcDialogueRegistration registration)
	{
		//IL_0006: Unknown result type (might be due to invalid IL or missing references)
		//IL_000c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0011: Unknown result type (might be due to invalid IL or missing references)
		//IL_0014: Unknown result type (might be due to invalid IL or missing references)
		//IL_0023: Unknown result type (might be due to invalid IL or missing references)
		//IL_0028: Unknown result type (might be due to invalid IL or missing references)
		int result;
		if (registration != null)
		{
			IdentityType type = ((Identity)(ref identity)).Type;
			Identity npcIdentity = registration.NpcIdentity;
			if (type == ((Identity)(ref npcIdentity)).Type)
			{
				int instance = ((Identity)(ref identity)).Instance;
				npcIdentity = registration.NpcIdentity;
				result = ((instance == ((Identity)(ref npcIdentity)).Instance) ? 1 : 0);
				goto IL_0035;
			}
		}
		result = 0;
		goto IL_0035;
		IL_0035:
		return (byte)result != 0;
	}

	private static bool IsExpectedPlayfield(ICharacter character, ContentDrivenNpcDialogueRegistration registration)
	{
		//IL_0032: Unknown result type (might be due to invalid IL or missing references)
		//IL_0037: Unknown result type (might be due to invalid IL or missing references)
		if (registration == null || !registration.PlayfieldId.HasValue)
		{
			return true;
		}
		int result;
		if (character != null && ((IInstancedEntity)character).Playfield != null)
		{
			Identity identity = ((IEntity)((IInstancedEntity)character).Playfield).Identity;
			result = ((((Identity)(ref identity)).Instance == registration.PlayfieldId.Value) ? 1 : 0);
		}
		else
		{
			result = 0;
		}
		return (byte)result != 0;
	}

	private static string CreateSessionKey(Identity characterIdentity, ContentDrivenNpcDialogueRegistration registration)
	{
		//IL_000b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0010: Unknown result type (might be due to invalid IL or missing references)
		string[] array = new string[5];
		IdentityType type = ((Identity)(ref characterIdentity)).Type;
		array[0] = ((object)(IdentityType)(ref type)).ToString();
		array[1] = ":";
		array[2] = ((Identity)(ref characterIdentity)).Instance.ToString();
		array[3] = "|";
		array[4] = registration.NpcIdentityText;
		return string.Concat(array);
	}

	private static bool IsRegistrationEnabled(ContentDrivenNpcDialogueRegistration registration)
	{
		if (registration == null)
		{
			return false;
		}
		if (string.IsNullOrWhiteSpace(registration.GateEnvironmentVariableName))
		{
			return true;
		}
		return AreteEnvironmentGate.IsDefaultEnabled(registration.GateEnvironmentVariableName);
	}

	static ContentDrivenNpcDialogueRouter()
	{
		//IL_0020: Unknown result type (might be due to invalid IL or missing references)
		//IL_0040: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a1: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c1: Unknown result type (might be due to invalid IL or missing references)
		//IL_0122: Unknown result type (might be due to invalid IL or missing references)
		//IL_0142: Unknown result type (might be due to invalid IL or missing references)
		//IL_01a3: Unknown result type (might be due to invalid IL or missing references)
		//IL_01c3: Unknown result type (might be due to invalid IL or missing references)
		//IL_0224: Unknown result type (might be due to invalid IL or missing references)
		//IL_0244: Unknown result type (might be due to invalid IL or missing references)
		//IL_02d2: Unknown result type (might be due to invalid IL or missing references)
		//IL_02f2: Unknown result type (might be due to invalid IL or missing references)
		//IL_0350: Unknown result type (might be due to invalid IL or missing references)
		//IL_0370: Unknown result type (might be due to invalid IL or missing references)
		//IL_03cd: Unknown result type (might be due to invalid IL or missing references)
		//IL_03ed: Unknown result type (might be due to invalid IL or missing references)
		//IL_044a: Unknown result type (might be due to invalid IL or missing references)
		//IL_046a: Unknown result type (might be due to invalid IL or missing references)
		//IL_04c7: Unknown result type (might be due to invalid IL or missing references)
		//IL_04e7: Unknown result type (might be due to invalid IL or missing references)
		ContentDrivenNpcDialogueRegistration obj = new ContentDrivenNpcDialogueRegistration
		{
			Name = "Rex Larsson",
			ExpectedNpcName = "Rex Larsson"
		};
		Identity npcIdentity = default(Identity);
		((Identity)(ref npcIdentity)).Type = (IdentityType)50000;
		((Identity)(ref npcIdentity)).Instance = 2016273768;
		obj.NpcIdentity = npcIdentity;
		obj.NpcIdentityText = "SimpleChar:782DE568";
		obj.PlayfieldId = 6553;
		obj.GateEnvironmentVariableName = "AO_REBIRTH_ENABLE_ARETE_REX_DIALOGUE_ROUTING";
		obj.LogPrefix = "ARETE_REX_DIALOGUE";
		RexLarssonRegistration = obj;
		ContentDrivenNpcDialogueRegistration obj2 = new ContentDrivenNpcDialogueRegistration
		{
			Name = "Marcus Stone",
			ExpectedNpcName = "Marcus Stone"
		};
		npcIdentity = default(Identity);
		((Identity)(ref npcIdentity)).Type = (IdentityType)50000;
		((Identity)(ref npcIdentity)).Instance = 2016273767;
		obj2.NpcIdentity = npcIdentity;
		obj2.NpcIdentityText = "SimpleChar:782DE567";
		obj2.PlayfieldId = 6553;
		obj2.GateEnvironmentVariableName = "AO_REBIRTH_ENABLE_ARETE_REX_DIALOGUE_ROUTING";
		obj2.LogPrefix = "ARETE_MARCUS_DIALOGUE";
		MarcusStoneRegistration = obj2;
		ContentDrivenNpcDialogueRegistration obj3 = new ContentDrivenNpcDialogueRegistration
		{
			Name = "Flint Novak",
			ExpectedNpcName = "Flint Novak"
		};
		npcIdentity = default(Identity);
		((Identity)(ref npcIdentity)).Type = (IdentityType)50000;
		((Identity)(ref npcIdentity)).Instance = 2028010596;
		obj3.NpcIdentity = npcIdentity;
		obj3.NpcIdentityText = "SimpleChar:78E0FC64";
		obj3.PlayfieldId = 6553;
		obj3.GateEnvironmentVariableName = "AO_REBIRTH_ENABLE_ARETE_REX_DIALOGUE_ROUTING";
		obj3.LogPrefix = "ARETE_FLINT_DIALOGUE";
		FlintNovakRegistration = obj3;
		ContentDrivenNpcDialogueRegistration obj4 = new ContentDrivenNpcDialogueRegistration
		{
			Name = "Alex Gibbs",
			ExpectedNpcName = "Alex Gibbs"
		};
		npcIdentity = default(Identity);
		((Identity)(ref npcIdentity)).Type = (IdentityType)50000;
		((Identity)(ref npcIdentity)).Instance = 2028010593;
		obj4.NpcIdentity = npcIdentity;
		obj4.NpcIdentityText = "SimpleChar:78E0FC61";
		obj4.PlayfieldId = 6553;
		obj4.GateEnvironmentVariableName = "AO_REBIRTH_ENABLE_ARETE_REX_DIALOGUE_ROUTING";
		obj4.LogPrefix = "ARETE_ALEX_DIALOGUE";
		AlexGibbsRegistration = obj4;
		ContentDrivenNpcDialogueRegistration obj5 = new ContentDrivenNpcDialogueRegistration
		{
			Name = "ICC Immigration Officer Bill",
			ExpectedNpcName = "ICC Immigration Officer Bill"
		};
		npcIdentity = default(Identity);
		((Identity)(ref npcIdentity)).Type = (IdentityType)50000;
		((Identity)(ref npcIdentity)).Instance = 2028010598;
		obj5.NpcIdentity = npcIdentity;
		obj5.NpcIdentityText = "SimpleChar:78E0FC66";
		obj5.PlayfieldId = 6553;
		obj5.GateEnvironmentVariableName = "AO_REBIRTH_ENABLE_ARETE_REX_DIALOGUE_ROUTING";
		obj5.LogPrefix = "ARETE_BILL_DIALOGUE";
		BillRegistration = obj5;
		WindcallerKarrecRegistration = CreateWindcallerRegistration(WindcallerKarrecNpcContent.Karrec);
		AnnoyingDudeRegistration = CreateWindcallerRegistration(WindcallerKarrecNpcContent.AnnoyingDude);
		MaddyCardileRegistration = CreateWindcallerRegistration(WindcallerKarrecNpcContent.MaddyCardile);
		ContentDrivenNpcDialogueRegistration obj6 = new ContentDrivenNpcDialogueRegistration
		{
			Name = "Tailor",
			ExpectedNpcName = "Tailor"
		};
		npcIdentity = default(Identity);
		((Identity)(ref npcIdentity)).Type = (IdentityType)50000;
		((Identity)(ref npcIdentity)).Instance = 2031312721;
		obj6.NpcIdentity = npcIdentity;
		obj6.NpcIdentityText = "SimpleChar:79135F51";
		obj6.PlayfieldId = 127;
		obj6.GateEnvironmentVariableName = "AO_REBIRTH_ENABLE_SUBWAY_TAILOR_DIALOGUE_ROUTING";
		obj6.LogPrefix = "SUBWAY_TAILOR_DIALOGUE";
		SubwayTailorRegistration = obj6;
		ContentDrivenNpcDialogueRegistration obj7 = new ContentDrivenNpcDialogueRegistration
		{
			Name = "Scientist Veronica Escobar",
			ExpectedNpcName = "Scientist Veronica Escobar"
		};
		npcIdentity = default(Identity);
		((Identity)(ref npcIdentity)).Type = (IdentityType)50000;
		((Identity)(ref npcIdentity)).Instance = 2021348530;
		obj7.NpcIdentity = npcIdentity;
		obj7.NpcIdentityText = "SimpleChar:787B54B2";
		obj7.PlayfieldId = 4310;
		obj7.GateEnvironmentVariableName = null;
		obj7.LogPrefix = "THRAK_GARDEN_KEY";
		VeronicaEscobarRegistration = obj7;
		ContentDrivenNpcDialogueRegistration obj8 = new ContentDrivenNpcDialogueRegistration
		{
			Name = "Prophet Yutt Thrak",
			ExpectedNpcName = "Prophet Yutt Thrak"
		};
		npcIdentity = default(Identity);
		((Identity)(ref npcIdentity)).Type = (IdentityType)50000;
		((Identity)(ref npcIdentity)).Instance = 2027061494;
		obj8.NpcIdentity = npcIdentity;
		obj8.NpcIdentityText = "SimpleChar:78D280F6";
		obj8.PlayfieldId = 4311;
		obj8.GateEnvironmentVariableName = null;
		obj8.LogPrefix = "THRAK_GARDEN_KEY";
		ProphetYuttRegistration = obj8;
		ContentDrivenNpcDialogueRegistration obj9 = new ContentDrivenNpcDialogueRegistration
		{
			Name = "Hypnagogic Urga-Lum Thrak",
			ExpectedNpcName = "Hypnagogic Urga-Lum Thrak"
		};
		npcIdentity = default(Identity);
		((Identity)(ref npcIdentity)).Type = (IdentityType)50000;
		((Identity)(ref npcIdentity)).Instance = 2037747514;
		obj9.NpcIdentity = npcIdentity;
		obj9.NpcIdentityText = "SimpleChar:79758F3A";
		obj9.PlayfieldId = 4677;
		obj9.GateEnvironmentVariableName = null;
		obj9.LogPrefix = "THRAK_GARDEN_KEY";
		HypnagogicUrgaLumRegistration = obj9;
		ContentDrivenNpcDialogueRegistration obj10 = new ContentDrivenNpcDialogueRegistration
		{
			Name = "Dreaming Silvertail",
			ExpectedNpcName = "Dreaming Silvertail"
		};
		npcIdentity = default(Identity);
		((Identity)(ref npcIdentity)).Type = (IdentityType)50000;
		((Identity)(ref npcIdentity)).Instance = 2037797536;
		obj10.NpcIdentity = npcIdentity;
		obj10.NpcIdentityText = "SimpleChar:797652A0";
		obj10.PlayfieldId = 4310;
		obj10.GateEnvironmentVariableName = null;
		obj10.LogPrefix = "THRAK_GARDEN_KEY";
		DreamingSilvertailRegistration = obj10;
		CraigOrFuriousFistsRegistration = CreateThrakGardenVendorRegistration("Craig-Or of the Furious Fists", 2037747519, "SimpleChar:79758F3F");
		CraigOrPreservationRegistration = CreateThrakGardenVendorRegistration("Craig-Or of Preservation", 2037747518, "SimpleChar:79758F3E");
		CraigOrFlamingBarrelsRegistration = CreateThrakGardenVendorRegistration("Craig-Or of Flaming Barrels", 2037747515, "SimpleChar:79758F3B");
		CraigOrGearAndAmmoRegistration = CreateThrakGardenVendorRegistration("Craig-Or of Gear & Ammo", 2037747516, "SimpleChar:79758F3C");
		CraigOrProtectionRegistration = CreateThrakGardenVendorRegistration("Craig-Or of Protection", 2037747517, "SimpleChar:79758F3D");
		SonLenRegistration = CreateThrakGardenVendorRegistration("Son-Len, Official of Power", 2037747520, "SimpleChar:79758F40");
		Registrations = new ContentDrivenNpcDialogueRegistration[19]
		{
			RexLarssonRegistration, MarcusStoneRegistration, FlintNovakRegistration, AlexGibbsRegistration, BillRegistration, WindcallerKarrecRegistration, AnnoyingDudeRegistration, MaddyCardileRegistration, SubwayTailorRegistration, VeronicaEscobarRegistration,
			ProphetYuttRegistration, HypnagogicUrgaLumRegistration, DreamingSilvertailRegistration, CraigOrFuriousFistsRegistration, CraigOrPreservationRegistration, CraigOrFlamingBarrelsRegistration, CraigOrGearAndAmmoRegistration, CraigOrProtectionRegistration, SonLenRegistration
		};
		SessionsByCharacter = new Dictionary<string, DialogueSessionRecord>(StringComparer.OrdinalIgnoreCase);
		TailorOpenHistoryByCharacter = new ConditionalWeakTable<ICharacter, object>();
		SyncRoot = new object();
	}
}
