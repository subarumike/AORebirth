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
    public class CamelotLootEvidenceTests
    {
        [TestMethod]
        public void WikiRowsArePlayfieldAndEnemyNameScoped()
        {
            Assert.AreEqual(31, DocumentedCamelotLootDefinitions.DocumentedDrops.Length);
            Assert.AreEqual(31, DocumentedCamelotLootDefinitions.DocumentedSourceItemIds.Length);
            Assert.AreEqual(
                1,
                DocumentedCamelotLootDefinitions.DocumentedDrops.Count(value => value.IsActive));
            Assert.AreEqual(2, Drops("Morgan Le Faye").Length);
            Assert.AreEqual(6, Drops("Lord Ghasap").Length);
            Assert.AreEqual(6, Drops("Reborn Lord Ghasap").Length);
            Assert.AreEqual(22, Drops("Tarasque").Length);
            Assert.AreEqual(1, Drops("Administrator DeValos").Length);
            Assert.AreEqual(0, Drops("Lord Ghasap's Elite").Length);
            Assert.AreEqual(
                0,
                DocumentedCamelotLootDefinitions.DropsForDisplayName(
                    DocumentedCryptOfHomeLootDefinitions.PlayfieldInstance,
                    "Administrator DeValos").Length);
        }

        [TestMethod]
        public void DeValosUsesPublishedGuaranteedExclusiveDrop()
        {
            LootTableDefinition table = EmptyTable("camelot.test.devalos");
            Assert.IsTrue(
                DocumentedCamelotLootDefinitions.ApplyDocumentedLoot(
                    table,
                    DocumentedCamelotLootDefinitions.PlayfieldInstance,
                    "Administrator DeValos"));
            Assert.AreEqual(1, table.RollGroups.Length);
            LootGroupDefinition group = table.RollGroups.Single();
            LootEntryDefinition entry = group.Entries.Single();
            Assert.AreEqual(LootRollMode.Independent, group.RollMode);
            Assert.AreEqual(10000, group.DropChanceBasisPoints);
            Assert.AreEqual(DocumentedCamelotLootDefinitions.NanobotInfusionDeviceItemId, entry.ItemTemplateId);
            Assert.AreEqual(1, entry.FixedQuality);
            Assert.AreEqual(10000, entry.DropChanceBasisPoints);
            Assert.AreEqual(DocumentedCamelotLootDefinitions.DocumentedLootSourceUrl, entry.EvidenceReference);
            Assert.IsTrue(table.AllowsDocumentedSupplement);
        }

        [TestMethod]
        public void UnresolvedMembershipNeverInventsRatesOrOutcomes()
        {
            string[] unresolvedNames =
            {
                "Morgan Le Faye",
                "Lord Ghasap",
                "Reborn Lord Ghasap",
                "Tarasque"
            };
            foreach (string name in unresolvedNames)
            {
                LootTableDefinition table = EmptyTable("camelot.test.inactive." + name);
                Assert.IsFalse(
                    DocumentedCamelotLootDefinitions.ApplyDocumentedLoot(
                        table,
                        DocumentedCamelotLootDefinitions.PlayfieldInstance,
                        name));
                Assert.AreEqual(0, table.RollGroups.Length);
            }
        }

        [TestMethod]
        public void ExistingNanobotInfusionDeviceIsNotDuplicated()
        {
            LootTableDefinition table = EmptyTable("camelot.test.existing");
            table.RollGroups = new[]
            {
                LegacyGroup(DocumentedCamelotLootDefinitions.NanobotInfusionDeviceItemId)
            };
            Assert.IsFalse(
                DocumentedCamelotLootDefinitions.ApplyDocumentedLoot(
                    table,
                    DocumentedCamelotLootDefinitions.PlayfieldInstance,
                    "Administrator DeValos"));
            Assert.AreEqual(1, table.RollGroups.Length);
            Assert.IsFalse(table.AllowsDocumentedSupplement);
        }

        [TestMethod]
        public void GlobalLootLegacySeamUsesCamelotPlayfieldScopedSupplement()
        {
            string source = File.ReadAllText(
                Path.Combine(
                    FindRepositoryRoot(),
                    @"AORebirth\Server\ZoneEngine\Core\Playfields\GlobalLootRuntimeService.cs"));
            StringAssert.Contains(source, "hasDocumentedCamelotLoot");
            StringAssert.Contains(source, "DocumentedCamelotLootDefinitions.ApplyDocumentedLoot");
            StringAssert.Contains(source, "DocumentedCamelotLootDefinitions.PlayfieldInstance");
        }

        [TestMethod]
        public void WikiAuditArtifactMatchesProductionDefinitions()
        {
            string artifactPath = Path.Combine(
                FindRepositoryRoot(),
                @"docs\generated\pf120_loot\camelot-loot-membership-audit.json");
            Assert.IsTrue(File.Exists(artifactPath));
            var serializer = new JavaScriptSerializer { MaxJsonLength = int.MaxValue };
            var artifact = (Dictionary<string, object>)serializer.DeserializeObject(
                File.ReadAllText(artifactPath));

            Assert.AreEqual(31, Convert.ToInt32(artifact["source_item_count"]));
            Assert.AreEqual(31, Convert.ToInt32(artifact["documented_mapping_count"]));
            Assert.AreEqual(1, Convert.ToInt32(artifact["active_mapping_count"]));
            Assert.AreEqual(30, Convert.ToInt32(artifact["inactive_mapping_count"]));
            Assert.AreEqual(120, Convert.ToInt32(artifact["playfield_instance"]));

            Dictionary<string, object>[] items = ((object[])artifact["items"])
                .Cast<Dictionary<string, object>>()
                .ToArray();
            CollectionAssert.AreEqual(
                DocumentedCamelotLootDefinitions.DocumentedSourceItemIds,
                items
                    .Select(value => Convert.ToInt32(value["item_id"]))
                    .OrderBy(value => value)
                    .ToArray());

            string[] productionActive = DocumentedCamelotLootDefinitions.DocumentedDrops
                .Where(value => value.IsActive)
                .Select(value => value.EnemyKey + ":" + value.ItemTemplateId)
                .ToArray();
            string[] artifactActive = ((object[])artifact["active_mappings"])
                .Cast<Dictionary<string, object>>()
                .Select(value => value["enemy_key"] + ":" + value["item_id"])
                .ToArray();
            CollectionAssert.AreEqual(productionActive, artifactActive);
        }

        private static CamelotDocumentedDropDefinition[] Drops(string displayName)
        {
            return DocumentedCamelotLootDefinitions.DropsForDisplayName(
                DocumentedCamelotLootDefinitions.PlayfieldInstance,
                displayName);
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
