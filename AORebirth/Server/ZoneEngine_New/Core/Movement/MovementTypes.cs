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
    /// CharDCMove MoveType values (legacy UpdateMoveType / AOSharp MovementAction).
    /// </summary>
    public enum MovementAction : byte
    {
        Update = 0,
        ForwardStart = 1,
        ForwardStop = 2,
        BackwardStart = 3,
        BackwardStop = 4,
        StrafeRightStart = 5,
        StrafeRightStop = 6,
        StrafeLeftStart = 7,
        StrafeLeftStop = 8,
        TurnRightStart = 9,
        MouseTurnRightStart = 10,
        TurnRightStop = 11,
        TurnLeftStart = 12,
        MouseTurnLeftStart = 13,
        TurnLeftStop = 14,
        JumpStart = 15,
        JumpStop = 16,
        ElevateUpStart = 17,
        ElevateUpStop = 18,
        FullStop = 21,
        SwitchToFrozen = 23,
        SwitchToWalk = 24,
        SwitchToRun = 25,
        SwitchToSwim = 26,
        SwitchToCrawl = 27,
        SwitchToSneak = 28,
        SwitchToFly = 29,
        SwitchToSit = 30,
        SwitchToSleep = 33,
        SwitchToLounge = 34,
        LeaveSwim = 35,
        LeaveSneak = 36,
        LeaveSit = 37,
        LeaveFrozen = 38,
        LeaveFly = 39,
        LeaveCrawl = 40,
        LeaveSleep = 41,
        LeaveLounge = 42,
    }
}
