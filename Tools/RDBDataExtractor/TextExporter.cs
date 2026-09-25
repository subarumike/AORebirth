namespace AORebirth.Tools.RDBDataExtractor
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Text.Encodings.Web;
    using System.Text.Json;

    using AODB;

    using AORebirth.Core.GameData;

    /// <summary>
    /// Exports client localisation text from cd_image/text into GameData/Text.json.
    /// text.mdb wins over ctext.ldb for the same category and id, matching
    /// <see cref="TextDatabase.GetCategory"/>.
    /// </summary>
    internal sealed class TextExporter
    {
        private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions
        {
            WriteIndented = true,
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        };

        private readonly string aoClientPath;
        private readonly string gameDataDirectory;

        internal TextExporter(string aoClientPath, string gameDataDirectory)
        {
            if (string.IsNullOrWhiteSpace(aoClientPath))
                throw new ArgumentException("AO client path is required.", "aoClientPath");
            if (string.IsNullOrWhiteSpace(gameDataDirectory))
                throw new ArgumentException("GameData directory is required.", "gameDataDirectory");

            this.aoClientPath = aoClientPath;
            this.gameDataDirectory = gameDataDirectory;
        }

        /// <summary>
        /// Writes Text.json under the GameData root. Existing files are skipped
        /// unless <paramref name="overwrite"/> is true.
        /// </summary>
        internal ExportFileCounts Export(bool overwrite)
        {
            return this.Write(TextDatabase.Load(this.aoClientPath), overwrite);
        }

        internal ExportFileCounts Write(TextDatabase database, bool overwrite)
        {
            if (database == null)
                throw new ArgumentNullException("database");

            string path = Path.Combine(
                this.gameDataDirectory,
                GameDataPaths.TextFileName);

            if (!overwrite && File.Exists(path))
                return new ExportFileCounts(0, 1);

            List<TextEntry> entries = BuildEntries(database);
            if (entries.Count == 0)
            {
                throw new InvalidOperationException(
                    "Text database contained no entries. Expected cd_image/text/text.mdb or ctext.ldb.");
            }

            Directory.CreateDirectory(this.gameDataDirectory);
            using (FileStream stream = File.Create(path))
            {
                JsonSerializer.Serialize(stream, entries, JsonOptions);
            }

            Console.WriteLine(
                "exported "
                + GameDataPaths.TextFileName
                + " entries="
                + entries.Count);
            return new ExportFileCounts(1, 0);
        }

        private static List<TextEntry> BuildEntries(TextDatabase database)
        {
            if (database == null)
                throw new ArgumentNullException("database");

            var entries = new List<TextEntry>();
            foreach (int category in database.Categories)
            {
                foreach (KeyValuePair<int, string> pair in database.GetCategory(category).OrderBy(pair => pair.Key))
                {
                    entries.Add(
                        new TextEntry
                        {
                            Category = category,
                            Id = pair.Key,
                            Text = pair.Value,
                        });
                }
            }

            return entries;
        }

        private sealed class TextEntry
        {
            public int Category { get; set; }

            public int Id { get; set; }

            public string Text { get; set; }
        }
    }
}
