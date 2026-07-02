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

namespace ZoneEngine.Core.Controllers
{
    #region Usings ...

    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;

    using AORebirth.Core.Entities;
    using AORebirth.Core.Events;
    using AORebirth.Core.Functions;
    using AORebirth.Core.Inventory;
    using AORebirth.Core.Items;
    using AORebirth.Core.Nanos;
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

    using Utility;

    using ZoneEngine.Core;
    using ZoneEngine.Core.Functions;
    using ZoneEngine.Core.Functions.GameFunctions;
    using ZoneEngine.Core.MessageHandlers;
    using ZoneEngine.Core.Playfields;

    using Quaternion = AORebirth.Core.Vector.Quaternion;
    using Vector3 = SmokeLounge.AOtomation.Messaging.GameData.Vector3;

    #endregion

    /// <summary>
    /// </summary>
    public class PlayerController : IController
    {
        // All functions return true if reply should be sent, false if no reply needed

        /// <summary>
        /// </summary>
        private Utility.WeakReference<ICharacter> character;

        private bool disposed = false;

        private CharacterState state = CharacterState.Idle;

        public PlayerController(IZoneClient client)
        {
            this.Client = client;
        }

        public CharacterState State
        {
            get
            {
                return this.state;
            }
            set
            {
                this.state = value;
            }
        }

        /// <summary>
        /// </summary>
        public ICharacter Character
        {
            get
            {
                return this.character.Target;
            }

            set
            {
                if (value == null)
                {
                    throw new Exception("Dont try to weak reference null");
                }

                this.character = new Utility.WeakReference<ICharacter>(value);
            }
        }

        public void Dispose()
        {
            this.Dispose(true);
            GC.SuppressFinalize(this);
        }

        public IZoneClient Client { get; set; }

        public void CallFunction(Function function, IEntity caller)
        {
            // TODO: Make it more versatile, not just applying stuff on yourself
            FunctionCollection.Instance.CallFunction(
                function.FunctionType,
                this.Character,
                caller,
                this.Character,
                function.Arguments.Values.ToArray());
        }

        public void MoveTo(Vector3 destination)
        {
            FollowTargetMessageHandler.Default.Send(this.Character, this.Character.RawCoordinates, destination);
        }

        public void Run()
        {
            this.Character.UpdateMoveType(25); // Magic number 25 = Run
        }

        public void StopMovement()
        {
            this.Character.UpdateMoveType(2); // Magic number: Stop movement
        }

        public void Walk()
        {
            this.Character.UpdateMoveType(24); // Magic number 24 = Walk
        }

        public bool SaveToDatabase
        {
            get
            {
                return true;
            }
        }

        public bool IsFollowing()
        {
            return false;
        }

        public void DoFollow()
        {
            throw new NotImplementedException();
        }

        public void StartPatrolling()
        {
            throw new NotImplementedException();
        }

        #region Generic character actions

        /// <summary>
        /// </summary>
        /// <param name="target">
        /// </param>
        /// <returns>
        /// </returns>
        /// <exception cref="NotImplementedException">
        /// </exception>
        public bool LookAt(Identity target)
        {
            // TODO: add Team lookup here too (F1-F6 for example)
            bool result = false;
            if (Pool.Instance.Contains(this.Character.Playfield.Identity, target))
            {
                this.Character.SetTarget(target);
                result = true;
            }
            return result;
        }

        /// <summary>
        /// </summary>
        /// <param name="nanoId">
        /// </param>
        /// <param name="target">
        /// </param>
        /// <returns>
        /// </returns>
        /// <exception cref="NotImplementedException">
        /// </exception>
        public bool CastNano(int nanoId, Identity target)
        {
            // Procedure:
            // 1. Check if nano can be casted (criteria to Use (3))
            // 2. Lock nanocasting ability
            // 3. Wait for cast attack delay
            // 4. Check target's restance to the nano
            // 5. Execute nanos gamefunctions
            // 6. Wait for nano recharge delay
            // 7. Unlock nano casting

            NanoFormula nano = NanoLoader.NanoList[nanoId];
            int strain = nano.NanoStrain();

            CastNanoSpellMessageHandler.Default.Send(this.Character, nanoId, target);

            // CharacterAction 107 - Finish nano casting
            int attackDelay = this.Character.CalculateNanoAttackTime(nano);
            Console.WriteLine("Attack-Delay: " + attackDelay);
            if (attackDelay != 1234567890)
            {
                Thread.Sleep(attackDelay * 10);
            }

            // Check here for nanoresist of the target, maybe the 1 in finishnanocasting is kind of did land/didnt land flag
            CharacterActionMessageHandler.Default.FinishNanoCasting(
                this.Character,
                CharacterActionType.FinishNanoCasting,
                Identity.None,
                1,
                nanoId);

            // TODO: Calculate nanocost modifiers etc.
            this.Character.Stats[StatIds.currentnano].Value -= nano.getItemAttribute(407);

            // CharacterAction 98 - Set nano duration
            CharacterActionMessageHandler.Default.SetNanoDuration(
                this.Character,
                target,
                nanoId,
                nano.getItemAttribute(8));

            Thread.Sleep(nano.getItemAttribute(210) * 10); // Recharge Delay
            return false;
        }

        /// <summary>
        /// </summary>
        /// <returns>
        /// </returns>
        /// <exception cref="NotImplementedException">
        /// </exception>
        public bool Search()
        {
            // Procedure:
            // 1. Gather stealthed entities inside range
            // 2. Check against each entities concealment skill
            // 3. Unhide successful found entities
            // 4. Lock search action for ?? seconds

            return false;
        }

        /// <summary>
        /// </summary>
        /// <returns>
        /// </returns>
        /// <exception cref="NotImplementedException">
        /// </exception>
        public bool Sneak()
        {
            // Procedure: 
            // 1. Gather surrounding mobs/players
            // 2. Check concealment against their perception skill
            // 3. Vanish for successful rolled chars/mobs

            return false;
        }

        /// <summary>
        /// </summary>
        /// <param name="visualFlag">
        /// </param>
        /// <returns>
        /// </returns>
        public bool ChangeVisualFlag(int visualFlag)
        {
            // Procedure:
            // 1. Set visualFlags stat
            // 2. Send AppearanceUpdate
            this.Character.Stats[StatIds.visualflags].Value = visualFlag;
            AppearanceUpdateMessageHandler.Default.Send(this.Character);
            return false;
        }

        /// <summary>
        /// </summary>
        /// <param name="moveType">
        /// </param>
        /// <param name="newCoordinates">
        /// </param>
        /// <param name="heading">
        /// </param>
        /// <returns>
        /// </returns>
        /// <exception cref="NotImplementedException">
        /// </exception>
        public bool Move(int moveType, Coordinate newCoordinates, Quaternion heading)
        {
            // Procedure:
            // 1. Check if new coordinates are plausible (in range of runspeed since last update)
            // 2. Set coordinates & heading

            // Is this correct? Shouldnt the client input be compared to the prediction and then be overridden to prevent teleportation exploits? 
            // - Algorithman

            // give it a bit uncertainty (2.0f)
            LogUtil.Debug(
                DebugInfoDetail.Movement,
                newCoordinates.ToString() + "<->" + this.Character.Coordinates().ToString());
            // if (newCoordinates.Distance2D(this.Character.Coordinates) < 2.0f)
            {
                this.Character.SetCoordinates(newCoordinates, heading);
                this.Character.UpdateMoveType((byte)moveType);
            }
            /*
            else
            {
                this.Character.StopMovement();
            }
            */
            return true;
        }

        /// <summary>
        /// </summary>
        /// <param name="sourceContainerType">
        /// </param>
        /// <param name="sourcePlacement">
        /// </param>
        /// <param name="target">
        /// </param>
        /// <param name="targetPlacement">
        /// </param>
        /// <returns>
        /// </returns>
        /// <exception cref="NotImplementedException">
        /// </exception>
        public bool ContainerAddItem(int sourceContainerType, int sourcePlacement, Identity target, int targetPlacement)
        {
            return InventoryContainerRuntimeService.Default.MovePlayerControllerContainerItem(
                this.Character,
                sourceContainerType,
                sourcePlacement,
                target,
                targetPlacement);
        }

        /// <summary>
        /// </summary>
        /// <param name="target">
        /// </param>
        /// <returns>
        /// </returns>
        /// <exception cref="NotImplementedException">
        /// </exception>
        public bool Follow(Identity target)
        {
            // Procedure:
            // 1. Check if target is still ingame
            // 2. Find a path to target and head accordingly
            // 3. Start movement (if not already)
            // 4. Start Pathfinding loop

            throw new NotImplementedException();
        }

        /// <summary>
        /// </summary>
        /// <returns>
        /// </returns>
        /// <exception cref="NotImplementedException">
        /// </exception>
        public bool Stand()
        {
            // Procedure:
            // 1. Update characters move mode
            // 2. Announce the action to the playfield (or range)
            // 3. If logout timer pending, cancel pending logout timer

            if (this.Character.InLogoutTimerPeriod())
            {
                this.Character.StopLogoutTimer();
            }

            this.Character.UpdateMoveType(37); // Magic number -> Stand
            return true;
        }

        /// <summary>
        /// </summary>
        /// <param name="action">
        /// </param>
        /// <param name="parameter1">
        /// </param>
        /// <param name="parameter2">
        /// </param>
        /// <param name="parameter3">
        /// </param>
        /// <param name="parameter4">
        /// </param>
        /// <param name="parameter5">
        /// </param>
        /// <returns>
        /// </returns>
        /// <exception cref="NotImplementedException">
        /// </exception>
        public bool SocialAction(
            SocialAction action,
            byte parameter1,
            byte parameter2,
            byte parameter3,
            byte parameter4,
            int parameter5)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// </summary>
        /// <param name="target">
        /// </param>
        /// <returns>
        /// </returns>
        /// <exception cref="NotImplementedException">
        /// </exception>
        public bool Trade(Identity target)
        {
            throw new NotImplementedException();
        }

        #endregion

        #region Player specific actions

        /// <summary>
        /// </summary>
        /// <param name="itemPosition">
        /// </param>
        /// <returns>
        /// </returns>
        /// <exception cref="NotImplementedException">
        /// </exception>
        public bool UseItem(Identity itemPosition)
        {
            return InventoryContainerRuntimeService.Default.UseInventoryItem(this.Character, itemPosition);
        }

        public bool TryUseBackpackContainer(Identity itemPosition)
        {
            return InventoryContainerRuntimeService.Default.TryUseBackpackContainer(this.Character, itemPosition);
        }

        public bool UseStatel(Identity identity, EventType eventType = EventType.OnUse)
        {
            if (PlayfieldLoader.PFData.ContainsKey(this.Character.Playfield.Identity.Instance))
            {
                StatelData sd =
                    PlayfieldLoader.PFData[this.Character.Playfield.Identity.Instance].Statels.FirstOrDefault(
                        x => (x.Identity.Type == identity.Type) && (x.Identity.Instance == identity.Instance));

                if (sd != null)
                {
                    this.SendChatText("Found Statel with " + sd.Events.Count + " events");
                    Event onUse = sd.Events.FirstOrDefault(x => x.EventType == eventType);
                    if (onUse != null)
                    {
                        onUse.Perform(this.Character, sd);
                    }
                }
            }
            return true;
        }

        public void SendChatText(string text)
        {
            ChatTextMessageHandler.Default.Send(this.Character, text);
        }

        /// <summary>
        /// </summary>
        /// <param name="container">
        /// </param>
        /// <param name="slotNumber">
        /// </param>
        /// <returns>
        /// </returns>
        /// <exception cref="NotImplementedException">
        /// </exception>
        public bool DeleteItem(int container, int slotNumber)
        {
            return InventoryContainerRuntimeService.Default.DeletePlayerControllerContainerItem(
                this.Character,
                container,
                slotNumber);
        }

        /// <summary>
        /// </summary>
        /// <param name="targetItem">
        /// </param>
        /// <param name="stackCount">
        /// </param>
        /// <returns>
        /// </returns>
        /// <exception cref="NotImplementedException">
        /// </exception>
        public bool SplitItemStack(Identity targetItem, int stackCount)
        {
            // Procedure:
            // 1. Check if Item exists
            // 2. Check if stackCount<item's stack - 1
            // 3. Create new item from old item with stack=stackCount
            // 4. Decrease old item's stack
            // 5. Add new item to inventory

            throw new NotImplementedException();
        }

        /// <summary>
        /// </summary>
        /// <param name="sourceItem">
        /// </param>
        /// <param name="targetItem">
        /// </param>
        /// <returns>
        /// </returns>
        /// <exception cref="NotImplementedException">
        /// </exception>
        public bool JoinItemStack(Identity sourceItem, Identity targetItem)
        {
            // Procedure:
            // 1. Check if items are the same itemid's
            // 2. Add sourceItem stack to targetItem
            // 3. Delete sourceItem

            throw new NotImplementedException();
        }

        /// <summary>
        /// </summary>
        /// <param name="sourceItem">
        /// </param>
        /// <param name="targetItem">
        /// </param>
        /// <returns>
        /// </returns>
        /// <exception cref="NotImplementedException">
        /// </exception>
        public bool CombineItems(Identity sourceItem, Identity targetItem)
        {
            // Procedure: 
            // See TradeSkillReceiver.TradeSkillBuildPressed

            throw new NotImplementedException();
        }

        /// <summary>
        /// </summary>
        /// <param name="inventoryPageId">
        /// </param>
        /// <param name="slotNumber">
        /// </param>
        /// <returns>
        /// </returns>
        /// <exception cref="NotImplementedException">
        /// </exception>
        public bool TradeSkillSourceChanged(int inventoryPageId, int slotNumber)
        {
            // Procedure see TradeSkillReceiver.TradeSkillSourceChanged

            throw new NotImplementedException();
        }

        /// <summary>
        /// </summary>
        /// <param name="inventoryPageId">
        /// </param>
        /// <param name="slotNumber">
        /// </param>
        /// <returns>
        /// </returns>
        /// <exception cref="NotImplementedException">
        /// </exception>
        public bool TradeSkillTargetChanged(int inventoryPageId, int slotNumber)
        {
            // Procedure see TradeSkillReceiver.TradeSkillTargetChanged

            throw new NotImplementedException();
        }

        /// <summary>
        /// </summary>
        /// <param name="targetItem">
        /// </param>
        /// <returns>
        /// </returns>
        /// <exception cref="NotImplementedException">
        /// </exception>
        public bool TradeSkillBuildPressed(Identity targetItem)
        {
            // Procedure see TradeSkillReceiver.TradeSkillBuildPressed

            throw new NotImplementedException();
        }

        /// <summary>
        /// </summary>
        /// <param name="command">
        /// </param>
        /// <param name="target">
        /// </param>
        /// <returns>
        /// </returns>
        /// <exception cref="NotImplementedException">
        /// </exception>
        public bool ChatCommand(string command, Identity target)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// </summary>
        /// <returns>
        /// </returns>
        /// <exception cref="NotImplementedException">
        /// </exception>
        public bool Logout()
        {
            // Procedure: 
            // 1. Sit down (if not already)
            // 2. Check if we are a GM
            // 2.1. Save character and logout immediately
            // 3. Start logout timer
            // 4. Save character
            // 5. Logout

            throw new NotImplementedException();
        }

        public void LogoffCharacter()
        {
            CharacterDao.Instance.SetOffline(this.Character.Identity.Instance);
        }

        /// <summary>
        /// </summary>
        /// <returns>
        /// </returns>
        /// <exception cref="NotImplementedException">
        /// </exception>
        public bool Login()
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// </summary>
        /// <returns>
        /// </returns>
        /// <exception cref="NotImplementedException">
        /// </exception>
        public bool StopLogout()
        {
            // Procedure:
            // 1. Stop pending logout timer
            // 2. Go back to previous move mode (dunno if really needed)

            throw new NotImplementedException();
        }

        /// <summary>
        /// </summary>
        /// <param name="target">
        /// </param>
        /// <returns>
        /// </returns>
        /// <exception cref="NotImplementedException">
        /// </exception>
        public bool GetTargetInfo(Identity target)
        {
            // Procedure:
            // 1. Gather data
            // 2. Send to client

            throw new NotImplementedException();
        }

        /// <summary>
        /// </summary>
        /// <param name="target">
        /// </param>
        /// <returns>
        /// </returns>
        /// <exception cref="NotImplementedException">
        /// </exception>
        public bool TeamInvite(Identity target)
        {
            return TeamRuntime.Invite(this.Character, target);
        }

        /// <summary>
        /// </summary>
        /// <param name="target">
        /// </param>
        /// <returns>
        /// </returns>
        /// <exception cref="NotImplementedException">
        /// </exception>
        public bool TeamKickMember(Identity target)
        {
            // Procedure:
            // 1. Kick Team member
            // 2. Send Team update message

            return TeamRuntime.Kick(this.Character, target);
        }

        /// <summary>
        /// </summary>
        /// <returns>
        /// </returns>
        /// <exception cref="NotImplementedException">
        /// </exception>
        public bool TeamLeave()
        {
            // Procedure:
            // 1. Leave the team
            // 2. Send Team update message

            return TeamRuntime.Leave(this.Character);
        }

        /// <summary>
        /// </summary>
        /// <param name="target">
        /// </param>
        /// <returns>
        /// </returns>
        /// <exception cref="NotImplementedException">
        /// </exception>
        public bool TransferTeamLeadership(Identity target)
        {
            // Procedure:
            // 1. Transfer Leadership
            // 2. Send Team update message

            ChatTextMessageHandler.Default.Send(this.Character, "Team leadership transfer is not wired yet.");
            return false;
        }

        /// <summary>
        /// </summary>
        /// <param name="target">
        /// </param>
        /// <returns>
        /// </returns>
        /// <exception cref="NotImplementedException">
        /// </exception>
        public bool TeamJoinRequest(Identity target)
        {
            // Procedure:
            // 1. Send target the invite

            return TeamRuntime.Invite(this.Character, target);
        }

        /// <summary>
        /// </summary>
        /// <param name="accept">
        /// </param>
        /// <param name="requester">
        /// </param>
        /// <returns>
        /// </returns>
        /// <exception cref="NotImplementedException">
        /// </exception>
        public bool TeamJoinReply(bool accept, Identity requester)
        {
            // Procedure:
            // 1. If accept==true
            // 2.    Call requester's TeamJoinAccepted
            // 3. else
            // 4.    Call requester's TeamJoinRejected

            return TeamRuntime.Reply(this.Character, accept, requester);
        }

        /// <summary>
        /// </summary>
        /// <param name="newTeamMember">
        /// </param>
        /// <returns>
        /// </returns>
        /// <exception cref="NotImplementedException">
        /// </exception>
        public bool TeamJoinAccepted(Identity newTeamMember)
        {
            // Procedure:
            // 1. If on team exists yet, create one
            // 2. Add yourself as TeamLeader
            // 3. Add newTeamMember
            // 4. Send out TeamMemberInfo etc. to all team members

            return TeamRuntime.AcceptDirect(this.Character, newTeamMember);
        }

        /// <summary>
        /// </summary>
        /// <param name="rejectingIdentity">
        /// </param>
        /// <returns>
        /// </returns>
        /// <exception cref="NotImplementedException">
        /// </exception>
        public bool TeamJoinRejected(Identity rejectingIdentity)
        {
            // Procedure: 
            // 1. Send back negative reply

            return TeamRuntime.RejectDirect(this.Character, rejectingIdentity);
        }

        /// <summary>
        /// </summary>
        /// <param name="client">
        /// </param>
        public void SendChangedStats()
        {
            Dictionary<int, uint> toPlayfield = new Dictionary<int, uint>();
            Dictionary<int, uint> toPlayer = new Dictionary<int, uint>();

            this.Character.Stats.GetChangedStats(toPlayer, toPlayfield);

            StatMessageHandler.Default.SendBulk(this.Character, toPlayer, toPlayfield);
        }

        #endregion

        ~PlayerController()
        {
            this.Dispose(false);
        }

        protected virtual void Dispose(bool disposing)
        {
            LogUtil.Debug(DebugInfoDetail.Memory, "Disposing of PlayerController");

            if (disposing)
            {
                if (!this.disposed)
                {
                    // Only remove the link to client here, client will be disposed on its own
                    this.Client = null;
                }
            }
            this.disposed = true;
        }
    }

    internal static class TeamRuntime
    {
        private static readonly object Sync = new object();

        private static readonly Dictionary<int, Identity> PendingInvites = new Dictionary<int, Identity>();

        private static readonly Dictionary<int, List<Identity>> TeamMembers = new Dictionary<int, List<Identity>>();

        private static readonly Dictionary<int, int> CharacterTeams = new Dictionary<int, int>();

        private static int nextTeamId = 1;

        public static bool Invite(ICharacter inviter, Identity targetIdentity)
        {
            ICharacter target = ResolveOnlineCharacter(inviter, targetIdentity);
            if (target == null || target.Identity.Equals(inviter.Identity))
            {
                ChatTextMessageHandler.Default.Send(inviter, "Team invite target is not available.");
                return false;
            }

            lock (Sync)
            {
                PendingInvites[target.Identity.Instance] = inviter.Identity;
            }

            ChatTextMessageHandler.Default.Send(inviter, "Team invite sent to " + target.Name + ".");
            ChatTextMessageHandler.Default.Send(
                target,
                inviter.Name + " invited you to a team. Use /team accept or /team decline.");

            LogUtil.Debug(
                DebugInfoDetail.Engine,
                "Team invite pending inviter=" + inviter.Identity.ToString(true)
                + " target=" + target.Identity.ToString(true));

            return true;
        }

        public static bool Reply(ICharacter character, bool accept, Identity requester)
        {
            Identity inviterIdentity;
            lock (Sync)
            {
                if (!PendingInvites.TryGetValue(character.Identity.Instance, out inviterIdentity))
                {
                    inviterIdentity = requester;
                }

                PendingInvites.Remove(character.Identity.Instance);
            }

            if (inviterIdentity == null || inviterIdentity.Equals(Identity.None))
            {
                ChatTextMessageHandler.Default.Send(character, "No pending team invite.");
                return false;
            }

            ICharacter inviter = ResolveOnlineCharacter(character, inviterIdentity);
            if (inviter == null)
            {
                ChatTextMessageHandler.Default.Send(character, "The team inviter is no longer available.");
                return false;
            }

            if (!accept)
            {
                ChatTextMessageHandler.Default.Send(character, "Team invite declined.");
                ChatTextMessageHandler.Default.Send(inviter, character.Name + " declined your team invite.");
                return true;
            }

            Join(inviter, character);
            return true;
        }

        public static bool AcceptDirect(ICharacter leader, Identity newMemberIdentity)
        {
            ICharacter newMember = ResolveOnlineCharacter(leader, newMemberIdentity);
            if (newMember == null)
            {
                ChatTextMessageHandler.Default.Send(leader, "Team member is not available.");
                return false;
            }

            Join(leader, newMember);
            return true;
        }

        public static bool RejectDirect(ICharacter inviter, Identity rejectingIdentity)
        {
            ICharacter rejectingCharacter = ResolveOnlineCharacter(inviter, rejectingIdentity);
            if (rejectingCharacter != null)
            {
                ChatTextMessageHandler.Default.Send(
                    inviter,
                    rejectingCharacter.Name + " declined your team invite.");
            }

            return true;
        }

        public static bool Leave(ICharacter character)
        {
            int teamId;
            List<Identity> remainingMembers;
            lock (Sync)
            {
                if (!CharacterTeams.TryGetValue(character.Identity.Instance, out teamId))
                {
                    ChatTextMessageHandler.Default.Send(character, "You are not in a team.");
                    return false;
                }

                remainingMembers = TeamMembers[teamId];
                remainingMembers.RemoveAll(x => x.Instance == character.Identity.Instance);
                CharacterTeams.Remove(character.Identity.Instance);
                if (remainingMembers.Count == 0)
                {
                    TeamMembers.Remove(teamId);
                }
            }

            ApplyTeamStats(character, 0, 1);
            ChatTextMessageHandler.Default.Send(character, "You left the team.");
            NotifyMembers(remainingMembers, character.Name + " left the team.");
            UpdateTeamMemberStats(teamId);
            return true;
        }

        public static bool Kick(ICharacter leader, Identity targetIdentity)
        {
            ICharacter target = ResolveOnlineCharacter(leader, targetIdentity);
            if (target == null)
            {
                ChatTextMessageHandler.Default.Send(leader, "Team kick target is not available.");
                return false;
            }

            int teamId;
            lock (Sync)
            {
                if (!CharacterTeams.TryGetValue(leader.Identity.Instance, out teamId)
                    || !CharacterTeams.ContainsKey(target.Identity.Instance)
                    || CharacterTeams[target.Identity.Instance] != teamId)
                {
                    ChatTextMessageHandler.Default.Send(leader, target.Name + " is not in your team.");
                    return false;
                }
            }

            Leave(target);
            ChatTextMessageHandler.Default.Send(leader, target.Name + " was removed from the team.");
            return true;
        }

        public static bool TryHandleChatCommand(ICharacter character, string[] args)
        {
            if (args == null || args.Length < 2)
            {
                ChatTextMessageHandler.Default.Send(
                    character,
                    "Team commands: /team invite <name>, /team accept, /team decline, /team leave.");
                return true;
            }

            string action = args[1].ToLowerInvariant();
            if (action == "accept")
            {
                return Reply(character, true, Identity.None);
            }

            if ((action == "decline") || (action == "reject"))
            {
                return Reply(character, false, Identity.None);
            }

            if (action == "leave")
            {
                return Leave(character);
            }

            if (action == "invite" && args.Length >= 3)
            {
                ICharacter target = FindOnlineCharacterByName(character, args[2]);
                if (target == null)
                {
                    ChatTextMessageHandler.Default.Send(character, "Could not find online character " + args[2] + ".");
                    return false;
                }

                return Invite(character, target.Identity);
            }

            ChatTextMessageHandler.Default.Send(character, "Unknown team command.");
            return false;
        }

        private static void Join(ICharacter leader, ICharacter newMember)
        {
            int teamId;
            List<Identity> members;
            lock (Sync)
            {
                if (!CharacterTeams.TryGetValue(leader.Identity.Instance, out teamId))
                {
                    teamId = nextTeamId++;
                    CharacterTeams[leader.Identity.Instance] = teamId;
                    TeamMembers[teamId] = new List<Identity> { leader.Identity };
                }

                members = TeamMembers[teamId];
                if (!members.Any(x => x.Instance == newMember.Identity.Instance))
                {
                    members.Add(newMember.Identity);
                }

                CharacterTeams[newMember.Identity.Instance] = teamId;
            }

            UpdateTeamMemberStats(teamId);
            NotifyMembers(members, newMember.Name + " joined the team.");
            LogUtil.Debug(
                DebugInfoDetail.Engine,
                "Team joined teamId=" + teamId
                + " leader=" + leader.Identity.ToString(true)
                + " member=" + newMember.Identity.ToString(true));
        }

        private static void UpdateTeamMemberStats(int teamId)
        {
            List<Identity> members;
            lock (Sync)
            {
                if (!TeamMembers.TryGetValue(teamId, out members))
                {
                    return;
                }

                members = members.ToList();
            }

            int memberCount = members.Count;
            foreach (Identity memberIdentity in members)
            {
                ICharacter member = Pool.Instance.GetObject<ICharacter>(memberIdentity);
                if (member != null)
                {
                    ApplyTeamStats(member, teamId, memberCount);
                }
            }
        }

        private static void ApplyTeamStats(ICharacter character, int teamId, int memberCount)
        {
            character.Stats[StatIds.team].Value = teamId;
            character.Stats[StatIds.team].BaseValue = (uint)teamId;
            character.Stats[StatIds.numberofteammembers].Value = memberCount;
            character.Stats[StatIds.numberofteammembers].BaseValue = (uint)memberCount;
            character.Controller.SendChangedStats();
        }

        private static void NotifyMembers(List<Identity> members, string text)
        {
            if (members == null)
            {
                return;
            }

            foreach (Identity identity in members.ToList())
            {
                ICharacter member = Pool.Instance.GetObject<ICharacter>(identity);
                if (member != null)
                {
                    ChatTextMessageHandler.Default.Send(member, text);
                }
            }
        }

        private static ICharacter ResolveOnlineCharacter(ICharacter reference, Identity identity)
        {
            if (reference == null || reference.Playfield == null || identity == null)
            {
                return null;
            }

            return Pool.Instance.GetObject<ICharacter>(reference.Playfield.Identity, identity)
                   ?? Pool.Instance.GetObject<ICharacter>(identity);
        }

        private static ICharacter FindOnlineCharacterByName(ICharacter reference, string name)
        {
            if (reference == null || string.IsNullOrWhiteSpace(name))
            {
                return null;
            }

            return Pool.Instance.GetAll<ICharacter>((int)IdentityType.CanbeAffected)
                .FirstOrDefault(
                    x => x != null
                         && x.Controller is PlayerController
                         && string.Equals(x.Name, name, StringComparison.OrdinalIgnoreCase));
        }
    }
}
