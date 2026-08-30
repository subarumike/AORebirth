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
    public class StepsOfMadnessLootEvidenceTests
    {
        [TestMethod]
        public void WikiRowsArePlayfieldScopedAndConservativelyActivated()
        {
            Assert.AreEqual(61, DocumentedStepsOfMadnessLootDefinitions.DocumentedDrops.Length);
            Assert.AreEqual(22, DocumentedStepsOfMadnessLootDefinitions.DocumentedSourceItemIds.Length);
            Assert.AreEqual(
                7,
                DocumentedStepsOfMadnessLootDefinitions.DocumentedDrops.Count(value => value.IsActive));
            Assert.AreEqual(
                54,
                DocumentedStepsOfMadnessLootDefinitions.DocumentedDrops.Count(value => !value.IsActive));

            Assert.AreEqual(7, Drops("Unlisted Steps of Madness Enemy").Length);
            Assert.AreEqual(9, Drops("Pulsing Hatred").Length);
            Assert.AreEqual(14, Drops("Notum Habit").Length);
            Assert.AreEqual(12, Drops("Neleb the Deranged").Length);
            Assert.AreEqual(0, Drops("Fragment of Sanity").Count(value => value.IsActive));
            Assert.AreEqual(
                0,
                DocumentedStepsOfMadnessLootDefinitions.DropsForDisplayName(
                    DocumentedForemansLootDefinitions.PlayfieldInstance,
                    "Neleb the Deranged").Length);
        }

        [TestMethod]
        public void ExactNamedDropsUsePublishedGuaranteedRates()
        {
            LootTableDefinition pulsingHatred = EmptyTable("steps.test.pulsing-hatred");
            LootTableDefinition notumHabit = EmptyTable("steps.test.notum-habit");

            Assert.IsTrue(
                DocumentedStepsOfMadnessLootDefinitions.ApplyDocumentedLoot(
                    pulsingHatred,
                    DocumentedStepsOfMadnessLootDefinitions.PlayfieldInstance,
                    "Pulsing Hatred"));
            Assert.IsTrue(
                DocumentedStepsOfMadnessLootDefinitions.ApplyDocumentedLoot(
                    notumHabit,
                    DocumentedStepsOfMadnessLootDefinitions.PlayfieldInstance,
                    "Notum Habit"));

            AssertGuaranteed(pulsingHatred, 152025, 50);
            AssertGuaranteed(notumHabit, 152027, 50);
            Assert.IsFalse(
                DocumentedStepsOfMadnessLootDefinitions.ApplyDocumentedLoot(
                    pulsingHatred,
                    DocumentedStepsOfMadnessLootDefinitions.PlayfieldInstance,
                    "Pulsing Hatred"));
            Assert.AreEqual(1, pulsingHatred.RollGroups.Length);
        }

        [TestMethod]
        public void NelebUsesFourGuaranteedDropsAndExactDarkDreamsOutcomeWeights()
        {
            LootTableDefinition neleb = EmptyTable("steps.test.neleb");

            Assert.IsTrue(
                DocumentedStepsOfMadnessLootDefinitions.ApplyDocumentedLoot(
                    neleb,
                    DocumentedStepsOfMadnessLootDefinitions.PlayfieldInstance,
                    "Neleb the Deranged"));
            Assert.AreEqual(5, neleb.RollGroups.Length);
            CollectionAssert.AreEqual(
                new[] { 151895, 151896, 152026, 274971, 274971, 274972 },
                neleb.RollGroups
                    .SelectMany(value => value.Entries)
                    .Select(value => value.ItemTemplateId)
                    .OrderBy(value => value)
                    .ToArray());

            LootGroupDefinition darkDreams = neleb.RollGroups.Single(
                value => value.LootGroupKey.EndsWith(
                    "." + DocumentedStepsOfMadnessLootDefinitions.DarkDreamsItemId,
                    StringComparison.Ordinal));
            Assert.AreEqual(LootRollMode.WeightedOne, darkDreams.RollMode);
            Assert.AreEqual(45, darkDreams.EmptyWeight);
            Assert.AreEqual(10000, darkDreams.DropChanceBasisPoints);
            CollectionAssert.AreEqual(
                new[] { "one-copy:1:40", "two-copies:2:15" },
                darkDreams.Entries
                    .OrderBy(value => value.MinimumQuantity)
                    .Select(
                        value => value.SelectionKey
                                 + ":"
                                 + value.MinimumQuantity
                                 + ":"
                                 + value.Weight)
                    .ToArray());
            Assert.IsTrue(neleb.AllowsDocumentedSupplement);
        }

        [TestMethod]
        public void QualitativeAndSameNameAmbiguousMembershipRemainsInactive()
        {
            string[] unresolvedNames =
            {
                "Unrelenting Fear",
                "Fragment of Sanity",
                "Suppressed Emotion",
                "Mind Shard",
                "Sanity's Edge",
                "Thief of Reason",
                "Betrayer of Memory",
                "Unlisted Steps of Madness Enemy"
            };
            foreach (string name in unresolvedNames)
            {
                LootTableDefinition table = EmptyTable("steps.test.inactive." + name);
                Assert.IsFalse(
                    DocumentedStepsOfMadnessLootDefinitions.ApplyDocumentedLoot(
                        table,
                        DocumentedStepsOfMadnessLootDefinitions.PlayfieldInstance,
                        name));
                Assert.AreEqual(0, table.RollGroups.Length);
            }

            LootTableDefinition wrongPlayfield = EmptyTable("steps.test.wrong-pf");
            Assert.IsFalse(
                DocumentedStepsOfMadnessLootDefinitions.ApplyDocumentedLoot(
                    wrongPlayfield,
                    1931,
                    "Neleb the Deranged"));
            Assert.AreEqual(0, wrongPlayfield.RollGroups.Length);
        }

        [TestMethod]
        public void ExistingRepositoryDropIsNotDuplicated()
        {
            LootTableDefinition table = EmptyTable("steps.test.existing");
            table.RollGroups = new[] { LegacyGroup(152025) };

            Assert.IsFalse(
                DocumentedStepsOfMadnessLootDefinitions.ApplyDocumentedLoot(
                    table,
                    DocumentedStepsOfMadnessLootDefinitions.PlayfieldInstance,
                    "Pulsing Hatred"));
            Assert.AreEqual(1, table.RollGroups.Length);
            Assert.IsFalse(table.AllowsDocumentedSupplement);

            LootTableDefinition captured = EmptyTable("steps.test.captured");
            captured.ObservedCorpseSnapshots = new[]
            {
                new ObservedCorpseSnapshotDefinition
                {
                    SnapshotKey = "captured.nervejolter",
                    Entries = LegacyGroup(152025).Entries
                }
            };
            Assert.IsFalse(
                DocumentedStepsOfMadnessLootDefinitions.ApplyDocumentedLoot(
                    captured,
                    DocumentedStepsOfMadnessLootDefinitions.PlayfieldInstance,
                    "Pulsing Hatred"));
            Assert.AreEqual(0, captured.RollGroups.Length);
        }

        [TestMethod]
        public void GlobalLootLegacySeamUsesStepsOfMadnessPlayfieldScopedSupplement()
        {
            string source = File.ReadAllText(
                Path.Combine(
                    FindRepositoryRoot(),
                    @"AORebirth\Server\ZoneEngine\Core\Playfields\GlobalLootRuntimeService.cs"));
            StringAssert.Contains(source, "hasDocumentedStepsOfMadnessLoot");
            StringAssert.Contains(
                source,
                "DocumentedStepsOfMadnessLootDefinitions.ApplyDocumentedLoot");
            StringAssert.Contains(
                source,
                "DocumentedStepsOfMadnessLootDefinitions.PlayfieldInstance");
        }

        [TestMethod]
        public void WikiAuditArtifactMatchesProductionDefinitions()
        {
            string artifactPath = Path.Combine(
                FindRepositoryRoot(),
                @"docs\generated\pf1933_loot\steps-of-madness-loot-membership-audit.json");
            Assert.IsTrue(File.Exists(artifactPath));
            var serializer = new JavaScriptSerializer { MaxJsonLength = int.MaxValue };
            var artifact = (Dictionary<string, object>)serializer.DeserializeObject(
                File.ReadAllText(artifactPath));

            Assert.AreEqual(22, Convert.ToInt32(artifact["source_item_count"]));
            Assert.AreEqual(61, Convert.ToInt32(artifact["documented_mapping_count"]));
            Assert.AreEqual(7, Convert.ToInt32(artifact["active_mapping_count"]));
            Assert.AreEqual(54, Convert.ToInt32(artifact["inactive_mapping_count"]));
            Assert.AreEqual(1933, Convert.ToInt32(artifact["playfield_instance"]));

            Dictionary<string, object>[] items = ((object[])artifact["items"])
                .Cast<Dictionary<string, object>>()
                .ToArray();
            CollectionAssert.AreEqual(
                DocumentedStepsOfMadnessLootDefinitions.DocumentedSourceItemIds,
                items
                    .Select(value => Convert.ToInt32(value["item_id"]))
                    .OrderBy(value => value)
                    .ToArray());

            string[] productionActive = DocumentedStepsOfMadnessLootDefinitions.DocumentedDrops
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

        private static StepsOfMadnessDocumentedDropDefinition[] Drops(string displayName)
        {
            return DocumentedStepsOfMadnessLootDefinitions.DropsForDisplayName(
                DocumentedStepsOfMadnessLootDefinitions.PlayfieldInstance,
                displayName);
        }

        private static void AssertGuaranteed(
            LootTableDefinition table,
            int itemTemplateId,
            int quality)
        {
            Assert.AreEqual(1, table.RollGroups.Length);
            LootGroupDefinition group = table.RollGroups.Single();
            LootEntryDefinition entry = group.Entries.Single();
            Assert.AreEqual(LootRollMode.Independent, group.RollMode);
            Assert.AreEqual(10000, group.DropChanceBasisPoints);
            Assert.AreEqual(itemTemplateId, entry.ItemTemplateId);
            Assert.AreEqual(quality, entry.FixedQuality);
            Assert.AreEqual(10000, entry.DropChanceBasisPoints);
            Assert.AreEqual(
                DocumentedStepsOfMadnessLootDefinitions.DocumentedLootSourceUrl,
                entry.EvidenceReference);
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
