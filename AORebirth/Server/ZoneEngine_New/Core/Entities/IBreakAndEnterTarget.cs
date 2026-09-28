namespace ZoneEngine_New.Core.Entities
{
    using SmokeLounge.AOtomation.Messaging.GameData;
    using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;

    /// <summary>
    /// Something a Break and Enter item (CanFlags.BreakAndEnter, e.g. Lock Pick 95577) can open: doors and chests now,
    /// traps later. The client only lets a pick be tried on a target that carries LockDifficulty (stat 299)
    /// (Gamecode.dll 0x10081a0d, "You can't pick this lock" otherwise), then sends GenericCmd UseItemOnItem with the
    /// item's inventory slot and the target.
    /// </summary>
    public interface IBreakAndEnterTarget
    {
        Identity Identity { get; }

        bool IsLocked { get; }

        /// <summary>Stat 299 the client needs on a lockable target; 0 when the target has no lock.</summary>
        int LockDifficulty { get; }

        /// <summary>Unlocks the target for <paramref name="player"/>; false when it is not locked or out of reach.</summary>
        bool TryBreakAndEnter(Player player);
    }

    /// <summary>
    /// The action result the client applies to a lockable target (ActionIIR_t, Gamecode.dll 0x1009ee72 -> the target's
    /// result handler 0x10087d40): code 0x64 clears its Locked flag (0x40) and opens it for the acting character,
    /// 0x65 shows "Lockpicking failed".
    /// </summary>
    public static class BreakAndEnterActions
    {
        public const int Unlocked = 0x64;

        public const int Failed = 0x65;

        public static ActionMessage Result(Identity target, Identity actor, int code) => new()
        {
            Identity = target,
            Unknown = 0,
            ActionCode = 1,
            ActionIdentity = code,
            Target = actor
        };
    }
}
