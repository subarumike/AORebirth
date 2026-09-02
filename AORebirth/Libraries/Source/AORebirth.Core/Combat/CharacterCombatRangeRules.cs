namespace AORebirth.Core.Combat
{
    public enum PlayerCombatRangeDecision
    {
        InRange,
        SoftSkip,
        HardCancel
    }

    public static class CharacterCombatRangeRules
    {
        public const double MaxMeleeCombatDistance = 4.0;

        private const double SoftRangeGraceMeters = 1.5;

        private const double HardRangeMultiplier = 3.0;

        public static PlayerCombatRangeDecision EvaluatePlayerRange(double distance, double attackRange)
        {
            double effectiveRange = attackRange > 0.0 ? attackRange : MaxMeleeCombatDistance;
            if (distance > effectiveRange * HardRangeMultiplier)
            {
                return PlayerCombatRangeDecision.HardCancel;
            }

            if (distance > effectiveRange + SoftRangeGraceMeters)
            {
                return PlayerCombatRangeDecision.SoftSkip;
            }

            return PlayerCombatRangeDecision.InRange;
        }
    }
}
