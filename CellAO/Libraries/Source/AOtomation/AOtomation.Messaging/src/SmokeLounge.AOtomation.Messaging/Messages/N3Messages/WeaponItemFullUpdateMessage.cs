// --------------------------------------------------------------------------------------------------------------------
// <copyright file="WeaponItemFullUpdateMessage.cs" company="SmokeLounge">
//   Copyright © 2013 SmokeLounge.
//   This program is free software. It comes without any warranty, to
//   the extent permitted by applicable law. You can redistribute it
//   and/or modify it under the terms of the Do What The Fuck You Want
//   To Public License, Version 2, as published by Sam Hocevar. See
//   http://www.wtfpl.net/ for more details.
// </copyright>
// <summary>
//   Defines the WeaponItemFullUpdateMessage type.
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace SmokeLounge.AOtomation.Messaging.Messages.N3Messages
{
    using SmokeLounge.AOtomation.Messaging.GameData;
    using SmokeLounge.AOtomation.Messaging.Serialization;
    using SmokeLounge.AOtomation.Messaging.Serialization.MappingAttributes;

    [AoContract((int)N3MessageType.WeaponItemFullUpdate)]
    public class WeaponItemFullUpdateMessage : N3Message
    {
        #region Constructors and Destructors

        public WeaponItemFullUpdateMessage()
        {
            this.N3MessageType = N3MessageType.WeaponItemFullUpdate;
        }

        #endregion

        #region AoMember Properties

        [AoMember(0)]
        public int Unknown1 { get; set; }

        [AoMember(1)]
        public Identity Owner { get; set; }

        [AoMember(2)]
        public int PlayfieldId { get; set; }

        [AoMember(3)]
        public Identity StateMachine { get; set; }

        [AoMember(4)]
        public short Unknown2 { get; set; }

        [AoMember(5, SerializeSize = ArraySizeType.X3F1)]
        public GameTuple<CharacterStat, uint>[] Stats { get; set; }

        [AoMember(6)]
        public int Unknown3 { get; set; }

        #endregion
    }
}
