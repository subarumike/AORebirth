namespace ZoneEngine_New.Core.Data
{
    using System.Collections.Generic;

    using AORebirth.Database.Domain.Quests;

    using ZoneEngine_New.Core.Logging;

    using static SharedCharacterPersistence;

    public interface ICharacterQuestStore
    {
        IList<CharacterQuestRow> Load(int characterId);

        IList<GeneratedQuestRow> LoadGeneratedFor(int characterId);

        GeneratedQuestRow? LoadGenerated(string questId);

        void Save(CharacterQuestRow row);

        void SaveGenerated(GeneratedQuestRow row);
    }

    /// <summary>characterquests and generatedquests through the shared database library.</summary>
    public sealed class MySqlCharacterQuestStore : ICharacterQuestStore
    {
        readonly MySqlCharacterQuestDao _dao;
        readonly IZoneLogger _logger;

        public MySqlCharacterQuestStore(IZoneLogger logger)
        {
            _logger = logger;
            string connectionString = MySqlConnectionSettings.GetRequiredConnectionString();
            _dao = new MySqlCharacterQuestDao(() => new MySqlConnector.MySqlConnection(connectionString));
        }

        public IList<CharacterQuestRow> Load(int characterId) => Run(() => _dao.LoadForCharacter(characterId), _logger);

        public IList<GeneratedQuestRow> LoadGeneratedFor(int characterId) => Run(() => _dao.LoadGeneratedForCharacter(characterId), _logger);

        public GeneratedQuestRow? LoadGenerated(string questId) => Run(() => _dao.LoadGenerated(questId), _logger);

        public void Save(CharacterQuestRow row) => Run(() => _dao.SaveCharacterQuest(row), _logger);

        public void SaveGenerated(GeneratedQuestRow row) => Run(() => _dao.SaveGenerated(row), _logger);
    }
}
