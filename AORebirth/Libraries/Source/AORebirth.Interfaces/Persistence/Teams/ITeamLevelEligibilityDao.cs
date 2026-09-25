namespace AORebirth.Interfaces.Persistence.Teams
{
    /// <summary>
    /// XP/SK team level windows from editable GameData (LFT filter + invite warnings).
    /// No hardcoded range tables in engines.
    /// </summary>
    public interface ITeamLevelEligibilityDao
    {
        /// <summary>Inclusive share window for a character of <paramref name="level"/>.</summary>
        bool TryGetRange(int level, out int minimum, out int maximum);

        /// <summary>Clamps to the configured level table endpoints.</summary>
        int ClampToConfiguredLevel(int level);

        /// <summary>
        /// True when <paramref name="candidateLevel"/> is inside the share window of
        /// <paramref name="searcherLevel"/> (LFT list filter).
        /// </summary>
        bool IsCompatible(int searcherLevel, int candidateLevel);

        /// <summary>True when invitee is above the member's share maximum.</summary>
        bool IsTooHighForMember(int memberLevel, int inviteeLevel);

        /// <summary>True when invitee is below the member's share minimum.</summary>
        bool IsTooLowForMember(int memberLevel, int inviteeLevel);
    }
}
