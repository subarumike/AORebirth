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
        public void CheckedInBootstrapLoadsAreteAndSubwayDialogueAsOneValidatedSet()
        {
            AreteFrameworkRegistries result =
                AreteFrameworkBootstrap.InitializeCheckedInContent(CheckedInRuntimeBaseDirectory());

            Assert.IsTrue(result.IsValid);
            Assert.IsTrue(result.DialogueRegistry.PackCount >= 4);
            Assert.IsTrue(result.DialogueRegistry.NpcCount >= 6);
            Assert.IsTrue(result.QuestRegistry.PackCount >= 2);
            Assert.IsTrue(result.QuestRegistry.QuestCount >= 4);
            Assert.AreSame(result, AreteFrameworkBootstrap.Current);

            AssertNpc(result, "SimpleChar:782DE568");
            AssertNpc(result, "SimpleChar:782DE567");
            AssertNpc(result, "SimpleChar:796360BB");
            AssertNpc(result, "SimpleChar:796360BD");
            AssertNpc(result, "SimpleChar:796360BC");
            AssertNpc(result, "SimpleChar:79135F51");
            AssertNpc(result, "SimpleChar:782DE699");
            AssertNpc(result, "SimpleChar:78E0FC77");
            AssertNpc(result, "SimpleChar:78E0FC7D");
            AssertQuest(result, "Mission:5514B18C");
            AssertQuest(result, "Mission:5514B18D");
            AssertQuest(result, "Mission:5514B18E");
            AssertQuest(result, "Mission:55579381");
        }

        [TestMethod]
        public void TailorDialoguePreservesCapturedPromptsOptionsAndMeasurementBranches()
        {
            AreteFrameworkRegistries result =
                AreteFrameworkBootstrap.InitializeCheckedInContent(CheckedInRuntimeBaseDirectory());

            DialogueNpcEntry tailor;
            Assert.IsTrue(result.DialogueRegistry.TryGetNpc("SimpleChar:79135F51", out tailor));

            DialogueNode root = tailor.Nodes.Single(node => node.Id == "tailor_root");
            Assert.AreEqual("Howdy.", root.PromptText);
            CollectionAssert.AreEqual(
                new[]
                {
                    "How is life?",
                    "Um, I'd just like to look at your wares.",
                    "Goodbye"
                },
                root.Options.OrderBy(option => option.Index).Select(option => option.Text).ToArray());

            DialogueNode about = tailor.Nodes.Single(node => node.Id == "tailor_about");
            Assert.AreEqual(2, about.PromptSegments.Count);
            Assert.AreEqual("Not much to tell really... ", about.PromptSegments[0].Text);
            Assert.AreEqual("\\nLife has actually become more interesting recently.", about.PromptSegments[1].Text);

            DialogueNode parts = tailor.Nodes.Single(node => node.Id == "tailor_parts");
            Assert.AreEqual(9, parts.Options.Count);
            CollectionAssert.AreEqual(
                new[]
                {
                    "The Jobe Suit Pants.",
                    "The Jobe Suit Sleeves.",
                    "The Jobe Suit Boots.",
                    "The Jobe Suit Gloves.",
                    "The Jobe Suit Vest.",
                    "The Jobe Suit Helmet.",
                    "The Jobe Suit Support System.",
                    "The Jobe Suit Shoulderpad."
                },
                parts.Options.OrderBy(option => option.Index).Take(8).Select(option => option.Text).ToArray());
            Assert.IsTrue(parts.Options.Take(8).All(option => option.NextNodeId == "tailor_measurement_done"));

            DialogueNode completed = tailor.Nodes.Single(node => node.Id == "tailor_measurement_done");
            Assert.AreEqual("There you go.  Now, is there something else I can help you with? ", completed.PromptText);
            Assert.AreEqual("tailor_parts", completed.Options.Single(option => option.Index == 0).NextNodeId);
            Assert.AreEqual("tailor_wares", completed.Options.Single(option => option.Index == 1).NextNodeId);
            Assert.AreEqual("tailor_goodbye", completed.Options.Single(option => option.Index == 2).NextNodeId);

            DialogueNode wares = tailor.Nodes.Single(node => node.Id == "tailor_wares");
            Assert.AreEqual(2, wares.PromptSegments.Count);
            Assert.AreEqual("Of course!", wares.PromptSegments[0].Text);
            Assert.AreEqual(0, wares.PromptSegments[0].Unknown2);
            Assert.AreEqual(
                "To do that you just left-clik the Shopping Basket icon at the bottom of this window.",
                wares.PromptSegments[1].Text);
            Assert.AreEqual(1, wares.PromptSegments[1].Unknown2);

            DialogueNode reopen = tailor.Nodes.Single(node => node.Id == "tailor_root_reopen");
            Assert.AreEqual("Yes?", reopen.PromptText);
        }

        [TestMethod]
        public void RuntimeDefinitionCatalogBuildsCheckedInAreteAndKarrecContracts()
        {
            AreteFrameworkRegistries registries =
                AreteFrameworkBootstrap.InitializeCheckedInContent(CheckedInRuntimeBaseDirectory());
            var definitions = MissionDefinitionCatalog.Build(registries.QuestRegistry);

            Assert.IsTrue(definitions.Count >= 6);
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

            QuestDefinition karrecContent;
            Assert.IsTrue(
                registries.QuestRegistry.TryGetQuest(
                    MissionDefinitionCatalog.WindcallerKarrecQuestId,
                    out karrecContent));
            QuestAction lifecycleEvidence = karrecContent.Steps
                .Single(step => step.StepId == "deliver_offerings")
                .Actions.Single(action => action.Type == "CapturedLifecycleEvidence");
            Assert.AreEqual("285612", lifecycleEvidence.Parameters["dailyRewardItemId"]);
            Assert.AreEqual(
                "two-tokens-per-mission-token-level-tier-clan-stat-62-omni-stat-75-neutral-zero",
                lifecycleEvidence.Parameters["sideTokenModel"]);
            Assert.AreEqual(
                "one-full-Rubika-level-direct-XP-from-XPTable-no-research-diversion",
                lifecycleEvidence.Parameters["xpRewardModel"]);
            Assert.AreEqual(
                "excluded-expansion-system-not-implemented",
                lifecycleEvidence.Parameters["researchRuntime"]);
            Assert.IsFalse(lifecycleEvidence.Parameters.ContainsKey("sideTokenDelta"));
            Assert.IsFalse(lifecycleEvidence.Parameters.ContainsKey("rewardXpEvidence"));
            Assert.IsFalse(karrecContent.UnresolvedFields.Contains("exactTotalXpAndResearchPersistenceSemantics"));
            Assert.IsTrue(karrecContent.UnresolvedFields.Contains("officialDirectXpPacketSequence"));
            Assert.AreEqual(1, b18f.Objectives.Count);
            Assert.AreEqual(0, b18f.Objectives.Single().RequiredCount);
            Assert.AreEqual(0, b18f.PrerequisiteQuestIds.Count);
            Assert.AreEqual(1, b194.Objectives.Count);
            Assert.AreEqual(1, b194.Objectives.Single().RequiredCount);
            Assert.AreEqual(0, b194.PrerequisiteQuestIds.Count);
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

        [TestMethod]
        public void BrontoBurgersVendorPreservesCapturedStatelTemplateAndInventoryOrder()
        {
            string root = FindRepositoryRoot();
            string vendors = File.ReadAllText(Path.Combine(
                root,
                @"AORebirth\Libraries\Source\AORebirth.Database\SqlTables\vendors.sql"));
            string templates = File.ReadAllText(Path.Combine(
                root,
                @"AORebirth\Libraries\Source\AORebirth.Database\SqlTables\vendortemplate.sql"));
            string inventory = File.ReadAllText(Path.Combine(
                root,
                @"AORebirth\Libraries\Source\AORebirth.Database\SqlTables\shopinventorytemplates.sql"));

            Assert.IsTrue(vendors.Contains("Statel: 0xC00E1999"));
            Assert.IsTrue(vendors.Contains(
                "VALUES (429457422, 6553, 0, 0, 0, 0, 0, 0, 1, '', 121036, 'ARBRTBG');"));
            Assert.IsTrue(templates.Contains(
                "VALUES ('ARBRTBG', 1, 'AreteBrontoBurgers', 121036, 'BRBG', 1, 1);"));

            int[] capturedOrder =
                {
                    130621, 130593, 130623, 130624, 130581,
                    130612, 130625, 130606, 130602, 130603
                };
            int cursor = 0;
            foreach (int itemId in capturedOrder)
            {
                string rowPrefix = "VALUES ('BRBG', " + itemId + ", " + itemId + ", 1, 1, 1,";
                int position = inventory.IndexOf(rowPrefix, cursor, StringComparison.Ordinal);
                Assert.IsTrue(position >= cursor, "Missing or out-of-order captured item " + itemId + ".");
                cursor = position + rowPrefix.Length;
            }

            Assert.AreEqual(
                10,
                inventory.Split(new[] { "VALUES ('BRBG'," }, StringSplitOptions.None).Length - 1);
        }

        [TestMethod]
        public void CapturedAreteRespawnIntervalsRemainScopedToProvenNpcKinds()
        {
            string root = FindRepositoryRoot();
            string alex = File.ReadAllText(Path.Combine(
                root,
                @"AORebirth\Server\ZoneEngine\Core\Playfields\AlexAreaMobRuntime.cs"));
            string oasis = File.ReadAllText(Path.Combine(
                root,
                @"AORebirth\Server\ZoneEngine\Core\Playfields\LoreleiOasisMobRuntime.cs"));
            string alien = File.ReadAllText(Path.Combine(
                root,
                @"AORebirth\Server\ZoneEngine\Core\Playfields\AreteAlienAreaMobRuntime.cs"));

            Assert.IsTrue(alex.Contains("CapturedDockerRespawnSeconds = 40.0"));
            Assert.IsTrue(alex.Contains("slot.Kind == MobKind.Docker"));
            Assert.IsTrue(alex.Contains("string.Equals(slot.Name, \"32-V Docker\", StringComparison.Ordinal)"));
            Assert.IsTrue(alex.Contains("DefaultRespawnSeconds = 30.0"));

            Assert.IsTrue(oasis.Contains("CapturedDesertReetRespawnSeconds = 40.0"));
            Assert.IsTrue(oasis.Contains("CapturedRollerratRespawnSeconds = 40.0"));
            Assert.IsTrue(oasis.Contains("string.Equals(slot.Name, \"Desert Reet\", StringComparison.Ordinal)"));
            Assert.IsTrue(oasis.Contains("string.Equals(slot.Name, \"Rollerrat\", StringComparison.Ordinal)"));
            Assert.IsTrue(oasis.Contains("DefaultRespawnSeconds = 30.0"));

            Assert.IsTrue(alien.Contains("CapturedWildlifeRespawnSeconds = 40.0"));
            Assert.IsTrue(alien.Contains("slot.Kind == MobKind.Rollerrat"));
            Assert.IsTrue(alien.Contains("string.Equals(slot.Name, \"Angry Minibull\", StringComparison.Ordinal)"));
            Assert.IsTrue(alien.Contains("TryResolveRespawnSeconds"));
            Assert.IsFalse(alien.Contains("DefaultRespawnSeconds"));
        }

        [TestMethod]
        public void CapturedNamedEnemyLifecycleUsesMeasuredReplacementDelays()
        {
            string root = FindRepositoryRoot();
            string landing = File.ReadAllText(Path.Combine(
                root,
                @"AORebirth\Server\ZoneEngine\Core\Playfields\AreteLandingSpawn.cs"));

            Assert.IsTrue(landing.Contains("Name = \"Violent Protester\""));
            Assert.IsTrue(landing.Contains("MonsterData = 203740"));
            Assert.IsTrue(landing.Contains("X = 3505.53418f"));
            Assert.IsTrue(landing.Contains("RespawnSeconds = 19.958"));
            Assert.IsTrue(landing.Contains("RespawnSeconds = 26.923"));
            Assert.IsTrue(landing.Contains("CapturedRespawnDueUtcByPlayfield"));
            Assert.IsTrue(landing.Contains("TimeSpan.FromSeconds(def.RespawnSeconds)"));
        }

        [TestMethod]
        public void EligibilityOnlyAggroUsesContactFloorWithoutClaimingExactRadius()
        {
            string source = File.ReadAllText(Path.Combine(
                FindRepositoryRoot(),
                @"AORebirth\Server\ZoneEngine\Core\Playfields\NPCRuntimeService.cs"));

            Assert.IsTrue(source.Contains(
                "CapturedEligibilityOnlyAggroRadiusMeters = 1.0d"));
            Assert.IsTrue(source.Contains(
                "capturedAreteAggro.TryGetRadius(evidence, out radius)"));
            Assert.IsTrue(source.Contains(
                "capturedAreteAggro.TryGetEligibility("));
            Assert.IsTrue(source.Contains(
                "radius = CapturedEligibilityOnlyAggroRadiusMeters;"));
        }

        private static string CheckedInPath(string area, string contentName, string fileName)
        {
            return Path.Combine(
                CheckedInRuntimeBaseDirectory(),
                "Content",
                area,
                contentName,
                fileName);
        }

        private static string CheckedInRuntimeBaseDirectory()
        {
            return Path.Combine(
                FindRepositoryRoot(),
                "AORebirth",
                "Server",
                "ZoneEngine");
        }

        private static string FindRepositoryRoot()
        {
            DirectoryInfo current = new DirectoryInfo(AppDomain.CurrentDomain.BaseDirectory);
            while (current != null)
            {
                if (File.Exists(Path.Combine(current.FullName, "AI_START_HERE.md")))
                {
                    return current.FullName;
                }

                current = current.Parent;
            }

            throw new DirectoryNotFoundException("Repository root not found.");
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
