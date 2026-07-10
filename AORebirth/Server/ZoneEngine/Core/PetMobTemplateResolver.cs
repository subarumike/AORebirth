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

        public static string Resolve(string petHash)
        {
            if (string.IsNullOrWhiteSpace(petHash))
            {
                return null;
            }

            if (MobTemplateDao.Instance.GetMobTemplateByHash(petHash) != null)
            {
                return petHash;
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
