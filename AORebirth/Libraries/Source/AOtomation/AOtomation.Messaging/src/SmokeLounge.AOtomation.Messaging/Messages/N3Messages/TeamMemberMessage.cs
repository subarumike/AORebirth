// --------------------------------------------------------------------------------------------------------------------
// <copyright file="TeamMemberMessage.cs" company="SmokeLounge">
//   Copyright © 2013 SmokeLounge.
// </copyright>
// <summary>
//   TeamMemberMessage — wire matched to capture 20260727-071217.
//   N3 header already has Identity(viewer) + Unknown(byte=0).
//   Body: Member, Team, Unknown4(-1), Level, Unknown5, Name.
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace SmokeLounge.AOtomation.Messaging.Messages.N3Messages
{
    using SmokeLounge.AOtomation.Messaging.GameData;
    using SmokeLounge.AOtomation.Messaging.Serialization;
    using SmokeLounge.AOtomation.Messaging.Serialization.MappingAttributes;

    [AoContract((int)N3MessageType.TeamMember)]
    public class TeamMemberMessage : N3Message
    {
        #region Constructors and Destructors

        public TeamMemberMessage()
        {
            this.N3MessageType = N3MessageType.TeamMember;
        }

        #endregion

        #region AoMember Properties

        /// <summary>Team member being announced.</summary>
        [AoMember(0)]
        public Identity Member { get; set; }

        /// <summary>TeamWindow identity (type 0xDEA9 + team instance).</summary>
        [AoMember(1)]
        public Identity Team { get; set; }

        /// <summary>Capture constant -1.</summary>
        [AoMember(2)]
        public int Unknown4 { get; set; }

        /// <summary>Capture uses character level.</summary>
        [AoMember(3)]
        public int Level { get; set; }

        /// <summary>Capture short (profession / side hint).</summary>
        [AoMember(4)]
        public short Unknown5 { get; set; }

        [AoMember(5, SerializeSize = ArraySizeType.Int32)]
        public string Name { get; set; }

        #endregion
    }
}
