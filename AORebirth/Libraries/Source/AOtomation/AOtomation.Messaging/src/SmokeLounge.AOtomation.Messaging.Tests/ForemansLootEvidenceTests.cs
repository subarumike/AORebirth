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
    public class ForemansLootEvidenceTests
    {
        [TestMethod]
        public void WikiRowsArePlayfieldScopedAndExcludeNonCorpseItems()
        {
            Assert.AreEqual(25, DocumentedForemansLootDefinitions.DocumentedDrops.Length);
            Assert.AreEqual(23, DocumentedForemansLootDefinitions.DocumentedSourceItemIds.Length);
            Assert.AreEqual(3, Drops("Unlisted Biomare Enemy").Length);
            Assert.AreEqual(4, Drops("Gunbeetle").Length);
            Assert.AreEqual(5, Drops("Bodyguard").Length);
            Assert.AreEqual(7, Drops("Lab Director").Length);
            Assert.AreEqual(10, Drops("Tri Plumbo").Length);
            Assert.AreEqual(5, Drops("T.I.M.").Length);
            Assert.AreEqual(
                0,
                DocumentedForemansLootDefinitions.DropsForDisplayName(
                    DocumentedInnerSanctumLootDefinitions.PlayfieldInstance,
                    "Lab Director").Length);

            int[] excluded =
            {
                259265, 258785, 259431, 155198, 155200, 156540, 156528, 156530
            };
            CollectionAssert.AreEqual(
                new int[0],
                DocumentedForemansLootDefinitions.DocumentedSourceItemIds
                    .Intersect(excluded)
                    .ToArray());
        }

        [TestMethod]
        public void WikiMembershipAddsSeparateGeneralAndNamedGroupsIdempotently()
        {
            LootTableDefinition bodyguard = EmptyTable("foremans.test.bodyguard");
            bodyguard.RollGroups = new[] { LegacyGroup(999999) };

            Assert.IsTrue(
                DocumentedForemansLootDefinitions.ApplyDocumentedMembership(
                    bodyguard,
                    DocumentedForemansLootDefinitions.PlayfieldInstance,
                    "Bodyguard"));
            Assert.AreEqual(3, bodyguard.RollGroups.Length);
            Assert.IsTrue(bodyguard.AllowsDocumentedSupplement);
            CollectionAssert.AreEqual(
                new[] { 136622, 136624, 136636 },
                Entries(bodyguard, DocumentedForemansLootDefinitions.EveryEnemyKey)
                    .Select(value => value.ItemTemplateId)
                    .OrderBy(value => value)
                    .ToArray());
            CollectionAssert.AreEqual(
                new[] { 156576, 156770 },
                Entries(bodyguard, DocumentedForemansLootDefinitions.BodyguardKey)
                    .Select(value => value.ItemTemplateId)
                    .OrderBy(value => value)
                    .ToArray());
            Assert.IsFalse(
                DocumentedForemansLootDefinitions.ApplyDocumentedMembership(
                    bodyguard,
                    DocumentedForemansLootDefinitions.PlayfieldInstance,
                    "Bodyguard"));
            Assert.AreEqual(3, bodyguard.RollGroups.Length);

            LootEntryDefinition notum = Entries(
                    bodyguard,
                    DocumentedForemansLootDefinitions.EveryEnemyKey)
                .Single(value => value.ItemTemplateId == 136622);
            Assert.AreEqual(136623, notum.HighItemTemplateId);
            Assert.AreEqual(30, notum.MinimumQuality);
            Assert.AreEqual(100, notum.MaximumQuality);
            Assert.AreEqual(0, notum.FixedQuality);
            Assert.AreEqual("unresolved-membership-only", notum.ProbabilityEvidence);

            LootTableDefinition triPlumbo = EmptyTable("foremans.test.tri-plumbo");
            Assert.IsTrue(
                DocumentedForemansLootDefinitions.ApplyDocumentedMembership(
                    triPlumbo,
                    DocumentedForemansLootDefinitions.PlayfieldInstance,
                    "Tri-Plumbo"));
            LootEntryDefinition carbonum = Entries(
                    triPlumbo,
                    DocumentedForemansLootDefinitions.TriPlumboKey)
                .Single(value => value.ItemTemplateId == 208253);
            Assert.AreEqual(208254, carbonum.HighItemTemplateId);
            Assert.AreEqual(60, carbonum.MinimumQuality);
            Assert.AreEqual(100, carbonum.MaximumQuality);
        }

        [TestMethod]
        public void WikiMembershipDoesNotCrossPlayfieldsOrBreakInnerSanctum()
        {
            LootTableDefinition wrongPlayfield = EmptyTable("foremans.test.wrong-pf");
            Assert.IsFalse(
                DocumentedForemansLootDefinitions.ApplyDocumentedMembership(
                    wrongPlayfield,
                    1931,
                    "Bodyguard"));
            Assert.AreEqual(0, wrongPlayfield.RollGroups.Length);

            LootTableDefinition innerSanctum = EmptyTable("foremans.test.inner-sanctum");
            Assert.IsTrue(
                DocumentedInnerSanctumLootDefinitions.ApplyDocumentedBossLoot(
                    innerSanctum,
                    DocumentedInnerSanctumLootDefinitions.PlayfieldInstance,
                    "Jeuru the Defiler"));
            Assert.AreEqual(18, innerSanctum.RollGroups.Length);
            Assert.IsFalse(
                DocumentedForemansLootDefinitions.ApplyDocumentedMembership(
                    innerSanctum,
                    DocumentedInnerSanctumLootDefinitions.PlayfieldInstance,
                    "Jeuru the Defiler"));
            Assert.AreEqual(18, innerSanctum.RollGroups.Length);
        }

        [TestMethod]
        public void GlobalLootLegacySeamUsesForemansPlayfieldScopedSupplement()
        {
            string source = File.ReadAllText(
                Path.Combine(
                    FindRepositoryRoot(),
                    @"AORebirth\Server\ZoneEngine\Core\Playfields\GlobalLootRuntimeService.cs"));
            StringAssert.Contains(source, "hasDocumentedForemansLoot");
            StringAssert.Contains(
                source,
                "DocumentedForemansLootDefinitions.ApplyDocumentedMembership");
            StringAssert.Contains(
                source,
                "DocumentedForemansLootDefinitions.PlayfieldInstance");
        }

        [TestMethod]
        public void WikiAuditArtifactMatchesProductionDefinitions()
        {
            string artifactPath = Path.Combine(
                FindRepositoryRoot(),
                @"docs\generated\pf1941_loot\foremans-loot-membership-audit.json");
            Assert.IsTrue(File.Exists(artifactPath));
            var serializer = new JavaScriptSerializer { MaxJsonLength = int.MaxValue };
            var artifact = (Dictionary<string, object>)serializer.DeserializeObject(
                File.ReadAllText(artifactPath));
            Assert.AreEqual(23, Convert.ToInt32(artifact["source_item_count"]));
            Assert.AreEqual(25, Convert.ToInt32(artifact["documented_mapping_count"]));
            Assert.AreEqual(12, Convert.ToInt32(artifact["drop_scope_count"]));
            Assert.AreEqual(0, Convert.ToInt32(artifact["production_pf1941_spawn_count"]));

            Dictionary<string, object>[] items = ((object[])artifact["items"])
                .Cast<Dictionary<string, object>>()
                .ToArray();
            CollectionAssert.AreEqual(
                DocumentedForemansLootDefinitions.DocumentedSourceItemIds,
                items
                    .Select(value => Convert.ToInt32(value["item_id"]))
                    .OrderBy(value => value)
                    .ToArray());

            string[] productionMappings = DocumentedForemansLootDefinitions.DocumentedDrops
                .Select(value => value.EnemyKey + ":" + value.ItemTemplateId)
                .OrderBy(value => value)
                .ToArray();
            string[] artifactMappings = ((object[])artifact["mappings"])
                .Cast<Dictionary<string, object>>()
                .Select(value => value["enemy_key"] + ":" + value["item_id"])
                .OrderBy(value => value)
                .ToArray();
            CollectionAssert.AreEqual(productionMappings, artifactMappings);
        }

        private static ForemansDocumentedDropDefinition[] Drops(string displayName)
        {
            return DocumentedForemansLootDefinitions.DropsForDisplayName(
                DocumentedForemansLootDefinitions.PlayfieldInstance,
                displayName);
        }

        private static LootEntryDefinition[] Entries(
            LootTableDefinition table,
            string enemyKey)
        {
            return table.RollGroups
                .Single(
                    value => string.Equals(
                        value.LootGroupKey,
                        DocumentedForemansLootDefinitions.DocumentedLootGroupPrefix + enemyKey,
                        StringComparison.Ordinal))
                .Entries;
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
