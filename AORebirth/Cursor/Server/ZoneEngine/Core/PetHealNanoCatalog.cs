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
    /// Capture-backed MP heal-pet nanos from 20260711-022256 (Belamorte's Blessing).
    /// </summary>
    internal static class PetHealNanoCatalog
    {
        public const int BelamorteBlessingNanoId = 125720;

        public const int PetNanoExecutedWithinOwnerNcuAction = 0x00000081;

        private static readonly int HitFunctionId = (int)FunctionType.Hit;

        private static readonly int HealthStatId = (int)StatIds.health;

        private static readonly Dictionary<int, int> HealNanoBySummonNano =
            new Dictionary<int, int>
            {
                { 125746, BelamorteBlessingNanoId },
            };

        private static readonly Dictionary<string, int> HealNanoByPetHash =
            new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
            {
                { "BSLX", BelamorteBlessingNanoId },
            };

        private static readonly Dictionary<int, string> HealNanoDisplayName =
            new Dictionary<int, string>
            {
                { BelamorteBlessingNanoId, "Belamorte's Blessing" },
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
