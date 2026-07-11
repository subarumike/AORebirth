#region License

// Copyright (c) 2005-2014, CellAO Team
//
// All rights reserved.

#endregion

namespace ZoneEngine.Core
{
    #region Usings ...

    using System;

    #endregion

    internal static class PetSlotClassifier
    {
        public const int RegularPetStrain = 1015;

        public const int HealingPetStrain = 1016;

        public const int HealingSpellListSlot = 2;

        public const int RegularSpellListSlot = 5;

        // Live AO pet summons (20260710-185528 / 20260711-013417) always publish this PetState value.
        public const int CapturedPetStateValue = 2304001;

        public static int ResolveStrain(string petHash)
        {
            if (string.IsNullOrWhiteSpace(petHash))
            {
                return 0;
            }

            if (petHash.StartsWith("PT", StringComparison.OrdinalIgnoreCase))
            {
                return RegularPetStrain;
            }

            return HealingPetStrain;
        }

        public static int ResolveSpellListSlot(int petSlotStrain)
        {
            return petSlotStrain == HealingPetStrain
                ? HealingSpellListSlot
                : RegularSpellListSlot;
        }
    }
}
