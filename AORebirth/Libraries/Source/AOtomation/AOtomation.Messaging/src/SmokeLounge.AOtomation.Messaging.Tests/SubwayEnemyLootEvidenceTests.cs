namespace SmokeLounge.AOtomation.Messaging.Tests
{
    using System;
    using System.Linq;

    using AORebirth.Core.Playfields;

    using Microsoft.VisualStudio.TestTools.UnitTesting;

    using ZoneEngine.Core.Playfields;

    [TestClass]
    public class SubwayEnemyLootEvidenceTests
    {
        [TestMethod]
        public void DisobedientBotUsesOnlyStrictlyProvenTransferredItems()
        {
            OrdinaryEnemyLootProfile loot = Profile("Disobedient Bot").Loot;

            Assert.AreEqual(OrdinaryEnemyLootPoolMode.WeightedOne, loot.PoolMode);
            Assert.AreEqual(5, loot.EmptyWeight);
            Assert.IsFalse(loot.ItemPoolComplete);
            Assert.AreEqual(7, loot.ObservedCompleteInventories);
            Assert.AreEqual(5, loot.ObservedEmptyInventories);
            Assert.AreEqual(2, loot.Entries.Length);

            AssertBotEntry(
                loot.Entries.Single(value => value.LowId == 234877),
                234877,
                1,
                "20260709-210452");
            AssertBotEntry(
                loot.Entries.Single(value => value.LowId == 104683),
                104684,
                10,
                "20260713-033511");
            Assert.IsFalse(loot.Entries.Any(value => value.LowId == 234876));
            Assert.AreEqual(15215, Profile("Disobedient Bot").Corpse.CapturedCatMesh.Value);
            CollectionAssert.AreEqual(
                new[] { "5:6:2", "6:8:2", "8:10:4", "9:11:3", "10:12:2" },
                loot.LevelCreditRules
                    .Select(
                        value => string.Format(
                            "{0}:{1}:{2}",
                            value.EnemyLevel,
                            value.MinimumCredits,
                            value.ObservedCorpses))
                    .ToArray());
        }

        [TestMethod]
        public void RejectedOtherEnemyAndContainerNoiseNeverEntersProfiles()
        {
            int[] bloodcreeperNoise = { 27199, 121743, 301712, 101675 };
            int[] disobedientBotNoise = { 103049, 101507, 234876 };

            OrdinaryEnemyLootProfile bloodcreeper = Profile("Bloodcreeper").Loot;
            OrdinaryEnemyLootProfile bot = Profile("Disobedient Bot").Loot;

            Assert.IsFalse(bloodcreeper.Entries.Any(value => bloodcreeperNoise.Contains(value.LowId)));
            Assert.IsFalse(bot.Entries.Any(value => disobedientBotNoise.Contains(value.LowId)));
        }

        [TestMethod]
        public void BloodcreeperKeepsItemsUnresolvedAndUsesCapturedCredits()
        {
            OrdinaryEnemyLootProfile loot = Profile("Bloodcreeper").Loot;

            Assert.AreEqual(OrdinaryEnemyLootPoolMode.IndependentEntries, loot.PoolMode);
            Assert.AreEqual(0, loot.EmptyWeight);
            Assert.IsFalse(loot.ItemPoolComplete);
            Assert.AreEqual(2, loot.ObservedCompleteInventories);
            Assert.AreEqual(2, loot.ObservedEmptyInventories);
            Assert.AreEqual(0, loot.Entries.Length);
            Assert.AreEqual(OrdinaryEnemyEvidenceState.Policy, loot.CreditEvidence);
            Assert.AreEqual(150, loot.MinimumCredits);
            Assert.AreEqual(150, loot.MaximumCredits);

            OrdinaryEnemyLootTableAdapterResult adapted = OrdinaryEnemyLootTableAdapter.Build(
                Profile("Bloodcreeper"),
                "subway.test.bloodcreeper",
                "subway.test.bloodcreeper.assignment");
            Assert.IsTrue(adapted.Table.ItemPoolUnresolved);
            Assert.AreEqual(0, adapted.Table.RollGroups.Length);
            Assert.AreEqual(CreditsPolicyMode.Fixed, adapted.Table.CreditsPolicy.Mode);
            Assert.AreEqual(150, adapted.Table.CreditsPolicy.MinimumCredits);
            Assert.AreEqual(150, adapted.Table.CreditsPolicy.MaximumCredits);
        }

        [TestMethod]
        public void ThiefAndFilthFleaPreserveCapturedLootCorpseAndLevelCreditEvidence()
        {
            OrdinaryEnemyLootProfile thief = Profile("Thief").Loot;
            Assert.AreEqual(OrdinaryEnemyLootPoolMode.IndependentEntries, thief.PoolMode);
            Assert.IsTrue(thief.ItemPoolComplete);
            Assert.AreEqual(1, thief.Entries.Length);
            Assert.AreEqual(297055, thief.Entries[0].LowId);
            Assert.AreEqual(297055, thief.Entries[0].HighId);
            Assert.AreEqual(1, thief.Entries[0].QualityLevel);
            Assert.AreEqual(1, thief.Entries[0].Quantity);
            Assert.AreEqual(10000, thief.Entries[0].DropChanceBasisPoints);
            Assert.AreEqual(OrdinaryEnemyCorpsePacketProfile.CapturedThief, Profile("Thief").Corpse.PacketProfile);
            Assert.AreEqual(5907, Profile("Thief").Corpse.CapturedCatMesh.Value);
            OrdinaryEnemyLevelCreditRule thiefCredits = thief.LevelCreditRules.Single();
            Assert.AreEqual(5, thiefCredits.EnemyLevel);
            Assert.AreEqual(29, thiefCredits.MinimumCredits);
            Assert.AreEqual(29, thiefCredits.MaximumCredits);
            Assert.AreEqual(3, thiefCredits.ObservedCorpses);

            OrdinaryEnemyLootProfile flea = Profile("Filth Flea").Loot;
            Assert.AreEqual(OrdinaryEnemyLootPoolMode.IndependentEntries, flea.PoolMode);
            Assert.IsTrue(flea.ItemPoolComplete);
            Assert.AreEqual(18, flea.ObservedCompleteInventories);
            Assert.AreEqual(5, flea.ObservedEmptyInventories);
            Assert.AreEqual(15, flea.Entries.Length);
            Assert.IsTrue(flea.Entries.All(value => value.ObservedCorpses == 18));
            Assert.IsTrue(flea.Entries.All(value => value.DropChanceBasisPoints == 556));
            CollectionAssert.AreEquivalent(
                new[]
                    {
                        234874, 103110, 101581, 110874, 101507, 202719, 234876, 101761,
                        110192, 112438, 101378, 136652, 111574, 111377, 102001
                    },
                flea.Entries.Select(value => value.LowId).ToArray());
            Assert.AreEqual(
                OrdinaryEnemyCorpsePacketProfile.CapturedFilthFlea,
                Profile("Filth Flea").Corpse.PacketProfile);
            Assert.AreEqual(15231, Profile("Filth Flea").Corpse.CapturedCatMesh.Value);
            CollectionAssert.AreEqual(
                new[]
                    {
                        "4:23:23:9",
                        "5:29:29:11",
                        "6:35:35:4",
                        "7:41:41:2",
                        "8:47:47:1",
                        "11:66:66:6",
                        "12:72:72:2",
                        "13:79:79:5",
                        "15:92:92:1",
                        "16:98:98:1",
                        "19:118:118:2",
                        "20:124:124:1",
                        "21:131:131:2"
                    },
                flea.LevelCreditRules
                    .Select(
                        value => string.Format(
                            "{0}:{1}:{2}:{3}",
                            value.EnemyLevel,
                            value.MinimumCredits,
                            value.MaximumCredits,
                            value.ObservedCorpses))
                    .ToArray());
            Assert.AreEqual(OrdinaryEnemyEvidenceState.Policy, flea.CreditEvidence);
            Assert.AreEqual(23, flea.MinimumCredits);
            Assert.AreEqual(79, flea.MaximumCredits);
            OrdinaryEnemyLootTableAdapterResult observedFleaCredits =
                OrdinaryEnemyLootTableAdapter.Build(
                    Profile("Filth Flea"),
                    4,
                    "subway.test.flea.level4",
                    "subway.test.flea.level4.assignment");
            Assert.AreEqual(CreditsPolicyMode.Fixed, observedFleaCredits.Table.CreditsPolicy.Mode);
            Assert.AreEqual(23, observedFleaCredits.Table.CreditsPolicy.MinimumCredits);
            OrdinaryEnemyLootTableAdapterResult fallbackFleaCredits =
                OrdinaryEnemyLootTableAdapter.Build(
                    Profile("Filth Flea"),
                    10,
                    "subway.test.flea.level10",
                    "subway.test.flea.level10.assignment");
            Assert.AreEqual(CreditsPolicyMode.Range, fallbackFleaCredits.Table.CreditsPolicy.Mode);
            Assert.AreEqual(23, fallbackFleaCredits.Table.CreditsPolicy.MinimumCredits);
            Assert.AreEqual(79, fallbackFleaCredits.Table.CreditsPolicy.MaximumCredits);
            StringAssert.Contains(
                flea.Entries.Single(value => value.LowId == 112438).EvidenceReference,
                "SimpleChar:794AD9A9>Corpse:F6E007>InventoryUpdate#8496");
            StringAssert.Contains(
                flea.Entries.Single(value => value.LowId == 111377).EvidenceReference,
                "SimpleChar:795F91B9>Corpse:F6C003>InventoryUpdate#742");
        }

        [TestMethod]
        public void Capture20260712153918PromotesOnlyExactDeathLinkedPositiveCreditCorpses()
        {
            var provider = new CapturedSubwayOrdinaryContentProvider();
            string[] evidence = new[] { 17649, 17657, 17720, 26092, 203733, 203734 }
                .SelectMany(value => provider.GetCorpseEvidence(value))
                .Where(value => value.Capture == "20260712-153918")
                .Select(
                    value => string.Format(
                        "{0}:{1}:{2}:{3}:{4}",
                        value.MonsterData,
                        value.EnemyLevel,
                        value.Credits,
                        value.DeadNpcIdentity,
                        value.CorpseIdentity))
                .OrderBy(value => value, StringComparer.Ordinal)
                .ToArray();

            CollectionAssert.AreEqual(
                new[]
                    {
                        "17649:6:8:(SimpleChar:795EC78D):(Corpse:00F6C00D)",
                        "17657:4:23:(SimpleChar:795EC774):(Corpse:00F6C002)",
                        "17657:4:23:(SimpleChar:795F9195):(Corpse:00F6C00D)",
                        "17657:5:29:(SimpleChar:795F9194):(Corpse:00F6C00B)",
                        "17657:6:35:(SimpleChar:795EC775):(Corpse:00F6C005)",
                        "17720:6:21:(SimpleChar:795EC7AE):(Corpse:00F6C01A)",
                        "17720:7:25:(SimpleChar:795EC786):(Corpse:00F6C001)",
                        "203733:7:25:(SimpleChar:795EC0CD):(Corpse:00F6C01F)",
                        "203734:5:44:(SimpleChar:795EC781):(Corpse:00F6C002)",
                        "203734:5:44:(SimpleChar:795F91A4):(Corpse:00F6C007)",
                        "26092:5:29:(SimpleChar:795F910E):(Corpse:00F6C007)"
                    }
                    .OrderBy(value => value, StringComparer.Ordinal)
                    .ToArray(),
                evidence);
        }

        [TestMethod]
        public void AuditedCorpsePromotionKeepsExactCaptureCountsAndIdentityLinkage()
        {
            var provider = new CapturedSubwayOrdinaryContentProvider();
            int[] monsterData =
                {
                    17649, 17657, 17720, 26092, 30379, 30464, 31909, 55648,
                    96056, 96193, 96195, 203727, 203728, 203729, 203730, 203731,
                    203733, 203734, 203736, 203739, 203743, 203745, 203746, 203747,
                    203854, 204178
                };
            CapturedSubwayCorpseEvidenceDefinition[] evidence = monsterData
                .SelectMany(value => provider.GetCorpseEvidence(value))
                .ToArray();

            Assert.AreEqual(282, evidence.Length);
            Assert.AreEqual(26, evidence.Select(value => value.MonsterData).Distinct().Count());
            CollectionAssert.AreEqual(
                new[]
                    {
                        "20260709-220439:41",
                        "20260709-222339:15",
                        "20260709-225408:61",
                        "20260712-223719:13",
                        "20260716-222007:3"
                    },
                evidence
                    .Where(
                        value => value.Capture == "20260709-220439"
                                 || value.Capture == "20260709-222339"
                                 || value.Capture == "20260709-225408"
                                 || value.Capture == "20260712-223719"
                                 || value.Capture == "20260716-222007")
                    .GroupBy(value => value.Capture)
                    .OrderBy(value => value.Key, StringComparer.Ordinal)
                    .Select(value => value.Key + ":" + value.Count())
                    .ToArray());
            Assert.IsTrue(evidence.All(value => value.Credits > 0));
            Assert.AreEqual(
                evidence.Length,
                evidence.Select(value => value.Capture + ":" + value.DeadNpcIdentity)
                    .Distinct(StringComparer.Ordinal)
                    .Count());
            CapturedSubwayCorpseEvidenceDefinition bloodcreeper = evidence.Single(
                value => value.Capture == "20260712-223719"
                         && value.DeadNpcIdentity == "(SimpleChar:7960785D)");
            Assert.AreEqual(24, bloodcreeper.EnemyLevel);
            Assert.AreEqual(30379, bloodcreeper.MonsterData);
            Assert.AreEqual(26978, bloodcreeper.CatMesh);
            Assert.AreEqual(150, bloodcreeper.Credits);
        }

        [TestMethod]
        public void StrictDeepCorpseSnapshotsAloneSupplyRuntimeProbabilityDenominators()
        {
            Assert.AreEqual(
                0,
                Profile("Redundant Scan").Loot.Entries.Length,
                "An exact empty snapshot plus a legacy item-bearing observation does not prove item odds.");

            OrdinaryEnemyLootEntry molested = Profile("Molested Molecules").Loot.Entries
                .Single(value => value.LowId == 301713);
            Assert.AreEqual(1, molested.ObservedCount);
            Assert.AreEqual(3, molested.ObservedCorpses);
            Assert.AreEqual(3333, molested.DropChanceBasisPoints);
            Assert.IsTrue(molested.EvidenceReference.Contains("20260716-221358"));

            OrdinaryEnemyLootEntry slumRunner = Profile("Slum Runner").Loot.Entries
                .Single(value => value.LowId == 234876);
            Assert.AreEqual(2, slumRunner.ObservedCount);
            Assert.AreEqual(12, slumRunner.ObservedCorpses);
            Assert.AreEqual(1667, slumRunner.DropChanceBasisPoints);
        }

        [TestMethod]
        public void LegacyItemSnapshotsStayIdentityLinkedAndCannotBecomeGuessedRuntimeDrops()
        {
            var provider = new CapturedSubwayOrdinaryContentProvider();
            CapturedSubwayLootOutcomeEvidenceDefinition[] vagabond =
                provider.GetLootOutcomeEvidence(203733);
            CapturedSubwayLootOutcomeEvidenceDefinition[] mugger =
                provider.GetLootOutcomeEvidence(203734);
            CapturedSubwayOrdinaryArchetypeDefinition stim = provider.GetArchetypes()
                .Single(value => value.Name == "Stim Fiend");

            CapturedSubwayLootOutcomeEvidenceDefinition vagabondOutcome = vagabond.Single(
                value => value.Capture == "20260709-210452"
                         && value.DeadNpcIdentity == "(SimpleChar:79528F80)"
                         && value.LowId == 130592);
            Assert.AreEqual("(Corpse:00F6E017)", vagabondOutcome.CorpseIdentity);
            Assert.AreEqual(203733, vagabondOutcome.MonsterData);

            CapturedSubwayLootOutcomeEvidenceDefinition muggerOutcome = mugger.Single(
                value => value.Capture == "20260709-212336"
                         && value.DeadNpcIdentity == "(SimpleChar:7953AA11)"
                         && value.LowId == 123704);
            Assert.AreEqual("(Corpse:00F6E00E)", muggerOutcome.CorpseIdentity);
            Assert.AreEqual(203734, muggerOutcome.MonsterData);

            Assert.IsFalse(stim.LootOutcomeEvidence.Any(value => value.LowId == 130592));
            Assert.IsFalse(stim.LootOutcomeEvidence.Any(value => value.LowId == 123704));
            Assert.IsTrue(stim.LootOutcomeEvidence.Any(value => value.LowId == 291082));
            Assert.IsTrue(stim.LootOutcomeEvidence.Any(value => value.LowId == 291043));

            OrdinaryEnemyProfile runtimeStim = Profile("Stim Fiend");
            Assert.AreEqual(0, runtimeStim.Loot.Entries.Length);
            Assert.IsFalse(runtimeStim.Loot.ItemPoolComplete);
            OrdinaryEnemyLootTableAdapterResult adapted = OrdinaryEnemyLootTableAdapter.Build(
                runtimeStim,
                "subway.test.stim-outcomes",
                "subway.test.stim-outcomes.assignment");
            Assert.AreEqual(0, adapted.Table.RollGroups.Length);
            Assert.IsTrue(adapted.Table.ItemPoolUnresolved);
        }

        [TestMethod]
        public void ShadowReopenUsesOnlyTheEarliestInventoryTimestampGroupForItsCorpseGeneration()
        {
            CapturedSubwayOrdinaryArchetypeDefinition shadow =
                new CapturedSubwayOrdinaryContentProvider().GetArchetypes()
                    .Single(value => value.Name == "Shadow");
            CapturedSubwayLootOutcomeEvidenceDefinition[] firstGenerationSnapshot =
                shadow.LootOutcomeEvidence
                    .Where(
                        value => value.Capture == "20260709-212336"
                                 && value.CorpseIdentity == "(Corpse:00F6E00F)"
                                 && value.DeadNpcIdentity == "(SimpleChar:79528828)")
                    .ToArray();

            Assert.AreEqual(2, firstGenerationSnapshot.Length);
            Assert.IsTrue(firstGenerationSnapshot.All(value => value.Sequence == 6874));
            Assert.AreEqual(1, firstGenerationSnapshot.Select(value => value.CapturedUtc).Distinct().Count());
            CollectionAssert.AreEquivalent(
                new[] { 234875, 124364 },
                firstGenerationSnapshot.Select(value => value.LowId).ToArray());
            Assert.AreEqual(
                1,
                firstGenerationSnapshot.Count(value => value.LowId == 234875),
                "The later reopen must not count the starter item a second time.");

            OrdinaryEnemyProfile runtimeShadow = Profile("Shadow");
            Assert.AreEqual(0, runtimeShadow.Loot.Entries.Length);
            Assert.IsFalse(runtimeShadow.Loot.ItemPoolComplete);
        }

        [TestMethod]
        public void SlumRunnerPreservesNineteenDeathLinkedCorpseVisualAndLevelCreditOutcomes()
        {
            CapturedSubwayOrdinaryArchetypeDefinition source =
                new CapturedSubwayOrdinaryContentProvider()
                    .GetArchetypes()
                    .Single(value => value.Name == "Slum Runner");
            Assert.AreEqual(19, source.CorpseEvidence.Length);
            Assert.AreEqual(4, source.CorpseEvidence.Count(value => value.Capture == "20260709-220439"));
            Assert.AreEqual(2, source.CorpseEvidence.Count(value => value.Capture == "20260709-222339"));
            Assert.AreEqual(6, source.CorpseEvidence.Count(value => value.Capture == "20260709-225408"));
            Assert.AreEqual(6, source.CorpseEvidence.Count(value => value.Capture == "20260716-034656"));
            Assert.AreEqual(1, source.CorpseEvidence.Count(value => value.Capture == "20260716-215947"));
            Assert.IsTrue(source.CorpseEvidence.All(value => value.MonsterData == 55648));
            Assert.IsTrue(source.CorpseEvidence.All(value => value.CatMesh == 31774));
            string[] identityLinks = source.CorpseEvidence
                .Select(value => value.DeadNpcIdentity + ">" + value.CorpseIdentity)
                .ToArray();
            Assert.IsTrue(
                new[]
                    {
                        "(SimpleChar:796D4080)>(Corpse:00F69005)",
                        "(SimpleChar:796D407E)>(Corpse:00F69007)",
                        "(SimpleChar:796D4078)>(Corpse:00F69008)",
                        "(SimpleChar:796D4083)>(Corpse:00F69009)",
                        "(SimpleChar:796D407A)>(Corpse:00F6900A)",
                        "(SimpleChar:796D407C)>(Corpse:00F6900B)",
                        "(SimpleChar:797024AE)>(Corpse:00F69002)"
                    }
                    .All(identityLinks.Contains));

            OrdinaryEnemyProfile profile = Profile("Slum Runner");
            Assert.IsTrue(profile.Corpse.CapturedCatMesh.HasValue);
            Assert.AreEqual(31774, profile.Corpse.CapturedCatMesh.Value);
            StringAssert.Contains(profile.Corpse.VisualEvidence, "20260716-034656");
            Assert.AreEqual(0, profile.Loot.ObservedCreditOutcomes.Length);
            CollectionAssert.AreEqual(
                new[]
                    {
                        "11:66:1",
                        "12:72:3",
                        "16:98:4",
                        "17:105:3",
                        "18:111:1",
                        "21:131:2",
                        "22:137:2",
                        "23:144:3"
                    },
                profile.Loot.LevelCreditRules
                    .Select(
                        value => string.Format(
                            "{0}:{1}:{2}",
                            value.EnemyLevel,
                            value.MinimumCredits,
                            value.ObservedCorpses))
                    .ToArray());
            Assert.AreEqual(string.Empty, profile.Loot.CreditEvidenceReference);

            OrdinaryEnemyLootTableAdapterResult adapted = OrdinaryEnemyLootTableAdapter.Build(
                profile,
                22,
                "subway.test.slum-runner",
                "subway.test.slum-runner.assignment");
            Assert.AreEqual(CreditsPolicyMode.Fixed, adapted.Table.CreditsPolicy.Mode);
            Assert.AreEqual(137, adapted.Table.CreditsPolicy.MinimumCredits);
            Assert.AreEqual(137, adapted.Table.CreditsPolicy.MaximumCredits);

            var registry = new LootTableRegistry(value => value > 0);
            registry.RegisterTableAndAssignment(adapted.Table, adapted.Assignment);
            var service = new LootGenerationService(registry, new LootAssignmentResolver());
            var context = new LootGenerationContext
            {
                EnemyProfileKey = profile.ProfileKey,
                FamilyKey = profile.FamilyKey,
                MonsterData = profile.MonsterData,
                Level = 22,
                PlayfieldId = OrdinaryEnemyCatalog.SubwayPlayfieldInstance
            };
            Assert.AreEqual(137, service.Generate(context, new FixedLootRandomSource(0)).Credits);
        }

        [TestMethod]
        public void OfficialDeathLinkedCorpseEvidenceReachesEveryAuditedOrdinaryProfile()
        {
            AssertCorpseAndLevelCredits(
                "Architect Striker",
                17870,
                "13:79:79:2",
                "14:85:85:1",
                "15:92:92:1");
            AssertCorpseAndLevelCredits("Bloodcreeper", 26978, "24:150:150:3");
            AssertCorpseAndLevelCredits(
                "Deranged Shopper",
                5927,
                "8:47:47:1",
                "9:53:53:1");
            AssertCorpseAndLevelCredits(
                "Discarded Pet",
                15929,
                "5:18:18:1",
                "6:21:21:2",
                "7:25:25:8",
                "8:28:28:1",
                "9:32:32:4",
                "10:35:35:7");
            AssertCorpseAndLevelCredits(
                "Empty Shell",
                5941,
                "19:118:118:1",
                "21:131:131:1");
            AssertCorpseAndLevelCredits(
                "Fragmented Soul",
                5921,
                "17:105:105:1",
                "18:111:111:2",
                "21:131:131:2");
            AssertCorpseAndLevelCredits(
                "Incomplete Rebuild",
                5921,
                "17:105:105:1",
                "19:118:118:3",
                "21:131:131:2");
            AssertCorpseAndLevelCredits(
                "Infected Attendant",
                96024,
                "11:14:14:2",
                "12:15:15:2",
                "15:19:19:1",
                "23:29:29:1");
            AssertCorpseAndLevelCredits(
                "Infector",
                31868,
                "16:98:98:2",
                "17:105:105:2",
                "19:118:118:3",
                "25:156:156:2");
            AssertCorpseAndLevelCredits(
                "Looter",
                17870,
                "9:53:53:2",
                "10:59:59:9");
            AssertCorpseAndLevelCredits(
                "Lost Thought",
                96179,
                "16:20:20:1",
                "18:23:23:1",
                "21:26:26:1",
                "22:28:28:1");
            AssertCorpseAndLevelCredits(
                "Melded Patterns",
                23368,
                "18:111:111:2",
                "20:124:124:1",
                "21:131:131:3",
                "24:150:150:1",
                "25:156:156:3");
            AssertCorpseAndLevelCredits(
                "Molested Molecules",
                5921,
                "19:118:118:1",
                "20:124:124:2",
                "21:131:131:1",
                "22:137:137:1",
                "23:144:144:1",
                "24:150:150:1");
            AssertCorpseAndLevelCredits(
                "Mugger",
                17534,
                "5:44:44:6",
                "8:71:71:6",
                "9:80:80:6",
                "10:88:88:6");
            AssertCorpseAndLevelCredits(
                "Neural Burnout",
                5941,
                "17:105:105:1",
                "18:111:111:2",
                "23:144:144:1");
            AssertCorpseAndLevelCredits(
                "Premature Pattern",
                5941,
                "18:111:111:1",
                "23:144:144:2");
            AssertCorpseAndLevelCredits(
                "Redundant Scan",
                23370,
                "19:118:118:1",
                "20:124:124:1",
                "21:131:131:1");
            AssertCorpseAndLevelCredits(
                "Shadow",
                30434,
                "9:53:53:3",
                "10:59:59:5",
                "11:66:66:1",
                "13:79:79:1",
                "14:85:85:2",
                "15:92:92:2",
                "21:131:131:1",
                "22:137:137:2",
                "23:144:144:3");
            AssertCorpseAndLevelCredits(
                "Stim Fiend",
                5907,
                "10:59:59:6",
                "11:66:66:2",
                "12:72:72:4",
                "13:79:79:2",
                "14:85:85:1");
            AssertCorpseAndLevelCredits(
                "Uncontrollable Anger",
                96177,
                "11:14:14:1",
                "13:16:16:2",
                "20:25:25:1");
            AssertCorpseAndLevelCredits(
                "Violent Vagabond",
                17870,
                "6:21:21:9",
                "7:25:25:5",
                "10:35:35:3");
            AssertCorpseAndLevelCredits(
                "Workman Striker",
                17899,
                "13:79:79:2",
                "14:85:85:7",
                "15:92:92:3",
                "16:98:98:4",
                "17:105:105:3",
                "25:156:156:1");
        }

        [TestMethod]
        public void MuggerLevelTenUsesIdentityLinkedCorpseCreditsWithoutInventingItemOdds()
        {
            CapturedSubwayCorpseEvidenceDefinition[] source =
                new CapturedSubwayOrdinaryContentProvider().GetCorpseEvidence(203734);
            CapturedSubwayCorpseEvidenceDefinition levelTen = source.Single(
                value => value.Capture == "20260710-202132");
            Assert.AreEqual("(SimpleChar:7957E5CA)", levelTen.DeadNpcIdentity);
            Assert.AreEqual("(Corpse:00F6C001)", levelTen.CorpseIdentity);
            Assert.AreEqual(10, levelTen.EnemyLevel);
            Assert.AreEqual(17534, levelTen.CatMesh);
            Assert.AreEqual(88, levelTen.Credits);

            OrdinaryEnemyProfile profile = Profile("Mugger");
            Assert.AreEqual(0, profile.Loot.Entries.Length);
            Assert.IsFalse(profile.Loot.ItemPoolComplete);

            OrdinaryEnemyLootTableAdapterResult adapted = OrdinaryEnemyLootTableAdapter.Build(
                profile,
                10,
                "subway.test.mugger.level10",
                "subway.test.mugger.level10.assignment");
            Assert.AreEqual(CreditsPolicyMode.Fixed, adapted.Table.CreditsPolicy.Mode);
            Assert.AreEqual(88, adapted.Table.CreditsPolicy.MinimumCredits);
            Assert.AreEqual(88, adapted.Table.CreditsPolicy.MaximumCredits);
            Assert.AreEqual(0, adapted.Table.RollGroups.Length);
            Assert.IsTrue(adapted.Table.ItemPoolUnresolved);
        }

        [TestMethod]
        public void AdapterPreservesCapturedItemIdentityAndEvidence()
        {
            OrdinaryEnemyProfile profile = Profile("Disobedient Bot");
            OrdinaryEnemyLootTableAdapterResult adapted = OrdinaryEnemyLootTableAdapter.Build(
                profile,
                "subway.test.disobedient-bot",
                "subway.test.disobedient-bot.assignment");

            Assert.AreEqual("subway.test.disobedient-bot", adapted.Table.LootTableKey);
            Assert.AreEqual("subway.test.disobedient-bot.assignment", adapted.Assignment.AssignmentKey);
            Assert.AreEqual(profile.ProfileKey, adapted.Assignment.TargetKey);
            Assert.AreEqual(adapted.Table.LootTableKey, adapted.Assignment.LootTableKey);
            Assert.IsTrue(adapted.Table.ItemPoolUnresolved);
            Assert.AreEqual(1, adapted.Table.RollGroups.Length);

            LootGroupDefinition group = adapted.Table.RollGroups[0];
            Assert.AreEqual(LootRollMode.WeightedOne, group.RollMode);
            Assert.AreEqual(5, group.EmptyWeight);
            Assert.AreEqual(2, group.Entries.Length);

            AssertAdaptedEntry(
                group.Entries.Single(value => value.ItemTemplateId == 234877),
                234877,
                1,
                "20260709-210452");
            AssertAdaptedEntry(
                group.Entries.Single(value => value.ItemTemplateId == 104683),
                104684,
                10,
                "20260713-033511");
        }

        [TestMethod]
        public void DisobedientBotWeightedRollSelectsEmptyAndEachProvenItem()
        {
            OrdinaryEnemyProfile profile = Profile("Disobedient Bot");
            OrdinaryEnemyLootTableAdapterResult adapted = OrdinaryEnemyLootTableAdapter.Build(
                profile,
                "subway.test.disobedient-bot.roll",
                "subway.test.disobedient-bot.roll.assignment");
            var registry = new LootTableRegistry(value => value > 0);
            registry.RegisterTableAndAssignment(adapted.Table, adapted.Assignment);
            var service = new LootGenerationService(registry, new LootAssignmentResolver());
            var context = new LootGenerationContext
            {
                EnemyProfileKey = profile.ProfileKey,
                FamilyKey = profile.FamilyKey,
                MonsterData = profile.MonsterData,
                Level = 9,
                PlayfieldId = OrdinaryEnemyCatalog.SubwayPlayfieldInstance
            };

            var emptyRandom = new FixedLootRandomSource(0);
            var firstItemRandom = new FixedLootRandomSource(5);
            var secondItemRandom = new FixedLootRandomSource(6);
            LootGenerationResult empty = service.Generate(context, emptyRandom);
            LootGenerationResult firstItem = service.Generate(context, firstItemRandom);
            LootGenerationResult secondItem = service.Generate(context, secondItemRandom);

            Assert.AreEqual(7, emptyRandom.RequestedMaximum);
            Assert.AreEqual(0, empty.Items.Count);
            Assert.AreEqual("empty", empty.RollEvidence.Single().Outcome);
            Assert.AreEqual(104683, firstItem.Items.Single().ItemTemplateId);
            Assert.AreEqual(10, firstItem.Items.Single().Quality);
            Assert.AreEqual(1, firstItem.Items.Single().Quantity);
            Assert.AreEqual(234877, secondItem.Items.Single().ItemTemplateId);
            Assert.AreEqual(1, secondItem.Items.Single().Quality);
            Assert.AreEqual(1, secondItem.Items.Single().Quantity);
        }

        [TestMethod]
        public void RegistryRejectsMissingEvidenceAndInvalidFixedQuality()
        {
            AssertRegistryRejects(entry => entry.EvidenceReference = string.Empty);
            AssertRegistryRejects(entry => entry.Evidence = LootEvidenceConfidence.Unresolved);
            AssertRegistryRejects(entry => entry.FixedQuality = entry.MaximumQuality + 1);
        }

        [TestMethod]
        public void RegistryRejectsOverlappingActiveAssignmentOwnership()
        {
            OrdinaryEnemyProfile profile = Profile("Disobedient Bot");
            OrdinaryEnemyLootTableAdapterResult first = OrdinaryEnemyLootTableAdapter.Build(
                profile,
                "subway.test.disobedient-bot.owner.1",
                "subway.test.disobedient-bot.owner.assignment.1");
            OrdinaryEnemyLootTableAdapterResult second = OrdinaryEnemyLootTableAdapter.Build(
                profile,
                "subway.test.disobedient-bot.owner.2",
                "subway.test.disobedient-bot.owner.assignment.2");
            var registry = new LootTableRegistry(value => value > 0);
            registry.RegisterTableAndAssignment(first.Table, first.Assignment);

            ExpectLootDefinitionFailure(
                () => registry.RegisterTableAndAssignment(second.Table, second.Assignment));
        }

        [TestMethod]
        public void AmbiguousOrUnresolvedLinkageCannotBecomeActiveLoot()
        {
            AssertInvalid(
                Entry(
                    234877,
                    234877,
                    OrdinaryEnemyLootLinkageEvidence.Ambiguous,
                    "capture:ambiguous"));
            AssertInvalid(
                Entry(
                    234877,
                    234877,
                    OrdinaryEnemyLootLinkageEvidence.Unresolved,
                    "capture:unresolved"));
        }

        [TestMethod]
        public void InvalidItemIdentityOrMissingEvidenceCannotBecomeActiveLoot()
        {
            AssertInvalid(
                Entry(
                    0,
                    234877,
                    OrdinaryEnemyLootLinkageEvidence.ProvenTransferredEnemyCorpseItem,
                    "capture:invalid-id"));
            AssertInvalid(
                Entry(
                    234877,
                    234877,
                    OrdinaryEnemyLootLinkageEvidence.ProvenTransferredEnemyCorpseItem,
                    string.Empty));
        }

        private static OrdinaryEnemyProfile Profile(string displayName)
        {
            var catalog = new OrdinaryEnemyCatalog(
                new CapturedSubwayContentProvider(),
                new CapturedSubwayOrdinaryContentProvider());
            return catalog.GetProfiles().Single(value => value.DisplayName == displayName);
        }

        private static void AssertCorpseAndLevelCredits(
            string displayName,
            int expectedCatMesh,
            params string[] expectedRules)
        {
            OrdinaryEnemyProfile profile = Profile(displayName);
            Assert.IsTrue(profile.Corpse.CapturedCatMesh.HasValue, displayName);
            Assert.AreEqual(expectedCatMesh, profile.Corpse.CapturedCatMesh.Value, displayName);
            CollectionAssert.AreEqual(
                expectedRules,
                profile.Loot.LevelCreditRules
                    .Select(
                        value => string.Format(
                            "{0}:{1}:{2}:{3}",
                            value.EnemyLevel,
                            value.MinimumCredits,
                            value.MaximumCredits,
                            value.ObservedCorpses))
                    .ToArray(),
                displayName);
        }

        private static OrdinaryEnemyLootEntry Entry(
            int lowId,
            int highId,
            OrdinaryEnemyLootLinkageEvidence linkageEvidence,
            string evidenceReference)
        {
            return new OrdinaryEnemyLootEntry(
                lowId,
                highId,
                1,
                0,
                1,
                1,
                0,
                OrdinaryEnemyLootEvidence.ObservedAvailableLoot,
                linkageEvidence,
                OrdinaryEnemyLootProbabilityEvidence.ProvisionalProjectPolicy,
                1,
                1,
                evidenceReference);
        }

        private static void AssertInvalid(OrdinaryEnemyLootEntry entry)
        {
            var loot = new OrdinaryEnemyLootProfile(
                OrdinaryEnemyLootEvidence.ObservedAvailableLoot,
                new[] { entry },
                OrdinaryEnemyLootPoolMode.WeightedOne,
                0,
                false,
                1,
                0,
                "capture:profile",
                OrdinaryEnemyEvidenceState.Unresolved,
                null,
                null,
                new OrdinaryEnemyLevelCreditRule[0]);

            try
            {
                OrdinaryEnemyProfileValidator.ValidateLootProfile("test.invalid", loot);
                Assert.Fail("Invalid ordinary-enemy loot passed validation.");
            }
            catch (InvalidOperationException)
            {
            }
        }

        private static void AssertBotEntry(
            OrdinaryEnemyLootEntry entry,
            int expectedHighId,
            int expectedQualityLevel,
            string expectedCapture)
        {
            Assert.AreEqual(expectedHighId, entry.HighId);
            Assert.AreEqual(expectedQualityLevel, entry.QualityLevel);
            Assert.AreEqual(1, entry.Quantity);
            Assert.AreEqual(1, entry.Weight);
            Assert.AreEqual(0, entry.DropChanceBasisPoints);
            Assert.AreEqual(
                OrdinaryEnemyLootLinkageEvidence.ProvenTransferredEnemyCorpseItem,
                entry.LinkageEvidence);
            Assert.AreEqual(
                OrdinaryEnemyLootProbabilityEvidence.ProvisionalProjectPolicy,
                entry.ProbabilityEvidence);
            StringAssert.Contains(entry.EvidenceReference, expectedCapture);
            StringAssert.Contains(entry.EvidenceReference, "SimpleChar:");
            StringAssert.Contains(entry.EvidenceReference, ">Corpse:");
            StringAssert.Contains(entry.EvidenceReference, ">InventoryUpdate#");
            StringAssert.Contains(entry.EvidenceReference, ">ContainerAddItem#");
        }

        private static void AssertAdaptedEntry(
            LootEntryDefinition entry,
            int expectedHighId,
            int expectedQualityLevel,
            string expectedCapture)
        {
            Assert.AreEqual(expectedHighId, entry.HighItemTemplateId);
            Assert.AreEqual(expectedQualityLevel, entry.FixedQuality);
            Assert.AreEqual(expectedQualityLevel, entry.MinimumQuality);
            Assert.AreEqual(expectedQualityLevel, entry.MaximumQuality);
            Assert.AreEqual(1, entry.MinimumQuantity);
            Assert.AreEqual(1, entry.MaximumQuantity);
            Assert.AreEqual(1, entry.Weight);
            Assert.AreEqual(0, entry.DropChanceBasisPoints);
            Assert.AreEqual(LootEvidenceConfidence.ProvenCapture, entry.Evidence);
            Assert.AreEqual(
                OrdinaryEnemyLootLinkageEvidence.ProvenTransferredEnemyCorpseItem.ToString(),
                entry.LinkageEvidence);
            Assert.AreEqual(
                OrdinaryEnemyLootProbabilityEvidence.ProvisionalProjectPolicy.ToString(),
                entry.ProbabilityEvidence);
            StringAssert.Contains(entry.EvidenceReference, expectedCapture);
        }

        private static void AssertRegistryRejects(Action<LootEntryDefinition> mutate)
        {
            OrdinaryEnemyProfile profile = Profile("Disobedient Bot");
            OrdinaryEnemyLootTableAdapterResult adapted = OrdinaryEnemyLootTableAdapter.Build(
                profile,
                "subway.test.invalid-registry",
                "subway.test.invalid-registry.assignment");
            mutate(adapted.Table.RollGroups[0].Entries[0]);

            var registry = new LootTableRegistry(value => value > 0);
            ExpectLootDefinitionFailure(
                () => registry.RegisterTableAndAssignment(adapted.Table, adapted.Assignment));
        }

        private static void ExpectLootDefinitionFailure(Action action)
        {
            try
            {
                action();
                Assert.Fail("Invalid loot definition passed registry validation.");
            }
            catch (LootDefinitionValidationException)
            {
            }
        }

        private sealed class FixedLootRandomSource : ILootRandomSource
        {
            private readonly int value;

            internal FixedLootRandomSource(int value)
            {
                this.value = value;
            }

            internal int RequestedMaximum { get; private set; }

            public int Next(int maximumExclusive)
            {
                this.RequestedMaximum = maximumExclusive;
                if (this.value < 0 || this.value >= maximumExclusive)
                {
                    throw new InvalidOperationException("Fixed loot roll is outside the requested range.");
                }

                return this.value;
            }
        }
    }
}
