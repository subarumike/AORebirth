// --------------------------------------------------------------------------------------------------------------------
// <copyright file="KnuBotRejectedItemsMessage.cs" company="SmokeLounge">
//   Copyright © 2013 SmokeLounge.
//   This program is free software. It comes without any warranty, to
//   the extent permitted by applicable law. You can redistribute it
//   and/or modify it under the terms of the Do What The Fuck You Want
//   To Public License, Version 2, as published by Sam Hocevar. See
//   http://www.wtfpl.net/ for more details.
// </copyright>
// <summary>
//   Defines the KnuBotRejectedItemsMessage type.
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace SmokeLounge.AOtomation.Messaging.Messages.N3Messages
{
    using SmokeLounge.AOtomation.Messaging.GameData;
    using SmokeLounge.AOtomation.Messaging.Serialization;
    using SmokeLounge.AOtomation.Messaging.Serialization.MappingAttributes;

    [AoContract((int)N3MessageType.KnuBotRejectedItems)]
    public class KnuBotRejectedItemsMessage : N3Message
    {
        #region Constructors and Destructors

        public KnuBotRejectedItemsMessage()
        {
            this.N3MessageType = N3MessageType.KnuBotRejectedItems;
        }

        #endregion

        #region AoMember Properties

        [AoMember(0)]
        public short Unknown1 { get; set; }

        [AoMember(1)]
        public Identity Target { get; set; }

        // Live server->client KnuBotRejectedItems carries an int32 element count (0 items => 4 zero bytes)
        // before Unknown2 (capture 20260716-Reset-perks #23, len=47). Without an explicit size the array
        // defaults to NoSerialization and the count is dropped, yielding a short 43-byte packet the client
        // cannot parse, so the trade ("Give Item") window never closes.
        [AoMember(2, SerializeSize = ArraySizeType.Int32)]
        public KnuBotRejectedItem[] Items { get; set; }

        [AoMember(3)]
        public int Unknown2 { get; set; }

        #endregion
    }
}