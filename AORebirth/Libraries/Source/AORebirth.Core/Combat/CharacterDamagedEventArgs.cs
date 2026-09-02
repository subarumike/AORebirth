namespace AORebirth.Core.Combat
{
    using System;

    using AORebirth.Core.Entities;

    using SmokeLounge.AOtomation.Messaging.GameData;

    public sealed class CharacterDamagedEventArgs : EventArgs
    {
        public ICharacter Attacker { get; set; }

        public ICharacter Target { get; set; }

        public CombatStrikeContext Context { get; set; }

        public int Damage { get; set; }

        public int PreviousHealth { get; set; }

        public int NewHealth { get; set; }

        public bool KillingHit { get; set; }

        public HitType HitType { get; set; } = HitType.Normal;
    }
}
