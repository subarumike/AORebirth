namespace ZoneEngine_New.Core.Mobs;

using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>Content authoring decisions, never inferred from a name, appearance or family fallback.</summary>
public sealed class NpcContentAcceptance
{
    public string Policy { get; set; } = "BLOCK_SPAWN";
    public bool IdentityResolved { get; set; }
    public bool CombatAccepted { get; set; }
    public bool Attackable { get; set; }
    public bool CombatAiEnabled { get; set; }
    public bool UnresolvedPlaceholder { get; set; }
    public string Source { get; set; } = string.Empty;
    public string[] Blockers { get; set; } = [];

    public static bool CanSpawn(MobTemplate template) => template.ContentAcceptance is not { } a
        || (a.Policy == "ACCEPTED" && a.IdentityResolved && !a.UnresolvedPlaceholder
            && a.CombatAccepted && a.Blockers.Length == 0 && !string.IsNullOrWhiteSpace(a.Source));

    public static void RequireSpawnable(MobTemplate template)
    {
        if (!CanSpawn(template)) throw new InvalidOperationException(
            $"NPC template '{template.Hash}' is blocked by its content acceptance policy.");
    }
}

/// <summary>A captured alternative at explicit qualities; entries are not simultaneous weapon slots.</summary>
public sealed class NpcWeaponVariant
{
    public int LowId { get; set; }
    public int HighId { get; set; }
    public int[] Qualities { get; set; } = [];
    public string[] Evidence { get; set; } = [];

    public static NpcWeaponVariant RequireUnique(IEnumerable<NpcWeaponVariant> variants, int quality)
    {
        var matches = variants.Where(v => v.Qualities.Contains(quality) && v.Evidence.Length > 0
            && v.LowId > 0 && v.HighId > 0).ToArray();
        if (matches.Length != 1) throw new InvalidOperationException(
            $"Weapon quality {quality} has {matches.Length} proven alternatives; exact loadout is required.");
        return matches[0];
    }
}
