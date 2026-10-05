// --------------------------------------------------------------------------------------------------------------------
// <copyright file="SocialActionCmdMessage.cs" company="SmokeLounge">
//   Copyright © 2013 SmokeLounge.
//   This program is free software. It comes without any warranty, to
//   the extent permitted by applicable law. You can redistribute it
//   and/or modify it under the terms of the Do What The Fuck You Want
//   To Public License, Version 2, as published by Sam Hocevar. See
//   http://www.wtfpl.net/ for more details.
// </copyright>
// <summary>
//   Defines the SocialActionCmdMessage type.
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace SmokeLounge.AOtomation.Messaging.Messages.N3Messages
{
    using SmokeLounge.AOtomation.Messaging.Serialization.MappingAttributes;

    [AoContract((int)N3MessageType.SocialActionCmd)]
    public class SocialActionCmdMessage : N3Message
    {
        #region Constructors and Destructors

        public SocialActionCmdMessage()
        {
            this.N3MessageType = N3MessageType.SocialActionCmd;
        }

        #endregion

        #region AoMember Properties

        // Gamecode.dll SocialActionCmd_t: n3Command_t::WriteSubClass (N3.dll 0x100037c5) writes +0x18 and +0x1c,
        // then SocialActionCmd_t (0x1007ad2a) writes the AbstractAnimID at +0x20.

        /// <summary>
        /// n3Command_t +0x18. The client executes the command (plays the emote, and for sleep/lounge switches
        /// movement mode 0x21/0x22) only when this is 1 (Gamecode.dll 0x1007ac9e). Client requests send 1.
        /// </summary>
        [AoMember(0)]
        public int CommandState { get; set; }

        /// <summary>
        /// n3Command_t +0x1c: the sending client's command reference, a per-client counter. A client recognises
        /// its own command by this ref (n3Command_t::IsForeign, N3.dll 0x100037e8) and does not replay the
        /// animation for it.
        /// </summary>
        [AoMember(1)]
        public int CommandRef { get; set; }

        /// <summary>
        /// AbstractAnimID. The client rejects a read outside 1..71 (Gamecode.dll 0x1007ad4a).
        /// </summary>
        [AoMember(2)]
        public SocialAction Action { get; set; }

        #endregion
    }
}