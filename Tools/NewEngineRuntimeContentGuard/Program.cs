using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

if (args.Length < 2 || args[0] is not ("--write" or "--check" or "--baseline" or "--self-test")) return 64;
string mode = args[0], root = Path.GetFullPath(args[1]);
if (mode == "--self-test") return GuardMutationTests.Run(root);
string? revision = mode == "--baseline" ? (args.Length > 2 ? args[2] : "HEAD") : null;
string Normalize(string value) => value.Replace('\\', '/');
string Rel(string value) => Normalize(Path.GetRelativePath(root, value));
string Git(params string[] arguments)
{
    var start = new ProcessStartInfo("git") { WorkingDirectory = root, RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false };
    foreach (string argument in arguments) start.ArgumentList.Add(argument);
    using var process = Process.Start(start)!;
    string result = process.StandardOutput.ReadToEnd(), error = process.StandardError.ReadToEnd();
    process.WaitForExit();
    if (process.ExitCode != 0) throw new InvalidOperationException(error);
    return result;
}
var tracked = Git("ls-tree", "-r", "--name-only", revision ?? "HEAD").Split('\n', StringSplitOptions.RemoveEmptyEntries).Select(s => s.TrimEnd('\r')).ToHashSet(StringComparer.Ordinal);
var texts = new Dictionary<string, string>(StringComparer.Ordinal);
string Read(string relative)
{
    if (texts.TryGetValue(relative, out string? text)) return text;
    text = revision == null ? File.ReadAllText(Path.Combine(root, relative)) : Git("show", revision + ":" + relative);
    // Repository checkouts may normalize LF to CRLF; semantic receipts must be portable.
    if (relative.EndsWith(".cs", StringComparison.Ordinal)) text = text.Replace("\r\n", "\n").Replace('\r', '\n');
    texts[relative] = text;
    return text;
}
bool Exists(string relative) => revision == null ? File.Exists(Path.Combine(root, relative)) : tracked.Contains(relative);
bool IsSource(string file) => file.EndsWith(".cs", StringComparison.Ordinal) && !file.Contains("/obj/") && !file.Contains("/bin/");
var projects = new SortedSet<string>(StringComparer.Ordinal);
var sourceFiles = new SortedSet<string>(StringComparer.Ordinal);
var unresolved = new SortedSet<string>(StringComparer.Ordinal);
var graphNotes = new SortedSet<string>(StringComparer.Ordinal);
IEnumerable<string> FilesUnder(string dir, string pattern = "*.cs") => revision != null ? tracked.Where(p => p.StartsWith(dir + "/", StringComparison.Ordinal))
    : Directory.EnumerateFiles(Path.Combine(root, dir), pattern, SearchOption.AllDirectories).Select(Rel);
string Resolve(string document, string value)
{
    value = value.Replace("$(AORebirthRepositoryRoot)", root).Replace("$(MSBuildThisFileDirectory)", Path.GetDirectoryName(Path.Combine(root, document)) + Path.DirectorySeparatorChar);
    if (value.Contains("$(", StringComparison.Ordinal)) { unresolved.Add(document + ": " + value); return string.Empty; }
    return Rel(Path.GetFullPath(Path.Combine(Path.GetDirectoryName(Path.Combine(root, document))!, value.Replace('\\', '/'))));
}
IEnumerable<string> Expand(string document, string value)
{
    foreach (string item in value.Split(';', StringSplitOptions.RemoveEmptyEntries))
    {
        string resolved = Resolve(document, item);
        if (resolved.Length == 0) continue;
        if (!resolved.Contains('*')) { yield return resolved; continue; }
        int wildcard = resolved.IndexOf('*'), slash = resolved.LastIndexOf('/', wildcard);
        string folder = resolved[..slash];
        string pattern = "^" + Regex.Escape(resolved).Replace(@"\*\*/", "(?:.*/)?").Replace(@"\*", "[^/]*") + "$";
        foreach (string match in FilesUnder(folder, "*").Where(f => Regex.IsMatch(f, pattern, RegexOptions.CultureInvariant))) yield return match;
    }
}
void ReadItems(string document, HashSet<string> visited, HashSet<string> projectSources)
{
    if (!visited.Add(document)) return;
    XDocument xml = XDocument.Parse(Read(document));
    foreach (var item in xml.Descendants())
    {
        if (item.Name.LocalName == "Import" && item.Attribute("Project") is { } import)
        {
            foreach (string path in Expand(document, import.Value))
                if (Exists(path)) ReadItems(path, visited, projectSources);
                else if (item.Attribute("Condition") == null) unresolved.Add("Missing project import: " + path);
                else graphNotes.Add("Conditional import unavailable: " + document + " -> " + path);
        }
        if (item.Name.LocalName == "Compile" && item.Attribute("Include") is { } include)
        {
            var excluded = item.Attribute("Exclude") is { } exclude
                ? Expand(document, exclude.Value).ToHashSet(StringComparer.Ordinal) : new HashSet<string>(StringComparer.Ordinal);
            foreach (string file in Expand(document, include.Value))
                if (!excluded.Contains(file))
                    if (Exists(file)) projectSources.Add(file); else unresolved.Add("Missing compile source: " + file);
        }
        if (item.Name.LocalName == "Compile" && item.Attribute("Remove") is { } remove)
        {
            // Unknown conditions must never make a possibly compiled file disappear.
            if (item.AncestorsAndSelf().Any(node => node.Attribute("Condition") != null))
                graphNotes.Add("Conditional Compile Remove retained conservatively: " + document + " -> " + remove.Value);
            else foreach (string file in Expand(document, remove.Value)) projectSources.Remove(file);
        }
        if (item.Name.LocalName == "ProjectReference" && item.Attribute("Include") is { } reference)
            foreach (string project in Expand(document, reference.Value))
                if (Exists(project)) AddProject(project); else unresolved.Add("Missing project reference: " + project);
    }
}
void AddProject(string project)
{
    if (!projects.Add(project)) return;
    XDocument xml = XDocument.Parse(Read(project));
    bool sdk = xml.Root!.Attribute("Sdk") != null;
    bool defaults = sdk && !xml.Descendants().Any(e => e.Name.LocalName == "EnableDefaultCompileItems" && e.Value.Trim() == "false"
        && !e.AncestorsAndSelf().Any(node => node.Attribute("Condition") != null));
    var projectSources = new HashSet<string>(StringComparer.Ordinal);
    if (defaults) foreach (string file in FilesUnder(Normalize(Path.GetDirectoryName(project)!)).Where(IsSource)) projectSources.Add(file);
    ReadItems(project, new(StringComparer.Ordinal), projectSources);
    // The same physical file may remain compiled by another referenced project.
    sourceFiles.UnionWith(projectSources);
}
AddProject("AORebirth/Server/ZoneEngine_New/ZoneEngine_New.csproj");

var contentName = new Regex(@"Scarlett|Buckethead|Accepted(?:Arete|Garden|Social|Subway)|DojaChip|FlintKneecapping|CapturedSubwayTailor|SparrowChild|MongoNano", RegexOptions.CultureInvariant);
var contentField = new Regex(@"(?:Npc|NPC|Nano|Quest|Vendor|Merchant|Template|Playfield|Mesh|Texture|Weapon|Reward|Item)(?:Id|Ids|Hash|Name|Template|Stock|Pool)|(?:Scarlett|Buckethead|Sarah|Stan|Arete)(?:Id|Instance|Position|Rotation|Name)|^(?:SpawnHash|MobHash|LowId|HighId|HeadMesh|CATMesh|MonsterData|NpcFamily)$", RegexOptions.CultureInvariant);
var findings = new List<Finding>();
var files = new List<object>();
string Category(string text)
{
    if (Regex.IsMatch(text, "Dialogue|Conversation", RegexOptions.IgnoreCase)) return "HARDCODED_DIALOGUE_CONTENT";
    if (Regex.IsMatch(text, "Vendor|Merchant|Shop|Stock", RegexOptions.IgnoreCase)) return "HARDCODED_VENDOR_CONTENT";
    if (Regex.IsMatch(text, "Quest|Doja|Sarah|Stan|Reward", RegexOptions.IgnoreCase)) return "HARDCODED_QUEST_CONTENT";
    if (Regex.IsMatch(text, "Mission", RegexOptions.IgnoreCase)) return "HARDCODED_MISSION_CONTENT";
    if (Regex.IsMatch(text, "Weapon|Pistol|Rifle", RegexOptions.IgnoreCase)) return "HARDCODED_WEAPON_CONTENT";
    if (Regex.IsMatch(text, "Appearance|Mesh|Texture|Morph", RegexOptions.IgnoreCase)) return "HARDCODED_APPEARANCE_CONTENT";
    if (Regex.IsMatch(text, "Spawn|Placement|Position|Rotation|Playfield", RegexOptions.IgnoreCase)) return "HARDCODED_SPAWN_CONTENT";
    if (Regex.IsMatch(text, "Npc|Mob|Scarlett", RegexOptions.IgnoreCase)) return "HARDCODED_NPC_CONTENT";
    if (Regex.IsMatch(text, "Item", RegexOptions.IgnoreCase)) return "HARDCODED_ITEM_CONTENT";
    return "HARDCODED_OTHER_GAME_CONTENT";
}
foreach (string file in sourceFiles.Where(IsSource))
{
    string source = Read(file);
    var syntax = CSharpSyntaxTree.ParseText(source, new CSharpParseOptions(LanguageVersion.Preview,
        preprocessorSymbols: ["DEBUG", "TRACE", "AOREBIRTH_WIN_NET10"]), path: file).GetRoot();
    var found = new List<Finding>();
    bool protocol = file.Contains("/AOtomation/") || file.Contains("/msgpack-cli/") || file.Contains("/Cell.Core/") || file.Contains("/Cell.Util/");
    void Add(SyntaxNode node, string rule, string basis)
    {
        var member = node.AncestorsAndSelf().OfType<MemberDeclarationSyntax>().FirstOrDefault();
        string symbol = member switch { MethodDeclarationSyntax m => m.Identifier.Text, TypeDeclarationSyntax t => t.Identifier.Text,
            PropertyDeclarationSyntax p => p.Identifier.Text, FieldDeclarationSyntax f => string.Join(",", f.Declaration.Variables.Select(v => v.Identifier.Text)), _ => string.Empty };
        int line = node.GetLocation().GetLineSpan().StartLinePosition.Line + 1;
        if (found.Any(f => f.Line == line && f.Rule == rule)) return;
        string excerpt = node.ToString().Replace("\r", " ").Replace("\n", " ");
        found.Add(new(file, line, symbol, Category(file + " " + symbol + " " + node), rule, basis,
            excerpt[..Math.Min(180, excerpt.Length)]));
    }
    foreach (var member in syntax.DescendantNodes().OfType<MemberDeclarationSyntax>())
    {
        string name = member switch { TypeDeclarationSyntax t => t.Identifier.Text, MethodDeclarationSyntax m => m.Identifier.Text, _ => "" };
        if (contentName.IsMatch(name)) Add(member, "CONTENT_SPECIFIC_RUNTIME_TYPE", "Named content behavior/bridge requires migration or semantic review.");
    }
    foreach (var literal in syntax.DescendantNodes().OfType<LiteralExpressionSyntax>())
    {
        if (literal.Ancestors().Any(n => n is EnumDeclarationSyntax or AttributeSyntax)) continue;
        var variable = literal.Ancestors().OfType<VariableDeclaratorSyntax>().FirstOrDefault();
        var property = literal.Ancestors().OfType<PropertyDeclarationSyntax>().FirstOrDefault();
        var assignment = literal.Ancestors().OfType<AssignmentExpressionSyntax>().FirstOrDefault();
        string target = assignment?.Left.ToString() ?? variable?.Identifier.Text ?? property?.Identifier.Text ?? "";
        string leaf = target.Split('.').Last();
        bool number = literal.IsKind(SyntaxKind.NumericLiteralExpression) && double.TryParse(literal.Token.Value?.ToString(), out double value) && value > 1;
        bool text = literal.IsKind(SyntaxKind.StringLiteralExpression) && literal.Token.ValueText.Length > 0;
        if (!number && !text) continue;
        bool infrastructureName = leaf.EndsWith("FileName", StringComparison.Ordinal) || leaf.EndsWith("DirectoryName", StringComparison.Ordinal)
            || (file.Contains("/AORebirth.Database/") && text && literal.Token.ValueText.StartsWith("system.", StringComparison.Ordinal));
        // An argument to a decoder/reader call is an index, not the assigned content value.
        bool directValue = !literal.Ancestors().TakeWhile(n => n != assignment && n != variable && n != property)
            .Any(n => n is InvocationExpressionSyntax or ElementAccessExpressionSyntax);
        if (contentField.IsMatch(leaf) && !infrastructureName && directValue)
            Add(literal, "CONTENT_LITERAL_BINDING", "Content identity/assignment value is embedded in a runtime declaration or initializer.");
        if (directValue && literal.Ancestors().OfType<ObjectCreationExpressionSyntax>().Any(creation =>
            Regex.IsMatch(creation.Type.ToString(), @"(?:^|\.)(?:MobTemplate|WorldNpcDefinition|WorldShopDefinition|WorldVendorStock|QuestDefinition|MissionDefinition|DialogueNode|DialogueOption|NpcFamilyStatTemplate)$", RegexOptions.CultureInvariant)))
            Add(literal, "COMPILED_CONTENT_RECORD", "A concrete content-model record is initialized with fixed runtime values.");
        if (text && contentName.IsMatch(literal.Token.ValueText) && assignment != null
            && Regex.IsMatch(leaf, "Name|Title|Text|Description|Route", RegexOptions.CultureInvariant))
            Add(literal, "NAMED_CONTENT_TEXT", "Specific game content text assigned in runtime C#.");
        if (number && literal.Ancestors().OfType<BinaryExpressionSyntax>().FirstOrDefault() is { } binary
            && binary.Kind() is SyntaxKind.EqualsExpression or SyntaxKind.NotEqualsExpression
            && (contentField.IsMatch(binary.Left.ToString().Split('.').Last()) || contentField.IsMatch(binary.Right.ToString().Split('.').Last())))
            Add(literal, "CONTENT_ID_BRANCH", "Runtime branch selects a specific content identity.");
        if (number && literal.Ancestors().OfType<SwitchStatementSyntax>().FirstOrDefault() is { } statement
            && literal.Ancestors().OfType<CaseSwitchLabelSyntax>().Any()
            && contentField.IsMatch(statement.Expression.ToString().Split('.').Last()))
            Add(literal, "CONTENT_ID_SWITCH", "Runtime switch selects a specific content identity.");
        if (number && literal.Ancestors().OfType<InvocationExpressionSyntax>().FirstOrDefault() is { } call
            && call.Expression.ToString().EndsWith(".Stats.Set", StringComparison.Ordinal)
            && (literal.Ancestors().OfType<TypeDeclarationSyntax>().Any(t => Regex.IsMatch(t.Identifier.Text, "Npc|NPC|Mob|Spawn|Vendor|Merchant", RegexOptions.CultureInvariant))
                || literal.Ancestors().OfType<MethodDeclarationSyntax>().Any(m => m.ParameterList.Parameters.Any(p => p.Type?.ToString().Contains("Npc", StringComparison.Ordinal) == true))))
            Add(literal, "COMPILED_NPC_STAT", "Runtime constructor assigns a fixed entity stat.");
        if (text && literal.Token.ValueText.Length > 256 && Regex.IsMatch(literal.Token.ValueText, "^[A-Za-z0-9+/=]+$")
            ) Add(literal, "COMPILED_CONTENT_PAYLOAD", "Opaque content payload embedded in runtime source.");
    }
    findings.AddRange(found);
    files.Add(new { path = file, sha256 = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(source))),
        classification = found.Count > 0 ? found.Select(f => f.Classification).Distinct().Order().ToArray()
            : new[] { protocol || syntax.DescendantNodes().OfType<EnumDeclarationSyntax>().Any() ? "GENERIC_PROTOCOL_CONSTANT" : "GENERIC_RUNTIME_LOGIC" },
        candidateCount = found.Count, reviewBasis = "Roslyn syntax and declaration-context scan; domain semantic audit is recorded in the bridge inventory." });
}
findings = findings.OrderBy(f => f.Path, StringComparer.Ordinal).ThenBy(f => f.Line).ThenBy(f => f.Rule, StringComparer.Ordinal).ToList();
var report = new { schemaVersion = 2, scope = "Conservative static NewEngine project-reference source graph including SDK default files, explicit imported Compile items, linked shared and library sources; tests/tools are not runtime.",
    sourceRevision = revision, runtimeCSharpFilesAudited = files.Count, totalRuntimeContentViolations = findings.Count,
    sourceGraphMethod = "Static project XML expansion, not an evaluated MSBuild compilation manifest. Conditional includes are included conservatively; conditional removals do not hide sources. SDK-generated sources and package binaries are outside this source audit.",
    sourceGraphLimitations = new[] { "Implicit SDK/Directory.Build imports, target-generated Compile items and arbitrary MSBuild property functions are not evaluated.",
        "C# scan uses DEBUG, TRACE and AOREBIRTH_WIN_NET10; inactive branches under other symbol sets require a separate profile audit." },
    sourceGraphNotes = graphNotes.ToArray(),
    classificationMethod = "Contextual syntax candidates plus separately recorded domain semantic review. No filename-wide grandfathered violation allowlist.",
    unresolvedProjectInputs = unresolved.ToArray(), projects = projects.ToArray(), files, findings };
string output = revision != null ? "tools-temp/content-cleanup-001/baseline-content-audit.json" : "docs/reports/NEWENGINE_CONTENT_ARCHITECTURE_GUARD.json";
int outputArgument = Array.IndexOf(args, "--output");
if (outputArgument >= 0)
{
    if (outputArgument + 1 >= args.Length) return 64;
    output = args[outputArgument + 1];
}
string serialized = JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true }).Replace("\r\n", "\n") + "\n";
if (mode == "--check")
{
    if (!File.Exists(Path.Combine(root, output)) || File.ReadAllText(Path.Combine(root, output)) != serialized)
        { Console.WriteLine("CONTENT_ARCHITECTURE_GUARD=STALE"); return 2; }
}
else { Directory.CreateDirectory(Path.GetDirectoryName(Path.Combine(root, output))!); File.WriteAllText(Path.Combine(root, output), serialized, new UTF8Encoding(false)); }
Console.WriteLine($"RUNTIME_CSHARP_FILES_AUDITED={files.Count}");
Console.WriteLine($"NEWENGINE_RUNTIME_CONTENT_VIOLATIONS={findings.Count}");
Console.WriteLine($"UNRESOLVED_PROJECT_INPUTS={unresolved.Count}");
return revision != null ? 0 : findings.Count == 0 && unresolved.Count == 0 ? 0 : 1;

internal sealed record Finding(string Path, int Line, string Symbol, string Classification, string Rule, string Basis, string Excerpt);

internal static class GuardMutationTests
{
    internal static int Run(string root)
    {
        string directory = Path.Combine(root, "AORebirth", "Server", "ZoneEngine_New");
        string injected = Path.Combine(directory, "GuardMutation_" + Guid.NewGuid().ToString("N") + ".cs");
        string report = Path.Combine(root, "docs", "reports", "NEWENGINE_CONTENT_ARCHITECTURE_GUARD.json");
        byte[]? previous = File.Exists(report) ? File.ReadAllBytes(report) : null;
        var cases = new (string Name, string Source, bool Blocked)[]
        {
            ("NpcRecord", "class TemporaryContent { object row = new MobTemplate { Name = \"EditorConfiguredNpc\", Hash = \"TST1\" }; }", true),
            ("VendorStock", "class TemporaryContent { object row = new WorldVendorStock { LowId = 12345, HighId = 23456, Quality = 12 }; }", true),
            ("QuestIdentity", "class TemporaryContent { const int QuestId = 31234; }", true),
            ("ReversedIdentityBranch", "class TemporaryContent { bool Match(int ItemId) => 31234 == ItemId; }", true),
            ("IdentitySwitch", "class TemporaryContent { int Pick(int NanoId) { switch (NanoId) { case 31234: return 1; default: return 0; } } }", true),
            ("ProtocolAndReader", "enum WireType { Example = 0xDAC3 } class TemporaryMechanic { int Read(System.Data.IDataRecord row) { return row.GetInt32(5); } }", false)
        };
        var results = new List<object>();
        bool passed = true;
        try
        {
            foreach (var test in cases)
            {
                File.WriteAllText(injected, test.Source);
                var start = new ProcessStartInfo("dotnet") { WorkingDirectory = root, RedirectStandardOutput = true,
                    RedirectStandardError = true, UseShellExecute = false };
                start.ArgumentList.Add(typeof(GuardMutationTests).Assembly.Location);
                start.ArgumentList.Add("--write"); start.ArgumentList.Add(root);
                using var process = Process.Start(start)!;
                _ = process.StandardOutput.ReadToEnd(); string error = process.StandardError.ReadToEnd(); process.WaitForExit();
                if (process.ExitCode is not (0 or 1)) throw new InvalidOperationException(error);
                using JsonDocument parsed = JsonDocument.Parse(File.ReadAllText(report));
                string relative = Path.GetRelativePath(root, injected).Replace('\\', '/');
                int count = parsed.RootElement.GetProperty("findings").EnumerateArray().Count(f => f.GetProperty("Path").GetString() == relative);
                bool ok = (count > 0) == test.Blocked;
                passed &= ok;
                results.Add(new { test.Name, expectedBlocked = test.Blocked, actualCandidates = count, pass = ok });
            }
        }
        finally
        {
            if (File.Exists(injected)) File.Delete(injected);
            if (previous != null) File.WriteAllBytes(report, previous); else if (File.Exists(report)) File.Delete(report);
        }
        string receipt = Path.Combine(root, "tools-temp", "content-cleanup-001", "content-guard-selftest.json");
        Directory.CreateDirectory(Path.GetDirectoryName(receipt)!);
        File.WriteAllText(receipt, JsonSerializer.Serialize(new { pass = passed, cases = results }, new JsonSerializerOptions { WriteIndented = true }) + "\n");
        Console.WriteLine("CONTENT_GUARD_MUTATION_TESTS=" + (passed ? "PASS" : "FAIL"));
        return passed ? 0 : 1;
    }
}
