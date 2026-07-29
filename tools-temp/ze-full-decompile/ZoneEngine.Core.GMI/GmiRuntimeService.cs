using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using AORebirth.Core.Components;
using AORebirth.Core.Entities;
using AORebirth.Core.Inventory;
using AORebirth.Core.Items;
using AORebirth.Database.Dao;
using AORebirth.Database.Entities;
using AORebirth.Enums;
using AORebirth.Interfaces;
using AORebirth.Stats;
using SmokeLounge.AOtomation.Messaging.GameData;
using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;
using ZoneEngine.Core.Mail;
using ZoneEngine.Core.MessageHandlers;

namespace ZoneEngine.Core.GMI;

internal static class GmiRuntimeService
{
	public sealed class GmiVaultItem
	{
		public int LowId { get; set; }

		public int HighId { get; set; }

		public int Quality { get; set; }

		public int Count { get; set; }

		public string Name { get; set; }

		public int Icon { get; set; }
	}

	public sealed class GmiVault
	{
		public long Credits { get; set; }

		public List<GmiVaultItem> Items { get; private set; }

		public GmiVault()
		{
			Items = new List<GmiVaultItem>();
		}
	}

	public const string FailureForbiddenItem = "Cannot Sell Container Backpack Nodrop Unique items";

	private static readonly ConcurrentDictionary<string, GmiVault> ByCharacter = new ConcurrentDictionary<string, GmiVault>(StringComparer.OrdinalIgnoreCase);

	private const string LocalWebVaultDataDir = "C:\\xampp\\htdocs\\market\\data";

	private const string LocalWebPendingDir = "C:\\xampp\\htdocs\\market\\data\\pending";

	private static readonly object PendingLock = new object();

	private static readonly Encoding Utf8NoBom = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);

	public static GmiVault GetOrCreate(ICharacter character)
	{
		string key = ((character != null) ? (((INamedEntity)character).Name ?? string.Empty) : string.Empty);
		return ByCharacter.GetOrAdd(key, (string _) => CreateVaultFromDisk(character));
	}

	public static GmiVault GetOrCreate(string characterName)
	{
		return ByCharacter.GetOrAdd(characterName ?? string.Empty, (string name) => CreateVaultFromDisk(name, 0));
	}

	private static GmiVault CreateVaultFromDisk(ICharacter character)
	{
		//IL_0015: Unknown result type (might be due to invalid IL or missing references)
		//IL_001a: Unknown result type (might be due to invalid IL or missing references)
		string characterName = ((character != null) ? ((INamedEntity)character).Name : null);
		int num;
		if (character == null)
		{
			num = 0;
		}
		else
		{
			Identity identity = ((IEntity)character).Identity;
			num = ((Identity)(ref identity)).Instance;
		}
		int characterInstance = num;
		return CreateVaultFromDisk(characterName, characterInstance);
	}

	private static GmiVault CreateVaultFromDisk(string characterName, int characterInstance)
	{
		GmiVault gmiVault = new GmiVault();
		if (characterInstance != 0)
		{
			ApplyDbSnapshot(GmiVaultDao.Load(characterInstance), gmiVault);
		}
		else
		{
			TryLoadVaultMirror(characterName, characterInstance, gmiVault);
		}
		EnrichVaultItems(gmiVault);
		return gmiVault;
	}

	private static void ApplyDbSnapshot(VaultSnapshot snap, GmiVault vault)
	{
		if (snap == null || vault == null)
		{
			return;
		}
		vault.Credits = snap.Credits;
		vault.Items.Clear();
		if (snap.Items == null)
		{
			return;
		}
		for (int i = 0; i < snap.Items.Count; i++)
		{
			VaultItemRow val = snap.Items[i];
			if (val != null)
			{
				vault.Items.Add(new GmiVaultItem
				{
					LowId = val.LowId,
					HighId = val.HighId,
					Quality = val.Quality,
					Count = val.StackCount,
					Icon = val.Icon,
					Name = (val.ItemName ?? string.Empty)
				});
			}
		}
	}

	private static void ReloadVaultFromDatabase(ICharacter character, GmiVault vault)
	{
		//IL_0008: Unknown result type (might be due to invalid IL or missing references)
		//IL_000d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0023: Unknown result type (might be due to invalid IL or missing references)
		//IL_0028: Unknown result type (might be due to invalid IL or missing references)
		if (character != null && vault != null)
		{
			Identity identity = ((IEntity)character).Identity;
			if (((Identity)(ref identity)).Instance != 0)
			{
				identity = ((IEntity)character).Identity;
				ApplyDbSnapshot(GmiVaultDao.Load(((Identity)(ref identity)).Instance), vault);
				EnrichVaultItems(vault);
			}
		}
	}

	public static bool TryDepositCredits(ICharacter character, int credits, out string failureReason)
	{
		failureReason = null;
		if (character == null)
		{
			failureReason = "No character.";
			return false;
		}
		if (credits <= 0)
		{
			failureReason = "Credits must be positive.";
			return false;
		}
		int num = CashStatRules.Clamp(((IStats)character).Stats[(StatIds)61].BaseValue);
		if (num < credits)
		{
			failureReason = "Not enough credits.";
			return false;
		}
		int num2 = CashStatRules.Clamp((long)num - (long)credits);
		((IStats)character).Stats[(StatIds)61].Set((uint)num2, false);
		BaseMessageHandler<StatMessage, StatMessageHandler>.Default.SendSingle(character, 61, (uint)num2);
		((IStats)character).Stats[(StatIds)521].Set(4u, false);
		BaseMessageHandler<StatMessage, StatMessageHandler>.Default.SendSingle(character, 521, 4u);
		GmiVault orCreate = GetOrCreate(character);
		ReloadVaultFromDatabase(character, orCreate);
		orCreate.Credits += credits;
		PersistVaultMirror(character, orCreate);
		return true;
	}

	public static bool TryDepositItem(ICharacter character, int clientItemId, int containerType, int placement, out string failureReason)
	{
		failureReason = null;
		if (character == null)
		{
			failureReason = "No character.";
			return false;
		}
		if (placement < 0)
		{
			failureReason = "Invalid item deposit.";
			return false;
		}
		NormalizeMarketSendRefs(ref containerType, ref placement);
		int pageType = containerType;
		if (!IsGmiDepositSourcePage(pageType))
		{
			if (containerType < 64 || containerType >= 200)
			{
				failureReason = string.Format(CultureInfo.InvariantCulture, "GMI items must come from Inventory (got container {0}).", containerType);
				return false;
			}
			placement = containerType;
			pageType = 104;
			containerType = pageType;
		}
		if (!TryGetPage(character, pageType, out var page) || page == null)
		{
			if (!TryGetInventoryPage(character, out page) || page == null)
			{
				failureReason = "No inventory.";
				return false;
			}
			pageType = 104;
		}
		if (!TryFindInventorySlot(page, placement, out var contentKey, out var item) || item == null)
		{
			if (!TryFindItemByClientId(character, clientItemId, placement, out pageType, out contentKey, out item) || item == null)
			{
				failureReason = BuildNotFoundReason(page, clientItemId, placement);
				return false;
			}
			if (!TryGetPage(character, pageType, out page) || page == null)
			{
				failureReason = "No inventory page for deposit.";
				return false;
			}
		}
		bool flag = InventoryItemRules.IsGmiForbiddenContainerItem(item);
		bool flag2 = InventoryItemRules.IsUnique(item);
		bool flag3 = IsGmiNoDrop(item);
		if (flag || flag2 || flag3)
		{
			failureReason = "Cannot Sell Container Backpack Nodrop Unique items";
			return false;
		}
		GmiVault orCreate = GetOrCreate(character);
		ReloadVaultFromDatabase(character, orCreate);
		if (orCreate.Items != null && orCreate.Items.Count >= 21)
		{
			failureReason = "Market inventory full (21/21).";
			return false;
		}
		int count = Math.Max(1, item.MultipleCount);
		int quality = item.Quality;
		int highID = item.HighID;
		int lowID = item.LowID;
		int placement2 = ((contentKey >= page.FirstSlotNumber) ? contentKey : (page.FirstSlotNumber + contentKey));
		try
		{
			((IItemContainer)character).BaseInventory.RemoveItem(pageType, contentKey);
			BaseMessageHandler<CharacterActionMessage, CharacterActionMessageHandler>.Default.SendDeleteItem(character, pageType, placement2);
		}
		catch (Exception)
		{
			failureReason = "Failed to remove item from inventory.";
			return false;
		}
		((IItemContainer)character).BaseInventory.Write();
		((IStats)character).Stats[(StatIds)521].Set(4u, false);
		BaseMessageHandler<StatMessage, StatMessageHandler>.Default.SendSingle(character, 521, 4u);
		GmiVaultItem gmiVaultItem = new GmiVaultItem
		{
			LowId = lowID,
			HighId = highID,
			Quality = quality,
			Count = count
		};
		ApplyItemMeta(gmiVaultItem, item);
		orCreate.Items.Add(gmiVaultItem);
		PersistVaultMirror(character, orCreate);
		return true;
	}

	public static int ProcessPendingWithdrawals(ICharacter character)
	{
		//IL_0094: Unknown result type (might be due to invalid IL or missing references)
		//IL_0099: Unknown result type (might be due to invalid IL or missing references)
		if (character == null || string.IsNullOrEmpty(((INamedEntity)character).Name))
		{
			return 0;
		}
		GmiVault orCreate = GetOrCreate(character);
		ReloadVaultFromDatabase(character, orCreate);
		int num = 0;
		lock (PendingLock)
		{
			if (!Directory.Exists("C:\\xampp\\htdocs\\market\\data\\pending"))
			{
				return 0;
			}
			string[] files;
			try
			{
				files = Directory.GetFiles("C:\\xampp\\htdocs\\market\\data\\pending", "*.json");
			}
			catch
			{
				return 0;
			}
			Array.Sort(files);
			string b = SanitizeFileToken(((INamedEntity)character).Name);
			Identity identity = ((IEntity)character).Identity;
			int instance = ((Identity)(ref identity)).Instance;
			foreach (string text in files)
			{
				string text2;
				try
				{
					text2 = File.ReadAllText(text, Encoding.UTF8);
				}
				catch
				{
					continue;
				}
				string value = ReadJsonStringLoose(text2, "character");
				string fileId = ReadJsonStringLoose(text2, "characterId");
				if ((string.IsNullOrEmpty(value) || !string.Equals(SanitizeFileToken(value), b, StringComparison.OrdinalIgnoreCase)) && !CharacterIdMatches(fileId, instance))
				{
					continue;
				}
				string a = ReadJsonStringLoose(text2, "kind") ?? string.Empty;
				string failureReason = null;
				bool flag = false;
				bool flag2 = ReadJsonIntLoose(text2, "preDebited") != 0;
				if (string.Equals(a, "credits", StringComparison.OrdinalIgnoreCase))
				{
					int credits = ReadJsonIntLoose(text2, "amount");
					flag = ((!flag2) ? TryWithdrawCredits(character, credits, out failureReason) : TryMailWithdrawCreditsOnly(character, credits, out failureReason));
				}
				else if (string.Equals(a, "item", StringComparison.OrdinalIgnoreCase))
				{
					int itemIndex = ReadJsonIntLoose(text2, "index");
					int num2 = ReadJsonIntLoose(text2, "count");
					if (num2 <= 0)
					{
						num2 = 1;
					}
					flag = ((!flag2) ? TryWithdrawItem(character, itemIndex, num2, out failureReason) : TryMailWithdrawItemOnly(character, text2, num2, out failureReason));
				}
				else if (string.Equals(a, "purchase_item", StringComparison.OrdinalIgnoreCase))
				{
					int num3 = ReadJsonIntLoose(text2, "count");
					if (num3 <= 0)
					{
						num3 = 1;
					}
					flag = TryMailPurchaseItemOnly(character, text2, num3, out failureReason);
				}
				else
				{
					failureReason = "Unknown withdraw kind.";
				}
				if (flag)
				{
					num++;
					try
					{
						File.Delete(text);
					}
					catch
					{
					}
					continue;
				}
				try
				{
					string text3 = text + ".failed";
					if (File.Exists(text3))
					{
						File.Delete(text3);
					}
					File.Move(text, text3);
					File.WriteAllText(text3 + ".txt", failureReason ?? "withdraw failed", Encoding.UTF8);
				}
				catch
				{
				}
			}
		}
		return num;
	}

	public static bool TryWithdrawCredits(ICharacter character, int credits, out string failureReason)
	{
		failureReason = null;
		if (character == null)
		{
			failureReason = "No character.";
			return false;
		}
		if (credits <= 0)
		{
			failureReason = "Credits must be positive.";
			return false;
		}
		GmiVault orCreate = GetOrCreate(character);
		ReloadVaultFromDatabase(character, orCreate);
		if (orCreate.Credits < credits)
		{
			failureReason = "Not enough market credits.";
			return false;
		}
		string subject = "Credit withdrawal";
		string body = string.Format(CultureInfo.InvariantCulture, "You withdrew {0} credits from the Omni-Trade GMI. Deliveries use the mail system and expire within 48 hours.", credits);
		if (!MailRuntimeService.TryEnqueueGmiDelivery(((INamedEntity)character).Name, credits, 0, 0, 0, 0, subject, body, out failureReason))
		{
			return false;
		}
		orCreate.Credits -= credits;
		PersistVaultMirror(character, orCreate);
		return true;
	}

	public static bool TryWithdrawItem(ICharacter character, int itemIndex, int count, out string failureReason)
	{
		failureReason = null;
		if (character == null)
		{
			failureReason = "No character.";
			return false;
		}
		GmiVault orCreate = GetOrCreate(character);
		ReloadVaultFromDatabase(character, orCreate);
		if (itemIndex < 0 || itemIndex >= orCreate.Items.Count)
		{
			failureReason = "Market inventory slot is empty.";
			return false;
		}
		GmiVaultItem gmiVaultItem = orCreate.Items[itemIndex];
		if (gmiVaultItem == null || gmiVaultItem.Count <= 0)
		{
			failureReason = "Market inventory slot is empty.";
			return false;
		}
		int num = ((count <= 0) ? gmiVaultItem.Count : Math.Min(count, gmiVaultItem.Count));
		string arg = (string.IsNullOrEmpty(gmiVaultItem.Name) ? ("Item " + gmiVaultItem.LowId.ToString(CultureInfo.InvariantCulture)) : gmiVaultItem.Name);
		string subject = "Item transfer";
		string body = string.Format(CultureInfo.InvariantCulture, "You withdrew {0} x {1} (QL {2}) from the Omni-Trade GMI. Deliveries use the mail system and expire within 48 hours.", num, arg, gmiVaultItem.Quality);
		if (!MailRuntimeService.TryEnqueueGmiDelivery(((INamedEntity)character).Name, 0, gmiVaultItem.LowId, gmiVaultItem.HighId, gmiVaultItem.Quality, num, subject, body, out failureReason))
		{
			return false;
		}
		if (num >= gmiVaultItem.Count)
		{
			orCreate.Items.RemoveAt(itemIndex);
		}
		else
		{
			gmiVaultItem.Count -= num;
		}
		PersistVaultMirror(character, orCreate);
		return true;
	}

	public static bool TryMailWithdrawCreditsOnly(ICharacter character, int credits, out string failureReason)
	{
		failureReason = null;
		if (character == null)
		{
			failureReason = "No character.";
			return false;
		}
		if (credits <= 0)
		{
			failureReason = "Credits must be positive.";
			return false;
		}
		ReloadVaultFromDisk(character);
		string subject = "Credit withdrawal";
		string body = string.Format(CultureInfo.InvariantCulture, "You withdrew {0} credits from the Omni-Trade GMI. Deliveries use the mail system and expire within 48 hours.", credits);
		return MailRuntimeService.TryEnqueueGmiDelivery(((INamedEntity)character).Name, credits, 0, 0, 0, 0, subject, body, out failureReason);
	}

	public static bool TryMailWithdrawItemOnly(ICharacter character, string pendingJson, int count, out string failureReason)
	{
		failureReason = null;
		if (character == null)
		{
			failureReason = "No character.";
			return false;
		}
		int num = ReadJsonIntLoose(pendingJson, "lowId");
		int num2 = ReadJsonIntLoose(pendingJson, "highId");
		int num3 = ReadJsonIntLoose(pendingJson, "quality");
		string text = ReadJsonStringLoose(pendingJson, "name");
		if (num <= 0 && num2 <= 0)
		{
			failureReason = "Pending item withdraw missing template ids.";
			return false;
		}
		if (num2 <= 0)
		{
			num2 = num;
		}
		if (num <= 0)
		{
			num = num2;
		}
		if (num3 <= 0)
		{
			num3 = 1;
		}
		int num4 = ((count <= 0) ? 1 : count);
		if (string.IsNullOrEmpty(text))
		{
			text = "Item " + num.ToString(CultureInfo.InvariantCulture);
		}
		ReloadVaultFromDisk(character);
		string subject = "Item transfer";
		string body = string.Format(CultureInfo.InvariantCulture, "You withdrew {0} x {1} (QL {2}) from the Omni-Trade GMI. Deliveries use the mail system and expire within 48 hours.", num4, text, num3);
		return MailRuntimeService.TryEnqueueGmiDelivery(((INamedEntity)character).Name, 0, num, num2, num3, num4, subject, body, out failureReason);
	}

	public static bool TryMailPurchaseItemOnly(ICharacter character, string pendingJson, int count, out string failureReason)
	{
		failureReason = null;
		if (character == null)
		{
			failureReason = "No character.";
			return false;
		}
		int num = ReadJsonIntLoose(pendingJson, "lowId");
		int num2 = ReadJsonIntLoose(pendingJson, "highId");
		int num3 = ReadJsonIntLoose(pendingJson, "quality");
		string text = ReadJsonStringLoose(pendingJson, "name");
		if (num <= 0 && num2 <= 0)
		{
			failureReason = "Pending purchase missing template ids.";
			return false;
		}
		if (num2 <= 0)
		{
			num2 = num;
		}
		if (num <= 0)
		{
			num = num2;
		}
		if (num3 <= 0)
		{
			num3 = 1;
		}
		int num4 = ((count <= 0) ? 1 : count);
		if (string.IsNullOrEmpty(text))
		{
			text = "Item " + num.ToString(CultureInfo.InvariantCulture);
		}
		string subject = "Market purchase successful";
		string body = string.Format(CultureInfo.InvariantCulture, "Item purchased from market. {0} x {1} (QL {2}).", num4, text, num3);
		return MailRuntimeService.TryEnqueueGmiDelivery(((INamedEntity)character).Name, 0, num, num2, num3, num4, subject, body, "Market", out failureReason);
	}

	private static void ReloadVaultFromDisk(ICharacter character)
	{
		//IL_0020: Unknown result type (might be due to invalid IL or missing references)
		//IL_0025: Unknown result type (might be due to invalid IL or missing references)
		if (character != null && !string.IsNullOrEmpty(((INamedEntity)character).Name))
		{
			string name = ((INamedEntity)character).Name;
			Identity identity = ((IEntity)character).Identity;
			GmiVault value = CreateVaultFromDisk(name, ((Identity)(ref identity)).Instance);
			ByCharacter[((INamedEntity)character).Name] = value;
		}
	}

	private static int ReadJsonIntLoose(string json, string key)
	{
		return ReadJsonInt(json, key);
	}

	private static string ReadJsonStringLoose(string json, string key)
	{
		return ReadJsonString(json, key);
	}

	private static string BuildNotFoundReason(IInventoryPage page, int clientItemId, int placement)
	{
		StringBuilder stringBuilder = new StringBuilder(128);
		stringBuilder.AppendFormat(CultureInfo.InvariantCulture, "item id {0} / slot {1} not found.", clientItemId, placement);
		if (page != null && TryFindInventorySlot(page, placement, out var _, out var item) && item != null)
		{
			stringBuilder.AppendFormat(CultureInfo.InvariantCulture, " Slot has {0}/{1} QL{2} x{3}.", item.LowID, item.HighID, item.Quality, item.MultipleCount);
			return stringBuilder.ToString();
		}
		if (page != null)
		{
			int num = 0;
			stringBuilder.Append(" Inventory LowIDs:");
			foreach (KeyValuePair<int, IItem> item2 in page.List())
			{
				if (item2.Value != null)
				{
					stringBuilder.AppendFormat(CultureInfo.InvariantCulture, " [{0}]={1}x{2}", item2.Key, item2.Value.LowID, item2.Value.MultipleCount);
					num++;
					if (num >= 8)
					{
						stringBuilder.Append("…");
						break;
					}
				}
			}
			if (num == 0)
			{
				stringBuilder.Append(" (empty)");
			}
		}
		return stringBuilder.ToString();
	}

	private static bool IsGmiDepositSourcePage(int containerType)
	{
		return containerType == 104 || containerType == 101 || containerType == 102 || containerType == 103 || containerType == 115 || containerType == 110;
	}

	private static void NormalizeMarketSendRefs(ref int containerType, ref int placement)
	{
		bool flag = IsGmiDepositSourcePage(containerType);
		bool flag2 = IsGmiDepositSourcePage(placement);
		if (!flag && flag2)
		{
			int num = containerType;
			containerType = placement;
			placement = num;
		}
	}

	private static bool IsGmiNoDrop(IItem item)
	{
		if (item == null)
		{
			return false;
		}
		if (ItemLoader.ItemList.TryGetValue(item.LowID, out var value) && value != null && value.IsNoDrop())
		{
			return true;
		}
		if (item.HighID != item.LowID && ItemLoader.ItemList.TryGetValue(item.HighID, out value) && value != null && value.IsNoDrop())
		{
			return true;
		}
		if (((uint)item.Flags & 0x4000000u) != 0)
		{
			return true;
		}
		return false;
	}

	private static void PersistVaultMirror(ICharacter character, GmiVault vault)
	{
		//IL_001f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0024: Unknown result type (might be due to invalid IL or missing references)
		//IL_007d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0082: Unknown result type (might be due to invalid IL or missing references)
		//IL_0090: Unknown result type (might be due to invalid IL or missing references)
		//IL_009e: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ac: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ba: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c8: Unknown result type (might be due to invalid IL or missing references)
		//IL_00df: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ee: Expected O, but got Unknown
		if (character == null || vault == null)
		{
			return;
		}
		try
		{
			EnrichVaultItems(vault);
			Identity identity = ((IEntity)character).Identity;
			int instance = ((Identity)(ref identity)).Instance;
			if (instance == 0)
			{
				return;
			}
			List<VaultItemRow> list = new List<VaultItemRow>();
			if (vault.Items != null)
			{
				for (int i = 0; i < vault.Items.Count; i++)
				{
					GmiVaultItem gmiVaultItem = vault.Items[i];
					if (gmiVaultItem != null)
					{
						list.Add(new VaultItemRow
						{
							LowId = gmiVaultItem.LowId,
							HighId = gmiVaultItem.HighId,
							Quality = gmiVaultItem.Quality,
							StackCount = gmiVaultItem.Count,
							Icon = gmiVaultItem.Icon,
							ItemName = (gmiVaultItem.Name ?? string.Empty),
							SlotIndex = (short)i
						});
					}
				}
			}
			GmiVaultDao.Save(instance, ((INamedEntity)character).Name ?? string.Empty, vault.Credits, (IList<VaultItemRow>)list);
		}
		catch
		{
		}
	}

	private static void TryWriteVaultFiles(string characterName, int characterInstance, GmiVault vault)
	{
		if (vault == null)
		{
			return;
		}
		try
		{
			if (!Directory.Exists("C:\\xampp\\htdocs\\market\\data"))
			{
				Directory.CreateDirectory("C:\\xampp\\htdocs\\market\\data");
			}
			string characterIdHex = characterInstance.ToString("X", CultureInfo.InvariantCulture);
			string contents = BuildVaultJson(characterName, characterIdHex, vault);
			List<string> list = new List<string>();
			if (!string.IsNullOrEmpty(characterName))
			{
				list.Add(Path.Combine("C:\\xampp\\htdocs\\market\\data", "name_" + SanitizeFileToken(characterName) + ".json"));
			}
			if (characterInstance != 0)
			{
				foreach (string item in ExpandCharacterIdTokens(characterInstance))
				{
					list.Add(Path.Combine("C:\\xampp\\htdocs\\market\\data", "char_" + item + ".json"));
				}
			}
			for (int i = 0; i < list.Count; i++)
			{
				File.WriteAllText(list[i], contents, Utf8NoBom);
			}
		}
		catch
		{
		}
	}

	private static void TryLoadVaultMirror(string characterName, int characterInstance, GmiVault vault)
	{
		if (vault == null)
		{
			return;
		}
		try
		{
			if (!Directory.Exists("C:\\xampp\\htdocs\\market\\data"))
			{
				return;
			}
			List<string> list = new List<string>();
			if (characterInstance != 0)
			{
				foreach (string item in ExpandCharacterIdTokens(characterInstance))
				{
					list.Add(Path.Combine("C:\\xampp\\htdocs\\market\\data", "char_" + item + ".json"));
				}
			}
			if (!string.IsNullOrEmpty(characterName))
			{
				list.Add(Path.Combine("C:\\xampp\\htdocs\\market\\data", "name_" + SanitizeFileToken(characterName) + ".json"));
			}
			string text = null;
			DateTime dateTime = DateTime.MinValue;
			for (int i = 0; i < list.Count; i++)
			{
				if (File.Exists(list[i]))
				{
					DateTime lastWriteTimeUtc = File.GetLastWriteTimeUtc(list[i]);
					if (text == null || lastWriteTimeUtc >= dateTime)
					{
						dateTime = lastWriteTimeUtc;
						text = list[i];
					}
				}
			}
			if (text != null)
			{
				string json = File.ReadAllText(text, Encoding.UTF8);
				TryParseVaultJson(json, vault);
			}
		}
		catch
		{
		}
	}

	private static IEnumerable<string> ExpandCharacterIdTokens(int characterInstance)
	{
		string hex = characterInstance.ToString("X", CultureInfo.InvariantCulture);
		string dec = characterInstance.ToString(CultureInfo.InvariantCulture);
		yield return hex;
		if (!string.Equals(hex, dec, StringComparison.OrdinalIgnoreCase))
		{
			yield return dec;
		}
		string hexLower = hex.ToLowerInvariant();
		if (!string.Equals(hexLower, hex, StringComparison.Ordinal))
		{
			yield return hexLower;
		}
	}

	private static bool CharacterIdMatches(string fileId, int characterInstance)
	{
		if (string.IsNullOrEmpty(fileId))
		{
			return false;
		}
		foreach (string item in ExpandCharacterIdTokens(characterInstance))
		{
			if (string.Equals(fileId, item, StringComparison.OrdinalIgnoreCase))
			{
				return true;
			}
		}
		return false;
	}

	private static bool TryParseVaultJson(string json, GmiVault vault)
	{
		if (string.IsNullOrEmpty(json) || vault == null)
		{
			return false;
		}
		try
		{
			Match match = Regex.Match(json, "\"credits\"\\s*:\\s*(-?\\d+)");
			if (match.Success && long.TryParse(match.Groups[1].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var result))
			{
				vault.Credits = result;
			}
			vault.Items.Clear();
			MatchCollection matchCollection = Regex.Matches(json, "\\{([^{}]+)\\}");
			for (int i = 0; i < matchCollection.Count; i++)
			{
				string value = matchCollection[i].Groups[1].Value;
				if (value.IndexOf("lowId", StringComparison.OrdinalIgnoreCase) >= 0)
				{
					GmiVaultItem gmiVaultItem = new GmiVaultItem();
					gmiVaultItem.LowId = ReadJsonInt(value, "lowId");
					gmiVaultItem.HighId = ReadJsonInt(value, "highId");
					gmiVaultItem.Quality = ReadJsonInt(value, "quality");
					gmiVaultItem.Count = ReadJsonInt(value, "count");
					gmiVaultItem.Icon = ReadJsonInt(value, "icon");
					gmiVaultItem.Name = ReadJsonString(value, "name");
					if (gmiVaultItem.LowId > 0 || gmiVaultItem.HighId > 0)
					{
						vault.Items.Add(gmiVaultItem);
					}
				}
			}
			return true;
		}
		catch
		{
			return false;
		}
	}

	private static int ReadJsonInt(string body, string key)
	{
		Match match = Regex.Match(body, "\"" + key + "\"\\s*:\\s*(-?\\d+)", RegexOptions.IgnoreCase);
		if (match.Success && int.TryParse(match.Groups[1].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var result))
		{
			return result;
		}
		return 0;
	}

	private static string ReadJsonString(string body, string key)
	{
		Match match = Regex.Match(body, "\"" + key + "\"\\s*:\\s*\"((?:\\\\.|[^\"\\\\])*)\"", RegexOptions.IgnoreCase);
		if (!match.Success)
		{
			return null;
		}
		return match.Groups[1].Value.Replace("\\\"", "\"").Replace("\\\\", "\\");
	}

	private static void EnrichVaultItems(GmiVault vault)
	{
		if (vault != null && vault.Items != null)
		{
			for (int i = 0; i < vault.Items.Count; i++)
			{
				ApplyItemMeta(vault.Items[i], null);
			}
		}
	}

	private static void ApplyItemMeta(GmiVaultItem vaultItem, IItem liveItem)
	{
		if (vaultItem == null)
		{
			return;
		}
		if (liveItem != null)
		{
			try
			{
				int attribute = liveItem.GetAttribute(79);
				if (attribute > 0)
				{
					vaultItem.Icon = attribute;
				}
			}
			catch
			{
			}
		}
		try
		{
			DBItemName val = ((Dao<DBItemName, ItemNamesDao>)(object)Dao<DBItemName, ItemNamesDao>.Instance).Get(vaultItem.LowId);
			if (val == null && vaultItem.HighId != vaultItem.LowId)
			{
				val = ((Dao<DBItemName, ItemNamesDao>)(object)Dao<DBItemName, ItemNamesDao>.Instance).Get(vaultItem.HighId);
			}
			if (val != null)
			{
				if (string.IsNullOrEmpty(vaultItem.Name) && !string.IsNullOrEmpty(val.Name))
				{
					vaultItem.Name = val.Name;
				}
				if (vaultItem.Icon <= 0 && !string.IsNullOrEmpty(val.Icon) && int.TryParse(val.Icon, NumberStyles.Integer, CultureInfo.InvariantCulture, out var result) && result > 0)
				{
					vaultItem.Icon = result;
				}
			}
		}
		catch
		{
		}
		if (vaultItem.Icon <= 0)
		{
			try
			{
				if (ItemLoader.ItemList.ContainsKey(vaultItem.LowId))
				{
					ItemTemplate val2 = ItemLoader.ItemList[vaultItem.LowId];
					if (val2.Stats != null && val2.Stats.ContainsKey(79))
					{
						vaultItem.Icon = val2.Stats[79];
					}
				}
			}
			catch
			{
			}
		}
		if (string.IsNullOrEmpty(vaultItem.Name))
		{
			vaultItem.Name = "Item " + vaultItem.LowId.ToString(CultureInfo.InvariantCulture);
		}
	}

	private static string BuildVaultJson(string characterName, string characterIdHex, GmiVault vault)
	{
		StringBuilder stringBuilder = new StringBuilder(256);
		stringBuilder.Append("{\"character\":\"");
		stringBuilder.Append(EscapeJson(characterName ?? string.Empty));
		stringBuilder.Append("\",\"characterId\":\"");
		stringBuilder.Append(EscapeJson(characterIdHex ?? string.Empty));
		stringBuilder.Append("\",\"credits\":");
		stringBuilder.Append(vault.Credits.ToString(CultureInfo.InvariantCulture));
		stringBuilder.Append(",\"items\":[");
		for (int i = 0; i < vault.Items.Count; i++)
		{
			GmiVaultItem gmiVaultItem = vault.Items[i];
			if (i > 0)
			{
				stringBuilder.Append(',');
			}
			stringBuilder.Append("{\"lowId\":");
			stringBuilder.Append(gmiVaultItem.LowId.ToString(CultureInfo.InvariantCulture));
			stringBuilder.Append(",\"highId\":");
			stringBuilder.Append(gmiVaultItem.HighId.ToString(CultureInfo.InvariantCulture));
			stringBuilder.Append(",\"quality\":");
			stringBuilder.Append(gmiVaultItem.Quality.ToString(CultureInfo.InvariantCulture));
			stringBuilder.Append(",\"count\":");
			stringBuilder.Append(gmiVaultItem.Count.ToString(CultureInfo.InvariantCulture));
			stringBuilder.Append(",\"icon\":");
			stringBuilder.Append(gmiVaultItem.Icon.ToString(CultureInfo.InvariantCulture));
			stringBuilder.Append(",\"name\":\"");
			stringBuilder.Append(EscapeJson(gmiVaultItem.Name ?? string.Empty));
			stringBuilder.Append("\"}");
		}
		stringBuilder.Append("]}");
		return stringBuilder.ToString();
	}

	private static string SanitizeFileToken(string value)
	{
		if (string.IsNullOrEmpty(value))
		{
			return "unknown";
		}
		StringBuilder stringBuilder = new StringBuilder(value.Length);
		string text = value.Trim().ToLowerInvariant();
		foreach (char c in text)
		{
			if ((c >= 'a' && c <= 'z') || (c >= '0' && c <= '9') || c == '-' || c == '_')
			{
				stringBuilder.Append(c);
			}
			else if (c == ' ')
			{
				stringBuilder.Append('_');
			}
		}
		return (stringBuilder.Length == 0) ? "unknown" : stringBuilder.ToString();
	}

	private static string EscapeJson(string value)
	{
		if (string.IsNullOrEmpty(value))
		{
			return string.Empty;
		}
		return value.Replace("\\", "\\\\").Replace("\"", "\\\"");
	}

	private static bool TryGetPage(ICharacter character, int pageType, out IInventoryPage page)
	{
		page = null;
		if (character == null || ((IItemContainer)character).BaseInventory == null || ((IItemContainer)character).BaseInventory.Pages == null)
		{
			return false;
		}
		try
		{
			if (!((IItemContainer)character).BaseInventory.Pages.ContainsKey(pageType))
			{
				return false;
			}
			page = ((IItemContainer)character).BaseInventory.Pages[pageType];
			return page != null;
		}
		catch
		{
			page = null;
			return false;
		}
	}

	private static bool TryGetInventoryPage(ICharacter character, out IInventoryPage page)
	{
		return TryGetPage(character, 104, out page);
	}

	private static bool TryFindItemByClientId(ICharacter character, int clientItemId, int placementHint, out int pageType, out int contentKey, out IItem item)
	{
		pageType = 104;
		contentKey = -1;
		item = null;
		if (character == null || ((IItemContainer)character).BaseInventory == null || clientItemId <= 0)
		{
			return false;
		}
		List<int> list = new List<int>();
		list.Add(104);
		list.Add(101);
		list.Add(102);
		list.Add(103);
		list.Add(115);
		foreach (KeyValuePair<int, IInventoryPage> page2 in ((IItemContainer)character).BaseInventory.Pages)
		{
			if (page2.Value is BackPackInventoryPage && !list.Contains(page2.Key))
			{
				list.Add(page2.Key);
			}
		}
		for (int i = 0; i < list.Count; i++)
		{
			if (!TryGetPage(character, list[i], out var page) || page == null)
			{
				continue;
			}
			foreach (KeyValuePair<int, IItem> item2 in page.List())
			{
				IItem value = item2.Value;
				if (!ItemMatchesClientId(value, clientItemId) || value.MultipleCount != placementHint)
				{
					continue;
				}
				pageType = list[i];
				contentKey = item2.Key;
				item = value;
				return true;
			}
			foreach (KeyValuePair<int, IItem> item3 in page.List())
			{
				IItem value2 = item3.Value;
				if (!ItemMatchesClientId(value2, clientItemId))
				{
					continue;
				}
				pageType = list[i];
				contentKey = item3.Key;
				item = value2;
				return true;
			}
		}
		return false;
	}

	private static bool ItemMatchesClientId(IItem candidate, int clientItemId)
	{
		if (candidate == null || clientItemId <= 0)
		{
			return false;
		}
		if (candidate.LowID == clientItemId || candidate.HighID == clientItemId)
		{
			return true;
		}
		try
		{
			if (candidate.GetAttribute(79) == clientItemId)
			{
				return true;
			}
		}
		catch
		{
		}
		return false;
	}

	private static bool TryFindInventorySlot(IInventoryPage page, int placement, out int contentKey, out IItem item)
	{
		contentKey = placement;
		item = null;
		if (page == null)
		{
			return false;
		}
		if (page.ValidSlot(placement) && page[placement] != null)
		{
			contentKey = placement;
			item = page[placement];
			return true;
		}
		if (placement >= 0 && placement < page.MaxSlots)
		{
			int num = page.FirstSlotNumber + placement;
			if (page.ValidSlot(num) && page[num] != null)
			{
				contentKey = num;
				item = page[num];
				return true;
			}
			if (page[placement] != null)
			{
				contentKey = placement;
				item = page[placement];
				return true;
			}
		}
		if (placement >= page.FirstSlotNumber && placement < page.FirstSlotNumber + page.MaxSlots)
		{
			int num2 = placement - page.FirstSlotNumber;
			if (page[num2] != null)
			{
				contentKey = num2;
				item = page[num2];
				return true;
			}
		}
		return false;
	}
}
