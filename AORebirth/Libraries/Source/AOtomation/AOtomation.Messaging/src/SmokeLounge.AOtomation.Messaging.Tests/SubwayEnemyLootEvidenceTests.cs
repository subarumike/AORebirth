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
        public void BloodcreeperUsesFourReviewedOpensAndKeepsItsPoolIncomplete()
        {
            OrdinaryEnemyLootProfile loot = Profile("Bloodcreeper").Loot;

            Assert.AreEqual(OrdinaryEnemyLootPoolMode.IndependentEntries, loot.PoolMode);
            Assert.AreEqual(0, loot.EmptyWeight);
            Assert.IsFalse(loot.ItemPoolComplete);
            Assert.AreEqual(4, loot.ObservedCompleteInventories);
            Assert.AreEqual(3, loot.ObservedEmptyInventories);
            Assert.AreEqual(1, loot.Entries.Length);
            Assert.AreEqual(42640, loot.Entries[0].LowId);
            Assert.AreEqual(42641, loot.Entries[0].HighId);
            Assert.AreEqual(30, loot.Entries[0].QualityLevel);
            Assert.AreEqual(1, loot.Entries[0].ObservedCount);
            Assert.AreEqual(4, loot.Entries[0].ObservedCorpses);
            Assert.AreEqual(2500, loot.Entries[0].DropChanceBasisPoints);
            Assert.AreEqual(OrdinaryEnemyEvidenceState.Policy, loot.CreditEvidence);
            Assert.AreEqual(150, loot.MinimumCredits);
            Assert.AreEqual(150, loot.MaximumCredits);

            OrdinaryEnemyLootTableAdapterResult adapted = OrdinaryEnemyLootTableAdapter.Build(
                Profile("Bloodcreeper"),
                "subway.test.bloodcreeper",
                "subway.test.bloodcreeper.assignment");
            Assert.IsTrue(adapted.Table.ItemPoolUnresolved);
            Assert.AreEqual(1, adapted.Table.RollGroups.Length);
            Assert.AreEqual(LootRollMode.Independent, adapted.Table.RollGroups[0].RollMode);
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

            Assert.AreEqual(298, evidence.Length);
            Assert.AreEqual(26, evidence.Select(value => value.MonsterData).Distinct().Count());
            CollectionAssert.AreEqual(
                new[]
                    {
                        "20260709-220439:41",
                        "20260709-222339:15",
                        "20260709-225408:61",
                        "20260710-211430:6",
                        "20260712-223719:13",
                        "20260712-232137:5",
                        "20260716-034104:1",
                        "20260716-221358:2",
                        "20260716-222007:3",
                        "20260716-222201:2"
                    },
                evidence
                    .Where(
                        value => value.Capture == "20260709-220439"
                                 || value.Capture == "20260709-222339"
                                 || value.Capture == "20260709-225408"
                                 || value.Capture == "20260710-211430"
                                 || value.Capture == "20260712-223719"
                                 || value.Capture == "20260712-232137"
                                 || value.Capture == "20260716-034104"
                                 || value.Capture == "20260716-221358"
                                 || value.Capture == "20260716-222007"
                                 || value.Capture == "20260716-222201")
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
        public void RecoveredLevelCreditRowsKeepSixteenExactDeathCorpseLinks()
        {
            var provider = new CapturedSubwayOrdinaryContentProvider();
            int[] monsterData = { 31909, 203746, 203730, 203727, 204178, 55648, 96195 };
            string[] captures =
                {
                    "20260710-211430",
                    "20260712-232137",
                    "20260716-034104",
                    "20260716-221358",
                    "20260716-222201"
                };
            string[] actual = monsterData
                .SelectMany(value => provider.GetCorpseEvidence(value))
                .Where(value => captures.Contains(value.Capture))
                .Select(
                    value => string.Format(
                        "{0}|{1}|{2}|{3}|{4}",
                        value.Capture,
                        value.DeadNpcIdentity,
                        value.CorpseIdentity,
                        value.EnemyLevel,
                        value.Credits))
                .ToArray();

            CollectionAssert.AreEquivalent(
                new[]
                    {
                        "20260710-211430|(SimpleChar:7957E62C)|(Corpse:00F6C010)|15|92",
                        "20260710-211430|(SimpleChar:7957E630)|(Corpse:00F6C00E)|12|15",
                        "20260710-211430|(SimpleChar:7957E648)|(Corpse:00F6C003)|18|111",
                        "20260710-211430|(SimpleChar:7957E653)|(Corpse:00F6C005)|21|26",
                        "20260710-211430|(SimpleChar:7957E656)|(Corpse:00F6C007)|16|98",
                        "20260710-211430|(SimpleChar:7957E65A)|(Corpse:00F6C019)|17|105",
                        "20260712-232137|(SimpleChar:79607AC5)|(Corpse:00F6C005)|24|150",
                        "20260712-232137|(SimpleChar:79607AC6)|(Corpse:00F6C008)|24|150",
                        "20260712-232137|(SimpleChar:79607AD0)|(Corpse:00F6C00A)|24|150",
                        "20260712-232137|(SimpleChar:79607AD1)|(Corpse:00F6C00D)|24|150",
                        "20260712-232137|(SimpleChar:79607AD2)|(Corpse:00F6C00B)|24|150",
                        "20260716-034104|(SimpleChar:796CD74A)|(Corpse:00F69001)|25|156",
                        "20260716-221358|(SimpleChar:79702517)|(Corpse:00F69007)|25|156",
                        "20260716-221358|(SimpleChar:7970251A)|(Corpse:00F69020)|25|156",
                        "20260716-222201|(SimpleChar:797024DA)|(Corpse:00F6901A)|20|124",
                        "20260716-222201|(SimpleChar:7970250F)|(Corpse:00F69009)|22|137"
                    },
                actual);
        }

        [TestMethod]
        public void StrictDeepCorpseSnapshotsAloneSupplyRuntimeProbabilityDenominators()
        {
            OrdinaryEnemyLootProfile redundant = Profile("Redundant Scan").Loot;
            Assert.AreEqual(2, redundant.ObservedCompleteInventories);
            Assert.AreEqual(1, redundant.ObservedEmptyInventories);
            Assert.IsFalse(redundant.ItemPoolComplete);
            OrdinaryEnemyLootEntry redundantItem = redundant.Entries.Single();
            Assert.AreEqual(27263, redundantItem.LowId);
            Assert.AreEqual(10, redundantItem.QualityLevel);
            Assert.AreEqual(5000, redundantItem.DropChanceBasisPoints);

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
        public void WorkmanStrikerUsesTenDeduplicatedCompleteCorpseOpens()
        {
            OrdinaryEnemyLootProfile loot = Profile("Workman Striker").Loot;

            Assert.AreEqual(OrdinaryEnemyLootPoolMode.IndependentEntries, loot.PoolMode);
            Assert.IsFalse(loot.ItemPoolComplete);
            Assert.AreEqual(10, loot.ObservedCompleteInventories);
            Assert.AreEqual(2, loot.ObservedEmptyInventories);
            Assert.AreEqual(10, loot.Entries.Length);
            Assert.IsTrue(
                loot.Entries.All(
                    value => value.Evidence == OrdinaryEnemyLootEvidence.ObservedAvailableLoot
                             && value.ProbabilityEvidence
                             == OrdinaryEnemyLootProbabilityEvidence.ExistingCapturePolicy
                             && value.ObservedCorpses == 10));
            CollectionAssert.AreEqual(
                new[]
                    {
                        "85562:85561:14:1:10:1000",
                        "124025:124026:12:1:10:1000",
                        "124263:124264:13:1:10:1000",
                        "130087:130088:16:1:10:1000",
                        "202719:202720:12:1:10:1000",
                        "202719:202720:14:2:10:2000",
                        "202719:202720:17:1:10:1000",
                        "234874:234874:1:1:10:1000",
                        "234877:234877:1:1:10:1000",
                        "301714:301714:1:2:10:2000"
                    },
                loot.Entries
                    .OrderBy(value => value.LowId)
                    .ThenBy(value => value.QualityLevel)
                    .Select(
                        value => string.Format(
                            "{0}:{1}:{2}:{3}:{4}:{5}",
                            value.LowId,
                            value.HighId,
                            value.QualityLevel,
                            value.ObservedCount,
                            value.ObservedCorpses,
                            value.DropChanceBasisPoints))
                    .ToArray());

            CapturedSubwayLootOutcomeEvidenceDefinition[] outcomes =
                new CapturedSubwayOrdinaryContentProvider().GetLootOutcomeEvidence(203854);
            Assert.AreEqual(12, outcomes.Length);
            Assert.IsFalse(outcomes.Any(value => value.Capture == "20260709-212115"));

            OrdinaryEnemyLootTableAdapterResult adapted = OrdinaryEnemyLootTableAdapter.Build(
                Profile("Workman Striker"),
                "subway.test.workman-striker",
                "subway.test.workman-striker.assignment");
            Assert.IsTrue(adapted.Table.ItemPoolUnresolved);
            Assert.AreEqual(10, adapted.Table.RollGroups.Length);
            Assert.IsTrue(
                adapted.Table.RollGroups.All(value => value.RollMode == LootRollMode.Independent));
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
            Assert.AreEqual(13, runtimeStim.Loot.ObservedCompleteInventories);
            Assert.AreEqual(0, runtimeStim.Loot.ObservedEmptyInventories);
            Assert.AreEqual(17, runtimeStim.Loot.Entries.Length);
            Assert.IsFalse(runtimeStim.Loot.Entries.Any(value => value.LowId == 130592));
            Assert.IsFalse(runtimeStim.Loot.Entries.Any(value => value.LowId == 123704));
            Assert.IsFalse(runtimeStim.Loot.ItemPoolComplete);
            OrdinaryEnemyLootTableAdapterResult adapted = OrdinaryEnemyLootTableAdapter.Build(
                runtimeStim,
                "subway.test.stim-outcomes",
                "subway.test.stim-outcomes.assignment");
            Assert.AreEqual(17, adapted.Table.RollGroups.Length);
            Assert.IsTrue(
                adapted.Table.RollGroups.All(value => value.RollMode == LootRollMode.Independent));
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
            Assert.AreEqual(10, runtimeShadow.Loot.Entries.Length);
            Assert.IsFalse(runtimeShadow.Loot.ItemPoolComplete);
        }

        [TestMethod]
        public void ReviewedLegacyStrictOpensSupplyFourIncompleteIndependentLootPools()
        {
            AssertReviewedLegacyStrictLoot(
                "Shadow",
                15,
                7,
                new[]
                    {
                        "21601:21601:1:1:15:667",
                        "27199:27199:10:1:15:667",
                        "121931:121932:15:1:15:667",
                        "122007:122008:12:1:15:667",
                        "123666:123667:9:1:15:667",
                        "124364:124365:10:1:15:667",
                        "124512:124513:28:1:15:667",
                        "152279:152280:18:1:15:667",
                        "234875:234875:1:2:15:1333",
                        "234876:234876:1:1:15:667"
                    });
            AssertReviewedLegacyStrictLoot(
                "Infector",
                7,
                4,
                new[]
                    {
                        "101507:101508:20:1:7:1429",
                        "101735:101736:21:1:7:1429",
                        "107491:107492:15:1:7:1429",
                        "234875:234875:1:1:7:1429"
                    });
            AssertReviewedLegacyStrictLoot(
                "Architect Striker",
                4,
                1,
                new[]
                    {
                        "122482:122483:14:1:4:2500",
                        "124422:124423:13:1:4:2500",
                        "128890:128891:14:1:4:2500",
                        "234877:234877:1:1:4:2500"
                    });
            AssertReviewedLegacyStrictLoot(
                "Melded Patterns",
                4,
                1,
                new[]
                    {
                        "122672:122673:15:1:4:2500",
                        "144067:144068:23:1:4:2500",
                        "152328:152329:24:1:4:2500",
                        "234874:234874:1:1:4:2500",
                        "301710:301710:1:1:4:2500"
                    });

            CapturedSubwayOrdinaryArchetypeDefinition[] source =
                new CapturedSubwayOrdinaryContentProvider().GetArchetypes();
            CapturedSubwayLootOutcomeEvidenceDefinition[] shadowOutcomes = source
                .Single(value => value.Name == "Shadow")
                .LootOutcomeEvidence;
            Assert.AreEqual(11, shadowOutcomes.Length);
            Assert.IsFalse(
                shadowOutcomes.Any(value => value.Capture == "20260709-212115"));
            CollectionAssert.AreEquivalent(
                new[] { "2914:152279", "2941:124512", "3180:21601" },
                shadowOutcomes
                    .Where(value => value.Capture == "20260712-223719")
                    .Select(value => value.Sequence + ":" + value.LowId)
                    .ToArray());

            CapturedSubwayLootOutcomeEvidenceDefinition[] meldedOutcomes = source
                .Single(value => value.Name == "Melded Patterns")
                .LootOutcomeEvidence;
            CollectionAssert.AreEquivalent(
                new[] { "2997:144067", "2997:301710" },
                meldedOutcomes
                    .Where(value => value.Capture == "20260712-223719")
                    .Select(value => value.Sequence + ":" + value.LowId)
                    .ToArray());
        }

        [TestMethod]
        public void RecoveredFirstOpenCorpusSuppliesFourteenIncompleteIndependentLootPools()
        {
            AssertRecoveredStrictLoot(
                "Mugger",
                17,
                3,
                new[]
                    {
                        "25822:25831:5:1", "85711:22014:8:1", "123704:123705:9:1",
                        "123723:123724:6:1", "123976:123977:9:1", "124348:124349:7:1",
                        "124545:124546:10:1", "128636:128637:8:1", "128839:128840:9:1",
                        "130060:130061:5:1", "130060:130061:9:1", "131605:131606:7:1",
                        "136638:136639:9:1", "136638:136639:12:1", "136640:136641:7:1",
                        "136640:136641:8:1", "136640:136641:9:1", "136646:136647:9:1",
                        "160224:160225:10:1", "234875:234875:1:2", "234876:234876:1:1"
                    });
            AssertRecoveredStrictLoot(
                "Discarded Pet",
                16,
                3,
                new[]
                    {
                        "101681:101682:7:1", "102283:102284:9:1", "103973:103974:10:1",
                        "106005:106006:11:1", "107283:107284:10:1", "109520:109521:7:1",
                        "111623:111624:8:1", "112160:112161:6:1", "112798:112799:6:1",
                        "234874:234874:1:3", "234876:234876:1:3", "234877:234877:1:1",
                        "290619:202727:9:1"
                    });
            AssertRecoveredStrictLoot(
                "Stim Fiend",
                13,
                0,
                new[]
                    {
                        "102055:102056:11:1", "112232:112233:11:1", "234874:234874:1:1",
                        "234876:234876:1:1", "234877:234877:1:1", "291043:291044:9:6",
                        "291043:291044:10:2", "291043:291044:11:1", "291043:291044:12:2",
                        "291043:291044:13:1", "291043:291044:15:1", "291082:291083:9:6",
                        "291082:291083:10:2", "291082:291083:11:1", "291082:291083:12:2",
                        "291082:291083:13:1", "291082:291083:15:1"
                    });
            AssertRecoveredStrictLoot(
                "Looter",
                11,
                5,
                new[]
                    {
                        "21605:21605:1:1", "85501:22343:12:1", "124422:124422:12:1",
                        "144082:144083:7:1", "234874:234874:1:1", "234875:234875:1:1",
                        "234877:234877:1:1", "301713:301713:1:1", "301714:301714:1:1"
                    });
            AssertRecoveredStrictLoot(
                "Violent Vagabond",
                11,
                1,
                new[]
                    {
                        "85531:22289:8:1", "122140:122141:7:1", "123704:123705:12:1",
                        "128715:128716:6:1", "130586:130586:1:4", "130592:130592:1:2",
                        "130621:130621:1:1", "152326:152327:6:1", "234876:234876:1:1",
                        "258543:258543:1:7", "273381:204397:8:1"
                    });
            AssertRecoveredStrictLoot(
                "Bloodcreeper",
                4,
                3,
                new[] { "42640:42641:30:1" });
            AssertRecoveredStrictLoot(
                "Infected Attendant",
                4,
                1,
                new[]
                    {
                        "101695:101696:24:1", "109194:109195:12:1", "112823:112824:17:1",
                        "234875:234875:1:1", "290619:202727:12:1"
                    });
            AssertRecoveredStrictLoot(
                "Fragmented Soul",
                4,
                0,
                new[]
                    {
                        "26471:26471:14:3", "85691:22004:18:1", "85732:21963:17:1",
                        "124304:124305:17:1", "234877:234877:1:2", "301712:301712:1:1"
                    });
            AssertRecoveredStrictLoot(
                "Deranged Shopper",
                2,
                0,
                new[] { "123019:123020:6:1", "124465:124466:10:1" });
            AssertRecoveredStrictLoot(
                "Incomplete Rebuild",
                2,
                0,
                new[] { "26503:26503:14:1", "142817:142818:16:1" });
            AssertRecoveredStrictLoot(
                "Redundant Scan",
                2,
                1,
                new[] { "27263:27263:10:1" });
            AssertRecoveredStrictLoot(
                "Uncontrollable Anger",
                2,
                0,
                new[]
                    {
                        "101809:101810:24:1", "109366:109367:9:1", "290619:202727:19:1"
                    });
            AssertRecoveredStrictLoot(
                "Lost Thought",
                1,
                0,
                new[] { "101675:101676:25:1" });
            AssertRecoveredStrictLoot(
                "Neural Burnout",
                4,
                2,
                new[]
                    {
                        "26471:26471:14:1", "123021:123021:21:1", "124560:124561:16:1"
                    });

            var provider = new CapturedSubwayOrdinaryContentProvider();
            foreach (string excludedName in new[] { "Empty Shell", "Premature Pattern" })
            {
                CapturedSubwayOrdinaryArchetypeDefinition archetype = provider.GetArchetypes()
                    .Single(value => value.Name == excludedName);
                Assert.IsNull(provider.GetStrictLootProfile(archetype.MonsterData), excludedName);
                Assert.AreEqual(0, Profile(excludedName).Loot.Entries.Length, excludedName);
            }

            CapturedSubwayStrictLootProfileDefinition mugger =
                provider.GetStrictLootProfile(203734);
            CapturedSubwayStrictLootProfileDefinition stim =
                provider.GetStrictLootProfile(203739);
            Assert.IsFalse(mugger.EvidenceCaptures.Contains("20260709-212115"));
            Assert.IsFalse(stim.EvidenceCaptures.Contains("20260709-212115"));
        }

        [TestMethod]
        public void SlumRunnerPreservesTwentyOneDeathLinkedCorpseVisualAndLevelCreditOutcomes()
        {
            CapturedSubwayOrdinaryArchetypeDefinition source =
                new CapturedSubwayOrdinaryContentProvider()
                    .GetArchetypes()
                    .Single(value => value.Name == "Slum Runner");
            Assert.AreEqual(21, source.CorpseEvidence.Length);
            Assert.AreEqual(4, source.CorpseEvidence.Count(value => value.Capture == "20260709-220439"));
            Assert.AreEqual(2, source.CorpseEvidence.Count(value => value.Capture == "20260709-222339"));
            Assert.AreEqual(6, source.CorpseEvidence.Count(value => value.Capture == "20260709-225408"));
            Assert.AreEqual(1, source.CorpseEvidence.Count(value => value.Capture == "20260710-211430"));
            Assert.AreEqual(6, source.CorpseEvidence.Count(value => value.Capture == "20260716-034656"));
            Assert.AreEqual(1, source.CorpseEvidence.Count(value => value.Capture == "20260716-215947"));
            Assert.AreEqual(1, source.CorpseEvidence.Count(value => value.Capture == "20260716-222201"));
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
                        "(SimpleChar:797024AE)>(Corpse:00F69002)",
                        "(SimpleChar:7957E62C)>(Corpse:00F6C010)",
                        "(SimpleChar:797024DA)>(Corpse:00F6901A)"
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
                        "15:92:1",
                        "16:98:4",
                        "17:105:3",
                        "18:111:1",
                        "20:124:1",
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
                "18:111:111:1",
                "19:118:118:3",
                "24:150:150:5",
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
                "24:150:150:1",
                "25:156:156:1");
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
                "16:98:98:1",
                "17:105:105:1",
                "18:111:111:2",
                "23:144:144:1",
                "25:156:156:2");
            AssertCorpseAndLevelCredits(
                "Premature Pattern",
                5941,
                "17:105:105:1",
                "18:111:111:1",
                "23:144:144:2");
            AssertCorpseAndLevelCredits(
                "Redundant Scan",
                23370,
                "19:118:118:1",
                "20:124:124:1",
                "21:131:131:1",
                "22:137:137:1");
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
                "12:15:15:1",
                "13:16:16:2",
                "20:25:25:1",
                "21:26:26:1");
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
        public void MuggerLevelTenUsesIdentityLinkedCreditsAndReviewedStrictLoot()
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
            Assert.AreEqual(17, profile.Loot.ObservedCompleteInventories);
            Assert.AreEqual(3, profile.Loot.ObservedEmptyInventories);
            Assert.AreEqual(21, profile.Loot.Entries.Length);
            Assert.IsFalse(profile.Loot.ItemPoolComplete);

            OrdinaryEnemyLootTableAdapterResult adapted = OrdinaryEnemyLootTableAdapter.Build(
                profile,
                10,
                "subway.test.mugger.level10",
                "subway.test.mugger.level10.assignment");
            Assert.AreEqual(CreditsPolicyMode.Fixed, adapted.Table.CreditsPolicy.Mode);
            Assert.AreEqual(88, adapted.Table.CreditsPolicy.MinimumCredits);
            Assert.AreEqual(88, adapted.Table.CreditsPolicy.MaximumCredits);
            Assert.AreEqual(21, adapted.Table.RollGroups.Length);
            Assert.IsTrue(
                adapted.Table.RollGroups.All(value => value.RollMode == LootRollMode.Independent));
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

        private static void AssertReviewedLegacyStrictLoot(
            string displayName,
            int observedCorpses,
            int observedEmptyCorpses,
            string[] expectedEntries)
        {
            OrdinaryEnemyProfile profile = Profile(displayName);
            OrdinaryEnemyLootProfile loot = profile.Loot;

            Assert.AreEqual(
                OrdinaryEnemyLootEvidence.ObservedAvailableLoot,
                loot.Evidence);
            Assert.AreEqual(OrdinaryEnemyLootPoolMode.IndependentEntries, loot.PoolMode);
            Assert.AreEqual(0, loot.EmptyWeight);
            Assert.IsFalse(loot.ItemPoolComplete);
            Assert.AreEqual(observedCorpses, loot.ObservedCompleteInventories);
            Assert.AreEqual(observedEmptyCorpses, loot.ObservedEmptyInventories);
            Assert.IsFalse(
                loot.Entries.Any(
                    value => value.Evidence == OrdinaryEnemyLootEvidence.GuaranteedProven));
            Assert.IsTrue(
                loot.Entries.All(
                    value => value.Evidence == OrdinaryEnemyLootEvidence.ObservedAvailableLoot
                             && value.ProbabilityEvidence
                             == OrdinaryEnemyLootProbabilityEvidence.ExistingCapturePolicy
                             && value.ObservedCorpses == observedCorpses));
            CollectionAssert.AreEqual(
                expectedEntries,
                loot.Entries
                    .OrderBy(value => value.LowId)
                    .ThenBy(value => value.QualityLevel)
                    .Select(
                        value => string.Format(
                            "{0}:{1}:{2}:{3}:{4}:{5}",
                            value.LowId,
                            value.HighId,
                            value.QualityLevel,
                            value.ObservedCount,
                            value.ObservedCorpses,
                            value.DropChanceBasisPoints))
                    .ToArray());

            OrdinaryEnemyLootTableAdapterResult adapted = OrdinaryEnemyLootTableAdapter.Build(
                profile,
                "subway.test.reviewed-legacy." + displayName,
                "subway.test.reviewed-legacy.assignment." + displayName);
            Assert.IsTrue(adapted.Table.ItemPoolUnresolved);
            Assert.AreEqual(expectedEntries.Length, adapted.Table.RollGroups.Length);
            Assert.IsTrue(
                adapted.Table.RollGroups.All(
                    value => value.RollMode == LootRollMode.Independent));
            Assert.IsFalse(
                adapted.Table.RollGroups.Any(
                    value => value.RollMode == LootRollMode.Guaranteed));
        }

        private static void AssertRecoveredStrictLoot(
            string displayName,
            int observedCorpses,
            int observedEmptyCorpses,
            string[] expectedEntries)
        {
            OrdinaryEnemyProfile profile = Profile(displayName);
            OrdinaryEnemyLootProfile loot = profile.Loot;
            var provider = new CapturedSubwayOrdinaryContentProvider();
            CapturedSubwayStrictLootProfileDefinition source =
                provider.GetStrictLootProfile(profile.MonsterData);

            Assert.IsNotNull(source, displayName);
            Assert.AreEqual(displayName, source.Name, displayName);
            Assert.AreEqual(observedCorpses, source.ObservedCompleteInventories, displayName);
            Assert.AreEqual(
                observedCorpses - observedEmptyCorpses,
                source.ObservedPositiveInventories,
                displayName);
            Assert.AreEqual(observedEmptyCorpses, source.ObservedEmptyInventories, displayName);
            Assert.IsFalse(source.ItemPoolComplete, displayName);
            Assert.AreEqual(expectedEntries.Length, source.Entries.Length, displayName);
            Assert.AreEqual(
                expectedEntries.Length,
                provider.BuildCapturedLootEntries()
                    .Count(
                        value => value.ExactName == displayName
                                 && value.MonsterData == profile.MonsterData),
                displayName + " legacy runtime table");

            Assert.AreEqual(
                OrdinaryEnemyLootEvidence.ObservedAvailableLoot,
                loot.Evidence,
                displayName);
            Assert.AreEqual(OrdinaryEnemyLootPoolMode.IndependentEntries, loot.PoolMode, displayName);
            Assert.AreEqual(0, loot.EmptyWeight, displayName);
            Assert.IsFalse(loot.ItemPoolComplete, displayName);
            Assert.AreEqual(observedCorpses, loot.ObservedCompleteInventories, displayName);
            Assert.AreEqual(observedEmptyCorpses, loot.ObservedEmptyInventories, displayName);
            Assert.IsTrue(
                loot.Entries.All(
                    value => value.Evidence == OrdinaryEnemyLootEvidence.ObservedAvailableLoot
                             && value.ProbabilityEvidence
                             == OrdinaryEnemyLootProbabilityEvidence.ExistingCapturePolicy
                             && value.ObservedCorpses == observedCorpses),
                displayName);
            Assert.IsFalse(
                loot.Entries.Any(
                    value => value.Evidence == OrdinaryEnemyLootEvidence.GuaranteedProven),
                displayName);
            CollectionAssert.AreEqual(
                expectedEntries,
                loot.Entries
                    .OrderBy(value => value.LowId)
                    .ThenBy(value => value.QualityLevel)
                    .Select(
                        value => string.Format(
                            "{0}:{1}:{2}:{3}",
                            value.LowId,
                            value.HighId,
                            value.QualityLevel,
                            value.ObservedCount))
                    .ToArray(),
                displayName);
            foreach (OrdinaryEnemyLootEntry entry in loot.Entries)
            {
                Assert.AreEqual(
                    Math.Min(
                        10000,
                        (int)Math.Round(
                            entry.ObservedCount * 10000.0 / observedCorpses)),
                    entry.DropChanceBasisPoints,
                    displayName + ":" + entry.LowId + ":" + entry.QualityLevel);
            }

            OrdinaryEnemyLootTableAdapterResult adapted = OrdinaryEnemyLootTableAdapter.Build(
                profile,
                "subway.test.recovered-strict." + displayName,
                "subway.test.recovered-strict.assignment." + displayName);
            Assert.IsTrue(adapted.Table.ItemPoolUnresolved, displayName);
            Assert.AreEqual(expectedEntries.Length, adapted.Table.RollGroups.Length, displayName);
            Assert.IsTrue(
                adapted.Table.RollGroups.All(
                    value => value.RollMode == LootRollMode.Independent),
                displayName);
            Assert.IsFalse(
                adapted.Table.RollGroups.Any(
                    value => value.RollMode == LootRollMode.Guaranteed),
                displayName);
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
