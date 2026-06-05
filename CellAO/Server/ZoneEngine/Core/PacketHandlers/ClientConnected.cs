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

namespace ZoneEngine.Core.PacketHandlers
{
    #region Usings ...

    using System;
    using System.Text;

    using CellAO.Core.Entities;
    using CellAO.Core.Network;
    using CellAO.Core.NPCHandler;
    using CellAO.Core.Playfields;
    using CellAO.Core.Vector;
    using CellAO.Enums;
    using CellAO.ObjectManager;

    using SmokeLounge.AOtomation.Messaging.GameData;
    using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;

    using ZoneEngine.Core;
    using ZoneEngine.Core.Controllers;
    using ZoneEngine.Core.InternalMessages;
    using ZoneEngine.Core.MessageHandlers;
    using ZoneEngine.Core.Packets;
    using ZoneEngine.Script;

    using Utility;

    #endregion

    /// <summary>
    /// </summary>
    public class ClientConnected
    {
        #region Public Methods and Operators

        /// <summary>
        /// </summary>
        /// <param name="str">
        /// </param>
        /// <returns>
        /// </returns>
        public static byte[] StrToByteArray(string str)
        {
            var encoding = new ASCIIEncoding();
            return encoding.GetBytes(str);
        }

        /// <summary>
        /// </summary>
        /// <param name="charID">
        /// </param>
        /// <param name="client">
        /// </param>
        public void Read(int charID, ZoneClient client)
        {
            // Don't edit anything in this region
            // unless you are 300% sure you know what you're doing

            // Character is created and read when Client connects in Client.cs->CreateCharacter
            // client.CreateCharacter(charID);
            client.Server.Info(
                client,
                "Client connected. ID: {0} IP: {1} Character name: {2}",
                client.Controller.Character.Identity.Instance,
                client.ClientAddress,
                client.Controller.Character.Name);

            // now we have to start sending packets like 
            // character stats, inventory, playfield info
            // and so on. I will put some packets here just 
            // to get us in game. We have to start moving
            // these packets somewhere else and make packet 
            // builders instead of sending (half) hardcoded
            // packets.

            /* send chat server info to client */
            ChatServerInfoMessageHandler.Default.Send(client.Controller.Character);

            /* send playfield info to client */
            PlayfieldAnarchyFMessageHandler.Default.Send(client.Controller.Character);


            foreach (
Vendor vendor in
Pool.Instance.GetAll<Vendor>(
client.Controller.Character.Playfield.Identity,
(int)IdentityType.VendingMachine))
            {
                VendingMachineFullUpdateMessageHandler.Default.Send(client.Controller.Character, vendor);
            }

            // Debug-only combat test mob spawns are disabled on normal login.
            // Use /command spawn / spawnleet for explicit test spawns.

            var sendSCFUs = new IMSendPlayerSCFUs { toClient = client };
            ((Playfield)client.Playfield).SendSCFUsToClient(sendSCFUs);

            /* Live login advertises the character as socially/action-ready. */
            client.Controller.Character.Stats[StatIds.socialstatus].BaseValue = 4;

            // Stat.SendDirect(client, 521, 0, false);

            var identity = new Identity { Type = IdentityType.CanbeAffected, Instance = charID };

            var gameTimeMessage = new GameTimeMessage
                                  {
                                      Identity = identity,
                                      Unknown1 = 30024.0f,
                                      Unknown3 = 185408,
                                      Unknown4 = 80183.3125f
                                  };
            client.SendCompressed(gameTimeMessage);

            InitializeActionableState(client);
            CharacterActionMessageHandler.Default.SendSkillAvailable(
                client.Controller.Character,
                (int)StatIds.treatment);

            client.SendCompressed(
                new StatMessage
                {
                    Identity = identity,
                    Stats =
                        new[]
                        {
                            new GameTuple<CharacterStat, uint>
                            {
                                Value1 = CharacterStat.SocialStatus,
                                Value2 = (uint)client.Controller.Character.Stats[StatIds.socialstatus].Value
                            }
                        }
                });


            /* set SocialStatus to 0 */
            // Stat.SendDirect(client, 521, 0, false);

            /* again */
            // Stat.SendDirect(client, 521, 0, false);

            /* visual */
            SimpleCharFullUpdate.SendToPlayfield(client);

            /* inventory, items and all that */
            Packets.WeaponItemFullUpdate.SendWeaponDefinitions(client.Controller.Character);
            FullCharacterMessageHandler.Default.Send(client.Controller.Character);

            var specials = new[]
                           {
                               new SpecialAttack
                               {
                                   Unknown1 = 0x0000AAC0,
                                   Unknown2 = 0x00023569,
                                   Unknown3 = 0x00000064,
                                   Unknown4 = "MAAT"
                               },
                               new SpecialAttack
                               {
                                   Unknown1 = 0x0000A431,
                                   Unknown2 = 0x0000A430,
                                   Unknown3 = 0x00000090,
                                   Unknown4 = "DIIT"
                               },
                               new SpecialAttack
                               {
                                   Unknown1 = 0x00011294,
                                   Unknown2 = 0x00011295,
                                   Unknown3 = 0x0000008E,
                                   Unknown4 = "BRAW"
                               }
                           };
            var specialAttackWeaponMessage = new SpecialAttackWeaponMessage { Identity = identity, Specials = specials };

            client.SendCompressed(specialAttackWeaponMessage);

            // done


            // spawn all active monsters to client
            // TODO: Implement NonPlayerCharacterHandler
            // NonPlayerCharacterHandler.SpawnMonstersInPlayfieldToClient(client, client.Character.PlayField);

            // TODO: Implement VendorHandler
            // if (VendorHandler.GetNumberofVendorsinPlayfield(client.Character.PlayField) > 0)
            // {
            // Shops 
            // VendorHandler.GetVendorsInPF(client);
            // }

            // Weapon item full updates are sent above immediately after full character sync.

            // TODO: create a better alternative to ProcessTimers
            // client.Character.ProcessTimers(DateTime.Now + TimeSpan.FromMilliseconds(200));
            client.Controller.Character.CalculateSkills();
            ClientMoveItemToInventoryMessageHandler.EnsureWeaponVisualMeshes(client.Controller.Character, false);

            AppearanceUpdateMessageHandler.Default.Send(client.Controller.Character);
            CompleteDeathRespawnCharInPlay(client);

            // done, so we call a hook.
            // Call all OnConnect script Methods
            ScriptCompiler.Instance.CallMethod("OnConnect", client.Controller.Character);

            // Timers are allowed to update client stats now.
            client.Controller.Character.DoNotDoTimers = false;
        }

        private static void CompleteDeathRespawnCharInPlay(ZoneClient client)
        {
            ICharacter character = client.Controller.Character;
            if (character.Stats[StatIds.health].Value <= 0 || character.Stats[StatIds.deadtimer].Value != 75)
            {
                return;
            }

            character.Starting = false;
            client.SendCompressed(new CharInPlayMessage { Identity = character.Identity, Unknown = 0x00 });
            LogUtil.Debug(
                DebugInfoDetail.Network,
                string.Format(
                    "Death respawn CharInPlay completion sent target={0} unknown=0 hp={1}/{2} deadTimer={3}",
                    character.Identity,
                    character.Stats[StatIds.health].Value,
                    character.Stats[StatIds.life].Value,
                    character.Stats[StatIds.deadtimer].Value));
        }

        private static void InitializeActionableState(ZoneClient client)
        {
            // Match the live client's login baseline so the client enables its built-in normal action table.
            SetStat(client, StatIds.state, 0);
            SetStat(client, StatIds.currentmovementmode, (int)MoveModes.Run);
            SetStat(client, StatIds.prevmovementmode, (int)MoveModes.Run);
            SetStat(client, StatIds.currentstate, 0);
            SetStat(client, StatIds.waitstate, 0);
            SetStat(client, StatIds.socialstatus, 4);
            SetStat(client, StatIds.specialcondition, 3);
            SetStat(client, StatIds.actioncategory, 0);
        }

        private static void SetStat(ZoneClient client, StatIds stat, int value)
        {
            client.Controller.Character.Stats[stat].Value = value;
            client.Controller.Character.Stats[stat].BaseValue = (uint)value;
        }

        #endregion
    }
}
