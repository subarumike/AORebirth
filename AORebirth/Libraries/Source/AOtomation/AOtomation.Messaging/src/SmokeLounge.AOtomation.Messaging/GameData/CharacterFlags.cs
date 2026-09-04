// --------------------------------------------------------------------------------------------------------------------
// <copyright file="CharacterFlags.cs" company="SmokeLounge">
//   Copyright © 2013 SmokeLounge.
//   This program is free software. It comes without any warranty, to
//   the extent permitted by applicable law. You can redistribute it
//   and/or modify it under the terms of the Do What The Fuck You Want
//   To Public License, Version 2, as published by Sam Hocevar. See
//   http://www.wtfpl.net/ for more details.
// </copyright>
// <summary>
//   Defines the CharacterFlags type.
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace SmokeLounge.AOtomation.Messaging.GameData
{
    using System;

    [Flags]
    public enum CharacterFlags
    {
        // 0000 0000 0000 0000 0000 0000 0000 0000
        None = 0x00000000, 

        /// <summary>
        /// Bit 17. When set, SCFU carries one tower-specific byte after CharacterInfo
        /// (AOSharp SimpleCharFullUpdateSerializer).
        /// </summary>
        Tower = 0x00020000,

        // 0000 0000 0100 0000 0000 0000 0000 0000
        HasVisibleName = 0x00400000,

        /// <summary>
        /// Bit 21. Present on live knubot vendors (Lorelei/Barry SCFU 279450113).
        /// Client uses this with HasBlueName to enable the dialogue Shop cart.
        /// Capture 20260721-loralei.
        /// </summary>
        HasShopCart = 0x00200000,

        /// <summary>
        /// Bit 23. On player SCFU this paints a blue nametag (Mike 2026-07-19).
        /// LTC / quest-style blue — not ARK/GM green.
        /// </summary>
        HasBlueName = 0x00800000,

        /// <summary>
        /// Bit 28. Common on live NPC SCFU CharacterFlags; does not color player
        /// nametags (Mike 2026-07-19: white with [GM] suffix). Retained for
        /// decode/docs only — not a green-name bit.
        /// </summary>
        NpcStyleFlag28 = 0x10000000,
    }
}
