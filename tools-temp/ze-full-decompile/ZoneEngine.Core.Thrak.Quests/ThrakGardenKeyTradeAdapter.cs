using System;
using System.Collections.Generic;
using AORebirth.Core.Components;
using AORebirth.Core.Entities;
using AORebirth.Core.Inventory;
using AORebirth.Core.Items;
using AORebirth.Interfaces;
using AORebirth.ObjectManager;
using SmokeLounge.AOtomation.Messaging.GameData;
using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;
using Utility;
using ZoneEngine.Core.Arete.Dialogue;
using ZoneEngine.Core.MessageHandlers;

namespace ZoneEngine.Core.Thrak.Quests;

internal static class ThrakGardenKeyTradeAdapter
{
	private enum TradeKind
	{
		None,
		ProphetDevice,
		ProphetInsignia,
		HypAnalyzer,
		HypReturn,
		Silvertail
	}

	private sealed class ThrakTradeSession
	{
		public Identity NpcIdentity;

		public TradeKind Kind;

		public int StagedItemId;

		public int StagedQuality;

		public Identity StagedContainer;
	}

	private static readonly Dictionary<int, ThrakTradeSession> SessionsByCharacter = new Dictionary<int, ThrakTradeSession>();

	private static readonly object SyncRoot = new object();

	internal static void BeginTrade(ICharacter source, Identity npcIdentity, string kind)
	{
		//IL_0005: Unknown result type (might be due to invalid IL or missing references)
		//IL_000a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0015: Unknown result type (might be due to invalid IL or missing references)
		//IL_0016: Unknown result type (might be due to invalid IL or missing references)
		//IL_0057: Unknown result type (might be due to invalid IL or missing references)
		//IL_005c: Unknown result type (might be due to invalid IL or missing references)
		//IL_006a: Unknown result type (might be due to invalid IL or missing references)
		//IL_006b: Unknown result type (might be due to invalid IL or missing references)
		if (source == null)
		{
			return;
		}
		Identity identity = ((IEntity)source).Identity;
		if (((Identity)(ref identity)).Instance <= 0 || npcIdentity == Identity.None)
		{
			return;
		}
		TradeKind tradeKind = ParseKind(kind);
		if (tradeKind == TradeKind.None)
		{
			return;
		}
		lock (SyncRoot)
		{
			Dictionary<int, ThrakTradeSession> sessionsByCharacter = SessionsByCharacter;
			identity = ((IEntity)source).Identity;
			sessionsByCharacter[((Identity)(ref identity)).Instance] = new ThrakTradeSession
			{
				NpcIdentity = npcIdentity,
				Kind = tradeKind
			};
		}
	}

	internal static bool IsThrakTradeNpc(ICharacter source, Identity npcIdentity)
	{
		//IL_0002: Unknown result type (might be due to invalid IL or missing references)
		//IL_000a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0012: Unknown result type (might be due to invalid IL or missing references)
		//IL_001a: Unknown result type (might be due to invalid IL or missing references)
		return ResolveNpcName(source, npcIdentity) != null || ThrakGardenKeyInteractionRules.IsProphet(npcIdentity) || ThrakGardenKeyInteractionRules.IsHypnagogic(npcIdentity) || ThrakGardenKeyInteractionRules.IsDreamingSilvertail(npcIdentity);
	}

	internal static bool TryStageTradeItem(ICharacter source, KnuBotTradeMessage message)
	{
		//IL_002c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0088: Unknown result type (might be due to invalid IL or missing references)
		//IL_008e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0076: Unknown result type (might be due to invalid IL or missing references)
		//IL_004a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0051: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a4: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a9: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b8: Unknown result type (might be due to invalid IL or missing references)
		//IL_00bd: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c1: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c7: Unknown result type (might be due to invalid IL or missing references)
		//IL_00cc: Unknown result type (might be due to invalid IL or missing references)
		//IL_010a: Unknown result type (might be due to invalid IL or missing references)
		//IL_010f: Unknown result type (might be due to invalid IL or missing references)
		if (message == null || source == null)
		{
			return false;
		}
		ThrakTradeSession session = GetSession(source);
		if (session == null)
		{
			if (!IsThrakTradeNpc(source, message.Target))
			{
				return false;
			}
			BeginTrade(source, message.Target, InferKindFromTarget(source, message.Target));
			session = GetSession(source);
		}
		if (session == null)
		{
			return IsThrakTradeNpc(source, message.Target);
		}
		if (!IdentitiesEqual(session.NpcIdentity, message.Target))
		{
			session.NpcIdentity = message.Target;
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
			return true;
		}
		int num = ResolveItemId(knuBotTradeItem);
		lock (SyncRoot)
		{
			session.StagedItemId = num;
			session.StagedContainer = message.Container;
			session.StagedQuality = ((knuBotTradeItem == null || knuBotTradeItem.Quality <= 0) ? 1 : knuBotTradeItem.Quality);
			RefineKindFromItem(session, num);
		}
		return true;
	}

	internal static bool TryFinishTrade(ICharacter source, KnuBotFinishTradeMessage message)
	{
		//IL_001c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0076: Unknown result type (might be due to invalid IL or missing references)
		//IL_003e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0044: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a4: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ab: Unknown result type (might be due to invalid IL or missing references)
		//IL_00e5: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ea: Unknown result type (might be due to invalid IL or missing references)
		//IL_00d0: Unknown result type (might be due to invalid IL or missing references)
		//IL_0157: Unknown result type (might be due to invalid IL or missing references)
		//IL_01c9: Unknown result type (might be due to invalid IL or missing references)
		//IL_01ce: Unknown result type (might be due to invalid IL or missing references)
		//IL_01fe: Unknown result type (might be due to invalid IL or missing references)
		if (message == null || source == null)
		{
			return false;
		}
		if (!IsThrakTradeNpc(source, message.Target))
		{
			ThrakTradeSession session = GetSession(source);
			if (session == null || !IdentitiesEqual(session.NpcIdentity, message.Target))
			{
				return false;
			}
		}
		if (message.Decline != 0)
		{
			SendRejectedItems(source, message.Target);
			ClearSession(source);
			return true;
		}
		ThrakTradeSession session2 = GetSession(source);
		if (session2 == null)
		{
			BeginTrade(source, message.Target, InferKindFromTarget(source, message.Target));
			session2 = GetSession(source);
		}
		if (session2 == null)
		{
			SendRejectedItems(source, message.Target);
			return true;
		}
		session2.NpcIdentity = message.Target;
		int num = session2.StagedItemId;
		if (num == 0)
		{
			num = FindExpectedItemInInventory(source, session2.Kind);
		}
		RefineKindFromItem(session2, num);
		bool flag = ApplyTrade(source, session2, num);
		int soulsBeforeSilvertail = ((session2.Kind == TradeKind.Silvertail) ? ThrakGardenKeyQuestRuntime.GetSoulCount(source) : 0);
		if (flag && IsInspectionKeepItemTrade(session2.Kind))
		{
			EnsureKeptItemPresent(source, session2, num);
		}
		SendRejectedItems(source, session2.NpcIdentity, session2.Kind, soulsBeforeSilvertail, num, session2.StagedQuality);
		if (flag)
		{
			if (session2.Kind == TradeKind.HypReturn)
			{
				ThrakGardenKeyQuestRuntime.TryGrantFavoredAnalyzer(source);
			}
			if (session2.Kind == TradeKind.Silvertail)
			{
				ThrakGardenKeyQuestRuntime.TryForceReturnFavoredAnalyzer(source);
				int num2 = ThrakGardenKeyQuestRuntime.IncrementSoulCount(source);
				string text = num2.ToString();
				Identity identity = ((IEntity)source).Identity;
				LogUtil.Debug((DebugInfoDetail)128, "ThrakGardenKey Silvertail trade souls=" + text + " by=" + ((Identity)(ref identity)).ToString(true));
			}
			if (session2.Kind == TradeKind.ProphetDevice)
			{
				ThrakGardenKeyQuestRuntime.TryRestoreAncientDeviceIfMissing(source);
			}
			ContentDrivenNpcDialogueRouter.TryResumeAfterNpcTrade(source, session2.NpcIdentity);
			if (session2.Kind == TradeKind.HypAnalyzer)
			{
				ThrakGardenKeyQuestRuntime.TryGrantInsignia(source);
				ThrakGardenKeyQuestRuntime.TryForceReturnAncientDevice(source);
			}
		}
		ClearSession(source);
		return true;
	}

	private static bool IsInspectionKeepItemTrade(TradeKind kind)
	{
		return kind == TradeKind.ProphetDevice || kind == TradeKind.ProphetInsignia || kind == TradeKind.HypAnalyzer || kind == TradeKind.Silvertail;
	}

	private static void EnsureKeptItemPresent(ICharacter source, ThrakTradeSession session, int itemId)
	{
		if (source == null || itemId <= 0)
		{
			return;
		}
		if (itemId == 214998 || itemId == 214783 || itemId == 214785)
		{
			if (!ThrakGardenKeyQuestRuntime.HasAnalyzer(source))
			{
				ThrakGardenKeyQuestRuntime.TryRestoreItem(source, itemId, (session == null || session.StagedQuality <= 0) ? 1 : session.StagedQuality);
			}
		}
		else if (itemId == 214789 && !ThrakGardenKeyQuestRuntime.HasInsignia(source))
		{
			ThrakGardenKeyQuestRuntime.TryRestoreItem(source, itemId, (session == null || session.StagedQuality <= 0) ? 1 : session.StagedQuality);
		}
	}

	private static void RefineKindFromItem(ThrakTradeSession session, int itemId)
	{
		if (session == null || itemId <= 0 || (session.Kind != TradeKind.ProphetDevice && session.Kind != TradeKind.ProphetInsignia))
		{
			return;
		}
		switch (itemId)
		{
		case 214789:
			session.Kind = TradeKind.ProphetInsignia;
			break;
		default:
			if (itemId != 214785)
			{
				break;
			}
			goto case 214783;
		case 214783:
		case 214998:
			session.Kind = TradeKind.ProphetDevice;
			break;
		}
	}

	private static bool ApplyTrade(ICharacter source, ThrakTradeSession session, int itemId)
	{
		//IL_0112: Unknown result type (might be due to invalid IL or missing references)
		//IL_0166: Unknown result type (might be due to invalid IL or missing references)
		switch (session.Kind)
		{
		case TradeKind.ProphetDevice:
			if (itemId != 214998 && itemId != 214783 && itemId != 214785)
			{
				return false;
			}
			ThrakGardenKeyQuestRuntime.MarkProphetDeviceInspected(source);
			return true;
		case TradeKind.ProphetInsignia:
			if (itemId != 214789)
			{
				return false;
			}
			ThrakGardenKeyQuestRuntime.CompleteQuest(source, "Mission:55563C16");
			ThrakGardenKeyQuestRuntime.AcceptQuest(source, "Mission:55563C18");
			return true;
		case TradeKind.HypAnalyzer:
			if (itemId != 214998 && itemId != 214783 && itemId != 214785)
			{
				return false;
			}
			ThrakGardenKeyQuestRuntime.CompleteQuest(source, "Mission:55563C18");
			ThrakGardenKeyQuestRuntime.AcceptQuest(source, "Mission:5556591A");
			return true;
		case TradeKind.HypReturn:
			if (itemId != 214785 && itemId != 214783 && itemId != 214998)
			{
				return false;
			}
			TryConsumeItem(source, itemId, session.StagedContainer);
			ThrakGardenKeyQuestRuntime.TryGrantGardenKey(source);
			ThrakGardenKeyQuestRuntime.CompleteQuest(source, "Mission:5556893D");
			ThrakGardenKeyQuestRuntime.ClearFinishedThrakChainJournal(source);
			return true;
		case TradeKind.Silvertail:
			if (itemId != 214785 && itemId != 214783 && itemId != 214998)
			{
				return false;
			}
			return ThrakGardenKeySilvertailTransform.TryCurseAndAggro(source, session.NpcIdentity);
		default:
			return false;
		}
	}

	private static bool TryConsumeItem(ICharacter source, int itemId, Identity stagedContainer)
	{
		//IL_00dd: Unknown result type (might be due to invalid IL or missing references)
		//IL_01e5: Unknown result type (might be due to invalid IL or missing references)
		//IL_0022: Unknown result type (might be due to invalid IL or missing references)
		//IL_004b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0057: Expected I4, but got Unknown
		//IL_00bd: Unknown result type (might be due to invalid IL or missing references)
		//IL_01a2: Unknown result type (might be due to invalid IL or missing references)
		//IL_01c6: Unknown result type (might be due to invalid IL or missing references)
		if (source == null || itemId <= 0 || ((IItemContainer)source).BaseInventory == null)
		{
			return false;
		}
		if ((int)((Identity)(ref stagedContainer)).Type != 0 && ((Identity)(ref stagedContainer)).Instance > 0 && ((IItemContainer)source).BaseInventory.Pages.TryGetValue((int)((Identity)(ref stagedContainer)).Type, out var value) && value != null)
		{
			IItem val = value[((Identity)(ref stagedContainer)).Instance];
			if (val != null && (val.LowID == itemId || val.HighID == itemId))
			{
				value.Remove(((Identity)(ref stagedContainer)).Instance);
				try
				{
					if (((IItemContainer)source).BaseInventory.Write())
					{
						NotifyItemRemoved(source, stagedContainer);
						return true;
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
				if (value3 == null || (value3.LowID != itemId && value3.HighID != itemId))
				{
					continue;
				}
				value2.Remove(item.Key);
				try
				{
					if (((IItemContainer)source).BaseInventory.Write())
					{
						Identity location = default(Identity);
						((Identity)(ref location)).Type = (IdentityType)page.Key;
						((Identity)(ref location)).Instance = item.Key;
						NotifyItemRemoved(source, location);
						return true;
					}
				}
				catch (Exception)
				{
				}
				value2.Add(item.Key, value3);
				return false;
			}
		}
		return false;
	}

	private static void NotifyItemRemoved(ICharacter source, Identity location)
	{
		//IL_000a: Unknown result type (might be due to invalid IL or missing references)
		//IL_001b: Expected I4, but got Unknown
		try
		{
			BaseMessageHandler<CharacterActionMessage, CharacterActionMessageHandler>.Default.SendDeleteItem(source, (int)((Identity)(ref location)).Type, ((Identity)(ref location)).Instance);
		}
		catch (Exception)
		{
		}
	}

	private static string ResolveNpcName(ICharacter source, Identity identity)
	{
		//IL_0024: Unknown result type (might be due to invalid IL or missing references)
		//IL_0029: Unknown result type (might be due to invalid IL or missing references)
		//IL_005e: Unknown result type (might be due to invalid IL or missing references)
		//IL_010a: Unknown result type (might be due to invalid IL or missing references)
		//IL_010f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0081: Unknown result type (might be due to invalid IL or missing references)
		//IL_0086: Unknown result type (might be due to invalid IL or missing references)
		try
		{
			Identity identity2;
			if (source != null && ((IInstancedEntity)source).Playfield != null)
			{
				ICharacter @object = Pool.Instance.GetObject<ICharacter>(((IEntity)((IInstancedEntity)source).Playfield).Identity, identity);
				if (@object != null && ThrakGardenKeyInteractionRules.IsThrakQuestNpcName(((INamedEntity)@object).Name))
				{
					return ((INamedEntity)@object).Name;
				}
				foreach (ICharacter item in Pool.Instance.GetAll<ICharacter>(((IEntity)((IInstancedEntity)source).Playfield).Identity))
				{
					if (item != null)
					{
						identity2 = ((IEntity)item).Identity;
						if (((Identity)(ref identity2)).Instance == ((Identity)(ref identity)).Instance && ThrakGardenKeyInteractionRules.IsThrakQuestNpcName(((INamedEntity)item).Name))
						{
							return ((INamedEntity)item).Name;
						}
					}
				}
			}
			foreach (ICharacter item2 in Pool.Instance.GetAll<ICharacter>(50000))
			{
				if (item2 != null)
				{
					identity2 = ((IEntity)item2).Identity;
					if (((Identity)(ref identity2)).Instance == ((Identity)(ref identity)).Instance && ThrakGardenKeyInteractionRules.IsThrakQuestNpcName(((INamedEntity)item2).Name))
					{
						return ((INamedEntity)item2).Name;
					}
				}
			}
		}
		catch (Exception)
		{
		}
		return null;
	}

	private static string InferKindFromTarget(ICharacter source, Identity target)
	{
		//IL_0002: Unknown result type (might be due to invalid IL or missing references)
		//IL_0017: Unknown result type (might be due to invalid IL or missing references)
		//IL_0057: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a6: Unknown result type (might be due to invalid IL or missing references)
		string a = ResolveNpcName(source, target);
		if (string.Equals(a, "Prophet Yutt Thrak", StringComparison.OrdinalIgnoreCase) || ThrakGardenKeyInteractionRules.IsProphet(target))
		{
			return (ThrakGardenKeyQuestRuntime.IsMissionActive(source, "Mission:55563C16") && ThrakGardenKeyQuestRuntime.HasProphetDeviceInspected(source)) ? "ProphetInsignia" : "ProphetDevice";
		}
		if (string.Equals(a, "Hypnagogic Urga-Lum Thrak", StringComparison.OrdinalIgnoreCase) || ThrakGardenKeyInteractionRules.IsHypnagogic(target))
		{
			return (ThrakGardenKeyQuestRuntime.GetSoulCount(source) >= 3 || ThrakGardenKeyQuestRuntime.IsMissionActive(source, "Mission:5556893D")) ? "HypReturn" : "HypAnalyzer";
		}
		if (string.Equals(a, "Dreaming Silvertail", StringComparison.OrdinalIgnoreCase) || string.Equals(a, "Cursed Silvertail", StringComparison.OrdinalIgnoreCase) || ThrakGardenKeyInteractionRules.IsDreamingSilvertail(target))
		{
			return "Silvertail";
		}
		return string.Empty;
	}

	private static int FindExpectedItemInInventory(ICharacter source, TradeKind kind)
	{
		switch (kind)
		{
		case TradeKind.ProphetDevice:
		case TradeKind.HypAnalyzer:
			if (ThrakGardenKeyQuestRuntime.HasAnalyzer(source))
			{
				return ThrakGardenKeyQuestRuntime.HasFavoredAnalyzer(source) ? 214785 : 214998;
			}
			break;
		case TradeKind.ProphetInsignia:
			if (ThrakGardenKeyQuestRuntime.HasInsignia(source))
			{
				return 214789;
			}
			break;
		case TradeKind.HypReturn:
		case TradeKind.Silvertail:
			if (ThrakGardenKeyQuestRuntime.HasFavoredAnalyzer(source))
			{
				return 214785;
			}
			if (ThrakGardenKeyQuestRuntime.HasAnalyzer(source))
			{
				return 214998;
			}
			break;
		}
		return 0;
	}

	private static void SendRejectedItems(ICharacter source, Identity npcIdentity, TradeKind kind, int soulsBeforeSilvertail = 0, int returnedItemId = 0, int returnedQuality = 1)
	{
		//IL_0079: Unknown result type (might be due to invalid IL or missing references)
		//IL_006b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0071: Expected O, but got Unknown
		int num = 1;
		if (kind == TradeKind.HypReturn)
		{
			num = 0;
		}
		else if (kind == TradeKind.Silvertail && soulsBeforeSilvertail >= 2)
		{
			num = 0;
		}
		else if (kind == TradeKind.HypAnalyzer)
		{
			num = 0;
		}
		Item[] items = (Item[])(object)new Item[0];
		if (num == 1 && returnedItemId > 0)
		{
			int num2 = ((returnedQuality <= 0) ? 1 : returnedQuality);
			items = (Item[])(object)new Item[1]
			{
				new Item(num2, returnedItemId, returnedItemId)
			};
		}
		BaseMessageHandler<KnuBotRejectedItemsMessage, KnuBotRejectedItemsMessageHandler>.Default.Send(source, npcIdentity, items, num);
	}

	private static void SendRejectedItems(ICharacter source, Identity npcIdentity)
	{
		//IL_0002: Unknown result type (might be due to invalid IL or missing references)
		SendRejectedItems(source, npcIdentity, TradeKind.None);
	}

	private static TradeKind ParseKind(string kind)
	{
		if (string.Equals(kind, "ProphetDevice", StringComparison.OrdinalIgnoreCase))
		{
			return TradeKind.ProphetDevice;
		}
		if (string.Equals(kind, "ProphetInsignia", StringComparison.OrdinalIgnoreCase))
		{
			return TradeKind.ProphetInsignia;
		}
		if (string.Equals(kind, "HypAnalyzer", StringComparison.OrdinalIgnoreCase))
		{
			return TradeKind.HypAnalyzer;
		}
		if (string.Equals(kind, "HypReturn", StringComparison.OrdinalIgnoreCase))
		{
			return TradeKind.HypReturn;
		}
		if (string.Equals(kind, "Silvertail", StringComparison.OrdinalIgnoreCase))
		{
			return TradeKind.Silvertail;
		}
		return TradeKind.None;
	}

	private static int ResolveItemId(IItem item)
	{
		if (item == null)
		{
			return 0;
		}
		int lowID = item.LowID;
		int highID = item.HighID;
		if (lowID == 214998 || highID == 214998)
		{
			return 214998;
		}
		if (lowID == 214783 || highID == 214783)
		{
			return 214783;
		}
		if (lowID == 214785 || highID == 214785)
		{
			return 214785;
		}
		if (lowID == 214789 || highID == 214789)
		{
			return 214789;
		}
		return (lowID > 0) ? lowID : highID;
	}

	private static ThrakTradeSession GetSession(ICharacter source)
	{
		//IL_0026: Unknown result type (might be due to invalid IL or missing references)
		//IL_002b: Unknown result type (might be due to invalid IL or missing references)
		if (source == null)
		{
			return null;
		}
		lock (SyncRoot)
		{
			Dictionary<int, ThrakTradeSession> sessionsByCharacter = SessionsByCharacter;
			Identity identity = ((IEntity)source).Identity;
			ThrakTradeSession value;
			return sessionsByCharacter.TryGetValue(((Identity)(ref identity)).Instance, out value) ? value : null;
		}
	}

	private static void ClearSession(ICharacter source)
	{
		//IL_0024: Unknown result type (might be due to invalid IL or missing references)
		//IL_0029: Unknown result type (might be due to invalid IL or missing references)
		if (source == null)
		{
			return;
		}
		lock (SyncRoot)
		{
			Dictionary<int, ThrakTradeSession> sessionsByCharacter = SessionsByCharacter;
			Identity identity = ((IEntity)source).Identity;
			sessionsByCharacter.Remove(((Identity)(ref identity)).Instance);
		}
	}

	private static bool IdentitiesEqual(Identity a, Identity b)
	{
		//IL_0003: Unknown result type (might be due to invalid IL or missing references)
		//IL_000a: Unknown result type (might be due to invalid IL or missing references)
		return ((Identity)(ref a)).Type == ((Identity)(ref b)).Type && ((Identity)(ref a)).Instance == ((Identity)(ref b)).Instance;
	}
}
