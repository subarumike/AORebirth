namespace ZoneEngine_New.Core.Missions;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ZoneEngine.Core.Arete;
using ZoneEngine.Core.Arete.Quests;
using ZoneEngine.Core.Missions;

/// <summary>Accepted authored content and the existing definition rules, not synthesized quest state.</summary>
public sealed class AuthoredQuestCatalog
{
    public AuthoredQuestCatalog(IEnumerable<QuestContentPack> packs)
    {
        var registry = new QuestContentRegistry();
        var validation = registry.Load(packs);
        if (!validation.IsValid) throw new InvalidOperationException(string.Join("; ", validation.Errors));
        Definitions = MissionDefinitionCatalog.Build(registry).ToArray();
    }

    public IReadOnlyList<MissionDefinition> Definitions { get; }

    public static AuthoredQuestCatalog Load(string contentRoot)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(contentRoot);
        // Both paths are already in AreteFrameworkBootstrap's accepted manifest set.
        var manifests = new[]
        {
            Path.Combine(contentRoot, "Arete", "flint-novak", "manifest.json"),
            Path.Combine(contentRoot, "Doja", "nascense-chip", "manifest.json")
        };
        var paths = new List<string>();
        foreach (var path in manifests)
        {
            var manifest = new AreteContentManifestLoader().Load(path);
            if (!manifest.IsValid) throw new InvalidOperationException(string.Join("; ", manifest.Validation.Errors));
            paths.AddRange(manifest.QuestPackFiles);
        }
        var loaded = new QuestContentPackLoader().LoadFiles(paths.Distinct(StringComparer.OrdinalIgnoreCase));
        if (!loaded.IsValid) throw new InvalidOperationException(string.Join("; ", loaded.Validation.Errors));
        return new AuthoredQuestCatalog(loaded.Packs);
    }
}
