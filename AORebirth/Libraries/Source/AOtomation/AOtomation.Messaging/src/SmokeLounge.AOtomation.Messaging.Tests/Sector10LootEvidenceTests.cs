namespace SmokeLounge.AOtomation.Messaging.Tests
{
    using System.Linq;

    using AORebirth.Core.Playfields;

    using Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestClass]
    public class Sector10LootEvidenceTests
    {
        [TestMethod]
        public void ExactNameStatsResolveThreeDistinctBossProfiles()
        {
            string profileKey;
            Assert.IsTrue(CapturedSector10LootDefinitions.TryResolveProfile(
                "Ilari Khazoh Ra", 257313, 190, out profileKey));
            Assert.AreEqual(CapturedSector10LootDefinitions.IlariProfileKey, profileKey);
            Assert.IsTrue(CapturedSector10LootDefinitions.TryResolveProfile(
                "Ankari Khazoh Ra", 257313, 190, out profileKey));
            Assert.AreEqual(CapturedSector10LootDefinitions.AnkariProfileKey, profileKey);
            Assert.IsTrue(CapturedSector10LootDefinitions.TryResolveProfile(
                "Cha Khazoh Ra", 257313, 190, out profileKey));
            Assert.AreEqual(CapturedSector10LootDefinitions.ChaProfileKey, profileKey);

            Assert.IsFalse(CapturedSector10LootDefinitions.TryResolveProfile(
                "Unknown Khazoh Ra", 257313, 190, out profileKey));
            Assert.IsFalse(CapturedSector10LootDefinitions.TryResolveProfile(
                "Ilari Khazoh Ra", 257312, 190, out profileKey));
            Assert.IsFalse(CapturedSector10LootDefinitions.TryResolveProfile(
                "Ilari Khazoh Ra", 257313, 189, out profileKey));
        }

        [TestMethod]
        public void IdentityLinkedCorpusPreservesEveryCapturedOutcome()
        {
            LootTableDefinition ilari = CapturedSector10LootDefinitions.BuildIlariLootTable();
            LootTableDefinition ankari = CapturedSector10LootDefinitions.BuildAnkariLootTable();
            LootTableDefinition cha = CapturedSector10LootDefinitions.BuildChaLootTable();

            AssertTable(ilari, new[] { 6, 7, 7, 7, 7 });
            AssertTable(ankari, new[] { 6, 8, 8, 7 });
            AssertTable(cha, new[] { 7 });

            Assert.AreEqual(2, BotEntries(ilari).Length);
            AssertBot(BotEntries(ilari)[0], 247140, 247141, 222);
            AssertBot(BotEntries(ilari)[1], 247140, 247141, 162);

            Assert.AreEqual(3, BotEntries(ankari).Length);
            AssertBot(BotEntries(ankari)[0], 247138, 247139, 190);
            AssertBot(BotEntries(ankari)[1], 247144, 247145, 163);
            AssertBot(BotEntries(ankari)[2], 247136, 247137, 157);

            Assert.AreEqual(0, BotEntries(cha).Length);
        }

        [TestMethod]
        public void RegistrationIsExactAndIdempotent()
        {
            var registry = new LootTableRegistry(itemId => itemId > 0);
            string profileKey;

            Assert.IsTrue(CapturedSector10LootDefinitions.TryRegister(
                registry, "Ilari Khazoh Ra", 257313, 190, out profileKey));
            Assert.IsTrue(registry.ContainsTable("captured." + profileKey));
            Assert.IsTrue(CapturedSector10LootDefinitions.TryRegister(
                registry, "Ilari Khazoh Ra", 257313, 190, out profileKey));
            Assert.IsTrue(registry.ContainsTable("captured." + profileKey));

            Assert.IsFalse(CapturedSector10LootDefinitions.TryRegister(
                registry, "Ilari Khazoh Ra", 257313, 191, out profileKey));
        }

        [TestMethod]
        public void RuntimeGenerationPreservesDuplicateItemsAsSeparateCorpseRows()
        {
            var registry = new LootTableRegistry(itemId => itemId > 0);
            string profileKey;
            Assert.IsTrue(CapturedSector10LootDefinitions.TryRegister(
                registry, "Ilari Khazoh Ra", 257313, 190, out profileKey));
            var generator = new LootGenerationService(
                registry,
                new LootAssignmentResolver());
            var context = new LootGenerationContext
            {
                EnemyProfileKey = profileKey,
                MonsterData = 257313,
                Level = 190,
                IsBoss = true
            };

            for (int seed = 0; seed < 20; seed++)
            {
                LootGenerationResult result = generator.Generate(
                    context,
                    new SeededLootRandomSource(seed));
                Assert.IsTrue(result.Items.Count == 6 || result.Items.Count == 7);
                Assert.AreEqual(
                    2,
                    result.Items.Count(item => item.ItemTemplateId == 257968));
                Assert.IsTrue(result.Items.All(item => item.Quantity == 1));
                Assert.AreEqual(35507, result.Credits);
            }
        }

        private static void AssertTable(LootTableDefinition table, int[] expectedItemCounts)
        {
            Assert.IsTrue(table.Enabled);
            Assert.AreEqual(LootTableType.Boss, table.TableType);
            Assert.IsTrue(table.ItemPoolUnresolved);
            Assert.AreEqual(0, table.RollGroups.Length);
            Assert.AreEqual(expectedItemCounts.Length, table.ObservedCorpseSnapshots.Length);
            CollectionAssert.AreEqual(
                expectedItemCounts,
                table.ObservedCorpseSnapshots
                    .Select(snapshot => snapshot.Entries.Length)
                    .ToArray());

            foreach (ObservedCorpseSnapshotDefinition snapshot in table.ObservedCorpseSnapshots)
            {
                Assert.AreEqual(35507, snapshot.Credits);
                Assert.AreEqual(LootEvidenceConfidence.ProvenCapture, snapshot.Evidence);
                Assert.AreEqual(
                    LootEvidenceConfidence.Unresolved,
                    snapshot.SelectionProbabilityEvidence);

                LootEntryDefinition gauge = snapshot.Entries.Single(entry => entry.ItemTemplateId == 287147);
                Assert.AreEqual(200, gauge.FixedQuality);
                Assert.AreEqual(1, gauge.MaximumQuantity);
                LootEntryDefinition[] hackers = snapshot.Entries
                    .Where(entry => entry.ItemTemplateId == 257968)
                    .ToArray();
                Assert.AreEqual(2, hackers.Length);
                Assert.IsTrue(snapshot.Entries.All(entry => entry.MaximumQuantity == 1));
                Assert.IsTrue(hackers.All(entry => entry.FixedQuality == 1));
            }
        }

        private static LootEntryDefinition[] BotEntries(LootTableDefinition table)
        {
            return table.ObservedCorpseSnapshots
                .SelectMany(snapshot => snapshot.Entries)
                .Where(entry => entry.ItemTemplateId >= 247136 && entry.ItemTemplateId <= 247145)
                .ToArray();
        }

        private static void AssertBot(
            LootEntryDefinition entry,
            int lowItemId,
            int highItemId,
            int quality)
        {
            Assert.AreEqual(lowItemId, entry.ItemTemplateId);
            Assert.AreEqual(highItemId, entry.HighItemTemplateId);
            Assert.AreEqual(quality, entry.FixedQuality);
            Assert.AreEqual(1, entry.MaximumQuantity);
        }
    }
}
