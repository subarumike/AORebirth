#region License

// Copyright (c) 2005-2014, CellAO Team
// All rights reserved.

#endregion

namespace ZoneEngine.Core.Mail
{
    #region Usings ...

    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Linq;
    using System.Threading;

    using AORebirth.Core.Entities;
    using AORebirth.Core.Inventory;
    using AORebirth.Core.Items;
    using AORebirth.Database.Dao;
    using AORebirth.Database.Entities;
    using AORebirth.Enums;
    using AORebirth.ObjectManager;

    using SmokeLounge.AOtomation.Messaging.GameData;
    using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;

    using ZoneEngine.Core.Controllers;
    using ZoneEngine.Core.MessageHandlers;

    #endregion

    /// <summary>
    /// In-memory mail store. Postage capture 20260714-182726. Take All delivers credits/items.
    /// Envelope HUD uses UnreadMailCount (unread only).
    /// Wire TimeField is days since 1970-01-01 (GUI boost date → "YYYY-Mon-DD 00:00");
    /// inbox Expires column is Sent + 2 days. Server drops mail after MailRetentionDays.
    /// </summary>
    internal static class MailRuntimeService
    {
        public const int StandardPostageCredits = 2000;

        public const int ExpressPostageCredits = 200000;

        public const int ComputerLiteracyLockSeconds = 30;

        /// <summary>Live inbox Expires offset / retention (Sent + 2 days).</summary>
        public const int MailRetentionDays = 2;

        /// <summary>Live Feedback_MailNoNodrops body (GUI dialog, not chat).</summary>
        public const string FailureNoDrop =
            "You can not send nodrop items through the mail system.";

        /// <summary>
        /// Capture 20260715-100540 Feedback_MailNoChests dialog body
        /// (client-local when Item IdentityType.Container; not a server FormatFeedback).
        /// </summary>
        public const string FailureNoChests =
            "You can not send container items through the mail system.";

        private static readonly DateTime UnixEpochDate = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        private static int nextMailId = unchecked((int)0x014A11CC);

        private static readonly ConcurrentDictionary<string, ConcurrentQueue<StoredMail>> ByRecipient =
            new ConcurrentDictionary<string, ConcurrentQueue<StoredMail>>(StringComparer.OrdinalIgnoreCase);

        public static int AllocateMailId()
        {
            return Interlocked.Increment(ref nextMailId);
        }

        public static int PostageForExpressFlag(byte expressFlag)
        {
            return expressFlag != 0 ? ExpressPostageCredits : StandardPostageCredits;
        }

        public static bool TrySendMail(ICharacter sender, MailMessage message, out string failureReason, out int mailId)
        {
            failureReason = null;
            mailId = 0;

            if (sender == null || message == null)
            {
                failureReason = "Mail send failed.";
                return false;
            }

            Character senderCharacter = sender as Character;
            if (senderCharacter != null
                && senderCharacter.IsSkillLocked((int)StatIds.computerliteracy))
            {
                int remaining = senderCharacter.GetSkillLockRemainingSeconds((int)StatIds.computerliteracy);
                failureReason = string.Format(
                    CultureInfo.InvariantCulture,
                    "Computer Literacy is locked for {0} more second(s).",
                    Math.Max(1, remaining));
                return false;
            }

            string recipient = (message.Recipient ?? string.Empty).Trim();
            if (recipient.Length == 0)
            {
                failureReason = "Mail requires a recipient name.";
                return false;
            }

            DBCharacter recipientRow = CharacterDao.Instance.GetByCharName(recipient);
            if (recipientRow == null)
            {
                failureReason = string.Format(
                    CultureInfo.InvariantCulture,
                    "Unknown mail recipient \"{0}\".",
                    recipient);
                return false;
            }

            if (string.Equals(sender.Name, recipient, StringComparison.OrdinalIgnoreCase))
            {
                failureReason = "You cannot mail yourself.";
                return false;
            }

            int postage = PostageForExpressFlag(message.ExpressFlag);
            int attachCredits = message.Credits > 0 ? message.Credits : 0;
            long totalDebit = (long)postage + attachCredits;

            int cash = CashStatRules.Clamp(sender.Stats[StatIds.cash].BaseValue);
            if (cash < totalDebit)
            {
                failureReason = string.Format(
                    CultureInfo.InvariantCulture,
                    "Not enough credits. Express/Standard postage is {0}{1}.",
                    postage,
                    attachCredits > 0
                        ? string.Format(CultureInfo.InvariantCulture, " plus {0} attached.", attachCredits)
                        : ".");
                return false;
            }

            int acgLow = 0;
            int acgHigh = 0;
            int acgLevel = 0;
            int acgMultipleCount = 0;
            int resolvedContainer = message.ItemField1;
            int resolvedPlacement = message.ItemField2;
            bool hasItem = message.ItemField1 != 0 || message.ItemField2 != 0;
            if (hasItem)
            {
                IItem attachItem;
                string itemFailure;
                if (!TryTakeAttachedItemFromSender(
                        sender,
                        message.ItemField1,
                        message.ItemField2,
                        out attachItem,
                        out resolvedContainer,
                        out resolvedPlacement,
                        out itemFailure))
                {
                    failureReason = itemFailure ?? "Could not attach that item.";
                    return false;
                }

                acgLow = attachItem.LowID;
                acgHigh = attachItem.HighID;
                acgLevel = attachItem.Quality;
                acgMultipleCount = ResolveAttachedStackCount(attachItem);
            }

            // Debit only after the attach item (if any) is secured.
            int cashAfter = CashStatRules.Clamp((long)cash - totalDebit);
            sender.Stats[StatIds.cash].Set((uint)cashAfter);
            StatMessageHandler.Default.SendSingle(sender, (int)StatIds.cash, (uint)cashAfter);

            sender.Stats[StatIds.socialstatus].Set(4);
            StatMessageHandler.Default.SendSingle(sender, (int)StatIds.socialstatus, 4);

            // Positive Credits = cash attachment; negative = COD (paid by recipient on Take All — not yet).
            int storedCredits = message.Credits;

            mailId = AllocateMailId();
            var stored = new StoredMail
            {
                MailId = mailId,
                SenderName = sender.Name ?? string.Empty,
                SenderId = sender.Identity.Instance,
                RecipientName = recipientRow.Name,
                RecipientId = recipientRow.Id,
                Subject = message.Subject ?? string.Empty,
                Body = message.Body ?? string.Empty,
                ItemField1 = resolvedContainer,
                ItemField2 = resolvedPlacement,
                AcgLow = acgLow,
                AcgHigh = acgHigh,
                AcgLevel = acgLevel,
                AcgMultipleCount = acgMultipleCount,
                Credits = storedCredits,
                ExpressFlag = message.ExpressFlag,
                SentAt = DateTime.Now,
                IsRead = false
            };

            ConcurrentQueue<StoredMail> queue = ByRecipient.GetOrAdd(
                recipientRow.Name,
                _ => new ConcurrentQueue<StoredMail>());
            queue.Enqueue(stored);

            if (senderCharacter != null)
            {
                ApplyComputerLiteracySendLock(senderCharacter);
            }

            NotifyRecipientEnvelope(recipientRow.Name);
            return true;
        }

        public static IList<StoredMail> PeekMailbox(string characterName)
        {
            ConcurrentQueue<StoredMail> queue;
            if (string.IsNullOrEmpty(characterName) || !ByRecipient.TryGetValue(characterName, out queue))
            {
                return new List<StoredMail>();
            }

            PurgeExpiredMail(characterName, queue);
            return new List<StoredMail>(queue.ToArray());
        }

        public static IList<MailListEntry> BuildMailboxListEntries(string characterName)
        {
            IList<StoredMail> pending = PeekMailbox(characterName);
            var entries = new List<MailListEntry>();
            int limit = Math.Min(30, pending.Count);
            for (int i = 0; i < limit; i++)
            {
                entries.Add(ToListEntry(pending[i], summary: true));
            }

            return entries;
        }

        private static void PurgeExpiredMail(string characterName, ConcurrentQueue<StoredMail> queue)
        {
            DateTime cutoff = DateTime.Now.Date.AddDays(-MailRetentionDays);
            var kept = new List<StoredMail>();
            StoredMail mail;
            while (queue.TryDequeue(out mail))
            {
                if (mail != null && mail.SentAt.Date >= cutoff)
                {
                    kept.Add(mail);
                }
            }

            for (int i = 0; i < kept.Count; i++)
            {
                queue.Enqueue(kept[i]);
            }
        }

        public static bool TryBuildMailDetail(string characterName, ulong mailId, out MailListEntry detail)
        {
            detail = null;
            StoredMail mail = FindMail(characterName, mailId);
            if (mail == null)
            {
                return false;
            }

            mail.IsRead = true;
            detail = ToListEntry(mail, summary: false);
            return true;
        }

        public static bool TryTakeAll(ICharacter character, ulong mailId, out string failureReason, out MailListEntry updatedDetail)
        {
            failureReason = null;
            updatedDetail = null;

            if (character == null || string.IsNullOrEmpty(character.Name))
            {
                failureReason = "Take All failed.";
                return false;
            }

            StoredMail mail = FindMail(character.Name, mailId);
            if (mail == null)
            {
                failureReason = "Mail not found.";
                return false;
            }

            mail.IsRead = true;

            int claimCredits = mail.Credits > 0 ? mail.Credits : 0;
            int codAmount = mail.Credits < 0 ? -mail.Credits : 0;
            bool hasItem = mail.AcgLow != 0 || mail.AcgHigh != 0;

            // COD: recipient must afford payment before attachments are released.
            if (codAmount > 0)
            {
                if (!hasItem)
                {
                    failureReason = "COD mail has no item to claim.";
                    return false;
                }

                int cash = CashStatRules.Clamp(character.Stats[StatIds.cash].BaseValue);
                if (cash < codAmount)
                {
                    failureReason = string.Format(
                        CultureInfo.InvariantCulture,
                        "Not enough credits for COD. Need {0}.",
                        codAmount);
                    return false;
                }
            }

            Item claimItem = null;
            if (hasItem)
            {
                try
                {
                    int low = mail.AcgLow != 0 ? mail.AcgLow : mail.AcgHigh;
                    int high = mail.AcgHigh != 0 ? mail.AcgHigh : mail.AcgLow;
                    claimItem = new Item(Math.Max(1, mail.AcgLevel), low, high);
                    int stack = mail.AcgMultipleCount > 0 ? mail.AcgMultipleCount : 1;
                    if (stack > 10000)
                    {
                        stack = 1;
                    }

                    claimItem.MultipleCount = Math.Max(1, stack);
                }
                catch (Exception)
                {
                    failureReason = "Attached item template is invalid.";
                    return false;
                }
            }

            if (codAmount > 0)
            {
                int cash = CashStatRules.Clamp(character.Stats[StatIds.cash].BaseValue);
                int cashAfter = CashStatRules.Clamp((long)cash - codAmount);
                character.Stats[StatIds.cash].Set((uint)cashAfter);
                StatMessageHandler.Default.SendSingle(character, (int)StatIds.cash, (uint)cashAfter);
            }
            else if (claimCredits > 0)
            {
                int cash = CashStatRules.Clamp(character.Stats[StatIds.cash].BaseValue);
                int cashAfter = CashStatRules.Clamp((long)cash + claimCredits);
                character.Stats[StatIds.cash].Set((uint)cashAfter);
                StatMessageHandler.Default.SendSingle(character, (int)StatIds.cash, (uint)cashAfter);
                mail.Credits = 0;
            }

            if (claimItem != null)
            {
                QuestRewardInventoryGrantResult grant =
                    InventoryContainerRuntimeService.Default.TryGrantQuestRewardItem(character, claimItem);
                if (grant.Status != QuestRewardInventoryGrantStatus.Success)
                {
                    if (codAmount > 0)
                    {
                        int cash = CashStatRules.Clamp(character.Stats[StatIds.cash].BaseValue);
                        int restored = CashStatRules.Clamp((long)cash + codAmount);
                        character.Stats[StatIds.cash].Set((uint)restored);
                        StatMessageHandler.Default.SendSingle(character, (int)StatIds.cash, (uint)restored);
                    }
                    else if (claimCredits > 0)
                    {
                        // Gift credits were added above; reverse if item (only) failed with credits+item mail.
                        int cash = CashStatRules.Clamp(character.Stats[StatIds.cash].BaseValue);
                        int restored = CashStatRules.Clamp((long)cash - claimCredits);
                        character.Stats[StatIds.cash].Set((uint)restored);
                        StatMessageHandler.Default.SendSingle(character, (int)StatIds.cash, (uint)restored);
                        mail.Credits = claimCredits;
                    }

                    failureReason = "Not enough inventory space for the attached item.";
                    return false;
                }

                AddTemplateMessageHandler.Default.Send(character, claimItem);
                mail.AcgLow = 0;
                mail.AcgHigh = 0;
                mail.AcgLevel = 0;
                mail.AcgMultipleCount = 0;
                mail.ItemField1 = 0;
                mail.ItemField2 = 0;
            }

            if (codAmount > 0)
            {
                PayCodToSender(mail.SenderName, codAmount);
                mail.Credits = 0;
            }

            updatedDetail = ToListEntry(mail, summary: false);
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

            StoredMail mail = FindMail(characterName, mailId);
            if (mail == null)
            {
                failureReason = "Mail not found.";
                return false;
            }

            bool hasItem = mail.AcgLow != 0 || mail.AcgHigh != 0;
            bool hasCredits = mail.Credits != 0;
            if (hasItem || hasCredits)
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

        private static bool RemoveMail(string characterName, ulong mailId)
        {
            ConcurrentQueue<StoredMail> queue;
            if (!ByRecipient.TryGetValue(characterName, out queue))
            {
                return false;
            }

            StoredMail[] snapshot = queue.ToArray();
            bool found = false;
            var replacement = new ConcurrentQueue<StoredMail>();
            for (int i = 0; i < snapshot.Length; i++)
            {
                StoredMail entry = snapshot[i];
                if (!found && entry != null && (ulong)(uint)entry.MailId == mailId)
                {
                    found = true;
                    continue;
                }

                if (entry != null)
                {
                    replacement.Enqueue(entry);
                }
            }

            if (!found)
            {
                return false;
            }

            ByRecipient[characterName] = replacement;
            return true;
        }

        private static void PayCodToSender(string senderName, int codAmount)
        {
            if (string.IsNullOrEmpty(senderName) || codAmount <= 0)
            {
                return;
            }

            ICharacter online = FindOnlinePlayerByName(senderName);
            if (online != null)
            {
                int cash = CashStatRules.Clamp(online.Stats[StatIds.cash].BaseValue);
                int cashAfter = CashStatRules.Clamp((long)cash + codAmount);
                online.Stats[StatIds.cash].Set((uint)cashAfter);
                StatMessageHandler.Default.SendSingle(online, (int)StatIds.cash, (uint)cashAfter);
                ChatTextMessageHandler.Default.Send(
                    online,
                    string.Format(
                        CultureInfo.InvariantCulture,
                        "You received {0} credits COD payment from mail.",
                        codAmount));
                return;
            }

            // Offline sender: queue a gift-credit mail back under their name (in-memory).
            ConcurrentQueue<StoredMail> queue = ByRecipient.GetOrAdd(
                senderName,
                _ => new ConcurrentQueue<StoredMail>());
            queue.Enqueue(
                new StoredMail
                {
                    MailId = AllocateMailId(),
                    SenderName = "Mail Terminal",
                    SenderId = 0,
                    RecipientName = senderName,
                    RecipientId = 0,
                    Subject = "COD payment",
                    Body = string.Format(
                        CultureInfo.InvariantCulture,
                        "COD payment of {0} credits.",
                        codAmount),
                    Credits = codAmount,
                    ExpressFlag = 0,
                    SentAt = DateTime.Now,
                    IsRead = false
                });
        }

        public static void SyncUnreadMailEnvelope(ICharacter character)
        {
            if (character == null || string.IsNullOrEmpty(character.Name))
            {
                return;
            }

            int unread = CountUnread(character.Name);
            uint value = (uint)Math.Max(0, unread);
            character.Stats[StatIds.unreadmailcount].Set(value);
            StatMessageHandler.Default.SendSingle(character, (int)StatIds.unreadmailcount, value);
        }

        private static int CountUnread(string characterName)
        {
            return PeekMailbox(characterName).Count(m => m != null && !m.IsRead);
        }

        private static StoredMail FindMail(string characterName, ulong mailId)
        {
            IList<StoredMail> pending = PeekMailbox(characterName);
            for (int i = 0; i < pending.Count; i++)
            {
                if ((ulong)(uint)pending[i].MailId == mailId)
                {
                    return pending[i];
                }
            }

            return null;
        }

        private static bool TryTakeAttachedItemFromSender(
            ICharacter sender,
            int containerType,
            int placement,
            out IItem item,
            out int resolvedContainer,
            out int resolvedPlacement,
            out string failureReason)
        {
            item = null;
            resolvedContainer = containerType;
            resolvedPlacement = placement;
            failureReason = null;

            if (sender == null || sender.BaseInventory == null)
            {
                failureReason = "Attached item slot is invalid.";
                return false;
            }

            // Live GUI (Feedback_MailItemMustBeInBackpack): only IdentityType.Inventory (0x68).
            if (containerType != (int)IdentityType.Inventory)
            {
                failureReason = "Mail items must be attached from Inventory.";
                return false;
            }

            IInventoryPage inventoryPage;
            if (!TryGetPage(sender.BaseInventory, (int)IdentityType.Inventory, out inventoryPage)
                || inventoryPage == null)
            {
                failureReason = string.Format(
                    CultureInfo.InvariantCulture,
                    "Attached item slot is invalid. ({0}/{1})",
                    containerType,
                    placement);
                return false;
            }

            int contentKey;
            if (!TryFindInventorySlot(inventoryPage, placement, out contentKey, out item) || item == null)
            {
                failureReason = string.Format(
                    CultureInfo.InvariantCulture,
                    "Attached item slot is invalid. ({0}/{1})",
                    containerType,
                    placement);
                return false;
            }

            resolvedContainer = (int)IdentityType.Inventory;
            // Client delete uses absolute inventory slot numbers.
            resolvedPlacement = contentKey >= inventoryPage.FirstSlotNumber
                ? contentKey
                : inventoryPage.FirstSlotNumber + contentKey;

            if (IsMailForbiddenContainer(item))
            {
                failureReason = FailureNoChests;
                return false;
            }

            if (IsMailNoDrop(item))
            {
                failureReason = FailureNoDrop;
                return false;
            }

            try
            {
                sender.BaseInventory.RemoveItem(resolvedContainer, contentKey);
                CharacterActionMessageHandler.Default.SendDeleteItem(
                    sender,
                    resolvedContainer,
                    resolvedPlacement);
            }
            catch (Exception)
            {
                failureReason = string.Format(
                    CultureInfo.InvariantCulture,
                    "Failed to remove attached item from inventory. ({0}/{1})",
                    resolvedContainer,
                    resolvedPlacement);
                return false;
            }

            sender.BaseInventory.Write();
            return true;
        }

        /// <summary>
        /// Capture send Identity is Inventory(0x68)+absolute slot (e.g. 89). Also accept
        /// relative 0..MaxSlots-1 content keys used by some DB placements.
        /// </summary>
        private static bool TryFindInventorySlot(
            IInventoryPage page,
            int placement,
            out int contentKey,
            out IItem item)
        {
            contentKey = placement;
            item = null;
            if (page == null)
            {
                return false;
            }

            // 1) Absolute inventory slot (capture / client).
            if (page.ValidSlot(placement) && page[placement] != null)
            {
                contentKey = placement;
                item = page[placement];
                return true;
            }

            // 2) Relative index 0..MaxSlots-1 as absolute FirstSlot+index.
            if (placement >= 0 && placement < page.MaxSlots)
            {
                int absolute = page.FirstSlotNumber + placement;
                if (page.ValidSlot(absolute) && page[absolute] != null)
                {
                    contentKey = absolute;
                    item = page[absolute];
                    return true;
                }

                // Content keyed relatively (DB oddity).
                if (page[placement] != null)
                {
                    contentKey = placement;
                    item = page[placement];
                    return true;
                }
            }

            // 3) Absolute placement but Content keyed relatively.
            if (placement >= page.FirstSlotNumber
                && placement < page.FirstSlotNumber + page.MaxSlots)
            {
                int relative = placement - page.FirstSlotNumber;
                if (page[relative] != null)
                {
                    contentKey = relative;
                    item = page[relative];
                    return true;
                }
            }

            // 4) Scan page list for either absolute or relative match.
            foreach (KeyValuePair<int, IItem> entry in page.List())
            {
                if (entry.Value == null)
                {
                    continue;
                }

                int key = entry.Key;
                if (key == placement
                    || key == placement - page.FirstSlotNumber
                    || key + page.FirstSlotNumber == placement)
                {
                    contentKey = key;
                    item = entry.Value;
                    return true;
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

            ItemTemplate template;
            if (ItemLoader.ItemList.TryGetValue(item.LowID, out template) && template != null && template.IsNoDrop())
            {
                return true;
            }

            if (item.HighID != item.LowID
                && ItemLoader.ItemList.TryGetValue(item.HighID, out template)
                && template != null
                && template.IsNoDrop())
            {
                return true;
            }

            // ItemFlags.NoDrop bit on live template Stats[0].
            if ((item.Flags & (int)ItemFlags.NoDrop) != 0)
            {
                return true;
            }

            return false;
        }

        /// <summary>
        /// Prefer explicit stack; clamp template MaxEnergy masquerading as MultipleCount (GUI overlay).
        /// </summary>
        private static int ResolveAttachedStackCount(IItem item)
        {
            if (item == null)
            {
                return 1;
            }

            int count = item.MultipleCount;
            if (count <= 0 || count > 10000)
            {
                return 1;
            }

            return count;
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
            int credits = mail.Credits > 0 ? mail.Credits : 0;
            int cod = mail.Credits < 0 ? -mail.Credits : 0;

            // GUI MailMessageWindow +0x11c (= MailMessage +0x64 ExtendedField64) drives
            // Credits/COD TextInputs and ConfirmTakeMailItems: positive = gift credits,
            // negative = COD to pay. CreditsField/CodField still written for list wire.
            // FlagsField: bit0 marks read so inbox shows open envelope after open/Take All.
            // ExtendedField74: stack count for ItemSlotView (avoid template MaxEnergy overlay).
            return new MailListEntry
            {
                MailId = unchecked((ulong)(uint)mail.MailId),
                TimeField = ToMailTimeField(mail.SentAt),
                From = mail.SenderName ?? string.Empty,
                Subject = mail.Subject ?? string.Empty,
                CreditsField = credits,
                CodField = cod,
                FlagsField = mail.IsRead ? 1 : 0,
                IsSummary = summary,
                ExtendedField64 = mail.Credits,
                AcgLow = mail.AcgLow,
                AcgHigh = mail.AcgHigh,
                AcgLevel = mail.AcgLevel,
                ExtendedField74 = mail.AcgMultipleCount > 0 ? mail.AcgMultipleCount : 0,
                Body = mail.Body ?? string.Empty
            };
        }

        /// <summary>
        /// GUI inbox formats TimeField with boost::posix_time as a calendar day
        /// ("YYYY-Mon-DD 00:00"). Value is whole days since 1970-01-01; Expires = Sent+2.
        /// Unix seconds / FILETIME high dword are out of day-number range and render as
        /// 1970-Jan-01 00:00.
        /// </summary>
        private static int ToMailTimeField(DateTime localOrUnspecified)
        {
            DateTime local = localOrUnspecified;
            if (local.Kind == DateTimeKind.Utc)
            {
                local = local.ToLocalTime();
            }
            else if (local.Kind == DateTimeKind.Unspecified)
            {
                local = DateTime.SpecifyKind(local, DateTimeKind.Local);
            }

            if (local.Year < 1970)
            {
                local = DateTime.Now;
            }

            // Calendar date in local server time, encoded as UTC-midnight day count.
            var day = new DateTime(local.Year, local.Month, local.Day, 0, 0, 0, DateTimeKind.Utc);
            int days = (int)(day - UnixEpochDate).TotalDays;
            return days < 0 ? 0 : days;
        }

        private static void ApplyComputerLiteracySendLock(Character character)
        {
            character.LockSkill((int)StatIds.computerliteracy, ComputerLiteracyLockSeconds);
            CharacterActionMessageHandler.Default.SendSkillUnavailable(
                character,
                (int)StatIds.computerliteracy,
                ComputerLiteracyLockSeconds);

            int delayMs = ComputerLiteracyLockSeconds * 1000;
            ThreadPool.QueueUserWorkItem(
                _ =>
                {
                    Thread.Sleep(delayMs);
                    if (character.Controller == null || character.Controller.Client == null)
                    {
                        return;
                    }

                    if (character.GetSkillLockRemainingSeconds((int)StatIds.computerliteracy) > 0)
                    {
                        return;
                    }

                    CharacterActionMessageHandler.Default.SendSkillAvailable(
                        character,
                        (int)StatIds.computerliteracy);
                });
        }

        private static void NotifyRecipientEnvelope(string recipientName)
        {
            ICharacter online = FindOnlinePlayerByName(recipientName);
            if (online == null)
            {
                return;
            }

            SyncUnreadMailEnvelope(online);
        }

        private static ICharacter FindOnlinePlayerByName(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                return null;
            }

            return Pool.Instance.GetAll<ICharacter>((int)IdentityType.CanbeAffected)
                .FirstOrDefault(
                    x => x != null
                         && x.Controller is PlayerController
                         && string.Equals(x.Name, name, StringComparison.OrdinalIgnoreCase));
        }

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

            public bool IsRead { get; set; }
        }
    }
}
