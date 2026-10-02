// --------------------------------------------------------------------------------------------------------------------
// <copyright file="KnuBotTradeMessage.cs" company="SmokeLounge">
//   Copyright © 2013 SmokeLounge.
//   This program is free software. It comes without any warranty, to
//   the extent permitted by applicable law. You can redistribute it
//   and/or modify it under the terms of the Do What The Fuck You Want
//   To Public License, Version 2, as published by Sam Hocevar. See
//   http://www.wtfpl.net/ for more details.
// </copyright>
// <summary>
//   Defines the KnuBotTradeMessage type.
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace SmokeLounge.AOtomation.Messaging.Messages.N3Messages
{
    using SmokeLounge.AOtomation.Messaging.GameData;
    using SmokeLounge.AOtomation.Messaging.Serialization.MappingAttributes;

    /// <summary>What a <see cref="KnuBotTradeMessage"/> does with its item.</summary>
    public enum KnuBotTradeAction
    {
        Add = 0,

        Remove = 1
    }

    [AoContract((int)N3MessageType.KnuBotTrade)]
    public class KnuBotTradeMessage : N3Message
    {
        #region Constructors and Destructors

        public KnuBotTradeMessage()
        {
            this.N3MessageType = N3MessageType.KnuBotTrade;
        }

        #endregion

        #region AoMember Properties

        // Client layout (Gamecode.dll KnubotTradeIIR_c, built by n3EngineClientAnarchy_t::
        // N3Msg_NPCChatAddTradeItem / N3Msg_NPCChatRemoveTradeItem): short 2, NPC identity, int action,
        // an identity the client always sends as zero, then the item's identity.

        [AoMember(0)]
        public short Unknown1 { get; set; }

        /// <summary>The NPC the item is given to.</summary>
        [AoMember(1)]
        public Identity Target { get; set; }

        [AoMember(2)]
        public KnuBotTradeAction Action { get; set; }

        /// <summary>Always zero from the client.</summary>
        [AoMember(3)]
        public Identity Unknown2 { get; set; }

        /// <summary>
        /// Add: where the item comes from (Inventory or OverflowWindow, instance = slot). Remove: the item's
        /// index in the client's KnuBot trade container (instance = index). The client has already moved the
        /// item locally when this arrives.
        /// </summary>
        [AoMember(4)]
        public Identity Container { get; set; }

        #endregion
    }
}