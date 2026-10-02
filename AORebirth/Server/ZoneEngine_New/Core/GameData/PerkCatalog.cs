namespace ZoneEngine_New.Core.GameData
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Text.Json;

    using AORebirth.Core.GameData;

    using Utility;

    /// <summary>
    /// Perk definitions from Perks.json (the client's perk records): perk id (TrainPerk / HasPerk id)
    /// to the perk item template that holds its stats and actions, the line's previous tier, and flags.
    /// </summary>
    public sealed class PerkCatalog
    {
        /// <summary>Flag 0x01: special perk; the client never trains one through TrainPerk (Gamecode.dll 0x100536d7).</summary>
        public const int SpecialFlag = 0x01;

        /// <summary>Flag 0x02: alien perk, paid with alien perk points.</summary>
        public const int AlienFlag = 0x02;

        /// <summary>Flags 0x40/0x80: research; costs no perk points.</summary>
        public const int ResearchFlags = 0xC0;

        static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

        static readonly Lazy<PerkCatalog> DefaultCatalog = new(() =>
            LoadOrEmpty(GameDataPaths.ResolveRuntimeRoot()));

        readonly Dictionary<int, PerkEntry> _perks;

        PerkCatalog(Dictionary<int, PerkEntry> perks) => _perks = perks;

        readonly record struct PerkEntry(int ItemId, int PreviousId, int Flags);

        public static PerkCatalog Empty { get; } = new(new Dictionary<int, PerkEntry>());

        /// <summary>Catalog from the runtime GameData folder. Empty, with an error logged, when it cannot be loaded.</summary>
        public static PerkCatalog Default => DefaultCatalog.Value;

        public int Count => _perks.Count;

        public bool TryGetItemId(int perkId, out int itemId)
        {
            bool found = _perks.TryGetValue(perkId, out PerkEntry perk);
            itemId = perk.ItemId;
            return found;
        }

        /// <summary>The previous tier of <paramref name="perkId"/>'s line, which must be trained first; false for tier 1.</summary>
        public bool TryGetPreviousId(int perkId, out int previousId)
        {
            previousId = _perks.TryGetValue(perkId, out PerkEntry perk) ? perk.PreviousId : 0;
            return previousId != 0;
        }

        public int GetFlags(int perkId) => _perks.TryGetValue(perkId, out PerkEntry perk) ? perk.Flags : 0;

        /// <summary>
        /// Only the Perk and Alien categories are trained through TrainPerk: research (0x40/0x80) and special
        /// (0x01, never alien) perks are not. Categories as in Perks.json: research, then alien, then special.
        /// </summary>
        public bool IsTrainable(int perkId)
        {
            if (!_perks.TryGetValue(perkId, out PerkEntry perk) || (perk.Flags & ResearchFlags) != 0)
                return false;
            return (perk.Flags & AlienFlag) != 0 || (perk.Flags & SpecialFlag) == 0;
        }

        /// <summary>Paid with regular perk points: neither alien nor research (client mask 0xC2).</summary>
        public bool CostsPerkPoint(int perkId) => (GetFlags(perkId) & (AlienFlag | ResearchFlags)) == 0;

        public bool CostsAlienPerkPoint(int perkId) => (GetFlags(perkId) & AlienFlag) != 0;

        /// <summary>
        /// Perk points a character of <paramref name="level"/> has earned: one per 10 levels below 200,
        /// then one per level (Gamecode.dll 0x10052f20: level &lt; 200 ? level / 10 : level - 180).
        /// </summary>
        public static int EarnedPerkPoints(int level) => level < 200 ? Math.Max(0, level) / 10 : level - 180;

        /// <summary>Alien perk points earned: one per alien level (Gamecode.dll 0x10052fc7).</summary>
        public static int EarnedAlienPerkPoints(int alienLevel) => Math.Max(0, alienLevel);

        public static PerkCatalog Load(string root)
        {
            string path = Path.Combine(root, GameDataPaths.PerksFileName);
            PerksFile file = JsonSerializer.Deserialize<PerksFile>(File.ReadAllText(path), JsonOptions)
                ?? throw Invalid("empty file");

            var perks = new Dictionary<int, PerkEntry>();
            foreach (PerkLineRow? line in file.PerkLines ?? [])
            {
                int previous = 0;
                foreach (PerkRow? perk in (line?.Perks ?? []).OrderBy(p => p?.Tier ?? 0))
                {
                    if (perk == null || perk.Id <= 0 || perk.ItemId <= 0
                        || !perks.TryAdd(perk.Id, new PerkEntry(perk.ItemId, previous, perk.Flags ?? line!.Flags)))
                        throw Invalid("perk " + perk?.Id);
                    previous = perk.Id;
                }
            }

            return new PerkCatalog(perks);
        }

        static PerkCatalog LoadOrEmpty(string root)
        {
            try
            {
                return Load(root);
            }
            catch (Exception exception) when (exception is IOException or JsonException or InvalidDataException or UnauthorizedAccessException)
            {
                LogUtil.ErrorException(exception, "Perk catalog not loaded from {0}; trained perks grant nothing", root);
                return Empty;
            }
        }

        static InvalidDataException Invalid(string detail)
            => new(GameDataPaths.PerksFileName + ": invalid " + detail);

        sealed class PerksFile
        {
            public PerkLineRow?[]? PerkLines { get; set; }
        }

        sealed class PerkLineRow
        {
            public int Flags { get; set; }

            public PerkRow?[]? Perks { get; set; }
        }

        sealed class PerkRow
        {
            public int Tier { get; set; }

            public int Id { get; set; }

            public int ItemId { get; set; }

            /// <summary>Only present where a tier's flags differ from its line's.</summary>
            public int? Flags { get; set; }
        }
    }
}
