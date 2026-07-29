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
    /// In-memory player mail store (no DB schema yet). Capture-backed send/open reply.
    /// Postage evidence: captures/20260714-182726 Cash deltas (Standard 2000, Express 200000).
    /// Envelope: UnreadMailCount (stat 649) drives the client mail icon.
    /// </summary>
    internal static class MailRuntimeService
    {
        public const int StandardPostageCredits = 2000;

        public const int ExpressPostageCredits = 200000;

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

            int cashAfter = CashStatRules.Clamp((long)cash - totalDebit);
            sender.Stats[StatIds.cash].Set((uint)cashAfter);
            StatMessageHandler.Default.SendSingle(sender, (int)StatIds.cash, (uint)cashAfter);

            // Capture: successful send also pulses SocialStatus=4 (same as Insurance Terminal).
            sender.Stats[StatIds.socialstatus].Set(4);
            StatMessageHandler.Default.SendSingle(sender, (int)StatIds.socialstatus, 4);

            // COD (negative credits) and item attach fields are accepted/stored; item removal TBD
            // until inventory slot mapping for fields 104/89 is capture-verified end-to-end.

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
                ItemField1 = message.ItemField1,
                ItemField2 = message.ItemField2,
                Credits = message.Credits,
                ExpressFlag = message.ExpressFlag,
                SentUtc = DateTime.UtcNow
            };

            ConcurrentQueue<StoredMail> queue = ByRecipient.GetOrAdd(
                recipientRow.Name,
                _ => new ConcurrentQueue<StoredMail>());
            queue.Enqueue(stored);

            // Delivered to online recipient immediately (envelope via UnreadMailCount).
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

            return new List<StoredMail>(queue.ToArray());
        }

        /// <summary>
        /// Inbox rows for Mail action 0. UI caps at 30 (MailWindow.xml InboxSize).
        /// Summary rows only — full body/ACGItem needs RequestMailMessage (action 1 + id).
        /// </summary>
        public static IList<MailListEntry> BuildMailboxListEntries(string characterName)
        {
            IList<StoredMail> pending = PeekMailbox(characterName);
            var entries = new List<MailListEntry>();
            int limit = Math.Min(30, pending.Count);
            for (int i = 0; i < limit; i++)
            {
                StoredMail mail = pending[i];
                int credits = mail.Credits > 0 ? mail.Credits : 0;
                int cod = mail.Credits < 0 ? -mail.Credits : 0;
                int sentUnix = 0;
                DateTime epoch = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);
                if (mail.SentUtc > epoch)
                {
                    sentUnix = (int)Math.Max(0, (mail.SentUtc - epoch).TotalSeconds);
                }

                entries.Add(
                    new MailListEntry
                    {
                        MailId = unchecked((ulong)(uint)mail.MailId),
                        TimeField = sentUnix,
                        From = mail.SenderName ?? string.Empty,
                        Subject = mail.Subject ?? string.Empty,
                        CreditsField = credits,
                        CodField = cod,
                        FlagsField = mail.ExpressFlag,
                        IsSummary = true
                    });
            }

            return entries;
        }

        /// <summary>
        /// Sets UnreadMailCount to pending inbox size and pushes the stat so the envelope icon shows.
        /// </summary>
        public static void SyncUnreadMailEnvelope(ICharacter character)
        {
            if (character == null || string.IsNullOrEmpty(character.Name))
            {
                return;
            }

            int pending = PeekMailbox(character.Name).Count;
            uint value = (uint)Math.Max(0, pending);
            character.Stats[StatIds.unreadmailcount].Set(value);
            StatMessageHandler.Default.SendSingle(character, (int)StatIds.unreadmailcount, value);
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

            public int Credits { get; set; }

            public byte ExpressFlag { get; set; }

            public DateTime SentUtc { get; set; }
        }
    }
}
