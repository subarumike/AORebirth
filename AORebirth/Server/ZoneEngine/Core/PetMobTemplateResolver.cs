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

    using AORebirth.Database.Dao;

    #endregion

    internal static class PetMobTemplateResolver
    {
        private static readonly Dictionary<string, string> PrefixFallbacks =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                { "PT50", "A120" },
                { "PT51", "A020" },
                { "PT52", "A120" },
                { "PT53", "A120" },
                { "PT54", "A020" },
                { "PT55", "A120" },
                { "PT56", "A120" },
            };

        // Soothing Spirits SpawnPet hashes (LYNX..RHEF) share the base heal-pet mob row.
        private static readonly Dictionary<string, string> SoothingSpiritsHashFallbacks =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                { "LYNX", "MT02" },
                { "JBOB", "MT02" },
                { "DKEL", "MT02" },
                { "QRMT", "MT02" },
                { "MNKW", "MT02" },
                { "RHEF", "MT02" },
            };

        public static string Resolve(string petHash)
        {
            return Resolve(petHash, null);
        }

        public static string Resolve(string petHash, string preferredBaseHash)
        {
            if (string.IsNullOrWhiteSpace(petHash))
            {
                return null;
            }

            if (MobTemplateDao.Instance.GetMobTemplateByHash(petHash) != null)
            {
                return petHash;
            }

            if (!string.IsNullOrWhiteSpace(preferredBaseHash)
                && SoothingSpiritsHealPetLadder.IsSoothingSpiritsUpgradeHash(petHash)
                && MobTemplateDao.Instance.GetMobTemplateByHash(preferredBaseHash) != null)
            {
                return preferredBaseHash;
            }

            string soothingFallback;
            if (SoothingSpiritsHashFallbacks.TryGetValue(petHash, out soothingFallback)
                && MobTemplateDao.Instance.GetMobTemplateByHash(soothingFallback) != null)
            {
                return soothingFallback;
            }

            string prefix = petHash.Length >= 4 ? petHash.Substring(0, 4) : petHash;
            string mapped;
            if (PrefixFallbacks.TryGetValue(prefix, out mapped)
                && MobTemplateDao.Instance.GetMobTemplateByHash(mapped) != null)
            {
                return mapped;
            }

            return null;
        }
    }
}
