namespace ZoneEngine_New.Core.Helpers
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Text.Json;

    /// <summary>
    /// Random damage multiplier for Aimed Shot, Sneak Attack and Backstab, rolled from the measured chances in
    /// GameData/SpecialAttackMultipliers.json (Gatester's Aimed Shot testing). The highest multiplier grows by one
    /// every 95 skill and low multipliers dominate; no closed formula fits the data, so the table is the rule.
    /// </summary>
    public static class StealthMultiplierTable
    {
        public const string FileName = "SpecialAttackMultipliers.json";

        sealed record Row(int Skill, double[] Weights);

        sealed class Document
        {
            public List<RowData> Rows { get; set; } = [];
        }

        sealed class RowData
        {
            public int Skill { get; set; }

            public double[] Weights { get; set; } = [];
        }

        static readonly object Gate = new();
        static string? _loadedFrom;
        static Row[] _rows = [];

        /// <summary>Rolls a whole multiplier of at least 1 for <paramref name="skill"/>.</summary>
        public static int Roll(string gameDataRoot, int skill)
        {
            double[] weights = WeightsFor(Load(gameDataRoot), skill);
            double total = 0;
            foreach (double weight in weights)
                total += weight;
            if (total <= 0)
                return 1;

            double pick = Random.Shared.NextDouble() * total;
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
            if (rows.Length == 0 || skill < rows[0].Skill)
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
            string path = Path.Combine(gameDataRoot, FileName);
            lock (Gate)
            {
                if (string.Equals(_loadedFrom, path, StringComparison.OrdinalIgnoreCase))
                    return _rows;

                _loadedFrom = path;
                _rows = [];
                if (!File.Exists(path))
                    return _rows;

                Document? document = JsonSerializer.Deserialize<Document>(
                    File.ReadAllText(path), new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                var rows = new List<Row>();
                foreach (RowData row in document?.Rows ?? [])
                {
                    if (row.Weights.Length > 0)
                        rows.Add(new Row(row.Skill, row.Weights));
                }

                rows.Sort((a, b) => a.Skill.CompareTo(b.Skill));
                _rows = rows.ToArray();
                return _rows;
            }
        }
    }
}
