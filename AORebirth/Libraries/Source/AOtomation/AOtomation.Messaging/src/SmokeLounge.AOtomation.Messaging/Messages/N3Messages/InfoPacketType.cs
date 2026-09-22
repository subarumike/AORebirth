// --------------------------------------------------------------------------------------------------------------------
// <copyright file="InfoPacketType.cs" company="SmokeLounge">
//   Copyright © 2013 SmokeLounge.
//   This program is free software. It comes without any warranty, to
//   the extent permitted by applicable law. You can redistribute it
//   and/or modify it under the terms of the Do What The Fuck You Want
//   To Public License, Version 2, as published by Sam Hocevar. See
//   http://www.wtfpl.net/ for more details.
// </copyright>
// <summary>
//   Defines the InfoPacketType type.
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace SmokeLounge.AOtomation.Messaging.Messages.N3Messages
{
    using System;

    [Flags]
    public enum InfoPacketFlags : byte
    {
        HasOrgRank = 0x01,

        HasTowerFields = 0x02,

        HasTowers = 0x04,

        HasGasChangeInfo = 0x08,

        HasPvpInfoHidden = 0x10,

        HasFactionInfo = 0x20,

        HasCharacterInfo = 0x40
    }

    public enum InfoPacketType : byte
    {
        Character = (byte)InfoPacketFlags.HasCharacterInfo, // 0x40

        CharacterOrg = (byte)(InfoPacketFlags.HasCharacterInfo | InfoPacketFlags.HasOrgRank), // 0x41

        CharacterOrgSite = (byte)(InfoPacketFlags.HasCharacterInfo | InfoPacketFlags.HasOrgRank | InfoPacketFlags.HasTowerFields), // 0x43

        CharacterOrgSiteTower = (byte)(InfoPacketFlags.HasCharacterInfo | InfoPacketFlags.HasOrgRank | InfoPacketFlags.HasTowerFields | InfoPacketFlags.HasTowers), // 0x47

        Monster = (byte)(InfoPacketFlags.HasCharacterInfo | InfoPacketFlags.HasPvpInfoHidden), // 0x50

        Tower = (byte)(InfoPacketFlags.HasCharacterInfo | InfoPacketFlags.HasPvpInfoHidden | InfoPacketFlags.HasTowers), // 0x54

        ControlTower = (byte)(InfoPacketFlags.HasCharacterInfo | InfoPacketFlags.HasPvpInfoHidden | InfoPacketFlags.HasTowers | InfoPacketFlags.HasGasChangeInfo) // 0x5C
    }
}