// --------------------------------------------------------------------------------------------------------------------
// <copyright file="TradeAction.cs" company="SmokeLounge">
//   Copyright © 2013 SmokeLounge.
//   This program is free software. It comes without any warranty, to
//   the extent permitted by applicable law. You can redistribute it
//   and/or modify it under the terms of the Do What The Fuck You Want
//   To Public License, Version 2, as published by Sam Hocevar. See
//   http://www.wtfpl.net/ for more details.
// </copyright>
// <summary>
//   Defines the TradeAction type.
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace SmokeLounge.AOtomation.Messaging.Messages.N3Messages
{
    public enum TradeAction : byte
    {
        Open = 0x00,

        None = Open,

        Accept = 0x01,

        End = Accept,

        Decline = 0x02,

        Confirm = 0x03,

        Complete = 0x04,

        Unknown = Complete,

        AddItem = 0x05,

        RemoveItem = 0x06,

        UpdateCredits = 0x07,

        Credits = UpdateCredits,

        OtherPlayerAddItem = 0x08
    }
}
