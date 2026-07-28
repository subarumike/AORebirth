namespace SmokeLounge.AOtomation.Messaging.Tests
{
    using System;
    using System.Collections.Generic;

    using Microsoft.VisualStudio.TestTools.UnitTesting;

    using AORebirth.Core.Playfields;

    using ZoneEngine.Core.Missions;

    [TestClass]
    public class MissionAcgLayoutCatalogTests
    {
        private const int BuildingIdentityType = 0x0000C79F;
        private const int CapturedPlayerIdentityType = 0x0000C350;
        private const int CapturedPlayerInstance = 0x12345678;

        [TestMethod]
        public void BundleDefensivelyCopiesMutableInputsAndPacketAccessors()
        {
            const int SourcePlayfield2 = 1441800;
            int buildingInstance = BuildingInstanceFor(SourcePlayfield2);
            byte[] generatorPayload = CreateGeneratorPayload(buildingInstance);
            var provenance = new List<MissionAcgProvenanceRecord>(
                CreateProvenance("capture-a"));
            var door = CreateDynelRecord(
                MissionAcgWireCategory.Door,
                0,
                SourcePlayfield2,
                buildingInstance,
                provenance);
            var dynels = new List<MissionAcgDynelRecord> { door };
            var npcSlots = new List<MissionAcgNpcSlotRecord>
            {
                CreateNpcSlot(0, SourcePlayfield2, buildingInstance, provenance)
            };
            var objectiveSlots = new List<MissionAcgObjectiveSlotRecord>
            {
                CreateObjectiveSlot(
                    0,
                    SourcePlayfield2,
                    buildingInstance,
                    new[] { MissionRollType.KillPerson },
                    provenance)
            };
            var missionTypes = new List<MissionRollType> { MissionRollType.KillPerson };
            var compatibility = new MissionAcgCompatibilityRecord(1, 250, missionTypes);

            MissionAcgLayoutBundle bundle = CreateSelectableBundle(
                "layout-a",
                SourcePlayfield2,
                generatorPayload,
                dynels,
                npcSlots,
                objectiveSlots,
                compatibility,
                provenance);

            generatorPayload[0] = 0xFF;
            dynels.Clear();
            npcSlots.Clear();
            objectiveSlots.Clear();
            missionTypes.Clear();
            provenance.Clear();

            CollectionAssert.AreEqual(
                CreateGeneratorPayload(buildingInstance),
                bundle.CopyGeneratorPayload());
            Assert.AreEqual(1, bundle.Dynels.Count);
            Assert.AreEqual(1, bundle.WireRecords.Count);
            Assert.AreEqual(1, bundle.Doors.Count);
            Assert.AreEqual(1, bundle.NpcSlots.Count);
            Assert.AreEqual(1, bundle.ObjectiveSlots.Count);
            Assert.IsTrue(bundle.SupportsMission(MissionRollType.KillPerson, 42));
            Assert.AreEqual(1, bundle.Provenance.Count);

            AssertDefensiveCopy(
                bundle.CopyGeneratorPayload,
                "generator payload");
            AssertDefensiveCopy(
                door.Wire.CopyPacketBytes,
                "dynel packet");
            AssertDefensiveCopy(
                bundle.NpcSlots[0].CopyRawPacket,
                "NPC packet");
            AssertDefensiveCopy(
                bundle.ObjectiveSlots[0].CopyRawPacket,
                "objective packet");
            AssertDefensiveCopy(
                bundle.Exit.CopyRawPacket,
                "exit packet");
        }

        [TestMethod]
        public void InstanceBindingPreservesAllRuntimeIdentityAndExpiryFields()
        {
            MissionAcgLayoutBundle bundle = CreateSelectableBundle(
                "layout-binding",
                1441800,
                new[] { MissionRollType.FindPerson });
            var acceptedQuest = new MissionAcgIdentityRecord(0x10, 0x20);
            var ownerOrTeam = new MissionAcgIdentityRecord(0x11, 0x21);
            var missionKey = new MissionAcgIdentityRecord(0x12, 0x22);
            var exteriorEntrance = new MissionAcgIdentityRecord(0x13, 0x23);
            DateTime expiryUtc =
                new DateTime(2026, 7, 29, 0, 0, 0, DateTimeKind.Utc);

            MissionAcgInstanceBinding binding = MissionAcgInstanceBinding.Create(
                acceptedQuest,
                ownerOrTeam,
                MissionRollType.FindPerson,
                42,
                missionKey,
                exteriorEntrance,
                bundle,
                1500001,
                expiryUtc,
                12345);

            Assert.AreEqual(MissionAcgInstanceBinding.CurrentFormatVersion, binding.BindingFormatVersion);
            Assert.AreEqual(acceptedQuest, binding.AcceptedQuestIdentity);
            Assert.AreEqual(ownerOrTeam, binding.OwnerOrTeamIdentity);
            Assert.AreEqual(MissionRollType.FindPerson, binding.MissionType);
            Assert.AreEqual(42, binding.MissionQuality);
            Assert.AreEqual(missionKey, binding.MissionKeyIdentity);
            Assert.AreEqual(exteriorEntrance, binding.ExteriorEntranceIdentity);
            Assert.AreEqual("layout-binding", binding.SelectedBundleId);
            Assert.AreEqual(bundle.BuildingIdentity, binding.AcgBuildingIdentity);
            Assert.AreEqual(1500001, binding.AllocatedLivePlayfield2);
            Assert.AreEqual(expiryUtc, binding.ExpiryUtc);
            Assert.AreEqual(12345, binding.DeterministicSeed);
        }

        [TestMethod]
        public void CatalogSnapshotsInputsAndKeepsExplicitIncompleteShapeExcluded()
        {
            MissionAcgLayoutBundle bundle = CreateSelectableBundle(
                "layout-catalog",
                1441800,
                new[] { MissionRollType.FindItem });
            var layouts = new List<MissionAcgLayoutBundle> { bundle };
            var exclusions = new List<MissionAcgLayoutExclusion>
            {
                new MissionAcgLayoutExclusion(
                    "legacy-1441804",
                    MissionAcgLayoutCatalogLoader.ExplicitlyIncompleteShapePlayfield2,
                    "NPC shape only; generator and door evidence are absent.")
            };

            MissionAcgLayoutCatalog catalog =
                MissionAcgLayoutCatalogLoader.Load(layouts, exclusions);
            layouts.Clear();
            exclusions.Clear();

            Assert.AreEqual(1, catalog.Layouts.Count);
            Assert.AreEqual(1, catalog.SelectableLayouts.Count);
            Assert.AreSame(bundle, catalog.FindByLayoutId("LAYOUT-CATALOG"));
            Assert.AreSame(bundle, catalog.FindBySourcePlayfield2(1441800));
            Assert.IsNull(
                catalog.FindBySourcePlayfield2(
                    MissionAcgLayoutCatalogLoader.ExplicitlyIncompleteShapePlayfield2));
            Assert.AreEqual(1, catalog.Exclusions.Count);
            Assert.AreEqual(
                MissionAcgLayoutCatalogLoader.ExplicitlyIncompleteShapePlayfield2,
                catalog.Exclusions[0].SourcePlayfield2);
        }

        [TestMethod]
        public void CatalogValidationRejectsEmptyDuplicateAndIncompleteInputs()
        {
            MissionAcgCatalogValidationResult empty =
                MissionAcgLayoutCatalogLoader.Validate(
                    new MissionAcgLayoutBundle[0],
                    new MissionAcgLayoutExclusion[0]);
            AssertHasIssue(empty, MissionAcgCatalogValidationCode.EmptyCatalog);

            MissionAcgLayoutBundle first = CreateSelectableBundle(
                "duplicate",
                1441800,
                new[] { MissionRollType.KillPerson });
            MissionAcgLayoutBundle duplicateId = CreateSelectableBundle(
                "DUPLICATE",
                1443840,
                new[] { MissionRollType.KillPerson });
            MissionAcgCatalogValidationResult duplicateIdResult =
                MissionAcgLayoutCatalogLoader.Validate(
                    new[] { first, duplicateId },
                    new MissionAcgLayoutExclusion[0]);
            AssertHasIssue(
                duplicateIdResult,
                MissionAcgCatalogValidationCode.DuplicateLayoutId);

            MissionAcgLayoutBundle duplicatePlayfield = CreateSelectableBundle(
                "duplicate-playfield",
                1441800,
                new[] { MissionRollType.KillPerson });
            MissionAcgCatalogValidationResult duplicatePlayfieldResult =
                MissionAcgLayoutCatalogLoader.Validate(
                    new[] { first, duplicatePlayfield },
                    new MissionAcgLayoutExclusion[0]);
            AssertHasIssue(
                duplicatePlayfieldResult,
                MissionAcgCatalogValidationCode.DuplicateSourcePlayfield2);

            int incompletePlayfield =
                MissionAcgLayoutCatalogLoader.ExplicitlyIncompleteShapePlayfield2;
            MissionAcgLayoutBundle incompleteSelectable = CreateSelectableBundle(
                "incomplete-1441804",
                incompletePlayfield,
                new[] { MissionRollType.FindItem });
            MissionAcgCatalogValidationResult incompleteResult =
                MissionAcgLayoutCatalogLoader.Validate(
                    new[] { incompleteSelectable },
                    new MissionAcgLayoutExclusion[0]);
            AssertHasIssue(
                incompleteResult,
                MissionAcgCatalogValidationCode.IncompleteShapeSelectable);
        }

        [TestMethod]
        public void CatalogValidationRejectsTruncatedPayloadAndBuildingMismatch()
        {
            const int TruncatedPlayfield2 = 1441800;
            MissionAcgLayoutBundle truncated = CreateSelectableBundle(
                "truncated",
                TruncatedPlayfield2,
                new byte[] { 0x00, 0x00, 0xC7, 0x9F },
                CreateDynels(TruncatedPlayfield2),
                CreateNpcSlots(TruncatedPlayfield2),
                CreateObjectiveSlots(
                    TruncatedPlayfield2,
                    new[] { MissionRollType.RepairMachine }),
                new MissionAcgCompatibilityRecord(
                    1,
                    250,
                    new[] { MissionRollType.RepairMachine }),
                CreateProvenance("capture-truncated"));
            MissionAcgCatalogValidationResult truncatedResult =
                MissionAcgLayoutCatalogLoader.Validate(
                    new[] { truncated },
                    new MissionAcgLayoutExclusion[0]);
            AssertHasIssue(
                truncatedResult,
                MissionAcgCatalogValidationCode.InvalidGeneratorPayload);

            const int MismatchPlayfield2 = 1443840;
            byte[] mismatchedPayload =
                CreateGeneratorPayload(BuildingInstanceFor(MismatchPlayfield2));
            mismatchedPayload[7] ^= 0x01;
            MissionAcgLayoutBundle mismatch = CreateSelectableBundle(
                "building-mismatch",
                MismatchPlayfield2,
                mismatchedPayload,
                CreateDynels(MismatchPlayfield2),
                CreateNpcSlots(MismatchPlayfield2),
                CreateObjectiveSlots(
                    MismatchPlayfield2,
                    new[] { MissionRollType.RepairMachine }),
                new MissionAcgCompatibilityRecord(
                    1,
                    250,
                    new[] { MissionRollType.RepairMachine }),
                CreateProvenance("capture-building-mismatch"));
            MissionAcgCatalogValidationResult mismatchResult =
                MissionAcgLayoutCatalogLoader.Validate(
                    new[] { mismatch },
                    new MissionAcgLayoutExclusion[0]);
            AssertHasIssue(
                mismatchResult,
                MissionAcgCatalogValidationCode.BuildingIdentityConflict);
        }

        [TestMethod]
        public void CatalogLoadFailsClosedWhenNoValidLayoutExists()
        {
            AssertInvalidOperation(
                delegate
                {
                    MissionAcgLayoutCatalogLoader.Load(
                        new MissionAcgLayoutBundle[0],
                        new MissionAcgLayoutExclusion[0]);
                });
        }

        [TestMethod]
        public void SelectorIsStableForSameInputAndIndependentOfPoolOrdering()
        {
            MissionAcgLayoutBundle alpha = CreateSelectableBundle(
                "alpha",
                1441800,
                new[] { MissionRollType.KillPerson });
            MissionAcgLayoutBundle bravo = CreateSelectableBundle(
                "bravo",
                1443840,
                new[] { MissionRollType.KillPerson });
            MissionAcgLayoutBundle charlie = CreateSelectableBundle(
                "charlie",
                1460226,
                new[] { MissionRollType.KillPerson });
            MissionAcgLayoutCatalog catalog = MissionAcgLayoutCatalogLoader.Load(
                new[] { alpha, bravo, charlie },
                new MissionAcgLayoutExclusion[0]);
            var input = new MissionAcgSelectionInput(
                99,
                MissionRollType.KillPerson,
                42,
                new MissionAcgIdentityRecord(0x20, 0x30));

            MissionAcgLayoutBundle first = MissionAcgLayoutSelector.Select(catalog, input);
            MissionAcgLayoutBundle second = MissionAcgLayoutSelector.Select(catalog, input);
            MissionAcgLayoutBundle reordered = MissionAcgLayoutSelector.Select(
                new[] { charlie, alpha, bravo },
                input);

            Assert.AreSame(first, second);
            Assert.AreSame(first, reordered);
            Assert.IsTrue(first.IsSelectable);
            Assert.IsTrue(first.Completeness.IsSelectionComplete);
            Assert.IsTrue(first.SupportsMission(MissionRollType.KillPerson, 42));
        }

        [TestMethod]
        public void SelectorCanVaryAcrossSeedsButRemainsInsideAdmittedPool()
        {
            MissionAcgLayoutBundle alpha = CreateSelectableBundle(
                "alpha-seed",
                1441800,
                new[] { MissionRollType.FindPerson });
            MissionAcgLayoutBundle bravo = CreateSelectableBundle(
                "bravo-seed",
                1443840,
                new[] { MissionRollType.FindPerson });
            MissionAcgLayoutBundle charlie = CreateSelectableBundle(
                "charlie-seed",
                1460226,
                new[] { MissionRollType.FindPerson });
            MissionAcgLayoutCatalog catalog = MissionAcgLayoutCatalogLoader.Load(
                new[] { alpha, bravo, charlie },
                new MissionAcgLayoutExclusion[0]);
            var selectedIds = new HashSet<string>(StringComparer.Ordinal);

            for (int seed = 0; seed < 128; seed++)
            {
                MissionAcgLayoutBundle selected = MissionAcgLayoutSelector.Select(
                    catalog,
                    new MissionAcgSelectionInput(
                        seed,
                        MissionRollType.FindPerson,
                        42,
                        new MissionAcgIdentityRecord(0x20, 0x30)));
                selectedIds.Add(selected.LayoutId);
                Assert.IsNotNull(catalog.FindByLayoutId(selected.LayoutId));
            }

            Assert.IsTrue(
                selectedIds.Count > 1,
                "The explicit deterministic seed never varied selection across 128 inputs.");
        }

        [TestMethod]
        public void SelectorReturnsSingleEligibleBundleAndFailsClosedOtherwise()
        {
            MissionAcgLayoutBundle only = CreateSelectableBundle(
                "only",
                1441800,
                new[] { MissionRollType.FindItem });
            MissionAcgLayoutCatalog catalog = MissionAcgLayoutCatalogLoader.Load(
                new[] { only },
                new MissionAcgLayoutExclusion[0]);
            var eligible = new MissionAcgSelectionInput(
                1,
                MissionRollType.FindItem,
                42,
                new MissionAcgIdentityRecord(0x20, 0x30));

            Assert.AreSame(only, MissionAcgLayoutSelector.Select(catalog, eligible));

            AssertInvalidOperation(
                delegate
                {
                    MissionAcgLayoutSelector.Select(
                        new MissionAcgLayoutBundle[0],
                        eligible);
                });
            AssertInvalidOperation(
                delegate
                {
                    MissionAcgLayoutSelector.Select(
                        catalog,
                        new MissionAcgSelectionInput(
                            1,
                            MissionRollType.RepairMachine,
                            42,
                            new MissionAcgIdentityRecord(0x20, 0x30)));
                });
        }

        [TestMethod]
        public void GeneratorPayloadHashIsStableAndSensitiveToExactBytes()
        {
            MissionAcgLayoutBundle first = CreateSelectableBundle(
                "hash-a",
                1441800,
                new[] { MissionRollType.FindItemReturn });
            byte[] changedPayload = first.CopyGeneratorPayload();
            changedPayload[changedPayload.Length - 1] ^= 0x01;

            Assert.AreEqual(
                MissionAcgHash.ComputeSha256(first.CopyGeneratorPayload()),
                first.GeneratorPayloadSha256);
            Assert.AreNotEqual(
                first.GeneratorPayloadSha256,
                MissionAcgHash.ComputeSha256(changedPayload));
        }

        [TestMethod]
        public void LegacyFactoryKeepsEightStructuralBundlesNonSelectableAndExcludes1441804()
        {
            int[] expectedPlayfields =
            {
                1441800,
                1443840,
                1460226,
                1456133,
                1419310,
                1419335,
                1419382,
                1419349
            };

            MissionAcgLayoutCatalog catalog =
                MissionAcgLegacyLayoutCatalogFactory.Create(new MissionAcgLayoutBundle[0]);

            Assert.AreEqual(8, catalog.Layouts.Count);
            Assert.AreEqual(0, catalog.SelectableLayouts.Count);
            Assert.AreEqual(1, catalog.Exclusions.Count);
            Assert.AreEqual(
                MissionAcgLayoutCatalogLoader.ExplicitlyIncompleteShapePlayfield2,
                catalog.Exclusions[0].SourcePlayfield2);

            var hashes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < expectedPlayfields.Length; i++)
            {
                MissionAcgLayoutBundle layout =
                    catalog.FindBySourcePlayfield2(expectedPlayfields[i]);
                Assert.IsNotNull(layout, "Missing legacy PF2 " + expectedPlayfields[i] + ".");
                Assert.AreEqual(
                    MissionAcgLayoutCompletenessState.StructurallyCompleteObjectiveIncomplete,
                    layout.Completeness.State);
                Assert.IsFalse(layout.IsSelectable);
                Assert.IsFalse(layout.Completeness.IsSelectionComplete);
                Assert.IsTrue(layout.Doors.Count > 0);
                Assert.IsTrue(layout.Chests.Count > 0);
                Assert.IsTrue(layout.NpcSlots.Count > 0);
                Assert.AreEqual(0, layout.ObjectiveSlots.Count);
                Assert.IsNull(layout.Exit);
                Assert.IsFalse(layout.Completeness.HasLifecycleCorrelation);
                CollectionAssert.AreEqual(
                    MissionInstanceShapeCatalog.GetGeneratorPayload(expectedPlayfields[i]),
                    layout.CopyGeneratorPayload(),
                    "Legacy generator payload changed for PF2 " + expectedPlayfields[i] + ".");
                Assert.AreEqual(
                    MissionAcgHash.ComputeSha256(layout.CopyGeneratorPayload()),
                    layout.GeneratorPayloadSha256);
                Assert.IsTrue(
                    hashes.Add(layout.GeneratorPayloadSha256),
                    "Duplicate legacy generator hash for PF2 " + expectedPlayfields[i] + ".");
            }

            Assert.IsNull(
                catalog.FindBySourcePlayfield2(
                    MissionAcgLayoutCatalogLoader.ExplicitlyIncompleteShapePlayfield2));
        }

        [TestMethod]
        public void SelectorIgnoresLegacyStructuralBundlesAndUsesGeneratedSelectableBundle()
        {
            MissionAcgLayoutBundle generated = CreateSelectableBundle(
                "generated-complete",
                1500002,
                new[] { MissionRollType.RepairMachine });
            MissionAcgLayoutCatalog catalog =
                MissionAcgLegacyLayoutCatalogFactory.Create(new[] { generated });
            var input = new MissionAcgSelectionInput(
                77,
                MissionRollType.RepairMachine,
                42,
                new MissionAcgIdentityRecord(0x20, 0x30));

            MissionAcgLayoutBundle selected = MissionAcgLayoutSelector.Select(catalog, input);

            Assert.AreSame(generated, selected);
            Assert.AreEqual(9, catalog.Layouts.Count);
            Assert.AreEqual(1, catalog.SelectableLayouts.Count);
        }

        [TestMethod]
        public void SelectorFailsClosedForLegacyStructuralOnlyCatalog()
        {
            MissionAcgLayoutCatalog catalog =
                MissionAcgLegacyLayoutCatalogFactory.Create(new MissionAcgLayoutBundle[0]);

            AssertInvalidOperation(
                delegate
                {
                    MissionAcgLayoutSelector.Select(
                        catalog,
                        new MissionAcgSelectionInput(
                            77,
                            MissionRollType.KillPerson,
                            42,
                            new MissionAcgIdentityRecord(0x20, 0x30)));
                });
        }

        [TestMethod]
        public void DefaultCatalogLoadsFiveCapturedSelectableBundlesAndEightLegacyAuditBundles()
        {
            MissionAcgLayoutCatalog catalog = MissionAcgLegacyLayoutCatalogFactory.Create();
            int[] sourcePlayfield2s =
            {
                0x0015F008,
                0x0016C80E,
                0x00169802,
                0x0015F00F,
                0x0016700C
            };
            int[] buildingInstances =
            {
                0x00D734E2,
                0x00D6FC77,
                0x00D6FC78,
                0x00D734E5,
                0x00D734E7
            };
            string[] payloadHashes =
            {
                "ffe4327ac8af0f0a41a04cff7fe53ecd40c55a027f10a2cda2cd2a8fc18f1269",
                "f7f00e3344bd12f2d7d302761403c9c5b083fc8a181417c7f2c9748da501ff59",
                "3cfe53d3a32b50679530bdfd5ff7572405eb8865f4ab0c13308c7bcd935bf431",
                "e75f1326a72db6d42ddb5ebd72320338148193e6469e70b1c30b2d8a0f6d1926",
                "d5413273f69b018b66fcd6fe31bfa7be15b338cb6cb8fd17d83f7e14c4e4be82"
            };
            MissionRollType[] missionTypes =
            {
                MissionRollType.KillPerson,
                MissionRollType.FindItemReturn,
                MissionRollType.FindItem,
                MissionRollType.RepairMachine,
                MissionRollType.FindPerson
            };
            int[] objectiveIdentityTypes =
            {
                0x0000C350,
                0x0000C74A,
                0x0000C73D,
                0x0000C73D,
                0x0000C350
            };
            int[] objectiveIdentityInstances =
            {
                unchecked((int)0x79A16B61),
                unchecked((int)0x2586CCB1),
                unchecked((int)0x57AC07B0),
                unchecked((int)0x57A3C596),
                unchecked((int)0x79A16EB9)
            };
            int[] exitIdentityInstances =
            {
                unchecked((int)0x109AAC07),
                unchecked((int)0x109AD151),
                unchecked((int)0x109AC391),
                unchecked((int)0x109AB591),
                unchecked((int)0x109AACF8)
            };
            int[,] rawCounts =
            {
                { 23, 22, 0, 16, 15, 7 },
                { 64, 44, 0, 59, 58, 11 },
                { 59, 27, 0, 40, 39, 5 },
                { 26, 15, 0, 29, 28, 6 },
                { 56, 39, 0, 46, 45, 49 }
            };
            int[,] normalizedCounts =
            {
                { 11, 8, 0, 7, 1 },
                { 21, 15, 0, 21, 1 },
                { 27, 13, 0, 18, 1 },
                { 17, 12, 0, 19, 1 },
                { 14, 11, 0, 14, 1 }
            };

            Assert.AreEqual(13, catalog.Layouts.Count);
            Assert.AreEqual(5, catalog.SelectableLayouts.Count);
            for (int i = 0; i < sourcePlayfield2s.Length; i++)
            {
                MissionAcgLayoutBundle layout =
                    catalog.FindBySourcePlayfield2(sourcePlayfield2s[i]);
                Assert.IsNotNull(layout);
                Assert.AreEqual(
                    MissionAcgLayoutCompletenessState.CompleteSelectable,
                    layout.Completeness.State);
                Assert.IsTrue(layout.IsSelectable);
                Assert.AreEqual(BuildingIdentityType, layout.BuildingIdentity.Type);
                Assert.AreEqual(buildingInstances[i], layout.BuildingIdentity.Instance);
                Assert.IsTrue(
                    string.Equals(
                        payloadHashes[i],
                        layout.GeneratorPayloadSha256,
                        StringComparison.OrdinalIgnoreCase));
                Assert.IsTrue(
                    string.Equals(
                        payloadHashes[i],
                        layout.ExpectedGeneratorPayloadSha256,
                        StringComparison.OrdinalIgnoreCase));
                Assert.IsNotNull(layout.CaptureCounts);
                Assert.IsNotNull(layout.CapturedPlayerIdentity);
                Assert.AreEqual(CapturedPlayerIdentityType, layout.CapturedPlayerIdentity.Type);
                Assert.AreEqual(1, layout.ObjectiveSlots.Count);
                Assert.IsNotNull(layout.Exit);
                Assert.IsTrue(layout.SupportsMission(missionTypes[i], 1));
                Assert.AreEqual(
                    objectiveIdentityTypes[i],
                    layout.ObjectiveSlots[0].CapturedIdentity.Type);
                Assert.AreEqual(
                    objectiveIdentityInstances[i],
                    layout.ObjectiveSlots[0].CapturedIdentity.Instance);
                Assert.AreEqual(0x0000C748, layout.Exit.CapturedIdentity.Type);
                Assert.AreEqual(
                    exitIdentityInstances[i],
                    layout.Exit.CapturedIdentity.Instance);
                Assert.AreEqual(
                    rawCounts[i, 0],
                    layout.CaptureCounts.DoorObservationCount);
                Assert.AreEqual(
                    rawCounts[i, 1],
                    layout.CaptureCounts.ChestObservationCount);
                Assert.AreEqual(
                    rawCounts[i, 2],
                    layout.CaptureCounts.TerminalObservationCount);
                Assert.AreEqual(
                    rawCounts[i, 3],
                    layout.CaptureCounts.SimpleCharObservationCount);
                Assert.AreEqual(
                    rawCounts[i, 4],
                    layout.CaptureCounts.NpcObservationCount);
                Assert.AreEqual(
                    rawCounts[i, 5],
                    layout.CaptureCounts.ObjectiveObservationCount);
                Assert.AreEqual(
                    normalizedCounts[i, 0],
                    layout.CaptureCounts.NormalizedDoorSlotCount);
                Assert.AreEqual(
                    normalizedCounts[i, 1],
                    layout.CaptureCounts.NormalizedChestSlotCount);
                Assert.AreEqual(
                    normalizedCounts[i, 2],
                    layout.CaptureCounts.NormalizedTerminalSlotCount);
                Assert.AreEqual(
                    normalizedCounts[i, 3],
                    layout.CaptureCounts.NormalizedNpcSlotCount);
                Assert.AreEqual(
                    normalizedCounts[i, 4],
                    layout.CaptureCounts.NormalizedObjectiveSlotCount);
            }
        }

        [TestMethod]
        public void CatalogValidationRejectsUnsupportedFormatAndIndependentHashMismatch()
        {
            MissionAcgLayoutBundle valid = CreateSelectableBundle(
                "contract-base",
                1441800,
                new[] { MissionRollType.KillPerson });
            MissionAcgLayoutBundle unsupported = RebuildBundle(
                valid,
                MissionAcgLayoutBundle.CurrentFormatVersion + 1,
                valid.ExpectedGeneratorPayloadSha256,
                valid.Dynels,
                valid.NpcSlots,
                valid.ObjectiveSlots,
                valid.EntryPoint,
                valid.CapturedPlayerIdentity);
            MissionAcgCatalogValidationResult unsupportedResult =
                MissionAcgLayoutCatalogLoader.Validate(
                    new[] { unsupported },
                    new MissionAcgLayoutExclusion[0]);
            AssertHasIssue(
                unsupportedResult,
                MissionAcgCatalogValidationCode.BundleFormatConflict);

            MissionAcgLayoutBundle hashMismatch = RebuildBundle(
                valid,
                valid.BundleFormatVersion,
                new string('0', 64),
                valid.Dynels,
                valid.NpcSlots,
                valid.ObjectiveSlots,
                valid.EntryPoint,
                valid.CapturedPlayerIdentity);
            MissionAcgCatalogValidationResult hashResult =
                MissionAcgLayoutCatalogLoader.Validate(
                    new[] { hashMismatch },
                    new MissionAcgLayoutExclusion[0]);
            AssertHasIssue(
                hashResult,
                MissionAcgCatalogValidationCode.GeneratorHashMismatch);
        }

        [TestMethod]
        public void CatalogValidationRejectsWireIdentityPlayfieldAndRetargetMismatches()
        {
            const int SourcePlayfield2 = 1441800;
            int buildingInstance = BuildingInstanceFor(SourcePlayfield2);
            var parent =
                new MissionAcgIdentityRecord(BuildingIdentityType, buildingInstance);
            var capturedIdentity = new MissionAcgIdentityRecord(0x101, 1);
            var rawIdentity = new MissionAcgIdentityRecord(0x101, 99);

            MissionAcgDynelRecord identityMismatch = CreateValidationDynel(
                MissionAcgWireCategory.Door,
                SourcePlayfield2,
                buildingInstance,
                capturedIdentity,
                rawIdentity,
                SourcePlayfield2,
                SourcePlayfield2,
                CreateExpectedRetargetSlots(
                    capturedIdentity,
                    parent,
                    SourcePlayfield2));
            MissionAcgCatalogValidationResult identityResult =
                ValidateWithReplacementDoor(SourcePlayfield2, identityMismatch);
            AssertHasIssue(identityResult, MissionAcgCatalogValidationCode.WireConflict);
            AssertHasIssue(
                identityResult,
                MissionAcgCatalogValidationCode.StructuredRecordConflict);

            MissionAcgDynelRecord playfieldMismatch = CreateValidationDynel(
                MissionAcgWireCategory.Door,
                SourcePlayfield2,
                buildingInstance,
                capturedIdentity,
                capturedIdentity,
                SourcePlayfield2 + 1,
                SourcePlayfield2 + 1,
                CreateExpectedRetargetSlots(
                    capturedIdentity,
                    parent,
                    SourcePlayfield2 + 1));
            MissionAcgCatalogValidationResult playfieldResult =
                ValidateWithReplacementDoor(SourcePlayfield2, playfieldMismatch);
            AssertHasIssue(playfieldResult, MissionAcgCatalogValidationCode.WireConflict);
            AssertHasIssue(
                playfieldResult,
                MissionAcgCatalogValidationCode.StructuredRecordConflict);

            MissionAcgDynelRecord retargetMismatch = CreateValidationDynel(
                MissionAcgWireCategory.Door,
                SourcePlayfield2,
                buildingInstance,
                capturedIdentity,
                capturedIdentity,
                SourcePlayfield2,
                SourcePlayfield2,
                new[]
                {
                    new MissionAcgRetargetSlotRecord(
                        MissionAcgRetargetCategory.Playfield2Instance,
                        0,
                        69,
                        SourcePlayfield2)
                });
            MissionAcgCatalogValidationResult retargetResult =
                ValidateWithReplacementDoor(SourcePlayfield2, retargetMismatch);
            AssertHasIssue(
                retargetResult,
                MissionAcgCatalogValidationCode.RetargetConflict);
        }

        [TestMethod]
        public void GeometryRecordsRejectNonFiniteValuesAtConstruction()
        {
            AssertArgumentOutOfRange(
                delegate
                {
                    new MissionAcgPointRecord(float.NaN, 0.0f, 0.0f);
                });
            AssertArgumentOutOfRange(
                delegate
                {
                    new MissionAcgRotationRecord(
                        0.0f,
                        0.0f,
                        0.0f,
                        float.PositiveInfinity);
                });
        }

        [TestMethod]
        public void RuntimeContractsRejectOutOfDomainEnumValues()
        {
            AssertArgumentOutOfRange(
                delegate
                {
                    new MissionAcgCompatibilityRecord(
                        1,
                        250,
                        new[] { (MissionRollType)int.MaxValue });
                });
            AssertArgumentOutOfRange(
                delegate
                {
                    new MissionAcgSelectionInput(
                        1,
                        (MissionRollType)int.MaxValue,
                        1,
                        new MissionAcgIdentityRecord(1, 1));
                });
            AssertArgumentOutOfRange(
                delegate
                {
                    new MissionAcgCompletenessRecord(
                        (MissionAcgLayoutCompletenessState)int.MaxValue,
                        true,
                        true,
                        true,
                        true,
                        true,
                        true,
                        true,
                        true,
                        true);
                });
            AssertArgumentOutOfRange(
                delegate
                {
                    new MissionAcgWireRecord(
                        (MissionAcgWireCategory)int.MaxValue,
                        0,
                        "00",
                        null,
                        null,
                        null,
                        new MissionAcgRetargetSlotRecord[0]);
                });
            AssertArgumentOutOfRange(
                delegate
                {
                    new MissionAcgDynelRecord(
                        (MissionAcgWireCategory)int.MaxValue,
                        0,
                        null,
                        null,
                        null,
                        null,
                        null,
                        0,
                        string.Empty,
                        string.Empty,
                        new MissionAcgRetargetSlotRecord[0],
                        new MissionAcgProvenanceRecord[0]);
                });
            AssertArgumentOutOfRange(
                delegate
                {
                    new MissionAcgRetargetSlotRecord(
                        (MissionAcgRetargetCategory)int.MaxValue,
                        0,
                        0,
                        0);
                });
        }

        [TestMethod]
        public void CatalogValidationRejectsConflictingNpcObjectiveIdentityAlias()
        {
            const int SourcePlayfield2 = 1441800;
            int buildingInstance = BuildingInstanceFor(SourcePlayfield2);
            MissionAcgNpcSlotRecord npc = CreateNpcSlots(SourcePlayfield2)[0];
            MissionAcgIdentityRecord identity = npc.CapturedIdentity;
            var parent =
                new MissionAcgIdentityRecord(BuildingIdentityType, buildingInstance);
            string packetHex = CreateIdentityPacketHex(
                MissionAcgWireCategory.Unknown,
                identity,
                parent,
                SourcePlayfield2);
            var conflictingObjective = new MissionAcgObjectiveSlotRecord(
                0,
                new[] { MissionRollType.KillPerson },
                identity,
                SourcePlayfield2,
                parent,
                new MissionAcgPointRecord(70.0f, 80.0f, 90.0f),
                new MissionAcgRotationRecord(0.0f, 0.0f, 0.0f, 1.0f),
                4000,
                "Conflicting objective alias",
                packetHex,
                CreateRawPacketProvenance(packetHex));
            MissionAcgLayoutBundle bundle = CreateSelectableBundle(
                "conflicting-alias",
                SourcePlayfield2,
                CreateGeneratorPayload(buildingInstance),
                CreateDynels(SourcePlayfield2),
                new[] { npc },
                new[] { conflictingObjective },
                new MissionAcgCompatibilityRecord(
                    1,
                    250,
                    new[] { MissionRollType.KillPerson }),
                CreateProvenance("capture-conflicting-alias"));

            MissionAcgCatalogValidationResult result =
                MissionAcgLayoutCatalogLoader.Validate(
                    new[] { bundle },
                    new MissionAcgLayoutExclusion[0]);

            AssertHasIssue(
                result,
                MissionAcgCatalogValidationCode.StructuredRecordConflict);
        }

        private static MissionAcgLayoutBundle CreateSelectableBundle(
            string layoutId,
            int sourcePlayfield2,
            MissionRollType[] missionTypes)
        {
            int buildingInstance = BuildingInstanceFor(sourcePlayfield2);
            return CreateSelectableBundle(
                layoutId,
                sourcePlayfield2,
                CreateGeneratorPayload(buildingInstance),
                CreateDynels(sourcePlayfield2),
                CreateNpcSlots(sourcePlayfield2),
                CreateObjectiveSlots(sourcePlayfield2, missionTypes),
                new MissionAcgCompatibilityRecord(1, 250, missionTypes),
                CreateProvenance("capture-" + layoutId));
        }

        private static MissionAcgLayoutBundle CreateSelectableBundle(
            string layoutId,
            int sourcePlayfield2,
            byte[] generatorPayload,
            IEnumerable<MissionAcgDynelRecord> dynels,
            IEnumerable<MissionAcgNpcSlotRecord> npcSlots,
            IEnumerable<MissionAcgObjectiveSlotRecord> objectiveSlots,
            MissionAcgCompatibilityRecord compatibility,
            IEnumerable<MissionAcgProvenanceRecord> provenance)
        {
            int buildingInstance = BuildingInstanceFor(sourcePlayfield2);
            var dynelList = new List<MissionAcgDynelRecord>(dynels);
            var npcList = new List<MissionAcgNpcSlotRecord>(npcSlots);
            var objectiveList = new List<MissionAcgObjectiveSlotRecord>(objectiveSlots);
            int doorCount = 0;
            int chestCount = 0;
            int terminalCount = 0;
            for (int i = 0; i < dynelList.Count; i++)
            {
                switch (dynelList[i].Category)
                {
                    case MissionAcgWireCategory.Door:
                        doorCount++;
                        break;
                    case MissionAcgWireCategory.Chest:
                        chestCount++;
                        break;
                    case MissionAcgWireCategory.Terminal:
                        terminalCount++;
                        break;
                }
            }

            return new MissionAcgLayoutBundle(
                MissionAcgLayoutBundle.CurrentFormatVersion,
                layoutId,
                sourcePlayfield2,
                new MissionAcgIdentityRecord(BuildingIdentityType, buildingInstance),
                generatorPayload,
                MissionAcgHash.ComputeSha256(generatorPayload),
                new MissionAcgPointRecord(1.0f, 2.0f, 3.0f),
                CreateExit(sourcePlayfield2, buildingInstance, provenance),
                dynelList,
                npcList,
                objectiveList,
                new MissionAcgCaptureCountsRecord(
                    doorCount,
                    chestCount,
                    terminalCount,
                    npcList.Count + 1,
                    npcList.Count,
                    objectiveList.Count,
                    doorCount,
                    chestCount,
                    terminalCount,
                    npcList.Count,
                    objectiveList.Count),
                new MissionAcgIdentityRecord(
                    CapturedPlayerIdentityType,
                    CapturedPlayerInstance),
                compatibility,
                provenance,
                new MissionAcgCompletenessRecord(
                    MissionAcgLayoutCompletenessState.CompleteSelectable,
                    true,
                    true,
                    true,
                    true,
                    true,
                    true,
                    true,
                    true,
                    true),
                true,
                string.Empty);
        }

        private static MissionAcgLayoutBundle RebuildBundle(
            MissionAcgLayoutBundle source,
            int bundleFormatVersion,
            string expectedGeneratorPayloadSha256,
            IEnumerable<MissionAcgDynelRecord> dynels,
            IEnumerable<MissionAcgNpcSlotRecord> npcSlots,
            IEnumerable<MissionAcgObjectiveSlotRecord> objectiveSlots,
            MissionAcgPointRecord entryPoint,
            MissionAcgIdentityRecord capturedPlayerIdentity)
        {
            return new MissionAcgLayoutBundle(
                bundleFormatVersion,
                source.LayoutId,
                source.SourcePlayfield2,
                source.BuildingIdentity,
                source.CopyGeneratorPayload(),
                expectedGeneratorPayloadSha256,
                entryPoint,
                source.Exit,
                dynels,
                npcSlots,
                objectiveSlots,
                source.CaptureCounts,
                capturedPlayerIdentity,
                source.Compatibility,
                source.Provenance,
                source.Completeness,
                source.IsSelectable,
                source.SelectionExclusionReason);
        }

        private static MissionAcgCatalogValidationResult ValidateWithReplacementDoor(
            int sourcePlayfield2,
            MissionAcgDynelRecord replacementDoor)
        {
            int buildingInstance = BuildingInstanceFor(sourcePlayfield2);
            MissionAcgLayoutBundle bundle = CreateSelectableBundle(
                "invalid-wire-" + replacementDoor.Name,
                sourcePlayfield2,
                CreateGeneratorPayload(buildingInstance),
                new[]
                {
                    replacementDoor,
                    CreateDynelRecord(
                        MissionAcgWireCategory.Chest,
                        0,
                        sourcePlayfield2,
                        buildingInstance,
                        CreateProvenance("capture-valid-chest"))
                },
                CreateNpcSlots(sourcePlayfield2),
                CreateObjectiveSlots(
                    sourcePlayfield2,
                    new[] { MissionRollType.KillPerson }),
                new MissionAcgCompatibilityRecord(
                    1,
                    250,
                    new[] { MissionRollType.KillPerson }),
                CreateProvenance("capture-invalid-wire"));
            return MissionAcgLayoutCatalogLoader.Validate(
                new[] { bundle },
                new MissionAcgLayoutExclusion[0]);
        }

        private static MissionAcgDynelRecord CreateValidationDynel(
            MissionAcgWireCategory category,
            int sourcePlayfield2,
            int buildingInstance,
            MissionAcgIdentityRecord capturedIdentity,
            MissionAcgIdentityRecord rawIdentity,
            int capturedPlayfield2,
            int packetPlayfield2,
            IEnumerable<MissionAcgRetargetSlotRecord> retargetSlots)
        {
            var parent =
                new MissionAcgIdentityRecord(BuildingIdentityType, buildingInstance);
            string packetHex = CreateIdentityPacketHex(
                category,
                rawIdentity,
                parent,
                packetPlayfield2);
            return new MissionAcgDynelRecord(
                category,
                0,
                capturedIdentity,
                capturedPlayfield2,
                parent,
                new MissionAcgPointRecord(1.0f, 2.0f, 3.0f),
                new MissionAcgRotationRecord(0.0f, 0.0f, 0.0f, 1.0f),
                1000,
                category + "-validation",
                packetHex,
                retargetSlots,
                CreateRawPacketProvenance(packetHex));
        }

        private static MissionAcgRetargetSlotRecord[] CreateExpectedRetargetSlots(
            MissionAcgIdentityRecord identity,
            MissionAcgIdentityRecord parent,
            int sourcePlayfield2)
        {
            return new[]
            {
                new MissionAcgRetargetSlotRecord(
                    MissionAcgRetargetCategory.CharacterInstance,
                    0,
                    12,
                    CapturedPlayerInstance),
                new MissionAcgRetargetSlotRecord(
                    MissionAcgRetargetCategory.DynelIdentityType,
                    0,
                    20,
                    identity.Type),
                new MissionAcgRetargetSlotRecord(
                    MissionAcgRetargetCategory.DynelIdentityInstance,
                    0,
                    24,
                    identity.Instance),
                new MissionAcgRetargetSlotRecord(
                    MissionAcgRetargetCategory.ParentIdentityType,
                    0,
                    33,
                    parent.Type),
                new MissionAcgRetargetSlotRecord(
                    MissionAcgRetargetCategory.ParentIdentityInstance,
                    0,
                    37,
                    parent.Instance),
                new MissionAcgRetargetSlotRecord(
                    MissionAcgRetargetCategory.Playfield2Instance,
                    0,
                    69,
                    sourcePlayfield2)
            };
        }

        private static MissionAcgDynelRecord[] CreateDynels(int sourcePlayfield2)
        {
            int buildingInstance = BuildingInstanceFor(sourcePlayfield2);
            MissionAcgProvenanceRecord[] provenance =
                CreateProvenance("capture-dynels-" + sourcePlayfield2);
            return new[]
            {
                CreateDynelRecord(
                    MissionAcgWireCategory.Door,
                    0,
                    sourcePlayfield2,
                    buildingInstance,
                    provenance),
                CreateDynelRecord(
                    MissionAcgWireCategory.Chest,
                    0,
                    sourcePlayfield2,
                    buildingInstance,
                    provenance)
            };
        }

        private static MissionAcgNpcSlotRecord[] CreateNpcSlots(int sourcePlayfield2)
        {
            int buildingInstance = BuildingInstanceFor(sourcePlayfield2);
            return new[]
            {
                CreateNpcSlot(
                    0,
                    sourcePlayfield2,
                    buildingInstance,
                    CreateProvenance("capture-npc-" + sourcePlayfield2))
            };
        }

        private static MissionAcgObjectiveSlotRecord[] CreateObjectiveSlots(
            int sourcePlayfield2,
            MissionRollType[] missionTypes)
        {
            int buildingInstance = BuildingInstanceFor(sourcePlayfield2);
            return new[]
            {
                CreateObjectiveSlot(
                    0,
                    sourcePlayfield2,
                    buildingInstance,
                    missionTypes,
                    CreateProvenance("capture-objective-" + sourcePlayfield2))
            };
        }

        private static MissionAcgDynelRecord CreateDynelRecord(
            MissionAcgWireCategory category,
            int slot,
            int sourcePlayfield2,
            int buildingInstance,
            IEnumerable<MissionAcgProvenanceRecord> provenance)
        {
            var capturedIdentity =
                new MissionAcgIdentityRecord(0x100 + (int)category, slot + 1);
            var parentIdentity =
                new MissionAcgIdentityRecord(BuildingIdentityType, buildingInstance);
            string packetHex =
                CreateIdentityPacketHex(
                    category,
                    capturedIdentity,
                    parentIdentity,
                    sourcePlayfield2);

            return new MissionAcgDynelRecord(
                category,
                slot,
                capturedIdentity,
                sourcePlayfield2,
                parentIdentity,
                new MissionAcgPointRecord(slot + 1.0f, 2.0f, 3.0f),
                new MissionAcgRotationRecord(0.0f, 0.0f, 0.0f, 1.0f),
                1000 + slot,
                category + "-" + slot,
                packetHex,
                new[]
                {
                    new MissionAcgRetargetSlotRecord(
                        MissionAcgRetargetCategory.CharacterInstance,
                        0,
                        12,
                        CapturedPlayerInstance),
                    new MissionAcgRetargetSlotRecord(
                        MissionAcgRetargetCategory.DynelIdentityType,
                        0,
                        20,
                        capturedIdentity.Type),
                    new MissionAcgRetargetSlotRecord(
                        MissionAcgRetargetCategory.DynelIdentityInstance,
                        0,
                        24,
                        capturedIdentity.Instance),
                    new MissionAcgRetargetSlotRecord(
                        MissionAcgRetargetCategory.ParentIdentityType,
                        0,
                        33,
                        parentIdentity.Type),
                    new MissionAcgRetargetSlotRecord(
                        MissionAcgRetargetCategory.ParentIdentityInstance,
                        0,
                        37,
                        parentIdentity.Instance),
                    new MissionAcgRetargetSlotRecord(
                        MissionAcgRetargetCategory.Playfield2Instance,
                        0,
                        69,
                        sourcePlayfield2)
                },
                CreateRawPacketProvenance(packetHex));
        }

        private static MissionAcgNpcSlotRecord CreateNpcSlot(
            int slot,
            int sourcePlayfield2,
            int buildingInstance,
            IEnumerable<MissionAcgProvenanceRecord> provenance)
        {
            var identity = new MissionAcgIdentityRecord(0x200, slot + 1);
            var parent = new MissionAcgIdentityRecord(BuildingIdentityType, buildingInstance);
            return new MissionAcgNpcSlotRecord(
                slot,
                identity,
                sourcePlayfield2,
                parent,
                new MissionAcgPointRecord(4.0f, 5.0f, 6.0f),
                new MissionAcgRotationRecord(0.0f, 0.0f, 0.0f, 1.0f),
                2000 + slot,
                3000 + slot,
                "NPC-" + slot,
                "ordinary",
                CreateIdentityPacketHex(
                    MissionAcgWireCategory.Unknown,
                    identity,
                    parent,
                    sourcePlayfield2),
                CreateRawPacketProvenance(
                    CreateIdentityPacketHex(
                        MissionAcgWireCategory.Unknown,
                        identity,
                        parent,
                        sourcePlayfield2)));
        }

        private static MissionAcgObjectiveSlotRecord CreateObjectiveSlot(
            int slot,
            int sourcePlayfield2,
            int buildingInstance,
            IEnumerable<MissionRollType> missionTypes,
            IEnumerable<MissionAcgProvenanceRecord> provenance)
        {
            var identity = new MissionAcgIdentityRecord(0x300, slot + 1);
            var parent = new MissionAcgIdentityRecord(BuildingIdentityType, buildingInstance);
            return new MissionAcgObjectiveSlotRecord(
                slot,
                missionTypes,
                identity,
                sourcePlayfield2,
                parent,
                new MissionAcgPointRecord(7.0f, 8.0f, 9.0f),
                new MissionAcgRotationRecord(0.0f, 0.0f, 0.0f, 1.0f),
                4000 + slot,
                "Objective-" + slot,
                CreateIdentityPacketHex(
                    MissionAcgWireCategory.Unknown,
                    identity,
                    parent,
                    sourcePlayfield2),
                CreateRawPacketProvenance(
                    CreateIdentityPacketHex(
                        MissionAcgWireCategory.Unknown,
                        identity,
                        parent,
                        sourcePlayfield2)));
        }

        private static MissionAcgExitRecord CreateExit(
            int sourcePlayfield2,
            int buildingInstance,
            IEnumerable<MissionAcgProvenanceRecord> provenance)
        {
            var identity = new MissionAcgIdentityRecord(0x400, 1);
            var parent = new MissionAcgIdentityRecord(BuildingIdentityType, buildingInstance);
            return new MissionAcgExitRecord(
                identity,
                sourcePlayfield2,
                parent,
                new MissionAcgPointRecord(10.0f, 11.0f, 12.0f),
                new MissionAcgRotationRecord(0.0f, 0.0f, 0.0f, 1.0f),
                5000,
                "Exit",
                CreateIdentityPacketHex(
                    MissionAcgWireCategory.Unknown,
                    identity,
                    parent,
                    sourcePlayfield2),
                CreateRawPacketProvenance(
                    CreateIdentityPacketHex(
                        MissionAcgWireCategory.Unknown,
                        identity,
                        parent,
                        sourcePlayfield2)));
        }

        private static MissionAcgProvenanceRecord[] CreateProvenance(string captureId)
        {
            return new[]
            {
                new MissionAcgProvenanceRecord(
                    captureId,
                    "packets.hex.log:1",
                    "fixture")
            };
        }

        private static string CreateIdentityPacketHex(
            MissionAcgWireCategory category,
            MissionAcgIdentityRecord identity,
            MissionAcgIdentityRecord parent,
            int sourcePlayfield2)
        {
            var packet = new byte[87];
            packet[6] = (byte)(packet.Length >> 8);
            packet[7] = (byte)packet.Length;
            WriteInt32BigEndian(packet, 8, CapturedPlayerIdentityType);
            WriteInt32BigEndian(packet, 12, CapturedPlayerInstance);
            WriteInt32BigEndian(packet, 16, ExpectedN3Type(category));
            WriteInt32BigEndian(packet, 20, identity.Type);
            WriteInt32BigEndian(packet, 24, identity.Instance);
            WriteInt32BigEndian(packet, 33, parent.Type);
            WriteInt32BigEndian(packet, 37, parent.Instance);
            WriteSingleBigEndian(packet, 41, 1.0f);
            WriteSingleBigEndian(packet, 45, 2.0f);
            WriteSingleBigEndian(packet, 49, 3.0f);
            WriteSingleBigEndian(packet, 53, 0.0f);
            WriteSingleBigEndian(packet, 57, 0.0f);
            WriteSingleBigEndian(packet, 61, 0.0f);
            WriteSingleBigEndian(packet, 65, 1.0f);
            WriteInt32BigEndian(packet, 69, sourcePlayfield2);
            WriteInt32BigEndian(packet, 83, identity.Instance);
            return MissionAcgHash.ToHex(packet);
        }

        private static MissionAcgProvenanceRecord[] CreateRawPacketProvenance(
            string packetHex)
        {
            byte[] packet = MissionAcgHash.ParseHex(packetHex, "packetHex");
            return new[]
            {
                new MissionAcgProvenanceRecord(
                    "fixture",
                    "packets.hex.log:1",
                    "synthetic fixed-offset wire fixture",
                    1,
                    1,
                    1,
                    "ServerToClient",
                    "2026-07-28T00:00:00.0000000Z",
                    "fixture",
                    "raw_packet_preserved",
                    packet.Length,
                    MissionAcgHash.ComputeSha256(packet),
                    "parsed")
            };
        }

        private static int ExpectedN3Type(MissionAcgWireCategory category)
        {
            switch (category)
            {
                case MissionAcgWireCategory.Door:
                    return unchecked((int)0x365A5071);
                case MissionAcgWireCategory.Chest:
                    return unchecked((int)0x465A5D73);
                case MissionAcgWireCategory.Terminal:
                    return unchecked((int)0x3B11256F);
                default:
                    return unchecked((int)0x3F11256F);
            }
        }

        private static byte[] CreateGeneratorPayload(int buildingInstance)
        {
            var payload = new byte[8];
            WriteInt32BigEndian(payload, 0, BuildingIdentityType);
            WriteInt32BigEndian(payload, 4, buildingInstance);
            return payload;
        }

        private static int BuildingInstanceFor(int sourcePlayfield2)
        {
            return 0x01000000 + sourcePlayfield2;
        }

        private static void WriteInt32BigEndian(byte[] bytes, int offset, int value)
        {
            bytes[offset] = (byte)(value >> 24);
            bytes[offset + 1] = (byte)(value >> 16);
            bytes[offset + 2] = (byte)(value >> 8);
            bytes[offset + 3] = (byte)value;
        }

        private static void WriteSingleBigEndian(byte[] bytes, int offset, float value)
        {
            byte[] raw = BitConverter.GetBytes(value);
            if (BitConverter.IsLittleEndian)
            {
                bytes[offset] = raw[3];
                bytes[offset + 1] = raw[2];
                bytes[offset + 2] = raw[1];
                bytes[offset + 3] = raw[0];
                return;
            }

            bytes[offset] = raw[0];
            bytes[offset + 1] = raw[1];
            bytes[offset + 2] = raw[2];
            bytes[offset + 3] = raw[3];
        }

        private static void AssertHasIssue(
            MissionAcgCatalogValidationResult result,
            MissionAcgCatalogValidationCode code)
        {
            for (int i = 0; i < result.Issues.Count; i++)
            {
                if (result.Issues[i].Code == code)
                {
                    return;
                }
            }

            Assert.Fail("Expected catalog validation issue " + code + ".");
        }

        private static void AssertDefensiveCopy(Func<byte[]> copy, string label)
        {
            byte[] first = copy();
            Assert.IsTrue(first.Length > 0, label + " fixture is empty.");
            byte expected = first[0];
            first[0] ^= 0xFF;
            Assert.AreEqual(expected, copy()[0], label + " leaked mutable bytes.");
        }

        private static void AssertInvalidOperation(Action action)
        {
            try
            {
                action();
                Assert.Fail("Expected InvalidOperationException.");
            }
            catch (InvalidOperationException)
            {
            }
        }

        private static void AssertArgumentOutOfRange(Action action)
        {
            try
            {
                action();
                Assert.Fail("Expected ArgumentOutOfRangeException.");
            }
            catch (ArgumentOutOfRangeException)
            {
            }
        }
    }
}
