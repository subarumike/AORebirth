namespace ZoneEngine_New.Core.Mail
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;

    using AORebirth.Enums;
    using AORebirth.Interfaces.Persistence.Characters;
    using AORebirth.Interfaces.Persistence.Mail;

    using SmokeLounge.AOtomation.Messaging.GameData;
    using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;

    using ZoneEngine_New.Core.Data;
    using ZoneEngine_New.Core.Entities;
    using ZoneEngine_New.Core.GameData;
    using ZoneEngine_New.Core.Inventory;
    using ZoneEngine_New.Core.Logging;
    using ZoneEngine_New.Core.Playfield;
    using ZoneEngine_New.Core.Trade;

    /// <summary>
    /// Mail Terminal gameplay. Tunables from <see cref="MailRulesCatalog"/>; durable rows via <see cref="IMailDao"/>.
    /// Capture authority: 20260714-182726 + 20260715 receive/datetime/flags;
    /// Return/flags/retention: Andromeda PF655 20260926-061753 (0x28/0x29/0x2B, COD SendAccepted).
    /// </summary>
    public sealed class MailService
    {
        readonly IMailDao _mail;
        readonly ICharacterDao _characters;
        readonly IGameData _gameData;
        readonly InventoryActionService _inventoryActions;
        readonly InventoryFlushService _flush;
        readonly IItemBuilder _items;
        readonly Lazy<PlayfieldManager> _playfields;
        readonly IZoneLogger _logger;

        public MailService(
            IMailDao mail,
            ICharacterDao characters,
            IGameData gameData,
            InventoryActionService inventoryActions,
            InventoryFlushService flush,
            IItemBuilder items,
            Lazy<PlayfieldManager> playfields,
            IZoneLogger logger)
        {
            _mail = mail ?? throw new ArgumentNullException(nameof(mail));
            _characters = characters ?? throw new ArgumentNullException(nameof(characters));
            _gameData = gameData ?? throw new ArgumentNullException(nameof(gameData));
            _inventoryActions = inventoryActions ?? throw new ArgumentNullException(nameof(inventoryActions));
            _flush = flush ?? throw new ArgumentNullException(nameof(flush));
            _items = items ?? throw new ArgumentNullException(nameof(items));
            _playfields = playfields ?? throw new ArgumentNullException(nameof(playfields));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        MailRulesCatalog Rules => _gameData.MailRules;

        public void Handle(Player player, MailMessage message)
        {
            ArgumentNullException.ThrowIfNull(player);
            ArgumentNullException.ThrowIfNull(message);

            _logger.Info(
                string.Format(
                    CultureInfo.InvariantCulture,
                    "Mail action={0}({1}) requestId={2} recipient={3} subject={4} credits={5} express={6}",
                    message.Action,
                    (int)message.Action,
                    message.RequestedMailId,
                    message.Recipient,
                    message.Subject,
                    message.Credits,
                    message.ExpressFlag));

            try
            {
                switch (message.Action)
                {
                    case MailAction.OpenOrRequest:
                        if (message.RequestedMailId == 0)
                            SendMailboxList(player);
                        else
                            SendMailDetail(player, message.RequestedMailId);
                        break;
                    case MailAction.SendMail:
                        HandleSendMail(player, message);
                        break;
                    case MailAction.TakeAll:
                        HandleTakeAll(player, message.RequestedMailId);
                        break;
                    case MailAction.Delete:
                        HandleDelete(player, message.RequestedMailId);
                        break;
                    case MailAction.ReturnToSender:
                        HandleReturnToSender(player, message.RequestedMailId);
                        break;
                    default:
                        Tell(player, string.Format(
                            CultureInfo.InvariantCulture,
                            "Mail action {0} is not implemented yet.",
                            (int)message.Action));
                        break;
                }
            }
            catch (Exception exception)
            {
                _logger.Error(exception, "Mail action failed.");
                Tell(player, "Mail storage is unavailable. Ask staff to apply character_mail.sql.");
            }
        }

        /// <summary>GMI / Market / system deliveries into a durable mailbox.</summary>
        public bool TryEnqueueSystemDelivery(
            string recipientName,
            int credits,
            int acgLow,
            int acgHigh,
            int acgLevel,
            int acgMultipleCount,
            string subject,
            string body,
            string? senderName,
            out string failureReason)
        {
            failureReason = string.Empty;
            recipientName = (recipientName ?? string.Empty).Trim();
            if (recipientName.Length == 0)
            {
                failureReason = "System mail needs a recipient.";
                return false;
            }

            bool hasCredits = credits != 0;
            bool hasItem = acgLow != 0 || acgHigh != 0;
            if (!hasCredits && !hasItem)
            {
                failureReason = "System delivery is empty.";
                return false;
            }

            CharacterDirectoryData? recipient = _characters.LoadByName(recipientName);
            if (recipient == null)
            {
                failureReason = string.Format(
                    CultureInfo.InvariantCulture,
                    "Unknown mail recipient \"{0}\".",
                    recipientName);
                return false;
            }

            string from = string.IsNullOrWhiteSpace(senderName)
                ? Rules.DefaultSystemSenderName
                : senderName.Trim();
            DateTime sent = DateTime.UtcNow;
            long mailId = _mail.Insert(new MailInsertData
            {
                SenderCharacterId = 0,
                SenderName = from,
                RecipientCharacterId = recipient.CharacterId,
                RecipientName = recipient.Name ?? recipientName,
                Subject = subject ?? string.Empty,
                Body = body ?? string.Empty,
                AcgLow = acgLow,
                AcgHigh = acgHigh,
                AcgLevel = Math.Max(1, acgLevel),
                AcgMultipleCount = acgMultipleCount > 0 ? acgMultipleCount : 0,
                Credits = credits,
                ExpressFlag = 0,
                SentAtUtc = sent,
                ExpiresAtUtc = sent.AddDays(Rules.RetentionDaysForCredits(credits))
            });

            NotifyRecipientEnvelope(recipient.CharacterId, recipient.Name ?? recipientName);
            _ = mailId;
            return true;
        }

        void HandleSendMail(Player player, MailMessage message)
        {
            if (!TrySendMail(player, message, out string failure, out int mailId))
            {
                SendMailFailureFeedback(player, failure);
                return;
            }

            player.Session?.Send(new MailMessage
            {
                Identity = player.Identity,
                Unknown = 0,
                Action = MailAction.SendAccepted,
                EchoAction = (short)MailAction.SendMail,
                Unknown1 = 0,
                MailId = mailId,
                Unknown2 = 0
            });

            string mode = message.ExpressFlag != 0 ? "Express" : "Standard";
            string attachNote = string.Empty;
            if (message.Credits > 0)
                attachNote = string.Format(CultureInfo.InvariantCulture, " Attached {0} credits.", message.Credits);
            else if (message.Credits < 0)
                attachNote = string.Format(CultureInfo.InvariantCulture, " COD {0} credits.", -message.Credits);
            if (message.ItemField1 != 0 || message.ItemField2 != 0)
                attachNote += " Item attached.";

            Tell(player, string.Format(
                CultureInfo.InvariantCulture,
                "Mail sent to {0} ({1}).{2}",
                message.Recipient,
                mode,
                attachNote));
        }

        bool TrySendMail(Player player, MailMessage message, out string failureReason, out int mailId)
        {
            failureReason = string.Empty;
            mailId = 0;

            if (Rules.FailureNoDrop.Length == 0)
            {
                failureReason = "Mail rules are not loaded.";
                return false;
            }

            if (_inventoryActions.TryGetSkillLockRemaining(player, (int)CharacterStat.ComputerLiteracy, out int remaining)
                && remaining > 0)
            {
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

            CharacterDirectoryData? recipientRow = _characters.LoadByName(recipient);
            if (recipientRow == null)
            {
                failureReason = string.Format(
                    CultureInfo.InvariantCulture,
                    "Unknown mail recipient \"{0}\".",
                    recipient);
                return false;
            }

            if (string.Equals(player.Name, recipient, StringComparison.OrdinalIgnoreCase)
                || recipientRow.CharacterId == player.Identity.Instance)
            {
                failureReason = "You cannot mail yourself.";
                return false;
            }

            int postage = Rules.PostageForExpressFlag(message.ExpressFlag);
            int attachCredits = message.Credits > 0 ? message.Credits : 0;
            long totalDebit = (long)postage + attachCredits;
            int cash = player.Stats.GetOrZero(CharacterStat.Cash);
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

            int acgLow = 0;
            int acgHigh = 0;
            int acgLevel = 0;
            int acgMultipleCount = 0;
            bool hasItem = message.ItemField1 != 0 || message.ItemField2 != 0;
            Identity itemSlot = default;
            Item? attachItem = null;

            if (hasItem)
            {
                if (!TryResolveInventoryAttach(player, message.ItemField1, message.ItemField2, out itemSlot, out attachItem, out failureReason))
                    return false;

                if (InventoryMoveService.IsBagItem(attachItem))
                {
                    failureReason = Rules.FailureNoChests;
                    return false;
                }

                if (TradeRules.IsNoDrop(attachItem))
                {
                    failureReason = Rules.FailureNoDrop;
                    return false;
                }

                acgLow = attachItem.LowId;
                acgHigh = attachItem.HighId;
                acgLevel = attachItem.Quality;
                acgMultipleCount = ResolveStack(attachItem);
            }

            int cashAfter = TradeRules.ClampCash((long)cash - totalDebit);
            var cashStat = new StatRecord { StatId = (int)CharacterStat.Cash, StatValue = cashAfter };

            if (hasItem && attachItem != null)
            {
                if (!_inventoryActions.TryRetireItem(player, itemSlot, attachItem.InstanceId, [cashStat]))
                {
                    failureReason = "Failed to remove attached item from inventory.";
                    return false;
                }

                player.Session?.Send(new CharacterActionMessage
                {
                    Identity = player.Identity,
                    Action = CharacterActionType.DeleteItem,
                    Target = itemSlot
                });
            }
            else if (!_inventoryActions.TryApplyStats(player, [cashStat]))
            {
                failureReason = "Failed to debit postage.";
                return false;
            }

            player.Stats.Set(CharacterStat.SocialStatus, 4, StatDetail.Base, dirty: true);
            player.FlushDirtyStats();
            player.Session?.Send(new StatMessage
            {
                Identity = player.Identity,
                Stats =
                [
                    new GameTuple<CharacterStat, uint> { Value1 = CharacterStat.SocialStatus, Value2 = 4 }
                ]
            });

            DateTime sent = DateTime.UtcNow;
            long assignedId = _mail.Insert(new MailInsertData
            {
                SenderCharacterId = player.Identity.Instance,
                SenderName = player.Name ?? string.Empty,
                RecipientCharacterId = recipientRow.CharacterId,
                RecipientName = recipientRow.Name ?? recipient,
                Subject = message.Subject ?? string.Empty,
                Body = message.Body ?? string.Empty,
                AcgLow = acgLow,
                AcgHigh = acgHigh,
                AcgLevel = acgLevel,
                AcgMultipleCount = acgMultipleCount,
                Credits = message.Credits,
                ExpressFlag = message.ExpressFlag,
                SentAtUtc = sent,
                ExpiresAtUtc = sent.AddDays(Rules.RetentionDaysForCredits(message.Credits))
            });

            mailId = unchecked((int)assignedId);
            _inventoryActions.TryLockSkill(player, (int)CharacterStat.ComputerLiteracy, Rules.ComputerLiteracyLockSeconds);
            NotifyRecipientEnvelope(recipientRow.CharacterId, recipientRow.Name ?? recipient);
            return true;
        }

        void HandleTakeAll(Player player, ulong requestedMailId)
        {
            if (!TryTakeAll(player, requestedMailId, out string failure, out MailListEntry? updated))
            {
                Tell(player, failure);
                return;
            }

            SyncUnreadMailEnvelope(player);
            player.Session?.Send(new MailMessage
            {
                Identity = player.Identity,
                Unknown = 0,
                Action = MailAction.MailDetail,
                Detail = updated
            });
            SendMailFlagsUpdate(player, requestedMailId);
            SendMailboxList(player);
            Tell(player, "Mail attachments taken.");
        }

        bool TryTakeAll(Player player, ulong requestedMailId, out string failureReason, out MailListEntry? updatedDetail)
        {
            failureReason = string.Empty;
            updatedDetail = null;
            if (!TryParseMailId(requestedMailId, out long mailId))
            {
                failureReason = "Mail not found.";
                return false;
            }

            MailRowData? mail = _mail.GetOwned(player.Identity.Instance, mailId);
            if (mail == null)
            {
                failureReason = "Mail not found.";
                return false;
            }

            int claimCredits = mail.Credits > 0 ? mail.Credits : 0;
            int codAmount = mail.Credits < 0 ? -mail.Credits : 0;
            bool hasItem = mail.AcgLow != 0 || mail.AcgHigh != 0;
            int cash = player.Stats.GetOrZero(CharacterStat.Cash);

            if (codAmount > 0)
            {
                if (!hasItem)
                {
                    failureReason = "COD mail has no item to claim.";
                    return false;
                }

                if (cash < codAmount)
                {
                    failureReason = string.Format(
                        CultureInfo.InvariantCulture,
                        "Not enough credits for COD. Need {0}.",
                        codAmount);
                    return false;
                }
            }

            Item? claimItem = null;
            int grantSlot = -1;
            if (hasItem)
            {
                int low = mail.AcgLow != 0 ? mail.AcgLow : mail.AcgHigh;
                int high = mail.AcgHigh != 0 ? mail.AcgHigh : mail.AcgLow;
                int stack = mail.AcgMultipleCount > 0 ? mail.AcgMultipleCount : 1;
                if (stack > 10000)
                    stack = 1;

                try
                {
                    claimItem = _items.CreateWithNewInstance(low, high, Math.Max(1, mail.AcgLevel), ItemSource.Other);
                    claimItem.StackCount = Math.Max(1, stack);
                }
                catch (Exception)
                {
                    failureReason = "Attached item template is invalid.";
                    return false;
                }

                if (!player.Inventory.IsHydrated || !player.Inventory.HasFreeInventorySlots(1))
                {
                    failureReason = "Not enough inventory space for the attached item.";
                    return false;
                }

                grantSlot = player.Inventory.Inventory.FindFreeSlot();
                if (grantSlot < 0)
                {
                    failureReason = "Not enough inventory space for the attached item.";
                    return false;
                }
            }

            int cashAfter = cash;
            if (codAmount > 0)
                cashAfter = TradeRules.ClampCash((long)cash - codAmount);
            else if (claimCredits > 0)
                cashAfter = TradeRules.ClampCash((long)cash + claimCredits);

            if (claimItem != null)
            {
                if (!player.Inventory.Inventory.Add(grantSlot, claimItem))
                {
                    failureReason = "Not enough inventory space for the attached item.";
                    return false;
                }

                player.Inventory.MarkDirty(claimItem, player.Inventory.Inventory, grantSlot);
                try
                {
                    _flush.HardFlush(player);
                }
                catch (Exception)
                {
                    player.Inventory.Inventory.Content.Remove(grantSlot);
                    failureReason = "Attached item could not be saved to inventory.";
                    return false;
                }

                player.Session?.Send(new AddTemplateMessage
                {
                    Identity = player.Identity,
                    LowId = claimItem.LowId,
                    HighId = claimItem.HighId,
                    Quality = claimItem.Quality,
                    Count = claimItem.StackCount
                });
            }

            if (cashAfter != cash)
            {
                var cashStat = new StatRecord { StatId = (int)CharacterStat.Cash, StatValue = cashAfter };
                if (!_inventoryActions.TryApplyStats(player, [cashStat]))
                {
                    failureReason = "Failed to update credits for mail attachments.";
                    return false;
                }
            }

            if (codAmount > 0)
                PayCodToSender(mail, codAmount);

            if (!_mail.ClearAttachments(player.Identity.Instance, mailId, markRead: true))
            {
                failureReason = "Mail not found.";
                return false;
            }

            mail.IsRead = true;
            mail.Credits = 0;
            mail.AcgLow = 0;
            mail.AcgHigh = 0;
            mail.AcgLevel = 0;
            mail.AcgMultipleCount = 0;
            updatedDetail = ToListEntry(mail, summary: false);
            return true;
        }

        void HandleDelete(Player player, ulong requestedMailId)
        {
            if (!TryParseMailId(requestedMailId, out long mailId))
            {
                Tell(player, "Delete failed.");
                return;
            }

            MailRowData? mail = _mail.GetOwned(player.Identity.Instance, mailId);
            if (mail == null)
            {
                Tell(player, "Mail not found.");
                return;
            }

            bool hasItem = mail.AcgLow != 0 || mail.AcgHigh != 0;
            bool hasCredits = mail.Credits != 0;
            if (hasItem || hasCredits)
            {
                Tell(player, "Cannot delete mail while it still has item or credit attachments.");
                return;
            }

            if (!_mail.TryDeleteEmpty(player.Identity.Instance, mailId))
            {
                Tell(player, "Mail not found.");
                return;
            }

            SyncUnreadMailEnvelope(player);
            SendMailboxList(player);
            Tell(player, "Mail deleted.");
        }

        void HandleReturnToSender(Player player, ulong requestedMailId)
        {
            if (!TryReturnToSender(player, requestedMailId, out string failure, out int acceptedMailId))
            {
                SendFormatFeedbackDialog(player, failure);
                Tell(player, failure);
                SendMailboxList(player);
                return;
            }

            // Capture 20260926-061753 COD return: SendAccepted EchoAction=ReturnToSender (7).
            player.Session?.Send(new MailMessage
            {
                Identity = player.Identity,
                Unknown = 0,
                Action = MailAction.SendAccepted,
                EchoAction = (short)MailAction.ReturnToSender,
                Unknown1 = 0,
                MailId = acceptedMailId,
                Unknown2 = 0
            });
            SyncUnreadMailEnvelope(player);

            if (!string.IsNullOrEmpty(failure))
            {
                SendFormatFeedbackDialog(player, failure);
                Tell(player, failure);
            }
        }

        bool TryReturnToSender(
            Player player,
            ulong requestedMailId,
            out string failureReason,
            out int acceptedMailId)
        {
            failureReason = string.Empty;
            acceptedMailId = 0;
            if (!TryParseMailId(requestedMailId, out long mailId) || mailId == 0)
            {
                failureReason = "No mail selected.";
                return false;
            }

            MailRowData? mail = _mail.GetOwned(player.Identity.Instance, mailId);
            if (mail == null)
            {
                failureReason = "Mail not found.";
                return false;
            }

            acceptedMailId = unchecked((int)(uint)mailId);

            string subject = mail.Subject ?? string.Empty;
            if (subject.StartsWith("Returned:", StringComparison.OrdinalIgnoreCase))
            {
                failureReason = Rules.FailureReturnNotEligible;
                return false;
            }

            if (!IsReturnEligible(player, mail))
            {
                failureReason = Rules.FailureReturnNotEligible;
                return false;
            }

            string originalSender = (mail.SenderName ?? string.Empty).Trim();
            if (originalSender.Length == 0)
            {
                if (!_mail.Delete(player.Identity.Instance, mailId))
                {
                    failureReason = "Mail not found.";
                    return false;
                }

                SyncUnreadMailEnvelope(player);
                failureReason = "Original sender is unknown; mail removed.";
                return true;
            }

            if (string.Equals(originalSender, player.Name, StringComparison.OrdinalIgnoreCase))
            {
                failureReason = "Cannot return mail to yourself.";
                return false;
            }

            if (Rules.IsSystemSender(originalSender))
            {
                if (!_mail.Delete(player.Identity.Instance, mailId))
                {
                    failureReason = "Mail not found.";
                    return false;
                }

                SyncUnreadMailEnvelope(player);
                failureReason = string.Format(
                    CultureInfo.InvariantCulture,
                    "Mail from \"{0}\" cannot be returned; removed from your mailbox.",
                    originalSender);
                return true;
            }

            string deliverToName = originalSender;
            int deliverToId = mail.SenderCharacterId;
            CharacterDirectoryData? senderRow = _characters.LoadByName(originalSender);
            if (senderRow == null && mail.SenderCharacterId != 0)
                senderRow = _characters.LoadById(mail.SenderCharacterId);
            if (senderRow != null)
            {
                deliverToName = senderRow.Name ?? deliverToName;
                deliverToId = senderRow.CharacterId;
            }
            else if (_playfields.Value.FindPlayerByName(originalSender, out Player? onlineSender) && onlineSender != null)
            {
                deliverToName = onlineSender.Name ?? deliverToName;
                deliverToId = onlineSender.Identity.Instance;
            }

            if (!_mail.Delete(player.Identity.Instance, mailId))
            {
                failureReason = "Mail not found.";
                return false;
            }

            subject = "Returned: " + subject;

            // Live: returned COD must not charge the original sender again — clear negative COD
            // so Take All is free. Positive gift credits (if any) stay attached.
            int returnedCredits = mail.Credits < 0 ? 0 : mail.Credits;
            DateTime sent = DateTime.UtcNow;
            _mail.Insert(new MailInsertData
            {
                SenderCharacterId = player.Identity.Instance,
                SenderName = player.Name ?? string.Empty,
                RecipientCharacterId = deliverToId,
                RecipientName = deliverToName,
                Subject = subject,
                Body = mail.Body ?? string.Empty,
                AcgLow = mail.AcgLow,
                AcgHigh = mail.AcgHigh,
                AcgLevel = mail.AcgLevel,
                AcgMultipleCount = mail.AcgMultipleCount,
                Credits = returnedCredits,
                ExpressFlag = 0,
                SentAtUtc = sent,
                ExpiresAtUtc = sent.AddDays(Rules.RetentionDaysForCredits(returnedCredits))
            });

            NotifyRecipientEnvelope(deliverToId, deliverToName);
            SyncUnreadMailEnvelope(player);
            return true;
        }

        /// <summary>
        /// Live: COD always returnable; otherwise unique item only when recipient already holds it.
        /// Capture 20260926-061753 — non-COD item/credit gifts reject with FailureReturnNotEligible.
        /// </summary>
        bool IsReturnEligible(Player player, MailRowData mail)
        {
            if (mail.Credits < 0)
                return true;

            bool hasItem = mail.AcgLow != 0 || mail.AcgHigh != 0;
            if (!hasItem)
                return false;

            int low = mail.AcgLow != 0 ? mail.AcgLow : mail.AcgHigh;
            int high = mail.AcgHigh != 0 ? mail.AcgHigh : mail.AcgLow;
            try
            {
                ItemTemplate template = _items.CreateTemplate(low, high, Math.Max(1, mail.AcgLevel));
                if ((template.Flags & (int)ItemFlags.Unique) == 0)
                    return false;
                return TradeRules.WouldDuplicateUnique(player, low, high);
            }
            catch (Exception)
            {
                return false;
            }
        }

        void SendMailboxList(Player player)
        {
            SyncUnreadMailEnvelope(player);
            IList<MailRowData> rows = _mail.ListInbox(player.Identity.Instance, Rules.MailboxListLimit);
            var entries = new List<MailListEntry>(rows.Count);
            for (int i = 0; i < rows.Count; i++)
                entries.Add(ToListEntry(rows[i], summary: true));

            player.Session?.Send(new MailMessage
            {
                Identity = player.Identity,
                Unknown = 0,
                Action = MailAction.MailboxList,
                Entries = entries
            });
        }

        void SendMailDetail(Player player, ulong requestedMailId)
        {
            if (!TryParseMailId(requestedMailId, out long mailId))
            {
                Tell(player, "Mail not found.");
                return;
            }

            // MarkRead may report 0 affected rows when already read (MySQL UseAffectedRows).
            _mail.MarkRead(player.Identity.Instance, mailId);
            MailRowData? mail = _mail.GetOwned(player.Identity.Instance, mailId);
            if (mail == null)
            {
                Tell(player, "Mail not found.");
                return;
            }

            mail.IsRead = true;
            SyncUnreadMailEnvelope(player);
            player.Session?.Send(new MailMessage
            {
                Identity = player.Identity,
                Unknown = 0,
                Action = MailAction.MailDetail,
                Detail = ToListEntry(mail, summary: false)
            });
            SendMailFlagsUpdate(player, requestedMailId);
            SendMailboxList(player);
        }

        void SendMailFlagsUpdate(Player player, ulong requestedMailId)
        {
            if (!TryParseMailId(requestedMailId, out long mailId))
                return;
            MailRowData? mail = _mail.GetOwned(player.Identity.Instance, mailId);
            if (mail == null)
                return;

            player.Session?.Send(new MailMessage
            {
                Identity = player.Identity,
                Unknown = 0,
                Action = MailAction.UpdateMailFlags,
                RequestedMailId = requestedMailId,
                MailFlagsUpdate = ComputeMailFlags(mail)
            });
        }

        /// <summary>
        /// Sets UnreadMailCount from the durable inbox and pushes the HUD envelope stat.
        /// Call on zone entry / CharInPlay so a stale DB unread value cannot keep the icon up.
        /// </summary>
        public void SyncUnreadMailEnvelope(Player player)
        {
            if (player == null)
                return;

            int unread = 0;
            try
            {
                unread = Math.Max(0, _mail.CountUnread(player.Identity.Instance));
            }
            catch (Exception exception)
            {
                _logger.Warn(
                    string.Format(
                        CultureInfo.InvariantCulture,
                        "Mail unread sync unavailable for character {0}: {1}",
                        player.Identity.Instance,
                        exception.Message));
            }

            player.Stats.Set(CharacterStat.UnreadMailCount, unread, StatDetail.Base, dirty: true);
            player.FlushDirtyStats();
            player.Session?.Send(new StatMessage
            {
                Identity = player.Identity,
                Stats =
                [
                    new GameTuple<CharacterStat, uint>
                    {
                        Value1 = CharacterStat.UnreadMailCount,
                        Value2 = (uint)unread
                    }
                ]
            });
        }

        /// <summary>
        /// Applies UnreadMailCount before FullCharacter so the envelope is correct on first paint
        /// even when the client never sends CharInPlay.
        /// </summary>
        public void ApplyUnreadMailCountForSpawn(Player player)
        {
            if (player == null)
                return;

            int unread = 0;
            try
            {
                unread = Math.Max(0, _mail.CountUnread(player.Identity.Instance));
            }
            catch (Exception)
            {
                unread = 0;
            }

            player.Stats.Set(CharacterStat.UnreadMailCount, unread, StatDetail.Base, dirty: false);
        }

        void NotifyRecipientEnvelope(int recipientCharacterId, string recipientName)
        {
            if (recipientCharacterId > 0 && _playfields.Value.FindPlayer(recipientCharacterId, out Player online))
            {
                SyncUnreadMailEnvelope(online);
                return;
            }

            if (_playfields.Value.FindPlayerByName(recipientName, out Player? byName) && byName != null)
                SyncUnreadMailEnvelope(byName);
        }

        void PayCodToSender(MailRowData mail, int codAmount)
        {
            if (codAmount <= 0)
                return;

            if (mail.SenderCharacterId > 0 && _playfields.Value.FindPlayer(mail.SenderCharacterId, out Player online))
            {
                int cash = online.Stats.GetOrZero(CharacterStat.Cash);
                int cashAfter = TradeRules.ClampCash((long)cash + codAmount);
                _inventoryActions.TryApplyStats(online, [new StatRecord { StatId = (int)CharacterStat.Cash, StatValue = cashAfter }]);
                Tell(online, string.Format(
                    CultureInfo.InvariantCulture,
                    "You received {0} credits COD payment from mail.",
                    codAmount));
                return;
            }

            if (_playfields.Value.FindPlayerByName(mail.SenderName, out Player? byName) && byName != null)
            {
                int cash = byName.Stats.GetOrZero(CharacterStat.Cash);
                int cashAfter = TradeRules.ClampCash((long)cash + codAmount);
                _inventoryActions.TryApplyStats(byName, [new StatRecord { StatId = (int)CharacterStat.Cash, StatValue = cashAfter }]);
                Tell(byName, string.Format(
                    CultureInfo.InvariantCulture,
                    "You received {0} credits COD payment from mail.",
                    codAmount));
                return;
            }

            CharacterDirectoryData? sender = !string.IsNullOrWhiteSpace(mail.SenderName)
                ? _characters.LoadByName(mail.SenderName)
                : (mail.SenderCharacterId != 0 ? _characters.LoadById(mail.SenderCharacterId) : null);
            if (sender == null)
                return;

            DateTime sent = DateTime.UtcNow;
            _mail.Insert(new MailInsertData
            {
                SenderCharacterId = 0,
                SenderName = Rules.CodPaymentSenderName,
                RecipientCharacterId = sender.CharacterId,
                RecipientName = sender.Name ?? mail.SenderName ?? string.Empty,
                Subject = Rules.CodPaymentSubject,
                Body = string.Format(
                    CultureInfo.InvariantCulture,
                    "COD payment of {0} credits.",
                    codAmount),
                Credits = codAmount,
                ExpressFlag = 0,
                SentAtUtc = sent,
                ExpiresAtUtc = sent.AddDays(Rules.MailRetentionDays)
            });
            NotifyRecipientEnvelope(sender.CharacterId, sender.Name ?? string.Empty);
        }

        bool TryResolveInventoryAttach(
            Player player,
            int containerType,
            int placement,
            out Identity slot,
            out Item item,
            out string failureReason)
        {
            slot = default;
            item = null!;
            failureReason = string.Empty;

            if (containerType != (int)IdentityType.Inventory)
            {
                failureReason = "Mail items must be attached from Inventory.";
                return false;
            }

            if (!player.Inventory.IsHydrated)
            {
                failureReason = "Attached item slot is invalid.";
                return false;
            }

            Container page = player.Inventory.Inventory;
            if (!page.Content.TryGetValue(placement, out Item? found) || found == null)
            {
                int relative = placement - page.Offset;
                if (relative >= 0 && relative < page.Capacity)
                    page.Content.TryGetValue(page.Offset + relative, out found);
            }

            if (found == null || found.InstanceId <= 0)
            {
                failureReason = string.Format(
                    CultureInfo.InvariantCulture,
                    "Attached item slot is invalid. ({0}/{1})",
                    containerType,
                    placement);
                return false;
            }

            int absolute = placement;
            if (!page.Content.ContainsKey(absolute))
            {
                foreach (KeyValuePair<int, Item> entry in page.Content)
                {
                    if (ReferenceEquals(entry.Value, found))
                    {
                        absolute = entry.Key;
                        break;
                    }
                }
            }

            slot = new Identity { Type = IdentityType.Inventory, Instance = absolute };
            item = found;
            return true;
        }

        MailListEntry ToListEntry(MailRowData mail, bool summary)
        {
            int sentUnix = ToUnixSeconds(mail.SentAtUtc);
            int expireUnix = ToUnixSeconds(mail.ExpiresAtUtc);
            return new MailListEntry
            {
                MailId = unchecked((ulong)(uint)mail.MailId),
                TimeField = 0,
                From = mail.SenderName ?? string.Empty,
                Subject = mail.Subject ?? string.Empty,
                CreditsField = sentUnix,
                CodField = expireUnix,
                FlagsField = ComputeMailFlags(mail),
                IsSummary = summary,
                ExtendedField64 = mail.Credits,
                AcgLow = mail.AcgLow,
                AcgHigh = mail.AcgHigh,
                AcgLevel = mail.AcgLevel,
                ExtendedField74 = mail.AcgMultipleCount > 0 ? mail.AcgMultipleCount : 0,
                Body = mail.Body ?? string.Empty
            };
        }

        int ComputeMailFlags(MailRowData mail)
        {
            int flags = Rules.FlagsBase;
            if (mail.IsRead)
                flags |= 1;
            bool hasItem = mail.AcgLow != 0 || mail.AcgHigh != 0;
            bool hasCredits = mail.Credits != 0;
            if (!hasItem && !hasCredits)
                flags |= 2;
            return flags;
        }

        void SendMailFailureFeedback(Player player, string failure)
        {
            if (string.Equals(failure, Rules.FailureNoDrop, StringComparison.Ordinal)
                || string.Equals(failure, Rules.FailureNoChests, StringComparison.Ordinal))
            {
                SendFormatFeedbackDialog(player, failure);
                return;
            }

            Tell(player, failure);
        }

        static void SendFormatFeedbackDialog(Player player, string text)
        {
            player.Session?.Send(new FormatFeedbackMessage
            {
                Identity = player.Identity,
                Unknown = 1,
                Unknown1 = 0,
                FormattedMessage = "~&!!!\":!!!)<sH" + text,
                Unknown2 = 0
            });
        }

        static void Tell(Player player, string text)
            => player.Session?.Send(new ChatTextMessage
            {
                Identity = player.Identity,
                Text = text,
                Unknown1 = 0,
                Unknown2 = 0,
                Unknown3 = 0
            });

        static int ResolveStack(Item item)
        {
            int count = item.StackCount;
            return count <= 0 || count > 10000 ? 1 : count;
        }

        static bool TryParseMailId(ulong requestedMailId, out long mailId)
        {
            mailId = unchecked((long)(uint)requestedMailId);
            return mailId > 0 || requestedMailId == 0;
        }

        static int ToUnixSeconds(DateTime utcOrUnspecified)
        {
            DateTime utc = utcOrUnspecified.Kind == DateTimeKind.Utc
                ? utcOrUnspecified
                : DateTime.SpecifyKind(utcOrUnspecified, DateTimeKind.Utc);
            long seconds = new DateTimeOffset(utc).ToUnixTimeSeconds();
            if (seconds < int.MinValue)
                return int.MinValue;
            if (seconds > int.MaxValue)
                return int.MaxValue;
            return (int)seconds;
        }
    }
}
