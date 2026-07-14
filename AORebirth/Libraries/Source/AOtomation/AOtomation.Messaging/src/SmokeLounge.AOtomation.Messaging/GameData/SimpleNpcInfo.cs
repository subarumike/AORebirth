// --------------------------------------------------------------------------------------------------------------------
// <copyright file="SimpleNpcInfo.cs" company="SmokeLounge">
//   Copyright © 2013 SmokeLounge.
//   This program is free software. It comes without any warranty, to
//   the extent permitted by applicable law. You can redistribute it
//   and/or modify it under the terms of the Do What The Fuck You Want
//   To Public License, Version 2, as published by Sam Hocevar. See
//   http://www.wtfpl.net/ for more details.
// </copyright>
// <summary>
//   Defines the SimpleNpcInfo type.
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace SmokeLounge.AOtomation.Messaging.GameData
{
    using SmokeLounge.AOtomation.Messaging.Serialization.MappingAttributes;

    public class SimpleNpcInfo : SimpleCharacterInfo
    {
        #region AoMember Properties

        [AoMember(0)]
        public short Family { get; set; }

        [AoMember(1)]
        public short LosHeight { get; set; }

        // Captured immediately after the compact NPC family/LOS fields.
        // Most legacy NPCs use 0; Subway Infectors use 0x0A.
        public short UnknownData { get; set; }

        // The live SCFU NPC block always carries this short. When it is non-zero,
        // one additional byte follows it. Preserve both instead of discarding them.
        public short UnknownData2 { get; set; }

        public byte UnknownData3 { get; set; }

        #endregion
    }
}
