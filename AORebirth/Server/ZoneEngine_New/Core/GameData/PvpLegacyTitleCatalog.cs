namespace ZoneEngine_New.Core.GameData
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Text.Json;

    using AORebirth.Core.GameData;

    using ZoneEngine_New.Core.Logging;

    /// <summary>
    /// The Legacy PvP title for a PvP_Rating (stat 333): PvpLegacyTitles.json's bands, each a minimum rating and a
    /// Text.json id in the file's category. A band without an id, or whose id has no text, uses its Fallback text
    /// (empty when absent).
    /// Ratings below the first band have no title. Loaded once at startup; a bad file logs and leaves no titles.
    /// </summary>
    public sealed class PvpLegacyTitleCatalog
    {
        public const string FileName = "PvpLegacyTitles.json";

        static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNameCaseInsensitive = true,
            ReadCommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true
        };

        readonly List<Band> _bands = [];

        public PvpLegacyTitleCatalog(IGameData gameData, IZoneLogger logger)
        {
            ArgumentNullException.ThrowIfNull(gameData);
            ArgumentNullException.ThrowIfNull(logger);
            try
            {
                Load(gameData.RootPath, logger);
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException
                or InvalidDataException or NotSupportedException)
            {
                _bands.Clear();
                logger.Warn("PvpLegacyTitles: " + exception.Message + " Legacy titles stay empty.");
            }
            logger.Info("PvpLegacyTitles bands=" + _bands.Count);
        }

        public int Count => _bands.Count;

        /// <summary>The Legacy title for <paramref name="rating"/>; empty below the first band.</summary>
        public string TitleFor(int rating)
        {
            string title = string.Empty;
            foreach (Band band in _bands)
            {
                if (rating < band.MinRating)
                    break;
                title = band.Name;
            }
            return title;
        }

        void Load(string root, IZoneLogger logger)
        {
            string path = GameDataPaths.ResolveFile(root, FileName);
            if (!File.Exists(path))
            {
                logger.Warn("PvpLegacyTitles: " + FileName + " not found. Legacy titles stay empty.");
                return;
            }

            BandFile? file = JsonSerializer.Deserialize<BandFile>(File.ReadAllText(path), JsonOptions);
            if (file?.TextCategory is not int category || file.Bands is null)
                throw new InvalidDataException(FileName + " needs TextCategory and Bands.");

            Dictionary<int, string> names = ReadTextNames(root, category);
            var bands = new List<Band>(file.Bands.Count);
            foreach (BandRow row in file.Bands)
            {
                if (row.MinRating is not int minRating || minRating < 0)
                    throw new InvalidDataException(FileName + ": every band needs a MinRating of 0 or more.");
                string name = row.TextId is int textId && names.TryGetValue(textId, out string? text) ? text : string.Empty;
                if (name.Length == 0)
                    name = row.Fallback ?? string.Empty;
                if (row.TextId is int id && name.Length == 0)
                    logger.Warn("PvpLegacyTitles: Text.json category " + category + " id " + id + " has no text.");
                bands.Add(new Band(minRating, name));
            }

            if (bands.Select(band => band.MinRating).Distinct().Count() != bands.Count)
                throw new InvalidDataException(FileName + ": two bands share a MinRating.");

            _bands.Clear();
            _bands.AddRange(bands.OrderBy(band => band.MinRating));
        }

        static Dictionary<int, string> ReadTextNames(string root, int category)
        {
            var names = new Dictionary<int, string>();
            string path = GameDataPaths.ResolveFile(root, GameDataPaths.TextFileName);
            if (!File.Exists(path))
                return names;

            using JsonDocument document = JsonDocument.Parse(File.ReadAllText(path));
            foreach (JsonElement entry in document.RootElement.EnumerateArray())
            {
                if (entry.TryGetProperty("category", out JsonElement entryCategory) && entryCategory.GetInt32() == category
                    && entry.TryGetProperty("id", out JsonElement id) && entry.TryGetProperty("text", out JsonElement text)
                    && !string.IsNullOrEmpty(text.GetString()))
                    names.TryAdd(id.GetInt32(), text.GetString()!);
            }
            return names;
        }

        readonly record struct Band(int MinRating, string Name);

        sealed class BandFile
        {
            public int? TextCategory { get; set; }

            public List<BandRow>? Bands { get; set; }
        }

        sealed class BandRow
        {
            public int? MinRating { get; set; }

            public int? TextId { get; set; }

            public string? Fallback { get; set; }

            public string? Note { get; set; }
        }
    }
}
