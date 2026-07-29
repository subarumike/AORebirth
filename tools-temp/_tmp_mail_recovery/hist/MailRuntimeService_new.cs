#region License

// Copyright (c) 2005-2014, CellAO Team
// All rights reserved.

#endregion

namespace ZoneEngine.Core
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
    /// In-memory player mail store (no DB schema yet). Capture-backed send/open/take/delete.
    /// Postage evidence: captures/20260714-182726 Cash deltas (Standard 2000, Express 200000).
    /// Envelope: UnreadMailCount (stat 649). TimeField: calendar days since 1970-01-01; retain +2 days.
    /// </summary>
    internal static class MailRuntimeService
    {
        public const int StandardPostageCredits = 2000;

        public const int ExpressPostageCredits = 200000;

        public const string FailureNoDrop = "You can not send nodrop items through the mail system.";

        public const string FailureNoChests = "You can not send container items through the mail system.";

        private const int MailRetentionDays = 2;

        private const int ReadFlagBit = 1;

        private static int nextMailId = unchecked((int)0x014A11CC);

        private static readonly ConcurrentDictionary<string, ConcurrentDictionary<int, StoredMail>> ByRecipient =
            new ConcurrentDictionary<string, ConcurrentDictionary<int, StoredMail>>(StringComparer.OrdinalIgnoreCase);

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
                    "Not enough credits. Express/Standard postage is {0}{1}",
                    postage,
                    attachCredits > 0
                        ? string.Format(CultureInfo.InvariantCulture, " plus {0} attached.", attachCredits)
                        : ".");
                return false;
            }

            StoredAttachment attachment = null;
            bool hasAttachRequest = message.ItemField1 != 0 || message.ItemField2 != 0;
            if (hasAttachRequest)
            {
                if (!TryTakeInventoryAttachment(sender, message.ItemField1, message.ItemField2, out attachment, out failureReason))
                {
                    return false;
                }
            }

            int cashAfter = CashStatRules.Clamp((long)cash - totalDebit);
            sender.Stats[StatIds.cash].Set((uint)cashAfter);
            StatMessageHandler.Default.SendSingle(sender, (int)StatIds.cash, (uint)cashAfter);

            sender.Stats[StatIds.socialstatus].Set(4);
            StatMessageHandler.Default.SendSingle(sender, (int)StatIds.socialstatus, 4);

            if (hasAttachRequest)
            {
                InventoryUpdateMessageHandler.Default.Send(
                    sender,
                    sender.BaseInventory.Pages[(int)IdentityType.Inventory]);
            }

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
                Credits = message.Credits,
                ExpressFlag = message.ExpressFlag,
                SentLocalDate = DateTime.Now.Date,
                Attachment = attachment,
                IsRead = false
            };

            ConcurrentDictionary<int, StoredMail> box = ByRecipient.GetOrAdd(
                recipientRow.Name,
                _ => new ConcurrentDictionary<int, StoredMail>());
            box[mailId] = stored;

            NotifyRecipientEnvelope(recipientRow.Name);
            return true;
        }

        public static IList<StoredMail> PeekMailbox(string characterName)
        {
            ConcurrentDictionary<int, StoredMail> box;
            if (string.IsNullOrEmpty(characterName) || !ByRecipient.TryGetValue(characterName, out box))
            {
                return new List<StoredMail>();
            }

            PurgeExpired(box);
            return box.Values.OrderBy(m => m.MailId).ToList();
        }

        public static bool TryGetMail(string characterName, ulong mailId, out StoredMail mail)
        {
            mail = null;
            ConcurrentDictionary<int, StoredMail> box;
            if (string.IsNullOrEmpty(characterName) || !ByRecipient.TryGetValue(characterName, out box))
            {
                return false;
            }

            PurgeExpired(box);
            return box.TryGetValue(unchecked((int)mailId), out mail);
        }

        public static IList<MailListEntry> BuildMailboxListEntries(string characterName)
        {
            IList<StoredMail> pending = PeekMailbox(characterName);
            var entries = new List<MailListEntry>();
            int limit = Math.Min(30, pending.Count);
            for (int i = 0; i < limit; i++)
            {
                entries.Add(ToSummaryEntry(pending[i]));
            }

            return entries;
        }

        public static MailListEntry BuildMailDetail(StoredMail mail)
        {
            if (mail == null)
            {
                return new MailListEntry { IsSummary = false };
            }

            mail.IsRead = true;
            MailListEntry entry = ToSummaryEntry(mail);
            entry.IsSummary = false;
            entry.Body = mail.Body ?? string.Empty;
            entry.ExtendedField64 = mail.Credits;
            entry.CreditsField = 0;
            entry.CodField = 0;

            if (mail.Attachment != null && mail.Attachment.LowId != 0)
            {
                entry.AcgLow = mail.Attachment.LowId;
                entry.AcgHigh = mail.Attachment.HighId;
                entry.AcgLevel = mail.Attachment.Quality;
                entry.ExtendedField74 = Math.Max(1, mail.Attachment.MultipleCount);
            }

            return entry;
        }

        public static bool TryTakeAll(ICharacter character, ulong mailId, out string failureReason)
        {
            failureReason = null;
            StoredMail mail;
            if (!TryGetMail(character.Name, mailId, out mail))
            {
                failureReason = "Mail not found.";
                return false;
            }

            bool hasItem = mail.Attachment != null && mail.Attachment.LowId != 0;
            bool hasGift = mail.Credits > 0;
            bool isCod = mail.Credits < 0;
            int codAmount = isCod ? -mail.Credits : 0;

            if (!hasItem && !hasGift && !isCod)
            {
                failureReason = "Nothing left to take.";
                return false;
            }

            if (isCod)
            {
                int cash = CashStatRules.Clamp(character.Stats[StatIds.cash].BaseValue);
                if (cash < codAmount)
                {
                    failureReason = string.Format(
                        CultureInfo.InvariantCulture,
                        "Not enough credits to pay C.O.D. ({0}).",
                        codAmount);
                    return false;
                }
            }

            if (hasItem)
            {
                Item granted = new Item(mail.Attachment.Quality, mail.Attachment.LowId, mail.Attachment.HighId);
                granted.MultipleCount = Math.Max(1, mail.Attachment.MultipleCount);
                if (character.BaseInventory.TryAdd(granted) != InventoryError.OK)
                {
                    failureReason = "Inventory is full.";
                    return false;
                }

                mail.Attachment = null;
                InventoryUpdateMessageHandler.Default.Send(
                    character,
                    character.BaseInventory.Pages[(int)IdentityType.Inventory]);
            }

            if (hasGift)
            {
                int cash = CashStatRules.Clamp(character.Stats[StatIds.cash].BaseValue);
                int after = CashStatRules.Clamp((long)cash + mail.Credits);
                character.Stats[StatIds.cash].Set((uint)after);
                StatMessageHandler.Default.SendSingle(character, (int)StatIds.cash, (uint)after);
                mail.Credits = 0;
            }
            else if (isCod)
            {
                int cash = CashStatRules.Clamp(character.Stats[StatIds.cash].BaseValue);
                int after = CashStatRules.Clamp((long)cash - codAmount);
                character.Stats[StatIds.cash].Set((uint)after);
                StatMessageHandler.Default.SendSingle(character, (int)StatIds.cash, (uint)after);
                PaySenderCod(mail.SenderName, mail.SenderId, codAmount, character.Name);
                mail.Credits = 0;
            }

            mail.IsRead = true;
            SyncUnreadMailEnvelope(character);
            return true;
        }

        public static bool TryDelete(ICharacter character, ulong mailId, out string failureReason)
        {
            failureReason = null;
            ConcurrentDictionary<int, StoredMail> box;
            if (string.IsNullOrEmpty(character.Name) || !ByRecipient.TryGetValue(character.Name, out box))
            {
                failureReason = "Mail not found.";
                return false;
            }

            StoredMail mail;
            if (!box.TryGetValue(unchecked((int)mailId), out mail))
            {
                failureReason = "Mail not found.";
                return false;
            }

            bool hasItem = mail.Attachment != null && mail.Attachment.LowId != 0;
            if (hasItem || mail.Credits != 0)
            {
                failureReason = "Remove attached items and credits before deleting.";
                return false;
            }

            StoredMail removed;
            box.TryRemove(unchecked((int)mailId), out removed);
            SyncUnreadMailEnvelope(character);
            return true;
        }

        public static void SyncUnreadMailEnvelope(ICharacter character)
        {
            if (character == null || string.IsNullOrEmpty(character.Name))
            {
                return;
            }

            int unread = PeekMailbox(character.Name).Count(m => !m.IsRead);
            uint value = (uint)Math.Max(0, unread);
            character.Stats[StatIds.unreadmailcount].Set(value);
            StatMessageHandler.Default.SendSingle(character, (int)StatIds.unreadmailcount, value);
        }

        private static void PaySenderCod(string senderName, int senderId, int amount, string payerName)
        {
            ICharacter online = FindOnlinePlayerByName(senderName);
            if (online != null)
            {
                int cash = CashStatRules.Clamp(online.Stats[StatIds.cash].BaseValue);
                int after = CashStatRules.Clamp((long)cash + amount);
                online.Stats[StatIds.cash].Set((uint)after);
                StatMessageHandler.Default.SendSingle(online, (int)StatIds.cash, (uint)after);
                return;
            }

            // Offline: credit mail so the sender can collect later.
            int mailId = AllocateMailId();
            var payment = new StoredMail
            {
                MailId = mailId,
                SenderName = payerName ?? "Mail System",
                SenderId = 0,
                RecipientName = senderName ?? string.Empty,
                RecipientId = senderId,
                Subject = "COD payment",
                Body = string.Format(
                    CultureInfo.InvariantCulture,
                    "C.O.D. payment of {0} credits.",
                    amount),
                Credits = amount,
                ExpressFlag = 0,
                SentLocalDate = DateTime.Now.Date,
                Attachment = null,
                IsRead = false
            };

            if (string.IsNullOrEmpty(payment.RecipientName))
            {
                return;
            }

            ConcurrentDictionary<int, StoredMail> box = ByRecipient.GetOrAdd(
                payment.RecipientName,
                _ => new ConcurrentDictionary<int, StoredMail>());
            box[mailId] = payment;
            NotifyRecipientEnvelope(payment.RecipientName);
        }

        private static bool TryTakeInventoryAttachment(
            ICharacter sender,
            int itemField1,
            int itemField2,
            out StoredAttachment attachment,
            out string failureReason)
        {
            attachment = null;
            failureReason = null;

            if (itemField1 != 0 && itemField1 != (int)IdentityType.Inventory)
            {
                failureReason = string.Format(
                    CultureInfo.InvariantCulture,
                    "Mail attach must use Inventory ({0}); got container/slot {1}/{2}.",
                    (int)IdentityType.Inventory,
                    itemField1,
                    itemField2);
                return false;
            }

            IInventoryPage page;
            if (!sender.BaseInventory.Pages.TryGetValue((int)IdentityType.Inventory, out page) || page == null)
            {
                failureReason = "Inventory page missing.";
                return false;
            }

            Item item = ResolveInventoryItem(sender, page, itemField2);
            if (item == null)
            {
                failureReason = string.Format(
                    CultureInfo.InvariantCulture,
                    "Attached item slot is invalid ({0}/{1}).",
                    (int)IdentityType.Inventory,
                    itemField2);
                return false;
            }

            if (IsNoDropItem(item))
            {
                failureReason = FailureNoDrop;
                return false;
            }

            if (InventoryItemRules.IsBackpackContainerItem(item))
            {
                failureReason = FailureNoChests;
                return false;
            }

            attachment = new StoredAttachment
            {
                LowId = item.LowID,
                HighId = item.HighID,
                Quality = item.Quality,
                MultipleCount = item.MultipleCount > 0 ? item.MultipleCount : 1
            };

            int slot = ResolveAbsoluteSlot(page, itemField2);
            page.Remove(slot);
            sender.BaseInventory.Write();
            return true;
        }

        private static Item ResolveInventoryItem(ICharacter sender, IInventoryPage page, int slotOrRelative)
        {
            try
            {
                Item direct = sender.BaseInventory.GetItemInContainer((int)IdentityType.Inventory, slotOrRelative);
                if (direct != null)
                {
                    return direct;
                }
            }
            catch
            {
            }

            int relative = slotOrRelative;
            if (relative >= page.FirstSlotNumber)
            {
                // already absolute; tried GetItemInContainer
            }
            else
            {
                int absolute = page.FirstSlotNumber + relative;
                try
                {
                    return sender.BaseInventory.GetItemInContainer((int)IdentityType.Inventory, absolute);
                }
                catch
                {
                }
            }

            // Content keys can be absolute Content instance values.
            try
            {
                return page[slotOrRelative] as Item;
            }
            catch
            {
                return null;
            }
        }

        private static int ResolveAbsoluteSlot(IInventoryPage page, int slotOrRelative)
        {
            if (slotOrRelative >= page.FirstSlotNumber
                && slotOrRelative <= page.FirstSlotNumber + page.MaxSlots)
            {
                return slotOrRelative;
            }

            return page.FirstSlotNumber + slotOrRelative;
        }

        private static bool IsNoDropItem(IItem item)
        {
            if (item == null)
            {
                return false;
            }

            ItemTemplate low;
            if (ItemLoader.ItemList.TryGetValue(item.LowID, out low) && low.IsNoDrop())
            {
                return true;
            }

            ItemTemplate high;
            if (ItemLoader.ItemList.TryGetValue(item.HighID, out high) && high.IsNoDrop())
            {
                return true;
            }

            return (item.GetAttribute(0) & (int)ItemFlags.NoDrop) != 0;
        }

        private static MailListEntry ToSummaryEntry(StoredMail mail)
        {
            int credits = mail.Credits > 0 ? mail.Credits : 0;
            int cod = mail.Credits < 0 ? -mail.Credits : 0;
            return new MailListEntry
            {
                MailId = unchecked((ulong)(uint)mail.MailId),
                TimeField = ToMailTimeField(mail.SentLocalDate),
                From = mail.SenderName ?? string.Empty,
                Subject = mail.Subject ?? string.Empty,
                CreditsField = credits,
                CodField = cod,
                FlagsField = mail.IsRead ? ReadFlagBit : 0,
                IsSummary = true,
                ExtendedField64 = mail.Credits
            };
        }

        private static int ToMailTimeField(DateTime localDate)
        {
            DateTime epoch = new DateTime(1970, 1, 1);
            DateTime day = localDate.Date;
            if (day < epoch)
            {
                return 0;
            }

            return (int)(day - epoch).TotalDays;
        }

        private static void PurgeExpired(ConcurrentDictionary<int, StoredMail> box)
        {
            DateTime cutoff = DateTime.Now.Date.AddDays(-MailRetentionDays);
            foreach (KeyValuePair<int, StoredMail> pair in box.ToArray())
            {
                if (pair.Value.SentLocalDate.Date < cutoff)
                {
                    StoredMail removed;
                    box.TryRemove(pair.Key, out removed);
                }
            }
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

            public int Credits { get; set; }

            public byte ExpressFlag { get; set; }

            public DateTime SentLocalDate { get; set; }

            public StoredAttachment Attachment { get; set; }

            public bool IsRead { get; set; }
        }

        internal sealed class StoredAttachment
        {
            public int LowId { get; set; }

            public int HighId { get; set; }

            public int Quality { get; set; }

            public int MultipleCount { get; set; }
        }
    }
}
