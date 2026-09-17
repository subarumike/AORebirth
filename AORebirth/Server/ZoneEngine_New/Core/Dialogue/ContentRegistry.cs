using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Web.Script.Serialization;

namespace ZoneEngine.Core.Arete.Dialogue
{
    /// <summary>Reads operator-authored dialogue packs and validates their graph before publication.</summary>
    public sealed class DialogueContentPackLoader
    {
        public AreteContentLoadResult<DialogueContentPack> LoadFiles(IEnumerable<string> filePaths)
        {
            var packs = new List<DialogueContentPack>();
            var errors = new AreteValidationResult();
            var json = new JavaScriptSerializer { MaxJsonLength = int.MaxValue };
            foreach (var entry in (filePaths ?? Enumerable.Empty<string>()).Select((path, index) => new { path, index }))
            {
                string location = string.IsNullOrWhiteSpace(entry.path) ? "dialogueFile[" + entry.index + "]" : entry.path;
                try
                {
                    if (string.IsNullOrWhiteSpace(entry.path))
                        throw new ArgumentException("missing JSON content file path");
                    if (!File.Exists(entry.path))
                        throw new FileNotFoundException("JSON content file was not found");
                    DialogueContentPack pack = json.Deserialize<DialogueContentPack>(File.ReadAllText(entry.path));
                    if (pack == null)
                        throw new InvalidDataException("JSON content file did not contain a content pack");
                    packs.Add(pack);
                }
                catch (Exception error)
                {
                    errors.AddError(location, "failed to read dialogue content: " + error.Message);
                }
            }
            errors.AddErrors(DialogueContentPackValidator.Validate(packs));
            return new AreteContentLoadResult<DialogueContentPack>(packs, errors);
        }

        public AreteContentLoadResult<DialogueContentPack> LoadFile(string filePath) => LoadFiles(new[] { filePath });

        public AreteContentLoadResult<DialogueContentPack> Load(IEnumerable<DialogueContentPack> packs)
        {
            var snapshot = (packs ?? Enumerable.Empty<DialogueContentPack>()).ToArray();
            return new AreteContentLoadResult<DialogueContentPack>(snapshot, DialogueContentPackValidator.Validate(snapshot));
        }

        public AreteContentLoadResult<DialogueContentPack> LoadEmpty() => Load(Enumerable.Empty<DialogueContentPack>());

        public AreteContentLoadResult<DialogueContentPack> LoadDirectory(string directoryPath)
        {
            if (!string.IsNullOrWhiteSpace(directoryPath) && Directory.Exists(directoryPath))
            {
                string[] paths = Directory.GetFiles(directoryPath, "*.json", SearchOption.TopDirectoryOnly);
                if (paths.Length != 0)
                    return LoadFiles(paths.OrderBy(path => path, StringComparer.OrdinalIgnoreCase));
            }
            var errors = new AreteValidationResult();
            errors.AddError(directoryPath, "A dialogue directory containing JSON content files is required.");
            return new AreteContentLoadResult<DialogueContentPack>(null, errors);
        }

        public AreteContentLoadResult<DialogueContentPack> LoadManifest(string manifestPath)
        {
            var manifest = new AreteContentManifestLoader().Load(manifestPath);
            return manifest.IsValid
                ? LoadFiles(manifest.DialoguePackFiles)
                : new AreteContentLoadResult<DialogueContentPack>(null, manifest.Validation);
        }
    }

    /// <summary>A failed reload leaves the previously published dialogue index intact.</summary>
    public sealed class DialogueContentRegistry
    {
        Dictionary<string, DialogueNpcEntry> _npcs = new Dictionary<string, DialogueNpcEntry>(StringComparer.OrdinalIgnoreCase);
        public int PackCount { get; private set; }
        public int NpcCount => _npcs.Count;

        public AreteValidationResult Load(IEnumerable<DialogueContentPack> packs)
            => Publish(new DialogueContentPackLoader().Load(packs));
        public AreteValidationResult LoadFromFiles(IEnumerable<string> paths)
            => Publish(new DialogueContentPackLoader().LoadFiles(paths));
        public AreteValidationResult LoadFromDirectory(string path)
            => Publish(new DialogueContentPackLoader().LoadDirectory(path));
        public AreteValidationResult LoadFromManifest(string path)
            => Publish(new DialogueContentPackLoader().LoadManifest(path));

        AreteValidationResult Publish(AreteContentLoadResult<DialogueContentPack> candidate)
        {
            if (candidate.IsValid)
            {
                var index = candidate.Packs.SelectMany(pack => pack.Npcs ?? Enumerable.Empty<DialogueNpcEntry>())
                    .ToDictionary(npc => npc.NpcIdentity, StringComparer.OrdinalIgnoreCase);
                _npcs = index;
                PackCount = candidate.Packs.Count;
            }
            return candidate.Validation;
        }

        public bool TryGetNpc(string identity, out DialogueNpcEntry npc)
        {
            npc = null;
            return !string.IsNullOrWhiteSpace(identity) && _npcs.TryGetValue(identity, out npc);
        }
    }
}
