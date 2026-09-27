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

        public Identity Owner { get; set; }

        [AoMember(1)]
        [AoFlags("weaponOwnerType")]
        public int OwnerType
        {
            get { return (int)this.Owner.Type; }
            set { this.Owner = new Identity { Type = (IdentityType)value, Instance = this.Owner.Instance }; }
        }

        [AoMember(2)]
        public int OwnerInstance
        {
            get { return this.Owner.Instance; }
            set { this.Owner = new Identity { Type = this.Owner.Type, Instance = value }; }
        }

        // Ownerless mission return items carry the same world transform as other
        // dropped items; owned/equipped weapons omit it.
        [AoMember(3)]
        [AoUsesFlags("weaponOwnerType", typeof(Vector3), FlagsCriteria.HasNone, new[] { int.MaxValue })]
        public Vector3 Coordinate { get; set; }

        [AoMember(4)]
        [AoUsesFlags("weaponOwnerType", typeof(Quaternion), FlagsCriteria.HasNone, new[] { int.MaxValue })]
        public Quaternion Heading { get; set; }

        [AoMember(5)]
        public int PlayfieldId { get; set; }

        [AoMember(6)]
        public Identity StateMachine { get; set; }

        [AoMember(7)]
        public short Unknown2 { get; set; }

        [AoMember(8, SerializeSize = ArraySizeType.X3F1)]
        public GameTuple<CharacterStat, uint>[] Stats { get; set; }

        [AoMember(9)]
        public int Unknown3 { get; set; }

        #endregion
    }
}
