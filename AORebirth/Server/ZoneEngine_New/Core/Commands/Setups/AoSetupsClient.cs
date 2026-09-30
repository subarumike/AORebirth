namespace ZoneEngine_New.Core.Commands.Setups;

using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

/// <summary>One aosetups.com equip setup, reduced to what <c>.give setup</c> applies.</summary>
public sealed record AoSetup(
    string Name,
    int Level,
    string Profession,
    IReadOnlyList<AoSetupItem> Items,
    IReadOnlyList<AoSetupImplant> Implants,
    IReadOnlyList<AoSetupSymbiant> Symbiants,
    IReadOnlyList<AoSetupSkill> Skills,
    IReadOnlyList<int> Buffs);

/// <summary>A weapon-page or armor-page item: aosetups stores only its high id and QL.</summary>
public sealed record AoSetupItem(bool Weapon, string Slot, int HighId, int Quality);

/// <summary>Cluster ids are aosetups' own; <see cref="AoSetupsReference"/> turns them into an implant item.</summary>
public sealed record AoSetupImplant(string Slot, int Quality, int Shiny, int Bright, int Faded);

/// <summary>A symbiant worn in an implant slot. aosetups stores its high id and chosen QL, same as a weapon or armor piece.</summary>
public sealed record AoSetupSymbiant(string Slot, int HighId, int Quality);

/// <summary>Points raised with IP above the starting base, by the client's short skill label.</summary>
public sealed record AoSetupSkill(string Name, int PointsFromIp);

/// <summary>
/// Reads aosetups.com. Only setup ids are accepted, never arbitrary URLs, so the server only ever calls this
/// one host. The item pairing and implant tables are fetched once and kept in memory.
/// </summary>
public sealed class AoSetupsClient
{
    public const string Host = "aosetups.com";
    const long MaxResponseBytes = 16 * 1024 * 1024;

    static readonly Regex SetupId = new("^[0-9a-f]{24}$", RegexOptions.CultureInvariant);

    readonly HttpClient _http;
    readonly object _gate = new();
    Task<AoSetupsReference>? _reference;

    public AoSetupsClient()
        : this(new HttpClient { BaseAddress = new Uri("https://" + Host + "/"), Timeout = TimeSpan.FromSeconds(30) })
    {
    }

    internal AoSetupsClient(HttpClient http)
    {
        _http = http ?? throw new ArgumentNullException(nameof(http));
        _http.MaxResponseContentBufferSize = MaxResponseBytes;
    }

    /// <summary>A bare setup id, or an https://aosetups.com/equip/&lt;id&gt; link.</summary>
    public static bool TryParseSetupId(string text, out string id)
    {
        id = string.Empty;
        string value = (text ?? string.Empty).Trim();
        if (Uri.TryCreate(value, UriKind.Absolute, out Uri? uri))
        {
            if (uri.Scheme != Uri.UriSchemeHttps && uri.Scheme != Uri.UriSchemeHttp)
                return false;
            if (!string.Equals(uri.Host, Host, StringComparison.OrdinalIgnoreCase)
                && !string.Equals(uri.Host, "www." + Host, StringComparison.OrdinalIgnoreCase))
                return false;

            string[] segments = uri.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries);
            if (segments.Length < 2 || !string.Equals(segments[0], "equip", StringComparison.OrdinalIgnoreCase))
                return false;
            value = segments[1];
        }

        value = value.ToLowerInvariant();
        if (!SetupId.IsMatch(value))
            return false;

        id = value;
        return true;
    }

    public async Task<AoSetup> GetSetupAsync(string id)
    {
        if (!SetupId.IsMatch(id ?? string.Empty))
            throw new ArgumentException("Not an aosetups setup id.", nameof(id));

        using JsonDocument document = JsonDocument.Parse(await _http.GetStringAsync("api/equip/" + id).ConfigureAwait(false));
        return Parse(document.RootElement);
    }

    /// <summary>Loaded on first use. A failed load is not kept, so the next command tries again.</summary>
    public Task<AoSetupsReference> GetReferenceAsync()
    {
        lock (_gate)
        {
            if (_reference == null || _reference.IsFaulted || _reference.IsCanceled)
                _reference = LoadReferenceAsync();
            return _reference;
        }
    }

    async Task<AoSetupsReference> LoadReferenceAsync()
    {
        Task<string> aodb = _http.GetStringAsync("assets/db-data/aodb.json");
        Task<string> clusters = _http.GetStringAsync("assets/db-data/Cluster.json");
        Task<string> implants = _http.GetStringAsync("assets/db-data/implantHelper.json");
        await Task.WhenAll(aodb, clusters, implants).ConfigureAwait(false);
        return AoSetupsReference.Parse(aodb.Result, clusters.Result, implants.Result);
    }

    static AoSetup Parse(JsonElement root)
    {
        var items = new List<AoSetupItem>();
        ReadItems(root, "weapons", weapon: true, items);
        ReadItems(root, "clothes", weapon: false, items);

        var implants = new List<AoSetupImplant>();
        var symbiants = new List<AoSetupSymbiant>();
        foreach (JsonElement implant in Array(root, "implants"))
        {
            if (implant.ValueKind != JsonValueKind.Object || !TryString(implant, "slot", out string slot))
                continue;

            string type = implant.TryGetProperty("type", out JsonElement typeElement) && typeElement.ValueKind == JsonValueKind.String
                ? typeElement.GetString() ?? string.Empty
                : "implant";
            if (string.Equals(type, "symbiant", StringComparison.OrdinalIgnoreCase))
            {
                if (TrySymbiant(implant, out int highId, out int quality))
                    symbiants.Add(new AoSetupSymbiant(slot, highId, quality));
                continue;
            }

            if (!string.Equals(type, "implant", StringComparison.OrdinalIgnoreCase) || !TryInt(implant, "ql", out int ql))
                continue;

            implant.TryGetProperty("clusters", out JsonElement clusters);
            implants.Add(new AoSetupImplant(slot, ql, Cluster(clusters, "Shiny"), Cluster(clusters, "Bright"), Cluster(clusters, "Faded")));
        }

        var skills = new List<AoSetupSkill>();
        int level = 0;
        string profession = string.Empty;
        if (root.TryGetProperty("character", out JsonElement character) && character.ValueKind == JsonValueKind.Object)
        {
            TryInt(character, "level", out level);
            TryString(character, "profession", out profession);
            foreach (JsonElement skill in Array(character, "skills"))
            {
                if (skill.ValueKind == JsonValueKind.Object && TryString(skill, "name", out string name) && TryInt(skill, "pointsFromIp", out int points))
                    skills.Add(new AoSetupSkill(name, points));
            }
        }

        var buffs = new List<int>();
        foreach (JsonElement buff in Array(root, "buffs"))
        {
            if (buff.ValueKind == JsonValueKind.Object && TryInt(buff, "aoid", out int aoid) && aoid > 0)
                buffs.Add(aoid);
        }

        string setupName = TryString(root, "name", out string value) ? value : string.Empty;
        return new AoSetup(setupName, level, profession, items, implants, symbiants, skills, buffs);
    }

    static void ReadItems(JsonElement root, string property, bool weapon, List<AoSetupItem> into)
    {
        foreach (JsonElement item in Array(root, property))
        {
            if (item.ValueKind == JsonValueKind.Object && TryString(item, "slot", out string slot)
                && TryInt(item, "highid", out int highId) && TryInt(item, "selectedQl", out int ql) && highId > 0)
                into.Add(new AoSetupItem(weapon, slot, highId, Math.Max(1, ql)));
        }
    }

    /// <summary>
    /// Symbiant rows carry the item under <c>symbiant.highid</c> / <c>symbiant.selectedQl</c>.
    /// The row's own <c>ql</c> is the implant-quality field and is not the item quality.
    /// </summary>
    static bool TrySymbiant(JsonElement row, out int highId, out int quality)
    {
        highId = 0;
        quality = 1;
        if (!row.TryGetProperty("symbiant", out JsonElement symbiant) || symbiant.ValueKind != JsonValueKind.Object
            || !TryInt(symbiant, "highid", out highId) || highId <= 0)
            return false;

        if (TryInt(symbiant, "selectedQl", out int selected))
            quality = Math.Max(1, selected);
        else if (TryInt(row, "ql", out int parentQl))
            quality = Math.Max(1, parentQl);
        return true;
    }

    static int Cluster(JsonElement clusters, string grade)
        => clusters.ValueKind == JsonValueKind.Object && clusters.TryGetProperty(grade, out JsonElement cluster)
            && cluster.ValueKind == JsonValueKind.Object && TryInt(cluster, "ClusterID", out int id) ? id : 0;

    static IEnumerable<JsonElement> Array(JsonElement parent, string property)
        => parent.TryGetProperty(property, out JsonElement array) && array.ValueKind == JsonValueKind.Array
            ? array.EnumerateArray()
            : [];

    static bool TryString(JsonElement parent, string property, out string value)
    {
        value = parent.TryGetProperty(property, out JsonElement element) && element.ValueKind == JsonValueKind.String
            ? element.GetString() ?? string.Empty
            : string.Empty;
        return value.Length != 0;
    }

    static bool TryInt(JsonElement parent, string property, out int value)
    {
        value = 0;
        return parent.TryGetProperty(property, out JsonElement element) && element.ValueKind == JsonValueKind.Number
            && element.TryGetInt32(out value);
    }
}

/// <summary>
/// aosetups' reference tables: low/high item pairs by QL range (aodb), cluster long names (Cluster), and the
/// built implant for each Shiny@Bright@Faded combination (implantHelper: low/high for QL 1-200, then 201-300).
/// </summary>
public sealed class AoSetupsReference
{
    readonly Dictionary<int, List<(int LowId, int LowQl, int HighQl)>> _pairsByHigh = new();
    readonly Dictionary<int, string> _clusterNames = new();
    readonly Dictionary<string, int[]> _implants = new(StringComparer.Ordinal);

    public static AoSetupsReference Parse(string aodb, string clusters, string implants)
    {
        var reference = new AoSetupsReference();
        foreach (Dictionary<string, JsonElement> row in Rows(aodb))
        {
            if (Int(row, "lowid") is int low && Int(row, "highid") is int high && Int(row, "lowql") is int lowQl && Int(row, "highql") is int highQl)
            {
                if (!reference._pairsByHigh.TryGetValue(high, out var pairs))
                    reference._pairsByHigh[high] = pairs = new();
                pairs.Add((low, lowQl, highQl));
            }
        }

        foreach (Dictionary<string, JsonElement> row in Rows(clusters))
        {
            if (Int(row, "ClusterID") is int id && row.TryGetValue("LongName", out JsonElement name) && name.ValueKind == JsonValueKind.String)
                reference._clusterNames[id] = name.GetString() ?? string.Empty;
        }

        using (JsonDocument document = JsonDocument.Parse(implants))
        {
            foreach (JsonProperty combination in document.RootElement.EnumerateObject())
            {
                if (combination.Value.ValueKind != JsonValueKind.Array)
                    continue;
                var ids = new List<int>();
                foreach (JsonElement id in combination.Value.EnumerateArray())
                    ids.Add(id.ValueKind == JsonValueKind.Number && id.TryGetInt32(out int value) ? value : 0);
                reference._implants[combination.Name] = ids.ToArray();
            }
        }

        return reference;
    }

    /// <summary>The low id whose QL range holds <paramref name="quality"/>; the closest range otherwise.</summary>
    public bool TryResolveLowId(int highId, int quality, out int lowId)
    {
        lowId = 0;
        if (!_pairsByHigh.TryGetValue(highId, out var pairs) || pairs.Count == 0)
            return false;

        (int LowId, int LowQl, int HighQl) best = pairs[0];
        int bestDistance = int.MaxValue;
        foreach (var pair in pairs)
        {
            int distance = quality < pair.LowQl ? pair.LowQl - quality : quality > pair.HighQl ? quality - pair.HighQl : 0;
            if (distance < bestDistance)
            {
                best = pair;
                bestDistance = distance;
            }
        }

        lowId = best.LowId;
        return true;
    }

    /// <summary>The built implant item for the cluster combination, in the QL band that holds <paramref name="quality"/>.</summary>
    public bool TryResolveImplant(AoSetupImplant implant, out int lowId, out int highId, out string combination)
    {
        lowId = highId = 0;
        combination = ClusterName(implant.Shiny) + "@" + ClusterName(implant.Bright) + "@" + ClusterName(implant.Faded);
        if (!_implants.TryGetValue(combination, out int[]? ids))
            return false;

        int band = implant.Quality > 200 && ids.Length >= 4 ? 2 : 0;
        if (ids.Length < band + 2 || ids[band] <= 0 || ids[band + 1] <= 0)
            return false;

        lowId = ids[band];
        highId = ids[band + 1];
        return true;
    }

    string ClusterName(int id) => id > 0 && _clusterNames.TryGetValue(id, out string? name) ? name : string.Empty;

    /// <summary>aosetups tables are <c>{ columns: [...], values: [[...], ...] }</c>.</summary>
    static IEnumerable<Dictionary<string, JsonElement>> Rows(string json)
    {
        using JsonDocument document = JsonDocument.Parse(json);
        JsonElement root = document.RootElement;
        if (!root.TryGetProperty("columns", out JsonElement columns) || !root.TryGetProperty("values", out JsonElement values))
            throw new InvalidOperationException("Unexpected aosetups table layout.");

        var names = new List<string>();
        foreach (JsonElement column in columns.EnumerateArray())
            names.Add(column.GetString() ?? string.Empty);

        var rows = new List<Dictionary<string, JsonElement>>();
        foreach (JsonElement value in values.EnumerateArray())
        {
            var row = new Dictionary<string, JsonElement>(StringComparer.Ordinal);
            int i = 0;
            foreach (JsonElement cell in value.EnumerateArray())
            {
                if (i < names.Count)
                    row[names[i]] = cell.Clone();
                i++;
            }

            rows.Add(row);
        }

        return rows;
    }

    static int? Int(Dictionary<string, JsonElement> row, string column)
        => row.TryGetValue(column, out JsonElement value) && value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out int number) ? number : null;
}
