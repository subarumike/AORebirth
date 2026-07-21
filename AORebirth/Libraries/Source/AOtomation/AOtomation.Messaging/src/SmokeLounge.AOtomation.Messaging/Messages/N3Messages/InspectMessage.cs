// --------------------------------------------------------------------------------------------------------------------
// <copyright file="InspectMessage.cs" company="SmokeLounge">
//   Copyright © 2013 SmokeLounge.
//   This program is free software. It comes without any warranty, to
//   the extent permitted by applicable law. You can redistribute it
//   and/or modify it under the terms of the Do What The Fuck You Want
//   To Public License, Version 2, as published by Sam Hocevar. See
//   http://www.wtfpl.net/ for more details.
// </copyright>
// <summary>
//   Defines the InspectMessage type.
//   Capture 20260719-182611: server reply to CharacterAction Inspect (0x105).
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace SmokeLounge.AOtomation.Messaging.Messages.N3Messages
{
    using SmokeLounge.AOtomation.Messaging.GameData;
    using SmokeLounge.AOtomation.Messaging.Serialization;
    using SmokeLounge.AOtomation.Messaging.Serialization.MappingAttributes;

    /// <summary>
    /// Server→client: equipped gear for Character Info → Inspect Equipment.
    /// </summary>
    [AoContract((int)N3MessageType.Inspect)]
    public class InspectMessage : N3Message
    {
        public InspectMessage()
        {
            this.N3MessageType = N3MessageType.Inspect;
            // Capture replies use Unknown=0 (not the N3Message default 0x01).
            this.Unknown = 0;
        }

        /// <summary>
        /// Inspected character identity.
        /// </summary>
        [AoMember(0)]
        public Identity Target { get; set; }

        /// <summary>
        /// Equipped slots (Weapon/Armor/Implant/Social pages). Empty X3F1 when none.
        /// </summary>
        [AoMember(1, SerializeSize = ArraySizeType.X3F1)]
        public InventorySlot[] Items { get; set; }
    }
}
