namespace ZoneEngine_New.Core.Mobs;

using System;

/// <summary>
/// Generic resolved-template safety. Evidence and provenance are never permissions.
/// Unresolved placeholders spawn as non-attackable markers so broken content stays visible in-world.
/// </summary>
public static class NpcTemplateValidation
{
    public static bool CanSpawn(MobTemplate template) => template != null
        && !string.IsNullOrWhiteSpace(template.Hash)
        && !string.IsNullOrWhiteSpace(template.Name)
        && template.Stats != null
        && (!template.UnresolvedPlaceholder || !template.Attackable);

    public static void RequireSpawnable(MobTemplate template)
    {
        if (!CanSpawn(template)) throw new InvalidOperationException("NPC template is unresolved or invalid.");
    }
}
