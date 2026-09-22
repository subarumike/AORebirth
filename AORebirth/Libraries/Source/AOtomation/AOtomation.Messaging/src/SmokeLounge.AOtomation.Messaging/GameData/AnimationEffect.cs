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
//   Retail CorpseFullUpdate ItemAnimEffect blob is 15 int32s. The 14th int
//   is MonsterData. CATMesh is a corpse stat, not this row.
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace SmokeLounge.AOtomation.Messaging.GameData
{
    using SmokeLounge.AOtomation.Messaging.Serialization.MappingAttributes;

    public class AnimationEffect
    {
        #region AoMember Properties

        [AoMember(0)]
        public int TypeId { get; set; }

        [AoMember(1)]
        public int HeaderB { get; set; }

        [AoMember(2)]
        public int HeaderC { get; set; }

        [AoMember(3)]
        public int Duration { get; set; }

        [AoMember(4)]
        public int Interval { get; set; }

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
        public int MonsterData { get; set; }

        [AoMember(14)]
        public int Unknown10 { get; set; }

        #endregion
    }
}
