namespace ZoneEngine_New.Core.Movement
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;

    using SmokeLounge.AOtomation.Messaging.GameData;
    using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;

    using AORebirth.World.Pathfinding;

    using N3Lite;
    using N3Lite.Surfaces;

    using ZoneEngine_New.Core.Ai;
    using ZoneEngine_New.Core.Entities;
    using ZoneEngine_New.Core.Metrics;
    using ZoneEngine_New.Core.Playfield;
    using ZoneEngine_New.Core.Playfield.Locality;
    using ZoneEngine_New.Core.WorldSimulation;

    using Quaternion = AORebirth.Core.Vector.Quaternion;
    using Vector3 = AORebirth.Core.Vector.Vector3;
    using MsgQuaternion = SmokeLounge.AOtomation.Messaging.GameData.Quaternion;
    using MsgVector3 = SmokeLounge.AOtomation.Messaging.GameData.Vector3;
    using CharMovement = N3Lite.AORules.CharMovement;
    using CharMovementRules = N3Lite.AORules.CharMovementRules;
    using MovementStats = N3Lite.AORules.MovementStats;
    using RulesStatus = N3Lite.AORules.CharMovementStatus;

    /// <summary>
    /// Server locomotion: one <see cref="CharMovement"/> (N3Lite.AORules) per character, the game's own movement state
    /// machine, speed curves, jump, flight and turn rates. A player is driven by the wire's movement actions through
    /// <see cref="CharMovement.Act"/>; an NPC takes no Act input, only states and the follow paths it announces to
    /// observers (<see cref="CharMovement.SetFollowPath"/>), so it moves along them the way their clients move it. The
    /// client-position gates, flight authority and navmesh planning stay here.
    /// </summary>
    public sealed class CharacterMotor
    {
        const MovementFlags TranslationFlags =
            MovementFlags.Forward | MovementFlags.Backward
            | MovementFlags.StrafeLeft | MovementFlags.StrafeRight;

        const MovementFlags TurnFlags = MovementFlags.TurnLeft | MovementFlags.TurnRight;

        readonly Character _character;
        readonly CharMovement _move;
        readonly CharVehicleSim _sim;
        readonly NpcVehicleSim? _npc;
        readonly List<Vector3> _path = new();
        readonly List<Vec3> _followScratch = new();
        readonly List<Vector3> _navigateScratch = new();
        readonly List<System.Numerics.Vector3> _navMeshScratch = new();

        Quaternion? _syncedRotation;
        int _playerPathIndex = -1;
        // The character a player's acknowledged /follow steers at, or null.
        Character? _followTarget;
        bool _voidWarned;
        bool _holdingOverVoid;
        bool _hadFlightAuthority;

        public CharacterMotor(Character character)
        {
            _character = character ?? throw new ArgumentNullException(nameof(character));
            _move = new CharMovement(isNpc: !character.IsPlayer, MovementConfig.Mass, MovementConfig.BodyRadius);
            _sim = _move.Core.Vehicle;
            _npc = _move.Core.Npc;
            _move.FlagsSet += flag => SetCharacterFlag(flag, true);
            _move.FlagsCleared += flag => SetCharacterFlag(flag, false);
            _move.Core.JumpStarted += () => Jumped?.Invoke();
            // Status.LastSpeedMode starts at Walk inside the package; a fresh body is a standing runner.
            _move.ApplyStatus(RulesStatus.Fields.Default, (int)MovementState.Run);
        }

        public MovementFlags MovementFlags => CurrentFlags;

        public MovementState State => (MovementState)_move.State;

        bool IsPlayer => _npc == null;

        bool IsPlayerFlying => IsPlayer && _move.State == (int)MovementState.Fly;

        /// <summary>The input flags, from the movement status (N3Lite's flags share our bit layout).</summary>
        MovementFlags CurrentFlags => (MovementFlags)(int)_move.Core.Flags;

        /// <summary>The body's arrive brake distance at its current top speed.</summary>
        public float SlowingDistance => _sim.SlowingDistance;

        /// <summary>The vehicle itself.</summary>
        public CharVehicleSim Vehicle => _sim;

        bool Jumping => _sim.JumpHeight != 0f;

        public CharMovementStatus BuildMovementStatus()
            => IsPlayer ? BuildPlayerMovementStatus() : BuildNpcMovementStatus();

        /// <summary>
        /// A player's status is the movement state machine's own fields. A server-driven path shows as forward.
        /// Players carry their world velocity (and speed factor 1.0) the way retail sends them: observers that spawn a
        /// player mid-run move him from this, and a zero velocity left him animating in place, snapping on updates. The
        /// 0x1A float goes out only while a translation is held, since on a standing player it set the client running
        /// at login.
        /// </summary>
        CharMovementStatus BuildPlayerMovementStatus()
        {
            RulesStatus.Fields f = _move.Status.Current;
            if (HasPath)
            {
                f.FwdState = RulesStatus.Active;
                f.FwdDir = RulesStatus.DirForward;
            }

            bool forward = f.FwdState == RulesStatus.Active && f.FwdDir == RulesStatus.DirForward;
            bool backward = f.FwdState == RulesStatus.Active && f.FwdDir == RulesStatus.DirReverse;
            bool moving = f.FwdState == RulesStatus.Active || f.StrafeState == RulesStatus.Active;
            ComputeSpawnVelocity(forward, backward, out float velocityX, out float velocityY, out float velocityZ);

            return new CharMovementStatus
            {
                VelocityX = velocityX,
                VelocityY = velocityY,
                VelocityZ = velocityZ,
                PlayerSpeedFactor = moving ? 1f : 0f,
                Header = 0x80,
                ModeId = (byte)f.Mode,
                FwdState = (byte)f.FwdState,
                FwdDir = (byte)f.FwdDir,
                StrafeState = (byte)f.StrafeState,
                StrafeDir = (byte)f.StrafeDir,
                ElevateState = (byte)f.ElevState,
                ElevateDir = (byte)f.ElevDir,
                TurnState = (byte)f.TurnState,
                TurnDir = (byte)f.TurnDir,
                JumpState = (byte)f.JumpState,
                LastSpeedMode = (byte)_move.LastSpeedMode
            };
        }

        /// <summary>An NPC moves only along its follow path, which shows as forward; no velocity block.</summary>
        CharMovementStatus BuildNpcMovementStatus()
        {
            int mode = _move.State;
            bool forward = HasPath && mode != (int)MovementState.Sit;
            int lastSpeed = mode is (int)MovementState.Walk or (int)MovementState.Run ? mode : _move.LastSpeedMode;
            return new CharMovementStatus
            {
                Header = 0,
                ModeId = (byte)mode,
                FwdState = (byte)(forward ? RulesStatus.Active : RulesStatus.Idle),
                FwdDir = (byte)(forward ? RulesStatus.DirForward : 0),
                StrafeState = RulesStatus.Idle,
                StrafeDir = 0,
                ElevateState = RulesStatus.Idle,
                ElevateDir = 0,
                TurnState = RulesStatus.Idle,
                TurnDir = 0,
                JumpState = (byte)(Jumping ? RulesStatus.Airborne : RulesStatus.Idle),
                LastSpeedMode = (byte)lastSpeed
            };
        }

        /// <summary>
        /// The body's velocity. Until it has picked up speed, a held forward/back key counts as full speed along the
        /// heading (retail: Aagent's (0.852, 0, -7.13) is his heading's forward axis times his run speed), so a player who
        /// just pressed forward is never sent as standing still.
        /// </summary>
        void ComputeSpawnVelocity(bool forward, bool backward, out float x, out float y, out float z)
        {
            Vec3 velocity = _sim.Velocity;
            x = velocity.X;
            y = velocity.Y;
            z = velocity.Z;
            if ((x * x) + (z * z) >= 0.01f || forward == backward)
                return;

            // Heading forward axis: the unit Z axis rotated by the character's quaternion.
            Quaternion q = _character.Rotation;
            float dirX = 2f * ((q.xf * q.zf) + (q.wf * q.yf));
            float dirZ = 1f - (2f * ((q.xf * q.xf) + (q.yf * q.yf)));
            float speed = _sim.MaxVel * (forward ? 1f : -1f);
            x = dirX * speed;
            y = 0f;
            z = dirZ * speed;
        }

        /// <summary>
        /// Directional CharDCMove packets for a client that just spawned this character.
        /// Posture and speed-mode switches are omitted. Unknown is 0 so the client applies
        /// each move to this identity. Empty when the character is not moving directionally.
        /// </summary>
        public List<CharDCMoveMessage> BuildSpawnMoves()
        {
            var messages = new List<CharDCMoveMessage>(4);
            if (!IsPlayer)
            {
                if (HasPath)
                    messages.Add(CreateMove(MovementAction.ForwardStart));
                return messages;
            }

            RulesStatus.Fields f = _move.Status.Current;
            if (HasPath || (f.FwdState == RulesStatus.Active && f.FwdDir == RulesStatus.DirForward))
                messages.Add(CreateMove(MovementAction.ForwardStart));
            else if (f.FwdState == RulesStatus.Active && f.FwdDir == RulesStatus.DirReverse)
                messages.Add(CreateMove(MovementAction.BackwardStart));
            if (f.StrafeState == RulesStatus.Active && f.StrafeDir == RulesStatus.DirLeft)
                messages.Add(CreateMove(MovementAction.StrafeLeftStart));
            else if (f.StrafeState == RulesStatus.Active && f.StrafeDir == RulesStatus.DirRight)
                messages.Add(CreateMove(MovementAction.StrafeRightStart));
            if (f.TurnState == RulesStatus.Turning && f.TurnDir == RulesStatus.DirLeft)
                messages.Add(CreateMove(MovementAction.TurnLeftStart));
            else if (f.TurnState == RulesStatus.Turning && f.TurnDir == RulesStatus.DirRight)
                messages.Add(CreateMove(MovementAction.TurnRightStart));
            if (f.ElevState == RulesStatus.Active && f.ElevDir == RulesStatus.DirUp)
                messages.Add(CreateMove(MovementAction.ElevateUpStart));
            if (f.JumpState == RulesStatus.Airborne)
                messages.Add(CreateMove(MovementAction.JumpStart));
            return messages;
        }

        CharDCMoveMessage CreateMove(MovementAction action)
        {
            Quaternion rotation = _character.Rotation;
            Vector3 position = _character.Position;
            return new CharDCMoveMessage
            {
                Identity = _character.Identity,
                Unknown = 0,
                MoveType = (byte)action,
                Heading = new MsgQuaternion
                {
                    X = rotation.xf,
                    Y = rotation.yf,
                    Z = rotation.zf,
                    W = rotation.wf
                },
                Coordinates = new MsgVector3
                {
                    X = position.xf,
                    Y = position.yf,
                    Z = position.zf
                },
                Unknown1 = 0,
                AuxA = 0,
                AuxB = 0
            };
        }

        /// <summary>An NPC is following its announced path; a player body is on a server-driven path.</summary>
        public bool HasPath => _npc != null
            ? _npc.HasFollowTarget
            : _playerPathIndex >= 0 && _playerPathIndex < _path.Count;

        /// <summary>
        /// True while translating (keys/path) or still carrying planar speed.
        /// Ranged weapon ticks use this so a held run key and residual slide both pause fire.
        /// </summary>
        public bool IsMoving =>
            (CurrentFlags & TranslationFlags) != 0
            || HasPath
            || _sim.Speed > MovementConfig.SpeedStopEpsilon;

        /// <summary>In the air (falling or jumping) and not flying.</summary>
        public bool IsFalling => _sim.Airborne && _move.State != (int)MovementState.Fly;

        public event Action? PathCompleted;

        /// <summary>Raised when a jump actually starts (not a failed sit/unarmed attempt).</summary>
        public event Action? Jumped;

        /// <summary>The movement rules' stats: <see cref="CharMovement.SetStats"/> takes the whole struct each time.</summary>
        public void RefreshFromStats() => _move.SetStats(BuildMovementStats());

        /// <summary>The full (buffed) values of the stats the movement rules read.</summary>
        MovementStats BuildMovementStats() => new()
        {
            RunSpeed = MovementRunSpeed(),
            Strength = _character.Stats.GetOrZero(CharacterStat.Strength),
            Agility = _character.Stats.GetOrZero(CharacterStat.Agility),
            GmLevel = _character.Stats.GetOrZero(CharacterStat.GmLevel),
            Features = ResolveFeatures(),
            OrientationMode = _character.Stats.GetOrZero(CharacterStat.OrientationMode),
            TurnSpeed = _character.Stats.GetOrZero(CharacterStat.TurnSpeed),
            MechData = _character.Stats.GetOrZero(CharacterStat.MechData),
            Scale = _character.Stats.GetOrZero(CharacterStat.Scale)
        };

        /// <summary>
        /// Run Speed as the movement rules take it: a player's full Run Speed without its ability trickle. With the trickle
        /// in, Delmus (Run Speed 1623, trickle 294) moved at 10.90 against the client's 9.83.
        /// </summary>
        int MovementRunSpeed()
        {
            int runSpeed = _character.Stats.GetOrZero(CharacterStat.RunSpeed);
            if (_character is Player player)
                runSpeed -= player.SkillCatalog.Trickle(_character.Stats, CharacterStat.RunSpeed);
            return Math.Max(0, runSpeed);
        }

        /// <summary>
        /// Features (stat 224) is the body's MonsterData record: an NPC has it copied on at spawn, a player never stores
        /// it. Bit 0x4 (can move) gates sitting and standing in the state machine and an NPC's following at all, so an
        /// unresolved body counts as mobile.
        /// </summary>
        int ResolveFeatures()
        {
            if (_character.Stats.TryGetValue(CharacterStat.Features, out int stored) && !StatCollection.IsUnset(stored))
                return stored;

            int monsterData = _character.Stats.GetOrZero(CharacterStat.MonsterData);
            if (_character.Playfield?.GetService<GameData.IGameData>() is { } gameData
                && gameData.TryGetMonsterFeatures(monsterData, out int features))
                return features;

            return GameData.MonsterBodyFeatures.CanMove;
        }

        public void OnStatChanged(CharacterStat stat, int previous, int next, bool isInitialSet)
        {
            switch (stat)
            {
                case CharacterStat.RunSpeed:
                case CharacterStat.Strength:
                case CharacterStat.Agility:
                case CharacterStat.Stamina:
                case CharacterStat.Sense:
                case CharacterStat.GmLevel:
                case CharacterStat.Features:
                case CharacterStat.MonsterData:
                case CharacterStat.OrientationMode:
                case CharacterStat.TurnSpeed:
                case CharacterStat.MechData:
                case CharacterStat.Scale:
                    RefreshFromStats();
                    break;
            }
        }

        /// <summary>Starts a new path from where the character stands.</summary>
        public void SetPath(IReadOnlyList<Vector3> waypoints)
        {
            ClearPath();
            if (IsPlayer)
            {
                _move.Apply((int)MovementAction.FullStop);
                CopyPlayerWaypoints(waypoints);
            }
            else
                FollowPath(waypoints);
        }

        /// <summary>
        /// Plans a corridor through the playfield navmesh when one is loaded; otherwise a single waypoint.
        /// Replaces an active path without halting residual velocity.
        /// </summary>
        public void NavigateTo(Vector3 destination)
        {
            PlanIntoScratch(destination);
            if (HasPath)
                ReplacePath(_navigateScratch);
            else
                SetPath(_navigateScratch);
        }

        /// <summary>
        /// Remaining path points for SCFU. Empty when idle so HasWaypoints stays clear. For an NPC these are the
        /// follow points it has not yet reached, i.e. what it is still steering along.
        /// </summary>
        public MsgVector3[] CopyRemainingWaypoints()
        {
            if (!HasPath)
                return [];

            if (_npc != null)
            {
                IReadOnlyList<Vec3> queue = _npc.FollowQueue;
                var points = new MsgVector3[queue.Count];
                for (int i = 0; i < queue.Count; i++)
                    points[i] = new MsgVector3 { X = queue[i].X, Y = queue[i].Y, Z = queue[i].Z };
                return points;
            }

            var remaining = new MsgVector3[_path.Count - _playerPathIndex];
            for (int i = _playerPathIndex; i < _path.Count; i++)
                remaining[i - _playerPathIndex] = new MsgVector3 { X = _path[i].xf, Y = _path[i].yf, Z = _path[i].zf };
            return remaining;
        }

        /// <summary>Flat meters of path still ahead of the body. Zero when idle.</summary>
        public float RemainingPathMeters()
        {
            if (!HasPath)
                return 0f;

            if (_npc != null)
            {
                IReadOnlyList<Vec3> queue = _npc.FollowQueue;
                if (queue.Count == 0)
                    return 0f;

                float ahead = FlatDistance(_sim.Position, queue[0]);
                for (int i = 1; i < queue.Count; i++)
                    ahead += FlatDistance(queue[i - 1], queue[i]);
                return ahead;
            }

            float remaining = FlatDistance(_character.Position, _path[_playerPathIndex]);
            for (int i = _playerPathIndex + 1; i < _path.Count; i++)
                remaining += FlatDistance(_path[i - 1], _path[i]);
            return remaining;
        }

        /// <summary>How far behind its target a /follow stops (Gamecode.dll player vehicle forward channel, 10071537).</summary>
        public const float FollowDistanceMeters = 4f;

        /// <summary>The character this player's /follow steers at, or null.</summary>
        public Character? FollowTarget => _followTarget;

        /// <summary>
        /// Starts the server's copy of a player /follow, as every client that applies the FollowTarget ack runs it: each
        /// tick the body steers at the point <see cref="FollowDistanceMeters"/> short of the target and stops inside it.
        /// The follower's client sends no movement actions meanwhile, only an Update about every 5 s, which corrects the
        /// position. Its next other movement action ends the follow (<see cref="Consume"/>), as it does on the clients.
        /// </summary>
        public void BeginFollow(Character target)
        {
            ArgumentNullException.ThrowIfNull(target);
            if (!IsPlayer || ReferenceEquals(target, _character))
                return;

            ClearPath();
            // As a server path does: standing status, and the follower drives the body directly.
            _move.Apply((int)MovementAction.FullStop);
            _followTarget = target;
            _move.OnWaypointPath = true;
        }

        /// <summary>Ends a /follow: the status takes the body back, stopped, as when a server path ends.</summary>
        public void EndFollow()
        {
            if (_followTarget == null)
                return;

            _followTarget = null;
            _move.OnWaypointPath = false;
            _sim.SetForwardDrive(0f);
            _sim.Halt();
            _sim.SetDirection(1);
        }

        /// <summary>
        /// One tick of the follow: hold while held in place (rooted or stunned: the client's Features lack 0x4), stop
        /// inside <see cref="FollowDistanceMeters"/>, otherwise drive at the point that far short of the target. Ends when
        /// the target is gone, dead or on another playfield, or the follower died.
        /// </summary>
        void SteerFollow()
        {
            Character target = _followTarget!;
            if (_character.IsDead || target.IsDead || !ReferenceEquals(target.Playfield, _character.Playfield))
            {
                EndFollow();
                return;
            }

            if (_character.IsRooted)
            {
                _sim.SetForwardDrive(0f);
                return;
            }

            float dx = (float)target.Position.x - _sim.Position.X;
            float dz = (float)target.Position.z - _sim.Position.Z;
            float distance = MathF.Sqrt((dx * dx) + (dz * dz));
            if (distance <= FollowDistanceMeters)
            {
                _sim.SetForwardDrive(0f);
                return;
            }

            var toward = new Vec3(dx / distance, 0f, dz / distance);
            _sim.SetRelRot(Quat.LookRotation(toward, Vec3.ReferenceUp));
            _sim.SetForwardDrive(1f);
        }

        public void ClearPath()
        {
            bool had = HasPath || _path.Count > 0;
            _path.Clear();
            _playerPathIndex = -1;
            if (_npc != null && _npc.HasFollowTarget)
                _move.Core.ClearPath();
            if (!had)
                return;

            if (IsPlayer)
            {
                // The path follower drove the body directly; the status (stopped since SetPath) takes it back.
                _move.OnWaypointPath = false;
                _sim.SetForwardDrive(0f);
                _sim.Halt();
                _sim.SetDirection(1);
            }

            PathCompleted?.Invoke();
        }

        /// <summary>
        /// Swaps the active path without clearing velocity or raising <see cref="PathCompleted"/>: the body keeps its
        /// speed into the new path.
        /// </summary>
        public void ReplacePath(IReadOnlyList<Vector3> waypoints)
        {
            if (!IsPlayer)
            {
                FollowPath(waypoints);
                return;
            }

            _path.Clear();
            _playerPathIndex = -1;
            _move.OnWaypointPath = false;
            CopyPlayerWaypoints(waypoints);
        }

        /// <summary>
        /// An NPC's path is the coordinate list its FollowTarget announces (<see cref="NpcFollowTarget.AnnounceCoordinatePath"/>):
        /// where it stands, then the waypoints. The follower snaps to that start and queues the rest, the way an
        /// observing client does, so both move it alike, up to the same <see cref="CharMovement.FollowPathCapacity"/>.
        /// </summary>
        void FollowPath(IReadOnlyList<Vector3> waypoints)
        {
            if (waypoints == null || waypoints.Count == 0)
                return;

            _followScratch.Clear();
            Vector3 start = _character.Position;
            _followScratch.Add(ToVec3(start));
            for (int i = 0; i < waypoints.Count; i++)
                _followScratch.Add(ToVec3(waypoints[i]));

            _move.SetFollowPath(_followScratch, snapToStart: true);
            _character.Position = ToVector3(_sim.Position);
        }

        /// <summary>A player body's server-driven path: Lost-Eden's point-and-drive follower, with Act locked out.</summary>
        void CopyPlayerWaypoints(IReadOnlyList<Vector3> waypoints)
        {
            if (waypoints == null || waypoints.Count == 0)
                return;

            Vector3 start = _character.Position;
            _path.Add(new Vector3(start.x, start.y, start.z));

            // Copy components: callers often pass live Position references that move every tick.
            for (int i = 0; i < waypoints.Count; i++)
            {
                Vector3 point = waypoints[i];
                if (i == 0
                    && Math.Abs(point.x - start.x) < 1e-4
                    && Math.Abs(point.y - start.y) < 1e-4
                    && Math.Abs(point.z - start.z) < 1e-4)
                    continue;

                _path.Add(new Vector3(point.x, point.y, point.z));
            }

            _playerPathIndex = 1;
            _move.OnWaypointPath = HasPath;
        }

        void PlanIntoScratch(Vector3 destination)
        {
            _navigateScratch.Clear();
            NavMeshPathfinder? finder = _character.Playfield?.Pathfinder;
            if (finder != null)
            {
                _navMeshScratch.Clear();
                Vector3 start = _character.Position;
                Playfield? playfield = _character.Playfield;
                if (playfield != null && playfield.TrySnapFeetToFloor(start, out Vector3 startFloor))
                    start = startFloor;
                if (playfield != null && playfield.TrySnapFeetToFloor(destination, out Vector3 destFloor))
                    destination = destFloor;
                if (PathDiag.TryFindPath(
                    finder,
                    new System.Numerics.Vector3((float)start.x, (float)start.y, (float)start.z),
                    new System.Numerics.Vector3((float)destination.x, (float)destination.y, (float)destination.z),
                    _navMeshScratch,
                    PathDiag.Motor)
                    && _navMeshScratch.Count > 0)
                {
                    for (int i = 0; i < _navMeshScratch.Count; i++)
                    {
                        System.Numerics.Vector3 point = _navMeshScratch[i];
                        _navigateScratch.Add(new Vector3(point.X, point.Y, point.Z));
                    }

                    return;
                }
            }

            _navigateScratch.Add(new Vector3(destination.x, destination.y, destination.z));
        }

        public void Halt() => _sim.Halt();

        /// <summary>
        /// Root landed: drop every movement flag, any path and residual speed. True when the
        /// character was moving, so the caller can tell observers it stopped.
        /// </summary>
        public bool StopForRoot()
        {
            bool moving = HasPath || CurrentFlags != MovementFlags.None || _sim.Speed > MovementConfig.SpeedStopEpsilon;
            ClearPath();
            if (IsPlayer)
                _move.Apply((int)MovementAction.FullStop);
            _sim.Halt();
            return moving;
        }

        public void ResetForPlayfieldTransfer(Vector3 position)
        {
            // Key releases on the old/loading connection may never reach this motor.
            // The destination must not inherit input, a path or a follow from the previous world.
            ClearPath();
            EndFollow();
            // A full status, no transition: standing still in the current mode.
            RulesStatus.Fields fields = RulesStatus.Fields.Default;
            fields.Mode = _move.State;
            _move.ApplyStatus(fields, _move.LastSpeedMode);
            SyncModeStats();
            Warp(position);
        }

        /// <summary>
        /// Places the vehicle without steering. A rotation goes through <c>SetRelRot</c>, so a warp
        /// that keeps its velocity carries it into the new facing.
        /// </summary>
        public void Warp(Vector3 position, Quaternion? rotation = null, bool resetVelocity = true)
        {
            _character.Position = position;
            _sim.Position = ToVec3(position);
            _holdingOverVoid = false;
            // The server placed the body: the client's next move is measured from here and from now.
            _lastAcceptedMoveUtc = DateTime.UtcNow;
            if (rotation != null)
            {
                _character.Rotation = rotation;
                ApplyHeadingToSim(rotation);
            }

            if (!resetVelocity)
                return;

            _sim.Halt();
            _sim.LandNow(_sim.Position.Y);
            if (_sim.FallingEnabled)
                _sim.BeginFalling();
        }

        /// <summary>
        /// Applies an inbound move. Returns false when the position is rejected, which changes nothing, or when the
        /// movement rules refuse the action, which keeps the position and heading; either way observers are not told.
        /// </summary>
        public bool Consume(CharDCMoveMessage message)
        {
            ArgumentNullException.ThrowIfNull(message);
            JumpCorrectionDue = false;
            if (_character.IsRooted)
                return ConsumeRooted(message);

            if (!TryAcceptClientPosition(message))
                return false;

            ClearPath();
            var action = (MovementAction)message.MoveType;
            // A following client sends only Updates; any other action is the player taking the controls back, which ends
            // the follow on every client.
            if (action != MovementAction.Update)
                EndFollow();
            // A position update carries no transition, so it never goes through the state machine.
            bool accepted = action == MovementAction.Update || ApplyAction(action);
            ApplyClientHeading(message);
            return accepted;
        }

        /// <summary>A rooted client that reported a position this far off gets pulled back.</summary>
        const float RootDriftMeters = 0.25f;

        /// <summary>Seconds between pull-backs, so a client pressing forward is not flooded.</summary>
        const double RootCorrectionSeconds = 0.5;

        DateTime _lastRootCorrectionUtc;

        /// <summary>True when the last rooted move drifted and the client needs the server position again.</summary>
        public bool RootCorrectionDue { get; private set; }

        /// <summary>True when the last move was refused as a hop the character could not have made (<see cref="IsPlausibleClientMove"/>).</summary>
        public bool JumpCorrectionDue { get; private set; }

        DateTime _lastAcceptedMoveUtc;
        DateTime _lastJumpCorrectionUtc;

        /// <summary>
        /// EXPLOIT guard: a reported position farther (XZ) from the server's than the character could have run since its
        /// last accepted move is refused and the client pulled back (<see cref="JumpCorrectionDue"/>): the vehicle's top
        /// speed over the time elapsed (credited up to <see cref="MovementConfig.ClientMoveElapsedCreditSeconds"/>) plus
        /// a lag allowance and a little slack. A server placement (<see cref="Warp"/>) restarts the clock, so a stale
        /// position sent after a teleport or zone landing is refused too. Flying is judged the same way: a flyer's top
        /// speed is its run speed. A GM is not judged.
        /// </summary>
        bool IsPlausibleClientMove(Player player, float x, float z, DateTime now)
        {
            if (player.Stats.GetOrZero(CharacterStat.GmLevel) > 0)
                return true;

            double elapsed = _lastAcceptedMoveUtc == default
                ? 0.0
                : Math.Clamp((now - _lastAcceptedMoveUtc).TotalSeconds, 0.0, MovementConfig.ClientMoveElapsedCreditSeconds);
            double allowed = MovementConfig.ClientMoveSlackMeters
                + (Math.Max(0f, _sim.MaxVel) * (elapsed + MovementConfig.ClientMoveLagAllowanceSeconds));
            double dx = x - _character.Position.x;
            double dz = z - _character.Position.z;
            double distance = Math.Sqrt((dx * dx) + (dz * dz));
            if (distance <= allowed)
                return true;

            if ((now - _lastJumpCorrectionUtc).TotalSeconds >= RootCorrectionSeconds)
            {
                JumpCorrectionDue = true;
                _lastJumpCorrectionUtc = now;
                player.Logger.Warn(string.Format(
                    CultureInfo.InvariantCulture,
                    "Rejected CharDCMove hop character={0} from=({1:F1},{2:F1}) to=({3:F1},{4:F1}) distance={5:F1}m allowed={6:F1}m maxVel={7:F1}",
                    player.Identity.Instance, _character.Position.x, _character.Position.z, x, z, distance, allowed, _sim.MaxVel));
            }

            return false;
        }

        /// <summary>
        /// Rooted: heading and turns still apply, but the position stays the server's and every
        /// translation start becomes its stop. The rewritten message is what observers see.
        /// </summary>
        bool ConsumeRooted(CharDCMoveMessage message)
        {
            RootCorrectionDue = false;
            Vector3 held = _character.Position;
            if (message.Coordinates != null)
            {
                // A root or stun holds the body where it stands, not where it hangs: one landing mid-jump or mid-fall
                // leaves the held point in the air while the client keeps falling. Judging that drop as drift pulled the
                // client back up every pull-back interval for the whole stun. So drift is horizontal only, and the held
                // body comes down with the client (never up), no lower than the floor.
                double dx = message.Coordinates.X - held.x;
                double dz = message.Coordinates.Z - held.z;
                DateTime now = DateTime.UtcNow;
                if ((dx * dx) + (dz * dz) > RootDriftMeters * RootDriftMeters
                    && (now - _lastRootCorrectionUtc).TotalSeconds >= RootCorrectionSeconds)
                {
                    RootCorrectionDue = true;
                    _lastRootCorrectionUtc = now;
                }

                float reportedY = message.Coordinates.Y;
                if (reportedY < held.yf)
                {
                    Playfield? playfield = _character.Playfield;
                    float y = reportedY;
                    if (playfield != null
                        && playfield.TrySnapFeetToFloor(new Vector3(held.x, reportedY, held.z), out Vector3 floor)
                        && y < (float)floor.y)
                        y = (float)floor.y;
                    if (y < held.yf)
                    {
                        held = new Vector3(held.x, y, held.z);
                        _character.Position = held;
                        _sim.Position = new Vec3(held.xf, y, held.zf);
                    }
                }

                message.Coordinates = new MsgVector3(held.xf, held.yf, held.zf);
            }

            MovementAction action = RootedAction((MovementAction)message.MoveType);
            message.MoveType = (byte)action;

            ClearPath();
            _sim.Halt();
            ApplyAction(action);
            ApplyClientHeading(message);
            return true;
        }

        static MovementAction RootedAction(MovementAction action) => action switch
        {
            MovementAction.ForwardStart => MovementAction.ForwardStop,
            MovementAction.BackwardStart => MovementAction.BackwardStop,
            MovementAction.StrafeRightStart => MovementAction.StrafeRightStop,
            MovementAction.StrafeLeftStart => MovementAction.StrafeLeftStop,
            MovementAction.JumpStart => MovementAction.JumpStop,
            MovementAction.ElevateUpStart => MovementAction.ElevateUpStop,
            MovementAction.ElevateDownStart => MovementAction.ElevateDownStop,
            _ => action
        };

        bool TryAcceptClientPosition(CharDCMoveMessage message)
        {
            // Playfield XZ is gated below; the distance since the last accepted move by IsPlausibleClientMove.
            if (message.Coordinates == null)
                return true;

            float x = message.Coordinates.X;
            float y = message.Coordinates.Y;
            float z = message.Coordinates.Z;

            Playfield? playfield = _character.Playfield;
            if (playfield != null)
            {
                PlayfieldLocality locality = playfield.GetRequiredService<PlayfieldLocality>();
                if (!locality.ContainsWorldPosition(x, z))
                {
                    if (_character is Player rejected)
                    {
                        rejected.Logger.Warn(
                            string.Format(
                                CultureInfo.InvariantCulture,
                                "Rejected CharDCMove outside playfield bounds character={0} pos=({1:F3},{2:F3},{3:F3}) world=({4:F0}x{5:F0})",
                                _character.Identity.Instance,
                                x,
                                y,
                                z,
                                locality.WorldSizeX,
                                locality.WorldSizeZ));
                    }

                    return false;
                }
            }

            DateTime now = DateTime.UtcNow;
            if (_character is Player mover && !IsPlausibleClientMove(mover, x, z, now))
                return false;

            var requested = new Vector3(x, y, z);
            if (playfield != null
                && playfield.TrySnapFeetToFloor(requested, out Vector3 floor)
                && y < (float)floor.y)
            {
                y = (float)floor.y;
                message.Coordinates.Y = y;
            }

            _character.Position = new Vector3(x, y, z);
            _sim.Position = new Vec3(x, y, z);
            _lastAcceptedMoveUtc = now;
            return true;
        }

        /// <summary>Client heading through <c>SetRelRot</c>, which re-aims residual velocity along it.</summary>
        void ApplyClientHeading(CharDCMoveMessage message)
        {
            var heading = new Quaternion(
                message.Heading.X,
                message.Heading.Y,
                message.Heading.Z,
                message.Heading.W);
            _character.Rotation = heading;
            ApplyHeadingToSim(heading);
        }

        void ApplyHeadingToSim(Quaternion heading)
        {
            _syncedRotation = heading;
            Vec3 forward = ToQuat(heading) * Vec3.ReferenceForward;
            if (IsPlayerFlying)
            {
                // Flying moves along the pitched heading, as the client does (live: heading forward (0.115, 0.426, 0.897)
                // climbed 19 m in 3 s). LookRotation projects the forward onto the plane normal to its up, so the up
                // given is already perpendicular to the forward; ReferenceUp would flatten the pitch away.
                if (forward.LengthSquared < 1e-8f)
                    return;
                forward /= forward.Length;
                if (Math.Abs(forward.Y) < 0.999f)
                {
                    Vec3 up = Vec3.ReferenceUp - (forward * forward.Y);
                    _sim.SetRelRot(Quat.LookRotation(forward, up / up.Length));
                    return;
                }
            }

            forward.Y = 0f;
            if (forward.LengthSquared < 1e-8f)
                return;

            _sim.SetRelRot(Quat.LookRotation(forward / forward.Length, _sim.SurfaceNormal));
        }

        /// <summary>
        /// One step of the movement rules' body against the playfield surface, from where the character stands. Over a
        /// hole in the playfield's collision the body holds where it is instead of free-falling. With no surface at all
        /// a player holds (its client places it), while an NPC still follows its path at its altitude; gravity itself
        /// stays the rules' own either way.
        /// </summary>
        public void Tick(double deltaTime)
        {
            RefreshFlightAuthority();
            if (IsPlayer)
                RefreshAiControl();
            float dt = (float)deltaTime;
            if (dt <= 0f)
                return;

            ISurface? surface = BindSurface();
            _sim.Position = ToVec3(_character.Position);
            // Flying, the body re-pitches itself along its velocity every step; the client's heading steers it, so it
            // goes back on every tick.
            if (_character.Rotation != null
                && (IsPlayerFlying || !ReferenceEquals(_character.Rotation, _syncedRotation)))
                ApplyHeadingToSim(_character.Rotation);

            if (_npc != null)
                SettleNpcVehicle();
            else if (surface == null)
            {
                // No collision to fall onto (the playfield has no surface geometry): the body holds its altitude, as
                // HoldOverVoid does over a hole, instead of staying airborne forever and refusing nanos as falling.
                if (_sim.Airborne)
                    _sim.LandNow(_sim.Position.Y);
                return;
            }

            bool idle = !HasPath
                && _followTarget == null
                && (CurrentFlags & (TranslationFlags | TurnFlags)) == 0
                && _move.State != (int)MovementState.Fly
                && !_sim.Airborne
                && _sim.Speed <= VehicleSim.MovingSpeedEpsilon
                && _sim.VerticalVelocity == 0f;
            if (idle)
                return;

            if (surface != null && HoldOverVoid())
                return;

            if (HasPath)
            {
                TickStallWatch.Stage("motor.path", _character.Identity.Instance);
                if (IsPlayer)
                    SteerAlongPlayerPath();
            }
            else if (_followTarget != null)
                SteerFollow();

            if (surface != null)
                _move.Tick(dt);
            else
                TickAtAltitude(dt);

            _character.Position = ToVector3(_sim.Position);
            PullHeading();

            if (_npc != null && _npc.HasFollowTarget && _npc.FollowQueue.Count == 0
                && _sim.Speed <= VehicleSim.MovingSpeedEpsilon)
                ClearPath();
        }

        /// <summary>No ground to find: the step's horizontal travel only, the body landed where it was.</summary>
        void TickAtAltitude(float dt)
        {
            float y = _sim.Position.Y;
            _move.Tick(dt);
            Vec3 position = _sim.Position;
            position.Y = y;
            _sim.Position = position;
            _sim.LandNow(y);
        }

        ISurface? BindSurface()
        {
            ISurface? surface = _character.Playfield?.WorldAccess.Instance?.Surface;
            _move.Core.Surface = surface;
            return surface;
        }

        /// <summary>
        /// With no path the NPC vehicle only integrates while airborne (<c>NpcVehicleSim.IsRunEnabled</c>), so leftover
        /// ground speed it can no longer brake off is dropped.
        /// </summary>
        void SettleNpcVehicle()
        {
            if (!HasPath && !_sim.Airborne && _sim.Speed > 0f)
                _sim.Halt();
        }

        /// <summary>
        /// Falling with nothing at all underneath means the playfield is missing collision: the body stays where it
        /// is until there is geometry below it again.
        /// </summary>
        bool HoldOverVoid()
        {
            if (!_holdingOverVoid && !(_sim.Airborne && _sim.FallingEnabled))
                return false;

            if (HasGeometryBelow())
            {
                _holdingOverVoid = false;
                return false;
            }

            _holdingOverVoid = true;
            _sim.LandNow(_sim.Position.Y);
            WarnNoGeometryBelow();
            return true;
        }

        /// <summary>
        /// The character faces where its body faces: keyboard turns, strafing, the client's heading and an NPC's travel
        /// alike. Flying keeps the client's pitched heading, except while a keyboard turn swings the body.
        /// </summary>
        void PullHeading()
        {
            if (IsPlayerFlying && (CurrentFlags & TurnFlags) == 0)
                return;

            SetYawFrom(_sim.BodyRotation * Vec3.ReferenceForward);
        }

        /// <summary>Fear (53121) and Mind Control (53232) take the controls from the player.</summary>
        void RefreshAiControl() => _move.AiControlled = _character.IsAiControlled;

        /// <summary>
        /// Unported Lost-Eden glue (<c>N3CharVehicle.SteerAlongPath</c>): point a player body at the next
        /// waypoint and drive forward.
        /// </summary>
        void SteerAlongPlayerPath()
        {
            while (HasPath)
            {
                Vector3 target = _path[_playerPathIndex];
                float dx = (float)target.x - _sim.Position.X;
                float dz = (float)target.z - _sim.Position.Z;
                float distance = MathF.Sqrt((dx * dx) + (dz * dz));
                if (distance <= MovementConfig.WaypointArrivalRadius)
                {
                    _playerPathIndex++;
                    if (HasPath)
                        continue;

                    _sim.SetForwardDrive(0f);
                    _sim.SetTurnRate(0f);
                    _sim.Halt();
                    ClearPath();
                    return;
                }

                var toward = new Vec3(dx / distance, 0f, dz / distance);
                _sim.SetRelRot(Quat.LookRotation(toward, Vec3.ReferenceUp));
                _sim.SetForwardDrive(1f);
                return;
            }
        }

        void WarnNoGeometryBelow()
        {
            if (_voidWarned || _character is not Player voidPlayer)
                return;

            _voidWarned = true;
            voidPlayer.Logger.Warn(
                string.Format(
                    CultureInfo.InvariantCulture,
                    "No collision geometry below character={0} at ({1:F3},{2:F3},{3:F3}); holding altitude",
                    _character.Identity.Instance,
                    _character.Position.x,
                    _character.Position.y,
                    _character.Position.z));
        }

        void SetYawFrom(Vec3 forward)
        {
            if (Math.Abs(forward.X) < 1e-5f && Math.Abs(forward.Z) < 1e-5f)
                return;

            float yaw = MathF.Atan2(forward.X, forward.Z);
            if (Math.Abs(NormalizeRadians(yaw - GetYawRadians())) < 1e-5f)
                return;

            float half = yaw * 0.5f;
            _character.Rotation = new Quaternion(0, MathF.Sin(half), 0, MathF.Cos(half));
            _syncedRotation = _character.Rotation;
        }

        /// <summary>
        /// Applies a movement action. False when it was refused. A player's go through the movement rules
        /// (<see cref="CharMovement.Act"/>), which refuse what the mode or the controls refuse; an NPC takes no Act
        /// input, only its mode switches.
        /// </summary>
        public bool ApplyAction(MovementAction action)
            => IsPlayer ? ActPlayer(action) : ApplyNpcAction(action);

        bool ActPlayer(MovementAction action)
        {
            RefreshFlightAuthority();
            RefreshAiControl();
            switch (action)
            {
                case MovementAction.SwitchToFly when !HasFlightAuthority():
                    LogRefusedAction(action, "no flight authority");
                    return false;
                case MovementAction.SwitchToWalk or MovementAction.SwitchToRun when !HasWalkRunSpecialAction():
                    LogRefusedAction(action, "no walk/run special action");
                    return false;
            }

            ISurface? surface = BindSurface();
            if (!_move.Act((int)action))
            {
                LogRefusedAction(action, "movement rules");
                return false;
            }

            switch (action)
            {
                case MovementAction.ForwardStart:
                case MovementAction.BackwardStart:
                case MovementAction.StrafeLeftStart:
                case MovementAction.StrafeRightStart:
                case MovementAction.ElevateUpStart:
                    _character.InterruptTimedActions(TimedActionInterrupt.Movement);
                    break;
                case MovementAction.JumpStart when surface == null:
                    // No ground to leave: the jump lands at once, which runs its end in the status.
                    _sim.LandNow(_sim.Position.Y);
                    break;
            }

            SyncModeStats();
            return true;
        }

        /// <summary>An NPC's mode switches into the rules' states; its movement itself is the follow path.</summary>
        bool ApplyNpcAction(MovementAction action)
        {
            switch (action)
            {
                case MovementAction.FullStop:
                    _sim.Halt();
                    return true;
                case MovementAction.SwitchToFrozen:
                    _move.EnterState((int)MovementState.Rooted);
                    break;
                case MovementAction.SwitchToWalk:
                    _move.EnterState((int)MovementState.Walk);
                    break;
                case MovementAction.SwitchToRun:
                    _move.EnterState((int)MovementState.Run);
                    break;
                case MovementAction.SwitchToSwim:
                    _move.EnterState((int)MovementState.Swim);
                    break;
                case MovementAction.SwitchToCrawl:
                    _move.EnterState((int)MovementState.Crawl);
                    break;
                case MovementAction.SwitchToSneak:
                    _move.EnterState((int)MovementState.Sneak);
                    break;
                case MovementAction.SwitchToFly:
                    if (!HasFlightAuthority())
                        return false;
                    _move.EnterState((int)MovementState.Fly);
                    break;
                case MovementAction.SwitchToSit:
                    _move.EnterState((int)MovementState.Sit);
                    break;
                case MovementAction.SwitchToSleep:
                    _move.EnterState((int)MovementState.Sleep);
                    break;
                case MovementAction.SwitchToLounge:
                    _move.EnterState((int)MovementState.Lounge);
                    break;
                case MovementAction.LeaveFly:
                    if (_move.State != (int)MovementState.Fly)
                        return false;
                    _move.LeaveFly();
                    break;
                case MovementAction.LeaveSwim:
                case MovementAction.LeaveSneak:
                case MovementAction.LeaveSit:
                case MovementAction.LeaveFrozen:
                case MovementAction.LeaveCrawl:
                case MovementAction.LeaveSleep:
                case MovementAction.LeaveLounge:
                    _move.LeaveState();
                    break;
                default:
                    return false;
            }

            SyncModeStats();
            return true;
        }

        void LogRefusedAction(MovementAction action, string reason)
        {
            if (_character is not Player player)
                return;

            RulesStatus.Fields f = _move.Status.Current;
            player.Logger.Info(
                string.Format(
                    CultureInfo.InvariantCulture,
                    "Refused movement action={0}(0x{1:X2}) reason={2} character={3} mode={4} fwd={5}/{6} jump={7} ai={8} path={9} isVehicle={10}",
                    action,
                    (int)action,
                    reason,
                    _character.Identity.Instance,
                    f.Mode,
                    f.FwdState,
                    f.FwdDir,
                    f.JumpState,
                    _move.AiControlled,
                    _move.OnWaypointPath,
                    _character.Stats.GetOrZero(CharacterStat.IsVehicle)));
        }

        /// <summary>
        /// The walk and run toggles are special actions (0x11 / 0x12); the client sends SwitchToWalk / SwitchToRun only
        /// while the character has them, which it does not while it is a vehicle.
        /// </summary>
        bool HasWalkRunSpecialAction() => _character.Stats.GetOrZero(CharacterStat.IsVehicle) == 0;

        /// <summary>
        /// CurrentMovementMode and WaitState follow the mode. WaitState as the client keeps it for itself: entering Sit
        /// (mode 8) sets 2 (Gamecode.dll 0x1006d4bc), leaving a mode sets 0 (0x1006e032). Item criteria such as the
        /// Health and Nano Recharger's [WaitState EqualTo 2] ("You must be sitting") read it. The flying flag is the
        /// rules' own; one left over (a stored Flags stat) is cleared outside Fly.
        /// </summary>
        void SyncModeStats()
        {
            int mode = _move.State;
            if (_character.Stats.GetOrZero(CharacterStat.CurrentMovementMode) != mode)
            {
                _character.Stats.Set(CharacterStat.CurrentMovementMode, mode, StatDetail.Base, dirty: true);
                _character.Stats.Set(CharacterStat.WaitState, mode == (int)MovementState.Sit ? 2 : 0, StatDetail.Base);
            }

            if (mode != (int)MovementState.Fly)
                SetCharacterFlag(CharMovementRules.FlagFlying, false);
        }

        /// <summary>The rules' <see cref="CharMovement.FlagsSet"/> / <see cref="CharMovement.FlagsCleared"/> into stat 0.</summary>
        void SetCharacterFlag(int flag, bool set)
        {
            int flags = _character.Stats.GetOrZero(CharacterStat.Flags);
            int updated = set ? flags | flag : flags & ~flag;
            if (updated != flags)
                _character.Stats.Set(CharacterStat.Flags, updated, StatDetail.Base, dirty: true);
        }

        // A running CanFly effect (StatModifierSpells: IsVehicle reads 1 while the nano runs or the item is worn) on a
        // playfield that allows flying (the FlyingAllowed criterion: outdoors, not the Shadowlands, metadata no-fly bit
        // clear). A client mode request cannot grant this authority. Checked every tick and on each movement
        // action, not on each transient Bonus clear during equipment/nano rebasing.
        bool HasFlightAuthority() => _character.Stats.GetOrZero(CharacterStat.IsVehicle) == 1
            && (_character.Playfield != null
                ? PlayfieldEntryFlags.IsFlyingAllowed(_character.Playfield.MetaData)
                : _character.Stats.GetOrZero(CharacterStat.ExpansionPlayfield) == 0);

        /// <summary>
        /// The server is the flight authority. The client switches itself into Fly when a CanFly effect starts (no
        /// SwitchToFly arrives) and out of it when the effect ends; a player follows the same edges. Only the edges: a
        /// player who lands while the effect still runs stays landed. Any character in Fly without authority lands.
        /// </summary>
        public void RefreshFlightAuthority()
        {
            bool authority = HasFlightAuthority();
            bool gained = authority && !_hadFlightAuthority;
            _hadFlightAuthority = authority;
            bool flying = _move.State == (int)MovementState.Fly;
            if (IsPlayer && gained && !flying)
                _move.Apply((int)MovementAction.SwitchToFly);
            else if (!authority && flying)
            {
                if (IsPlayer)
                    _move.Apply((int)MovementAction.LeaveFly);
                else
                    _move.LeaveFly();
            }
            else
                return;

            SyncModeStats();
        }

        /// <summary>
        /// Leaves Sit for the last walk/run mode. Attacking stands a seated character up on the client without a movement
        /// change, so the attack does it here; otherwise the server would keep the character seated while it fights.
        /// </summary>
        public void StandUp()
        {
            if (_move.State != (int)MovementState.Sit)
                return;

            if (IsPlayer)
                _move.Apply((int)MovementAction.LeaveSit);
            else
                _move.LeaveState();
            SyncModeStats();
        }

        bool HasGeometryBelow()
        {
            Playfield? playfield = _character.Playfield;
            if (playfield == null)
                return false;

            if (playfield.TrySnapFeetToFloor(_character.Position, out _))
                return true;

            PlayfieldWorldSimulation? world = playfield.WorldAccess.Instance;
            if (world == null)
                return false;

            Vector3 from = new(
                _character.Position.x,
                _character.Position.y + MovementConfig.VoidProbeLift,
                _character.Position.z);
            return world.TryRaycastDown(from, MovementConfig.VoidProbeDepth, out _);
        }

        float GetYawRadians()
        {
            Quaternion q = _character.Rotation ?? new Quaternion();
            float siny = 2f * ((float)(q.wf * q.yf + q.xf * q.zf));
            float cosy = 1f - (2f * ((float)(q.yf * q.yf + q.zf * q.zf)));
            return MathF.Atan2(siny, cosy);
        }

        static float NormalizeRadians(float radians)
        {
            while (radians > MathF.PI)
                radians -= 2f * MathF.PI;
            while (radians < -MathF.PI)
                radians += 2f * MathF.PI;
            return radians;
        }

        static float FlatDistance(Vector3 a, Vector3 b)
        {
            float dx = (float)(b.x - a.x);
            float dz = (float)(b.z - a.z);
            return MathF.Sqrt((dx * dx) + (dz * dz));
        }

        static float FlatDistance(Vec3 a, Vec3 b)
        {
            float dx = b.X - a.X;
            float dz = b.Z - a.Z;
            return MathF.Sqrt((dx * dx) + (dz * dz));
        }

        static Vec3 ToVec3(Vector3 v) => new((float)v.x, (float)v.y, (float)v.z);

        static Vector3 ToVector3(Vec3 v) => new(v.X, v.Y, v.Z);

        static Quat ToQuat(Quaternion? q)
        {
            if (q == null)
                return Quat.Identity;

            var quat = new Quat(q.xf, q.yf, q.zf, q.wf);
            return quat.Length < 1e-6f ? Quat.Identity : quat.Normalized;
        }
    }
}
