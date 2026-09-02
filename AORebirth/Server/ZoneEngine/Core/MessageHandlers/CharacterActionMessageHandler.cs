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

    using System.Linq;
    using System.Threading;

    using AORebirth.Core.Components;
    using AORebirth.Core.Entities;
    using AORebirth.Core.Network;
    using AORebirth.Core.Playfields;
    using AORebirth.Enums;
    using AORebirth.Interfaces;
    using AORebirth.ObjectManager;

    using SmokeLounge.AOtomation.Messaging.GameData;
    using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;

    using Utility;

    using ZoneEngine.Core;
    using ZoneEngine.Core.Controllers;
    using ZoneEngine.Core.InternalMessages;
    using ZoneEngine.Core.Packets;
    using ZoneEngine.Core.PacketHandlers;
    using ZoneEngine.Core.Perks;
    using ZoneEngine.Core.Playfields;

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
                {
                    var zoneClient = client as ZoneClient;
                    if (zoneClient != null
                        && AORebirth.Core.Playfields.NascenceDungeon1SearchRuntime.TryHandleSearch(
                            zoneClient,
                            client.Controller.Character))
                    {
                        break;
                    }

                    if (zoneClient != null
                        && AORebirth.Core.Playfields.NascenceDungeon2SearchRuntime.TryHandleSearch(
                            zoneClient,
                            client.Controller.Character))
                    {
                        break;
                    }

                    if (zoneClient != null
                        && AORebirth.Core.Playfields.NascenceDungeon3SearchRuntime.TryHandleSearch(
                            zoneClient,
                            client.Controller.Character))
                    {
                        break;
                    }

                    if (zoneClient != null
                        && AORebirth.Core.Playfields.NascenceDungeon4SearchRuntime.TryHandleSearch(
                            zoneClient,
                            client.Controller.Character))
                    {
                        break;
                    }

                    /* Msg 110:136744723 = "No hidden objects found." */
                    FeedbackMessageHandler.Default.Send(client.Controller.Character, 110, 136744723);
                    break;
                }

                case CharacterActionType.InfoRequest:
                {
                    ICharacter infoTarget = LftInviteClientPresence.ResolveOnlinePlayer(
                        client.Controller.Character,
                        message.Target);
                    if (infoTarget != null)
                    {
                        string armedName;
                        LftInviteArm.TryGetArmedName(
                            client.Controller.Character,
                            message.Target,
                            out armedName);
                        LftInviteClientPresence.SeedForInviteLookup(
                            client.Controller.Character,
                            infoTarget,
                            armedName);
                        LftInviteClientPresence.WireInviteLevelStatToViewer(
                            client.Controller.Character,
                            infoTarget);

                        CharacterInfoPacketMessageHandler.Default.Send(
                            client.Controller.Character,
                            infoTarget);
                    }
                    else
                    {
                        CharacterInfoPacketMessageHandler.Default.Send(
                            client.Controller.Character,
                            message.Target);
                    }

                    ZoneEngine.Core.Missions.MissionFindPersonService.TryHandleInfoRequest(
                        client,
                        message.Target);
                }

                    break;

                case CharacterActionType.Inspect:

                    // Character Info → Inspect Equipment (capture 20260719-182611).
                    IInstancedEntity inspectEntity =
                        client.Controller.Character.Playfield.FindByIdentity(message.Target);
                    var inspectTarget = inspectEntity as Character;
                    if (inspectTarget != null)
                    {
                        InspectMessageHandler.Default.Send(client.Controller.Character, inspectTarget);
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
                    // Capture 20260806-063619: OUT Action=263 Target=self is Daily Login claim,
                    // not posture. Posture uses StandUp / ChangeAnimationAndStance.
                    DailyLoginRewardRuntime.TryHandleClaim(client.Controller.Character);
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
                    // Live TooHigh 20260815-194517: OUT 0x1A p2=0 → IN 0xA9; Yes = p2=1.
                    // Live TooLow 20260815-222131: OUT 0x1A p2=0 → IN 0xA8; Yes = p2=1.
                    int charId = 0;
                    if (message.Target.Type == IdentityType.CanbeAffected && message.Target.Instance != 0)
                    {
                        charId = message.Target.Instance;
                    }
                    else if (message.Parameter2 > 1)
                    {
                        charId = message.Parameter2;
                    }
                    else if (message.Parameter1 > 1)
                    {
                        charId = message.Parameter1;
                    }
                    else if (message.Target.Instance != 0)
                    {
                        charId = message.Target.Instance;
                    }

                    Identity inviteTarget = new Identity
                    {
                        Type = IdentityType.CanbeAffected,
                        Instance = charId
                    };

                    ICharacter inviter = client.Controller.Character;
                    ICharacter invitee = LftInviteClientPresence.ResolveOnlinePlayer(inviter, inviteTarget);
                    if (invitee != null)
                    {
                        string armedName;
                        LftInviteArm.TryGetArmedName(inviter, inviteTarget, out armedName);
                        LftInviteClientPresence.SeedForInviteLookup(inviter, invitee, armedName);
                    }

                    TeamRuntime.Invite(
                        client.Controller.Character,
                        inviteTarget,
                        message.Parameter2);
                }

                    break;
                case CharacterActionType.ClientTeamInviteReply:
                {
                    // Live L60: Accept only Parameter2=1. Decline=20 (also 0 on No).
                    // Never treat p2=0 as Accept — that joins on every No / dialog noise.
                    if (message.Parameter2 == 1)
                    {
                        client.Controller.TeamJoinReply(true, message.Target);
                    }
                    else if (message.Parameter2 == 20 || message.Parameter2 == 0)
                    {
                        client.Controller.TeamJoinReply(false, message.Target);
                    }
                }

                    break;
                case CharacterActionType.TeamRequestReply:
                {
                    // Live gold L60 (20260729-173311 / 173411):
                    //   Accept: OUT TeamRequestReply (0x15) Parameter2=1 Target=inviter
                    //   Decline: Parameter2=20 (No also uses 0 on private)
                    // Parameter2=17 is server→client join-ack only — never Accept.
                    if (message.Parameter2 == 1)
                    {
                        client.Controller.TeamJoinReply(true, message.Target);
                    }
                    else if (message.Parameter2 == 20 || message.Parameter2 == 0)
                    {
                        client.Controller.TeamJoinReply(false, message.Target);
                    }
                }

                    break;

                case CharacterActionType.AcceptTeamRequest:
                {
                    // Capture: server→client leadership marker (Target=self, TeamWindow id).
                    // Never treat as invite accept — echoes were calling TeamJoinReply wrongly.
                }

                    break;

                case CharacterActionType.DeleteItem: // Remove/Delete item
                    if (client.Controller.Character.Playfield.TryDeleteCorpseLootItem(
                            client.Controller.Character,
                            message.Target,
                            message.Parameter1,
                            message.Parameter2))
                    {
                        this.AcknowledgeDelete(client.Controller.Character, message);
                        break;
                    }

                    if (AORebirth.Core.Playfields.Playfield.ClaimsGeneratedMissionCorpseContainer(
                            client.Controller.Character.Playfield,
                            message.Target,
                            message.Parameter1,
                            message.Parameter2)
                        || (message.Target.Type == IdentityType.Corpse
                            && ZoneEngine.Core.Missions.MissionAcgBindingRuntime.ClaimsGeneratedLivePlayfield(
                                client.Controller.Character.Playfield.Identity.Instance)))
                    {
                        break;
                    }

                    if (!InventoryContainerRuntimeService.Default.DeleteInventoryItemAction(
                            client.Controller.Character,
                            message))
                    {
                        // Sacred Thrak garden key refused — keep on server and re-sync client.
                        ZoneEngine.Core.Thrak.Quests.ThrakGardenKeyQuestRuntime.TryForceReturnGardenKey(
                            client.Controller.Character);
                        break;
                    }

                    this.AcknowledgeDelete(client.Controller.Character, message);
                    break;

                case CharacterActionType.Split: // Split?
                    InventoryContainerRuntimeService.Default.SplitInventoryItemStackAction(client.Controller.Character, message);
                    // Does it need to Acknowledge? Need to check that - Algorithman
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
                    AreteRoboticGuardDogRuntime.NoteSneakStarted(client.Controller.Character);
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
                    // quality < 0 → derive from implant QL (+ bump), not hardcode 300.
                    TradeSkillReceiver.TradeSkillBuildPressed(client, -1);

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

                case CharacterActionType.RemoveFriendlyNano:
                    ActiveNanoRuntimeService.Default.TryHandleRemoveFriendlyNano(client, message);
                    break;

                case CharacterActionType.TrainPerk:
                    PerkRuntimeService.Default.TryHandleTrainPerk(client, message);
                    break;

                case CharacterActionType.UsePerk:
                    PerkRuntimeService.Default.TryHandleUsePerk(client, message);
                    break;

                case CharacterActionType.Reload:
                    WeaponReloadRuntimeService.TryHandleReload(client, message);
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
        /// Capture 20260727-065826: deliver invite popup to target (Action=TeamRequestInvite / 0x1A).
        /// </summary>
        public void SendTeamInviteRequest(ICharacter invitee, ICharacter inviter)
        {
            if (invitee == null || inviter == null)
            {
                return;
            }

            this.Send(
                invitee,
                x =>
                {
                    x.Identity = invitee.Identity;
                    x.Unknown = 0;
                    x.Action = CharacterActionType.TeamRequestInvite;
                    x.Unknown1 = 0;
                    x.Target = inviter.Identity;
                    x.Parameter1 = 0;
                    x.Parameter2 = 0;
                    x.Unknown2 = 0;
                },
                false);
        }

        /// <summary>
        /// Server→inviter Action=0xA9 (TeamInviteAck) = TooHigh Yes/No warn.
        /// Live 20260815-194517 (64 vs 200): after OUT 0x1A p2=0, before any invite
        /// popup. In-range must not send this hex.
        /// </summary>
        public void SendTeamInviteAck(ICharacter inviter, ICharacter invitee)
        {
            this.SendTeamInviteRangeWarn(inviter, invitee, CharacterActionType.TeamInviteAck);
        }

        /// <summary>
        /// Server→inviter Action=0xA8 = TooLow Yes/No warn.
        /// Live 20260815-222131 (200 vs 64 / Nicoldoc): after OUT 0x1A p2=0.
        /// </summary>
        public void SendTeamInviteTooLow(ICharacter inviter, ICharacter invitee)
        {
            this.SendTeamInviteRangeWarn(inviter, invitee, CharacterActionType.TeamInviteTooLow);
        }

        private void SendTeamInviteRangeWarn(
            ICharacter inviter,
            ICharacter invitee,
            CharacterActionType action)
        {
            if (inviter == null || invitee == null)
            {
                return;
            }

            this.Send(
                inviter,
                x =>
                {
                    x.Identity = inviter.Identity;
                    x.Unknown = 0;
                    x.Action = action;
                    x.Unknown1 = 0;
                    x.Target = invitee.Identity;
                    x.Parameter1 = 0;
                    x.Parameter2 = 0;
                    x.Unknown2 = 0;
                },
                false);
        }

        /// <summary>
        /// Capture 20260727-071217: Action=21 TeamRequestReply Parameter2=17 Target=None on successful join.
        /// </summary>
        public void SendTeamRequestReplyAck(ICharacter character)
        {
            if (character == null)
            {
                return;
            }

            this.Send(
                character,
                x =>
                {
                    x.Identity = character.Identity;
                    x.Unknown = 0;
                    x.Action = CharacterActionType.TeamRequestReply;
                    x.Unknown1 = 0;
                    x.Target = Identity.None;
                    x.Parameter1 = 0;
                    x.Parameter2 = 17;
                    x.Unknown2 = 0;
                },
                false);
        }

        /// <summary>
        /// Capture 20260727-071217: Action=21 Target=decliner Parameter2=20 to inviter.
        /// </summary>
        public void SendTeamRequestDeclined(ICharacter inviter, ICharacter decliner)
        {
            if (inviter == null || decliner == null)
            {
                return;
            }

            this.Send(
                inviter,
                x =>
                {
                    x.Identity = inviter.Identity;
                    x.Unknown = 0;
                    x.Action = CharacterActionType.TeamRequestReply;
                    x.Unknown1 = 0;
                    x.Target = decliner.Identity;
                    x.Parameter1 = 0;
                    x.Parameter2 = 20;
                    x.Unknown2 = 0;
                },
                false);
        }

        /// <summary>
        /// Capture 20260727-065826: AcceptTeamRequest with Parameter1=TeamWindow type, Parameter2=team id.
        /// </summary>
        public void SendAcceptTeamRequest(ICharacter character, int teamInstance)
        {
            if (character == null)
            {
                return;
            }

            this.Send(
                character,
                x =>
                {
                    x.Identity = character.Identity;
                    x.Unknown = 0;
                    x.Action = CharacterActionType.AcceptTeamRequest;
                    x.Unknown1 = 0;
                    x.Target = character.Identity;
                    x.Parameter1 = (int)IdentityType.TeamWindow;
                    x.Parameter2 = teamInstance;
                    x.Unknown2 = 0;
                },
                false);
        }

        /// <summary>
        /// Capture 20260727-065826: TeamMemberLeft Action=0x20.
        /// </summary>
        public void SendTeamMemberLeft(ICharacter character, Identity leavingMember, int teamInstance)
        {
            if (character == null)
            {
                return;
            }

            this.Send(
                character,
                x =>
                {
                    x.Identity = character.Identity;
                    x.Unknown = 0;
                    x.Action = CharacterActionType.TeamMemberLeft;
                    x.Unknown1 = 0;
                    x.Target = leavingMember;
                    x.Parameter1 = teamInstance;
                    x.Parameter2 = -1;
                    x.Unknown2 = 0;
                },
                false);
        }

        /// <summary>
        /// Owner CharacterAction 129 — "executes within your NCU" (20260711-022256).
        /// Parameter2 is the heal roll from the nano hit function.
        /// </summary>
        public void SendPetNanoExecutedWithinOwnerNcu(ICharacter owner, ICharacter pet, int healRoll)
        {
            this.Send(
                owner,
                x =>
                {
                    x.Identity = owner.Identity;
                    x.Unknown = 0x00;
                    x.Action = (CharacterActionType)PetHealNanoCatalog.PetNanoExecutedWithinOwnerNcuAction;
                    x.Unknown1 = 0x00000000;
                    x.Target = pet.Identity;
                    x.Parameter1 = 0;
                    x.Parameter2 = healRoll;
                    x.Unknown2 = 0x0000;
                },
                true);
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
            ICharacter recipient = character;
            if (character != null
                && character.Playfield != null
                && target.Instance != 0
                && target.Instance != character.Identity.Instance)
            {
                ICharacter found = character.Playfield.FindByIdentity<ICharacter>(target);
                if (found != null)
                {
                    recipient = found;
                }
            }

            int strain = ActiveNanoRuntimeService.Default.ResolveNanoStrain(recipient, unknown1);
            if (duration > 0)
            {
                // Perk pet buffs (Channel Rage) must land on the pet even if NCU gate is tight.
                // Vehicle/morph nanos must also stay in ActiveNanos — otherwise NCU cancel
                // only removes the client Buff and leaves MonsterData/IsVehicle stuck.
                if (!ActiveNanoRuntimeService.Default.ApplyActiveNano(
                    recipient,
                    unknown1,
                    duration,
                    target,
                    strain))
                {
                    // Capture 20260830-110744 / 124309: Overview of Nascence and Jobe (223767)
                    // must land in ActiveNanos. Sending SetNanoDuration without Apply makes the
                    // client SetFlag MapsC (PF map unlock) while server has no NCU entry — map
                    // stays open after cancel/relog without the nano uploaded/cast.
                    const int overviewOfNascenceAndJobeNanoId = 223767;
                    if (AdventurerMorphFlightRuntime.IsMorphFlightNano(unknown1)
                        || AdventurerMorphFlightRuntime.IsVehicleMorphNano(unknown1)
                        || unknown1 == overviewOfNascenceAndJobeNanoId)
                    {
                        ActiveNanoRuntimeService.Default.ApplyActiveNano(
                            recipient,
                            unknown1,
                            duration,
                            target,
                            strain,
                            true);
                    }
                    else
                    {
                        // Still notify client so NCU icon/duration appear for perk buffs.
                        this.Send(character, this.ConstructSetNanoDuration(character, target, unknown1, duration));
                        return;
                    }
                }

                if (character.Controller != null && character.Controller.Client != null)
                {
                    SimpleCharFullUpdate.SendToOne(character, character.Controller.Client);
                }
            }

            this.Send(character, this.ConstructSetNanoDuration(character, target, unknown1, duration));
        }

        public void NotifyActiveNanoDuration(ICharacter character, Identity target, int nanoId, int duration)
        {
            this.Send(character, this.ConstructSetNanoDuration(character, target, nanoId, duration));
        }

        public void NotifyActiveNanoDurationToPlayfield(
            ICharacter character,
            Identity target,
            int nanoId,
            int duration)
        {
            this.Send(
                character,
                this.ConstructSetNanoDuration(character, target, nanoId, duration),
                true);
        }

        public void SendActiveNanoDuration(ICharacter character, Identity target, int nanoId, int duration)
        {
            this.NotifyActiveNanoDuration(character, target, nanoId, duration);
        }

        public void AcknowledgeRemoveFriendlyNano(ICharacter character, CharacterActionMessage message, int nanoId)
        {
            if (nanoId > 0)
            {
                BuffMessageHandler.Default.SendRemoveNanoBuff(character, nanoId);
            }
        }

        public void CompleteFriendlyNanoRemoval(
            ICharacter character,
            CharacterActionMessage message,
            System.Collections.Generic.List<ActiveNanoRuntimeService.ActiveNanoRemovalTarget> removalTargets)
        {
            if (removalTargets == null)
            {
                return;
            }

            IZoneClient client = character.Controller != null ? character.Controller.Client as IZoneClient : null;
            foreach (ActiveNanoRuntimeService.ActiveNanoRemovalTarget removalTarget in removalTargets)
            {
                BuffMessageHandler.Default.SendRemoveNanoBuff(character, removalTarget.NanoId);
                if (client != null)
                {
                    client.Server.Info(
                        client,
                        "RemoveFriendlyNano outbound Buff remove nanoId={0} instance={1}",
                        removalTarget.NanoId,
                        removalTarget.NanoInstance);
                }
            }
        }

        public void CompleteFriendlyNanoRemoval(
            ICharacter character,
            int nanoId,
            Identity identity,
            int nanoInstance)
        {
            BuffMessageHandler.Default.SendRemoveNanoBuff(character, nanoId);
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
