namespace ZoneEngine_New.Core.Data
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using AORebirth.Interfaces.Persistence.Characters;
    using ZoneEngine_New.Core.Logging;
    using static SharedCharacterPersistence;
    public sealed class MySqlUploadedNanoRepository : IUploadedNanoRepository
    {
        readonly IZoneLogger _logger;
        readonly ICharacterPersistenceDao _persistence;
        public MySqlUploadedNanoRepository(IZoneLogger logger, ICharacterPersistenceDao? persistence = null)
        { _logger = logger ?? throw new ArgumentNullException(nameof(logger)); _persistence = persistence ?? Create(); }
        public IReadOnlyList<int> GetForCharacter(int characterId) => Run(() => _persistence.LoadUploadedNanos(characterId).ToArray(), _logger);
    }
}
