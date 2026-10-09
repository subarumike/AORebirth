namespace AORebirth.Core.Teams
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.IO;
    using System.Text.RegularExpressions;

    using AORebirth.Core.GameData;
    using AORebirth.Interfaces.Persistence.Teams;

    /// <summary>
    /// Loads <c>GameData/Teams/LevelEligibility.json</c> for ChatEngine + ZoneEngine_New.
    /// Content-only — no C# range tables.
    /// </summary>
    public sealed class JsonTeamLevelEligibilityDao : ITeamLevelEligibilityDao
    {
        static readonly Regex RangePattern = new Regex(
            "\"Level\"\\s*:\\s*(\\d+)\\s*,\\s*\"Minimum\"\\s*:\\s*(\\d+)\\s*,\\s*\"Maximum\"\\s*:\\s*(\\d+)",
            RegexOptions.CultureInvariant | RegexOptions.Compiled);

        readonly Dictionary<int, Range> _ranges;
        readonly int _minimumLevel;
        readonly int _maximumLevel;
        readonly int[] _memberXpPermille;

        JsonTeamLevelEligibilityDao(
            Dictionary<int, Range> ranges,
            int minimumLevel,
            int maximumLevel,
            int[] memberXpPermille)
        {
            _ranges = ranges;
            _minimumLevel = minimumLevel;
            _maximumLevel = maximumLevel;
            _memberXpPermille = memberXpPermille;
        }

        /// <summary>Process singleton loaded from the runtime GameData tree.</summary>
        public static JsonTeamLevelEligibilityDao Current { get; } =
            Load(Path.Combine(GameDataPaths.ResolveRuntimeRoot(), "Teams", "LevelEligibility.json"));

        public static JsonTeamLevelEligibilityDao Load(string path)
        {
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
                throw new FileNotFoundException("Team LevelEligibility.json is required.", path);

            string text = File.ReadAllText(path);
            int minimumLevel = ReadIntField(text, "MinimumLevel", 1);
            int maximumLevel = ReadIntField(text, "MaximumLevel", 220);
            if (minimumLevel <= 0 || maximumLevel < minimumLevel)
                throw new InvalidDataException("Team eligibility MinimumLevel/MaximumLevel is invalid.");

            var ranges = new Dictionary<int, Range>();
            foreach (Match match in RangePattern.Matches(text))
            {
                int level = int.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture);
                int min = int.Parse(match.Groups[2].Value, CultureInfo.InvariantCulture);
                int max = int.Parse(match.Groups[3].Value, CultureInfo.InvariantCulture);
                if (level < minimumLevel || level > maximumLevel
                    || min < minimumLevel || max > maximumLevel
                    || min > level || max < level)
                {
                    throw new InvalidDataException(
                        "Team eligibility range is invalid for level "
                        + level.ToString(CultureInfo.InvariantCulture) + ".");
                }

                ranges[level] = new Range(min, max);
            }

            long expected = (long)maximumLevel - minimumLevel + 1;
            if (ranges.Count != expected)
            {
                throw new InvalidDataException(
                    "Team eligibility requires one range for every configured level.");
            }

            for (int level = minimumLevel; level <= maximumLevel; level++)
            {
                if (!ranges.ContainsKey(level))
                    throw new InvalidDataException(
                        "Team eligibility is missing level "
                        + level.ToString(CultureInfo.InvariantCulture) + ".");
            }

            return new JsonTeamLevelEligibilityDao(ranges, minimumLevel, maximumLevel, ReadMemberXpPermille(text));
        }

        public bool TryGetRange(int level, out int minimum, out int maximum)
        {
            int clamped = ClampToConfiguredLevel(level);
            if (_ranges.TryGetValue(clamped, out Range range))
            {
                minimum = range.Minimum;
                maximum = range.Maximum;
                return true;
            }

            minimum = 0;
            maximum = 0;
            return false;
        }

        public int ClampToConfiguredLevel(int level)
        {
            if (level < _minimumLevel)
                return _minimumLevel;
            if (level > _maximumLevel)
                return _maximumLevel;
            return level;
        }

        public bool IsCompatible(int searcherLevel, int candidateLevel)
        {
            if (!TryGetRange(searcherLevel, out int min, out int max))
                return false;
            return candidateLevel >= min && candidateLevel <= max;
        }

        public bool IsTooHighForMember(int memberLevel, int inviteeLevel)
        {
            if (!TryGetRange(memberLevel, out _, out int max))
                return false;
            return inviteeLevel > max;
        }

        public bool IsTooLowForMember(int memberLevel, int inviteeLevel)
        {
            if (!TryGetRange(memberLevel, out int min, out _))
                return false;
            return inviteeLevel < min;
        }

        public int MemberXpPermille(int teamSize)
        {
            if (teamSize <= 1)
                return 1000;
            if (teamSize <= _memberXpPermille.Length)
                return _memberXpPermille[teamSize - 1];
            return 1000 / teamSize;
        }

        /// <summary>Optional <c>MemberXpPermille</c> array, indexed by team size - 1. Empty when absent.</summary>
        static int[] ReadMemberXpPermille(string text)
        {
            Match match = Regex.Match(
                text,
                "\"MemberXpPermille\"\\s*:\\s*\\[([^\\]]*)\\]",
                RegexOptions.CultureInvariant);
            if (!match.Success || string.IsNullOrWhiteSpace(match.Groups[1].Value))
                return new int[0];

            string[] parts = match.Groups[1].Value.Split(',');
            var values = new int[parts.Length];
            for (int i = 0; i < parts.Length; i++)
            {
                if (!int.TryParse(parts[i].Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out values[i]))
                    throw new InvalidDataException("Team eligibility MemberXpPermille is invalid.");
            }

            return values;
        }

        static int ReadIntField(string text, string name, int fallback)
        {
            Match match = Regex.Match(
                text,
                "\"" + name + "\"\\s*:\\s*(\\d+)",
                RegexOptions.CultureInvariant);
            if (!match.Success)
                return fallback;
            return int.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture);
        }

        readonly struct Range
        {
            public Range(int minimum, int maximum)
            {
                Minimum = minimum;
                Maximum = maximum;
            }

            public int Minimum { get; }

            public int Maximum { get; }
        }
    }
}
