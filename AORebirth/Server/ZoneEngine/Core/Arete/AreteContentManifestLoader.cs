namespace ZoneEngine.Core.Arete
{
    #region Usings ...

    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Web.Script.Serialization;

    #endregion

    public sealed class AreteContentManifestLoader
    {
        public AreteContentManifestLoadResult Load(string manifestPath)
        {
            var validation = new AreteValidationResult();
            var dialoguePackFiles = new List<string>();
            var questPackFiles = new List<string>();

            if (string.IsNullOrWhiteSpace(manifestPath))
            {
                validation.AddError("contentManifest", "missing content manifest file path");
                return new AreteContentManifestLoadResult(dialoguePackFiles, questPackFiles, validation);
            }

            if (!File.Exists(manifestPath))
            {
                validation.AddError(manifestPath, "content manifest file was not found");
                return new AreteContentManifestLoadResult(dialoguePackFiles, questPackFiles, validation);
            }

            AreteContentManifest manifest;
            try
            {
                var serializer = new JavaScriptSerializer { MaxJsonLength = int.MaxValue };
                manifest = serializer.Deserialize<AreteContentManifest>(File.ReadAllText(manifestPath));
            }
            catch (Exception exception)
            {
                validation.AddError(
                    manifestPath,
                    "failed to parse JSON manifest: " + exception.GetType().Name + ": " + exception.Message);
                return new AreteContentManifestLoadResult(dialoguePackFiles, questPackFiles, validation);
            }

            if (manifest == null)
            {
                validation.AddError(manifestPath, "JSON manifest did not contain a content manifest");
                return new AreteContentManifestLoadResult(dialoguePackFiles, questPackFiles, validation);
            }

            string baseDirectory = Path.GetDirectoryName(Path.GetFullPath(manifestPath));
            if (string.IsNullOrWhiteSpace(baseDirectory))
            {
                baseDirectory = Directory.GetCurrentDirectory();
            }

            AddResolvedPaths(
                dialoguePackFiles,
                manifest.DialoguePacks,
                baseDirectory,
                manifestPath,
                "DialoguePacks",
                validation);

            AddResolvedPaths(
                questPackFiles,
                manifest.QuestPacks,
                baseDirectory,
                manifestPath,
                "QuestPacks",
                validation);

            return new AreteContentManifestLoadResult(dialoguePackFiles, questPackFiles, validation);
        }

        private static void AddResolvedPaths(
            IList<string> resolvedPaths,
            IEnumerable<string> manifestPaths,
            string baseDirectory,
            string manifestPath,
            string collectionName,
            AreteValidationResult validation)
        {
            int pathIndex = 0;
            foreach (string contentPath in manifestPaths ?? Enumerable.Empty<string>())
            {
                string location = manifestPath + "." + collectionName + "[" + pathIndex + "]";
                if (string.IsNullOrWhiteSpace(contentPath))
                {
                    validation.AddError(location, "missing manifest content file path");
                    pathIndex++;
                    continue;
                }

                try
                {
                    string resolvedPath = Path.IsPathRooted(contentPath)
                        ? contentPath
                        : Path.Combine(baseDirectory, contentPath);

                    resolvedPaths.Add(Path.GetFullPath(resolvedPath));
                }
                catch (Exception exception)
                {
                    validation.AddError(
                        location,
                        "failed to resolve manifest content file path: "
                        + exception.GetType().Name
                        + ": "
                        + exception.Message);
                }

                pathIndex++;
            }
        }
    }
}
