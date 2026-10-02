namespace ZoneEngine_New.Core.Data
{
    using System.Collections.Generic;
    using System.Linq;
    using AORebirth.Interfaces.Persistence.Characters;
    using static SharedCharacterPersistence;

    /// <summary>Reads <c>characterperklocks</c> for login; the flush writer replaces the set after a LockPerk.</summary>
    public sealed class MySqlPerkLockRepository : IPerkLockRepository
    {
        readonly ICharacterPersistenceDao _persistence;

        public MySqlPerkLockRepository(ICharacterPersistenceDao? persistence = null)
            => _persistence = persistence ?? Create();

        public IReadOnlyList<SkillLockRecord> GetForCharacter(int characterId)
        {
            if (characterId <= 0)
                return [];

            return Run(() => _persistence.LoadPerkLocks(characterId)
                .Select(v => new SkillLockRecord { StatId = v.PerkId, ExpiresAtUtcTicks = v.ExpiresAtUtcTicks })
                .ToArray());
        }

        public void Save(int characterId, IReadOnlyList<SkillLockRecord> perkLocks)
            => Run(() => _persistence.SavePerkLocks(characterId, perkLocks
                .Select(v => new PersistedPerkLockData { PerkId = v.StatId, ExpiresAtUtcTicks = v.ExpiresAtUtcTicks })
                .ToArray()));
    }
}
