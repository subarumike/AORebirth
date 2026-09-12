using System.Collections;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;

// Deliberately offline. Reflection accesses the existing internal catalog without
// making its Legacy runtime types an API or copying its selection rules.
const BindingFlags Members = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
if (args.Length != 2 || args[0] is not ("--write" or "--check"))
    throw new ArgumentException("Usage: AcceptedOrdinaryBindingExport --write|--check <repository-root>");
string root = Path.GetFullPath(args[1]);
var assembly = Assembly.Load("ZoneEngine");
Type CatalogType(string name) => assembly.GetType(
    (name == "CapturedSubwayContentProvider" ? "ZoneEngine.Core.Playfields." : "AORebirth.Core.Playfields.") + name,
    throwOnError: true)!;
object Construct(string name, params object[] values) => Activator.CreateInstance(CatalogType(name),
    Members, binder: null, args: values, culture: System.Globalization.CultureInfo.InvariantCulture)!;
object? Property(object value, string name) => value.GetType().GetProperty(name, Members)!.GetValue(value);
object Invoke(object value, string method, params object[] values) => value.GetType()
    .GetMethod(method, Members, values.Select(v => v.GetType()).ToArray())!.Invoke(value, values)!;
IEnumerable<object> Rows(object value) => ((IEnumerable)value).Cast<object>();
object? Snapshot(object? value)
{
    if (value == null || value is string || value.GetType().IsPrimitive || value is decimal) return value;
    if (value.GetType().IsEnum) return value.ToString();
    if (value is IEnumerable rows) return rows.Cast<object?>().Select(Snapshot).ToArray();
    return value.GetType().GetProperties(Members)
        .Where(p => p.GetIndexParameters().Length == 0 && p.GetMethod is { IsPrivate: false })
        .OrderBy(p => p.Name, StringComparer.Ordinal)
        .ToDictionary(p => p.Name, p => Snapshot(p.GetValue(value)), StringComparer.Ordinal);
}

object catalog = Construct("OrdinaryEnemyCatalog", Construct("CapturedSubwayContentProvider"),
    Construct("CapturedSubwayOrdinaryContentProvider"), Construct("CapturedTempleOfThreeWindsContentProvider"));
var profiles = Rows(Invoke(catalog, "GetProfiles")).ToDictionary(p => (string)Property(p, "ProfileKey")!);
var spawns = Rows(Invoke(catalog, "GetSpawns"))
    .Where(s => (int)Property(s, "PlayfieldInstance")! is 127 or 1931)
    .OrderBy(s => (int)Property(s, "PlayfieldInstance")!)
    .ThenBy(s => (string)Property(s, "SpawnKey")!, StringComparer.Ordinal).ToArray();
if (spawns.Select(s => ((int)Property(s, "PlayfieldInstance")!, (string)Property(s, "SpawnKey")!)).Distinct().Count() != spawns.Length)
    throw new InvalidDataException("Duplicate accepted placement key.");
var resolveCombat = CatalogType("OrdinaryEnemyRuntimeService").GetMethod("ResolveCombatContractForSpawn",
    BindingFlags.Static | BindingFlags.NonPublic)!;
var definitions = new SortedDictionary<string, object?>(StringComparer.Ordinal);
string Definition(string kind, object? value)
{
    object? snapshot = Snapshot(value);
    string key = kind + ":" + Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(snapshot))).ToLowerInvariant();
    definitions.TryAdd(key, snapshot);
    return key;
}
var bindings = spawns.Select(spawn =>
{
    var profile = profiles[(string)Property(spawn, "ProfileKey")!];
    object level = Property(spawn, "LevelDefinition")!;
    var explicitVariants = Rows(Invoke(level, "GetExplicitVariants")).ToArray();
    var variants = explicitVariants.Length != 0 ? explicitVariants
        : Enumerable.Range((int)Property(level, "MinimumLevel")!,
            (int)Property(level, "MaximumLevel")! - (int)Property(level, "MinimumLevel")! + 1)
            .Select(l => Invoke(level, "Resolve", l)).ToArray();
    var combat = variants.Select(variant =>
    {
        object?[] parameters = [spawn, profile, variant, false];
        object? contract = resolveCombat.Invoke(null, parameters);
        if (!(bool)Property(variant, "IsValid")! || (int)Property(variant, "Health")! <= 0)
            throw new InvalidDataException("Accepted binding contains an invalid selected variant.");
        return new { Variant = Snapshot(variant), ContractBeforeRuntimePreparation = Definition("combat-contract", contract),
            RetaliationEligibilityPromoted = parameters[3] };
    }).ToArray();
    bool active = Property(spawn, "Disposition")!.ToString() == "Active";
    bool scripted = (bool)Property(profile, "BossOrScripted")!;
    return new
    {
        playfield = Property(spawn, "PlayfieldInstance"),
        placement_identity = Property(spawn, "SpawnKey"),
        source_identity = $"SimpleChar:{(int)Property(spawn, "SourceIdentity")!:X8}",
        accepted_template = new { ConstructionMode = Property(profile, "ConstructionMode")!.ToString(),
            TemplateHash = Property(profile, "TemplateHash"), MonsterData = Property(profile, "MonsterData"),
            DisplayName = Property(profile, "DisplayName") },
        accepted_level_or_variant = Snapshot(level),
        accepted_visual_identity = Definition("appearance", Property(profile, "Appearance")),
        accepted_behavior_mapping = new { ProfileKey = Property(profile, "ProfileKey"),
            Aggression = Snapshot(Property(profile, "Aggression")), Movement = Property(spawn, "MovementMode")!.ToString(),
            SupportNano = Snapshot(Property(profile, "SupportNano")), BossOrScripted = scripted },
        accepted_combat_mapping = combat,
        accepted_combat_profile = Definition("combat-profile", Property(profile, "Combat")),
        accepted_loot_mapping = Definition("loot", Property(profile, "Loot")),
        accepted_corpse_mapping = Definition("corpse", Property(profile, "Corpse")),
        // These are declared capabilities, not a claim of final combat readiness:
        // Legacy Prepare may retain a quarantined passive actor. The New adapter
        // still has to resolve/init the exact contract and preserve that distinction.
        accepted_capabilities = active ? new[] { scripted ? "special_scripted_actor" : "ordinary_hostile_NPC", "combat_NPC" } : [],
        current_runtime_consumer = (string?)null,
        consumer_status = active ? "ACCEPTED_BUT_NO_CONSUMER" : "UNPROVEN",
        missing_runtime_connection = active ? "OrdinaryEnemyRuntimeService accepted per-generation materialization, exact CapturedEnemyCombatRuntime.Prepare resolution/quarantine, movement, corpse/loot/reward and population lifecycle adapter" : "Catalog disposition is not Active",
        provenance = new { SourceCapture = Property(spawn, "SourceCapture"), SourceTimestamp = Property(spawn, "SourceTimestamp"),
            SourceOwnerIdentity = Property(spawn, "SourceOwnerIdentity"), ProfileEvidence = Snapshot(Property(profile, "Evidence")),
            ConsumerAuthority = "OrdinaryEnemyRuntimeService.Spawn/ResolveCombatContractForSpawn" },
        source_spawn = Snapshot(spawn)
    };
}).ToArray();
string[] sources = ["OrdinaryEnemyCatalog.cs", "OrdinaryEnemyProfile.cs", "OrdinaryEnemyRuntimeService.cs",
    "CapturedSubwayContentProvider.cs", "CapturedSubwayOrdinaryContentProvider.cs",
    "CapturedTempleOfThreeWindsContentProvider.cs", "CapturedTempleOfThreeWindsCombatCatalog.cs",
    "CapturedSubwayRetaliationEligibilityResolver.cs", "OrdinaryEnemyCombatSetupGenerator.cs",
    "OrdinaryEnemyCombatSetupGenerator.Data.cs", "CapturedEnemyCombatProfileCatalog.g.cs",
    "CapturedEnemyCombatContract.cs", "CapturedEnemyCombatContract.Data.cs"];
var result = new
{
    SchemaVersion = 1,
    Authority = "Existing compiled OrdinaryEnemyCatalog and exact per-source per-variant combat resolver; no runtime activation authorized by this export",
    StartingCheckpoint = "44fa42fce316d98c1d5f6326d92caae909398289",
    Sources = sources.Select(name =>
    {
        string path = "AORebirth/Server/ZoneEngine/Core/Playfields/" + name;
        return new { Path = path, Sha256 = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(Path.Combine(root, path)))).ToLowerInvariant() };
    }).ToArray(),
    AcceptedBindingsVerified = bindings.Length,
    SubwayBindings = bindings.Count(b => (int)b.playfield! == 127),
    TempleBindings = bindings.Count(b => (int)b.playfield! == 1931),
    Bindings = bindings,
    Definitions = definitions
};
// Match repository LF checkout policy on both Windows and Linux.
string json = JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true })
    .Replace("\r\n", "\n", StringComparison.Ordinal) + "\n";
string output = Path.Combine(root, "docs/evidence/ZONEENGINE_NEW_ORDINARY_BINDING_CONSUMERS.json");
if (args[0] == "--write") File.WriteAllText(output, json, new System.Text.UTF8Encoding(false));
else if (!File.Exists(output) || File.ReadAllText(output) != json)
    throw new InvalidDataException("Accepted binding ledger differs from the compiled source catalog.");
Console.WriteLine($"ACCEPTED_BINDINGS_VERIFIED={result.AcceptedBindingsVerified}; SUBWAY={result.SubwayBindings}; TOTW={result.TempleBindings}");
