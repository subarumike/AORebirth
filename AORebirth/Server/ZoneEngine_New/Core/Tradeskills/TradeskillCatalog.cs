namespace ZoneEngine_New.Core.Tradeskills
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Text.Json;
    using System.Text.Json.Serialization;

    /// <summary>
    /// Tradeskill recipes: an item (Source) combined with another (Target) makes a Result. Live keeps recipes on the
    /// server only (the RDB says nothing about what two items make), so they come from captures and curated data:
    /// <c>Tradeskills.json</c> (captured and hand-checked recipes) is searched before <c>Tradeskills.Legacy.json</c>
    /// (the CellAO tradeskill table, unverified). An item matches a recipe id by its low or its high template id.
    /// </summary>
    public sealed class TradeskillCatalog
    {
        public const string FileName = "Tradeskills.json";
        public const string LegacyFileName = "Tradeskills.Legacy.json";

        static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNameCaseInsensitive = true,
            ReadCommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true,
            Converters = { new JsonStringEnumConverter() }
        };

        public static readonly TradeskillCatalog Empty = new(Array.Empty<TradeskillRecipe>());

        readonly TradeskillRecipe[] _recipes;
        readonly Dictionary<int, List<TradeskillRecipe>> _bySource = new();
        readonly HashSet<int> _sourceIds = new();
        readonly HashSet<int> _targetIds = new();

        public TradeskillCatalog(IEnumerable<TradeskillRecipe> recipes)
        {
            ArgumentNullException.ThrowIfNull(recipes);
            _recipes = recipes.Where(r => r != null && r.Source.Length > 0 && r.Target.Length > 0 && r.Result.LowId > 0).ToArray();
            foreach (TradeskillRecipe recipe in _recipes)
            {
                foreach (int id in recipe.Source.Distinct())
                {
                    if (!_bySource.TryGetValue(id, out List<TradeskillRecipe>? list))
                        _bySource[id] = list = new List<TradeskillRecipe>();
                    list.Add(recipe);
                    _sourceIds.Add(id);
                    if (recipe.AnyOrder) _targetIds.Add(id);
                }
                foreach (int id in recipe.Target)
                {
                    _targetIds.Add(id);
                    if (recipe.AnyOrder) _sourceIds.Add(id);
                }
            }
        }

        public int Count => _recipes.Length;

        public IReadOnlyList<TradeskillRecipe> Recipes => _recipes;

        /// <summary>
        /// The recipe for <paramref name="source"/> used on <paramref name="target"/> (each given as low and high id).
        /// <paramref name="swapped"/> is true when the items match the recipe's Target and Source the other way round
        /// (allowed only for <see cref="TradeskillRecipe.AnyOrder"/> recipes). Earlier recipes win.
        /// </summary>
        public bool TryMatch(int sourceLow, int sourceHigh, int targetLow, int targetHigh, out TradeskillRecipe recipe, out bool swapped)
        {
            TradeskillRecipe? forward = First(sourceLow, sourceHigh, targetLow, targetHigh, anyOrderOnly: false);
            TradeskillRecipe? reverse = First(targetLow, targetHigh, sourceLow, sourceHigh, anyOrderOnly: true);
            if (forward != null && (reverse == null || forward.Order <= reverse.Order))
            {
                recipe = forward;
                swapped = false;
                return true;
            }

            recipe = reverse!;
            swapped = reverse != null;
            return swapped;
        }

        /// <summary>Whether some recipe takes an item with these ids as its source / target (the window's counters).</summary>
        public bool CanBeSource(int low, int high) => _sourceIds.Contains(low) || _sourceIds.Contains(high);

        public bool CanBeTarget(int low, int high) => _targetIds.Contains(low) || _targetIds.Contains(high);

        TradeskillRecipe? First(int sourceLow, int sourceHigh, int targetLow, int targetHigh, bool anyOrderOnly)
        {
            TradeskillRecipe? best = null;
            foreach (int id in sourceLow == sourceHigh ? new[] { sourceLow } : new[] { sourceLow, sourceHigh })
            {
                if (!_bySource.TryGetValue(id, out List<TradeskillRecipe>? list))
                    continue;
                foreach (TradeskillRecipe recipe in list)
                {
                    if (anyOrderOnly && !recipe.AnyOrder)
                        continue;
                    if (!recipe.Target.Contains(targetLow) && !recipe.Target.Contains(targetHigh))
                        continue;
                    if (best == null || recipe.Order < best.Order)
                        best = recipe;
                    break;
                }
            }

            return best;
        }

        /// <summary>Reads <see cref="FileName"/> then <see cref="LegacyFileName"/> from the GameData root; missing files are empty.</summary>
        public static TradeskillCatalog Load(string gameDataRoot, Action<string>? log = null)
        {
            var recipes = new List<TradeskillRecipe>();
            foreach (string name in new[] { FileName, LegacyFileName })
            {
                string path = Path.Combine(gameDataRoot, name);
                if (!File.Exists(path))
                {
                    log?.Invoke(name + " not found at " + path);
                    continue;
                }

                TradeskillFile? file = JsonSerializer.Deserialize<TradeskillFile>(File.ReadAllText(path), JsonOptions);
                int before = recipes.Count;
                foreach (TradeskillRecipe recipe in file?.Recipes ?? new List<TradeskillRecipe>())
                {
                    recipe.Order = recipes.Count;
                    recipe.Origin = name;
                    recipes.Add(recipe);
                }
                log?.Invoke(name + ": " + (recipes.Count - before) + " recipes from " + path);
            }

            return new TradeskillCatalog(recipes);
        }

        public static TradeskillCatalog Parse(string json)
        {
            TradeskillFile? file = JsonSerializer.Deserialize<TradeskillFile>(json, JsonOptions);
            List<TradeskillRecipe> recipes = file?.Recipes ?? new List<TradeskillRecipe>();
            for (int i = 0; i < recipes.Count; i++)
                recipes[i].Order = i;
            return new TradeskillCatalog(recipes);
        }

        sealed class TradeskillFile
        {
            public List<TradeskillRecipe> Recipes { get; set; } = new();
        }
    }

    /// <summary>How the result's quality is chosen.</summary>
    public enum TradeskillResultQl
    {
        /// <summary>The target's QL, plus bumps from skill above the requirement (CellAO: the implant's QL).</summary>
        Target,

        /// <summary>The source's QL.</summary>
        Source,

        /// <summary><see cref="TradeskillRecipe.FixedQl"/> (live Arete combines grant QL 1).</summary>
        Fixed
    }

    public sealed class TradeskillRecipe
    {
        /// <summary>"&lt;source&gt;&gt;&lt;target&gt;"; the capture tool replaces only the keys it made.</summary>
        public string Key { get; set; } = string.Empty;

        public string? Name { get; set; }

        /// <summary>Template ids (low or high) any of which makes an item this recipe's Source.</summary>
        public int[] Source { get; set; } = Array.Empty<int>();

        public int[] Target { get; set; } = Array.Empty<int>();

        /// <summary>The items may go in either order (live Arete combines accept both drags).</summary>
        public bool AnyOrder { get; set; }

        public TradeskillResult Result { get; set; } = new();

        public TradeskillResultQl ResultQl { get; set; } = TradeskillResultQl.Target;

        public int FixedQl { get; set; } = 1;

        public bool ConsumeSource { get; set; }

        public bool ConsumeTarget { get; set; }

        /// <summary>The target must be at least this QL (CellAO MinTarget, tier armour); 0 = any.</summary>
        public int MinTargetQl { get; set; }

        /// <summary>
        /// How far below the target's QL the source may be, in percent of the target's QL (CellAO QlRangePercent;
        /// 1 means the source must be at least the target's QL); 0 = no check.
        /// </summary>
        public int QlRangePercent { get; set; }

        public List<TradeskillSkill> Skills { get; set; } = new();

        /// <summary>Most QL bumps; implant recipes are also capped by the implant's QL tier.</summary>
        public int MaxBump { get; set; }

        public bool Implant { get; set; }

        public int MinXp { get; set; }

        public int MaxXp { get; set; }

        public string? Provenance { get; set; }

        public string? Evidence { get; set; }

        /// <summary>Position in the load order: lower wins when two recipes match.</summary>
        [JsonIgnore]
        public int Order { get; set; }

        [JsonIgnore]
        public string Origin { get; set; } = string.Empty;
    }

    public sealed class TradeskillResult
    {
        public int LowId { get; set; }

        public int HighId { get; set; }
    }

    public sealed class TradeskillSkill
    {
        /// <summary>Skill stat id: ME 125, EE 126, Tutoring 141, QFT 157, WS 158, PT 159, NP 160, CL 161, Psy 162, Chem 163, B&amp;E 165.</summary>
        public int Stat { get; set; }

        /// <summary>Skill needed as a percentage of the target's QL (250 = 2.5 x QL).</summary>
        public int Percent { get; set; }

        /// <summary>Skill points above the requirement per extra result QL; 0 = no bumps.</summary>
        public int PerBump { get; set; }
    }
}
