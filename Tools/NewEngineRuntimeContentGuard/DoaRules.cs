using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

internal static class DoaRules
{
    internal const string LayoutPath = "AORebirth/Server/ZoneEngine_New/Core/Quests/Dungeons/DungeonLayoutGenerator.cs";
    private const string DataPath = "AORebirth/GameData/";
    private const string DungeonPolicy = "DungeonGeneration.json";
    private static readonly string[] CapturedSymbols =
        ["MissionRollCaptureLibrary", "MissionRollCaptureTemplate", "CapturedRollBodiesHex", "CapturedPacketHex"];
    private static readonly string[] CapturedMembers = ["CapturedBody", "CapturedCount", "DecodeTemplate", "TemplateBody"];
    private static readonly string[] ContentCalls =
        ["CreateWithNewInstance", "TryResolveMobTemplate", "TryGetMobTemplate", "HasMobTemplate", "CanResolveItemHash", "TryRollIds", "TryMint"];
    private static readonly string[] MissionPolicyFields =
        ["StyleId", "FloorCount", "FloorSize", "FloorHeight", "LockedDoorChancePercent", "SpawnPointChancePercent",
         "ItemHashes", "NpcHashes", "FindPersonNpcHashes", "RegularNpcHashes", "MinQuality", "MaxQuality", "MinItems", "MaxItems",
         "RoomDensity", "CountVariationFraction", "RarityWeights"];
    private static readonly IReadOnlyDictionary<string, int> HistoricalLayoutFields = new Dictionary<string, int>(StringComparer.Ordinal)
    {
        ["TemporaryStyle"] = 324, ["TemporaryFloors"] = 2, ["TemporaryFloorSize"] = 16,
        ["TemporaryLockedDoorChancePercent"] = 5
    };

    internal static string SourceFingerprint(SyntaxNode node)
    {
        SyntaxNode context = node.AncestorsAndSelf().FirstOrDefault(ancestor => ancestor is StatementSyntax or EqualsValueClauseSyntax
            or SwitchExpressionArmSyntax or AttributeSyntax) ?? node;
        return Digest(Tokens(context) + "\n" + Tokens(node));
    }

    internal static Finding DataFinding(string path, string symbol, string rule, string reason, string value)
        => new(path, 0, symbol, "DATA_ORIENTED_ARCHITECTURE", rule, reason, value, Digest(value + "\n" + reason));

    internal static bool IsForbiddenRuntimeProjectReference(string target)
    {
        string[] parts = target.Split('/');
        return parts.Any(part => part.Equals("tools", StringComparison.OrdinalIgnoreCase)
            || part.Equals("tools-temp", StringComparison.OrdinalIgnoreCase)
            || part.Equals("tests", StringComparison.OrdinalIgnoreCase)
            || part.EndsWith(".Tests", StringComparison.OrdinalIgnoreCase)
            || part.EndsWith(".Tests.csproj", StringComparison.OrdinalIgnoreCase))
            || (target.StartsWith("AORebirth/Server/", StringComparison.Ordinal)
                && parts.Skip(2).Any(part => part is "LoginEngine" or "WorldEngine" or "ChatEngine" or "WebEngine"));
    }

    internal static IEnumerable<(SyntaxNode Node, string Rule, string Basis)> SourceIssues(string path, SyntaxNode syntax)
    {
        bool missions = path.StartsWith("AORebirth/Server/ZoneEngine_New/", StringComparison.Ordinal)
            && (path.Contains("/Missions/", StringComparison.Ordinal) || path.Contains("/Quests/", StringComparison.Ordinal));
        bool offerGeneration = path is "AORebirth/Server/ZoneEngine_New/Core/Missions/GeneratedMissionRollService.cs"
            or "AORebirth/Server/ZoneEngine_New/Core/Missions/Content/MissionOfferCatalog.cs";
        bool historicalLayout = path == LayoutPath && IsHistoricalLayoutContract(syntax);
        foreach (var name in syntax.DescendantNodes().OfType<SimpleNameSyntax>())
        {
            if (CapturedSymbols.Contains(name.Identifier.ValueText, StringComparer.Ordinal)
                || name.Parent is MemberAccessExpressionSyntax access && access.Name == name
                && Name(access.Expression) == "GeneratedMissionWire"
                && (CapturedMembers.Contains(name.Identifier.ValueText, StringComparer.Ordinal)
                    || offerGeneration && name.Identifier.ValueText == "Read"))
                yield return (name, "DOA_RECORDED_PACKET_DEPENDENCY", "Production code depends on a recorded roll payload or gameplay packet template.");
        }

        foreach (var literal in syntax.DescendantNodes().OfType<LiteralExpressionSyntax>())
        {
            if (literal.Token.ValueText is "RollBodies.json" or "RollTemplate.json")
                yield return (literal, "DOA_RECORDED_PACKET_DEPENDENCY", "Recorded packet bodies belong in offline evidence and test fixtures.");

            if (!IsAuthoredLiteral(literal) || literal.Ancestors().Any(node => node is AttributeSyntax or EnumDeclarationSyntax)) continue;
            string? target = DirectTarget(literal);
            if (target != null && IsContentIdentity(target))
                yield return (literal, "DOA_LITERAL_CONTENT_IDENTITY", "A runtime binding embeds a concrete gameplay identity.");
            if (missions && target != null && (MissionPolicyFields.Contains(target, StringComparer.Ordinal)
                    || HistoricalLayoutFields.ContainsKey(target))
                && !(historicalLayout && HistoricalLayoutFields.ContainsKey(target)))
                yield return (literal, "DOA_FIXED_MISSION_POLICY", "Mission selection, QL or generation parameters belong in validated GameData.");

            ExpressionSyntax operand = UnwrapParent(literal);
            if (operand.Parent is ArgumentSyntax argument && argument.Parent is ArgumentListSyntax list
                && list.Parent is InvocationExpressionSyntax call && ContentCalls.Contains(Name(call.Expression), StringComparer.Ordinal)
                && list.Arguments.IndexOf(argument) == 0)
                yield return (literal, "DOA_LITERAL_CONTENT_CALL", "A content resolver or item builder receives a fixed identity.");
            if (missions && literal.Token.Value is int version && version is 1 or 2
                && operand.Parent is ArgumentSyntax versionArgument && versionArgument.Parent is ArgumentListSyntax versionList
                && versionList.Arguments.Count == 4 && versionList.Arguments.IndexOf(versionArgument) == 1
                && versionList.Parent is InvocationExpressionSyntax historicalCall && Name(historicalCall.Expression) == "TryGenerate")
                yield return (literal, "DOA_FIXED_MISSION_POLICY", "A caller selects an old seed-only layout instead of reading the persisted generator version.");
            if (operand.Parent is BinaryExpressionSyntax fallback && fallback.IsKind(SyntaxKind.CoalesceExpression)
                && fallback.Right == operand && (IsContentIdentity(Name(fallback.Left))
                    || missions && MissionPolicyFields.Contains(Name(fallback.Left), StringComparer.Ordinal)))
                yield return (literal, "DOA_FALLBACK_CONTENT", "Missing gameplay content silently selects a fixed replacement.");
            if (operand.Parent is ConstantPatternSyntax pattern && (pattern.Parent is IsPatternExpressionSyntax condition
                    && IsContentIdentity(Name(condition.Expression)) || pattern.Parent is SwitchExpressionArmSyntax arm
                    && arm.Parent is SwitchExpressionSyntax selection && IsContentIdentity(Name(selection.GoverningExpression))))
                yield return (literal, "DOA_LITERAL_CONTENT_BRANCH", "A runtime pattern branches on a fixed gameplay identity.");
        }

        if (missions)
        {
            foreach (var import in syntax.DescendantNodes().OfType<UsingDirectiveSyntax>())
                if (import.Name?.ToString() is "MySqlConnector" or "System.Data.SqlClient" or "Microsoft.Data.SqlClient")
                    yield return (import, "DOA_MODULE_DEPENDENCY", "Mission mechanics use the persistence contracts instead of SQL providers.");
            foreach (var creation in syntax.DescendantNodes().OfType<ObjectCreationExpressionSyntax>())
                if (Name(creation.Type) is "MySqlConnection" or "SqlConnection" or "MySqlCharacterQuestDao" or "MySqlMissionDao")
                    yield return (creation, "DOA_MODULE_DEPENDENCY", "Mission mechanics must use injected persistence contracts.");
        }
    }

    // 8f517d711f6208c3308c06c68b414fd59a1316df introduced seed-only payloads with these exact values.
    // ProceduralDungeonFoundationTests.PreviouslyAcceptedV2SeedsKeepExactGeneratorPayloadAndSpawnBits
    // checks their output against Fixtures/ProceduralDungeonV2Golden.json. New versions cannot use this exception.
    internal static bool IsHistoricalLayoutContract(SyntaxNode syntax)
    {
        var declarations = syntax.DescendantNodes().OfType<VariableDeclaratorSyntax>().ToArray();
        foreach (var field in HistoricalLayoutFields)
        {
            VariableDeclaratorSyntax[] matches = declarations.Where(node => node.Identifier.ValueText == field.Key).ToArray();
            if (matches.Length != 1 || matches[0].Initializer?.Value is not LiteralExpressionSyntax number
                || number.Token.Value is not int value || value != field.Value
                || matches[0].Parent?.Parent is not FieldDeclarationSyntax declaration
                || !declaration.Modifiers.Any(SyntaxKind.ConstKeyword)) return false;
        }

        MethodDeclarationSyntax[] methods = syntax.DescendantNodes().OfType<MethodDeclarationSyntax>()
            .Where(method => method.Identifier.ValueText == "TryGenerate" && method.ParameterList.Parameters.Count == 4
                && method.ParameterList.Parameters[1].Identifier.ValueText == "version").ToArray();
        if (methods.Length != 1 || methods[0].Body?.Statements.Skip(1).FirstOrDefault() is not IfStatementSyntax guard
            || Tokens(guard) != Tokens(SyntaxFactory.ParseStatement("if (version != 2 || _generator == null) return false;")))
            return false;
        return syntax.DescendantNodes().OfType<IdentifierNameSyntax>()
            .Where(name => HistoricalLayoutFields.ContainsKey(name.Identifier.ValueText))
            .All(name => name.Ancestors().Contains(methods[0]));
    }

    internal static IEnumerable<Finding> CatalogIssues(Func<string, string?> read)
    {
        foreach (string path in new[] { "Missions/RollBodies.json", "Missions/RollTemplate.json" })
            if (read(path) is string bytes)
                yield return DataFinding(DataPath + path, path, "DOA_RECORDED_PACKET_CATALOG", "Recorded packets remain in runtime GameData.", Digest(bytes));

        foreach (string path in new[] { DungeonPolicy, "Missions/MissionOffers.json", "Missions/Keys.json" })
        {
            if (read(path) is not string json)
            {
                yield return DataFinding(DataPath + path, path, "DOA_MISSING_CATALOG", "Required typed mission catalog is missing.", path);
                continue;
            }
            if (!TryParse(json, out JsonDocument? parsed))
            {
                yield return DataFinding(DataPath + path, path, "DOA_INVALID_CATALOG", "Mission catalog is not a JSON object.", Digest(json));
                continue;
            }
            using (parsed)
            {
                JsonElement root = parsed!.RootElement;
                foreach (Finding finding in path switch
                {
                    DungeonPolicy => CoverageIssues(root),
                    "Missions/MissionOffers.json" => OfferIssues(root, path),
                    _ => KeyIssues(root, path)
                }) yield return finding;
            }
        }
    }

    internal static IEnumerable<Finding> AuthoritativeReferenceIssues(string root)
    {
        string[] files = [DungeonPolicy, "ItemTemplates.json", "NpcTemplates.json"];
        var documents = new List<JsonDocument>();
        try
        {
            foreach (string name in files)
            {
                string path = Path.Combine(root, name);
                if (File.Exists(path) && TryParse(File.ReadAllText(path), out JsonDocument? doc)) documents.Add(doc!);
                else yield return DataFinding(name, name, "DOA_UNAVAILABLE_AUTHORITATIVE_CATALOG", "Authoritative reference validation requires this catalog.", name);
            }
            if (documents.Count != files.Length) yield break;
            foreach (var pool in MissionPools(documents[0].RootElement))
            {
                JsonElement catalog = documents[pool.Npc ? 2 : 1].RootElement;
                foreach (string hash in Hashes(pool.Values).Distinct(StringComparer.Ordinal))
                    if (!HasResolvedLeaves(catalog, hash, new HashSet<string>(StringComparer.Ordinal)))
                        yield return DataFinding(DungeonPolicy, pool.Name, "DOA_UNRESOLVED_REFERENCE",
                            "A mission hash has a missing, empty or cyclic authoritative family.", hash);
            }
        }
        finally
        {
            foreach (JsonDocument document in documents) document.Dispose();
        }
    }

    private static IEnumerable<Finding> CoverageIssues(JsonElement root)
    {
        JsonElement coverage = Field(root, "ContentCoverage");
        foreach (var pool in MissionPools(root))
        {
            string[] hashes = Hashes(pool.Values);
            string fingerprint = pool.Values.ValueKind == JsonValueKind.Undefined ? "MISSING" : JsonSerializer.Serialize(pool.Values);
            if (hashes.Length == 0 || hashes.Length != pool.Values.GetArrayLength()
                || hashes.Any(hash => !IsHashName(hash)) || hashes.Distinct(StringComparer.Ordinal).Count() != hashes.Length)
            {
                yield return DataFinding(DataPath + DungeonPolicy, pool.Name, "DOA_INVALID_GAMEPLAY_POOL",
                    "Configured mission pools require distinct, nonempty item or NPC hashes.", fingerprint);
                continue;
            }
            JsonElement declaration = Field(coverage, pool.Name);
            string? status = StringField(declaration, "Status");
            if (status == "Provisional" && !string.IsNullOrWhiteSpace(StringField(declaration, "Gap"))) continue;
            if (status == "Complete" && hashes.Length > 1 && Hashes(Field(declaration, "Evidence")).Length > 0) continue;
            yield return DataFinding(DataPath + DungeonPolicy, pool.Name, "DOA_UNSUPPORTED_COMPLETENESS",
                "A mission pool needs an explicit provisional evidence gap or supported completeness evidence; singleton pools are provisional.",
                fingerprint + "|" + (declaration.ValueKind == JsonValueKind.Undefined ? "UNDECLARED" : JsonSerializer.Serialize(declaration)));
        }
    }

    private static IEnumerable<Finding> OfferIssues(JsonElement root, string path)
    {
        JsonElement responses = Field(root, "Responses");
        JsonElement provenance = Field(root, "Provenance");
        if (Number(root, "FormatVersion") != 1 || !IsHex(StringField(provenance, "InputSha256"), 64)
            || !IsHex(StringField(provenance, "SourceRevision"), 40)
            || responses.ValueKind != JsonValueKind.Array || responses.GetArrayLength() == 0
            || responses.EnumerateArray().Any(row => !IsHex(StringField(row, "BodySha256"), 64)
                || Field(row, "Offers") is not { ValueKind: JsonValueKind.Array } offers || offers.GetArrayLength() != 5
                || offers.EnumerateArray().Any(offer => offer.ValueKind != JsonValueKind.Object)))
            yield return DataFinding(DataPath + path, "Responses", "DOA_INVALID_MISSION_OFFERS",
                "Typed offers require format v1, provenance hashes and exactly five typed rows per response.", Digest(JsonSerializer.Serialize(root)));
        if (HasRecordedPayload(root))
            yield return DataFinding(DataPath + path, "Responses", "DOA_RECORDED_PACKET_CATALOG",
                "Mission offers may retain provenance hashes and typed fields. Recorded packet payloads belong in fixtures.", Digest(JsonSerializer.Serialize(root)));
    }

    private static IEnumerable<Finding> KeyIssues(JsonElement root, string path)
    {
        int? key = Number(root, "KeyTemplateId"), duplicator = Number(root, "DuplicatorTemplateId");
        if (Number(root, "Version") != 1 || key is not > 0 || duplicator is not > 0 || key == duplicator
            || Number(root, "KeyQuality") is not > 0 || Number(root, "MaxKeysPerQuest") is not > 0
            || string.IsNullOrWhiteSpace(StringField(root, "NamePrefix")))
            yield return DataFinding(DataPath + path, "Keys", "DOA_INVALID_MISSION_KEYS",
                "Mission keys require distinct configured identities, a quality, a limit and a name prefix.", Digest(JsonSerializer.Serialize(root)));
    }

    private static IEnumerable<(string Name, JsonElement Values, bool Npc)> MissionPools(JsonElement root)
    {
        yield return ("FindItem", Field(Field(root, "FindItem"), "ItemHashes"), false);
        yield return ("ReturnItem", Field(Field(root, "ReturnItem"), "ItemHashes"), false);
        JsonElement repair = Field(root, "RepairMachine");
        yield return ("RepairMachine", Field(Field(repair, "Source"), "ItemHashes"), false);
        yield return ("RepairMachine", Field(Field(repair, "Target"), "ItemHashes"), false);
        JsonElement objectives = Field(root, "NpcObjectives");
        yield return ("KillPerson", Field(objectives, "NpcHashes"), true);
        yield return ("FindPerson", Field(objectives, "FindPersonNpcHashes"), true);
        yield return ("Population", Field(Field(root, "Population"), "RegularNpcHashes"), true);
    }

    private static bool HasResolvedLeaves(JsonElement catalog, string hash, HashSet<string> branch)
    {
        if (branch.Count >= 64 || !branch.Add(hash)) return false;
        try
        {
            JsonElement row = Field(catalog, hash);
            JsonElement children = Field(row, "Children");
            if (children.ValueKind == JsonValueKind.Array && children.GetArrayLength() > 0)
            {
                string[] names = Hashes(children);
                return names.Length == children.GetArrayLength() && names.All(child => HasResolvedLeaves(catalog, child, branch));
            }
            JsonElement templates = Field(row, "Templates");
            return templates.ValueKind == JsonValueKind.Array && templates.GetArrayLength() > 0;
        }
        finally { branch.Remove(hash); }
    }

    private static bool HasRecordedPayload(JsonElement node)
    {
        if (node.ValueKind == JsonValueKind.Array) return node.EnumerateArray().Any(HasRecordedPayload);
        return node.ValueKind == JsonValueKind.Object && node.EnumerateObject().Any(property =>
            property.Name is "RawHex" or "BodyHex" or "PacketHex" or "PacketBytes" or "CapturedPacketHex" or "CapturedRollBodiesHex"
            || HasRecordedPayload(property.Value));
    }

    private static bool TryParse(string json, out JsonDocument? document)
    {
        try { document = JsonDocument.Parse(json.TrimStart('\uFEFF')); }
        catch (JsonException) { document = null; return false; }
        if (document.RootElement.ValueKind == JsonValueKind.Object) return true;
        document.Dispose();
        document = null;
        return false;
    }

    private static JsonElement Field(JsonElement node, string name)
        => node.ValueKind == JsonValueKind.Object && node.TryGetProperty(name, out JsonElement value) ? value : default;

    private static string? StringField(JsonElement node, string name)
        => Field(node, name) is { ValueKind: JsonValueKind.String } value ? value.GetString() : null;

    private static int? Number(JsonElement node, string name)
        => Field(node, name) is { ValueKind: JsonValueKind.Number } value && value.TryGetInt32(out int number) ? number : null;

    private static string[] Hashes(JsonElement node)
        => node.ValueKind == JsonValueKind.Array ? node.EnumerateArray().Where(value => value.ValueKind == JsonValueKind.String
            && !string.IsNullOrWhiteSpace(value.GetString())).Select(value => value.GetString()!).ToArray() : [];

    private static bool IsHashName(string value) => value.Length > 0 && value.All(character => char.IsAsciiLetterOrDigit(character) || character is '_' or '-');
    private static bool IsHex(string? value, int length) => value?.Length == length && value.All(Uri.IsHexDigit);
    private static bool IsContentIdentity(string name) => name.EndsWith("LowId", StringComparison.OrdinalIgnoreCase)
        || name.EndsWith("HighId", StringComparison.OrdinalIgnoreCase) || name.EndsWith("ItemId", StringComparison.OrdinalIgnoreCase)
        || name.EndsWith("TemplateId", StringComparison.OrdinalIgnoreCase)
        || name is "NpcHash" or "NPCId" or "NpcId" or "WorldItem" or "SpecItem";

    private static bool IsAuthoredLiteral(LiteralExpressionSyntax node) => node.Token.Value switch
    {
        int value => value > 0,
        uint value => value > 0,
        long value => value > 0,
        ulong value => value > 0,
        float value => value > 0,
        double value => value > 0,
        decimal value => value > 0,
        string value => node.IsKind(SyntaxKind.StringLiteralExpression) && value.Length > 0,
        _ => false
    };

    private static ExpressionSyntax UnwrapParent(ExpressionSyntax expression)
    {
        while (expression.Parent is ParenthesizedExpressionSyntax or CastExpressionSyntax)
            expression = (ExpressionSyntax)expression.Parent;
        return expression;
    }

    private static string? DirectTarget(ExpressionSyntax expression)
    {
        SyntaxNode current = UnwrapParent(expression);
        while (current.Parent is ExpressionElementSyntax or CollectionExpressionSyntax or ImplicitArrayCreationExpressionSyntax
            or ArrayCreationExpressionSyntax || current.Parent is InitializerExpressionSyntax initializer
                && initializer.IsKind(SyntaxKind.ArrayInitializerExpression))
            current = current.Parent;
        return current.Parent switch
        {
            EqualsValueClauseSyntax { Parent: VariableDeclaratorSyntax variable } => variable.Identifier.ValueText,
            EqualsValueClauseSyntax { Parent: PropertyDeclarationSyntax property } => property.Identifier.ValueText,
            AssignmentExpressionSyntax assignment when assignment.Right == current => Name(assignment.Left),
            _ => null
        };
    }

    private static string Name(SyntaxNode node) => node switch
    {
        SimpleNameSyntax name => name.Identifier.ValueText,
        MemberAccessExpressionSyntax member => member.Name.Identifier.ValueText,
        MemberBindingExpressionSyntax binding => binding.Name.Identifier.ValueText,
        ConditionalAccessExpressionSyntax conditional => Name(conditional.WhenNotNull),
        QualifiedNameSyntax qualified => qualified.Right.Identifier.ValueText,
        _ => string.Empty
    };

    private static string Tokens(SyntaxNode node) => string.Join("\n", node.DescendantTokens().Select(token => token.RawKind + ":" + token.Text));
    private static string Digest(string value) => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
}
