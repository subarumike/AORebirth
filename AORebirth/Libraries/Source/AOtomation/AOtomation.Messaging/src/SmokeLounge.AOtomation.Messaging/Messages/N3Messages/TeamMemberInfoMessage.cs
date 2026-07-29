// --------------------------------------------------------------------------------------------------------------------
// <copyright file="TeamMemberInfoMessage.cs" company="SmokeLounge">
//   Copyright © 2013 SmokeLounge.
// </copyright>
// <summary>
//   TeamMemberInfoMessage — wire matched to capture 20260727-071217.
//   N3 header already has Identity(viewer) + Unknown(byte=0).
//   Body: Member, Unknown3..Unknown6.
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace SmokeLounge.AOtomation.Messaging.Messages.N3Messages
{
    using SmokeLounge.AOtomation.Messaging.GameData;
    using SmokeLounge.AOtomation.Messaging.Serialization.MappingAttributes;

    [AoContract((int)N3MessageType.TeamMemberInfo)]
    public class TeamMemberInfoMessage : N3Message
    {
        #region Constructors and Destructors

        public TeamMemberInfoMessage()
        {
            this.N3MessageType = N3MessageType.TeamMemberInfo;
        }

        #endregion

        #region AoMember Properties

        /// <summary>Team member the info describes.</summary>
        [AoMember(0)]
        public Identity Member { get; set; }

        [AoMember(1)]
        public int Unknown3 { get; set; }

        [AoMember(2)]
        public int Unknown4 { get; set; }

        [AoMember(3)]
        public int Unknown5 { get; set; }

        [AoMember(4)]
        public int Unknown6 { get; set; }

        #endregion
    }
}
