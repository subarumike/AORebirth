namespace SmokeLounge.AOtomation.Messaging.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.IO;
    using System.Web.Script.Serialization;

    using Microsoft.VisualStudio.TestTools.UnitTesting;

    using SmokeLounge.AOtomation.Messaging.GameData;
    using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;

    using ZoneEngine.Core.Missions;
    using ZoneEngine_New.Core.Missions;

    [TestClass]
    [DeploymentItem(@".\XML Data\MissionLevels.csv", @"XML Data")]
    [DeploymentItem(@".\XML Data\MissionRewards", @"XML Data\MissionRewards")]
    public class MissionRollSemanticsTests
    {
        [TestMethod]
        public void MalisRollableNanosAreDistinctFamiliesAtOneLockedQuality()
        {
            string path = Path.Combine(
                AppDomain.CurrentDomain.BaseDirectory,
                "XML Data",
                "MissionRewards",
                "ItemDb_Nanos.json");
            var serializer = new JavaScriptSerializer { MaxJsonLength = int.MaxValue };
            List<MalisNanoEntry> rows =
                serializer.Deserialize<List<MalisNanoEntry>>(File.ReadAllText(path));
            var families = new HashSet<string>(StringComparer.Ordinal);

            Assert.AreEqual(2112, rows.Count);
            foreach (MalisNanoEntry row in rows)
            {
                Assert.IsNotNull(row);
                Assert.IsNotNull(row.Key);
                Assert.AreEqual(
                    row.Key.LowQl,
                    row.Key.HighQl,
                    "Nano family "
                    + row.Key.LowId
                    + ":"
                    + row.Key.HighId
                    + " must have one locked QL.");
                families.Add(row.Key.LowId + ":" + row.Key.HighId);
            }

            Assert.AreEqual(2110, families.Count);
        }

        [TestMethod]
        public void OfficialFindAndReturnItemIconsMapToCapturedBehavior()
        {
            Assert.AreEqual(MissionRollType.FindItemReturn, MissionRollPolicy.Current.TypeFromIcon(11329));
            Assert.AreEqual(MissionRollType.FindItem, MissionRollPolicy.Current.TypeFromIcon(11337));
            Assert.AreEqual(11329, MissionRollPolicy.Current.Icon(MissionRollType.FindItemReturn));
            Assert.AreEqual(11337, MissionRollPolicy.Current.Icon(MissionRollType.FindItem));
            Assert.AreEqual(MissionRollType.Unknown, MissionRollPolicy.Current.TypeFromIcon(999999));
        }

        [TestMethod]
        public void DifficultyWireValuesAreOneBasedAndLevel60EasiestIsQl42()
        {
            int sliderIndex;
            Assert.IsTrue(MissionLevelRuntime.TryDecodeDifficultySlider(1, out sliderIndex));
            Assert.AreEqual(0, sliderIndex);
            Assert.IsTrue(MissionLevelRuntime.TryDecodeDifficultySlider(11, out sliderIndex));
            Assert.AreEqual(10, sliderIndex);
            Assert.IsFalse(MissionLevelRuntime.TryDecodeDifficultySlider(0, out sliderIndex));
            Assert.IsFalse(MissionLevelRuntime.TryDecodeDifficultySlider(12, out sliderIndex));

            int missionQuality;
            Assert.IsTrue(MissionLevelRuntime.TryGetMissionQuality(60, 1, out missionQuality));
            Assert.AreEqual(42, missionQuality);
            Assert.IsTrue(MissionLevelRuntime.TryGetMissionQuality(220, 11, out missionQuality));
            Assert.AreEqual(250, missionQuality);
            Assert.IsFalse(MissionLevelRuntime.TryGetMissionQuality(60, 0, out missionQuality));
            Assert.IsFalse(MissionLevelRuntime.TryGetMissionQuality(60, 12, out missionQuality));

            Assert.AreEqual(1, MissionLevelRuntime.ClampCharacterLevel(0));
            Assert.AreEqual(220, MissionLevelRuntime.ClampCharacterLevel(221));
        }

        [TestMethod]
        public void GeneratedRollUsesTheClampedCharacterLevelConsistently()
        {
            QuestAlternativeMessage request = Request(1, 0, 0, 0, 0, 0, 0);
            CollectionAssert.AreEqual(
                GeneratedMissionWire.Write(Build(request, 1, 707)),
                GeneratedMissionWire.Write(Build(request, 0, 707)));
            CollectionAssert.AreEqual(
                GeneratedMissionWire.Write(Build(request, 220, 808)),
                GeneratedMissionWire.Write(Build(request, 221, 808)));
        }

        [TestMethod]
        public void ContinuousSliderBytesDecodeAsSignedPercentAndRejectInvalidRange()
        {
            Assert.IsTrue(MissionRollSliders.TryCreate(Request(1, 156, 0, 0, 0, 0, 0), out var left, out _));
            Assert.AreEqual(-100, left.GoodBad);
            Assert.IsTrue(MissionRollSliders.TryCreate(Request(1, 0, 0, 0, 0, 0, 0), out var neutral, out _));
            Assert.AreEqual(0, neutral.GoodBad);
            Assert.IsTrue(MissionRollSliders.TryCreate(Request(1, 100, 0, 0, 0, 0, 0), out var right, out _));
            Assert.AreEqual(100, right.GoodBad);
            Assert.IsFalse(MissionRollSliders.TryCreate(Request(1, 101, 0, 0, 0, 0, 0), out _, out _));
            Assert.IsFalse(MissionRollSliders.TryCreate(Request(1, 155, 0, 0, 0, 0, 0), out _, out _));
        }

        [TestMethod]
        public void GeneratedRollRejectsUnsupportedDifficultyAndSliderWireValues()
        {
            AssertRejected(delegate { Build(Request(0, 0, 0, 0, 0, 0, 0), 60, 1); });
            AssertRejected(delegate { Build(Request(12, 0, 0, 0, 0, 0, 0), 60, 1); });
            AssertRejected(delegate { Build(Request(1, 101, 0, 0, 0, 0, 0), 60, 1); });
            AssertRejected(delegate { Build(Request(1, 0, 0, 0, 155, 0, 0), 60, 1); });
        }

        [TestMethod]
        public void UnresolvedSliderCombinationsFallBackCategoricallyToNeutralEvidence()
        {
            MissionRollSliders neutral = Profile(Request(1, 0, 0, 0, 0, 0, 0));
            MissionRollSliders capturedLeft = Profile(Request(1, 156, 156, 0, 0, 0, 156));
            MissionRollSliders unresolved = Profile(Request(1, 25, 231, 100, 156, 50, 25));

            Assert.AreEqual("Neutral", neutral.EvidenceProfile);
            Assert.AreEqual(
                "CapturedLeftGoodBadOrderChaosCreditsXp",
                capturedLeft.EvidenceProfile);
            Assert.AreEqual("Unresolved", unresolved.EvidenceProfile);
            Assert.AreEqual(0, unresolved.SemanticDistance(0, 0, 0, 0, 0, 0));
            Assert.AreEqual(1, unresolved.SemanticDistance(-100, -100, 0, 0, 0, -100));
            Assert.AreEqual(0, capturedLeft.SemanticDistance(-100, -100, 0, 0, 0, -100));
            Assert.AreEqual(1, capturedLeft.SemanticDistance(0, 0, 0, 0, 0, 0));
        }

        [TestMethod]
        public void CapturedLibraryCoversEveryFinalizedMissionTypeWithCompatibleActions()
        {
            var types = new HashSet<MissionRollType>();
            for (int rollIndex = 0; rollIndex < GeneratedMissionWire.CapturedCount; rollIndex++)
            {
                QuestAlternativeMessage roll = GeneratedMissionWire.Read(GeneratedMissionWire.CapturedBody(rollIndex));
                foreach (QuestInfo offer in roll.QuestInfos)
                {
                    MissionOfferDescriptor descriptor;
                    string error;
                    Assert.IsTrue(
                        MissionOfferCompatibility.TryDescribeCaptured(
                            offer,
                            roll.MissionTerminalIdentity,
                            out descriptor,
                            out error),
                        "roll " + rollIndex + ": " + error);
                    types.Add(descriptor.Type);
                }
            }

            CollectionAssert.AreEquivalent(
                new[]
                {
                    MissionRollType.KillPerson,
                    MissionRollType.FindPerson,
                    MissionRollType.FindItem,
                    MissionRollType.FindItemReturn,
                    MissionRollType.RepairMachine
                },
                new List<MissionRollType>(types));
        }

        [TestMethod]
        public void LeftCapturedSliderProfileReproducesItsObservedTypeCohort()
        {
            QuestAlternativeMessage response = Build(
                Request(1, 156, 156, 0, 0, 0, 156),
                60,
                17);
            var counts = new Dictionary<MissionRollType, int>();
            foreach (QuestInfo offer in response.QuestInfos)
            {
                MissionRollType type = MissionRollPolicy.Current.TypeFromIcon(offer.MissionIconId);
                counts[type] = counts.ContainsKey(type) ? counts[type] + 1 : 1;
            }

            Assert.AreEqual(3, counts[MissionRollType.RepairMachine]);
            Assert.AreEqual(1, counts[MissionRollType.KillPerson]);
            Assert.AreEqual(1, counts[MissionRollType.FindPerson]);
            Assert.AreEqual(3, counts.Count);
        }

        [TestMethod]
        public void AllCapturedDifficultyDetentsGenerateForNeutralLeftAndUnresolvedProfiles()
        {
            int[] levels = { 60, 220 };
            for (int levelIndex = 0; levelIndex < levels.Length; levelIndex++)
            {
                for (byte difficulty = 1; difficulty <= 11; difficulty++)
                {
                    QuestAlternativeMessage[] requests =
                    {
                        Request(difficulty, 0, 0, 0, 0, 0, 0),
                        Request(difficulty, 156, 156, 0, 0, 0, 156),
                        Request(difficulty, 25, 231, 100, 156, 50, 25)
                    };
                    int expectedQuality;
                    Assert.IsTrue(
                        MissionLevelRuntime.TryGetMissionQuality(
                            levels[levelIndex],
                            difficulty,
                            out expectedQuality));

                    for (int profileIndex = 0; profileIndex < requests.Length; profileIndex++)
                    {
                        QuestAlternativeMessage response = Build(
                            requests[profileIndex],
                            levels[levelIndex],
                            (levels[levelIndex] * 100) + (difficulty * 10) + profileIndex);
                        Assert.AreEqual(5, response.QuestInfos.Length);
                        foreach (QuestInfo offer in response.QuestInfos)
                        {
                            Assert.AreEqual(expectedQuality, offer.Quality);
                        }
                    }
                }
            }
        }

        [TestMethod]
        public void GeneratedRollsAreDeterministicForTheSameSeed()
        {
            QuestAlternativeMessage request = Request(1, 0, 0, 0, 0, 0, 0);
            byte[] first = GeneratedMissionWire.Write(Build(request, 60, 12345));
            byte[] second = GeneratedMissionWire.Write(Build(request, 60, 12345));
            CollectionAssert.AreEqual(first, second);

            byte[] different = GeneratedMissionWire.Write(Build(request, 60, 54321));
            Assert.IsFalse(AreEqual(first, different), "Different seeds should be able to select a different valid roll.");
        }

        [TestMethod]
        public void GeneratedRollsDoNotMutateCapturedBodies()
        {
            var before = new byte[GeneratedMissionWire.CapturedCount][];
            for (int i = 0; i < before.Length; i++)
            {
                before[i] = GeneratedMissionWire.CapturedBody(i);
            }

            for (int seed = 0; seed < 16; seed++)
            {
                Build(Request(1, 0, 0, 0, 0, 0, 0), 60, seed);
            }

            for (int i = 0; i < before.Length; i++)
            {
                CollectionAssert.AreEqual(before[i], GeneratedMissionWire.CapturedBody(i), "roll " + i);
            }
        }

        [TestMethod]
        public void GeneratedFiveOfferSetsAreIndependentlyCoherent()
        {
            for (int seed = 0; seed < 24; seed++)
            {
                QuestAlternativeMessage request = Request(1, 0, 0, 0, 0, 0, 0);
                QuestAlternativeMessage response = Build(request, 60, seed);
                Assert.AreEqual(5, response.QuestInfos.Length, "seed " + seed);

                MissionRollSliders sliders;
                string sliderError;
                Assert.IsTrue(MissionRollSliders.TryCreate(request, out sliders, out sliderError));
                var questIds = new HashSet<int>();
                foreach (QuestInfo offer in response.QuestInfos)
                {
                    MissionOfferDescriptor descriptor;
                    string compatibilityError;
                    Assert.IsTrue(
                        MissionOfferCompatibility.TryDescribe(
                            offer,
                            out descriptor,
                            out compatibilityError),
                        "seed " + seed + ": " + compatibilityError);
                    Assert.IsTrue(MissionOfferCompatibility.IsCompatibleWithSliders(descriptor, sliders));
                    Assert.AreEqual(42, offer.Quality);
                    Assert.AreEqual(MissionRollPolicy.Current.Icon(descriptor.Type), offer.MissionIconId);
                    Assert.AreEqual(
                        31,
                        offer.ShortInfo.Length,
                        "Captured title width must remain client-decodable.");
                    Assert.IsTrue(offer.ShortInfo.EndsWith("...", StringComparison.Ordinal));
                    Assert.IsTrue(questIds.Add(offer.QuestIdentity.Instance), "duplicate quest id");

                    QuestActionList destination = offer.QuestActions[0];
                    string coordinate = string.Format(
                        CultureInfo.InvariantCulture,
                        "{0:0.0}, {1:0.0}",
                        destination.X,
                        destination.Z);
                    Assert.IsTrue(offer.Info.Contains(coordinate), offer.Info);
                    Assert.IsTrue(
                        offer.Info.Contains(offer.CashReward.ToString(CultureInfo.InvariantCulture)),
                        offer.Info);
                    Assert.IsTrue(
                        offer.Info.Contains(offer.ExperienceReward.ToString(CultureInfo.InvariantCulture)),
                        offer.Info);
                    if (!string.IsNullOrEmpty(descriptor.TargetName))
                    {
                        Assert.IsTrue(offer.Info.Contains(descriptor.TargetName), offer.Info);
                    }

                    Assert.IsTrue(
                        MissionRewardEvidenceModel.IsCapturedPair(
                            descriptor.Type,
                            60,
                            1,
                            42,
                            sliders,
                            offer.CashReward,
                            offer.ExperienceReward),
                        string.Format(
                            CultureInfo.InvariantCulture,
                            "seed {0}, type {1}, pf {2}, cash {3}, xp {4} was not an exact captured type/context pair.",
                            seed,
                            descriptor.Type,
                            destination.Playfield.Instance,
                            offer.CashReward,
                            offer.ExperienceReward));
                    if (MissionRewardEvidenceModel.HasExactEvidence(
                            descriptor.Type,
                            60,
                            1,
                            42,
                            sliders,
                            destination.Playfield.Instance))
                    {
                        Assert.IsTrue(
                            MissionRewardEvidenceModel.IsCapturedPair(
                                descriptor.Type,
                                60,
                                1,
                                42,
                                sliders,
                                destination.Playfield.Instance,
                                offer.CashReward,
                                offer.ExperienceReward),
                            "A destination-specific captured reward was available but not selected.");
                    }

                    if (offer.ItemRewards != null && offer.ItemRewards.Length > 0)
                    {
                        Assert.IsTrue(
                            Math.Abs(offer.ItemRewards[0].Quality - offer.Quality) <= 10,
                            "Item reward must remain QL-aware.");
                    }
                }
            }
        }

        [TestMethod]
        public void ReturnOffersRetargetTheirObjectiveToTheIssuingTerminal()
        {
            bool foundReturn = false;
            for (int seed = 0; seed < 64 && !foundReturn; seed++)
            {
                QuestAlternativeMessage response = Build(
                    Request(1, 0, 0, 0, 0, 0, 0),
                    60,
                    seed);
                foreach (QuestInfo offer in response.QuestInfos)
                {
                    Identity objectiveTerminal = offer.QuestActions[0].Unknown1;
                    if (MissionRollPolicy.Current.TypeFromIcon(offer.MissionIconId)
                        == MissionRollType.FindItemReturn)
                    {
                        Assert.AreEqual(response.MissionTerminalIdentity, objectiveTerminal);
                        foundReturn = true;
                    }
                    else
                    {
                        Assert.AreNotEqual(response.MissionTerminalIdentity, objectiveTerminal);
                    }
                }
            }

            Assert.IsTrue(foundReturn, "Deterministic capture-backed cohorts should expose a return offer.");
        }

        [TestMethod]
        public void CompatibilityLayerRejectsTextObjectiveAndRewardContradictions()
        {
            QuestAlternativeMessage request = Request(1, 0, 0, 0, 0, 0, 0);
            MissionRollSliders sliders = Profile(request);
            QuestAlternativeMessage response = Build(request, 60, 404);
            QuestInfo offer = response.QuestInfos[0];

            MissionOfferDescriptor source;
            string error;
            Assert.IsTrue(MissionOfferCompatibility.TryDescribe(offer, out source, out error), error);

            MissionOfferDescriptor generated;
            Assert.IsTrue(
                MissionOfferCompatibility.TryValidateGenerated(
                    offer,
                    source,
                    sliders,
                    response.MissionTerminalIdentity,
                    out generated,
                    out error),
                error);

            string originalTitle = offer.ShortInfo;
            offer.ShortInfo = "Wrong mission family";
            Assert.IsFalse(
                MissionOfferCompatibility.TryValidateGenerated(
                    offer,
                    source,
                    sliders,
                    response.MissionTerminalIdentity,
                    out generated,
                    out error));
            offer.ShortInfo = originalTitle;

            int originalRewardQuality = offer.ItemRewards[0].Quality;
            offer.ItemRewards[0].Quality = offer.Quality + 11;
            Assert.IsFalse(
                MissionOfferCompatibility.TryValidateGenerated(
                    offer,
                    source,
                    sliders,
                    response.MissionTerminalIdentity,
                    out generated,
                    out error));
            offer.ItemRewards[0].Quality = originalRewardQuality;

            Identity objectiveIdentity = offer.QuestActions[0].Action;
            offer.QuestActions[0].Action = new Identity
                                           {
                                               Type = objectiveIdentity.Type,
                                               Instance = unchecked(objectiveIdentity.Instance + 1)
                                           };
            Assert.IsFalse(
                MissionOfferCompatibility.TryValidateGenerated(
                    offer,
                    source,
                    sliders,
                    response.MissionTerminalIdentity,
                    out generated,
                    out error));
        }

        [TestMethod]
        public void FinalizedQl42RewardPairsRemainExactEvidence()
        {
            MissionRollSliders neutral = Profile(Request(1, 0, 0, 0, 0, 0, 0));
            MissionRollSliders left = Profile(Request(1, 156, 156, 0, 0, 0, 156));

            Assert.IsTrue(MissionRewardEvidenceModel.IsCapturedPair(
                MissionRollType.FindItemReturn, 60, 1, 42, neutral, 670, 13007, 1808));
            Assert.IsTrue(MissionRewardEvidenceModel.IsCapturedPair(
                MissionRollType.FindItem, 60, 1, 42, neutral, 695, 6537, 2016));
            Assert.IsTrue(MissionRewardEvidenceModel.IsCapturedPair(
                MissionRollType.RepairMachine, 60, 1, 42, neutral, 635, 5627, 2124));
            Assert.IsTrue(MissionRewardEvidenceModel.IsCapturedPair(
                MissionRollType.KillPerson, 60, 1, 42, neutral, 635, 4500, 2155));
            Assert.IsTrue(MissionRewardEvidenceModel.IsCapturedPair(
                MissionRollType.FindPerson, 60, 1, 42, left, 635, 5917, 2002));
        }

        [TestMethod]
        public void UnchangedCapturedCombinationPreservesExactText()
        {
            QuestAlternativeMessage roll = GeneratedMissionWire.Read(GeneratedMissionWire.CapturedBody(0));
            QuestInfo offer = roll.QuestInfos[0];
            string originalTitle = offer.ShortInfo;
            string originalDescription = offer.Info;
            MissionOfferDescriptor descriptor;
            string error;
            Assert.IsTrue(
                MissionOfferCompatibility.TryDescribeCaptured(
                    offer,
                    roll.MissionTerminalIdentity,
                    out descriptor,
                    out error),
                error);

            MissionOfferTextBuilder.Apply(
                offer,
                descriptor,
                MissionOfferTextBuilder.Capture(offer));

            Assert.AreEqual(originalTitle, offer.ShortInfo);
            Assert.AreEqual(originalDescription, offer.Info);
        }

        [TestMethod]
        public void RollFeeRulesPreserveDeductionAndInsufficientCreditBehavior()
        {
            Assert.AreEqual(60, MissionRollPolicy.Current.Fee(60));
            Assert.AreEqual(1, MissionRollPolicy.Current.Fee(0));
            // Credit deduction and insufficient-funds preservation are exercised against the actual
            // DAO by Tools/MissionDaoValidation, not by a duplicate calculator in this test fixture.
        }

        [TestMethod]
        public void LevelFourNeutralIccRollStaysInTheTerminalPlayfield()
        {
            int nextIdentity = 1000;
            QuestAlternativeMessage generated =
                GeneratedMissionRollService.Generate(
                    Request(1, 0, 0, 0, 0, 0, 0),
                    new Identity
                    {
                        Type = IdentityType.CanbeAffected,
                        Instance = 0x12345678
                    },
                    4,
                    655,
                    3238f,
                    918f,
                    MissionLocationSide.Neutral,
                    1201445827,
                    12345,
                    0x24681357,
                    () => ++nextIdentity);

            Assert.AreEqual(5, generated.QuestInfos.Length);
            foreach (QuestInfo offer in generated.QuestInfos)
            {
                Assert.AreEqual(
                    655,
                    offer.QuestActions[0].Playfield.Instance,
                    "A neutral ICC roll must prefer proven same-playfield markers.");
            }
        }

        [TestMethod]
        public void AcgNpcDifficultyReusesStableMissionQualityPolicy()
        {
            var first = new Random(0x12345678);
            var second = new Random(0x12345678);

            int firstLevel = MissionGenerationSettings.Current.Level.Sample(2, first);
            int firstHealth = MissionGenerationSettings.Current.Health.Sample(firstLevel, first);
            int secondLevel = MissionGenerationSettings.Current.Level.Sample(2, second);
            int secondHealth = MissionGenerationSettings.Current.Health.Sample(secondLevel, second);

            Assert.AreEqual(firstLevel, secondLevel);
            Assert.AreEqual(firstHealth, secondHealth);
            Assert.IsTrue(firstLevel >= 1 && firstLevel <= 4);
            Assert.IsTrue(firstHealth >= 50 && firstHealth <= 100);
            Assert.AreNotEqual(38, firstLevel);
            Assert.AreNotEqual(1221, firstHealth);
        }

        private static QuestAlternativeMessage Build(
            QuestAlternativeMessage request,
            int characterLevel,
            int seed)
        {
            int nextIdentity = 1000;
            return GeneratedMissionRollService.Generate(
                request,
                new Identity { Type = IdentityType.CanbeAffected, Instance = 0x12345678 },
                characterLevel,
                710,
                300f,
                300f,
                MissionLocationSide.Omni,
                1201445827,
                seed,
                0x24681357,
                () => ++nextIdentity);
        }

        private static QuestAlternativeMessage Request(
            byte difficulty,
            byte goodBad,
            byte orderChaos,
            byte openHidden,
            byte physicalMystical,
            byte headOnStealth,
            byte moneyExperience)
        {
            return new QuestAlternativeMessage
                   {
                       VersionId = 4,
                       LevelSlider = difficulty,
                       GoodBadSlider = goodBad,
                       OrderChaosSlider = orderChaos,
                       OpenHiddenSlider = openHidden,
                       PhysicalMysticalSlider = physicalMystical,
                       HeadOnStealthSlider = headOnStealth,
                       MoneyExperienceSlider = moneyExperience,
                       MissionTerminalIdentity =
                           new Identity { Type = (IdentityType)0x0000DAC1, Instance = 0x60000001 },
                       QuestInfos = new QuestInfo[0]
                   };
        }

        private static MissionRollSliders Profile(QuestAlternativeMessage request)
        {
            MissionRollSliders profile;
            string error;
            Assert.IsTrue(MissionRollSliders.TryCreate(request, out profile, out error), error);
            return profile;
        }

        private static bool AreEqual(byte[] first, byte[] second)
        {
            if (first.Length != second.Length)
            {
                return false;
            }

            for (int i = 0; i < first.Length; i++)
            {
                if (first[i] != second[i])
                {
                    return false;
                }
            }

            return true;
        }

        private static void AssertRejected(Action action)
        {
            try
            {
                action();
                Assert.Fail("Expected unsupported mission slider input to fail closed.");
            }
            catch (Exception failure) when (failure is ArgumentException || failure is InvalidOperationException)
            {
            }
        }

        private sealed class MalisNanoEntry
        {
            public MalisNanoKey Key { get; set; }
        }

        private sealed class MalisNanoKey
        {
            public int LowId { get; set; }

            public int HighId { get; set; }

            public int LowQl { get; set; }

            public int HighQl { get; set; }
        }
    }
}
