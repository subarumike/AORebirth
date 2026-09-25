namespace ZoneEngine_New.Core.Teams;

using System;

using AORebirth.Core.Teams;
using AORebirth.Interfaces.Persistence.Teams;

/// <summary>
/// Zone wrapper over <see cref="ITeamLevelEligibilityDao"/> (GameData JSON).
/// Invitation policy remains in <see cref="TeamService"/>.
/// </summary>
internal sealed class TeamLevelEligibility
{
    internal readonly record struct LevelRange(int Level, int Minimum, int Maximum);

    internal static TeamLevelEligibility Current { get; } =
        new(JsonTeamLevelEligibilityDao.Current);

    readonly ITeamLevelEligibilityDao _dao;

    TeamLevelEligibility(ITeamLevelEligibilityDao dao) => _dao = dao;

    /// <summary>Test helper: load an alternate JSON path.</summary>
    internal static TeamLevelEligibility Load(string path)
        => new(JsonTeamLevelEligibilityDao.Load(path));

    internal LevelRange ForLevel(int level)
    {
        int clamped = _dao.ClampToConfiguredLevel(level);
        if (!_dao.TryGetRange(clamped, out int minimum, out int maximum))
            throw new InvalidOperationException("Team eligibility has no range for level " + clamped + ".");
        return new LevelRange(clamped, minimum, maximum);
    }

    internal bool IsTooHighForMember(int memberLevel, int inviteeLevel)
        => _dao.IsTooHighForMember(memberLevel, inviteeLevel);

    internal bool IsTooLowForMember(int memberLevel, int inviteeLevel)
        => _dao.IsTooLowForMember(memberLevel, inviteeLevel);
}
