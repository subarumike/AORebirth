namespace ZoneEngine.Core.Missions
{
    /// <summary>
    /// RK mission types and client journal icons from the live roll fixtures, with Find/Return behavior
    /// resolved by finalized lifecycle captures 20260728-003410 and 20260728-005042.
    /// Five gameplay types: KillPerson, FindPerson, FindItem, FindItemReturn, RepairMachine.
    /// </summary>
    internal enum MissionRollType
    {
        Unknown = -1,

        KillPerson = 0,

        FindPerson = 1,

        FindItem = 2,

        RepairMachine = 3,

        FindItemReturn = 4
    }

    internal static class MissionTypeCatalog
    {
        // Captured MissionIconId values (QuestInfo.MissionIconId / Quest.MissionIconId).
        internal const int KillPersonIcon = 11330; // 0x2C42 — accept capture kill mission

        internal const int FindPersonIcon = 11335; // 0x2C47

        internal const int ReturnItemIcon = 11329; // 0x2C41 — recover item and return it to the terminal

        internal const int FindItemIcon = 11337; // 0x2C49 — locate/pick up item; no terminal return

        internal const int RepairMachineIcon = 11342; // 0x2C4E

        internal static int IconId(MissionRollType type, int salt)
        {
            switch (type)
            {
                case MissionRollType.KillPerson:
                    return KillPersonIcon;
                case MissionRollType.FindPerson:
                    return FindPersonIcon;
                case MissionRollType.FindItem:
                    return FindItemIcon;
                case MissionRollType.FindItemReturn:
                    return ReturnItemIcon;
                case MissionRollType.RepairMachine:
                    return RepairMachineIcon;
                default:
                    return 0;
            }
        }

        /// <summary>
        /// Maps a captured MissionIconId back to the roll type. Finalized accepted missions prove
        /// 11337 = locate/keep and 11329 = recover/return to the issuing terminal.
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

            if (missionIconId == FindItemIcon)
            {
                return MissionRollType.FindItem;
            }

            if (missionIconId == ReturnItemIcon)
            {
                return MissionRollType.FindItemReturn;
            }

            if (missionIconId == RepairMachineIcon)
            {
                return MissionRollType.RepairMachine;
            }

            return MissionRollType.Unknown;
        }

        internal static bool TryTypeFromIcon(int missionIconId, out MissionRollType type)
        {
            type = TypeFromIcon(missionIconId);
            return type != MissionRollType.Unknown;
        }

        internal static int ExpectedActionCode(MissionRollType type)
        {
            switch (type)
            {
                case MissionRollType.KillPerson:
                    return 1;
                case MissionRollType.FindPerson:
                    return 16;
                case MissionRollType.FindItem:
                    return 15;
                case MissionRollType.FindItemReturn:
                case MissionRollType.RepairMachine:
                    return 8;
                default:
                    return 0;
            }
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
                case MissionRollType.FindItemReturn:
                    return "Return item mission";
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
                case MissionRollType.FindItemReturn:
                    return "FindItemReturn";
                case MissionRollType.RepairMachine:
                    return "RepairMachine";
                default:
                    return "Unknown";
            }
        }

        internal static bool IsFindItemFamily(MissionRollType type)
        {
            return type == MissionRollType.FindItem || type == MissionRollType.FindItemReturn;
        }
    }
}
