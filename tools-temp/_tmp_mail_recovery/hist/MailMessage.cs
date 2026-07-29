// --------------------------------------------------------------------------------------------------------------------
// <copyright file="MailMessage.cs" company="SmokeLounge">
//   Copyright © 2013 SmokeLounge.
// </copyright>
// --------------------------------------------------------------------------------------------------------------------

namespace SmokeLounge.AOtomation.Messaging.Messages.N3Messages
{
    using System.Collections.Generic;

    using SmokeLounge.AOtomation.Messaging.Serialization.MappingAttributes;

    /// <summary>
    /// N3 Mail (0x333B2867). Layout is action-dependent; deserialize via MailMessageSerializer.
    /// Capture: tools-temp/AOSharpLiveCapture/.../captures/20260714-182726
    /// List entry wire: Gamecode.dll MailIIR action 0 / MailMessage 0x10125ec5.
    /// </summary>
    [AoContract((int)N3MessageType.Mail)]
    public class MailMessage : N3Message
    {
        public MailMessage()
        {
            this.N3MessageType = N3MessageType.Mail;
            this.Recipient = string.Empty;
            this.Subject = string.Empty;
            this.Body = string.Empty;
            this.Entries = new List<MailListEntry>();
        }

        public MailAction Action { get; set; }

        public string Recipient { get; set; }

        public string Subject { get; set; }

        public string Body { get; set; }

        /// <summary>Inventory/container field from send (capture item attach: 104).</summary>
        public int ItemField1 { get; set; }

        /// <summary>Slot/instance field from send (capture item attach: 89).</summary>
        public int ItemField2 { get; set; }

        /// <summary>Attached credits; negative = COD amount in capture.</summary>
        public int Credits { get; set; }

        /// <summary>0 = standard, 1 = express (capture).</summary>
        public byte ExpressFlag { get; set; }

        /// <summary>SendAccepted: echoed SendMail action (6).</summary>
        public short EchoAction { get; set; }

        /// <summary>SendAccepted: assigned mail id (capture sequence 0x014A11CD+).</summary>
        public int MailId { get; set; }

        public int Unknown1 { get; set; }

        public int Unknown2 { get; set; }

        /// <summary>MailboxList (action 0) rows.</summary>
        public IList<MailListEntry> Entries { get; set; }
    }
}
