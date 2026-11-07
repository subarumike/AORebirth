using System.Text.Json;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

internal static class DoaMutationTests
{
    private const string MissionPath = "AORebirth/Server/ZoneEngine_New/Core/Missions/GuardProbe.cs";
    private const string Generation = """
        {
          "FindItem": { "ItemHashes": ["FIND"] },
          "ReturnItem": { "ItemHashes": ["RETN"] },
          "RepairMachine": { "Source": { "ItemHashes": ["TOOL"] }, "Target": { "ItemHashes": ["FIXD"] } },
          "NpcObjectives": { "NpcHashes": ["BOSS"], "FindPersonNpcHashes": ["BOSS"] },
          "Population": { "RegularNpcHashes": ["BOSS"] },
          "ContentCoverage": {
            "FindItem": { "Status": "Provisional", "Gap": "More verified objective variants" },
            "ReturnItem": { "Status": "Provisional", "Gap": "More verified objective variants" },
            "RepairMachine": { "Status": "Provisional", "Gap": "More verified pairs" },
            "KillPerson": { "Status": "Provisional", "Gap": "More verified NPC families" },
            "FindPerson": { "Status": "Provisional", "Gap": "More verified NPC families" },
            "Population": { "Status": "Provisional", "Gap": "Verified family coverage by band" }
          }
        }
        """;

    internal static int Run(string root)
    {
        var cases = new (string Name, Action Check)[]
        {
            ("literal item identity", () => Block("class Test { const int KeyLowId = 3210; }", "DOA_LITERAL_CONTENT_IDENTITY")),
            ("direct content resolver", () => Block("class Test { bool Run() => data.TryResolveMobTemplate(\"F001\", ql, out _); }", "DOA_LITERAL_CONTENT_CALL")),
            ("inline objective pool", () => Block("class Test { string[] NpcHashes = [\"F001\"]; }", "DOA_FIXED_MISSION_POLICY")),
            ("content identity pattern", () => Block("class Test { bool Run(int itemLowId) => itemLowId is 3210; }", "DOA_LITERAL_CONTENT_BRANCH")),
            ("fixed floor parameter", () => Block("class Test { int FloorCount = 2; }", "DOA_FIXED_MISSION_POLICY")),
            ("silent objective fallback", () => Block("class Test { object Run() => row.NpcHash ?? \"F001\"; }", "DOA_FALLBACK_CONTENT")),
            ("recorded packet dependency", () => Block("class Test { object Run() => GeneratedMissionWire.CapturedBody(0); }", "DOA_RECORDED_PACKET_DEPENDENCY")),
            ("offer wire reader boundary", CheckOfferWireReader),
            ("typed wire contract", () => Require(!Rules("enum WireCode { Quest = 0xDAC3 } class Test { int Read() => row.GetInt32(5); }").Any())),
            ("mission persistence boundary", () => Block("using MySqlConnector; class Test { object Open() => new MySqlConnection(); }", "DOA_MODULE_DEPENDENCY")),
            ("project dependency boundary", CheckProjects),
            ("unchanged and introduced findings", CheckDiff),
            ("versioned layout exception", () => CheckHistoricalLayout(root)),
            ("provisional catalog validation", CheckCoverage),
            ("recorded payload field", CheckRecordedPayload),
            ("key catalog validation", CheckKeyCatalog),
            ("authoritative references", CheckReferences)
        };
        int passed = 0;
        foreach (var test in cases)
        {
            try
            {
                test.Check();
                Console.WriteLine("PASS " + test.Name);
                passed++;
            }
            catch (Exception exception)
            {
                Console.Error.WriteLine("FAIL " + test.Name + " " + exception.Message);
            }
        }
        Console.WriteLine($"DOA_MUTATION_TESTS={passed}/{cases.Length}");
        return passed == cases.Length ? 0 : 1;
    }

    private static void Block(string source, string rule) => Require(Rules(source).Contains(rule));
    private static IEnumerable<string> Rules(string source) => DoaRules.SourceIssues(MissionPath, Parse(source)).Select(issue => issue.Rule);
    private static SyntaxNode Parse(string source) => CSharpSyntaxTree.ParseText(source, new CSharpParseOptions(LanguageVersion.Preview)).GetRoot();
    private static void Require(bool condition)
    {
        if (!condition) throw new InvalidOperationException("Expected architecture check did not hold.");
    }

    private static void CheckOfferWireReader()
    {
        const string rollService = "AORebirth/Server/ZoneEngine_New/Core/Missions/GeneratedMissionRollService.cs";
        const string offerCatalog = "AORebirth/Server/ZoneEngine_New/Core/Missions/Content/MissionOfferCatalog.cs";
        const string dao = "AORebirth/Libraries/Source/AORebirth.Database/Domain/Quests/MySqlCharacterQuestDao.cs";
        var read = Parse("class Test { object Run() => GeneratedMissionWire.Read(renamedInput); }");
        Require(DoaRules.SourceIssues(rollService, read).Any(issue => issue.Rule == "DOA_RECORDED_PACKET_DEPENDENCY"));
        Require(DoaRules.SourceIssues(offerCatalog, read).Any(issue => issue.Rule == "DOA_RECORDED_PACKET_DEPENDENCY"));
        Require(!DoaRules.SourceIssues(dao, read).Any());
        Require(!DoaRules.SourceIssues(rollService, Parse("class Test { object Run() => GeneratedMissionWire.Write(typedOffer); }")).Any());
    }

    private static void CheckProjects()
    {
        Require(DoaRules.IsForbiddenRuntimeProjectReference("Tools/Probe/Probe.csproj"));
        Require(DoaRules.IsForbiddenRuntimeProjectReference("AORebirth/Server/ZoneEngine_New.Tests/ZoneEngine_New.Tests.csproj"));
        Require(DoaRules.IsForbiddenRuntimeProjectReference("AORebirth/Server/WorldEngine/WorldEngine.csproj"));
        Require(!DoaRules.IsForbiddenRuntimeProjectReference("AORebirth/Libraries/Source/AORebirth.Modules.Abstractions/AORebirth.Modules.Abstractions.csproj"));
    }

    private static void CheckDiff()
    {
        Finding old = DoaRules.DataFinding(MissionPath, "Probe", "EXAMPLE", "Open debt", "old");
        Finding changed = DoaRules.DataFinding(MissionPath, "Probe", "EXAMPLE", "Open debt", "changed");
        var result = DoaEnforcement.Compare([old], [old with { Line = 200 }, old, changed]);
        Require(result.Inherited.Length == 1 && result.Introduced.Length == 2);
    }

    private static void CheckHistoricalLayout(string root)
    {
        string source = File.ReadAllText(Path.Combine(root, DoaRules.LayoutPath));
        Require(DoaRules.IsHistoricalLayoutContract(Parse(source)));
        Require(!DoaRules.SourceIssues(DoaRules.LayoutPath, Parse(source)).Any(issue => issue.Rule == "DOA_FIXED_MISSION_POLICY"));
        string changed = source.Replace("version != 2 &&", "version != 3 &&", StringComparison.Ordinal);
        Require(!DoaRules.IsHistoricalLayoutContract(Parse(changed)));
        Require(DoaRules.SourceIssues(DoaRules.LayoutPath, Parse(changed)).Any(issue => issue.Rule == "DOA_FIXED_MISSION_POLICY"));
    }

    private static void CheckCoverage()
    {
        Require(!Catalog(Generation).Any(finding => finding.Path.EndsWith("DungeonGeneration.json", StringComparison.Ordinal)));
        string invalid = Generation.Replace("\"Provisional\"", "\"Complete\"", StringComparison.Ordinal);
        Require(Catalog(invalid).Any(finding => finding.Rule == "DOA_UNSUPPORTED_COMPLETENESS"));
        string noPool = Generation.Replace("[\"FIND\"]", "[]", StringComparison.Ordinal);
        Require(Catalog(noPool).Any(finding => finding.Rule == "DOA_INVALID_GAMEPLAY_POOL"));
    }

    private static void CheckRecordedPayload()
    {
        var typed = new
        {
            FormatVersion = 1, Provenance = new { InputSha256 = new string('a', 64), SourceRevision = new string('b', 40) },
            Responses = new[] { new { BodySha256 = new string('c', 64), Offers = Enumerable.Range(0, 5).Select(index => new { Index = index }) } }
        };
        string json = JsonSerializer.Serialize(typed);
        Require(!Catalog(Generation, offers: json).Any(finding => finding.Path.EndsWith("MissionOffers.json", StringComparison.Ordinal)));
        Require(Catalog(Generation, offers: json.Insert(1, "\"BodyHex\":\"abcd\",")).Any(finding => finding.Rule == "DOA_RECORDED_PACKET_CATALOG"));
    }

    private static void CheckKeyCatalog()
    {
        const string keys = """{"Version":1,"KeyTemplateId":100,"DuplicatorTemplateId":200,"KeyQuality":1,"MaxKeysPerQuest":2,"NamePrefix":"Key to "}""";
        Require(!Catalog(Generation, keys: keys).Any(finding => finding.Path.EndsWith("Keys.json", StringComparison.Ordinal)));
        Require(Catalog(Generation, keys: keys.Replace("200", "100", StringComparison.Ordinal)).Any(finding => finding.Rule == "DOA_INVALID_MISSION_KEYS"));
    }

    private static IEnumerable<Finding> Catalog(string generation, string? offers = null, string? keys = null)
        => DoaRules.CatalogIssues(path => path switch
        {
            "DungeonGeneration.json" => generation,
            "Missions/MissionOffers.json" => offers,
            "Missions/Keys.json" => keys,
            _ => null
        });

    private static void CheckReferences()
    {
        DirectoryInfo root = Directory.CreateTempSubdirectory("aorebirth-doa-test-");
        string generation = Path.Combine(root.FullName, "DungeonGeneration.json");
        string items = Path.Combine(root.FullName, "ItemTemplates.json");
        string npcs = Path.Combine(root.FullName, "NpcTemplates.json");
        try
        {
            File.WriteAllText(generation, Generation);
            File.WriteAllText(items, """{"FIND":{"Templates":[1]},"RETN":{"Templates":[2]},"TOOL":{"Templates":[3]},"FIXD":{"Templates":[4]}}""");
            File.WriteAllText(npcs, """{"BOSS":{"Children":["LEAF"]},"LEAF":{"Templates":[{"Level":1}]}}""");
            Require(!DoaRules.AuthoritativeReferenceIssues(root.FullName).Any());
            File.WriteAllText(npcs, """{"BOSS":{"Children":["BOSS"]}}""");
            Require(DoaRules.AuthoritativeReferenceIssues(root.FullName).Any(finding => finding.Rule == "DOA_UNRESOLVED_REFERENCE"));
        }
        finally
        {
            File.Delete(generation);
            File.Delete(items);
            File.Delete(npcs);
            root.Delete();
        }
    }
}
