using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

if (args.Length != 2 || args[0] is not ("--write" or "--check")) return 64;
string root = Path.GetFullPath(args[1]);
string engine = "AORebirth/Server/ZoneEngine_New";
string legacy = "AORebirth/Server/ZoneEngine/";
string Rel(string path) => Path.GetRelativePath(root, path).Replace('\\', '/');
string Full(string path) => Path.GetFullPath(Path.Combine(root, path));
string Hash(string path) => Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(path)));
SyntaxNode Parse(string path) => CSharpSyntaxTree.ParseText(File.ReadAllText(path),
    new CSharpParseOptions(LanguageVersion.Latest, preprocessorSymbols: ["DEBUG", "TRACE", "AOREBIRTH_WIN_NET10"]), path: Rel(path)).GetRoot();
bool Source(string path) => !path.Replace('\\', '/').Contains("/obj/") && !path.Replace('\\', '/').Contains("/bin/");
var files = Directory.GetFiles(Full(engine), "*.cs", SearchOption.AllDirectories).Where(Source).Order(StringComparer.Ordinal).ToArray();
var syntax = files.ToDictionary(Rel, Parse, StringComparer.Ordinal);
var identifierConsumers = syntax.ToDictionary(pair => pair.Key,
    pair => pair.Value.DescendantNodes().OfType<IdentifierNameSyntax>().Select(n => n.Identifier.ValueText).ToHashSet(StringComparer.Ordinal));

// Follow the NewEngine project-reference graph, including conditional platform
// companions conservatively. Do not count unrelated Legacy-only build projects.
var projects = new SortedSet<string>(StringComparer.Ordinal);
void AddProject(string path)
{
    path = Path.GetFullPath(path);
    if (!projects.Add(path)) return;
    foreach (var item in XDocument.Load(path).Descendants().Where(e => e.Name.LocalName == "ProjectReference"))
    {
        string include = (item.Attribute("Include")?.Value ?? "").Replace("$(AORebirthRepositoryRoot)", root).Replace('\\', '/');
        if (include.Length == 0 || include.Contains("$(", StringComparison.Ordinal))
            throw new InvalidOperationException("Unresolved project reference: " + Rel(path) + ": " + include);
        AddProject(Path.Combine(Path.GetDirectoryName(path)!, include));
    }
}
AddProject(Full(engine + "/ZoneEngine_New.csproj"));
var engineTrees = syntax.Values.Select(n => n.SyntaxTree).ToList();
foreach (var item in XDocument.Load(Full(engine + "/ZoneEngine_New.csproj")).Descendants().Where(e => e.Name.LocalName == "Compile"))
{
    string include = (item.Attribute("Include")?.Value ?? "").Replace("$(AORebirthRepositoryRoot)", root).Replace('\\', '/');
    if (include.Length == 0 || include.Contains("$(") || include.Contains('*')) continue;
    string source = Path.GetFullPath(Path.Combine(Full(engine), include));
    if (engineTrees.All(t => t.FilePath != Rel(source))) engineTrees.Add(Parse(source).SyntaxTree);
}
string runtimeDirectory = Full("AORebirth/Built/Debug/ZoneEngine_New");
var referencePaths = ((string?)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES") ?? "").Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries)
    .Concat(Directory.GetFiles(runtimeDirectory, "*.dll").Where(p => Path.GetFileName(p) != "ZoneEngine_New.dll"))
    .GroupBy(Path.GetFileName, StringComparer.OrdinalIgnoreCase).Select(g => g.First());
var compilation = CSharpCompilation.Create("ZoneEngine_New", engineTrees,
    referencePaths.Select(p => MetadataReference.CreateFromFile(p)), new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, allowUnsafe: true));
var resolvedUses = new Dictionary<string, SortedDictionary<string, SortedSet<string>>>(StringComparer.Ordinal);
foreach (var tree in engineTrees)
{
    var model = compilation.GetSemanticModel(tree);
    foreach (var identifier in tree.GetRoot().DescendantNodes().OfType<IdentifierNameSyntax>())
    {
        var symbol = model.GetSymbolInfo(identifier).Symbol;
        if (symbol == null) continue;
        var owner = symbol as INamedTypeSymbol ?? symbol.ContainingType;
        if (owner == null) continue;
        foreach (var definition in owner.OriginalDefinition.DeclaringSyntaxReferences)
        {
            string source = definition.SyntaxTree.FilePath;
            if (!source.StartsWith(legacy, StringComparison.Ordinal) || source == tree.FilePath) continue;
            if (!resolvedUses.TryGetValue(source, out var consumers)) resolvedUses[source] = consumers = new(StringComparer.Ordinal);
            if (!consumers.TryGetValue(tree.FilePath, out var symbols)) consumers[tree.FilePath] = symbols = new(StringComparer.Ordinal);
            symbols.Add(symbol.ToDisplayString());
        }
    }
}
var semanticErrors = compilation.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error)
    .Select(d => new { id = d.Id, file = d.Location.SourceTree?.FilePath, line = d.Location.GetLineSpan().StartLinePosition.Line + 1 })
    .OrderBy(d => d.file, StringComparer.Ordinal).ThenBy(d => d.line).ThenBy(d => d.id, StringComparer.Ordinal).ToArray();
var dependencies = new List<object>();
foreach (string project in projects)
{
    string relativeProject = Rel(project);
    if (relativeProject.StartsWith("tools-temp/", StringComparison.Ordinal)) continue;
    XDocument xml = XDocument.Load(project);
    foreach (var compile in xml.Descendants().Where(e => e.Name.LocalName == "Compile" && e.Attribute("Include") != null))
    {
        string include = compile.Attribute("Include")!.Value.Replace("$(AORebirthRepositoryRoot)", root).Replace('\\', '/');
        if (include.Contains("$(", StringComparison.Ordinal) || include.Contains('*')) continue;
        string source = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(project)!, include));
        if (!Rel(source).StartsWith(legacy, StringComparison.Ordinal)) continue;
        if (!File.Exists(source)) throw new InvalidOperationException("Missing linked Legacy source: " + Rel(source));
        SyntaxNode tree = Parse(source);
        string[] symbols = tree.DescendantNodes().OfType<BaseTypeDeclarationSyntax>().Select(n => n.Identifier.ValueText)
            .Concat(tree.DescendantNodes().OfType<DelegateDeclarationSyntax>().Select(n => n.Identifier.ValueText))
            .Distinct().Order(StringComparer.Ordinal).ToArray();
        var uses = identifierConsumers.Where(pair => symbols.Any(pair.Value.Contains)).OrderBy(pair => pair.Key, StringComparer.Ordinal)
            .Select(pair => new { file = pair.Key, symbols = symbols.Where(pair.Value.Contains).ToArray() }).ToArray();
        string link = compile.Elements().FirstOrDefault(e => e.Name.LocalName == "Link")?.Value ?? compile.Attribute("Link")?.Value ?? "";
        bool contentCarrier = Rel(source).Contains(".Data.cs") || Rel(source).EndsWith(".g.cs") || Path.GetFileName(source).StartsWith("Captured", StringComparison.Ordinal);
        string classification = contentCarrier ? "SHARED_MODEL"
            : link.Contains("Packets", StringComparison.OrdinalIgnoreCase) || Rel(source).Contains("/Packets/") ? "SHARED_PROTOCOL"
            : symbols.Any(s => s.StartsWith('I') && s.Length > 1 && char.IsUpper(s[1])) && tree.DescendantNodes().OfType<InterfaceDeclarationSyntax>().Any() ? "SHARED_CONTRACT"
            : Rel(source).Contains("Models") || Rel(source).Contains("Model.cs") ? "SHARED_MODEL"
            : Rel(source).Contains("Loader") || Rel(source).Contains("Catalog") || Rel(source).Contains("Registry") ? "SHARED_DATA_LOADER"
            : Rel(source).Contains("Bootstrap") || Rel(source).Contains("NoOp") ? "LEGACY_SPECIFIC"
            : Rel(source).Contains("Rules") || Rel(source).Contains("Policy") || Rel(source).Contains("Validator") || Rel(source).Contains("Service") || Rel(source).Contains("Window") ? "SHARED_GAMEPLAY_MECHANIC" : "UNKNOWN";
        dependencies.Add(new { legacySourceFile = Rel(source), consumerProject = relativeProject, sourceSha256 = Hash(source), declaredSymbols = symbols,
            symbolsUsed = resolvedUses.GetValueOrDefault(Rel(source)), symbolsUsedCandidates = uses,
            symbolResolution = semanticErrors.Length == 0 ? "ROSLYN_RESOLVED_ENGINE_COMPILATION" : "PARTIAL_ROSLYN_RESOLUTION_SEE_DIAGNOSTICS", classification,
            contentSeparationRequired = contentCarrier,
            classificationBasis = "conservative extraction category; UNKNOWN and LEGACY_SPECIFIC require owner review before movement",
            recommendedDestination = contentCarrier ? "validated content data with shared neutral model/loader; do not move embedded content into NewEngine C#"
                : classification == "SHARED_PROTOCOL" ? "existing shared communication/protocol layer"
                : classification == "SHARED_CONTRACT" ? "AORebirth.Interfaces or existing neutral domain contract layer"
                : "neutral shared domain module after content/mechanic classification; never copy into NewEngine",
            newEngineProjectDirectLink = relativeProject == engine + "/ZoneEngine_New.csproj" });
    }
}

var persistence = new List<object>();
var sqlTokens = new Regex(@"\b(?:SELECT|INSERT|UPDATE|DELETE|REPLACE|CREATE|ALTER|FROM|JOIN|INTO)\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
var tableTokens = new Regex(@"\b(?:FROM|JOIN|INTO|UPDATE|TABLE)\s+`?([A-Za-z_][A-Za-z_0-9]*)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
foreach (var pair in syntax.OrderBy(p => p.Key, StringComparer.Ordinal))
{
    var tree = pair.Value;
    string[] classSql = tree.DescendantNodes().OfType<LiteralExpressionSyntax>().Where(n => n.IsKind(SyntaxKind.StringLiteralExpression))
        .Select(n => n.Token.ValueText).Where(s => sqlTokens.IsMatch(s)).ToArray();
    var fields = tree.DescendantNodes().OfType<FieldDeclarationSyntax>().SelectMany(field => field.Declaration.Variables.Select(v => new { name = v.Identifier.ValueText, type = field.Declaration.Type.ToString() }))
        .GroupBy(f => f.name).ToDictionary(g => g.Key, g => string.Join(";", g.Select(f => f.type).Distinct()));
    foreach (var method in tree.DescendantNodes().OfType<BaseMethodDeclarationSyntax>())
    {
        string text = method.ToString();
        string[] calls = method.DescendantNodes().OfType<InvocationExpressionSyntax>().Select(i => i.Expression.ToString()).Distinct().Order(StringComparer.Ordinal).ToArray();
        string[] creations = method.DescendantNodes().OfType<ObjectCreationExpressionSyntax>().Select(n => n.Type.ToString()).Distinct().Order(StringComparer.Ordinal).ToArray();
        var methodVariables = new Dictionary<string, string>(fields);
        foreach (var parameter in method.ParameterList.Parameters) methodVariables[parameter.Identifier.ValueText] = parameter.Type?.ToString() ?? "";
        string[] receivers = method.DescendantNodes().OfType<InvocationExpressionSyntax>().Select(i => i.Expression).OfType<MemberAccessExpressionSyntax>()
            .Select(m => m.Expression.ToString()).Where(methodVariables.ContainsKey).Select(f => methodVariables[f]).Distinct().Order(StringComparer.Ordinal).ToArray();
        bool direct = creations.Any(t => t.Contains("MySql") || t.Contains("SqlConnection") || t.Contains("SqlCommand"))
            || calls.Any(c => c.Contains("Connector.") || c.EndsWith(".ExecuteReader") || c.EndsWith(".ExecuteNonQuery") || c.EndsWith(".ExecuteScalar"));
        bool fileAccess = calls.Any(c => c.StartsWith("File.") || c.StartsWith("Directory.") || c.StartsWith("JsonSerializer."))
            || creations.Any(c => c is "FileStream" or "StreamReader" or "StreamWriter");
        string[] repositoryTypes = receivers.Where(t => Regex.IsMatch(t, "Repository|Persistence|Dao|CoalesceCommit|InstanceIdAllocator|HydrationService|SnapshotService|InventoryFlushService")).ToArray();
        bool composed = creations.Any(t => Regex.IsMatch(t, "^MySql|Dao$"));
        if (!direct && !fileAccess && repositoryTypes.Length == 0 && !composed) continue;
        string[] localSql = method.DescendantNodes().OfType<LiteralExpressionSyntax>().Where(n => n.IsKind(SyntaxKind.StringLiteralExpression)).Select(n => n.Token.ValueText).Where(s => sqlTokens.IsMatch(s)).ToArray();
        string[] tables = (direct ? classSql : localSql).SelectMany(s => tableTokens.Matches(s).Select(m => m.Groups[1].Value.ToLowerInvariant()))
            .Where(s => s is not ("set" or "if" or "where" or "exists")).Distinct().Order(StringComparer.Ordinal).ToArray();
        string name = method switch { MethodDeclarationSyntax m => m.Identifier.ValueText, ConstructorDeclarationSyntax c => c.Identifier.ValueText, _ => method.Kind().ToString() };
        string type = method.Ancestors().OfType<TypeDeclarationSyntax>().FirstOrDefault()?.Identifier.ValueText ?? "<top-level>";
        string transaction = calls.Any(c => c.EndsWith(".BeginTransaction")) ? "OWNS_TRANSACTION_REVIEW_COMMIT_AND_FAILURE_PATHS"
            : method.ParameterList.Parameters.Any(p => p.Type?.ToString().Contains("Transaction") == true) ? "CALLER_TRANSACTION"
            : repositoryTypes.Length > 0 ? "DELEGATED_TO_RECORDED_REPOSITORY; SEE IMPLEMENTATION ROWS"
            : direct ? "NO_METHOD_OWNED_TRANSACTION; READ_OR_SINGLE_WRITE_REQUIRES_REVIEW" : "NOT_SQL_TRANSACTION";
        persistence.Add(new { file = pair.Key, sourceSha256 = Hash(Full(pair.Key)), @class = type, method = name,
            signature = method.ParameterList.ToString(), line = method.GetLocation().GetLineSpan().StartLinePosition.Line + 1,
            tables, tableResolution = direct ? "FILE_SQL_LITERAL_CANDIDATES_INCLUDES_CONSTANTS; DYNAMIC_OR_HELPER_SQL_REQUIRES_REVIEW" : "DELEGATED_OR_FILE_ACCESS",
            operation = direct ? "DIRECT_SQL" : composed ? "COMPOSITION" : fileAccess ? "FILE_OR_SERIALIZATION" : "REPOSITORY_CONSUMER",
            transactionalBehavior = transaction, existingEquivalent = repositoryTypes, calls,
            migrationTarget = pair.Key.Contains("Mission") ? "reuse IMissionDao/IGeneratedMissionDao and IMissionInventoryMutationTransaction; preserve existing mission aggregate"
                : pair.Key.Contains("Inventory") || pair.Key.Contains("Trade") || pair.Key.Contains("ItemInstance") ? "shared inventory DAO and existing aggregate trade/inventory transaction owner; no independent participant commits"
                : pair.Key.Contains("Nano") ? "character nano DAO with aggregate nano/stat transaction"
                : pair.Key.Contains("Character") || pair.Key.Contains("Stat") || pair.Key.Contains("Spawn") ? "character/stat DAO; reuse ICharacterDao directory foundation without conflating it with snapshots or leases"
                : fileAccess ? "existing shared validated content loader; not forced into SQL" : "existing composition/readiness boundary; separate provider construction from gameplay",
            cutoverCritical = direct || repositoryTypes.Length > 0 ? "YES" : "NO",
            resolution = "SYNTAX_INVENTORY_NOT_A_PROOF_OF_ATOMICITY" });
    }
}

var options = new JsonSerializerOptions { WriteIndented = true };
bool Write(string name, object value)
{
    string path = Full("docs/reports/" + name + ".json");
    byte[] bytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(value, options).Replace("\r\n", "\n") + "\n");
    if (args[0] == "--check") return File.Exists(path) && File.ReadAllBytes(path).SequenceEqual(bytes);
    Directory.CreateDirectory(Path.GetDirectoryName(path)!); File.WriteAllBytes(path, bytes); return true;
}
bool ok = semanticErrors.Length == 0;
ok &= Write("NEWENGINE_LEGACY_DEPENDENCY_INVENTORY", new { schemaVersion = 1, source = "repository source hashes; no timestamps or machine paths", dependencyEdges = dependencies.Count, semanticErrors, dependencies });
ok &= Write("NEWENGINE_DAO_GAP_INVENTORY", new { schemaVersion = 1, sourceFilesScanned = files.Length, persistenceMethodRows = persistence.Count, entries = persistence });
using var matrix = JsonDocument.Parse(File.ReadAllText(Full("docs/reports/NEWENGINE_SUPPORTED_FEATURE_MATRIX.json")));
var features = matrix.RootElement.GetProperty("features").EnumerateArray().ToArray();
if (features.Select(f => f.GetProperty("feature").GetString()).Distinct().Count() != features.Length) ok = false;
foreach (var feature in features)
{
    if (feature.GetProperty("status").GetString() is not ("SUPPORTED" or "PARTIALLY_SUPPORTED_FAIL_CLOSED" or "UNSUPPORTED_FAIL_CLOSED" or "NOT_RELEVANT_TO_CUTOVER")) ok = false;
    foreach (var test in feature.GetProperty("tests").EnumerateArray())
        if (!File.Exists(Full("AORebirth/Server/ZoneEngine_New.Tests/" + test.GetString()))) ok = false;
}
Console.WriteLine($"CUTOVER_INVENTORIES={(ok ? "PASS" : "FAIL")} LEGACY_DEPENDENCY_EDGES={dependencies.Count} PERSISTENCE_METHOD_ROWS={persistence.Count}");
return ok ? 0 : 1;
