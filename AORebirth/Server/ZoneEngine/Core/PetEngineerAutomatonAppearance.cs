#region License

// Copyright (c) 2005-2014, CellAO Team
//
// All rights reserved.

#endregion

namespace ZoneEngine.Core
{
    #region Usings ...

    using System;

    using AORebirth.Core.Entities;
    using AORebirth.Core.Textures;
    using AORebirth.Enums;

    #endregion

    /// <summary>
    /// Capture-backed Engineer Automaton I pet body.
    /// Sources: 20260726-160216 (QL1/QL2 shells), 20260726-160924 (QL5 shell).
    /// Live SCFU uses MonsterData 17649 on an A004 body — not A120 (Anger Manifestation).
    /// </summary>
    internal static class PetEngineerAutomatonAppearance
    {
        public const int MonsterData = 17649;

        public const string PetName = "Engineer Automaton I";

        public const string PetHash = "PT50";

        private const int CharacterFlags = 268964353;

        private const int NpcFamily = 95;

        public static bool IsEngineerAutomatonHash(string petHash)
        {
            return !string.IsNullOrWhiteSpace(petHash)
                   && petHash.StartsWith(PetHash, StringComparison.OrdinalIgnoreCase);
        }

        public static bool IsEngineerAutomatonNano(int nanoId)
        {
            CapturedBureaucratShellDisplay ignored;
            return PetSummonNanoCatalog.TryGetEngineerShellDisplay(nanoId, out ignored);
        }

        public static void Apply(Character petCharacter, int petTypeId)
        {
            if (petCharacter == null)
            {
                return;
            }

            int level = Math.Max(1, petTypeId);
            int health;
            int scale;
            int runSpeed;
            ResolveTier(level, out health, out scale, out runSpeed);

            petCharacter.Name = PetName;
            SetStat(petCharacter, StatIds.monsterdata, MonsterData);
            SetStat(petCharacter, StatIds.level, level);
            SetStat(petCharacter, StatIds.life, health);
            SetStat(petCharacter, StatIds.health, health);
            SetStat(petCharacter, StatIds.monsterscale, scale);
            SetStat(petCharacter, StatIds.runspeed, runSpeed);
            SetStat(petCharacter, StatIds.flags, CharacterFlags);
            SetStat(petCharacter, StatIds.visualflags, 31);
            SetStat(petCharacter, StatIds.npcfamily, NpcFamily);
            SetStat(petCharacter, StatIds.side, 0);
            SetStat(petCharacter, StatIds.breed, 7);
            SetStat(petCharacter, StatIds.sex, 1);
            SetStat(petCharacter, StatIds.race, 1);
            SetStat(petCharacter, StatIds.headmesh, 0);

            if (petCharacter.Textures != null)
            {
                petCharacter.Textures.Clear();
                for (int i = 0; i < 5; i++)
                {
                    petCharacter.Textures.Add(new AOTextures(i, 0));
                }
            }

            if (petCharacter.MeshLayer != null)
            {
                petCharacter.MeshLayer.Clear();
            }

            if (petCharacter.SocialMeshLayer != null)
            {
                petCharacter.SocialMeshLayer.Clear();
            }
        }

        private static void ResolveTier(int level, out int health, out int scale, out int runSpeed)
        {
            // Exact capture rows; interpolate between known tiers for other QLs.
            if (level <= 1)
            {
                health = 30;
                scale = 90;
                runSpeed = 8;
                return;
            }

            if (level == 2)
            {
                health = 57;
                scale = 91;
                runSpeed = 15;
                return;
            }

            if (level >= 5)
            {
                // Capture 20260726-160924 own pet SCFU: hp 138, scale 93, run 29.
                double t = Math.Min(1.0, (level - 5) / 10.0);
                health = 138 + (int)(t * 40);
                scale = 93;
                runSpeed = 29;
                return;
            }

            // Lerp 2 → 5
            double u = (level - 2) / 3.0;
            health = 57 + (int)Math.Round((138 - 57) * u);
            scale = 91 + (int)Math.Round((93 - 91) * u);
            runSpeed = 15 + (int)Math.Round((29 - 15) * u);
        }

        private static void SetStat(Character pet, StatIds id, int value)
        {
            pet.Stats.SetBaseValueWithoutTriggering((int)id, (uint)value);
            pet.Stats[id].Value = value;
        }
    }
}
