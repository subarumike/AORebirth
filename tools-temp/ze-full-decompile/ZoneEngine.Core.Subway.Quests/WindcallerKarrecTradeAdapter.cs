using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using AORebirth.Core.Components;
using AORebirth.Core.Entities;
using AORebirth.Core.Inventory;
using AORebirth.Core.Items;
using AORebirth.Interfaces;
using SmokeLounge.AOtomation.Messaging.GameData;
using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;
using ZoneEngine.Core.MessageHandlers;
using ZoneEngine.Core.Missions;

namespace ZoneEngine.Core.Subway.Quests;

internal static class WindcallerKarrecTradeAdapter
{
	private sealed class KarrecTradeSession
	{
		public Identity KarrecIdentity { get; set; }

		public IDictionary<int, Identity> ItemLocations { get; private set; }

		public ISet<string> StagedLocations { get; private set; }

		public bool ContainsUnrecognizedItem { get; set; }

		public KarrecTradeSession()
		{
			ItemLocations = new Dictionary<int, Identity>();
			StagedLocations = new HashSet<string>(StringComparer.Ordinal);
		}
	}

	private sealed class StagedOffering
	{
		public int ItemId { get; set; }

		public IInventoryPage Page { get; set; }

		public Identity Location { get; set; }

		public int Slot { get; set; }

		public Item Item { get; set; }
	}

	private const string TradePendingFlag = "trade-consumption-pending";

	private const string TradeConsumedFlag = "trade-items-consumed";

	private static readonly Dictionary<int, KarrecTradeSession> SessionsByCharacter = new Dictionary<int, KarrecTradeSession>();

	private static readonly object SyncRoot = new object();

	internal static void BeginTrade(ICharacter source, Identity karrecIdentity)
	{
		//IL_0005: Unknown result type (might be due to invalid IL or missing references)
		//IL_000a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0015: Unknown result type (might be due to invalid IL or missing references)
		//IL_0016: Unknown result type (might be due to invalid IL or missing references)
		//IL_0042: Unknown result type (might be due to invalid IL or missing references)
		//IL_0047: Unknown result type (might be due to invalid IL or missing references)
		//IL_0055: Unknown result type (might be due to invalid IL or missing references)
		if (source == null)
		{
			return;
		}
		Identity identity = ((IEntity)source).Identity;
		if (((Identity)(ref identity)).Instance <= 0 || karrecIdentity == Identity.None)
		{
			return;
		}
		lock (SyncRoot)
		{
			Dictionary<int, KarrecTradeSession> sessionsByCharacter = SessionsByCharacter;
			identity = ((IEntity)source).Identity;
			sessionsByCharacter[((Identity)(ref identity)).Instance] = new KarrecTradeSession
			{
				KarrecIdentity = karrecIdentity
			};
		}
	}

	internal static bool TryStageTradeItem(ICharacter source, KnuBotTradeMessage message)
	{
		//IL_00c2: Unknown result type (might be due to invalid IL or missing references)
		//IL_001b: Unknown result type (might be due to invalid IL or missing references)
		//IL_003b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0051: Unknown result type (might be due to invalid IL or missing references)
		//IL_0098: Unknown result type (might be due to invalid IL or missing references)
		//IL_009d: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a1: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a7: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ac: Unknown result type (might be due to invalid IL or missing references)
		//IL_00f2: Unknown result type (might be due to invalid IL or missing references)
		//IL_013a: Unknown result type (might be due to invalid IL or missing references)
		if (message == null)
		{
			return false;
		}
		KarrecTradeSession session = GetSession(source);
		if (!IsHandledKarrecTarget(session, message.Target))
		{
			return false;
		}
		if (session == null && WindcallerKarrecInteractionRules.IsKarrec(message.Target))
		{
			BeginTrade(source, message.Target);
			session = GetSession(source);
		}
		if (!WindcallerKarrecQuestRuntime.IsActive(source))
		{
			return true;
		}
		if (session == null)
		{
			return true;
		}
		IItem knuBotTradeItem;
		try
		{
			InventoryContainerRuntimeService @default = InventoryContainerRuntimeService.Default;
			Identity container = message.Container;
			IdentityType type = ((Identity)(ref container)).Type;
			container = message.Container;
			knuBotTradeItem = @default.GetKnuBotTradeItem(source, type, ((Identity)(ref container)).Instance);
		}
		catch (Exception)
		{
			MarkUnrecognizedOffering(session, message.Container);
			return true;
		}
		int num = ResolveOfferingItemId(knuBotTradeItem);
		lock (SyncRoot)
		{
			string item = MakeLocationKey(message.Container);
			if (!session.StagedLocations.Add(item))
			{
				return true;
			}
			if (num == 0)
			{
				session.ContainsUnrecognizedItem = true;
				return true;
			}
			session.ItemLocations[num] = message.Container;
		}
		return true;
	}

	internal static bool TryFinishTrade(ICharacter source, KnuBotFinishTradeMessage message)
	{
		//IL_002d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0049: Unknown result type (might be due to invalid IL or missing references)
		//IL_0097: Unknown result type (might be due to invalid IL or missing references)
		//IL_009c: Unknown result type (might be due to invalid IL or missing references)
		//IL_00cb: Unknown result type (might be due to invalid IL or missing references)
		//IL_0204: Unknown result type (might be due to invalid IL or missing references)
		//IL_0209: Unknown result type (might be due to invalid IL or missing references)
		//IL_0148: Unknown result type (might be due to invalid IL or missing references)
		//IL_018f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0194: Unknown result type (might be due to invalid IL or missing references)
		//IL_02a3: Unknown result type (might be due to invalid IL or missing references)
		//IL_02a8: Unknown result type (might be due to invalid IL or missing references)
		//IL_0328: Unknown result type (might be due to invalid IL or missing references)
		//IL_032d: Unknown result type (might be due to invalid IL or missing references)
		//IL_03ab: Unknown result type (might be due to invalid IL or missing references)
		//IL_03d7: Unknown result type (might be due to invalid IL or missing references)
		//IL_03dc: Unknown result type (might be due to invalid IL or missing references)
		//IL_0389: Unknown result type (might be due to invalid IL or missing references)
		//IL_038e: Unknown result type (might be due to invalid IL or missing references)
		if (message == null || source == null)
		{
			return false;
		}
		bool flag = WindcallerKarrecInteractionRules.IsKarrec(message.Target);
		KarrecTradeSession session = GetSession(source);
		if (!flag && !IsHandledKarrecTarget(session, message.Target))
		{
			return false;
		}
		Identity identity;
		if (message.Decline != 0)
		{
			ForgetSession(source);
			object[] array = new object[1];
			identity = ((IEntity)source).Identity;
			array[0] = ((Identity)(ref identity)).Instance;
			MissionDiagnostics.Log("karrec-trade decline character={0}", array);
			return true;
		}
		if (session == null)
		{
			BeginTrade(source, message.Target);
			session = GetSession(source);
		}
		if (WindcallerKarrecQuestRuntime.IsCompleted(source) && WindcallerKarrecQuestRuntime.HasAccountAccess(source))
		{
			IList<StagedOffering> offerings = null;
			if (TryResolveOfferings(source, session, out offerings) || TryFindOfferingsInInventory(source, out offerings))
			{
				TryConsumeAndNotifyOfferings(source, offerings);
			}
			SendImmediateTradeAcceptanceUi(source, message.Target);
			EnsureProjection(source, "completion-delete-projected", () => WindcallerKarrecPacketSender.TrySendCompletionAndDelete(source));
			ForgetSession(source);
			object[] array2 = new object[2];
			identity = ((IEntity)source).Identity;
			array2[0] = ((Identity)(ref identity)).Instance;
			array2[1] = offerings?.Count ?? 0;
			MissionDiagnostics.Log("karrec-trade replay-ui character={0} (already completed, leftovers-consumed={1})", array2);
			return true;
		}
		if (!TryResolveOfferings(source, session, out var offerings2) && !TryFindOfferingsInInventory(source, out offerings2))
		{
			object[] array3 = new object[3];
			identity = ((IEntity)source).Identity;
			array3[0] = ((Identity)(ref identity)).Instance;
			array3[1] = WindcallerKarrecQuestRuntime.IsActive(source);
			array3[2] = session?.StagedLocations.Count ?? 0;
			MissionDiagnostics.Log("karrec-trade finish rejected character={0} reason=missing-offerings active={1} staged={2}", array3);
			ForgetSession(source);
			return true;
		}
		if (!WindcallerKarrecQuestRuntime.IsActive(source) && !WindcallerKarrecQuestRuntime.IsCompleted(source))
		{
			MissionOperationResult missionOperationResult = WindcallerKarrecQuestRuntime.Accept(source);
			object[] array4 = new object[3];
			identity = ((IEntity)source).Identity;
			array4[0] = ((Identity)(ref identity)).Instance;
			array4[1] = ((missionOperationResult == null) ? "null" : missionOperationResult.Status.ToString());
			array4[2] = ((missionOperationResult == null) ? string.Empty : missionOperationResult.Message);
			MissionDiagnostics.Log("karrec-trade re-accept character={0} status={1} message={2}", array4);
		}
		KarrecCompletionResult karrecCompletionResult = WindcallerKarrecQuestRuntime.CompleteAfterOfferingsConsumed(source);
		if (!karrecCompletionResult.Completed)
		{
			object[] array5 = new object[2];
			identity = ((IEntity)source).Identity;
			array5[0] = ((Identity)(ref identity)).Instance;
			array5[1] = karrecCompletionResult.Error;
			MissionDiagnostics.Log("karrec-trade completion-failed character={0} error={1}", array5);
			ForgetSession(source);
			return true;
		}
		if (!TryConsumeAndNotifyOfferings(source, offerings2))
		{
			object[] array6 = new object[1];
			identity = ((IEntity)source).Identity;
			array6[0] = ((Identity)(ref identity)).Instance;
			MissionDiagnostics.Log("karrec-trade consume-failed character={0} (passage already granted)", array6);
		}
		SendCompletionProjection(source, message.Target, karrecCompletionResult);
		ForgetSession(source);
		object[] array7 = new object[1];
		identity = ((IEntity)source).Identity;
		array7[0] = ((Identity)(ref identity)).Instance;
		MissionDiagnostics.Log("karrec-trade completed character={0} totw-access=granted", array7);
		return true;
	}

	private static bool TryConsumeAndNotifyOfferings(ICharacter source, IList<StagedOffering> offerings)
	{
		//IL_001a: Unknown result type (might be due to invalid IL or missing references)
		//IL_001f: Unknown result type (might be due to invalid IL or missing references)
		if (!TryConsumeOfferings(source, offerings))
		{
			return false;
		}
		PersistentMissionService service = MissionRuntime.Service;
		Identity identity = ((IEntity)source).Identity;
		service.SetFlag(((Identity)(ref identity)).Instance, "Mission:55579381", "trade-items-consumed", "297042,297043");
		NotifyOfferingsRemoved(source, offerings);
		return true;
	}

	private static void NotifyOfferingsRemoved(ICharacter source, IList<StagedOffering> offerings)
	{
		//IL_002c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0031: Unknown result type (might be due to invalid IL or missing references)
		//IL_0034: Unknown result type (might be due to invalid IL or missing references)
		//IL_0044: Expected I4, but got Unknown
		if (source == null || offerings == null)
		{
			return;
		}
		foreach (StagedOffering offering in offerings)
		{
			try
			{
				CharacterActionMessageHandler @default = BaseMessageHandler<CharacterActionMessage, CharacterActionMessageHandler>.Default;
				Identity location = offering.Location;
				@default.SendDeleteItem(source, (int)((Identity)(ref location)).Type, offering.Slot);
			}
			catch (Exception)
			{
			}
		}
	}

	private static bool TryFindOfferingsInInventory(ICharacter source, out IList<StagedOffering> offerings)
	{
		offerings = new List<StagedOffering>();
		if (source == null || ((IItemContainer)source).BaseInventory == null)
		{
			return false;
		}
		if (!TryFindSingleOfferingInCarriedInventory(source, 297042, out var offering) || !TryFindSingleOfferingInCarriedInventory(source, 297043, out var offering2))
		{
			return false;
		}
		offerings.Add(offering);
		offerings.Add(offering2);
		return true;
	}

	private static bool TryFindSingleOfferingInCarriedInventory(ICharacter source, int itemId, out StagedOffering offering)
	{
		//IL_00a4: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c2: Unknown result type (might be due to invalid IL or missing references)
		offering = null;
		int[] array = new int[2] { 104, 110 };
		foreach (int num in array)
		{
			if (!((IItemContainer)source).BaseInventory.Pages.TryGetValue(num, out var value) || value == null)
			{
				continue;
			}
			foreach (KeyValuePair<int, IItem> item2 in value.List())
			{
				IItem value2 = item2.Value;
				Item item = (Item)(object)((value2 is Item) ? value2 : null);
				if (ResolveOfferingItemId((IItem)(object)item) != itemId)
				{
					continue;
				}
				StagedOffering obj = new StagedOffering
				{
					ItemId = itemId,
					Page = value
				};
				Identity location = default(Identity);
				((Identity)(ref location)).Type = (IdentityType)num;
				((Identity)(ref location)).Instance = item2.Key;
				obj.Location = location;
				obj.Slot = item2.Key;
				obj.Item = item;
				offering = obj;
				return true;
			}
		}
		return false;
	}

	internal static bool TryResumeDurableCompletion(ICharacter source, Identity karrecIdentity)
	{
		//IL_005a: Unknown result type (might be due to invalid IL or missing references)
		//IL_005f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0030: Unknown result type (might be due to invalid IL or missing references)
		//IL_011b: Unknown result type (might be due to invalid IL or missing references)
		if (WindcallerKarrecQuestRuntime.IsCompleted(source))
		{
			KarrecCompletionResult karrecCompletionResult = WindcallerKarrecQuestRuntime.CompleteAfterOfferingsConsumed(source);
			if (!karrecCompletionResult.Completed)
			{
				return false;
			}
			SendCompletionProjection(source, karrecIdentity, karrecCompletionResult);
			return true;
		}
		if (!WindcallerKarrecQuestRuntime.IsActive(source))
		{
			return false;
		}
		Identity identity = ((IEntity)source).Identity;
		int instance = ((Identity)(ref identity)).Instance;
		MissionFlagRecord flag = MissionRuntime.Service.GetFlag(instance, "Mission:55579381", "trade-consumption-pending");
		if (flag == null)
		{
			return false;
		}
		MissionFlagRecord flag2 = MissionRuntime.Service.GetFlag(instance, "Mission:55579381", "trade-items-consumed");
		if (flag2 == null)
		{
			if (!ArePersistedOfferingSlotsConsumed(source, flag.Value))
			{
				return false;
			}
			MissionOperationResult result = MissionRuntime.Service.SetFlag(instance, "Mission:55579381", "trade-items-consumed", "recovered-after-persisted-trade-pending");
			if (IsPersistenceFailure(result))
			{
				return false;
			}
		}
		KarrecCompletionResult karrecCompletionResult2 = WindcallerKarrecQuestRuntime.CompleteAfterOfferingsConsumed(source);
		if (!karrecCompletionResult2.Completed)
		{
			return false;
		}
		SendCompletionProjection(source, karrecIdentity, karrecCompletionResult2);
		ForgetSession(source);
		return true;
	}

	private static void SendCompletionProjection(ICharacter source, Identity karrecIdentity, KarrecCompletionResult completion)
	{
		//IL_001b: Unknown result type (might be due to invalid IL or missing references)
		SendImmediateTradeAcceptanceUi(source, karrecIdentity);
		EnsureProjection(source, "personal-research-feedback-projected", () => WindcallerKarrecPacketSender.TrySendPersonalResearchFeedback(source));
		EnsureProjection(source, "side-token-projected", () => WindcallerKarrecPacketSender.TrySendSideTokenProjection(source, completion.SideTokenValue));
		EnsureProjection(source, "completion-delete-projected", () => WindcallerKarrecPacketSender.TrySendCompletionAndDelete(source));
	}

	private static void SendImmediateTradeAcceptanceUi(ICharacter source, Identity karrecIdentity)
	{
		//IL_0008: Unknown result type (might be due to invalid IL or missing references)
		//IL_0023: Unknown result type (might be due to invalid IL or missing references)
		//IL_003e: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b0: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b5: Unknown result type (might be due to invalid IL or missing references)
		//IL_007d: Unknown result type (might be due to invalid IL or missing references)
		try
		{
			BaseMessageHandler<KnuBotRejectedItemsMessage, KnuBotRejectedItemsMessageHandler>.Default.Send(source, karrecIdentity, (IEnumerable<Item>)(object)new Item[0]);
			Thread.Sleep(25);
			BaseMessageHandler<KnuBotAppendTextMessage, KnuBotAppendTextMessageHandler>.Default.Send(source, karrecIdentity, "Karrec hands you a note covered with strange words and symbols, none of which make any sense to you. You upload the information to your ncu and throw the paper away.", 1);
			Thread.Sleep(25);
			BaseMessageHandler<KnuBotAppendTextMessage, KnuBotAppendTextMessageHandler>.Default.Send(source, karrecIdentity, "Your devotion to the Cult of Three Winds gains you passage to the sacred Temple " + (string.IsNullOrWhiteSpace(((INamedEntity)source).Name) ? string.Empty : ((INamedEntity)source).Name) + ". You may now use the gateway.", 0);
			Thread.Sleep(25);
			BaseMessageHandler<KnuBotAnswerListMessage, KnuBotAnswerListMessageHandler>.Default.Send(source, karrecIdentity, new string[2] { "Thank you, Karrec.", "Goodbye" });
		}
		catch (Exception ex)
		{
			object[] array = new object[2];
			int num;
			if (source != null)
			{
				Identity identity = ((IEntity)source).Identity;
				num = ((Identity)(ref identity)).Instance;
			}
			else
			{
				num = 0;
			}
			array[0] = num;
			array[1] = ex.Message;
			MissionDiagnostics.Log("karrec-trade ui-send-failed character={0} error={1}", array);
		}
	}

	private static bool EnsureProjection(ICharacter source, string flagKey, Func<bool> sender)
	{
		//IL_0002: Unknown result type (might be due to invalid IL or missing references)
		//IL_0007: Unknown result type (might be due to invalid IL or missing references)
		Identity identity = ((IEntity)source).Identity;
		int instance = ((Identity)(ref identity)).Instance;
		if (MissionRuntime.Service.GetFlag(instance, "Mission:55579381", flagKey) != null)
		{
			return true;
		}
		bool flag;
		try
		{
			flag = sender();
		}
		catch (Exception)
		{
			return false;
		}
		if (!flag)
		{
			return false;
		}
		MissionOperationResult result = MissionRuntime.Service.SetFlag(instance, "Mission:55579381", flagKey, "true");
		return !IsPersistenceFailure(result);
	}

	private static bool TryResolveOfferings(ICharacter source, KarrecTradeSession session, out IList<StagedOffering> offerings)
	{
		//IL_008a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0096: Expected I4, but got Unknown
		//IL_00ef: Unknown result type (might be due to invalid IL or missing references)
		offerings = new List<StagedOffering>();
		if (!WindcallerKarrecInteractionRules.HasExactOfferings(session.ItemLocations.Keys, session.StagedLocations.Count, session.ContainsUnrecognizedItem))
		{
			return false;
		}
		int[] array = new int[2] { 297042, 297043 };
		foreach (int num in array)
		{
			if (!session.ItemLocations.TryGetValue(num, out var value))
			{
				return false;
			}
			if (!((IItemContainer)source).BaseInventory.Pages.TryGetValue((int)((Identity)(ref value)).Type, out var value2))
			{
				return false;
			}
			IItem obj = value2[((Identity)(ref value)).Instance];
			Item item = (Item)(object)((obj is Item) ? obj : null);
			if (ResolveOfferingItemId((IItem)(object)item) != num)
			{
				return false;
			}
			offerings.Add(new StagedOffering
			{
				ItemId = num,
				Page = value2,
				Location = value,
				Slot = ((Identity)(ref value)).Instance,
				Item = item
			});
		}
		return offerings.Count == 2;
	}

	private static string SerializePendingOfferings(IEnumerable<StagedOffering> offerings)
	{
		return string.Join(";", offerings.Select(delegate(StagedOffering value)
		{
			//IL_0027: Unknown result type (might be due to invalid IL or missing references)
			//IL_002c: Unknown result type (might be due to invalid IL or missing references)
			//IL_002f: Unknown result type (might be due to invalid IL or missing references)
			//IL_0035: Expected I4, but got Unknown
			//IL_004d: Unknown result type (might be due to invalid IL or missing references)
			//IL_0052: Unknown result type (might be due to invalid IL or missing references)
			string[] obj = new string[5]
			{
				value.ItemId.ToString(CultureInfo.InvariantCulture),
				":",
				null,
				null,
				null
			};
			Identity location = value.Location;
			obj[2] = ((int)((Identity)(ref location)).Type).ToString(CultureInfo.InvariantCulture);
			obj[3] = ":";
			location = value.Location;
			obj[4] = ((Identity)(ref location)).Instance.ToString(CultureInfo.InvariantCulture);
			return string.Concat(obj);
		}).ToArray());
	}

	private static bool ArePersistedOfferingSlotsConsumed(ICharacter source, string pendingValue)
	{
		if (source == null || string.IsNullOrWhiteSpace(pendingValue))
		{
			return false;
		}
		string[] array = pendingValue.Split(new char[1] { ';' }, StringSplitOptions.RemoveEmptyEntries);
		HashSet<int> hashSet = new HashSet<int>();
		string[] array2 = array;
		foreach (string text in array2)
		{
			string[] array3 = text.Split(':');
			if (array3.Length != 3 || !int.TryParse(array3[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out var result) || !int.TryParse(array3[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var result2) || !int.TryParse(array3[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out var result3) || (result != 297042 && result != 297043) || !hashSet.Add(result))
			{
				return false;
			}
			if (((IItemContainer)source).BaseInventory.Pages.TryGetValue(result2, out var value) && ResolveOfferingItemId((IItem)/*isinst with value type is only supported in some contexts*/) == result)
			{
				return false;
			}
		}
		return hashSet.SetEquals(new int[2] { 297042, 297043 });
	}

	private static bool TryConsumeOfferings(ICharacter source, IList<StagedOffering> offerings)
	{
		//IL_0083: Unknown result type (might be due to invalid IL or missing references)
		foreach (StagedOffering offering in offerings)
		{
			offering.Page.Remove(offering.Slot);
		}
		try
		{
			if (((IItemContainer)source).BaseInventory.Write())
			{
				return true;
			}
		}
		catch (Exception)
		{
		}
		foreach (StagedOffering offering2 in offerings)
		{
			offering2.Page.Add(offering2.Slot, (IItem)(object)offering2.Item);
		}
		return false;
	}

	private static int ResolveOfferingItemId(IItem item)
	{
		if (item == null)
		{
			return 0;
		}
		if (item.LowID == 297042 || item.HighID == 297042)
		{
			return 297042;
		}
		if (item.LowID == 297043 || item.HighID == 297043)
		{
			return 297043;
		}
		return 0;
	}

	private static bool IsHandledKarrecTarget(KarrecTradeSession session, Identity identity)
	{
		//IL_0001: Unknown result type (might be due to invalid IL or missing references)
		//IL_0014: Unknown result type (might be due to invalid IL or missing references)
		//IL_0019: Unknown result type (might be due to invalid IL or missing references)
		//IL_001c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0023: Unknown result type (might be due to invalid IL or missing references)
		//IL_002b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0030: Unknown result type (might be due to invalid IL or missing references)
		if (WindcallerKarrecInteractionRules.IsKarrec(identity))
		{
			return true;
		}
		int result;
		if (session != null)
		{
			Identity karrecIdentity = session.KarrecIdentity;
			if (((Identity)(ref karrecIdentity)).Type == ((Identity)(ref identity)).Type)
			{
				karrecIdentity = session.KarrecIdentity;
				result = ((((Identity)(ref karrecIdentity)).Instance == ((Identity)(ref identity)).Instance) ? 1 : 0);
				goto IL_0044;
			}
		}
		result = 0;
		goto IL_0044;
		IL_0044:
		return (byte)result != 0;
	}

	private static void MarkUnrecognizedOffering(KarrecTradeSession session, Identity location)
	{
		//IL_0024: Unknown result type (might be due to invalid IL or missing references)
		if (session == null)
		{
			return;
		}
		lock (SyncRoot)
		{
			session.StagedLocations.Add(MakeLocationKey(location));
			session.ContainsUnrecognizedItem = true;
		}
	}

	private static string MakeLocationKey(Identity location)
	{
		//IL_0003: Unknown result type (might be due to invalid IL or missing references)
		//IL_0009: Expected I4, but got Unknown
		return ((int)((Identity)(ref location)).Type).ToString(CultureInfo.InvariantCulture) + ":" + ((Identity)(ref location)).Instance.ToString(CultureInfo.InvariantCulture);
	}

	private static bool IsPersistenceFailure(MissionOperationResult result)
	{
		return result == null || result.Status == MissionOperationStatus.Rejected || result.Status == MissionOperationStatus.NotFound || result.Status == MissionOperationStatus.Unresolved;
	}

	private static KarrecTradeSession GetSession(ICharacter source)
	{
		//IL_0026: Unknown result type (might be due to invalid IL or missing references)
		//IL_002b: Unknown result type (might be due to invalid IL or missing references)
		if (source == null)
		{
			return null;
		}
		lock (SyncRoot)
		{
			Dictionary<int, KarrecTradeSession> sessionsByCharacter = SessionsByCharacter;
			Identity identity = ((IEntity)source).Identity;
			KarrecTradeSession value;
			return sessionsByCharacter.TryGetValue(((Identity)(ref identity)).Instance, out value) ? value : null;
		}
	}

	private static void ForgetSession(ICharacter source)
	{
		//IL_0024: Unknown result type (might be due to invalid IL or missing references)
		//IL_0029: Unknown result type (might be due to invalid IL or missing references)
		if (source == null)
		{
			return;
		}
		lock (SyncRoot)
		{
			Dictionary<int, KarrecTradeSession> sessionsByCharacter = SessionsByCharacter;
			Identity identity = ((IEntity)source).Identity;
			sessionsByCharacter.Remove(((Identity)(ref identity)).Instance);
		}
	}
}
