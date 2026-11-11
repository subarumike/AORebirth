using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;

internal static class DoaAdditionalInputs
{
    internal const string NativeSourceRoot = "Tools/AOClientRoomSpaceGuard/ProxyDll/src";
    internal const string PackagedInteractions = "AORebirth/Server/ZoneEngine_New/Content/Runtime/interactions.json";
    internal const string MonsterPolicy = "MonsterCombatRules.json";
    internal const string LftPolicy = "Teams/LftLocations.json";

    private static readonly Regex NativeCommentsAndStrings = new(
        """(?<comment>//[^\r\n]*|/\*[\s\S]*?\*/)|"(?:\\.|[^"\\])*"|'(?:\\.|[^'\\])*'""",
        RegexOptions.CultureInvariant | RegexOptions.NonBacktracking);
    private static readonly Regex NativeBinding = new(
        """\b(?:const|constexpr)\s+(?<type>(?:[\w:]+[\s*]+)+)(?<name>\w+)\s*(?:\[[^\]]*\])?\s*=\s*(?<value>[^;]+);""",
        RegexOptions.CultureInvariant | RegexOptions.NonBacktracking);
    private static readonly Regex EndpointBinding = new(
        @"(?:Login|Endpoint|Server|Local).*(?:Ip|Address|Port|Host)",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    internal static bool IsNativeSource(string path)
        => Path.GetExtension(path).ToLowerInvariant() is ".cpp" or ".c" or ".cxx" or ".h" or ".hpp" or ".inc";

    internal static IEnumerable<Finding> NativeIssues(string path, string source)
    {
        string code = NativeCommentsAndStrings.Replace(source, match => match.Groups["comment"].Success
            ? new string(match.Value.Select(character => character is '\r' or '\n' ? character : ' ').ToArray())
            : match.Value);
        foreach (Match binding in NativeBinding.Matches(code))
        {
            string name = binding.Groups["name"].Value;
            string type = binding.Groups["type"].Value.Trim();
            string value = binding.Groups["value"].Value.Trim();
            if (type.EndsWith("PlayfieldName", StringComparison.Ordinal) && value.StartsWith('{'))
                yield return AtBinding("DOA_NATIVE_COMPILED_PLAYFIELDS",
                    "The client playfield name table must be loaded from validated data.");
            if (EndpointBinding.IsMatch(name) && (value.StartsWith('"')
                || value.TrimStart('(', ' ').StartsWithAsciiDigit()))
                yield return AtBinding("DOA_NATIVE_COMPILED_ENDPOINT",
                    "The client login endpoint must be selected from editable configuration.");

            Finding AtBinding(string rule, string reason)
                => DoaRules.DataFinding(path, name, rule, reason, value) with
                {
                    Line = 1 + code.Take(binding.Index).Count(character => character == '\n')
                };
        }
    }

    internal static IEnumerable<Finding> PackagedInteractionIssues(string text)
    {
        using JsonDocument? document = ParseObject(text);
        if (document == null)
        {
            yield return DoaRules.DataFinding(PackagedInteractions, "Interactions", "DOA_INVALID_PACKAGED_INTERACTIONS",
                "The packaged interactions catalog must be valid JSON.", "INVALID");
            yield break;
        }

        JsonElement root = document.RootElement;
        if (root.TryGetProperty("Journals", out JsonElement journals) && journals.ValueKind == JsonValueKind.Object)
        {
            foreach (JsonProperty journal in journals.EnumerateObject())
            {
                JsonElement value = journal.Value;
                if (value.ValueKind == JsonValueKind.Object
                    && (value.TryGetProperty("Hex", out JsonElement hex) && hex.ValueKind == JsonValueKind.String
                        && !string.IsNullOrWhiteSpace(hex.GetString())
                    || value.TryGetProperty("DeleteHex", out JsonElement deletes) && deletes.ValueKind == JsonValueKind.Array
                        && deletes.GetArrayLength() != 0))
                    yield return DoaRules.DataFinding(PackagedInteractions, journal.Name, "DOA_RECORDED_JOURNAL_PAYLOAD",
                        "Journal update and deletion packets must use typed serialization.", journal.Name);
            }
        }

        if (root.TryGetProperty("TimedTurnIns", out JsonElement rules) && rules.ValueKind == JsonValueKind.Array)
        {
            foreach (JsonElement rule in rules.EnumerateArray())
            {
                if (rule.ValueKind != JsonValueKind.Object || !rule.TryGetProperty("TurnInNpc", out JsonElement npc)
                    || npc.ValueKind == JsonValueKind.Null)
                    continue;

                string key = rule.TryGetProperty("Key", out JsonElement keyValue) && keyValue.ValueKind == JsonValueKind.String
                    ? keyValue.GetString()! : "TimedTurnIns";
                yield return DoaRules.DataFinding(PackagedInteractions, key, "DOA_PACKAGED_GAMEPLAY_BINDING",
                    "Timed turn-in NPC identities must be owned by the selected GameData binding.", key);
            }
        }
    }

    internal static IEnumerable<Finding> MonsterPolicyIssues(string path, string? text)
    {
        using JsonDocument? document = text == null ? null : ParseObject(text);
        if (document == null)
        {
            yield return DoaRules.DataFinding(path, MonsterPolicy, "DOA_MISSING_ENCOUNTER_POLICY_CATALOG",
                "Encounter policy validation requires a valid MonsterCombatRules catalog.", "MISSING_OR_INVALID");
            yield break;
        }

        foreach (JsonProperty encounter in document.RootElement.EnumerateObject())
        {
            JsonElement value = encounter.Value;
            if (value.ValueKind != JsonValueKind.Object)
            {
                yield return DoaRules.DataFinding(path, encounter.Name, "DOA_INVALID_ENCOUNTER_POLICY",
                    "An encounter rule must be a JSON object.", encounter.Name);
                continue;
            }

            if (value.TryGetProperty("Pets", out JsonElement pets) && pets.ValueKind != JsonValueKind.Null
                && (pets.ValueKind != JsonValueKind.Object || !NonnegativeInteger(pets, "MaxPets")
                    || !NonnegativeNumber(pets, "SummonIntervalSeconds")
                    || !NonnegativeNumber(pets, "RespawnDelaySeconds")))
                yield return DoaRules.DataFinding(path, encounter.Name, "DOA_UNAUTHORED_PET_POLICY",
                    "A pet rule must explicitly specify MaxPets, SummonIntervalSeconds and RespawnDelaySeconds.", encounter.Name);

            if (!value.TryGetProperty("TriggeredSelfNanos", out JsonElement nanos) || nanos.ValueKind == JsonValueKind.Null)
                continue;
            if (nanos.ValueKind != JsonValueKind.Array)
            {
                yield return DoaRules.DataFinding(path, encounter.Name, "DOA_INVALID_NANO_POLICY",
                    "TriggeredSelfNanos must be an array of authored nano rules.", encounter.Name);
                continue;
            }

            int index = 0;
            foreach (JsonElement nano in nanos.EnumerateArray())
            {
                if (nano.ValueKind != JsonValueKind.Object || !nano.TryGetProperty("MaxCastsPerFight", out JsonElement limit)
                    || (limit.ValueKind != JsonValueKind.Null && !PositiveInteger(limit))
                    || !NonnegativeInteger(nano, "CooldownSeconds"))
                    yield return DoaRules.DataFinding(path, $"{encounter.Name}.TriggeredSelfNanos[{index}]",
                        "DOA_UNAUTHORED_NANO_POLICY", "A nano trigger must explicitly specify MaxCastsPerFight and CooldownSeconds.",
                        $"{encounter.Name}:{index}");
                index++;
            }
        }
    }

    internal static IEnumerable<Finding> LftPolicyIssues(string path, string? text, Func<int, bool>? resolves = null)
    {
        if (text == null)
        {
            yield return DoaRules.DataFinding(path, LftPolicy, "DOA_MISSING_LFT_POLICY",
                "The LFT location catalog is required.", "MISSING");
            yield break;
        }

        using JsonDocument? document = ParseObject(text);
        if (document == null || !ReadLftLocations(document.RootElement, out HashSet<int> playfields))
        {
            yield return DoaRules.DataFinding(path, LftPolicy, "DOA_INVALID_LFT_POLICY",
                "LFT policy requires version 1, an explicit unlisted region and distinct positive playfield IDs.", "INVALID");
            yield break;
        }

        if (resolves != null)
            foreach (int playfield in playfields.Order())
                if (!resolves(playfield))
                    yield return DoaRules.DataFinding(path, playfield.ToString(CultureInfo.InvariantCulture),
                        "DOA_UNRESOLVED_LFT_PLAYFIELD", "LFT playfield ID has no matching canonical playfield metadata.",
                        playfield.ToString(CultureInfo.InvariantCulture));
    }

    private static bool ReadLftLocations(JsonElement root, out HashSet<int> playfields)
    {
        playfields = [];
        if (!root.TryGetProperty("Version", out JsonElement version) || version.ValueKind != JsonValueKind.Number
            || !version.TryGetInt32(out int schema) || schema != 1
            || !root.TryGetProperty("UnlistedRegion", out JsonElement region) || region.ValueKind != JsonValueKind.String
            || region.GetString() is not ("RubiKa" or "Shadowlands")
            || !root.TryGetProperty("ShadowlandsPlayfieldIds", out JsonElement rows) || rows.ValueKind != JsonValueKind.Array)
            return false;

        var properties = new HashSet<string>(StringComparer.Ordinal);
        foreach (JsonProperty property in root.EnumerateObject())
            if (property.Name is not ("Version" or "UnlistedRegion" or "ShadowlandsPlayfieldIds")
                || !properties.Add(property.Name))
                return false;

        foreach (JsonElement row in rows.EnumerateArray())
            if (row.ValueKind != JsonValueKind.Number || !row.TryGetInt32(out int id) || id <= 0 || !playfields.Add(id))
                return false;

        return playfields.Count > 0;
    }

    internal static bool LftMetadataMatches(string root, int id)
    {
        string path = Path.Combine(root, "Playfields", id.ToString(CultureInfo.InvariantCulture), "metadata.json");
        return File.Exists(path) && LftMetadataIdentityMatches(File.ReadAllText(path), id);
    }

    internal static bool LftMetadataIdentityMatches(string text, int id)
    {
        using JsonDocument? document = ParseObject(text);
        if (document == null)
            return false;

        JsonProperty[] identities = document.RootElement.EnumerateObject()
            .Where(property => property.Name.Equals("playfieldId", StringComparison.OrdinalIgnoreCase)).ToArray();
        return identities.Length == 1 && identities[0].Value.ValueKind == JsonValueKind.Number
            && identities[0].Value.TryGetInt32(out int declared) && declared == id;
    }

    private static bool StartsWithAsciiDigit(this string value) => value.Length != 0 && char.IsAsciiDigit(value[0]);

    private static bool PositiveInteger(JsonElement value) => value.ValueKind == JsonValueKind.Number
        && value.TryGetInt32(out int number) && number > 0;

    private static bool NonnegativeInteger(JsonElement value, string name)
        => value.TryGetProperty(name, out JsonElement property) && property.ValueKind == JsonValueKind.Number
            && property.TryGetInt32(out int number) && number >= 0;

    private static bool NonnegativeNumber(JsonElement value, string name)
        => value.TryGetProperty(name, out JsonElement property) && property.ValueKind == JsonValueKind.Number
            && property.TryGetDouble(out double number) && double.IsFinite(number) && number >= 0;

    private static JsonDocument? ParseObject(string text)
    {
        try
        {
            JsonDocument document = JsonDocument.Parse(text.TrimStart('\uFEFF'));
            if (document.RootElement.ValueKind == JsonValueKind.Object)
                return document;

            document.Dispose();
            return null;
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
