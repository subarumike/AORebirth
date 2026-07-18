namespace ZoneEngine.Core.Playfields
{
    #region Usings

    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.IO;
    using System.Linq;
    using System.Text;

    using Utility;

    #endregion

    internal sealed class SubwayVisibilityDiagnosticManifestEntry
    {
        internal int Ordinal { get; set; }
        internal int SourceInstance { get; set; }
        internal string Name { get; set; }
        internal string Family { get; set; }
        internal string Classification { get; set; }
        internal float X { get; set; }
        internal float Y { get; set; }
        internal float Z { get; set; }
        internal string SourceCapture { get; set; }
    }

    internal sealed class SubwayVisibilityDiagnosticConfiguration
    {
        internal static readonly SubwayVisibilityDiagnosticConfiguration Disabled =
            new SubwayVisibilityDiagnosticConfiguration(false, string.Empty, "NONE", string.Empty, 0, new int[0]);

        internal SubwayVisibilityDiagnosticConfiguration(
            bool enabled,
            string sessionId,
            string slice,
            string artifactDirectory,
            int expectedQuarantinedRowCount,
            IEnumerable<int> selectedSourceInstances)
        {
            this.Enabled = enabled;
            this.SessionId = sessionId ?? string.Empty;
            this.Slice = slice ?? "NONE";
            this.ArtifactDirectory = artifactDirectory ?? string.Empty;
            this.ExpectedQuarantinedRowCount = expectedQuarantinedRowCount;
            this.SelectedSourceInstances =
                new HashSet<int>(selectedSourceInstances ?? new int[0]);
        }

        internal bool Enabled { get; private set; }
        internal string SessionId { get; private set; }
        internal string Slice { get; private set; }
        internal string ArtifactDirectory { get; private set; }
        internal int ExpectedQuarantinedRowCount { get; private set; }
        internal HashSet<int> SelectedSourceInstances { get; private set; }
    }

    internal static class SubwayVisibilityDiagnosticSelection
    {
        private const string ManifestRelativePath =
            @"docs\generated\subway_pf127_visibility_diagnostic_manifest.csv";

        private const string ActiveConfigurationRelativePath =
            @".local\subway-visibility\active-session.cfg";

        private static readonly object Sync = new object();

        private static readonly object PopulationLedgerSync = new object();

        private static readonly Dictionary<int, SubwayVisibilityDiagnosticManifestEntry> RuntimeEntries =
            new Dictionary<int, SubwayVisibilityDiagnosticManifestEntry>();

        private static readonly HashSet<string> PopulationEventKeys = new HashSet<string>();

        private static bool loaded;
        private static SubwayVisibilityDiagnosticConfiguration configuration =
            SubwayVisibilityDiagnosticConfiguration.Disabled;
        private static Dictionary<int, SubwayVisibilityDiagnosticManifestEntry> manifestBySource =
            new Dictionary<int, SubwayVisibilityDiagnosticManifestEntry>();

        internal static SubwayVisibilityDiagnosticConfiguration Configuration
        {
            get
            {
                EnsureLoaded();
                return configuration;
            }
        }

        internal static bool ShouldIncludeQuarantined(int sourceInstance)
        {
            SubwayVisibilityDiagnosticConfiguration current = Configuration;
            bool selected = current.Enabled && current.SelectedSourceInstances.Contains(sourceInstance);
            if (selected)
            {
                RecordPopulationEventOnce(current, sourceInstance, "ELIGIBLE", null, "selected quarantine row enabled");
            }

            return selected;
        }

        internal static void RegisterRuntimeIdentity(int runtimeInstance, int sourceInstance)
        {
            EnsureLoaded();
            SubwayVisibilityDiagnosticManifestEntry entry;
            if (!manifestBySource.TryGetValue(sourceInstance, out entry))
            {
                return;
            }

            lock (Sync)
            {
                RuntimeEntries[runtimeInstance] = entry;
            }

            RecordPopulationEventOnce(
                Configuration,
                sourceInstance,
                "MATERIALIZED",
                runtimeInstance,
                "runtime identity registered");
        }

        internal static void RecordPopulationFailure(int sourceInstance, string detail)
        {
            SubwayVisibilityDiagnosticConfiguration current = Configuration;
            if (!current.Enabled || !current.SelectedSourceInstances.Contains(sourceInstance))
            {
                return;
            }

            RecordPopulationEventOnce(current, sourceInstance, "FAILED", null, detail);
        }

        internal static void RemoveRuntimeIdentity(int runtimeInstance)
        {
            lock (Sync)
            {
                RuntimeEntries.Remove(runtimeInstance);
            }
        }

        internal static bool TryGetRuntimeEntry(
            int runtimeInstance,
            out SubwayVisibilityDiagnosticManifestEntry entry)
        {
            lock (Sync)
            {
                return RuntimeEntries.TryGetValue(runtimeInstance, out entry);
            }
        }

        internal static SubwayVisibilityDiagnosticManifestEntry[] ManifestEntries()
        {
            EnsureLoaded();
            return manifestBySource.Values.OrderBy(value => value.Ordinal).ToArray();
        }

        private static void EnsureLoaded()
        {
            if (loaded)
            {
                return;
            }

            lock (Sync)
            {
                if (loaded)
                {
                    return;
                }

                try
                {
                    string repositoryRoot = FindRepositoryRoot();
                    if (string.IsNullOrEmpty(repositoryRoot))
                    {
                        loaded = true;
                        return;
                    }

                    manifestBySource = LoadManifest(Path.Combine(repositoryRoot, ManifestRelativePath));
                    configuration = LoadConfiguration(
                        repositoryRoot,
                        Path.Combine(repositoryRoot, ActiveConfigurationRelativePath),
                        manifestBySource);
                }
                catch (Exception exception)
                {
                    configuration = SubwayVisibilityDiagnosticConfiguration.Disabled;
                    manifestBySource = new Dictionary<int, SubwayVisibilityDiagnosticManifestEntry>();
                    LogUtil.Debug(
                        DebugInfoDetail.Error,
                        "PF127 visibility diagnostics disabled: " + exception.Message);
                }
                finally
                {
                    loaded = true;
                }
            }
        }

        private static string FindRepositoryRoot()
        {
            string baseDirectory = AppDomain.CurrentDomain.BaseDirectory;
            if (string.IsNullOrEmpty(baseDirectory))
            {
                return string.Empty;
            }

            DirectoryInfo directory = new DirectoryInfo(baseDirectory);
            while (directory != null)
            {
                string manifest = Path.Combine(directory.FullName, ManifestRelativePath);
                if (File.Exists(manifest))
                {
                    return directory.FullName;
                }

                directory = directory.Parent;
            }

            return string.Empty;
        }

        private static Dictionary<int, SubwayVisibilityDiagnosticManifestEntry> LoadManifest(string path)
        {
            var result = new Dictionary<int, SubwayVisibilityDiagnosticManifestEntry>();
            string[] lines = File.ReadAllLines(path);
            for (int index = 1; index < lines.Length; index++)
            {
                if (string.IsNullOrWhiteSpace(lines[index]))
                {
                    continue;
                }

                string[] fields = lines[index].Split(',');
                if (fields.Length != 9)
                {
                    throw new InvalidDataException("Invalid PF127 diagnostic manifest row " + (index + 1));
                }

                var entry = new SubwayVisibilityDiagnosticManifestEntry
                {
                    Ordinal = int.Parse(fields[0], CultureInfo.InvariantCulture),
                    SourceInstance = int.Parse(fields[1], NumberStyles.HexNumber, CultureInfo.InvariantCulture),
                    Name = fields[2],
                    Family = fields[3],
                    Classification = fields[4],
                    X = float.Parse(fields[5], CultureInfo.InvariantCulture),
                    Y = float.Parse(fields[6], CultureInfo.InvariantCulture),
                    Z = float.Parse(fields[7], CultureInfo.InvariantCulture),
                    SourceCapture = fields[8]
                };
                if (entry.Ordinal != result.Count + 1 || result.ContainsKey(entry.SourceInstance))
                {
                    throw new InvalidDataException("PF127 diagnostic manifest ordering or identity uniqueness failed");
                }

                result.Add(entry.SourceInstance, entry);
            }

            int supported = result.Values.Count(value => value.Classification == "SUPPORTED_FAMILY_RESTORE");
            int ordinary = result.Values.Count(value => value.Classification == "ORDINARY_ENEMY_REGENERATE");
            if (result.Count != 38 || supported != 29 || ordinary != 9)
            {
                throw new InvalidDataException("PF127 diagnostic manifest must contain 38 rows split 29/9");
            }

            return result;
        }

        private static SubwayVisibilityDiagnosticConfiguration LoadConfiguration(
            string repositoryRoot,
            string path,
            IDictionary<int, SubwayVisibilityDiagnosticManifestEntry> manifest)
        {
            if (!File.Exists(path))
            {
                return SubwayVisibilityDiagnosticConfiguration.Disabled;
            }

            var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (string line in File.ReadAllLines(path))
            {
                int separator = line.IndexOf('=');
                if (separator <= 0)
                {
                    continue;
                }

                values[line.Substring(0, separator).Trim()] = line.Substring(separator + 1).Trim();
            }

            string enabled;
            string sessionId;
            string slice;
            string artifactDirectory;
            string expectedText;
            string selectedText;
            if (!values.TryGetValue("enabled", out enabled)
                || enabled != "1"
                || !values.TryGetValue("session_id", out sessionId)
                || !IsSafeSessionId(sessionId)
                || !values.TryGetValue("slice", out slice)
                || !IsKnownSlice(slice)
                || !values.TryGetValue("artifact_directory", out artifactDirectory)
                || !values.TryGetValue("expected_quarantined_row_count", out expectedText)
                || !values.TryGetValue("selected_source_instances", out selectedText))
            {
                throw new InvalidDataException("PF127 active diagnostic configuration is incomplete or invalid");
            }

            string expectedArtifactRoot =
                Path.GetFullPath(Path.Combine(repositoryRoot, @".local\subway-visibility"))
                .TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            string fullArtifactDirectory =
                Path.GetFullPath(artifactDirectory).TrimEnd(Path.DirectorySeparatorChar)
                + Path.DirectorySeparatorChar;
            if (!fullArtifactDirectory.StartsWith(expectedArtifactRoot, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidDataException("PF127 diagnostic artifact directory is outside the ignored session root");
            }

            int expected = int.Parse(expectedText, CultureInfo.InvariantCulture);
            var selected = new HashSet<int>();
            if (!string.IsNullOrWhiteSpace(selectedText))
            {
                foreach (string identity in selectedText.Split(','))
                {
                    int sourceInstance = int.Parse(
                        identity.Trim(),
                        NumberStyles.HexNumber,
                        CultureInfo.InvariantCulture);
                    if (!manifest.ContainsKey(sourceInstance))
                    {
                        throw new InvalidDataException("PF127 diagnostic configuration contains an unknown identity");
                    }

                    selected.Add(sourceInstance);
                }
            }

            if (expected != selected.Count || expected < 0 || expected > 38)
            {
                throw new InvalidDataException("PF127 diagnostic selected count does not match configuration");
            }

            if (slice == "ALL_38" && selected.Count != 38)
            {
                throw new InvalidDataException("ALL_38 requires all 38 explicit manifest identities");
            }

            return new SubwayVisibilityDiagnosticConfiguration(
                true,
                sessionId,
                slice,
                fullArtifactDirectory.TrimEnd(Path.DirectorySeparatorChar),
                expected,
                selected);
        }

        private static bool IsSafeSessionId(string value)
        {
            if (string.IsNullOrEmpty(value) || value.Length > 64)
            {
                return false;
            }

            for (int index = 0; index < value.Length; index++)
            {
                char character = value[index];
                if (!char.IsLetterOrDigit(character)
                    && character != '.'
                    && character != '_'
                    && character != '-')
                {
                    return false;
                }
            }

            return true;
        }

        private static bool IsKnownSlice(string value)
        {
            return value == "NONE"
                   || value == "ALL_38"
                   || value == "SUPPORTED_29"
                   || value == "ORDINARY_9"
                   || value == "FIRST_N"
                   || value == "ORDINAL_RANGE"
                   || value == "IDENTITY_LIST"
                   || value == "FAMILY";
        }

        private static void RecordPopulationEventOnce(
            SubwayVisibilityDiagnosticConfiguration current,
            int sourceInstance,
            string phase,
            int? runtimeInstance,
            string detail)
        {
            if (current == null
                || !current.Enabled
                || !current.SelectedSourceInstances.Contains(sourceInstance))
            {
                return;
            }

            SubwayVisibilityDiagnosticManifestEntry entry;
            if (!manifestBySource.TryGetValue(sourceInstance, out entry))
            {
                return;
            }

            string eventKey = phase + ":" + sourceInstance.ToString("X8", CultureInfo.InvariantCulture);
            lock (PopulationLedgerSync)
            {
                if (PopulationEventKeys.Contains(eventKey))
                {
                    return;
                }

                try
                {
                    Directory.CreateDirectory(current.ArtifactDirectory);
                    string path = Path.Combine(current.ArtifactDirectory, "population-activation-ledger.csv");
                    bool writeHeader = !File.Exists(path);
                    using (var writer = new StreamWriter(path, true, new UTF8Encoding(false)))
                    {
                        if (writeHeader)
                        {
                            writer.WriteLine(
                                "TimestampUtc,SessionId,Slice,ProcessId,Phase,SourceInstanceHex,RuntimeInstance,ManifestOrdinal,Name,Family,Detail");
                        }

                        writer.WriteLine(
                            string.Join(
                                ",",
                                Csv(DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture)),
                                Csv(current.SessionId),
                                Csv(current.Slice),
                                Csv(System.Diagnostics.Process.GetCurrentProcess().Id.ToString(CultureInfo.InvariantCulture)),
                                Csv(phase),
                                Csv(sourceInstance.ToString("X8", CultureInfo.InvariantCulture)),
                                Csv(runtimeInstance.HasValue
                                    ? runtimeInstance.Value.ToString(CultureInfo.InvariantCulture)
                                    : string.Empty),
                                Csv(entry.Ordinal.ToString(CultureInfo.InvariantCulture)),
                                Csv(entry.Name),
                                Csv(entry.Family),
                                Csv(detail)));
                    }

                    PopulationEventKeys.Add(eventKey);
                }
                catch (Exception exception)
                {
                    LogUtil.Debug(
                        DebugInfoDetail.Error,
                        "PF127 population activation diagnostic write failed: " + exception.Message);
                }
            }
        }

        private static string Csv(string value)
        {
            string text = value ?? string.Empty;
            return "\"" + text.Replace("\"", "\"\"") + "\"";
        }
    }
}
