namespace ZoneEngine_New.Core.Movement
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;

    using SmokeLounge.AOtomation.Messaging.GameData;
    using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;

    using AORebirth.World.Pathfinding;

    using ZoneEngine_New.Core.Entities;
    using ZoneEngine_New.Core.Metrics;
    using ZoneEngine_New.Core.Playfield;
    using ZoneEngine_New.Core.Playfield.Locality;
    using ZoneEngine_New.Core.WorldSimulation;

    using Quaternion = AORebirth.Core.Vector.Quaternion;
    using Vector3 = AORebirth.Core.Vector.Vector3;
    using MsgQuaternion = SmokeLounge.AOtomation.Messaging.GameData.Quaternion;
    using MsgVector3 = SmokeLounge.AOtomation.Messaging.GameData.Vector3;

    /// <summary>
    /// Server locomotion copied from client <c>Vehicle_t::Run</c> /
    /// <c>WaypointPath_c</c>. Keyed motion uses instant <c>SetVel</c>; NPC paths
    /// are constant-speed polylines, then surface snap.
    /// </summary>
    public sealed class CharacterMotor
    {
        const MovementFlags TranslationFlags =
            MovementFlags.Forward | MovementFlags.Backward
            | MovementFlags.StrafeLeft | MovementFlags.StrafeRight;

        readonly Character _character;
        readonly VehiclePath _vehiclePath = new();
        readonly List<Vector3> _path = new();
        readonly List<Vector3> _navigateScratch = new();
        readonly List<System.Numerics.Vector3> _navMeshScratch = new();

        MovementFlags _flags;
        MovementState _state = MovementState.Run;
        MovementState _lastSpeedMode = MovementState.Run;

        Vector3 _velocity = new(0, 0, 0);
        float _verticalVelocity;
        int _jumpStrength;
        int _jumpAgility;
        int _jumpGmLevel;
        bool _jumpArmed = true;
        bool _voidWarned;
        VelocityLimits _runLimits = new(5f, 3f, 2.5f);

        public CharacterMotor(Character character)
        {
            _character = character ?? throw new ArgumentNullException(nameof(character));
        }

        public MovementFlags MovementFlags => _flags;

        public MovementState State => _state;

        public CharMovementStatus BuildMovementStatus()
        {
            bool onPath = HasPath;
            bool forward = onPath || (_flags & MovementFlags.Forward) != 0;
            bool backward = !onPath && (_flags & MovementFlags.Backward) != 0;
            bool strafeLeft = (_flags & MovementFlags.StrafeLeft) != 0;
            bool strafeRight = (_flags & MovementFlags.StrafeRight) != 0;
            bool turnLeft = (_flags & MovementFlags.TurnLeft) != 0;
            bool turnRight = (_flags & MovementFlags.TurnRight) != 0;
            bool elevateUp = (_flags & MovementFlags.ElevateUp) != 0;
            bool elevateDown = (_flags & MovementFlags.ElevateDown) != 0;
            bool jumping = !_jumpArmed || (_flags & MovementFlags.Jump) != 0;

            if (_state == MovementState.Sit)
            {
                forward = false;
                backward = false;
                strafeLeft = false;
                strafeRight = false;
                jumping = false;
            }

            MovementState lastSpeed = _state == MovementState.Walk || _state == MovementState.Run
                ? _state
                : _lastSpeedMode;

            return new CharMovementStatus
            {
                Header = _character.IsPlayer ? (byte)0x80 : (byte)0,
                ModeId = (byte)_state,
                FwdState = (byte)(forward || backward ? 2 : 1),
                FwdDir = (byte)(forward ? 1 : backward ? 2 : 0),
                StrafeState = (byte)(strafeLeft || strafeRight ? 2 : 1),
                StrafeDir = (byte)(strafeLeft && !strafeRight ? 3 : strafeRight && !strafeLeft ? 4 : 0),
                ElevateState = (byte)(elevateUp || elevateDown ? 2 : 1),
                ElevateDir = (byte)(elevateUp ? 5 : 0),
                TurnState = (byte)(turnLeft || turnRight ? 4 : 1),
                TurnDir = (byte)(turnLeft && !turnRight ? 3 : turnRight && !turnLeft ? 4 : 0),
                JumpState = (byte)(jumping ? 3 : 1),
                LastSpeedMode = (byte)lastSpeed
            };
        }

        /// <summary>
        /// Directional CharDCMove packets for a client that just spawned this character.
        /// Posture and speed-mode switches are omitted. Unknown is 0 so the client applies
        /// each move to this identity. Empty when the character is not moving directionally.
        /// </summary>
        public List<CharDCMoveMessage> BuildSpawnMoves()
        {
            var actions = new List<MovementAction>(4);
            bool forward = HasPath || (_flags & MovementFlags.Forward) != 0;
            bool backward = !HasPath && (_flags & MovementFlags.Backward) != 0;
            if (forward)
                actions.Add(MovementAction.ForwardStart);
            if (backward)
                actions.Add(MovementAction.BackwardStart);
            if ((_flags & MovementFlags.StrafeLeft) != 0)
                actions.Add(MovementAction.StrafeLeftStart);
            if ((_flags & MovementFlags.StrafeRight) != 0)
                actions.Add(MovementAction.StrafeRightStart);
            if ((_flags & MovementFlags.TurnLeft) != 0)
                actions.Add((_flags & MovementFlags.MouseTurn) != 0
                    ? MovementAction.TurnLeftMouse
                    : MovementAction.TurnLeftStart);
            if ((_flags & MovementFlags.TurnRight) != 0)
                actions.Add((_flags & MovementFlags.MouseTurn) != 0
                    ? MovementAction.TurnRightMouse
                    : MovementAction.TurnRightStart);
            if ((_flags & MovementFlags.ElevateUp) != 0)
                actions.Add(MovementAction.ElevateUpStart);
            if ((_flags & MovementFlags.ElevateDown) != 0)
                actions.Add(MovementAction.ElevateDownStart);
            if (!_jumpArmed || (_flags & MovementFlags.Jump) != 0)
                actions.Add(MovementAction.JumpStart);

            var messages = new List<CharDCMoveMessage>(actions.Count);
            foreach (MovementAction action in actions)
                messages.Add(CreateMove(action));
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

        public bool HasPath => _vehiclePath.IsActive;

        /// <summary>
        /// True while translating (keys/path) or still carrying planar speed.
        /// Ranged weapon ticks use this so a held run key and residual slide both pause fire.
        /// </summary>
        public bool IsMoving =>
            (_flags & TranslationFlags) != 0
            || HasPath
            || Vector3.Abs(_velocity) > MovementConfig.SpeedStopEpsilon;

        public event Action? PathCompleted;

        /// <summary>Raised when a jump actually starts (not a failed sit/unarmed attempt).</summary>
        public event Action? Jumped;

        public void RefreshFromStats()
        {
            int runSpeed = _character.Stats.GetOrZero(CharacterStat.RunSpeed);
            int health = _character.Stats.GetOrZero(CharacterStat.Health);
            int maxHealth = _character.Stats.GetOrZero(CharacterStat.MaxHealth);
            _runLimits = ComputeRunLimits(runSpeed, health, maxHealth);
            RefreshJumpStats();
        }

        public void RefreshJumpStats()
        {
            _jumpStrength = _character.Stats.GetOrZero(CharacterStat.Strength);
            _jumpAgility = _character.Stats.GetOrZero(CharacterStat.Agility);
            _jumpGmLevel = _character.Stats.GetOrZero(CharacterStat.GmLevel);
        }

        public void OnStatChanged(CharacterStat stat, int previous, int next, bool isInitialSet)
        {
            switch (stat)
            {
                case CharacterStat.RunSpeed:
                case CharacterStat.Health:
                case CharacterStat.MaxHealth:
                    RefreshFromStats();
                    break;
                case CharacterStat.Strength:
                case CharacterStat.Agility:
                case CharacterStat.GmLevel:
                    RefreshJumpStats();
                    break;
            }
        }

        public void SetPath(IReadOnlyList<Vector3> waypoints)
        {
            ClearPath();
            StopAllFlags();
            CopyWaypoints(waypoints);
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
        /// Updates the final path point without clearing velocity. False when there is no active path.
        /// When a navmesh is loaded, returns false so the caller repaths through <see cref="NavigateTo"/>.
        /// </summary>
        public bool TryRetargetFinalWaypoint(Vector3 destination, float minDeltaMeters)
        {
            if (_character.Playfield?.Pathfinder != null)
                return false;
            if (!HasPath || _path.Count == 0)
                return false;

            Vector3 current = _path[_path.Count - 1];
            if (Vector3.Abs(destination - current) < minDeltaMeters)
                return true;

            Vector3 start = _character.Position;
            _path.Clear();
            _path.Add(new Vector3(start.x, start.y, start.z));
            _path.Add(new Vector3(destination.x, destination.y, destination.z));
            _vehiclePath.Set(_path, GetActiveLimits().Forward);
            return true;
        }

        /// <summary>
        /// Remaining path points for SCFU. Empty when idle so HasWaypoints stays clear.
        /// </summary>
        public MsgVector3[] CopyRemainingWaypoints()
        {
            if (!HasPath)
                return [];

            return _vehiclePath.CopyRemainingWaypoints();
        }

        public void ClearPath()
        {
            bool had = HasPath || _path.Count > 0;
            _path.Clear();
            _vehiclePath.Clear();
            if (had)
                PathCompleted?.Invoke();
        }

        void CopyWaypoints(IReadOnlyList<Vector3> waypoints)
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

            _vehiclePath.Set(_path, GetActiveLimits().Forward);
        }

        void ReplacePath(IReadOnlyList<Vector3> waypoints)
        {
            _path.Clear();
            _vehiclePath.Clear();
            CopyWaypoints(waypoints);
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
                if (finder.TryFindPath(
                    new System.Numerics.Vector3((float)start.x, (float)start.y, (float)start.z),
                    new System.Numerics.Vector3((float)destination.x, (float)destination.y, (float)destination.z),
                    _navMeshScratch)
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

        public void Halt()
        {
            _velocity = new Vector3(0, 0, 0);
        }

        public void ResetForPlayfieldTransfer(Vector3 position)
        {
            // Key releases on the old/loading connection may never reach this motor.
            // The destination must not inherit input or a path from the previous world.
            ClearPath();
            StopAllFlags();
            Warp(position);
        }

        public void Warp(Vector3 position, Quaternion? rotation = null, bool resetVelocity = true)
        {
            float previousYaw = GetYawDegrees();
            _character.Position = position;
            if (rotation != null)
                _character.Rotation = rotation;

            if (resetVelocity)
            {
                Halt();
                _verticalVelocity = MovementConfig.GroundStickVelocity;
                _jumpArmed = true;
            }
            else if (rotation != null)
            {
                float yawDelta = NormalizeAngle(GetYawDegrees() - previousYaw);
                if (Math.Abs(yawDelta) >= 1e-6f)
                {
                    float rad = yawDelta * (MathF.PI / 180f);
                    float c = MathF.Cos(rad);
                    float s = MathF.Sin(rad);
                    float vx = (float)_velocity.x;
                    float vz = (float)_velocity.z;
                    _velocity = new Vector3((vx * c) + (vz * s), 0, (-vx * s) + (vz * c));
                }
            }
        }

        /// <summary>
        /// Applies an inbound move. Returns false when the position is rejected;
        /// rejected moves do not change path, action, heading, or position.
        /// </summary>
        public bool Consume(CharDCMoveMessage message)
        {
            ArgumentNullException.ThrowIfNull(message);
            if (!TryAcceptClientPosition(message))
                return false;

            ClearPath();
            ApplyAction((MovementAction)message.MoveType);
            ApplyClientHeading(message);
            return true;
        }

        bool TryAcceptClientPosition(CharDCMoveMessage message)
        {
            // EXPLOIT: client XYZ is applied unclamped vs speed/last update. Playfield XZ is gated below.
            if (message.Coordinates == null)
                return true;

            float x = message.Coordinates.X;
            float y = message.Coordinates.Y;
            float z = message.Coordinates.Z;

            Playfield? playfield = _character.Playfield;
            if (playfield is MissionPlayfield mission
                && !mission.World.AcceptsMovement(_character, new Vector3(x, y, z)))
                return false;
            if (playfield is MissionPlayfield && _character is Player missionPlayer
                && !playfield.GetRequiredService<ZoneEngine_New.Core.Missions.GeneratedMissionAcgService>()
                    .TryPersistPlayerPosition(missionPlayer, new Vector3(x, y, z)))
                return false;
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

            var requested = new Vector3(x, y, z);
            if (playfield != null
                && playfield.TrySnapFeetToFloor(requested, out Vector3 floor)
                && y < (float)floor.y)
            {
                y = (float)floor.y;
                message.Coordinates.Y = y;
            }

            _character.Position = new Vector3(x, y, z);
            return true;
        }

        /// <summary>
        /// Applies client heading and rotates residual planar velocity by the yaw delta
        /// (Lost Eden <c>Warp(..., resetVelocity: false)</c>).
        /// </summary>
        void ApplyClientHeading(CharDCMoveMessage message)
        {
            float previousYaw = GetYawDegrees();
            _character.Rotation = new Quaternion(
                message.Heading.X,
                message.Heading.Y,
                message.Heading.Z,
                message.Heading.W);

            float yawDelta = NormalizeAngle(GetYawDegrees() - previousYaw);
            if (Math.Abs(yawDelta) < 1e-6f)
                return;

            float rad = yawDelta * (MathF.PI / 180f);
            float c = MathF.Cos(rad);
            float s = MathF.Sin(rad);
            float vx = (float)_velocity.x;
            float vz = (float)_velocity.z;
            _velocity = new Vector3((vx * c) + (vz * s), 0, (-vx * s) + (vz * c));
        }

        public void Tick(double deltaTime)
        {
            RefreshFlightAuthority();
            float dt = (float)deltaTime;
            if (dt <= 0f)
                return;

            bool idle = !HasPath
                && (_flags & TranslationFlags) == 0
                && Math.Abs(_verticalVelocity) < MovementConfig.SpeedStopEpsilon
                && Vector3.Abs(_velocity) < MovementConfig.SpeedStopEpsilon
                && _state != MovementState.Sit
                && _jumpArmed;
            if (idle && TryResolveGroundSupport(out float idleGroundY))
            {
                _character.Position = new Vector3(_character.Position.x, idleGroundY, _character.Position.z);
                return;
            }

            if (HasPath)
            {
                TickStallWatch.Stage("motor.path", _character.Identity.Instance);
                TickWaypointPath(dt);
                return;
            }

            // DummyVehicle_t::CalcSteering is 0; keyed motion is Vehicle_t::SetVel.
            _velocity = ComputeActionDesiredVelocity(dt);

            // Lost Eden grounded / airborne vertical update, then move, then land check.
            float groundY = 0f;
            bool grounded = _verticalVelocity <= 0f && TryResolveGroundSupport(out groundY);
            if (grounded)
            {
                if (!_jumpArmed && _verticalVelocity <= 0f)
                    CompleteLanding();

                if (_verticalVelocity < 0f)
                    _verticalVelocity = MovementConfig.GroundStickVelocity;

                _character.Position = new Vector3(_character.Position.x, groundY, _character.Position.z);
            }
            else
            {
                ApplyGravity(dt);
            }

            Vector3 planar = _velocity;
            Vector3 start = _character.Position;
            double endY = grounded ? start.y : start.y + (_verticalVelocity * dt);
            Vector3 end = new(
                start.x + (planar.x * dt),
                endY,
                start.z + (planar.z * dt));

            if (_character.Playfield is MissionPlayfield mission && !mission.World.AcceptsMovement(_character, end))
            {
                Halt(); _verticalVelocity = 0;
                return;
            }

            // Falling-enabled keyed motion: slide then tripod snap (Vehicle_t::Run).
            EnsureSurfaceAlignment.Result aligned = AlignToSurface(start, end, allowSlide: true);
            _character.Position = aligned.Position;
            if (aligned.Normal.y > MovementConfig.SurfaceSlideFloorY && _verticalVelocity <= 0f)
            {
                _verticalVelocity = MovementConfig.GroundStickVelocity;
                if (!_jumpArmed)
                    CompleteLanding();
            }
            else if (aligned.Normal.y < -MovementConfig.SurfaceSlideFloorY && _verticalVelocity > 0f)
            {
                _verticalVelocity = 0f;
            }
        }

        void ApplyGravity(float dt)
        {
            // Falling with nothing at all underneath means the playfield is missing collision, not
            // that the character walked off the world. Hold position and say so once.
            if (!HasGeometryBelow())
            {
                _verticalVelocity = 0f;
                if (!_voidWarned && _character is Player voidPlayer)
                {
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

                return;
            }

            _verticalVelocity += MovementConfig.Gravity * dt;
            _verticalVelocity = Math.Clamp(
                _verticalVelocity,
                -MovementConfig.TerminalVelocity,
                MovementConfig.TerminalVelocity);
        }

        public void ApplyAction(MovementAction action)
        {
            RefreshFlightAuthority();
            switch (action)
            {
                case MovementAction.ForwardStart:
                    SetFlags(_flags | MovementFlags.Forward);
                    _character.InterruptTimedActions(TimedActionInterrupt.Movement);
                    break;
                case MovementAction.ForwardStop:
                    SetFlags(_flags & ~MovementFlags.Forward);
                    break;
                case MovementAction.BackwardStart:
                    SetFlags(_flags | MovementFlags.Backward);
                    _character.InterruptTimedActions(TimedActionInterrupt.Movement);
                    break;
                case MovementAction.BackwardStop:
                    SetFlags(_flags & ~MovementFlags.Backward);
                    break;
                case MovementAction.StrafeLeftStart:
                    SetFlags(_flags | MovementFlags.StrafeLeft);
                    _character.InterruptTimedActions(TimedActionInterrupt.Movement);
                    break;
                case MovementAction.StrafeLeftStop:
                    SetFlags(_flags & ~MovementFlags.StrafeLeft);
                    break;
                case MovementAction.StrafeRightStart:
                    SetFlags(_flags | MovementFlags.StrafeRight);
                    _character.InterruptTimedActions(TimedActionInterrupt.Movement);
                    break;
                case MovementAction.StrafeRightStop:
                    SetFlags(_flags & ~MovementFlags.StrafeRight);
                    break;
                case MovementAction.TurnLeftStart:
                    SetFlags((_flags | MovementFlags.TurnLeft) & ~MovementFlags.TurnRight);
                    break;
                case MovementAction.TurnLeftMouse:
                    SetFlags((_flags | MovementFlags.TurnLeft | MovementFlags.MouseTurn) & ~MovementFlags.TurnRight);
                    break;
                case MovementAction.TurnLeftStop:
                    SetFlags(_flags & ~(MovementFlags.TurnLeft | MovementFlags.MouseTurn));
                    break;
                case MovementAction.TurnRightStart:
                    SetFlags((_flags | MovementFlags.TurnRight) & ~MovementFlags.TurnLeft);
                    break;
                case MovementAction.TurnRightMouse:
                    SetFlags((_flags | MovementFlags.TurnRight | MovementFlags.MouseTurn) & ~MovementFlags.TurnLeft);
                    break;
                case MovementAction.TurnRightStop:
                    SetFlags(_flags & ~(MovementFlags.TurnRight | MovementFlags.MouseTurn));
                    break;
                case MovementAction.JumpStart:
                    SetFlags(_flags | MovementFlags.Jump);
                    TryStartJump(requireGrounded: false);
                    break;
                case MovementAction.JumpStop:
                    SetFlags(_flags & ~MovementFlags.Jump);
                    break;
                case MovementAction.ElevateUpStart:
                    SetFlags((_flags | MovementFlags.ElevateUp) & ~MovementFlags.ElevateDown);
                    _character.InterruptTimedActions(TimedActionInterrupt.Movement);
                    break;
                case MovementAction.ElevateUpStop:
                    SetFlags(_flags & ~MovementFlags.ElevateUp);
                    break;
                case MovementAction.ElevateDownStart:
                    SetFlags((_flags | MovementFlags.ElevateDown) & ~MovementFlags.ElevateUp);
                    _character.InterruptTimedActions(TimedActionInterrupt.Movement);
                    break;
                case MovementAction.ElevateDownStop:
                    SetFlags(_flags & ~MovementFlags.ElevateDown);
                    break;
                case MovementAction.FullStop:
                    StopAllFlags();
                    Halt();
                    break;
                case MovementAction.SwitchToFrozen:
                    EnterMovementState(MovementState.Rooted);
                    break;
                case MovementAction.SwitchToWalk:
                    EnterMovementState(MovementState.Walk);
                    break;
                case MovementAction.SwitchToRun:
                    EnterMovementState(MovementState.Run);
                    break;
                case MovementAction.SwitchToSwim:
                    EnterMovementState(MovementState.Swim);
                    break;
                case MovementAction.SwitchToCrawl:
                    EnterMovementState(MovementState.Crawl);
                    break;
                case MovementAction.SwitchToSneak:
                    EnterMovementState(MovementState.Sneak);
                    break;
                case MovementAction.SwitchToFly:
                    if (HasFlightAuthority()) EnterMovementState(MovementState.Fly);
                    break;
                case MovementAction.SwitchToSit:
                    EnterMovementState(MovementState.Sit);
                    break;
                case MovementAction.SwitchToSleep:
                    EnterMovementState(MovementState.Sleep);
                    break;
                case MovementAction.SwitchToLounge:
                    EnterMovementState(MovementState.Lounge);
                    break;
                case MovementAction.LeaveSwim:
                case MovementAction.LeaveSneak:
                case MovementAction.LeaveSit:
                case MovementAction.LeaveFrozen:
                case MovementAction.LeaveFly:
                case MovementAction.LeaveCrawl:
                case MovementAction.LeaveSleep:
                case MovementAction.LeaveLounge:
                    LeaveMovementState();
                    break;
            }
        }

        // The accepted CanFly effect explicitly grants IsVehicle=1 outside Shadowlands. A
        // client mode request cannot grant this authority. Check after whole owner operations,
        // not each transient Bonus clear during equipment/nano rebasing.
        bool HasFlightAuthority() => _character.Stats.GetOrZero(CharacterStat.IsVehicle) == 1
            && (_character.Playfield != null
                ? _character.Playfield.Identity.Instance is < 4000 or > 4999
                : _character.Stats.GetOrZero((CharacterStat)531) == 0);

        public void RefreshFlightAuthority()
        {
            if (_state != MovementState.Fly || HasFlightAuthority()) return;
            _flags &= ~(MovementFlags.ElevateUp | MovementFlags.ElevateDown);
            LeaveMovementState();
        }

        void SetFlags(MovementFlags flags)
        {
            _flags = flags;
            // Lost Eden: clearing all translation input hard-stops planar speed.
            if ((_flags & TranslationFlags) == 0)
                Halt();
        }

        void StopAllFlags() => SetFlags(MovementFlags.None);

        void EnterMovementState(MovementState state)
        {
            if (_state == MovementState.Walk || _state == MovementState.Run)
                _lastSpeedMode = _state;

            if (state == MovementState.Sit)
                StopAllFlags();

            _state = state;
            SyncMovementModeStat();
        }

        void LeaveMovementState()
        {
            _state = _lastSpeedMode is MovementState.Walk or MovementState.Run
                ? _lastSpeedMode
                : MovementState.Run;
            SyncMovementModeStat();
        }

        void SyncMovementModeStat()
        {
            _character.Stats.Set(CharacterStat.CurrentMovementMode, (int)_state, StatDetail.Base, dirty: true);
        }

        VelocityLimits GetActiveLimits()
        {
            if (_state == MovementState.Walk)
            {
                float cap = MovementConfig.WalkBaseVelocity;
                return new VelocityLimits(
                    Math.Min(_runLimits.Forward, cap),
                    Math.Min(_runLimits.Backward, cap),
                    Math.Min(_runLimits.Strafe, cap));
            }

            return _runLimits;
        }

        void TickWaypointPath(float dt)
        {
            Vector3 previous = _character.Position;
            bool stillOnPath = _vehiclePath.Advance(dt, out Vector3 pathPos, out Vector3 direction);
            Halt();
            _verticalVelocity = 0f;

            if (Vector3.Abs(direction) > 1e-6)
                FacePathDirection(direction);

            // DisableFalling: no FUN_1000b2e5 lateral slide. Probe + 0.5 abort only.
            EnsureSurfaceAlignment.Result aligned = AlignToSurface(previous, pathPos, allowSlide: false);
            _character.Position = aligned.Position;

            if (Vector3.Abs(_character.Position - pathPos) >= MovementConfig.PathSampleAbortDistance)
            {
                _character.Position = previous;
                ClearPath();
                return;
            }

            if (!stillOnPath)
                ClearPath();
        }

        EnsureSurfaceAlignment.Result AlignToSurface(Vector3 previous, Vector3 desired, bool allowSlide)
        {
            Playfield? playfield = _character.Playfield;
            if (playfield != null
                && playfield.TrySnapFeetToFloor(desired, out Vector3 floor)
                && desired.y < floor.y)
                desired = new Vector3(desired.x, floor.y, desired.z);

            // Indoor Grid: vehicle tripod linecasts hit baked GNDA at ~Y=0 and pull authored
            // pad landings into the floor (arrive Y=4.2 → snapshot Y=0).
            if (playfield?.MetaData?.IsIndoor == true)
                return EnsureSurfaceAlignment.Apply(null, previous, desired, allowSlide);

            IVehicleSurface? surface = playfield?.WorldAccess.Instance?.CreateVehicleSurface();
            return EnsureSurfaceAlignment.Apply(surface, previous, desired, allowSlide);
        }

        Vector3 ComputeActionDesiredVelocity(float dt)
        {
            if (_state == MovementState.Sit)
                return new Vector3(0, 0, 0);

            float turn = 0f;
            if ((_flags & MovementFlags.TurnLeft) != 0)
                turn -= 1f;
            if ((_flags & MovementFlags.TurnRight) != 0)
                turn += 1f;
            if (turn != 0f)
                RotateYaw(turn * GetTurnRateRadians() * (180f / MathF.PI) * dt);

            return ComputeActionPlanarVelocity();
        }

        /// <summary>
        /// Planar intent from movement flags. Combined axes share one speed
        /// (forward &gt; backward &gt; strafe) and only change direction.
        /// </summary>
        Vector3 ComputeActionPlanarVelocity()
        {
            Vector3 forward = GetForward();
            Vector3 right = new(forward.z, 0, -forward.x);
            Vector3 dir = new(0, 0, 0);
            VelocityLimits limits = GetActiveLimits();

            if ((_flags & MovementFlags.Forward) != 0)
                dir += forward;
            if ((_flags & MovementFlags.Backward) != 0)
                dir -= forward;
            if ((_flags & MovementFlags.StrafeRight) != 0)
                dir += right;
            if ((_flags & MovementFlags.StrafeLeft) != 0)
                dir -= right;

            double len = Vector3.Abs(dir);
            if (len < 1e-6)
                return new Vector3(0, 0, 0);

            float speed;
            if ((_flags & MovementFlags.Forward) != 0)
                speed = limits.Forward;
            else if ((_flags & MovementFlags.Backward) != 0)
                speed = limits.Backward;
            else
                speed = limits.Strafe;

            dir = dir * (1.0 / len);
            return dir * speed;
        }

        float GetTurnRateRadians()
        {
            bool moving = ((_flags & TranslationFlags) != 0)
                || HasPath
                || Vector3.Abs(_velocity) > MovementConfig.SpeedStopEpsilon;
            return moving
                ? MovementConfig.TurnRateRadiansMoving
                : MovementConfig.TurnRateRadiansStopped;
        }

        /// <summary>
        /// Client <c>FUN_1000a47a</c>: path ticks copy the segment tangent onto
        /// body forward immediately. We store heading as yaw around Y.
        /// </summary>
        void FacePathDirection(Vector3 direction)
        {
            SetYaw(MathF.Atan2((float)direction.x, (float)direction.z) * (180f / MathF.PI));
        }

        void RotateYaw(float yawDeltaDegrees)
        {
            if (Math.Abs(yawDeltaDegrees) < 1e-6f)
                return;

            float yaw = GetYawDegrees() + yawDeltaDegrees;
            SetYaw(yaw);
            float rad = yawDeltaDegrees * (MathF.PI / 180f);
            float c = MathF.Cos(rad);
            float s = MathF.Sin(rad);
            float vx = (float)_velocity.x;
            float vz = (float)_velocity.z;
            _velocity = new Vector3((vx * c) + (vz * s), 0, (-vx * s) + (vz * c));
        }

        void SetYaw(float yawDegrees)
        {
            float rad = yawDegrees * (MathF.PI / 180f);
            // Yaw around Y: quaternion (0, sin(y/2), 0, cos(y/2))
            float half = rad * 0.5f;
            _character.Rotation = new Quaternion(0, MathF.Sin(half), 0, MathF.Cos(half));
        }

        float GetYawDegrees()
        {
            Quaternion q = _character.Rotation ?? new Quaternion();
            // yaw from quaternion
            float siny = 2f * ((float)(q.wf * q.yf + q.xf * q.zf));
            float cosy = 1f - (2f * ((float)(q.yf * q.yf + q.zf * q.zf)));
            return MathF.Atan2(siny, cosy) * (180f / MathF.PI);
        }

        Vector3 GetForward()
        {
            float yaw = GetYawDegrees() * (MathF.PI / 180f);
            return new Vector3(MathF.Sin(yaw), 0, MathF.Cos(yaw));
        }

        /// <summary>
        /// Finds the surface holding the character up. Origin is torso height: spawn and Recast
        /// poses can sit under the Bepu floor, and a foot-height downward ray then starts below
        /// the triangle and misses it.
        /// </summary>
        bool TryResolveGroundSupport(out float groundY)
        {
            groundY = (float)_character.Position.y;
            Playfield? playfield = _character.Playfield;
            if (playfield == null)
                return true;

            if (!playfield.TrySnapFeetToFloor(_character.Position, out Vector3 hit))
                return playfield.WorldAccess.Instance == null;

            groundY = (float)hit.y;
            return true;
        }

        bool HasGeometryBelow()
        {
            Playfield? playfield = _character.Playfield;
            if (playfield == null)
                return false;

            // Indoor (Grid): only mesh snaps count. A raw downward ray hits baked GNDA at ~Y=0
            // and would falsely enable gravity, pulling pad landings into the floor.
            if (playfield.MetaData?.IsIndoor == true)
                return playfield.TrySnapFeetToFloor(_character.Position, out _);

            if (playfield.TrySnapFeetToFloor(_character.Position, out _))
                return true;

            WorldSimulation.PlayfieldWorldSimulation? world = playfield.WorldAccess.Instance;
            if (world == null)
                return false;

            Vector3 from = new(
                _character.Position.x,
                _character.Position.y + MovementConfig.CapsuleCenterLift,
                _character.Position.z);
            return world.TryRaycastDown(from, MovementConfig.VoidProbeDepth, out _);
        }

        bool TryStartJump(bool requireGrounded)
        {
            if (!_jumpArmed || _state == MovementState.Sit)
                return false;
            if (requireGrounded && !TryResolveGroundSupport(out _))
                return false;

            _verticalVelocity = ComputeJumpVerticalVelocity(_jumpStrength, _jumpAgility, _jumpGmLevel);
            _jumpArmed = false;
            Jumped?.Invoke();
            return true;
        }

        void CompleteLanding()
        {
            if (_jumpArmed)
                return;

            _jumpArmed = true;
            _flags &= ~MovementFlags.Jump;
        }

        static float ComputeJumpVerticalVelocity(int strength, int agility, int gmLevel)
        {
            float str = strength;
            float agi = agility;
            if (str + agi > MovementConfig.JumpStatCap && gmLevel == 0)
            {
                str = MovementConfig.JumpStatCap;
                agi = 0f;
            }

            float height = ((str + agi) / MovementConfig.JumpHeightPerStatPool) + MovementConfig.JumpHeightBase;
            if (height < MovementConfig.JumpHeightFloor)
                height = MovementConfig.JumpHeightFloor;

            return MathF.Sqrt(2f * height * MathF.Abs(MovementConfig.Gravity));
        }

        static VelocityLimits ComputeRunLimits(int runSpeed, int curHp, int maxHp)
        {
            float statFactor = ComputeStatFactor(runSpeed, curHp, maxHp);
            float fwd = Math.Clamp(
                (statFactor * MovementConfig.RunForwardSlope) + MovementConfig.RunForwardBase,
                MovementConfig.RunForwardMin,
                MovementConfig.RunForwardMax);
            float back = Math.Clamp(
                (statFactor * MovementConfig.RunBackSlope) + MovementConfig.RunBackBase,
                MovementConfig.RunBackMin,
                MovementConfig.RunBackMax);
            float strafe = Math.Clamp(
                MovementConfig.RunStrafeBase + (statFactor * MovementConfig.RunStrafeSlope),
                MovementConfig.RunStrafeMin,
                MovementConfig.RunStrafeMax);
            return new VelocityLimits(fwd, back, strafe);
        }

        /// <summary>
        /// Below <see cref="MovementConfig.HealthPenalty"/> of maximum health, run speed is scaled
        /// down toward the minimum. Applying that needs both health pools; without them there is no
        /// penalty to apply, and guessing one would cripple the character.
        /// </summary>
        static float ComputeStatFactor(int runSpeed, int curHp, int maxHp)
        {
            if (curHp <= 0 || maxHp <= 0)
                return runSpeed;

            float ratio = curHp / (maxHp * MovementConfig.HealthPenalty);
            if (ratio < 1f)
                return (ratio * (runSpeed + MovementConfig.StatOffset)) - MovementConfig.StatOffset;
            return runSpeed;
        }

        static float NormalizeAngle(float degrees)
        {
            while (degrees > 180f)
                degrees -= 360f;
            while (degrees < -180f)
                degrees += 360f;
            return degrees;
        }

        readonly struct VelocityLimits
        {
            public VelocityLimits(float forward, float backward, float strafe)
            {
                Forward = forward;
                Backward = backward;
                Strafe = strafe;
            }

            public float Forward { get; }

            public float Backward { get; }

            public float Strafe { get; }
        }
    }
}
