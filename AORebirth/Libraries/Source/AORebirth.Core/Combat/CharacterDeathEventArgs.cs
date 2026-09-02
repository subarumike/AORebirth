namespace AORebirth.Core.Combat
{
    using System;

    using AORebirth.Core.Entities;

    public enum CharacterDeathCause
    {
        Combat,
        Script,
        Environment,
        Forced
    }

    public sealed class CharacterDeathEventArgs : EventArgs
    {
        public ICharacter Victim { get; set; }

        public ICharacter Killer { get; set; }

        public CharacterDeathCause Cause { get; set; }

        public CombatStrikeContext Context { get; set; }
    }
}
