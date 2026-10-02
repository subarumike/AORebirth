namespace ZoneEngine_New.Core.Data
{
    using System.Collections.Generic;
    using System.Linq;
    using AORebirth.Interfaces.Persistence.Characters;
    using static SharedCharacterPersistence;

    /// <summary>Reads <c>charactersperks</c> for login; the flush writer replaces the set after train/untrain.</summary>
    public sealed class MySqlTrainedPerkRepository : ITrainedPerkRepository
    {
        readonly ICharacterPersistenceDao _persistence;

        public MySqlTrainedPerkRepository(ICharacterPersistenceDao? persistence = null)
            => _persistence = persistence ?? Create();

        public IReadOnlyList<int> GetForCharacter(int characterId)
        {
            if (characterId <= 0)
                return [];

            return Run(() => _persistence.LoadTrainedPerks(characterId).ToArray());
        }

        public void Save(int characterId, IReadOnlyList<int> perkIds)
            => Run(() => _persistence.SaveTrainedPerks(characterId, perkIds.ToArray()));
    }
}
