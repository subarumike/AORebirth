namespace SmokeLounge.AOtomation.Messaging.Tests
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Web.Script.Serialization;

    using AORebirth.Core.Playfields.OfficialPlacements;

    using Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestClass]
    public class OfficialPlayfieldPlacementCatalogTests
    {
        private static readonly Lazy<OfficialPlayfieldPlacementCatalog> SharedCatalog =
            new Lazy<OfficialPlayfieldPlacementCatalog>(
                () => new OfficialPlayfieldPlacementCatalog(CorpusRoot()));

        [TestMethod]
        public void CatalogLoadsTypedDistrictsAndStableRecordsFromTheTrackedCorpus()
        {
            OfficialPlayfieldPlacementCatalog catalog = SharedCatalog.Value;

            OfficialPlayfieldPlacementShard shard = catalog.GetPlayfield(4582);
            IList<OfficialPlayfieldPlacementDistrict> districts = catalog.GetDistricts(4582);
            IList<OfficialPlayfieldPlacement> placements = catalog.GetPlacements(4582);
            OfficialPlayfieldPlacement ncnn = catalog.GetByOfficialSpawnRecordId(
                "18.8.62_EP1:1000014:4582:district-1:record-50");

            Assert.AreEqual(2, shard.SchemaVersion.Value);
            Assert.AreEqual(2, districts.Count);
            Assert.AreEqual(207, placements.Count);
            Assert.AreEqual(142, districts[0].HashSpawnRecordCount.Value);
            Assert.AreEqual(65, districts[1].HashSpawnRecordCount.Value);
            Assert.IsNotNull(ncnn);
            Assert.AreEqual("NCNN", ncnn.CanonicalAcgHashText);
            Assert.IsTrue(ncnn.PlacementKnown.Value);
            Assert.IsFalse(ncnn.IdentityResolved.Value);
            Assert.IsFalse(ncnn.BehaviorReady.Value);
            Assert.IsFalse(ncnn.RuntimeActivationAuthorized.Value);
        }

        [TestMethod]
        public void ParserLimitedResourcesRemainTypedEmptyAndUnavailable()
        {
            OfficialPlayfieldPlacementCatalog catalog = SharedCatalog.Value;

            foreach (int playfieldId in new[] { 103, 615, 4805 })
            {
                OfficialPlayfieldPlacementShard shard = catalog.GetPlayfield(playfieldId);
                Assert.AreEqual("MALFORMED_FOR_CURRENT_EXTRACTOR", shard.ParseStatus);
                Assert.IsFalse(shard.DistrictCount.HasValue);
                Assert.IsFalse(shard.OfficialSpawnCount.HasValue);
                Assert.AreEqual(0, catalog.GetDistricts(playfieldId).Count);
                Assert.AreEqual(0, catalog.GetPlacements(playfieldId).Count);
            }
        }

        [TestMethod]
        public void SharedValidationWritesCanonicalArtifactsWithoutABuiltBinaryDependency()
        {
            OfficialPlayfieldPlacementCatalog catalog = SharedCatalog.Value;
            string outputRoot = Path.Combine(
                Path.GetTempPath(),
                "aorebirth-official-placement-" + Guid.NewGuid().ToString("N"));
            string manifest = Path.Combine(outputRoot, "windows-manifest.json");
            string provenance = Path.Combine(outputRoot, "windows-provenance.env");
            string linuxManifest = Path.Combine(outputRoot, "linux-manifest.json");
            string linuxProvenance = Path.Combine(outputRoot, "linux-provenance.env");
            Directory.CreateDirectory(outputRoot);
            try
            {
                catalog.WriteValidationArtifacts(
                    new string('0', 40),
                    "windows",
                    manifest,
                    provenance);
                catalog.WriteValidationArtifacts(
                    new string('0', 40),
                    "Linux",
                    linuxManifest,
                    linuxProvenance);

                Assert.IsTrue(File.Exists(manifest));
                Assert.IsTrue(File.Exists(provenance));
                CollectionAssert.AreEqual(
                    File.ReadAllBytes(manifest),
                    File.ReadAllBytes(linuxManifest));
                string manifestText = File.ReadAllText(manifest);
                Assert.IsFalse(manifestText.Contains("\r"));
                Assert.IsTrue(manifestText.EndsWith("\n", StringComparison.Ordinal));
                Assert.IsFalse(manifestText.TrimEnd('\n').Contains("\n"));
                byte[] manifestBytes = File.ReadAllBytes(manifest);
                Assert.IsFalse(
                    manifestBytes.Length >= 3
                    && manifestBytes[0] == 0xEF
                    && manifestBytes[1] == 0xBB
                    && manifestBytes[2] == 0xBF);
                var serializer = new JavaScriptSerializer { MaxJsonLength = int.MaxValue };
                var root = serializer.Deserialize<Dictionary<string, object>>(manifestText);
                var metrics = (Dictionary<string, object>)root["Metrics"];
                Assert.AreEqual(630, Convert.ToInt32(metrics["ResourceCount"]));
                Assert.AreEqual(4146, Convert.ToInt32(metrics["DistrictCount"]));
                Assert.AreEqual(32805, Convert.ToInt32(metrics["PlacementCount"]));
                Assert.AreEqual(4016, Convert.ToInt32(metrics["UniqueAcgHashCount"]));
                Assert.AreEqual(
                    199,
                    Convert.ToInt32(metrics["RuntimeActivationAuthorizedCount"]));
                string provenanceText = File.ReadAllText(provenance);
                StringAssert.Contains(provenanceText, "SOURCE_SHA=" + new string('0', 40));
                StringAssert.Contains(provenanceText, "BUILD_PLATFORM=windows");
                StringAssert.Contains(provenanceText, "PLACEMENT_BUILD_MANIFEST_SHA256=");
                StringAssert.Contains(
                    File.ReadAllText(linuxProvenance),
                    "BUILD_PLATFORM=linux");
            }
            finally
            {
                if (Directory.Exists(outputRoot))
                {
                    Directory.Delete(outputRoot, true);
                }
            }
        }

        private static string CorpusRoot()
        {
            string repositoryRoot = TestRepositoryRootResolver.FindFromCallerFilePath();
            return Path.Combine(repositoryRoot, "docs", "generated", "playfields");
        }
    }
}
