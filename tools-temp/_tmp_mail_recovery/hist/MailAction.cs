// --------------------------------------------------------------------------------------------------------------------
// <copyright file="MailAction.cs" company="SmokeLounge">
//   Copyright © 2013 SmokeLounge.
// </copyright>
// --------------------------------------------------------------------------------------------------------------------

namespace SmokeLounge.AOtomation.Messaging.Messages.N3Messages
{
    /// <summary>
    /// Capture-backed Mail opcodes (20260714-182726).
    /// </summary>
    public enum MailAction : short
    {
        /// <summary>Server → client: mailbox list (may be empty X3F1).</summary>
        MailboxList = 0,

        /// <summary>Client → server: open mailbox after Terminal Use.</summary>
        OpenMailbox = 1,

        /// <summary>Client → server: send mail (standard / express / item / credits / COD).</summary>
        SendMail = 6,

        /// <summary>Server → client: send accepted (includes assigned mail id).</summary>
        SendAccepted = 8
    }
}
