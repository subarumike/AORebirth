namespace ZoneEngine.Core.Arete
{
    #region Usings ...

    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;

    using ZoneEngine.Core.Arete.Dialogue;
    using ZoneEngine.Core.Arete.Quests;

    #endregion

    public sealed class AreteFrameworkRegistries
    {
        public AreteFrameworkRegistries(
            DialogueContentRegistry dialogueRegistry,
            QuestContentRegistry questRegistry,
            AreteValidationResult validation)
        {
            this.DialogueRegistry = dialogueRegistry;
            this.QuestRegistry = questRegistry;
            this.Validation = validation;
        }

        public DialogueContentRegistry DialogueRegistry { get; private set; }

        public QuestContentRegistry QuestRegistry { get; private set; }

        public AreteValidationResult Validation { get; private set; }

        public bool IsValid
        {
            get
            {
                return this.Validation.IsValid;
            }
        }
    }

    public static class AreteFrameworkBootstrap
    {
        private static readonly object SyncRoot = new object();

        private static readonly string[] CheckedInManifestRelativePaths =
        {
            Path.Combine("Content", "Arete", "rex-larsson", "manifest.json"),
            Path.Combine("Content", "Arete", "marcus-stone", "manifest.json"),
            Path.Combine("Content", "Subway", "windcaller-karrec", "manifest.json"),
            Path.Combine("Content", "Subway", "tailor", "manifest.json")
        };

        private static AreteFrameworkRegistries current;

        public static AreteFrameworkRegistries Current
        {
            get
            {
                lock (SyncRoot)
                {
                    if (current == null)
                    {
                        current = CreateCheckedInContent(AppDomain.CurrentDomain.BaseDirectory);
                    }

                    return current;
                }
            }
        }

        public static AreteFrameworkRegistries InitializeCheckedInContent()
        {
            return InitializeCheckedInContent(AppDomain.CurrentDomain.BaseDirectory);
        }

        public static AreteFrameworkRegistries InitializeCheckedInContent(string runtimeBaseDirectory)
        {
            AreteFrameworkRegistries initialized = CreateCheckedInContent(runtimeBaseDirectory);

            lock (SyncRoot)
            {
                current = initialized;
                return current;
            }
        }

        public static AreteFrameworkRegistries LoadManifestSet(IEnumerable<string> manifestPaths)
        {
            var validation = new AreteValidationResult();
            var dialogueFilePaths = new List<string>();
            var questFilePaths = new List<string>();
            List<string> manifests = new List<string>(manifestPaths ?? Enumerable.Empty<string>());

            if (manifests.Count == 0)
            {
                validation.AddError("contentManifests", "no content manifest paths were provided");
            }

            var manifestLoader = new AreteContentManifestLoader();
            foreach (string manifestPath in manifests)
            {
                AreteContentManifestLoadResult manifestResult = manifestLoader.Load(manifestPath);
                validation.AddErrors(manifestResult.Validation);

                if (!manifestResult.IsValid)
                {
                    continue;
                }

                dialogueFilePaths.AddRange(manifestResult.DialoguePackFiles);
                questFilePaths.AddRange(manifestResult.QuestPackFiles);
            }

            AreteContentLoadResult<DialogueContentPack> dialogueLoadResult =
                new DialogueContentPackLoader().LoadFiles(dialogueFilePaths);
            AreteContentLoadResult<QuestContentPack> questLoadResult =
                new QuestContentPackLoader().LoadFiles(questFilePaths);

            validation.AddErrors(dialogueLoadResult.Validation);
            validation.AddErrors(questLoadResult.Validation);

            var dialogueRegistry = new DialogueContentRegistry();
            var questRegistry = new QuestContentRegistry();

            if (!validation.IsValid)
            {
                return new AreteFrameworkRegistries(dialogueRegistry, questRegistry, validation);
            }

            validation.AddErrors(dialogueRegistry.Load(dialogueLoadResult.Packs));
            validation.AddErrors(questRegistry.Load(questLoadResult.Packs));

            if (validation.IsValid)
            {
                validation.AddErrors(
                    DialogueActionReferenceValidator.Validate(dialogueLoadResult.Packs, questRegistry));
                validation.AddErrors(
                    AreteConditionReferenceValidator.Validate(
                        dialogueLoadResult.Packs,
                        questLoadResult.Packs,
                        dialogueRegistry,
                        questRegistry));
            }

            if (!validation.IsValid)
            {
                dialogueRegistry = new DialogueContentRegistry();
                questRegistry = new QuestContentRegistry();
            }

            return new AreteFrameworkRegistries(dialogueRegistry, questRegistry, validation);
        }

        public static AreteFrameworkRegistries InitializeEmptyRegistries()
        {
            var validation = new AreteValidationResult();
            var dialogueRegistry = new DialogueContentRegistry();
            var questRegistry = new QuestContentRegistry();

            validation.AddErrors(dialogueRegistry.Load(Enumerable.Empty<DialogueContentPack>()));
            validation.AddErrors(questRegistry.Load(Enumerable.Empty<QuestContentPack>()));

            return new AreteFrameworkRegistries(dialogueRegistry, questRegistry, validation);
        }

        private static AreteFrameworkRegistries CreateCheckedInContent(string runtimeBaseDirectory)
        {
            if (string.IsNullOrWhiteSpace(runtimeBaseDirectory))
            {
                throw new ArgumentException("A runtime base directory is required.", "runtimeBaseDirectory");
            }

            string fullBaseDirectory = Path.GetFullPath(runtimeBaseDirectory);
            AreteFrameworkRegistries result = LoadManifestSet(
                CheckedInManifestRelativePaths.Select(
                    relativePath => Path.Combine(fullBaseDirectory, relativePath)));

            if (!result.IsValid)
            {
                throw new InvalidDataException(
                    "Checked-in dialogue and quest content failed validation:"
                    + Environment.NewLine
                    + string.Join(Environment.NewLine, result.Validation.Errors));
            }

            return result;
        }
    }
}
