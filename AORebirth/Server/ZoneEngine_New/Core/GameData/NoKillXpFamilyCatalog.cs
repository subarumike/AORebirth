namespace ZoneEngine_New.Core.GameData
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Text.Json;

    using AORebirth.Core.GameData;
    using AORebirth.Modules.Content;

    using ZoneEngine_New.Core.Configuration;
    using ZoneEngine_New.Core.Logging;

    /// <summary>
    /// The NPCFamily (455) values whose kills give no regular XP at all, not even the grey 1 XP: Xp.json's
    /// <c>"NoKillXpFamilies": [ { "Family": 0, "Note": "Guards and civilians" }, ... ]</c>, beside the level rows. While
    /// Xp.json has no such key the list is Gameplay.json <c>xp.noKillXpNpcFamilies</c>; once it does, it alone decides.
    /// Read through the GameData override folder and swapped whole on reload, so the next kill sees the new list, mobs
    /// already spawned included. A file that fails to read, parse or validate keeps the old list and reports why.
    /// </summary>
    public sealed class NoKillXpFamilyCatalog
    {
        public const string FileName = GameDataPaths.XpFileName;

        /// <summary>The name the catalog reloads under.</summary>
        public const string ReloadName = "NoKillXpFamilies";

        /// <summary>The Xp.json key holding the list.</summary>
        public const string PropertyName = "NoKillXpFamilies";

        static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNameCaseInsensitive = true,
            ReadCommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true
        };

        readonly string _root;
        readonly IReadOnlyCollection<int> _fallback;
        volatile HashSet<int> _families;

        public NoKillXpFamilyCatalog(IGameData gameData, GameplaySettings settings, IZoneLogger logger)
            : this(
                (gameData ?? throw new ArgumentNullException(nameof(gameData))).RootPath,
                (settings ?? throw new ArgumentNullException(nameof(settings))).Xp.NoKillXpNpcFamilies)
        {
            ArgumentNullException.ThrowIfNull(logger);
            foreach (string problem in LoadProblems)
                logger.Warn("NoKillXpFamilies: " + problem);
            logger.Info("NoKillXpFamilies families=" + _families.Count + (FromFile ? " from Xp.json" : " from Gameplay.json"));
        }

        /// <param name="gameDataRoot">The GameData root.</param>
        /// <param name="fallback">Gameplay.json xp.noKillXpNpcFamilies, used while Xp.json has no list.</param>
        public NoKillXpFamilyCatalog(string gameDataRoot, IReadOnlyCollection<int> fallback)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(gameDataRoot);
            _root = gameDataRoot;
            _fallback = fallback ?? throw new ArgumentNullException(nameof(fallback));
            _families = new HashSet<int>(fallback);
            LoadProblems = Reload().Problems;
        }

        /// <summary>What the first load reported; empty when it was clean.</summary>
        public IReadOnlyList<string> LoadProblems { get; }

        /// <summary>True when the current list came from Xp.json, false for the Gameplay.json fallback.</summary>
        public bool FromFile { get; private set; }

        public int Count => _families.Count;

        /// <summary>One set lookup; no allocation.</summary>
        public bool GivesNoKillXp(int npcFamily) => _families.Contains(npcFamily);

        /// <summary>Reads Xp.json again. Off the playfield tick: it reads the file.</summary>
        public GameDataReloadResult Reload()
        {
            HashSet<int>? loaded;
            try
            {
                loaded = Load(GameDataPaths.ResolveFile(_root, FileName));
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException
                or InvalidDataException or NotSupportedException)
            {
                return new GameDataReloadResult(ReloadName, false, _families.Count, [FileName + ": " + exception.Message]);
            }

            FromFile = loaded != null;
            _families = loaded ?? new HashSet<int>(_fallback);
            return new GameDataReloadResult(ReloadName, true, _families.Count, []);
        }

        /// <summary>The file's list; null when the file or its key is absent (Gameplay.json decides).</summary>
        static HashSet<int>? Load(string path)
        {
            if (!File.Exists(path))
                return null;

            using JsonDocument document = JsonDocument.Parse(
                File.ReadAllText(path),
                new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true });
            if (document.RootElement.ValueKind != JsonValueKind.Object)
                throw new InvalidDataException("the file must be an object.");

            JsonElement list = default;
            bool found = false;
            foreach (JsonProperty property in document.RootElement.EnumerateObject())
            {
                if (string.Equals(property.Name, PropertyName, StringComparison.OrdinalIgnoreCase))
                {
                    list = property.Value;
                    found = true;
                    break;
                }
            }

            if (!found)
                return null;
            if (list.ValueKind != JsonValueKind.Array)
                throw new InvalidDataException(PropertyName + " must be a list of { \"Family\": n, \"Note\": \"...\" } (it may be empty).");

            List<FamilyRow>? rows = list.Deserialize<List<FamilyRow>>(JsonOptions);
            var families = new HashSet<int>();
            for (int i = 0; i < (rows?.Count ?? 0); i++)
            {
                FamilyRow? row = rows![i];
                if (row?.Family is not int family || family < 0)
                    throw new InvalidDataException(PropertyName + "[" + i + "]: Family must be an NPCFamily value, 0 or more.");
                families.Add(family);
            }

            return families;
        }

        /// <summary>One Xp.json NoKillXpFamilies entry.</summary>
        public sealed class FamilyRow
        {
            /// <summary>The NPCFamily (stat 455) value.</summary>
            public int? Family { get; set; }

            /// <summary>For the reader: which NPCs the family is.</summary>
            public string? Note { get; set; }
        }
    }

    /// <summary>Xp.json's NoKillXpFamilies list (<see cref="NoKillXpFamilyCatalog"/>).</summary>
    public sealed class NoKillXpFamiliesReload(NoKillXpFamilyCatalog catalog) : IReloadableGameData
    {
        public string Name => NoKillXpFamilyCatalog.ReloadName;

        public IReadOnlyList<string> Files { get; } = [NoKillXpFamilyCatalog.FileName];

        public GameDataReloadResult Reload() => catalog.Reload();
    }
}
