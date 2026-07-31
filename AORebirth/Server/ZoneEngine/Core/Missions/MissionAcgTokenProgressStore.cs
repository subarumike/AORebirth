namespace ZoneEngine.Core.Missions
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.IO;
    using System.Security.Cryptography;
    using System.Text;

    internal sealed class MissionAcgTokenProgressLoadResult
    {
        internal MissionAcgTokenProgressLoadResult(
            IEnumerable<MissionAcgTokenProgressRecord> records,
            IEnumerable<string> diagnostics)
        {
            this.Records =
                new List<MissionAcgTokenProgressRecord>(records).AsReadOnly();
            this.Diagnostics = new List<string>(diagnostics).AsReadOnly();
        }

        internal IList<MissionAcgTokenProgressRecord> Records { get; private set; }

        internal IList<string> Diagnostics { get; private set; }

        internal bool IsValid
        {
            get
            {
                return this.Diagnostics.Count == 0;
            }
        }
    }

    /// <summary>
    /// Versioned, integrity-checked, atomic, append-only audit persistence for
    /// generated-mission token progress.
    /// </summary>
    internal sealed class MissionAcgTokenProgressStore
    {
        internal const string DirectoryName = "acg-token-progress";

        internal const string FileExtension = ".token-progress";

        private const string Header = "AORebirth-MissionAcgTokenProgress";

        private const int BaseFieldCount = 55;

        private const int EventFieldCount = 17;

        private const int MaximumEventCount = 100000;

        private readonly string directoryPath;

        internal MissionAcgTokenProgressStore(string missionStateDirectory)
        {
            if (string.IsNullOrWhiteSpace(missionStateDirectory))
            {
                throw new ArgumentException(
                    "Mission state directory is required.",
                    "missionStateDirectory");
            }

            this.directoryPath =
                Path.Combine(missionStateDirectory, DirectoryName);
        }

        internal string DirectoryPath
        {
            get
            {
                return this.directoryPath;
            }
        }

        internal string PathFor(MissionAcgIdentityRecord acceptedQuestIdentity)
        {
            RequireConcreteIdentity(
                acceptedQuestIdentity,
                "acceptedQuestIdentity");
            return Path.Combine(
                this.directoryPath,
                acceptedQuestIdentity.Type.ToString(
                    "X8",
                    CultureInfo.InvariantCulture)
                + "-"
                + acceptedQuestIdentity.Instance.ToString(
                    "X8",
                    CultureInfo.InvariantCulture)
                + FileExtension);
        }

        internal MissionAcgTokenProgressLoadResult LoadAll()
        {
            var records = new List<MissionAcgTokenProgressRecord>();
            var diagnostics = new List<string>();
            if (!Directory.Exists(this.directoryPath))
            {
                return new MissionAcgTokenProgressLoadResult(
                    records,
                    diagnostics);
            }

            string[] paths =
                Directory.GetFiles(
                    this.directoryPath,
                    "*" + FileExtension,
                    SearchOption.TopDirectoryOnly);
            Array.Sort(paths, StringComparer.OrdinalIgnoreCase);
            var accepted = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < paths.Length; i++)
            {
                MissionAcgTokenProgressRecord record;
                string failure;
                if (!this.TryRead(paths[i], out record, out failure))
                {
                    diagnostics.Add(paths[i] + ": " + failure);
                    continue;
                }

                string acceptedKey =
                    IdentityKey(record.State.AcceptedQuestIdentity);
                if (!accepted.Add(acceptedKey))
                {
                    diagnostics.Add(
                        paths[i]
                        + ": duplicate token-progress accepted quest id "
                        + acceptedKey
                        + ".");
                    continue;
                }

                records.Add(record);
            }

            return new MissionAcgTokenProgressLoadResult(
                records,
                diagnostics);
        }

        internal MissionAcgTokenProgressLoadResult LoadAll(
            IEnumerable<MissionAcgBindingRecord> bindingRecords,
            IEnumerable<MissionAcgObjectiveRecord> objectiveRecords)
        {
            MissionAcgTokenProgressLoadResult loaded = this.LoadAll();
            var records =
                new List<MissionAcgTokenProgressRecord>(loaded.Records);
            var diagnostics = new List<string>(loaded.Diagnostics);
            var bindings =
                new Dictionary<string, MissionAcgInstanceBinding>(
                    StringComparer.Ordinal);
            var objectives =
                new Dictionary<string, MissionAcgObjectiveBinding>(
                    StringComparer.Ordinal);
            if (bindingRecords == null || objectiveRecords == null)
            {
                diagnostics.Add(
                    "Binding and objective records are required to validate "
                    + "token-progress state.");
                return new MissionAcgTokenProgressLoadResult(
                    records,
                    diagnostics);
            }

            foreach (MissionAcgBindingRecord bindingRecord in bindingRecords)
            {
                if (bindingRecord == null || bindingRecord.Binding == null)
                {
                    diagnostics.Add(
                        "Null binding record cannot validate token-progress state.");
                    continue;
                }

                string key =
                    IdentityKey(bindingRecord.Binding.AcceptedQuestIdentity);
                if (bindings.ContainsKey(key))
                {
                    diagnostics.Add(
                        "Duplicate accepted quest id "
                        + key
                        + " in binding set.");
                    continue;
                }

                bindings.Add(key, bindingRecord.Binding);
            }

            foreach (MissionAcgObjectiveRecord objectiveRecord in objectiveRecords)
            {
                if (objectiveRecord == null || objectiveRecord.Binding == null)
                {
                    diagnostics.Add(
                        "Null objective record cannot validate token-progress state.");
                    continue;
                }

                string key =
                    IdentityKey(
                        objectiveRecord.Binding.AcceptedQuestIdentity);
                if (objectives.ContainsKey(key))
                {
                    diagnostics.Add(
                        "Duplicate accepted quest id "
                        + key
                        + " in objective set.");
                    continue;
                }

                objectives.Add(key, objectiveRecord.Binding);
            }

            for (int i = 0; i < records.Count; i++)
            {
                MissionAcgTokenProgressRecord record = records[i];
                string key = IdentityKey(record.State.AcceptedQuestIdentity);
                MissionAcgInstanceBinding binding;
                MissionAcgObjectiveBinding objective;
                if (!bindings.TryGetValue(key, out binding)
                    || !objectives.TryGetValue(key, out objective))
                {
                    diagnostics.Add(
                        record.RecordPath
                        + ": orphan token-progress state for accepted quest "
                        + key
                        + ".");
                    continue;
                }

                string failure;
                if (!record.State.Matches(binding, objective, out failure))
                {
                    diagnostics.Add(record.RecordPath + ": " + failure);
                }
            }

            return new MissionAcgTokenProgressLoadResult(
                records,
                diagnostics);
        }

        internal bool TryLoad(
            MissionAcgIdentityRecord acceptedQuestIdentity,
            out MissionAcgTokenProgressRecord record,
            out string failure)
        {
            record = null;
            failure = string.Empty;
            string path;
            try
            {
                path = this.PathFor(acceptedQuestIdentity);
            }
            catch (Exception ex)
            {
                failure = ex.GetType().Name + ": " + ex.Message;
                return false;
            }

            if (!File.Exists(path))
            {
                failure = "Token-progress sidecar does not exist: " + path;
                return false;
            }

            return this.TryRead(path, out record, out failure);
        }

        internal bool TryCreate(
            MissionAcgTokenProgressState state,
            out MissionAcgTokenProgressRecord persisted,
            out string failure)
        {
            return this.TryCreate(
                state == null
                    ? null
                    : new MissionAcgTokenProgressRecord(state, string.Empty),
                out persisted,
                out failure);
        }

        internal bool TryCreate(
            MissionAcgTokenProgressRecord record,
            out MissionAcgTokenProgressRecord persisted,
            out string failure)
        {
            persisted = null;
            failure = string.Empty;
            if (record == null)
            {
                failure = "Token-progress state is required.";
                return false;
            }

            Directory.CreateDirectory(this.directoryPath);
            string path = this.PathFor(record.State.AcceptedQuestIdentity);
            if (File.Exists(path))
            {
                failure =
                    "Duplicate token-progress accepted quest id "
                    + IdentityKey(record.State.AcceptedQuestIdentity)
                    + ".";
                return false;
            }

            var withPath =
                new MissionAcgTokenProgressRecord(record.State, path);
            if (!this.TryWriteAtomic(withPath, false, out failure))
            {
                return false;
            }

            persisted = withPath;
            return true;
        }

        internal bool TryReplace(
            MissionAcgTokenProgressRecord record,
            out MissionAcgTokenProgressRecord persisted,
            out string failure)
        {
            persisted = null;
            failure = string.Empty;
            if (record == null)
            {
                failure = "Token-progress state is required.";
                return false;
            }

            string expected = this.PathFor(record.State.AcceptedQuestIdentity);
            string path =
                string.IsNullOrWhiteSpace(record.RecordPath)
                    ? expected
                    : record.RecordPath;
            if (!string.Equals(
                    Path.GetFullPath(path),
                    Path.GetFullPath(expected),
                    StringComparison.OrdinalIgnoreCase))
            {
                failure =
                    "Token-progress record path does not match accepted quest.";
                return false;
            }

            if (!File.Exists(path))
            {
                failure = "Token-progress sidecar does not exist: " + path;
                return false;
            }

            MissionAcgTokenProgressRecord existing;
            string readFailure;
            if (!this.TryRead(path, out existing, out readFailure))
            {
                failure =
                    "Existing token-progress sidecar is invalid: "
                    + readFailure;
                return false;
            }

            if (!CanReplace(existing.State, record.State, out failure))
            {
                return false;
            }

            var current =
                new MissionAcgTokenProgressRecord(record.State, path);
            if (!this.TryWriteAtomic(current, true, out failure))
            {
                return false;
            }

            persisted = current;
            return true;
        }

        private bool TryRead(
            string path,
            out MissionAcgTokenProgressRecord record,
            out string failure)
        {
            record = null;
            failure = string.Empty;
            try
            {
                string[] lines =
                    File.ReadAllLines(path, new UTF8Encoding(false, true));
                if (lines.Length < BaseFieldCount + 2
                    || !string.Equals(
                        lines[0],
                        Header,
                        StringComparison.Ordinal))
                {
                    failure =
                        "Missing token-progress header or truncated sidecar.";
                    return false;
                }

                var values =
                    new Dictionary<string, string>(StringComparer.Ordinal);
                for (int i = 1; i < lines.Length; i++)
                {
                    int separator = lines[i].IndexOf('=');
                    if (separator <= 0)
                    {
                        failure = "Malformed field at line " + (i + 1) + ".";
                        return false;
                    }

                    string key = lines[i].Substring(0, separator);
                    string value = lines[i].Substring(separator + 1);
                    if (values.ContainsKey(key))
                    {
                        failure = "Duplicate field " + key + ".";
                        return false;
                    }

                    values.Add(key, value);
                }

                string suppliedHash = Require(values, "RecordSha256");
                values.Remove("RecordSha256");
                int eventCount =
                    ParseInt(Require(values, "EventCount"), "EventCount");
                if (eventCount < 0 || eventCount > MaximumEventCount)
                {
                    failure = "Token-progress event count is invalid.";
                    return false;
                }

                int expectedFieldCount =
                    checked(BaseFieldCount + (eventCount * EventFieldCount));
                if (values.Count != expectedFieldCount)
                {
                    failure =
                        "Token-progress field set is incomplete or contains "
                        + "unknown fields.";
                    return false;
                }

                string canonical = SerializeValues(values);
                string computedHash = ComputeSha256(canonical);
                if (!string.Equals(
                        suppliedHash,
                        computedHash,
                        StringComparison.OrdinalIgnoreCase))
                {
                    failure = "Record SHA-256 mismatch.";
                    return false;
                }

                int version =
                    ParseInt(Require(values, "FormatVersion"), "FormatVersion");
                if (version != MissionAcgTokenProgressState.CurrentFormatVersion)
                {
                    failure =
                        "Unknown token-progress format version "
                        + version
                        + ".";
                    return false;
                }

                MissionAcgInstanceBinding binding = ParseBinding(values);
                MissionAcgObjectiveBinding objective =
                    ParseObjective(values, binding);
                var events =
                    new List<MissionAcgTokenProgressDeathEvent>(eventCount);
                for (int i = 0; i < eventCount; i++)
                {
                    string prefix =
                        "Event"
                        + (i + 1).ToString(
                            "D8",
                            CultureInfo.InvariantCulture)
                        + ".";
                    events.Add(
                        new MissionAcgTokenProgressDeathEvent(
                            DecodeUtf8Base64(
                                Require(values, prefix + "EventIdBase64"),
                                prefix + "EventIdBase64"),
                            ParseIdentity(
                                values,
                                prefix + "SourceRuntime"),
                            ParseIdentity(values, prefix + "Actor"),
                            ParseInt(
                                Require(values, prefix + "CapturedSlot"),
                                prefix + "CapturedSlot"),
                            ParseInt(
                                Require(values, prefix + "SpawnGeneration"),
                                prefix + "SpawnGeneration"),
                            ParseInt(
                                Require(values, prefix + "Sequence"),
                                prefix + "Sequence"),
                            ParseInt(
                                Require(values, prefix + "AppliedCountBefore"),
                                prefix + "AppliedCountBefore"),
                            ParseInt(
                                Require(values, prefix + "AppliedCountAfter"),
                                prefix + "AppliedCountAfter"),
                            ParseInt(
                                Require(values, prefix + "PercentBefore"),
                                prefix + "PercentBefore"),
                            ParseInt(
                                Require(values, prefix + "PercentAfter"),
                                prefix + "PercentAfter"),
                            ParseUtc(
                                Require(values, prefix + "ObservedUtc"),
                                prefix + "ObservedUtc"),
                            ParseUtc(
                                Require(values, prefix + "UpdatedUtc"),
                                prefix + "UpdatedUtc"),
                            (MissionAcgTokenProgressEventPhase)ParseInt(
                                Require(values, prefix + "Phase"),
                                prefix + "Phase"),
                            ParseBool(
                                Require(values, prefix + "WasDurablyApplied"),
                                prefix + "WasDurablyApplied"),
                            DecodeUtf8Base64(
                                Require(values, prefix + "LastFailureBase64"),
                                prefix + "LastFailureBase64")));
                }

                var state =
                    new MissionAcgTokenProgressState(
                        version,
                        binding,
                        objective,
                        ParseInt(
                            Require(
                                values,
                                "TotalCountableAmbientSlots"),
                            "TotalCountableAmbientSlots"),
                        ParseInt(
                            Require(values, "InitialPercent"),
                            "InitialPercent"),
                        ParseInt(
                            Require(values, "AppliedCount"),
                            "AppliedCount"),
                        ParseInt(Require(values, "Percent"), "Percent"),
                        (MissionAcgLifecycleState)ParseInt(
                            Require(values, "Lifecycle"),
                            "Lifecycle"),
                        (MissionAcgLifecycleState)ParseInt(
                            Require(values, "TerminalReason"),
                            "TerminalReason"),
                        DecodeUtf8Base64(
                            Require(
                                values,
                                "LifecycleDiagnosticBase64"),
                            "LifecycleDiagnosticBase64"),
                        ParseUtc(
                            Require(values, "CreatedUtc"),
                            "CreatedUtc"),
                        ParseUtc(
                            Require(values, "UpdatedUtc"),
                            "UpdatedUtc"),
                        events);
                record = new MissionAcgTokenProgressRecord(state, path);
                return true;
            }
            catch (Exception ex)
            {
                failure = ex.GetType().Name + ": " + ex.Message;
                return false;
            }
        }

        private bool TryWriteAtomic(
            MissionAcgTokenProgressRecord record,
            bool replace,
            out string failure)
        {
            failure = string.Empty;
            string target = record.RecordPath;
            string temporary =
                target
                + "."
                + Guid.NewGuid().ToString("N")
                + ".tmp";
            try
            {
                SortedDictionary<string, string> values =
                    BuildValues(record.State);
                string canonical = SerializeValues(values);
                string complete =
                    Header
                    + "\r\n"
                    + canonical
                    + "RecordSha256="
                    + ComputeSha256(canonical)
                    + "\r\n";
                byte[] bytes = new UTF8Encoding(false).GetBytes(complete);
                using (var stream =
                    new FileStream(
                        temporary,
                        FileMode.CreateNew,
                        FileAccess.Write,
                        FileShare.None))
                {
                    stream.Write(bytes, 0, bytes.Length);
                    stream.Flush(true);
                }

                MissionAcgTokenProgressRecord roundTrip;
                string readFailure;
                if (!this.TryRead(temporary, out roundTrip, out readFailure)
                    || !string.Equals(
                        SerializeValues(BuildValues(record.State)),
                        SerializeValues(BuildValues(roundTrip.State)),
                        StringComparison.Ordinal))
                {
                    failure =
                        "Atomic token-progress write validation failed: "
                        + (readFailure ?? "round-trip mismatch");
                    return false;
                }

                if (replace)
                {
                    string backup = target + ".bak";
                    File.Replace(temporary, target, backup, true);
                    if (File.Exists(backup))
                    {
                        File.Delete(backup);
                    }
                }
                else
                {
                    File.Move(temporary, target);
                }

                return true;
            }
            catch (Exception ex)
            {
                failure = ex.GetType().Name + ": " + ex.Message;
                return false;
            }
            finally
            {
                if (File.Exists(temporary))
                {
                    File.Delete(temporary);
                }
            }
        }

        private static bool CanReplace(
            MissionAcgTokenProgressState existing,
            MissionAcgTokenProgressState replacement,
            out string failure)
        {
            failure = string.Empty;
            string matchFailure;
            if (!replacement.Matches(
                    existing.Binding,
                    existing.ObjectiveBinding,
                    out matchFailure)
                || replacement.TotalCountableAmbientSlots
                   != existing.TotalCountableAmbientSlots
                || replacement.InitialPercent != existing.InitialPercent
                || replacement.CreatedUtc != existing.CreatedUtc)
            {
                failure =
                    "Token-progress ownership or frozen totals cannot be replaced.";
                return false;
            }

            if (replacement.UpdatedUtc < existing.UpdatedUtc
                || replacement.AppliedCount < existing.AppliedCount
                || replacement.Percent < existing.Percent
                || replacement.DeathEvents.Count < existing.DeathEvents.Count
                || replacement.DeathEvents.Count
                   > existing.DeathEvents.Count + 1)
            {
                failure = "Token-progress audit cannot regress or skip events.";
                return false;
            }

            if (!MissionAcgTokenProgressState.CanTransition(
                    existing.Lifecycle,
                    replacement.Lifecycle))
            {
                failure = "Token-progress lifecycle cannot regress.";
                return false;
            }

            if (existing.TerminalReason != 0
                && existing.TerminalReason != replacement.TerminalReason)
            {
                failure = "Token-progress terminal reason cannot be replaced.";
                return false;
            }

            if (existing.Lifecycle == MissionAcgLifecycleState.Cleaned
                && !string.Equals(
                    SerializeValues(BuildValues(existing)),
                    SerializeValues(BuildValues(replacement)),
                    StringComparison.Ordinal))
            {
                failure = "Cleaned token-progress audit is immutable.";
                return false;
            }

            for (int i = 0; i < existing.DeathEvents.Count; i++)
            {
                MissionAcgTokenProgressDeathEvent oldEvent =
                    existing.DeathEvents[i];
                MissionAcgTokenProgressDeathEvent nextEvent =
                    replacement.DeathEvents[i];
                if (!SameEventOwnership(oldEvent, nextEvent))
                {
                    failure =
                        "Persisted token-progress event ownership cannot be replaced.";
                    return false;
                }

                if (oldEvent.Phase == nextEvent.Phase)
                {
                    if (oldEvent.UpdatedUtc != nextEvent.UpdatedUtc
                        || oldEvent.WasDurablyApplied
                           != nextEvent.WasDurablyApplied
                        || !string.Equals(
                            oldEvent.LastFailure,
                            nextEvent.LastFailure,
                            StringComparison.Ordinal))
                    {
                        failure =
                            "Token-progress event changed without a phase advance.";
                        return false;
                    }
                }
                else if (!MissionAcgTokenProgressDeathEvent.CanAdvance(
                             oldEvent.Phase,
                             nextEvent.Phase)
                         || nextEvent.UpdatedUtc < oldEvent.UpdatedUtc)
                {
                    failure = "Token-progress event phase cannot regress.";
                    return false;
                }
            }

            if (replacement.DeathEvents.Count
                == existing.DeathEvents.Count + 1)
            {
                MissionAcgTokenProgressDeathEvent appended =
                    replacement.DeathEvents[replacement.DeathEvents.Count - 1];
                if (appended.Phase
                        != MissionAcgTokenProgressEventPhase.Validated
                    || appended.Sequence != replacement.DeathEvents.Count)
                {
                    failure =
                        "New token-progress audit entry must begin validated.";
                    return false;
                }
            }

            return true;
        }

        private static bool SameEventOwnership(
            MissionAcgTokenProgressDeathEvent left,
            MissionAcgTokenProgressDeathEvent right)
        {
            return string.Equals(
                       left.EventId,
                       right.EventId,
                       StringComparison.Ordinal)
                   && left.SourceRuntimeIdentity.Equals(
                       right.SourceRuntimeIdentity)
                   && left.ActorIdentity.Equals(right.ActorIdentity)
                   && left.CapturedSlot == right.CapturedSlot
                   && left.SpawnGeneration == right.SpawnGeneration
                   && left.Sequence == right.Sequence
                   && left.AppliedCountBefore == right.AppliedCountBefore
                   && left.AppliedCountAfter == right.AppliedCountAfter
                   && left.PercentBefore == right.PercentBefore
                   && left.PercentAfter == right.PercentAfter
                   && left.ObservedUtc == right.ObservedUtc;
        }

        private static MissionAcgInstanceBinding ParseBinding(
            IDictionary<string, string> values)
        {
            bool explicitNoTeam =
                ParseBool(
                    Require(values, "ExplicitNoTeam"),
                    "ExplicitNoTeam");
            MissionAcgIdentityRecord persistedTeam =
                ParseIdentityAllowZero(values, "Team");
            if (explicitNoTeam
                && (persistedTeam.Type != 0
                    || persistedTeam.Instance != 0))
            {
                throw new FormatException(
                    "Explicit no-team token-progress state contains a team.");
            }

            return new MissionAcgInstanceBinding(
                ParseInt(
                    Require(values, "BindingFormatVersion"),
                    "BindingFormatVersion"),
                ParseIdentity(values, "AcceptedQuest"),
                ParseIdentity(values, "OriginalOffer"),
                ParseIdentity(values, "Owner"),
                explicitNoTeam ? null : persistedTeam,
                (MissionRollType)ParseInt(
                    Require(values, "MissionType"),
                    "MissionType"),
                ParseInt(
                    Require(values, "MissionQuality"),
                    "MissionQuality"),
                ParseInt(
                    Require(values, "MissionSeed"),
                    "MissionSeed"),
                ParseIdentity(values, "MissionKey"),
                ParseIdentity(values, "ExteriorEntrance"),
                ParseInt(
                    Require(values, "ExteriorEntranceLow"),
                    "ExteriorEntranceLow"),
                ParseInt(
                    Require(values, "ExteriorEntranceHigh"),
                    "ExteriorEntranceHigh"),
                ParseFloat(Require(values, "ExteriorX"), "ExteriorX"),
                ParseFloat(Require(values, "ExteriorY"), "ExteriorY"),
                ParseFloat(Require(values, "ExteriorZ"), "ExteriorZ"),
                ParseIdentity(values, "IssuingTerminal"),
                DecodeUtf8Base64(
                    Require(values, "SelectedBundleIdBase64"),
                    "SelectedBundleIdBase64"),
                Require(values, "SelectedBundlePayloadSha256"),
                ParseIdentity(values, "AcgBuilding"),
                ParseInt(
                    Require(values, "AllocatedLivePlayfield2"),
                    "AllocatedLivePlayfield2"),
                ParseUtc(Require(values, "AcceptedUtc"), "AcceptedUtc"),
                ParseUtc(Require(values, "ExpiryUtc"), "ExpiryUtc"),
                explicitNoTeam);
        }

        private static MissionAcgObjectiveBinding ParseObjective(
            IDictionary<string, string> values,
            MissionAcgInstanceBinding binding)
        {
            return new MissionAcgObjectiveBinding(
                ParseInt(
                    Require(values, "ObjectiveFormatVersion"),
                    "ObjectiveFormatVersion"),
                binding.AcceptedQuestIdentity,
                binding.OwnerIdentity,
                binding.TeamIdentity,
                binding.ExplicitNoTeam,
                binding.MissionType,
                binding.AllocatedLivePlayfield2,
                binding.SelectedBundleId,
                binding.SelectedBundlePayloadSha256,
                binding.AcgBuildingIdentity,
                ParseInt(
                    Require(values, "CapturedObjectiveSlot"),
                    "CapturedObjectiveSlot"),
                ParseIdentity(values, "CapturedObjective"),
                ParseIdentity(values, "RuntimeObjective"),
                ParseInt(
                    Require(values, "ObjectiveTemplateId"),
                    "ObjectiveTemplateId"),
                DecodeUtf8Base64(
                    Require(values, "ObjectiveNameBase64"),
                    "ObjectiveNameBase64"),
                (MissionAcgObjectiveInteraction)ParseInt(
                    Require(values, "RequiredInteraction"),
                    "RequiredInteraction"),
                ParseIdentityAllowZeroOrNull(
                    values,
                    "ObjectiveIssuingTerminal"),
                ParseInt(
                    Require(values, "RequiredMissionItemTemplateId"),
                    "RequiredMissionItemTemplateId"),
                ParseInt(
                    Require(values, "RequiredMachineTemplateId"),
                    "RequiredMachineTemplateId"));
        }

        private static SortedDictionary<string, string> BuildValues(
            MissionAcgTokenProgressState state)
        {
            MissionAcgInstanceBinding binding = state.Binding;
            MissionAcgObjectiveBinding objective = state.ObjectiveBinding;
            var values =
                new SortedDictionary<string, string>(StringComparer.Ordinal)
                {
                    {
                        "AcceptedUtc",
                        binding.AcceptedUtc.ToString(
                            "O",
                            CultureInfo.InvariantCulture)
                    },
                    {
                        "AllocatedLivePlayfield2",
                        Format(binding.AllocatedLivePlayfield2)
                    },
                    { "AppliedCount", Format(state.AppliedCount) },
                    {
                        "BindingFormatVersion",
                        Format(binding.BindingFormatVersion)
                    },
                    {
                        "CapturedObjectiveSlot",
                        Format(objective.CapturedObjectiveSlot)
                    },
                    {
                        "CreatedUtc",
                        state.CreatedUtc.ToString(
                            "O",
                            CultureInfo.InvariantCulture)
                    },
                    { "EventCount", Format(state.DeathEvents.Count) },
                    {
                        "ExplicitNoTeam",
                        binding.ExplicitNoTeam.ToString(
                            CultureInfo.InvariantCulture)
                    },
                    {
                        "ExpiryUtc",
                        binding.ExpiryUtc.ToString(
                            "O",
                            CultureInfo.InvariantCulture)
                    },
                    {
                        "ExteriorEntranceHigh",
                        Format(binding.ExteriorEntranceHigh)
                    },
                    {
                        "ExteriorEntranceLow",
                        Format(binding.ExteriorEntranceLow)
                    },
                    {
                        "ExteriorX",
                        binding.ExteriorX.ToString(
                            "R",
                            CultureInfo.InvariantCulture)
                    },
                    {
                        "ExteriorY",
                        binding.ExteriorY.ToString(
                            "R",
                            CultureInfo.InvariantCulture)
                    },
                    {
                        "ExteriorZ",
                        binding.ExteriorZ.ToString(
                            "R",
                            CultureInfo.InvariantCulture)
                    },
                    { "FormatVersion", Format(state.FormatVersion) },
                    { "InitialPercent", Format(state.InitialPercent) },
                    { "Lifecycle", Format((int)state.Lifecycle) },
                    {
                        "LifecycleDiagnosticBase64",
                        EncodeUtf8Base64(state.LifecycleDiagnostic)
                    },
                    {
                        "MissionQuality",
                        Format(binding.MissionQuality)
                    },
                    { "MissionSeed", Format(binding.DeterministicSeed) },
                    { "MissionType", Format((int)binding.MissionType) },
                    {
                        "ObjectiveFormatVersion",
                        Format(objective.FormatVersion)
                    },
                    {
                        "ObjectiveNameBase64",
                        EncodeUtf8Base64(objective.ObjectiveName)
                    },
                    {
                        "ObjectiveTemplateId",
                        Format(objective.ObjectiveTemplateId)
                    },
                    { "Percent", Format(state.Percent) },
                    {
                        "RequiredInteraction",
                        Format((int)objective.RequiredInteraction)
                    },
                    {
                        "RequiredMachineTemplateId",
                        Format(objective.RequiredMachineTemplateId)
                    },
                    {
                        "RequiredMissionItemTemplateId",
                        Format(objective.RequiredMissionItemTemplateId)
                    },
                    {
                        "SelectedBundleIdBase64",
                        EncodeUtf8Base64(binding.SelectedBundleId)
                    },
                    {
                        "SelectedBundlePayloadSha256",
                        binding.SelectedBundlePayloadSha256
                    },
                    {
                        "TerminalReason",
                        Format((int)state.TerminalReason)
                    },
                    {
                        "TotalCountableAmbientSlots",
                        Format(state.TotalCountableAmbientSlots)
                    },
                    {
                        "UpdatedUtc",
                        state.UpdatedUtc.ToString(
                            "O",
                            CultureInfo.InvariantCulture)
                    }
                };
            AddIdentity(values, "AcceptedQuest", binding.AcceptedQuestIdentity);
            AddIdentity(values, "AcgBuilding", binding.AcgBuildingIdentity);
            AddIdentity(
                values,
                "CapturedObjective",
                objective.CapturedObjectiveIdentity);
            AddIdentity(
                values,
                "ExteriorEntrance",
                binding.ExteriorEntranceIdentity);
            AddIdentity(
                values,
                "IssuingTerminal",
                binding.IssuingTerminalIdentity);
            AddIdentity(values, "MissionKey", binding.MissionKeyIdentity);
            AddIdentity(
                values,
                "ObjectiveIssuingTerminal",
                objective.IssuingTerminalIdentity);
            AddIdentity(values, "OriginalOffer", binding.OriginalOfferIdentity);
            AddIdentity(values, "Owner", binding.OwnerIdentity);
            AddIdentity(
                values,
                "RuntimeObjective",
                objective.RuntimeObjectiveIdentity);
            AddIdentity(values, "Team", binding.TeamIdentity);

            for (int i = 0; i < state.DeathEvents.Count; i++)
            {
                MissionAcgTokenProgressDeathEvent progressEvent =
                    state.DeathEvents[i];
                string prefix =
                    "Event"
                    + (i + 1).ToString(
                        "D8",
                        CultureInfo.InvariantCulture)
                    + ".";
                values.Add(
                    prefix + "AppliedCountAfter",
                    Format(progressEvent.AppliedCountAfter));
                values.Add(
                    prefix + "AppliedCountBefore",
                    Format(progressEvent.AppliedCountBefore));
                AddIdentity(
                    values,
                    prefix + "Actor",
                    progressEvent.ActorIdentity);
                values.Add(
                    prefix + "CapturedSlot",
                    Format(progressEvent.CapturedSlot));
                values.Add(
                    prefix + "EventIdBase64",
                    EncodeUtf8Base64(progressEvent.EventId));
                values.Add(
                    prefix + "LastFailureBase64",
                    EncodeUtf8Base64(progressEvent.LastFailure));
                values.Add(
                    prefix + "ObservedUtc",
                    progressEvent.ObservedUtc.ToString(
                        "O",
                        CultureInfo.InvariantCulture));
                values.Add(
                    prefix + "PercentAfter",
                    Format(progressEvent.PercentAfter));
                values.Add(
                    prefix + "PercentBefore",
                    Format(progressEvent.PercentBefore));
                values.Add(
                    prefix + "Phase",
                    Format((int)progressEvent.Phase));
                values.Add(
                    prefix + "Sequence",
                    Format(progressEvent.Sequence));
                AddIdentity(
                    values,
                    prefix + "SourceRuntime",
                    progressEvent.SourceRuntimeIdentity);
                values.Add(
                    prefix + "SpawnGeneration",
                    Format(progressEvent.SpawnGeneration));
                values.Add(
                    prefix + "UpdatedUtc",
                    progressEvent.UpdatedUtc.ToString(
                        "O",
                        CultureInfo.InvariantCulture));
                values.Add(
                    prefix + "WasDurablyApplied",
                    progressEvent.WasDurablyApplied.ToString(
                        CultureInfo.InvariantCulture));
            }

            return values;
        }

        private static void AddIdentity(
            IDictionary<string, string> values,
            string prefix,
            MissionAcgIdentityRecord identity)
        {
            values.Add(
                prefix + "Type",
                Format(identity == null ? 0 : identity.Type));
            values.Add(
                prefix + "Instance",
                Format(identity == null ? 0 : identity.Instance));
        }

        private static string SerializeValues(
            IDictionary<string, string> values)
        {
            var keys = new List<string>(values.Keys);
            keys.Sort(StringComparer.Ordinal);
            var builder = new StringBuilder();
            for (int i = 0; i < keys.Count; i++)
            {
                builder.Append(keys[i]);
                builder.Append('=');
                builder.Append(values[keys[i]]);
                builder.Append("\r\n");
            }

            return builder.ToString();
        }

        private static string ComputeSha256(string value)
        {
            using (SHA256 sha = SHA256.Create())
            {
                byte[] hash =
                    sha.ComputeHash(new UTF8Encoding(false).GetBytes(value));
                var builder = new StringBuilder(hash.Length * 2);
                for (int i = 0; i < hash.Length; i++)
                {
                    builder.Append(
                        hash[i].ToString(
                            "x2",
                            CultureInfo.InvariantCulture));
                }

                return builder.ToString();
            }
        }

        private static MissionAcgIdentityRecord ParseIdentity(
            IDictionary<string, string> values,
            string prefix)
        {
            MissionAcgIdentityRecord identity =
                ParseIdentityAllowZero(values, prefix);
            RequireConcreteIdentity(identity, prefix);
            return identity;
        }

        private static MissionAcgIdentityRecord ParseIdentityAllowZero(
            IDictionary<string, string> values,
            string prefix)
        {
            return new MissionAcgIdentityRecord(
                ParseInt(
                    Require(values, prefix + "Type"),
                    prefix + "Type"),
                ParseInt(
                    Require(values, prefix + "Instance"),
                    prefix + "Instance"));
        }

        private static MissionAcgIdentityRecord ParseIdentityAllowZeroOrNull(
            IDictionary<string, string> values,
            string prefix)
        {
            MissionAcgIdentityRecord identity =
                ParseIdentityAllowZero(values, prefix);
            if (identity.Type == 0 && identity.Instance == 0)
            {
                return null;
            }

            RequireConcreteIdentity(identity, prefix);
            return identity;
        }

        private static void RequireConcreteIdentity(
            MissionAcgIdentityRecord identity,
            string parameter)
        {
            if (identity == null || identity.Type == 0 || identity.Instance == 0)
            {
                throw new ArgumentException(
                    "Concrete identity is required.",
                    parameter);
            }
        }

        private static string Require(
            IDictionary<string, string> values,
            string key)
        {
            string value;
            if (!values.TryGetValue(key, out value))
            {
                throw new FormatException("Missing field " + key + ".");
            }

            return value;
        }

        private static int ParseInt(string value, string field)
        {
            int parsed;
            if (!int.TryParse(
                    value,
                    NumberStyles.Integer,
                    CultureInfo.InvariantCulture,
                    out parsed))
            {
                throw new FormatException(
                    "Invalid integer field " + field + ".");
            }

            return parsed;
        }

        private static float ParseFloat(string value, string field)
        {
            float parsed;
            if (!float.TryParse(
                    value,
                    NumberStyles.Float,
                    CultureInfo.InvariantCulture,
                    out parsed)
                || float.IsNaN(parsed)
                || float.IsInfinity(parsed))
            {
                throw new FormatException(
                    "Invalid float field " + field + ".");
            }

            return parsed;
        }

        private static bool ParseBool(string value, string field)
        {
            bool parsed;
            if (!bool.TryParse(value, out parsed))
            {
                throw new FormatException(
                    "Invalid boolean field " + field + ".");
            }

            return parsed;
        }

        private static DateTime ParseUtc(string value, string field)
        {
            DateTime parsed;
            if (!DateTime.TryParseExact(
                    value,
                    "O",
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.RoundtripKind,
                    out parsed)
                || parsed.Kind != DateTimeKind.Utc)
            {
                throw new FormatException(
                    "Invalid UTC timestamp field " + field + ".");
            }

            return parsed;
        }

        private static string EncodeUtf8Base64(string value)
        {
            return Convert.ToBase64String(
                new UTF8Encoding(false).GetBytes(value ?? string.Empty));
        }

        private static string DecodeUtf8Base64(string value, string field)
        {
            try
            {
                return new UTF8Encoding(false, true).GetString(
                    Convert.FromBase64String(value));
            }
            catch (Exception ex)
            {
                throw new FormatException(
                    "Invalid UTF-8 base64 field " + field + ".",
                    ex);
            }
        }

        private static string Format(int value)
        {
            return value.ToString(CultureInfo.InvariantCulture);
        }

        private static string IdentityKey(MissionAcgIdentityRecord identity)
        {
            return identity.Type.ToString(
                       "X8",
                       CultureInfo.InvariantCulture)
                   + ":"
                   + identity.Instance.ToString(
                       "X8",
                       CultureInfo.InvariantCulture);
        }
    }
}
