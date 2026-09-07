namespace ZoneEngine_New.Core.Movement
{
    using System;

    [Flags]
    public enum MovementFlags
    {
        None = 0,
        Forward = 1 << 0,
        Backward = 1 << 1,
        TurnLeft = 1 << 2,
        TurnRight = 1 << 3,
        StrafeLeft = 1 << 4,
        StrafeRight = 1 << 5,
        Jump = 1 << 6,
        MouseTurn = 1 << 7,
        ElevateUp = 1 << 8,
        ElevateDown = 1 << 9,
    }

    /// <summary>
    /// Matches AO CurrentMovementMode / CharDCMove mode ids.
    /// </summary>
    public enum MovementState
    {
        Unknown = 0,
        Rooted = 1,
        Walk = 2,
        Run = 3,
        Swim = 4,
        Crawl = 5,
        Sneak = 6,
        Fly = 7,
        Sit = 8,
        RootedCanSit = 9,
        Sleep = 11,
        Lounge = 12,
    }

    /// <summary>
    /// CharDCMove MoveType values.
    /// </summary>
    public enum MovementAction : byte
    {
        ForwardStart = 0x01,
        ForwardStop = 0x02,
        BackwardStart = 0x03,
        BackwardStop = 0x04,
        StrafeRightStart = 0x05,
        StrafeRightStop = 0x06,
        StrafeLeftStart = 0x07,
        StrafeLeftStop = 0x08,
        TurnRightStart = 0x09,
        TurnRightMouse = 0x0a,
        TurnRightStop = 0x0b,
        TurnLeftStart = 0x0c,
        TurnLeftMouse = 0x0d,
        TurnLeftStop = 0x0e,
        JumpStart = 0x0f,
        JumpStop = 0x10,
        ElevateUpStart = 0x11,
        ElevateUpStop = 0x12,
        ElevateDownStart = 0x13,
        ElevateDownStop = 0x14,
        FullStop = 0x15,
        Update = 0x16,
        SwitchToFrozen = 0x17,
        SwitchToWalk = 0x18,
        SwitchToRun = 0x19,
        SwitchToSwim = 0x1a,
        SwitchToCrawl = 0x1b,
        SwitchToSneak = 0x1c,
        SwitchToFly = 0x1d,
        SwitchToSit = 0x1e,
        Unknown0x1f = 0x1f,
        Unknown0x20 = 0x20,
        SwitchToSleep = 0x21,
        SwitchToLounge = 0x22,
        LeaveSwim = 0x23,
        LeaveSneak = 0x24,
        LeaveSit = 0x25,
        LeaveFrozen = 0x26,
        LeaveFly = 0x27,
        LeaveCrawl = 0x28,
        LeaveSleep = 0x29,
        LeaveLounge = 0x2a
    }
}
