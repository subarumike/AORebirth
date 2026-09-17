using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Web.Script.Serialization;

namespace ZoneEngine_New.Core.Missions.Content
{
    /// <summary>Diagnostics produced while reading editable interaction content.</summary>
    public sealed class ContentValidationResult
    {
        readonly List<string> messages = new List<string>();
        public IEnumerable<string> Errors => messages.AsReadOnly();
        public bool IsValid => messages.Count == 0;
        public int ErrorCount => messages.Count;
        public void AddError(string source, string message) => messages.Add((source ?? "content") + ": " + message);
        public void AddErrors(ContentValidationResult other)
        {
            if (other != null) messages.AddRange(other.Errors);
        }
    }

    public sealed class ContentReadResult<T>
    {
        public ContentReadResult(IEnumerable<T> values, ContentValidationResult validation)
        {
            Packs = Array.AsReadOnly((values ?? Enumerable.Empty<T>()).ToArray());
            Validation = validation ?? new ContentValidationResult();
        }
        public IList<T> Packs { get; }
        public ContentValidationResult Validation { get; }
        public bool IsValid => Validation.IsValid;
    }

    /// <summary>The JSON manifest schema is shared by dialogue and quest content.</summary>
    public sealed class InteractionManifest
    {
        public IList<string> DialoguePacks { get; set; } = new List<string>();
        public IList<string> QuestPacks { get; set; } = new List<string>();

        public static InteractionManifest Read(string path)
        {
            var manifest = ContentJson.Read<InteractionManifest>(path);
            string directory = Path.GetDirectoryName(Path.GetFullPath(path));
            manifest.DialoguePacks = Resolve(manifest.DialoguePacks, directory);
            manifest.QuestPacks = Resolve(manifest.QuestPacks, directory);
            return manifest;
        }

        static IList<string> Resolve(IEnumerable<string> entries, string directory)
            => (entries ?? Enumerable.Empty<string>()).Select(entry => {
                if (string.IsNullOrWhiteSpace(entry)) throw new InvalidDataException("Manifest contains an empty content path.");
                return Path.GetFullPath(Path.IsPathRooted(entry) ? entry : Path.Combine(directory, entry));
            }).ToArray();
    }

    internal static class ContentJson
    {
        // The same serializer is used by the native dialogue reader and its .NET Framework source-linked tests.
        internal static T Read<T>(string path) where T : class
        {
            if (string.IsNullOrWhiteSpace(path)) throw new InvalidDataException("A content file path is required.");
            var result = new JavaScriptSerializer { MaxJsonLength = int.MaxValue }.Deserialize<T>(File.ReadAllText(path));
            return result ?? throw new InvalidDataException("Content file is empty: " + path);
        }
    }
}
