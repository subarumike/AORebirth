namespace ZoneEngine.Core.Missions
{
    #region Usings ...

    using System;
    using System.Globalization;
    using System.IO;

    #endregion

    /// <summary>
    /// Maps a character level plus the mission-terminal Easy/Hard (level) slider position to the mission QL
    /// that should be rolled, and to the token reward for that level. The data is the authoritative
    /// per-level table shipped in <c>XML Data/MissionLevels.csv</c> (levels 1-220, 11 slider columns from
    /// easiest/left to hardest/right, plus the token count).
    ///
    /// The Easy/Hard slider has 11 detents; finalized requests prove that the client sends its position in
    /// <see cref="SmokeLounge.AOtomation.Messaging.Messages.N3Messages.QuestAlternativeMessage.LevelSlider"/>
    /// as a one-based wire value (1..11). QL = table[clampedCharacterLevel][wireValue - 1].
    /// </summary>
    internal static class MissionLevelTable
    {
        internal const int SliderPositions = 11;

        internal const byte MinimumDifficultyWireValue = 1;

        internal const byte MaximumDifficultyWireValue = 11;

        private const int MinLevel = 1;

        private const int MaxLevel = 220;

        private static readonly object InitLock = new object();

        // [level-1][sliderIndex] mission QL; tokens[level-1] reward count. Null until loaded.
        private static int[][] qualityByLevel;

        private static int[] tokensByLevel;

        /// <summary>
        /// Returns the mission QL for a character level and one-based difficulty wire value. Character level
        /// is clamped to the table's 1..220 range. Unsupported wire values and missing rows fail closed.
        /// </summary>
        public static int GetMissionQuality(int characterLevel, int difficultyWireValue)
        {
            int quality;
            if (!TryGetMissionQuality(characterLevel, difficultyWireValue, out quality))
            {
                throw new ArgumentOutOfRangeException(
                    "difficultyWireValue",
                    difficultyWireValue,
                    "Mission difficulty must be a captured one-based detent from 1 through 11.");
            }

            return quality;
        }

        internal static bool TryGetMissionQuality(
            int characterLevel,
            int difficultyWireValue,
            out int missionQuality)
        {
            missionQuality = 0;
            int sliderIndex;
            if (!TryDecodeDifficultySlider(difficultyWireValue, out sliderIndex))
            {
                return false;
            }

            EnsureLoaded();
            if (qualityByLevel == null)
            {
                return false;
            }

            int level = ClampCharacterLevel(characterLevel);
            int[] row = qualityByLevel[level - 1];
            if (row == null || sliderIndex < 0 || sliderIndex >= row.Length)
            {
                return false;
            }

            missionQuality = row[sliderIndex];
            return missionQuality > 0;
        }

        internal static bool TryDecodeDifficultySlider(int difficultyWireValue, out int sliderIndex)
        {
            sliderIndex = -1;
            if (difficultyWireValue < MinimumDifficultyWireValue
                || difficultyWireValue > MaximumDifficultyWireValue)
            {
                return false;
            }

            sliderIndex = difficultyWireValue - MinimumDifficultyWireValue;
            return true;
        }

        internal static int ClampCharacterLevel(int characterLevel)
        {
            return Clamp(characterLevel, MinLevel, MaxLevel);
        }

        /// <summary>
        /// Returns the token reward for completing a mission rolled at the given character level.
        /// </summary>
        public static int GetTokenReward(int characterLevel)
        {
            EnsureLoaded();

            if (tokensByLevel == null)
            {
                return 0;
            }

            int level = ClampCharacterLevel(characterLevel);
            return tokensByLevel[level - 1];
        }

        private static int Clamp(int value, int min, int max)
        {
            if (value < min)
            {
                return min;
            }

            return value > max ? max : value;
        }

        private static void EnsureLoaded()
        {
            if (qualityByLevel != null)
            {
                return;
            }

            lock (InitLock)
            {
                if (qualityByLevel != null)
                {
                    return;
                }

                var quality = new int[MaxLevel][];
                var tokens = new int[MaxLevel];

                string path = FindDataFile("MissionLevels.csv");
                if (path == null || !File.Exists(path))
                {
                    return;
                }

                foreach (string rawLine in File.ReadAllLines(path))
                {
                    string line = rawLine == null ? string.Empty : rawLine.Trim();
                    if (line.Length == 0 || line.StartsWith("Level", StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    string[] parts = line.Split(',');
                    if (parts.Length < 2 + SliderPositions)
                    {
                        continue;
                    }

                    int level;
                    if (!int.TryParse(parts[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out level)
                        || level < MinLevel || level > MaxLevel)
                    {
                        continue;
                    }

                    var row = new int[SliderPositions];
                    bool ok = true;
                    for (int i = 0; i < SliderPositions; i++)
                    {
                        if (!int.TryParse(parts[1 + i], NumberStyles.Integer, CultureInfo.InvariantCulture, out row[i]))
                        {
                            ok = false;
                            break;
                        }
                    }

                    if (!ok)
                    {
                        continue;
                    }

                    int token;
                    int.TryParse(parts[1 + SliderPositions], NumberStyles.Integer, CultureInfo.InvariantCulture, out token);

                    quality[level - 1] = row;
                    tokens[level - 1] = token;
                }

                tokensByLevel = tokens;
                qualityByLevel = quality;
            }
        }

        private static string FindDataFile(string fileName)
        {
            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
            string[] candidates =
                {
                    Path.Combine(baseDir, "XML Data", fileName),
                    Path.Combine(baseDir, fileName),
                    Path.Combine(Directory.GetCurrentDirectory(), "XML Data", fileName),
                    Path.Combine(Directory.GetCurrentDirectory(), fileName)
                };

            foreach (string candidate in candidates)
            {
                if (File.Exists(candidate))
                {
                    return candidate;
                }
            }

            return null;
        }
    }
}
