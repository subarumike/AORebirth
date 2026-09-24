namespace ZoneEngine_New.Core.GameData
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Text.Json;

    using AORebirth.Core.GameData;

    using SmokeLounge.AOtomation.Messaging.GameData;

    using Utility;

    using ZoneEngine_New.Core.Entities;

    /// <summary>
    /// Skill trickle, IP training costs and training caps from SkillTrickle.json, SkillCosts.json and
    /// AbilityCosts.json. Trickle factors are held as whole percents and skill multipliers as tenths,
    /// so the floors in trickle, cost and caps are exact integer math.
    /// </summary>
    public sealed class SkillCatalog
    {
        /// <summary>Skill base that costs nothing; the first paid point is the one from 5 to 6.</summary>
        public const int SkillFloor = 5;

        static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

        static readonly Lazy<SkillCatalog> DefaultCatalog = new(() =>
            LoadOrEmpty(GameDataPaths.ResolveRuntimeRoot()));

        readonly CharacterStat[] _trickleAbilities;
        readonly Dictionary<CharacterStat, int[]> _tricklePercents;
        readonly Dictionary<(Profession, CharacterStat), int> _skillCostTenths;
        readonly Dictionary<(Breed, CharacterStat), AbilityRow> _abilityRows;
        readonly SkillCaps _skillCaps;
        readonly AbilityCaps _abilityCaps;
        readonly HashSet<Profession> _professions = new();
        readonly HashSet<Breed> _breeds = new();

        SkillCatalog(
            CharacterStat[] trickleAbilities,
            Dictionary<CharacterStat, int[]> tricklePercents,
            Dictionary<(Profession, CharacterStat), int> skillCostTenths,
            Dictionary<(Breed, CharacterStat), AbilityRow> abilityRows,
            SkillCaps skillCaps,
            AbilityCaps abilityCaps)
        {
            _trickleAbilities = trickleAbilities;
            _tricklePercents = tricklePercents;
            _skillCostTenths = skillCostTenths;
            _abilityRows = abilityRows;
            _skillCaps = skillCaps;
            _abilityCaps = abilityCaps;
            foreach ((Profession profession, _) in skillCostTenths.Keys)
                _professions.Add(profession);
            foreach ((Breed breed, _) in abilityRows.Keys)
                _breeds.Add(breed);
        }

        public static SkillCatalog Empty { get; } = new([], new(), new(), new(), new([], 0, []), new(0, 0));

        /// <summary>Catalog from the runtime GameData folder. Empty, with an error logged, when it cannot be loaded.</summary>
        public static SkillCatalog Default => DefaultCatalog.Value;

        public static bool IsAbility(CharacterStat stat) => stat is >= CharacterStat.Strength and <= CharacterStat.Psychic;

        public static bool IsSkill(CharacterStat stat) => stat is >= CharacterStat.MartialArts and <= CharacterStat.NanoResist;

        public bool Trains(Breed breed, Profession profession) => _breeds.Contains(breed) && _professions.Contains(profession);

        public int Trickle(StatCollection stats, CharacterStat skill)
            => _tricklePercents.TryGetValue(skill, out int[]? percents)
                ? Saturate(Math.Floor(Weighted(percents, stats, null) / 400d))
                : 0;

        /// <summary>
        /// Highest trained base a skill's abilities allow: floor(2 * weighted abilities) over ability finals
        /// with <paramref name="abilityRaises"/> added to them. Past the cap level the client adds the skill's
        /// per-level step on top of this bound as well as the title cap, so both grow together.
        /// </summary>
        public int AbilityCap(
            StatCollection stats,
            Profession profession,
            CharacterStat skill,
            int level,
            IReadOnlyDictionary<CharacterStat, int>? abilityRaises = null)
        {
            if (!_tricklePercents.TryGetValue(skill, out int[]? percents))
                return int.MaxValue;

            long bound = Weighted(percents, stats, abilityRaises) / 50;
            if (bound <= 0)
                return int.MaxValue;
            if (level > _skillCaps.CapLevel && _skillCostTenths.TryGetValue((profession, skill), out int tenths))
                bound += (long)(level - _skillCaps.CapLevel) * Tier(tenths).PerLevelAbove;
            return Saturate(bound);
        }

        /// <summary>
        /// Highest trained base <paramref name="level"/> allows, or 0 when the stat is not trainable.
        /// Title caps hold through the data's cap level; past it every level adds a fixed step.
        /// </summary>
        public int LevelCap(Breed breed, Profession profession, CharacterStat stat, int level)
        {
            level = Math.Max(1, level);
            if (_abilityRows.TryGetValue((breed, stat), out AbilityRow ability))
            {
                return level <= _abilityCaps.CapLevel
                    ? Math.Min(ability.Base + _abilityCaps.PerLevel * level, ability.Cap)
                    : Above(ability.Cap, level - _abilityCaps.CapLevel, ability.PerLevelAbove);
            }

            if (!_skillCostTenths.TryGetValue((profession, stat), out int tenths))
                return 0;

            CapTier tier = Tier(tenths);
            if (level > _skillCaps.CapLevel)
                return Above(tier.TitleCaps[^1], level - _skillCaps.CapLevel, tier.PerLevelAbove);

            int[] starts = _skillCaps.TitleStartLevels;
            int title = Array.FindLastIndex(starts, start => level >= start);
            int previous = title == 0 ? SkillFloor : tier.TitleCaps[title - 1];
            return Math.Min(tier.TitleCaps[title], previous + tier.PerLevel * (level - starts[title] + 1));
        }

        /// <summary>
        /// Adds each skill's trickle to its bonus layer. Run after gear and buff bonuses so the
        /// ability finals it reads are complete; skills the character does not hold are skipped.
        /// </summary>
        public void ApplyTrickle(StatCollection stats)
        {
            foreach (CharacterStat skill in _tricklePercents.Keys)
            {
                if (stats.TryGetValue(skill, out _))
                    stats.AddBonus(skill, Trickle(stats, skill));
            }
        }

        /// <summary>
        /// IP to raise a trained base from <paramref name="from"/> to <paramref name="to"/>. False when the
        /// stat is not trainable for this breed and profession, or <paramref name="to"/> is below its floor.
        /// </summary>
        public bool TryGetRaiseCost(Breed breed, Profession profession, CharacterStat stat, int from, int to, out long cost)
        {
            cost = 0;
            if (_abilityRows.TryGetValue((breed, stat), out AbilityRow ability))
            {
                if (to < ability.Base)
                    return false;
                for (long point = Math.Max(from, ability.Base); point < to; point++)
                    cost += ability.Cost * point;
                return true;
            }

            if (!_skillCostTenths.TryGetValue((profession, stat), out int tenths) || to < SkillFloor)
                return false;
            for (long point = Math.Max(from, SkillFloor); point < to; point++)
                cost += tenths * point / 10;
            return true;
        }

        public static SkillCatalog Load(string root)
        {
            TrickleFile trickle = Read<TrickleFile>(root, GameDataPaths.SkillTrickleFileName);
            SkillCostFile skillCosts = Read<SkillCostFile>(root, GameDataPaths.SkillCostsFileName);
            AbilityCostFile abilityCosts = Read<AbilityCostFile>(root, GameDataPaths.AbilityCostsFileName);

            CharacterStat[] trickleAbilities = Abilities(trickle.Abilities, GameDataPaths.SkillTrickleFileName);
            var tricklePercents = new Dictionary<CharacterStat, int[]>();
            foreach ((string name, double[] factors) in trickle.Skills)
            {
                CharacterStat skill = Skill(name, GameDataPaths.SkillTrickleFileName);
                tricklePercents[skill] = Scaled(factors, trickleAbilities.Length, 100, name);
            }

            var professions = new Profession[skillCosts.Professions.Length];
            for (int i = 0; i < professions.Length; i++)
            {
                professions[i] = Named<Profession>(skillCosts.Professions[i], GameDataPaths.SkillCostsFileName);
                if (professions[i] is Profession.None or Profession.Monster || Array.IndexOf(professions, professions[i]) != i)
                    throw Invalid(GameDataPaths.SkillCostsFileName, "profession " + professions[i]);
            }

            var skillCostTenths = new Dictionary<(Profession, CharacterStat), int>();
            foreach ((string name, double[] multipliers) in skillCosts.Skills)
            {
                CharacterStat skill = Skill(name, GameDataPaths.SkillCostsFileName);
                int[] tenths = Scaled(multipliers, professions.Length, 10, name);
                for (int i = 0; i < professions.Length; i++)
                {
                    if (tenths[i] == 0)
                        throw Invalid(GameDataPaths.SkillCostsFileName, name);
                    skillCostTenths[(professions[i], skill)] = tenths[i];
                }
            }

            SkillCaps skillCaps = LoadSkillCaps(skillCosts.LevelCaps);
            foreach (int tenths in skillCostTenths.Values)
            {
                if (tenths > skillCaps.Tiers[^1].MaxTenths)
                    throw Invalid(GameDataPaths.SkillCostsFileName, "level cap tiers do not cover multiplier " + tenths / 10d);
            }

            CharacterStat[] abilities = Abilities(abilityCosts.Abilities, GameDataPaths.AbilityCostsFileName);
            if (abilityCosts.PerLevel < 1 || abilityCosts.CapLevel < 1)
                throw Invalid(GameDataPaths.AbilityCostsFileName, "level caps");
            var abilityRows = new Dictionary<(Breed, CharacterStat), AbilityRow>();
            foreach ((string name, BreedRow row) in abilityCosts.Breeds)
            {
                Breed breed = Named<Breed>(name, GameDataPaths.AbilityCostsFileName);
                if (breed == Breed.None
                    || row?.Base?.Length != abilities.Length
                    || row.Cost?.Length != abilities.Length
                    || row.Cap?.Length != abilities.Length
                    || row.PerLevelAbove?.Length != abilities.Length)
                    throw Invalid(GameDataPaths.AbilityCostsFileName, name);
                for (int i = 0; i < abilities.Length; i++)
                {
                    if (row.Base[i] < 1 || row.Cost[i] < 1 || row.Cap[i] < row.Base[i] || row.PerLevelAbove[i] < 0)
                        throw Invalid(GameDataPaths.AbilityCostsFileName, name);
                    abilityRows[(breed, abilities[i])] = new AbilityRow(row.Base[i], row.Cost[i], row.Cap[i], row.PerLevelAbove[i]);
                }
            }

            return new SkillCatalog(
                trickleAbilities,
                tricklePercents,
                skillCostTenths,
                abilityRows,
                skillCaps,
                new AbilityCaps(abilityCosts.PerLevel, abilityCosts.CapLevel));
        }

        static SkillCaps LoadSkillCaps(LevelCapFile? file)
        {
            const string fileName = GameDataPaths.SkillCostsFileName;
            int[] starts = file?.TitleStartLevels ?? [];
            if (starts.Length == 0 || starts[0] != 1 || file!.CapLevel < starts[^1] || file.Tiers is not { Length: > 0 })
                throw Invalid(fileName, "level caps");
            for (int i = 1; i < starts.Length; i++)
            {
                if (starts[i] <= starts[i - 1])
                    throw Invalid(fileName, "title start levels");
            }

            var tiers = new CapTier[file.Tiers.Length];
            for (int i = 0; i < tiers.Length; i++)
            {
                TierRow? row = file.Tiers[i];
                if (row?.TitleCaps?.Length != starts.Length || row.PerLevel < 1 || row.PerLevelAbove < 0)
                    throw Invalid(fileName, "level cap tier " + i);
                int maxTenths = Scaled([row.MaxMultiplier], 1, 10, "level cap tier " + i)[0];
                if (i > 0 && maxTenths <= tiers[i - 1].MaxTenths)
                    throw Invalid(fileName, "level cap tier order");
                tiers[i] = new CapTier(maxTenths, row.PerLevel, row.TitleCaps, row.PerLevelAbove);
            }

            return new SkillCaps(starts, file.CapLevel, tiers);
        }

        long Weighted(int[] percents, StatCollection stats, IReadOnlyDictionary<CharacterStat, int>? raises)
        {
            long weighted = 0;
            for (int i = 0; i < percents.Length; i++)
            {
                CharacterStat ability = _trickleAbilities[i];
                int value = stats.GetOrZero(ability) + (raises?.GetValueOrDefault(ability) ?? 0);
                weighted += (long)percents[i] * value;
            }

            return weighted;
        }

        CapTier Tier(int tenths) => Array.Find(_skillCaps.Tiers, candidate => tenths <= candidate.MaxTenths)!;

        static int Above(int cap, int levels, int perLevel)
            => (int)Math.Min(int.MaxValue, cap + (long)levels * perLevel);

        static int Saturate(double value)
            => (int)Math.Clamp(value, int.MinValue, int.MaxValue);

        static SkillCatalog LoadOrEmpty(string root)
        {
            try
            {
                return Load(root);
            }
            catch (Exception exception) when (exception is IOException or JsonException or InvalidDataException or UnauthorizedAccessException)
            {
                LogUtil.ErrorException(exception, "Skill catalog not loaded from {0}; trickle and skill training are disabled", root);
                return Empty;
            }
        }

        static T Read<T>(string root, string fileName)
            where T : class
            => JsonSerializer.Deserialize<T>(File.ReadAllText(Path.Combine(root, fileName)), JsonOptions)
               ?? throw Invalid(fileName, "empty file");

        static CharacterStat[] Abilities(string[] names, string fileName)
        {
            var abilities = new CharacterStat[names.Length];
            for (int i = 0; i < names.Length; i++)
            {
                abilities[i] = Named<CharacterStat>(names[i], fileName);
                if (!IsAbility(abilities[i]) || Array.IndexOf(abilities, abilities[i]) != i)
                    throw Invalid(fileName, names[i]);
            }

            return abilities.Length == 6 ? abilities : throw Invalid(fileName, "ability list");
        }

        static CharacterStat Skill(string name, string fileName)
        {
            CharacterStat skill = Named<CharacterStat>(name, fileName);
            return IsSkill(skill) ? skill : throw Invalid(fileName, name);
        }

        static T Named<T>(string name, string fileName)
            where T : struct, Enum
            => Enum.IsDefined(typeof(T), name ?? string.Empty) ? Enum.Parse<T>(name!) : throw Invalid(fileName, name ?? "null");

        static int[] Scaled(double[] values, int length, int scale, string name)
        {
            if (values?.Length != length)
                throw Invalid(name, "wrong column count");

            var scaled = new int[length];
            for (int i = 0; i < length; i++)
            {
                double value = values[i] * scale;
                scaled[i] = (int)Math.Round(value);
                if (!double.IsFinite(value) || scaled[i] < 0 || Math.Abs(value - scaled[i]) > 1e-6)
                    throw Invalid(name, values[i].ToString(System.Globalization.CultureInfo.InvariantCulture));
            }

            return scaled;
        }

        static InvalidDataException Invalid(string fileName, string detail)
            => new(fileName + ": invalid " + detail);

        sealed class TrickleFile
        {
            public string[] Abilities { get; set; } = [];

            public Dictionary<string, double[]> Skills { get; set; } = new();
        }

        /// <summary>Skill title caps as trained bases, one tier per band of cost multipliers.</summary>
        sealed record SkillCaps(int[] TitleStartLevels, int CapLevel, CapTier[] Tiers);

        sealed record CapTier(int MaxTenths, int PerLevel, int[] TitleCaps, int PerLevelAbove);

        readonly record struct AbilityCaps(int PerLevel, int CapLevel);

        readonly record struct AbilityRow(int Base, int Cost, int Cap, int PerLevelAbove);

        sealed class SkillCostFile
        {
            public LevelCapFile? LevelCaps { get; set; }

            public string[] Professions { get; set; } = [];

            public Dictionary<string, double[]> Skills { get; set; } = new();
        }

        sealed class LevelCapFile
        {
            public int[] TitleStartLevels { get; set; } = [];

            public int CapLevel { get; set; }

            public TierRow[] Tiers { get; set; } = [];
        }

        sealed class TierRow
        {
            public double MaxMultiplier { get; set; }

            public int PerLevel { get; set; }

            public int[] TitleCaps { get; set; } = [];

            public int PerLevelAbove { get; set; }
        }

        sealed class AbilityCostFile
        {
            public string[] Abilities { get; set; } = [];

            public int PerLevel { get; set; }

            public int CapLevel { get; set; }

            public Dictionary<string, BreedRow> Breeds { get; set; } = new();
        }

        sealed class BreedRow
        {
            public int[] Base { get; set; } = [];

            public int[] Cost { get; set; } = [];

            public int[] Cap { get; set; } = [];

            public int[] PerLevelAbove { get; set; } = [];
        }
    }
}
