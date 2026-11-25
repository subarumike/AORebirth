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

        /// <summary>
        /// The mindcontrolled-character slot (charmed NPCs). A charm nano's TestNumPets on it shows as "Must have no
        /// other mindcontrolled character" (7001) or "no more than 1 of pet type: mindcontrolled character" (7002).
        /// </summary>
        public const int MindControlledSlot = 7;

        /// <summary>The tower slot (service towers and other pets of type 5000-5999).</summary>
        public const int TowerSlot = 5;

        /// <summary>A tower stays where it was put: it does not zone with its owner.</summary>
        public static bool StaysBehind(int petType) => Slot(petType) == TowerSlot;

        /// <summary>
        /// The pet type a charmed NPC takes: the mindcontrolled place its charm nano lets it fill, one under the
        /// TestNumPets value that allowed the cast (7001: the first, 7000; 7002: the second, 7001). Each charm nano
        /// thus has its own place, so a second charm does not replace the first. A nano without that test takes the
        /// first place.
        /// </summary>
        public static int CharmType(IReadOnlyList<Inventory.ItemRequirement> requirements)
        {
            ArgumentNullException.ThrowIfNull(requirements);
            foreach (Inventory.ItemRequirement row in requirements)
            {
                if (row.Operator == (int)AORebirth.Enums.Operator.TestNumPets
                    && Slot(Math.Abs(row.Value)) == MindControlledSlot && MaxOwned(Math.Abs(row.Value)) > 0)
                    return Math.Abs(row.Value) - 1;
            }

            return MindControlledSlot * 1000;
        }

        /// <summary>The slot of a pet type or TestNumPets value.</summary>
        public static int Slot(int value) => Math.Max(0, value) / 1000;

        /// <summary>How many pets of the value's slot may be owned.</summary>
        public static int MaxOwned(int value) => Math.Max(0, value) % 1000;

        /// <summary>
        /// The Pets stat (251) holds a 4-bit count of owned pets per slot: slot n in bits 4n..4n+3 (Gamecode.dll
        /// 0x10007cbb reads <c>(Pets &gt;&gt; ((value / 1000 &amp; 7) * 4)) &amp; 0xF</c>). This is what one pet of
        /// <paramref name="petType"/> adds.
        /// </summary>
        public static int CountUnit(int petType) => 1 << ((Slot(petType) & 7) * 4);

        /// <summary>How many pets of <paramref name="value"/>'s slot the Pets stat records.</summary>
        public static int OwnedInSlot(int petsStat, int value) => (petsStat >> ((Slot(value) & 7) * 4)) & 0xF;

        /// <summary>
        /// Operator TestNumPets on the Pets stat, as the client runs it: passes while the value's slot holds fewer
        /// pets than the value allows; a negative value inverts the test.
        /// </summary>
        public static bool TestNumPets(int petsStat, int value)
        {
            bool room = OwnedInSlot(petsStat, Math.Abs(value)) < MaxOwned(Math.Abs(value));
            return value >= 0 ? room : !room;
        }
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
    /// GameData/PetTypes.json: the slot from each summon's TestNumPets requirement, plus the support pets with
    /// mezz behavior. Loaded once per GameData root.
    /// </summary>
    public sealed class PetTypeCatalog
    {
        public const string FileName = "PetTypes.json";

        static readonly ConcurrentDictionary<string, PetTypeCatalog> ByRoot = new(StringComparer.OrdinalIgnoreCase);

        readonly Dictionary<string, int> _types;
        readonly HashSet<string>? _mezzPets;

        PetTypeCatalog(int defaultType, Dictionary<string, int> types, HashSet<string>? mezzPets)
        {
            DefaultType = defaultType;
            _types = types;
            _mezzPets = mezzPets;
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

        /// <summary>Older catalogs without role metadata keep their original Support behavior.</summary>
        internal bool IsMezzPet(string hash, int type)
            => PetTypes.Slot(type) == PetTypes.Slot(PetTypes.Support) && (_mezzPets?.Contains(hash.Trim()) ?? true);

        sealed class CatalogFile
        {
            public int DefaultType { get; set; } = PetTypes.Attack;

            public Dictionary<string, int> Types { get; set; } = new();

            public string[]? MezzPets { get; set; }
        }

        static PetTypeCatalog Load(string root)
        {
            var types = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            string path = Path.Combine(root, FileName);
            if (!File.Exists(path))
                return new PetTypeCatalog(PetTypes.Attack, types, null);

            CatalogFile? file = JsonSerializer.Deserialize<CatalogFile>(File.ReadAllText(path),
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            foreach (KeyValuePair<string, int> entry in file?.Types ?? new())
            {
                if (!string.IsNullOrWhiteSpace(entry.Key) && entry.Value > 0)
                    types[entry.Key.Trim()] = entry.Value;
            }

            HashSet<string>? mezzPets = null;
            if (file?.MezzPets is { } listedMezzPets)
            {
                mezzPets = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (string? entry in listedMezzPets)
                {
                    string hash = entry?.Trim() ?? string.Empty;
                    if (!types.TryGetValue(hash, out int type) || PetTypes.Slot(type) != PetTypes.Slot(PetTypes.Support))
                        throw new InvalidDataException($"{FileName}: mezz pet '{hash}' must name a listed support pet.");
                    if (!mezzPets.Add(hash))
                        throw new InvalidDataException($"{FileName}: duplicate mezz pet '{hash}'.");
                }
            }

            int fallback = file?.DefaultType > 0 ? file.DefaultType : PetTypes.Attack;
            return new PetTypeCatalog(fallback, types, mezzPets);
        }
    }
}
