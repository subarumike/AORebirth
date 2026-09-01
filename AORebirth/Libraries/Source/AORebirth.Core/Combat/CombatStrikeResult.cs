namespace AORebirth.Core.Combat
{
    using SmokeLounge.AOtomation.Messaging.GameData;

    public enum StrikeOutcome
    {
        Applied,
        Missed,
        SkippedSoftRange,
        RejectedHardRange,
        RejectedInvalidTarget,
        RejectedNoWeapon,
        RejectedEngage
    }

    public sealed class CombatStrikeResult
    {
        public StrikeOutcome Outcome { get; set; }

        public int Damage { get; set; }

        public int PreviousHealth { get; set; }

        public int NewHealth { get; set; }

        public bool KillingHit { get; set; }

        public bool IsHit { get; set; }

        public HitType HitType { get; set; } = HitType.Normal;
    }
}
