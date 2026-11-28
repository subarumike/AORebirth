namespace ZoneEngine_New.Core.GameData;

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using AORebirth.Core.GameData;
using AORebirth.DungeonGenerator;
using AORebirth.World.Collision;
using ZoneEngine_New.Core.Quests.Dungeons;

/// <summary>Resolves private, content-addressed original dungeon resources from the selected GameData tree.</summary>
internal sealed class RetainedDungeonContent(string gameDataRoot)
{
    internal const string Folder = "Missions/DungeonContent";
    internal const string CurrentFile = Folder + "/Current.json";
    const string IdentityHeader = "AORebirth dungeon content v1\n";

    readonly ConcurrentDictionary<DungeonContentReference, Lazy<FrozenDungeonStyle>> _retained = new();
    readonly RetainedMissionNpcContent _npcs = new(gameDataRoot);

    internal static readonly JsonSerializerOptions JsonOptions = new()
    {
        IncludeFields = true,
        PropertyNameCaseInsensitive = true
    };

    internal FrozenDungeonStyle Resolve(DungeonContentReference reference)
    {
        if (!reference.IsValid)
            throw new InvalidDataException("Invalid retained dungeon content reference.");

        // A published snapshot owns parsed bytes for this specific content identity.
        // Missing or invalid files do not become a cached successful or partial snapshot.
        var candidate = _retained.GetOrAdd(reference, key => new(() => Load(key)));
        try
        {
            return candidate.Value;
        }
        catch
        {
            _retained.TryRemove(new KeyValuePair<DungeonContentReference, Lazy<FrozenDungeonStyle>>(reference, candidate));
            throw;
        }
    }

    FrozenDungeonStyle Load(DungeonContentReference reference)
    {
        int style = reference.StyleId;
        string directory = Folder + "/" + reference.Sha256;
        string styleFolder = "Playfields/" + style.ToString(CultureInfo.InvariantCulture) + "/";
        string[] required =
        [
            DungeonLayoutGenerator.GenerationFileName, DungeonLayoutGenerator.DoorStylesFileName, DungeonEntranceMap.FileName,
            GameDataPaths.DynelCollisionFileName, "NpcCatalog.json",
            styleFolder + GameDataPaths.MetadataFileName, styleFolder + GameDataPaths.RoomsFileName,
            styleFolder + GameDataPaths.DistrictsFileName, styleFolder + GameDataPaths.SurfacesFileName,
            styleFolder + "DCGA.png", styleFolder + "GNDA.png"
        ];
        var bytes = new Dictionary<string, byte[]>(StringComparer.Ordinal);
        var identity = new StringBuilder(IdentityHeader);
        foreach (string path in required.Order(StringComparer.Ordinal))
        {
            byte[] file = GameDataContentProvider.ReadCanonicalFile(gameDataRoot, directory + "/" + path)
                ?? throw new InvalidDataException($"Retained dungeon content {reference.Sha256} is missing '{path}'.");
            if (file.Length == 0)
                throw new InvalidDataException($"Retained dungeon content {reference.Sha256} has an empty '{path}'.");
            bytes.Add(path, file);
            identity.Append(path).Append(' ').Append(Hash(file)).Append('\n');
        }

        if (!string.Equals(Hash(Encoding.UTF8.GetBytes(identity.ToString())), reference.Sha256, StringComparison.Ordinal))
            throw new InvalidDataException($"Retained dungeon content digest mismatch for style {style}: {reference.Sha256}.");

        byte[] generationBytes = bytes[DungeonLayoutGenerator.GenerationFileName];
        byte[] doorBytes = bytes[DungeonLayoutGenerator.DoorStylesFileName];
        DungeonGenerationPolicy policy = DungeonGenerationPolicy.Parse(generationBytes);
        DungeonDoorStyle door = DungeonGenerationContent.ParseDoors(doorBytes).TryGetValue(style, out var styleDoor)
            ? styleDoor : throw new InvalidDataException($"Retained dungeon content has no door style for {style}.");

        string districtsPath = styleFolder + GameDataPaths.DistrictsFileName;
        var districts = JsonSerializer.Deserialize<PlayfieldDistrictsData>(bytes[districtsPath], JsonOptions)
            ?? throw new InvalidDataException($"Invalid retained district data for style {style}.");
        if (districts.SchemaVersion != PlayfieldDistrictsData.SupportedSchemaVersion || districts.RecordId != style
            || districts.Districts is not { Length: > 0 }
            || districts.Districts.Select(district => district.DistrictIndex).Distinct().Count() != districts.Districts.Length)
            throw new InvalidDataException($"Invalid retained district definitions in '{districtsPath}'.");

        var dynel = JsonSerializer.Deserialize<DynelCollisionData>(bytes[GameDataPaths.DynelCollisionFileName], JsonOptions);
        if (dynel?.SchemaVersion != DynelCollisionData.SupportedSchemaVersion
            || dynel.PlayerSphere is not { Radius: > 0, CenterY: > 0 } player
            || !float.IsFinite(player.Radius) || !float.IsFinite(player.CenterY))
            throw new InvalidDataException("Retained dungeon content has no valid player collision sphere.");

        DungeonStyleCatalog catalog = DungeonStyleCatalog.Load(style, name => bytes[styleFolder + name]);
        var entrances = DungeonEntranceMap.Parse(bytes[DungeonEntranceMap.FileName], style);
        return new(reference, Hash(generationBytes), Hash(doorBytes), Path.Combine(gameDataRoot, directory), policy, door,
            catalog, StyleRoomSet.Load(catalog, entrances), districts, player, _npcs.ResolveStyleDependency(bytes["NpcCatalog.json"]));
    }

    internal static string Hash(ReadOnlySpan<byte> bytes) => Convert.ToHexStringLower(SHA256.HashData(bytes));
}

internal sealed record FrozenDungeonStyle(DungeonContentReference Reference, string GenerationSha, string DoorSha,
    string Root, DungeonGenerationPolicy Policy, DungeonDoorStyle Doors, DungeonStyleCatalog Catalog,
    StyleRoomSet RoomSet, PlayfieldDistrictsData Districts, CollisionSphereData PlayerSphere, NpcTemplateCatalog NpcTemplates);
