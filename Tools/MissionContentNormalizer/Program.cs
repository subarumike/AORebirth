using System.Reflection;
using System.Runtime.Loader;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;

// Offline only. The decoder assembly is explicit so this tool never discovers
// capture locations, starts an engine, or writes its historical inputs.
if (args.Length == 3 && args[0] == "--audit-bindings")
{
    PersistenceAudit.Run(args[1], args[2]);
    return;
}
if (args.Length is not (3 or 4)) throw new ArgumentException("decoder-engine.dll input-Missions-directory output-directory [historical-layouts.json]");
string engine = Path.GetFullPath(args[0]), input = Path.GetFullPath(args[1]), output = Path.GetFullPath(args[2]);
if (input.Equals(output, StringComparison.OrdinalIgnoreCase) || output.StartsWith(@"D:\AORebirthCaptures", StringComparison.OrdinalIgnoreCase))
    throw new ArgumentException("Output must be separate from evidence and inputs.");
Directory.CreateDirectory(output);
Environment.SetEnvironmentVariable("AO_REBIRTH_GAMEDATA_PATH", Path.GetDirectoryName(input));
var context = new DecoderContext(engine);
var assembly = context.LoadFromAssemblyPath(engine);
const BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static;
var options = new JsonSerializerOptions { WriteIndented = true, IncludeFields = true };
var wire = assembly.GetType("ZoneEngine_New.Core.Missions.GeneratedMissionWire", true)!;
var codecType = assembly.GetType("ZoneEngine_New.Core.Network.ZoneMessageCodec", true)!;
var codec = Activator.CreateInstance(codecType)!;
var decode = codecType.GetMethod("Deserialize", [typeof(byte[])])!;
var records = new List<object>();
var fields = new SortedSet<string>();
var promotedFields = new SortedSet<string>();
void CollectFields(JsonNode? node, string path)
{
    if (node is JsonObject obj) foreach (var property in obj) CollectFields(property.Value, path + "." + property.Key);
    else if (node is JsonArray array)
    {
        promotedFields.Add(path + "[]");
        foreach (var value in array) CollectFields(value, path + "[]");
    }
    else promotedFields.Add(path);
}
var failures = new List<string>();
JsonObject DecodePacket(string hex, string key)
{
    var packet = Convert.FromHexString(hex);
    object message;
    try { message = decode.Invoke(codec, [packet])!; }
    catch (TargetInvocationException e)
    {
        var error=e.InnerException!;
        while(error.InnerException!=null) error=error.InnerException;
        records.Add(new { key, error=error.Message, source_bytes=packet.Length });
        failures.Add(key + ": " + error.Message);
        return new JsonObject { ["Kind"]="DECODE_FAILED", ["Error"]=error.Message };
    }
    var body = message.GetType().GetProperty("Body")!.GetValue(message)!;
    var node = JsonSerializer.SerializeToNode(body, body.GetType(), options)!.AsObject();
    if (body.GetType().Name == "SimpleCharFullUpdateMessage")
    {
        if (node["TailFullyDecoded"]?.GetValue<bool>() != true || node["UndecodedTail"]?.GetValue<string>() is { Length: > 0 })
            failures.Add(key + ": incomplete NPC appearance decoding");
    }
    else
    {
        var clone=node.Deserialize(body.GetType(),options)!;
        message.GetType().GetProperty("Body")!.SetValue(message,clone);
        var serialized=(byte[])codecType.GetMethods().Single(m=>m.Name=="Serialize" && m.GetParameters().Length==1).Invoke(codec,[message])!;
        if(!packet.SequenceEqual(serialized)) failures.Add(key + ": typed static projection does not reproduce original bytes");
    }
    records.Add(new { key, type = body.GetType().Name, source_bytes = packet.Length, source_sha256 = Convert.ToHexString(SHA256.HashData(packet)), fields = node.Select(p => p.Key).ToArray() });
    foreach (var p in node) fields.Add(body.GetType().Name + "." + p.Key);
    return new JsonObject { ["Kind"] = body.GetType().Name, ["Definition"] = node };
}
var bodies = JsonSerializer.Deserialize<string[]>(File.ReadAllText(Path.Combine(input,"RollBodies.json")))!;
var offers = new JsonArray();
foreach (var hex in bodies)
{
    var value = wire.GetMethod("Read", flags)!.Invoke(null, [Convert.FromHexString(hex)])!;
    var node = JsonSerializer.SerializeToNode(value, value.GetType(), options)!;
    var clone=node.Deserialize(value.GetType(),options)!;
    var before=(byte[])wire.GetMethod("Write",flags)!.Invoke(null,[value])!;
    var after=(byte[])wire.GetMethod("Write",flags)!.Invoke(null,[clone])!;
    if(!Convert.FromHexString(hex).SequenceEqual(before) || !before.SequenceEqual(after)) failures.Add("Response " + offers.Count + ": typed offer field loss");
    CollectFields(node, "MissionOffers.Responses[]");
    offers.Add(node);
}
string layoutInput = args.Length == 4 ? Path.GetFullPath(args[3]) : Path.Combine(input, "Layouts.json");
var layouts = JsonNode.Parse(File.ReadAllText(layoutInput))!.AsArray();
foreach (var node in layouts)
{
    var layout = node!.AsObject();string id=layout["LayoutId"]!.GetValue<string>();
    foreach(var name in new[]{"NpcSlots","ObjectiveSlots"})
        foreach(var slot in layout[name]!.AsArray())
        {
            var item=slot!.AsObject();
            Normalize(item, DecodePacket(item["RawPacketHex"]!.GetValue<string>(),id+"/"+name+"/"+item["Slot"]));
        }
    foreach(var dynel in layout["Dynels"]!.AsArray())
    {
        var item=dynel!.AsObject();var source=item["Wire"]!.AsObject();
        Normalize(source, DecodePacket(source["PacketHex"]!.GetValue<string>(),id+"/Dynels/"+item["Slot"]));
        source.Remove("RetargetSlots");
    }
    var exit=layout["Exit"]!.AsObject();
    Normalize(exit, DecodePacket(exit["RawPacketHex"]!.GetValue<string>(),id+"/Exit"));
    foreach(var derived in new[]{"Doors","Chests","Terminals","WireRecords","CompatibleMissionTypes","GeneratorPayloadSha256"}) layout.Remove(derived);
    layout["BundleFormatVersion"]=2;
}
RemoveHistoricalPayloads(layouts);
void RemoveHistoricalPayloads(JsonNode? value)
{
    if (value is JsonObject obj)
        foreach (var property in obj.ToArray())
        {
            if (new[] { "RawPacketHex", "PacketHex", "RawPacket", "PacketBytes", "RawBody", "UndecodedTail", "RetargetSlots" }
                .Contains(property.Key, StringComparer.OrdinalIgnoreCase)) obj.Remove(property.Key);
            else RemoveHistoricalPayloads(property.Value);
        }
    else if (value is JsonArray array) foreach (var child in array) RemoveHistoricalPayloads(child);
}
if(failures.Count>0) throw new InvalidDataException(string.Join(Environment.NewLine,failures));
Write("MissionOffers.json", new JsonObject { ["FormatVersion"] = 1, ["Responses"] = offers });
Write("Layouts.json",layouts);
Write("normalization-inventory.json",JsonSerializer.SerializeToNode(new { input, layoutInput, promotedFields, decoder_sha256=Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(engine))), responses=bodies.Length, records, fields },options)!);
Console.WriteLine(JsonSerializer.Serialize(new { responses=bodies.Length, layouts=layouts.Count, projections=records.Count, output }));
void Normalize(JsonObject target, JsonObject projection)
{
    var kind=projection["Kind"]!.GetValue<string>();
    if(kind=="DECODE_FAILED") return;
    var definition=projection["Definition"]!.AsObject();
    if(kind=="SimpleCharFullUpdateMessage")
        target["Appearance"]=new JsonObject {
            ["Scale"]=definition["MonsterScale"]!.DeepClone(), ["HeadMesh"]=definition["HeadMesh"]?.DeepClone(),
            ["Textures"]=definition["Textures"]?.DeepClone() ?? new JsonArray(), ["Meshes"]=definition["Meshes"]?.DeepClone() ?? new JsonArray()
        };
    else
    {
        string property=kind switch { "DoorFullUpdateMessage"=>"Door", "ChestItemFullUpdateMessage"=>"Chest", "SimpleItemFullUpdateMessage"=>"Item", "WeaponItemFullUpdateMessage"=>"Weapon", _=>throw new InvalidDataException("Unsupported mission object: "+kind) };
        target["Spawn"]=new JsonObject { [property]=definition.DeepClone() };
    }
    if (target["Appearance"] is { } appearance) CollectFields(appearance, "Layouts.NpcSlots[].Appearance");
    if (target["Spawn"] is { } spawn) CollectFields(spawn, "Layouts.Spawn");
    foreach(var field in new[]{"RawPacketHex","RawPacketSha256","PacketHex","PacketSha256"}) target.Remove(field);
}
void Write(string name,JsonNode value)
{
    using var stream=new FileStream(Path.Combine(output,name),FileMode.CreateNew);
    using var writer=new Utf8JsonWriter(stream,new JsonWriterOptions { Indented=true });
    value.WriteTo(writer,options);
}
sealed class DecoderContext(string path) : AssemblyLoadContext
{
    readonly AssemblyDependencyResolver resolver=new(path);
    readonly string directory=Path.GetDirectoryName(path)!;
    protected override Assembly? Load(AssemblyName name)
    {
        string? path=resolver.ResolveAssemblyToPath(name);
        if(path==null && File.Exists(Path.Combine(directory,name.Name+".dll"))) path=Path.Combine(directory,name.Name+".dll");
        return path==null ? null : LoadFromAssemblyPath(path);
    }
}
