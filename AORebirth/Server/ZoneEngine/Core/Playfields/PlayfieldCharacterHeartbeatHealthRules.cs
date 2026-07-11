namespace ZoneEngine.Core.Playfields
{
    using System;

    internal static class PlayfieldCharacterHeartbeatHealthRules
    {
        internal static bool IsLivingHealth(int currentHealth)
        {
            return currentHealth > 0;
        }

        internal static bool CanRegenerateNpcHealth(int currentHealth, int maximumHealth)
        {
            return IsLivingHealth(currentHealth) && maximumHealth > currentHealth;
        }

        internal static bool IsLivingNpcAttackCandidate<TCandidate>(
            TCandidate candidate,
            bool targetsNpc,
            Func<TCandidate, int> readCurrentHealth)
        {
            if (!targetsNpc)
            {
                return false;
            }

            if (readCurrentHealth == null)
            {
                throw new ArgumentNullException("readCurrentHealth");
            }

            return IsLivingHealth(readCurrentHealth(candidate));
        }
    }
}
