namespace ZoneEngine_New.Core.Movement
{
    using System;

    using SmokeLounge.AOtomation.Messaging.GameData;
    using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;

    using ZoneEngine_New.Core.Entities;

    using Quaternion = AORebirth.Core.Vector.Quaternion;
    using Vector3 = AORebirth.Core.Vector.Vector3;

    /// <summary>
    /// Tracks movement flags/state and applies CharDCMove pose.
    /// Velocity integration is deferred.
    /// </summary>
    public sealed class CharacterMotor
    {
        readonly Character _character;

        MovementFlags _flags;
        MovementState _state = MovementState.Run;
        MovementState _lastSpeedMode = MovementState.Run;

        public CharacterMotor(Character character)
        {
            _character = character ?? throw new ArgumentNullException(nameof(character));
        }

        public MovementFlags MovementFlags => _flags;

        public MovementState State => _state;

        public void RefreshFromStats()
        {
            // Stat caches used when translation simulation lands.
        }

        public void OnStatChanged(CharacterStat stat, int previous, int next, bool isInitialSet)
        {
            switch (stat)
            {
                case CharacterStat.RunSpeed:
                case CharacterStat.Health:
                case CharacterStat.MaxHealth:
                case CharacterStat.Strength:
                case CharacterStat.Agility:
                case CharacterStat.GmLevel:
                    RefreshFromStats();
                    break;
            }
        }

        public void Consume(CharDCMoveMessage message)
        {
            ArgumentNullException.ThrowIfNull(message);

            ApplyAction((MovementAction)message.MoveType);

            _character.Position = new Vector3(
                message.Coordinates.X,
                message.Coordinates.Y,
                message.Coordinates.Z);
            _character.Rotation = new Quaternion(
                message.Heading.X,
                message.Heading.Y,
                message.Heading.Z,
                message.Heading.W);
        }

        public void Tick(double deltaTime)
        {
            // Translation simulation deferred.
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
                    SetFlags(_flags | MovementFlags.TurnLeft);
                    break;
                case MovementAction.TurnLeftStop:
                    SetFlags(_flags & ~MovementFlags.TurnLeft);
                    break;
                case MovementAction.TurnRightStart:
                    SetFlags(_flags | MovementFlags.TurnRight);
                    break;
                case MovementAction.TurnRightStop:
                    SetFlags(_flags & ~MovementFlags.TurnRight);
                    break;
                case MovementAction.JumpStart:
                    SetFlags(_flags | MovementFlags.Jump);
                    break;
                case MovementAction.JumpStop:
                    SetFlags(_flags & ~MovementFlags.Jump);
                    break;
                case MovementAction.FullStop:
                    StopAllFlags();
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
        }

        void StopAllFlags()
        {
            SetFlags(MovementFlags.None);
        }

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
    }
}
