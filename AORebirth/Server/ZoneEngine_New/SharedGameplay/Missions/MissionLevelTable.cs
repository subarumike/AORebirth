namespace ZoneEngine.Core.Missions
{
    #region Usings ...

    using System;

    #endregion

    /// <summary>
    /// Maps character level plus the mission-terminal Easy/Hard detent to the
    /// exact official mission quality. Runtime data is compiled from the
    /// canonical checked-in source by <c>tools/generate_mission_level_graph.cmd</c>.
    /// The complete 220-level by 11-position graph is validated before one
    /// immutable snapshot is published.
    ///
    /// The client sends the difficulty detent as the captured one-based wire
    /// value 1..11. QL = graph[clampedCharacterLevel][wireValue - 1].
    /// </summary>
    internal static class MissionLevelTable
    {
        internal const int SliderPositions =
            MissionLevelGraph.DifficultyCount;

        internal const byte MinimumDifficultyWireValue = 1;

        internal const byte MaximumDifficultyWireValue = 11;

        private static readonly object InitLock = new object();

        private static readonly MissionLevelGraphPublication Publication =
            new MissionLevelGraphPublication();

        private static volatile bool initialized;

        public static int GetMissionQuality(
            int characterLevel,
            int difficultyWireValue)
        {
            return GetRequiredMissionQualityForRoll(
                characterLevel,
                difficultyWireValue);
        }

        internal static bool TryGetMissionQuality(
            int characterLevel,
            int difficultyWireValue,
            out int missionQuality)
        {
            string failure;
            return TryGetMissionQuality(
                characterLevel,
                difficultyWireValue,
                out missionQuality,
                out failure);
        }

        internal static bool TryGetMissionQuality(
            int characterLevel,
            int difficultyWireValue,
            out int missionQuality,
            out string failure)
        {
            missionQuality = 0;
            failure = string.Empty;

            int difficultyIndex;
            if (!TryDecodeDifficultySlider(
                    difficultyWireValue,
                    out difficultyIndex))
            {
                return false;
            }

            EnsureLoaded();
            MissionLevelGraph graph;
            if (!Publication.TryGet(out graph, out failure))
            {
                return false;
            }

            int level = ClampCharacterLevel(characterLevel);
            if (!graph.TryGetMissionQuality(
                    level,
                    difficultyIndex,
                    out missionQuality))
            {
                failure =
                    "The validated official mission-level graph lacks the requested cell.";
                missionQuality = 0;
                return false;
            }

            return true;
        }

        internal static int GetRequiredMissionQualityForRoll(
            int characterLevel,
            int difficultyWireValue)
        {
            EnsureLoaded();
            return GetRequiredMissionQualityForRoll(
                Publication,
                characterLevel,
                difficultyWireValue);
        }

        internal static int GetRequiredMissionQualityForRoll(
            MissionLevelGraphPublication publication,
            int characterLevel,
            int difficultyWireValue)
        {
            int difficultyIndex;
            if (!TryDecodeDifficultySlider(
                    difficultyWireValue,
                    out difficultyIndex))
            {
                throw new ArgumentOutOfRangeException(
                    "difficultyWireValue",
                    difficultyWireValue,
                    "Mission difficulty must be a captured one-based detent from 1 through 11.");
            }

            if (publication == null)
            {
                throw new ArgumentNullException("publication");
            }

            MissionLevelGraph graph;
            string failure;
            if (!publication.TryGet(out graph, out failure))
            {
                throw new InvalidOperationException(
                    "The official mission-level graph is unavailable or invalid: "
                    + failure);
            }

            int missionQuality;
            int level = ClampCharacterLevel(characterLevel);
            if (!graph.TryGetMissionQuality(
                    level,
                    difficultyIndex,
                    out missionQuality))
            {
                throw new InvalidOperationException(
                    "The validated official mission-level graph lacks the requested cell.");
            }

            return missionQuality;
        }

        internal static bool TryDecodeDifficultySlider(
            int difficultyWireValue,
            out int difficultyIndex)
        {
            difficultyIndex = -1;
            if (difficultyWireValue < MinimumDifficultyWireValue
                || difficultyWireValue > MaximumDifficultyWireValue)
            {
                return false;
            }

            difficultyIndex =
                difficultyWireValue - MinimumDifficultyWireValue;
            return true;
        }

        internal static int ClampCharacterLevel(int characterLevel)
        {
            if (characterLevel < MissionLevelGraph.MinimumLevel)
            {
                return MissionLevelGraph.MinimumLevel;
            }

            return characterLevel > MissionLevelGraph.MaximumLevel
                       ? MissionLevelGraph.MaximumLevel
                       : characterLevel;
        }

        internal static bool IsGraphAvailable
        {
            get
            {
                EnsureLoaded();
                MissionLevelGraph graph;
                string failure;
                return Publication.TryGet(out graph, out failure);
            }
        }

        internal static string LastLoadError
        {
            get
            {
                EnsureLoaded();
                MissionLevelGraph graph;
                string failure;
                return Publication.TryGet(out graph, out failure)
                           ? string.Empty
                           : failure;
            }
        }

        /// <summary>
        /// Returns the unchanged official token reward column for a mission
        /// rolled at the supplied character level.
        /// </summary>
        public static int GetTokenReward(int characterLevel)
        {
            int tokenCount;
            string failure;
            return TryGetTokenReward(
                       characterLevel,
                       out tokenCount,
                       out failure)
                       ? tokenCount
                       : 0;
        }

        /// <summary>
        /// Resolves the unchanged official token column without converting a
        /// missing or invalid graph into a guessed reward.
        /// </summary>
        internal static bool TryGetTokenReward(
            int characterLevel,
            out int tokenCount,
            out string failure)
        {
            tokenCount = 0;
            failure = string.Empty;
            EnsureLoaded();
            MissionLevelGraph graph;
            if (!Publication.TryGet(out graph, out failure))
            {
                return false;
            }

            if (!graph.TryGetTokenCount(
                    ClampCharacterLevel(characterLevel),
                    out tokenCount))
            {
                failure =
                    "The validated official mission-level graph lacks the requested token cell.";
                tokenCount = 0;
                return false;
            }

            return true;
        }

        private static void EnsureLoaded()
        {
            if (initialized)
            {
                return;
            }

            lock (InitLock)
            {
                if (initialized)
                {
                    return;
                }

                string sourceProvenance =
                    MissionLevelGraphData.SourceRepositoryPath
                    + " SHA-256 "
                    + MissionLevelGraphData.SourceSha256
                    + "; upstream "
                    + MissionLevelGraphData.UpstreamOdsFileName
                    + " SHA-256 "
                    + MissionLevelGraphData.UpstreamOdsSha256
                    + " ("
                    + MissionLevelGraphData.UpstreamOdsVerification
                    + ")";
                string failure;
                bool loaded = Publication.TryPublish(
                    MissionLevelGraphData.CanonicalCsv,
                    MissionLevelGraphData.CanonicalPayloadSha256,
                    MissionLevelGraphData.SourceSha256,
                    sourceProvenance,
                    out failure);

                initialized = true;
                if (loaded)
                {
                    MissionDiagnostics.Log(
                        "MISSION-LEVEL-GRAPH loaded format={0} levels={1} positions={2} sourceSha256={3} payloadSha256={4}",
                        MissionLevelGraphData.FormatVersion,
                        MissionLevelGraph.MaximumLevel,
                        MissionLevelGraph.DifficultyCount,
                        MissionLevelGraphData.SourceSha256,
                        MissionLevelGraphData.CanonicalPayloadSha256);
                }
                else
                {
                    MissionDiagnostics.Log(
                        "MISSION-LEVEL-GRAPH INVALID source={0} sourceSha256={1} payloadSha256={2} reason={3}",
                        MissionLevelGraphData.SourceRepositoryPath,
                        MissionLevelGraphData.SourceSha256,
                        MissionLevelGraphData.CanonicalPayloadSha256,
                        failure);
                }
            }
        }
    }
}
