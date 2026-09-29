namespace ZoneEngine_New.Core.Pets
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.IO;
    using System.Text.Json;

    /// <summary>
    /// Pet slots. A pet type (and a summon item's TestNumPets requirement on the Pets stat 251) is
    /// slot * 1000 + how many of that slot may be owned: Attack 1, Heal 1001, Support 2001, and the rarer
    /// unicorn 3001, social 4001, tower 5001 and charm 7001/7002.
    /// </summary>
    public static class PetTypes
    {
        public const int Attack = 1;

        public const int Heal = 1001;

        public const int Support = 2001;

        /// <summary>The slot of a pet type or TestNumPets value.</summary>
        public static int Slot(int value) => Math.Max(0, value) / 1000;

        /// <summary>How many pets of the value's slot may be owned.</summary>
        public static int MaxOwned(int value) => Math.Max(0, value) % 1000;

        /// <summary>The Pets stat (251) bit that records a pet of this type's slot is owned.</summary>
        public static int Flag(int petType)
        {
            int slot = Slot(petType);
            return slot < 31 ? 1 << slot : 0;
        }

        /// <summary>
        /// Operator TestNumPets on the Pets stat: passes while the value's slot has room. The stat records only
        /// whether a slot is taken, and players own one pet per slot, so an owned slot is full.
        /// </summary>
        public static bool TestNumPets(int petsStat, int value)
            => MaxOwned(value) > 0 && (petsStat & Flag(value)) == 0;
    }

    /// <summary>PetCommand codes (Gamecode.dll 0x1005408d builds them from the /pet words).</summary>
    public enum PetCommandCode
    {
        Follow = 1,
        Behind = 2,
        Survive = 3,
        Wait = 4,
        Cycle = 5,
        Guard = 6,
        Attack = 7,
        Social = 9,
        Terminate = 10,
        Free = 11,
        Heal = 12,
        Report = 14,
        Rename = 15,
        Chat = 16,
        Script = 17
    }

    /// <summary>What a pet is doing when it is not told otherwise.</summary>
    public enum PetMode
    {
        /// <summary>Default for player pets: follows its owner and fights whatever fights its owner or itself.</summary>
        Guard,

        /// <summary>Follows its owner; only defends itself.</summary>
        Follow,

        /// <summary>Stays where it was told to wait and does nothing.</summary>
        Wait,

        /// <summary>Fights the target it was sent at, then goes back to what it was doing before.</summary>
        Attack,

        /// <summary>An NPC's pet: fights what its owner fights and stays with it.</summary>
        Assist
    }

    /// <summary>
    /// GameData/PetTypes.json: the pet type of each summoned mob hash, taken from the TestNumPets requirement of
    /// the item that summons it. Loaded once per GameData root.
    /// </summary>
    public sealed class PetTypeCatalog
    {
        public const string FileName = "PetTypes.json";

        static readonly ConcurrentDictionary<string, PetTypeCatalog> ByRoot = new(StringComparer.OrdinalIgnoreCase);

        readonly Dictionary<string, int> _types;

        PetTypeCatalog(int defaultType, Dictionary<string, int> types)
        {
            DefaultType = defaultType;
            _types = types;
        }

        public int DefaultType { get; }

        public int Count => _types.Count;

        public static PetTypeCatalog For(string gameDataRoot)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(gameDataRoot);
            return ByRoot.GetOrAdd(gameDataRoot, Load);
        }

        /// <summary>The pet type of <paramref name="hash"/>, or <see cref="DefaultType"/> when it is not listed.</summary>
        public int TypeOf(string hash)
            => !string.IsNullOrWhiteSpace(hash) && _types.TryGetValue(hash.Trim(), out int type) ? type : DefaultType;

        sealed class CatalogFile
        {
            public int DefaultType { get; set; } = PetTypes.Attack;

            public Dictionary<string, int> Types { get; set; } = new();
        }

        static PetTypeCatalog Load(string root)
        {
            var types = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            string path = Path.Combine(root, FileName);
            if (!File.Exists(path))
                return new PetTypeCatalog(PetTypes.Attack, types);

            CatalogFile? file = JsonSerializer.Deserialize<CatalogFile>(File.ReadAllText(path),
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            foreach (KeyValuePair<string, int> entry in file?.Types ?? new())
            {
                if (!string.IsNullOrWhiteSpace(entry.Key) && entry.Value > 0)
                    types[entry.Key.Trim()] = entry.Value;
            }

            int fallback = file?.DefaultType > 0 ? file.DefaultType : PetTypes.Attack;
            return new PetTypeCatalog(fallback, types);
        }
    }
}
