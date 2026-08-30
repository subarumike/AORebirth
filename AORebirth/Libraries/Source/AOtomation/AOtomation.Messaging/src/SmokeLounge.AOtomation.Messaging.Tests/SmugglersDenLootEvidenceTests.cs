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
    public class SmugglersDenLootEvidenceTests
    {
        [TestMethod]
        public void WikiRowsArePlayfieldAndEnemyNameScoped()
        {
            Assert.AreEqual(23, DocumentedSmugglersDenLootDefinitions.DocumentedDrops.Length);
            Assert.AreEqual(22, DocumentedSmugglersDenLootDefinitions.DocumentedSourceItemIds.Length);
            Assert.AreEqual(
                1,
                DocumentedSmugglersDenLootDefinitions.DocumentedDrops.Count(value => value.IsActive));
            Assert.AreEqual(
                22,
                DocumentedSmugglersDenLootDefinitions.DocumentedDrops.Count(value => !value.IsActive));

            Assert.AreEqual(12, Drops("Den Mantis Digger").Length);
            Assert.AreEqual(12, Drops("Den Mantis Worker").Length);
            Assert.AreEqual(2, Drops("Den Mantis Breeder").Length);
            Assert.AreEqual(2, Drops("Den Smuggler Pilot").Length);
            Assert.AreEqual(4, Drops("Clawfinger Forefather").Length);
            Assert.AreEqual(3, Drops("Den Mantis Queen").Length);
            Assert.AreEqual(0, Drops("Den Smuggler").Length);
            Assert.AreEqual(
                0,
                DocumentedSmugglersDenLootDefinitions.DropsForDisplayName(
                    DocumentedStepsOfMadnessLootDefinitions.PlayfieldInstance,
                    "Den Mantis Queen").Length);
        }

        [TestMethod]
        public void QueenMantisEggUsesPublishedGuaranteedRate()
        {
            LootTableDefinition queen = EmptyTable("smugglers-den.test.queen");

            Assert.IsTrue(
                DocumentedSmugglersDenLootDefinitions.ApplyDocumentedLoot(
                    queen,
                    DocumentedSmugglersDenLootDefinitions.PlayfieldInstance,
                    "Den Mantis Queen"));
            Assert.AreEqual(1, queen.RollGroups.Length);
            LootGroupDefinition group = queen.RollGroups.Single();
            LootEntryDefinition entry = group.Entries.Single();
            Assert.AreEqual(LootRollMode.Independent, group.RollMode);
            Assert.AreEqual(10000, group.DropChanceBasisPoints);
            Assert.AreEqual(DocumentedSmugglersDenLootDefinitions.MantisEggItemId, entry.ItemTemplateId);
            Assert.AreEqual(190, entry.FixedQuality);
            Assert.AreEqual(10000, entry.DropChanceBasisPoints);
            Assert.AreEqual(
                DocumentedSmugglersDenLootDefinitions.ExactProbabilitySourceUrl,
                entry.EvidenceReference);
            Assert.IsTrue(queen.AllowsDocumentedSupplement);
        }

        [TestMethod]
        public void UnresolvedMembershipNeverInventsRatesOrOutcomes()
        {
            string[] unresolvedNames =
            {
                "Den Mantis Digger",
                "Den Mantis Breeder",
                "Den Smuggler Pilot",
                "Clawfinger Forefather"
            };
            foreach (string name in unresolvedNames)
            {
                LootTableDefinition table = EmptyTable("smugglers-den.test.inactive." + name);
                Assert.IsFalse(
                    DocumentedSmugglersDenLootDefinitions.ApplyDocumentedLoot(
                        table,
                        DocumentedSmugglersDenLootDefinitions.PlayfieldInstance,
                        name));
                Assert.AreEqual(0, table.RollGroups.Length);
            }

            LootTableDefinition wrongPlayfield = EmptyTable("smugglers-den.test.wrong-pf");
            Assert.IsFalse(
                DocumentedSmugglersDenLootDefinitions.ApplyDocumentedLoot(
                    wrongPlayfield,
                    1933,
                    "Den Mantis Queen"));
            Assert.AreEqual(0, wrongPlayfield.RollGroups.Length);
        }

        [TestMethod]
        public void ExistingMantisEggIsNotDuplicated()
        {
            LootTableDefinition table = EmptyTable("smugglers-den.test.existing");
            table.RollGroups = new[]
            {
                LegacyGroup(DocumentedSmugglersDenLootDefinitions.MantisEggItemId)
            };

            Assert.IsFalse(
                DocumentedSmugglersDenLootDefinitions.ApplyDocumentedLoot(
                    table,
                    DocumentedSmugglersDenLootDefinitions.PlayfieldInstance,
                    "Den Mantis Queen"));
            Assert.AreEqual(1, table.RollGroups.Length);
            Assert.IsFalse(table.AllowsDocumentedSupplement);

            LootTableDefinition captured = EmptyTable("smugglers-den.test.captured");
            captured.ObservedCorpseSnapshots = new[]
            {
                new ObservedCorpseSnapshotDefinition
                {
                    SnapshotKey = "captured.mantis-egg",
                    Entries = LegacyGroup(
                        DocumentedSmugglersDenLootDefinitions.MantisEggItemId).Entries
                }
            };
            Assert.IsFalse(
                DocumentedSmugglersDenLootDefinitions.ApplyDocumentedLoot(
                    captured,
                    DocumentedSmugglersDenLootDefinitions.PlayfieldInstance,
                    "Den Mantis Queen"));
            Assert.AreEqual(0, captured.RollGroups.Length);
        }

        [TestMethod]
        public void GlobalLootLegacySeamUsesSmugglersDenPlayfieldScopedSupplement()
        {
            string source = File.ReadAllText(
                Path.Combine(
                    FindRepositoryRoot(),
                    @"AORebirth\Server\ZoneEngine\Core\Playfields\GlobalLootRuntimeService.cs"));
            StringAssert.Contains(source, "hasDocumentedSmugglersDenLoot");
            StringAssert.Contains(
                source,
                "DocumentedSmugglersDenLootDefinitions.ApplyDocumentedLoot");
            StringAssert.Contains(
                source,
                "DocumentedSmugglersDenLootDefinitions.PlayfieldInstance");
        }

        [TestMethod]
        public void WikiAuditArtifactMatchesProductionDefinitions()
        {
            string artifactPath = Path.Combine(
                FindRepositoryRoot(),
                @"docs\generated\pf1862_loot\smugglers-den-loot-membership-audit.json");
            Assert.IsTrue(File.Exists(artifactPath));
            var serializer = new JavaScriptSerializer { MaxJsonLength = int.MaxValue };
            var artifact = (Dictionary<string, object>)serializer.DeserializeObject(
                File.ReadAllText(artifactPath));

            Assert.AreEqual(22, Convert.ToInt32(artifact["source_item_count"]));
            Assert.AreEqual(23, Convert.ToInt32(artifact["documented_mapping_count"]));
            Assert.AreEqual(1, Convert.ToInt32(artifact["active_mapping_count"]));
            Assert.AreEqual(22, Convert.ToInt32(artifact["inactive_mapping_count"]));
            Assert.AreEqual(1862, Convert.ToInt32(artifact["playfield_instance"]));

            Dictionary<string, object>[] items = ((object[])artifact["items"])
                .Cast<Dictionary<string, object>>()
                .ToArray();
            CollectionAssert.AreEqual(
                DocumentedSmugglersDenLootDefinitions.DocumentedSourceItemIds,
                items
                    .Select(value => Convert.ToInt32(value["item_id"]))
                    .OrderBy(value => value)
                    .ToArray());

            string[] productionActive = DocumentedSmugglersDenLootDefinitions.DocumentedDrops
                .Where(value => value.IsActive)
                .Select(value => value.EnemyKey + ":" + value.ItemTemplateId)
                .OrderBy(value => value, StringComparer.Ordinal)
                .ToArray();
            string[] artifactActive = ((object[])artifact["active_mappings"])
                .Cast<Dictionary<string, object>>()
                .Select(value => value["enemy_key"] + ":" + value["item_id"])
                .OrderBy(value => value, StringComparer.Ordinal)
                .ToArray();
            CollectionAssert.AreEqual(productionActive, artifactActive);
        }

        private static SmugglersDenDocumentedDropDefinition[] Drops(string displayName)
        {
            return DocumentedSmugglersDenLootDefinitions.DropsForDisplayName(
                DocumentedSmugglersDenLootDefinitions.PlayfieldInstance,
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
                        FixedQuality = 190,
                        MinimumQuality = 190,
                        MaximumQuality = 190,
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
