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

        bool TryAcceptOfferQuest(int ownerId, int offerType, int offerInstance, GeneratedQuestRow quest,
            CharacterQuestRow characterRow, QuestDungeonKeyItemRow key, long nowUtcTicks);

        string? TryAddDuplicateKey(int sourceKeyInstanceId, QuestDungeonKeyItemRow copy, int maxKeys, long nowUtcTicks);

        IDictionary<int, string> LoadKeyQuests(IReadOnlyCollection<int> keyInstanceIds);

        IList<RetiredDungeonKey> EndQuestKeys(string questId);

        IList<int> RetireUnlinkedKeys(IReadOnlyCollection<int> keyInstanceIds, int keyLowId);

        int RetireDeadKeysForCharacter(int characterId, int keyLowId, IReadOnlyCollection<int> ownedContainerTypes, int bagContainerType);

        IList<string> LoadExpiredQuestsWithKeys(long nowUtcTicks, int limit);

        int PurgeEndedGeneratedQuests(long cutoffUtcTicks, int limit);

        void CloseCharacterQuests(string questId, int state, long nowUtcTicks);
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

        public bool TryAcceptOfferQuest(int ownerId, int offerType, int offerInstance, GeneratedQuestRow quest,
            CharacterQuestRow characterRow, QuestDungeonKeyItemRow key, long nowUtcTicks)
            => Run(() => _dao.TryAcceptOfferQuest(ownerId, offerType, offerInstance, quest, characterRow, key, nowUtcTicks), _logger);

        public string? TryAddDuplicateKey(int sourceKeyInstanceId, QuestDungeonKeyItemRow copy, int maxKeys, long nowUtcTicks)
            => Run(() => _dao.TryAddDuplicateKey(sourceKeyInstanceId, copy, maxKeys, nowUtcTicks), _logger);

        public IDictionary<int, string> LoadKeyQuests(IReadOnlyCollection<int> keyInstanceIds)
            => Run(() => _dao.LoadKeyQuests(keyInstanceIds), _logger);

        public IList<RetiredDungeonKey> EndQuestKeys(string questId) => Run(() => _dao.EndQuestKeys(questId), _logger);

        public IList<int> RetireUnlinkedKeys(IReadOnlyCollection<int> keyInstanceIds, int keyLowId)
            => Run(() => _dao.RetireUnlinkedKeys(keyInstanceIds, keyLowId), _logger);

        public int RetireDeadKeysForCharacter(int characterId, int keyLowId, IReadOnlyCollection<int> ownedContainerTypes, int bagContainerType)
            => Run(() => _dao.RetireDeadKeysForCharacter(characterId, keyLowId, ownedContainerTypes, bagContainerType), _logger);

        public IList<string> LoadExpiredQuestsWithKeys(long nowUtcTicks, int limit)
            => Run(() => _dao.LoadExpiredQuestsWithKeys(nowUtcTicks, limit), _logger);

        public int PurgeEndedGeneratedQuests(long cutoffUtcTicks, int limit)
            => Run(() => _dao.PurgeEndedGeneratedQuests(cutoffUtcTicks, limit), _logger);

        public void CloseCharacterQuests(string questId, int state, long nowUtcTicks)
            => Run(() => _dao.CloseCharacterQuests(questId, state, nowUtcTicks), _logger);
    }
}
