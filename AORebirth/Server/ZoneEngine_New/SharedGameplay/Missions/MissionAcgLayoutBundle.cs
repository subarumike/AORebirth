namespace ZoneEngine.Core.Missions
{
    #region Usings ...

    using System;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;

    #endregion

    /// <summary>
    /// Immutable, internally coherent ACG layout evidence. The generator body, building identity,
    /// entry point, captured dynel wire, compatible mission types, and provenance travel together
    /// so later runtime code cannot select one shape and replay another shape's packets.
    /// </summary>
    internal sealed class MissionAcgLayoutBundle
    {
        internal const int CurrentFormatVersion = 1;

        private readonly byte[] generatorPayload;

        internal MissionAcgLayoutBundle(
            int bundleFormatVersion,
            string layoutId,
            int sourcePlayfield2,
            MissionAcgIdentityRecord buildingIdentity,
            byte[] generatorPayload,
            MissionAcgPointRecord entryPoint,
            MissionAcgExitRecord exit,
            IEnumerable<MissionAcgDynelRecord> dynels,
            IEnumerable<MissionAcgNpcSlotRecord> npcSlots,
            IEnumerable<MissionAcgObjectiveSlotRecord> objectiveSlots,
            MissionAcgCompatibilityRecord compatibility,
            IEnumerable<MissionAcgProvenanceRecord> provenance,
            MissionAcgCompletenessRecord completeness,
            bool isSelectable,
            string selectionExclusionReason)
            : this(
                bundleFormatVersion,
                layoutId,
                sourcePlayfield2,
                buildingIdentity,
                generatorPayload,
                string.Empty,
                entryPoint,
                exit,
                dynels,
                npcSlots,
                objectiveSlots,
                null,
                null,
                compatibility,
                provenance,
                completeness,
                isSelectable,
                selectionExclusionReason)
        {
        }

        internal MissionAcgLayoutBundle(
            int bundleFormatVersion,
            string layoutId,
            int sourcePlayfield2,
            MissionAcgIdentityRecord buildingIdentity,
            byte[] generatorPayload,
            string expectedGeneratorPayloadSha256,
            MissionAcgPointRecord entryPoint,
            MissionAcgExitRecord exit,
            IEnumerable<MissionAcgDynelRecord> dynels,
            IEnumerable<MissionAcgNpcSlotRecord> npcSlots,
            IEnumerable<MissionAcgObjectiveSlotRecord> objectiveSlots,
            MissionAcgCaptureCountsRecord captureCounts,
            MissionAcgIdentityRecord capturedPlayerIdentity,
            MissionAcgCompatibilityRecord compatibility,
            IEnumerable<MissionAcgProvenanceRecord> provenance,
            MissionAcgCompletenessRecord completeness,
            bool isSelectable,
            string selectionExclusionReason)
        {
            if (bundleFormatVersion <= 0)
            {
                throw new ArgumentOutOfRangeException("bundleFormatVersion");
            }

            if (string.IsNullOrWhiteSpace(layoutId))
            {
                throw new ArgumentException("Layout id is required.", "layoutId");
            }

            if (sourcePlayfield2 <= 0)
            {
                throw new ArgumentOutOfRangeException("sourcePlayfield2");
            }

            if (completeness == null)
            {
                throw new ArgumentNullException("completeness");
            }

            if (compatibility == null)
            {
                throw new ArgumentNullException("compatibility");
            }

            if (isSelectable && !string.IsNullOrWhiteSpace(selectionExclusionReason))
            {
                throw new ArgumentException(
                    "A selectable layout cannot carry an exclusion reason.",
                    "selectionExclusionReason");
            }

            if (!isSelectable && string.IsNullOrWhiteSpace(selectionExclusionReason))
            {
                throw new ArgumentException(
                    "An unselectable layout requires an exclusion reason.",
                    "selectionExclusionReason");
            }

            this.BundleFormatVersion = bundleFormatVersion;
            this.LayoutId = layoutId.Trim();
            this.SourcePlayfield2 = sourcePlayfield2;
            this.BuildingIdentity = buildingIdentity;
            this.generatorPayload =
                generatorPayload == null ? new byte[0] : (byte[])generatorPayload.Clone();
            this.GeneratorPayloadSha256 =
                this.generatorPayload.Length == 0
                    ? string.Empty
                    : MissionAcgHash.ComputeSha256(this.generatorPayload);
            this.ExpectedGeneratorPayloadSha256 =
                (expectedGeneratorPayloadSha256 ?? string.Empty).Trim();
            this.EntryPoint = entryPoint;
            this.Exit = exit;

            List<MissionAcgDynelRecord> allDynels = CopyDynels(dynels);
            List<MissionAcgWireRecord> allWire = SelectWireRecords(allDynels);
            this.Dynels = allDynels.AsReadOnly();
            this.WireRecords = allWire.AsReadOnly();
            this.Doors = SelectWireRecords(allWire, MissionAcgWireCategory.Door);
            this.Chests = SelectWireRecords(allWire, MissionAcgWireCategory.Chest);
            this.Terminals = SelectWireRecords(allWire, MissionAcgWireCategory.Terminal);
            this.NpcSlots = CopyNpcSlots(npcSlots);
            this.ObjectiveSlots = CopyObjectiveSlots(objectiveSlots);
            this.CaptureCounts = captureCounts;
            this.CapturedPlayerIdentity = capturedPlayerIdentity;
            this.Compatibility = compatibility;
            this.CompatibleMissionTypes = this.Compatibility.MissionTypes;
            this.Provenance = CopyProvenance(provenance);
            this.Completeness = completeness;
            this.IsSelectable = isSelectable;
            this.SelectionExclusionReason = (selectionExclusionReason ?? string.Empty).Trim();
        }

        internal int BundleFormatVersion { get; private set; }

        internal string LayoutId { get; private set; }

        internal int SourcePlayfield2 { get; private set; }

        internal MissionAcgIdentityRecord BuildingIdentity { get; private set; }

        internal string GeneratorPayloadSha256 { get; private set; }

        internal string ExpectedGeneratorPayloadSha256 { get; private set; }

        internal MissionAcgPointRecord EntryPoint { get; private set; }

        internal MissionAcgExitRecord Exit { get; private set; }

        internal ReadOnlyCollection<MissionAcgDynelRecord> Dynels { get; private set; }

        internal ReadOnlyCollection<MissionAcgWireRecord> WireRecords { get; private set; }

        internal ReadOnlyCollection<MissionAcgWireRecord> Doors { get; private set; }

        internal ReadOnlyCollection<MissionAcgWireRecord> Chests { get; private set; }

        internal ReadOnlyCollection<MissionAcgWireRecord> Terminals { get; private set; }

        internal ReadOnlyCollection<MissionAcgNpcSlotRecord> NpcSlots { get; private set; }

        internal ReadOnlyCollection<MissionAcgObjectiveSlotRecord> ObjectiveSlots { get; private set; }

        internal MissionAcgCaptureCountsRecord CaptureCounts { get; private set; }

        internal MissionAcgIdentityRecord CapturedPlayerIdentity { get; private set; }

        internal MissionAcgCompatibilityRecord Compatibility { get; private set; }

        internal ReadOnlyCollection<MissionRollType> CompatibleMissionTypes { get; private set; }

        internal ReadOnlyCollection<MissionAcgProvenanceRecord> Provenance { get; private set; }

        internal MissionAcgCompletenessRecord Completeness { get; private set; }

        internal bool IsSelectable { get; private set; }

        internal string SelectionExclusionReason { get; private set; }

        internal byte[] CopyGeneratorPayload()
        {
            return (byte[])this.generatorPayload.Clone();
        }

        internal bool SupportsMissionType(MissionRollType missionType)
        {
            return this.Compatibility.Supports(
                missionType,
                this.Compatibility.MinimumMissionQuality);
        }

        internal bool SupportsMission(MissionRollType missionType, int missionQuality)
        {
            return this.Compatibility.Supports(missionType, missionQuality);
        }

        private static List<MissionAcgDynelRecord> CopyDynels(
            IEnumerable<MissionAcgDynelRecord> records)
        {
            var copy = new List<MissionAcgDynelRecord>();
            if (records == null)
            {
                return copy;
            }

            foreach (MissionAcgDynelRecord record in records)
            {
                if (record == null)
                {
                    throw new ArgumentException("Dynel records cannot contain null.", "records");
                }

                copy.Add(record);
            }

            return copy;
        }

        private static List<MissionAcgWireRecord> SelectWireRecords(
            IEnumerable<MissionAcgDynelRecord> records)
        {
            var selected = new List<MissionAcgWireRecord>();
            foreach (MissionAcgDynelRecord record in records)
            {
                if (record.Wire != null)
                {
                    selected.Add(record.Wire);
                }
            }

            return selected;
        }

        private static ReadOnlyCollection<MissionAcgWireRecord> SelectWireRecords(
            IEnumerable<MissionAcgWireRecord> records,
            MissionAcgWireCategory category)
        {
            var selected = new List<MissionAcgWireRecord>();
            foreach (MissionAcgWireRecord record in records)
            {
                if (record.Category == category)
                {
                    selected.Add(record);
                }
            }

            return selected.AsReadOnly();
        }

        private static ReadOnlyCollection<MissionAcgNpcSlotRecord> CopyNpcSlots(
            IEnumerable<MissionAcgNpcSlotRecord> records)
        {
            var copy = new List<MissionAcgNpcSlotRecord>();
            if (records != null)
            {
                foreach (MissionAcgNpcSlotRecord record in records)
                {
                    if (record == null)
                    {
                        throw new ArgumentException("NPC slots cannot contain null.", "records");
                    }

                    copy.Add(record);
                }
            }

            return copy.AsReadOnly();
        }

        private static ReadOnlyCollection<MissionAcgObjectiveSlotRecord> CopyObjectiveSlots(
            IEnumerable<MissionAcgObjectiveSlotRecord> records)
        {
            var copy = new List<MissionAcgObjectiveSlotRecord>();
            if (records != null)
            {
                foreach (MissionAcgObjectiveSlotRecord record in records)
                {
                    if (record == null)
                    {
                        throw new ArgumentException("Objective slots cannot contain null.", "records");
                    }

                    copy.Add(record);
                }
            }

            return copy.AsReadOnly();
        }

        private static ReadOnlyCollection<MissionAcgProvenanceRecord> CopyProvenance(
            IEnumerable<MissionAcgProvenanceRecord> records)
        {
            var copy = new List<MissionAcgProvenanceRecord>();
            if (records != null)
            {
                foreach (MissionAcgProvenanceRecord record in records)
                {
                    if (record == null)
                    {
                        throw new ArgumentException("Provenance cannot contain null.", "records");
                    }

                    copy.Add(record);
                }
            }

            return copy.AsReadOnly();
        }
    }
}
