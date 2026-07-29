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

    using AORebirth.Core.Components;
    using AORebirth.Core.Entities;
    using AORebirth.Core.Network;
    using AORebirth.Interfaces;

    using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;

    using Utility;

    using ZoneEngine.Core;

    #endregion

    /// <summary>
    /// </summary>
    [MessageHandler(MessageHandlerDirection.InboundOnly)]
    public class LookAtMessageHandler : BaseMessageHandler<LookAtMessage, LookAtMessageHandler>
    {
        /// <summary>
        /// </summary>
        public LookAtMessageHandler()
        {
            this.UpdateCharacterStatsOnReceive = true;
        }

        #region Inbound

        /// <summary>
        /// </summary>
        /// <param name="message">
        /// </param>
        /// <param name="client">
        /// </param>
        protected override void Read(LookAtMessage message, IZoneClient client)
        {
            LogUtil.Debug(
                DebugInfoDetail.Error,
                string.Format(
                    "LookAt target={0} returnInfo={1}",
                    message.Target.ToString(true),
                    message.ReturnInfo));

            PetCommandService.OnOwnerLookAtTarget(client.Controller.Character, message.Target);

            // Do NOT complete missions on LookAt — targeting a mob for combat was wiping the journal.
            // Finish is Kill-target death (and later FindItem / Repair once those objectives exist).

            if (client.Controller.LookAt(message.Target))
            {
                PetCommandService.ResolveFriendlyHealTargetForSelection(
                    client.Controller.Character,
                    message.Target);

                if (message.ReturnInfo != 1)
                {
                    CharacterInfoPacketMessageHandler.Default.Send(
                        client.Controller.Character,
                        message.Target);
                }
            }
            else
            {
                // Cross-zone LFT: LookAt finds no local dynel. Seed name so Invite is not NoName.
                var remote = LftInviteClientPresence.ResolveOnlinePlayer(
                    client.Controller.Character,
                    message.Target);
                if (remote != null)
                {
                    string armedName;
                    LftInviteArm.TryGetArmedName(client.Controller.Character, message.Target, out armedName);
                    LftInviteClientPresence.SeedForInviteLookup(
                        client.Controller.Character,
                        remote,
                        armedName);
                    if (message.ReturnInfo != 1)
                    {
                        CharacterInfoPacketMessageHandler.Default.Send(
                            client.Controller.Character,
                            remote);
                    }
                }
            }
        }

        #endregion
    }
}
