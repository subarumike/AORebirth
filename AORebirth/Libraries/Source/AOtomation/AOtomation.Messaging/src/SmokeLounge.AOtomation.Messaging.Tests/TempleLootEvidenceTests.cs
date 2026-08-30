namespace SmokeLounge.AOtomation.Messaging.Tests
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Web.Script.Serialization;

    using AORebirth.Core.Playfields;

    using Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestClass]
    public class TempleLootEvidenceTests
    {
        [TestMethod]
        public void WikiRowsArePlayfieldAndCapturedProfileScoped()
        {
            Assert.AreEqual(7, DocumentedTempleOfThreeWindsLootDefinitions.DocumentedDrops.Length);
            CollectionAssert.AreEqual(
                new[] { 204575, 204576, 204577, 204578, 204613, 204647, 204748 },
                DocumentedTempleOfThreeWindsLootDefinitions.DocumentedSourceItemIds);
            Assert.AreEqual(
                1,
                DocumentedTempleOfThreeWindsLootDefinitions.DropsForProfile(
                    1931,
                    CapturedTempleOfThreeWindsLootDefinitions.YatilaProfileKey).Length);
            Assert.AreEqual(
                3,
                DocumentedTempleOfThreeWindsLootDefinitions.DropsForProfile(
                    1931,
                    CapturedTempleOfThreeWindsLootDefinitions.CuratorProfileKey).Length);
            Assert.AreEqual(
                2,
                DocumentedTempleOfThreeWindsLootDefinitions.DropsForProfile(
                    1931,
                    CapturedTempleOfThreeWindsLootDefinitions.NematetProfileKey).Length);
            Assert.AreEqual(
                1,
                DocumentedTempleOfThreeWindsLootDefinitions.DropsForProfile(
                    1931,
                    CapturedTempleOfThreeWindsLootDefinitions.GuardianProfileKey).Length);
            Assert.AreEqual(
                0,
                DocumentedTempleOfThreeWindsLootDefinitions.DropsForProfile(
                    127,
                    CapturedTempleOfThreeWindsLootDefinitions.YatilaProfileKey).Length);
            Assert.AreEqual(
                0,
                DocumentedTempleOfThreeWindsLootDefinitions.DropsForProfile(
                    1931,
                    "totw.unproven.enemy").Length);
        }

        [TestMethod]
        public void WikiSupplementPreservesCapturedSnapshotsAndUsesPublishedLowerBounds()
        {
            LootTableDefinition yatila =
                CapturedTempleOfThreeWindsLootDefinitions.BuildYatilaLootTable();
            ObservedCorpseSnapshotDefinition[] capturedSnapshots = yatila.ObservedCorpseSnapshots;

            Assert.IsTrue(
                DocumentedTempleOfThreeWindsLootDefinitions.ApplyDocumentedBossLoot(
                    yatila,
                    1931,
                    CapturedTempleOfThreeWindsLootDefinitions.YatilaProfileKey));
            Assert.AreSame(capturedSnapshots, yatila.ObservedCorpseSnapshots);
            Assert.AreEqual(1, yatila.RollGroups.Length);
            Assert.AreEqual(600, yatila.RollGroups[0].DropChanceBasisPoints);
            Assert.AreEqual(204576, yatila.RollGroups[0].Entries.Single().ItemTemplateId);
            Assert.AreEqual(
                "documented-lower-bound:approximately 6-9%",
                yatila.RollGroups[0].Entries.Single().ProbabilityEvidence);
            Assert.IsTrue(yatila.AllowsDocumentedSupplement);
            Assert.IsFalse(
                DocumentedTempleOfThreeWindsLootDefinitions.ApplyDocumentedBossLoot(
                    yatila,
                    1931,
                    CapturedTempleOfThreeWindsLootDefinitions.YatilaProfileKey));
            Assert.AreEqual(1, yatila.RollGroups.Length);
        }

        [TestMethod]
        public void WikiSupplementAddsOnlyMissingNumericBossDrops()
        {
            LootTableDefinition curator =
                CapturedTempleOfThreeWindsLootDefinitions.BuildCuratorLootTable();
            LootTableDefinition nematet =
                CapturedTempleOfThreeWindsLootDefinitions.BuildNematetLootTable();
            LootTableDefinition guardian =
                CapturedTempleOfThreeWindsLootDefinitions.BuildGuardianLootTable();

            Assert.IsTrue(
                DocumentedTempleOfThreeWindsLootDefinitions.ApplyDocumentedBossLoot(
                    curator,
                    1931,
                    CapturedTempleOfThreeWindsLootDefinitions.CuratorProfileKey));
            Assert.IsTrue(
                DocumentedTempleOfThreeWindsLootDefinitions.ApplyDocumentedBossLoot(
                    nematet,
                    1931,
                    CapturedTempleOfThreeWindsLootDefinitions.NematetProfileKey));
            Assert.IsTrue(
                DocumentedTempleOfThreeWindsLootDefinitions.ApplyDocumentedBossLoot(
                    guardian,
                    1931,
                    CapturedTempleOfThreeWindsLootDefinitions.GuardianProfileKey));

            CollectionAssert.AreEqual(
                new[] { 204575, 204577, 204578 },
                EntryIds(curator));
            CollectionAssert.AreEqual(
                new[] { 204613, 204647 },
                EntryIds(nematet));
            CollectionAssert.AreEqual(
                new[] { 204748 },
                EntryIds(guardian));
            CollectionAssert.AreEqual(
                new[] { 100, 200, 600 },
                curator.RollGroups
                    .Select(value => value.DropChanceBasisPoints)
                    .OrderBy(value => value)
                    .ToArray());
            CollectionAssert.AreEqual(
                new[] { 300, 2000 },
                nematet.RollGroups
                    .Select(value => value.DropChanceBasisPoints)
                    .OrderBy(value => value)
                    .ToArray());
            Assert.AreEqual(400, guardian.RollGroups.Single().DropChanceBasisPoints);
        }

        [TestMethod]
        public void WikiSupplementDeduplicatesItemsAlreadyPresentInCapturedEvidence()
        {
            LootTableDefinition table =
                CapturedTempleOfThreeWindsLootDefinitions.BuildYatilaLootTable();
            table.ObservedCorpseSnapshots[0].Entries = table.ObservedCorpseSnapshots[0].Entries
                .Concat(
                    new[]
                    {
                        new LootEntryDefinition
                        {
                            ItemTemplateId = 204576,
                            HighItemTemplateId = 204576,
                            FixedQuality = 1,
                            MinimumQuality = 1,
                            MaximumQuality = 1,
                            MinimumQuantity = 1,
                            MaximumQuantity = 1
                        }
                    })
                .ToArray();

            Assert.IsFalse(
                DocumentedTempleOfThreeWindsLootDefinitions.ApplyDocumentedBossLoot(
                    table,
                    1931,
                    CapturedTempleOfThreeWindsLootDefinitions.YatilaProfileKey));
            Assert.AreEqual(0, table.RollGroups.Length);
            Assert.IsFalse(table.AllowsDocumentedSupplement);
        }

        [TestMethod]
        public void CapturedTempleRegistrationAppliesWikiSupplementBeforeRegistering()
        {
            string sourcePath = Path.Combine(
                FindRepositoryRoot(),
                @"AORebirth\Server\ZoneEngine\Core\Playfields\CapturedTempleOfThreeWindsLootDefinitions.cs");
            string source = File.ReadAllText(sourcePath);

            StringAssert.Contains(
                source,
                "DocumentedTempleOfThreeWindsLootDefinitions.ApplyDocumentedBossLoot(");
            StringAssert.Contains(source, "table,");
            StringAssert.Contains(source, "PlayfieldInstance,");
            StringAssert.Contains(source, "profileKey);");
        }

        [TestMethod]
        public void WikiAuditArtifactMatchesProductionDefinitions()
        {
            string artifactPath = Path.Combine(
                FindRepositoryRoot(),
                @"docs\generated\pf1931_loot\temple-loot-membership-audit.json");
            Assert.IsTrue(File.Exists(artifactPath));

            var serializer = new JavaScriptSerializer { MaxJsonLength = int.MaxValue };
            var artifact = (Dictionary<string, object>)serializer.DeserializeObject(
                File.ReadAllText(artifactPath));
            Assert.AreEqual(7, Convert.ToInt32(artifact["source_item_count"]));
            Assert.AreEqual(7, Convert.ToInt32(artifact["documented_mapping_count"]));
            Assert.AreEqual(1931, Convert.ToInt32(artifact["playfield_instance"]));

            string[] productionMappings = DocumentedTempleOfThreeWindsLootDefinitions
                .DocumentedDrops
                .Select(
                    value => string.Join(
                        "|",
                        value.ProfileKey,
                        value.ItemTemplateId,
                        value.MinimumDropChanceBasisPoints,
                        value.MaximumDropChanceBasisPoints))
                .OrderBy(value => value, StringComparer.Ordinal)
                .ToArray();
            string[] artifactMappings = ((object[])artifact["items"])
                .Cast<Dictionary<string, object>>()
                .Select(
                    value => string.Join(
                        "|",
                        Convert.ToString(value["profile_key"]),
                        Convert.ToString(value["item_id"]),
                        Convert.ToString(value["minimum_basis_points"]),
                        Convert.ToString(value["maximum_basis_points"])))
                .OrderBy(value => value, StringComparer.Ordinal)
                .ToArray();
            CollectionAssert.AreEqual(productionMappings, artifactMappings);
        }

        private static int[] EntryIds(LootTableDefinition table)
        {
            return table.RollGroups
                .SelectMany(value => value.Entries)
                .Select(value => value.ItemTemplateId)
                .OrderBy(value => value)
                .ToArray();
        }

        private static string FindRepositoryRoot()
        {
            string current = AppDomain.CurrentDomain.BaseDirectory;
            while (!string.IsNullOrWhiteSpace(current))
            {
                if (Directory.Exists(Path.Combine(current, "AORebirth", "Server", "ZoneEngine"))
                    && Directory.Exists(Path.Combine(current, "docs", "generated")))
                {
                    return current;
                }

                DirectoryInfo parent = Directory.GetParent(current);
                if (parent == null)
                {
                    break;
                }
                current = parent.FullName;
            }

            throw new DirectoryNotFoundException("Could not find the AORebirth repository root.");
        }
    }
}
