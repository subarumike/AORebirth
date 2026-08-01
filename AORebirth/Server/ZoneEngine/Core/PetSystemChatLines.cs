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

    #endregion

    /// <summary>
    /// Owner-only pet SystemMessage dialogue (chat type 35).
    /// Defaults: Worker / CEO Guardian (20260731-054922, 20260731-072612).
    /// Carlo Pinnetti: capture 20260731-072612.
    /// </summary>
    internal static class PetSystemChatLines
    {
        // Shared Worker / CEO Guardian / default attack pets.
        private const string DefaultSpawn =
            "Hello master. I'm ready to obey your commands...";

        private const string DefaultFollow =
            "I will follow you wherever you go, master.";

        private const string DefaultWait = "I will wait here.";

        private const string DefaultAttack = "Charge!";

        private const string DefaultGuard =
            "I will protect you to the best of my ability.";

        private const string DefaultBehind =
            "I will stay out of it until you need me again, master.";

        private const string DefaultTerminate = "Deactivating...";

        // Capture 20260731-072612: CEO Guardian all-pets dismiss — wish then Deactivating.
        private const string CeoTerminateFarewell =
            "If that is your wish, master...";

        // Capture 20260731-072612: Carlo Pinnetti.
        private const string CarloSpawn =
            "I'll destroy anyone opposing our initiatives.";

        private const string CarloFollowBehind =
            "I'll be right behind you.  Don't answer any questions.";

        private const string CarloWait =
            "Sure I'll wait...I'm billing by the hour.";

        private const string CarloGuard =
            "If anyone bothers you I'll bury them in a year of paperwork.";

        private const string CarloAttack = "I'll fix his habius corpus!";

        private const string CarloTerminateCiao = "Ciao!";

        private const string CarloTerminateAppointment =
            "I have another appointment.  Call my office if you need me.";

        public static string Spawn(ICharacter pet)
        {
            if (IsCarlo(pet))
            {
                return CarloSpawn;
            }

            return DefaultSpawn;
        }

        public static string Follow(ICharacter pet)
        {
            if (IsCarlo(pet))
            {
                // Capture: PetCommand Follow (id=1) → behind-you line.
                return CarloFollowBehind;
            }

            return DefaultFollow;
        }

        public static string Behind(ICharacter pet)
        {
            if (IsCarlo(pet))
            {
                return CarloFollowBehind;
            }

            return DefaultBehind;
        }

        public static string Wait(ICharacter pet)
        {
            if (IsCarlo(pet))
            {
                return CarloWait;
            }

            return DefaultWait;
        }

        public static string Guard(ICharacter pet)
        {
            if (IsCarlo(pet))
            {
                return CarloGuard;
            }

            return DefaultGuard;
        }

        public static string Attack(ICharacter pet)
        {
            if (IsCarlo(pet))
            {
                return CarloAttack;
            }

            return DefaultAttack;
        }

        /// <summary>
        /// Capture 20260731-072612 all-pets terminate order:
        /// CEO wish, Carlo Ciao, CEO Deactivating, Carlo appointment —
        /// first farewell for each pet, then deactivation for each.
        /// Worker: single Deactivating line (secondary null).
        /// </summary>
        public static void GetTerminateLines(
            ICharacter pet,
            out string farewell,
            out string deactivation)
        {
            farewell = null;
            deactivation = DefaultTerminate;

            if (IsCarlo(pet))
            {
                farewell = CarloTerminateCiao;
                deactivation = CarloTerminateAppointment;
                return;
            }

            if (IsCeoGuardian(pet))
            {
                farewell = CeoTerminateFarewell;
                deactivation = DefaultTerminate;
            }
        }

        private static bool IsCarlo(ICharacter pet)
        {
            return pet != null
                   && !string.IsNullOrEmpty(pet.Name)
                   && string.Equals(pet.Name, "Carlo Pinnetti", StringComparison.Ordinal);
        }

        private static bool IsCeoGuardian(ICharacter pet)
        {
            return pet != null
                   && !string.IsNullOrEmpty(pet.Name)
                   && string.Equals(pet.Name, "CEO Guardian", StringComparison.Ordinal);
        }
    }
}
