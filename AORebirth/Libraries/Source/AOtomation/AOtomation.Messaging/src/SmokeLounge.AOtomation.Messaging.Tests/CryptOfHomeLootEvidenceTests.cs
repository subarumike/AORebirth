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
    public class CryptOfHomeLootEvidenceTests
    {
        [TestMethod]
        public void WikiRowsArePlayfieldAndEnemyNameScoped()
        {
            Assert.AreEqual(64, DocumentedCryptOfHomeLootDefinitions.DocumentedDrops.Length);
            Assert.AreEqual(36, DocumentedCryptOfHomeLootDefinitions.DocumentedSourceItemIds.Length);
            Assert.AreEqual(
                0,
                DocumentedCryptOfHomeLootDefinitions.DocumentedDrops.Count(value => value.IsActive));

            Assert.AreEqual(7, Drops("Dark Cenobite").Length);
            Assert.AreEqual(4, Drops("Dark Sanitary").Length);
            Assert.AreEqual(6, Drops("Dark Summoner").Length);
            Assert.AreEqual(4, Drops("Cenobite Shadow").Length);
            Assert.AreEqual(2, Drops("Blorrg").Length);
            Assert.AreEqual(4, Drops("Eclipser").Length);
            Assert.AreEqual(4, Drops("Necromancer").Length);
            Assert.AreEqual(2, Drops("Kizzermole").Length);
            Assert.AreEqual(3, Drops("Awakened Pit Demon").Length);
            Assert.AreEqual(3, Drops("Crypt Guardian").Length);
            Assert.AreEqual(4, Drops("Alpha Skincrawler").Length);
            Assert.AreEqual(5, Drops("Bane").Length);
            Assert.AreEqual(6, Drops("Tentacle of Chill").Length);
            Assert.AreEqual(6, Drops("Lazy Tentacle").Length);
            Assert.AreEqual(10, Drops("Cerubin the Rejected").Length);
            Assert.AreEqual(0, Drops("Skincrawler").Length);
            Assert.AreEqual(
                0,
                DocumentedCryptOfHomeLootDefinitions.DropsForDisplayName(
                    DocumentedCyborgBarracksLootDefinitions.PlayfieldInstance,
                    "Cerubin the Rejected").Length);
        }

        [TestMethod]
        public void QualitativeMembershipNeverInventsRatesOrOutcomes()
        {
            string[] unresolvedNames =
            {
                "Dark Cenobite",
                "Dark Sanitary",
                "Dark Summoner",
                "Cenobite Shadow",
                "Blorrg",
                "Eclipser",
                "Necromancer",
                "Kizzermole",
                "Awakened Pit Demon",
                "Crypt Guardian",
                "Alpha Skincrawler",
                "Bane",
                "Tentacle of Cure",
                "Cerubin the Rejected"
            };
            foreach (string name in unresolvedNames)
            {
                LootTableDefinition table = EmptyTable("crypt-of-home.test.inactive." + name);
                Assert.IsFalse(
                    DocumentedCryptOfHomeLootDefinitions.ApplyDocumentedLoot(
                        table,
                        DocumentedCryptOfHomeLootDefinitions.PlayfieldInstance,
                        name));
                Assert.AreEqual(0, table.RollGroups.Length);
                Assert.IsFalse(table.AllowsDocumentedSupplement);
            }
        }

        [TestMethod]
        public void GlobalLootLegacySeamUsesCryptOfHomePlayfieldScopedSupplement()
        {
            string source = File.ReadAllText(
                Path.Combine(
                    FindRepositoryRoot(),
                    @"AORebirth\Server\ZoneEngine\Core\Playfields\GlobalLootRuntimeService.cs"));
            StringAssert.Contains(source, "hasDocumentedCryptOfHomeLoot");
            StringAssert.Contains(
                source,
                "DocumentedCryptOfHomeLootDefinitions.ApplyDocumentedLoot");
            StringAssert.Contains(
                source,
                "DocumentedCryptOfHomeLootDefinitions.PlayfieldInstance");
        }

        [TestMethod]
        public void WikiAuditArtifactMatchesProductionDefinitions()
        {
            string artifactPath = Path.Combine(
                FindRepositoryRoot(),
                @"docs\generated\pf4805_loot\crypt-of-home-loot-membership-audit.json");
            Assert.IsTrue(File.Exists(artifactPath));
            var serializer = new JavaScriptSerializer { MaxJsonLength = int.MaxValue };
            var artifact = (Dictionary<string, object>)serializer.DeserializeObject(
                File.ReadAllText(artifactPath));

            Assert.AreEqual(36, Convert.ToInt32(artifact["source_item_count"]));
            Assert.AreEqual(64, Convert.ToInt32(artifact["documented_mapping_count"]));
            Assert.AreEqual(0, Convert.ToInt32(artifact["active_mapping_count"]));
            Assert.AreEqual(64, Convert.ToInt32(artifact["inactive_mapping_count"]));
            Assert.AreEqual(4805, Convert.ToInt32(artifact["playfield_instance"]));

            Dictionary<string, object>[] items = ((object[])artifact["items"])
                .Cast<Dictionary<string, object>>()
                .ToArray();
            CollectionAssert.AreEqual(
                DocumentedCryptOfHomeLootDefinitions.DocumentedSourceItemIds,
                items
                    .Select(value => Convert.ToInt32(value["item_id"]))
                    .OrderBy(value => value)
                    .ToArray());
            Assert.AreEqual(0, ((object[])artifact["active_mappings"]).Length);
        }

        private static CryptOfHomeDocumentedDropDefinition[] Drops(string displayName)
        {
            return DocumentedCryptOfHomeLootDefinitions.DropsForDisplayName(
                DocumentedCryptOfHomeLootDefinitions.PlayfieldInstance,
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
