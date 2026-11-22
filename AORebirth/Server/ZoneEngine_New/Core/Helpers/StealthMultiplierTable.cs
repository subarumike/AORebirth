namespace ZoneEngine_New.Core.Helpers
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Text.Json;

    using AORebirth.Core.GameData;

    /// <summary>
    /// Random damage multiplier for Aimed Shot, Sneak Attack and Backstab, rolled from the measured chances in
    /// GameData/SpecialAttackMultipliers.json (Gatester's Aimed Shot testing).
    /// </summary>
    public static class StealthMultiplierTable
    {
        public const string FileName = "SpecialAttackMultipliers.json";

        sealed record Row(int Skill, double[] Weights);

        sealed class Document
        {
            public RowData?[]? Rows { get; set; }
        }

        sealed class RowData
        {
            public int? Skill { get; set; }

            public double[]? Weights { get; set; }
        }

        static readonly object Gate = new();
        static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNameCaseInsensitive = true,
            AllowDuplicateProperties = false
        };
        static string? _loadedRoot;
        static Row[]? _rows;

        /// <summary>Loads the required combat catalog before ZoneEngine reports readiness.</summary>
        public static void Validate(string gameDataRoot) => _ = Load(gameDataRoot);

        /// <summary>Rolls a whole multiplier of at least 1 for <paramref name="skill"/>.</summary>
        public static int Roll(string gameDataRoot, int skill) => Roll(gameDataRoot, skill, Random.Shared.NextDouble());

        internal static int Roll(string gameDataRoot, int skill, double unitSample)
        {
            double[] weights = WeightsFor(Load(gameDataRoot), skill);
            double total = 0;
            foreach (double weight in weights)
                total += weight;
            double pick = unitSample * total;
            for (int i = 0; i < weights.Length; i++)
            {
                pick -= weights[i];
                if (pick < 0)
                    return i + 1;
            }

            return weights.Length;
        }

        /// <summary>Below the first row: always 1x. Between rows: each chance interpolated. Past the last row: the last row.</summary>
        static double[] WeightsFor(Row[] rows, int skill)
        {
            if (skill < rows[0].Skill)
                return [1.0];
            if (skill >= rows[^1].Skill)
                return rows[^1].Weights;

            int upper = 1;
            while (rows[upper].Skill <= skill)
                upper++;

            Row low = rows[upper - 1];
            Row high = rows[upper];
            double t = (skill - low.Skill) / (double)(high.Skill - low.Skill);
            var weights = new double[Math.Max(low.Weights.Length, high.Weights.Length)];
            for (int i = 0; i < weights.Length; i++)
            {
                double a = i < low.Weights.Length ? low.Weights[i] : 0;
                double b = i < high.Weights.Length ? high.Weights[i] : 0;
                weights[i] = a + ((b - a) * t);
            }

            return weights;
        }

        static Row[] Load(string gameDataRoot)
        {
            lock (Gate)
            {
                if (_rows != null && string.Equals(_loadedRoot, gameDataRoot, StringComparison.Ordinal))
                    return _rows;

                string path = GameDataPaths.ResolveFile(gameDataRoot, FileName);
                try
                {
                    using FileStream stream = File.OpenRead(path);
                    Document? document = JsonSerializer.Deserialize<Document>(stream, JsonOptions);
                    _rows = ValidateRows(document);
                    _loadedRoot = gameDataRoot;
                    return _rows;
                }
                catch (Exception error) when (error is IOException or UnauthorizedAccessException or JsonException or InvalidDataException)
                {
                    throw new InvalidDataException($"Required stealth multiplier table '{path}' could not be loaded: {error.Message}", error);
                }
            }
        }

        static Row[] ValidateRows(Document? document)
        {
            if (document?.Rows is not { Length: > 0 } data)
                throw new InvalidDataException("At least one skill row is required.");

            var rows = new Row[data.Length];
            var skills = new HashSet<int>();
            for (int i = 0; i < data.Length; i++)
            {
                if (data[i]?.Skill is not int skill || skill < 0 || !skills.Add(skill))
                    throw new InvalidDataException($"Skill row {i + 1} requires a unique nonnegative skill.");
                if (data[i]?.Weights is not { Length: > 0 } weights)
                    throw new InvalidDataException($"Skill {skill} requires at least one multiplier weight.");

                double total = 0;
                foreach (double weight in weights)
                {
                    if (!double.IsFinite(weight) || weight < 0 || weight > 1)
                        throw new InvalidDataException($"Skill {skill} has a multiplier weight outside [0, 1].");
                    total += weight;
                }
                if (total <= 0)
                    throw new InvalidDataException($"Skill {skill} requires a positive multiplier weight.");
                rows[i] = new Row(skill, weights);
            }

            Array.Sort(rows, (a, b) => a.Skill.CompareTo(b.Skill));
            return rows;
        }
    }
}
