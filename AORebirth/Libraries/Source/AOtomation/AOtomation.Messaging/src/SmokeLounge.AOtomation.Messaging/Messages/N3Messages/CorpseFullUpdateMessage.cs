// --------------------------------------------------------------------------------------------------------------------
// <copyright file="CorpseFullUpdateMessage.cs" company="SmokeLounge">
//   Copyright © 2013 SmokeLounge.
//   This program is free software. It comes without any warranty, to
//   the extent permitted by applicable law. You can redistribute it
//   and/or modify it under the terms of the Do What The Fuck You Want
//   To Public License, Version 2, as published by Sam Hocevar. See
//   http://www.wtfpl.net/ for more details.
// </copyright>
// <summary>
//   Defines the CorpseFullUpdate type.
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace SmokeLounge.AOtomation.Messaging.Messages.N3Messages
{
    using SmokeLounge.AOtomation.Messaging.GameData;
    using SmokeLounge.AOtomation.Messaging.Serialization;
    using SmokeLounge.AOtomation.Messaging.Serialization.MappingAttributes;

    [AoContract((int)N3MessageType.CorpseFullUpdate)]
    public class CorpseFullUpdateMessage : N3Message
    {
        #region Constructors and Destructors

        public CorpseFullUpdateMessage()
        {
            this.N3MessageType = N3MessageType.CorpseFullUpdate;
        }

        #endregion

        #region AoMember Properties

        [AoMember(0)]
        public int Unknown1 { get; set; }

        [AoMember(1)]
        public int Unknown2 { get; set; }

        [AoMember(2)]
        public Identity Owner { get; set; }

        [AoMember(3)]
        public Vector3 Position { get; set; }

        [AoMember(4)]
        public Quaternion Heading { get; set; }

        [AoMember(5)]
        public int PlayfieldId { get; set; }

        [AoMember(6)]
        public Identity StateMachine { get; set; }

        [AoMember(7)]
        public short Unknown3 { get; set; }

        [AoMember(8, SerializeSize = ArraySizeType.X3F1)]
        public GameTuple<CharacterStat, uint>[] Stats { get; set; }

        /// <summary>AOSharp: Name.Length + 1 (includes trailing null byte).</summary>
        [AoMember(10)]
        public int NameLength { get; set; }

        [AoMember(11, SerializeSize = ArraySizeType.NoSerialization)]
        public string Name { get; set; }

        /// <summary>Null terminator after name chars (AOSharp WriteByte(0)).</summary>
        [AoMember(12)]
        public byte NameTerminator { get; set; }

        [AoMember(13)]
        public int Unknown4 { get; set; }

        [AoMember(14)]
        public int Unknown5 { get; set; }

        [AoMember(15, SerializeSize = ArraySizeType.X3F1)]
        public int[] UnknownArray { get; set; }

        [AoMember(16)]
        public int Unknown6 { get; set; }

        [AoMember(17, SerializeSize = ArraySizeType.X3F1)]
        public AnimationEffect[] AnimationEffects { get; set; }

        [AoMember(18)]
        public Identity UnknownIdentity { get; set; }

        [AoMember(19, SerializeSize = ArraySizeType.X3F1)]
        public Texture[] Textures { get; set; }

        /// <summary>Trailing int after cloth data (live CFU / AOSharp writer).</summary>
        [AoMember(20)]
        public int Unknown7 { get; set; }

        #endregion
    }
}
