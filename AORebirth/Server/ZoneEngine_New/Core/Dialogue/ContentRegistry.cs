using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Web.Script.Serialization;
using ZoneEngine_New.Core.Missions.Content;

namespace ZoneEngine.Core.Arete.Dialogue
{
    /// <summary>Reads operator-authored dialogue packs and validates their graph before publication.</summary>
    public sealed class DialogueContentPackLoader
    {
        public ContentReadResult<DialogueContentPack> LoadFiles(IEnumerable<string> filePaths)
        {
            var packs = new List<DialogueContentPack>();
            var errors = new ContentValidationResult();
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
            return new ContentReadResult<DialogueContentPack>(packs, errors);
        }

        public ContentReadResult<DialogueContentPack> LoadFile(string filePath) => LoadFiles(new[] { filePath });

        public ContentReadResult<DialogueContentPack> Load(IEnumerable<DialogueContentPack> packs)
        {
            var snapshot = (packs ?? Enumerable.Empty<DialogueContentPack>()).ToArray();
            return new ContentReadResult<DialogueContentPack>(snapshot, DialogueContentPackValidator.Validate(snapshot));
        }

        public ContentReadResult<DialogueContentPack> LoadEmpty() => Load(Enumerable.Empty<DialogueContentPack>());

        public ContentReadResult<DialogueContentPack> LoadDirectory(string directoryPath)
        {
            if (!string.IsNullOrWhiteSpace(directoryPath) && Directory.Exists(directoryPath))
            {
                string[] paths = Directory.GetFiles(directoryPath, "*.json", SearchOption.TopDirectoryOnly);
                if (paths.Length != 0)
                    return LoadFiles(paths.OrderBy(path => path, StringComparer.OrdinalIgnoreCase));
            }
            var errors = new ContentValidationResult();
            errors.AddError(directoryPath, "A dialogue directory containing JSON content files is required.");
            return new ContentReadResult<DialogueContentPack>(null, errors);
        }

        public ContentReadResult<DialogueContentPack> LoadManifest(string manifestPath)
        {
            try { return LoadFiles(InteractionManifest.Read(manifestPath).DialoguePacks); }
            catch (Exception failure)
            {
                var errors = new ContentValidationResult();
                errors.AddError(manifestPath, failure.Message);
                return new ContentReadResult<DialogueContentPack>(null, errors);
            }
        }
    }

    /// <summary>A failed reload leaves the previously published dialogue index intact.</summary>
    public sealed class DialogueContentRegistry
    {
        Dictionary<string, DialogueNpcEntry> _npcs = new Dictionary<string, DialogueNpcEntry>(StringComparer.OrdinalIgnoreCase);
        public int PackCount { get; private set; }
        public int NpcCount => _npcs.Count;

        public ContentValidationResult Load(IEnumerable<DialogueContentPack> packs)
            => Publish(new DialogueContentPackLoader().Load(packs));
        public ContentValidationResult LoadFromFiles(IEnumerable<string> paths)
            => Publish(new DialogueContentPackLoader().LoadFiles(paths));
        public ContentValidationResult LoadFromDirectory(string path)
            => Publish(new DialogueContentPackLoader().LoadDirectory(path));
        public ContentValidationResult LoadFromManifest(string path)
            => Publish(new DialogueContentPackLoader().LoadManifest(path));

        ContentValidationResult Publish(ContentReadResult<DialogueContentPack> candidate)
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
