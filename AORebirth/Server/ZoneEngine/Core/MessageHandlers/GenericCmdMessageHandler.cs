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

    // TODO: Make this to EntityEnvent or something like this
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Linq;
    using System.Threading;

    using AORebirth.Core.Components;
    using AORebirth.Core.Entities;
    using AORebirth.Core.Events;
    using AORebirth.Core.Functions;
    using AORebirth.Core.Inventory;
    using AORebirth.Core.Items;
    using AORebirth.Core.Network;
    using AORebirth.Core.Requirements;
    using AORebirth.Core.Statels;
    using AORebirth.Core.Vector;
    using AORebirth.Database.Dao;
    using AORebirth.Enums;
    using AORebirth.Interfaces;
    using AORebirth.ObjectManager;

    using SmokeLounge.AOtomation.Messaging.GameData;
    using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;

    using ZoneEngine.Core.Arete.Quests;
    using ZoneEngine.Core.Playfields;

    #endregion

    /// <summary>
    /// </summary>
    [MessageHandler(MessageHandlerDirection.All)]
    public class GenericCmdMessageHandler : BaseMessageHandler<GenericCmdMessage, GenericCmdMessageHandler>
    {
        #region Inbound

        /// <summary>
        /// </summary>
        /// <param name="message">
        /// </param>
        /// <param name="client">
        /// </param>
        /// <exception cref="NullReferenceException">
        /// </exception>
        protected override void Read(GenericCmdMessage message, IZoneClient client)
        {
            Identity target = message.Target != null && message.Target.Length > 0
                                  ? message.Target[0]
                                  : Identity.None;
            client.Server.Info(
                client,
                "GenericCmd action={0}({1}) temp1={2} count={3} temp4={4} user={5} target={6}",
                message.Action,
                (int)message.Action,
                message.Temp1,
                message.Count,
                message.Temp4,
                message.User,
                target);

            switch (message.Action)
            {
                case GenericCmdAction.Get:
                    break;
                case GenericCmdAction.Drop:
                    break;
                case GenericCmdAction.Use:
                    if (RexB18DBoxProgressTracker.TryObserveBoxUse(
                        client.Controller.Character,
                        target))
                    {
                        this.Acknowledge(client.Controller.Character, message);
                    }
                    else if (InventoryContainerInteractionHandler.Default.TryHandleUse(client, message, target))
                    {
                        break;
                    }
                    else if (GuestKeyGeneratorInteractionHandler.Default.TryHandleUse(client, message, target))
                    {
                        break;
                    }
                    else if (CityControllerInteractionHandler.Default.TryHandleUse(client, message, target))
                    {
                        break;
                    }
                    else if (CorpseInteractionHandler.Default.TryHandleUse(client, message, target))
                    {
                        break;
                    }
                    else if (GridTerminalInteractionHandler.Default.TryHandleCapturedUse(client, target))
                    {
                        break;
                    }
                    else if (GridTerminalInteractionHandler.Default.TryHandleGridEnterUse(client, target))
                    {
                        break;
                    }
                    else if (SurgeryClinicInteractionHandler.Default.TryHandleUse(client, message, target))
                    {
                        break;
                    }
                    else if (StaticDynelInteractionHandler.Default.TryHandleUse(client, message, target))
                    {
                        break;
                    }
                    else if (StatelInteractionHandler.Default.TryHandleUse(client, message, target))
                    {
                        break;
                    }

                    break;
                case GenericCmdAction.UseItemOnItem:
                    UseItemOnItemInteractionHandler.Default.TryHandle(client, message);
                    break;
            }
        }

        #endregion

        #region Outbound

        /// <summary>
        /// </summary>
        /// <param name="character">
        /// </param>
        /// <param name="message">
        /// </param>
        /// <param name="announceToPlayfield">
        /// </param>
        public void Acknowledge(ICharacter character, GenericCmdMessage message, bool announceToPlayfield = false)
        {
            this.Send(character, this.Reply(character, message), announceToPlayfield);
        }

        public void AcknowledgeDenied(
            ICharacter character,
            GenericCmdMessage message,
            bool announceToPlayfield = false)
        {
            this.Send(character, this.Reply(character, message, Identity.None, message.Temp4, 2), announceToPlayfield);
        }

        public void AcknowledgeWithTarget(
            ICharacter character,
            GenericCmdMessage message,
            Identity target,
            bool announceToPlayfield = false)
        {
            this.Send(character, this.Reply(character, message, target), announceToPlayfield);
        }

        public void AcknowledgeCorpseUse(
            ICharacter character,
            GenericCmdMessage message,
            Identity corpse,
            bool announceToPlayfield = false)
        {
            this.Send(character, this.Reply(character, message, corpse, 1), announceToPlayfield);
        }

        /// <summary>
        /// </summary>
        /// <param name="character">
        /// </param>
        /// <param name="message">
        /// </param>
        /// <returns>
        /// </returns>
        private MessageDataFiller Reply(ICharacter character, GenericCmdMessage message)
        {
            return this.Reply(character, message, Identity.None);
        }

        private MessageDataFiller Reply(ICharacter character, GenericCmdMessage message, Identity targetOverride)
        {
            return this.Reply(character, message, targetOverride, message.Temp4);
        }

        private MessageDataFiller Reply(
            ICharacter character,
            GenericCmdMessage message,
            Identity targetOverride,
            int temp4)
        {
            return this.Reply(character, message, targetOverride, temp4, 1);
        }

        private MessageDataFiller Reply(
            ICharacter character,
            GenericCmdMessage message,
            Identity targetOverride,
            int temp4,
            int temp1)
        {
            return x =>
            {
                Identity[] targets = message.Target.ToList().ToArray();
                if (targetOverride != Identity.None && targets.Length > 0)
                {
                    targets[0] = targetOverride;
                }

                x.Identity = message.Identity;
                x.N3MessageType = message.N3MessageType;
                x.Target = targets;
                x.Temp1 = temp1;
                x.Count = message.Count;
                x.Action = message.Action;
                x.Temp4 = temp4;
                x.User = message.User;
                x.Unknown = 0;
            };
        }

        #endregion

    }

}
