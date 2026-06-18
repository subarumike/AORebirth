// --------------------------------------------------------------------------------------------------------------------
// <copyright file="QuestMessage.cs" company="SmokeLounge">
//   Copyright © 2013 SmokeLounge.
//   This program is free software. It comes without any warranty, to
//   the extent permitted by applicable law. You can redistribute it
//   and/or modify it under the terms of the Do What The Fuck You Want
//   To Public License, Version 2, as published by Sam Hocevar. See
//   http://www.wtfpl.net/ for more details.
// </copyright>
// <summary>
//   Defines the QuestMessage type.
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace SmokeLounge.AOtomation.Messaging.Messages.N3Messages
{
    using SmokeLounge.AOtomation.Messaging.GameData;
    using SmokeLounge.AOtomation.Messaging.Serialization.MappingAttributes;

    [AoContract((int)N3MessageType.Quest)]
    public class QuestMessage : N3Message
    {
        #region Constructors and Destructors

        public QuestMessage()
        {
            this.N3MessageType = N3MessageType.Quest;
        }

        #endregion

        #region AoMember Properties

        [AoMember(0)]
        public QuestAction Action { get; set; }

        [AoMember(1)]
        public int Unknown1 { get; set; }

        [AoMember(2)]
        public Identity Mission { get; set; }

        [AoMember(3)]
        public int Unknown2 { get; set; }

        [AoMember(4)]
        public int Unknown3 { get; set; }

        #endregion
    }
}
