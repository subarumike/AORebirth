namespace ZoneEngine.Core.Arete.Quests
{
    #region Usings ...

    using System.Collections.Generic;
    using System.Linq;

    using ZoneEngine.Core.Arete;

    #endregion

    public sealed class QuestContentRegistry
    {
        private readonly Dictionary<string, QuestContentPack> packsById =
            new Dictionary<string, QuestContentPack>(System.StringComparer.OrdinalIgnoreCase);

        private readonly Dictionary<string, QuestDefinition> questsById =
            new Dictionary<string, QuestDefinition>(System.StringComparer.OrdinalIgnoreCase);

        private readonly List<QuestChainLinkMetadata> links = new List<QuestChainLinkMetadata>();

        public int PackCount
        {
            get
            {
                return this.packsById.Count;
            }
        }

        public int QuestCount
        {
            get
            {
                return this.questsById.Count;
            }
        }

        public AreteValidationResult Load(IEnumerable<QuestContentPack> packs)
        {
            AreteContentLoadResult<QuestContentPack> loadResult = new QuestContentPackLoader().Load(packs);
            return this.ApplyLoadResult(loadResult);
        }

        public AreteValidationResult LoadFromFiles(IEnumerable<string> filePaths)
        {
            AreteContentLoadResult<QuestContentPack> loadResult = new QuestContentPackLoader().LoadFiles(filePaths);
            return this.ApplyLoadResult(loadResult);
        }

        public AreteValidationResult LoadFromDirectory(string directoryPath)
        {
            AreteContentLoadResult<QuestContentPack> loadResult = new QuestContentPackLoader().LoadDirectory(directoryPath);
            return this.ApplyLoadResult(loadResult);
        }

        public AreteValidationResult LoadFromManifest(string manifestPath)
        {
            AreteContentLoadResult<QuestContentPack> loadResult = new QuestContentPackLoader().LoadManifest(manifestPath);
            return this.ApplyLoadResult(loadResult);
        }

        private AreteValidationResult ApplyLoadResult(AreteContentLoadResult<QuestContentPack> loadResult)
        {
            if (!loadResult.IsValid)
            {
                return loadResult.Validation;
            }

            this.packsById.Clear();
            this.questsById.Clear();
            this.links.Clear();

            foreach (QuestContentPack pack in loadResult.Packs)
            {
                this.packsById.Add(pack.Identity.Id, pack);
                foreach (QuestDefinition quest in pack.Quests ?? Enumerable.Empty<QuestDefinition>())
                {
                    this.questsById.Add(quest.QuestId, quest);
                }

                foreach (QuestChainLinkMetadata link in pack.Links ?? Enumerable.Empty<QuestChainLinkMetadata>())
                {
                    this.links.Add(link);
                }
            }

            return loadResult.Validation;
        }

        public IEnumerable<QuestChainLinkMetadata> GetLinksFrom(string questId)
        {
            if (string.IsNullOrWhiteSpace(questId))
            {
                return Enumerable.Empty<QuestChainLinkMetadata>();
            }

            return this.links
                .Where(
                    link => link != null
                            && string.Equals(link.FromQuestId, questId, System.StringComparison.OrdinalIgnoreCase))
                .ToList();
        }

        public IEnumerable<QuestChainLinkMetadata> GetLinksTo(string questId)
        {
            if (string.IsNullOrWhiteSpace(questId))
            {
                return Enumerable.Empty<QuestChainLinkMetadata>();
            }

            return this.links
                .Where(
                    link => link != null
                            && string.Equals(link.ToQuestId, questId, System.StringComparison.OrdinalIgnoreCase))
                .ToList();
        }

        public IEnumerable<QuestDefinition> GetQuests()
        {
            return this.questsById.Values.ToList();
        }

        public bool TryGetQuest(string questId, out QuestDefinition quest)
        {
            if (string.IsNullOrWhiteSpace(questId))
            {
                quest = null;
                return false;
            }

            return this.questsById.TryGetValue(questId, out quest);
        }
    }
}
