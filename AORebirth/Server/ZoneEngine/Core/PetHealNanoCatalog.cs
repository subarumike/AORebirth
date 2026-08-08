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

    using AORebirth.Core.Entities;
    using AORebirth.Core.Events;
    using AORebirth.Core.Functions;
    using AORebirth.Core.Nanos;
    using AORebirth.Enums;

    #endregion

    /// <summary>
    /// Capture-backed MP heal-pet nanos from 20260711-022256 (Belamorte) and 20260711-195926 (MT01-MT04).
    /// </summary>
    internal static class PetHealNanoCatalog
    {
        public const int BelamorteBlessingNanoId = 125720;

        public const int ValentyiaHeatNanoId = 125721;

        public const int SalvinousTouchNanoId = 125722;

        public const int SanooPulseNanoId = 125723;

        public const int MedinosWhisperNanoId = 125728;

        public const int RestiteBloodAnvilNanoId = 125724;

        public const int PetNanoExecutedWithinOwnerNcuAction = 0x00000081;

        private static readonly int HitFunctionId = (int)FunctionType.Hit;

        private static readonly int HealthStatId = (int)StatIds.health;

        private static readonly Dictionary<int, int> HealNanoBySummonNano =
            new Dictionary<int, int>
            {
                { 125738, MedinosWhisperNanoId },
                { 125742, RestiteBloodAnvilNanoId },
                { 125743, SanooPulseNanoId },
                { 125744, ValentyiaHeatNanoId },
                { 125745, SalvinousTouchNanoId },
                { 125746, BelamorteBlessingNanoId },
            };

        private static readonly Dictionary<string, int> HealNanoByPetHash =
            new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
            {
                { "MT01", MedinosWhisperNanoId },
                { "MT02", SalvinousTouchNanoId },
                { "MT03", ValentyiaHeatNanoId },
                { "MT04", SanooPulseNanoId },
                { "MT05", RestiteBloodAnvilNanoId },
                { "BSLX", BelamorteBlessingNanoId },
                { "TRXY", RestiteBloodAnvilNanoId },
                { "KCIO", RestiteBloodAnvilNanoId },
                { "MBYQ", RestiteBloodAnvilNanoId },
                { "GWAD", RestiteBloodAnvilNanoId },
                { "DSEJ", RestiteBloodAnvilNanoId },
                { "SAFE", RestiteBloodAnvilNanoId },
            };

        private static readonly Dictionary<int, string> HealNanoDisplayName =
            new Dictionary<int, string>
            {
                { BelamorteBlessingNanoId, "Belamorte's Blessing" },
                { ValentyiaHeatNanoId, "Valentyia's Heat" },
                { SalvinousTouchNanoId, "Touch of Salvinous" },
                { SanooPulseNanoId, "Pulse of Sanoo" },
                { MedinosWhisperNanoId, "Whisper of Medinos" },
                { RestiteBloodAnvilNanoId, "Blood Anvil of Restite" },
            };

        private static readonly Dictionary<string, int> HealingPetNanoPoolByHash =
            new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
            {
                { "MT01", 379 },
                { "MT02", 1207 },
                { "MT03", 2370 },
                { "MT04", 3767 },
                // Capture 20260808-mp-pets: Restite L99; interpolate between Sanoo and Belamorte.
                { "MT05", 5500 },
                { "BSLX", 13184 },
            };

        private static readonly Dictionary<int, double> HealRechargeSecondsByNano =
            new Dictionary<int, double>
            {
                { MedinosWhisperNanoId, 8.0 },
                { SalvinousTouchNanoId, 8.9 },
                { ValentyiaHeatNanoId, 12.0 },
                { SanooPulseNanoId, 8.7 },
                { RestiteBloodAnvilNanoId, 9.0 },
                { BelamorteBlessingNanoId, 6.0 },
            };

        public static bool TryResolveHealNano(int summonNanoId, string petHash, out int healNanoId)
        {
            if (summonNanoId > 0 && HealNanoBySummonNano.TryGetValue(summonNanoId, out healNanoId))
            {
                return true;
            }

            healNanoId = 0;
            if (string.IsNullOrWhiteSpace(petHash))
            {
                return false;
            }

            return HealNanoByPetHash.TryGetValue(petHash, out healNanoId);
        }

        public static string GetHealNanoDisplayName(int healNanoId)
        {
            string displayName;
            return HealNanoDisplayName.TryGetValue(healNanoId, out displayName)
                ? displayName
                : "Heal";
        }

        public static bool TryGetHealingPetNanoPool(string petHash, out int currentNano, out int maxNano)
        {
            currentNano = 0;
            maxNano = 0;
            if (string.IsNullOrWhiteSpace(petHash))
            {
                return false;
            }

            if (!HealingPetNanoPoolByHash.TryGetValue(petHash, out currentNano))
            {
                return false;
            }

            maxNano = currentNano;
            return true;
        }

        public static double GetHealRechargeSeconds(int healNanoId)
        {
            double rechargeSeconds;
            return HealRechargeSecondsByNano.TryGetValue(healNanoId, out rechargeSeconds)
                ? rechargeSeconds
                : 9.0;
        }

        public static int GetNanoCastCost(NanoFormula nano)
        {
            if (nano == null)
            {
                return 0;
            }

            int cost = nano.getItemAttribute(407);
            return cost > 0 ? cost : nano.NCUCost();
        }

        public static bool TryRollHealAmount(NanoFormula nano, ICharacter target, out int healRoll, out int healApplied)
        {
            healRoll = 0;
            healApplied = 0;
            if (nano == null || target == null)
            {
                return false;
            }

            int minHeal;
            int maxHeal;
            if (!TryGetHealthHitRange(nano, out minHeal, out maxHeal))
            {
                return false;
            }

            if (minHeal > maxHeal)
            {
                int swap = minHeal;
                minHeal = maxHeal;
                maxHeal = swap;
            }

            healRoll = minHeal == maxHeal
                ? minHeal
                : new Random().Next(minHeal, maxHeal);

            int missingHealth = target.Stats[StatIds.life].Value - target.Stats[StatIds.health].Value;
            if (missingHealth <= 0)
            {
                return true;
            }

            healApplied = Math.Min(healRoll, missingHealth);
            return true;
        }

        private static bool TryGetHealthHitRange(NanoFormula nano, out int minHeal, out int maxHeal)
        {
            minHeal = 0;
            maxHeal = 0;
            if (nano.Events == null)
            {
                return false;
            }

            foreach (Event nanoEvent in nano.Events)
            {
                if (nanoEvent.EventType != EventType.OnUse || nanoEvent.Functions == null)
                {
                    continue;
                }

                foreach (Function function in nanoEvent.Functions)
                {
                    if (function.FunctionType != HitFunctionId
                        || function.Arguments == null
                        || function.Arguments.Values.Count < 3)
                    {
                        continue;
                    }

                    if (function.Arguments.Values[0].AsInt32() != HealthStatId)
                    {
                        continue;
                    }

                    minHeal = function.Arguments.Values[1].AsInt32();
                    maxHeal = function.Arguments.Values[2].AsInt32();
                    return true;
                }
            }

            return false;
        }
    }
}
