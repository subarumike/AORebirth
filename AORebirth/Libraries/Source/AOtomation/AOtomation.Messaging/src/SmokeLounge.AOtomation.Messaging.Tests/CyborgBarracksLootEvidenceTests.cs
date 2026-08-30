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
    public class CyborgBarracksLootEvidenceTests
    {
        [TestMethod]
        public void WikiRowsArePlayfieldAndEnemyNameScoped()
        {
            Assert.AreEqual(13, DocumentedCyborgBarracksLootDefinitions.DocumentedDrops.Length);
            Assert.AreEqual(13, DocumentedCyborgBarracksLootDefinitions.DocumentedSourceItemIds.Length);
            Assert.AreEqual(
                0,
                DocumentedCyborgBarracksLootDefinitions.DocumentedDrops.Count(value => value.IsActive));

            Assert.AreEqual(2, Drops("Augmented Cyborg Hellfury").Length);
            Assert.AreEqual(1, Drops("Cyborg Biotechnician").Length);
            Assert.AreEqual(2, Drops("Eradicator Deimos").Length);
            Assert.AreEqual(2, Drops("General Severus").Length);
            Assert.AreEqual(6, Drops("Commander Jocasta").Length);
            Assert.AreEqual(1, Drops("Prototype Inferno").Length);
            Assert.AreEqual(0, Drops("Janella Gheron").Length);
            Assert.AreEqual(
                0,
                DocumentedCyborgBarracksLootDefinitions.DropsForDisplayName(
                    DocumentedSmugglersDenLootDefinitions.PlayfieldInstance,
                    "Commander Jocasta").Length);
        }

        [TestMethod]
        public void QualitativeMembershipNeverInventsRatesOrOutcomes()
        {
            string[] unresolvedNames =
            {
                "Augmented Cyborg Hellfury",
                "Cyborg Biotechnician",
                "Eradicator Deimos",
                "General Severus",
                "Commander Jocasta",
                "Prototype Inferno"
            };
            foreach (string name in unresolvedNames)
            {
                LootTableDefinition table = EmptyTable("cyborg-barracks.test.inactive." + name);
                Assert.IsFalse(
                    DocumentedCyborgBarracksLootDefinitions.ApplyDocumentedLoot(
                        table,
                        DocumentedCyborgBarracksLootDefinitions.PlayfieldInstance,
                        name));
                Assert.AreEqual(0, table.RollGroups.Length);
                Assert.IsFalse(table.AllowsDocumentedSupplement);
            }
        }

        [TestMethod]
        public void GlobalLootLegacySeamUsesCyborgBarracksPlayfieldScopedSupplement()
        {
            string source = File.ReadAllText(
                Path.Combine(
                    FindRepositoryRoot(),
                    @"AORebirth\Server\ZoneEngine\Core\Playfields\GlobalLootRuntimeService.cs"));
            StringAssert.Contains(source, "hasDocumentedCyborgBarracksLoot");
            StringAssert.Contains(
                source,
                "DocumentedCyborgBarracksLootDefinitions.ApplyDocumentedLoot");
            StringAssert.Contains(
                source,
                "DocumentedCyborgBarracksLootDefinitions.PlayfieldInstance");
        }

        [TestMethod]
        public void WikiAuditArtifactMatchesProductionDefinitions()
        {
            string artifactPath = Path.Combine(
                FindRepositoryRoot(),
                @"docs\generated\pf1833_loot\cyborg-barracks-loot-membership-audit.json");
            Assert.IsTrue(File.Exists(artifactPath));
            var serializer = new JavaScriptSerializer { MaxJsonLength = int.MaxValue };
            var artifact = (Dictionary<string, object>)serializer.DeserializeObject(
                File.ReadAllText(artifactPath));

            Assert.AreEqual(13, Convert.ToInt32(artifact["source_item_count"]));
            Assert.AreEqual(13, Convert.ToInt32(artifact["documented_mapping_count"]));
            Assert.AreEqual(0, Convert.ToInt32(artifact["active_mapping_count"]));
            Assert.AreEqual(13, Convert.ToInt32(artifact["inactive_mapping_count"]));
            Assert.AreEqual(1833, Convert.ToInt32(artifact["playfield_instance"]));

            Dictionary<string, object>[] items = ((object[])artifact["items"])
                .Cast<Dictionary<string, object>>()
                .ToArray();
            CollectionAssert.AreEqual(
                DocumentedCyborgBarracksLootDefinitions.DocumentedSourceItemIds,
                items
                    .Select(value => Convert.ToInt32(value["item_id"]))
                    .OrderBy(value => value)
                    .ToArray());

            Assert.AreEqual(0, ((object[])artifact["active_mappings"]).Length);
        }

        private static CyborgBarracksDocumentedDropDefinition[] Drops(string displayName)
        {
            return DocumentedCyborgBarracksLootDefinitions.DropsForDisplayName(
                DocumentedCyborgBarracksLootDefinitions.PlayfieldInstance,
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
