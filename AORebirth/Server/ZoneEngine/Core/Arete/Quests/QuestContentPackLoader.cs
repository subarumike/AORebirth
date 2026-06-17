namespace ZoneEngine.Core.Arete.Quests
{
    #region Usings ...

    using System.Collections.Generic;
    using System.Linq;

    using ZoneEngine.Core.Arete;

    #endregion

    public sealed class QuestContentPackLoader
    {
        public AreteContentLoadResult<QuestContentPack> LoadFile(string filePath)
        {
            return this.LoadFiles(new[] { filePath });
        }

        public AreteContentLoadResult<QuestContentPack> LoadDirectory(string directoryPath)
        {
            return AreteJsonContentFileLoader.LoadDirectory<QuestContentPack>(
                directoryPath,
                "quest",
                QuestContentPackValidator.Validate);
        }

        public AreteContentLoadResult<QuestContentPack> LoadFiles(IEnumerable<string> filePaths)
        {
            return AreteJsonContentFileLoader.LoadFiles<QuestContentPack>(
                filePaths,
                "quest",
                QuestContentPackValidator.Validate);
        }

        public AreteContentLoadResult<QuestContentPack> LoadManifest(string manifestPath)
        {
            AreteContentManifestLoadResult manifestResult = new AreteContentManifestLoader().Load(manifestPath);
            var validation = new AreteValidationResult();
            validation.AddErrors(manifestResult.Validation);

            if (!manifestResult.IsValid)
            {
                return new AreteContentLoadResult<QuestContentPack>(
                    Enumerable.Empty<QuestContentPack>(),
                    validation);
            }

            AreteContentLoadResult<QuestContentPack> loadResult = this.LoadFiles(manifestResult.QuestPackFiles);
            validation.AddErrors(loadResult.Validation);
            return new AreteContentLoadResult<QuestContentPack>(loadResult.Packs, validation);
        }

        public AreteContentLoadResult<QuestContentPack> Load(IEnumerable<QuestContentPack> packs)
        {
            List<QuestContentPack> loadedPacks = new List<QuestContentPack>(packs ?? Enumerable.Empty<QuestContentPack>());
            AreteValidationResult validation = QuestContentPackValidator.Validate(loadedPacks);
            return new AreteContentLoadResult<QuestContentPack>(loadedPacks, validation);
        }

        public AreteContentLoadResult<QuestContentPack> LoadEmpty()
        {
            return this.Load(Enumerable.Empty<QuestContentPack>());
        }
    }
}
