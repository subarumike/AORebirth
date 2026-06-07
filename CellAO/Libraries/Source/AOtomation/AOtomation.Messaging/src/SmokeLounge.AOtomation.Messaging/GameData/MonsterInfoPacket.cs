// --------------------------------------------------------------------------------------------------------------------
// <copyright file="MonsterInfoPacket.cs" company="SmokeLounge">
//   Copyright © 2013 SmokeLounge.
//   This program is free software. It comes without any warranty, to
//   the extent permitted by applicable law. You can redistribute it
//   and/or modify it under the terms of the Do What The Fuck You Want
//   To Public License, Version 2, as published by Sam Hocevar. See
//   http://www.wtfpl.net/ for more details.
// </copyright>
// <summary>
//   Defines the MonsterInfoPacket type.
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace SmokeLounge.AOtomation.Messaging.GameData
{
    using SmokeLounge.AOtomation.Messaging.Serialization.MappingAttributes;

    public class MonsterInfoPacket : InfoPacket
    {
        [AoMember(0)]
        public byte Unknown1 { get; set; }
        [AoMember (1)]
        public byte Profession { get; set; }
        [AoMember(2)]
        public byte Level { get; set; }
        [AoMember(3)]
        public byte TitleLevel { get; set; }
        [AoMember(4)]
        public byte VisualProfession { get; set; }
        [AoMember(5)]

        public short Unknown2 { get; set; }
        [AoMember(6)]
        public int CurrentHealth { get; set; }
        [AoMember(7)]
        public int MaxHealth { get; set; }
        [AoMember(8)]
        // Perhaps breed hostility or NPCFamily
        public int Unknown3 { get; set; }
        [AoMember(9)]
        public int OrganizationId { get; set; }
        [AoMember(10)]
        public short Unknown4 { get; set; }
        [AoMember(11)]
        public short Unknown5 { get; set; }
        [AoMember(12)]
        public short Unknown6 { get; set; }
        [AoMember(13)]
        public short Unknown7 { get; set; }
        [AoMember(14)]
        // 1234567890
        public int Unknown8 { get; set; }
        [AoMember(15)]
        // 1234567890
        public int Unknown9 { get; set; }
        [AoMember(16)]
        // 1234567890
        public int Unknown10 { get; set; }

    }
}