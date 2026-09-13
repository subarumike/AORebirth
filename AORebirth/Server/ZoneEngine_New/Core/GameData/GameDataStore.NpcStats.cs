namespace ZoneEngine_New.Core.GameData;

using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using ZoneEngine_New.Core.Mobs;

public sealed partial class GameDataStore
{
    private NpcFamilyStatCatalog _npcFamilies = NpcFamilyStatCatalog.Build(null, _ => { });
    private NpcStatTemplateCatalog _npcOverlays = NpcStatTemplateCatalog.Build(null, _ => { });

    private void LoadNpcStatContent()
    {
        _npcFamilies = NpcFamilyStatCatalog.Build(Read<Dictionary<int, NpcFamilyStatTemplateData>>("NpcFamilyStatTemplates.json"),
            message => throw new InvalidDataException(message));
        _npcOverlays = NpcStatTemplateCatalog.Build(Read<Dictionary<int, NpcStatTemplateData>>("NpcStatTemplateOverlays.json"),
            message => throw new InvalidDataException(message));
        T? Read<T>(string file) => File.Exists(Path.Combine(RootPath, file))
            ? JsonSerializer.Deserialize<T>(File.ReadAllText(Path.Combine(RootPath, file)), CatalogJsonOptions) : default;
    }

    public Dictionary<int, int> ComposeNpcStats(MobTemplate template, int? level)
    {
        NpcFamilyStatTemplate? family = null;
        NpcStatTemplate? overlay = null;
        if (template.NpcFamily is { } familyId && !_npcFamilies.TryGet(familyId, out family))
            throw new InvalidDataException($"Missing exact NPC family {familyId} for '{template.Hash}'.");
        if (template.NpcStatTemplate != 0 && !_npcOverlays.TryGet(template.NpcStatTemplate, out overlay))
            throw new InvalidDataException($"Missing exact NPC overlay {template.NpcStatTemplate} for '{template.Hash}'.");
        if (family == null && overlay == null) return new Dictionary<int, int>(template.Stats);
        return MobStatResolver.Resolve(template, level, family, overlay);
    }
}
