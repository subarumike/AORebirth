namespace ZoneEngine_New.Core.Data
{
    using System.Collections.Generic;
    using System.Linq;
    using AORebirth.Interfaces.Persistence.Characters;
    using static SharedCharacterPersistence;

    /// <summary>
    /// Reads <c>characterskilllocks</c> for login. Writes go through the character coalesce commit,
    /// which replaces the whole set alongside inventory and NCU.
    /// </summary>
    public sealed class MySqlSkillLockRepository : ISkillLockRepository
    {
        readonly ICharacterPersistenceDao _persistence;

        public MySqlSkillLockRepository(ICharacterPersistenceDao? persistence = null)
            => _persistence = persistence ?? Create();

        public IReadOnlyList<SkillLockRecord> GetForCharacter(int characterId)
        {
            if (characterId <= 0)
                return [];

            return Run(() => _persistence.LoadSkillLocks(characterId)
                .Select(v => new SkillLockRecord
                {
                    StatId = v.StatId,
                    ExpiresAtUtcTicks = v.ExpiresAtUtcTicks
                }).ToArray());
        }
    }
}
