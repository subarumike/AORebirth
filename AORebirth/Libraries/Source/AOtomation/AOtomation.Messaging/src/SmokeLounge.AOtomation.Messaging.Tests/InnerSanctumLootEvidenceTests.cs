namespace SmokeLounge.AOtomation.Messaging.Tests
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Web.Script.Serialization;

    using AORebirth.Core.Playfields;

    using Microsoft.VisualStudio.TestTools.UnitTesting;

    using ZoneEngine.Core.Playfields;

    [TestClass]
    public class InnerSanctumLootEvidenceTests
    {
        [TestMethod]
        public void WikiBossDropsAreScopedAndPreserveUnresolvedProbabilities()
        {
            AssertBossCounts("The Re-Incarnator", 12, 2);
            AssertBossCounts("Jeuru the Defiler", 18, 18);
            AssertBossCounts("Iskop the Idolator", 19, 18);
            AssertBossCounts("Dominus Ummoh the Pedagogue", 29, 28);
            AssertBossCounts("Dominus Facut the Bloodless", 15, 0);
            AssertBossCounts("Dominus Jiannu", 26, 26);
            AssertBossCounts("Inobak the Gelid", 23, 23);
            AssertBossCounts("Hezak the Immortal", 18, 0);

            Assert.AreEqual(
                0,
                DocumentedInnerSanctumLootDefinitions
                    .DropsForDisplayName(1931, "The Re-Animator")
                    .Length,
                "The Inner Sanctum alias must never affect the Temple of Three Winds playfield.");
            Assert.AreEqual(
                DocumentedInnerSanctumLootDefinitions.ReIncarnatorBossKey,
                DocumentedInnerSanctumLootDefinitions
                    .BossKeyForDisplayName("The Re-Animator"));

            InnerSanctumDocumentedDropDefinition crystal = Drops("The Re-Incarnator")
                .Single(value => value.ItemTemplateId == 204829);
            Assert.AreEqual(390, crystal.Quality);
            Assert.AreEqual(10000, crystal.MinimumDropChanceBasisPoints);
            Assert.IsTrue(crystal.IsActive);

            InnerSanctumDocumentedDropDefinition iskopAscendancy =
                Drops("Iskop the Idolator")
                    .Single(value => value.ItemTemplateId == 206063);
            Assert.IsFalse(iskopAscendancy.IsActive);
            Assert.AreEqual(0, iskopAscendancy.MinimumDropChanceBasisPoints);
            Assert.AreEqual(399, iskopAscendancy.MaximumDropChanceBasisPoints);
            StringAssert.Contains(iskopAscendancy.SourceProbability, "if it drops at all");

            Assert.IsTrue(
                DocumentedInnerSanctumLootDefinitions.DocumentedDrops
                    .Where(value => value.IsActive)
                    .All(
                        value => value.MinimumDropChanceBasisPoints > 0
                                 && value.MinimumDropChanceBasisPoints
                                    <= value.MaximumDropChanceBasisPoints));
        }

        [TestMethod]
        public void WikiSupplementKeepsLegacyLootAndUsesRangeLowerBounds()
        {
            var table = new LootTableDefinition
            {
                LootTableKey = "inner-sanctum.test.jeuru",
                DisplayName = "Jeuru test",
                TableType = LootTableType.EnemyType,
                RollGroups = new[]
                {
                    LegacyGroup(206049)
                },
                ObservedCorpseSnapshots = new ObservedCorpseSnapshotDefinition[0],
                CreditsPolicy = new CreditsPolicyDefinition
                {
                    Mode = CreditsPolicyMode.Unresolved,
                    Evidence = LootEvidenceConfidence.Unresolved
                },
                Enabled = true
            };

            Assert.IsTrue(
                DocumentedInnerSanctumLootDefinitions.ApplyDocumentedBossLoot(
                    table,
                    DocumentedInnerSanctumLootDefinitions.PlayfieldInstance,
                    "Jeuru the Defiler"));
            Assert.AreEqual(18, table.RollGroups.Length);
            Assert.AreEqual(
                1,
                table.RollGroups
                    .SelectMany(value => value.Entries)
                    .Count(value => value.ItemTemplateId == 206049));
            Assert.IsTrue(table.AllowsDocumentedSupplement);

            LootGroupDefinition icebound = table.RollGroups.Single(
                value => value.LootGroupKey.EndsWith(".206056", StringComparison.Ordinal));
            Assert.AreEqual(LootRollMode.Independent, icebound.RollMode);
            Assert.AreEqual(1500, icebound.DropChanceBasisPoints);
            Assert.AreEqual(
                "documented-lower-bound:15-20%",
                icebound.Entries.Single().ProbabilityEvidence);
            Assert.AreEqual(
                LootEvidenceConfidence.CommunityDocumented,
                icebound.Entries.Single().Evidence);
            Assert.AreEqual(
                DocumentedInnerSanctumLootDefinitions.DocumentedLootSourceUrl,
                icebound.Entries.Single().EvidenceReference);

            int groupCount = table.RollGroups.Length;
            Assert.IsFalse(
                DocumentedInnerSanctumLootDefinitions.ApplyDocumentedBossLoot(
                    table,
                    DocumentedInnerSanctumLootDefinitions.PlayfieldInstance,
                    "Jeuru the Defiler"));
            Assert.AreEqual(groupCount, table.RollGroups.Length);

            var registry = new LootTableRegistry(value => true);
            registry.RegisterTable(table);
            Assert.IsTrue(registry.ContainsTable(table.LootTableKey));
        }

        [TestMethod]
        public void WikiSupplementAddsNothingForWrongPlayfieldOrUnratedBosses()
        {
            var table = new LootTableDefinition
            {
                LootTableKey = "inner-sanctum.test.unrated",
                DisplayName = "Unrated test",
                TableType = LootTableType.EnemyType,
                RollGroups = new LootGroupDefinition[0],
                ObservedCorpseSnapshots = new ObservedCorpseSnapshotDefinition[0],
                CreditsPolicy = new CreditsPolicyDefinition
                {
                    Mode = CreditsPolicyMode.Unresolved,
                    Evidence = LootEvidenceConfidence.Unresolved
                },
                Enabled = true
            };

            Assert.IsFalse(
                DocumentedInnerSanctumLootDefinitions.ApplyDocumentedBossLoot(
                    table,
                    1931,
                    "The Re-Animator"));
            Assert.IsFalse(
                DocumentedInnerSanctumLootDefinitions.ApplyDocumentedBossLoot(
                    table,
                    DocumentedInnerSanctumLootDefinitions.PlayfieldInstance,
                    "Dominus Facut"));
            Assert.IsFalse(
                DocumentedInnerSanctumLootDefinitions.ApplyDocumentedBossLoot(
                    table,
                    DocumentedInnerSanctumLootDefinitions.PlayfieldInstance,
                    "Hezak the Immortal"));
            Assert.AreEqual(0, table.RollGroups.Length);
        }

        [TestMethod]
        public void WikiAuditArtifactMatchesProductionSourceIdsAndBossCounts()
        {
            string root = FindRepositoryRoot();
            string artifactPath = Path.Combine(
                root,
                @"docs\generated\pf1943_loot\inner-sanctum-boss-loot-audit.json");
            Assert.IsTrue(File.Exists(artifactPath));
            var serializer = new JavaScriptSerializer { MaxJsonLength = int.MaxValue };
            var artifact = (Dictionary<string, object>)serializer.DeserializeObject(
                File.ReadAllText(artifactPath));
            object[] rows = (object[])artifact["items"];
            Assert.AreEqual(64, Convert.ToInt32(artifact["source_item_count"]));
            Assert.AreEqual(64, rows.Length);
            Assert.AreEqual(
                64,
                DocumentedInnerSanctumLootDefinitions.DocumentedSourceItemIds.Length,
                "Production source unique-item count");
            Dictionary<string, object>[] itemRows = rows
                .Cast<Dictionary<string, object>>()
                .ToArray();
            CollectionAssert.AreEqual(
                DocumentedInnerSanctumLootDefinitions.DocumentedSourceItemIds,
                itemRows
                    .Select(value => Convert.ToInt32(value["item_id"]))
                    .OrderBy(value => value)
                    .ToArray());
            Dictionary<int, int> artifactQualityById = itemRows.ToDictionary(
                value => Convert.ToInt32(value["item_id"]),
                value => Convert.ToInt32(value["quality"]));
            foreach (IGrouping<int, InnerSanctumDocumentedDropDefinition> item in
                     DocumentedInnerSanctumLootDefinitions.DocumentedDrops
                         .GroupBy(value => value.ItemTemplateId))
            {
                Assert.AreEqual(
                    1,
                    item.Select(value => value.Quality).Distinct().Count(),
                    "Conflicting production quality for " + item.Key);
                Assert.AreEqual(
                    artifactQualityById[item.Key],
                    item.First().Quality,
                    "Audit quality mismatch for " + item.Key);
            }

            object[] bosses = (object[])artifact["bosses"];
            Assert.AreEqual(8, bosses.Length);
            Assert.AreEqual(
                160,
                bosses
                    .Cast<Dictionary<string, object>>()
                    .Sum(value => Convert.ToInt32(value["documented_rows"])));
            Assert.AreEqual(
                115,
                bosses
                    .Cast<Dictionary<string, object>>()
                    .Sum(value => Convert.ToInt32(value["active_rows"])));
        }

        private static InnerSanctumDocumentedDropDefinition[] Drops(string displayName)
        {
            return DocumentedInnerSanctumLootDefinitions.DropsForDisplayName(
                DocumentedInnerSanctumLootDefinitions.PlayfieldInstance,
                displayName);
        }

        private static void AssertBossCounts(
            string displayName,
            int expectedDocumented,
            int expectedActive)
        {
            InnerSanctumDocumentedDropDefinition[] drops = Drops(displayName);
            Assert.AreEqual(expectedDocumented, drops.Length, displayName);
            Assert.AreEqual(
                expectedActive,
                drops.Count(value => value.IsActive),
                displayName);
        }

        private static LootGroupDefinition LegacyGroup(int itemTemplateId)
        {
            return new LootGroupDefinition
            {
                LootGroupKey = "legacy." + itemTemplateId,
                RollMode = LootRollMode.WeightedOne,
                RollCount = 1,
                DropChanceBasisPoints = 10000,
                Entries = new[]
                {
                    new LootEntryDefinition
                    {
                        ItemTemplateId = itemTemplateId,
                        HighItemTemplateId = itemTemplateId,
                        FixedQuality = 1,
                        MinimumQuality = 1,
                        MaximumQuality = 1,
                        MinimumQuantity = 1,
                        MaximumQuantity = 1,
                        Weight = 1,
                        DropChanceBasisPoints = 10000,
                        Semantics = LootSemantics.WeightedDocumented,
                        Evidence = LootEvidenceConfidence.ProvenRepository,
                        EvidenceReference = "test.legacy"
                    }
                },
                Conditions = new string[0]
            };
        }

        private static string FindRepositoryRoot()
        {
            DirectoryInfo current = new DirectoryInfo(AppDomain.CurrentDomain.BaseDirectory);
            while (current != null)
            {
                if (Directory.Exists(Path.Combine(current.FullName, "AORebirth"))
                    && Directory.Exists(Path.Combine(current.FullName, "docs")))
                {
                    return current.FullName;
                }

                current = current.Parent;
            }

            throw new DirectoryNotFoundException("Repository root not found.");
        }

    }
}
