namespace LoginEngine.CharacterCreation
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Text.Json;
    using System.Text.Json.Serialization;

    using AORebirth.Core.GameData;

    /// <summary>
    /// GameData/NewCharacter.json: the location and stats every new character starts with, and each breed's starting
    /// abilities. Stats taken from the creation choices (sex, head mesh, breed, profession, fatness, scale) or the
    /// account (GM level, expansions) are not in the file.
    /// </summary>
    internal sealed class NewCharacterStats
    {
        static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions
        {
            ReadCommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true
        };

        static readonly Lazy<NewCharacterStats> LazyCurrent = new Lazy<NewCharacterStats>(
            () => Load(Path.Combine(GameDataPaths.ResolveRuntimeRoot(), GameDataPaths.NewCharacterFileName)));

        readonly Dictionary<int, BreedAbilities> _breeds;

        NewCharacterStats(IReadOnlyList<StatValue> stats, Dictionary<int, BreedAbilities> breeds, NewCharacterStartLocation startLocation)
        {
            Stats = stats;
            _breeds = breeds;
            StartLocation = startLocation;
        }

        /// <summary>Loaded from the runtime GameData tree on first use.</summary>
        public static NewCharacterStats Current => LazyCurrent.Value;

        /// <summary>Stat id and value pairs written for every new character.</summary>
        public IReadOnlyList<StatValue> Stats { get; }

        public NewCharacterStartLocation StartLocation { get; }

        public static NewCharacterStats Load(string path)
        {
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
                throw new FileNotFoundException("GameData " + GameDataPaths.NewCharacterFileName + " is required.", path);

            NewCharacterFile file = JsonSerializer.Deserialize<NewCharacterFile>(File.ReadAllText(path), JsonOptions);
            if (file?.Stats == null || file.Breeds == null)
                throw new InvalidDataException(path + " needs Stats and Breeds.");

            if (file.StartLocation == null || file.StartLocation.Playfield <= 0
                || float.IsNaN(file.StartLocation.X) || float.IsInfinity(file.StartLocation.X)
                || float.IsNaN(file.StartLocation.Y) || float.IsInfinity(file.StartLocation.Y)
                || float.IsNaN(file.StartLocation.Z) || float.IsInfinity(file.StartLocation.Z))
                throw new InvalidDataException(path + ": StartLocation needs a positive Playfield and finite X, Y, Z.");

            var breeds = new Dictionary<int, BreedAbilities>();
            foreach (BreedAbilities breed in file.Breeds)
            {
                if (breed == null || breed.Breed <= 0 || !breeds.TryAdd(breed.Breed, breed))
                    throw new InvalidDataException(path + ": each Breeds entry needs a unique Breed id.");
            }

            foreach (StatValue stat in file.Stats)
            {
                if (stat == null || stat.Stat < 0)
                    throw new InvalidDataException(path + ": each Stats entry needs a Stat id.");
            }

            return new NewCharacterStats(file.Stats, breeds, file.StartLocation);
        }

        /// <summary>Starting abilities for a breed, or null when the file has none for it.</summary>
        public BreedAbilities AbilitiesFor(int breed) => _breeds.TryGetValue(breed, out BreedAbilities abilities) ? abilities : null;

        sealed class NewCharacterFile
        {
            public List<StatValue> Stats { get; set; }

            public List<BreedAbilities> Breeds { get; set; }

            public NewCharacterStartLocation StartLocation { get; set; }
        }
    }

    internal sealed class NewCharacterStartLocation
    {
        [JsonRequired]
        public int Playfield { get; set; }

        [JsonRequired]
        public float X { get; set; }

        [JsonRequired]
        public float Y { get; set; }

        [JsonRequired]
        public float Z { get; set; }
    }

    internal sealed class StatValue
    {
        public int Stat { get; set; }

        public int Value { get; set; }
    }

    internal sealed class BreedAbilities
    {
        public int Breed { get; set; }

        public int Strength { get; set; }

        public int Agility { get; set; }

        public int Stamina { get; set; }

        public int Intelligence { get; set; }

        public int Sense { get; set; }

        public int Psychic { get; set; }

        /// <summary>The order <see cref="StarterVitalStats"/> takes: Str, Psy, Sen, Int, Sta, Agi.</summary>
        public int[] ToLegacyOrder() => new[] { Strength, Psychic, Sense, Intelligence, Stamina, Agility };
    }
}
