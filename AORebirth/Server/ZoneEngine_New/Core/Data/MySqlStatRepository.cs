namespace ZoneEngine_New.Core.Data
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using AORebirth.Interfaces.Persistence.Characters;
    using ZoneEngine_New.Core.Logging;
    using static SharedCharacterPersistence;
    public sealed class MySqlStatRepository : IStatRepository
    {
        readonly IZoneLogger _logger;
        readonly ICharacterPersistenceDao _persistence;
        public MySqlStatRepository(IZoneLogger logger, ICharacterPersistenceDao? persistence = null)
        { _logger = logger ?? throw new ArgumentNullException(nameof(logger)); _persistence = persistence ?? Create(); }
        public IReadOnlyList<StatRecord> GetForCharacter(int characterId)
            => Run(() => _persistence.LoadStats(characterId).Select(Map).ToArray(), _logger);
        public void UpsertForCharacter(int characterId, IReadOnlyList<StatRecord> stats)
            => Run(() => _persistence.SaveStats(characterId, stats.Select(Map).ToArray()), _logger);
    }
}
