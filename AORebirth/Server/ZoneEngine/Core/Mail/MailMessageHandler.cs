#region License

// Copyright (c) 2005-2014, CellAO Team
// All rights reserved.

#endregion

namespace ZoneEngine.Core.Mail
{
    #region Usings ...

    using System;
    using System.Collections.Generic;
    using System.Globalization;

    using AORebirth.Core.Components;
    using AORebirth.Core.Entities;
    using AORebirth.Core.Network;

    using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;

    using ZoneEngine.Core.MessageHandlers;

    #endregion

    /// <summary>
    /// Mail Terminal: open/list/detail/send/take-all.
    /// </summary>
    [MessageHandler(MessageHandlerDirection.All)]
    public class MailMessageHandler : BaseMessageHandler<MailMessage, MailMessageHandler>
    {
        public MailMessageHandler()
        {
            this.UpdateCharacterStatsOnReceive = false;
        }

        protected override void Read(MailMessage message, IZoneClient client)
        {
            ICharacter character = client.Controller.Character;
            client.Server.Info(
                client,
                "Mail action={0}({1}) requestId={2} recipient={3} subject={4} credits={5} express={6}",
                message.Action,
                (int)message.Action,
                message.RequestedMailId,
                message.Recipient,
                message.Subject,
                message.Credits,
                message.ExpressFlag);

            switch (message.Action)
            {
                case MailAction.OpenOrRequest:
                    // Re-bind backpack Container dynels before compose UI is used.
                    InventoryContainerRuntimeService.Default.PublishMailBlockedContainerLinks(character);
                    if (message.RequestedMailId == 0)
                    {
                        this.SendMailboxList(character);
                    }
                    else
                    {
                        this.SendMailDetail(character, message.RequestedMailId);
                    }
                    break;

                case MailAction.SendMail:
                    this.HandleSendMail(character, message);
                    break;

                case MailAction.TakeAll:
                    this.HandleTakeAll(character, message.RequestedMailId);
                    break;

                case MailAction.Delete:
                    this.HandleDelete(character, message.RequestedMailId);
                    break;

                case MailAction.ReturnToSender:
                    this.HandleReturnToSender(character, message.RequestedMailId);
                    break;

                default:
                    ChatTextMessageHandler.Default.Send(
                        character,
                        string.Format(
                            CultureInfo.InvariantCulture,
                            "Mail action {0} is not implemented yet.",
                            (int)message.Action));
                    break;
            }
        }

        private void HandleSendMail(ICharacter character, MailMessage message)
        {
            string failure;
            int mailId;
            if (!MailRuntimeService.TrySendMail(character, message, out failure, out mailId))
            {
                this.SendMailFailureFeedback(character, failure ?? "Mail send failed.");
                return;
            }

            this.Send(
                character,
                x =>
                {
                    x.Identity = character.Identity;
                    x.Unknown = 0;
                    x.Action = MailAction.SendAccepted;
                    x.EchoAction = (short)MailAction.SendMail;
                    x.Unknown1 = 0;
                    x.MailId = mailId;
                    x.Unknown2 = 0;
                });

            string mode = message.ExpressFlag != 0 ? "Express" : "Standard";
            string attachNote = string.Empty;
            if (message.Credits > 0)
            {
                attachNote = string.Format(
                    CultureInfo.InvariantCulture,
                    " Attached {0} credits.",
                    message.Credits);
            }
            else if (message.Credits < 0)
            {
                attachNote = string.Format(
                    CultureInfo.InvariantCulture,
                    " COD {0} credits.",
                    -message.Credits);
            }

            if (message.ItemField1 != 0 || message.ItemField2 != 0)
            {
                attachNote += " Item attached.";
            }

            ChatTextMessageHandler.Default.Send(
                character,
                string.Format(
                    CultureInfo.InvariantCulture,
                    "Mail sent to {0} ({1}).{2}",
                    message.Recipient,
                    mode,
                    attachNote));
        }

        private void HandleTakeAll(ICharacter character, ulong mailId)
        {
            string failure;
            MailListEntry updated;
            if (!MailRuntimeService.TryTakeAll(character, mailId, out failure, out updated))
            {
                ChatTextMessageHandler.Default.Send(character, failure ?? "Take All failed.");
                return;
            }

            MailRuntimeService.SyncUnreadMailEnvelope(character);

            this.Send(
                character,
                x =>
                {
                    x.Identity = character.Identity;
                    x.Unknown = 0;
                    x.Action = MailAction.MailDetail;
                    x.Detail = updated;
                });

            // Refresh inbox so read icon / attachment state update on list rows.
            this.SendMailboxList(character);

            ChatTextMessageHandler.Default.Send(character, "Mail attachments taken.");
        }

        private void HandleDelete(ICharacter character, ulong mailId)
        {
            string failure;
            if (!MailRuntimeService.TryDeleteMail(character.Name, mailId, out failure))
            {
                ChatTextMessageHandler.Default.Send(character, failure ?? "Delete failed.");
                return;
            }

            MailRuntimeService.SyncUnreadMailEnvelope(character);
            this.SendMailboxList(character);
            ChatTextMessageHandler.Default.Send(character, "Mail deleted.");
        }

        private void HandleReturnToSender(ICharacter character, ulong mailId)
        {
            string failure;
            if (!MailRuntimeService.TryReturnToSender(character, mailId, out failure))
            {
                ChatTextMessageHandler.Default.Send(character, failure ?? "Return to sender failed.");
                return;
            }

            this.SendMailboxList(character);
            ChatTextMessageHandler.Default.Send(character, "Mail returned to sender.");
        }

        /// <summary>
        /// Capture 20260715-100540: container Item-field popup is client Feedback_MailNoChests
        /// (IdentityType.Container) — no FormatFeedback packet. Server send-reject still uses
        /// FormatFeedback with the same caption. NoDrop uses Feedback_MailNoNodrops the same way.
        /// </summary>
        private void SendMailFailureFeedback(ICharacter character, string failure)
        {
            if (string.Equals(failure, MailRuntimeService.FailureNoDrop, StringComparison.Ordinal)
                || string.Equals(failure, MailRuntimeService.FailureNoChests, StringComparison.Ordinal))
            {
                this.SendFormatFeedbackDialog(character, failure);
                return;
            }

            ChatTextMessageHandler.Default.Send(character, failure);
        }

        private void SendFormatFeedbackDialog(ICharacter character, string text)
        {
            if (character == null || character.Controller == null || character.Controller.Client == null)
            {
                return;
            }

            character.Controller.Client.SendCompressed(
                new FormatFeedbackMessage
                {
                    Identity = character.Identity,
                    Unknown = 1,
                    Unknown1 = 0,
                    FormattedMessage = "~&!!!\":!!!)<sH" + text,
                    Unknown2 = 0
                },
                character.Identity.Instance);
        }

        private void SendMailboxList(ICharacter character)
        {
            MailRuntimeService.SyncUnreadMailEnvelope(character);
            IList<MailListEntry> entries = MailRuntimeService.BuildMailboxListEntries(character.Name);

            this.Send(
                character,
                x =>
                {
                    x.Identity = character.Identity;
                    x.Unknown = 0;
                    x.Action = MailAction.MailboxList;
                    x.Entries = entries;
                });
        }

        private void SendMailDetail(ICharacter character, ulong mailId)
        {
            MailListEntry detail;
            if (!MailRuntimeService.TryBuildMailDetail(character.Name, mailId, out detail))
            {
                ChatTextMessageHandler.Default.Send(character, "Mail not found.");
                return;
            }

            MailRuntimeService.SyncUnreadMailEnvelope(character);

            this.Send(
                character,
                x =>
                {
                    x.Identity = character.Identity;
                    x.Unknown = 0;
                    x.Action = MailAction.MailDetail;
                    x.Detail = detail;
                });

            // Open marks read; push list so inbox envelope icon can flip.
            this.SendMailboxList(character);
        }
    }
}
