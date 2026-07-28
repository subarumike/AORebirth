namespace AOSharpMissionCaptureAnalyzer
{
    #region Usings ...

    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.IO;
    using System.Linq;
    using System.Text;

    #endregion

    /// <summary>
    /// Deterministically emits the runtime catalog source from the five finalized lifecycle
    /// captures. The manifest is a promotion gate: no output is written unless every authoritative
    /// tuple, physical observation count, payload hash, and catalog state matches.
    /// </summary>
    internal static class MissionAcgCapturedLayoutCatalogEmitter
    {
        private const string ExpectedSchema = "ao-rebirth.mission-acg-layout";

        private static readonly CaptureExpectation[] Manifest =
        {
            new CaptureExpectation(
                "20260728-001044",
                "kill",
                11330,
                0x00D734E2,
                0x0015F008,
                new CaptureCountExpectation(23, 22, 0, 16, 15, 7, 11, 8, 0, 7, 1),
                "ffe4327ac8af0f0a41a04cff7fe53ecd40c55a027f10a2cda2cd2a8fc18f1269",
                new CapturedRecordExpectation(
                    0x0000C350,
                    0x79A16B61,
                    26097,
                    "accepted_qfu_action_identity_to_scfu",
                    "QuestActions[0].UnknownId2",
                    "8eb8f42d1e5ab931e37a25da740dc9c965a387bc7d2a6ad061c0922b2c868de0"),
                new CapturedRecordExpectation(
                    0x0000C748,
                    0x109AAC07,
                    0,
                    "unique_decoded_boundary_sentinel_nearest_interior_spawn",
                    "DoorFullUpdate.Unknown6+Unknown7",
                    "b441fbf108dd4eb52dd2d8d1775cb265ab9c027fe056855a9bcf2600954963c5"),
                "complete_and_selectable",
                true),
            new CaptureExpectation(
                "20260728-003410",
                "return_item",
                11329,
                0x00D6FC77,
                0x0016C80E,
                new CaptureCountExpectation(64, 44, 0, 59, 58, 11, 21, 15, 0, 21, 1),
                "f7f00e3344bd12f2d7d302761403c9c5b083fc8a181417c7f2c9748da501ff59",
                new CapturedRecordExpectation(
                    0x0000C74A,
                    0x2586CCB1,
                    124914,
                    "accepted_qfu_action_identity_to_item_full_update",
                    "QuestActions[0].Action",
                    "9ae822c3853c45b046b4b5b1f489e873ac1a22a14f059810dda2728382e25b48"),
                new CapturedRecordExpectation(
                    0x0000C748,
                    0x109AD151,
                    0,
                    "unique_decoded_boundary_sentinel_nearest_interior_spawn",
                    "DoorFullUpdate.Unknown6+Unknown7",
                    "28c96e6e81fb6158276b75e4aab54a4595931bc58e9fea459af0f197ba489682"),
                "complete_and_selectable",
                true),
            new CaptureExpectation(
                "20260728-005042",
                "find_item",
                11337,
                0x00D6FC78,
                0x00169802,
                new CaptureCountExpectation(59, 27, 0, 40, 39, 5, 27, 13, 0, 18, 1),
                "3cfe53d3a32b50679530bdfd5ff7572405eb8865f4ab0c13308c7bcd935bf431",
                new CapturedRecordExpectation(
                    0x0000C73D,
                    0x57AC07B0,
                    40838,
                    "accepted_qfu_action_identity_to_item_full_update",
                    "QuestActions[0].Action",
                    "bcb404c9e7b32d26942a5256a63cc482feac6e0bc8318877abff64420ef0c683"),
                new CapturedRecordExpectation(
                    0x0000C748,
                    0x109AC391,
                    0,
                    "unique_decoded_boundary_sentinel_nearest_interior_spawn",
                    "DoorFullUpdate.Unknown6+Unknown7",
                    "c147df9dd9f3c5cb16b0e8603b540c0b10be5da9d2bbcad810eb119fe7d6fd38"),
                "complete_and_selectable",
                true),
            new CaptureExpectation(
                "20260728-010220",
                "repair",
                11342,
                0x00D734E5,
                0x0015F00F,
                new CaptureCountExpectation(26, 15, 0, 29, 28, 6, 17, 12, 0, 19, 1),
                "e75f1326a72db6d42ddb5ebd72320338148193e6469e70b1c30b2d8a0f6d1926",
                new CapturedRecordExpectation(
                    0x0000C73D,
                    0x57A3C596,
                    100358,
                    "accepted_qfu_action_identity_to_item_full_update",
                    "QuestActions[0].UnknownId1",
                    "5d55d2a137fbbd874d8fed736b8743ec8632c5ac98836773c51163daf683f1d8"),
                new CapturedRecordExpectation(
                    0x0000C748,
                    0x109AB591,
                    0,
                    "unique_decoded_boundary_sentinel_nearest_interior_spawn",
                    "DoorFullUpdate.Unknown6+Unknown7",
                    "2dea4d956445b3c4337ad8092dee9e1778e6a54ac9c4b69ed25bf20ab3dbc93e"),
                "complete_and_selectable",
                true),
            new CaptureExpectation(
                "20260728-012547",
                "find_person",
                11335,
                0x00D734E7,
                0x0016700C,
                new CaptureCountExpectation(56, 39, 0, 46, 45, 49, 14, 11, 0, 14, 1),
                "d5413273f69b018b66fcd6fe31bfa7be15b338cb6cb8fd17d83f7e14c4e4be82",
                new CapturedRecordExpectation(
                    0x0000C350,
                    0x79A16EB9,
                    26097,
                    "accepted_qfu_action_identity_to_scfu",
                    "QuestActions[0].UnknownId2",
                    "84089493d345420a3199cd978dd5e34dafd1ec51a93b855e070313b82ad31f6b"),
                new CapturedRecordExpectation(
                    0x0000C748,
                    0x109AACF8,
                    0,
                    "unique_decoded_boundary_sentinel_nearest_interior_spawn",
                    "DoorFullUpdate.Unknown6+Unknown7",
                    "e2d4d82b8595e84d48f652932252a199c47b82b0c0b2047ccc43691a8a4188fe"),
                "complete_and_selectable",
                true)
        };

        internal static int EmitFromCorpus(string capturesRoot, string outputPath)
        {
            try
            {
                Emit(capturesRoot, outputPath);
                Console.WriteLine(
                    "Mission ACG captured runtime catalog emitted: " + Path.GetFullPath(outputPath));
                return 0;
            }
            catch (Exception exception)
            {
                Console.Error.WriteLine(
                    "Mission ACG captured runtime catalog emission FAIL: " + exception.Message);
                return 1;
            }
        }

        internal static void Emit(string capturesRoot, string outputPath)
        {
            if (string.IsNullOrWhiteSpace(outputPath))
            {
                throw new ArgumentException("Output source path is required.", "outputPath");
            }

            IList<AcgLayoutArtifact> artifacts = ExtractAndValidateManifest(capturesRoot);
            string source = BuildSource(artifacts);
            string fullOutputPath = Path.GetFullPath(outputPath);
            string outputDirectory = Path.GetDirectoryName(fullOutputPath);
            if (string.IsNullOrEmpty(outputDirectory))
            {
                throw new InvalidOperationException("Output source directory could not be resolved.");
            }

            Directory.CreateDirectory(outputDirectory);
            File.WriteAllText(fullOutputPath, source, new UTF8Encoding(false));
        }

        internal static IList<AcgLayoutArtifact> ExtractAndValidateManifest(string capturesRoot)
        {
            if (string.IsNullOrWhiteSpace(capturesRoot))
            {
                throw new ArgumentException("Captures root is required.", "capturesRoot");
            }

            string root = Path.GetFullPath(capturesRoot);
            if (!Directory.Exists(root))
            {
                throw new DirectoryNotFoundException("Captures root was not found: " + root);
            }

            var artifacts = new List<AcgLayoutArtifact>();
            for (int i = 0; i < Manifest.Length; i++)
            {
                CaptureExpectation expectation = Manifest[i];
                string captureFolder = Path.Combine(root, expectation.Session);
                if (!Directory.Exists(captureFolder))
                {
                    throw new DirectoryNotFoundException(
                        "Required finalized capture was not found: " + captureFolder);
                }

                AcgLayoutArtifact artifact = AcgLayoutExtractor.Extract(captureFolder);
                Validate(expectation, artifact);
                artifacts.Add(artifact);
            }

            if (artifacts.Count != Manifest.Length)
            {
                throw new InvalidOperationException(
                    "Generated catalog artifact count does not match the five-entry manifest.");
            }

            return artifacts.AsReadOnly();
        }

        internal static string BuildSource(IList<AcgLayoutArtifact> artifacts)
        {
            if (artifacts == null)
            {
                throw new ArgumentNullException("artifacts");
            }

            var bySession =
                artifacts.ToDictionary(
                    artifact => artifact.CaptureSession,
                    StringComparer.Ordinal);
            var ordered = new List<AcgLayoutArtifact>();
            for (int i = 0; i < Manifest.Length; i++)
            {
                AcgLayoutArtifact artifact;
                if (!bySession.TryGetValue(Manifest[i].Session, out artifact))
                {
                    throw new InvalidOperationException(
                        "Artifact list is missing manifest session " + Manifest[i].Session + ".");
                }

                Validate(Manifest[i], artifact);
                ordered.Add(artifact);
            }

            if (bySession.Count != Manifest.Length)
            {
                throw new InvalidOperationException(
                    "Artifact list contains a duplicate or non-manifest capture session.");
            }

            var builder = new StringBuilder(1024 * 256);
            Line(builder, 0, "// <auto-generated />");
            Line(builder, 0, "namespace ZoneEngine.Core.Missions");
            Line(builder, 0, "{");
            Line(builder, 1, "using System.Collections.Generic;");
            Line(builder, 0, string.Empty);
            Line(builder, 1, "internal static class MissionAcgCapturedLayoutCatalog");
            Line(builder, 1, "{");
            Line(builder, 2, "internal static MissionAcgLayoutBundle[] CreateBundles()");
            Line(builder, 2, "{");
            Line(builder, 3, "return new[]");
            Line(builder, 3, "{");
            for (int i = 0; i < ordered.Count; i++)
            {
                Line(
                    builder,
                    4,
                    BundleFactoryMethodName(ordered[i].CaptureSession)
                    + "()"
                    + (i + 1 < ordered.Count ? "," : string.Empty));
            }

            Line(builder, 3, "};");
            Line(builder, 2, "}");
            Line(builder, 0, string.Empty);
            for (int i = 0; i < ordered.Count; i++)
            {
                Line(
                    builder,
                    2,
                    "private static MissionAcgLayoutBundle "
                    + BundleFactoryMethodName(ordered[i].CaptureSession)
                    + "()");
                Line(builder, 2, "{");
                Line(builder, 3, "return");
                AppendBundle(builder, ordered[i], 4);
                builder.Append(';');
                builder.Append('\n');
                Line(builder, 2, "}");
                if (i + 1 < ordered.Count)
                {
                    Line(builder, 0, string.Empty);
                }
            }

            Line(builder, 1, "}");
            Line(builder, 0, "}");
            return builder.ToString();
        }

        private static string BundleFactoryMethodName(string captureSession)
        {
            return "CreateCapture" + (captureSession ?? string.Empty).Replace("-", string.Empty);
        }

        private static void AppendBundle(StringBuilder builder, AcgLayoutArtifact artifact, int indent)
        {
            PlayfieldAnarchyFRecord paf = artifact.PlayfieldAnarchyF;
            MissionTypeEmission missionType = ResolveMissionType(artifact.AcceptedMission.MissionType);
            MissionAcgEmissionState state = ResolveState(artifact.CompletenessStatus);
            int? capturedMissionQuality = artifact.AcceptedMission.MissionQuality;
            bool hasCapturedMissionQuality =
                capturedMissionQuality.HasValue && capturedMissionQuality.Value > 0;
            string missionQualityEvidenceField =
                string.IsNullOrWhiteSpace(artifact.AcceptedMission.MissionQualityEvidenceField)
                    ? "none"
                    : artifact.AcceptedMission.MissionQualityEvidenceField;
            int minimumMissionQuality =
                hasCapturedMissionQuality ? capturedMissionQuality.Value : 1;
            int maximumMissionQuality =
                hasCapturedMissionQuality ? capturedMissionQuality.Value : 250;
            string missionQualityNotes =
                hasCapturedMissionQuality
                    ? "Captured mission QL "
                      + capturedMissionQuality.Value.ToString(CultureInfo.InvariantCulture)
                      + " from "
                      + missionQualityEvidenceField
                      + "."
                    : "Captured mission QL is unresolved; evidence field="
                      + missionQualityEvidenceField
                      + ". Compatibility QL 1..250 is an explicit Rebirth layout-selection "
                      + "policy for this captured mission family, not a captured fact.";
            Line(builder, indent, "new MissionAcgLayoutBundle(");
            Line(builder, indent + 1, "MissionAcgLayoutBundle.CurrentFormatVersion,");
            Line(builder, indent + 1, Quote(artifact.BundleId) + ",");
            Line(builder, indent + 1, HexInt(paf.CapturedPf2) + ",");
            AppendIdentity(builder, paf.PayloadBuilding, indent + 1, true);
            AppendByteArray(builder, paf.GeneratorPayload, indent + 1, true);
            Line(builder, indent + 1, Quote(paf.GeneratorPayloadSha256) + ",");
            AppendPoint(builder, artifact.InteriorSpawn, indent + 1, true);
            AppendExit(builder, artifact, indent + 1, true);
            AppendDynels(builder, artifact, indent + 1, true);
            AppendNpcs(builder, artifact, indent + 1, true);
            AppendObjectives(builder, artifact, missionType.EnumName, indent + 1, true);
            AppendCaptureCounts(builder, artifact, indent + 1, true);
            AppendIdentity(builder, artifact.Teleport.PlayerIdentity, indent + 1, true);
            Line(builder, indent + 1, "new MissionAcgCompatibilityRecord(");
            Line(
                builder,
                indent + 2,
                minimumMissionQuality.ToString(CultureInfo.InvariantCulture) + ",");
            Line(
                builder,
                indent + 2,
                maximumMissionQuality.ToString(CultureInfo.InvariantCulture) + ",");
            Line(
                builder,
                indent + 2,
                "new[] { MissionRollType." + missionType.EnumName + " }),");
            Line(builder, indent + 1, "new[]");
            Line(builder, indent + 1, "{");
            AppendProvenance(
                builder,
                artifact.CaptureSession,
                artifact.SourceFile,
                "PlayfieldAnarchyF generator payload and bundle provenance. "
                + missionQualityNotes,
                paf.Provenance,
                indent + 2);
            builder.Append('\n');
            builder.Append(',');
            builder.Append('\n');
            AppendProvenance(
                builder,
                artifact.CaptureSession,
                artifact.SourceFile,
                "Accepted mission type/icon/QL provenance. " + missionQualityNotes,
                artifact.AcceptedMission.Provenance,
                indent + 2);
            builder.Append('\n');
            Line(builder, indent + 1, "},");
            Line(builder, indent + 1, "new MissionAcgCompletenessRecord(");
            Line(
                builder,
                indent + 2,
                "MissionAcgLayoutCompletenessState." + state.EnumName + ",");
            Line(builder, indent + 2, Bool(paf.GeneratorPayload != null && paf.GeneratorPayload.Length > 0) + ",");
            Line(builder, indent + 2, Bool(paf.PayloadBuilding != null) + ",");
            Line(builder, indent + 2, Bool(artifact.InteriorSpawn != null) + ",");
            Line(builder, indent + 2, Bool(artifact.Exit != null) + ",");
            Line(builder, indent + 2, Bool(artifact.LayoutSlots.Doors.Count > 0) + ",");
            Line(builder, indent + 2, Bool(artifact.LayoutSlots.Chests.Count > 0) + ",");
            Line(builder, indent + 2, Bool(artifact.LayoutSlots.Npcs.Count > 0) + ",");
            Line(builder, indent + 2, Bool(artifact.LayoutSlots.Objectives.Count > 0) + ",");
            Line(builder, indent + 2, Bool(state.IsLifecycleCorrelated) + "),");
            Line(builder, indent + 1, Bool(artifact.Selectable) + ",");
            Line(
                builder,
                indent + 1,
                Quote(BuildExclusionReason(artifact)) + ")");
        }

        private static void AppendCaptureCounts(
            StringBuilder builder,
            AcgLayoutArtifact artifact,
            int indent,
            bool trailingComma)
        {
            Line(builder, indent, "new MissionAcgCaptureCountsRecord(");
            Line(
                builder,
                indent + 1,
                artifact.Doors.Count.ToString(CultureInfo.InvariantCulture) + ",");
            Line(
                builder,
                indent + 1,
                artifact.Chests.Count.ToString(CultureInfo.InvariantCulture) + ",");
            Line(
                builder,
                indent + 1,
                artifact.Terminals.Count.ToString(CultureInfo.InvariantCulture) + ",");
            Line(
                builder,
                indent + 1,
                artifact.SimpleCharObservations.Count.ToString(CultureInfo.InvariantCulture) + ",");
            Line(
                builder,
                indent + 1,
                artifact.NpcSlots.Count.ToString(CultureInfo.InvariantCulture) + ",");
            Line(
                builder,
                indent + 1,
                artifact.ObjectiveSlots.Count.ToString(CultureInfo.InvariantCulture) + ",");
            Line(
                builder,
                indent + 1,
                artifact.LayoutSlots.Doors.Count.ToString(CultureInfo.InvariantCulture) + ",");
            Line(
                builder,
                indent + 1,
                artifact.LayoutSlots.Chests.Count.ToString(CultureInfo.InvariantCulture) + ",");
            Line(
                builder,
                indent + 1,
                artifact.LayoutSlots.Terminals.Count.ToString(CultureInfo.InvariantCulture) + ",");
            Line(
                builder,
                indent + 1,
                artifact.LayoutSlots.Npcs.Count.ToString(CultureInfo.InvariantCulture) + ",");
            Line(
                builder,
                indent + 1,
                artifact.LayoutSlots.Objectives.Count.ToString(CultureInfo.InvariantCulture)
                + ")"
                + (trailingComma ? "," : string.Empty));
        }

        private static void AppendDynels(
            StringBuilder builder,
            AcgLayoutArtifact artifact,
            int indent,
            bool trailingComma)
        {
            var dynels = new List<LayoutDynelRecord>();
            dynels.AddRange(artifact.LayoutSlots.Doors);
            dynels.AddRange(artifact.LayoutSlots.Chests);
            dynels.AddRange(artifact.LayoutSlots.Terminals);
            dynels.Sort(CompareDynels);
            Line(builder, indent, "new MissionAcgDynelRecord[]");
            Line(builder, indent, "{");
            for (int i = 0; i < dynels.Count; i++)
            {
                LayoutDynelRecord dynel = dynels[i];
                Line(builder, indent + 1, "new MissionAcgDynelRecord(");
                Line(
                    builder,
                    indent + 2,
                    "MissionAcgWireCategory." + WireCategory(dynel.Category) + ",");
                Line(builder, indent + 2, dynel.Slot.ToString(CultureInfo.InvariantCulture) + ",");
                AppendIdentity(builder, dynel.CapturedIdentity, indent + 2, true);
                Line(builder, indent + 2, NullableInt(dynel.CapturedPf2) + ",");
                AppendOptionalIdentity(builder, dynel.ParentIdentity, indent + 2, true);
                AppendPoint(builder, dynel.Position, indent + 2, true);
                AppendRotation(builder, dynel.Heading, indent + 2, true);
                Line(builder, indent + 2, dynel.Template.ToString(CultureInfo.InvariantCulture) + ",");
                Line(builder, indent + 2, Quote(dynel.Name) + ",");
                Line(
                    builder,
                    indent + 2,
                    Quote(dynel.Provenance == null ? string.Empty : dynel.Provenance.RawPacketHex)
                    + ",");
                AppendRetargetSlots(builder, artifact, dynel, indent + 2, true);
                AppendProvenanceArray(
                    builder,
                    artifact,
                    dynel,
                    "Captured " + dynel.Category + " slot " + dynel.Slot + ".",
                    indent + 2,
                    false);
                Line(builder, indent + 1, ")" + (i + 1 < dynels.Count ? "," : string.Empty));
            }

            Line(builder, indent, "}" + (trailingComma ? "," : string.Empty));
        }

        private static void AppendNpcs(
            StringBuilder builder,
            AcgLayoutArtifact artifact,
            int indent,
            bool trailingComma)
        {
            List<LayoutDynelRecord> npcs = artifact.LayoutSlots.Npcs;
            Line(builder, indent, "new MissionAcgNpcSlotRecord[]");
            Line(builder, indent, "{");
            for (int i = 0; i < npcs.Count; i++)
            {
                LayoutDynelRecord npc = npcs[i];
                Line(builder, indent + 1, "new MissionAcgNpcSlotRecord(");
                Line(builder, indent + 2, npc.Slot.ToString(CultureInfo.InvariantCulture) + ",");
                AppendIdentity(builder, npc.CapturedIdentity, indent + 2, true);
                Line(builder, indent + 2, NullableInt(npc.CapturedPf2) + ",");
                AppendOptionalIdentity(builder, npc.ParentIdentity, indent + 2, true);
                AppendPoint(builder, npc.Position, indent + 2, true);
                AppendRotation(builder, npc.Heading, indent + 2, true);
                Line(builder, indent + 2, npc.Template.ToString(CultureInfo.InvariantCulture) + ",");
                SimpleCharDecodedFields fields = RequireSimpleCharFields(artifact, npc);
                Line(
                    builder,
                    indent + 2,
                    "unchecked((int)"
                    + UnsignedHex(fields.MonsterData)
                    + "),");
                Line(builder, indent + 2, fields.Level.ToString(CultureInfo.InvariantCulture) + ",");
                Line(builder, indent + 2, fields.Health.ToString(CultureInfo.InvariantCulture) + ",");
                Line(
                    builder,
                    indent + 2,
                    fields.HealthDamage.ToString(CultureInfo.InvariantCulture) + ",");
                Line(
                    builder,
                    indent + 2,
                    fields.MonsterScale.ToString(CultureInfo.InvariantCulture) + ",");
                Line(
                    builder,
                    indent + 2,
                    (fields.HeadMesh.HasValue
                         ? fields.HeadMesh.Value.ToString(CultureInfo.InvariantCulture)
                         : "null")
                    + ",");
                Line(builder, indent + 2, Quote(npc.Name) + ",");
                Line(builder, indent + 2, Quote(npc.RetargetingCategory) + ",");
                AppendNpcTextures(builder, fields, indent + 2, true);
                AppendNpcMeshes(builder, fields, indent + 2, true);
                Line(
                    builder,
                    indent + 2,
                    Quote(npc.Provenance == null ? string.Empty : npc.Provenance.RawPacketHex)
                    + ",");
                AppendProvenanceArray(
                    builder,
                    artifact,
                    npc,
                    "Captured NPC slot " + npc.Slot + ".",
                    indent + 2,
                    false);
                Line(builder, indent + 1, ")" + (i + 1 < npcs.Count ? "," : string.Empty));
            }

            Line(builder, indent, "}" + (trailingComma ? "," : string.Empty));
        }

        private static void AppendObjectives(
            StringBuilder builder,
            AcgLayoutArtifact artifact,
            string missionTypeEnum,
            int indent,
            bool trailingComma)
        {
            List<LayoutDynelRecord> objectives = artifact.LayoutSlots.Objectives;
            Line(builder, indent, "new MissionAcgObjectiveSlotRecord[]");
            Line(builder, indent, "{");
            for (int i = 0; i < objectives.Count; i++)
            {
                LayoutDynelRecord objective = objectives[i];
                Line(builder, indent + 1, "new MissionAcgObjectiveSlotRecord(");
                Line(
                    builder,
                    indent + 2,
                    objective.Slot.ToString(CultureInfo.InvariantCulture) + ",");
                Line(
                    builder,
                    indent + 2,
                    "new[] { MissionRollType." + missionTypeEnum + " },");
                AppendIdentity(builder, objective.CapturedIdentity, indent + 2, true);
                Line(builder, indent + 2, NullableInt(objective.CapturedPf2) + ",");
                AppendOptionalIdentity(builder, objective.ParentIdentity, indent + 2, true);
                AppendPoint(builder, objective.Position, indent + 2, true);
                AppendRotation(builder, objective.Heading, indent + 2, true);
                Line(
                    builder,
                    indent + 2,
                    objective.Template.ToString(CultureInfo.InvariantCulture) + ",");
                Line(builder, indent + 2, Quote(objective.Name) + ",");
                Line(
                    builder,
                    indent + 2,
                    Quote(
                        objective.Provenance == null
                            ? string.Empty
                            : objective.Provenance.RawPacketHex)
                    + ",");
                AppendProvenanceArray(
                    builder,
                    artifact,
                    objective,
                    "Captured objective slot " + objective.Slot + ".",
                    indent + 2,
                    false);
                Line(
                    builder,
                    indent + 1,
                    ")" + (i + 1 < objectives.Count ? "," : string.Empty));
            }

            Line(builder, indent, "}" + (trailingComma ? "," : string.Empty));
        }

        private static void AppendExit(
            StringBuilder builder,
            AcgLayoutArtifact artifact,
            int indent,
            bool trailingComma)
        {
            LayoutDynelRecord exit = artifact.Exit;
            if (exit == null)
            {
                Line(builder, indent, "null" + (trailingComma ? "," : string.Empty));
                return;
            }

            Line(builder, indent, "new MissionAcgExitRecord(");
            AppendIdentity(builder, exit.CapturedIdentity, indent + 1, true);
            Line(builder, indent + 1, NullableInt(exit.CapturedPf2) + ",");
            AppendOptionalIdentity(builder, exit.ParentIdentity, indent + 1, true);
            AppendPoint(builder, exit.Position, indent + 1, true);
            AppendRotation(builder, exit.Heading, indent + 1, true);
            Line(builder, indent + 1, exit.Template.ToString(CultureInfo.InvariantCulture) + ",");
            Line(builder, indent + 1, Quote(exit.Name) + ",");
            Line(
                builder,
                indent + 1,
                Quote(exit.Provenance == null ? string.Empty : exit.Provenance.RawPacketHex) + ",");
            AppendProvenanceArray(
                builder,
                artifact,
                exit,
                "Lifecycle-correlated captured exit.",
                indent + 1,
                false);
            Line(builder, indent, ")" + (trailingComma ? "," : string.Empty));
        }

        private static void AppendRetargetSlots(
            StringBuilder builder,
            AcgLayoutArtifact artifact,
            LayoutDynelRecord record,
            int indent,
            bool trailingComma)
        {
            List<RetargetEmission> slots = BuildRetargetSlots(artifact, record);
            if (slots.Count == 0)
            {
                Line(
                    builder,
                    indent,
                    "new MissionAcgRetargetSlotRecord[0]"
                    + (trailingComma ? "," : string.Empty));
                return;
            }

            Line(builder, indent, "new MissionAcgRetargetSlotRecord[]");
            Line(builder, indent, "{");
            for (int i = 0; i < slots.Count; i++)
            {
                RetargetEmission slot = slots[i];
                Line(
                    builder,
                    indent + 1,
                    "new MissionAcgRetargetSlotRecord("
                    + "MissionAcgRetargetCategory."
                    + slot.Category
                    + ", "
                    + slot.Slot.ToString(CultureInfo.InvariantCulture)
                    + ", "
                    + slot.ByteOffset.ToString(CultureInfo.InvariantCulture)
                    + ", "
                    + HexInt(slot.CapturedValue)
                    + ")"
                    + (i + 1 < slots.Count ? "," : string.Empty));
            }

            Line(builder, indent, "}" + (trailingComma ? "," : string.Empty));
        }

        private static List<RetargetEmission> BuildRetargetSlots(
            AcgLayoutArtifact artifact,
            LayoutDynelRecord record)
        {
            if (record == null
                || record.Provenance == null
                || string.IsNullOrWhiteSpace(record.Provenance.RawPacketHex))
            {
                throw new InvalidOperationException(
                    artifact.CaptureSession + " has a catalog dynel without preserved raw bytes.");
            }

            byte[] packet = ParseHex(record.Provenance.RawPacketHex);
            var slots = new List<RetargetEmission>();
            var usedOffsets = new HashSet<int>();

            if (!IsZeroIdentity(record.CapturedIdentity))
            {
                AddFixedRetarget(
                    slots,
                    usedOffsets,
                    packet,
                    "DynelIdentityType",
                    20,
                    record.CapturedIdentity.Type,
                    artifact,
                    record);
                AddFixedRetarget(
                    slots,
                    usedOffsets,
                    packet,
                    "DynelIdentityInstance",
                    24,
                    record.CapturedIdentity.Instance,
                    artifact,
                    record);
            }

            if (!IsZeroIdentity(record.ParentIdentity))
            {
                AddFixedRetarget(
                    slots,
                    usedOffsets,
                    packet,
                    "ParentIdentityType",
                    33,
                    record.ParentIdentity.Type,
                    artifact,
                    record);
                AddFixedRetarget(
                    slots,
                    usedOffsets,
                    packet,
                    "ParentIdentityInstance",
                    37,
                    record.ParentIdentity.Instance,
                    artifact,
                    record);
            }

            if (record.CapturedPf2 != 0)
            {
                AddFixedRetarget(
                    slots,
                    usedOffsets,
                    packet,
                    "Playfield2Instance",
                    69,
                    record.CapturedPf2,
                    artifact,
                    record);
            }

            IdentityValue playerIdentity =
                artifact.Teleport == null ? null : artifact.Teleport.PlayerIdentity;
            if (!IsZeroIdentity(playerIdentity)
                && packet.Length >= 16
                && ReadInt32BigEndian(packet, 12) == playerIdentity.Instance)
            {
                AddFixedRetarget(
                    slots,
                    usedOffsets,
                    packet,
                    "CharacterInstance",
                    12,
                    playerIdentity.Instance,
                    artifact,
                    record);
            }

            slots.Sort(
                delegate(RetargetEmission left, RetargetEmission right)
                {
                    return left.ByteOffset.CompareTo(right.ByteOffset);
                });
            return slots;
        }

        private static void AddFixedRetarget(
            ICollection<RetargetEmission> slots,
            ISet<int> usedOffsets,
            byte[] packet,
            string category,
            int byteOffset,
            int capturedValue,
            AcgLayoutArtifact artifact,
            LayoutDynelRecord record)
        {
            if (byteOffset < 0
                || byteOffset + 4 > packet.Length
                || ReadInt32BigEndian(packet, byteOffset) != capturedValue
                || !usedOffsets.Add(byteOffset))
            {
                throw new InvalidOperationException(
                    artifact.CaptureSession
                    + " has an invalid "
                    + category
                    + " mapping for "
                    + record.Category
                    + ":"
                    + record.Slot.ToString(CultureInfo.InvariantCulture)
                    + ".");
            }

            slots.Add(
                new RetargetEmission(
                    category,
                    NextRetargetSlot(slots, category),
                    byteOffset,
                    capturedValue));
        }

        private static int NextRetargetSlot(
            IEnumerable<RetargetEmission> slots,
            string category)
        {
            int count = 0;
            foreach (RetargetEmission slot in slots)
            {
                if (string.Equals(slot.Category, category, StringComparison.Ordinal))
                {
                    count++;
                }
            }

            return count;
        }

        private static void AppendNpcTextures(
            StringBuilder builder,
            SimpleCharDecodedFields fields,
            int indent,
            bool trailingComma)
        {
            IList<SimpleCharTextureRecord> textures =
                fields.Textures == null
                    ? (IList<SimpleCharTextureRecord>)new SimpleCharTextureRecord[0]
                    : fields.Textures;
            Line(builder, indent, "new MissionAcgNpcTextureRecord[]");
            Line(builder, indent, "{");
            for (int i = 0; i < textures.Count; i++)
            {
                SimpleCharTextureRecord texture = textures[i];
                Line(
                    builder,
                    indent + 1,
                    "new MissionAcgNpcTextureRecord("
                    + texture.Place.ToString(CultureInfo.InvariantCulture)
                    + ", "
                    + texture.Id.ToString(CultureInfo.InvariantCulture)
                    + ", "
                    + texture.Unknown.ToString(CultureInfo.InvariantCulture)
                    + ")"
                    + (i + 1 < textures.Count ? "," : string.Empty));
            }

            Line(builder, indent, "}" + (trailingComma ? "," : string.Empty));
        }

        private static void AppendNpcMeshes(
            StringBuilder builder,
            SimpleCharDecodedFields fields,
            int indent,
            bool trailingComma)
        {
            IList<SimpleCharMeshRecord> meshes =
                fields.Meshes == null
                    ? (IList<SimpleCharMeshRecord>)new SimpleCharMeshRecord[0]
                    : fields.Meshes;
            Line(builder, indent, "new MissionAcgNpcMeshRecord[]");
            Line(builder, indent, "{");
            for (int i = 0; i < meshes.Count; i++)
            {
                SimpleCharMeshRecord mesh = meshes[i];
                Line(
                    builder,
                    indent + 1,
                    "new MissionAcgNpcMeshRecord("
                    + mesh.Position.ToString(CultureInfo.InvariantCulture)
                    + ", unchecked((int)"
                    + UnsignedHex(mesh.Id)
                    + "), "
                    + mesh.OverrideTextureId.ToString(CultureInfo.InvariantCulture)
                    + ", "
                    + mesh.Layer.ToString(CultureInfo.InvariantCulture)
                    + ")"
                    + (i + 1 < meshes.Count ? "," : string.Empty));
            }

            Line(builder, indent, "}" + (trailingComma ? "," : string.Empty));
        }

        private static SimpleCharDecodedFields RequireSimpleCharFields(
            AcgLayoutArtifact artifact,
            LayoutDynelRecord npc)
        {
            if (npc.SimpleCharFields == null
                || npc.SimpleCharFields.Textures == null
                || npc.SimpleCharFields.Meshes == null)
            {
                throw new InvalidOperationException(
                    artifact.CaptureSession
                    + " NPC slot "
                    + npc.Slot.ToString(CultureInfo.InvariantCulture)
                    + " lacks complete decoded SimpleCharFullUpdate field collections.");
            }

            return npc.SimpleCharFields;
        }

        private static void AppendProvenanceArray(
            StringBuilder builder,
            AcgLayoutArtifact artifact,
            LayoutDynelRecord record,
            string notes,
            int indent,
            bool trailingComma)
        {
            Line(builder, indent, "new[]");
            Line(builder, indent, "{");
            bool hasCorrelation = record.CorrelationProvenance != null;
            bool hasInteraction = record.InteractionProvenance != null;
            AppendProvenance(
                builder,
                artifact.CaptureSession,
                artifact.SourceFile,
                notes
                + " Retargeting category: "
                + (record.RetargetingCategory ?? string.Empty)
                + ". "
                + BuildEvidenceNotes(record),
                record.Provenance,
                indent + 1,
                hasCorrelation || hasInteraction);
            if (hasCorrelation)
            {
                AppendProvenance(
                    builder,
                    artifact.CaptureSession,
                    artifact.SourceFile,
                    "Correlated lifecycle/objective provenance. " + BuildEvidenceNotes(record),
                    record.CorrelationProvenance,
                    indent + 1,
                    hasInteraction);
            }

            if (hasInteraction)
            {
                AppendProvenance(
                    builder,
                    artifact.CaptureSession,
                    artifact.SourceFile,
                    "Correlated interaction provenance. " + BuildEvidenceNotes(record),
                    record.InteractionProvenance,
                    indent + 1,
                    false);
            }

            Line(builder, indent, "}" + (trailingComma ? "," : string.Empty));
        }

        private static string BuildEvidenceNotes(LayoutDynelRecord record)
        {
            var parts = new List<string>();
            if (!string.IsNullOrWhiteSpace(record.EvidenceKind))
            {
                parts.Add("evidenceKind=" + record.EvidenceKind);
            }

            if (!string.IsNullOrWhiteSpace(record.EvidenceField))
            {
                parts.Add("evidenceField=" + record.EvidenceField);
            }

            if (!IsZeroIdentity(record.EvidenceIdentity))
            {
                parts.Add(
                    "evidenceIdentity="
                    + ((uint)record.EvidenceIdentity.Type).ToString(
                        "X8",
                        CultureInfo.InvariantCulture)
                    + ":"
                    + ((uint)record.EvidenceIdentity.Instance).ToString(
                        "X8",
                        CultureInfo.InvariantCulture));
            }

            if (record.DistanceFromInteriorSpawn.HasValue)
            {
                if (float.IsNaN(record.DistanceFromInteriorSpawn.Value)
                    || float.IsInfinity(record.DistanceFromInteriorSpawn.Value))
                {
                    throw new InvalidOperationException(
                        "Generated ACG source contains a non-finite objective/exit distance.");
                }

                parts.Add(
                    "distanceFromInteriorSpawn="
                    + record.DistanceFromInteriorSpawn.Value.ToString(
                        "R",
                        CultureInfo.InvariantCulture));
            }

            return parts.Count == 0
                       ? "No additional structured correlation fields."
                       : string.Join("; ", parts.ToArray()) + ".";
        }

        private static void AppendProvenance(
            StringBuilder builder,
            string captureSession,
            string source,
            string notes,
            PacketProvenance provenance,
            int indent)
        {
            AppendProvenance(
                builder,
                captureSession,
                source,
                notes,
                provenance,
                indent,
                false);
        }

        private static void AppendProvenance(
            StringBuilder builder,
            string captureSession,
            string source,
            string notes,
            PacketProvenance provenance,
            int indent,
            bool trailingComma)
        {
            if (provenance == null)
            {
                Line(
                    builder,
                    indent,
                    "new MissionAcgProvenanceRecord("
                    + Quote(captureSession)
                    + ", "
                    + Quote(source)
                    + ", "
                    + Quote(notes)
                    + ")"
                    + (trailingComma ? "," : string.Empty));
                return;
            }

            Line(builder, indent, "new MissionAcgProvenanceRecord(");
            Line(builder, indent + 1, Quote(captureSession) + ",");
            Line(builder, indent + 1, Quote(source) + ",");
            Line(builder, indent + 1, Quote(notes) + ",");
            Line(builder, indent + 1, provenance.CsvLine.ToString(CultureInfo.InvariantCulture) + ",");
            Line(builder, indent + 1, provenance.GlobalOrdinal.ToString(CultureInfo.InvariantCulture) + "L,");
            Line(builder, indent + 1, provenance.Sequence.ToString(CultureInfo.InvariantCulture) + ",");
            Line(builder, indent + 1, Quote(provenance.Direction) + ",");
            Line(builder, indent + 1, Quote(provenance.CapturedUtc) + ",");
            Line(builder, indent + 1, Quote(provenance.MessageType) + ",");
            Line(builder, indent + 1, Quote(provenance.PreservationStatus) + ",");
            Line(builder, indent + 1, provenance.RawPacketLength.ToString(CultureInfo.InvariantCulture) + ",");
            Line(builder, indent + 1, Quote(provenance.RawPacketSha256) + ",");
            Line(
                builder,
                indent + 1,
                Quote(provenance.ParseStatus) + ")" + (trailingComma ? "," : string.Empty));
        }

        private static void AppendIdentity(
            StringBuilder builder,
            IdentityValue identity,
            int indent,
            bool trailingComma)
        {
            string suffix = trailingComma ? "," : string.Empty;
            if (identity == null)
            {
                Line(builder, indent, "null" + suffix);
                return;
            }

            Line(
                builder,
                indent,
                "new MissionAcgIdentityRecord("
                + HexInt(identity.Type)
                + ", "
                + HexInt(identity.Instance)
                + ")"
                + suffix);
        }

        private static void AppendOptionalIdentity(
            StringBuilder builder,
            IdentityValue identity,
            int indent,
            bool trailingComma)
        {
            AppendIdentity(
                builder,
                IsZeroIdentity(identity) ? null : identity,
                indent,
                trailingComma);
        }

        private static bool IsZeroIdentity(IdentityValue identity)
        {
            return identity == null || (identity.Type == 0 && identity.Instance == 0);
        }

        private static void AppendPoint(
            StringBuilder builder,
            Vector3Value point,
            int indent,
            bool trailingComma)
        {
            string suffix = trailingComma ? "," : string.Empty;
            if (point == null)
            {
                Line(builder, indent, "null" + suffix);
                return;
            }

            Line(
                builder,
                indent,
                "new MissionAcgPointRecord("
                + Float(point.X)
                + ", "
                + Float(point.Y)
                + ", "
                + Float(point.Z)
                + ")"
                + suffix);
        }

        private static void AppendRotation(
            StringBuilder builder,
            QuaternionValue rotation,
            int indent,
            bool trailingComma)
        {
            string suffix = trailingComma ? "," : string.Empty;
            if (rotation == null)
            {
                Line(builder, indent, "null" + suffix);
                return;
            }

            Line(
                builder,
                indent,
                "new MissionAcgRotationRecord("
                + Float(rotation.X)
                + ", "
                + Float(rotation.Y)
                + ", "
                + Float(rotation.Z)
                + ", "
                + Float(rotation.W)
                + ")"
                + suffix);
        }

        private static void AppendByteArray(
            StringBuilder builder,
            byte[] bytes,
            int indent,
            bool trailingComma)
        {
            if (bytes == null)
            {
                Line(builder, indent, "null" + (trailingComma ? "," : string.Empty));
                return;
            }

            Line(builder, indent, "new byte[]");
            Line(builder, indent, "{");
            for (int offset = 0; offset < bytes.Length; offset += 16)
            {
                int count = Math.Min(16, bytes.Length - offset);
                var line = new StringBuilder();
                for (int i = 0; i < count; i++)
                {
                    if (i > 0)
                    {
                        line.Append(", ");
                    }

                    line.Append("0x");
                    line.Append(bytes[offset + i].ToString("X2", CultureInfo.InvariantCulture));
                }

                if (offset + count < bytes.Length)
                {
                    line.Append(',');
                }

                Line(builder, indent + 1, line.ToString());
            }

            Line(builder, indent, "}" + (trailingComma ? "," : string.Empty));
        }

        private static void Validate(
            CaptureExpectation expectation,
            AcgLayoutArtifact artifact)
        {
            if (artifact == null)
            {
                throw Mismatch(expectation, "extractor returned null");
            }

            if (!string.Equals(artifact.Schema, ExpectedSchema, StringComparison.Ordinal)
                || artifact.SchemaVersion != 1)
            {
                throw Mismatch(expectation, "schema/version");
            }

            if (!string.Equals(
                artifact.CaptureSession,
                expectation.Session,
                StringComparison.Ordinal))
            {
                throw Mismatch(expectation, "capture session");
            }

            if (artifact.AcceptedMission == null
                || !string.Equals(
                    artifact.AcceptedMission.MissionType,
                    expectation.MissionType,
                    StringComparison.Ordinal)
                || artifact.AcceptedMission.MissionIcon != expectation.MissionIcon
                || artifact.AcceptedMission.MissionQuality.HasValue)
            {
                throw Mismatch(expectation, "accepted mission type/icon/unresolved QL");
            }

            if (IsZeroIdentity(artifact.AcceptedMission.AcceptedQfuIdentity)
                || IsZeroIdentity(artifact.AcceptedMission.AcceptedQfuBuilding)
                || IsZeroIdentity(artifact.AcceptedMission.MissionKeyIdentity)
                || artifact.AcceptedMission.ExteriorEntrance == null
                || IsZeroIdentity(
                    artifact.AcceptedMission.ExteriorEntrance.MissionBuildingReference)
                || IsZeroIdentity(artifact.AcceptedMission.ExteriorEntrance.ExteriorPlayfield)
                || !IsFinite(artifact.AcceptedMission.ExteriorEntrance.Position)
                || artifact.AcceptedMission.Provenance == null)
            {
                throw Mismatch(expectation, "accepted QFU/key/exterior entrance evidence");
            }

            if (artifact.Teleport == null
                || IsZeroIdentity(artifact.Teleport.PlayerIdentity)
                || IsZeroIdentity(artifact.Teleport.Building)
                || artifact.Teleport.CapturedPf2 != expectation.Playfield2
                || artifact.Teleport.Building.Instance != expectation.BuildingInstance
                || artifact.Teleport.Provenance == null
                || !IsFinite(artifact.InteriorSpawn))
            {
                throw Mismatch(expectation, "entry teleport/interior spawn");
            }

            PlayfieldAnarchyFRecord paf = artifact.PlayfieldAnarchyF;
            if (paf == null
                || paf.PayloadBuilding == null
                || paf.PayloadBuilding.Type != 0x0000C79F
                || paf.PayloadBuilding.Instance != expectation.BuildingInstance
                || paf.CapturedPf2 != expectation.Playfield2
                || paf.GeneratorPayload == null
                || !string.Equals(
                    paf.GeneratorPayloadSha256,
                    expectation.GeneratorPayloadSha256,
                    StringComparison.OrdinalIgnoreCase)
                || !string.Equals(
                    AcgHash.Sha256(paf.GeneratorPayload),
                    expectation.GeneratorPayloadSha256,
                    StringComparison.OrdinalIgnoreCase))
            {
                throw Mismatch(expectation, "building/PF2/generator payload tuple");
            }

            if (artifact.AcceptedMission.AcceptedQfuBuilding.Type != paf.PayloadBuilding.Type
                || artifact.AcceptedMission.AcceptedQfuBuilding.Instance
                != paf.PayloadBuilding.Instance
                || artifact.AcceptedMission.ExteriorEntrance.MissionBuildingReference.Type
                != paf.PayloadBuilding.Type
                || artifact.AcceptedMission.ExteriorEntrance.MissionBuildingReference.Instance
                != paf.PayloadBuilding.Instance
                || artifact.Teleport.Building.Type != paf.PayloadBuilding.Type
                || artifact.Teleport.Building.Instance != paf.PayloadBuilding.Instance)
            {
                throw Mismatch(expectation, "QFU/entrance/teleport/PAF building coherence");
            }

            CaptureCountExpectation counts = expectation.Counts;
            if (artifact.Doors == null
                || artifact.Chests == null
                || artifact.Terminals == null
                || artifact.NpcSlots == null
                || artifact.SimpleCharObservations == null
                || artifact.ObjectiveSlots == null
                || artifact.Doors.Count != counts.DoorObservations
                || artifact.Chests.Count != counts.ChestObservations
                || artifact.Terminals.Count != counts.TerminalObservations
                || artifact.SimpleCharObservations.Count != counts.SimpleCharObservations
                || artifact.NpcSlots.Count != counts.NpcObservations
                || artifact.ObjectiveSlots.Count != counts.ObjectiveObservations)
            {
                throw Mismatch(expectation, "physical observation counts");
            }

            if (artifact.LayoutSlots == null
                || artifact.LayoutSlots.Doors == null
                || artifact.LayoutSlots.Chests == null
                || artifact.LayoutSlots.Terminals == null
                || artifact.LayoutSlots.Npcs == null
                || artifact.LayoutSlots.Objectives == null
                || artifact.LayoutSlots.Doors.Count != counts.DoorSlots
                || artifact.LayoutSlots.Chests.Count != counts.ChestSlots
                || artifact.LayoutSlots.Terminals.Count != counts.TerminalSlots
                || artifact.LayoutSlots.Npcs.Count != counts.NpcSlots
                || artifact.LayoutSlots.Objectives.Count != counts.ObjectiveSlots)
            {
                throw Mismatch(expectation, "deduplicated catalog slots");
            }

            ValidateCatalogRecord(
                expectation,
                artifact.LayoutSlots.Objectives[0],
                expectation.Objective,
                "objective");
            ValidateCatalogRecord(
                expectation,
                artifact.Exit,
                expectation.Exit,
                "exit");
            ValidateNormalizedPlayfields(expectation, artifact);

            for (int i = 0; i < artifact.LayoutSlots.Npcs.Count; i++)
            {
                SimpleCharDecodedFields fields =
                    artifact.LayoutSlots.Npcs[i].SimpleCharFields;
                if (fields == null
                    || !fields.DecodeFullyConsumed
                    || fields.Textures == null
                    || fields.Meshes == null)
                {
                    throw Mismatch(expectation, "fully decoded NPC slots");
                }
            }

            if (artifact.Issues != null
                && artifact.Issues.Any(
                    issue => issue != null
                             && string.Equals(
                                 issue.Severity,
                                 "error",
                                 StringComparison.OrdinalIgnoreCase)))
            {
                throw Mismatch(expectation, "error-severity extraction issue");
            }

            if (!string.Equals(
                    artifact.CompletenessStatus,
                    expectation.CompletenessStatus,
                    StringComparison.Ordinal)
                || artifact.Selectable != expectation.Selectable)
            {
                throw Mismatch(expectation, "completeness/selectability");
            }

            string expectedBundleId = string.Format(
                CultureInfo.InvariantCulture,
                "capture-{0}-{1:X8}-{2:X8}-{3}",
                expectation.Session,
                expectation.BuildingInstance,
                expectation.Playfield2,
                expectation.GeneratorPayloadSha256.Substring(0, 16));
            if (!string.Equals(artifact.BundleId, expectedBundleId, StringComparison.Ordinal))
            {
                throw Mismatch(expectation, "bundle id");
            }
        }

        private static void ValidateCatalogRecord(
            CaptureExpectation expectation,
            LayoutDynelRecord record,
            CapturedRecordExpectation expected,
            string label)
        {
            if (record == null
                || expected == null
                || record.CapturedIdentity == null
                || record.CapturedIdentity.Type != expected.IdentityType
                || record.CapturedIdentity.Instance != expected.IdentityInstance
                || record.CapturedPf2 != expectation.Playfield2
                || record.Template != expected.Template
                || !string.Equals(
                    record.EvidenceKind,
                    expected.EvidenceKind,
                    StringComparison.Ordinal)
                || !string.Equals(
                    record.EvidenceField,
                    expected.EvidenceField,
                    StringComparison.Ordinal)
                || record.EvidenceIdentity == null
                || record.EvidenceIdentity.Type != expected.IdentityType
                || record.EvidenceIdentity.Instance != expected.IdentityInstance
                || !IsFinite(record.Position)
                || (record.Heading != null && !IsFinite(record.Heading))
                || record.Provenance == null
                || record.CorrelationProvenance == null
                || string.IsNullOrWhiteSpace(record.Provenance.RawPacketHex)
                || !string.Equals(
                    record.Provenance.RawPacketSha256,
                    expected.RawPacketSha256,
                    StringComparison.OrdinalIgnoreCase)
                || !string.Equals(
                    AcgHash.Sha256(ParseHex(record.Provenance.RawPacketHex)),
                    expected.RawPacketSha256,
                    StringComparison.OrdinalIgnoreCase))
            {
                throw Mismatch(expectation, label + " identity/correlation/raw evidence");
            }
        }

        private static void ValidateNormalizedPlayfields(
            CaptureExpectation expectation,
            AcgLayoutArtifact artifact)
        {
            ValidateNormalizedRecords(
                expectation,
                artifact.LayoutSlots.Doors,
                "door");
            ValidateNormalizedRecords(
                expectation,
                artifact.LayoutSlots.Chests,
                "chest");
            ValidateNormalizedRecords(
                expectation,
                artifact.LayoutSlots.Terminals,
                "terminal");
            ValidateNormalizedRecords(
                expectation,
                artifact.LayoutSlots.Npcs,
                "NPC");
            ValidateNormalizedRecords(
                expectation,
                artifact.LayoutSlots.Objectives,
                "objective");
        }

        private static void ValidateNormalizedRecords(
            CaptureExpectation expectation,
            IEnumerable<LayoutDynelRecord> records,
            string label)
        {
            foreach (LayoutDynelRecord record in records)
            {
                if (record == null
                    || IsZeroIdentity(record.CapturedIdentity)
                    || record.CapturedPf2 != expectation.Playfield2
                    || !IsFinite(record.Position)
                    || (record.Heading != null && !IsFinite(record.Heading))
                    || record.Provenance == null
                    || string.IsNullOrWhiteSpace(record.Provenance.RawPacketHex)
                    || !string.Equals(
                        AcgHash.Sha256(ParseHex(record.Provenance.RawPacketHex)),
                        record.Provenance.RawPacketSha256,
                        StringComparison.OrdinalIgnoreCase))
                {
                    throw Mismatch(expectation, label + " normalized record evidence");
                }
            }
        }

        private static bool IsFinite(Vector3Value point)
        {
            return point != null
                   && IsFinite(point.X)
                   && IsFinite(point.Y)
                   && IsFinite(point.Z);
        }

        private static bool IsFinite(QuaternionValue rotation)
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

        private static InvalidOperationException Mismatch(
            CaptureExpectation expectation,
            string field)
        {
            return new InvalidOperationException(
                "Finalized ACG capture manifest mismatch for "
                + expectation.Session
                + ": "
                + field
                + ".");
        }

        private static int CompareDynels(LayoutDynelRecord left, LayoutDynelRecord right)
        {
            int category = string.Compare(left.Category, right.Category, StringComparison.Ordinal);
            return category != 0 ? category : left.Slot.CompareTo(right.Slot);
        }

        private static int FindStat(LayoutDynelRecord record, string statName)
        {
            if (record == null || record.Stats == null)
            {
                return 0;
            }

            for (int i = 0; i < record.Stats.Count; i++)
            {
                StatValueRecord stat = record.Stats[i];
                if (stat != null
                    && string.Equals(stat.Name, statName, StringComparison.OrdinalIgnoreCase))
                {
                    return stat.Value;
                }
            }

            return 0;
        }

        private static MissionTypeEmission ResolveMissionType(string missionType)
        {
            switch (missionType)
            {
                case "kill":
                    return new MissionTypeEmission("KillPerson");
                case "find_person":
                    return new MissionTypeEmission("FindPerson");
                case "find_item":
                    return new MissionTypeEmission("FindItem");
                case "return_item":
                    return new MissionTypeEmission("FindItemReturn");
                case "repair":
                    return new MissionTypeEmission("RepairMachine");
                default:
                    throw new InvalidOperationException(
                        "Unsupported generated ACG mission type: " + missionType + ".");
            }
        }

        private static MissionAcgEmissionState ResolveState(string completeness)
        {
            switch (completeness)
            {
                case "complete_and_selectable":
                    return new MissionAcgEmissionState("CompleteSelectable", true);
                case "structurally_complete_but_objective_incomplete":
                    return new MissionAcgEmissionState(
                        "StructurallyCompleteObjectiveIncomplete",
                        false);
                case "incomplete_and_non_selectable":
                    return new MissionAcgEmissionState("IncompleteNonSelectable", false);
                case "conflicting_and_rejected":
                    return new MissionAcgEmissionState("ConflictingRejected", false);
                default:
                    throw new InvalidOperationException(
                        "Unsupported generated ACG completeness state: " + completeness + ".");
            }
        }

        private static string WireCategory(string category)
        {
            switch ((category ?? string.Empty).ToLowerInvariant())
            {
                case "door":
                    return "Door";
                case "chest":
                    return "Chest";
                case "terminal":
                    return "Terminal";
                default:
                    throw new InvalidOperationException(
                        "Unsupported generated ACG wire category: " + category + ".");
            }
        }

        private static string BuildExclusionReason(AcgLayoutArtifact artifact)
        {
            if (artifact.Selectable)
            {
                return string.Empty;
            }

            var codes = new SortedSet<string>(StringComparer.Ordinal);
            if (artifact.Issues != null)
            {
                foreach (ExtractionIssue issue in artifact.Issues)
                {
                    if (issue != null && !string.IsNullOrWhiteSpace(issue.Code))
                    {
                        codes.Add(issue.Code);
                    }
                }
            }

            return "Generated capture artifact is "
                   + artifact.CompletenessStatus
                   + (codes.Count == 0 ? "." : "; issues=" + string.Join(",", codes) + ".");
        }

        private static string NullableInt(int value)
        {
            return value == 0 ? "null" : HexInt(value);
        }

        private static string HexInt(int value)
        {
            return "unchecked((int)0x" + ((uint)value).ToString("X8", CultureInfo.InvariantCulture) + ")";
        }

        private static string UnsignedHex(uint value)
        {
            return "0x" + value.ToString("X8", CultureInfo.InvariantCulture);
        }

        private static byte[] ParseHex(string value)
        {
            string normalized = (value ?? string.Empty).Trim();
            if (normalized.Length == 0 || (normalized.Length & 1) != 0)
            {
                throw new InvalidOperationException(
                    "Generated ACG catalog contains invalid preserved packet hex.");
            }

            var bytes = new byte[normalized.Length / 2];
            for (int i = 0; i < bytes.Length; i++)
            {
                byte parsed;
                if (!byte.TryParse(
                        normalized.Substring(i * 2, 2),
                        NumberStyles.HexNumber,
                        CultureInfo.InvariantCulture,
                        out parsed))
                {
                    throw new InvalidOperationException(
                        "Generated ACG catalog contains invalid preserved packet hex.");
                }

                bytes[i] = parsed;
            }

            return bytes;
        }

        private static int ReadInt32BigEndian(byte[] bytes, int offset)
        {
            return (bytes[offset] << 24)
                   | (bytes[offset + 1] << 16)
                   | (bytes[offset + 2] << 8)
                   | bytes[offset + 3];
        }

        private static string Float(float value)
        {
            if (float.IsNaN(value) || float.IsInfinity(value))
            {
                throw new InvalidOperationException("Generated ACG source contains a non-finite float.");
            }

            return value.ToString("R", CultureInfo.InvariantCulture) + "f";
        }

        private static string Bool(bool value)
        {
            return value ? "true" : "false";
        }

        private static string Quote(string value)
        {
            if (value == null)
            {
                return "string.Empty";
            }

            var builder = new StringBuilder(value.Length + 2);
            builder.Append('"');
            foreach (char character in value)
            {
                switch (character)
                {
                    case '\\':
                        builder.Append("\\\\");
                        break;
                    case '"':
                        builder.Append("\\\"");
                        break;
                    case '\r':
                        builder.Append("\\r");
                        break;
                    case '\n':
                        builder.Append("\\n");
                        break;
                    case '\t':
                        builder.Append("\\t");
                        break;
                    default:
                        if (char.IsControl(character))
                        {
                            builder.Append("\\u");
                            builder.Append(((int)character).ToString("X4", CultureInfo.InvariantCulture));
                        }
                        else
                        {
                            builder.Append(character);
                        }

                        break;
                }
            }

            builder.Append('"');
            return builder.ToString();
        }

        private static void Line(StringBuilder builder, int indent, string text)
        {
            builder.Append(' ', indent * 4);
            builder.Append(text);
            builder.Append('\n');
        }

        private sealed class CaptureExpectation
        {
            internal CaptureExpectation(
                string session,
                string missionType,
                int missionIcon,
                int buildingInstance,
                int playfield2,
                CaptureCountExpectation counts,
                string generatorPayloadSha256,
                CapturedRecordExpectation objective,
                CapturedRecordExpectation exit,
                string completenessStatus,
                bool selectable)
            {
                this.Session = session;
                this.MissionType = missionType;
                this.MissionIcon = missionIcon;
                this.BuildingInstance = buildingInstance;
                this.Playfield2 = playfield2;
                this.Counts = counts;
                this.GeneratorPayloadSha256 = generatorPayloadSha256;
                this.Objective = objective;
                this.Exit = exit;
                this.CompletenessStatus = completenessStatus;
                this.Selectable = selectable;
            }

            internal string Session { get; private set; }
            internal string MissionType { get; private set; }
            internal int MissionIcon { get; private set; }
            internal int BuildingInstance { get; private set; }
            internal int Playfield2 { get; private set; }
            internal CaptureCountExpectation Counts { get; private set; }
            internal string GeneratorPayloadSha256 { get; private set; }
            internal CapturedRecordExpectation Objective { get; private set; }
            internal CapturedRecordExpectation Exit { get; private set; }
            internal string CompletenessStatus { get; private set; }
            internal bool Selectable { get; private set; }
        }

        private sealed class CaptureCountExpectation
        {
            internal CaptureCountExpectation(
                int doorObservations,
                int chestObservations,
                int terminalObservations,
                int simpleCharObservations,
                int npcObservations,
                int objectiveObservations,
                int doorSlots,
                int chestSlots,
                int terminalSlots,
                int npcSlots,
                int objectiveSlots)
            {
                this.DoorObservations = doorObservations;
                this.ChestObservations = chestObservations;
                this.TerminalObservations = terminalObservations;
                this.SimpleCharObservations = simpleCharObservations;
                this.NpcObservations = npcObservations;
                this.ObjectiveObservations = objectiveObservations;
                this.DoorSlots = doorSlots;
                this.ChestSlots = chestSlots;
                this.TerminalSlots = terminalSlots;
                this.NpcSlots = npcSlots;
                this.ObjectiveSlots = objectiveSlots;
            }

            internal int DoorObservations { get; private set; }
            internal int ChestObservations { get; private set; }
            internal int TerminalObservations { get; private set; }
            internal int SimpleCharObservations { get; private set; }
            internal int NpcObservations { get; private set; }
            internal int ObjectiveObservations { get; private set; }
            internal int DoorSlots { get; private set; }
            internal int ChestSlots { get; private set; }
            internal int TerminalSlots { get; private set; }
            internal int NpcSlots { get; private set; }
            internal int ObjectiveSlots { get; private set; }
        }

        private sealed class CapturedRecordExpectation
        {
            internal CapturedRecordExpectation(
                int identityType,
                int identityInstance,
                int template,
                string evidenceKind,
                string evidenceField,
                string rawPacketSha256)
            {
                this.IdentityType = identityType;
                this.IdentityInstance = identityInstance;
                this.Template = template;
                this.EvidenceKind = evidenceKind;
                this.EvidenceField = evidenceField;
                this.RawPacketSha256 = rawPacketSha256;
            }

            internal int IdentityType { get; private set; }
            internal int IdentityInstance { get; private set; }
            internal int Template { get; private set; }
            internal string EvidenceKind { get; private set; }
            internal string EvidenceField { get; private set; }
            internal string RawPacketSha256 { get; private set; }
        }

        private sealed class MissionTypeEmission
        {
            internal MissionTypeEmission(string enumName)
            {
                this.EnumName = enumName;
            }

            internal string EnumName { get; private set; }
        }

        private sealed class MissionAcgEmissionState
        {
            internal MissionAcgEmissionState(string enumName, bool isLifecycleCorrelated)
            {
                this.EnumName = enumName;
                this.IsLifecycleCorrelated = isLifecycleCorrelated;
            }

            internal string EnumName { get; private set; }
            internal bool IsLifecycleCorrelated { get; private set; }
        }

        private sealed class RetargetEmission
        {
            internal RetargetEmission(
                string category,
                int slot,
                int byteOffset,
                int capturedValue)
            {
                this.Category = category;
                this.Slot = slot;
                this.ByteOffset = byteOffset;
                this.CapturedValue = capturedValue;
            }

            internal string Category { get; private set; }
            internal int Slot { get; private set; }
            internal int ByteOffset { get; private set; }
            internal int CapturedValue { get; private set; }
        }
    }
}
