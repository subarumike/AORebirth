namespace ZoneEngine_New.Core.Data
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using AORebirth.Interfaces.Persistence.Characters;
    using ZoneEngine_New.Core.Logging;
    using static SharedCharacterPersistence;
    public sealed class MySqlItemNameRepository : IItemNameRepository
    {
        readonly Dictionary<int, string> _names;
        public MySqlItemNameRepository(IZoneLogger logger, ICharacterPersistenceDao? persistence = null)
        {
            ArgumentNullException.ThrowIfNull(logger);
            _names = Run(() => new Dictionary<int, string>((persistence ?? Create()).LoadItemNames()), logger);
            logger.Info($"ItemNameRepository loaded {_names.Count} names");
        }
        public bool TryGetName(int aoid, out string name) => _names.TryGetValue(aoid, out name!);
        public IReadOnlyDictionary<int, string> GetAllNames() => _names;
    }
}
