namespace SmokeLounge.AOtomation.Messaging.Tests
{
    using System;
    using System.IO;
    using System.Linq;

    using AORebirth.Core.Playfields;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestClass]
    public class GlobalLootFoundationTests
    {
        [TestMethod]
        public void RegistryRejectsDuplicateMissingInvalidAndEvidenceUnsafeDefinitions()
        {
            LootTableRegistry registry = Registry();
            registry.RegisterTable(Table("valid", LootRollMode.Guaranteed, Guaranteed(1001), CreditsPolicyMode.None, 0, 0));
            AssertThrows<LootDefinitionValidationException>(() =>
                registry.RegisterTable(Table("valid", LootRollMode.Guaranteed, Guaranteed(1001), CreditsPolicyMode.None, 0, 0)));
            AssertThrows<LootDefinitionValidationException>(() => registry.RegisterAssignment(Assignment("missing", "missing", LootAssignmentTargetType.Global, null, 0)));
            AssertThrows<LootDefinitionValidationException>(() =>
                registry.RegisterTable(Table("unknown", LootRollMode.Guaranteed, Guaranteed(9999), CreditsPolicyMode.None, 0, 0)));

            LootEntryDefinition invalidQuality = Guaranteed(1001);
            invalidQuality.MinimumQuality = 5;
            invalidQuality.MaximumQuality = 4;
            AssertThrows<LootDefinitionValidationException>(() =>
                registry.RegisterTable(Table("quality", LootRollMode.Guaranteed, invalidQuality, CreditsPolicyMode.None, 0, 0)));

            LootEntryDefinition observedGuaranteed = Guaranteed(1001);
            observedGuaranteed.Semantics = LootSemantics.ObservedAvailable;
            observedGuaranteed.Evidence = LootEvidenceConfidence.ObservedAvailableLoot;
            AssertThrows<LootDefinitionValidationException>(() =>
                registry.RegisterTable(Table("observed", LootRollMode.Guaranteed, observedGuaranteed, CreditsPolicyMode.None, 0, 0)));
        }

        [TestMethod]
        public void GuaranteedIndependentWeightedQualityQuantityAndUniqueGenerationAreDeterministic()
        {
            LootTableRegistry registry = Registry();
            LootTableDefinition table = Table("modes", LootRollMode.Guaranteed, Guaranteed(1001), CreditsPolicyMode.Range, 4, 8);
            table.RollGroups = new[]
            {
                Group("guaranteed", LootRollMode.Guaranteed, 1, 10000, 0, Guaranteed(1001)),
                Group("independent", LootRollMode.Independent, 1, 10000, 0, Ranged(1002, 2, 5, 2, 4, 10000)),
                Group("weighted", LootRollMode.WeightedOne, 1, 10000, 0, Weighted(1003, 1), Weighted(1004, 3)),
                Group("unique", LootRollMode.WeightedMany, 4, 10000, 0, Unique(1005, 1))
            };
            registry.RegisterTable(table);
            registry.RegisterAssignment(Assignment("global", "modes", LootAssignmentTargetType.Global, null, 0));
            LootGenerationService service = Service(registry);
            LootGenerationContext context = Context();
            LootGenerationResult first = service.Generate(context, new SeededLootRandomSource(41));
            LootGenerationResult second = service.Generate(context, new SeededLootRandomSource(41));

            CollectionAssert.AreEqual(
                first.Items.Select(x => x.ItemTemplateId + ":" + x.Quality + ":" + x.Quantity).ToArray(),
                second.Items.Select(x => x.ItemTemplateId + ":" + x.Quality + ":" + x.Quantity).ToArray());
            Assert.AreEqual(first.Credits, second.Credits);
            Assert.AreEqual(1, first.Items.Count(x => x.ItemTemplateId == 1005));
            Assert.IsTrue(first.Items.Any(x => x.ItemTemplateId == 1002 && x.Quality >= 2 && x.Quality <= 5));
            Assert.IsTrue(first.Credits >= 4 && first.Credits <= 8);
        }

        [TestMethod]
        public void AssignmentPrecedenceAccumulatesStableGlobalFamilyEnemyDynaBossAndEncounterLayers()
        {
            LootTableRegistry registry = Registry();
            RegisterLayer(registry, "global", 1001, LootAssignmentTargetType.Global, null, 0);
            RegisterLayer(registry, "family", 1002, LootAssignmentTargetType.Family, "flea", 0);
            RegisterLayer(registry, "enemy", 1003, LootAssignmentTargetType.EnemyType, "filth-flea", 0);
            RegisterLayer(registry, "dyna", 1004, LootAssignmentTargetType.DynaGlobal, "any", 0);
            RegisterLayer(registry, "band", 1005, LootAssignmentTargetType.DynaLevelBand, "1-25", 0);
            RegisterLayer(registry, "boss", 1006, LootAssignmentTargetType.Boss, "filth-flea", 0);
            RegisterLayer(registry, "encounter", 1007, LootAssignmentTargetType.Encounter, "boss-room", 0);
            LootGenerationContext context = Context();
            context.FamilyKey = "flea";
            context.EnemyProfileKey = "filth-flea";
            context.IsDyna = true;
            context.IsBoss = true;
            context.DynaLevelBandKey = "1-25";
            context.EncounterKey = "boss-room";
            LootGenerationResult result = Service(registry).Generate(context, new SeededLootRandomSource(1));
            CollectionAssert.AreEqual(new[] { "global", "family", "enemy", "dyna", "band", "boss", "encounter" }, result.AppliedTableKeys.ToArray());
            CollectionAssert.AreEqual(new[] { 1001, 1002, 1003, 1004, 1005, 1006, 1007 }, result.Items.Select(x => x.ItemTemplateId).ToArray());
        }

        [TestMethod]
        public void NoAssignmentUnresolvedAndOwnedSummonPathsFailClosed()
        {
            LootTableRegistry registry = Registry();
            LootGenerationService service = Service(registry);
            LootGenerationResult missing = service.Generate(Context(), new SeededLootRandomSource(1));
            Assert.IsTrue(missing.LootUnresolved);
            Assert.AreEqual(0, missing.Items.Count);

            LootGenerationContext owned = Context();
            owned.IsOwnedSummon = true;
            LootGenerationResult summon = service.Generate(owned, new SeededLootRandomSource(1));
            Assert.IsFalse(summon.LootUnresolved);
            Assert.AreEqual(0, summon.Items.Count);
        }

        [TestMethod]
        public void CreditsNoneFixedRangeAndUnresolvedRemainDistinct()
        {
            AssertCredits(CreditsPolicyMode.None, 0, 0, 0, false);
            AssertCredits(CreditsPolicyMode.Fixed, 7, 7, 7, false);
            LootGenerationResult ranged = GenerateCredits(CreditsPolicyMode.Range, 3, 9);
            Assert.IsTrue(ranged.Credits >= 3 && ranged.Credits <= 9);
            LootGenerationResult unresolved = GenerateCredits(CreditsPolicyMode.Unresolved, 0, 0);
            Assert.IsTrue(unresolved.CreditsUnresolved);
        }

        [TestMethod]
        public void ArchitectureGuardrailsKeepLootOwnershipOutOfPlayfieldAndEnemyBranches()
        {
            string root = FindRepositoryRoot();
            string playfield = File.ReadAllText(Path.Combine(root, @"AORebirth\Server\ZoneEngine\Core\Playfields\Playfield.cs"));
            string runtime = File.ReadAllText(Path.Combine(root, @"AORebirth\Server\ZoneEngine\Core\Playfields\OrdinaryEnemyRuntimeService.cs"));
            string corpseService = File.ReadAllText(Path.Combine(root, @"AORebirth\Server\ZoneEngine\Core\Playfields\CorpseInventoryService.cs"));
            Assert.IsFalse(playfield.Contains("RollCorpseLootItems") || playfield.Contains("GetDatabaseLootTable") || playfield.Contains("DebugLootTable"));
            Assert.IsTrue(playfield.Contains("GlobalLootRuntimeService.Generate"));
            Assert.IsFalse(runtime.Contains("AddItem(") || runtime.Contains("LootTableDefinition"));
            Assert.IsTrue(corpseService.Contains("CorpseState Create(")
                && corpseService.Contains("bool RemoveItem(")
                && corpseService.Contains("bool RemoveCredits(")
                && corpseService.Contains("int ClearPlayfield(")
                && corpseService.Contains("void ClearAll("));
            Assert.IsTrue(File.Exists(Path.Combine(root, @"docs\architecture\AO_REBIRTH_LOOT_ARCHITECTURE.md")));
        }

        private static LootTableRegistry Registry() { return new LootTableRegistry(value => value >= 1001 && value <= 1007); }
        private static LootGenerationService Service(LootTableRegistry registry) { return new LootGenerationService(registry, new LootAssignmentResolver()); }
        private static LootGenerationContext Context() { return new LootGenerationContext { EnemyProfileKey = "enemy", FamilyKey = "family", Level = 5, PlayfieldId = 127 }; }

        private static void RegisterLayer(LootTableRegistry registry, string key, int item, LootAssignmentTargetType type, string target, int priority)
        {
            registry.RegisterTable(Table(key, LootRollMode.Guaranteed, Guaranteed(item), CreditsPolicyMode.None, 0, 0));
            registry.RegisterAssignment(Assignment(key, key, type, target, priority));
        }

        private static LootTableDefinition Table(string key, LootRollMode mode, LootEntryDefinition entry, CreditsPolicyMode credits, int min, int max)
        {
            return new LootTableDefinition
            {
                LootTableKey = key, DisplayName = key, TableType = LootTableType.EnemyType,
                RollGroups = new[] { Group("group", mode, 1, 10000, 0, entry) },
                CreditsPolicy = new CreditsPolicyDefinition { Mode = credits, MinimumCredits = min, MaximumCredits = max, Evidence = LootEvidenceConfidence.ProvenRepository },
                QualityPolicy = "test", Evidence = "test", Confidence = LootEvidenceConfidence.ProvenRepository, Enabled = true
            };
        }

        private static LootGroupDefinition Group(string key, LootRollMode mode, int count, int chance, int empty, params LootEntryDefinition[] entries)
        {
            return new LootGroupDefinition { LootGroupKey = key, RollMode = mode, RollCount = count, DropChanceBasisPoints = chance, EmptyWeight = empty, Entries = entries, Conditions = new string[0] };
        }

        private static LootEntryDefinition Guaranteed(int id)
        {
            return new LootEntryDefinition { ItemTemplateId = id, HighItemTemplateId = id, FixedQuality = 1, MinimumQuality = 1, MaximumQuality = 1, MinimumQuantity = 1, MaximumQuantity = 1, Weight = 1, DropChanceBasisPoints = 10000, UniquePerCorpse = false, Semantics = LootSemantics.GuaranteedProven, Evidence = LootEvidenceConfidence.ProvenCapture };
        }

        private static LootEntryDefinition Ranged(int id, int minQl, int maxQl, int minQty, int maxQty, int chance)
        {
            return new LootEntryDefinition { ItemTemplateId = id, HighItemTemplateId = id, MinimumQuality = minQl, MaximumQuality = maxQl, MinimumQuantity = minQty, MaximumQuantity = maxQty, Weight = 1, DropChanceBasisPoints = chance, Semantics = LootSemantics.WeightedDocumented, Evidence = LootEvidenceConfidence.ProvenRepository };
        }

        private static LootEntryDefinition Weighted(int id, int weight) { LootEntryDefinition value = Guaranteed(id); value.Weight = weight; value.Semantics = LootSemantics.WeightedDocumented; return value; }
        private static LootEntryDefinition Unique(int id, int weight) { LootEntryDefinition value = Weighted(id, weight); value.UniquePerCorpse = true; return value; }
        private static LootAssignmentDefinition Assignment(string key, string table, LootAssignmentTargetType type, string target, int priority) { return new LootAssignmentDefinition { AssignmentKey = key, LootTableKey = table, TargetType = type, TargetKey = target, Priority = priority, Enabled = true, Evidence = "test", Confidence = LootEvidenceConfidence.ProvenRepository, Conditions = new string[0] }; }

        private static LootGenerationResult GenerateCredits(CreditsPolicyMode mode, int min, int max)
        {
            LootTableRegistry registry = Registry();
            registry.RegisterTable(Table("credits", LootRollMode.Guaranteed, Guaranteed(1001), mode, min, max));
            registry.RegisterAssignment(Assignment("credits", "credits", LootAssignmentTargetType.Global, null, 0));
            return Service(registry).Generate(Context(), new SeededLootRandomSource(7));
        }

        private static void AssertCredits(CreditsPolicyMode mode, int min, int max, int expected, bool unresolved)
        {
            LootGenerationResult result = GenerateCredits(mode, min, max);
            Assert.AreEqual(expected, result.Credits);
            Assert.AreEqual(unresolved, result.CreditsUnresolved);
        }

        private static string FindRepositoryRoot()
        {
            DirectoryInfo current = new DirectoryInfo(AppDomain.CurrentDomain.BaseDirectory);
            while (current != null)
            {
                if (Directory.Exists(Path.Combine(current.FullName, "AORebirth")) && Directory.Exists(Path.Combine(current.FullName, "docs"))) return current.FullName;
                current = current.Parent;
            }
            throw new DirectoryNotFoundException("Repository root not found.");
        }

        private static void AssertThrows<TException>(Action action) where TException : Exception
        {
            try
            {
                action();
            }
            catch (TException)
            {
                return;
            }
            Assert.Fail("Expected exception " + typeof(TException).Name + ".");
        }
    }
}
