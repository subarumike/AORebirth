// --------------------------------------------------------------------------------------------------------------------
// <copyright file="MailAction.cs" company="SmokeLounge">
//   Copyright © 2013 SmokeLounge.
// </copyright>
// --------------------------------------------------------------------------------------------------------------------

namespace SmokeLounge.AOtomation.Messaging.Messages.N3Messages
{
    /// <summary>
    /// Mail opcodes from capture 20260714-182726 + Gamecode N3Msg_* actions.
    /// </summary>
    public enum MailAction : short
    {
        /// <summary>Server → client: mailbox list (X3F1 + summary rows).</summary>
        MailboxList = 0,

        /// <summary>
        /// Client → server: open mailbox (mail id 0) or request one message (mail id ≠ 0).
        /// N3Msg_Open path and N3Msg_RequestMailMessage both use action 1.
        /// </summary>
        OpenOrRequest = 1,

        /// <summary>Server → client: full mail body (single MailMessage entry, not summary).</summary>
        MailDetail = 2,

        /// <summary>Client → server: take all attachments (N3Msg_MailTakeAll).</summary>
        TakeAll = 3,

        /// <summary>Client → server: delete mail (N3Msg_DeleteMail).</summary>
        Delete = 5,

        /// <summary>Client → server: send mail.</summary>
        SendMail = 6,

        /// <summary>Client → server: return to sender (N3Msg_ReturnMail).</summary>
        ReturnToSender = 7,

        /// <summary>Server → client: send accepted (includes assigned mail id).</summary>
        SendAccepted = 8
    }
}
