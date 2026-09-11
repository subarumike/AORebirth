namespace ZoneEngine_New.Core.Data
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using AORebirth.Interfaces.Persistence.Characters;
    using ZoneEngine_New.Core.Logging;
    using static SharedCharacterPersistence;
    public sealed class MySqlCharacterRepository : ICharacterRepository
    {
        readonly IZoneLogger _logger;
        readonly ICharacterDao _directory;
        readonly ICharacterPersistenceDao _persistence;
        public MySqlCharacterRepository(IZoneLogger logger, ICharacterDao? directory = null, ICharacterPersistenceDao? persistence = null)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _persistence = persistence ?? Create();
            _directory = directory ?? new AORebirth.Database.Domain.Characters.MySqlCharacterDao(() =>
                new MySqlConnector.MySqlConnection(MySqlConnectionSettings.GetRequiredConnectionString()));
        }
        public CharacterRecord? GetById(int characterId) => Run(() =>
        {
            var row = _persistence.LoadCharacter(characterId);
            return row == null ? null : Map(row);
        }, _logger);
        public void SaveLocation(CharacterRecord character, int online) => Run(() => _persistence.SaveLocation(Map(character), online), _logger);
        public void SaveSnapshot(CharacterRecord character, int online, IReadOnlyList<StatRecord> stats)
            => Run(() => _persistence.SaveSnapshot(Map(character), online, stats.Select(Map).ToArray()), _logger);
        public void SetOnline(int characterId) => SetOnlineState(characterId, 1);
        public void SetOffline(int characterId) => SetOnlineState(characterId, 0);
        private void SetOnlineState(int characterId, int online)
        {
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(characterId);
            if ((online == 1 ? _directory.MarkOnline(characterId) : _directory.MarkOffline(characterId)) != 1)
                throw new InvalidOperationException("Character row is missing during online ownership acquisition.");
        }
    }
}
