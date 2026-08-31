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
    public class MercenaryCampLootEvidenceTests
    {
        [TestMethod]
        public void WikiRowsArePlayfieldAndEnemyNameScoped()
        {
            Assert.AreEqual(48, DocumentedMercenaryCampLootDefinitions.DocumentedDrops.Length);
            Assert.AreEqual(24, DocumentedMercenaryCampLootDefinitions.DocumentedSourceItemIds.Length);
            Assert.AreEqual(
                3,
                DocumentedMercenaryCampLootDefinitions.DocumentedDrops.Count(value => value.IsActive));
            Assert.AreEqual(13, Drops("Ian Warr").Length);
            Assert.AreEqual(8, Drops("Nelly Johnson").Length);
            Assert.AreEqual(11, Drops("Patricia Johnson").Length);
            Assert.AreEqual(9, Drops("Peter Lee").Length);
            Assert.AreEqual(7, Drops("Ris Lee").Length);
            Assert.AreEqual(0, Drops("Otacustes").Length);
            Assert.AreEqual(
                0,
                DocumentedMercenaryCampLootDefinitions.DropsForDisplayName(
                    DocumentedCamelotLootDefinitions.PlayfieldInstance,
                    "Ian Warr").Length);
        }

        [TestMethod]
        public void ExplicitHundredPercentDropsAreActive()
        {
            AssertGuaranteed(
                "Ian Warr",
                DocumentedMercenaryCampLootDefinitions.BreastplateOfAzureReveriesItemId,
                200);
            AssertGuaranteed(
                "Nelly Johnson",
                DocumentedMercenaryCampLootDefinitions.NellyJohnsonsLittleBlackDressItemId,
                200);
            AssertGuaranteed(
                "Ris Lee",
                DocumentedMercenaryCampLootDefinitions.FancyStethoscopicGlassesItemId,
                1);
        }

        [TestMethod]
        public void RandomPoolsAndApproximateRatesRemainInactive()
        {
            LootTableDefinition ian = EmptyTable("mercenary-camp.test.ian");
            Assert.IsTrue(
                DocumentedMercenaryCampLootDefinitions.ApplyDocumentedLoot(
                    ian,
                    DocumentedMercenaryCampLootDefinitions.PlayfieldInstance,
                    "Ian Warr"));
            Assert.AreEqual(1, ian.RollGroups.Length);

            LootTableDefinition nelly = EmptyTable("mercenary-camp.test.nelly");
            Assert.IsTrue(
                DocumentedMercenaryCampLootDefinitions.ApplyDocumentedLoot(
                    nelly,
                    DocumentedMercenaryCampLootDefinitions.PlayfieldInstance,
                    "Nelly Johnson"));
            Assert.AreEqual(1, nelly.RollGroups.Length);

            LootTableDefinition patricia = EmptyTable("mercenary-camp.test.patricia");
            Assert.IsFalse(
                DocumentedMercenaryCampLootDefinitions.ApplyDocumentedLoot(
                    patricia,
                    DocumentedMercenaryCampLootDefinitions.PlayfieldInstance,
                    "Patricia Johnson"));
            Assert.AreEqual(0, patricia.RollGroups.Length);

            LootTableDefinition peter = EmptyTable("mercenary-camp.test.peter");
            Assert.IsFalse(
                DocumentedMercenaryCampLootDefinitions.ApplyDocumentedLoot(
                    peter,
                    DocumentedMercenaryCampLootDefinitions.PlayfieldInstance,
                    "Peter Lee"));
            Assert.AreEqual(0, peter.RollGroups.Length);

            LootTableDefinition ris = EmptyTable("mercenary-camp.test.ris");
            Assert.IsTrue(
                DocumentedMercenaryCampLootDefinitions.ApplyDocumentedLoot(
                    ris,
                    DocumentedMercenaryCampLootDefinitions.PlayfieldInstance,
                    "Ris Lee"));
            Assert.AreEqual(1, ris.RollGroups.Length);
        }

        [TestMethod]
        public void ExistingGuaranteedItemIsNotDuplicated()
        {
            LootTableDefinition table = EmptyTable("mercenary-camp.test.existing");
            table.RollGroups = new[]
            {
                LegacyGroup(DocumentedMercenaryCampLootDefinitions.BreastplateOfAzureReveriesItemId)
            };
            Assert.IsFalse(
                DocumentedMercenaryCampLootDefinitions.ApplyDocumentedLoot(
                    table,
                    DocumentedMercenaryCampLootDefinitions.PlayfieldInstance,
                    "Ian Warr"));
            Assert.AreEqual(1, table.RollGroups.Length);
            Assert.IsFalse(table.AllowsDocumentedSupplement);
        }

        [TestMethod]
        public void GlobalLootLegacySeamUsesMercenaryCampPlayfieldScopedSupplement()
        {
            string source = File.ReadAllText(
                Path.Combine(
                    FindRepositoryRoot(),
                    @"AORebirth\Server\ZoneEngine\Core\Playfields\GlobalLootRuntimeService.cs"));
            StringAssert.Contains(source, "hasDocumentedMercenaryCampLoot");
            StringAssert.Contains(source, "DocumentedMercenaryCampLootDefinitions.ApplyDocumentedLoot");
            StringAssert.Contains(source, "DocumentedMercenaryCampLootDefinitions.PlayfieldInstance");
        }

        [TestMethod]
        public void WikiAuditArtifactMatchesProductionDefinitions()
        {
            string artifactPath = Path.Combine(
                FindRepositoryRoot(),
                @"docs\generated\pf620_loot\mercenary-camp-loot-membership-audit.json");
            Assert.IsTrue(File.Exists(artifactPath));
            var serializer = new JavaScriptSerializer { MaxJsonLength = int.MaxValue };
            var artifact = (Dictionary<string, object>)serializer.DeserializeObject(
                File.ReadAllText(artifactPath));

            Assert.AreEqual(24, Convert.ToInt32(artifact["source_item_count"]));
            Assert.AreEqual(48, Convert.ToInt32(artifact["documented_mapping_count"]));
            Assert.AreEqual(3, Convert.ToInt32(artifact["active_mapping_count"]));
            Assert.AreEqual(45, Convert.ToInt32(artifact["inactive_mapping_count"]));
            Assert.AreEqual(620, Convert.ToInt32(artifact["playfield_instance"]));

            Dictionary<string, object>[] items = ((object[])artifact["items"])
                .Cast<Dictionary<string, object>>()
                .ToArray();
            CollectionAssert.AreEqual(
                DocumentedMercenaryCampLootDefinitions.DocumentedSourceItemIds,
                items
                    .Select(value => Convert.ToInt32(value["item_id"]))
                    .OrderBy(value => value)
                    .ToArray());

            string[] productionActive = DocumentedMercenaryCampLootDefinitions.DocumentedDrops
                .Where(value => value.IsActive)
                .Select(value => value.EnemyKey + ":" + value.ItemTemplateId)
                .ToArray();
            string[] artifactActive = ((object[])artifact["active_mappings"])
                .Cast<Dictionary<string, object>>()
                .Select(value => value["enemy_key"] + ":" + value["item_id"])
                .ToArray();
            CollectionAssert.AreEqual(productionActive, artifactActive);
        }

        private static MercenaryCampDocumentedDropDefinition[] Drops(string displayName)
        {
            return DocumentedMercenaryCampLootDefinitions.DropsForDisplayName(
                DocumentedMercenaryCampLootDefinitions.PlayfieldInstance,
                displayName);
        }

        private static void AssertGuaranteed(string displayName, int itemTemplateId, int quality)
        {
            LootTableDefinition table = EmptyTable("mercenary-camp.test." + displayName);
            Assert.IsTrue(
                DocumentedMercenaryCampLootDefinitions.ApplyDocumentedLoot(
                    table,
                    DocumentedMercenaryCampLootDefinitions.PlayfieldInstance,
                    displayName));
            Assert.AreEqual(1, table.RollGroups.Length);
            LootGroupDefinition group = table.RollGroups.Single();
            LootEntryDefinition entry = group.Entries.Single();
            Assert.AreEqual(LootRollMode.Independent, group.RollMode);
            Assert.AreEqual(10000, group.DropChanceBasisPoints);
            Assert.AreEqual(itemTemplateId, entry.ItemTemplateId);
            Assert.AreEqual(quality, entry.FixedQuality);
            Assert.AreEqual(10000, entry.DropChanceBasisPoints);
            Assert.AreEqual(DocumentedMercenaryCampLootDefinitions.DocumentedLootSourceUrl, entry.EvidenceReference);
            Assert.IsTrue(table.AllowsDocumentedSupplement);
        }

        private static LootTableDefinition EmptyTable(string key)
        {
            return new LootTableDefinition
            {
                LootTableKey = key,
                DisplayName = key,
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
                        Weight = 1,
                        DropChanceBasisPoints = 10000
                    }
                },
                Conditions = new string[0]
            };
        }

        private static string FindRepositoryRoot()
        {
            string current = AppDomain.CurrentDomain.BaseDirectory;
            while (!string.IsNullOrWhiteSpace(current))
            {
                if (File.Exists(Path.Combine(current, @"AORebirth\AORebirth.sln")))
                {
                    return current;
                }

                DirectoryInfo parent = Directory.GetParent(current);
                current = parent == null ? null : parent.FullName;
            }

            throw new DirectoryNotFoundException("Could not find the AORebirth repository root.");
        }
    }
}
