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

    using System.Threading;

    using AORebirth.Core.Components;
    using AORebirth.Core.Entities;
    using AORebirth.Core.Items;
    using AORebirth.Core.Network;
    using AORebirth.Core.Playfields;
    using AORebirth.Database.Dao;
    using AORebirth.Enums;

    using SmokeLounge.AOtomation.Messaging.GameData;
    using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;

    using Utility;

    using ZoneEngine.Core.InternalMessages;
    using ZoneEngine.Core.Packets;
    using ZoneEngine.Core.PacketHandlers;

    #endregion

    /// <summary>
    /// </summary>
    [MessageHandler(MessageHandlerDirection.All)]
    public class CharacterActionMessageHandler :
        BaseMessageHandler<CharacterActionMessage, CharacterActionMessageHandler>
    {
        private const int CompatSitDownActionCode = 0x0000011E;
        private const int CompatStandUpActionCode = 0x00000057;
        private const int LiveDeathRespawnDelayMilliseconds = 2700;

        /// <summary>
        /// </summary>
        public CharacterActionMessageHandler()
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
        protected override void Read(CharacterActionMessage message, IZoneClient client)
        {
            LogUtil.Debug(DebugInfoDetail.NetworkMessages, "Reading CharacterActionMessage");
            client.Server.Info(
                client,
                "CharacterAction action={0}({1}) target={2} p1={3} p2={4} u1={5} u2={6}",
                message.Action,
                (int)message.Action,
                message.Target,
                message.Parameter1,
                message.Parameter2,
                message.Unknown1,
                message.Unknown2);

            if (this.TryHandleCompatPostureAction(message, client))
            {
                return;
            }

            // var actionNum = (int)characterAction.Action;
            // int unknown1 = message.Unknown1;
            // int args1 = message.Parameter1;
            // int nanoId = message.Parameter2;
            // short unknown2 = message.Unknown2;

            IdentityType targetIdentityType = message.Target.Type;

            switch (message.Action)
            {
                case CharacterActionType.CastNano:

                    // Cast nano
                    // CastNanoSpell

                    // TODO: This has to be delayed (Casting attack speed) and needs to move to some other part
                    // TODO: Check nanoskill requirements
                    // TODO: Lower current nano points/check if enough nano points

                    client.Controller.CastNano(message.Parameter2, message.Target);

                    break;

                    /* this is here to prevent server crash that is caused by search action if server doesn't reply if something is found or not */
                case CharacterActionType.Search:

                    // If action == search
                    /* Msg 110:136744723 = "No hidden objects found." */
                    // TODO: SEARCH!!
                    FeedbackMessageHandler.Default.Send(client.Controller.Character, 110, 136744723);
                    break;

                case CharacterActionType.InfoRequest:

                    // If action == Info Request
                    IInstancedEntity tPlayer = client.Controller.Character.Playfield.FindByIdentity(message.Target);

                    // TODO: Think of a new method to distinguish players from mobs (NPCFamily for example)
                    var tChar = tPlayer as Character;
                    if (tChar != null)
                    {
                        // Is it a Character object? (player and npcs)
                        CharacterInfoPacketMessageHandler.Default.Send(client.Controller.Character, tChar);
                    }
                    else
                    {
                        // TODO: NPC's
                        /*
                            var npc =
                                (NonPlayerCharacterClass)
                                FindDynel.FindDynelById(packet.Target);
                            if (npc != null)
                            {
                                var infoPacket = new PacketWriter();

                                 Start packet header
                                infoPacket.PushByte(0xDF);
                                infoPacket.PushByte(0xDF);
                                infoPacket.PushShort(10);
                                infoPacket.PushShort(1);
                                infoPacket.PushShort(0);
                                infoPacket.PushInt(3086);  sender (server ID)
                                infoPacket.PushInt(client.Character.Id.Instance);  receiver 
                                infoPacket.PushInt(0x4D38242E);  packet ID
                                infoPacket.PushIdentity(npc.Id);  affected identity
                                infoPacket.PushByte(0);  ?

                                 End packet header
                                infoPacket.PushByte(0x50);  npc's just have 0x50
                                infoPacket.PushByte(1);  esi_001?
                                infoPacket.PushByte((byte)npc.Stats.Profession.Value);  Profession
                                infoPacket.PushByte((byte)npc.Stats.Level.Value);  Level
                                infoPacket.PushByte((byte)npc.Stats.TitleLevel.Value);  Titlelevel
                                infoPacket.PushByte((byte)npc.Stats.VisualProfession.Value);  Visual Profession

                                infoPacket.PushShort(0);  no idea for npc's
                                infoPacket.PushUInt(npc.Stats.Health.Value);  Current Health (Health)
                                infoPacket.PushUInt(npc.Stats.Life.Value);  Max Health (Life)
                                infoPacket.PushInt(0);  BreedHostility?
                                infoPacket.PushUInt(0);  org ID
                                infoPacket.PushShort(0);
                                infoPacket.PushShort(0);
                                infoPacket.PushShort(0);
                                infoPacket.PushShort(0);
                                infoPacket.PushInt(0x499602d2);
                                infoPacket.PushInt(0x499602d2);
                                infoPacket.PushInt(0x499602d2);
                                var infoPacketA = infoPacket.Finish();
                                client.SendCompressed(infoPacketA);
                            }*/
                    }

                    break;

                case CharacterActionType.Logout:

                    // If action == Logout
                    this.ApplyLogoutSit(client);
                    this.SendOwnerLogoutSitAction(client);
                    this.SendStartLogout(client.Controller.Character);
                    this.SendLogoutMovementModeStat(client);
                    client.Controller.Character.StartLogoutTimer();

                    break;

                case CharacterActionType.StopLogout:

                    // If action == Stop Logout
                    this.ApplyStand(client);
                    break;

                case CharacterActionType.Die:
                {
                    client.Server.Info(
                        client,
                        "Player death action received. character={0} controller={1} playfield={2}",
                        client.Controller.Character.Identity,
                        client.Controller.Character.Controller == null
                            ? "null"
                            : client.Controller.Character.Controller.GetType().FullName,
                        client.Controller.Character.Playfield == null
                            ? "null"
                            : client.Controller.Character.Playfield.Identity.ToString());

                    Playfield playfield = client.Controller.Character.Playfield as Playfield;
                    if (playfield != null)
                    {
                        Thread.Sleep(LiveDeathRespawnDelayMilliseconds);
                        playfield.RespawnPlayer(client.Controller.Character);
                    }
                    else
                    {
                        LogUtil.Debug(
                            DebugInfoDetail.Network,
                            "Player death respawn deferred because current playfield is not a ZoneEngine playfield.");
                    }

                    break;
                }

                case CharacterActionType.StandUp:
                {
                    // If action == Stand
                    this.ApplyStand(client);

                    if (client.Controller.Character.InLogoutTimerPeriod())
                    {
                        this.Send(client.Controller.Character, this.StopLogout(client.Controller.Character), true);
                        client.Controller.Character.StopLogoutTimer();
                    }

                    // Send stand up packet, and cancel timer/send stop logout packet if timer is enabled
                    // ((ZoneClient)client).StandCancelLogout();
                }

                    break;

                case CharacterActionType.SitDown:
                {
                    if ((client.Controller.Character.MoveMode == MoveModes.Sit)
                        || (client.Controller.Character.MoveMode == MoveModes.Sleep)
                        || (client.Controller.Character.MoveMode == MoveModes.Lounge))
                    {
                        this.ApplyStand(client);
                    }
                    else
                    {
                        this.ApplySit(client);
                    }
                }

                    break;

                case CharacterActionType.ChangeAnimationAndStance:
                {
                    if (message.Parameter1 == 0)
                    {
                        this.ApplySit(client);
                    }
                    else
                    {
                        this.ApplyStand(client);
                    }
                }

                    break;

                case CharacterActionType.TeamKickMember:
                {
                    // Kick Team Member
                    client.Controller.TeamKickMember(message.Target);
                }

                    break;

                case CharacterActionType.LeaveTeam:
                {
                    // Leave Team
                    client.Controller.TeamLeave();
                }

                    break;
                case CharacterActionType.TransferLeader:
                {
                    // Transfer Team Leadership
                    client.Controller.TransferTeamLeadership(message.Target);
                }

                    break;

                case CharacterActionType.TeamRequestInvite:
                {
                    // Team Join Request
                    // Send Team Invite Request To Target Player
                    client.Controller.TeamJoinRequest(message.Target);
                }

                    break;
                case CharacterActionType.TeamRequestReply:
                {
                    client.Controller.TeamJoinReply(message.Parameter1 != 0, message.Target);
                }

                    break;

                case CharacterActionType.DeleteItem: // Remove/Delete item
                    ItemDao.Instance.Delete(
                        new
                        {
                            containertype = (int)targetIdentityType,
                            containerinstance = client.Controller.Character.Identity.Instance,
                            Id = message.Target.Instance
                        });
                    client.Controller.Character.BaseInventory.RemoveItem(
                        (int)targetIdentityType,
                        message.Target.Instance);

                    this.AcknowledgeDelete(client.Controller.Character, message);
                    break;

                case CharacterActionType.Split: // Split?
                    IItem it =
                        client.Controller.Character.BaseInventory.Pages[(int)targetIdentityType][message.Target.Instance
                            ];
                    it.MultipleCount -= message.Parameter2;
                    Item newItem = new Item(it.Quality, it.LowID, it.HighID);
                    newItem.MultipleCount = message.Parameter2;

                    client.Controller.Character.BaseInventory.Pages[(int)targetIdentityType].Add(
                        client.Controller.Character.BaseInventory.Pages[(int)targetIdentityType].FindFreeSlot(),
                        newItem);
                    client.Controller.Character.BaseInventory.Pages[(int)targetIdentityType].Write();

                    // Does it need to Acknowledge? Need to check that - Algorithman
                    break;

                case CharacterActionType.AcceptTeamRequest:
                    client.Controller.Character.BaseInventory.Pages[(int)targetIdentityType][message.Target.Instance]
                        .MultipleCount +=
                        client.Controller.Character.BaseInventory.Pages[(int)targetIdentityType][message.Parameter2]
                            .MultipleCount;
                    client.Controller.Character.BaseInventory.Pages[(int)targetIdentityType].Remove(message.Parameter2);
                    client.Controller.Character.BaseInventory.Pages[(int)targetIdentityType].Write();
                    this.Acknowledge(client.Controller.Character, message);
                    break;

                    // ###################################################################################
                    // Spandexpants: This is all i have done so far as to make sneak turn on and off, 
                    // currently i cannot find a missing packet or link which tells the server the player
                    // has stopped sneaking, hidden packet or something, will come back to later.
                    // ###################################################################################

                    // Sneak Packet Received
                case CharacterActionType.StartSneak:

                    // TODO: IF SNEAKING IS ALLOWED RUN THIS CODE.
                    // TODO: Insert perception checks on receiving characters/mobs and then dont send to playfield
                    // Send Action 162 : Enable Sneak

                    this.Send(client.Controller.Character, this.Sneak(client.Controller.Character), true);

                    // End of Enable sneak
                    // TODO: IF SNEAKING IS NOT ALLOWED SEND REJECTION PACKET
                    break;

                case CharacterActionType.UseItemOnItem:
                {
                    Identity item1 = message.Target;
                    var item2 = new Identity { Type = (IdentityType)message.Parameter1, Instance = message.Parameter2 };

                    client.Controller.Character.TradeSkillSource = new TradeSkillInfo(
                        0,
                        (int)item1.Type,
                        item1.Instance);
                    client.Controller.Character.TradeSkillTarget = new TradeSkillInfo(
                        1,
                        (int)item2.Type,
                        item2.Instance);
                    TradeSkillReceiver.TradeSkillBuildPressed(client, 300);

                    break;
                }

                case CharacterActionType.ChangeVisualFlag:
                {
                    client.Controller.Character.Stats[StatIds.visualflags].Value = message.Parameter2;

                    ChatTextMessageHandler.Default.Send(
                        client.Controller.Character,
                        "Setting Visual Flag to " + message.Parameter2);
                    AppearanceUpdateMessageHandler.Default.Send(client.Controller.Character);
                }

                    break;
                case CharacterActionType.TradeskillSourceChanged:
                    TradeSkillReceiver.TradeSkillSourceChanged(client, message.Parameter1, message.Parameter2);
                    break;

                case CharacterActionType.TradeskillTargetChanged:
                    TradeSkillReceiver.TradeSkillTargetChanged(client, message.Parameter1, message.Parameter2);
                    break;

                case CharacterActionType.TradeskillBuildPressed:
                    TradeSkillReceiver.TradeSkillBuildPressed(client, message.Target.Instance);
                    break;

                default:
                {
                    // unkown
                    client.Controller.Character.Playfield.Announce(message);
                }

                    break;
            }
        }

        #endregion

        #region Outbound

        /// <summary>
        /// </summary>
        /// <param name="character">
        /// </param>
        /// <param name="actionType">
        /// </param>
        /// <param name="target">
        /// </param>
        /// <param name="unknown1">
        /// </param>
        /// <param name="unknown2">
        /// </param>
        public void FinishNanoCasting(
            ICharacter character,
            CharacterActionType actionType,
            Identity target,
            int unknown1,
            int unknown2)
        {
            this.Send(character, this.ConstructFinishNanoCasting(character, target, unknown1, unknown2), true);
        }

        /// <summary>
        /// </summary>
        /// <param name="character">
        /// </param>
        /// <param name="target">
        /// </param>
        /// <param name="unknown1">
        /// </param>
        /// <param name="unknown2">
        /// </param>
        /// <returns>
        /// </returns>
        private MessageDataFiller ConstructFinishNanoCasting(
            ICharacter character,
            Identity target,
            int unknown1,
            int unknown2)
        {
            return x =>
            {
                x.Identity = character.Identity;
                x.Unknown = 0x00;
                x.Action = CharacterActionType.FinishNanoCasting;
                x.Unknown1 = 0x00000000;
                x.Target = Identity.None;
                x.Parameter1 = unknown1;
                x.Parameter2 = unknown2;
                x.Unknown2 = 0x0000;
            };
        }

        /// <summary>
        /// </summary>
        /// <param name="character">
        /// </param>
        /// <param name="target">
        /// </param>
        /// <param name="unknown1">
        /// </param>
        /// <param name="duration">
        /// </param>
        /// <returns>
        /// </returns>
        private MessageDataFiller ConstructSetNanoDuration(
            ICharacter character,
            Identity target,
            int unknown1,
            int duration = 0x249F0)
        {
            return x =>
            {
                x.Identity = target;
                x.Unknown = 0x00;
                x.Action = CharacterActionType.SetNanoDuration;
                x.Unknown1 = 0x00000000;
                x.Target = new Identity { Type = IdentityType.NanoProgram, Instance = unknown1 };
                x.Parameter1 = character.Identity.Instance;
                x.Parameter2 = duration; // duration
                x.Unknown2 = 0x0000;
            };
        }

        /// <summary>
        /// </summary>
        /// <param name="character">
        /// </param>
        /// <param name="target">
        /// </param>
        /// <param name="unknown1">
        /// </param>
        /// <param name="duration">
        /// </param>
        public void SetNanoDuration(ICharacter character, Identity target, int unknown1, int duration = 0x249F0)
        {
            this.Send(character, this.ConstructSetNanoDuration(character, target, unknown1, duration));
        }

        private MessageDataFiller SkillUnavailableAction(ICharacter character, int statId, int durationSeconds)
        {
            return x =>
            {
                x.Identity = character.Identity;
                x.Unknown = 0x00;
                x.Action = CharacterActionType.SpecialUnavailable;
                x.Unknown1 = 0x00000000;
                x.Target = Identity.None;
                x.Parameter1 = statId;
                x.Parameter2 = durationSeconds;
                x.Unknown2 = 0x0000;
            };
        }

        public void SendSkillUnavailable(ICharacter character, int statId, int durationSeconds)
        {
            this.Send(character, this.SkillUnavailableAction(character, statId, durationSeconds));
        }

        private MessageDataFiller SkillAvailableAction(ICharacter character, int statId)
        {
            return x =>
            {
                x.Identity = character.Identity;
                x.Unknown = 0x00;
                x.Action = CharacterActionType.SpecialAvailable;
                x.Unknown1 = 0x00000000;
                x.Target = Identity.None;
                x.Parameter1 = 0;
                x.Parameter2 = statId;
                x.Unknown2 = 0x0000;
            };
        }

        public void SendSkillAvailable(ICharacter character, int statId)
        {
            this.Send(character, this.SkillAvailableAction(character, statId));
        }

        /// <summary>
        /// </summary>
        /// <param name="character">
        /// </param>
        /// <param name="container">
        /// </param>
        /// <param name="placement">
        /// </param>
        /// <returns>
        /// </returns>
        private MessageDataFiller DeleteItemAction(ICharacter character, int container, int placement)
        {
            return x =>
            {
                x.Identity = character.Identity;
                x.Action = CharacterActionType.DeleteItem;
                x.Target = new Identity() { Type = (IdentityType)container, Instance = placement };
            };
        }

        /// <summary>
        /// </summary>
        /// <param name="character">
        /// </param>
        /// <param name="container">
        /// </param>
        /// <param name="placement">
        /// </param>
        public void SendDeleteItem(ICharacter character, int container, int placement)
        {
            this.Send(character, this.DeleteItemAction(character, container, placement));
        }

        /// <summary>
        /// </summary>
        /// <param name="character">
        /// </param>
        /// <returns>
        /// </returns>
        private MessageDataFiller Sneak(ICharacter character)
        {
            return x =>
            {
                x.Identity = character.Identity;
                x.Unknown = 0x00;
                x.Action = CharacterActionType.StartedSneaking;
                x.Unknown1 = 0x00000000;
                x.Target = Identity.None;
                x.Parameter1 = 0;
                x.Parameter2 = 0;
                x.Unknown2 = 0;
            };
        }

        /// <summary>
        /// </summary>
        /// <param name="character">
        /// </param>
        /// <param name="message">
        /// </param>
        private void Acknowledge(ICharacter character, CharacterActionMessage message)
        {
            this.Send(character, this.Reply(message));
        }

        /// <summary>
        /// </summary>
        /// <param name="message">
        /// </param>
        /// <returns>
        /// </returns>
        private MessageDataFiller Reply(CharacterActionMessage message)
        {
            return x =>
            {
                x.Action = message.Action;
                x.Identity = message.Identity;
                x.Parameter1 = message.Parameter1;
                x.Parameter2 = message.Parameter2;
                x.Target = message.Target;
                x.Unknown1 = message.Unknown1;
                x.Unknown2 = message.Unknown2;
                x.Unknown = message.Unknown;
            };
        }

        private void AcknowledgeDelete(ICharacter character, CharacterActionMessage message)
        {
            this.Send(character, this.ReplyWithoutParameters(message));
        }

        private MessageDataFiller ReplyWithoutParameters(CharacterActionMessage message)
        {
            return x =>
            {
                x.Action = message.Action;
                x.Identity = message.Identity;
                x.Parameter1 = 0;
                x.Parameter2 = 0;
                x.Target = message.Target;
                x.Unknown1 = message.Unknown1;
                x.Unknown2 = message.Unknown2;
                x.Unknown = message.Unknown;
            };
        }

        private bool TryHandleCompatPostureAction(CharacterActionMessage message, IZoneClient client)
        {
            int action = (int)message.Action;
            bool looksLikeSit = action == CompatSitDownActionCode
                                || message.Parameter1 == CompatSitDownActionCode
                                || message.Parameter2 == CompatSitDownActionCode;
            bool looksLikeStand = action == CompatStandUpActionCode
                                  || message.Parameter1 == CompatStandUpActionCode
                                  || message.Parameter2 == CompatStandUpActionCode;

            if (looksLikeSit)
            {
                this.ApplySit(client);
                return true;
            }

            if (looksLikeStand)
            {
                this.ApplyStand(client);
                return true;
            }

            return false;
        }

        private void ApplySit(IZoneClient client)
        {
            ICharacter character = client.Controller.Character;
            character.EnterLogoutSitPosture();
            client.Controller.State = CharacterState.Idle;
            this.SendPostureMove(character, 30);
            SimpleCharFullUpdate.SendToPlayfield(client.Controller.Client);
        }

        private void ApplyLogoutSit(IZoneClient client)
        {
            ICharacter character = client.Controller.Character;
            character.EnterLogoutSitPosture();
            client.Controller.State = CharacterState.Idle;

            CharDCMoveMessage postureUpdate = this.CreatePostureMove(character, 30);
            SimpleCharFullUpdateMessage fullUpdate = SimpleCharFullUpdate.ConstructMessage((Character)character);

            client.SendCompressed(postureUpdate);
            client.SendCompressed(fullUpdate);

            character.Playfield.AnnounceOthers(postureUpdate, character.Identity);
            character.Playfield.AnnounceOthers(fullUpdate, character.Identity);
        }

        private void SendOwnerLogoutSitAction(IZoneClient client)
        {
            ICharacter character = client.Controller.Character;

            client.SendCompressed(
                new CharacterActionMessage
                {
                    Identity = character.Identity,
                    Unknown = 0x00,
                    Action = CharacterActionType.ChangeAnimationAndStance,
                    Unknown1 = 0,
                    Target = Identity.None,
                    Parameter1 = 0,
                    Parameter2 = 0,
                    Unknown2 = 0
                });
        }

        private void ApplyStand(IZoneClient client)
        {
            ICharacter character = client.Controller.Character;
            character.UpdateMoveType(37);
            character.Playfield.Announce(
                new CharacterActionMessage
                {
                    Identity = character.Identity,
                    Unknown = 0x00,
                    Action = CharacterActionType.StandUp,
                    Unknown1 = 0,
                    Target = Identity.None,
                    Parameter1 = 0,
                    Parameter2 = 0,
                    Unknown2 = 0
                });

            this.SendPostureMove(character, 37);

            if (character.InLogoutTimerPeriod())
            {
                this.SendStopLogout(character);
                this.Send(character, this.StopLogout(character), true);
                character.StopLogoutTimer();
            }
        }

        private void SendStartLogout(ICharacter character)
        {
            character.Controller.Client.SendCompressed(
                new StartLogoutMessage
                    {
                        Identity = character.Identity
                    });
        }

        private void SendStopLogout(ICharacter character)
        {
            character.Controller.Client.SendCompressed(
                new StopLogoutMessage
                    {
                        Identity = character.Identity
                    });
        }

        private void SendLogoutMovementModeStat(IZoneClient client)
        {
            ICharacter character = client.Controller.Character;

            client.SendCompressed(
                new StatMessage
                {
                    Identity = character.Identity,
                    Unknown = 1,
                    Stats =
                        new[]
                        {
                            new GameTuple<CharacterStat, uint>
                            {
                                Value1 = (CharacterStat)StatIds.currentmovementmode,
                                Value2 = (uint)character.Stats[StatIds.currentmovementmode].Value
                            }
                        }
                });
        }

        private void SendPostureMove(ICharacter character, byte moveType)
        {
            CharDCMoveMessage postureUpdate = this.CreatePostureMove(character, moveType);

            character.Playfield.Publish(new IMSendAOtomationMessageToPlayfield { Body = postureUpdate });
        }

        private CharDCMoveMessage CreatePostureMove(ICharacter character, byte moveType)
        {
            return new CharDCMoveMessage
                   {
                       Identity = character.Identity,
                       Unknown = 0x00,
                       MoveType = moveType,
                       Heading =
                           new Quaternion
                           {
                               X = character.Heading.xf,
                               Y = character.Heading.yf,
                               Z = character.Heading.zf,
                               W = character.Heading.wf
                           },
                       Coordinates =
                           new Vector3
                           {
                               X = character.RawCoordinates.X,
                               Y = character.RawCoordinates.Y,
                               Z = character.RawCoordinates.Z
                           },
                       Unknown1 = 0,
                       Unknown2 = 0,
                       Unknown3 = 0
                   };
        }

        /// <summary>
        /// </summary>
        /// <param name="character">
        /// </param>
        /// <returns>
        /// </returns>
        private MessageDataFiller StopLogout(ICharacter character)
        {
            return x =>
            {
                x.Action = CharacterActionType.StopLogout;
                x.Identity = character.Identity;
            };
        }

        #endregion
    }
}
