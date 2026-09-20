namespace ZoneEngine_New.Core.GameData;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

using AORebirth.Core.GameData;

public sealed class PlayfieldNpcContentCatalog
{
    public static PlayfieldNpcContentCatalog Empty { get; } = new();

    public int SchemaVersion { get; set; } = 1;

    public int PlayfieldId { get; set; }

    public WorldNpcDefinition[] Npcs { get; set; } = [];

    public static PlayfieldNpcContentCatalog Load(string root, int playfieldId)
    {
        if (string.IsNullOrWhiteSpace(root) || playfieldId <= 0) return Empty;
        string path = Path.Combine(root, GameDataPaths.PlayfieldNpcsRelativePath(playfieldId));
        return File.Exists(path) ? Parse(File.ReadAllText(path), playfieldId) : Empty;
    }

    public static PlayfieldNpcContentCatalog Parse(string json, int expectedPlayfieldId)
    {
        PlayfieldNpcContentCatalog content = JsonSerializer.Deserialize<PlayfieldNpcContentCatalog>(
            json,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true, IncludeFields = true })
            ?? throw new InvalidDataException("Playfield NPC content is null.");
        content.Validate(expectedPlayfieldId);
        return content;
    }

    public void Validate(int expectedPlayfieldId)
    {
        if (SchemaVersion != 1 || PlayfieldId != expectedPlayfieldId || Npcs == null)
            throw new InvalidDataException("Unsupported or incomplete playfield NPC content document.");

        var keys = new HashSet<string>(StringComparer.Ordinal);
        var identities = new HashSet<int>();
        var shopIdentities = new HashSet<int>();
        foreach (WorldNpcDefinition npc in Npcs)
        {
            if (npc == null || npc.PlayfieldId != PlayfieldId || string.IsNullOrWhiteSpace(npc.Key)
                || !keys.Add(npc.Key) || npc.InstanceId <= 0 || !identities.Add(npc.InstanceId)
                || string.IsNullOrWhiteSpace(npc.Name) || npc.Stats == null || npc.Textures == null
                || npc.Meshes == null || npc.Presentation == null
                || (npc.HasDialogue && string.IsNullOrWhiteSpace(npc.ContentNpcIdentity)))
                throw new InvalidDataException("Invalid or duplicate playfield NPC definition.");

            WorldContentCatalog.RequireTransform(npc.Position, npc.Rotation, npc.Key);
            if (npc.Textures.Any(t => t == null || t.Place < 0 || t.Id < 0)
                || npc.Meshes.Any(m => m == null))
                throw new InvalidDataException("Invalid playfield NPC appearance: " + npc.Key);

            if (npc.Vendor != null)
            {
                WorldContentCatalog.RequireVendor(npc.Vendor);
                if (!shopIdentities.Add(npc.Vendor.InstanceId))
                    throw new InvalidDataException("Duplicate playfield shop identity.");
            }
        }
    }
}
