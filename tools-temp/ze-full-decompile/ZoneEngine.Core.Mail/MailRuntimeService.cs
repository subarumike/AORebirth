using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using AORebirth.Core.Components;
using AORebirth.Core.Entities;
using AORebirth.Core.Inventory;
using AORebirth.Core.Items;
using AORebirth.Database.Dao;
using AORebirth.Database.Entities;
using AORebirth.Enums;
using AORebirth.Interfaces;
using AORebirth.ObjectManager;
using AORebirth.Stats;
using SmokeLounge.AOtomation.Messaging.GameData;
using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;
using ZoneEngine.Core.Controllers;
using ZoneEngine.Core.MessageHandlers;

namespace ZoneEngine.Core.Mail;

internal static class MailRuntimeService
{
	internal sealed class StoredMail
	{
		public int MailId { get; set; }

		public string SenderName { get; set; }

		public int SenderId { get; set; }

		public string RecipientName { get; set; }

		public int RecipientId { get; set; }

		public string Subject { get; set; }

		public string Body { get; set; }

		public int ItemField1 { get; set; }

		public int ItemField2 { get; set; }

		public int AcgLow { get; set; }

		public int AcgHigh { get; set; }

		public int AcgLevel { get; set; }

		public int AcgMultipleCount { get; set; }

		public int Credits { get; set; }

		public byte ExpressFlag { get; set; }

		public DateTime SentAt { get; set; }

		public int SentDayNumber { get; set; }

		public bool IsRead { get; set; }
	}

	public const int StandardPostageCredits = 2000;

	public const int ExpressPostageCredits = 200000;

	public const int ComputerLiteracyLockSeconds = 30;

	public const int MailRetentionDays = 2;

	public const string FailureNoDrop = "You can not send nodrop items through the mail system.";

	public const string FailureNoChests = "You can not send container items through the mail system.";

	private static int nextMailId = 21631436;

	private static readonly ConcurrentDictionary<string, ConcurrentQueue<StoredMail>> ByRecipient = new ConcurrentDictionary<string, ConcurrentQueue<StoredMail>>(StringComparer.OrdinalIgnoreCase);

	public static int AllocateMailId()
	{
		return Interlocked.Increment(ref nextMailId);
	}

	public static int PostageForExpressFlag(byte expressFlag)
	{
		return (expressFlag != 0) ? 200000 : 2000;
	}

	public static bool TrySendMail(ICharacter sender, MailMessage message, out string failureReason, out int mailId)
	{
		//IL_02c4: Unknown result type (might be due to invalid IL or missing references)
		//IL_02c9: Unknown result type (might be due to invalid IL or missing references)
		failureReason = null;
		mailId = 0;
		if (sender == null || message == null)
		{
			failureReason = "Mail send failed.";
			return false;
		}
		Character val = (Character)(object)((sender is Character) ? sender : null);
		if (val != null && val.IsSkillLocked(161))
		{
			int skillLockRemainingSeconds = val.GetSkillLockRemainingSeconds(161);
			failureReason = string.Format(CultureInfo.InvariantCulture, "Computer Literacy is locked for {0} more second(s).", Math.Max(1, skillLockRemainingSeconds));
			return false;
		}
		string text = (message.Recipient ?? string.Empty).Trim();
		if (text.Length == 0)
		{
			failureReason = "Mail requires a recipient name.";
			return false;
		}
		DBCharacter byCharName = Dao<DBCharacter, CharacterDao>.Instance.GetByCharName(text);
		if (byCharName == null)
		{
			failureReason = string.Format(CultureInfo.InvariantCulture, "Unknown mail recipient \"{0}\".", text);
			return false;
		}
		if (string.Equals(((INamedEntity)sender).Name, text, StringComparison.OrdinalIgnoreCase))
		{
			failureReason = "You cannot mail yourself.";
			return false;
		}
		int num = PostageForExpressFlag(message.ExpressFlag);
		int num2 = ((message.Credits > 0) ? message.Credits : 0);
		long num3 = (long)num + (long)num2;
		int num4 = CashStatRules.Clamp(((IStats)sender).Stats[(StatIds)61].BaseValue);
		if (num4 < num3)
		{
			failureReason = string.Format(CultureInfo.InvariantCulture, "Not enough credits. Express/Standard postage is {0}{1}.", num, (num2 > 0) ? string.Format(CultureInfo.InvariantCulture, " plus {0} attached.", num2) : ".");
			return false;
		}
		int acgLow = 0;
		int acgHigh = 0;
		int acgLevel = 0;
		int acgMultipleCount = 0;
		int resolvedContainer = message.ItemField1;
		int resolvedPlacement = message.ItemField2;
		if (message.ItemField1 != 0 || message.ItemField2 != 0)
		{
			if (!TryTakeAttachedItemFromSender(sender, message.ItemField1, message.ItemField2, out var item, out resolvedContainer, out resolvedPlacement, out var failureReason2))
			{
				failureReason = failureReason2 ?? "Could not attach that item.";
				return false;
			}
			acgLow = item.LowID;
			acgHigh = item.HighID;
			acgLevel = item.Quality;
			acgMultipleCount = ResolveAttachedStackCount(item);
		}
		int num5 = CashStatRules.Clamp(num4 - num3);
		((IStats)sender).Stats[(StatIds)61].Set((uint)num5, false);
		BaseMessageHandler<StatMessage, StatMessageHandler>.Default.SendSingle(sender, 61, (uint)num5);
		((IStats)sender).Stats[(StatIds)521].Set(4u, false);
		BaseMessageHandler<StatMessage, StatMessageHandler>.Default.SendSingle(sender, 521, 4u);
		int credits = message.Credits;
		mailId = AllocateMailId();
		DateTime now = DateTime.Now;
		StoredMail obj = new StoredMail
		{
			MailId = mailId,
			SenderName = (((INamedEntity)sender).Name ?? string.Empty)
		};
		Identity identity = ((IEntity)sender).Identity;
		obj.SenderId = ((Identity)(ref identity)).Instance;
		obj.RecipientName = byCharName.Name;
		obj.RecipientId = byCharName.Id;
		obj.Subject = message.Subject ?? string.Empty;
		obj.Body = message.Body ?? string.Empty;
		obj.ItemField1 = resolvedContainer;
		obj.ItemField2 = resolvedPlacement;
		obj.AcgLow = acgLow;
		obj.AcgHigh = acgHigh;
		obj.AcgLevel = acgLevel;
		obj.AcgMultipleCount = acgMultipleCount;
		obj.Credits = credits;
		obj.ExpressFlag = message.ExpressFlag;
		obj.SentAt = now;
		obj.SentDayNumber = ToMailTimeField(now);
		obj.IsRead = false;
		StoredMail item2 = obj;
		ConcurrentQueue<StoredMail> orAdd = ByRecipient.GetOrAdd(byCharName.Name, (string _) => new ConcurrentQueue<StoredMail>());
		orAdd.Enqueue(item2);
		if (val != null)
		{
			ApplyComputerLiteracySendLock(val);
		}
		NotifyRecipientEnvelope(byCharName.Name);
		return true;
	}

	public static IList<StoredMail> PeekMailbox(string characterName)
	{
		if (string.IsNullOrEmpty(characterName) || !ByRecipient.TryGetValue(characterName, out var value))
		{
			return new List<StoredMail>();
		}
		PurgeExpiredMail(characterName, value);
		return new List<StoredMail>(value.ToArray());
	}

	public static IList<MailListEntry> BuildMailboxListEntries(string characterName)
	{
		IList<StoredMail> list = PeekMailbox(characterName);
		List<MailListEntry> list2 = new List<MailListEntry>();
		int num = Math.Min(30, list.Count);
		for (int i = 0; i < num; i++)
		{
			list2.Add(ToListEntry(list[i], summary: true));
		}
		return list2;
	}

	private static void PurgeExpiredMail(string characterName, ConcurrentQueue<StoredMail> queue)
	{
		DateTime date = DateTime.Now.Date;
		List<StoredMail> list = new List<StoredMail>();
		StoredMail result;
		while (queue.TryDequeue(out result))
		{
			if (result != null)
			{
				DateTime dateTime = result.SentAt.Date.AddDays(2.0);
				if (date <= dateTime)
				{
					list.Add(result);
				}
			}
		}
		for (int i = 0; i < list.Count; i++)
		{
			queue.Enqueue(list[i]);
		}
	}

	public static bool TryBuildMailDetail(string characterName, ulong mailId, out MailListEntry detail)
	{
		detail = null;
		StoredMail storedMail = FindMail(characterName, mailId);
		if (storedMail == null)
		{
			return false;
		}
		storedMail.IsRead = true;
		detail = ToListEntry(storedMail, summary: false);
		return true;
	}

	public static bool TryTakeAll(ICharacter character, ulong mailId, out string failureReason, out MailListEntry updatedDetail)
	{
		//IL_01a2: Unknown result type (might be due to invalid IL or missing references)
		//IL_01a9: Expected O, but got Unknown
		failureReason = null;
		updatedDetail = null;
		if (character == null || string.IsNullOrEmpty(((INamedEntity)character).Name))
		{
			failureReason = "Take All failed.";
			return false;
		}
		StoredMail storedMail = FindMail(((INamedEntity)character).Name, mailId);
		if (storedMail == null)
		{
			failureReason = "Mail not found.";
			return false;
		}
		storedMail.IsRead = true;
		int num = ((storedMail.Credits > 0) ? storedMail.Credits : 0);
		int num2 = ((storedMail.Credits < 0) ? (-storedMail.Credits) : 0);
		bool flag = storedMail.AcgLow != 0 || storedMail.AcgHigh != 0;
		if (num2 > 0)
		{
			if (!flag)
			{
				failureReason = "COD mail has no item to claim.";
				return false;
			}
			int num3 = CashStatRules.Clamp(((IStats)character).Stats[(StatIds)61].BaseValue);
			if (num3 < num2)
			{
				failureReason = string.Format(CultureInfo.InvariantCulture, "Not enough credits for COD. Need {0}.", num2);
				return false;
			}
		}
		Item val = null;
		if (flag)
		{
			int num4 = ((storedMail.AcgLow != 0) ? storedMail.AcgLow : storedMail.AcgHigh);
			int num5 = ((storedMail.AcgHigh != 0) ? storedMail.AcgHigh : storedMail.AcgLow);
			if (!ItemLoader.ItemList.ContainsKey(num4) || !ItemLoader.ItemList.ContainsKey(num5))
			{
				failureReason = string.Format(CultureInfo.InvariantCulture, "Attached item template {0}/{1} is missing from item data.", num4, num5);
				return false;
			}
			try
			{
				val = new Item(Math.Max(1, storedMail.AcgLevel), num4, num5);
				int num6 = ((storedMail.AcgMultipleCount <= 0) ? 1 : storedMail.AcgMultipleCount);
				if (num6 > 10000)
				{
					num6 = 1;
				}
				val.MultipleCount = Math.Max(1, num6);
			}
			catch (Exception)
			{
				failureReason = "Attached item template is invalid.";
				return false;
			}
		}
		if (num2 > 0)
		{
			int num7 = CashStatRules.Clamp(((IStats)character).Stats[(StatIds)61].BaseValue);
			int num8 = CashStatRules.Clamp((long)num7 - (long)num2);
			((IStats)character).Stats[(StatIds)61].Set((uint)num8, false);
			BaseMessageHandler<StatMessage, StatMessageHandler>.Default.SendSingle(character, 61, (uint)num8);
		}
		else if (num > 0)
		{
			int num9 = CashStatRules.Clamp(((IStats)character).Stats[(StatIds)61].BaseValue);
			int num10 = CashStatRules.Clamp((long)num9 + (long)num);
			((IStats)character).Stats[(StatIds)61].Set((uint)num10, false);
			BaseMessageHandler<StatMessage, StatMessageHandler>.Default.SendSingle(character, 61, (uint)num10);
			storedMail.Credits = 0;
		}
		if (val != null)
		{
			QuestRewardInventoryGrantResult questRewardInventoryGrantResult = InventoryContainerRuntimeService.Default.TryGrantQuestRewardItem(character, val);
			if (questRewardInventoryGrantResult.Status != 0)
			{
				if (num2 > 0)
				{
					int num11 = CashStatRules.Clamp(((IStats)character).Stats[(StatIds)61].BaseValue);
					int num12 = CashStatRules.Clamp((long)num11 + (long)num2);
					((IStats)character).Stats[(StatIds)61].Set((uint)num12, false);
					BaseMessageHandler<StatMessage, StatMessageHandler>.Default.SendSingle(character, 61, (uint)num12);
				}
				else if (num > 0)
				{
					int num13 = CashStatRules.Clamp(((IStats)character).Stats[(StatIds)61].BaseValue);
					int num14 = CashStatRules.Clamp((long)num13 - (long)num);
					((IStats)character).Stats[(StatIds)61].Set((uint)num14, false);
					BaseMessageHandler<StatMessage, StatMessageHandler>.Default.SendSingle(character, 61, (uint)num14);
					storedMail.Credits = num;
				}
				failureReason = "Not enough inventory space for the attached item.";
				if (questRewardInventoryGrantResult.Status == QuestRewardInventoryGrantStatus.PersistFailed || questRewardInventoryGrantResult.Status == QuestRewardInventoryGrantStatus.PersistReturnedFalse)
				{
					failureReason = "Attached item could not be saved to inventory.";
				}
				return false;
			}
			BaseMessageHandler<AddTemplateMessage, AddTemplateMessageHandler>.Default.Send(character, val);
			storedMail.AcgLow = 0;
			storedMail.AcgHigh = 0;
			storedMail.AcgLevel = 0;
			storedMail.AcgMultipleCount = 0;
			storedMail.ItemField1 = 0;
			storedMail.ItemField2 = 0;
		}
		if (num2 > 0)
		{
			PayCodToSender(storedMail.SenderName, num2);
			storedMail.Credits = 0;
		}
		updatedDetail = ToListEntry(storedMail, summary: false);
		return true;
	}

	public static bool TryGetMailFlagsUpdate(string characterName, ulong mailId, out int flags)
	{
		flags = 0;
		StoredMail storedMail = FindMail(characterName, mailId);
		if (storedMail == null)
		{
			return false;
		}
		flags = ComputeMailFlags(storedMail);
		return true;
	}

	public static bool TryDeleteMail(string characterName, ulong mailId, out string failureReason)
	{
		failureReason = null;
		if (string.IsNullOrEmpty(characterName))
		{
			failureReason = "Delete failed.";
			return false;
		}
		StoredMail storedMail = FindMail(characterName, mailId);
		if (storedMail == null)
		{
			failureReason = "Mail not found.";
			return false;
		}
		bool flag = storedMail.AcgLow != 0 || storedMail.AcgHigh != 0;
		bool flag2 = storedMail.Credits != 0;
		if (flag || flag2)
		{
			failureReason = "Cannot delete mail while it still has item or credit attachments.";
			return false;
		}
		if (!RemoveMail(characterName, mailId))
		{
			failureReason = "Mail not found.";
			return false;
		}
		return true;
	}

	public static bool TryReturnToSender(ICharacter character, ulong mailId, out string failureReason)
	{
		//IL_01c1: Unknown result type (might be due to invalid IL or missing references)
		//IL_01c6: Unknown result type (might be due to invalid IL or missing references)
		//IL_0262: Unknown result type (might be due to invalid IL or missing references)
		//IL_0267: Unknown result type (might be due to invalid IL or missing references)
		failureReason = null;
		if (character == null || string.IsNullOrEmpty(((INamedEntity)character).Name))
		{
			failureReason = "Return to sender failed.";
			return false;
		}
		if (mailId == 0)
		{
			failureReason = "No mail selected.";
			return false;
		}
		StoredMail storedMail = FindMail(((INamedEntity)character).Name, mailId);
		if (storedMail == null)
		{
			failureReason = "Mail not found.";
			return false;
		}
		string text = (storedMail.SenderName ?? string.Empty).Trim();
		if (text.Length == 0)
		{
			if (!RemoveMail(((INamedEntity)character).Name, mailId))
			{
				failureReason = "Mail not found.";
				return false;
			}
			SyncUnreadMailEnvelope(character);
			failureReason = "Original sender is unknown; mail removed.";
			return true;
		}
		if (string.Equals(text, ((INamedEntity)character).Name, StringComparison.OrdinalIgnoreCase))
		{
			failureReason = "Cannot return mail to yourself.";
			return false;
		}
		if (IsSystemMailSender(text))
		{
			if (!RemoveMail(((INamedEntity)character).Name, mailId))
			{
				failureReason = "Mail not found.";
				return false;
			}
			SyncUnreadMailEnvelope(character);
			failureReason = string.Format(CultureInfo.InvariantCulture, "Mail from \"{0}\" cannot be returned; removed from your mailbox.", text);
			return true;
		}
		string text2 = text;
		int recipientId = storedMail.SenderId;
		DBCharacter val = Dao<DBCharacter, CharacterDao>.Instance.GetByCharName(text);
		if (val == null && storedMail.SenderId != 0)
		{
			val = ((Dao<DBCharacter, CharacterDao>)(object)Dao<DBCharacter, CharacterDao>.Instance).Get(storedMail.SenderId);
		}
		Identity identity;
		if (val != null)
		{
			text2 = val.Name;
			recipientId = val.Id;
		}
		else
		{
			ICharacter val2 = FindOnlinePlayerByName(text);
			if (val2 != null)
			{
				text2 = ((INamedEntity)val2).Name;
				identity = ((IEntity)val2).Identity;
				recipientId = ((Identity)(ref identity)).Instance;
			}
		}
		if (!RemoveMail(((INamedEntity)character).Name, mailId))
		{
			failureReason = "Mail not found.";
			return false;
		}
		DateTime now = DateTime.Now;
		string text3 = storedMail.Subject ?? string.Empty;
		if (!text3.StartsWith("Returned:", StringComparison.OrdinalIgnoreCase))
		{
			text3 = "Returned: " + text3;
		}
		int mailId2 = AllocateMailId();
		StoredMail obj = new StoredMail
		{
			MailId = mailId2,
			SenderName = (((INamedEntity)character).Name ?? string.Empty)
		};
		identity = ((IEntity)character).Identity;
		obj.SenderId = ((Identity)(ref identity)).Instance;
		obj.RecipientName = text2;
		obj.RecipientId = recipientId;
		obj.Subject = text3;
		obj.Body = storedMail.Body ?? string.Empty;
		obj.ItemField1 = 0;
		obj.ItemField2 = 0;
		obj.AcgLow = storedMail.AcgLow;
		obj.AcgHigh = storedMail.AcgHigh;
		obj.AcgLevel = storedMail.AcgLevel;
		obj.AcgMultipleCount = storedMail.AcgMultipleCount;
		obj.Credits = storedMail.Credits;
		obj.ExpressFlag = 0;
		obj.SentAt = now;
		obj.SentDayNumber = ToMailTimeField(now);
		obj.IsRead = false;
		StoredMail item = obj;
		ConcurrentQueue<StoredMail> orAdd = ByRecipient.GetOrAdd(text2, (string _) => new ConcurrentQueue<StoredMail>());
		orAdd.Enqueue(item);
		NotifyRecipientEnvelope(text2);
		SyncUnreadMailEnvelope(character);
		return true;
	}

	private static bool IsSystemMailSender(string senderName)
	{
		if (string.IsNullOrEmpty(senderName))
		{
			return true;
		}
		return string.Equals(senderName, "Omni-Trade", StringComparison.OrdinalIgnoreCase) || string.Equals(senderName, "Market", StringComparison.OrdinalIgnoreCase) || string.Equals(senderName, "GMI", StringComparison.OrdinalIgnoreCase) || string.Equals(senderName, "System", StringComparison.OrdinalIgnoreCase);
	}

	private static bool RemoveMail(string characterName, ulong mailId)
	{
		if (!ByRecipient.TryGetValue(characterName, out var value))
		{
			return false;
		}
		StoredMail[] array = value.ToArray();
		bool flag = false;
		ConcurrentQueue<StoredMail> concurrentQueue = new ConcurrentQueue<StoredMail>();
		foreach (StoredMail storedMail in array)
		{
			if (!flag && storedMail != null && SameMailId(storedMail.MailId, mailId))
			{
				flag = true;
			}
			else if (storedMail != null)
			{
				concurrentQueue.Enqueue(storedMail);
			}
		}
		if (!flag)
		{
			return false;
		}
		ByRecipient[characterName] = concurrentQueue;
		return true;
	}

	private static void PayCodToSender(string senderName, int codAmount)
	{
		if (string.IsNullOrEmpty(senderName) || codAmount <= 0)
		{
			return;
		}
		ICharacter val = FindOnlinePlayerByName(senderName);
		if (val != null)
		{
			int num = CashStatRules.Clamp(((IStats)val).Stats[(StatIds)61].BaseValue);
			int num2 = CashStatRules.Clamp((long)num + (long)codAmount);
			((IStats)val).Stats[(StatIds)61].Set((uint)num2, false);
			BaseMessageHandler<StatMessage, StatMessageHandler>.Default.SendSingle(val, 61, (uint)num2);
			BaseMessageHandler<ChatTextMessage, ChatTextMessageHandler>.Default.Send(val, string.Format(CultureInfo.InvariantCulture, "You received {0} credits COD payment from mail.", codAmount), 0, 0);
			return;
		}
		ConcurrentQueue<StoredMail> orAdd = ByRecipient.GetOrAdd(senderName, (string _) => new ConcurrentQueue<StoredMail>());
		orAdd.Enqueue(new StoredMail
		{
			MailId = AllocateMailId(),
			SenderName = "Mail Terminal",
			SenderId = 0,
			RecipientName = senderName,
			RecipientId = 0,
			Subject = "COD payment",
			Body = string.Format(CultureInfo.InvariantCulture, "COD payment of {0} credits.", codAmount),
			Credits = codAmount,
			ExpressFlag = 0,
			SentAt = DateTime.Now,
			SentDayNumber = ToMailTimeField(DateTime.Now),
			IsRead = false
		});
	}

	public static void SyncUnreadMailEnvelope(ICharacter character)
	{
		if (character != null && !string.IsNullOrEmpty(((INamedEntity)character).Name))
		{
			int val = CountUnread(((INamedEntity)character).Name);
			uint num = (uint)Math.Max(0, val);
			((IStats)character).Stats[(StatIds)649].SetBaseValue(num);
			((IStats)character).Stats[(StatIds)649].Changed = true;
			BaseMessageHandler<StatMessage, StatMessageHandler>.Default.SendSingle(character, 649, num);
			((IStats)character).Stats[(StatIds)649].Changed = false;
		}
	}

	private static int CountUnread(string characterName)
	{
		return PeekMailbox(characterName).Count((StoredMail m) => m != null && !m.IsRead);
	}

	private static StoredMail FindMail(string characterName, ulong mailId)
	{
		IList<StoredMail> list = PeekMailbox(characterName);
		for (int i = 0; i < list.Count; i++)
		{
			if (list[i] != null && SameMailId(list[i].MailId, mailId))
			{
				return list[i];
			}
		}
		return null;
	}

	private static bool SameMailId(int storedMailId, ulong requestedMailId)
	{
		return storedMailId == (int)requestedMailId;
	}

	private static bool TryTakeAttachedItemFromSender(ICharacter sender, int containerType, int placement, out IItem item, out int resolvedContainer, out int resolvedPlacement, out string failureReason)
	{
		item = null;
		resolvedContainer = containerType;
		resolvedPlacement = placement;
		failureReason = null;
		if (sender == null || ((IItemContainer)sender).BaseInventory == null)
		{
			failureReason = "Attached item slot is invalid.";
			return false;
		}
		if (containerType != 104)
		{
			failureReason = "Mail items must be attached from Inventory.";
			return false;
		}
		if (!TryGetPage(((IItemContainer)sender).BaseInventory, 104, out var page) || page == null)
		{
			failureReason = string.Format(CultureInfo.InvariantCulture, "Attached item slot is invalid. ({0}/{1})", containerType, placement);
			return false;
		}
		if (!TryFindInventorySlot(page, placement, out var contentKey, out item) || item == null)
		{
			failureReason = string.Format(CultureInfo.InvariantCulture, "Attached item slot is invalid. ({0}/{1})", containerType, placement);
			return false;
		}
		resolvedContainer = 104;
		resolvedPlacement = ((contentKey >= page.FirstSlotNumber) ? contentKey : (page.FirstSlotNumber + contentKey));
		if (IsMailForbiddenContainer(item))
		{
			failureReason = "You can not send container items through the mail system.";
			return false;
		}
		if (IsMailNoDrop(item))
		{
			failureReason = "You can not send nodrop items through the mail system.";
			return false;
		}
		try
		{
			((IItemContainer)sender).BaseInventory.RemoveItem(resolvedContainer, contentKey);
			BaseMessageHandler<CharacterActionMessage, CharacterActionMessageHandler>.Default.SendDeleteItem(sender, resolvedContainer, resolvedPlacement);
		}
		catch (Exception)
		{
			failureReason = string.Format(CultureInfo.InvariantCulture, "Failed to remove attached item from inventory. ({0}/{1})", resolvedContainer, resolvedPlacement);
			return false;
		}
		((IItemContainer)sender).BaseInventory.Write();
		return true;
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
		foreach (KeyValuePair<int, IItem> item2 in page.List())
		{
			if (item2.Value != null)
			{
				int key = item2.Key;
				if (key == placement || key == placement - page.FirstSlotNumber || key + page.FirstSlotNumber == placement)
				{
					contentKey = key;
					item = item2.Value;
					return true;
				}
			}
		}
		return false;
	}

	private static bool IsMailForbiddenContainer(IItem item)
	{
		return InventoryItemRules.IsMailForbiddenContainerItem(item);
	}

	private static bool IsMailNoDrop(IItem item)
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

	private static int ResolveAttachedStackCount(IItem item)
	{
		if (item == null)
		{
			return 1;
		}
		int multipleCount = item.MultipleCount;
		if (multipleCount <= 0 || multipleCount > 10000)
		{
			return 1;
		}
		return multipleCount;
	}

	private static bool TryGetPage(IInventoryPages inventory, int containerType, out IInventoryPage page)
	{
		page = null;
		if (inventory == null || inventory.Pages == null || !inventory.Pages.ContainsKey(containerType))
		{
			return false;
		}
		page = inventory.Pages[containerType];
		return page != null;
	}

	private static MailListEntry ToListEntry(StoredMail mail, bool summary)
	{
		//IL_001c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0021: Unknown result type (might be due to invalid IL or missing references)
		//IL_002f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0037: Unknown result type (might be due to invalid IL or missing references)
		//IL_004d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0063: Unknown result type (might be due to invalid IL or missing references)
		//IL_006b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0073: Unknown result type (might be due to invalid IL or missing references)
		//IL_007b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0083: Unknown result type (might be due to invalid IL or missing references)
		//IL_0090: Unknown result type (might be due to invalid IL or missing references)
		//IL_009d: Unknown result type (might be due to invalid IL or missing references)
		//IL_00aa: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b7: Unknown result type (might be due to invalid IL or missing references)
		//IL_00d0: Unknown result type (might be due to invalid IL or missing references)
		//IL_00e7: Expected O, but got Unknown
		int num = ToMailUnixSeconds(mail.SentAt);
		int codField = num + 172800;
		int flagsField = ComputeMailFlags(mail);
		return new MailListEntry
		{
			MailId = (uint)mail.MailId,
			TimeField = 0,
			From = (mail.SenderName ?? string.Empty),
			Subject = (mail.Subject ?? string.Empty),
			CreditsField = num,
			CodField = codField,
			FlagsField = flagsField,
			IsSummary = summary,
			ExtendedField64 = mail.Credits,
			AcgLow = mail.AcgLow,
			AcgHigh = mail.AcgHigh,
			AcgLevel = mail.AcgLevel,
			ExtendedField74 = ((mail.AcgMultipleCount > 0) ? mail.AcgMultipleCount : 0),
			Body = (mail.Body ?? string.Empty)
		};
	}

	private static int ComputeMailFlags(StoredMail mail)
	{
		if (mail == null)
		{
			return 124;
		}
		int num = 124;
		if (mail.IsRead)
		{
			num |= 1;
		}
		bool flag = mail.AcgLow != 0 || mail.AcgHigh != 0;
		bool flag2 = mail.Credits != 0;
		if (!flag && !flag2)
		{
			num |= 2;
		}
		return num;
	}

	private static int ToMailUnixSeconds(DateTime localOrUnspecified)
	{
		DateTime dateTime = ((localOrUnspecified.Kind == DateTimeKind.Utc) ? localOrUnspecified : ((localOrUnspecified.Kind != DateTimeKind.Local) ? DateTime.SpecifyKind(localOrUnspecified, DateTimeKind.Local).ToUniversalTime() : localOrUnspecified.ToUniversalTime()));
		if (dateTime.Year < 1970)
		{
			dateTime = DateTime.UtcNow;
		}
		long num = (long)(dateTime - new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc)).TotalSeconds;
		if (num < 0)
		{
			return 0;
		}
		if (num > int.MaxValue)
		{
			return int.MaxValue;
		}
		return (int)num;
	}

	private static int ToMailTimeField(DateTime localOrUnspecified)
	{
		DateTime dateTime = localOrUnspecified;
		if (dateTime.Kind == DateTimeKind.Utc)
		{
			dateTime = dateTime.ToLocalTime();
		}
		if (dateTime.Year < 1970)
		{
			dateTime = DateTime.Now;
		}
		DateTime date = dateTime.Date;
		DateTime dateTime2 = new DateTime(1970, 1, 1);
		int num = (int)(date - dateTime2).TotalDays;
		return (num >= 0) ? num : 0;
	}

	private static void ApplyComputerLiteracySendLock(Character character)
	{
		character.LockSkill(161, 30);
		BaseMessageHandler<CharacterActionMessage, CharacterActionMessageHandler>.Default.SendSkillUnavailable((ICharacter)(object)character, 161, 30);
		int delayMs = 30000;
		ThreadPool.QueueUserWorkItem(delegate
		{
			Thread.Sleep(delayMs);
			if (((Dynel)character).Controller != null && ((Dynel)character).Controller.Client != null && character.GetSkillLockRemainingSeconds(161) <= 0)
			{
				BaseMessageHandler<CharacterActionMessage, CharacterActionMessageHandler>.Default.SendSkillAvailable((ICharacter)(object)character, 161);
			}
		});
	}

	public static bool TryEnqueueGmiDelivery(string recipientName, int credits, int acgLow, int acgHigh, int acgLevel, int acgMultipleCount, string subject, string body, out string failureReason)
	{
		return TryEnqueueGmiDelivery(recipientName, credits, acgLow, acgHigh, acgLevel, acgMultipleCount, subject, body, "Omni-Trade", out failureReason);
	}

	public static bool TryEnqueueGmiDelivery(string recipientName, int credits, int acgLow, int acgHigh, int acgLevel, int acgMultipleCount, string subject, string body, string senderName, out string failureReason)
	{
		failureReason = null;
		recipientName = (recipientName ?? string.Empty).Trim();
		if (recipientName.Length == 0)
		{
			failureReason = "GMI mail needs a recipient.";
			return false;
		}
		bool flag = credits > 0;
		bool flag2 = acgLow != 0 || acgHigh != 0;
		if (!flag && !flag2)
		{
			failureReason = "GMI delivery is empty.";
			return false;
		}
		if (string.IsNullOrEmpty(senderName))
		{
			senderName = "Omni-Trade";
		}
		DateTime now = DateTime.Now;
		StoredMail item = new StoredMail
		{
			MailId = AllocateMailId(),
			SenderName = senderName,
			SenderId = 0,
			RecipientName = recipientName,
			RecipientId = 0,
			Subject = (string.IsNullOrEmpty(subject) ? "GMI Delivery" : subject),
			Body = (string.IsNullOrEmpty(body) ? "Delivery from the Omni-Trade Global Market Interface." : body),
			Credits = (flag ? credits : 0),
			AcgLow = (flag2 ? ((acgLow != 0) ? acgLow : acgHigh) : 0),
			AcgHigh = (flag2 ? ((acgHigh != 0) ? acgHigh : acgLow) : 0),
			AcgLevel = (flag2 ? Math.Max(1, acgLevel) : 0),
			AcgMultipleCount = (flag2 ? Math.Max(1, acgMultipleCount) : 0),
			ExpressFlag = 0,
			SentAt = now,
			SentDayNumber = ToMailTimeField(now),
			IsRead = false
		};
		ConcurrentQueue<StoredMail> orAdd = ByRecipient.GetOrAdd(recipientName, (string _) => new ConcurrentQueue<StoredMail>());
		orAdd.Enqueue(item);
		NotifyRecipientEnvelope(recipientName);
		return true;
	}

	private static void NotifyRecipientEnvelope(string recipientName)
	{
		ICharacter val = FindOnlinePlayerByName(recipientName);
		if (val != null)
		{
			SyncUnreadMailEnvelope(val);
		}
	}

	private static ICharacter FindOnlinePlayerByName(string name)
	{
		if (string.IsNullOrWhiteSpace(name))
		{
			return null;
		}
		return Pool.Instance.GetAll<ICharacter>(50000).FirstOrDefault((ICharacter x) => x != null && ((IDynel)x).Controller is PlayerController && string.Equals(((INamedEntity)x).Name, name, StringComparison.OrdinalIgnoreCase));
	}
}
