namespace ZoneEngine_New.Core.Movement
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;

    using SmokeLounge.AOtomation.Messaging.GameData;
    using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;

    using ZoneEngine_New.Core.Entities;
    using ZoneEngine_New.Core.Playfield;
    using ZoneEngine_New.Core.Playfield.Locality;
    using ZoneEngine_New.Core.WorldSimulation;

    using Quaternion = AORebirth.Core.Vector.Quaternion;
    using Vector3 = AORebirth.Core.Vector.Vector3;

    /// <summary>
    /// Server locomotion: Lost Eden velocity model + optional path following; Bepu sweep resolve.
    /// </summary>
    public sealed class CharacterMotor
    {
        const MovementFlags TranslationFlags =
            MovementFlags.Forward | MovementFlags.Backward
            | MovementFlags.StrafeLeft | MovementFlags.StrafeRight;

        readonly Character _character;
        readonly List<Vector3> _path = new();
        int _pathIndex = -1;

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

        public bool HasPath => _pathIndex >= 0 && _pathIndex < _path.Count;

        /// <summary>
        /// True while the character is trying to translate (keys or path), not leftover slide.
        /// </summary>
        public bool IsMoving =>
            (_flags & TranslationFlags) != 0
            || HasPath;

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
            if (waypoints == null || waypoints.Count == 0)
                return;

            for (int i = 0; i < waypoints.Count; i++)
                _path.Add(waypoints[i]);
            _pathIndex = 0;
        }

        public void ClearPath()
        {
            bool had = HasPath;
            _path.Clear();
            _pathIndex = -1;
            if (had)
                PathCompleted?.Invoke();
        }

        public void Halt()
        {
            _velocity = new Vector3(0, 0, 0);
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

        public void Consume(CharDCMoveMessage message)
        {
            ArgumentNullException.ThrowIfNull(message);

            ClearPath();
            ApplyAction((MovementAction)message.MoveType);
            ApplyClientHeading(message);
            SnapToClientPosition(message);
        }

        void SnapToClientPosition(CharDCMoveMessage message)
        {
            // EXPLOIT: client XYZ is applied unclamped vs speed/last update. Playfield XZ is gated below.
            if (message.Coordinates == null)
                return;

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

                    return;
                }
            }

            _character.Position = new Vector3(x, y, z);
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
                // Nothing is driving the character and it has a floor: settle onto it and skip the
                // rest of the step. An idle character with no floor falls through the normal path.
                _character.Position = new Vector3(_character.Position.x, idleGroundY, _character.Position.z);
                return;
            }

            Vector3 desired;
            float maxVel;
            if (HasPath)
            {
                desired = ComputePathDesiredVelocity(dt);
                maxVel = GetActiveLimits().Forward;
            }
            else
            {
                desired = ComputeActionDesiredVelocity(dt);
                maxVel = GetLocomotionMaxSpeed();
            }

            bool strafing = !HasPath
                && (_flags & (MovementFlags.StrafeLeft | MovementFlags.StrafeRight)) != 0;
            if (strafing)
                _velocity = desired;
            else
                IntegrateVelocity(desired, maxVel, dt);

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
            Vector3 end = new(
                start.x + (planar.x * dt),
                start.y + (_verticalVelocity * dt),
                start.z + (planar.z * dt));

            WorldSimulation.PlayfieldWorldSimulation? world = _character.Playfield?.WorldAccess.Instance;
            if (world != null
                && world.TryMoveCapsule(
                    start,
                    end,
                    MovementConfig.CapsuleRadius,
                    MovementConfig.CapsuleHalfHeight,
                    MovementConfig.CapsuleCenterLift,
                    MovementConfig.SweepSkin,
                    out Vector3 resolved,
                    out Vector3 normal))
            {
                _character.Position = resolved;

                // Floor contact while descending sticks; ceiling kills rise. Wall scrapes leave vy alone.
                if (normal.y > 0.5 && _verticalVelocity <= 0f)
                    _verticalVelocity = MovementConfig.GroundStickVelocity;
                else if (normal.y < -0.5 && _verticalVelocity > 0f)
                    _verticalVelocity = 0f;
            }
            else
            {
                _character.Position = end;
            }

            if (!_jumpArmed
                && _verticalVelocity <= 0f
                && TryResolveGroundSupport(out float landY))
            {
                CompleteLanding();
                _character.Position = new Vector3(_character.Position.x, landY, _character.Position.z);
                if (_verticalVelocity < 0f)
                    _verticalVelocity = MovementConfig.GroundStickVelocity;
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
            switch (action)
            {
                case MovementAction.ForwardStart:
                    SetFlags(_flags | MovementFlags.Forward);
                    break;
                case MovementAction.ForwardStop:
                    SetFlags(_flags & ~MovementFlags.Forward);
                    break;
                case MovementAction.BackwardStart:
                    SetFlags(_flags | MovementFlags.Backward);
                    break;
                case MovementAction.BackwardStop:
                    SetFlags(_flags & ~MovementFlags.Backward);
                    break;
                case MovementAction.StrafeLeftStart:
                    SetFlags(_flags | MovementFlags.StrafeLeft);
                    break;
                case MovementAction.StrafeLeftStop:
                    SetFlags(_flags & ~MovementFlags.StrafeLeft);
                    break;
                case MovementAction.StrafeRightStart:
                    SetFlags(_flags | MovementFlags.StrafeRight);
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
                    break;
                case MovementAction.ElevateUpStop:
                    SetFlags(_flags & ~MovementFlags.ElevateUp);
                    break;
                case MovementAction.ElevateDownStart:
                    SetFlags((_flags | MovementFlags.ElevateDown) & ~MovementFlags.ElevateUp);
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
                    EnterMovementState(MovementState.Fly);
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

        /// <summary>
        /// Max planar speed for the active locomotion direction (Lost Eden).
        /// Forward/back + strafe keeps forward/back speed and only redirects.
        /// </summary>
        float GetLocomotionMaxSpeed()
        {
            if (HasPath)
                return GetActiveLimits().Forward;

            Vector3 planar = ComputeActionPlanarVelocity();
            double speed = Vector3.Abs(planar);
            if (speed > 1e-6)
                return (float)speed;

            return GetActiveLimits().Forward;
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

        Vector3 ComputePathDesiredVelocity(float dt)
        {
            float forwardLimit = GetActiveLimits().Forward;
            float arrival = MovementConfig.WaypointArrivalRadius;
            float mass = MovementConfig.Mass;

            while (HasPath)
            {
                Vector3 target = _path[_pathIndex];
                Vector3 to = new(target.x - _character.Position.x, 0, target.z - _character.Position.z);
                float distance = (float)Vector3.Abs(to);
                bool isFinal = _pathIndex >= _path.Count - 1;

                if (!isFinal && distance <= arrival)
                {
                    _pathIndex++;
                    continue;
                }

                if (isFinal)
                {
                    float speed = (float)Vector3.Abs(_velocity);
                    float deceleration = ComputeMaxForce(forwardLimit) / mass;
                    float stopDistance = (speed * speed) / (2f * Math.Max(deceleration, 0.01f));
                    bool shouldBrake = distance <= Math.Max(arrival, stopDistance);

                    if (shouldBrake && speed <= MovementConfig.SpeedStopEpsilon && distance <= arrival)
                    {
                        ClearPath();
                        return new Vector3(0, 0, 0);
                    }

                    if (shouldBrake)
                    {
                        if (distance > 1e-4f)
                            RotateToward(to * (1.0 / distance), dt);
                        return new Vector3(0, 0, 0);
                    }

                    Vector3 dir = to * (1.0 / Math.Max(distance, 1e-4f));
                    RotateToward(dir, dt);
                    return dir * forwardLimit;
                }

                Vector3 mid = to * (1.0 / Math.Max(distance, 1e-4f));
                RotateToward(mid, dt);
                return mid * forwardLimit;
            }

            return new Vector3(0, 0, 0);
        }

        void IntegrateVelocity(Vector3 desired, float maxVel, float dt)
        {
            float mass = MovementConfig.Mass;
            float maxForce = ComputeMaxForce(maxVel);
            Vector3 steerForce = (desired - _velocity) * maxForce;
            double steerLen = Vector3.Abs(steerForce);
            Vector3 force = steerLen > maxForce && steerLen > 1e-8
                ? steerForce * (maxForce / steerLen)
                : steerForce;
            _velocity += force * (dt / mass);

            double speed = Vector3.Abs(_velocity);
            if (speed > maxVel && speed > 1e-8)
                _velocity *= maxVel / speed;

            // Only snap to zero when not trying to move — otherwise low max speeds
            // (walk/strafe/back) never exceed SpeedStopEpsilon on the first frames.
            if (Vector3.Abs(desired) < 1e-6
                && Vector3.Abs(_velocity) < MovementConfig.SpeedStopEpsilon)
                _velocity = new Vector3(0, 0, 0);
        }

        float ComputeMaxForce(float maxVel) =>
            MovementConfig.Mass * maxVel / MovementConfig.ForceReachTime;

        float GetTurnRateRadians()
        {
            bool moving = ((_flags & TranslationFlags) != 0)
                || HasPath
                || Vector3.Abs(_velocity) > MovementConfig.SpeedStopEpsilon;
            return moving
                ? MovementConfig.TurnRateRadiansMoving
                : MovementConfig.TurnRateRadiansStopped;
        }

        void RotateToward(Vector3 direction, float dt)
        {
            if (Vector3.Abs(direction) < 1e-6)
                return;

            float targetYaw = MathF.Atan2((float)direction.x, (float)direction.z) * (180f / MathF.PI);
            float currentYaw = GetYawDegrees();
            float delta = NormalizeAngle(targetYaw - currentYaw);
            float maxStep = MovementConfig.PathTurnRateDegrees * dt;
            delta = Math.Clamp(delta, -maxStep, maxStep);
            RotateYaw(delta);
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
        /// Finds the surface holding the character up. Support has to be tolerant: the client's
        /// reported Y can sit a little above the baked terrain (props and buildings have no
        /// collision yet), and a zero-tolerance probe there would make the character fall every
        /// tick and get snapped back by the next CharDCMove.
        /// </summary>
        bool TryResolveGroundSupport(out float groundY)
        {
            groundY = (float)_character.Position.y;
            WorldSimulation.PlayfieldWorldSimulation? world = _character.Playfield?.WorldAccess.Instance;
            if (world == null)
                return true;

            Vector3 from = new(
                _character.Position.x,
                _character.Position.y + MovementConfig.GroundProbeLift,
                _character.Position.z);
            float depth = MovementConfig.GroundProbeLift + MovementConfig.GroundSnapTolerance;
            if (!world.TryRaycastDown(from, depth, out Vector3 hit))
                return false;

            groundY = (float)hit.y;
            return true;
        }

        bool HasGeometryBelow()
        {
            WorldSimulation.PlayfieldWorldSimulation? world = _character.Playfield?.WorldAccess.Instance;
            if (world == null)
                return false;

            Vector3 from = new(
                _character.Position.x,
                _character.Position.y + MovementConfig.GroundProbeLift,
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
