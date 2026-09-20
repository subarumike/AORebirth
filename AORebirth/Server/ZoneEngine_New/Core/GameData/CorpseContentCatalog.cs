namespace ZoneEngine_New.Core.GameData;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

using AORebirth.Core.GameData;

using SmokeLounge.AOtomation.Messaging.GameData;

public sealed class CorpseContentCatalog
{
    public static CorpseContentCatalog Empty { get; } = new();

    public int SchemaVersion { get; set; } = 1;

    public int Flags { get; set; }

    public AnimationEffect[] AnimationEffects { get; set; } = [];

    public Dictionary<int, int> MonsterDataAliases { get; set; } = new();

    public static CorpseContentCatalog Load(string root)
    {
        if (string.IsNullOrWhiteSpace(root))
            return Empty;

        string path = Path.Combine(root, GameDataPaths.CorpseFileName);
        return File.Exists(path) ? Parse(File.ReadAllText(path)) : Empty;
    }

    public static CorpseContentCatalog Parse(string json)
    {
        CorpseContentCatalog content = JsonSerializer.Deserialize<CorpseContentCatalog>(
            json,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
            ?? throw new InvalidDataException("Corpse content is null.");
        content.Validate();
        return content;
    }

    public void Validate()
    {
        if (SchemaVersion != 1
            || AnimationEffects == null
            || MonsterDataAliases == null
            || MonsterDataAliases.Any(pair => pair.Key <= 0 || pair.Value <= 0))
        {
            throw new InvalidDataException("Invalid corpse content.");
        }
    }
}
