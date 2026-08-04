namespace ZoneEngine.Core.Arete.Quests
{
    #region Usings ...

    using System.Collections.Generic;

    using AORebirth.Core.Items;

    using ZoneEngine.Core;

    #endregion

    /// <summary>
    /// Capture 20260721-001538 / 20260720-190432 Personalized Basic Robot Brain tradeskill chain:
    /// Screwdriver (150922) + Robot Junk (42620/42619) → Nano Sensor (delete target only)
    /// Bio Analyzing Computer (156020/156021) + Nano Sensor → Basic Robot Brain (delete both)
    /// MasterComm (156024/156025) + Basic Robot Brain → Personalized Basic Robot Brain (delete both)
    /// QL1 grants use low==high; QL3 grants use low/high pairs. Prefer templates present in ItemLoader.
    /// </summary>
    internal static class PersonalizedRobotBrainCombineRules
    {
        internal const int ScrewdriverId = 150922;

        internal const int RobotJunkLowId = 42620;

        internal const int RobotJunkHighId = 42619;

        internal const int NanoSensorLowId = 150923;

        internal const int NanoSensorHighId = 150924;

        internal const int BioAnalyzingComputerLowId = 156020;

        internal const int BioAnalyzingComputerHighId = 156021;

        internal const int BasicRobotBrainLowId = 156022;

        internal const int BasicRobotBrainHighId = 156023;

        internal const int MasterCommLowId = 156024;

        internal const int MasterCommHighId = 156025;

        internal const int PersonalizedBasicRobotBrainLowId = 156026;

        internal const int PersonalizedBasicRobotBrainHighId = 156027;

        internal static TradeSkillEntry TryMatch(int sourceHighId, int targetHighId)
        {
            // Accept either drag order. Wrong order used to fall through to DB Skill='0' rows
            // and fail WindowBuild (ZoneEngineLog: UseItemOnItem with no tip-advance).
            if ((IsScrewdriver(sourceHighId) && IsRobotJunk(targetHighId))
                || (IsRobotJunk(sourceHighId) && IsScrewdriver(targetHighId)))
            {
                return CreateEntry(
                    sourceHighId,
                    targetHighId,
                    NanoSensorLowId,
                    NanoSensorHighId,
                    deleteFlag: 2);
            }

            if ((IsBioAnalyzingComputer(sourceHighId) && IsNanoSensor(targetHighId))
                || (IsNanoSensor(sourceHighId) && IsBioAnalyzingComputer(targetHighId)))
            {
                return CreateEntry(
                    sourceHighId,
                    targetHighId,
                    BasicRobotBrainLowId,
                    BasicRobotBrainHighId,
                    deleteFlag: 3);
            }

            if ((IsMasterComm(sourceHighId) && IsBasicRobotBrain(targetHighId))
                || (IsBasicRobotBrain(sourceHighId) && IsMasterComm(targetHighId)))
            {
                return CreateEntry(
                    sourceHighId,
                    targetHighId,
                    PersonalizedBasicRobotBrainLowId,
                    PersonalizedBasicRobotBrainHighId,
                    deleteFlag: 3);
            }

            return null;
        }

        internal static int SourceProcessBonus(int itemHighId)
        {
            // Either slot may be source/target (client drag order varies).
            if (IsScrewdriver(itemHighId)
                || IsRobotJunk(itemHighId)
                || IsBioAnalyzingComputer(itemHighId)
                || IsNanoSensor(itemHighId)
                || IsMasterComm(itemHighId)
                || IsBasicRobotBrain(itemHighId))
            {
                return 1;
            }

            return 0;
        }

        internal static int TargetProcessBonus(int itemHighId)
        {
            return SourceProcessBonus(itemHighId);
        }

        internal static bool IsPersonalizedBrain(int lowId, int highId)
        {
            return lowId == PersonalizedBasicRobotBrainLowId
                   || lowId == PersonalizedBasicRobotBrainHighId
                   || highId == PersonalizedBasicRobotBrainLowId
                   || highId == PersonalizedBasicRobotBrainHighId;
        }

        /// <summary>
        /// Nano Sensor / Basic Robot Brain / Personalized Basic Robot Brain combine results.
        /// Capture path: Overflow TemplateAction — never AddTemplate (client crash).
        /// </summary>
        internal static bool IsCombineResult(int lowId, int highId)
        {
            return IsNanoSensor(lowId)
                   || IsNanoSensor(highId)
                   || IsBasicRobotBrain(lowId)
                   || IsBasicRobotBrain(highId)
                   || IsPersonalizedBrain(lowId, highId);
        }

        private static bool IsScrewdriver(int id)
        {
            return id == ScrewdriverId;
        }

        private static bool IsRobotJunk(int id)
        {
            return id == RobotJunkLowId || id == RobotJunkHighId;
        }

        private static bool IsNanoSensor(int id)
        {
            return id == NanoSensorLowId || id == NanoSensorHighId;
        }

        private static bool IsBioAnalyzingComputer(int id)
        {
            return id == BioAnalyzingComputerLowId || id == BioAnalyzingComputerHighId;
        }

        private static bool IsBasicRobotBrain(int id)
        {
            return id == BasicRobotBrainLowId || id == BasicRobotBrainHighId;
        }

        private static bool IsMasterComm(int id)
        {
            return id == MasterCommLowId || id == MasterCommHighId;
        }

        private static TradeSkillEntry CreateEntry(
            int id1,
            int id2,
            int resultLowId,
            int resultHighId,
            int deleteFlag)
        {
            int low = ResolveTemplateId(resultLowId, resultHighId);
            int high = ResolveTemplateId(resultHighId, low);
            return new TradeSkillEntry
                   {
                       ID1 = id1,
                       ID2 = id2,
                       DeleteFlag = deleteFlag,
                       IsImplant = false,
                       MaxBump = 0,
                       MaxXP = 0,
                       MinTargetQL = 0,
                       MinXP = 0,
                       QLRangePercent = 0,
                       ResultLowId = low,
                       ResultHighId = high,
                       // Empty skills — DB Skill='0' rows fail WindowBuild via Stats[0].
                       Skills = new List<TradeSkillSkill>()
                   };
        }

        private static int ResolveTemplateId(int preferred, int fallback)
        {
            if (ItemLoader.ItemList != null && ItemLoader.ItemList.ContainsKey(preferred))
            {
                return preferred;
            }

            if (ItemLoader.ItemList != null && ItemLoader.ItemList.ContainsKey(fallback))
            {
                return fallback;
            }

            return preferred;
        }
    }
}
