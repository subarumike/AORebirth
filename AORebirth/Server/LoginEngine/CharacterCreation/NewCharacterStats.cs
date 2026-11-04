namespace LoginEngine.CharacterCreation
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Text.Json;
    using System.Text.Json.Serialization;

    using AORebirth.Core.GameData;

    using SmokeLounge.AOtomation.Messaging.GameData;

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

        static readonly object Gate = new object();
        static NewCharacterStats current;

        readonly Dictionary<int, BreedAbilities> _breeds;

        NewCharacterStats(IReadOnlyList<StatValue> stats, Dictionary<int, BreedAbilities> breeds, NewCharacterStartLocation startLocation)
        {
            Stats = stats;
            _breeds = breeds;
            StartLocation = startLocation;
        }

        /// <summary>Cache successful loads only; a corrected configuration can be retried after a failed load.</summary>
        public static NewCharacterStats Current
        {
            get
            {
                lock (Gate)
                    return current ?? (current = Load(Path.Combine(GameDataPaths.ResolveRuntimeRoot(), GameDataPaths.NewCharacterFileName)));
            }
        }

        /// <summary>Stat id and value pairs written for every new character.</summary>
        public IReadOnlyList<StatValue> Stats { get; }

        public NewCharacterStartLocation StartLocation { get; }

        public static NewCharacterStats Load(string path)
        {
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
                throw new FileNotFoundException("GameData " + GameDataPaths.NewCharacterFileName + " is required.", path);

            NewCharacterFile file;
            try
            {
                file = JsonSerializer.Deserialize<NewCharacterFile>(File.ReadAllText(path), JsonOptions);
            }
            catch (JsonException exception)
            {
                throw new InvalidDataException(path + ": invalid creation JSON. " + exception.Message, exception);
            }
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
                if (breed.Strength <= 0 || breed.Agility <= 0 || breed.Stamina <= 0
                    || breed.Intelligence <= 0 || breed.Sense <= 0 || breed.Psychic <= 0)
                    throw new InvalidDataException(path + ": breed " + breed.Breed + " needs all six positive starting abilities.");
                if (breed.Gender is int gender && (gender <= 0 || !Enum.IsDefined(typeof(Gender), gender)))
                    throw new InvalidDataException(path + ": breed " + breed.Breed + " has an invalid Gender " + gender + ".");
            }

            if (breeds.Count == 0)
                throw new InvalidDataException(path + ": Breeds must not be empty.");

            var statIds = new HashSet<int>();
            bool hasLevel = false;
            foreach (StatValue stat in file.Stats)
            {
                if (stat == null || stat.Stat < 0 || !statIds.Add(stat.Stat))
                    throw new InvalidDataException(path + ": each Stats entry needs a unique nonnegative Stat id.");
                if (stat.Stat == (int)CharacterStat.Level)
                {
                    if (stat.Value <= 0)
                        throw new InvalidDataException(path + ": starting Level (stat 54) must be positive.");
                    hasLevel = true;
                }
            }

            if (!hasLevel || !statIds.Contains((int)CharacterStat.NanoPool) || !statIds.Contains((int)CharacterStat.BodyDevelopment))
                throw new InvalidDataException(path + ": Stats must include Level (54), NanoPool (132), and BodyDevelopment (152).");

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
        [JsonRequired]
        public int Stat { get; set; }

        [JsonRequired]
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

        /// <summary>When set, every character of this breed gets this gender (1 Uni, 2 Male, 3 Female) whatever the client sent.</summary>
        public int? Gender { get; set; }

        /// <summary>The order <see cref="StarterVitalStats"/> takes: Str, Psy, Sen, Int, Sta, Agi.</summary>
        public int[] ToLegacyOrder() => new[] { Strength, Psychic, Sense, Intelligence, Stamina, Agility };
    }
}
