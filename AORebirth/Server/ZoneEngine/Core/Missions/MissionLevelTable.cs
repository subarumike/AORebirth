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
    /// The Easy/Hard slider has 11 detents; the client sends its position in
    /// <see cref="SmokeLounge.AOtomation.Messaging.Messages.N3Messages.QuestAlternativeMessage.LevelSlider"/>
    /// as a 0-based index. QL = table[characterLevel][sliderIndex].
    /// </summary>
    internal static class MissionLevelTable
    {
        internal const int SliderPositions = 11;

        private const int MinLevel = 1;

        private const int MaxLevel = 220;

        private static readonly object InitLock = new object();

        // [level-1][sliderIndex] mission QL; tokens[level-1] reward count. Null until loaded.
        private static int[][] qualityByLevel;

        private static int[] tokensByLevel;

        /// <summary>
        /// Returns the mission QL for a character level and slider index. Level is clamped to 1-220 and the
        /// slider index to 0-10. Returns 1 if the table could not be loaded.
        /// </summary>
        public static int GetMissionQuality(int characterLevel, int sliderIndex)
        {
            EnsureLoaded();

            if (qualityByLevel == null)
            {
                return 1;
            }

            int level = Clamp(characterLevel, MinLevel, MaxLevel);
            int slider = Clamp(sliderIndex, 0, SliderPositions - 1);

            int[] row = qualityByLevel[level - 1];
            if (row == null)
            {
                return 1;
            }

            return row[slider];
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

            int level = Clamp(characterLevel, MinLevel, MaxLevel);
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
