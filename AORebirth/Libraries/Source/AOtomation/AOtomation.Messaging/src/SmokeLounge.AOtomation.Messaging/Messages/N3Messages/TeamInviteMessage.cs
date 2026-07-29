// --------------------------------------------------------------------------------------------------------------------
// <copyright file="TeamInviteMessage.cs" company="SmokeLounge">
//   Copyright © 2013 SmokeLounge.
// </copyright>
// <summary>
//   TeamInviteMessage — wire matched to capture 20260728-234012.
//   N3 header: Identity(invitee) + Unknown(byte=1).
//   Body: Inviter Identity, NameLen(Int16), Inviter name.
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace SmokeLounge.AOtomation.Messaging.Messages.N3Messages
{
    using SmokeLounge.AOtomation.Messaging.GameData;
    using SmokeLounge.AOtomation.Messaging.Serialization;
    using SmokeLounge.AOtomation.Messaging.Serialization.MappingAttributes;

    [AoContract((int)N3MessageType.TeamInvite)]
    public class TeamInviteMessage : N3Message
    {
        #region Constructors and Destructors

        public TeamInviteMessage()
        {
            this.N3MessageType = N3MessageType.TeamInvite;
        }

        #endregion

        #region AoMember Properties

        /// <summary>Inviting character.</summary>
        [AoMember(0)]
        public Identity Inviter { get; set; }

        [AoMember(1, SerializeSize = ArraySizeType.Int16)]
        public string Name { get; set; }

        #endregion
    }
}
