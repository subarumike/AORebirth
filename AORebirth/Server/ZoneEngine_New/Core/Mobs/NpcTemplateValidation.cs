namespace ZoneEngine_New.Core.Mobs;

using System;

/// <summary>Generic resolved-template safety. Evidence and provenance are never permissions.</summary>
public static class NpcTemplateValidation
{
    public static bool CanSpawn(MobTemplate template) => template != null
        && !string.IsNullOrWhiteSpace(template.Hash)
        && !string.Equals(template.Hash, MobTemplate.FallbackHash, StringComparison.Ordinal)
        && !template.UnresolvedPlaceholder && !string.IsNullOrWhiteSpace(template.Name)
        && template.Stats != null;

    public static void RequireSpawnable(MobTemplate template)
    {
        if (!CanSpawn(template)) throw new InvalidOperationException("NPC template is unresolved, invalid, or a placeholder.");
    }
}
