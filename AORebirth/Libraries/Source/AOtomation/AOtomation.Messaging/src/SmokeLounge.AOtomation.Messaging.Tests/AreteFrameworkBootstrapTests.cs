namespace SmokeLounge.AOtomation.Messaging.Tests
{
    #region Usings ...

    using System;
    using System.IO;
    using System.Linq;

    using Microsoft.VisualStudio.TestTools.UnitTesting;

    using ZoneEngine.Core.Arete;
    using ZoneEngine.Core.Arete.Dialogue;
    using ZoneEngine.Core.Arete.Quests;
    using ZoneEngine.Core.Missions;

    #endregion

    [TestClass]
    public class AreteFrameworkBootstrapTests
    {
        [TestMethod]
        public void CheckedInBootstrapLoadsRexMarcusAndWindcallerAsOneValidatedSet()
        {
            AreteFrameworkRegistries result =
                AreteFrameworkBootstrap.InitializeCheckedInContent(AppDomain.CurrentDomain.BaseDirectory);

            Assert.IsTrue(result.IsValid);
            Assert.AreEqual(3, result.DialogueRegistry.PackCount);
            Assert.AreEqual(5, result.DialogueRegistry.NpcCount);
            Assert.AreEqual(2, result.QuestRegistry.PackCount);
            Assert.AreEqual(4, result.QuestRegistry.QuestCount);
            Assert.AreSame(result, AreteFrameworkBootstrap.Current);

            AssertNpc(result, "SimpleChar:782DE568");
            AssertNpc(result, "SimpleChar:782DE567");
            AssertNpc(result, "SimpleChar:796360BB");
            AssertNpc(result, "SimpleChar:796360BD");
            AssertNpc(result, "SimpleChar:796360BC");
            AssertQuest(result, "Mission:5514B18C");
            AssertQuest(result, "Mission:5514B18D");
            AssertQuest(result, "Mission:5514B18E");
            AssertQuest(result, "Mission:55579381");
        }

        [TestMethod]
        public void RuntimeDefinitionCatalogBuildsCheckedInAreteAndKarrecContracts()
        {
            AreteFrameworkRegistries registries =
                AreteFrameworkBootstrap.InitializeCheckedInContent(AppDomain.CurrentDomain.BaseDirectory);
            var definitions = MissionDefinitionCatalog.Build(registries.QuestRegistry);

            Assert.AreEqual(6, definitions.Count);
            MissionDefinition b18d = definitions.Single(value => value.QuestId == MissionDefinitionCatalog.RexB18DQuestId);
            MissionDefinition b18e = definitions.Single(value => value.QuestId == MissionDefinitionCatalog.RexB18EQuestId);
            MissionDefinition karrec = definitions.Single(
                value => value.QuestId == MissionDefinitionCatalog.WindcallerKarrecQuestId);
            MissionDefinition b18f = definitions.Single(value => value.QuestId == MissionDefinitionCatalog.RexB18FQuestId);
            MissionDefinition b194 = definitions.Single(value => value.QuestId == MissionDefinitionCatalog.RexB194QuestId);

            Assert.AreEqual(1, b18d.Objectives.Single().RequiredCount);
            Assert.AreEqual(MissionDefinitionCatalog.RexB18CQuestId, b18d.PrerequisiteQuestIds.Single());
            Assert.AreEqual(1, b18e.Objectives.Single().RequiredCount);
            Assert.AreEqual(MissionDefinitionCatalog.RexB18DQuestId, b18e.PrerequisiteQuestIds.Single());
            Assert.AreEqual(2, karrec.Objectives.Single().RequiredCount);
            Assert.AreEqual(0, b18f.Objectives.Count);
            Assert.AreEqual(MissionDefinitionCatalog.RexB18EQuestId, b18f.PrerequisiteQuestIds.Single());
            Assert.AreEqual(0, b194.Objectives.Count);
            Assert.AreEqual(MissionDefinitionCatalog.RexB18FQuestId, b194.PrerequisiteQuestIds.Single());
        }

        [TestMethod]
        public void DuplicatePacksAcrossManifestsFailClosed()
        {
            string rexManifest = CheckedInPath("Arete", "rex-larsson", "manifest.json");

            AreteFrameworkRegistries result =
                AreteFrameworkBootstrap.LoadManifestSet(new[] { rexManifest, rexManifest });

            Assert.IsFalse(result.IsValid);
            Assert.AreEqual(0, result.DialogueRegistry.PackCount);
            Assert.AreEqual(0, result.QuestRegistry.PackCount);
            Assert.IsTrue(
                result.Validation.Errors.Any(
                    error => error.IndexOf(
                                 "duplicate dialogue content pack id",
                                 StringComparison.OrdinalIgnoreCase) >= 0));
            Assert.IsTrue(
                result.Validation.Errors.Any(
                    error => error.IndexOf("duplicate quest content pack id", StringComparison.OrdinalIgnoreCase) >= 0));
        }

        [TestMethod]
        public void MissingCheckedInContentThrowsWithResolvedValidationDetails()
        {
            string missingBaseDirectory = Path.Combine(
                Path.GetTempPath(),
                "aorebirth-missing-checked-in-content-" + Guid.NewGuid().ToString("N"));

            try
            {
                AreteFrameworkBootstrap.InitializeCheckedInContent(missingBaseDirectory);
                Assert.Fail("Expected InvalidDataException.");
            }
            catch (InvalidDataException exception)
            {
                Assert.IsTrue(exception.Message.Contains("Checked-in dialogue and quest content failed validation"));
                Assert.IsTrue(exception.Message.Contains("rex-larsson"));
                Assert.IsTrue(exception.Message.Contains("windcaller-karrec"));
                Assert.IsTrue(exception.Message.Contains("content manifest file was not found"));
            }
        }

        private static string CheckedInPath(string area, string contentName, string fileName)
        {
            return Path.Combine(
                AppDomain.CurrentDomain.BaseDirectory,
                "Content",
                area,
                contentName,
                fileName);
        }

        private static void AssertNpc(AreteFrameworkRegistries result, string npcIdentity)
        {
            DialogueNpcEntry npc;
            Assert.IsTrue(result.DialogueRegistry.TryGetNpc(npcIdentity, out npc), npcIdentity);
            Assert.IsNotNull(npc);
        }

        private static void AssertQuest(AreteFrameworkRegistries result, string questIdentity)
        {
            QuestDefinition quest;
            Assert.IsTrue(result.QuestRegistry.TryGetQuest(questIdentity, out quest), questIdentity);
            Assert.IsNotNull(quest);
        }
    }
}
