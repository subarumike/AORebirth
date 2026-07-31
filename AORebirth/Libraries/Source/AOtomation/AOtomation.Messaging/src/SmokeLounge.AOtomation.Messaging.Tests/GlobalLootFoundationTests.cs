namespace SmokeLounge.AOtomation.Messaging.Tests
{
    using System;
    using System.IO;
    using System.Linq;

    using AORebirth.Core.Playfields;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestClass]
    public class GlobalLootFoundationTests
    {
        [TestMethod]
        public void RegistryRejectsDuplicateMissingInvalidAndEvidenceUnsafeDefinitions()
        {
            LootTableRegistry registry = Registry();
            registry.RegisterTable(Table("valid", LootRollMode.Guaranteed, Guaranteed(1001), CreditsPolicyMode.None, 0, 0));
            AssertThrows<LootDefinitionValidationException>(() =>
                registry.RegisterTable(Table("valid", LootRollMode.Guaranteed, Guaranteed(1001), CreditsPolicyMode.None, 0, 0)));
            AssertThrows<LootDefinitionValidationException>(() => registry.RegisterAssignment(Assignment("missing", "missing", LootAssignmentTargetType.Global, null, 0)));
            AssertThrows<LootDefinitionValidationException>(() =>
                registry.RegisterTable(Table("unknown", LootRollMode.Guaranteed, Guaranteed(9999), CreditsPolicyMode.None, 0, 0)));

            LootEntryDefinition invalidQuality = Guaranteed(1001);
            invalidQuality.MinimumQuality = 5;
            invalidQuality.MaximumQuality = 4;
            AssertThrows<LootDefinitionValidationException>(() =>
                registry.RegisterTable(Table("quality", LootRollMode.Guaranteed, invalidQuality, CreditsPolicyMode.None, 0, 0)));

            LootEntryDefinition observedGuaranteed = Guaranteed(1001);
            observedGuaranteed.Semantics = LootSemantics.ObservedAvailable;
            observedGuaranteed.Evidence = LootEvidenceConfidence.ObservedAvailableLoot;
            AssertThrows<LootDefinitionValidationException>(() =>
                registry.RegisterTable(Table("observed", LootRollMode.Guaranteed, observedGuaranteed, CreditsPolicyMode.None, 0, 0)));
        }

        [TestMethod]
        public void GuaranteedIndependentWeightedQualityQuantityAndUniqueGenerationAreDeterministic()
        {
            LootTableRegistry registry = Registry();
            LootTableDefinition table = Table("modes", LootRollMode.Guaranteed, Guaranteed(1001), CreditsPolicyMode.Range, 4, 8);
            table.RollGroups = new[]
            {
                Group("guaranteed", LootRollMode.Guaranteed, 1, 10000, 0, Guaranteed(1001)),
                Group("independent", LootRollMode.Independent, 1, 10000, 0, Ranged(1002, 2, 5, 2, 4, 10000)),
                Group("weighted", LootRollMode.WeightedOne, 1, 10000, 0, Weighted(1003, 1), Weighted(1004, 3)),
                Group("unique", LootRollMode.WeightedMany, 4, 10000, 0, Unique(1005, 1))
            };
            registry.RegisterTable(table);
            registry.RegisterAssignment(Assignment("global", "modes", LootAssignmentTargetType.Global, null, 0));
            LootGenerationService service = Service(registry);
            LootGenerationContext context = Context();
            LootGenerationResult first = service.Generate(context, new SeededLootRandomSource(41));
            LootGenerationResult second = service.Generate(context, new SeededLootRandomSource(41));

            CollectionAssert.AreEqual(
                first.Items.Select(x => x.ItemTemplateId + ":" + x.Quality + ":" + x.Quantity).ToArray(),
                second.Items.Select(x => x.ItemTemplateId + ":" + x.Quality + ":" + x.Quantity).ToArray());
            Assert.AreEqual(first.Credits, second.Credits);
            Assert.AreEqual(1, first.Items.Count(x => x.ItemTemplateId == 1005));
            Assert.IsTrue(first.Items.Any(x => x.ItemTemplateId == 1002 && x.Quality >= 2 && x.Quality <= 5));
            Assert.IsTrue(first.Credits >= 4 && first.Credits <= 8);
        }

        [TestMethod]
        public void VergilObservedCorpseSnapshotsGenerateOnlyExactLinkedBundles()
        {
            LootTableDefinition table = BuildVergilObservedSnapshotTableForTest();
            Assert.AreEqual(0, table.RollGroups.Length);
            Assert.AreEqual(3, table.ObservedCorpseSnapshots.Length);
            Assert.IsTrue(table.ItemPoolUnresolved);
            Assert.AreEqual(CreditsPolicyMode.Unresolved, table.CreditsPolicy.Mode);
            Assert.AreEqual(LootEvidenceConfidence.Unresolved, table.CreditsPolicy.Evidence);
            Assert.IsTrue(table.ObservedCorpseSnapshots.All(
                value => value.SelectionProbabilityEvidence == LootEvidenceConfidence.Unresolved));

            var registry = new LootTableRegistry(value => value > 0);
            registry.RegisterTable(table);
            registry.RegisterAssignment(new LootAssignmentDefinition
            {
                AssignmentKey = "test.vergil.snapshots",
                TargetType = LootAssignmentTargetType.Boss,
                TargetKey = "test.vergil",
                LootTableKey = table.LootTableKey,
                PlayfieldId = 127,
                Priority = 0,
                Conditions = new string[0],
                Evidence = "test:vergil-observed-corpse-snapshots",
                Confidence = LootEvidenceConfidence.ProvenCapture,
                Enabled = true
            });
            var service = Service(registry);
            var context = new LootGenerationContext
            {
                EnemyProfileKey = "test.vergil",
                MonsterData = 203748,
                Level = 29,
                PlayfieldId = 127,
                IsBoss = true
            };
            string[][] expectedItems =
            {
                new[] { "301713:301713:1:1", "202743:202744:32:1", "287146:287146:200:1" },
                new[] { "301714:301714:1:1", "123571:123572:23:1", "287146:287146:200:1" },
                new[]
                {
                    "202734:202735:33:1",
                    "301715:301715:1:1",
                    "160051:160050:24:1",
                    "21605:21605:1:100",
                    "287146:287146:200:1"
                }
            };
            int[] expectedCredits = { 610, 587, 563 };

            for (int snapshotIndex = 0; snapshotIndex < expectedItems.Length; snapshotIndex++)
            {
                var random = new FixedIndexLootRandomSource(snapshotIndex);
                LootGenerationResult result = service.Generate(context, random);

                CollectionAssert.AreEqual(
                    expectedItems[snapshotIndex],
                    result.Items.Select(ItemSignature).ToArray(),
                    "A generated corpse must match one captured snapshot without cross-snapshot items.");
                Assert.AreEqual(expectedCredits[snapshotIndex], result.Credits);
                Assert.IsTrue(result.LootUnresolved);
                Assert.IsTrue(result.CreditsUnresolved);
                Assert.AreEqual(1, random.CallCount);
                Assert.AreEqual(3, random.RequestedMaximum);
                Assert.AreEqual(
                    1,
                    result.RollEvidence.Count(value => value.EntryTemplateId == 0
                        && value.Outcome.Contains("snapshot-selected:")));
            }

            LootGenerationResult capturedFiveItemSnapshot = service.Generate(
                context,
                new FixedIndexLootRandomSource(2));
            Assert.AreEqual(
                100,
                capturedFiveItemSnapshot.Items.Single(value => value.ItemTemplateId == 21605).Quantity);

            string root = FindRepositoryRoot();
            string globalLoot = File.ReadAllText(Path.Combine(
                root,
                @"AORebirth\Server\ZoneEngine\Core\Playfields\GlobalLootRuntimeService.cs"))
                .Replace("\r\n", "\n");
            Assert.IsTrue(
                globalLoot.Contains("ObservedCorpseSnapshot(\n                        \"capture.20260712-232711\",\n                        610,")
                && globalLoot.Contains("ObservedCorpseSnapshot(\n                        \"capture.20260712-234401\",\n                        587,")
                && globalLoot.Contains("ObservedCorpseSnapshot(\n                        \"capture.20260716-034433\",\n                        563,")
                && globalLoot.Contains("ObservedCorpseSnapshotEntry(\"capture.20260716-034433\", 202734, 202735, 33, 1)")
                && globalLoot.Contains("ObservedCorpseSnapshotEntry(\"capture.20260716-034433\", 301715, 301715, 1, 1)")
                && globalLoot.Contains("ObservedCorpseSnapshotEntry(\"capture.20260716-034433\", 160051, 160050, 24, 1)")
                && globalLoot.Contains("ObservedCorpseSnapshotEntry(\"capture.20260716-034433\", 21605, 21605, 1, 100)")
                && globalLoot.Contains("ObservedCorpseSnapshotEntry(\"capture.20260716-034433\", 287146, 287146, 200, 1)"),
                "Vergil runtime loot must retain all three exact linked observed corpse snapshots.");
        }

        [TestMethod]
        public void StrikeForemanObservedSnapshotsUseEnemyLevelWithinItemQlBounds()
        {
            LootTableDefinition table = BuildStrikeObservedSnapshotTableForTest();
            var registry = new LootTableRegistry(value => value > 0);
            registry.RegisterTable(table);
            registry.RegisterAssignment(new LootAssignmentDefinition
            {
                AssignmentKey = "test.strike.snapshots",
                TargetType = LootAssignmentTargetType.EnemyType,
                TargetKey = "test.strike",
                LootTableKey = table.LootTableKey,
                PlayfieldId = 127,
                Priority = 0,
                Conditions = new string[0],
                Evidence = "test:strike-observed-corpse-snapshots",
                Confidence = LootEvidenceConfidence.ProvenCapture,
                Enabled = true
            });
            var context = new LootGenerationContext
            {
                EnemyProfileKey = "test.strike",
                MonsterData = 203744,
                Level = 19,
                PlayfieldId = 127
            };
            LootGenerationService service = Service(registry);

            LootGenerationResult first = service.Generate(
                context,
                new FixedIndexLootRandomSource(0));
            Assert.AreEqual(176, first.Credits);
            Assert.AreEqual(
                10,
                first.Items.Single(value => value.ItemTemplateId == 27199).Quality);
            Assert.AreEqual(
                19,
                first.Items.Single(value => value.ItemTemplateId == 123744).Quality);
            Assert.AreEqual(
                1,
                first.Items.Single(value => value.ItemTemplateId == 301713).Quality);

            LootGenerationResult second = service.Generate(
                context,
                new FixedIndexLootRandomSource(1));
            Assert.AreEqual(176, second.Credits);
            Assert.AreEqual(
                19,
                second.Items.Single(value => value.ItemTemplateId == 85676).Quality);
            Assert.AreEqual(
                1,
                second.Items.Single(value => value.ItemTemplateId == 301707).Quality);
            Assert.IsTrue(table.ItemPoolUnresolved);
            Assert.IsTrue(table.ObservedCorpseSnapshots.All(
                value => value.SelectionProbabilityEvidence
                         == LootEvidenceConfidence.Unresolved));

            LootTableDefinition invalid = BuildStrikeObservedSnapshotTableForTest();
            invalid.ObservedCorpseSnapshots[0].Entries[1].FixedQuality = 19;
            AssertThrows<LootDefinitionValidationException>(() =>
                new LootTableRegistry(value => value > 0).RegisterTable(invalid));

            string globalLoot = File.ReadAllText(Path.Combine(
                FindRepositoryRoot(),
                @"AORebirth\Server\ZoneEngine\Core\Playfields\GlobalLootRuntimeService.cs"));
            Assert.IsTrue(
                globalLoot.Contains(
                    "CapturedStrikeForemanCredits = 176")
                && globalLoot.Contains(
                    "LevelBoundedObservedCorpseSnapshotEntry(")
                && globalLoot.Contains(
                    "\"capture.20260720-032106\"")
                && globalLoot.Contains(
                    "\"capture.20260720-033513\"")
                && globalLoot.Contains(
                    "\"captured-atomic-membership-enemy-level-bounded-item-ql\""),
                "Production must retain the two exact Strike Foreman atomic memberships while enemy level owns QL inside each item range.");
        }

        [TestMethod]
        public void ObservedCorpseSnapshotsRejectIndependentProbabilityDefinitions()
        {
            LootTableDefinition weightedEntry = BuildVergilObservedSnapshotTableForTest();
            weightedEntry.ObservedCorpseSnapshots[0].Entries[0].Weight = 1;
            AssertThrows<LootDefinitionValidationException>(() =>
                new LootTableRegistry(value => value > 0).RegisterTable(weightedEntry));

            LootTableDefinition independentCredits = BuildVergilObservedSnapshotTableForTest();
            independentCredits.CreditsPolicy = new CreditsPolicyDefinition
            {
                Mode = CreditsPolicyMode.ObservedSet,
                MinimumCredits = 563,
                MaximumCredits = 610,
                ObservedCredits = new[] { 563, 587, 610 },
                Evidence = LootEvidenceConfidence.ObservedAvailableLoot
            };
            AssertThrows<LootDefinitionValidationException>(() =>
                new LootTableRegistry(value => value > 0).RegisterTable(independentCredits));
        }

        [TestMethod]
        public void AretePartOneVariantLootRemainsNameScopedAndAtomic()
        {
            string source = File.ReadAllText(Path.Combine(
                FindRepositoryRoot(),
                @"AORebirth\Server\ZoneEngine\Core\Playfields\GlobalLootRuntimeService.cs"));

            int cleanmeisterNameGate = source.IndexOf(
                "this.EnsureAreteCleanmeister();",
                StringComparison.Ordinal);
            int supremeNameGate = source.IndexOf(
                "this.EnsureAreteSupremeCollector();",
                StringComparison.Ordinal);
            int malfunctioningNameGate = source.IndexOf(
                "this.EnsureAreteMalfunctioningRobot();",
                StringComparison.Ordinal);
            int sharedMonsterDataGate = source.IndexOf(
                "if (context.MonsterData == AlexWasteMonsterData)",
                StringComparison.Ordinal);

            Assert.IsTrue(cleanmeisterNameGate >= 0 && cleanmeisterNameGate < sharedMonsterDataGate);
            Assert.IsTrue(supremeNameGate >= 0 && supremeNameGate < sharedMonsterDataGate);
            Assert.IsTrue(malfunctioningNameGate >= 0 && malfunctioningNameGate < sharedMonsterDataGate);
            Assert.IsTrue(source.Contains("capture.20260722-104809.cleanmeister.7988C930"));
            Assert.IsTrue(source.Contains("capture.20260722-104809.cleanmeister.7988CAD3"));
            Assert.IsTrue(source.Contains("capture.20260722-104809.supreme-collector.79882C8F"));
            Assert.IsTrue(source.Contains("capture.20260722-104809.supreme-collector.7988CB09"));
            Assert.IsTrue(source.Contains("capture.20260722-104809.malfunctioning-robot.7988C9B9"));
            Assert.IsTrue(source.Contains("capture.20260722-104809.malfunctioning-robot.7988C9BF"));
            Assert.IsTrue(source.Contains("ObservedCorpseSnapshots = snapshots"));
            Assert.IsTrue(source.Contains("ItemPoolUnresolved = true"));
        }

        [TestMethod]
        public void Arete104809OrdinaryLootPreservesEveryIdentityLinkedAtomicSnapshot()
        {
            string source = File.ReadAllText(Path.Combine(
                FindRepositoryRoot(),
                @"AORebirth\Server\ZoneEngine\Core\Playfields\GlobalLootRuntimeService.cs"));
            string docker = SourceSlice(
                source,
                "internal static ObservedCorpseSnapshotDefinition[] BuildArete104809DockerSnapshots()",
                "private void EnsureAlexDocker()");
            string waste = SourceSlice(
                source,
                "internal static ObservedCorpseSnapshotDefinition[] BuildArete104809WasteCollectorSnapshots()",
                "private void EnsureAlexWasteCollector()");
            string flea = SourceSlice(
                source,
                "internal static ObservedCorpseSnapshotDefinition[] BuildArete104809GarbageFleaSnapshots()",
                "private void EnsureAlexGarbageFlea()");
            string cleaning = SourceSlice(
                source,
                "internal static ObservedCorpseSnapshotDefinition[] BuildArete104809CleaningRobotSnapshots()",
                "private void EnsureCleaningRobot()");
            string cleaningRegistration = SourceSlice(
                source,
                "private void EnsureCleaningRobot()",
                "private void EnsureNascenceBarkingChimera()");

            Assert.AreEqual(1, CountOccurrences(docker, "capture.20260722-104809.docker."));
            Assert.AreEqual(14, CountOccurrences(waste, "capture.20260722-104809.waste."));
            Assert.AreEqual(11, CountOccurrences(flea, "capture.20260722-104809.garbage-flea."));
            Assert.AreEqual(15, CountOccurrences(cleaning, "capture.20260722-104809.cleaning-robot."));
            Assert.IsFalse(source.Contains("capture.20260722.docker."));
            Assert.IsFalse(source.Contains("capture.20260722.waste."));
            Assert.IsFalse(source.Contains("capture.20260722.flea."));
            Assert.IsFalse(source.Contains("20260722-cap-mob-drop-cred"));
            Assert.IsTrue(docker.Contains("capture.20260722-104809.docker.7988284D"));
            Assert.IsTrue(docker.Contains("ObservedCorpseSnapshot(AretePartOneLootEvidence, key, 4)"));

            Assert.IsTrue(waste.Contains("capture.20260722-104809.waste.7988CADF"));
            Assert.IsTrue(waste.Contains("ObservedCorpseSnapshotEntry(e, eleventh, 248315, 248315, 1, 1)"));
            Assert.IsTrue(waste.Contains("ObservedCorpseSnapshotEntry(e, eleventh, 248319, 248319, 1, 1)"));
            Assert.IsTrue(waste.Contains("ObservedCorpseSnapshotEntry(e, eleventh, 42620, 42619, 2, 1)"));

            Assert.IsTrue(flea.Contains("capture.20260722-104809.garbage-flea.7988CAED"));
            Assert.IsTrue(flea.Contains("ObservedCorpseSnapshotEntry(e, seventh, 70560, 85688, 2, 1)"));
            Assert.IsTrue(flea.Contains("ObservedCorpseSnapshotEntry(e, seventh, 248322, 248322, 1, 1)"));

            Assert.IsTrue(cleaning.Contains("capture.20260722-104809.cleaning-robot.7988C84C"));
            Assert.IsTrue(cleaning.Contains("ObservedCorpseSnapshotEntry(e, eighth, 155685, 155685, 1, 1)"));
            Assert.IsTrue(cleaning.Contains("ObservedCorpseSnapshotEntry(e, eighth, 84144, 84144, 1, 1)"));
            Assert.IsTrue(cleaning.Contains("ObservedCorpseSnapshotEntry(e, eighth, 70559, 70559, 1, 1)"));
            Assert.IsTrue(cleaning.Contains("ObservedCorpseSnapshotEntry(e, eleventh, 70560, 70560, 1, 1)"));
            Assert.IsTrue(cleaning.Contains("ObservedCorpseSnapshotEntry(e, eleventh, 42620, 42620, 1, 1)"));
            Assert.IsFalse(cleaning.Contains("155666"));
            Assert.IsFalse(cleaning.Contains("84148"));
            Assert.IsFalse(cleaning.Contains("36783"));
            Assert.IsTrue(cleaningRegistration.Contains("BuildArete104809CleaningRobotSnapshots()"));
            Assert.IsFalse(cleaningRegistration.Contains("LootRollMode.WeightedOne"));
            Assert.IsFalse(cleaningRegistration.Contains("155666"));
            Assert.IsFalse(cleaningRegistration.Contains("84148"));
            Assert.IsFalse(cleaningRegistration.Contains("36783"));
        }

        [TestMethod]
        public void Arete152454BlankNameCorpseRowsRemainCorrelatedAndAtomic()
        {
            string source = File.ReadAllText(Path.Combine(
                FindRepositoryRoot(),
                @"AORebirth\Server\ZoneEngine\Core\Playfields\GlobalLootRuntimeService.cs"));
            string supreme = SourceSlice(
                source,
                "internal static ObservedCorpseSnapshotDefinition BuildArete152454ResolvedSupremeCollectorSnapshot()",
                "private void EnsureAreteSupremeCollector()");
            string gnarl = SourceSlice(
                source,
                "internal static ObservedCorpseSnapshotDefinition BuildArete152454ResolvedGnarlSnapshot()",
                "private void EnsureAreteGnarl()");

            Assert.IsTrue(supreme.Contains("capture.20260722-152454.supreme-collector.798911CF"));
            Assert.AreEqual(7, CountOccurrences(supreme, "ObservedCorpseSnapshotEntry("));
            Assert.IsTrue(supreme.Contains("key,\n                35,"));
            Assert.IsTrue(supreme.Contains("key, 70558, 85640, 5, 1"));
            Assert.IsTrue(supreme.Contains("key, 162497, 162497, 14, 1"));
            Assert.IsTrue(supreme.Contains("key, 201076, 201077, 5, 1"));

            Assert.IsTrue(gnarl.Contains("capture.20260722-152454.gnarl.79891585"));
            Assert.AreEqual(7, CountOccurrences(gnarl, "ObservedCorpseSnapshotEntry("));
            Assert.IsTrue(gnarl.Contains("key,\n                0,"));
            Assert.IsTrue(gnarl.Contains("key, 85548, 22258, 7, 1"));
            Assert.IsTrue(gnarl.Contains("key, 162715, 162715, 7, 1"));
            Assert.IsTrue(gnarl.Contains("key, 201139, 201140, 7, 1"));
            Assert.IsTrue(source.Contains(
                "SelectionProbabilityEvidence = LootEvidenceConfidence.Unresolved"));
        }

        [TestMethod]
        public void AretePartTwoLootRemainsPlayfieldScopedIdentityLinkedAndAtomic()
        {
            string source = File.ReadAllText(Path.Combine(
                FindRepositoryRoot(),
                @"AORebirth\Server\ZoneEngine\Core\Playfields\GlobalLootRuntimeService.cs"));

            Assert.IsTrue(source.Contains("private const int AretePlayfieldId = 6553;"));
            Assert.IsFalse(source.Contains("private const int AretePlayfieldId = 1044525;"));
            Assert.IsTrue(source.Contains("context.PlayfieldId == AretePlayfieldId"));
            Assert.IsTrue(source.Contains("context.MonsterData == AreteRollerratMonsterData"));
            Assert.IsTrue(source.Contains("context.MonsterData == AreteDesertReetMonsterData"));
            Assert.IsTrue(source.Contains("context.MonsterData == AreteAngryMinibullMonsterData"));
            Assert.IsTrue(source.Contains("this.EnsureAreteGnarl();"));
            Assert.IsTrue(source.Contains("this.EnsureAreteKneebreaker();"));
            Assert.IsTrue(source.Contains("capture.20260722-152454.rollerrat.798915D0"));
            Assert.IsTrue(source.Contains("capture.20260722-152454.desert-reet.798828E7"));
            Assert.IsTrue(source.Contains("capture.20260722-152454.angry-minibull.79891779"));
            Assert.IsTrue(source.Contains("capture.20260722-152454.gnarl.79891671"));
            Assert.IsTrue(source.Contains("capture.20260722-152454.kneebreaker.7989147B"));
            Assert.IsTrue(source.Contains("capture.20260722-152454.cleanmeister.798915E0"));
            Assert.IsTrue(source.Contains("capture.20260722-152454.supreme-collector.7989146B"));
            Assert.IsTrue(source.Contains("capture.20260722-152454.supreme-collector.798911CF"));
            Assert.IsTrue(source.Contains("capture.20260722-152454.gnarl.79891585"));
            Assert.IsTrue(source.Contains("capture.20260722-152454.waste.798913CD"));
            Assert.IsTrue(source.Contains("capture.20260722-152454.docker.798914DC"));
            Assert.IsTrue(source.Contains("RegisterObservedAretePartTwoTable("));
            Assert.IsTrue(source.Contains("ObservedCorpseSnapshots = snapshots"));
            Assert.IsTrue(source.Contains("ItemPoolUnresolved = true"));
        }

        [TestMethod]
        public void AssignmentPrecedenceAccumulatesStableGlobalFamilyEnemyDynaBossAndEncounterLayers()
        {
            LootTableRegistry registry = Registry();
            RegisterLayer(registry, "global", 1001, LootAssignmentTargetType.Global, null, 0);
            RegisterLayer(registry, "family", 1002, LootAssignmentTargetType.Family, "flea", 0);
            RegisterLayer(registry, "enemy", 1003, LootAssignmentTargetType.EnemyType, "filth-flea", 0);
            RegisterLayer(registry, "dyna", 1004, LootAssignmentTargetType.DynaGlobal, "any", 0);
            RegisterLayer(registry, "band", 1005, LootAssignmentTargetType.DynaLevelBand, "1-25", 0);
            RegisterLayer(registry, "boss", 1006, LootAssignmentTargetType.Boss, "filth-flea", 0);
            RegisterLayer(registry, "encounter", 1007, LootAssignmentTargetType.Encounter, "boss-room", 0);
            LootGenerationContext context = Context();
            context.FamilyKey = "flea";
            context.EnemyProfileKey = "filth-flea";
            context.IsDyna = true;
            context.IsBoss = true;
            context.DynaLevelBandKey = "1-25";
            context.EncounterKey = "boss-room";
            LootGenerationResult result = Service(registry).Generate(context, new SeededLootRandomSource(1));
            CollectionAssert.AreEqual(new[] { "global", "family", "enemy", "dyna", "band", "boss", "encounter" }, result.AppliedTableKeys.ToArray());
            CollectionAssert.AreEqual(new[] { 1001, 1002, 1003, 1004, 1005, 1006, 1007 }, result.Items.Select(x => x.ItemTemplateId).ToArray());
        }

        [TestMethod]
        public void NoAssignmentUnresolvedAndOwnedSummonPathsFailClosed()
        {
            LootTableRegistry registry = Registry();
            LootGenerationService service = Service(registry);
            LootGenerationResult missing = service.Generate(Context(), new SeededLootRandomSource(1));
            Assert.IsTrue(missing.LootUnresolved);
            Assert.AreEqual(0, missing.Items.Count);

            LootGenerationContext owned = Context();
            owned.IsOwnedSummon = true;
            LootGenerationResult summon = service.Generate(owned, new SeededLootRandomSource(1));
            Assert.IsFalse(summon.LootUnresolved);
            Assert.AreEqual(0, summon.Items.Count);
        }

        [TestMethod]
        public void CreditsNoneFixedRangeAndUnresolvedRemainDistinct()
        {
            AssertCredits(CreditsPolicyMode.None, 0, 0, 0, false);
            AssertCredits(CreditsPolicyMode.Fixed, 7, 7, 7, false);
            LootGenerationResult ranged = GenerateCredits(CreditsPolicyMode.Range, 3, 9);
            Assert.IsTrue(ranged.Credits >= 3 && ranged.Credits <= 9);
            LootGenerationResult unresolved = GenerateCredits(CreditsPolicyMode.Unresolved, 0, 0);
            Assert.IsTrue(unresolved.CreditsUnresolved);
        }

        [TestMethod]
        public void ObservedCreditSetsRemainUniqueWhileObservedSamplesPreserveMultiplicity()
        {
            LootTableRegistry registry = Registry();
            LootTableDefinition set = Table(
                "credits-set",
                LootRollMode.Guaranteed,
                Guaranteed(1001),
                CreditsPolicyMode.ObservedSet,
                1,
                2);
            set.CreditsPolicy.ObservedCredits = new[] { 2, 1, 1 };
            registry.RegisterTable(set);
            CollectionAssert.AreEqual(new[] { 1, 2 }, set.CreditsPolicy.ObservedCredits);

            LootTableDefinition samples = Table(
                "credits-samples",
                LootRollMode.Guaranteed,
                Guaranteed(1001),
                CreditsPolicyMode.ObservedSamples,
                1,
                2);
            samples.CreditsPolicy.ObservedCredits = new[] { 2, 1, 1 };
            registry.RegisterTable(samples);
            CollectionAssert.AreEqual(new[] { 1, 1, 2 }, samples.CreditsPolicy.ObservedCredits);
        }

        [TestMethod]
        public void ArchitectureGuardrailsKeepLootOwnershipOutOfPlayfieldAndEnemyBranches()
        {
            string root = FindRepositoryRoot();
            string playfield = File.ReadAllText(Path.Combine(root, @"AORebirth\Server\ZoneEngine\Core\Playfields\Playfield.cs"));
            string runtime = File.ReadAllText(Path.Combine(root, @"AORebirth\Server\ZoneEngine\Core\Playfields\OrdinaryEnemyRuntimeService.cs"));
            string corpseService = File.ReadAllText(Path.Combine(root, @"AORebirth\Server\ZoneEngine\Core\Playfields\CorpseInventoryService.cs"));
            Assert.IsFalse(playfield.Contains("RollCorpseLootItems") || playfield.Contains("GetDatabaseLootTable") || playfield.Contains("DebugLootTable"));
            Assert.IsTrue(playfield.Contains("GlobalLootRuntimeService.Generate"));
            Assert.IsFalse(runtime.Contains("AddItem(") || runtime.Contains("LootTableDefinition"));
            Assert.IsTrue(corpseService.Contains("CorpseState Create(")
                && corpseService.Contains("bool RemoveItem(")
                && corpseService.Contains("bool RemoveCredits(")
                && corpseService.Contains("int ClearPlayfield(")
                && corpseService.Contains("void ClearAll("));
            Assert.IsTrue(File.Exists(Path.Combine(root, @"docs\architecture\AO_REBIRTH_LOOT_ARCHITECTURE.md")));
        }

        private static LootTableRegistry Registry() { return new LootTableRegistry(value => value >= 1001 && value <= 1007); }
        private static LootGenerationService Service(LootTableRegistry registry) { return new LootGenerationService(registry, new LootAssignmentResolver()); }
        private static LootGenerationContext Context() { return new LootGenerationContext { EnemyProfileKey = "enemy", FamilyKey = "family", Level = 5, PlayfieldId = 127 }; }

        private static void RegisterLayer(LootTableRegistry registry, string key, int item, LootAssignmentTargetType type, string target, int priority)
        {
            registry.RegisterTable(Table(key, LootRollMode.Guaranteed, Guaranteed(item), CreditsPolicyMode.None, 0, 0));
            registry.RegisterAssignment(Assignment(key, key, type, target, priority));
        }

        private static LootTableDefinition Table(string key, LootRollMode mode, LootEntryDefinition entry, CreditsPolicyMode credits, int min, int max)
        {
            return new LootTableDefinition
            {
                LootTableKey = key, DisplayName = key, TableType = LootTableType.EnemyType,
                RollGroups = new[] { Group("group", mode, 1, 10000, 0, entry) },
                CreditsPolicy = new CreditsPolicyDefinition { Mode = credits, MinimumCredits = min, MaximumCredits = max, Evidence = LootEvidenceConfidence.ProvenRepository },
                QualityPolicy = "test", Evidence = "test", Confidence = LootEvidenceConfidence.ProvenRepository, Enabled = true
            };
        }

        private static LootGroupDefinition Group(string key, LootRollMode mode, int count, int chance, int empty, params LootEntryDefinition[] entries)
        {
            return new LootGroupDefinition { LootGroupKey = key, RollMode = mode, RollCount = count, DropChanceBasisPoints = chance, EmptyWeight = empty, Entries = entries, Conditions = new string[0] };
        }

        private static LootEntryDefinition Guaranteed(int id)
        {
            return new LootEntryDefinition { ItemTemplateId = id, HighItemTemplateId = id, FixedQuality = 1, MinimumQuality = 1, MaximumQuality = 1, MinimumQuantity = 1, MaximumQuantity = 1, Weight = 1, DropChanceBasisPoints = 10000, UniquePerCorpse = false, Semantics = LootSemantics.GuaranteedProven, Evidence = LootEvidenceConfidence.ProvenCapture, EvidenceReference = "test:guaranteed" };
        }

        private static LootEntryDefinition Ranged(int id, int minQl, int maxQl, int minQty, int maxQty, int chance)
        {
            return new LootEntryDefinition { ItemTemplateId = id, HighItemTemplateId = id, MinimumQuality = minQl, MaximumQuality = maxQl, MinimumQuantity = minQty, MaximumQuantity = maxQty, Weight = 1, DropChanceBasisPoints = chance, Semantics = LootSemantics.WeightedDocumented, Evidence = LootEvidenceConfidence.ProvenRepository, EvidenceReference = "test:weighted" };
        }

        private static LootEntryDefinition Weighted(int id, int weight) { LootEntryDefinition value = Guaranteed(id); value.Weight = weight; value.Semantics = LootSemantics.WeightedDocumented; return value; }
        private static LootEntryDefinition Unique(int id, int weight) { LootEntryDefinition value = Weighted(id, weight); value.UniquePerCorpse = true; return value; }
        private static LootAssignmentDefinition Assignment(string key, string table, LootAssignmentTargetType type, string target, int priority) { return new LootAssignmentDefinition { AssignmentKey = key, LootTableKey = table, TargetType = type, TargetKey = target, Priority = priority, Enabled = true, Evidence = "test", Confidence = LootEvidenceConfidence.ProvenRepository, Conditions = new string[0] }; }

        private static LootTableDefinition BuildVergilObservedSnapshotTableForTest()
        {
            return new LootTableDefinition
            {
                LootTableKey = "test.vergil.observed-corpse-snapshots",
                DisplayName = "Vergil observed corpse snapshots test",
                TableType = LootTableType.Boss,
                RollGroups = new LootGroupDefinition[0],
                ObservedCorpseSnapshots = new[]
                {
                    Snapshot(
                        "capture.20260712-232711",
                        610,
                        SnapshotEntry("capture.20260712-232711", 301713, 301713, 1, 1),
                        SnapshotEntry("capture.20260712-232711", 202743, 202744, 32, 1),
                        SnapshotEntry("capture.20260712-232711", 287146, 287146, 200, 1)),
                    Snapshot(
                        "capture.20260712-234401",
                        587,
                        SnapshotEntry("capture.20260712-234401", 301714, 301714, 1, 1),
                        SnapshotEntry("capture.20260712-234401", 123571, 123572, 23, 1),
                        SnapshotEntry("capture.20260712-234401", 287146, 287146, 200, 1)),
                    Snapshot(
                        "capture.20260716-034433",
                        563,
                        SnapshotEntry("capture.20260716-034433", 202734, 202735, 33, 1),
                        SnapshotEntry("capture.20260716-034433", 301715, 301715, 1, 1),
                        SnapshotEntry("capture.20260716-034433", 160051, 160050, 24, 1),
                        SnapshotEntry("capture.20260716-034433", 21605, 21605, 1, 100),
                        SnapshotEntry("capture.20260716-034433", 287146, 287146, 200, 1))
                },
                CreditsPolicy = new CreditsPolicyDefinition
                {
                    Mode = CreditsPolicyMode.Unresolved,
                    Evidence = LootEvidenceConfidence.Unresolved
                },
                QualityPolicy = "captured-observed-corpse-snapshots",
                Evidence = "test:vergil-observed-corpse-snapshots",
                Confidence = LootEvidenceConfidence.ObservedAvailableLoot,
                ItemPoolUnresolved = true,
                Enabled = true
            };
        }

        private static LootTableDefinition BuildStrikeObservedSnapshotTableForTest()
        {
            return new LootTableDefinition
            {
                LootTableKey = "test.strike.observed-corpse-snapshots",
                DisplayName = "Strike Foreman observed corpse snapshots test",
                TableType = LootTableType.EnemyType,
                RollGroups = new LootGroupDefinition[0],
                ObservedCorpseSnapshots = new[]
                {
                    Snapshot(
                        "capture.20260720-032106",
                        176,
                        LevelBoundedSnapshotEntry(
                            "capture.20260720-032106",
                            27199,
                            27199,
                            10,
                            10,
                            1),
                        LevelBoundedSnapshotEntry(
                            "capture.20260720-032106",
                            123744,
                            123745,
                            12,
                            21,
                            1),
                        LevelBoundedSnapshotEntry(
                            "capture.20260720-032106",
                            301713,
                            301713,
                            1,
                            1,
                            1)),
                    Snapshot(
                        "capture.20260720-033513",
                        176,
                        LevelBoundedSnapshotEntry(
                            "capture.20260720-033513",
                            85676,
                            22072,
                            1,
                            200,
                            1),
                        LevelBoundedSnapshotEntry(
                            "capture.20260720-033513",
                            301707,
                            301707,
                            1,
                            1,
                            1))
                },
                CreditsPolicy = new CreditsPolicyDefinition
                {
                    Mode = CreditsPolicyMode.Unresolved,
                    Evidence = LootEvidenceConfidence.Unresolved
                },
                QualityPolicy =
                    "captured-atomic-membership-enemy-level-bounded-item-ql",
                Evidence = "test:strike-observed-corpse-snapshots",
                Confidence = LootEvidenceConfidence.ObservedAvailableLoot,
                ItemPoolUnresolved = true,
                Enabled = true
            };
        }

        private static LootEntryDefinition LevelBoundedSnapshotEntry(
            string snapshotKey,
            int itemTemplateId,
            int highItemTemplateId,
            int minimumQuality,
            int maximumQuality,
            int quantity)
        {
            return new LootEntryDefinition
            {
                SelectionKey = snapshotKey,
                ItemTemplateId = itemTemplateId,
                HighItemTemplateId = highItemTemplateId,
                UsesEnemyLevelQuality = true,
                MinimumQuality = minimumQuality,
                MaximumQuality = maximumQuality,
                MinimumQuantity = quantity,
                MaximumQuantity = quantity,
                Weight = 0,
                DropChanceBasisPoints = 0,
                UniquePerCorpse = true,
                Semantics = LootSemantics.ObservedAvailable,
                Evidence = LootEvidenceConfidence.ObservedAvailableLoot,
                EvidenceReference = "test:" + snapshotKey,
                ProbabilityEvidence = "unresolved"
            };
        }

        private static ObservedCorpseSnapshotDefinition Snapshot(
            string snapshotKey,
            int credits,
            params LootEntryDefinition[] entries)
        {
            return new ObservedCorpseSnapshotDefinition
            {
                SnapshotKey = snapshotKey,
                Credits = credits,
                Entries = entries,
                Evidence = LootEvidenceConfidence.ProvenCapture,
                SelectionProbabilityEvidence = LootEvidenceConfidence.Unresolved,
                EvidenceReference = "test:" + snapshotKey
            };
        }

        private static LootEntryDefinition SnapshotEntry(
            string snapshotKey,
            int itemTemplateId,
            int highItemTemplateId,
            int quality,
            int quantity)
        {
            return new LootEntryDefinition
            {
                SelectionKey = snapshotKey,
                ItemTemplateId = itemTemplateId,
                HighItemTemplateId = highItemTemplateId,
                FixedQuality = quality,
                MinimumQuality = quality,
                MaximumQuality = quality,
                MinimumQuantity = quantity,
                MaximumQuantity = quantity,
                Weight = 0,
                DropChanceBasisPoints = 0,
                UniquePerCorpse = true,
                Semantics = LootSemantics.ObservedAvailable,
                Evidence = LootEvidenceConfidence.ObservedAvailableLoot,
                EvidenceReference = "test:" + snapshotKey,
                ProbabilityEvidence = "unresolved"
            };
        }

        private static string ItemSignature(GeneratedLootItem item)
        {
            return item.ItemTemplateId + ":" + item.HighItemTemplateId + ":" + item.Quality + ":" + item.Quantity;
        }

        private static LootGenerationResult GenerateCredits(CreditsPolicyMode mode, int min, int max)
        {
            LootTableRegistry registry = Registry();
            registry.RegisterTable(Table("credits", LootRollMode.Guaranteed, Guaranteed(1001), mode, min, max));
            registry.RegisterAssignment(Assignment("credits", "credits", LootAssignmentTargetType.Global, null, 0));
            return Service(registry).Generate(Context(), new SeededLootRandomSource(7));
        }

        private static void AssertCredits(CreditsPolicyMode mode, int min, int max, int expected, bool unresolved)
        {
            LootGenerationResult result = GenerateCredits(mode, min, max);
            Assert.AreEqual(expected, result.Credits);
            Assert.AreEqual(unresolved, result.CreditsUnresolved);
        }

        private static string SourceSlice(string source, string startMarker, string endMarker)
        {
            int start = source.IndexOf(startMarker, StringComparison.Ordinal);
            Assert.IsTrue(start >= 0, "Missing source marker: " + startMarker);
            int end = source.IndexOf(endMarker, start + startMarker.Length, StringComparison.Ordinal);
            Assert.IsTrue(end > start, "Missing source marker: " + endMarker);
            return source.Substring(start, end - start).Replace("\r\n", "\n");
        }

        private static int CountOccurrences(string source, string value)
        {
            int count = 0;
            int offset = 0;
            while ((offset = source.IndexOf(value, offset, StringComparison.Ordinal)) >= 0)
            {
                count++;
                offset += value.Length;
            }
            return count;
        }

        private static string FindRepositoryRoot()
        {
            DirectoryInfo current = new DirectoryInfo(AppDomain.CurrentDomain.BaseDirectory);
            while (current != null)
            {
                if (Directory.Exists(Path.Combine(current.FullName, "AORebirth")) && Directory.Exists(Path.Combine(current.FullName, "docs"))) return current.FullName;
                current = current.Parent;
            }
            throw new DirectoryNotFoundException("Repository root not found.");
        }

        private static void AssertThrows<TException>(Action action) where TException : Exception
        {
            try
            {
                action();
            }
            catch (TException)
            {
                return;
            }
            Assert.Fail("Expected exception " + typeof(TException).Name + ".");
        }

        private sealed class FixedIndexLootRandomSource : ILootRandomSource
        {
            private readonly int index;

            internal FixedIndexLootRandomSource(int index)
            {
                this.index = index;
            }

            internal int CallCount { get; private set; }
            internal int RequestedMaximum { get; private set; }

            public int Next(int maximumExclusive)
            {
                this.CallCount++;
                this.RequestedMaximum = maximumExclusive;
                if (this.index < 0 || this.index >= maximumExclusive)
                {
                    throw new ArgumentOutOfRangeException("maximumExclusive");
                }
                return this.index;
            }
        }
    }
}
