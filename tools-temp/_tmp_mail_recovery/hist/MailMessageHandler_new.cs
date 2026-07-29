#region License

// Copyright (c) 2005-2014, CellAO Team
// All rights reserved.

#endregion

namespace ZoneEngine.Core.MessageHandlers
{
    #region Usings ...

    using System.Collections.Generic;
    using System.Globalization;

    using AORebirth.Core.Components;
    using AORebirth.Core.Entities;
    using AORebirth.Core.Network;

    using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;

    using ZoneEngine.Core;

    #endregion

    /// <summary>
    /// Capture-backed Mail Terminal (open / list / detail / send / take / delete).
    /// Evidence: captures/20260714-182726 Mail OUT/IN actions.
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
                "Mail action={0}({1}) recipient={2} subject={3} credits={4} item1={5} item2={6} express={7} reqId={8}",
                message.Action,
                (int)message.Action,
                message.Recipient,
                message.Subject,
                message.Credits,
                message.ItemField1,
                message.ItemField2,
                message.ExpressFlag,
                message.RequestedMailId);

            switch (message.Action)
            {
                case MailAction.OpenOrRequest:
                    if (message.RequestedMailId == 0)
                    {
                        this.SendMailboxList(character);
                    }
                    else
                    {
                        this.HandleRequestDetail(character, message.RequestedMailId);
                    }
                    break;

                case MailAction.MailDetail:
                    this.HandleRequestDetail(character, message.RequestedMailId != 0
                        ? message.RequestedMailId
                        : (message.Detail != null ? message.Detail.MailId : 0));
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
                this.SendMailFailure(character, failure ?? "Mail send failed.");
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
                    " C.O.D. {0} credits.",
                    -message.Credits);
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

        private void HandleRequestDetail(ICharacter character, ulong mailId)
        {
            MailRuntimeService.StoredMail mail;
            if (!MailRuntimeService.TryGetMail(character.Name, mailId, out mail))
            {
                ChatTextMessageHandler.Default.Send(character, "Mail not found.");
                return;
            }

            MailListEntry detail = MailRuntimeService.BuildMailDetail(mail);
            this.Send(
                character,
                x =>
                {
                    x.Identity = character.Identity;
                    x.Unknown = 0;
                    x.Action = MailAction.MailDetail;
                    x.Detail = detail;
                });

            MailRuntimeService.SyncUnreadMailEnvelope(character);
            this.SendMailboxList(character);
        }

        private void HandleTakeAll(ICharacter character, ulong mailId)
        {
            string failure;
            if (!MailRuntimeService.TryTakeAll(character, mailId, out failure))
            {
                ChatTextMessageHandler.Default.Send(character, failure ?? "Take All failed.");
                return;
            }

            MailRuntimeService.StoredMail mail;
            if (MailRuntimeService.TryGetMail(character.Name, mailId, out mail))
            {
                MailListEntry detail = MailRuntimeService.BuildMailDetail(mail);
                this.Send(
                    character,
                    x =>
                    {
                        x.Identity = character.Identity;
                        x.Unknown = 0;
                        x.Action = MailAction.MailDetail;
                        x.Detail = detail;
                    });
            }

            this.SendMailboxList(character);
        }

        private void HandleDelete(ICharacter character, ulong mailId)
        {
            string failure;
            if (!MailRuntimeService.TryDelete(character, mailId, out failure))
            {
                ChatTextMessageHandler.Default.Send(character, failure ?? "Delete failed.");
                return;
            }

            this.SendMailboxList(character);
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

        private void SendMailFailure(ICharacter character, string failure)
        {
            if (failure == MailRuntimeService.FailureNoDrop
                || failure == MailRuntimeService.FailureNoChests)
            {
                SendFormatFeedback(character, failure);
                return;
            }

            ChatTextMessageHandler.Default.Send(character, failure);
        }

        private static void SendFormatFeedback(ICharacter character, string text)
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
                    FormattedMessage = text,
                    Unknown2 = 0
                },
                character.Identity.Instance);
        }
    }
}
