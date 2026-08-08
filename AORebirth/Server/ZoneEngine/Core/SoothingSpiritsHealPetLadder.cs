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
    using System.Text;

    using AORebirth.Core.Entities;

    #endregion

    /// <summary>
    /// Capture-backed Soothing Spirits (UI: Shooting Spirits) heal-pet visual ladder.
    /// Texture id = 288730 + tier; capture 20260716-061522 (SS1=288731, SS8=288734).
    /// SS10 (tier 6) = 288736 purple metapet_healing.
    /// </summary>
    internal static class SoothingSpiritsHealPetLadder
    {
        public const int BaseMetapetHealingTextureId = 288730;

        /// <summary>Soothing Spirits 1 PacketID; ranks 1-10 are 720-729.</summary>
        private const int SoothingSpirits1PacketId = 720;

        private static readonly byte[] MetapetHealingName =
            Encoding.ASCII.GetBytes("metapet_healing\0");

        private static readonly HashSet<string> SoothingSpiritsUpgradeHashes =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "LYNX",
                "JBOB",
                "DKEL",
                "QRMT",
                "MNKW",
                "RHEF",
                // Capture 20260808-mp-pets: Calling of Restite (MT05) Soothing Spirits ladder.
                "TRXY",
                "KCIO",
                "MBYQ",
                "GWAD",
                "DSEJ",
                "SAFE",
            };

        private static readonly Dictionary<string, int> TextureTierBySpawnHash =
            new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
            {
                { "MT01", 0 },
                { "MT02", 0 },
                { "MT03", 0 },
                { "MT04", 0 },
                { "MT05", 0 },
                { "BSLX", 0 },
                { "LYNX", 1 },
                { "JBOB", 2 },
                { "DKEL", 3 },
                { "QRMT", 4 },
                { "MNKW", 5 },
                { "RHEF", 6 },
                { "TRXY", 1 },
                { "KCIO", 2 },
                { "MBYQ", 3 },
                { "GWAD", 4 },
                { "DSEJ", 5 },
                { "SAFE", 6 },
            };

        public static bool IsSoothingSpiritsUpgradeHash(string petHash)
        {
            return !string.IsNullOrWhiteSpace(petHash)
                && SoothingSpiritsUpgradeHashes.Contains(petHash);
        }

        /// <summary>Highest trained Soothing Spirits rank 0..10.</summary>
        public static int GetHighestRank(ICharacter owner)
        {
            Character character = owner as Character;
            if (character == null)
            {
                return 0;
            }

            character.EnsureTrainedPerks();
            for (int rank = 10; rank >= 1; rank--)
            {
                if (character.HasPerk(SoothingSpirits1PacketId + rank - 1))
                {
                    return rank;
                }
            }

            return 0;
        }

        /// <summary>
        /// Tier index 0..6 for metapet_healing (none / SS1-2 / SS3-4 / SS5-6 / SS7-8 / SS9 / SS10).
        /// </summary>
        public static int ResolveTextureTierFromRank(int soothingSpiritsRank)
        {
            if (soothingSpiritsRank <= 0)
            {
                return 0;
            }

            if (soothingSpiritsRank <= 2)
            {
                return 1;
            }

            if (soothingSpiritsRank <= 4)
            {
                return 2;
            }

            if (soothingSpiritsRank <= 6)
            {
                return 3;
            }

            if (soothingSpiritsRank <= 8)
            {
                return 4;
            }

            if (soothingSpiritsRank == 9)
            {
                return 5;
            }

            return 6;
        }

        public static int ResolveTextureIdFromRank(int soothingSpiritsRank)
        {
            return BaseMetapetHealingTextureId + ResolveTextureTierFromRank(soothingSpiritsRank);
        }

        public static int ResolveTextureId(ICharacter owner)
        {
            return ResolveTextureIdFromRank(GetHighestRank(owner));
        }

        public static int ResolveTextureIdFromSpawnHash(string petHash, ICharacter owner)
        {
            int tier;
            if (!string.IsNullOrWhiteSpace(petHash)
                && TextureTierBySpawnHash.TryGetValue(petHash, out tier))
            {
                // Prefer spawn-hash tier when nano gates selected LYNX..RHEF.
                if (tier > 0 || IsBaseHealPetHash(petHash))
                {
                    int fromHash = BaseMetapetHealingTextureId + tier;
                    int fromRank = ResolveTextureId(owner);
                    // Use the higher of hash-gate and trained rank (keeps purple if SS10 trained).
                    return Math.Max(fromHash, fromRank);
                }
            }

            return ResolveTextureId(owner);
        }

        public static bool TryPatchMetapetHealingTexture(byte[] data, int textureId)
        {
            if (data == null || textureId <= 0)
            {
                return false;
            }

            int nameOffset = IndexOf(data, MetapetHealingName);
            if (nameOffset < 0)
            {
                return false;
            }

            // Capture layout: name\0 + 16 zero pad + BE uint32 texture id.
            int textureOffset = nameOffset + MetapetHealingName.Length + 16;
            if (textureOffset + 4 > data.Length)
            {
                return false;
            }

            data[textureOffset] = (byte)((textureId >> 24) & 0xFF);
            data[textureOffset + 1] = (byte)((textureId >> 16) & 0xFF);
            data[textureOffset + 2] = (byte)((textureId >> 8) & 0xFF);
            data[textureOffset + 3] = (byte)(textureId & 0xFF);
            return true;
        }

        private static bool IsBaseHealPetHash(string petHash)
        {
            return string.Equals(petHash, "MT01", StringComparison.OrdinalIgnoreCase)
                || string.Equals(petHash, "MT02", StringComparison.OrdinalIgnoreCase)
                || string.Equals(petHash, "MT03", StringComparison.OrdinalIgnoreCase)
                || string.Equals(petHash, "MT04", StringComparison.OrdinalIgnoreCase)
                || string.Equals(petHash, "MT05", StringComparison.OrdinalIgnoreCase)
                || string.Equals(petHash, "BSLX", StringComparison.OrdinalIgnoreCase);
        }

        private static int IndexOf(byte[] haystack, byte[] needle)
        {
            if (haystack == null || needle == null || needle.Length == 0
                || haystack.Length < needle.Length)
            {
                return -1;
            }

            for (int i = 0; i <= haystack.Length - needle.Length; i++)
            {
                bool match = true;
                for (int j = 0; j < needle.Length; j++)
                {
                    if (haystack[i + j] != needle[j])
                    {
                        match = false;
                        break;
                    }
                }

                if (match)
                {
                    return i;
                }
            }

            return -1;
        }
    }
}
