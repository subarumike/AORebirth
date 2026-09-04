// --------------------------------------------------------------------------------------------------------------------
// <copyright file="AnimationEffect.cs" company="SmokeLounge">
//   Copyright © 2013 SmokeLounge.
//   This program is free software. It comes without any warranty, to
//   the extent permitted by applicable law. You can redistribute it
//   and/or modify it under the terms of the Do What The Fuck You Want
//   To Public License, Version 2, as published by Sam Hocevar. See
//   http://www.wtfpl.net/ for more details.
// </copyright>
// <summary>
//   Defines the AnimationEffect type (AOSharp CFU spell/anim row: 15 ints).
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace SmokeLounge.AOtomation.Messaging.GameData
{
    using SmokeLounge.AOtomation.Messaging.Serialization.MappingAttributes;

    public class AnimationEffect
    {
        #region AoMember Properties

        [AoMember(0)]
        public int IdentityType { get; set; }

        [AoMember(1)]
        public int NanoId { get; set; }

        [AoMember(2)]
        public int NanoInstance { get; set; }

        [AoMember(3)]
        public int Time1 { get; set; }

        [AoMember(4)]
        public int Time2 { get; set; }

        [AoMember(5)]
        public int Unknown2 { get; set; }

        [AoMember(6)]
        public int Unknown3 { get; set; }

        [AoMember(7)]
        public int Unknown4 { get; set; }

        [AoMember(8)]
        public int Unknown5 { get; set; }

        [AoMember(9)]
        public int Unknown6 { get; set; }

        [AoMember(10)]
        public int Unknown7 { get; set; }

        [AoMember(11)]
        public int Unknown8 { get; set; }

        [AoMember(12)]
        public int Unknown9 { get; set; }

        [AoMember(13)]
        public int VisualDataId { get; set; }

        [AoMember(14)]
        public int Unknown10 { get; set; }

        #endregion
    }
}
