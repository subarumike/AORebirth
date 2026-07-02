#region License

// Copyright (c) 2005-2014, CellAO Team
// 
// 
// All rights reserved.
// 
// 
// Redistribution and use in source and binary forms, with or without modification, are permitted provided that the following conditions are met:
// 
// 
//     * Redistributions of source code must retain the above copyright notice, this list of conditions and the following disclaimer.
//     * Redistributions in binary form must reproduce the above copyright notice, this list of conditions and the following disclaimer in the documentation and/or other materials provided with the distribution.
//     * Neither the name of the CellAO Team nor the names of its contributors may be used to endorse or promote products derived from this software without specific prior written permission.
// 
// 
// THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS
// "AS IS" AND ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT
// LIMITED TO, THE IMPLIED WARRANTIES OF MERCHANTABILITY AND FITNESS FOR
// A PARTICULAR PURPOSE ARE DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT OWNER OR
// CONTRIBUTORS BE LIABLE FOR ANY DIRECT, INDIRECT, INCIDENTAL, SPECIAL,
// EXEMPLARY, OR CONSEQUENTIAL DAMAGES (INCLUDING, BUT NOT LIMITED TO,
// PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES; LOSS OF USE, DATA, OR
// PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND ON ANY THEORY OF
// LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT (INCLUDING
// NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS
// SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
// 

#endregion

namespace ZoneEngine.Core.MessageHandlers
{
    #region Usings ...

    // TODO: Change Actions to something more suitable (maybe EntityAction?)
    using System;
    using AORebirth.Core.Components;
    using AORebirth.Core.Entities;
    using AORebirth.Core.Inventory;
    using AORebirth.Core.Items;
    using AORebirth.Core.Network;
    using AORebirth.Enums;
    using AORebirth.ObjectManager;

    using SmokeLounge.AOtomation.Messaging.GameData;
    using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;

    using ZoneEngine.Core.Packets;

    #endregion

    /// <summary>
    /// </summary>
    [MessageHandler(MessageHandlerDirection.All)]
    public class ContainerAddItemMessageHandler :
        BaseMessageHandler<ContainerAddItemMessage, ContainerAddItemMessageHandler>
    {
        #region Inbound

        /// <summary>
        /// </summary>
        /// <param name="message">
        /// </param>
        /// <param name="client">
        /// </param>
        protected override void Read(ContainerAddItemMessage message, IZoneClient client)
        {
            /* Container ID's:
             * 0065 Weaponpage
             * 0066 Armorpage
             * 0067 Implantpage
             * 0068 Inventory (places 64-93)
             * 0069 Bank
             * 006B Backpack
             * 006C KnuBot Trade Window
             * 006E Overflow window
             * 006F Trade Window
             * 0073 Socialpage
             * 0767 Shop Inventory
             * 0790 Playershop Inventory
             * DEAD Trade Window (incoming) It's bank now (when you put something into the bank)
             */

            if (client.Controller.Character.Playfield.TryLootCorpseItem(
                client.Controller.Character,
                message.SourceContainer,
                message.Target,
                message.TargetPlacement))
            {
                return;
            }

            InventoryContainerRuntimeService.Default.HandleContainerAddItem(client, message);
        }

        public void Send(ICharacter character, Identity sourceContainer, int slot)
        {
            this.Send(character, this.FillContainerAddItem(character, sourceContainer, slot));
        }

        private MessageDataFiller FillContainerAddItem(ICharacter character, Identity sourceContainer, int slot)
        {
            return x =>
            {
                x.Identity = character.Identity;
                // Live/clientless inventory evidence treats the first body identity as the source slot/container
                // and the second body identity as the target character/container.
                x.SourceContainer = sourceContainer;
                x.TargetPlacement = slot;
                x.Target = character.Identity;
                x.Unknown = 0;
            };
        }

        #endregion
    }
}
