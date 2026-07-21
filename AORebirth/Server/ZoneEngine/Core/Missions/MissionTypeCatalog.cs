namespace ZoneEngine.Core.Missions
{
    /// <summary>
    /// RK mission types and the client journal icons captured from live rolls
    /// (capture 20260717-Mission terminal2 / pull-mish-doit).
    /// </summary>
    internal enum MissionRollType
    {
        KillPerson = 0,

        FindPerson = 1,

        FindItem = 2,

        RepairMachine = 3
    }

    internal static class MissionTypeCatalog
    {
        // Captured MissionIconId values (QuestInfo.MissionIconId / Quest.MissionIconId).
        internal const int KillPersonIcon = 11330; // 0x2C42 — accept capture kill mission

        internal const int FindPersonIcon = 11335; // 0x2C47

        internal const int FindItemIconA = 11329; // 0x2C41

        internal const int FindItemIconB = 11337; // 0x2C49

        internal const int RepairMachineIcon = 11342; // 0x2C4E

        /// <summary>
        /// Template offer indices inside <see cref="MissionRollCaptureTemplate"/> (0-based) that best match
        /// each type. The capture has no Kill offer, so Kill reuses a FindPerson shell and swaps the icon.
        /// </summary>
        internal static int ArchetypeIndex(MissionRollType type)
        {
            switch (type)
            {
                case MissionRollType.KillPerson:
                    return 0; // FindPerson shell → icon overridden to Kill
                case MissionRollType.FindPerson:
                    return 1;
                case MissionRollType.FindItem:
                    return 2;
                case MissionRollType.RepairMachine:
                    return 3;
                default:
                    return 0;
            }
        }

        internal static int IconId(MissionRollType type, int salt)
        {
            switch (type)
            {
                case MissionRollType.KillPerson:
                    return KillPersonIcon;
                case MissionRollType.FindPerson:
                    return FindPersonIcon;
                case MissionRollType.FindItem:
                    return (salt & 1) == 0 ? FindItemIconA : FindItemIconB;
                case MissionRollType.RepairMachine:
                    return RepairMachineIcon;
                default:
                    return FindPersonIcon;
            }
        }

        /// <summary>
        /// Maps a captured MissionIconId back to the roll type (capture 20260719-Rolling different mishes).
        /// </summary>
        internal static MissionRollType TypeFromIcon(int missionIconId)
        {
            if (missionIconId == KillPersonIcon)
            {
                return MissionRollType.KillPerson;
            }

            if (missionIconId == FindPersonIcon)
            {
                return MissionRollType.FindPerson;
            }

            if (missionIconId == FindItemIconA || missionIconId == FindItemIconB)
            {
                return MissionRollType.FindItem;
            }

            if (missionIconId == RepairMachineIcon)
            {
                return MissionRollType.RepairMachine;
            }

            return MissionRollType.FindPerson;
        }

        /// <summary>
        /// Fixed-size ShortInfo (32 chars) shown in the terminal list / journal title area.
        /// </summary>
        internal static string ShortTitle(MissionRollType type)
        {
            switch (type)
            {
                case MissionRollType.KillPerson:
                    return "Kill person mission";
                case MissionRollType.FindPerson:
                    return "Find person mission";
                case MissionRollType.FindItem:
                    return "Find item mission";
                case MissionRollType.RepairMachine:
                    return "Repair machine mission";
                default:
                    return "Mission";
            }
        }

        internal static string TypeName(MissionRollType type)
        {
            switch (type)
            {
                case MissionRollType.KillPerson:
                    return "KillPerson";
                case MissionRollType.FindPerson:
                    return "FindPerson";
                case MissionRollType.FindItem:
                    return "FindItem";
                case MissionRollType.RepairMachine:
                    return "RepairMachine";
                default:
                    return "Unknown";
            }
        }

        /// <summary>
        /// Builds a 5-offer type mix that changes every roll and is never five-of-a-kind.
        /// </summary>
        internal static MissionRollType[] NextMix(System.Random rng)
        {
            var mix = new MissionRollType[5];
            var bag = new[]
                      {
                          MissionRollType.KillPerson,
                          MissionRollType.KillPerson,
                          MissionRollType.FindPerson,
                          MissionRollType.FindPerson,
                          MissionRollType.FindItem,
                          MissionRollType.FindItem,
                          MissionRollType.RepairMachine,
                          MissionRollType.RepairMachine
                      };

            // Shuffle bag and take 5, then ensure at least 2 distinct types.
            for (int i = bag.Length - 1; i > 0; i--)
            {
                int j = rng.Next(i + 1);
                MissionRollType tmp = bag[i];
                bag[i] = bag[j];
                bag[j] = tmp;
            }

            for (int i = 0; i < 5; i++)
            {
                mix[i] = bag[i];
            }

            if (CountDistinct(mix) < 2)
            {
                mix[0] = MissionRollType.KillPerson;
                mix[1] = MissionRollType.FindPerson;
                mix[2] = MissionRollType.FindItem;
                mix[3] = MissionRollType.RepairMachine;
                mix[4] = MissionRollType.FindPerson;
            }

            // Final shuffle of the five slots so order varies too.
            for (int i = 4; i > 0; i--)
            {
                int j = rng.Next(i + 1);
                MissionRollType tmp = mix[i];
                mix[i] = mix[j];
                mix[j] = tmp;
            }

            return mix;
        }

        private static int CountDistinct(MissionRollType[] mix)
        {
            int mask = 0;
            for (int i = 0; i < mix.Length; i++)
            {
                mask |= 1 << (int)mix[i];
            }

            int count = 0;
            while (mask != 0)
            {
                count += mask & 1;
                mask >>= 1;
            }

            return count;
        }

    }
}
