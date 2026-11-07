using System.Globalization;
using System.IO.Compression;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using AORebirth.Enums;
using SmokeLounge.AOtomation.Messaging.GameData;
using ZoneEngine_New.Core.Inventory.Dat;

// Offline metadata export only. It shares the existing item reader and never creates runtime item instances.
bool measureReader = args.Length == 9 && args[4] == "--reader-validation"
    && args[5] == "--exporter-assembly" && args[7] == "--ao-client-root";
if ((args.Length != 4 && !measureReader) || args[0] != "--gamedata-root" || args[2] != "--output-root")
{
    Console.Error.WriteLine("MissionChestRKCatalog --gamedata-root <private root> --output-root <private output> [--reader-validation --exporter-assembly <current RDBDataExtractor.dll> --ao-client-root <installed client>]");
    return 1;
}
CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
CultureInfo.CurrentUICulture = CultureInfo.InvariantCulture;
string source = Path.Combine(Path.GetFullPath(args[1]), "items.dat");
string output = Path.GetFullPath(args[3]);
Directory.CreateDirectory(output);
string destination = Path.Combine(output, "item-metadata.jsonl");
string partial = destination + ".partial";
string? sourceHashBefore = measureReader ? Hash(source) : null;
List<DatItemTemplate> items = ItemsDatReader.Read(source);
if (measureReader)
{
    WriteReaderValidation(source, output, args[6], args[8], sourceHashBefore!, items);
    Console.WriteLine("PASS currentReaderRecords=" + items.Count + " validation=" + Path.Combine(output, "reader-validation.json"));
    return 0;
}
var ids = new HashSet<int>();
using (var writer = new StreamWriter(partial, false, new UTF8Encoding(false)))
{
    foreach (DatItemTemplate item in items.OrderBy(item => item.ID).ThenBy(item => item.Quality))
    {
        if (!ids.Add(item.ID))
            throw new InvalidDataException("Repeated template ID prevents an unambiguous review index.");
        var criteria = new List<object>();
        foreach (DatAction action in item.Actions ?? [])
        {
            int index = 0;
            foreach (DatRequirement requirement in action.Requirements ?? [])
            {
                criteria.Add(new { Source = "Action:" + action.ActionType, Index = index++,
                    Stat = requirement.Statnumber, StatName = Enum.GetName((CharacterStat)requirement.Statnumber),
                    requirement.Value, Operator = (int)requirement.Operator, OperatorName = requirement.Operator.ToString(),
                    Target = (int)requirement.Target, ChildOperator = (int)requirement.ChildOperator });
            }
        }
        var functions = new SortedSet<int>();
        var uploads = new SortedSet<int>();
        var functionDetails = new List<object>();
        foreach (DatEvent ev in item.Events ?? [])
        {
            foreach (DatFunction function in ev.Functions ?? [])
            {
                functions.Add(function.FunctionType);
                functionDetails.Add(new { Event = ev.EventType.ToString(), Function = function.FunctionType,
                    FunctionName = Enum.GetName((FunctionType)function.FunctionType), function.Target,
                    IntegerArguments = (function.Arguments?.Values ?? []).Select(value => value.IsTypeOf<int>() == true ? (int?)value.AsInt32() : null),
                    StringArguments = (function.Arguments?.Values ?? []).Select(value => value.IsTypeOf<string>() == true ? value.AsString() : null) });
                if (function.FunctionType == (int)FunctionType.UploadNano
                    && function.Arguments?.Values is { Count: > 0 } arguments
                    && arguments[0].IsTypeOf<int>() == true && arguments[0].AsInt32() > 0)
                    uploads.Add(arguments[0].AsInt32());
                int index = 0;
                foreach (DatRequirement requirement in function.Requirements ?? [])
                {
                    criteria.Add(new { Source = "Event:" + ev.EventType + "/" + function.FunctionType,
                        Index = index++, Stat = requirement.Statnumber,
                        StatName = Enum.GetName((CharacterStat)requirement.Statnumber), requirement.Value,
                        Operator = (int)requirement.Operator, OperatorName = requirement.Operator.ToString(),
                        Target = (int)requirement.Target, ChildOperator = (int)requirement.ChildOperator });
                }
            }
        }
        var named = new SortedDictionary<string, int>(StringComparer.Ordinal);
        foreach (var stat in item.Stats ?? [])
            named[Enum.GetName((CharacterStat)stat.Key) ?? stat.Key.ToString(CultureInfo.InvariantCulture)] = stat.Value;
        writer.WriteLine(JsonSerializer.Serialize(new { Id = item.ID, item.Name, item.Quality, item.Flags,
            item.ItemType, item.DynelType, Stats = item.Stats, NamedStats = named, item.Attack, item.Defend,
            RdbPresent = item.Rdb != null, ReadComplete = item.Rdb?.ReadComplete == true,
            BytesLeft = item.Rdb?.BytesLeft, ReadStopReason = item.Rdb?.ReadStopReason,
            LiveItemClass = item.Rdb?.LiveStats.GetValueOrDefault((int)CharacterStat.ItemClass),
            NanoTemplate = item.Rdb?.Nano != null, Criteria = criteria,
            FunctionIds = functions, FunctionNames = functions.Select(value => Enum.GetName((FunctionType)value) ?? value.ToString()),
            UploadNanoIds = uploads, FunctionDetails = functionDetails,
            ShopInventory = (item.Rdb?.ShopInventory ?? []).Select(line => new { line.Hash,
                line.MinQl, line.MaxQl, line.Amount, line.Chance, line.Extra, line.Regen, line.Interval }) }));
    }
}
File.Move(partial, destination, true);
var options = new JsonSerializerOptions { WriteIndented = true };
File.WriteAllText(Path.Combine(output, "metadata-enums.json"), JsonSerializer.Serialize(new {
    CharacterStats = Enum.GetValues<CharacterStat>().Distinct().ToDictionary(value => value.ToString(), value => (int)value),
    ItemFlags = Enum.GetValues<ItemFlags>().Distinct().ToDictionary(value => value.ToString(), value => (int)value),
    CanFlags = Enum.GetValues<CanFlags>().Distinct().ToDictionary(value => value.ToString(), value => (uint)value),
    FunctionTypes = Enum.GetValues<FunctionType>().Distinct().ToDictionary(value => value.ToString(), value => (int)value)
}, options));
File.WriteAllText(Path.Combine(output, "item-metadata-source.json"), JsonSerializer.Serialize(new {
    Source = source, SourceSha256 = Hash(source), RecordCount = items.Count, Output = destination,
    OutputSha256 = Hash(destination), Method = "Existing ItemsDatReader and DatItemTemplate; one complete pass; no runtime items"
}, options));
Console.WriteLine("PASS metadataRecords=" + items.Count + " output=" + destination);
return 0;

static string Hash(string path)
{
    using var file = File.OpenRead(path);
    return Convert.ToHexString(SHA256.HashData(file)).ToLowerInvariant();
}


// Measurement only: reuse the current reader's objects and the current AODB source index.
// This mode never writes items.dat, item instances, policy catalogs or runtime configuration.
static void WriteReaderValidation(string source, string output, string exporterPath, string clientRoot,
    string sourceHashBefore, List<DatItemTemplate> items)
{
    exporterPath = Path.GetFullPath(exporterPath);
    if (!string.Equals(Path.GetFileName(exporterPath), "RDBDataExtractor.dll", StringComparison.OrdinalIgnoreCase)
        || AssemblyName.GetAssemblyName(exporterPath).Name != "RDBDataExtractor")
        throw new InvalidDataException("The exporter identity must be the actual RDBDataExtractor assembly, not this metadata tool.");
    string exporterHash = Hash(exporterPath);
    Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
    int itemRecords;
    int nanoRecords;
    using (var controller = new AODB.RdbController(Path.GetFullPath(clientRoot)))
    {
        itemRecords = controller.RecordTypeToId[(int)AODB.Common.RDBObjects.ResourceTypeId.RDBItem].Count;
        // The source nano resource kind used by the existing ItemDatExporter.
        nanoRecords = controller.RecordTypeToId[1040005].Count;
    }
    int expected = checked(itemRecords + nanoRecords);
    if (items.Count == 0 || items.Count != expected || items.Any(item => item.Rdb == null))
        throw new InvalidDataException("Decoded current reader population/full RDB objects differ from the actual source index.");
    object header = MeasureItemsHeader(source, items.Count);
    var functions = items.SelectMany(item => item.Events ?? [])
        .SelectMany(ev => ev.Functions ?? []).ToArray();
    var aura = items.Where(item => item.ID == 212161).SelectMany(item => item.Events ?? [])
        .SelectMany(ev => (ev.Functions ?? []).Select(function => new {
            eventType = (int)ev.EventType, functionType = function.FunctionType,
            tickCount = function.TickCount, tickInterval = function.TickInterval
        })).ToArray();
    if (!aura.Any(function => function.tickCount == -1 && function.tickInterval > 0))
        throw new InvalidDataException("Current decoded Aura of Revival does not retain its required periodic tick fields.");
    var bosses = items.Where(item => item.Name?.StartsWith("Boss Modifier Item", StringComparison.OrdinalIgnoreCase) == true)
        .OrderBy(item => item.ID).Select(item => new { id = item.ID, name = item.Name! }).ToArray();
    if (bosses.Length == 0)
        throw new InvalidDataException("Current decoded population has no named boss modifier records.");
    var fields = new {
        fullRdb = items.Count(item => item.Rdb != null),
        stats = items.Count(item => item.Stats is { Count: > 0 }),
        attack = items.Count(item => item.Attack is { Count: > 0 }),
        defend = items.Count(item => item.Defend is { Count: > 0 }),
        eventsOrActions = items.Count(item => item.Events is { Count: > 0 } || item.Actions is { Count: > 0 }),
        shops = items.Count(item => item.Rdb?.ShopInventory is { Count: > 0 }),
        shopLines = items.Sum(item => item.Rdb?.ShopInventory?.Count ?? 0),
        rawFallback = items.Count(item => item.Rdb?.Record is { Length: > 0 }),
        nanoFields = items.Count(item => item.Rdb?.Nano != null),
        positiveAmmoType = items.Count(item => item.Stats?.GetValueOrDefault((int)CharacterStat.AmmoType) > 0),
        positiveMaxEnergy = items.Count(item => item.Stats?.GetValueOrDefault((int)CharacterStat.MaxEnergy) > 0),
        positiveMultipleCount = items.Count(item => item.MultipleCount > 0),
        negativeOneTickFunctions = functions.Count(function => function.TickCount == -1),
        periodicFunctions = functions.Count(function => function.TickInterval > 0)
    };
    if (fields.nanoFields != nanoRecords || Hash(source) != sourceHashBefore || Hash(exporterPath) != exporterHash)
        throw new InvalidDataException("Source nano population or measured items/exporter bytes changed during reader validation.");
    var validation = new {
        CURRENT_ITEMS_DAT_READER = "PASS",
        ITEM_TEMPLATE_CATALOG = "DEFERRED_TO_REAL_WINDOWS_STARTUP",
        EXPORTER_ASSEMBLY_SHA256 = exporterHash,
        ITEMS_DAT_SHA256 = sourceHashBefore,
        ITEMS_DAT_SIZE = new FileInfo(source).Length,
        HEADER = header,
        SOURCE_POPULATION = new { itemRecords, nanoRecords, expected },
        DECODED_POPULATION = items.Count,
        DISTINCT_IDS = items.Select(item => item.ID).Distinct().Count(),
        NAMED_TEMPLATES = items.Count(item => !string.IsNullOrWhiteSpace(item.Name)),
        UNNAMED_IDS = items.Where(item => string.IsNullOrWhiteSpace(item.Name)).Select(item => item.ID).Order().ToArray(),
        BOSS_MODIFIER_COUNT = bosses.Length,
        BOSS_MODIFIERS = bosses,
        CURRENT_FIELDS = fields,
        AURA_OF_REVIVAL_212161 = aura
    };
    string readerAssembly = typeof(DatItemTemplate).Assembly.Location;
    var provenance = new {
        Method = "Current linked ItemsDatReader objects, literal staged header and zlib sizes, actual current AODB source index; no policy regeneration",
        Source = Path.GetFullPath(source), SourceSha256 = sourceHashBefore,
        ExporterAssembly = exporterPath, ExporterAssemblySha256 = exporterHash,
        ReaderAssembly = readerAssembly, ReaderAssemblySha256 = Hash(readerAssembly),
        ClientRoot = Path.GetFullPath(clientRoot)
    };
    var options = new JsonSerializerOptions { WriteIndented = true };
    using (var stream = new FileStream(Path.Combine(output, "reader-validation.json"), FileMode.CreateNew, FileAccess.Write, FileShare.None))
        JsonSerializer.Serialize(stream, validation, options);
    using (var stream = new FileStream(Path.Combine(output, "reader-validation-source.json"), FileMode.CreateNew, FileAccess.Write, FileShare.None))
        JsonSerializer.Serialize(stream, provenance, options);
}

static object MeasureItemsHeader(string source, int decodedCount)
{
    using var stream = File.OpenRead(source);
    using var reader = new BinaryReader(stream, Encoding.ASCII, leaveOpen: true);
    int versionLength = reader.ReadByte();
    byte[] label = reader.ReadBytes(versionLength);
    if (label.Length != versionLength)
        throw new EndOfStreamException("Truncated items.dat version label.");
    string version = Encoding.ASCII.GetString(label);
    int packCount = reader.ReadInt32();
    int templateCount = reader.ReadInt32();
    int slices = reader.ReadInt32();
    if (version != "RDBDataExtractor" || packCount <= 0 || templateCount != decodedCount || slices <= 0
        || slices != (templateCount + (long)packCount - 1) / packCount)
        throw new InvalidDataException("Staged items.dat header differs from the current decoded population.");
    var sliceStates = new List<object>();
    for (int index = 0; index < slices; index++)
    {
        int compressedBytes = reader.ReadInt32();
        if (compressedBytes <= 0 || compressedBytes > stream.Length - stream.Position)
            throw new InvalidDataException("Invalid items.dat compressed slice size.");
        byte[] bytes = reader.ReadBytes(compressedBytes);
        using var compressed = new MemoryStream(bytes, writable: false);
        using var zlib = new ZLibStream(compressed, CompressionMode.Decompress);
        using var unpacked = new MemoryStream();
        zlib.CopyTo(unpacked);
        long uncompressedBytes = unpacked.Length;
        if (uncompressedBytes <= 0)
            throw new InvalidDataException("Empty items.dat decoded slice.");
        sliceStates.Add(new { index, compressedBytes, uncompressedBytes });
    }
    if (stream.Position != stream.Length)
        throw new InvalidDataException("Trailing bytes after items.dat slices.");
    return new { version, packCount, templateCount, slices, sliceStates };
}
