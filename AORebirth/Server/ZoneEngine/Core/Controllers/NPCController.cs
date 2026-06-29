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
    using System.Diagnostics;
    using System.Globalization;

    using AORebirth.Core.Entities;
    using AORebirth.Core.Functions;
    using AORebirth.Core.Network;
    using AORebirth.Core.Vector;
    using AORebirth.Enums;
    using AORebirth.Interfaces;
    using AORebirth.ObjectManager;

    using SmokeLounge.AOtomation.Messaging.GameData;
    using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;

    using Utility;

    using ZoneEngine.Core.Arete.Dialogue;
    using ZoneEngine.Core.Functions;
    using ZoneEngine.Core.InternalMessages;
    using ZoneEngine.Core.KnuBot;
    using ZoneEngine.Core.MessageHandlers;

    using Quaternion = AORebirth.Core.Vector.Quaternion;
    using Vector3 = AORebirth.Core.Vector.Vector3;

    #endregion

    /// <summary>
    /// </summary>
    public class NPCController : IController
    {
        public BaseKnuBot KnuBot = null;

        private Identity followIdentity = Identity.None;

        private Vector3 followCoordinates = new Vector3();

        private NpcMotionSegment followMotionSegment;

        private double followStopDistance;

        private DateTime lastMotionPacketUtc = DateTime.MinValue;

        private Vector3 lastMotionPacketDestination = new Vector3();

        private bool hasMotionPacket;

        private bool suppressMotionSegmentUpdates;

        private CharacterState state = CharacterState.Idle;

        private int activeWaypoint = 0;

        private const double MaxNpcFollowSpeedPerSecond = EnemyBehaviorContract.MaxNpcFollowSpeedPerSecond;

        private const double MaxPlayerChaseProjectionDistance = EnemyBehaviorContract.MaxPlayerChaseProjectionDistance;

        private const double MinVisibleFollowUpdateSeconds = 0.35;

        private const double MinVisibleFollowTargetDelta = 1.0;

        private const double CoordinateFollowArrivalDistance = 0.3;

        private const double WalkFollowSpeedPerSecond = 1.5;

        public NpcAiProfile AiProfile { get; set; } = NpcAiProfile.Passive;

        private struct NpcMotionSegment
        {
            public Vector3 Start;

            public Vector3 End;

            public DateTime StartedUtc;

            public bool Active;
        }

        private static string FormatVector(Vector3 vector)
        {
            return string.Format(
                CultureInfo.InvariantCulture,
                "{0:0.00},{1:0.00},{2:0.00}",
                vector.x,
                vector.y,
                vector.z);
        }

        private static Vector3 GetFollowTargetPosition(ICharacter target)
        {
            if (target.Controller is PlayerController)
            {
                Vector3 rawPosition = target.RawCoordinates;
                Vector3 predictedPosition = target.Coordinates().coordinate;
                return MoveToward(rawPosition, predictedPosition, MaxPlayerChaseProjectionDistance);
            }

            return target.Coordinates().coordinate;
        }

        private void LogChase(string phase, Vector3 start, Vector3 destination)
        {
            Vector3 raw = this.Character.RawCoordinates;
            LogUtil.Debug(
                DebugInfoDetail.Engine,
                string.Format(
                    CultureInfo.InvariantCulture,
                    "NPCCHASE phase={0} npc={1} target={2} raw={3} start={4} dest={5} dist={6:0.00} stop={7:0.00} mode={8}",
                    phase,
                    this.Character.Identity.ToString(true),
                    this.followIdentity.ToString(true),
                    FormatVector(raw),
                    FormatVector(start),
                    FormatVector(destination),
                    start.Distance2D(destination),
                    this.followStopDistance,
                    this.Character.MoveMode));
        }

        private static Vector3 MoveToward(Vector3 start, Vector3 destination, double maxDistance)
        {
            double distance = start.Distance2D(destination);
            if (distance < 0.001 || maxDistance <= 0)
            {
                return new Vector3(start.x, start.y, start.z);
            }

            double step = Math.Min(distance, maxDistance);
            double factor = step / distance;
            return new Vector3(
                start.x + ((destination.x - start.x) * factor),
                start.y + ((destination.y - start.y) * factor),
                start.z + ((destination.z - start.z) * factor));
        }

        private static ICharacter GetCharacterFromPool(Identity parent, Identity identity)
        {
            return Pool.Instance.GetObject(parent, identity) as ICharacter;
        }

        private void ResetFollowPosition()
        {
            this.followMotionSegment = new NpcMotionSegment();
            this.followStopDistance = 0.0;
            this.hasMotionPacket = false;
            this.suppressMotionSegmentUpdates = false;
            this.lastMotionPacketUtc = DateTime.MinValue;
            this.lastMotionPacketDestination = new Vector3();
        }

        private Vector3 CurrentMotionSegmentPosition(DateTime now)
        {
            if (!this.followMotionSegment.Active)
            {
                return this.Character.Coordinates().coordinate;
            }

            double elapsedSeconds = Math.Max(0.0, (now - this.followMotionSegment.StartedUtc).TotalSeconds);
            return MoveToward(
                this.followMotionSegment.Start,
                this.followMotionSegment.End,
                this.CurrentFollowSpeedPerSecond() * elapsedSeconds);
        }

        private double CurrentFollowSpeedPerSecond()
        {
            if (this.followIdentity.Equals(Identity.None) && this.Character.MoveMode == MoveModes.Walk)
            {
                return WalkFollowSpeedPerSecond;
            }

            return MaxNpcFollowSpeedPerSecond;
        }

        private Vector3 UpdateMotionSegmentPosition(DateTime now)
        {
            Vector3 position = this.CurrentMotionSegmentPosition(now);
            this.Character.Coordinates(position);
            return position;
        }

        private void FaceToward(Vector3 start, Vector3 destination)
        {
            Vector3 normalizedDirection;
            this.TryFaceToward(start, destination, out normalizedDirection);
        }

        private bool TryFaceToward(Vector3 start, Vector3 destination, out Vector3 normalizedDirection)
        {
            normalizedDirection = new Vector3();
            if (start.Distance2D(destination) < 0.001)
            {
                return false;
            }

            Vector3 direction = destination - start;
            direction.y = 0;
            normalizedDirection = direction.Normalize();
            this.Character.Heading = (Quaternion)Quaternion.GenerateRotationFromDirectionVector(normalizedDirection);
            return true;
        }

        private Vector3 BuildVisibleFollowDestination(Vector3 start, Vector3 targetPosition)
        {
            if (this.followStopDistance <= 0.0)
            {
                return targetPosition;
            }

            double distance = start.Distance2D(targetPosition);
            if (distance <= this.followStopDistance)
            {
                return start;
            }

            return MoveToward(start, targetPosition, distance - this.followStopDistance);
        }

        private void SetMotionSegment(Vector3 start, Vector3 destination, DateTime now)
        {
            this.followMotionSegment = new NpcMotionSegment
                                       {
                                           Start = start,
                                           End = destination,
                                           StartedUtc = now,
                                           Active = true
                                       };
        }

        private bool ShouldSendMotionSegmentUpdate(Vector3 currentPosition, Vector3 targetPosition, DateTime now)
        {
            if (!this.followMotionSegment.Active || !this.hasMotionPacket)
            {
                return true;
            }

            if ((now - this.lastMotionPacketUtc).TotalSeconds < MinVisibleFollowUpdateSeconds)
            {
                return false;
            }

            Vector3 destination = this.BuildVisibleFollowDestination(currentPosition, targetPosition);
            if (this.lastMotionPacketDestination.Distance2D(destination) >= MinVisibleFollowTargetDelta)
            {
                return true;
            }

            double desiredSpacing = Math.Max(this.followStopDistance, 0.3);
            return currentPosition.Distance2D(this.followMotionSegment.End) < 0.3
                   && currentPosition.Distance2D(targetPosition) > desiredSpacing + MinVisibleFollowTargetDelta;
        }

        private void SendMotionSegmentFollow(string phase, Vector3 start, Vector3 targetPosition, DateTime now)
        {
            Vector3 destination = this.BuildVisibleFollowDestination(start, targetPosition);
            if (!this.followIdentity.Equals(Identity.None))
            {
                this.Run();
            }

            this.Character.Coordinates(start);
            this.FaceToward(start, destination);
            this.LogChase(phase, start, destination);
            FollowTargetMessageHandler.Default.Send(this.Character, start, destination);
            this.SetMotionSegment(start, destination, now);
            this.lastMotionPacketUtc = now;
            this.lastMotionPacketDestination = destination;
            this.hasMotionPacket = true;
        }

        private bool TryCompleteCoordinateFollow(Vector3 current, Vector3 targetPosition)
        {
            if (!this.followIdentity.Equals(Identity.None)
                || current.Distance2D(targetPosition) > CoordinateFollowArrivalDistance)
            {
                return false;
            }

            this.StopMovement();
            this.Character.Coordinates(targetPosition);
            this.StopFollow();
            return true;
        }

        public void SuppressMotionSegmentUpdates(bool suppress)
        {
            this.suppressMotionSegmentUpdates = suppress;
        }

        private void SendWantedDirection(Vector3 direction)
        {
            this.Character.Playfield.Publish(
                new IMSendAOtomationMessageToPlayfield
                {
                    Body =
                        new SetWantedDirectionMessage
                        {
                            Identity = this.Character.Identity,
                            Unknown = 0,
                            DirectinVector =
                                new SmokeLounge.AOtomation.Messaging.GameData.Vector3
                                {
                                    X = direction.xf,
                                    Y = direction.yf,
                                    Z = direction.zf
                                }
                        }
                });
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
        public ICharacter Character { get; set; }

        // Always null here
        public IZoneClient Client
        {
            get
            {
                return null;
            }
            set
            {
                throw new Exception("NPC's dont have a client. Faulty code tries to use it!!");
            }
        }

        public void Dispose()
        {
            this.Dispose(true);
            GC.SuppressFinalize(this);
        }

        public bool LookAt(Identity target)
        {
            throw new NotImplementedException();
        }

        public bool UseStatel(Identity identity, EventType eventType = EventType.OnUse)
        {
            throw new NotImplementedException();
        }

        public void SendChatText(string text)
        {
            throw new NotImplementedException();
        }

        public bool CastNano(int nanoId, Identity target)
        {
            throw new NotImplementedException();
        }

        public bool Search()
        {
            throw new NotImplementedException();
        }

        public bool Sneak()
        {
            throw new NotImplementedException();
        }

        public bool ChangeVisualFlag(int visualFlag)
        {
            throw new NotImplementedException();
        }

        public bool Move(int moveType, Coordinate newCoordinates, Quaternion heading)
        {
            throw new NotImplementedException();
        }

        public bool ContainerAddItem(int sourceContainerType, int sourcePlacement, Identity target, int targetPlacement)
        {
            throw new NotImplementedException();
        }

        public bool Follow(Identity target)
        {
            return this.Follow(target, 0.0);
        }

        public bool Follow(Identity target, double stopDistance)
        {
            this.followIdentity = target;
            ICharacter npc = GetCharacterFromPool(this.Character.Playfield.Identity, target);
            if (npc == null)
            {
                this.StopFollow();
                return false;
            }

            DateTime now = DateTime.UtcNow;
            Vector3 start = this.UpdateMotionSegmentPosition(now);
            this.ResetFollowPosition();
            this.followIdentity = target;
            this.followStopDistance = Math.Max(0.0, stopDistance);
            Vector3 targetPosition = GetFollowTargetPosition(npc);
            this.followCoordinates = targetPosition;
            if (this.followStopDistance > 0.0 && start.Distance2D(targetPosition) <= this.followStopDistance)
            {
                this.Character.Coordinates(start);
                this.FaceToward(start, targetPosition);
                return true;
            }

            this.Run();
            this.FaceToward(start, targetPosition);
            this.SendMotionSegmentFollow("coordinate-follow", start, targetPosition, now);

            return true;
        }

        public bool Stand()
        {
            throw new NotImplementedException();
        }

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

        public bool Trade(Identity target)
        {
            if (ContentDrivenNpcDialogueRouter.TryStartDialogue(this.Character, target))
            {
                return true;
            }

            // Do we have a attached KnuBot?
            if ((this.KnuBot != null) && (this.KnuBot.Character.Target == null))
            {
                ICharacter source = Pool.Instance.GetObject<ICharacter>(this.Character.Playfield.Identity, target);
                this.FaceDialoguePartner(source);
                return this.KnuBot.StartDialog(source);
            }
            return false;
        }

        public bool FaceDialoguePartner(ICharacter source)
        {
            if (source == null || this.Character == null || this.Character.Playfield == null)
            {
                return false;
            }

            if (source.Playfield == null || !source.Playfield.Identity.Equals(this.Character.Playfield.Identity))
            {
                return false;
            }

            Vector3 normalizedDirection;
            if (!this.TryFaceToward(this.Character.RawCoordinates, source.RawCoordinates, out normalizedDirection))
            {
                return false;
            }

            SendWantedDirection(normalizedDirection);
            LogUtil.Debug(
                DebugInfoDetail.KnuBot,
                "NPC dialogue facing npc=" + this.Character.Identity.ToString(true)
                + " source=" + source.Identity.ToString(true));
            return true;
        }

        public bool UseItem(Identity itemPosition)
        {
            throw new NotImplementedException();
        }

        public bool TryUseBackpackContainer(Identity itemPosition)
        {
            return false;
        }

        public bool DeleteItem(int container, int slotNumber)
        {
            throw new NotImplementedException();
        }

        public bool SplitItemStack(Identity targetItem, int stackCount)
        {
            throw new NotImplementedException();
        }

        public bool JoinItemStack(Identity sourceItem, Identity targetItem)
        {
            throw new NotImplementedException();
        }

        public bool CombineItems(Identity sourceItem, Identity targetItem)
        {
            throw new NotImplementedException();
        }

        public bool TradeSkillSourceChanged(int inventoryPageId, int slotNumber)
        {
            throw new NotImplementedException();
        }

        public bool TradeSkillTargetChanged(int inventoryPageId, int slotNumber)
        {
            throw new NotImplementedException();
        }

        public bool TradeSkillBuildPressed(Identity targetItem)
        {
            throw new NotImplementedException();
        }

        public bool ChatCommand(string command, Identity target)
        {
            throw new NotImplementedException();
        }

        public bool Logout()
        {
            throw new NotImplementedException();
        }

        public void LogoffCharacter()
        {
        }

        public bool Login()
        {
            throw new NotImplementedException();
        }

        public bool StopLogout()
        {
            throw new NotImplementedException();
        }

        public bool GetTargetInfo(Identity target)
        {
            throw new NotImplementedException();
        }

        public bool TeamInvite(Identity target)
        {
            throw new NotImplementedException();
        }

        public bool TeamKickMember(Identity target)
        {
            throw new NotImplementedException();
        }

        public bool TeamLeave()
        {
            throw new NotImplementedException();
        }

        public bool TransferTeamLeadership(Identity target)
        {
            throw new NotImplementedException();
        }

        public bool TeamJoinRequest(Identity target)
        {
            throw new NotImplementedException();
        }

        public bool TeamJoinReply(bool accept, Identity requester)
        {
            throw new NotImplementedException();
        }

        public bool TeamJoinAccepted(Identity newTeamMember)
        {
            throw new NotImplementedException();
        }

        public bool TeamJoinRejected(Identity rejectingIdentity)
        {
            throw new NotImplementedException();
        }

        public void SendChangedStats()
        {
            Dictionary<int, uint> toPlayfield = new Dictionary<int, uint>();
            Dictionary<int, uint> toPlayer = new Dictionary<int, uint>();

            this.Character.Stats.GetChangedStats(toPlayer, toPlayfield);
            toPlayer.Clear();
            StatMessageHandler.Default.SendBulk(this.Character, toPlayer, toPlayfield);
        }

        public void CallFunction(Function function, IEntity caller)
        {
            FunctionCollection.Instance.CallFunction(
                function.FunctionType,
                this.Character,
                caller,
                this.Character,
                function.Arguments.Values.ToArray());
        }

        public void MoveTo(SmokeLounge.AOtomation.Messaging.GameData.Vector3 destination)
        {
            Vector3 dest = destination;
            Vector3 start = this.Character.Coordinates().coordinate;
            DateTime now = DateTime.UtcNow;
            if (start.Distance2D(dest) < 0.3f)
            {
                this.StopMovement();
                this.StopFollow();
                this.Character.RawCoordinates = destination;
                FollowTargetMessageHandler.Default.Send(this.Character, destination);
                return;
            }

            dest = dest - start;
            dest.y = 0;
            this.Character.Heading = (Quaternion)Quaternion.GenerateRotationFromDirectionVector(dest.Normalize());
            this.SendMotionSegmentFollow("moveto", start, destination, now);

            Coordinate c = new Coordinate(destination);
            this.followCoordinates = c.coordinate;
        }

        public void DoFollow()
        {
            Vector3 targetPosition = this.followCoordinates;
            if (!this.followIdentity.Equals(Identity.None))
            {
                ICharacter targetChar = GetCharacterFromPool(this.Character.Playfield.Identity, this.followIdentity);
                if (targetChar == null)
                {
                    // If target does not longer exist (death or zone or logoff) then stop following
                    this.StopFollow();
                    return;
                }

                targetPosition = GetFollowTargetPosition(targetChar);
            }

            // Do we have coordinates to follow?
            if (targetPosition.Distance2D(new Vector3()) < 0.01f)
            {
                return;
            }

            DateTime now = DateTime.UtcNow;
            Vector3 current = this.UpdateMotionSegmentPosition(now);
            this.followCoordinates = targetPosition;

            if (this.TryCompleteCoordinateFollow(current, targetPosition))
            {
                return;
            }

            if (this.followStopDistance > 0.0 && current.Distance2D(targetPosition) <= this.followStopDistance)
            {
                this.Character.Coordinates(current);
                this.FaceToward(current, targetPosition);
                return;
            }

            this.FaceToward(current, targetPosition);
            if (this.suppressMotionSegmentUpdates)
            {
                return;
            }

            if (this.ShouldSendMotionSegmentUpdate(current, targetPosition, now))
            {
                this.SendMotionSegmentFollow("coordinate-update", current, targetPosition, now);
            }
        }

        public void StartPatrolling()
        {
            Waypoint next = this.FindNextWaypoint();

            // If a suitable waypoint is found
            if (next != null)
            {
                if (next.Running)
                {
                    this.Run();
                }
                else
                {
                    this.Walk();
                }
                this.followCoordinates = next.Position;
                DateTime now = DateTime.UtcNow;
                Vector3 start = this.Character.Coordinates().coordinate;
                Vector3 temp = start - next.Position;
                temp.y = 0;
                this.Character.Heading = (Quaternion)Quaternion.GenerateRotationFromDirectionVector(temp).Normalize();
                LogUtil.Debug(DebugInfoDetail.Movement, "Direction: " + this.Character.Heading.ToString());
                this.SendMotionSegmentFollow("patrol-start", start, next.Position, now);
                this.StartMovement();
                LogUtil.Debug(DebugInfoDetail.Movement, "Walking to: " + this.followCoordinates);
            }
        }

        public bool IsFollowing()
        {
            return ((!this.followIdentity.Equals(Identity.None)) || (this.followCoordinates.x != 0.0f)
                    || (this.followCoordinates.y != 0.0f) || (this.followCoordinates.z != 0.0f));
        }

        public bool IsFollowing(Identity target)
        {
            return this.followIdentity.Equals(target);
        }

        public void StopFollowForCombatRange(Vector3 targetPosition)
        {
            Vector3 current = this.UpdateMotionSegmentPosition(DateTime.UtcNow);

            this.Character.Coordinates(current);
            this.FaceToward(current, targetPosition);
            this.LogChase("combat-stop", current, targetPosition);
            FollowTargetMessageHandler.Default.Send(this.Character, current);
            this.followIdentity = Identity.None;
            lock (this.followCoordinates)
            {
                this.followCoordinates = new Vector3();
            }

            this.ResetFollowPosition();
        }

        public void Run()
        {
            this.Character.UpdateMoveType(25); // Magic number: Switch to run
        }

        public void StopMovement()
        {
            this.Character.UpdateMoveType(2); // Magic number: Forward stop
        }

        public void Walk()
        {
            this.Character.UpdateMoveType(24); // Magic number: Switch to walk
        }

        public bool SaveToDatabase
        {
            get
            {
                return false;
            }
        }

        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (this.Client != null)
                {
                    this.Client = null;
                }
            }
        }

        private Waypoint FindNextWaypoint()
        {
            Waypoint result = null;
            if (this.Character.Waypoints.Count < 2)
            {
                return null;
            }
            if (this.Character.Waypoints.Count <= this.activeWaypoint)
            {
                this.activeWaypoint = 0;
            }
            int len = this.Character.Waypoints.Count;
            do
            {
                this.activeWaypoint = (this.activeWaypoint + 1) % len;
                result = this.Character.Waypoints[this.activeWaypoint];
            }
            while (result.Position.Distance2D(this.Character.Coordinates().coordinate) < 0.2f);
            return result;
        }

        public void StartMovement()
        {
            this.Character.UpdateMoveType(1); // Magic number: Forward start
        }

        ~NPCController()
        {
            LogUtil.Debug(DebugInfoDetail.Memory, "NPC Controller finished");
            LogUtil.Debug(DebugInfoDetail.Memory, new StackTrace().ToString());
            this.Dispose(false);
        }

        public bool Move(
            int moveType,
            Coordinate newCoordinates,
            SmokeLounge.AOtomation.Messaging.GameData.Quaternion heading)
        {
            return false;
        }

        public void Move()
        {
        }

        public void StopFollow()
        {
            this.followIdentity = Identity.None;
            this.ResetFollowPosition();
            lock (this.followCoordinates)
            {
                this.followCoordinates = new Vector3();
            }
        }

        public void SetKnuBot(BaseKnuBot knubot)
        {
            this.KnuBot = knubot;
            this.AiProfile = NpcAiProfile.Social;
        }
    }
}
