namespace AOSharpMissionCaptureAnalyzer
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.IO;
    using System.Linq;
    using System.Security.Cryptography;
    using System.Text;

    using AORebirth.CaptureProtocol;

    using SmokeLounge.AOtomation.Messaging.GameData;
    using SmokeLounge.AOtomation.Messaging.Messages;
    using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;
    using SmokeLounge.AOtomation.Messaging.Serialization;

    internal static class AcgLayoutExtractor
    {
        internal const string ArtifactFileName = "mission-acg-layout.json";
        internal const string CorpusFileName = "mission-acg-layout-corpus.json";

        private const int N3BodyOffset = 16;
        private const int PlayfieldAnarchyFType = unchecked((int)0x5F4B1A39);
        private const int N3TeleportType = unchecked((int)0x43197D22);
        private const int AcgBuildingType = 0x0000C79F;
        private const int MissionKeyType = 0x0000C76D;
        private const int IncompleteShape = 1441804;
        private const int SimpleCharIdentityType = 0x0000C350;
        private const int ExitBoundaryUnknown6 = 2;
        private const int ExitBoundaryUnknown7 = unchecked((int)0xFFFF0000);

        private static readonly IDictionary<int, string> MissionTypesByIcon =
            new SortedDictionary<int, string>
                {
                    { 11329, "return_item" },
                    { 11330, "kill" },
                    { 11335, "find_person" },
                    { 11337, "find_item" },
                    { 11342, "repair" }
                };

        internal static int AnalyzeAndWrite(string captureFolder)
        {
            if (!Directory.Exists(captureFolder)
                || !File.Exists(Path.Combine(captureFolder, "raw-packets.csv")))
            {
                Console.Error.WriteLine("ACG extraction FAIL: raw-packets.csv was not found in " + captureFolder);
                return 2;
            }

            try
            {
                AcgLayoutArtifact artifact = Extract(captureFolder);
                string outputPath = Path.Combine(captureFolder, ArtifactFileName);
                WriteCanonical(outputPath, artifact);
                Console.WriteLine(
                    string.Format(
                        CultureInfo.InvariantCulture,
                        "{0}: ACG extraction status={1} selectable={2} doors={3} chests={4} terminals={5} npcs={6} output={7}",
                        artifact.CaptureSession,
                        artifact.CompletenessStatus,
                        artifact.Selectable,
                        artifact.Doors.Count,
                        artifact.Chests.Count,
                        artifact.Terminals.Count,
                        artifact.NpcSlots.Count,
                        outputPath));
                return artifact.CompletenessStatus == "conflicting_and_rejected" ? 1 : 0;
            }
            catch (Exception exception)
            {
                Console.Error.WriteLine(
                    "ACG extraction FAIL: " + exception.GetType().Name + ": " + exception.Message);
                return 1;
            }
        }

        internal static int AnalyzeCorpus(string capturesRoot, string outputPath)
        {
            if (!Directory.Exists(capturesRoot))
            {
                Console.Error.WriteLine("ACG corpus FAIL: directory was not found: " + capturesRoot);
                return 2;
            }

            string[] rawPacketFiles = Directory.GetFiles(
                capturesRoot,
                "raw-packets.csv",
                SearchOption.AllDirectories);
            Array.Sort(rawPacketFiles, StringComparer.OrdinalIgnoreCase);

            var artifacts = new List<AcgLayoutArtifact>();
            var corpusFailures = new List<ExtractionIssue>();
            foreach (string rawPacketFile in rawPacketFiles)
            {
                string captureFolder = Path.GetDirectoryName(rawPacketFile);
                try
                {
                    AcgLayoutArtifact artifact = Extract(captureFolder);
                    WriteCanonical(Path.Combine(captureFolder, ArtifactFileName), artifact);
                    artifacts.Add(artifact);
                }
                catch (Exception exception)
                {
                    corpusFailures.Add(
                        new ExtractionIssue
                            {
                                Code = "capture_extraction_failed",
                                Severity = "error",
                                Message = Path.GetFileName(captureFolder)
                                          + ": "
                                          + exception.GetType().Name
                                          + ": "
                                          + exception.Message
                            });
                }
            }

            artifacts.Sort(
                delegate(AcgLayoutArtifact left, AcgLayoutArtifact right)
                    {
                        return StringComparer.Ordinal.Compare(left.CaptureSession, right.CaptureSession);
                    });
            corpusFailures.Sort(
                delegate(ExtractionIssue left, ExtractionIssue right)
                    {
                        return StringComparer.Ordinal.Compare(left.Message, right.Message);
                    });

            string finalOutputPath = outputPath ?? Path.Combine(capturesRoot, CorpusFileName);
            WriteCanonicalCorpus(finalOutputPath, artifacts, corpusFailures);
            Console.WriteLine(
                string.Format(
                    CultureInfo.InvariantCulture,
                    "ACG corpus analyzed={0} extractionFailures={1} selectable={2} output={3}",
                    artifacts.Count,
                    corpusFailures.Count,
                    artifacts.Count(artifact => artifact.Selectable),
                    finalOutputPath));
            return corpusFailures.Count == 0 ? 0 : 1;
        }

        internal static AcgLayoutArtifact Extract(string captureFolder)
        {
            string sourcePath = Path.Combine(captureFolder, "raw-packets.csv");
            string captureSession = Path.GetFileName(
                captureFolder.TrimEnd(
                    Path.DirectorySeparatorChar,
                    Path.AltDirectorySeparatorChar));
            var issues = new List<ExtractionIssue>();
            List<CapturePacketRow> rows = LoadRows(sourcePath, issues);
            foreach (CapturePacketRow row in rows)
            {
                row.CaptureSession = captureSession;
            }

            rows.Sort(CompareRows);

            var pafCandidates = new List<PlayfieldAnarchyFRecord>();
            foreach (CapturePacketRow row in rows)
            {
                if (!string.Equals(row.N3TypeName, "PlayfieldAnarchyF", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                try
                {
                    byte[] packet = DecodePacket(row);
                    PlayfieldAnarchyFRecord record = ParsePlayfieldAnarchyF(row, packet);
                    if (record.Building != null && record.Building.Type == AcgBuildingType)
                    {
                        pafCandidates.Add(record);
                    }
                }
                catch (Exception exception)
                {
                    issues.Add(PacketIssue(row, "paf_parse_failed", exception.Message));
                }
            }

            PlayfieldAnarchyFRecord paf = SelectGeneratorPaf(pafCandidates, issues);
            long windowStart = paf == null ? long.MaxValue : paf.Provenance.GlobalOrdinal;
            long windowEnd = long.MaxValue;
            TeleportRecord teleport = null;
            TeleportRecord exitTeleport = null;
            if (paf != null)
            {
                var teleports = new List<TeleportRecord>();
                foreach (CapturePacketRow row in rows)
                {
                    if (!string.Equals(row.N3TypeName, "N3Teleport", StringComparison.OrdinalIgnoreCase)
                        && !string.Equals(row.N3TypeName, "Teleport", StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    try
                    {
                        TeleportRecord candidate = ParseTeleport(row, DecodePacket(row));
                        teleports.Add(candidate);
                    }
                    catch (Exception exception)
                    {
                        issues.Add(PacketIssue(row, "teleport_parse_failed", exception.Message));
                    }
                }

                teleport = teleports
                    .Where(candidate => candidate.Provenance.GlobalOrdinal < windowStart
                                        && candidate.Building != null
                                        && candidate.Building.Type == AcgBuildingType)
                    .OrderByDescending(candidate => candidate.Provenance.GlobalOrdinal)
                    .FirstOrDefault();
                exitTeleport = teleports
                    .Where(candidate => candidate.Provenance.GlobalOrdinal > windowStart)
                    .OrderBy(candidate => candidate.Provenance.GlobalOrdinal)
                    .FirstOrDefault();
                if (exitTeleport != null)
                {
                    windowEnd = exitTeleport.Provenance.GlobalOrdinal;
                }
            }

            AcceptedMissionRecord acceptedMission = paf == null
                                                        ? null
                                                        : ExtractAcceptedMission(
                                                            rows,
                                                            paf,
                                                            issues);
            List<PacketProvenance> lifecycleEvidence = ExtractLifecycleEvidence(
                rows,
                windowStart,
                windowEnd,
                issues);

            IdentityValue playerIdentity = teleport == null ? null : teleport.PlayerIdentity;
            var doors = new List<LayoutDynelRecord>();
            var chests = new List<LayoutDynelRecord>();
            var terminals = new List<LayoutDynelRecord>();
            var npcSlots = new List<LayoutDynelRecord>();
            var simpleCharObservations = new List<LayoutDynelRecord>();
            var objectiveSlots = new List<LayoutDynelRecord>();
            var charInPlay = new List<LayoutDynelRecord>();

            foreach (CapturePacketRow row in rows)
            {
                if (row.GlobalOrdinal <= windowStart || row.GlobalOrdinal >= windowEnd)
                {
                    continue;
                }

                try
                {
                    byte[] packet = DecodePacket(row);
                    if (string.Equals(row.N3TypeName, "DoorFullUpdate", StringComparison.OrdinalIgnoreCase))
                    {
                        AddIfTargetPlayfield(
                            doors,
                            ParseDoorFullUpdate(row, packet),
                            paf,
                            issues);
                    }
                    else if (string.Equals(row.N3TypeName, "ChestFullUpdate", StringComparison.OrdinalIgnoreCase)
                             || string.Equals(row.N3TypeName, "ChestItemFullUpdate", StringComparison.OrdinalIgnoreCase))
                    {
                        LayoutDynelRecord chest = TryParseEnvelopeDynel(
                            row,
                            packet,
                            "chest",
                            paf.CapturedPf2);
                        if (chest != null)
                        {
                            if (chest.CapturedPf2 != paf.CapturedPf2)
                            {
                                AddError(
                                    issues,
                                    "chest_off_pf_critical_record",
                                    "A positional ChestFullUpdate inside the layout window uses an unexpected PF2.");
                                continue;
                            }

                            PopulatePositionedItemFields(
                                packet,
                                chest,
                                row.N3TypeName);
                        }

                        if (chest == null && PacketContainsInt32(packet, paf.CapturedPf2))
                        {
                            chest = ParseIdentityOnly(row, packet, "chest");
                            chest.LayoutEligibility =
                                "non_layout_identity_only_inventory_or_lifecycle_observation";
                            issues.Add(
                                PacketIssue(
                                    row,
                                    "chest_non_layout_identity_only_observation",
                                    "Identity-only chest evidence was preserved as an observation but excluded from physical layout slots."));
                        }

                        if (chest != null)
                        {
                            if (string.IsNullOrEmpty(chest.LayoutEligibility))
                            {
                                chest.LayoutEligibility = "physical_layout_slot";
                            }

                            chests.Add(chest);
                        }
                    }
                    else if (string.Equals(row.N3TypeName, "VendingMachineFullUpdate", StringComparison.OrdinalIgnoreCase))
                    {
                        LayoutDynelRecord terminal =
                            ParseVendingMachineFullUpdate(row, packet);
                        if (terminal.Position == null)
                        {
                            terminals.Add(terminal);
                            issues.Add(
                                PacketIssue(
                                    row,
                                    "terminal_non_layout_owned_observation",
                                    "A VendingMachineFullUpdate without physical placement was preserved as a non-layout observation."));
                        }
                        else
                        {
                            AddIfTargetPlayfield(
                                terminals,
                                terminal,
                                paf,
                                issues);
                        }
                    }
                    else if (string.Equals(row.N3TypeName, "SimpleCharFullUpdate", StringComparison.OrdinalIgnoreCase))
                    {
                        RawSimpleCharFullUpdate decoded;
                        string decodeError;
                        if (!RawSimpleCharFullUpdateDecoder.TryDecodePacket(packet, out decoded, out decodeError))
                        {
                            throw new InvalidDataException(decodeError);
                        }

                        bool isPlayer = playerIdentity != null
                                        && decoded.Identity.Type == playerIdentity.Type
                                        && decoded.Identity.Instance == playerIdentity.Instance;
                        bool matchingPf = decoded.PlayfieldId.HasValue
                                          && paf != null
                                          && decoded.PlayfieldId.Value == paf.CapturedPf2;
                        var observation = new LayoutDynelRecord
                                              {
                                                  Category = isPlayer ? "player" : "npc",
                                                  CapturedIdentity =
                                                      new IdentityValue(
                                                          decoded.Identity.Type,
                                                          decoded.Identity.Instance),
                                                  CapturedPf2 =
                                                      decoded.PlayfieldId.GetValueOrDefault(),
                                                  CapturedPf2Known =
                                                      decoded.PlayfieldId.HasValue,
                                                  Position =
                                                      new Vector3Value(
                                                          decoded.Position.X,
                                                          decoded.Position.Y,
                                                          decoded.Position.Z),
                                                  Heading =
                                                      new QuaternionValue(
                                                          decoded.Heading.X,
                                                          decoded.Heading.Y,
                                                          decoded.Heading.Z,
                                                          decoded.Heading.W),
                                                  Name = decoded.Name ?? string.Empty,
                                                  Template =
                                                      unchecked((int)decoded.MonsterData),
                                                  LayoutEligibility =
                                                      matchingPf
                                                          ? isPlayer
                                                                ? "non_layout_player_observation"
                                                                : "physical_layout_slot"
                                                          : decoded.PlayfieldId.HasValue
                                                                ? "non_layout_off_pf_critical_record"
                                                                : "non_layout_pf_unknown_critical_record",
                                                  ParentIdentity =
                                                      decoded.Owner.HasValue
                                                          ? new IdentityValue(
                                                              decoded.Owner.Value.Type,
                                                              decoded.Owner.Value.Instance)
                                                          : null,
                                                  SimpleCharFields =
                                                      BuildSimpleCharFields(decoded),
                                                  RetargetingCategory = "npc_slot",
                                                  Provenance =
                                                      PacketProvenance.From(
                                                          row,
                                                          packet,
                                                          "decoded")
                                              };
                        simpleCharObservations.Add(observation);
                        if (matchingPf)
                        {
                            if (!isPlayer && decoded.Npc != null)
                            {
                                npcSlots.Add(observation.CloneAs("npc", "npc_slot"));
                            }
                        }
                        else if (decoded.PlayfieldId.HasValue && paf != null)
                        {
                            ExtractionIssue issue = PacketIssue(
                                row,
                                "simple_char_off_pf_critical_record",
                                "A SimpleCharFullUpdate inside the layout window uses an unexpected PF2.");
                            issue.Severity = "error";
                            issues.Add(issue);
                        }
                        else
                        {
                            ExtractionIssue issue = PacketIssue(
                                row,
                                "simple_char_pf_missing_critical_record",
                                "A SimpleCharFullUpdate inside the layout window has no decoded PF2.");
                            issue.Severity = "error";
                            issues.Add(issue);
                        }
                    }
                    else if (string.Equals(row.N3TypeName, "CharInPlay", StringComparison.OrdinalIgnoreCase))
                    {
                        LayoutDynelRecord record = ParseCharInPlay(row, packet);
                        charInPlay.Add(record);
                    }
                    else if (IsObjectiveMessageFamily(row.N3TypeName))
                    {
                        LayoutDynelRecord record = ParseObjectiveDynel(
                            row,
                            packet,
                            paf.CapturedPf2);
                        if (!record.CapturedPf2Known)
                        {
                            objectiveSlots.Add(record);
                        }
                        else if (record.CapturedPf2 != paf.CapturedPf2)
                        {
                            objectiveSlots.Add(record);
                            ExtractionIssue issue = PacketIssue(
                                row,
                                "objective_off_pf_critical_record",
                                row.N3TypeName
                                + " inside the layout window uses an unexpected PF2.");
                            issue.Severity = "error";
                            issues.Add(issue);
                        }
                        else
                        {
                            objectiveSlots.Add(record);
                        }
                    }
                }
                catch (Exception exception)
                {
                    ExtractionIssue issue =
                        PacketIssue(row, "record_parse_failed", exception.Message);
                    if (IsCriticalLayoutMessageFamily(row.N3TypeName))
                    {
                        issue.Code = "critical_record_parse_failed";
                        issue.Severity = "error";
                    }

                    issues.Add(issue);
                }
            }

            AddNpcObjectiveSlot(
                acceptedMission,
                npcSlots,
                objectiveSlots,
                issues);
            CorrelateItemObjectiveSlots(
                acceptedMission,
                objectiveSlots,
                issues);
            SortAndNumber(doors, "door");
            SortAndNumber(chests, "chest");
            SortAndNumber(terminals, "terminal");
            SortAndNumber(npcSlots, "npc");
            SortAndNumber(simpleCharObservations, "simple_char_observation");
            SortAndNumber(objectiveSlots, "objective");
            SortAndNumber(charInPlay, "char_in_play");

            var layoutSlots = new AcgLayoutSlots
                                  {
                                      Doors = NormalizeSlots(doors, true, issues),
                                      Chests = NormalizeSlots(
                                          chests.Where(record => record.Position != null),
                                          true,
                                          issues),
                                      Terminals = NormalizeSlots(
                                          terminals.Where(
                                              record => record.Position != null
                                                        && string.Equals(
                                                            record.LayoutEligibility,
                                                            "physical_layout_slot",
                                                            StringComparison.Ordinal)),
                                          true,
                                          issues),
                                      Npcs = NormalizeSlots(npcSlots, false, issues),
                                      Objectives = NormalizeSlots(
                                          objectiveSlots.Where(
                                              record => record.Position != null
                                                        && string.Equals(
                                                            record.LayoutEligibility,
                                                            "physical_layout_slot",
                                                            StringComparison.Ordinal)
                                                        && !string.IsNullOrEmpty(
                                                            record.EvidenceKind)),
                                          true,
                                          issues)
                                  };
            ValidateCrossCategoryIdentityReuse(layoutSlots, issues);

            LayoutDynelRecord exit = FindExit(
                rows,
                windowStart,
                windowEnd,
                layoutSlots.Doors,
                paf == null ? null : paf.CharacterCoordinates,
                paf == null ? null : paf.Provenance,
                issues);
            Vector3Value interiorSpawn = paf == null ? null : paf.CharacterCoordinates;
            ValidateSelectableEvidenceIntegrity(
                captureSession,
                paf,
                teleport,
                acceptedMission,
                exit,
                exitTeleport,
                layoutSlots,
                issues);
            Validate(
                paf,
                teleport,
                acceptedMission,
                interiorSpawn,
                exit,
                exitTeleport,
                doors,
                chests,
                terminals,
                npcSlots,
                issues);

            bool conflicting = issues.Any(issue => issue.Severity == "error");
            bool structurallyComplete = HasGeneratorPayload(paf)
                                        && teleport != null
                                        && IdentityEquals(teleport.Building, paf.Building)
                                        && teleport.CapturedPf2 == paf.CapturedPf2
                                        && interiorSpawn != null
                                        && exit != null
                                        && HasAcceptedMissionStructure(acceptedMission, paf)
                                        && layoutSlots.Doors.Count > 0
                                        && layoutSlots.Chests.Count > 0
                                        && layoutSlots.Npcs.Count > 0;
            bool objectiveIncomplete = acceptedMission == null
                                       || acceptedMission.MissionType == "unknown"
                                       || layoutSlots.Objectives.Count != 1;
            bool knownIncomplete = paf != null && paf.CapturedPf2 == IncompleteShape;
            string completeness;
            bool selectable;
            if (conflicting)
            {
                completeness = "conflicting_and_rejected";
                selectable = false;
            }
            else if (!structurallyComplete || knownIncomplete)
            {
                completeness = "incomplete_and_non_selectable";
                selectable = false;
            }
            else if (objectiveIncomplete)
            {
                completeness = "structurally_complete_but_objective_incomplete";
                selectable = false;
            }
            else
            {
                completeness = "complete_and_selectable";
                selectable = true;
            }

            var mappings = new List<IdentityMappingRecord>();
            AddMappings(mappings, layoutSlots.Doors);
            AddMappings(mappings, layoutSlots.Chests);
            AddMappings(mappings, layoutSlots.Terminals);
            AddMappings(mappings, layoutSlots.Npcs);
            AddMappings(mappings, layoutSlots.Objectives);
            mappings.Sort(
                delegate(IdentityMappingRecord left, IdentityMappingRecord right)
                    {
                        int category = StringComparer.Ordinal.Compare(left.Category, right.Category);
                        return category != 0 ? category : left.Slot.CompareTo(right.Slot);
                    });

            string bundleId = BuildBundleId(captureSession, paf);
            ApplyIssueContext(issues, captureSession, "raw-packets.csv");
            issues.Sort(CompareIssues);

            return new AcgLayoutArtifact
                       {
                           Schema = "ao-rebirth.mission-acg-layout",
                           SchemaVersion = 1,
                           BundleId = bundleId,
                           CaptureSession = captureSession,
                           SourceFile = "raw-packets.csv",
                           AcceptedMission = acceptedMission,
                           Teleport = teleport,
                           PlayfieldAnarchyF = paf,
                           InteriorSpawn = interiorSpawn,
                           Exit = exit,
                           ExitTeleport = exitTeleport,
                           LifecycleEvidence = lifecycleEvidence,
                           Doors = doors,
                           Chests = chests,
                           Terminals = terminals,
                           NpcSlots = npcSlots,
                           SimpleCharObservations = simpleCharObservations,
                           ObjectiveSlots = objectiveSlots,
                           CharInPlay = charInPlay,
                           LayoutSlots = layoutSlots,
                           IdentityMappings = mappings,
                           Issues = issues,
                           CompletenessStatus = completeness,
                           Selectable = selectable
                       };
        }

        private static List<PacketProvenance> ExtractLifecycleEvidence(
            IEnumerable<CapturePacketRow> rows,
            long windowStart,
            long windowEnd,
            ICollection<ExtractionIssue> issues)
        {
            var result = new List<PacketProvenance>();
            foreach (CapturePacketRow row in rows.Where(
                         candidate => candidate.GlobalOrdinal > windowStart
                                      && candidate.GlobalOrdinal <= windowEnd
                                      && IsLifecycleEvidenceFamily(candidate.N3TypeName)))
            {
                try
                {
                    byte[] packet = DecodePacket(row);
                    result.Add(
                        PacketProvenance.From(
                            row,
                            packet,
                            "raw_preserved_lifecycle_evidence"));
                }
                catch (Exception exception)
                {
                    issues.Add(
                        PacketIssue(
                            row,
                            "lifecycle_evidence_parse_failed",
                            exception.Message));
                }
            }

            return result;
        }

        private static bool IsLifecycleEvidenceFamily(string n3TypeName)
        {
            return string.Equals(n3TypeName, "GenericCmd", StringComparison.OrdinalIgnoreCase)
                   || string.Equals(
                       n3TypeName,
                       "UseItemOnItem",
                       StringComparison.OrdinalIgnoreCase)
                   || string.Equals(
                       n3TypeName,
                       "DoorStatusUpdate",
                       StringComparison.OrdinalIgnoreCase)
                   || string.Equals(n3TypeName, "Despawn", StringComparison.OrdinalIgnoreCase)
                   || string.Equals(
                       n3TypeName,
                       "CharacterAction",
                       StringComparison.OrdinalIgnoreCase)
                   || string.Equals(n3TypeName, "Quest", StringComparison.OrdinalIgnoreCase)
                   || string.Equals(
                       n3TypeName,
                       "QuestFullUpdate",
                       StringComparison.OrdinalIgnoreCase)
                   || string.Equals(n3TypeName, "N3Teleport", StringComparison.OrdinalIgnoreCase)
                   || string.Equals(n3TypeName, "Teleport", StringComparison.OrdinalIgnoreCase);
        }

        internal static void RunSelfTest()
        {
            byte[] packet = new byte[86];
            WriteInt16(packet, 6, packet.Length);
            WriteInt32(packet, 16, PlayfieldAnarchyFType);
            WriteInt32(packet, 20, 0x9C50);
            WriteInt32(packet, 24, 0x15F008);
            packet[28] = 0;
            WriteInt32(packet, 29, 4);
            WriteSingle(packet, 33, 1.5f);
            WriteSingle(packet, 37, 2.5f);
            WriteSingle(packet, 41, 3.5f);
            packet[45] = 0x61;
            WriteInt32(packet, 46, AcgBuildingType);
            WriteInt32(packet, 50, 0xD734E2);
            WriteInt32(packet, 54, 0);
            WriteInt32(packet, 58, 0);
            WriteInt32(packet, 62, 0x9C50);
            WriteInt32(packet, 66, 0x15F008);
            WriteInt32(packet, 70, AcgBuildingType);
            WriteInt32(packet, 74, 0xD734E2);
            WriteInt32(packet, 78, 2);
            WriteInt32(packet, 82, -1);

            var row = new CapturePacketRow
                          {
                              CaptureSession = "self-test",
                              CapturedUtc = "2026-07-28T00:00:00.0000000Z",
                              Direction = "IN",
                              GlobalOrdinal = 42,
                              Sequence = 7,
                              N3TypeValue = PlayfieldAnarchyFType,
                              N3TypeName = "PlayfieldAnarchyF",
                              PreservationStatus = "raw_complete",
                              RawHex = ToHex(packet),
                              CsvLineNumber = 43
                          };
            PlayfieldAnarchyFRecord record = ParsePlayfieldAnarchyF(row, packet);
            Require(record.Building.Instance == 0xD734E2, "self-test PAF building");
            Require(record.CapturedPf2 == 0x15F008, "self-test PAF PF2");
            Require(record.GeneratorPayload.Length == 16, "self-test payload length");
            Require(
                string.Equals(record.GeneratorPayloadSha256, Sha256(record.GeneratorPayload), StringComparison.Ordinal),
                "self-test payload hash");

            var artifact = new AcgLayoutArtifact
                               {
                                   Schema = "ao-rebirth.mission-acg-layout",
                                   SchemaVersion = 1,
                                   BundleId = "self-test",
                                   CaptureSession = "self-test",
                                   SourceFile = "raw-packets.csv",
                                   PlayfieldAnarchyF = record,
                                   Doors = new List<LayoutDynelRecord>(),
                                   Chests = new List<LayoutDynelRecord>(),
                                   Terminals = new List<LayoutDynelRecord>(),
                                   NpcSlots = new List<LayoutDynelRecord>(),
                                   SimpleCharObservations = new List<LayoutDynelRecord>(),
                                   ObjectiveSlots = new List<LayoutDynelRecord>(),
                                   CharInPlay = new List<LayoutDynelRecord>(),
                                   LayoutSlots = new AcgLayoutSlots
                                                     {
                                                         Doors = new List<LayoutDynelRecord>(),
                                                         Chests = new List<LayoutDynelRecord>(),
                                                         Terminals = new List<LayoutDynelRecord>(),
                                                         Npcs = new List<LayoutDynelRecord>(),
                                                         Objectives = new List<LayoutDynelRecord>()
                                                     },
                                   IdentityMappings = new List<IdentityMappingRecord>(),
                                   Issues = new List<ExtractionIssue>(),
                                   CompletenessStatus = "incomplete_and_non_selectable",
                                   Selectable = false
                               };
            string first = CanonicalJson.WriteArtifact(artifact);
            string second = CanonicalJson.WriteArtifact(artifact);
            Require(string.Equals(first, second, StringComparison.Ordinal), "self-test canonical determinism");

            var corruptedTeleport = new TeleportRecord
                                        {
                                            Building = new IdentityValue(AcgBuildingType, 0xD734E3),
                                            CapturedPf2 = 0x15F008
                                        };
            var validationIssues = new List<ExtractionIssue>();
            Validate(
                record,
                corruptedTeleport,
                null,
                record.CharacterCoordinates,
                null,
                null,
                new List<LayoutDynelRecord>(),
                new List<LayoutDynelRecord>(),
                new List<LayoutDynelRecord>(),
                new List<LayoutDynelRecord>(),
                validationIssues);
            Require(
                validationIssues.Any(issue => issue.Code == "teleport_paf_building_mismatch"),
                "self-test building mismatch rejection");

            var corruptedPfTeleport = new TeleportRecord
                                          {
                                              Building =
                                                  new IdentityValue(
                                                      AcgBuildingType,
                                                      0xD734E2),
                                              CapturedPf2 =
                                                  record.CapturedPf2 + 1
                                          };
            var pfValidationIssues = new List<ExtractionIssue>();
            Validate(
                record,
                corruptedPfTeleport,
                null,
                record.CharacterCoordinates,
                null,
                null,
                new List<LayoutDynelRecord>(),
                new List<LayoutDynelRecord>(),
                new List<LayoutDynelRecord>(),
                new List<LayoutDynelRecord>(),
                pfValidationIssues);
            Require(
                pfValidationIssues.Any(
                    issue => issue.Code == "teleport_paf_pf2_mismatch"
                             && issue.Severity == "error"),
                "self-test PF2 mismatch rejection");

            var offPfIssues = new List<ExtractionIssue>();
            var offPfRecords = new List<LayoutDynelRecord>();
            AddIfTargetPlayfield(
                offPfRecords,
                new LayoutDynelRecord
                    {
                        Category = "door",
                        CapturedPf2 = record.CapturedPf2 + 1,
                        CapturedPf2Known = true,
                        Provenance =
                            new PacketProvenance
                                {
                                    MessageType = "DoorFullUpdate"
                                }
                    },
                record,
                offPfIssues);
            Require(
                offPfRecords.Count == 0
                && offPfIssues.Any(
                    issue => issue.Code == "door_off_pf_critical_record"
                             && issue.Severity == "error"),
                "self-test off-PF critical record rejection");

            var crossSessionIssues = new List<ExtractionIssue>();
            ValidateRequiredProvenance(
                "self-test",
                "door_slot",
                new PacketProvenance
                    {
                        CaptureSession = "other-capture",
                        PreservationStatus = "raw_complete",
                        MessageType = "DoorFullUpdate"
                    },
                crossSessionIssues);
            Require(
                crossSessionIssues.Any(
                    issue => issue.Code
                             == "required_evidence_session_mismatch"
                             && issue.Severity == "error"),
                "self-test cross-capture evidence rejection");

            var missingProvenanceIssues = new List<ExtractionIssue>();
            ValidateRequiredProvenance(
                "self-test",
                "door_slot",
                null,
                missingProvenanceIssues);
            Require(
                missingProvenanceIssues.Any(
                    issue => issue.Code
                             == "required_evidence_provenance_missing"
                             && issue.Severity == "error"),
                "self-test missing provenance rejection");

            var incompletePreservationIssues = new List<ExtractionIssue>();
            ValidateRequiredProvenance(
                "self-test",
                "door_slot",
                new PacketProvenance
                    {
                        CaptureSession = "self-test",
                        PreservationStatus = "raw_partial",
                        MessageType = "DoorFullUpdate"
                    },
                incompletePreservationIssues);
            Require(
                incompletePreservationIssues.Any(
                    issue => issue.Code
                             == "required_evidence_not_raw_complete"
                             && issue.Severity == "error"),
                "self-test incomplete preservation rejection");

            var missingPayloadPaf = new PlayfieldAnarchyFRecord
                                        {
                                            Building = record.Building,
                                            CapturedPf2 = record.CapturedPf2,
                                            GeneratorPayload = new byte[0]
                                        };
            Require(
                !HasGeneratorPayload(missingPayloadPaf),
                "self-test missing payload is non-selectable");

            var incompleteNpcIssues = new List<ExtractionIssue>();
            ValidateSelectableEvidenceIntegrity(
                "self-test",
                null,
                null,
                null,
                null,
                null,
                new AcgLayoutSlots
                    {
                        Doors = new List<LayoutDynelRecord>(),
                        Chests = new List<LayoutDynelRecord>(),
                        Terminals = new List<LayoutDynelRecord>(),
                        Npcs =
                            new List<LayoutDynelRecord>
                                {
                                    new LayoutDynelRecord
                                        {
                                            Category = "npc",
                                            SimpleCharFields =
                                                new SimpleCharDecodedFields
                                                    {
                                                        DecodeFullyConsumed =
                                                            false,
                                                        UndecodedTailHex = "00"
                                                    },
                                            Provenance =
                                                new PacketProvenance
                                                    {
                                                        CaptureSession =
                                                            "self-test",
                                                        PreservationStatus =
                                                            "raw_complete",
                                                        MessageType =
                                                            "SimpleCharFullUpdate"
                                                    }
                                        }
                                },
                        Objectives = new List<LayoutDynelRecord>()
                    },
                incompleteNpcIssues);
            Require(
                incompleteNpcIssues.Any(
                    issue => issue.Code == "npc_scfu_not_fully_decoded"
                             && issue.Severity == "error"),
                "self-test incomplete selectable NPC rejection");

            bool nonFiniteRejected = false;
            try
            {
                var nonFiniteWriter = new CanonicalJsonWriter();
                nonFiniteWriter.Float(float.NaN);
            }
            catch (InvalidDataException)
            {
                nonFiniteRejected = true;
            }

            Require(nonFiniteRejected, "self-test non-finite JSON rejection");
            RunKnownFiveCaptureRegressionSelfTest();
        }

        private static void RunKnownFiveCaptureRegressionSelfTest()
        {
            string capturesRoot = Path.Combine(
                Environment.CurrentDirectory,
                "tools-temp",
                "AOSharpLiveCapture",
                "bin",
                "Debug",
                "captures");
            var expectations = new[]
                                   {
                                       new KnownCaptureExpectation(
                                           "20260728-001044",
                                           "kill",
                                           23,
                                           22,
                                           16,
                                           11,
                                           8,
                                           7,
                                           0x00D734E2,
                                           1437704,
                                           0x0000C350,
                                           0x79A16B61,
                                           "QuestActions[0].UnknownId2",
                                           "ffe4327ac8af0f0a41a04cff7fe53ecd40c55a027f10a2cda2cd2a8fc18f1269",
                                           false),
                                       new KnownCaptureExpectation(
                                           "20260728-003410",
                                           "return_item",
                                           64,
                                           44,
                                           59,
                                           21,
                                           15,
                                           21,
                                           0x00D6FC77,
                                           1493006,
                                           0x0000C74A,
                                           0x2586CCB1,
                                           "QuestActions[0].Action",
                                           "f7f00e3344bd12f2d7d302761403c9c5b083fc8a181417c7f2c9748da501ff59",
                                           true),
                                       new KnownCaptureExpectation(
                                           "20260728-005042",
                                           "find_item",
                                           59,
                                           27,
                                           40,
                                           27,
                                           13,
                                           18,
                                           0x00D6FC78,
                                           1480706,
                                           0x0000C73D,
                                           0x57AC07B0,
                                           "QuestActions[0].Action",
                                           "3cfe53d3a32b50679530bdfd5ff7572405eb8865f4ab0c13308c7bcd935bf431",
                                           true),
                                       new KnownCaptureExpectation(
                                           "20260728-010220",
                                           "repair",
                                           26,
                                           15,
                                           29,
                                           17,
                                           12,
                                           19,
                                           0x00D734E5,
                                           1437711,
                                           0x0000C73D,
                                           0x57A3C596,
                                           "QuestActions[0].UnknownId1",
                                           "e75f1326a72db6d42ddb5ebd72320338148193e6469e70b1c30b2d8a0f6d1926",
                                           true),
                                       new KnownCaptureExpectation(
                                           "20260728-012547",
                                           "find_person",
                                           56,
                                           39,
                                           46,
                                           14,
                                           11,
                                           14,
                                           0x00D734E7,
                                           1470476,
                                           0x0000C350,
                                           0x79A16EB9,
                                           "QuestActions[0].UnknownId2",
                                           "d5413273f69b018b66fcd6fe31bfa7be15b338cb6cb8fd17d83f7e14c4e4be82",
                                           true)
                                   };

            foreach (KnownCaptureExpectation expectation in expectations)
            {
                string captureFolder = Path.Combine(capturesRoot, expectation.Session);
                Require(
                    File.Exists(Path.Combine(captureFolder, "raw-packets.csv")),
                    "self-test known capture " + expectation.Session + " exists");
                AcgLayoutArtifact artifact = Extract(captureFolder);
                Require(
                    artifact.Doors.Count == expectation.DoorObservations
                    && artifact.Chests.Count == expectation.ChestObservations
                    && artifact.SimpleCharObservations.Count
                    == expectation.SimpleCharObservations,
                    "self-test raw observation tuple " + expectation.Session);
                Require(
                    artifact.LayoutSlots.Doors.Count == expectation.DoorSlots
                    && artifact.LayoutSlots.Chests.Count == expectation.ChestSlots
                    && artifact.LayoutSlots.Npcs.Count == expectation.NpcSlots,
                    "self-test physical slot tuple " + expectation.Session);
                Require(
                    artifact.AcceptedMission != null
                    && artifact.AcceptedMission.MissionType == expectation.MissionType
                    && !artifact.AcceptedMission.MissionQuality.HasValue,
                    "self-test accepted mission evidence " + expectation.Session);
                Require(
                    artifact.LayoutSlots.Objectives.Count == 1
                    && IdentityEquals(
                        artifact.LayoutSlots.Objectives[0].EvidenceIdentity,
                        new IdentityValue(
                            expectation.ObjectiveIdentityType,
                            expectation.ObjectiveIdentityInstance))
                    && string.Equals(
                        artifact.LayoutSlots.Objectives[0].EvidenceField,
                        expectation.ObjectiveEvidenceField,
                        StringComparison.Ordinal),
                    "self-test exact objective correlation " + expectation.Session);
                Require(
                    artifact.Exit != null
                    && artifact.Exit.DoorFields != null
                    && artifact.Exit.DoorFields.Unknown6 == ExitBoundaryUnknown6
                    && artifact.Exit.DoorFields.Unknown7 == ExitBoundaryUnknown7
                    && string.IsNullOrEmpty(
                        artifact.Exit.DoorFields.UndecodedTailHex)
                    && artifact.ExitTeleport != null == expectation.HasExitTeleport,
                    "self-test exit boundary evidence " + expectation.Session);
                Require(
                    artifact.PlayfieldAnarchyF.Building.Type == AcgBuildingType
                    && artifact.PlayfieldAnarchyF.Building.Instance
                    == expectation.BuildingInstance
                    && artifact.PlayfieldAnarchyF.CapturedPf2
                    == expectation.CapturedPf2,
                    "self-test exact building/PF2 " + expectation.Session);
                Require(
                    artifact.PlayfieldAnarchyF.GeneratorPayloadSha256
                    == expectation.PayloadSha256,
                    "self-test payload hash " + expectation.Session);
                Require(
                    artifact.Selectable
                    && artifact.CompletenessStatus == "complete_and_selectable",
                    "self-test selectability " + expectation.Session);
                Require(
                    CanonicalJson.WriteArtifact(artifact)
                    == CanonicalJson.WriteArtifact(Extract(captureFolder)),
                    "self-test canonical byte determinism " + expectation.Session);
            }
        }

        private static AcceptedMissionRecord ExtractAcceptedMission(
            IList<CapturePacketRow> rows,
            PlayfieldAnarchyFRecord paf,
            ICollection<ExtractionIssue> issues)
        {
            byte[] buildingBytes = new byte[8];
            WriteInt32(buildingBytes, 0, paf.Building.Type);
            WriteInt32(buildingBytes, 4, paf.Building.Instance);
            var serializer = new MessageSerializer();

            foreach (CapturePacketRow row in rows
                         .Where(candidate => candidate.GlobalOrdinal < paf.Provenance.GlobalOrdinal
                                             && string.Equals(
                                                 candidate.N3TypeName,
                                                 "QuestFullUpdate",
                                                 StringComparison.OrdinalIgnoreCase))
                         .OrderByDescending(candidate => candidate.GlobalOrdinal))
            {
                byte[] packet;
                try
                {
                    packet = DecodePacket(row);
                }
                catch (Exception exception)
                {
                    issues.Add(PacketIssue(row, "qfu_packet_invalid", exception.Message));
                    continue;
                }

                if (FindBytes(packet, buildingBytes, N3BodyOffset) < 0)
                {
                    continue;
                }

                try
                {
                    Message decodedMessage = serializer.Deserialize(packet);
                    var qfu = decodedMessage == null
                                  ? null
                                  : decodedMessage.Body as QuestFullUpdateMessage;
                    Quest quest = qfu == null || qfu.Quests == null
                                      ? null
                                      : qfu.Quests.FirstOrDefault(
                                          candidate => candidate != null
                                                       && candidate.UnknownId3.Type != 0
                                                       && (int)candidate.UnknownId3.Type == paf.Building.Type
                                                       && candidate.UnknownId3.Instance == paf.Building.Instance);
                    if (quest == null)
                    {
                        continue;
                    }

                    QuestActionInfo action = quest.QuestActions == null
                                                 ? null
                                                 : quest.QuestActions.FirstOrDefault();
                    IdentityValue missionKey = null;
                    if (quest.QuestIdentities != null)
                    {
                        foreach (QuestIdentity questIdentity in quest.QuestIdentities)
                        {
                            if (questIdentity != null
                                && (int)questIdentity.Identity.Type == MissionKeyType)
                            {
                                missionKey =
                                    new IdentityValue(
                                        (int)questIdentity.Identity.Type,
                                        questIdentity.Identity.Instance);
                                break;
                            }
                        }
                    }

                    string missionType;
                    if (!MissionTypesByIcon.TryGetValue(quest.MissionIconId, out missionType))
                    {
                        missionType = "unknown";
                    }

                    List<QuestActionIdentityRecord> actionIdentities =
                        ExtractQuestActionIdentities(quest.QuestActions);
                    return new AcceptedMissionRecord
                               {
                                   MissionType = missionType,
                                   MissionIcon = quest.MissionIconId,
                                   MissionQuality = null,
                                   MissionQualityEvidenceField = string.Empty,
                                   AcceptedQfuIdentity =
                                       new IdentityValue(
                                           (int)quest.QuestId.Type,
                                           quest.QuestId.Instance),
                                   AcceptedQfuBuilding =
                                       new IdentityValue(
                                           (int)quest.UnknownId3.Type,
                                           quest.UnknownId3.Instance),
                                   IssuingTerminal =
                                       new IdentityValue(
                                           (int)quest.UnknownId1.Type,
                                           quest.UnknownId1.Instance),
                                   ExteriorEntrance =
                                       action == null
                                           ? null
                                           : new ExteriorEntranceRecord
                                                 {
                                                     MissionBuildingReference =
                                                         new IdentityValue(
                                                             (int)quest.UnknownId3.Type,
                                                             quest.UnknownId3.Instance),
                                                     ExteriorPlayfield =
                                                         new IdentityValue(
                                                             (int)action.PlayfieldId.Type,
                                                             action.PlayfieldId.Instance),
                                                     Position =
                                                         new Vector3Value(
                                                             action.Position.X,
                                                             action.Position.Y,
                                                             action.Position.Z)
                                                 },
                                   MissionKeyIdentity = missionKey,
                                   Title = quest.ShortInfo ?? string.Empty,
                                   QuestActionIdentities = actionIdentities,
                                   DecodedScalarFields = ExtractQuestScalarFields(quest),
                                   Provenance = PacketProvenance.From(row, packet, "decoded")
                               };
                }
                catch (Exception exception)
                {
                    issues.Add(PacketIssue(row, "qfu_parse_failed", exception.Message));
                }
            }

            issues.Add(
                new ExtractionIssue
                    {
                        Code = "accepted_qfu_not_correlated",
                        Severity = "warning",
                        Message = "No accepted QuestFullUpdate containing the selected ACG building could be decoded."
                    });
            return null;
        }

        private static List<QuestActionIdentityRecord> ExtractQuestActionIdentities(
            IEnumerable<QuestActionInfo> actions)
        {
            var records = new List<QuestActionIdentityRecord>();
            if (actions == null)
            {
                return records;
            }

            int actionIndex = 0;
            foreach (QuestActionInfo action in actions)
            {
                if (action != null)
                {
                    AddQuestActionIdentity(records, actionIndex, "Action", action.Action);
                    AddQuestActionIdentity(records, actionIndex, "UnknownId1", action.UnknownId1);
                    AddQuestActionIdentity(records, actionIndex, "UnknownId2", action.UnknownId2);
                    AddQuestActionIdentity(records, actionIndex, "UnknownId3", action.UnknownId3);
                    AddQuestActionIdentity(records, actionIndex, "UnknownId4", action.UnknownId4);
                    AddQuestActionIdentity(records, actionIndex, "UnknownId5", action.UnknownId5);
                    AddQuestActionIdentity(records, actionIndex, "UnknownId6", action.UnknownId6);
                    AddQuestActionIdentity(records, actionIndex, "UnknownId7", action.UnknownId7);
                    AddQuestActionIdentity(records, actionIndex, "PlayfieldId", action.PlayfieldId);
                }

                actionIndex++;
            }

            return records;
        }

        private static void AddQuestActionIdentity(
            ICollection<QuestActionIdentityRecord> records,
            int actionIndex,
            string field,
            AOSharp.Common.GameData.Identity identity)
        {
            records.Add(
                new QuestActionIdentityRecord
                    {
                        ActionIndex = actionIndex,
                        Field = field,
                        Identity =
                            new IdentityValue(
                                (int)identity.Type,
                                identity.Instance)
                    });
        }

        private static List<NamedIntValue> ExtractQuestScalarFields(Quest quest)
        {
            return new List<NamedIntValue>
                       {
                           NamedInt("Unknown1", quest.Unknown1),
                           NamedInt("Unknown2", quest.Unknown2),
                           NamedInt("Unknown3", quest.Unknown3),
                           NamedInt("Unknown4", quest.Unknown4),
                           NamedInt("Unknown5", quest.Unknown5),
                           NamedInt("Unknown6", quest.Unknown6),
                           NamedInt("Unknown7", quest.Unknown7),
                           NamedInt("Unknown8", quest.Unknown8),
                           NamedInt("Unknown9", quest.Unknown9),
                           NamedInt("Unknown10", quest.Unknown10),
                           NamedInt("Unknown11", quest.Unknown11),
                           NamedInt("Unknown12", quest.Unknown12),
                           NamedInt("Unknown13", quest.Unknown13),
                           NamedInt("Unknown14", quest.Unknown14),
                           NamedInt("Unknown15", quest.Unknown15),
                           NamedInt("Unknown16", quest.Unknown16),
                           NamedInt("Unknown17", quest.Unknown17),
                           NamedInt("Unknown18", quest.Unknown18),
                           NamedInt("MissionIconId", quest.MissionIconId),
                           NamedInt("Unknown20", quest.Unknown20),
                           NamedInt("Unknown21", quest.Unknown21),
                           NamedInt("Unknown22", quest.Unknown22),
                           NamedInt("Unknown23", quest.Unknown23),
                           NamedInt("Unknown24", quest.Unknown24),
                           NamedInt("Unknown25", quest.Unknown25),
                           NamedInt("Unknown26", quest.Unknown26),
                           NamedInt("Unknown27", quest.Unknown27),
                           NamedInt("Unknown28", quest.Unknown28)
                       };
        }

        private static NamedIntValue NamedInt(string field, int value)
        {
            return new NamedIntValue { Field = field, Value = value };
        }

        private static PlayfieldAnarchyFRecord SelectGeneratorPaf(
            IList<PlayfieldAnarchyFRecord> candidates,
            ICollection<ExtractionIssue> issues)
        {
            if (candidates.Count == 0)
            {
                issues.Add(
                    new ExtractionIssue
                        {
                            Code = "generator_paf_missing",
                            Severity = "warning",
                            Message = "No C79F PlayfieldAnarchyF generator record was found."
                        });
                return null;
            }

            PlayfieldAnarchyFRecord selected = candidates
                .OrderBy(candidate => candidate.Provenance.GlobalOrdinal)
                .First();
            foreach (PlayfieldAnarchyFRecord candidate in candidates.Skip(1))
            {
                if (candidate.Building.Instance != selected.Building.Instance
                    || candidate.CapturedPf2 != selected.CapturedPf2
                    || !string.Equals(
                        candidate.GeneratorPayloadSha256,
                        selected.GeneratorPayloadSha256,
                        StringComparison.Ordinal))
                {
                    issues.Add(
                        new ExtractionIssue
                            {
                                Code = "multiple_conflicting_generator_paf",
                                Severity = "error",
                                Message = "Capture contains multiple conflicting C79F generator records."
                            });
                    break;
                }
            }

            return selected;
        }

        private static PlayfieldAnarchyFRecord ParsePlayfieldAnarchyF(
            CapturePacketRow row,
            byte[] packet)
        {
            RequirePacket(packet, 70, PlayfieldAnarchyFType, "PlayfieldAnarchyF");
            byte[] payload = Copy(packet, 70, packet.Length - 70);
            return new PlayfieldAnarchyFRecord
                       {
                           Identity = ReadIdentity(packet, 20),
                           HeaderUnknown = packet[28],
                           Unknown1 = ReadInt32(packet, 29),
                           CharacterCoordinates =
                               new Vector3Value(
                                   ReadSingle(packet, 33),
                                   ReadSingle(packet, 37),
                                   ReadSingle(packet, 41)),
                           Unknown2 = packet[45],
                           Building = ReadIdentity(packet, 46),
                           Unknown3 = ReadInt32(packet, 54),
                           Unknown4 = ReadInt32(packet, 58),
                           PlayfieldIdentity = ReadIdentity(packet, 62),
                           CapturedPf2 = ReadInt32(packet, 66),
                           GeneratorPayload = payload,
                           GeneratorPayloadSha256 = Sha256(payload),
                           PayloadBuilding =
                               payload.Length >= 8 ? ReadIdentity(payload, 0) : null,
                           Provenance = PacketProvenance.From(row, packet, "decoded")
                       };
        }

        private static TeleportRecord ParseTeleport(CapturePacketRow row, byte[] packet)
        {
            RequirePacket(packet, 102, N3TeleportType, "N3Teleport");
            return new TeleportRecord
                       {
                           PlayerIdentity = ReadIdentity(packet, 20),
                           Destination =
                               new Vector3Value(
                                   ReadSingle(packet, 29),
                                   ReadSingle(packet, 33),
                                   ReadSingle(packet, 37)),
                           Heading =
                               new QuaternionValue(
                                   ReadSingle(packet, 41),
                                   ReadSingle(packet, 45),
                                   ReadSingle(packet, 49),
                                   ReadSingle(packet, 53)),
                           Building = ReadIdentity(packet, 58),
                           GameServerId = ReadInt32(packet, 66),
                           SgId = ReadInt32(packet, 70),
                           ChangePlayfield = ReadIdentity(packet, 74),
                           CapturedPf2 = ReadInt32(packet, 78),
                           Playfield2 = ReadIdentity(packet, 90),
                           Provenance = PacketProvenance.From(row, packet, "decoded")
                       };
        }

        private static LayoutDynelRecord ParseEnvelopeDynel(
            CapturePacketRow row,
            byte[] packet,
            string category)
        {
            RequirePacket(packet, 73, row.N3TypeValue, row.N3TypeName);
            return new LayoutDynelRecord
                       {
                           Category = category,
                           CapturedIdentity = ReadIdentity(packet, 20),
                           ParentIdentity = ReadIdentity(packet, 33),
                           Position =
                               new Vector3Value(
                                   ReadSingle(packet, 41),
                                   ReadSingle(packet, 45),
                                   ReadSingle(packet, 49)),
                           Heading =
                               new QuaternionValue(
                                   ReadSingle(packet, 53),
                                   ReadSingle(packet, 57),
                                   ReadSingle(packet, 61),
                                   ReadSingle(packet, 65)),
                           CapturedPf2 = ReadInt32(packet, 69),
                           CapturedPf2Known = true,
                           Name = string.Empty,
                           LayoutEligibility = "physical_layout_slot",
                           RetargetingCategory = category,
                           Provenance = PacketProvenance.From(row, packet, "decoded")
                       };
        }

        private static LayoutDynelRecord ParseDoorFullUpdate(
            CapturePacketRow row,
            byte[] packet)
        {
            LayoutDynelRecord record = ParseEnvelopeDynel(row, packet, "door");
            int offset = 73;
            var decoded = new DoorDecodedFields
                              {
                                  MessageVersion = ReadInt32(packet, 29),
                                  OwnerIdentity = ReadIdentity(packet, 33),
                                  StateMachine = ReadIdentity(packet, offset)
                              };
            offset += 8;
            EnsureAvailable(packet, offset, 2, "DoorFullUpdate flags");
            decoded.Unknown2 = packet[offset++];
            decoded.Unknown3 = packet[offset++];
            decoded.Stats = ReadStatArray(packet, ref offset, "DoorFullUpdate stats");
            decoded.Name = ReadLengthString(packet, ref offset, "DoorFullUpdate name");
            decoded.Unknown4 = ReadRequiredInt32(packet, ref offset, "DoorFullUpdate Unknown4");
            decoded.Unknown5 = ReadRequiredInt32(packet, ref offset, "DoorFullUpdate Unknown5");
            decoded.Identities = ReadIdentityArray(packet, ref offset, "DoorFullUpdate identities");
            decoded.Unknown6 = ReadRequiredInt32(packet, ref offset, "DoorFullUpdate Unknown6");
            decoded.Unknown7 = ReadRequiredInt32(packet, ref offset, "DoorFullUpdate Unknown7");
            decoded.UndecodedTailHex = offset == packet.Length
                                          ? string.Empty
                                          : AcgHash.ToHex(Copy(packet, offset, packet.Length - offset));

            record.ParentIdentity = decoded.OwnerIdentity;
            record.Name = decoded.Name;
            record.Stats = decoded.Stats;
            StatValueRecord template = decoded.Stats.FirstOrDefault(
                stat => stat.Id == 0x2BE);
            record.Template = template == null ? 0 : template.Value;
            record.DoorFields = decoded;
            return record;
        }

        private static LayoutDynelRecord ParseVendingMachineFullUpdate(
            CapturePacketRow row,
            byte[] packet)
        {
            RequirePacket(packet, 45, row.N3TypeValue, row.N3TypeName);
            int offset = 29;
            int typeIdentifier = ReadRequiredInt32(
                packet,
                ref offset,
                "VendingMachineFullUpdate TypeIdentifier");
            IdentityValue npcIdentity = ReadIdentity(packet, offset);
            offset += 8;

            Vector3Value position = null;
            QuaternionValue heading = null;
            if (npcIdentity.Instance == 0)
            {
                EnsureAvailable(
                    packet,
                    offset,
                    28,
                    "VendingMachineFullUpdate placement");
                position =
                    new Vector3Value(
                        ReadSingle(packet, offset),
                        ReadSingle(packet, offset + 4),
                        ReadSingle(packet, offset + 8));
                heading =
                    new QuaternionValue(
                        ReadSingle(packet, offset + 12),
                        ReadSingle(packet, offset + 16),
                        ReadSingle(packet, offset + 20),
                        ReadSingle(packet, offset + 24));
                offset += 28;
            }

            int capturedPf2 = ReadRequiredInt32(
                packet,
                ref offset,
                "VendingMachineFullUpdate PlayfieldId");
            int unknown4 = ReadRequiredInt32(
                packet,
                ref offset,
                "VendingMachineFullUpdate Unknown4");
            int unknown5 = ReadRequiredInt32(
                packet,
                ref offset,
                "VendingMachineFullUpdate Unknown5");
            short unknown6 = ReadRequiredInt16(
                packet,
                ref offset,
                "VendingMachineFullUpdate Unknown6");
            List<StatValueRecord> stats = ReadStatArray(
                packet,
                ref offset,
                "VendingMachineFullUpdate stats");
            string displayName = ReadLengthString(
                packet,
                ref offset,
                "VendingMachineFullUpdate display string").Replace(
                    "\0",
                    string.Empty);
            int unknown8 = ReadRequiredInt32(
                packet,
                ref offset,
                "VendingMachineFullUpdate Unknown8");
            int? unknown9 = null;
            var unknown10 = new List<IdentityValue>();
            if (unknown8 == 2)
            {
                unknown9 = ReadRequiredInt32(
                    packet,
                    ref offset,
                    "VendingMachineFullUpdate Unknown9");
                unknown10 = ReadIdentityArray(
                    packet,
                    ref offset,
                    "VendingMachineFullUpdate Unknown10");
            }

            int unknown11 = ReadRequiredInt32(
                packet,
                ref offset,
                "VendingMachineFullUpdate Unknown11");
            var decoded = new VendingDecodedFields
                              {
                                  TypeIdentifier = typeIdentifier,
                                  NpcIdentity = npcIdentity,
                                  Unknown4 = unknown4,
                                  Unknown5 = unknown5,
                                  Unknown6 = unknown6,
                                  Stats = stats,
                                  DisplayString = displayName,
                                  Unknown8 = unknown8,
                                  Unknown9 = unknown9,
                                  Unknown10 = unknown10,
                                  Unknown11 = unknown11,
                                  UndecodedTailHex =
                                      offset == packet.Length
                                          ? string.Empty
                                          : AcgHash.ToHex(
                                              Copy(
                                                  packet,
                                                  offset,
                                                  packet.Length - offset))
                              };
            StatValueRecord template =
                stats.FirstOrDefault(stat => stat.Id == 0x2BE);
            return new LayoutDynelRecord
                       {
                           Category =
                               position == null
                                   ? "terminal_observation"
                                   : "terminal",
                           CapturedIdentity = ReadIdentity(packet, 20),
                           ParentIdentity = npcIdentity,
                           Position = position,
                           Heading = heading,
                           CapturedPf2 = capturedPf2,
                           CapturedPf2Known = true,
                           Template =
                               template == null
                                   ? typeIdentifier
                                   : template.Value,
                           Name = displayName,
                           Stats = stats,
                           VendingFields = decoded,
                           LayoutEligibility =
                               position == null
                                   ? "non_layout_owned_terminal_observation"
                                   : "physical_layout_slot",
                           RetargetingCategory = "terminal",
                           Provenance =
                               PacketProvenance.From(
                                   row,
                                   packet,
                                   "decoded_vending_machine_full_update")
                       };
        }

        private static SimpleCharDecodedFields BuildSimpleCharFields(
            RawSimpleCharFullUpdate decoded)
        {
            return new SimpleCharDecodedFields
                       {
                           Level = decoded.Level,
                           Health = decoded.Health,
                           HealthDamage = decoded.HealthDamage,
                           MonsterData = decoded.MonsterData,
                           MonsterScale = decoded.MonsterScale,
                           HeadMesh = decoded.HeadMesh,
                           Textures =
                               (decoded.Textures ?? new RawScfuTexture[0])
                               .Select(
                                   texture => new SimpleCharTextureRecord
                                                  {
                                                      Place = texture.Place,
                                                      Id = texture.Id,
                                                      Unknown = texture.Unknown
                                                  })
                               .ToList(),
                           Meshes =
                               (decoded.Meshes ?? new RawScfuMesh[0])
                               .Select(
                                   mesh => new SimpleCharMeshRecord
                                               {
                                                   Position = mesh.Position,
                                                   Id = mesh.Id,
                                                   OverrideTextureId = mesh.OverrideTextureId,
                                                   Layer = mesh.Layer
                                               })
                               .ToList(),
                           BytesConsumed = decoded.BytesConsumed,
                           DecodeFullyConsumed = decoded.DecodeFullyConsumed,
                           UndecodedTailHex = AcgHash.ToHex(decoded.UndecodedTail ?? new byte[0])
                       };
        }

        private static List<StatValueRecord> ReadStatArray(
            byte[] packet,
            ref int offset,
            string field)
        {
            int count = ReadX3F1Count(packet, ref offset, field);
            var result = new List<StatValueRecord>(count);
            for (int index = 0; index < count; index++)
            {
                EnsureAvailable(packet, offset, 8, field);
                int statOffset = offset;
                int statId = ReadInt32(packet, offset);
                int value = ReadInt32(packet, offset + 4);
                offset += 8;
                result.Add(
                    new StatValueRecord
                        {
                            Id = statId,
                            Name = StatName(statId),
                            Value = value,
                            PacketOffset = statOffset
                        });
            }

            return result;
        }

        private static List<IdentityValue> ReadIdentityArray(
            byte[] packet,
            ref int offset,
            string field)
        {
            int count = ReadX3F1Count(packet, ref offset, field);
            var result = new List<IdentityValue>(count);
            for (int index = 0; index < count; index++)
            {
                EnsureAvailable(packet, offset, 8, field);
                result.Add(ReadIdentity(packet, offset));
                offset += 8;
            }

            return result;
        }

        private static int ReadX3F1Count(byte[] packet, ref int offset, string field)
        {
            int encoded = ReadRequiredInt32(packet, ref offset, field + " length");
            if (encoded < 0x3F1 || encoded % 0x3F1 != 0)
            {
                throw new InvalidDataException(field + " has an invalid X3F1 length.");
            }

            int count = (encoded / 0x3F1) - 1;
            if (count < 0 || count > 4096)
            {
                throw new InvalidDataException(field + " count is outside the supported range.");
            }

            return count;
        }

        private static string ReadLengthString(byte[] packet, ref int offset, string field)
        {
            int length = ReadRequiredInt32(packet, ref offset, field + " length");
            if (length < 0)
            {
                throw new InvalidDataException(field + " length is negative.");
            }

            EnsureAvailable(packet, offset, length, field);
            string result = Encoding.UTF8.GetString(packet, offset, length);
            offset += length;
            return result;
        }

        private static int ReadRequiredInt32(byte[] packet, ref int offset, string field)
        {
            EnsureAvailable(packet, offset, 4, field);
            int value = ReadInt32(packet, offset);
            offset += 4;
            return value;
        }

        private static short ReadRequiredInt16(
            byte[] packet,
            ref int offset,
            string field)
        {
            EnsureAvailable(packet, offset, 2, field);
            short value = ReadInt16(packet, offset);
            offset += 2;
            return value;
        }

        private static void EnsureAvailable(
            byte[] packet,
            int offset,
            int length,
            string field)
        {
            if (offset < 0 || length < 0 || offset > packet.Length - length)
            {
                throw new InvalidDataException(field + " is truncated.");
            }
        }

        private static string StatName(int statId)
        {
            if (Enum.IsDefined(typeof(AOSharp.Common.GameData.Stat), statId))
            {
                return ((AOSharp.Common.GameData.Stat)statId).ToString();
            }

            return "Stat_" + statId.ToString(CultureInfo.InvariantCulture);
        }

        private static LayoutDynelRecord TryParseEnvelopeDynel(
            CapturePacketRow row,
            byte[] packet,
            string category,
            int expectedPf2)
        {
            if (packet == null
                || packet.Length < 73
                || ReadInt32(packet, 33) != 0)
            {
                return null;
            }

            LayoutDynelRecord record = ParseEnvelopeDynel(row, packet, category);
            if (record.CapturedPf2 != expectedPf2)
            {
                record.LayoutEligibility = "non_layout_off_pf_critical_record";
            }

            return record;
        }

        private static LayoutDynelRecord ParseIdentityOnly(
            CapturePacketRow row,
            byte[] packet,
            string category)
        {
            RequirePacket(packet, 29, row.N3TypeValue, row.N3TypeName);
            return new LayoutDynelRecord
                       {
                           Category = category,
                           CapturedIdentity = ReadIdentity(packet, 20),
                           Name = string.Empty,
                           RetargetingCategory = category,
                           Provenance = PacketProvenance.From(row, packet, "identity_only")
                       };
        }

        private static LayoutDynelRecord ParseCharInPlay(
            CapturePacketRow row,
            byte[] packet)
        {
            if (packet == null || packet.Length < 29)
            {
                throw new InvalidDataException("CharInPlay packet is truncated.");
            }

            if (ReadInt32(packet, N3BodyOffset) != row.N3TypeValue)
            {
                throw new InvalidDataException("CharInPlay N3 type mismatch.");
            }

            int declaredLength = (packet[6] << 8) | packet[7];
            string parseStatus;
            if (declaredLength == packet.Length)
            {
                parseStatus = "identity_only";
            }
            else if (declaredLength == 0
                     && packet.Length == 29
                     && string.Equals(row.Direction, "OUT", StringComparison.OrdinalIgnoreCase))
            {
                parseStatus = "decoded_outbound_zero_declared_length_variant";
            }
            else
            {
                throw new InvalidDataException(
                    "CharInPlay frame length mismatch: declared="
                    + declaredLength.ToString(CultureInfo.InvariantCulture)
                    + " actual="
                    + packet.Length.ToString(CultureInfo.InvariantCulture));
            }

            return new LayoutDynelRecord
                       {
                           Category = "char_in_play",
                           CapturedIdentity = ReadIdentity(packet, 20),
                           Name = string.Empty,
                           RetargetingCategory = "char_in_play",
                           Provenance = PacketProvenance.From(row, packet, parseStatus)
                       };
        }

        private static LayoutDynelRecord ParseObjectiveDynel(
            CapturePacketRow row,
            byte[] packet,
            int expectedPf2)
        {
            RequirePacket(packet, 87, row.N3TypeValue, row.N3TypeName);
            int ownerType = ReadInt32(packet, 33);
            int ownerInstance = ReadInt32(packet, 37);
            if (ownerType != 0)
            {
                return new LayoutDynelRecord
                           {
                               Category = "objective_item_observation",
                               CapturedIdentity = ReadIdentity(packet, 20),
                               ParentIdentity =
                                   new IdentityValue(ownerType, ownerInstance),
                               Name = string.Empty,
                               LayoutEligibility =
                                   "non_layout_owned_item_inventory_or_lifecycle_observation",
                               RetargetingCategory = "objective_item",
                               Provenance =
                                   PacketProvenance.From(
                                       row,
                                       packet,
                                       "raw_preserved_non_layout_owned_item_full_update")
                           };
            }

            int capturedPf2 = ReadInt32(packet, 69);
            int offset = 83;
            List<StatValueRecord> stats = ReadStatArray(
                packet,
                ref offset,
                row.N3TypeName + " stats");
            StatValueRecord templateStat = stats.FirstOrDefault(stat => stat.Id == 0x2BE);
            string itemName = string.Empty;
            int? trailingInt32 = null;
            if (string.Equals(
                    row.N3TypeName,
                    "SimpleItemFullUpdate",
                    StringComparison.OrdinalIgnoreCase))
            {
                itemName = ReadLengthString(
                    packet,
                    ref offset,
                    row.N3TypeName + " name").Replace(
                        "\0",
                        string.Empty);
            }
            else if (string.Equals(
                         row.N3TypeName,
                         "WeaponItemFullUpdate",
                         StringComparison.OrdinalIgnoreCase))
            {
                trailingInt32 = ReadRequiredInt32(
                    packet,
                    ref offset,
                    row.N3TypeName + " Unknown3");
            }

            var itemFields = new ItemDecodedFields
                                 {
                                     MessageVersion = ReadInt32(packet, 29),
                                     OwnerIdentity = new IdentityValue(ownerType, ownerInstance),
                                     StateMachine = ReadIdentity(packet, 73),
                                     Unknown2 = ReadInt16(packet, 81),
                                     Stats = stats,
                                     Name = itemName,
                                     TrailingInt32 = trailingInt32,
                                     UndecodedTailHex =
                                         offset == packet.Length
                                             ? string.Empty
                                             : AcgHash.ToHex(
                                                 Copy(packet, offset, packet.Length - offset))
                                 };

            return new LayoutDynelRecord
                       {
                           Category = "objective_item",
                           CapturedIdentity = ReadIdentity(packet, 20),
                           ParentIdentity = new IdentityValue(ownerType, ownerInstance),
                           Position =
                               new Vector3Value(
                                   ReadSingle(packet, 41),
                                   ReadSingle(packet, 45),
                                   ReadSingle(packet, 49)),
                           Heading =
                               new QuaternionValue(
                                   ReadSingle(packet, 53),
                                   ReadSingle(packet, 57),
                                   ReadSingle(packet, 61),
                                   ReadSingle(packet, 65)),
                           CapturedPf2 = capturedPf2,
                           CapturedPf2Known = true,
                           Template = templateStat == null ? 0 : templateStat.Value,
                           Name = itemName,
                           Stats = stats,
                           ItemFields = itemFields,
                           LayoutEligibility =
                               capturedPf2 == expectedPf2
                                   ? "physical_layout_slot"
                                   : "non_layout_off_pf_critical_record",
                           RetargetingCategory = "objective_item",
                           Provenance =
                               PacketProvenance.From(
                                   row,
                                   packet,
                                   "decoded_positioned_item_full_update")
                       };
        }

        private static void PopulatePositionedItemFields(
            byte[] packet,
            LayoutDynelRecord record,
            string messageType)
        {
            EnsureAvailable(packet, 83, 4, messageType + " stats");
            int offset = 83;
            List<StatValueRecord> stats = ReadStatArray(
                packet,
                ref offset,
                messageType + " stats");
            StatValueRecord template = stats.FirstOrDefault(stat => stat.Id == 0x2BE);
            record.Stats = stats;
            record.Template = template == null ? 0 : template.Value;
            record.ItemFields =
                new ItemDecodedFields
                    {
                        MessageVersion = ReadInt32(packet, 29),
                        OwnerIdentity = record.ParentIdentity,
                        StateMachine = ReadIdentity(packet, 73),
                        Unknown2 = ReadInt16(packet, 81),
                        Stats = stats,
                        UndecodedTailHex =
                            offset == packet.Length
                                ? string.Empty
                                : AcgHash.ToHex(
                                    Copy(packet, offset, packet.Length - offset))
                    };
        }

        private static void AddNpcObjectiveSlot(
            AcceptedMissionRecord mission,
            IEnumerable<LayoutDynelRecord> npcObservations,
            ICollection<LayoutDynelRecord> objectiveSlots,
            ICollection<ExtractionIssue> issues)
        {
            if (mission == null
                || (mission.MissionType != "kill" && mission.MissionType != "find_person"))
            {
                return;
            }

            var observationsByIdentity = npcObservations
                .Where(record => record.CapturedIdentity != null)
                .GroupBy(
                    record => IdentityKey(record.CapturedIdentity),
                    StringComparer.Ordinal)
                .ToDictionary(
                    group => group.Key,
                    group => group.OrderBy(record => record.Provenance.GlobalOrdinal).First(),
                    StringComparer.Ordinal);
            var matches = new List<QuestActionIdentityRecord>();
            foreach (QuestActionIdentityRecord actionIdentity in
                     mission.QuestActionIdentities ?? new List<QuestActionIdentityRecord>())
            {
                if (actionIdentity.Identity == null
                    || actionIdentity.Identity.Type != SimpleCharIdentityType
                    || !observationsByIdentity.ContainsKey(IdentityKey(actionIdentity.Identity)))
                {
                    continue;
                }

                if (!matches.Any(
                        existing => IdentityEquals(existing.Identity, actionIdentity.Identity)))
                {
                    matches.Add(actionIdentity);
                }
            }

            if (matches.Count == 0)
            {
                AddWarning(
                    issues,
                    "npc_objective_not_correlated",
                    "The accepted mission QFU did not correlate an objective identity to a captured NPC.");
                return;
            }

            if (matches.Count != 1)
            {
                AddError(
                    issues,
                    "npc_objective_identity_conflict",
                    "The accepted mission QFU correlated more than one captured NPC objective identity.");
                return;
            }

            QuestActionIdentityRecord evidence = matches[0];
            LayoutDynelRecord npc = observationsByIdentity[IdentityKey(evidence.Identity)];
            LayoutDynelRecord objective = npc.CloneAs("objective_npc", "objective_npc_slot");
            objective.EvidenceKind = "accepted_qfu_action_identity_to_scfu";
            objective.EvidenceField =
                "QuestActions["
                + evidence.ActionIndex.ToString(CultureInfo.InvariantCulture)
                + "]."
                + evidence.Field;
            objective.EvidenceIdentity = evidence.Identity;
            objective.CorrelationProvenance = mission.Provenance;
            objectiveSlots.Add(objective);
        }

        private static void CorrelateItemObjectiveSlots(
            AcceptedMissionRecord mission,
            IEnumerable<LayoutDynelRecord> objectiveObservations,
            ICollection<ExtractionIssue> issues)
        {
            if (mission == null)
            {
                return;
            }

            string expectedMessageType;
            switch (mission.MissionType)
            {
                case "return_item":
                    expectedMessageType = "WeaponItemFullUpdate";
                    break;
                case "find_item":
                case "repair":
                    expectedMessageType = "SimpleItemFullUpdate";
                    break;
                default:
                    return;
            }

            List<LayoutDynelRecord> physicalCandidates = objectiveObservations
                .Where(
                    record => record.CapturedIdentity != null
                              && record.Position != null
                              && string.Equals(
                                  record.LayoutEligibility,
                                  "physical_layout_slot",
                                  StringComparison.Ordinal)
                              && record.Provenance != null
                              && string.Equals(
                                  record.Provenance.MessageType,
                                  expectedMessageType,
                                  StringComparison.OrdinalIgnoreCase))
                .ToList();
            List<QuestActionIdentityRecord> identityMatches =
                (mission.QuestActionIdentities ?? new List<QuestActionIdentityRecord>())
                .Where(
                    actionIdentity => actionIdentity.Identity != null
                                      && physicalCandidates.Any(
                                          record => IdentityEquals(
                                              record.CapturedIdentity,
                                              actionIdentity.Identity)))
                .GroupBy(
                    actionIdentity => IdentityKey(actionIdentity.Identity),
                    StringComparer.Ordinal)
                .Select(group => group.First())
                .ToList();

            if (identityMatches.Count == 0)
            {
                AddWarning(
                    issues,
                    "item_objective_not_correlated",
                    "No physical "
                    + expectedMessageType
                    + " identity exactly matched the accepted mission QFU actions.");
                return;
            }

            if (identityMatches.Count != 1)
            {
                AddError(
                    issues,
                    "item_objective_identity_conflict",
                    "More than one physical item identity matched the accepted mission QFU actions.");
                return;
            }

            QuestActionIdentityRecord evidence = identityMatches[0];
            foreach (LayoutDynelRecord record in physicalCandidates)
            {
                if (!IdentityEquals(record.CapturedIdentity, evidence.Identity))
                {
                    record.LayoutEligibility = "non_layout_uncorrelated_item_observation";
                    continue;
                }

                record.EvidenceKind = "accepted_qfu_action_identity_to_item_full_update";
                record.EvidenceField =
                    "QuestActions["
                    + evidence.ActionIndex.ToString(CultureInfo.InvariantCulture)
                    + "]."
                    + evidence.Field;
                record.EvidenceIdentity = evidence.Identity;
                record.CorrelationProvenance = mission.Provenance;
            }
        }

        private static LayoutDynelRecord FindExit(
            IEnumerable<CapturePacketRow> rows,
            long windowStart,
            long windowEnd,
            IEnumerable<LayoutDynelRecord> doors,
            Vector3Value interiorSpawn,
            PacketProvenance spawnProvenance,
            ICollection<ExtractionIssue> issues)
        {
            List<LayoutDynelRecord> uniqueDoors = doors.ToList();
            List<LayoutDynelRecord> sentinelCandidates = uniqueDoors
                .Where(
                    candidate => candidate.DoorFields != null
                                 && candidate.DoorFields.Unknown6 == ExitBoundaryUnknown6
                                 && candidate.DoorFields.Unknown7 == ExitBoundaryUnknown7
                                 && string.IsNullOrEmpty(candidate.DoorFields.UndecodedTailHex))
                .ToList();
            if (sentinelCandidates.Count == 0)
            {
                AddWarning(
                    issues,
                    "exit_boundary_sentinel_missing",
                    "No fully decoded door has the captured exit-boundary trailing sentinel.");
                return null;
            }

            if (sentinelCandidates.Count != 1)
            {
                AddWarning(
                    issues,
                    "exit_boundary_sentinel_not_unique",
                    "The captured exit-boundary trailing sentinel is not unique within this layout.");
                return null;
            }

            if (interiorSpawn == null)
            {
                AddWarning(
                    issues,
                    "exit_boundary_spawn_correlation_missing",
                    "The unique exit-boundary sentinel cannot be correlated without an interior spawn.");
                return null;
            }

            LayoutDynelRecord candidateExit = sentinelCandidates[0];
            float candidateDistanceSquared = DistanceSquared(
                candidateExit.Position,
                interiorSpawn);
            float nearestDistanceSquared = uniqueDoors.Min(
                candidate => DistanceSquared(candidate.Position, interiorSpawn));
            int nearestCount = uniqueDoors.Count(
                candidate => NearlyEqual(
                    DistanceSquared(candidate.Position, interiorSpawn),
                    nearestDistanceSquared));
            if (!NearlyEqual(candidateDistanceSquared, nearestDistanceSquared)
                || nearestCount != 1)
            {
                AddWarning(
                    issues,
                    "exit_boundary_spawn_correlation_failed",
                    "The unique exit-boundary sentinel is not the unique door nearest the interior spawn.");
                return null;
            }

            LayoutDynelRecord exit = candidateExit.CloneAs("exit", "exit");
            exit.EvidenceKind = "unique_decoded_boundary_sentinel_nearest_interior_spawn";
            exit.EvidenceField = "DoorFullUpdate.Unknown6+Unknown7";
            exit.EvidenceIdentity = candidateExit.CapturedIdentity;
            exit.CorrelationProvenance = spawnProvenance;
            exit.DistanceFromInteriorSpawn = (float)Math.Sqrt(candidateDistanceSquared);

            var serializer = new MessageSerializer();
            foreach (CapturePacketRow row in rows
                         .Where(candidate => candidate.GlobalOrdinal > windowStart
                                             && candidate.GlobalOrdinal < windowEnd
                                             && string.Equals(
                                                 candidate.Direction,
                                                 "OUT",
                                                 StringComparison.OrdinalIgnoreCase)
                                             && string.Equals(
                                                 candidate.N3TypeName,
                                                 "GenericCmd",
                                                 StringComparison.OrdinalIgnoreCase))
                         .OrderByDescending(candidate => candidate.GlobalOrdinal))
            {
                try
                {
                    byte[] packet = DecodePacket(row);
                    Message decodedMessage = serializer.Deserialize(packet);
                    var command = decodedMessage == null
                                      ? null
                                      : decodedMessage.Body as GenericCmdMessage;
                    if (command == null || command.Action != GenericCmdAction.Use)
                    {
                        continue;
                    }

                    var target = new IdentityValue((int)command.Target.Type, command.Target.Instance);
                    if (!IdentityEquals(candidateExit.CapturedIdentity, target))
                    {
                        continue;
                    }

                    exit.InteractionProvenance = PacketProvenance.From(row, packet, "decoded");
                    break;
                }
                catch (Exception exception)
                {
                    issues.Add(PacketIssue(row, "exit_interaction_parse_failed", exception.Message));
                }
            }

            return exit;
        }

        private static float DistanceSquared(Vector3Value left, Vector3Value right)
        {
            if (left == null || right == null)
            {
                return float.PositiveInfinity;
            }

            float x = left.X - right.X;
            float y = left.Y - right.Y;
            float z = left.Z - right.Z;
            return (x * x) + (y * y) + (z * z);
        }

        private static bool NearlyEqual(float left, float right)
        {
            float difference = Math.Abs(left - right);
            float scale = Math.Max(1.0f, Math.Max(Math.Abs(left), Math.Abs(right)));
            return difference <= 0.00001f * scale;
        }

        private static string IdentityKey(IdentityValue identity)
        {
            return identity.Type.ToString(CultureInfo.InvariantCulture)
                   + ":"
                   + identity.Instance.ToString(CultureInfo.InvariantCulture);
        }

        private static void AddIfTargetPlayfield(
            ICollection<LayoutDynelRecord> records,
            LayoutDynelRecord record,
            PlayfieldAnarchyFRecord paf,
            ICollection<ExtractionIssue> issues)
        {
            if (paf != null
                && record.CapturedPf2Known
                && record.CapturedPf2 == paf.CapturedPf2)
            {
                records.Add(record);
                return;
            }

            AddError(
                issues,
                record.Category + "_off_pf_critical_record",
                record.Provenance.MessageType
                + " inside the layout window uses an unexpected PF2.");
        }

        private static List<LayoutDynelRecord> NormalizeSlots(
            IEnumerable<LayoutDynelRecord> observations,
            bool immutablePlacement,
            ICollection<ExtractionIssue> issues)
        {
            var seen = new Dictionary<string, LayoutDynelRecord>(StringComparer.Ordinal);
            var slots = new List<LayoutDynelRecord>();
            foreach (LayoutDynelRecord observation in observations.OrderBy(
                         record => record.Provenance.GlobalOrdinal))
            {
                string key = observation.CapturedIdentity == null
                                 ? observation.Provenance.GlobalOrdinal.ToString(CultureInfo.InvariantCulture)
                                 : observation.CapturedIdentity.Type.ToString(CultureInfo.InvariantCulture)
                                   + ":"
                                   + observation.CapturedIdentity.Instance.ToString(CultureInfo.InvariantCulture);
                LayoutDynelRecord existing;
                if (!seen.TryGetValue(key, out existing))
                {
                    LayoutDynelRecord slot = observation.CloneAs(
                        observation.Category,
                        observation.RetargetingCategory);
                    seen.Add(key, slot);
                    slots.Add(slot);
                    continue;
                }

                if (RecordsConflict(existing, observation, immutablePlacement))
                {
                    issues.Add(
                        new ExtractionIssue
                            {
                                Code = "duplicate_identity_conflict",
                                Severity = "error",
                                Message = observation.Category
                                          + " identity "
                                          + key
                                          + " has conflicting immutable placement records."
                            });
                }
            }

            SortAndNumber(slots, slots.Count == 0 ? string.Empty : slots[0].Category);
            return slots;
        }

        private static void ValidateCrossCategoryIdentityReuse(
            AcgLayoutSlots layoutSlots,
            ICollection<ExtractionIssue> issues)
        {
            var categories = new[]
                                 {
                                     layoutSlots.Doors,
                                     layoutSlots.Chests,
                                     layoutSlots.Terminals,
                                     layoutSlots.Npcs,
                                     layoutSlots.Objectives
                                 };
            var seen = new Dictionary<string, LayoutDynelRecord>(StringComparer.Ordinal);
            foreach (IEnumerable<LayoutDynelRecord> category in categories)
            {
                foreach (LayoutDynelRecord record in category)
                {
                    if (record.CapturedIdentity == null)
                    {
                        continue;
                    }

                    string key = IdentityKey(record.CapturedIdentity);
                    LayoutDynelRecord existing;
                    if (!seen.TryGetValue(key, out existing))
                    {
                        seen.Add(key, record);
                        continue;
                    }

                    bool npcObjectiveOverlay =
                        (existing.RetargetingCategory == "npc_slot"
                         && record.RetargetingCategory == "objective_npc_slot")
                        || (existing.RetargetingCategory == "objective_npc_slot"
                            && record.RetargetingCategory == "npc_slot");
                    bool sameCategory = string.Equals(
                        existing.Category,
                        record.Category,
                        StringComparison.Ordinal);
                    if (npcObjectiveOverlay
                        && !RecordsConflict(existing, record, true))
                    {
                        continue;
                    }

                    if (!sameCategory)
                    {
                        AddError(
                            issues,
                            "cross_category_identity_conflict",
                            "Captured identity "
                            + key
                            + " is reused by "
                            + existing.Category
                            + " and "
                            + record.Category
                            + (npcObjectiveOverlay
                                   ? " with incoherent NPC/objective overlay fields."
                                   : "."));
                    }
                }
            }
        }

        private static bool RecordsConflict(
            LayoutDynelRecord left,
            LayoutDynelRecord right,
            bool immutablePlacement)
        {
            if (left.CapturedPf2Known != right.CapturedPf2Known
                || (left.CapturedPf2Known
                    && left.CapturedPf2 != right.CapturedPf2)
                || !NullableIdentityEquals(left.ParentIdentity, right.ParentIdentity)
                || left.Template != right.Template
                || !string.Equals(left.Name ?? string.Empty, right.Name ?? string.Empty, StringComparison.Ordinal))
            {
                return true;
            }

            if (immutablePlacement
                && (!VectorEquals(left.Position, right.Position)
                    || !QuaternionEquals(left.Heading, right.Heading)
                    || !StatsEqual(left.Stats, right.Stats)
                    || DoorFieldsConflict(left.DoorFields, right.DoorFields)
                    || ItemFieldsConflict(left.ItemFields, right.ItemFields)
                    || VendingFieldsConflict(
                        left.VendingFields,
                        right.VendingFields)))
            {
                return true;
            }

            return SimpleCharFieldsConflict(left.SimpleCharFields, right.SimpleCharFields);
        }

        private static bool DoorFieldsConflict(
            DoorDecodedFields left,
            DoorDecodedFields right)
        {
            if (left == null || right == null)
            {
                return left != right;
            }

            return left.MessageVersion != right.MessageVersion
                   || !NullableIdentityEquals(left.OwnerIdentity, right.OwnerIdentity)
                   || !NullableIdentityEquals(left.StateMachine, right.StateMachine)
                   || left.Unknown2 != right.Unknown2
                   || left.Unknown3 != right.Unknown3
                   || !string.Equals(left.Name ?? string.Empty, right.Name ?? string.Empty, StringComparison.Ordinal)
                   || left.Unknown4 != right.Unknown4
                   || left.Unknown5 != right.Unknown5
                   || left.Unknown6 != right.Unknown6
                   || left.Unknown7 != right.Unknown7
                   || !IdentityListsEqual(left.Identities, right.Identities)
                   || !string.Equals(
                       left.UndecodedTailHex ?? string.Empty,
                       right.UndecodedTailHex ?? string.Empty,
                       StringComparison.Ordinal);
        }

        private static bool ItemFieldsConflict(
            ItemDecodedFields left,
            ItemDecodedFields right)
        {
            if (left == null || right == null)
            {
                return left != right;
            }

            return left.MessageVersion != right.MessageVersion
                   || !NullableIdentityEquals(left.OwnerIdentity, right.OwnerIdentity)
                   || !NullableIdentityEquals(left.StateMachine, right.StateMachine)
                   || left.Unknown2 != right.Unknown2
                   || !StatsEqual(left.Stats, right.Stats)
                   || !string.Equals(
                       left.Name ?? string.Empty,
                       right.Name ?? string.Empty,
                       StringComparison.Ordinal)
                   || left.TrailingInt32 != right.TrailingInt32
                   || !string.Equals(
                       left.UndecodedTailHex ?? string.Empty,
                       right.UndecodedTailHex ?? string.Empty,
                       StringComparison.Ordinal);
        }

        private static bool VendingFieldsConflict(
            VendingDecodedFields left,
            VendingDecodedFields right)
        {
            if (left == null || right == null)
            {
                return left != right;
            }

            return left.TypeIdentifier != right.TypeIdentifier
                   || !NullableIdentityEquals(
                       left.NpcIdentity,
                       right.NpcIdentity)
                   || left.Unknown4 != right.Unknown4
                   || left.Unknown5 != right.Unknown5
                   || left.Unknown6 != right.Unknown6
                   || !StatsEqual(left.Stats, right.Stats)
                   || !string.Equals(
                       left.DisplayString ?? string.Empty,
                       right.DisplayString ?? string.Empty,
                       StringComparison.Ordinal)
                   || left.Unknown8 != right.Unknown8
                   || left.Unknown9 != right.Unknown9
                   || !IdentityListsEqual(left.Unknown10, right.Unknown10)
                   || left.Unknown11 != right.Unknown11
                   || !string.Equals(
                       left.UndecodedTailHex ?? string.Empty,
                       right.UndecodedTailHex ?? string.Empty,
                       StringComparison.Ordinal);
        }

        private static bool IdentityListsEqual(
            IList<IdentityValue> left,
            IList<IdentityValue> right)
        {
            IList<IdentityValue> leftValues = left ?? new List<IdentityValue>();
            IList<IdentityValue> rightValues = right ?? new List<IdentityValue>();
            return leftValues.Count == rightValues.Count
                   && !leftValues.Where(
                           (value, index) =>
                               !NullableIdentityEquals(value, rightValues[index]))
                       .Any();
        }

        private static bool NullableIdentityEquals(
            IdentityValue left,
            IdentityValue right)
        {
            return left == null && right == null || IdentityEquals(left, right);
        }

        private static bool QuaternionEquals(
            QuaternionValue left,
            QuaternionValue right)
        {
            if (left == null || right == null)
            {
                return left == right;
            }

            return left.X.Equals(right.X)
                   && left.Y.Equals(right.Y)
                   && left.Z.Equals(right.Z)
                   && left.W.Equals(right.W);
        }

        private static bool StatsEqual(
            IList<StatValueRecord> left,
            IList<StatValueRecord> right)
        {
            IList<StatValueRecord> leftValues =
                (left ?? new List<StatValueRecord>())
                .Where(value => value.Id != 0)
                .ToList();
            IList<StatValueRecord> rightValues =
                (right ?? new List<StatValueRecord>())
                .Where(value => value.Id != 0)
                .ToList();
            if (leftValues.Count != rightValues.Count)
            {
                return false;
            }

            for (int index = 0; index < leftValues.Count; index++)
            {
                if (leftValues[index].Id != rightValues[index].Id
                    || leftValues[index].Value != rightValues[index].Value)
                {
                    return false;
                }
            }

            return true;
        }

        private static bool SimpleCharFieldsConflict(
            SimpleCharDecodedFields left,
            SimpleCharDecodedFields right)
        {
            if (left == null || right == null)
            {
                return left != right;
            }

            return left.Level != right.Level
                   || left.MonsterData != right.MonsterData
                   || left.MonsterScale != right.MonsterScale
                   || left.HeadMesh != right.HeadMesh
                   || !TexturesEqual(left.Textures, right.Textures)
                   || !MeshesEqual(left.Meshes, right.Meshes);
        }

        private static bool TexturesEqual(
            IList<SimpleCharTextureRecord> left,
            IList<SimpleCharTextureRecord> right)
        {
            IList<SimpleCharTextureRecord> leftValues =
                left ?? new List<SimpleCharTextureRecord>();
            IList<SimpleCharTextureRecord> rightValues =
                right ?? new List<SimpleCharTextureRecord>();
            return leftValues.Count == rightValues.Count
                   && !leftValues.Where(
                           (value, index) =>
                               value.Place != rightValues[index].Place
                               || value.Id != rightValues[index].Id
                               || value.Unknown != rightValues[index].Unknown)
                       .Any();
        }

        private static bool MeshesEqual(
            IList<SimpleCharMeshRecord> left,
            IList<SimpleCharMeshRecord> right)
        {
            IList<SimpleCharMeshRecord> leftValues =
                left ?? new List<SimpleCharMeshRecord>();
            IList<SimpleCharMeshRecord> rightValues =
                right ?? new List<SimpleCharMeshRecord>();
            return leftValues.Count == rightValues.Count
                   && !leftValues.Where(
                           (value, index) =>
                               value.Position != rightValues[index].Position
                               || value.Id != rightValues[index].Id
                               || value.OverrideTextureId
                               != rightValues[index].OverrideTextureId
                               || value.Layer != rightValues[index].Layer)
                       .Any();
        }

        private static void SortAndNumber(IList<LayoutDynelRecord> records, string category)
        {
            var sorted = records.OrderBy(record => record.Provenance.GlobalOrdinal).ToList();
            records.Clear();
            for (int index = 0; index < sorted.Count; index++)
            {
                sorted[index].Slot = index;
                sorted[index].Category = category;
                records.Add(sorted[index]);
            }
        }

        private static void AddMappings(
            ICollection<IdentityMappingRecord> mappings,
            IEnumerable<LayoutDynelRecord> records)
        {
            foreach (LayoutDynelRecord record in records)
            {
                mappings.Add(
                    new IdentityMappingRecord
                        {
                            Category = record.Category,
                            Slot = record.Slot,
                            CapturedIdentity = record.CapturedIdentity,
                            CapturedPf2 = record.CapturedPf2,
                            ParentIdentity = record.ParentIdentity,
                            RetargetingCategory = record.RetargetingCategory
                        });
            }
        }

        private static bool HasAcceptedMissionStructure(
            AcceptedMissionRecord mission,
            PlayfieldAnarchyFRecord paf)
        {
            return mission != null
                   && IsNonZeroIdentity(mission.AcceptedQfuIdentity)
                   && IdentityEquals(mission.AcceptedQfuBuilding, paf.Building)
                   && mission.ExteriorEntrance != null
                   && IdentityEquals(
                       mission.ExteriorEntrance.MissionBuildingReference,
                       paf.Building)
                   && IsNonZeroIdentity(mission.ExteriorEntrance.ExteriorPlayfield)
                   && IsFiniteVector(mission.ExteriorEntrance.Position)
                   && mission.MissionKeyIdentity != null
                   && mission.MissionKeyIdentity.Type == MissionKeyType
                   && mission.MissionKeyIdentity.Instance != 0;
        }

        private static bool HasGeneratorPayload(PlayfieldAnarchyFRecord paf)
        {
            return paf != null
                   && paf.GeneratorPayload != null
                   && paf.GeneratorPayload.Length > 0;
        }

        private static void ValidateSelectableEvidenceIntegrity(
            string captureSession,
            PlayfieldAnarchyFRecord paf,
            TeleportRecord teleport,
            AcceptedMissionRecord mission,
            LayoutDynelRecord exit,
            TeleportRecord exitTeleport,
            AcgLayoutSlots layoutSlots,
            ICollection<ExtractionIssue> issues)
        {
            if (paf != null)
            {
                ValidateRequiredProvenance(
                    captureSession,
                    "generator_paf",
                    paf.Provenance,
                    issues);
            }

            if (teleport != null)
            {
                ValidateRequiredProvenance(
                    captureSession,
                    "entry_teleport",
                    teleport.Provenance,
                    issues);
            }

            if (mission != null)
            {
                ValidateRequiredProvenance(
                    captureSession,
                    "accepted_qfu",
                    mission.Provenance,
                    issues);
            }

            if (exit != null)
            {
                ValidateRequiredProvenance(
                    captureSession,
                    "exit_boundary",
                    exit.Provenance,
                    issues);
            }

            if (exitTeleport != null)
            {
                ValidateRequiredProvenance(
                    captureSession,
                    "exit_teleport",
                    exitTeleport.Provenance,
                    issues);
            }

            if (layoutSlots == null)
            {
                return;
            }

            IEnumerable<LayoutDynelRecord> requiredSlots =
                (layoutSlots.Doors ?? new List<LayoutDynelRecord>())
                .Concat(layoutSlots.Chests ?? new List<LayoutDynelRecord>())
                .Concat(layoutSlots.Terminals ?? new List<LayoutDynelRecord>())
                .Concat(layoutSlots.Npcs ?? new List<LayoutDynelRecord>())
                .Concat(layoutSlots.Objectives ?? new List<LayoutDynelRecord>());
            foreach (LayoutDynelRecord record in requiredSlots)
            {
                ValidateRequiredProvenance(
                    captureSession,
                    record.Category + "_slot",
                    record.Provenance,
                    issues);
            }

            foreach (LayoutDynelRecord npc in
                     layoutSlots.Npcs ?? new List<LayoutDynelRecord>())
            {
                if (npc.SimpleCharFields == null
                    || !npc.SimpleCharFields.DecodeFullyConsumed
                    || !string.IsNullOrEmpty(
                        npc.SimpleCharFields.UndecodedTailHex))
                {
                    AddProvenanceError(
                        issues,
                        npc.Provenance,
                        "npc_scfu_not_fully_decoded",
                        "A selectable NPC slot requires a fully consumed SimpleCharFullUpdate with no undecoded tail.");
                }
            }
        }

        private static void ValidateRequiredProvenance(
            string captureSession,
            string evidenceRole,
            PacketProvenance provenance,
            ICollection<ExtractionIssue> issues)
        {
            if (provenance == null)
            {
                AddProvenanceError(
                    issues,
                    null,
                    "required_evidence_provenance_missing",
                    evidenceRole + " requires raw packet provenance.");
                return;
            }

            if (!string.Equals(
                    provenance.PreservationStatus,
                    "raw_complete",
                    StringComparison.OrdinalIgnoreCase))
            {
                AddProvenanceError(
                    issues,
                    provenance,
                    "required_evidence_not_raw_complete",
                    evidenceRole
                    + " requires PreservationStatus=raw_complete.");
            }

            if (!string.Equals(
                    provenance.CaptureSession,
                    captureSession,
                    StringComparison.Ordinal))
            {
                AddProvenanceError(
                    issues,
                    provenance,
                    "required_evidence_session_mismatch",
                    evidenceRole
                    + " comes from a different capture session.");
            }
        }

        private static void AddProvenanceError(
            ICollection<ExtractionIssue> issues,
            PacketProvenance provenance,
            string code,
            string message)
        {
            issues.Add(
                new ExtractionIssue
                    {
                        Code = code,
                        Severity = "error",
                        Message = message,
                        CaptureSession =
                            provenance == null
                                ? string.Empty
                                : provenance.CaptureSession,
                        CsvLine = provenance == null ? 0 : provenance.CsvLine,
                        GlobalOrdinal =
                            provenance == null ? 0 : provenance.GlobalOrdinal,
                        Sequence = provenance == null ? 0 : provenance.Sequence,
                        Direction =
                            provenance == null ? string.Empty : provenance.Direction,
                        CapturedUtc =
                            provenance == null
                                ? string.Empty
                                : provenance.CapturedUtc,
                        MessageType =
                            provenance == null
                                ? string.Empty
                                : provenance.MessageType,
                        PreservationStatus =
                            provenance == null
                                ? string.Empty
                                : provenance.PreservationStatus,
                        RawPacketLength =
                            provenance == null ? 0 : provenance.RawPacketLength,
                        RawPacketSha256 =
                            provenance == null
                                ? string.Empty
                                : provenance.RawPacketSha256,
                        RawPacketHex =
                            provenance == null
                                ? string.Empty
                                : provenance.RawPacketHex
                    });
        }

        private static bool IsNonZeroIdentity(IdentityValue identity)
        {
            return identity != null && identity.Type != 0 && identity.Instance != 0;
        }

        private static bool IsFiniteVector(Vector3Value value)
        {
            return value != null
                   && !float.IsNaN(value.X)
                   && !float.IsInfinity(value.X)
                   && !float.IsNaN(value.Y)
                   && !float.IsInfinity(value.Y)
                   && !float.IsNaN(value.Z)
                   && !float.IsInfinity(value.Z);
        }

        private static void Validate(
            PlayfieldAnarchyFRecord paf,
            TeleportRecord teleport,
            AcceptedMissionRecord mission,
            Vector3Value interiorSpawn,
            LayoutDynelRecord exit,
            TeleportRecord exitTeleport,
            IList<LayoutDynelRecord> doors,
            IList<LayoutDynelRecord> chests,
            IList<LayoutDynelRecord> terminals,
            IList<LayoutDynelRecord> npcs,
            ICollection<ExtractionIssue> issues)
        {
            if (paf == null)
            {
                AddWarning(issues, "generator_paf_required", "A generator PlayfieldAnarchyF record is required.");
                return;
            }

            if (paf.GeneratorPayload == null || paf.GeneratorPayload.Length == 0)
            {
                AddWarning(issues, "generator_payload_missing", "Generator payload bytes are required.");
            }

            if (paf.PayloadBuilding == null
                || paf.PayloadBuilding.Type != paf.Building.Type
                || paf.PayloadBuilding.Instance != paf.Building.Instance)
            {
                AddError(
                    issues,
                    "payload_paf_building_mismatch",
                    "The first payload identity does not match the PlayfieldAnarchyF building.");
            }

            if (teleport == null)
            {
                AddWarning(issues, "mission_teleport_missing", "No preceding C79F mission teleport was found.");
            }
            else
            {
                if (!IdentityEquals(teleport.Building, paf.Building))
                {
                    AddError(
                        issues,
                        "teleport_paf_building_mismatch",
                        "N3Teleport building differs from PlayfieldAnarchyF building.");
                }

                if (teleport.CapturedPf2 != paf.CapturedPf2)
                {
                    AddError(
                        issues,
                        "teleport_paf_pf2_mismatch",
                        "N3Teleport PF2 differs from PlayfieldAnarchyF PF2.");
                }
            }

            if (mission == null)
            {
                AddWarning(
                    issues,
                    "accepted_qfu_missing",
                    "Accepted QFU evidence could not be correlated to the selected layout.");
            }
            else if (!IdentityEquals(mission.AcceptedQfuBuilding, paf.Building))
            {
                AddError(
                    issues,
                    "accepted_qfu_building_mismatch",
                    "Accepted QFU building differs from PlayfieldAnarchyF building.");
            }
            else
            {
                if (!IsNonZeroIdentity(mission.AcceptedQfuIdentity))
                {
                    AddWarning(
                        issues,
                        "accepted_qfu_identity_missing",
                        "Accepted QFU identity is required.");
                }

                if (mission.ExteriorEntrance == null
                    || !IdentityEquals(
                        mission.ExteriorEntrance.MissionBuildingReference,
                        paf.Building)
                    || !IsNonZeroIdentity(mission.ExteriorEntrance.ExteriorPlayfield)
                    || !IsFiniteVector(mission.ExteriorEntrance.Position))
                {
                    AddWarning(
                        issues,
                        "accepted_qfu_exterior_entrance_incomplete",
                        "Accepted QFU exterior entrance requires the matching building, playfield, and finite position.");
                }

                if (mission.MissionKeyIdentity == null
                    || mission.MissionKeyIdentity.Type != MissionKeyType
                    || mission.MissionKeyIdentity.Instance == 0)
                {
                    AddWarning(
                        issues,
                        "accepted_qfu_mission_key_missing",
                        "Accepted QFU requires an exact C76D mission-key identity.");
                }
            }

            if (interiorSpawn == null)
            {
                AddWarning(issues, "interior_spawn_missing", "No interior spawn was identified.");
            }

            if (exit == null)
            {
                AddWarning(issues, "exit_missing", "No exit door was identified.");
            }

            if (exitTeleport == null)
            {
                AddWarning(
                    issues,
                    "exit_teleport_missing",
                    "No post-layout exit teleport was captured; the decoded exit-boundary door remains usable evidence.");
            }

            if (doors.Count == 0)
            {
                AddWarning(issues, "doors_missing", "No interior doors were correlated.");
            }

            if (chests.Count == 0)
            {
                AddWarning(issues, "chests_missing", "No interior chests were correlated.");
            }

            ValidatePf2("door", doors, paf.CapturedPf2, issues);
            ValidatePf2("chest", chests, paf.CapturedPf2, issues);
            ValidatePf2("terminal", terminals, paf.CapturedPf2, issues);
            ValidatePf2("npc", npcs, paf.CapturedPf2, issues);

            if (paf.CapturedPf2 == IncompleteShape)
            {
                AddWarning(
                    issues,
                    "known_incomplete_shape_1441804",
                    "Captured shape 1441804 is explicitly non-selectable.");
            }
        }

        private static void ValidatePf2(
            string category,
            IEnumerable<LayoutDynelRecord> records,
            int expectedPf2,
            ICollection<ExtractionIssue> issues)
        {
            if (records.Any(
                record => record.Position != null
                          && (!record.CapturedPf2Known
                              || record.CapturedPf2 != expectedPf2)))
            {
                AddError(
                    issues,
                    category + "_pf2_mismatch",
                    "One or more " + category + " records use an unexpected PF2.");
            }
        }

        private static List<CapturePacketRow> LoadRows(
            string path,
            ICollection<ExtractionIssue> issues)
        {
            var rows = new List<CapturePacketRow>();
            using (var reader = new System.IO.StreamReader(path, Encoding.UTF8, true))
            {
                string header = reader.ReadLine();
                if (!string.Equals(
                        header,
                        "CapturedUtc,ElapsedMilliseconds,Direction,GlobalOrdinal,Sequence,PacketLength,N3TypeValue,N3TypeName,IdentityType,IdentityInstance,PreservationStatus,RawHex",
                        StringComparison.Ordinal))
                {
                    throw new InvalidDataException("raw-packets.csv header is not recognized");
                }

                string line;
                int lineNumber = 1;
                while ((line = reader.ReadLine()) != null)
                {
                    lineNumber++;
                    if (string.IsNullOrWhiteSpace(line))
                    {
                        continue;
                    }

                    try
                    {
                        string[] fields = ParseCsvLine(line);
                        if (fields.Length != 12)
                        {
                            throw new InvalidDataException(
                                "expected 12 fields; got "
                                + fields.Length.ToString(CultureInfo.InvariantCulture));
                        }

                        DateTime capturedUtc;
                        long globalOrdinal;
                        int sequence;
                        int packetLength;
                        int n3TypeValue;
                        int identityType;
                        int identityInstance;
                        if (!DateTime.TryParse(
                                fields[0],
                                CultureInfo.InvariantCulture,
                                DateTimeStyles.RoundtripKind,
                                out capturedUtc)
                            || !long.TryParse(
                                fields[3],
                                NumberStyles.Integer,
                                CultureInfo.InvariantCulture,
                                out globalOrdinal)
                            || !int.TryParse(
                                fields[4],
                                NumberStyles.Integer,
                                CultureInfo.InvariantCulture,
                                out sequence)
                            || !int.TryParse(
                                fields[5],
                                NumberStyles.Integer,
                                CultureInfo.InvariantCulture,
                                out packetLength)
                            || !int.TryParse(
                                fields[6],
                                NumberStyles.Integer,
                                CultureInfo.InvariantCulture,
                                out n3TypeValue)
                            || !int.TryParse(
                                fields[8],
                                NumberStyles.Integer,
                                CultureInfo.InvariantCulture,
                                out identityType)
                            || !int.TryParse(
                                fields[9],
                                NumberStyles.Integer,
                                CultureInfo.InvariantCulture,
                                out identityInstance))
                        {
                            throw new InvalidDataException("invalid metadata");
                        }

                        rows.Add(
                            new CapturePacketRow
                                {
                                    CapturedUtc = capturedUtc.ToUniversalTime().ToString(
                                        "O",
                                        CultureInfo.InvariantCulture),
                                    Direction = fields[2],
                                    GlobalOrdinal = globalOrdinal,
                                    Sequence = sequence,
                                    PacketLength = packetLength,
                                    N3TypeValue = n3TypeValue,
                                    N3TypeName = fields[7],
                                    IdentityType = identityType,
                                    IdentityInstance = identityInstance,
                                    PreservationStatus = fields[10],
                                    RawHex = fields[11],
                                    CsvLineNumber = lineNumber
                                });
                    }
                    catch (Exception exception)
                    {
                        issues.Add(
                            new ExtractionIssue
                                {
                                    Code = "csv_row_malformed",
                                    Severity = "error",
                                    Message = "raw-packets.csv line "
                                              + lineNumber.ToString(CultureInfo.InvariantCulture)
                                              + ": "
                                              + exception.Message,
                                    CsvLine = lineNumber,
                                    RawSourceLine = line
                                });
                    }
                }
            }

            return rows;
        }

        private static string[] ParseCsvLine(string line)
        {
            var fields = new List<string>();
            var field = new StringBuilder();
            bool quoted = false;
            for (int index = 0; index < line.Length; index++)
            {
                char current = line[index];
                if (current == '"')
                {
                    if (quoted && index + 1 < line.Length && line[index + 1] == '"')
                    {
                        field.Append('"');
                        index++;
                    }
                    else
                    {
                        quoted = !quoted;
                    }
                }
                else if (current == ',' && !quoted)
                {
                    fields.Add(field.ToString());
                    field.Length = 0;
                }
                else
                {
                    field.Append(current);
                }
            }

            if (quoted)
            {
                throw new InvalidDataException("unterminated CSV quote");
            }

            fields.Add(field.ToString());
            return fields.ToArray();
        }

        private static byte[] DecodePacket(CapturePacketRow row)
        {
            if (string.IsNullOrEmpty(row.RawHex) || (row.RawHex.Length & 1) != 0)
            {
                throw new InvalidDataException("raw packet hex length is invalid");
            }

            var bytes = new byte[row.RawHex.Length / 2];
            for (int index = 0; index < bytes.Length; index++)
            {
                byte value;
                if (!byte.TryParse(
                        row.RawHex.Substring(index * 2, 2),
                        NumberStyles.HexNumber,
                        CultureInfo.InvariantCulture,
                        out value))
                {
                    throw new InvalidDataException(
                        "raw packet hex is invalid at byte "
                        + index.ToString(CultureInfo.InvariantCulture));
                }

                bytes[index] = value;
            }

            if (row.PacketLength != 0 && bytes.Length != row.PacketLength)
            {
                throw new InvalidDataException(
                    "CSV packet length "
                    + row.PacketLength.ToString(CultureInfo.InvariantCulture)
                    + " differs from raw length "
                    + bytes.Length.ToString(CultureInfo.InvariantCulture));
            }

            return bytes;
        }

        private static void RequirePacket(
            byte[] packet,
            int minimumLength,
            int expectedType,
            string label)
        {
            if (packet == null || packet.Length < minimumLength)
            {
                throw new InvalidDataException(label + " packet is truncated.");
            }

            int declaredLength = (packet[6] << 8) | packet[7];
            if (declaredLength != packet.Length)
            {
                throw new InvalidDataException(
                    label
                    + " frame length mismatch: declared="
                    + declaredLength.ToString(CultureInfo.InvariantCulture)
                    + " actual="
                    + packet.Length.ToString(CultureInfo.InvariantCulture));
            }

            int actualType = ReadInt32(packet, N3BodyOffset);
            if (actualType != expectedType)
            {
                throw new InvalidDataException(label + " N3 type mismatch.");
            }
        }

        private static bool IsObjectiveMessageFamily(string n3TypeName)
        {
            return string.Equals(
                       n3TypeName,
                       "WeaponItemFullUpdate",
                       StringComparison.OrdinalIgnoreCase)
                   || string.Equals(
                       n3TypeName,
                       "SimpleItemFullUpdate",
                       StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsCriticalLayoutMessageFamily(string n3TypeName)
        {
            return string.Equals(n3TypeName, "DoorFullUpdate", StringComparison.OrdinalIgnoreCase)
                   || string.Equals(n3TypeName, "ChestFullUpdate", StringComparison.OrdinalIgnoreCase)
                   || string.Equals(n3TypeName, "ChestItemFullUpdate", StringComparison.OrdinalIgnoreCase)
                   || string.Equals(
                       n3TypeName,
                       "VendingMachineFullUpdate",
                       StringComparison.OrdinalIgnoreCase)
                   || string.Equals(
                       n3TypeName,
                       "SimpleCharFullUpdate",
                       StringComparison.OrdinalIgnoreCase)
                   || string.Equals(n3TypeName, "CharInPlay", StringComparison.OrdinalIgnoreCase)
                   || IsObjectiveMessageFamily(n3TypeName);
        }

        private static bool PacketContainsInt32(byte[] packet, int value)
        {
            var needle = new byte[4];
            WriteInt32(needle, 0, value);
            return FindBytes(packet, needle, N3BodyOffset) >= 0;
        }

        private static string BuildBundleId(string captureSession, PlayfieldAnarchyFRecord paf)
        {
            if (paf == null)
            {
                return "capture-" + captureSession + "-incomplete";
            }

            return string.Format(
                CultureInfo.InvariantCulture,
                "capture-{0}-{1:X8}-{2:X8}-{3}",
                captureSession,
                paf.Building.Instance,
                paf.CapturedPf2,
                paf.GeneratorPayloadSha256.Substring(0, 16));
        }

        private static ExtractionIssue PacketIssue(
            CapturePacketRow row,
            string code,
            string message)
        {
            var issue = new ExtractionIssue
                            {
                                Code = code,
                                Severity = "warning",
                                Message = message,
                                CsvLine = row.CsvLineNumber,
                                GlobalOrdinal = row.GlobalOrdinal,
                                Sequence = row.Sequence,
                                Direction = row.Direction,
                                CapturedUtc = row.CapturedUtc,
                                MessageType = row.N3TypeName,
                                PreservationStatus = row.PreservationStatus,
                                RawIdentity =
                                    new IdentityValue(
                                        row.IdentityType,
                                        row.IdentityInstance),
                                RawPacketLength = row.PacketLength,
                                RawPacketHex = row.RawHex ?? string.Empty
                            };
            try
            {
                byte[] packet = DecodePacket(row);
                issue.RawPacketLength = packet.Length;
                issue.RawPacketSha256 = Sha256(packet);
            }
            catch
            {
                issue.RawPacketSha256 = string.Empty;
            }

            return issue;
        }

        private static void ApplyIssueContext(
            IEnumerable<ExtractionIssue> issues,
            string captureSession,
            string sourceFile)
        {
            foreach (ExtractionIssue issue in issues)
            {
                issue.CaptureSession = captureSession;
                issue.SourceFile = sourceFile;
            }
        }

        private static void AddWarning(
            ICollection<ExtractionIssue> issues,
            string code,
            string message)
        {
            issues.Add(new ExtractionIssue { Code = code, Severity = "warning", Message = message });
        }

        private static void AddError(
            ICollection<ExtractionIssue> issues,
            string code,
            string message)
        {
            issues.Add(new ExtractionIssue { Code = code, Severity = "error", Message = message });
        }

        private static int CompareRows(CapturePacketRow left, CapturePacketRow right)
        {
            int ordinal = left.GlobalOrdinal.CompareTo(right.GlobalOrdinal);
            return ordinal != 0 ? ordinal : left.CsvLineNumber.CompareTo(right.CsvLineNumber);
        }

        private static int CompareIssues(ExtractionIssue left, ExtractionIssue right)
        {
            int ordinal = left.GlobalOrdinal.CompareTo(right.GlobalOrdinal);
            if (ordinal != 0)
            {
                return ordinal;
            }

            int line = left.CsvLine.CompareTo(right.CsvLine);
            return line != 0 ? line : StringComparer.Ordinal.Compare(left.Code, right.Code);
        }

        private static bool VectorEquals(Vector3Value left, Vector3Value right)
        {
            if (left == null || right == null)
            {
                return left == right;
            }

            return FloatBits(left.X) == FloatBits(right.X)
                   && FloatBits(left.Y) == FloatBits(right.Y)
                   && FloatBits(left.Z) == FloatBits(right.Z);
        }

        private static int FloatBits(float value)
        {
            return BitConverter.ToInt32(BitConverter.GetBytes(value), 0);
        }

        private static bool IdentityEquals(IdentityValue left, IdentityValue right)
        {
            return left != null
                   && right != null
                   && left.Type == right.Type
                   && left.Instance == right.Instance;
        }

        private static IdentityValue ReadIdentity(byte[] bytes, int offset)
        {
            return new IdentityValue(ReadInt32(bytes, offset), ReadInt32(bytes, offset + 4));
        }

        private static int ReadInt32(byte[] bytes, int offset)
        {
            return (bytes[offset] << 24)
                   | (bytes[offset + 1] << 16)
                   | (bytes[offset + 2] << 8)
                   | bytes[offset + 3];
        }

        private static short ReadInt16(byte[] bytes, int offset)
        {
            return unchecked((short)((bytes[offset] << 8) | bytes[offset + 1]));
        }

        private static float ReadSingle(byte[] bytes, int offset)
        {
            var littleEndian = new byte[4];
            littleEndian[0] = bytes[offset + 3];
            littleEndian[1] = bytes[offset + 2];
            littleEndian[2] = bytes[offset + 1];
            littleEndian[3] = bytes[offset];
            float value = BitConverter.ToSingle(littleEndian, 0);
            if (float.IsNaN(value) || float.IsInfinity(value))
            {
                throw new InvalidDataException(
                    "Non-finite float at packet offset "
                    + offset.ToString(CultureInfo.InvariantCulture)
                    + ".");
            }

            return value;
        }

        private static void WriteInt16(byte[] bytes, int offset, int value)
        {
            bytes[offset] = (byte)((value >> 8) & 0xFF);
            bytes[offset + 1] = (byte)(value & 0xFF);
        }

        private static void WriteInt32(byte[] bytes, int offset, int value)
        {
            bytes[offset] = (byte)((value >> 24) & 0xFF);
            bytes[offset + 1] = (byte)((value >> 16) & 0xFF);
            bytes[offset + 2] = (byte)((value >> 8) & 0xFF);
            bytes[offset + 3] = (byte)(value & 0xFF);
        }

        private static void WriteSingle(byte[] bytes, int offset, float value)
        {
            byte[] littleEndian = BitConverter.GetBytes(value);
            bytes[offset] = littleEndian[3];
            bytes[offset + 1] = littleEndian[2];
            bytes[offset + 2] = littleEndian[1];
            bytes[offset + 3] = littleEndian[0];
        }

        private static int FindBytes(byte[] haystack, byte[] needle, int start)
        {
            if (haystack == null || needle == null || needle.Length == 0)
            {
                return -1;
            }

            for (int index = Math.Max(0, start); index <= haystack.Length - needle.Length; index++)
            {
                bool match = true;
                for (int needleIndex = 0; needleIndex < needle.Length; needleIndex++)
                {
                    if (haystack[index + needleIndex] != needle[needleIndex])
                    {
                        match = false;
                        break;
                    }
                }

                if (match)
                {
                    return index;
                }
            }

            return -1;
        }

        private static byte[] Copy(byte[] bytes, int offset, int length)
        {
            var result = new byte[length];
            Buffer.BlockCopy(bytes, offset, result, 0, length);
            return result;
        }

        private static string Sha256(byte[] bytes)
        {
            using (SHA256 sha256 = SHA256.Create())
            {
                return ToHex(sha256.ComputeHash(bytes)).ToLowerInvariant();
            }
        }

        private static string ToHex(byte[] bytes)
        {
            var builder = new StringBuilder(bytes.Length * 2);
            foreach (byte value in bytes)
            {
                builder.Append(value.ToString("X2", CultureInfo.InvariantCulture));
            }

            return builder.ToString();
        }

        private static void WriteCanonical(string path, AcgLayoutArtifact artifact)
        {
            File.WriteAllText(path, CanonicalJson.WriteArtifact(artifact), new UTF8Encoding(false));
        }

        private static void WriteCanonicalCorpus(
            string path,
            IList<AcgLayoutArtifact> artifacts,
            IList<ExtractionIssue> failures)
        {
            string directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            File.WriteAllText(
                path,
                CanonicalJson.WriteCorpus(artifacts, failures),
                new UTF8Encoding(false));
        }

        private static void Require(bool condition, string name)
        {
            if (!condition)
            {
                throw new InvalidOperationException("ACG extractor " + name + " failed.");
            }
        }
    }

    internal sealed class CapturePacketRow
    {
        internal string CaptureSession { get; set; }
        internal string CapturedUtc { get; set; }
        internal string Direction { get; set; }
        internal long GlobalOrdinal { get; set; }
        internal int Sequence { get; set; }
        internal int PacketLength { get; set; }
        internal int N3TypeValue { get; set; }
        internal string N3TypeName { get; set; }
        internal int IdentityType { get; set; }
        internal int IdentityInstance { get; set; }
        internal string PreservationStatus { get; set; }
        internal string RawHex { get; set; }
        internal int CsvLineNumber { get; set; }
    }

    internal sealed class KnownCaptureExpectation
    {
        internal KnownCaptureExpectation(
            string session,
            string missionType,
            int doorObservations,
            int chestObservations,
            int simpleCharObservations,
            int doorSlots,
            int chestSlots,
            int npcSlots,
            int buildingInstance,
            int capturedPf2,
            int objectiveIdentityType,
            int objectiveIdentityInstance,
            string objectiveEvidenceField,
            string payloadSha256,
            bool hasExitTeleport)
        {
            this.Session = session;
            this.MissionType = missionType;
            this.DoorObservations = doorObservations;
            this.ChestObservations = chestObservations;
            this.SimpleCharObservations = simpleCharObservations;
            this.DoorSlots = doorSlots;
            this.ChestSlots = chestSlots;
            this.NpcSlots = npcSlots;
            this.BuildingInstance = buildingInstance;
            this.CapturedPf2 = capturedPf2;
            this.ObjectiveIdentityType = objectiveIdentityType;
            this.ObjectiveIdentityInstance = objectiveIdentityInstance;
            this.ObjectiveEvidenceField = objectiveEvidenceField;
            this.PayloadSha256 = payloadSha256;
            this.HasExitTeleport = hasExitTeleport;
        }

        internal string Session { get; private set; }
        internal string MissionType { get; private set; }
        internal int DoorObservations { get; private set; }
        internal int ChestObservations { get; private set; }
        internal int SimpleCharObservations { get; private set; }
        internal int DoorSlots { get; private set; }
        internal int ChestSlots { get; private set; }
        internal int NpcSlots { get; private set; }
        internal int BuildingInstance { get; private set; }
        internal int CapturedPf2 { get; private set; }
        internal int ObjectiveIdentityType { get; private set; }
        internal int ObjectiveIdentityInstance { get; private set; }
        internal string ObjectiveEvidenceField { get; private set; }
        internal string PayloadSha256 { get; private set; }
        internal bool HasExitTeleport { get; private set; }
    }

    internal sealed class IdentityValue
    {
        internal IdentityValue()
        {
        }

        internal IdentityValue(int type, int instance)
        {
            this.Type = type;
            this.Instance = instance;
        }

        internal int Type { get; set; }
        internal int Instance { get; set; }
    }

    internal sealed class Vector3Value
    {
        internal Vector3Value()
        {
        }

        internal Vector3Value(float x, float y, float z)
        {
            this.X = x;
            this.Y = y;
            this.Z = z;
        }

        internal float X { get; set; }
        internal float Y { get; set; }
        internal float Z { get; set; }
    }

    internal sealed class QuaternionValue
    {
        internal QuaternionValue()
        {
        }

        internal QuaternionValue(float x, float y, float z, float w)
        {
            this.X = x;
            this.Y = y;
            this.Z = z;
            this.W = w;
        }

        internal float X { get; set; }
        internal float Y { get; set; }
        internal float Z { get; set; }
        internal float W { get; set; }
    }

    internal sealed class PacketProvenance
    {
        internal string CaptureSession { get; set; }
        internal int CsvLine { get; set; }
        internal long GlobalOrdinal { get; set; }
        internal int Sequence { get; set; }
        internal string Direction { get; set; }
        internal string CapturedUtc { get; set; }
        internal string MessageType { get; set; }
        internal string PreservationStatus { get; set; }
        internal int RawPacketLength { get; set; }
        internal string RawPacketSha256 { get; set; }
        internal string RawPacketHex { get; set; }
        internal string ParseStatus { get; set; }

        internal static PacketProvenance From(
            CapturePacketRow row,
            byte[] packet,
            string parseStatus)
        {
            return new PacketProvenance
                       {
                           CaptureSession = row.CaptureSession,
                           CsvLine = row.CsvLineNumber,
                           GlobalOrdinal = row.GlobalOrdinal,
                           Sequence = row.Sequence,
                           Direction = row.Direction,
                           CapturedUtc = row.CapturedUtc,
                           MessageType = row.N3TypeName,
                           PreservationStatus = row.PreservationStatus,
                           RawPacketLength = packet.Length,
                           RawPacketSha256 = AcgHash.Sha256(packet),
                           RawPacketHex = AcgHash.ToHex(packet),
                           ParseStatus = parseStatus
                       };
        }
    }

    internal static class AcgHash
    {
        internal static string Sha256(byte[] bytes)
        {
            using (SHA256 sha256 = SHA256.Create())
            {
                byte[] hash = sha256.ComputeHash(bytes);
                var builder = new StringBuilder(hash.Length * 2);
                foreach (byte value in hash)
                {
                    builder.Append(value.ToString("x2", CultureInfo.InvariantCulture));
                }

                return builder.ToString();
            }
        }

        internal static string ToHex(byte[] bytes)
        {
            var builder = new StringBuilder(bytes.Length * 2);
            foreach (byte value in bytes)
            {
                builder.Append(value.ToString("X2", CultureInfo.InvariantCulture));
            }

            return builder.ToString();
        }
    }

    internal sealed class PlayfieldAnarchyFRecord
    {
        internal IdentityValue Identity { get; set; }
        internal byte HeaderUnknown { get; set; }
        internal int Unknown1 { get; set; }
        internal Vector3Value CharacterCoordinates { get; set; }
        internal byte Unknown2 { get; set; }
        internal IdentityValue Building { get; set; }
        internal int Unknown3 { get; set; }
        internal int Unknown4 { get; set; }
        internal IdentityValue PlayfieldIdentity { get; set; }
        internal int CapturedPf2 { get; set; }
        internal IdentityValue PayloadBuilding { get; set; }
        internal byte[] GeneratorPayload { get; set; }
        internal string GeneratorPayloadSha256 { get; set; }
        internal PacketProvenance Provenance { get; set; }
    }

    internal sealed class TeleportRecord
    {
        internal IdentityValue PlayerIdentity { get; set; }
        internal Vector3Value Destination { get; set; }
        internal QuaternionValue Heading { get; set; }
        internal IdentityValue Building { get; set; }
        internal int GameServerId { get; set; }
        internal int SgId { get; set; }
        internal IdentityValue ChangePlayfield { get; set; }
        internal int CapturedPf2 { get; set; }
        internal IdentityValue Playfield2 { get; set; }
        internal PacketProvenance Provenance { get; set; }
    }

    internal sealed class AcceptedMissionRecord
    {
        internal string MissionType { get; set; }
        internal int MissionIcon { get; set; }
        internal int? MissionQuality { get; set; }
        internal string MissionQualityEvidenceField { get; set; }
        internal IdentityValue AcceptedQfuIdentity { get; set; }
        internal IdentityValue AcceptedQfuBuilding { get; set; }
        internal IdentityValue IssuingTerminal { get; set; }
        internal ExteriorEntranceRecord ExteriorEntrance { get; set; }
        internal IdentityValue MissionKeyIdentity { get; set; }
        internal string Title { get; set; }
        internal List<QuestActionIdentityRecord> QuestActionIdentities { get; set; }
        internal List<NamedIntValue> DecodedScalarFields { get; set; }
        internal PacketProvenance Provenance { get; set; }
    }

    internal sealed class QuestActionIdentityRecord
    {
        internal int ActionIndex { get; set; }
        internal string Field { get; set; }
        internal IdentityValue Identity { get; set; }
    }

    internal sealed class NamedIntValue
    {
        internal string Field { get; set; }
        internal int Value { get; set; }
    }

    internal sealed class ExteriorEntranceRecord
    {
        internal IdentityValue MissionBuildingReference { get; set; }
        internal IdentityValue ExteriorPlayfield { get; set; }
        internal Vector3Value Position { get; set; }
    }

    internal sealed class LayoutDynelRecord
    {
        internal string Category { get; set; }
        internal int Slot { get; set; }
        internal IdentityValue CapturedIdentity { get; set; }
        internal int CapturedPf2 { get; set; }
        internal bool CapturedPf2Known { get; set; }
        internal IdentityValue ParentIdentity { get; set; }
        internal Vector3Value Position { get; set; }
        internal QuaternionValue Heading { get; set; }
        internal int Template { get; set; }
        internal string Name { get; set; }
        internal string LayoutEligibility { get; set; }
        internal string RetargetingCategory { get; set; }
        internal List<StatValueRecord> Stats { get; set; }
        internal DoorDecodedFields DoorFields { get; set; }
        internal ItemDecodedFields ItemFields { get; set; }
        internal VendingDecodedFields VendingFields { get; set; }
        internal SimpleCharDecodedFields SimpleCharFields { get; set; }
        internal string EvidenceKind { get; set; }
        internal string EvidenceField { get; set; }
        internal IdentityValue EvidenceIdentity { get; set; }
        internal float? DistanceFromInteriorSpawn { get; set; }
        internal PacketProvenance Provenance { get; set; }
        internal PacketProvenance CorrelationProvenance { get; set; }
        internal PacketProvenance InteractionProvenance { get; set; }

        internal LayoutDynelRecord CloneAs(string category, string retargetingCategory)
        {
            return new LayoutDynelRecord
                       {
                           Category = category,
                           Slot = 0,
                           CapturedIdentity = this.CapturedIdentity,
                           CapturedPf2 = this.CapturedPf2,
                           CapturedPf2Known = this.CapturedPf2Known,
                           ParentIdentity = this.ParentIdentity,
                           Position = this.Position,
                           Heading = this.Heading,
                           Template = this.Template,
                           Name = this.Name,
                           LayoutEligibility = this.LayoutEligibility,
                           RetargetingCategory = retargetingCategory,
                           Stats = this.Stats,
                           DoorFields = this.DoorFields,
                           ItemFields = this.ItemFields,
                           VendingFields = this.VendingFields,
                           SimpleCharFields = this.SimpleCharFields,
                           EvidenceKind = this.EvidenceKind,
                           EvidenceField = this.EvidenceField,
                           EvidenceIdentity = this.EvidenceIdentity,
                           DistanceFromInteriorSpawn = this.DistanceFromInteriorSpawn,
                           Provenance = this.Provenance,
                           CorrelationProvenance = this.CorrelationProvenance,
                           InteractionProvenance = this.InteractionProvenance
                       };
        }
    }

    internal sealed class StatValueRecord
    {
        internal int Id { get; set; }
        internal string Name { get; set; }
        internal int Value { get; set; }
        internal int PacketOffset { get; set; }
    }

    internal sealed class DoorDecodedFields
    {
        internal int MessageVersion { get; set; }
        internal IdentityValue OwnerIdentity { get; set; }
        internal IdentityValue StateMachine { get; set; }
        internal byte Unknown2 { get; set; }
        internal byte Unknown3 { get; set; }
        internal List<StatValueRecord> Stats { get; set; }
        internal string Name { get; set; }
        internal int Unknown4 { get; set; }
        internal int Unknown5 { get; set; }
        internal List<IdentityValue> Identities { get; set; }
        internal int Unknown6 { get; set; }
        internal int Unknown7 { get; set; }
        internal string UndecodedTailHex { get; set; }
    }

    internal sealed class SimpleCharDecodedFields
    {
        internal int Level { get; set; }
        internal int Health { get; set; }
        internal int HealthDamage { get; set; }
        internal uint MonsterData { get; set; }
        internal int MonsterScale { get; set; }
        internal int? HeadMesh { get; set; }
        internal List<SimpleCharTextureRecord> Textures { get; set; }
        internal List<SimpleCharMeshRecord> Meshes { get; set; }
        internal int BytesConsumed { get; set; }
        internal bool DecodeFullyConsumed { get; set; }
        internal string UndecodedTailHex { get; set; }
    }

    internal sealed class ItemDecodedFields
    {
        internal int MessageVersion { get; set; }
        internal IdentityValue OwnerIdentity { get; set; }
        internal IdentityValue StateMachine { get; set; }
        internal int Unknown2 { get; set; }
        internal List<StatValueRecord> Stats { get; set; }
        internal string Name { get; set; }
        internal int? TrailingInt32 { get; set; }
        internal string UndecodedTailHex { get; set; }
    }

    internal sealed class VendingDecodedFields
    {
        internal int TypeIdentifier { get; set; }
        internal IdentityValue NpcIdentity { get; set; }
        internal int Unknown4 { get; set; }
        internal int Unknown5 { get; set; }
        internal short Unknown6 { get; set; }
        internal List<StatValueRecord> Stats { get; set; }
        internal string DisplayString { get; set; }
        internal int Unknown8 { get; set; }
        internal int? Unknown9 { get; set; }
        internal List<IdentityValue> Unknown10 { get; set; }
        internal int Unknown11 { get; set; }
        internal string UndecodedTailHex { get; set; }
    }

    internal sealed class SimpleCharTextureRecord
    {
        internal int Place { get; set; }
        internal int Id { get; set; }
        internal int Unknown { get; set; }
    }

    internal sealed class SimpleCharMeshRecord
    {
        internal int Position { get; set; }
        internal uint Id { get; set; }
        internal int OverrideTextureId { get; set; }
        internal int Layer { get; set; }
    }

    internal sealed class IdentityMappingRecord
    {
        internal string Category { get; set; }
        internal int Slot { get; set; }
        internal IdentityValue CapturedIdentity { get; set; }
        internal int CapturedPf2 { get; set; }
        internal IdentityValue ParentIdentity { get; set; }
        internal string RetargetingCategory { get; set; }
    }

    internal sealed class ExtractionIssue
    {
        internal string Code { get; set; }
        internal string Severity { get; set; }
        internal string Message { get; set; }
        internal string CaptureSession { get; set; }
        internal string SourceFile { get; set; }
        internal int CsvLine { get; set; }
        internal long GlobalOrdinal { get; set; }
        internal int Sequence { get; set; }
        internal string Direction { get; set; }
        internal string CapturedUtc { get; set; }
        internal string MessageType { get; set; }
        internal string PreservationStatus { get; set; }
        internal IdentityValue RawIdentity { get; set; }
        internal int RawPacketLength { get; set; }
        internal string RawPacketSha256 { get; set; }
        internal string RawPacketHex { get; set; }
        internal string RawSourceLine { get; set; }
    }

    internal sealed class AcgLayoutArtifact
    {
        internal string Schema { get; set; }
        internal int SchemaVersion { get; set; }
        internal string BundleId { get; set; }
        internal string CaptureSession { get; set; }
        internal string SourceFile { get; set; }
        internal AcceptedMissionRecord AcceptedMission { get; set; }
        internal TeleportRecord Teleport { get; set; }
        internal PlayfieldAnarchyFRecord PlayfieldAnarchyF { get; set; }
        internal Vector3Value InteriorSpawn { get; set; }
        internal LayoutDynelRecord Exit { get; set; }
        internal TeleportRecord ExitTeleport { get; set; }
        internal List<PacketProvenance> LifecycleEvidence { get; set; }
        internal List<LayoutDynelRecord> Doors { get; set; }
        internal List<LayoutDynelRecord> Chests { get; set; }
        internal List<LayoutDynelRecord> Terminals { get; set; }
        internal List<LayoutDynelRecord> NpcSlots { get; set; }
        internal List<LayoutDynelRecord> SimpleCharObservations { get; set; }
        internal List<LayoutDynelRecord> ObjectiveSlots { get; set; }
        internal List<LayoutDynelRecord> CharInPlay { get; set; }
        internal AcgLayoutSlots LayoutSlots { get; set; }
        internal List<IdentityMappingRecord> IdentityMappings { get; set; }
        internal List<ExtractionIssue> Issues { get; set; }
        internal string CompletenessStatus { get; set; }
        internal bool Selectable { get; set; }
    }

    internal sealed class AcgLayoutSlots
    {
        internal List<LayoutDynelRecord> Doors { get; set; }
        internal List<LayoutDynelRecord> Chests { get; set; }
        internal List<LayoutDynelRecord> Terminals { get; set; }
        internal List<LayoutDynelRecord> Npcs { get; set; }
        internal List<LayoutDynelRecord> Objectives { get; set; }
    }

    internal static class CanonicalJson
    {
        internal static string WriteArtifact(AcgLayoutArtifact artifact)
        {
            var writer = new CanonicalJsonWriter();
            WriteArtifact(writer, artifact);
            return writer.Finish();
        }

        internal static string WriteCorpus(
            IList<AcgLayoutArtifact> artifacts,
            IList<ExtractionIssue> failures)
        {
            var writer = new CanonicalJsonWriter();
            writer.BeginObject();
            writer.Property("schema", "ao-rebirth.mission-acg-layout-corpus");
            writer.Property("schemaVersion", 1);
            writer.PropertyName("artifacts");
            writer.BeginArray();
            foreach (AcgLayoutArtifact artifact in artifacts)
            {
                WriteArtifact(writer, artifact);
            }

            writer.EndArray();
            writer.PropertyName("failures");
            WriteIssues(writer, failures);
            writer.EndObject();
            return writer.Finish();
        }

        private static void WriteArtifact(CanonicalJsonWriter writer, AcgLayoutArtifact artifact)
        {
            writer.BeginObject();
            writer.Property("schema", artifact.Schema);
            writer.Property("schemaVersion", artifact.SchemaVersion);
            writer.Property("bundleId", artifact.BundleId);
            writer.Property("captureSession", artifact.CaptureSession);
            writer.Property("sourceFile", artifact.SourceFile);
            writer.PropertyName("acceptedMission");
            WriteAcceptedMission(writer, artifact.AcceptedMission);
            writer.PropertyName("teleport");
            WriteTeleport(writer, artifact.Teleport);
            writer.PropertyName("playfieldAnarchyF");
            WritePaf(writer, artifact.PlayfieldAnarchyF);
            writer.PropertyName("interiorSpawn");
            WriteVector(writer, artifact.InteriorSpawn);
            writer.PropertyName("exit");
            WriteDynel(writer, artifact.Exit);
            writer.PropertyName("exitTeleport");
            WriteTeleport(writer, artifact.ExitTeleport);
            writer.PropertyName("lifecycleEvidence");
            writer.BeginArray();
            foreach (PacketProvenance provenance in
                     artifact.LifecycleEvidence ?? new List<PacketProvenance>())
            {
                WriteProvenance(writer, provenance);
            }

            writer.EndArray();
            writer.PropertyName("doors");
            WriteDynels(writer, artifact.Doors);
            writer.PropertyName("chests");
            WriteDynels(writer, artifact.Chests);
            writer.PropertyName("terminals");
            WriteDynels(writer, artifact.Terminals);
            writer.PropertyName("npcSlots");
            WriteDynels(writer, artifact.NpcSlots);
            writer.PropertyName("simpleCharObservations");
            WriteDynels(writer, artifact.SimpleCharObservations);
            writer.PropertyName("objectiveSlots");
            WriteDynels(writer, artifact.ObjectiveSlots);
            writer.PropertyName("charInPlay");
            WriteDynels(writer, artifact.CharInPlay);
            writer.PropertyName("counts");
            writer.BeginObject();
            writer.Property("doorObservationCount", artifact.Doors.Count);
            writer.Property("chestObservationCount", artifact.Chests.Count);
            writer.Property("terminalObservationCount", artifact.Terminals.Count);
            writer.Property("simpleCharObservationCount", artifact.SimpleCharObservations.Count);
            writer.Property("npcObservationCount", artifact.NpcSlots.Count);
            writer.Property("objectiveObservationCount", artifact.ObjectiveSlots.Count);
            writer.Property("uniqueDoorSlotCount", artifact.LayoutSlots.Doors.Count);
            writer.Property("uniqueChestSlotCount", artifact.LayoutSlots.Chests.Count);
            writer.Property("uniqueTerminalSlotCount", artifact.LayoutSlots.Terminals.Count);
            writer.Property("uniqueNpcSlotCount", artifact.LayoutSlots.Npcs.Count);
            writer.Property("uniqueObjectiveSlotCount", artifact.LayoutSlots.Objectives.Count);
            writer.EndObject();
            writer.PropertyName("layoutSlots");
            writer.BeginObject();
            writer.PropertyName("doors");
            WriteDynels(writer, artifact.LayoutSlots.Doors);
            writer.PropertyName("chests");
            WriteDynels(writer, artifact.LayoutSlots.Chests);
            writer.PropertyName("terminals");
            WriteDynels(writer, artifact.LayoutSlots.Terminals);
            writer.PropertyName("npcs");
            WriteDynels(writer, artifact.LayoutSlots.Npcs);
            writer.PropertyName("objectives");
            WriteDynels(writer, artifact.LayoutSlots.Objectives);
            writer.EndObject();
            writer.PropertyName("identityMappings");
            writer.BeginArray();
            foreach (IdentityMappingRecord mapping in artifact.IdentityMappings)
            {
                writer.BeginObject();
                writer.Property("category", mapping.Category);
                writer.Property("slot", mapping.Slot);
                writer.PropertyName("capturedIdentity");
                WriteIdentity(writer, mapping.CapturedIdentity);
                writer.Property("capturedPf2", mapping.CapturedPf2);
                writer.Property("capturedPf2Hex", Hex(mapping.CapturedPf2));
                writer.PropertyName("capturedParentIdentity");
                WriteIdentity(writer, mapping.ParentIdentity);
                writer.Property("retargetingCategory", mapping.RetargetingCategory);
                writer.EndObject();
            }

            writer.EndArray();
            writer.PropertyName("issues");
            WriteIssues(writer, artifact.Issues);
            writer.Property("completenessStatus", artifact.CompletenessStatus);
            writer.Property("selectable", artifact.Selectable);
            writer.EndObject();
        }

        private static void WriteAcceptedMission(
            CanonicalJsonWriter writer,
            AcceptedMissionRecord mission)
        {
            if (mission == null)
            {
                writer.Null();
                return;
            }

            writer.BeginObject();
            writer.Property("missionType", mission.MissionType);
            writer.Property("missionIcon", mission.MissionIcon);
            writer.PropertyName("missionQuality");
            if (mission.MissionQuality.HasValue)
            {
                writer.Integer(mission.MissionQuality.Value);
            }
            else
            {
                writer.Null();
            }
            writer.Property("missionQualityEvidenceField", mission.MissionQualityEvidenceField);
            writer.PropertyName("acceptedQfuIdentity");
            WriteIdentity(writer, mission.AcceptedQfuIdentity);
            writer.PropertyName("acceptedQfuBuilding");
            WriteIdentity(writer, mission.AcceptedQfuBuilding);
            writer.PropertyName("issuingTerminal");
            WriteIdentity(writer, mission.IssuingTerminal);
            writer.PropertyName("exteriorEntrance");
            if (mission.ExteriorEntrance == null)
            {
                writer.Null();
            }
            else
            {
                writer.BeginObject();
                writer.PropertyName("missionBuildingReference");
                WriteIdentity(
                    writer,
                    mission.ExteriorEntrance.MissionBuildingReference);
                writer.PropertyName("exteriorPlayfield");
                WriteIdentity(writer, mission.ExteriorEntrance.ExteriorPlayfield);
                writer.PropertyName("position");
                WriteVector(writer, mission.ExteriorEntrance.Position);
                writer.EndObject();
            }

            writer.PropertyName("missionKeyIdentity");
            WriteIdentity(writer, mission.MissionKeyIdentity);
            writer.Property("title", mission.Title);
            writer.PropertyName("questActionIdentities");
            writer.BeginArray();
            foreach (QuestActionIdentityRecord actionIdentity in
                     mission.QuestActionIdentities ?? new List<QuestActionIdentityRecord>())
            {
                writer.BeginObject();
                writer.Property("actionIndex", actionIdentity.ActionIndex);
                writer.Property("field", actionIdentity.Field);
                writer.PropertyName("identity");
                WriteIdentity(writer, actionIdentity.Identity);
                writer.EndObject();
            }

            writer.EndArray();
            writer.PropertyName("decodedScalarFields");
            writer.BeginArray();
            foreach (NamedIntValue scalar in
                     mission.DecodedScalarFields ?? new List<NamedIntValue>())
            {
                writer.BeginObject();
                writer.Property("field", scalar.Field);
                writer.Property("value", scalar.Value);
                writer.Property("valueHex", Hex(scalar.Value));
                writer.EndObject();
            }

            writer.EndArray();
            writer.PropertyName("provenance");
            WriteProvenance(writer, mission.Provenance);
            writer.EndObject();
        }

        private static void WritePaf(
            CanonicalJsonWriter writer,
            PlayfieldAnarchyFRecord paf)
        {
            if (paf == null)
            {
                writer.Null();
                return;
            }

            writer.BeginObject();
            writer.PropertyName("identity");
            WriteIdentity(writer, paf.Identity);
            writer.Property("headerUnknown", paf.HeaderUnknown);
            writer.Property("unknown1", paf.Unknown1);
            writer.PropertyName("characterCoordinates");
            WriteVector(writer, paf.CharacterCoordinates);
            writer.Property("unknown2", paf.Unknown2);
            writer.PropertyName("building");
            WriteIdentity(writer, paf.Building);
            writer.Property("unknown3", paf.Unknown3);
            writer.Property("unknown4", paf.Unknown4);
            writer.PropertyName("playfieldIdentity");
            WriteIdentity(writer, paf.PlayfieldIdentity);
            writer.Property("capturedPf2", paf.CapturedPf2);
            writer.Property("capturedPf2Hex", Hex(paf.CapturedPf2));
            writer.PropertyName("payloadBuilding");
            WriteIdentity(writer, paf.PayloadBuilding);
            writer.Property("generatorPayloadLength", paf.GeneratorPayload == null ? 0 : paf.GeneratorPayload.Length);
            writer.Property(
                "generatorPayloadHex",
                paf.GeneratorPayload == null ? string.Empty : BytesToHex(paf.GeneratorPayload));
            writer.Property("generatorPayloadSha256", paf.GeneratorPayloadSha256);
            writer.PropertyName("provenance");
            WriteProvenance(writer, paf.Provenance);
            writer.EndObject();
        }

        private static void WriteTeleport(CanonicalJsonWriter writer, TeleportRecord teleport)
        {
            if (teleport == null)
            {
                writer.Null();
                return;
            }

            writer.BeginObject();
            writer.PropertyName("playerIdentity");
            WriteIdentity(writer, teleport.PlayerIdentity);
            writer.PropertyName("destination");
            WriteVector(writer, teleport.Destination);
            writer.PropertyName("heading");
            WriteQuaternion(writer, teleport.Heading);
            writer.PropertyName("building");
            WriteIdentity(writer, teleport.Building);
            writer.Property("gameServerId", teleport.GameServerId);
            writer.Property("sgId", teleport.SgId);
            writer.PropertyName("changePlayfield");
            WriteIdentity(writer, teleport.ChangePlayfield);
            writer.Property("capturedPf2", teleport.CapturedPf2);
            writer.Property("capturedPf2Hex", Hex(teleport.CapturedPf2));
            writer.PropertyName("playfield2");
            WriteIdentity(writer, teleport.Playfield2);
            writer.PropertyName("provenance");
            WriteProvenance(writer, teleport.Provenance);
            writer.EndObject();
        }

        private static void WriteDynels(
            CanonicalJsonWriter writer,
            IEnumerable<LayoutDynelRecord> records)
        {
            writer.BeginArray();
            if (records != null)
            {
                foreach (LayoutDynelRecord record in records)
                {
                    WriteDynel(writer, record);
                }
            }

            writer.EndArray();
        }

        private static void WriteDynel(CanonicalJsonWriter writer, LayoutDynelRecord record)
        {
            if (record == null)
            {
                writer.Null();
                return;
            }

            writer.BeginObject();
            writer.Property("category", record.Category);
            writer.Property("slot", record.Slot);
            writer.PropertyName("capturedIdentity");
            WriteIdentity(writer, record.CapturedIdentity);
            writer.PropertyName("capturedPf2");
            if (record.CapturedPf2Known)
            {
                writer.Integer(record.CapturedPf2);
            }
            else
            {
                writer.Null();
            }

            writer.Property(
                "capturedPf2Hex",
                record.CapturedPf2Known ? Hex(record.CapturedPf2) : null);
            writer.PropertyName("capturedParentIdentity");
            WriteIdentity(writer, record.ParentIdentity);
            writer.PropertyName("position");
            WriteVector(writer, record.Position);
            writer.PropertyName("heading");
            WriteQuaternion(writer, record.Heading);
            writer.Property("template", record.Template);
            writer.Property("name", record.Name ?? string.Empty);
            writer.Property("layoutEligibility", record.LayoutEligibility ?? string.Empty);
            writer.Property("retargetingCategory", record.RetargetingCategory);
            writer.PropertyName("stats");
            writer.BeginArray();
            if (record.Stats != null)
            {
                foreach (StatValueRecord stat in record.Stats.OrderBy(value => value.PacketOffset))
                {
                    writer.BeginObject();
                    writer.Property("id", stat.Id);
                    writer.Property("idHex", Hex(stat.Id));
                    writer.Property("name", stat.Name);
                    writer.Property("value", stat.Value);
                    writer.Property("packetOffset", stat.PacketOffset);
                    writer.EndObject();
                }
            }

            writer.EndArray();
            writer.PropertyName("doorFullUpdateFields");
            WriteDoorFields(writer, record.DoorFields);
            writer.PropertyName("itemFullUpdateFields");
            WriteItemFields(writer, record.ItemFields);
            writer.PropertyName("vendingMachineFullUpdateFields");
            WriteVendingFields(writer, record.VendingFields);
            writer.PropertyName("simpleCharFullUpdateFields");
            WriteSimpleCharFields(writer, record.SimpleCharFields);
            writer.Property("evidenceKind", record.EvidenceKind ?? string.Empty);
            writer.Property("evidenceField", record.EvidenceField ?? string.Empty);
            writer.PropertyName("evidenceIdentity");
            WriteIdentity(writer, record.EvidenceIdentity);
            writer.PropertyName("distanceFromInteriorSpawn");
            if (record.DistanceFromInteriorSpawn.HasValue)
            {
                writer.Float(record.DistanceFromInteriorSpawn.Value);
            }
            else
            {
                writer.Null();
            }
            writer.PropertyName("provenance");
            WriteProvenance(writer, record.Provenance);
            writer.PropertyName("correlationProvenance");
            WriteProvenance(writer, record.CorrelationProvenance);
            writer.PropertyName("interactionProvenance");
            WriteProvenance(writer, record.InteractionProvenance);
            writer.EndObject();
        }

        private static void WriteItemFields(
            CanonicalJsonWriter writer,
            ItemDecodedFields fields)
        {
            if (fields == null)
            {
                writer.Null();
                return;
            }

            writer.BeginObject();
            writer.Property("messageVersion", fields.MessageVersion);
            writer.PropertyName("ownerIdentity");
            WriteIdentity(writer, fields.OwnerIdentity);
            writer.PropertyName("stateMachine");
            WriteIdentity(writer, fields.StateMachine);
            writer.Property("unknown2", fields.Unknown2);
            writer.PropertyName("stats");
            writer.BeginArray();
            foreach (StatValueRecord stat in
                     fields.Stats ?? new List<StatValueRecord>())
            {
                writer.BeginObject();
                writer.Property("id", stat.Id);
                writer.Property("idHex", Hex(stat.Id));
                writer.Property("name", stat.Name);
                writer.Property("value", stat.Value);
                writer.Property("packetOffset", stat.PacketOffset);
                writer.EndObject();
            }

            writer.EndArray();
            writer.Property("name", fields.Name ?? string.Empty);
            writer.PropertyName("trailingInt32");
            if (fields.TrailingInt32.HasValue)
            {
                writer.Integer(fields.TrailingInt32.Value);
            }
            else
            {
                writer.Null();
            }

            writer.Property("undecodedTailHex", fields.UndecodedTailHex ?? string.Empty);
            writer.EndObject();
        }

        private static void WriteVendingFields(
            CanonicalJsonWriter writer,
            VendingDecodedFields fields)
        {
            if (fields == null)
            {
                writer.Null();
                return;
            }

            writer.BeginObject();
            writer.Property("typeIdentifier", fields.TypeIdentifier);
            writer.Property("typeIdentifierHex", Hex(fields.TypeIdentifier));
            writer.PropertyName("npcIdentity");
            WriteIdentity(writer, fields.NpcIdentity);
            writer.Property("unknown4", fields.Unknown4);
            writer.Property("unknown5", fields.Unknown5);
            writer.Property("unknown6", fields.Unknown6);
            writer.PropertyName("stats");
            writer.BeginArray();
            foreach (StatValueRecord stat in
                     fields.Stats ?? new List<StatValueRecord>())
            {
                writer.BeginObject();
                writer.Property("id", stat.Id);
                writer.Property("idHex", Hex(stat.Id));
                writer.Property("name", stat.Name);
                writer.Property("value", stat.Value);
                writer.Property("packetOffset", stat.PacketOffset);
                writer.EndObject();
            }

            writer.EndArray();
            writer.Property("displayString", fields.DisplayString ?? string.Empty);
            writer.Property("unknown8", fields.Unknown8);
            writer.PropertyName("unknown9");
            if (fields.Unknown9.HasValue)
            {
                writer.Integer(fields.Unknown9.Value);
            }
            else
            {
                writer.Null();
            }

            writer.PropertyName("unknown10");
            writer.BeginArray();
            foreach (IdentityValue identity in
                     fields.Unknown10 ?? new List<IdentityValue>())
            {
                WriteIdentity(writer, identity);
            }

            writer.EndArray();
            writer.Property("unknown11", fields.Unknown11);
            writer.Property(
                "undecodedTailHex",
                fields.UndecodedTailHex ?? string.Empty);
            writer.EndObject();
        }

        private static void WriteSimpleCharFields(
            CanonicalJsonWriter writer,
            SimpleCharDecodedFields fields)
        {
            if (fields == null)
            {
                writer.Null();
                return;
            }

            writer.BeginObject();
            writer.Property("level", fields.Level);
            writer.Property("health", fields.Health);
            writer.Property("healthDamage", fields.HealthDamage);
            writer.Property("monsterData", (long)fields.MonsterData);
            writer.Property("monsterDataHex", Hex(unchecked((int)fields.MonsterData)));
            writer.Property("monsterScale", fields.MonsterScale);
            writer.PropertyName("headMesh");
            if (fields.HeadMesh.HasValue)
            {
                writer.Integer(fields.HeadMesh.Value);
            }
            else
            {
                writer.Null();
            }

            writer.PropertyName("textures");
            writer.BeginArray();
            foreach (SimpleCharTextureRecord texture in
                     fields.Textures ?? new List<SimpleCharTextureRecord>())
            {
                writer.BeginObject();
                writer.Property("place", texture.Place);
                writer.Property("id", texture.Id);
                writer.Property("unknown", texture.Unknown);
                writer.EndObject();
            }

            writer.EndArray();
            writer.PropertyName("meshes");
            writer.BeginArray();
            foreach (SimpleCharMeshRecord mesh in
                     fields.Meshes ?? new List<SimpleCharMeshRecord>())
            {
                writer.BeginObject();
                writer.Property("position", mesh.Position);
                writer.Property("id", (long)mesh.Id);
                writer.Property("idHex", Hex(unchecked((int)mesh.Id)));
                writer.Property("overrideTextureId", mesh.OverrideTextureId);
                writer.Property("layer", mesh.Layer);
                writer.EndObject();
            }

            writer.EndArray();
            writer.Property("bytesConsumed", fields.BytesConsumed);
            writer.Property("decodeFullyConsumed", fields.DecodeFullyConsumed);
            writer.Property("undecodedTailHex", fields.UndecodedTailHex ?? string.Empty);
            writer.EndObject();
        }

        private static void WriteDoorFields(
            CanonicalJsonWriter writer,
            DoorDecodedFields fields)
        {
            if (fields == null)
            {
                writer.Null();
                return;
            }

            writer.BeginObject();
            writer.Property("messageVersion", fields.MessageVersion);
            writer.PropertyName("ownerIdentity");
            WriteIdentity(writer, fields.OwnerIdentity);
            writer.PropertyName("stateMachine");
            WriteIdentity(writer, fields.StateMachine);
            writer.Property("unknown2", fields.Unknown2);
            writer.Property("unknown3", fields.Unknown3);
            writer.PropertyName("stats");
            writer.BeginArray();
            foreach (StatValueRecord stat in fields.Stats.OrderBy(value => value.PacketOffset))
            {
                writer.BeginObject();
                writer.Property("id", stat.Id);
                writer.Property("idHex", Hex(stat.Id));
                writer.Property("name", stat.Name);
                writer.Property("value", stat.Value);
                writer.Property("packetOffset", stat.PacketOffset);
                writer.EndObject();
            }

            writer.EndArray();
            writer.Property("name", fields.Name ?? string.Empty);
            writer.Property("unknown4", fields.Unknown4);
            writer.Property("unknown4Hex", Hex(fields.Unknown4));
            writer.Property("unknown5", fields.Unknown5);
            writer.Property("unknown5Hex", Hex(fields.Unknown5));
            writer.PropertyName("identities");
            writer.BeginArray();
            foreach (IdentityValue identity in fields.Identities)
            {
                WriteIdentity(writer, identity);
            }

            writer.EndArray();
            writer.Property("unknown6", fields.Unknown6);
            writer.Property("unknown6Hex", Hex(fields.Unknown6));
            writer.Property("unknown7", fields.Unknown7);
            writer.Property("unknown7Hex", Hex(fields.Unknown7));
            writer.Property("undecodedTailHex", fields.UndecodedTailHex ?? string.Empty);
            writer.EndObject();
        }

        private static void WriteIdentity(CanonicalJsonWriter writer, IdentityValue identity)
        {
            if (identity == null)
            {
                writer.Null();
                return;
            }

            writer.BeginObject();
            writer.Property("type", identity.Type);
            writer.Property("typeHex", Hex(identity.Type));
            writer.Property("instance", identity.Instance);
            writer.Property("instanceHex", Hex(identity.Instance));
            writer.EndObject();
        }

        private static void WriteVector(CanonicalJsonWriter writer, Vector3Value value)
        {
            if (value == null)
            {
                writer.Null();
                return;
            }

            writer.BeginObject();
            writer.Property("x", value.X);
            writer.Property("y", value.Y);
            writer.Property("z", value.Z);
            writer.EndObject();
        }

        private static void WriteQuaternion(CanonicalJsonWriter writer, QuaternionValue value)
        {
            if (value == null)
            {
                writer.Null();
                return;
            }

            writer.BeginObject();
            writer.Property("x", value.X);
            writer.Property("y", value.Y);
            writer.Property("z", value.Z);
            writer.Property("w", value.W);
            writer.EndObject();
        }

        private static void WriteProvenance(
            CanonicalJsonWriter writer,
            PacketProvenance provenance)
        {
            if (provenance == null)
            {
                writer.Null();
                return;
            }

            writer.BeginObject();
            writer.Property("captureSession", provenance.CaptureSession ?? string.Empty);
            writer.Property("csvLine", provenance.CsvLine);
            writer.Property("globalOrdinal", provenance.GlobalOrdinal);
            writer.Property("sequence", provenance.Sequence);
            writer.Property("direction", provenance.Direction);
            writer.Property("capturedUtc", provenance.CapturedUtc);
            writer.Property("messageType", provenance.MessageType);
            writer.Property("preservationStatus", provenance.PreservationStatus);
            writer.Property("rawPacketLength", provenance.RawPacketLength);
            writer.Property("rawPacketSha256", provenance.RawPacketSha256);
            writer.Property("rawPacketHex", provenance.RawPacketHex);
            writer.Property("parseStatus", provenance.ParseStatus);
            writer.EndObject();
        }

        private static void WriteIssues(
            CanonicalJsonWriter writer,
            IEnumerable<ExtractionIssue> issues)
        {
            writer.BeginArray();
            if (issues != null)
            {
                foreach (ExtractionIssue issue in issues)
                {
                    writer.BeginObject();
                    writer.Property("code", issue.Code);
                    writer.Property("severity", issue.Severity);
                    writer.Property("message", issue.Message);
                    writer.Property("captureSession", issue.CaptureSession ?? string.Empty);
                    writer.Property("sourceFile", issue.SourceFile ?? string.Empty);
                    writer.Property("csvLine", issue.CsvLine);
                    writer.Property("globalOrdinal", issue.GlobalOrdinal);
                    writer.Property("sequence", issue.Sequence);
                    writer.Property("direction", issue.Direction ?? string.Empty);
                    writer.Property("capturedUtc", issue.CapturedUtc ?? string.Empty);
                    writer.Property("messageType", issue.MessageType ?? string.Empty);
                    writer.Property(
                        "preservationStatus",
                        issue.PreservationStatus ?? string.Empty);
                    writer.PropertyName("rawIdentity");
                    WriteIdentity(writer, issue.RawIdentity);
                    writer.Property("rawPacketLength", issue.RawPacketLength);
                    writer.Property(
                        "rawPacketSha256",
                        issue.RawPacketSha256 ?? string.Empty);
                    writer.Property("rawPacketHex", issue.RawPacketHex ?? string.Empty);
                    writer.Property("rawSourceLine", issue.RawSourceLine ?? string.Empty);
                    writer.EndObject();
                }
            }

            writer.EndArray();
        }

        private static string Hex(int value)
        {
            return "0x" + unchecked((uint)value).ToString("X8", CultureInfo.InvariantCulture);
        }

        private static string BytesToHex(byte[] bytes)
        {
            var builder = new StringBuilder(bytes.Length * 2);
            foreach (byte value in bytes)
            {
                builder.Append(value.ToString("X2", CultureInfo.InvariantCulture));
            }

            return builder.ToString();
        }
    }

    internal sealed class CanonicalJsonWriter
    {
        private readonly StringBuilder builder = new StringBuilder();
        private readonly Stack<Scope> scopes = new Stack<Scope>();
        private int indent;
        private bool propertyPending;

        internal void BeginObject()
        {
            this.BeforeValue();
            this.builder.Append('{');
            this.scopes.Push(new Scope { IsObject = true, First = true });
            this.indent++;
        }

        internal void EndObject()
        {
            this.indent--;
            Scope scope = this.scopes.Pop();
            if (!scope.First)
            {
                this.NewLine();
            }

            this.builder.Append('}');
            this.propertyPending = false;
        }

        internal void BeginArray()
        {
            this.BeforeValue();
            this.builder.Append('[');
            this.scopes.Push(new Scope { IsObject = false, First = true });
            this.indent++;
        }

        internal void EndArray()
        {
            this.indent--;
            Scope scope = this.scopes.Pop();
            if (!scope.First)
            {
                this.NewLine();
            }

            this.builder.Append(']');
            this.propertyPending = false;
        }

        internal void PropertyName(string name)
        {
            this.BeforeElement();
            this.WriteString(name);
            this.builder.Append(": ");
            this.propertyPending = true;
        }

        internal void Property(string name, string value)
        {
            this.PropertyName(name);
            if (value == null)
            {
                this.Null();
            }
            else
            {
                this.BeforeValue();
                this.WriteString(value);
            }
        }

        internal void Property(string name, int value)
        {
            this.PropertyName(name);
            this.Integer(value);
        }

        internal void Integer(int value)
        {
            this.BeforeValue();
            this.builder.Append(value.ToString(CultureInfo.InvariantCulture));
        }

        internal void Property(string name, long value)
        {
            this.PropertyName(name);
            this.BeforeValue();
            this.builder.Append(value.ToString(CultureInfo.InvariantCulture));
        }

        internal void Property(string name, float value)
        {
            this.PropertyName(name);
            this.Float(value);
        }

        internal void Float(float value)
        {
            if (float.IsNaN(value) || float.IsInfinity(value))
            {
                throw new InvalidDataException("Canonical JSON cannot contain a non-finite float.");
            }

            this.BeforeValue();
            this.builder.Append(value.ToString("R", CultureInfo.InvariantCulture));
        }

        internal void Property(string name, bool value)
        {
            this.PropertyName(name);
            this.BeforeValue();
            this.builder.Append(value ? "true" : "false");
        }

        internal void Null()
        {
            this.BeforeValue();
            this.builder.Append("null");
        }

        internal string Finish()
        {
            return this.builder.ToString() + "\n";
        }

        private void BeforeValue()
        {
            if (this.propertyPending)
            {
                this.propertyPending = false;
                return;
            }

            if (this.scopes.Count > 0 && !this.scopes.Peek().IsObject)
            {
                this.BeforeElement();
            }
        }

        private void BeforeElement()
        {
            Scope scope = this.scopes.Peek();
            if (!scope.First)
            {
                this.builder.Append(',');
            }

            scope.First = false;
            this.NewLine();
        }

        private void NewLine()
        {
            this.builder.Append('\n');
            this.builder.Append(' ', this.indent * 2);
        }

        private void WriteString(string value)
        {
            this.builder.Append('"');
            foreach (char current in value ?? string.Empty)
            {
                switch (current)
                {
                    case '"':
                        this.builder.Append("\\\"");
                        break;
                    case '\\':
                        this.builder.Append("\\\\");
                        break;
                    case '\b':
                        this.builder.Append("\\b");
                        break;
                    case '\f':
                        this.builder.Append("\\f");
                        break;
                    case '\n':
                        this.builder.Append("\\n");
                        break;
                    case '\r':
                        this.builder.Append("\\r");
                        break;
                    case '\t':
                        this.builder.Append("\\t");
                        break;
                    default:
                        if (current < 0x20)
                        {
                            this.builder.Append("\\u");
                            this.builder.Append(((int)current).ToString("X4", CultureInfo.InvariantCulture));
                        }
                        else
                        {
                            this.builder.Append(current);
                        }

                        break;
                }
            }

            this.builder.Append('"');
        }

        private sealed class Scope
        {
            internal bool IsObject { get; set; }
            internal bool First { get; set; }
        }
    }
}
