namespace AORebirth.Enums
{
    using System;

    /// <summary>
    /// Nano-program bits stored on <c>ItemTemplate.Flags</c>. Same integer as item flags,
    /// different meaning when the template is a nano.
    /// </summary>
    [Flags]
    public enum NanoFlags : int
    {
        None = 0,
        NoResistCannotFumble = 1 << 1,
        BreakOnAttack = 1 << 3,
        BreakOnSpellAttack = 1 << 5,
        BreakOnDebuff = 1 << 7,
        Interruptable = BreakOnAttack | BreakOnSpellAttack | BreakOnDebuff,
        NoRemoveNoNCUFriendly = 1 << 8,
        NotRemovable = 1 << 14,
        IsHostile = 1 << 15,
        IsBuff = 1 << 16,
        NoTimerNotify = 1 << 19,
        DontRemoveOnDeath = 1 << 21,
        CannotRefresh = 1 << 23,
        WantCollision = 1 << 31,
    }
}
