namespace ZoneEngine_New.Core.GameData;

using System;
using System.IO;
using System.Linq;
using System.Text.Json;

using AORebirth.Core.GameData;

public sealed class RespawnContentCatalog
{
    public static RespawnContentCatalog Empty { get; } = new();

    public int SchemaVersion { get; set; } = 1;

    public int PlayfieldId { get; set; }

    public float[] Position { get; set; } = [];

    public static RespawnContentCatalog Load(string root)
    {
        if (string.IsNullOrWhiteSpace(root))
            return Empty;

        string path = Path.Combine(root, GameDataPaths.RespawnFileName);
        return File.Exists(path) ? Parse(File.ReadAllText(path)) : Empty;
    }

    public static RespawnContentCatalog Parse(string json)
    {
        RespawnContentCatalog content = JsonSerializer.Deserialize<RespawnContentCatalog>(
            json,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
            ?? throw new InvalidDataException("Respawn content is null.");
        content.Validate();
        return content;
    }

    public void Validate()
    {
        if (SchemaVersion != 1
            || PlayfieldId <= 0
            || Position is not { Length: 3 }
            || Position.Any(value => !float.IsFinite(value)))
        {
            throw new InvalidDataException("Invalid respawn content.");
        }
    }
}
