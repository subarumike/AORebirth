namespace ZoneEngine.Core.Arete.Dialogue
{
    #region Usings ...

    using System.Collections.Generic;
    using System.Linq;

    using ZoneEngine.Core.Arete;

    #endregion

    public sealed class DialogueContentPackLoader
    {
        public AreteContentLoadResult<DialogueContentPack> LoadFile(string filePath)
        {
            return this.LoadFiles(new[] { filePath });
        }

        public AreteContentLoadResult<DialogueContentPack> LoadDirectory(string directoryPath)
        {
            return AreteJsonContentFileLoader.LoadDirectory<DialogueContentPack>(
                directoryPath,
                "dialogue",
                DialogueContentPackValidator.Validate);
        }

        public AreteContentLoadResult<DialogueContentPack> LoadFiles(IEnumerable<string> filePaths)
        {
            return AreteJsonContentFileLoader.LoadFiles<DialogueContentPack>(
                filePaths,
                "dialogue",
                DialogueContentPackValidator.Validate);
        }

        public AreteContentLoadResult<DialogueContentPack> LoadManifest(string manifestPath)
        {
            AreteContentManifestLoadResult manifestResult = new AreteContentManifestLoader().Load(manifestPath);
            var validation = new AreteValidationResult();
            validation.AddErrors(manifestResult.Validation);

            if (!manifestResult.IsValid)
            {
                return new AreteContentLoadResult<DialogueContentPack>(
                    Enumerable.Empty<DialogueContentPack>(),
                    validation);
            }

            AreteContentLoadResult<DialogueContentPack> loadResult = this.LoadFiles(manifestResult.DialoguePackFiles);
            validation.AddErrors(loadResult.Validation);
            return new AreteContentLoadResult<DialogueContentPack>(loadResult.Packs, validation);
        }

        public AreteContentLoadResult<DialogueContentPack> Load(IEnumerable<DialogueContentPack> packs)
        {
            List<DialogueContentPack> loadedPacks =
                new List<DialogueContentPack>(packs ?? Enumerable.Empty<DialogueContentPack>());
            AreteValidationResult validation = DialogueContentPackValidator.Validate(loadedPacks);
            return new AreteContentLoadResult<DialogueContentPack>(loadedPacks, validation);
        }

        public AreteContentLoadResult<DialogueContentPack> LoadEmpty()
        {
            return this.Load(Enumerable.Empty<DialogueContentPack>());
        }
    }
}
