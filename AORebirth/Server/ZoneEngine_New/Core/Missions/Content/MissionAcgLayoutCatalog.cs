namespace ZoneEngine.Core.Missions
{
    #region Usings ...

    using System;
    using ZoneEngine_New.Core.Missions;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.Text;

    #endregion

    internal enum MissionAcgCatalogValidationCode
    {
        EmptyCatalog,
        NullBundle,
        DuplicateLayoutId,
        DuplicateSourcePlayfield2,
        BundleFormatConflict,
        BuildingPlayfieldConflict,
        InvalidGeneratorPayload,
        GeneratorHashMismatch,
        BuildingIdentityConflict,
        CompletenessConflict,
        SelectionConflict,
        MissionTypeConflict,
        ProvenanceMissing,
        WireConflict,
        StructuredRecordConflict,
        NonFiniteGeometry,
        CaptureCountConflict,
        RetargetConflict,
        DuplicateExclusion,
        ExclusionConflict,
        IncompleteShapeSelectable
    }

    internal sealed class MissionAcgCatalogValidationIssue
    {
        internal MissionAcgCatalogValidationIssue(
            MissionAcgCatalogValidationCode code,
            string layoutId,
            string message)
        {
            this.Code = code;
            this.LayoutId = layoutId ?? string.Empty;
            this.Message = message ?? string.Empty;
        }

        internal MissionAcgCatalogValidationCode Code { get; private set; }

        internal string LayoutId { get; private set; }

        internal string Message { get; private set; }
    }

    internal sealed class MissionAcgCatalogValidationResult
    {
        internal MissionAcgCatalogValidationResult(IEnumerable<MissionAcgCatalogValidationIssue> issues)
        {
            var copy = new List<MissionAcgCatalogValidationIssue>();
            if (issues != null)
            {
                foreach (MissionAcgCatalogValidationIssue issue in issues)
                {
                    if (issue != null)
                    {
                        copy.Add(issue);
                    }
                }
            }

            this.Issues = copy.AsReadOnly();
        }

        internal ReadOnlyCollection<MissionAcgCatalogValidationIssue> Issues { get; private set; }

        internal bool IsValid
        {
            get
            {
                return this.Issues.Count == 0;
            }
        }
    }

    internal sealed class MissionAcgLayoutCatalog
    {
        private readonly Dictionary<string, MissionAcgLayoutBundle> byLayoutId;

        private readonly Dictionary<int, MissionAcgLayoutBundle> bySourcePlayfield2;

        internal MissionAcgLayoutCatalog(
            IEnumerable<MissionAcgLayoutBundle> layouts,
            IEnumerable<MissionAcgLayoutExclusion> exclusions)
        {
            if (layouts == null)
            {
                throw new ArgumentNullException("layouts");
            }

            if (exclusions == null)
            {
                throw new ArgumentNullException("exclusions");
            }

            var layoutCopy = new List<MissionAcgLayoutBundle>(layouts);
            var exclusionCopy = new List<MissionAcgLayoutExclusion>(exclusions);
            layoutCopy.Sort(CompareLayouts);
            exclusionCopy.Sort(CompareExclusions);
            var selectable = new List<MissionAcgLayoutBundle>();
            this.byLayoutId =
                new Dictionary<string, MissionAcgLayoutBundle>(StringComparer.OrdinalIgnoreCase);
            this.bySourcePlayfield2 = new Dictionary<int, MissionAcgLayoutBundle>();

            for (int i = 0; i < layoutCopy.Count; i++)
            {
                MissionAcgLayoutBundle layout = layoutCopy[i];
                this.byLayoutId.Add(layout.LayoutId, layout);
                this.bySourcePlayfield2.Add(layout.SourcePlayfield2, layout);
                if (layout.IsSelectable)
                {
                    selectable.Add(layout);
                }
            }

            this.Layouts = layoutCopy.AsReadOnly();
            this.SelectableLayouts = selectable.AsReadOnly();
            this.Exclusions = exclusionCopy.AsReadOnly();
        }

        internal ReadOnlyCollection<MissionAcgLayoutBundle> Layouts { get; private set; }

        internal ReadOnlyCollection<MissionAcgLayoutBundle> SelectableLayouts { get; private set; }

        internal ReadOnlyCollection<MissionAcgLayoutExclusion> Exclusions { get; private set; }

        internal MissionAcgLayoutBundle FindByLayoutId(string layoutId)
        {
            if (string.IsNullOrWhiteSpace(layoutId))
            {
                return null;
            }

            MissionAcgLayoutBundle layout;
            return this.byLayoutId.TryGetValue(layoutId, out layout) ? layout : null;
        }

        internal MissionAcgLayoutBundle FindBySourcePlayfield2(int sourcePlayfield2)
        {
            MissionAcgLayoutBundle layout;
            return this.bySourcePlayfield2.TryGetValue(sourcePlayfield2, out layout) ? layout : null;
        }

        private static int CompareLayouts(MissionAcgLayoutBundle left, MissionAcgLayoutBundle right)
        {
            int result = string.Compare(
                left.LayoutId,
                right.LayoutId,
                StringComparison.Ordinal);
            return result != 0 ? result : left.SourcePlayfield2.CompareTo(right.SourcePlayfield2);
        }

        private static int CompareExclusions(
            MissionAcgLayoutExclusion left,
            MissionAcgLayoutExclusion right)
        {
            int result = string.Compare(left.LayoutId, right.LayoutId, StringComparison.Ordinal);
            return result != 0
                       ? result
                       : left.SourcePlayfield2.CompareTo(right.SourcePlayfield2);
        }
    }

    /// <summary>
    /// Builds an immutable catalog from generated or adapter-provided bundles. Validation is
    /// fail-closed: conflicts are reported together and Load never returns a partial catalog.
    /// </summary>
    internal static class MissionAcgLayoutCatalogLoader
    {
        internal static MissionAcgLayoutCatalog Load(
            IEnumerable<MissionAcgLayoutBundle> layouts,
            IEnumerable<MissionAcgLayoutExclusion> exclusions)
        {
            if (layouts == null)
            {
                throw new ArgumentNullException("layouts");
            }

            var layoutSnapshot = new List<MissionAcgLayoutBundle>(layouts);
            foreach (var layout in layoutSnapshot) layout?.ValidateShape();
            var exclusionSnapshot =
                exclusions == null
                    ? new List<MissionAcgLayoutExclusion>()
                    : new List<MissionAcgLayoutExclusion>(exclusions);
            SortSnapshots(layoutSnapshot, exclusionSnapshot);
            MissionAcgCatalogValidationResult validation =
                ValidateSnapshot(layoutSnapshot, exclusionSnapshot);
            if (!validation.IsValid)
            {
                throw new InvalidOperationException(BuildFailureMessage(validation));
            }

            return new MissionAcgLayoutCatalog(layoutSnapshot, exclusionSnapshot);
        }

        internal static MissionAcgCatalogValidationResult Validate(
            IEnumerable<MissionAcgLayoutBundle> layouts,
            IEnumerable<MissionAcgLayoutExclusion> exclusions)
        {
            if (layouts == null)
            {
                return new MissionAcgCatalogValidationResult(
                    new[]
                    {
                        new MissionAcgCatalogValidationIssue(
                            MissionAcgCatalogValidationCode.EmptyCatalog,
                            string.Empty,
                            "Mission ACG layout catalog input is null.")
                    });
            }

            var layoutSnapshot = new List<MissionAcgLayoutBundle>(layouts);
            var exclusionSnapshot =
                exclusions == null
                    ? new List<MissionAcgLayoutExclusion>()
                    : new List<MissionAcgLayoutExclusion>(exclusions);
            SortSnapshots(layoutSnapshot, exclusionSnapshot);
            return ValidateSnapshot(layoutSnapshot, exclusionSnapshot);
        }

        private static MissionAcgCatalogValidationResult ValidateSnapshot(
            IList<MissionAcgLayoutBundle> layouts,
            IList<MissionAcgLayoutExclusion> exclusions)
        {
            var issues = new List<MissionAcgCatalogValidationIssue>();
            if (layouts.Count == 0)
            {
                issues.Add(
                    new MissionAcgCatalogValidationIssue(
                        MissionAcgCatalogValidationCode.EmptyCatalog,
                        string.Empty,
                        "Mission ACG layout catalog is empty."));
            }

            var layoutIds = new Dictionary<string, MissionAcgLayoutBundle>(StringComparer.OrdinalIgnoreCase);
            var playfieldIds = new Dictionary<int, MissionAcgLayoutBundle>();
            var buildingOwners = new Dictionary<string, MissionAcgLayoutBundle>(StringComparer.Ordinal);

            for (int i = 0; i < layouts.Count; i++)
            {
                MissionAcgLayoutBundle layout = layouts[i];
                if (layout == null)
                {
                    issues.Add(
                        new MissionAcgCatalogValidationIssue(
                            MissionAcgCatalogValidationCode.NullBundle,
                            string.Empty,
                            "Mission ACG layout catalog contains a null bundle at index " + i + "."));
                    continue;
                }

                MissionAcgLayoutBundle conflict;
                if (layoutIds.TryGetValue(layout.LayoutId, out conflict))
                {
                    issues.Add(
                        Issue(
                            MissionAcgCatalogValidationCode.DuplicateLayoutId,
                            layout,
                            "Duplicate layout id conflicts with source PF2 "
                            + conflict.SourcePlayfield2
                            + "."));
                }
                else
                {
                    layoutIds.Add(layout.LayoutId, layout);
                }

                if (playfieldIds.TryGetValue(layout.SourcePlayfield2, out conflict))
                {
                    issues.Add(
                        Issue(
                            MissionAcgCatalogValidationCode.DuplicateSourcePlayfield2,
                            layout,
                            "Source PF2 conflicts with layout " + conflict.LayoutId + "."));
                }
                else
                {
                    playfieldIds.Add(layout.SourcePlayfield2, layout);
                }

                if (layout.BuildingIdentity != null)
                {
                    string buildingKey =
                        layout.BuildingIdentity.Type + ":" + layout.BuildingIdentity.Instance;
                    if (buildingOwners.TryGetValue(buildingKey, out conflict)
                        && conflict.SourcePlayfield2 != layout.SourcePlayfield2
                        && !string.Equals(
                            conflict.GeneratorPayloadSha256,
                            layout.GeneratorPayloadSha256,
                            StringComparison.OrdinalIgnoreCase))
                    {
                        issues.Add(
                            Issue(
                                MissionAcgCatalogValidationCode.BuildingPlayfieldConflict,
                                layout,
                                "Building identity conflicts with PF2/layout "
                                + conflict.SourcePlayfield2
                                + "/"
                                + conflict.LayoutId
                                + "."));
                    }
                    else
                    {
                        buildingOwners[buildingKey] = layout;
                    }
                }

                ValidateBundle(layout, issues);
            }

            ValidateExclusions(exclusions, playfieldIds, layoutIds, issues);
            return new MissionAcgCatalogValidationResult(issues);
        }

        private static void ValidateBundle(
            MissionAcgLayoutBundle layout,
            ICollection<MissionAcgCatalogValidationIssue> issues)
        {
            byte[] payload = layout.CopyGeneratorPayload();
            bool hasPayload = payload.Length > 0;
            bool hasBuilding = layout.BuildingIdentity != null;
            bool hasEntry = layout.EntryPoint != null;
            bool hasExit = layout.Exit != null;
            bool hasDoors = layout.Doors.Count > 0;
            bool hasChests = layout.Chests.Count > 0;
            bool hasNpcSlots = layout.NpcSlots.Count > 0;
            bool hasObjectiveSlots = layout.ObjectiveSlots.Count > 0;

            if (layout.BundleFormatVersion != MissionAcgLayoutBundle.CurrentFormatVersion)
            {
                issues.Add(
                    Issue(
                        MissionAcgCatalogValidationCode.BundleFormatConflict,
                        layout,
                        "Bundle format version is not supported by this runtime."));
            }

            if (hasPayload)
            {
                string actualHash = MissionAcgHash.ComputeSha256(payload);
                if (!string.Equals(
                    actualHash,
                    layout.ExpectedGeneratorPayloadSha256,
                    StringComparison.OrdinalIgnoreCase))
                {
                    issues.Add(
                        Issue(
                            MissionAcgCatalogValidationCode.GeneratorHashMismatch,
                            layout,
                            "Generator SHA-256 does not match its independent expected hash."));
                }

                if (payload.Length < 8)
                {
                    issues.Add(
                        Issue(
                            MissionAcgCatalogValidationCode.InvalidGeneratorPayload,
                            layout,
                            "Generator payload is shorter than its building identity header."));
                }
                else if (!hasBuilding
                         || MissionAcgHash.ReadInt32BigEndian(payload, 0) != layout.BuildingIdentity.Type
                         || MissionAcgHash.ReadInt32BigEndian(payload, 4)
                         != layout.BuildingIdentity.Instance)
                {
                    issues.Add(
                        Issue(
                            MissionAcgCatalogValidationCode.BuildingIdentityConflict,
                            layout,
                            "Generator header conflicts with the captured building identity."));
                }
            }

            MissionAcgCompletenessRecord completeness = layout.Completeness;
            if (!Enum.IsDefined(
                    typeof(MissionAcgLayoutCompletenessState),
                    completeness.State))
            {
                issues.Add(
                    Issue(
                        MissionAcgCatalogValidationCode.CompletenessConflict,
                        layout,
                        "Completeness state is outside the supported enum domain."));
            }

            if (completeness.HasGeneratorPayload != hasPayload
                || completeness.HasBuildingIdentity != hasBuilding
                || completeness.HasEntryPoint != hasEntry
                || completeness.HasExit != hasExit
                || completeness.HasDoorWire != hasDoors
                || completeness.HasChestWire != hasChests
                || completeness.HasNpcSlots != hasNpcSlots
                || completeness.HasObjectiveSlots != hasObjectiveSlots)
            {
                issues.Add(
                    Issue(
                        MissionAcgCatalogValidationCode.CompletenessConflict,
                        layout,
                        "Completeness flags conflict with bundle content."));
            }

            if (layout.IsSelectable)
            {
                if (!completeness.IsSelectionComplete)
                {
                    issues.Add(
                        Issue(
                            MissionAcgCatalogValidationCode.SelectionConflict,
                            layout,
                            "Selectable layout is not complete and coherent."));
                }

                if (!string.IsNullOrEmpty(layout.SelectionExclusionReason))
                {
                    issues.Add(
                        Issue(
                            MissionAcgCatalogValidationCode.SelectionConflict,
                            layout,
                            "Selectable layout carries an exclusion reason."));
                }
            }
            else if (string.IsNullOrEmpty(layout.SelectionExclusionReason))
            {
                issues.Add(
                    Issue(
                        MissionAcgCatalogValidationCode.SelectionConflict,
                        layout,
                        "Unselectable layout is missing an exclusion reason."));
            }

            if (completeness.State == MissionAcgLayoutCompletenessState.CompleteSelectable
                && !layout.IsSelectable)
            {
                issues.Add(
                    Issue(
                        MissionAcgCatalogValidationCode.SelectionConflict,
                        layout,
                        "CompleteSelectable state must be selectable."));
            }

            if (completeness.State != MissionAcgLayoutCompletenessState.CompleteSelectable
                && layout.IsSelectable)
            {
                issues.Add(
                    Issue(
                        MissionAcgCatalogValidationCode.SelectionConflict,
                        layout,
                        "Only CompleteSelectable state may enter the selection pool."));
            }

            if (completeness.State == MissionAcgLayoutCompletenessState.ConflictingRejected)
            {
                issues.Add(
                    Issue(
                        MissionAcgCatalogValidationCode.SelectionConflict,
                        layout,
                        "ConflictingRejected bundle cannot be loaded into a catalog."));
            }

            ValidateMissionTypes(layout, issues);
            // Provenance remains available to offline audits; structural content owns runtime eligibility.
            ValidateWire(layout, issues);
            ValidateStructuredRecords(layout, issues);
        }

        private static void ValidateMissionTypes(
            MissionAcgLayoutBundle layout,
            ICollection<MissionAcgCatalogValidationIssue> issues)
        {
            var seen = new HashSet<MissionRollType>();
            for (int i = 0; i < layout.CompatibleMissionTypes.Count; i++)
            {
                MissionRollType missionType = layout.CompatibleMissionTypes[i];
                if (!Enum.IsDefined(typeof(MissionRollType), missionType)
                    || missionType == MissionRollType.Unknown
                    || !seen.Add(missionType))
                {
                    issues.Add(
                        Issue(
                            MissionAcgCatalogValidationCode.MissionTypeConflict,
                            layout,
                            "Mission type compatibility contains Unknown or a duplicate value."));
                }
            }

            if (layout.IsSelectable && seen.Count == 0)
            {
                issues.Add(
                    Issue(
                        MissionAcgCatalogValidationCode.MissionTypeConflict,
                        layout,
                        "Selectable layout has no compatible mission types."));
            }
        }

        private static void ValidateWire(MissionAcgLayoutBundle layout, ICollection<MissionAcgCatalogValidationIssue> issues)
        {
            var slots = new HashSet<string>(StringComparer.Ordinal);
            var identities = new HashSet<string>(StringComparer.Ordinal);
            foreach (var wire in layout.WireRecords)
            {
                if (!Enum.IsDefined(typeof(MissionAcgWireCategory), wire.Category) || wire.Category == MissionAcgWireCategory.Unknown
                    || !slots.Add(((int)wire.Category) + ":" + wire.Slot) || IsZeroIdentity(wire.CapturedIdentity)
                    || !identities.Add(wire.CapturedIdentity.Type + ":" + wire.CapturedIdentity.Instance)
                    || wire.CapturedPlayfield2 != layout.SourcePlayfield2)
                    AddStructuredIssue(layout, issues, "Invalid or duplicate typed dynel slot/identity/playfield.");
                try
                {
                    var spawn = wire.Spawn ?? throw new InvalidOperationException("Missing typed dynel content.");
                    spawn.Validate();
                    if (wire.Category == MissionAcgWireCategory.Door && spawn.Door == null
                        || wire.Category == MissionAcgWireCategory.Chest && spawn.Chest == null
                        || wire.Category == MissionAcgWireCategory.Terminal && spawn.Item == null)
                        throw new InvalidOperationException("Dynel category conflicts with its typed definition.");
                }
                catch (Exception error) { AddStructuredIssue(layout, issues, error.Message); }
            }
        }

        private static void ValidateStructuredRecords(
            MissionAcgLayoutBundle layout,
            ICollection<MissionAcgCatalogValidationIssue> issues)
        {
            if (!IsFinite(layout.EntryPoint))
            {
                issues.Add(
                    Issue(
                        MissionAcgCatalogValidationCode.NonFiniteGeometry,
                        layout,
                        "Entry point is missing or contains NaN/Infinity."));
            }

            var identities =
                new Dictionary<string, StructuredIdentityEvidence>(StringComparer.Ordinal);
            var dynelSlots = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < layout.Dynels.Count; i++)
            {
                MissionAcgDynelRecord dynel = layout.Dynels[i];
                if (!Enum.IsDefined(typeof(MissionAcgWireCategory), dynel.Category)
                    || dynel.Category == MissionAcgWireCategory.Unknown)
                {
                    AddStructuredIssue(
                        layout,
                        issues,
                        "Dynel category is outside the supported enum domain.");
                }

                string category = "dynel:" + dynel.Category;
                string slotKey = category + ":" + dynel.Slot;
                if (!dynelSlots.Add(slotKey))
                {
                    AddStructuredIssue(layout, issues, "Duplicate structured slot " + slotKey + ".");
                }

                MissionAcgWireRecord wire = dynel.Wire;
                ValidateStructuredRecord(
                    layout,
                    category,
                    dynel.Slot,
                    dynel.CapturedIdentity,
                    dynel.CapturedPlayfield2,
                    dynel.CapturedParentIdentity,
                    dynel.Position,
                    dynel.Heading,
                    wire?.Spawn,
                    null,
                    dynel.Provenance,
                    identities,
                    issues);
            }

            var npcSlots = new HashSet<int>();
            for (int i = 0; i < layout.NpcSlots.Count; i++)
            {
                MissionAcgNpcSlotRecord npc = layout.NpcSlots[i];
                if (!npcSlots.Add(npc.Slot))
                {
                    AddStructuredIssue(
                        layout,
                        issues,
                        "Duplicate NPC slot " + npc.Slot + ".");
                }

                ValidateStructuredRecord(
                    layout,
                    "npc",
                    npc.Slot,
                    npc.CapturedIdentity,
                    npc.CapturedPlayfield2,
                    npc.CapturedParentIdentity,
                    npc.Position,
                    npc.Heading,
                    npc.Spawn,
                    npc.Appearance,
                    npc.Provenance,
                    identities,
                    issues);
            }

            var objectiveSlots = new HashSet<int>();
            for (int i = 0; i < layout.ObjectiveSlots.Count; i++)
            {
                MissionAcgObjectiveSlotRecord objective = layout.ObjectiveSlots[i];
                if (!objectiveSlots.Add(objective.Slot))
                {
                    AddStructuredIssue(
                        layout,
                        issues,
                        "Duplicate objective slot " + objective.Slot + ".");
                }

                if (layout.IsSelectable
                    && !HasCompatibleObjectiveType(layout, objective))
                {
                    AddStructuredIssue(
                        layout,
                        issues,
                        "Objective slot "
                        + objective.Slot
                        + " has no mission type shared with its bundle.");
                }

                ValidateStructuredRecord(
                    layout,
                    "objective",
                    objective.Slot,
                    objective.CapturedIdentity,
                    objective.CapturedPlayfield2,
                    objective.CapturedParentIdentity,
                    objective.Position,
                    objective.Heading,
                    objective.Spawn,
                    objective.Appearance,
                    objective.Provenance,
                    identities,
                    issues);
            }

            if (layout.Exit != null)
            {
                ValidateStructuredRecord(
                    layout,
                    "exit",
                    0,
                    layout.Exit.CapturedIdentity,
                    layout.Exit.CapturedPlayfield2,
                    layout.Exit.CapturedParentIdentity,
                    layout.Exit.Position,
                    layout.Exit.Heading,
                    layout.Exit.Spawn,
                    layout.Exit.Appearance,
                    layout.Exit.Provenance,
                    identities,
                    issues);
            }
        }

        private static void ValidateStructuredRecord(
            MissionAcgLayoutBundle layout,
            string category,
            int slot,
            MissionAcgIdentityRecord identity,
            int? capturedPlayfield2,
            MissionAcgIdentityRecord parentIdentity,
            MissionAcgPointRecord position,
            MissionAcgRotationRecord heading,
            MissionSpawnContent spawn,
            MissionNpcAppearance appearance,
            IReadOnlyList<MissionAcgProvenanceRecord> provenance,
            IDictionary<string, StructuredIdentityEvidence> identities,
            ICollection<MissionAcgCatalogValidationIssue> issues)
        {
            string label = category + ":" + slot;
            bool required = layout.IsSelectable;
            bool hasIdentity = !IsZeroIdentity(identity);
            if (required && !hasIdentity)
            {
                AddStructuredIssue(layout, issues, label + " has no placement identity.");
            }

            if (!capturedPlayfield2.HasValue)
            {
                if (required)
                {
                    AddStructuredIssue(layout, issues, label + " has no source playfield.");
                }
            }
            else if (capturedPlayfield2.Value != layout.SourcePlayfield2)
            {
                AddStructuredIssue(
                    layout,
                    issues,
                    label + " PF2 conflicts with the bundle source PF2.");
            }

            if (position == null)
            {
                if (required)
                {
                    AddStructuredIssue(layout, issues, label + " has no placement position.");
                }
            }
            else if (!IsFinite(position))
            {
                issues.Add(
                    Issue(
                        MissionAcgCatalogValidationCode.NonFiniteGeometry,
                        layout,
                        label + " position contains NaN/Infinity."));
            }

            if (heading != null && !IsFinite(heading))
            {
                issues.Add(
                    Issue(
                        MissionAcgCatalogValidationCode.NonFiniteGeometry,
                        layout,
                        label + " heading contains NaN/Infinity."));
            }

            try
            {
                if (appearance != null) appearance.Validate();
                else if (spawn != null) spawn.ValidatePlacement(identity, capturedPlayfield2);
                else if (required) throw new InvalidOperationException(label + " has no permanent typed content.");
            }
            catch (Exception error) { AddStructuredIssue(layout, issues, error.Message); }
            string contentSignature = System.Text.Json.JsonSerializer.Serialize(new { spawn, appearance }, MissionTypedJson.Options);

            if (hasIdentity)
            {
                RegisterStructuredIdentity(
                    layout,
                    category,
                    slot,
                    identity,
                    capturedPlayfield2,
                    parentIdentity,
                    position,
                    heading,
                    contentSignature,
                    identities,
                    issues);
            }
        }

        private static void RegisterStructuredIdentity(
            MissionAcgLayoutBundle layout,
            string category,
            int slot,
            MissionAcgIdentityRecord identity,
            int? capturedPlayfield2,
            MissionAcgIdentityRecord parentIdentity,
            MissionAcgPointRecord position,
            MissionAcgRotationRecord heading,
            string contentSignature,
            IDictionary<string, StructuredIdentityEvidence> identities,
            ICollection<MissionAcgCatalogValidationIssue> issues)
        {
            string key = identity.Type + ":" + identity.Instance;
            var current =
                new StructuredIdentityEvidence(
                    category,
                    slot,
                    capturedPlayfield2,
                    parentIdentity,
                    position,
                    heading,
                    contentSignature);
            StructuredIdentityEvidence existing;
            if (!identities.TryGetValue(key, out existing))
            {
                identities.Add(key, current);
                return;
            }

            if (string.Equals(existing.Category, category, StringComparison.Ordinal)
                || !StructuredIdentityEvidence.AreCoherent(existing, current))
            {
                AddStructuredIssue(
                    layout,
                    issues,
                    "Placement identity "
                    + key
                    + " is reused by conflicting "
                    + existing.Category
                    + ":"
                    + existing.Slot
                    + " and "
                    + category
                    + ":"
                    + slot
                    + " records.");
            }
        }

        private static bool HasCompatibleObjectiveType(
            MissionAcgLayoutBundle layout,
            MissionAcgObjectiveSlotRecord objective)
        {
            for (int i = 0; i < objective.CompatibleMissionTypes.Count; i++)
            {
                MissionRollType objectiveType = objective.CompatibleMissionTypes[i];
                if (objectiveType == MissionRollType.Unknown)
                {
                    continue;
                }

                for (int j = 0; j < layout.CompatibleMissionTypes.Count; j++)
                {
                    if (objectiveType == layout.CompatibleMissionTypes[j])
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private static void AddStructuredIssue(
            MissionAcgLayoutBundle layout,
            ICollection<MissionAcgCatalogValidationIssue> issues,
            string message)
        {
            issues.Add(
                Issue(
                    MissionAcgCatalogValidationCode.StructuredRecordConflict,
                    layout,
                    message));
        }

        private static bool IsZeroIdentity(MissionAcgIdentityRecord identity)
        {
            return identity == null || (identity.Type == 0 && identity.Instance == 0);
        }

        private static bool IsFinite(MissionAcgPointRecord point)
        {
            return point != null
                   && IsFinite(point.X)
                   && IsFinite(point.Y)
                   && IsFinite(point.Z);
        }

        private static bool IsFinite(MissionAcgRotationRecord rotation)
        {
            return rotation != null
                   && IsFinite(rotation.X)
                   && IsFinite(rotation.Y)
                   && IsFinite(rotation.Z)
                   && IsFinite(rotation.W);
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }

        private static void ValidateExclusions(
            IEnumerable<MissionAcgLayoutExclusion> exclusions,
            IDictionary<int, MissionAcgLayoutBundle> playfieldIds,
            IDictionary<string, MissionAcgLayoutBundle> layoutIds,
            ICollection<MissionAcgCatalogValidationIssue> issues)
        {
            var excludedPlayfields = new HashSet<int>();
            var excludedLayoutIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (MissionAcgLayoutExclusion exclusion in exclusions)
            {
                if (exclusion == null)
                {
                    issues.Add(
                        new MissionAcgCatalogValidationIssue(
                            MissionAcgCatalogValidationCode.DuplicateExclusion,
                            string.Empty,
                            "Exclusion catalog contains null."));
                    continue;
                }

                if (!excludedPlayfields.Add(exclusion.SourcePlayfield2)
                    || (!string.IsNullOrEmpty(exclusion.LayoutId)
                        && !excludedLayoutIds.Add(exclusion.LayoutId)))
                {
                    issues.Add(
                        new MissionAcgCatalogValidationIssue(
                            MissionAcgCatalogValidationCode.DuplicateExclusion,
                            exclusion.LayoutId,
                            "Duplicate layout exclusion for PF2 " + exclusion.SourcePlayfield2 + "."));
                }

                MissionAcgLayoutBundle layout;
                if (playfieldIds.TryGetValue(exclusion.SourcePlayfield2, out layout)
                    && layout.IsSelectable)
                {
                    issues.Add(
                        Issue(
                            MissionAcgCatalogValidationCode.ExclusionConflict,
                            layout,
                            "Selectable layout conflicts with its PF2 exclusion."));
                }

                if (!string.IsNullOrEmpty(exclusion.LayoutId)
                    && layoutIds.TryGetValue(exclusion.LayoutId, out layout)
                    && layout.IsSelectable)
                {
                    issues.Add(
                        Issue(
                            MissionAcgCatalogValidationCode.ExclusionConflict,
                            layout,
                            "Selectable layout conflicts with its layout-id exclusion."));
                }
            }
        }

        private sealed class StructuredIdentityEvidence
        {
            internal StructuredIdentityEvidence(
                string category,
                int slot,
                int? capturedPlayfield2,
                MissionAcgIdentityRecord parentIdentity,
                MissionAcgPointRecord position,
                MissionAcgRotationRecord heading,
                string contentSignature)
            {
                this.Category = category;
                this.Slot = slot;
                this.CapturedPlayfield2 = capturedPlayfield2;
                this.ParentIdentity = parentIdentity;
                this.Position = position;
                this.Heading = heading;
                this.ContentSignature = contentSignature ?? string.Empty;
            }

            internal string Category { get; private set; }

            internal int Slot { get; private set; }

            internal int? CapturedPlayfield2 { get; private set; }

            internal MissionAcgIdentityRecord ParentIdentity { get; private set; }

            internal MissionAcgPointRecord Position { get; private set; }

            internal MissionAcgRotationRecord Heading { get; private set; }

            internal string ContentSignature { get; private set; }

            internal static bool AreCoherent(
                StructuredIdentityEvidence left,
                StructuredIdentityEvidence right)
            {
                return left.CapturedPlayfield2 == right.CapturedPlayfield2
                       && IdentityEquals(left.ParentIdentity, right.ParentIdentity)
                       && PointEquals(left.Position, right.Position)
                       && RotationEquals(left.Heading, right.Heading)
                       && !string.IsNullOrEmpty(left.ContentSignature)
                       && string.Equals(
                           left.ContentSignature,
                           right.ContentSignature,
                           StringComparison.Ordinal);
            }

            private static bool IdentityEquals(
                MissionAcgIdentityRecord left,
                MissionAcgIdentityRecord right)
            {
                if (ReferenceEquals(left, right))
                {
                    return true;
                }

                return left != null
                       && right != null
                       && left.Type == right.Type
                       && left.Instance == right.Instance;
            }

            private static bool PointEquals(
                MissionAcgPointRecord left,
                MissionAcgPointRecord right)
            {
                if (ReferenceEquals(left, right))
                {
                    return true;
                }

                return left != null
                       && right != null
                       && left.X.Equals(right.X)
                       && left.Y.Equals(right.Y)
                       && left.Z.Equals(right.Z);
            }

            private static bool RotationEquals(
                MissionAcgRotationRecord left,
                MissionAcgRotationRecord right)
            {
                if (ReferenceEquals(left, right))
                {
                    return true;
                }

                return left != null
                       && right != null
                       && left.X.Equals(right.X)
                       && left.Y.Equals(right.Y)
                       && left.Z.Equals(right.Z)
                       && left.W.Equals(right.W);
            }
        }

        private static MissionAcgCatalogValidationIssue Issue(
            MissionAcgCatalogValidationCode code,
            MissionAcgLayoutBundle layout,
            string message)
        {
            return new MissionAcgCatalogValidationIssue(code, layout.LayoutId, message);
        }

        private static string BuildFailureMessage(MissionAcgCatalogValidationResult validation)
        {
            var builder = new StringBuilder("Mission ACG layout catalog validation failed");
            int count = Math.Min(validation.Issues.Count, 8);
            for (int i = 0; i < count; i++)
            {
                MissionAcgCatalogValidationIssue issue = validation.Issues[i];
                builder.Append(i == 0 ? ": " : " | ");
                builder.Append(issue.Code);
                if (!string.IsNullOrEmpty(issue.LayoutId))
                {
                    builder.Append(" [");
                    builder.Append(issue.LayoutId);
                    builder.Append("]");
                }

                builder.Append(" ");
                builder.Append(issue.Message);
            }

            if (validation.Issues.Count > count)
            {
                builder.Append(" | ");
                builder.Append(validation.Issues.Count - count);
                builder.Append(" additional issue(s)");
            }

            return builder.ToString();
        }

        private static void SortSnapshots(
            List<MissionAcgLayoutBundle> layouts,
            List<MissionAcgLayoutExclusion> exclusions)
        {
            layouts.Sort(
                delegate(MissionAcgLayoutBundle left, MissionAcgLayoutBundle right)
                {
                    if (ReferenceEquals(left, right))
                    {
                        return 0;
                    }

                    if (left == null)
                    {
                        return -1;
                    }

                    if (right == null)
                    {
                        return 1;
                    }

                    int result = string.Compare(
                        left.LayoutId,
                        right.LayoutId,
                        StringComparison.Ordinal);
                    return result != 0
                               ? result
                               : left.SourcePlayfield2.CompareTo(right.SourcePlayfield2);
                });
            exclusions.Sort(
                delegate(MissionAcgLayoutExclusion left, MissionAcgLayoutExclusion right)
                {
                    if (ReferenceEquals(left, right))
                    {
                        return 0;
                    }

                    if (left == null)
                    {
                        return -1;
                    }

                    if (right == null)
                    {
                        return 1;
                    }

                    int result = string.Compare(
                        left.LayoutId,
                        right.LayoutId,
                        StringComparison.Ordinal);
                    return result != 0
                               ? result
                               : left.SourcePlayfield2.CompareTo(right.SourcePlayfield2);
                });
        }
    }
}
