#region License

// Copyright (c) 2005-2014, CellAO Team
// All rights reserved.

#endregion

namespace ZoneEngine.Core.MessageHandlers
{
    #region Usings ...

    using System;
    using System.Collections.Generic;
    using System.Globalization;

    using AORebirth.Core.Components;
    using AORebirth.Core.Entities;
    using AORebirth.Core.Network;

    using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;

    using ZoneEngine.Core;

    #endregion

    /// <summary>
    /// Capture-backed Mail Terminal options (open mailbox + send mail).
    /// Evidence: captures/20260714-182726 Mail OUT/IN actions 1/0/6/8.
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
                "Mail action={0}({1}) recipient={2} subject={3} credits={4} item1={5} item2={6} express={7}",
                message.Action,
                (int)message.Action,
                message.Recipient,
                message.Subject,
                message.Credits,
                message.ItemField1,
                message.ItemField2,
                message.ExpressFlag);

            switch (message.Action)
            {
                case MailAction.OpenMailbox:
                    this.SendMailboxList(character);
                    break;

                case MailAction.SendMail:
                    this.HandleSendMail(character, message);
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
                ChatTextMessageHandler.Default.Send(character, failure ?? "Mail send failed.");
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
            ChatTextMessageHandler.Default.Send(
                character,
                string.Format(
                    CultureInfo.InvariantCulture,
                    "Mail sent to {0} ({1}).",
                    message.Recipient,
                    mode));
        }

        private void SendMailboxList(ICharacter character)
        {
            // Action 0 list wire: Gamecode MailIIR (X3F1 + MailMessage summary rows).
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
    }
}
