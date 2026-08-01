namespace ZoneEngine.Core.Missions
{
    #region Usings ...

    using System;
    using System.Globalization;
    using System.Security.Cryptography;
    using System.Text;
    using System.Threading;

    #endregion

    /// <summary>
    /// One fully validated immutable official mission-level graph.
    /// </summary>
    internal sealed class MissionLevelGraph
    {
        internal const int MinimumLevel = 1;

        internal const int MaximumLevel = 220;

        internal const int DifficultyCount = 11;

        internal const int MinimumMissionQuality = 1;

        internal const int MaximumMissionQuality = 250;

        internal const int MinimumTokenCount = 1;

        internal const int MaximumTokenCount = 9;

        private readonly int[] qualities;

        private readonly int[] tokens;

        internal MissionLevelGraph(
            int[] qualities,
            int[] tokens,
            string payloadSha256,
            string sourceSha256,
            string sourceProvenance)
        {
            if (qualities == null
                || qualities.Length
                   != MaximumLevel * DifficultyCount)
            {
                throw new ArgumentException(
                    "A complete 220 by 11 quality graph is required.",
                    "qualities");
            }

            if (tokens == null || tokens.Length != MaximumLevel)
            {
                throw new ArgumentException(
                    "A complete 220-level token table is required.",
                    "tokens");
            }

            this.qualities = (int[])qualities.Clone();
            this.tokens = (int[])tokens.Clone();
            this.PayloadSha256 = payloadSha256;
            this.SourceSha256 = sourceSha256;
            this.SourceProvenance = sourceProvenance;
        }

        internal string PayloadSha256 { get; private set; }

        internal string SourceSha256 { get; private set; }

        internal string SourceProvenance { get; private set; }

        internal bool TryGetMissionQuality(
            int level,
            int difficultyIndex,
            out int missionQuality)
        {
            missionQuality = 0;
            if (level < MinimumLevel
                || level > MaximumLevel
                || difficultyIndex < 0
                || difficultyIndex >= DifficultyCount)
            {
                return false;
            }

            missionQuality =
                this.qualities[
                    ((level - MinimumLevel) * DifficultyCount)
                    + difficultyIndex];
            return true;
        }

        internal bool TryGetTokenCount(int level, out int tokenCount)
        {
            tokenCount = 0;
            if (level < MinimumLevel || level > MaximumLevel)
            {
                return false;
            }

            tokenCount = this.tokens[level - MinimumLevel];
            return true;
        }

        internal string SerializeCanonicalCsv()
        {
            var builder = new StringBuilder(20000);
            builder.Append("Level");
            for (int difficultyIndex = 0;
                 difficultyIndex < DifficultyCount;
                 difficultyIndex++)
            {
                builder.Append(",Q");
                builder.Append(
                    difficultyIndex.ToString(
                        CultureInfo.InvariantCulture));
            }

            builder.Append(",Tokens\n");
            for (int level = MinimumLevel; level <= MaximumLevel; level++)
            {
                builder.Append(level.ToString(CultureInfo.InvariantCulture));
                for (int difficultyIndex = 0;
                     difficultyIndex < DifficultyCount;
                     difficultyIndex++)
                {
                    int missionQuality;
                    this.TryGetMissionQuality(
                        level,
                        difficultyIndex,
                        out missionQuality);
                    builder.Append(',');
                    builder.Append(
                        missionQuality.ToString(
                            CultureInfo.InvariantCulture));
                }

                int tokenCount;
                this.TryGetTokenCount(level, out tokenCount);
                builder.Append(',');
                builder.Append(
                    tokenCount.ToString(CultureInfo.InvariantCulture));
                builder.Append('\n');
            }

            return builder.ToString();
        }
    }

    /// <summary>
    /// All-or-nothing parser and semantic validator for the generated graph
    /// payload.
    /// </summary>
    internal static class MissionLevelGraphLoader
    {
        private const int ExpectedColumnCount =
            2 + MissionLevelGraph.DifficultyCount;

        internal static bool TryLoad(
            string canonicalCsv,
            string expectedPayloadSha256,
            string sourceSha256,
            string sourceProvenance,
            out MissionLevelGraph graph,
            out string failure)
        {
            graph = null;
            failure = string.Empty;

            if (string.IsNullOrEmpty(canonicalCsv))
            {
                failure = "Official mission-level graph payload is empty.";
                return false;
            }

            if (!IsLowercaseSha256(expectedPayloadSha256))
            {
                failure =
                    "Official mission-level graph payload hash metadata is malformed.";
                return false;
            }

            if (!IsLowercaseSha256(sourceSha256))
            {
                failure =
                    "Official mission-level graph source hash metadata is malformed.";
                return false;
            }

            if (string.IsNullOrWhiteSpace(sourceProvenance))
            {
                failure =
                    "Official mission-level graph source provenance is missing.";
                return false;
            }

            string actualPayloadSha256 = ComputeSha256(canonicalCsv);
            if (!string.Equals(
                    actualPayloadSha256,
                    expectedPayloadSha256,
                    StringComparison.Ordinal))
            {
                failure =
                    "Official mission-level graph payload SHA-256 does not match.";
                return false;
            }

            if (canonicalCsv.IndexOf('\r') >= 0)
            {
                failure =
                    "Official mission-level graph must use canonical LF line endings.";
                return false;
            }

            if (!canonicalCsv.EndsWith("\n", StringComparison.Ordinal))
            {
                failure =
                    "Official mission-level graph is truncated or lacks its terminal newline.";
                return false;
            }

            string[] lines = canonicalCsv.Split('\n');
            if (lines.Length == 0
                || lines[lines.Length - 1].Length != 0)
            {
                failure =
                    "Official mission-level graph has a malformed terminal row.";
                return false;
            }

            int dataRowCount = lines.Length - 2;
            if (dataRowCount < MissionLevelGraph.MaximumLevel)
            {
                failure =
                    "Official mission-level graph is missing one or more level rows.";
                return false;
            }

            if (dataRowCount > MissionLevelGraph.MaximumLevel)
            {
                failure =
                    "Official mission-level graph contains an unexpected extra row.";
                return false;
            }

            int[] difficultyColumns;
            if (!TryParseHeader(
                    lines[0],
                    out difficultyColumns,
                    out failure))
            {
                return false;
            }

            var qualities =
                new int[
                    MissionLevelGraph.MaximumLevel
                    * MissionLevelGraph.DifficultyCount];
            var tokens = new int[MissionLevelGraph.MaximumLevel];
            var seenLevels = new bool[MissionLevelGraph.MaximumLevel];

            for (int lineIndex = 1;
                 lineIndex <= MissionLevelGraph.MaximumLevel;
                 lineIndex++)
            {
                string line = lines[lineIndex];
                if (line.Length == 0)
                {
                    failure =
                        "Official mission-level graph contains an empty level row.";
                    return false;
                }

                string[] cells = line.Split(',');
                if (cells.Length < ExpectedColumnCount)
                {
                    failure =
                        "Official mission-level graph contains a row with missing columns.";
                    return false;
                }

                if (cells.Length > ExpectedColumnCount)
                {
                    failure =
                        "Official mission-level graph contains a row with an unexpected extra column.";
                    return false;
                }

                int level;
                if (!TryParseCanonicalUnsigned(cells[0], out level))
                {
                    failure =
                        "Official mission-level graph contains a malformed level token.";
                    return false;
                }

                if (level < MissionLevelGraph.MinimumLevel
                    || level > MissionLevelGraph.MaximumLevel)
                {
                    failure =
                        "Official mission-level graph contains an out-of-range level.";
                    return false;
                }

                var rowQualities =
                    new int[MissionLevelGraph.DifficultyCount];
                for (int columnOffset = 0;
                     columnOffset < difficultyColumns.Length;
                     columnOffset++)
                {
                    int missionQuality;
                    if (!TryParseCanonicalUnsigned(
                            cells[1 + columnOffset],
                            out missionQuality))
                    {
                        failure =
                            "Official mission-level graph contains a malformed mission-quality token.";
                        return false;
                    }

                    if (missionQuality
                        < MissionLevelGraph.MinimumMissionQuality
                        || missionQuality
                        > MissionLevelGraph.MaximumMissionQuality)
                    {
                        failure =
                            "Official mission-level graph contains an out-of-range mission quality.";
                        return false;
                    }

                    rowQualities[difficultyColumns[columnOffset]] =
                        missionQuality;
                }

                int tokenCount;
                if (!TryParseCanonicalUnsigned(
                        cells[ExpectedColumnCount - 1],
                        out tokenCount))
                {
                    failure =
                        "Official mission-level graph contains a malformed token-count value.";
                    return false;
                }

                if (tokenCount < MissionLevelGraph.MinimumTokenCount
                    || tokenCount > MissionLevelGraph.MaximumTokenCount)
                {
                    failure =
                        "Official mission-level graph contains an out-of-range token count.";
                    return false;
                }

                int levelOffset = level - MissionLevelGraph.MinimumLevel;
                if (seenLevels[levelOffset])
                {
                    bool conflicts = tokens[levelOffset] != tokenCount;
                    for (int difficultyIndex = 0;
                         difficultyIndex
                         < MissionLevelGraph.DifficultyCount;
                         difficultyIndex++)
                    {
                        if (qualities[
                                (levelOffset
                                 * MissionLevelGraph.DifficultyCount)
                                + difficultyIndex]
                            != rowQualities[difficultyIndex])
                        {
                            conflicts = true;
                        }
                    }

                    failure = conflicts
                                  ? "Official mission-level graph contains a conflicting duplicate level row."
                                  : "Official mission-level graph contains a duplicate level row.";
                    return false;
                }

                if (!IsNondecreasing(rowQualities))
                {
                    failure =
                        "Official mission-level graph decreases across difficulty positions.";
                    return false;
                }

                if (rowQualities[5] != level)
                {
                    failure =
                        "Official mission-level graph has an impossible neutral-difficulty value.";
                    return false;
                }

                seenLevels[levelOffset] = true;
                tokens[levelOffset] = tokenCount;
                for (int difficultyIndex = 0;
                     difficultyIndex
                     < MissionLevelGraph.DifficultyCount;
                     difficultyIndex++)
                {
                    qualities[
                        (levelOffset * MissionLevelGraph.DifficultyCount)
                        + difficultyIndex] =
                        rowQualities[difficultyIndex];
                }
            }

            for (int levelOffset = 0;
                 levelOffset < MissionLevelGraph.MaximumLevel;
                 levelOffset++)
            {
                if (!seenLevels[levelOffset])
                {
                    failure =
                        "Official mission-level graph is missing level "
                        + (levelOffset + MissionLevelGraph.MinimumLevel)
                              .ToString(CultureInfo.InvariantCulture)
                        + ".";
                    return false;
                }
            }

            for (int difficultyIndex = 0;
                 difficultyIndex < MissionLevelGraph.DifficultyCount;
                 difficultyIndex++)
            {
                for (int levelOffset = 1;
                     levelOffset < MissionLevelGraph.MaximumLevel;
                     levelOffset++)
                {
                    int previous =
                        qualities[
                            ((levelOffset - 1)
                             * MissionLevelGraph.DifficultyCount)
                            + difficultyIndex];
                    int current =
                        qualities[
                            (levelOffset
                             * MissionLevelGraph.DifficultyCount)
                            + difficultyIndex];
                    if (current < previous)
                    {
                        failure =
                            "Official mission-level graph decreases between levels.";
                        return false;
                    }
                }
            }

            if (!IsNondecreasing(tokens))
            {
                failure =
                    "Official mission-level graph token counts decrease between levels.";
                return false;
            }

            var candidate =
                new MissionLevelGraph(
                    qualities,
                    tokens,
                    actualPayloadSha256,
                    sourceSha256,
                    sourceProvenance);
            string serialized = candidate.SerializeCanonicalCsv();
            if (!string.Equals(
                    serialized,
                    canonicalCsv,
                    StringComparison.Ordinal))
            {
                failure =
                    "Official mission-level graph is not in deterministic canonical order.";
                return false;
            }

            graph = candidate;
            return true;
        }

        internal static string ComputeSha256(string value)
        {
            if (value == null)
            {
                throw new ArgumentNullException("value");
            }

            return ComputeSha256(
                new UTF8Encoding(false, true).GetBytes(value));
        }

        internal static string ComputeSha256(byte[] value)
        {
            if (value == null)
            {
                throw new ArgumentNullException("value");
            }

            using (SHA256 sha256 = SHA256.Create())
            {
                byte[] hash = sha256.ComputeHash(value);
                var builder = new StringBuilder(hash.Length * 2);
                for (int index = 0; index < hash.Length; index++)
                {
                    builder.Append(
                        hash[index].ToString(
                            "x2",
                            CultureInfo.InvariantCulture));
                }

                return builder.ToString();
            }
        }

        private static bool TryParseHeader(
            string header,
            out int[] difficultyColumns,
            out string failure)
        {
            difficultyColumns = null;
            failure = string.Empty;
            if (string.IsNullOrEmpty(header))
            {
                failure =
                    "Official mission-level graph header is missing.";
                return false;
            }

            string[] columns = header.Split(',');
            if (columns.Length < ExpectedColumnCount)
            {
                failure =
                    "Official mission-level graph header is missing a difficulty column.";
                return false;
            }

            if (columns.Length > ExpectedColumnCount)
            {
                failure =
                    "Official mission-level graph header contains an unexpected extra column.";
                return false;
            }

            if (!string.Equals(
                    columns[0],
                    "Level",
                    StringComparison.Ordinal)
                || !string.Equals(
                    columns[ExpectedColumnCount - 1],
                    "Tokens",
                    StringComparison.Ordinal))
            {
                failure =
                    "Official mission-level graph header is malformed.";
                return false;
            }

            var mapped =
                new int[MissionLevelGraph.DifficultyCount];
            var seen =
                new bool[MissionLevelGraph.DifficultyCount];
            for (int columnOffset = 0;
                 columnOffset < MissionLevelGraph.DifficultyCount;
                 columnOffset++)
            {
                string token = columns[1 + columnOffset];
                if (token.Length < 2
                    || token[0] != 'Q')
                {
                    failure =
                        "Official mission-level graph difficulty header is malformed.";
                    return false;
                }

                int difficultyIndex;
                if (!TryParseCanonicalUnsigned(
                        token.Substring(1),
                        out difficultyIndex))
                {
                    failure =
                        "Official mission-level graph difficulty index is malformed.";
                    return false;
                }

                if (difficultyIndex < 0
                    || difficultyIndex
                       >= MissionLevelGraph.DifficultyCount)
                {
                    failure =
                        "Official mission-level graph difficulty index is out of range.";
                    return false;
                }

                if (seen[difficultyIndex])
                {
                    failure =
                        "Official mission-level graph contains a duplicate difficulty cell.";
                    return false;
                }

                if (difficultyIndex != columnOffset)
                {
                    failure =
                        "Official mission-level graph difficulty columns are out of canonical order.";
                    return false;
                }

                seen[difficultyIndex] = true;
                mapped[columnOffset] = difficultyIndex;
            }

            for (int index = 0; index < seen.Length; index++)
            {
                if (!seen[index])
                {
                    failure =
                        "Official mission-level graph is missing a difficulty position.";
                    return false;
                }
            }

            difficultyColumns = mapped;
            return true;
        }

        private static bool TryParseCanonicalUnsigned(
            string token,
            out int value)
        {
            value = 0;
            if (string.IsNullOrEmpty(token))
            {
                return false;
            }

            if (token.Length > 1 && token[0] == '0')
            {
                return false;
            }

            for (int index = 0; index < token.Length; index++)
            {
                if (token[index] < '0' || token[index] > '9')
                {
                    return false;
                }
            }

            return int.TryParse(
                token,
                NumberStyles.None,
                CultureInfo.InvariantCulture,
                out value);
        }

        private static bool IsNondecreasing(int[] values)
        {
            for (int index = 1; index < values.Length; index++)
            {
                if (values[index] < values[index - 1])
                {
                    return false;
                }
            }

            return true;
        }

        private static bool IsLowercaseSha256(string value)
        {
            if (value == null || value.Length != 64)
            {
                return false;
            }

            for (int index = 0; index < value.Length; index++)
            {
                char character = value[index];
                if ((character < '0' || character > '9')
                    && (character < 'a' || character > 'f'))
                {
                    return false;
                }
            }

            return true;
        }
    }

    /// <summary>
    /// Atomically publishes only fully validated immutable graph snapshots.
    /// Failed reloads never replace a previously published valid snapshot.
    /// </summary>
    internal sealed class MissionLevelGraphPublication
    {
        private MissionLevelGraph current;

        private string lastFailure =
            "No official mission-level graph has been published.";

        internal bool TryPublish(
            string canonicalCsv,
            string expectedPayloadSha256,
            string sourceSha256,
            string sourceProvenance,
            out string failure)
        {
            MissionLevelGraph candidate;
            if (!MissionLevelGraphLoader.TryLoad(
                    canonicalCsv,
                    expectedPayloadSha256,
                    sourceSha256,
                    sourceProvenance,
                    out candidate,
                    out failure))
            {
                Volatile.Write(ref this.lastFailure, failure);
                return false;
            }

            Interlocked.Exchange(ref this.current, candidate);
            Volatile.Write(ref this.lastFailure, string.Empty);
            return true;
        }

        internal bool TryGet(
            out MissionLevelGraph graph,
            out string failure)
        {
            graph = Volatile.Read(ref this.current);
            if (graph != null)
            {
                failure = string.Empty;
                return true;
            }

            failure = Volatile.Read(ref this.lastFailure);
            if (string.IsNullOrEmpty(failure))
            {
                failure =
                    "No official mission-level graph has been published.";
            }

            return false;
        }
    }
}
