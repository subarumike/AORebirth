namespace SmokeLounge.AOtomation.Messaging.Tests
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;

    using AORebirth.Core.Playfields.OfficialPlacements;

    using Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestClass]
    public class AcgDevelopmentPlaceholderCatalogTests
    {
        private static readonly Lazy<AcgDevelopmentPlaceholderCatalog> SharedCatalog =
            new Lazy<AcgDevelopmentPlaceholderCatalog>(
                () => new AcgDevelopmentPlaceholderCatalog(CorpusRoot()));

        [TestMethod]
        public void CorpusAuditPreservesAllPinnedCountsDuplicatesAndMalformedBoundaries()
        {
            AcgDevelopmentPlaceholderCatalog catalog = SharedCatalog.Value;
            AcgDevelopmentPlaceholderCorpusAudit audit = catalog.AuditAllShards();

            Assert.AreEqual(
                "379e39cf3a2a697b5613316ff2a7da66a9d5f0ecc30d1b75efe0a4dffc7d093e",
                catalog.Manifest.PortablePackageSha256);
            Assert.AreEqual(630, catalog.Manifest.Metrics.EnumeratedResourceCount.Value);
            Assert.AreEqual(627, catalog.Manifest.Metrics.ParsedResourceCount.Value);
            Assert.AreEqual(459, catalog.Manifest.Metrics.PlayfieldsWithPlacements.Value);
            Assert.AreEqual(32805, audit.PrimaryRecordCount);
            Assert.AreEqual(32737, audit.AdditionalPointCount);
            Assert.AreEqual(65542, audit.TotalCoordinateCount);
            Assert.AreEqual(4016, audit.CapturePlanTargetCount);
            Assert.AreEqual(207, audit.Pf4582PrimaryRecordCount);
            Assert.AreEqual(9, audit.Pf4582FdqoPlacementCount);
            Assert.IsTrue(audit.Pf4582NcnnUnresolvedPresent);
            Assert.AreEqual(
                catalog.Manifest.Metrics.DuplicatePrimaryCoordinateRowCount.Value,
                audit.DuplicatePrimaryCoordinateRowCount);
            Assert.IsTrue(audit.DuplicatePrimaryCoordinateRowCount > 0);
            CollectionAssert.AreEqual(
                new[] { 103, 615, 4805 },
                catalog.Manifest.MalformedResources.Select(row => row.ResourceInstance.Value)
                    .OrderBy(value => value).ToArray());
        }

        [TestMethod]
        public void NativeVisualRegistryKeepsEvidenceGradesAndNonPrintableKeysSeparate()
        {
            AcgDevelopmentPlaceholderCatalog catalog = SharedCatalog.Value;
            AcgVisualResolution fdqo = catalog.GetVisual(0x4644514F);
            AcgVisualResolution uigu = catalog.GetVisual(0x55494755);
            AcgVisualResolution rpof = catalog.GetVisual(0x52504F46);
            AcgVisualResolution vawt = catalog.GetVisual(0x56415754);
            AcgVisualResolution variants = catalog.GetVisual(0x30315631);
            AcgVisualResolution spaces = catalog.GetVisual(0x20202020);
            AcgVisualResolution nonPrintable = catalog.GetVisual(0x9F9F9F9F);

            Assert.AreEqual("ExactOfficial", fdqo.EvidenceGrade);
            Assert.AreEqual(43296, fdqo.ServerTemplateId.Value);
            Assert.AreEqual("A004", fdqo.ServerTemplateHash);
            Assert.AreEqual(17655, fdqo.MonsterDataInstance.Value);
            Assert.AreEqual(15222, fdqo.ExactMeshInstance.Value);
            Assert.AreEqual("CaptureCorrelated", uigu.EvidenceGrade);
            CollectionAssert.AreEqual(new[] { 1578 }, uigu.AppearanceIds);
            CollectionAssert.AreEqual(new[] { "1010002:5907" }, uigu.MeshResourceIds);
            Assert.AreEqual("CaptureCorrelated", rpof.EvidenceGrade);
            CollectionAssert.AreEqual(new[] { 1576 }, rpof.AppearanceIds);
            CollectionAssert.AreEqual(new[] { "1010002:5907" }, rpof.MeshResourceIds);
            Assert.AreEqual("CaptureCorrelated", vawt.EvidenceGrade);
            CollectionAssert.AreEqual(new[] { 1576 }, vawt.AppearanceIds);
            CollectionAssert.AreEqual(new[] { "1010002:5907" }, vawt.MeshResourceIds);
            Assert.AreEqual("CaptureCorrelatedMultipleVariants", variants.EvidenceGrade);
            CollectionAssert.AreEqual(new[] { 1576, 1896 }, variants.AppearanceIds);
            CollectionAssert.AreEqual(
                new[] { "1010002:5907", "1010002:5941" },
                variants.MeshResourceIds);
            Assert.IsTrue(variants.AdditionalVariantUnresolved.Value);
            Assert.AreNotEqual(spaces.AcgHashNativeUInt32, nonPrintable.AcgHashNativeUInt32);
            Assert.AreNotEqual(spaces.AcgHashWireBytes, nonPrintable.AcgHashWireBytes);
            Assert.AreEqual(1, catalog.Manifest.Metrics.ExactOfficialCount.Value);
            Assert.AreEqual(4, catalog.Manifest.Metrics.CaptureCorrelatedCount.Value);
            Assert.AreEqual(4011, catalog.Manifest.Metrics.UnresolvedCount.Value);
        }

        [TestMethod]
        public void OffIsDefaultAndCreatesNoPlanOrShardLoad()
        {
            var environment = new Dictionary<string, string>();
            AcgDevelopmentPlaceholderOptions options =
                AcgDevelopmentPlaceholderOptions.Parse(
                    name => environment.ContainsKey(name) ? environment[name] : null,
                    true);
            var catalog = new AcgDevelopmentPlaceholderCatalog(CorpusRoot());

            Assert.AreEqual(AcgDevelopmentPlaceholderMode.Off, options.Mode);
            Assert.IsFalse(options.SelectedPlayfield.HasValue);
            Assert.AreEqual(0, catalog.CreatePlan(options, 4582).Count);
            Assert.AreEqual(0, catalog.LoadedPlayfields.Count);
        }

        [TestMethod]
        public void NonOffModesRequireOneSelectedPlayfieldAndADebugBuild()
        {
            var environment = new Dictionary<string, string>
            {
                { AcgDevelopmentPlaceholderOptions.ModeEnvironmentVariable, "CapturePlan" },
                { AcgDevelopmentPlaceholderOptions.PlayfieldEnvironmentVariable, "4582" }
            };

            AcgDevelopmentPlaceholderOptions enabled =
                AcgDevelopmentPlaceholderOptions.Parse(name => environment[name], true);
            Assert.AreEqual(AcgDevelopmentPlaceholderMode.CapturePlan, enabled.Mode);
            Assert.AreEqual(4582, enabled.SelectedPlayfield.Value);

            AssertThrows<InvalidOperationException>(
                () => AcgDevelopmentPlaceholderOptions.Parse(name => environment[name], false));
            environment.Remove(AcgDevelopmentPlaceholderOptions.PlayfieldEnvironmentVariable);
            AssertThrows<InvalidDataException>(
                () => AcgDevelopmentPlaceholderOptions.Parse(
                    name => environment.ContainsKey(name) ? environment[name] : null,
                    true));
        }

        [TestMethod]
        public void PrimaryAndCaptureModesLoadOnlyTheSelectedCurrentPlayfield()
        {
            var catalog = new AcgDevelopmentPlaceholderCatalog(CorpusRoot());
            var primaryOptions = new AcgDevelopmentPlaceholderOptions(
                AcgDevelopmentPlaceholderMode.CurrentPlayfieldPrimary,
                4582);

            Assert.AreEqual(0, catalog.CreatePlan(primaryOptions, 655).Count);
            Assert.AreEqual(0, catalog.LoadedPlayfields.Count);

            IList<AcgDevelopmentPlaceholderPlanEntry> primary =
                catalog.CreatePlan(primaryOptions, 4582);
            Assert.AreEqual(207, primary.Count);
            Assert.IsTrue(primary.All(row => row.LocationKind == AcgPlaceholderLocationKind.Primary));
            Assert.IsTrue(primary.All(row => !row.AdditionalPointOrdinal.HasValue));
            Assert.AreEqual(9, primary.Count(row => row.UseExactOfficialVisual));
            Assert.IsTrue(primary
                .Where(row => row.UseExactOfficialVisual)
                .All(row => row.SelectedCatMeshId == 15222));
            Assert.IsTrue(primary
                .Where(row => row.UseExactOfficialVisual)
                .All(row => !row.SelectedItemId.HasValue && !row.SelectedMeshId.HasValue));
            Assert.IsTrue(primary
                .Where(row => !row.UseExactOfficialVisual)
                .All(row => !row.SelectedCatMeshId.HasValue));
            Assert.IsTrue(primary
                .Where(row => !row.UseExactOfficialVisual)
                .All(row => row.SelectedItemId == 283862 && row.SelectedMeshId == 283882));
            Assert.IsTrue(primary
                .Where(row => !row.UseExactOfficialVisual)
                .All(row => row.SelectedVisualSource == "items.dat Item 283862 equipped-mesh stat 209"));
            CollectionAssert.AreEqual(new[] { 4582 }, catalog.LoadedPlayfields.ToArray());

            AcgDevelopmentPlaceholderManifestPlayfield capturePlayfield =
                catalog.Manifest.Playfields.First(row => row.CapturePlanTargetCount.Value > 0);
            var captureCatalog = new AcgDevelopmentPlaceholderCatalog(CorpusRoot());
            var captureOptions = new AcgDevelopmentPlaceholderOptions(
                AcgDevelopmentPlaceholderMode.CapturePlan,
                capturePlayfield.ResourceInstance.Value);
            IList<AcgDevelopmentPlaceholderPlanEntry> capture = captureCatalog.CreatePlan(
                captureOptions,
                capturePlayfield.ResourceInstance.Value);
            Assert.AreEqual(capturePlayfield.CapturePlanTargetCount.Value, capture.Count);
            CollectionAssert.AreEqual(
                new[] { capturePlayfield.ResourceInstance.Value },
                captureCatalog.LoadedPlayfields.ToArray());
        }

        [TestMethod]
        public void AllPointsPreservesParentAndOneBasedAdditionalOrdinals()
        {
            AcgDevelopmentPlaceholderCatalog catalog = SharedCatalog.Value;
            AcgDevelopmentPlaceholderManifestPlayfield playfield =
                catalog.Manifest.Playfields.First(row => row.AdditionalPointCount.Value > 0);
            var options = new AcgDevelopmentPlaceholderOptions(
                AcgDevelopmentPlaceholderMode.CurrentPlayfieldAllPoints,
                playfield.ResourceInstance.Value);

            IList<AcgDevelopmentPlaceholderPlanEntry> plan = catalog.CreatePlan(
                options,
                playfield.ResourceInstance.Value);
            Assert.AreEqual(
                playfield.PrimaryRecordCount.Value + playfield.AdditionalPointCount.Value,
                plan.Count);
            IList<AcgDevelopmentPlaceholderPlanEntry> additional = plan
                .Where(row => row.LocationKind == AcgPlaceholderLocationKind.AdditionalPoint)
                .ToList();
            Assert.AreEqual(playfield.AdditionalPointCount.Value, additional.Count);
            Assert.IsTrue(additional.All(row => row.AdditionalPointOrdinal.HasValue));
            Assert.IsTrue(additional.All(row => !string.IsNullOrWhiteSpace(row.OfficialSpawnRecordId)));
            Assert.IsTrue(additional.All(row => row.VisibleName.StartsWith("[ADD] ACG ", StringComparison.Ordinal)));
        }

        [TestMethod]
        public void EveryDevelopmentPlanEntryIsInertAndRetainsServerSideProvenance()
        {
            AcgDevelopmentPlaceholderCatalog catalog = SharedCatalog.Value;
            var options = new AcgDevelopmentPlaceholderOptions(
                AcgDevelopmentPlaceholderMode.CurrentPlayfieldPrimary,
                4582);
            IList<AcgDevelopmentPlaceholderPlanEntry> plan = catalog.CreatePlan(options, 4582);

            Assert.IsTrue(plan.Count > 0);
            foreach (AcgDevelopmentPlaceholderPlanEntry row in plan)
            {
                Assert.IsFalse(row.CanAttack);
                Assert.IsFalse(row.CanAggro);
                Assert.IsFalse(row.AwardsXp);
                Assert.IsFalse(row.ExposesLoot);
                Assert.IsTrue(row.Invulnerable);
                Assert.IsTrue(row.Stationary);
                Assert.IsTrue(row.Neutral);
                Assert.IsFalse(row.CollisionSuppressionProven);
                Assert.AreEqual("18.8.62_EP1", row.BuildId);
                Assert.AreEqual(1000014, row.ResourceType);
                Assert.AreEqual(4582, row.ResourceInstance);
                Assert.IsFalse(string.IsNullOrWhiteSpace(row.OfficialSpawnRecordId));
                Assert.IsFalse(string.IsNullOrWhiteSpace(row.AcgHashWireBytes));
                Assert.IsTrue(row.VisibleName.Length <= 31);
            }
        }

        [TestMethod]
        public void RuntimeWiringFailsClosedAndGuardsCombatDeathXpAndLegacyRouting()
        {
            string runtime = ReadRepositoryFile(
                "AORebirth",
                "Server",
                "ZoneEngine",
                "Core",
                "Playfields",
                "OfficialPlacements",
                "AcgDevelopmentPlaceholderRuntimeService.cs");
            string playfield = ReadRepositoryFile(
                "AORebirth",
                "Server",
                "ZoneEngine",
                "Core",
                "Playfields",
                "Playfield.cs");
            string attackHandler = ReadRepositoryFile(
                "AORebirth",
                "Server",
                "ZoneEngine",
                "Core",
                "MessageHandlers",
                "AttackMessageHandler.cs");
            string zoneServer = ReadRepositoryFile(
                "AORebirth",
                "Server",
                "ZoneEngine",
                "Core",
                "ZoneServer.cs");

            StringAssert.Contains(runtime, "if (options.IsOff)");
            StringAssert.Contains(runtime, "if (this.options.IsOff)");
            StringAssert.Contains(runtime, "return 0;");
            StringAssert.Contains(runtime, "DoNotDoTimers = true");
            StringAssert.Contains(runtime, "(uint)Side.Neutral");
            StringAssert.Contains(runtime, "(int)StatIds.catmesh");
            StringAssert.Contains(runtime, "(int)StatIds.displaycatmesh");
            StringAssert.Contains(runtime, "ItemLoader.ItemList.TryGetValue");
            StringAssert.Contains(runtime, "DefaultPlaceholderMeshStatId");
            StringAssert.Contains(runtime, "(uint)entry.SelectedCatMeshId.Value");
            StringAssert.Contains(runtime, "(int)StatIds.monsterdata");
            StringAssert.Contains(runtime, "character.MeshLayer.Clear()");
            StringAssert.Contains(runtime, "character.MeshLayer.AddMesh(");
            StringAssert.Contains(runtime, "entry.SelectedMeshId.Value");
            StringAssert.Contains(attackHandler, "AcgDevelopmentPlaceholderRuntimeRegistry.IsPlaceholder");
            StringAssert.Contains(playfield, "private void DoCombatTick(ICharacter attacker)");
            StringAssert.Contains(playfield, "private void KillNpcTarget(ICharacter attacker, ICharacter target)");
            StringAssert.Contains(playfield, "internal void HandleCombatKillingHit(ICharacter attacker, ICharacter target)");
            StringAssert.Contains(playfield, "internal void AwardCombatXp(ICharacter attacker, ICharacter target)");
            Assert.IsTrue(CountOccurrences(
                playfield,
                "AcgDevelopmentPlaceholderRuntimeRegistry.IsPlaceholder") >= 7);
            StringAssert.Contains(zoneServer, "PlayfieldHydrationMode.Legacy");
            StringAssert.Contains(zoneServer, "playfield.MaterializeAcgDevelopmentPlaceholders(");
        }

        [TestMethod]
        public void MalformedPlayfieldsFailClosedInsteadOfBecomingEmptySyntheticData()
        {
            foreach (int playfield in new[] { 103, 615, 4805 })
            {
                var catalog = new AcgDevelopmentPlaceholderCatalog(CorpusRoot());
                var options = new AcgDevelopmentPlaceholderOptions(
                    AcgDevelopmentPlaceholderMode.CurrentPlayfieldPrimary,
                    playfield);
                AssertThrows<InvalidDataException>(
                    () => catalog.CreatePlan(options, playfield));
            }
        }

        private static string CorpusRoot()
        {
            string repositoryRoot = TestRepositoryRootResolver.FindFromCallerFilePath();
            return Path.Combine(
                repositoryRoot,
                "docs",
                "generated",
                "acg_development_placeholders");
        }

        private static string ReadRepositoryFile(params string[] pathParts)
        {
            string path = TestRepositoryRootResolver.FindFromCallerFilePath();
            foreach (string part in pathParts)
            {
                path = Path.Combine(path, part);
            }

            return File.ReadAllText(path);
        }

        private static int CountOccurrences(string value, string token)
        {
            int count = 0;
            int offset = 0;
            while ((offset = value.IndexOf(token, offset, StringComparison.Ordinal)) >= 0)
            {
                count++;
                offset += token.Length;
            }

            return count;
        }

        private static void AssertThrows<TException>(Action action)
            where TException : Exception
        {
            try
            {
                action();
            }
            catch (TException)
            {
                return;
            }

            Assert.Fail("Expected exception " + typeof(TException).FullName + ".");
        }
    }
}
