namespace ZoneEngine_New.Core.Mobs;

using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>A captured alternative at explicit qualities; entries are not simultaneous weapon slots.</summary>
public sealed class NpcWeaponVariant
{
    public int LowId { get; set; }
    public int HighId { get; set; }
    public int[] Qualities { get; set; } = [];
    public string[] Evidence { get; set; } = [];

    public static NpcWeaponVariant RequireUnique(IEnumerable<NpcWeaponVariant> variants, int quality)
    {
        var matches = variants.Where(v => v.Qualities.Contains(quality)
            && v.LowId > 0 && v.HighId > 0).ToArray();
        if (matches.Length != 1) throw new InvalidOperationException(
            $"Weapon quality {quality} has {matches.Length} configured alternatives; exact loadout is required.");
        return matches[0];
    }
}
