namespace ZoneEngine_New.Core.GameData;

using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;

using AORebirth.Core.GameData;

using ZoneEngine_New.Core.Mobs;

/// <summary>Shares the exact retained NPC templates and spawn features across all dungeon styles that reference them.</summary>
internal sealed class RetainedMissionNpcContent(string gameDataRoot)
{
    internal const string ReferenceFile = "NpcCatalog.json";
    internal const string Folder = "Missions/DungeonContent/NpcCatalog";
    internal const string IdentityHeader = "AORebirth dungeon NPC content v1\n";
    const int SchemaVersion = 1;

    // NPC catalogs are large. The style cache keeps only their identities so it cannot extend this bound.
    const int MaxRetainedNpcCatalogs = 2;
    readonly RetainedResourceCache<string, NpcTemplateCatalog> _catalogs = new(MaxRetainedNpcCatalogs);

    sealed record Reference(int SchemaVersion, string Sha256);

    internal NpcTemplateCatalog ResolveStyleDependency(ReadOnlySpan<byte> linkJson)
        => Resolve(ReadStyleDependency(linkJson));

    internal NpcTemplateCatalog Resolve(string identity) => _catalogs.Get(identity, Load);

    internal static string ReadStyleDependency(ReadOnlySpan<byte> linkJson)
    {
        Reference? reference = JsonSerializer.Deserialize<Reference>(linkJson, RetainedDungeonContent.JsonOptions);
        if (reference is not { SchemaVersion: SchemaVersion, Sha256.Length: 64 }
            || reference.Sha256.Any(character => character is not (>= '0' and <= '9' or >= 'a' and <= 'f')))
            throw new InvalidDataException("The accepted dungeon has an invalid retained NPC catalog reference.");

        return reference.Sha256;
    }

    NpcTemplateCatalog Load(string identity)
    {
        string directory = Folder + "/" + identity + "/";
        byte[] rules = Read(MonsterCombatRulesCatalog.FileName);
        byte[] templates = Read(GameDataPaths.NpcTemplatesFileName);
        string review = IdentityHeader
            + MonsterCombatRulesCatalog.FileName + " " + RetainedDungeonContent.Hash(rules) + "\n"
            + GameDataPaths.NpcTemplatesFileName + " " + RetainedDungeonContent.Hash(templates) + "\n";
        if (!string.Equals(RetainedDungeonContent.Hash(Encoding.UTF8.GetBytes(review)), identity, StringComparison.Ordinal))
            throw new InvalidDataException("Retained mission NPC catalog digest mismatch: " + identity);

        var rulesByHash = MonsterCombatRulesCatalog.ParseRetainedRules(WithoutBom(rules));
        NpcTemplateCatalog catalog = NpcTemplateCatalog.ParseUtf8(WithoutBom(templates));
        if (catalog.LeafCount == 0)
            throw new InvalidDataException("Retained mission NPC catalog has no templates: " + identity);
        catalog.RuleFeatures = hash => rulesByHash.TryGetValue(hash, out MonsterCombatRules? rule) ? rule.Features : null;
        return catalog;

        byte[] Read(string name) => GameDataContentProvider.ReadCanonicalFile(gameDataRoot, directory + name)
            ?? throw new InvalidDataException("Retained mission NPC catalog " + identity + " is missing '" + name + "'.");
    }

    static ReadOnlySpan<byte> WithoutBom(ReadOnlySpan<byte> bytes)
        => bytes.StartsWith("\uFEFF"u8) ? bytes[3..] : bytes;
}
