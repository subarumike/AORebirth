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
            ("strict acceptance and authority boundary", CheckStrictAcceptance),
            ("inactive conditional source", CheckConditionalSource),
            ("literal and implicit encounter policies", CheckEncounterPolicy),
            ("authored operation privilege bypass", CheckAuthoredPrivilege),
            ("compiled native content", CheckNativePolicy),
            ("packaged and recorded gameplay data", CheckPackagedInteractions),
            ("required encounter catalog inputs", CheckEncounterCatalog),
            ("required LFT catalog and canonical playfield references", CheckLftPolicy),
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

    private static void CheckStrictAcceptance()
    {
        Finding inherited = DoaRules.DataFinding(MissionPath, "Probe", "EXAMPLE", "Open debt", "old");
        Finding[] current = [inherited];
        Require(DoaEnforcement.Compare([inherited], current).Introduced.Length == 0);
        Require(DoaEnforcement.AcceptanceStatus(false, true, current.Length, 0, 0) == "FAIL_ARCHITECTURE");
        Require(DoaEnforcement.AcceptanceStatus(false, true, 0, 1, 0) == "FAIL_ARCHITECTURE");
        Require(DoaEnforcement.AcceptanceStatus(false, true, 0, 0, 1) == "FAIL_ARCHITECTURE");
        Require(DoaEnforcement.AcceptanceStatus(false, false, 0, 0, 0) == "FAIL_ARCHITECTURE");
        Require(DoaEnforcement.AcceptanceStatus(false, true, 0, 0, 0) == "PASS_ARCHITECTURE_AND_REFERENCES");
        Require(DoaEnforcement.AcceptanceStatus(true, false, 0, 1, 0) == "FAIL_ARCHITECTURE");
        Require(DoaEnforcement.AcceptanceStatus(true, false, 0, 0, 0) == "PASS_STRUCTURE_ONLY");
    }

    private static void CheckConditionalSource()
    {
        const string source = """
            #if UNUSED_PROFILE
            class Test { const int ItemLowId = 3210; }
            #else
            class Test { int Read() => data.GetInt32(5); }
            #endif
            """;
        var unresolved = new List<string>();
        Require(DoaRules.SourceVariants(MissionPath, source, unresolved.Add)
            .SelectMany(syntax => DoaRules.SourceIssues(MissionPath, syntax))
            .Any(finding => finding.Rule == "DOA_LITERAL_CONTENT_IDENTITY"));
        Require(unresolved.Count == 0);

        const string excessive = "#if A || B || C || D || E || F || G || H || I\nclass Test {}\n#endif";
        _ = DoaRules.SourceVariants(MissionPath, excessive, unresolved.Add).ToArray();
        Require(unresolved.Count == 1);
    }

    private static void CheckEncounterPolicy()
    {
        Block("class MonsterPetRule { int MaxPets { get; set; } = 1; }", "DOA_FIXED_ENCOUNTER_POLICY");
        Block("class MonsterTriggeredSelfNano { int MaxCastsPerFight { get; set; } = 1; }", "DOA_FIXED_ENCOUNTER_POLICY");
        Block("class NpcPetSummons { const int RetrySeconds = 1; }", "DOA_FIXED_ENCOUNTER_POLICY");
        Block("class Test { object Make() => rules.Pets ?? new MonsterPetRule(); }", "DOA_IMPLICIT_ENCOUNTER_POLICY");
        Require(!Rules("class MonsterPetRule { public required int MaxPets { get; init; } }").Any());
    }

    private static void CheckAuthoredPrivilege()
    {
        Block("class Test { bool Apply(ItemSpell spell) => NanoRuntime.TryApplyImmediate(a,b,c,d,e,f, gmApply: true); }",
            "DOA_AUTHORED_PRIVILEGE_BYPASS");
        Block("class Test { bool Apply(ItemSpell spell) => NanoRuntime.TryApplyImmediate(a,b,c,d,e,f, isEligible); }",
            "DOA_AUTHORED_PRIVILEGE_BYPASS");
        Block("class Test { bool Apply(ItemSpell spell) => NanoCastFunctions.TryExecute(a,b,spell,d,e, IsEligible(spell)); }",
            "DOA_AUTHORED_PRIVILEGE_BYPASS");
        Require(!Rules("class Test { bool Apply(ItemSpell spell) => NanoRuntime.TryApplyImmediate(a,b,c,d,e,f, areaSpread: true); }").Any());
    }

    private static void CheckNativePolicy()
    {
        const string path = DoaAdditionalInputs.NativeSourceRoot + "/test.cpp";
        const string source = """
            // const int ServerPort = 9999;
            constexpr unsigned long LocalIpHostOrder = (127ul << 24) | 1ul;
            constexpr unsigned long LoginPort = 7500;
            constexpr auto GamecodeBuild = 0x1234;
            const PlayfieldName Rows[] = {
            #include "playfields.inc"
            };
            """;
        Finding[] issues = DoaAdditionalInputs.NativeIssues(path, source).ToArray();
        Require(issues.Count(finding => finding.Rule == "DOA_NATIVE_COMPILED_ENDPOINT") == 2);
        Require(issues.Count(finding => finding.Rule == "DOA_NATIVE_COMPILED_PLAYFIELDS") == 1);
        Require(issues.All(finding => finding.Line > 1));
        Require(!DoaAdditionalInputs.NativeIssues(path, "const auto LoginPort = config.LoginPort;").Any());
    }

    private static void CheckPackagedInteractions()
    {
        const string authored = """{"Journals":{"quest":{"Hex":"1234"}},"TimedTurnIns":[{"Key":"turnin","TurnInNpc":"SimpleChar:1"}]}""";
        string[] rules = DoaAdditionalInputs.PackagedInteractionIssues(authored).Select(finding => finding.Rule).ToArray();
        Require(rules.Contains("DOA_RECORDED_JOURNAL_PAYLOAD") && rules.Contains("DOA_PACKAGED_GAMEPLAY_BINDING"));
        Require(DoaAdditionalInputs.PackagedInteractionIssues("""{"Journals":{"quest":{"DeleteHex":["1234"]}}}""")
            .Any(finding => finding.Rule == "DOA_RECORDED_JOURNAL_PAYLOAD"));
        Require(!DoaAdditionalInputs.PackagedInteractionIssues("""{"Journals":{"quest":{"Packet":{}}},"TimedTurnIns":[{"Key":"turnin"}]}""").Any());
    }

    private static void CheckEncounterCatalog()
    {
        const string path = DoaAdditionalInputs.MonsterPolicy;
        Require(DoaAdditionalInputs.MonsterPolicyIssues(path, null).Any());
        Require(DoaAdditionalInputs.MonsterPolicyIssues(path, """{"A":{"Pets":{"MaxPets":2}}}""")
            .Any(finding => finding.Rule == "DOA_UNAUTHORED_PET_POLICY"));
        Require(DoaAdditionalInputs.MonsterPolicyIssues(path, """{"A":{"Pets":true,"TriggeredSelfNanos":"unknown"}}""")
            .Count() == 2);
        Require(DoaAdditionalInputs.MonsterPolicyIssues(path, """{"A":{"TriggeredSelfNanos":[{"MaxCastsPerFight":null}]}}""")
            .Any(finding => finding.Rule == "DOA_UNAUTHORED_NANO_POLICY"));
        Require(!DoaAdditionalInputs.MonsterPolicyIssues(path, """
            {"A":{"Pets":{"MaxPets":2,"SummonIntervalSeconds":1,"RespawnDelaySeconds":10},
                "TriggeredSelfNanos":[{"MaxCastsPerFight":null,"CooldownSeconds":94}]}}
            """).Any());
    }

    private static void CheckLftPolicy()
    {
        const string path = DoaAdditionalInputs.LftPolicy;
        const string authored = """{"Version":1,"UnlistedRegion":"RubiKa","ShadowlandsPlayfieldIds":[1,2]}""";
        Require(DoaAdditionalInputs.LftPolicyIssues(path, null).Any(finding => finding.Rule == "DOA_MISSING_LFT_POLICY"));
        Require(DoaAdditionalInputs.LftPolicyIssues(path, authored.Replace("[1,2]", "[1,1]", StringComparison.Ordinal))
            .Any(finding => finding.Rule == "DOA_INVALID_LFT_POLICY"));
        Require(DoaAdditionalInputs.LftPolicyIssues(path, authored.Replace("RubiKa", "Unknown", StringComparison.Ordinal))
            .Any(finding => finding.Rule == "DOA_INVALID_LFT_POLICY"));
        Require(DoaAdditionalInputs.LftPolicyIssues(path, authored, id => id == 1)
            .Single().Rule == "DOA_UNRESOLVED_LFT_PLAYFIELD");
        Require(!DoaAdditionalInputs.LftPolicyIssues(path, authored, _ => true).Any());
        Require(DoaAdditionalInputs.LftMetadataIdentityMatches("""{"playfieldId":42}""", 42));
        Require(DoaAdditionalInputs.LftMetadataIdentityMatches("""{"PlayfieldId":42}""", 42));
        Require(!DoaAdditionalInputs.LftMetadataIdentityMatches("""{"playfieldId":43}""", 42));
        Require(!DoaAdditionalInputs.LftMetadataIdentityMatches("""{"playfieldId":42,"PlayfieldId":43}""", 42));
    }

    private static void CheckHistoricalLayout(string root)
    {
        string source = File.ReadAllText(Path.Combine(root, DoaRules.LayoutPath));
        Require(DoaRules.IsHistoricalLayoutContract(Parse(source)));
        Require(!DoaRules.SourceIssues(DoaRules.LayoutPath, Parse(source)).Any(issue => issue.Rule == "DOA_FIXED_MISSION_POLICY"));
        foreach (int otherVersion in new[] { 1, 3 })
        {
            string changed = source.Replace("version != 2 ||", $"(version != 2 && version != {otherVersion}) ||", StringComparison.Ordinal);
            Require(!DoaRules.IsHistoricalLayoutContract(Parse(changed)));
            Require(DoaRules.SourceIssues(DoaRules.LayoutPath, Parse(changed)).Any(issue => issue.Rule == "DOA_FIXED_MISSION_POLICY"));
        }
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
