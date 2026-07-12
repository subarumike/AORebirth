// --------------------------------------------------------------------------------------------------------------------
// <copyright file="NewLevelMessage.cs" company="SmokeLounge">
//   Copyright © 2013 SmokeLounge.
//   This program is free software. It comes without any warranty, to
//   the extent permitted by applicable law. You can redistribute it
//   and/or modify it under the terms of the Do What The Fuck You Want
//   To Public License, Version 2, as published by Sam Hocevar. See
//   http://www.wtfpl.net/ for more details.
// </copyright>
// <summary>
//   Defines the NewLevelMessage type.
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace SmokeLounge.AOtomation.Messaging.Messages.N3Messages
{
    using SmokeLounge.AOtomation.Messaging.Serialization.MappingAttributes;

    [AoContract((int)N3MessageType.NewLevel)]
    public class NewLevelMessage : N3Message
    {
        public NewLevelMessage()
        {
            this.N3MessageType = N3MessageType.NewLevel;
        }

        // Capture 20260712-131331 proves eight Int32 fields after the N3 header.
        // Example level 2:
        // 2, 5500, 1450, 1450, 4050, 0, 4, 145.
        [AoMember(0)]
        public int Level { get; set; }

        [AoMember(1)]
        public int Ip { get; set; }

        [AoMember(2)]
        public int Xp { get; set; }

        [AoMember(3)]
        public int LastSaveXp { get; set; }

        [AoMember(4)]
        public int NextLevelXp { get; set; }

        [AoMember(5)]
        public int Unknown1 { get; set; }

        [AoMember(6)]
        public int Unknown2 { get; set; }

        [AoMember(7)]
        public int LastXp { get; set; }
    }
}
