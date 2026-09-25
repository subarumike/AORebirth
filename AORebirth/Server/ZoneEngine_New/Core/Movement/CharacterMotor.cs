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
    /// Server locomotion on N3Lite's vehicle: a <see cref="CharVehicleSim"/> for players and an
    /// <see cref="NpcVehicleSim"/> for NPCs, colliding against the playfield's <see cref="ISurface"/>.
    /// The client-position gates, movement-mode speed curves, flight authority and navmesh planning
    /// stay here; the integrator and the surfaces are the package.
    /// </summary>
    public sealed class CharacterMotor
    {
        const MovementFlags TranslationFlags =
            MovementFlags.Forward | MovementFlags.Backward
            | MovementFlags.StrafeLeft | MovementFlags.StrafeRight;

        const MovementFlags TurnFlags = MovementFlags.TurnLeft | MovementFlags.TurnRight;

        readonly Character _character;
        readonly CharVehicleSim _sim;
        readonly NpcVehicleSim? _npc;
        readonly List<Vector3> _path = new();
        readonly List<Vector3> _navigateScratch = new();
        readonly List<System.Numerics.Vector3> _navMeshScratch = new();

        MovementFlags _flags;
        MovementState _state = MovementState.Run;
        MovementState _lastSpeedMode = MovementState.Run;

        Quaternion? _syncedRotation;
        int _playerPathIndex = -1;
        int _jumpStrength;
        int _jumpAgility;
        int _jumpGmLevel;
        bool _voidWarned;

        public CharacterMotor(Character character)
        {
            _character = character ?? throw new ArgumentNullException(nameof(character));
            _npc = character.IsPlayer ? null : new NpcVehicleSim();
            _sim = _npc ?? new CharVehicleSim();
            _sim.Mass = MovementConfig.Mass;
            _sim.MaxForce = MovementConfig.InitialMaxForce;
            _sim.MaxVel = MovementConfig.InitialMaxVel;
            _sim.NearProbeOffset = MovementConfig.BodyRadius;
            _sim.SlowingDistance = MovementConfig.InitialSlowingDistance;
            _sim.BodyHeight = 2f;
            _sim.JumpLanded += OnJumpLanded;
            _sim.DisableSurfaceHug();
            _sim.UseSurfaceNormal();
            ApplyMotionConstraints();
        }

        public MovementFlags MovementFlags => _flags;

        public MovementState State => _state;

        /// <summary>The vehicle itself.</summary>
        public CharVehicleSim Vehicle => _sim;

        bool Jumping => _sim.JumpHeight != 0f;

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
            bool jumping = Jumping || (_flags & MovementFlags.Jump) != 0;

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
            if (Jumping || (_flags & MovementFlags.Jump) != 0)
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

        public bool HasPath => _npc != null
            ? !_npc.Path.Empty
            : _playerPathIndex >= 0 && _playerPathIndex < _path.Count;

        /// <summary>
        /// True while translating (keys/path) or still carrying planar speed.
        /// Ranged weapon ticks use this so a held run key and residual slide both pause fire.
        /// </summary>
        public bool IsMoving =>
            (_flags & TranslationFlags) != 0
            || HasPath
            || _sim.Speed > MovementConfig.SpeedStopEpsilon;

        public event Action? PathCompleted;

        /// <summary>Raised when a jump actually starts (not a failed sit/unarmed attempt).</summary>
        public event Action? Jumped;

        /// <summary>
        /// The run-speed stat drives the whole speed curve. N3Lite takes the resulting max speed;
        /// the stance curves stay here.
        /// </summary>
        public void RefreshFromStats()
        {
            ApplyFlagsToAxes();
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

            _navigateScratch.Clear();
            _navigateScratch.Add(new Vector3(destination.x, destination.y, destination.z));
            ReplacePath(_navigateScratch);
            return true;
        }

        /// <summary>
        /// Remaining path points for SCFU. Empty when idle so HasWaypoints stays clear. For an NPC these
        /// are the waypoints its guide has not yet passed, i.e. what the vehicle is still steering along.
        /// </summary>
        public MsgVector3[] CopyRemainingWaypoints()
        {
            if (!HasPath)
                return [];

            int first = _npc != null ? FirstWaypointAheadOfGuide() : _playerPathIndex;
            if (first >= _path.Count)
                first = _path.Count - 1;

            var remaining = new MsgVector3[_path.Count - first];
            for (int i = first; i < _path.Count; i++)
                remaining[i - first] = new MsgVector3 { X = _path[i].xf, Y = _path[i].yf, Z = _path[i].zf };
            return remaining;
        }

        /// <summary>The first kept waypoint the guide has not reached, measured along the flat path.</summary>
        int FirstWaypointAheadOfGuide()
        {
            float consumed = _npc!.Guide.Time * _npc.Guide.MaxSpeed;
            float along = 0f;
            for (int i = 1; i < _path.Count; i++)
            {
                along += FlatDistance(_path[i - 1], _path[i]);
                if (along > consumed)
                    return i;
            }

            return _path.Count - 1;
        }

        public void ClearPath()
        {
            bool had = HasPath || _path.Count > 0;
            _path.Clear();
            _playerPathIndex = -1;
            _npc?.Path.Clear();
            if (!had)
                return;

            ApplyFlagsToAxes();
            PathCompleted?.Invoke();
        }

        /// <summary>
        /// Keeps the full waypoint list (with Y) for SCFU and fills the vehicle's path. An NPC gets
        /// stock's <c>Path_t</c> + guide; a player body keeps Lost-Eden's point-and-drive follower.
        /// </summary>
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

            if (_npc != null)
            {
                _npc.Path.Clear();
                for (int i = 0; i < _path.Count; i++)
                    _npc.Path.AddWaypoint(ToVec3(_path[i]));
                _npc.RestartPath();
                return;
            }

            _playerPathIndex = 1;
        }

        void ReplacePath(IReadOnlyList<Vector3> waypoints)
        {
            _path.Clear();
            _playerPathIndex = -1;
            _npc?.Path.Clear();
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

        public void Halt() => _sim.Halt();

        public void ResetForPlayfieldTransfer(Vector3 position)
        {
            // Key releases on the old/loading connection may never reach this motor.
            // The destination must not inherit input or a path from the previous world.
            ClearPath();
            StopAllFlags();
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
            _sim.Position = new Vec3(x, y, z);
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
            forward.Y = 0f;
            if (forward.LengthSquared < 1e-8f)
                return;

            _sim.SetRelRot(Quat.LookRotation(forward / forward.Length, _sim.SurfaceNormal));
        }

        public void Tick(double deltaTime)
        {
            RefreshFlightAuthority();
            float dt = (float)deltaTime;
            if (dt <= 0f)
                return;

            BindSurface();
            _sim.Position = ToVec3(_character.Position);
            if (_character.Rotation != null && !ReferenceEquals(_character.Rotation, _syncedRotation))
                ApplyHeadingToSim(_character.Rotation);

            bool idle = !HasPath
                && (_flags & (TranslationFlags | TurnFlags)) == 0
                && !_sim.Airborne
                && _sim.Speed <= VehicleSim.MovingSpeedEpsilon
                && _sim.VerticalVelocity == 0f;
            if (idle)
                return;

            if (HasPath)
            {
                TickStallWatch.Stage("motor.path", _character.Identity.Instance);
                if (_npc != null)
                    _npc.AdvanceGuide(dt);
                else
                    SteerAlongPlayerPath();
            }

            HoldAltitudeOverVoid();

            Vec3 previous = _sim.Position;
            _sim.Run(dt);

            if (_character.Playfield is MissionPlayfield mission
                && !mission.World.AcceptsMovement(_character, ToVector3(_sim.Position)))
            {
                _sim.Position = previous;
                _sim.Halt();
                _sim.VerticalVelocity = 0f;
                return;
            }

            _character.Position = ToVector3(_sim.Position);
            PullHeading();

            if (_npc != null && HasPath && NpcPathFinished())
                ClearPath();
        }

        /// <summary>
        /// The guide has consumed the whole path and the body has arrived, or has stopped trying
        /// (<c>SteeringDirArrive</c> halts once it passes the target, and a wall halts it too).
        /// </summary>
        bool NpcPathFinished()
        {
            if (_npc!.Guide.Time * _npc.Guide.MaxSpeed < _npc.Path.TotalLength)
                return false;

            Vec3 last = _npc.Path.GetWaypoint(_npc.Path.Size - 1);
            float dx = last.X - _sim.Position.X;
            float dz = last.Z - _sim.Position.Z;
            return (dx * dx) + (dz * dz) <= MovementConfig.PathArrivalRadius * MovementConfig.PathArrivalRadius
                || _sim.Speed <= VehicleSim.MovingSpeedEpsilon;
        }

        /// <summary>
        /// Unported Lost-Eden glue (<c>N3CharVehicle.SteerAlongPath</c>): point a player body at the next
        /// waypoint and drive forward. Stock's follow-target branch is not recovered.
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

        /// <summary>
        /// Binds the playfield's surface. With none the vehicle cannot find the ground, so falling is off
        /// and it holds its altitude; Fly (vehicle state 7) turns falling off by itself.
        /// </summary>
        void BindSurface()
        {
            ISurface? surface = _character.Playfield?.WorldAccess.Instance?.Surface;
            _sim.Surface = surface;
            bool falling = surface != null && _state != MovementState.Fly;
            if (falling == _sim.FallingEnabled)
                return;

            if (falling)
                _sim.EnableFalling();
            else
                _sim.DisableFalling();
        }

        /// <summary>
        /// Falling with nothing at all underneath means the playfield is missing collision, not that the
        /// character walked off the world. Hold position and say so once.
        /// </summary>
        void HoldAltitudeOverVoid()
        {
            if (!_sim.FallingEnabled || !_sim.Airborne || HasGeometryBelow())
                return;

            _sim.DisableFalling();
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

        /// <summary>
        /// The dynel keeps the heading only; the body's surface tilt stays inside the vehicle
        /// (<c>N3CharVehicle.PullSimToTransform</c>). A moving body faces along its velocity, flipped when
        /// backing up, which is what orientation mode 1 does whenever the surface is bound.
        /// </summary>
        void PullHeading()
        {
            Vec3 forward = _sim.GetBodyForward();
            float vx = _sim.Velocity.X;
            float vz = _sim.Velocity.Z;
            if ((vx * vx) + (vz * vz) > 1e-8f)
                forward = new Vec3(vx * _sim.Direction, 0f, vz * _sim.Direction);

            if (Math.Abs(forward.X) < 1e-5f && Math.Abs(forward.Z) < 1e-5f)
                return;

            float yaw = MathF.Atan2(forward.X, forward.Z);
            if (Math.Abs(NormalizeRadians(yaw - GetYawRadians())) < 1e-5f)
                return;

            float half = yaw * 0.5f;
            _character.Rotation = new Quaternion(0, MathF.Sin(half), 0, MathF.Cos(half));
            _syncedRotation = _character.Rotation;
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
                    TryStartJump();
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
            ApplyFlagsToAxes();
        }

        void StopAllFlags() => SetFlags(MovementFlags.None);

        /// <summary>
        /// Input flags into the four axes. An NPC body has none, and the release branch's halt
        /// would fight the path guide, so NPC flags stay flags. Speeds are applied either way:
        /// a rooted NPC still has to brake.
        /// </summary>
        void ApplyFlagsToAxes()
        {
            ApplyMotionConstraints();
            if (_npc != null)
                return;

            float drive = 0f;
            if ((_flags & MovementFlags.Forward) != 0)
                drive += 1f;
            if ((_flags & MovementFlags.Backward) != 0)
                drive -= 1f;

            // forward SetDirection(1) then drive, backward drive then SetDirection(-1),
            // release drive 0, halt, then SetDirection(1). Both the halt and the direction
            // are required.
            if (drive != _sim.ForwardDrive)
            {
                if (drive > 0f)
                {
                    _sim.SetDirection(1);
                    _sim.SetForwardDrive(drive);
                }
                else if (drive < 0f)
                {
                    _sim.SetForwardDrive(drive);
                    _sim.SetDirection(-1);
                }
                else
                {
                    _sim.SetForwardDrive(0f);
                    _sim.Halt();
                    _sim.SetDirection(1);
                }
            }

            float strafe = 0f;
            if ((_flags & MovementFlags.StrafeRight) != 0)
                strafe += 1f;
            if ((_flags & MovementFlags.StrafeLeft) != 0)
                strafe -= 1f;
            _sim.SetStrafe(strafe);

            float turn = 0f;
            if ((_flags & MovementFlags.TurnRight) != 0)
                turn += 1f;
            if ((_flags & MovementFlags.TurnLeft) != 0)
                turn -= 1f;
            _sim.SetTurnRate(turn * GetTurnRateRadians());
        }

        /// <summary>Unported Lost-Eden glue: these two rates are not stock.</summary>
        float GetTurnRateRadians() => IsMoving
            ? MovementConfig.TurnRateRadiansMoving
            : MovementConfig.TurnRateRadiansStopped;

        void EnterMovementState(MovementState state)
        {
            if (_state == MovementState.Walk || _state == MovementState.Run)
                _lastSpeedMode = _state;

            if (state == MovementState.Sit)
                StopAllFlags();

            _state = state;
            if (state == MovementState.Fly)
                _sim.DisableFalling();
            ApplyFlagsToAxes();
            SyncMovementModeStat();
        }

        void LeaveMovementState()
        {
            EnterMovementState(_lastSpeedMode is MovementState.Walk or MovementState.Run
                ? _lastSpeedMode
                : MovementState.Run);
        }

        void SyncMovementModeStat()
        {
            _character.Stats.Set(CharacterStat.CurrentMovementMode, (int)_state, StatDetail.Base, dirty: true);
        }

        bool HasGeometryBelow()
        {
            Playfield? playfield = _character.Playfield;
            if (playfield == null)
                return false;

            if (playfield.TrySnapFeetToFloor(_character.Position, out _))
                return true;

            WorldSimulation.PlayfieldWorldSimulation? world = playfield.WorldAccess.Instance;
            if (world == null)
                return false;

            Vector3 from = new(
                _character.Position.x,
                _character.Position.y + MovementConfig.VoidProbeLift,
                _character.Position.z);
            return world.TryRaycastDown(from, MovementConfig.VoidProbeDepth, out _);
        }

        /// <summary>
        /// <c>JumpStartTransitionAction_t</c>: the owner's jump height into <see cref="CharVehicleSim.Jump"/>.
        /// With no surface bound there is no ground to leave, so only the event is raised.
        /// </summary>
        bool TryStartJump()
        {
            if (_state == MovementState.Sit || Jumping)
                return false;

            if (_sim.Surface != null
                && !_sim.Jump(JumpHeightFromStats(_jumpStrength, _jumpAgility, _jumpGmLevel)))
                return false;

            Jumped?.Invoke();
            return true;
        }

        void OnJumpLanded() => _flags &= ~MovementFlags.Jump;

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

        static Vec3 ToVec3(Vector3 v) => new((float)v.x, (float)v.y, (float)v.z);

        static Vector3 ToVector3(Vec3 v) => new(v.X, v.Y, v.Z);

        static Quat ToQuat(Quaternion? q)
        {
            if (q == null)
                return Quat.Identity;

            var quat = new Quat(q.xf, q.yf, q.zf, q.wf);
            return quat.Length < 1e-6f ? Quat.Identity : quat.Normalized;
        }

        /// <summary>
        /// Writes N3Lite's max speed, strafe speed and drive lock from the current stance.
        /// Backing up selects the reverse curve. Rooted, sit and rooted-can-sit refuse a player's
        /// drive; only rooted brakes an NPC, which is how <c>NpcVehicleSim</c> treats a locked drive.
        /// </summary>
        void ApplyMotionConstraints()
        {
            float drive = 0f;
            if ((_flags & MovementFlags.Forward) != 0)
                drive += 1f;
            if ((_flags & MovementFlags.Backward) != 0)
                drive -= 1f;

            int state = (int)_state;
            SpeedCurve curve = SpeedCurve.For(state, drive < 0f ? 2 : 1);
            float runSpeed = _character.Stats.GetOrZero(CharacterStat.RunSpeed);
            _sim.UpdateMotionConstraints(curve.MaxSpeed(runSpeed));
            _sim.StrafeSpeed = curve.Strafe(state, runSpeed);
            _sim.DriveLocked = _npc != null
                ? _state == MovementState.Rooted
                : _state is MovementState.Rooted or MovementState.Sit or MovementState.RootedCanSit;
        }

        /// <summary>
        /// Jump height from Strength, Agility and GmLevel: <c>(str + agi) / 200 + 1</c>, at least 0.5.
        /// Past 800 in total a non-GM counts as exactly 800.
        /// </summary>
        static float JumpHeightFromStats(int strength, int agility, int gmLevel)
        {
            float str = strength;
            float agi = agility;
            if (800f < agi + str && gmLevel == 0)
            {
                str = 800f;
                agi = 0f;
            }

            float height = (float)((agi + str) / 200.0 + 1.0);
            if (height < 0.5f)
                height = 0.5f;
            return height;
        }

        struct SpeedCurve
        {
            public float Divisor;
            public float Base;
            public float Max;
            public float Min;
            public bool Constant;

            public static SpeedCurve For(int state, int direction)
            {
                switch (state)
                {
                    case 3:
                        return direction == 2
                            ? new SpeedCurve { Divisor = 275f / 0.7f, Base = 3f, Max = 9.099999f, Min = 1.05f }
                            : new SpeedCurve { Divisor = 275f, Base = 5f, Max = 13f, Min = 1.5f };
                    case 4:
                        return new SpeedCurve { Divisor = 275f / 0.625f, Base = 3f, Max = 8f, Min = 1.5f };
                    case 7:
                        return new SpeedCurve { Divisor = 275f, Base = 7f, Max = 15f, Min = 1.5f };
                    case 5:
                        return new SpeedCurve { Base = 1f, Constant = true };
                    default:
                        return new SpeedCurve { Base = 1.5f, Constant = true };
                }
            }

            public float MaxSpeed(float runSpeedStat)
            {
                if (Constant)
                {
                    float v = Base;
                    if (v < 0.01f)
                        v = 0.1f;
                    return v;
                }

                float speed = runSpeedStat / Divisor + Base;
                if (speed > Max)
                    speed = Max;
                if (speed < Min)
                    speed = Min;
                return speed;
            }

            public float Strafe(int state, float runSpeedStat)
            {
                const float Scale = 0.5f;
                const float Floor = 0.75f;
                if (state == 2)
                    return Math.Max(Floor, 1.5f);

                if (Constant)
                    return Math.Max(Floor, Base);

                float v = Scale * runSpeedStat / Divisor + Scale * Base;
                float max = Scale * Max;
                if (v > max)
                    v = max;
                if (v < Floor)
                    v = Floor;
                return v;
            }
        }
    }
}
