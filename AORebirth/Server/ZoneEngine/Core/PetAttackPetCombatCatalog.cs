#region License

// Copyright (c) 2005-2014, CellAO Team
//
// All rights reserved.

#endregion

namespace ZoneEngine.Core
{
    #region Usings ...

    using System;
    using System.Collections.Generic;

    #endregion

    /// <summary>
    /// Capture-backed per-tier MP attack pet melee damage vs armored targets.
    /// Source: 20260711-192136 (Claw-C27 Outlaw L81-84), 20260710-220653 Demon (PT56).
    /// Low-level mob bands from 20260711-181536 retained only where 192136 has no outlaw hits (PT50).
    /// </summary>
    internal static class PetAttackPetCombatCatalog
    {
        internal sealed class Profile
        {
            public int MinDamage { get; set; }

            public int MaxDamage { get; set; }
        }

        private static readonly Dictionary<string, Profile> ProfilesByHashPrefix =
            new Dictionary<string, Profile>(StringComparer.OrdinalIgnoreCase)
            {
                {
                    "PT50",
                    new Profile
                    {
                        // Anger Manifestation L10: no outlaw hits in 192136; scaled from Fury armored ratio (~0.59)
                        MinDamage = 12,
                        MaxDamage = 18,
                    }
                },
                {
                    "PT51",
                    new Profile
                    {
                        // Fury Externalization L32 vs Claw-C27 Outlaw: normal 53 (114 crit handled separately)
                        MinDamage = 53,
                        MaxDamage = 53,
                    }
                },
                {
                    "PT52",
                    new Profile
                    {
                        // Rage Materialization L62 vs Claw-C27 Outlaw: 126-156
                        MinDamage = 126,
                        MaxDamage = 156,
                    }
                },
                {
                    "PT53",
                    new Profile
                    {
                        // Wrath Incarnation L95 vs Claw-C27 Outlaw: 220-344
                        MinDamage = 220,
                        MaxDamage = 344,
                    }
                },
                {
                    "PT54",
                    new Profile
                    {
                        // Frenzy Embodiment L137 vs Claw-C27 Outlaw: 419-642 (1042 was crit)
                        MinDamage = 419,
                        MaxDamage = 642,
                    }
                },
                {
                    "PT56",
                    new Profile
                    {
                        // Metaphysical Demon: capture-backed fallback band from 20260710-220653
                        MinDamage = 850,
                        MaxDamage = 930,
                    }
                },
            };

        public static bool TryGet(string petHash, out Profile profile)
        {
            profile = null;
            if (string.IsNullOrWhiteSpace(petHash))
            {
                return false;
            }

            string prefix = petHash.Length >= 4 ? petHash.Substring(0, 4) : petHash;
            return ProfilesByHashPrefix.TryGetValue(prefix, out profile);
        }
    }
}
