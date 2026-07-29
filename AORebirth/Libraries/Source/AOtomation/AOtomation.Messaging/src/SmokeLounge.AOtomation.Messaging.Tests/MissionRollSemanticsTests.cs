namespace SmokeLounge.AOtomation.Messaging.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;

    using Microsoft.VisualStudio.TestTools.UnitTesting;

    using SmokeLounge.AOtomation.Messaging.GameData;
    using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;

    using ZoneEngine.Core.Missions;

    [TestClass]
    [DeploymentItem(@".\XML Data\MissionLevels.csv", @"XML Data")]
    [DeploymentItem(@".\XML Data\MissionRewards", @"XML Data\MissionRewards")]
    public class MissionRollSemanticsTests
    {
        [TestMethod]
        public void OfficialFindAndReturnItemIconsMapToCapturedBehavior()
        {
            Assert.AreEqual(MissionRollType.FindItemReturn, MissionTypeCatalog.TypeFromIcon(11329));
            Assert.AreEqual(MissionRollType.FindItem, MissionTypeCatalog.TypeFromIcon(11337));
            Assert.AreEqual(11329, MissionTypeCatalog.IconId(MissionRollType.FindItemReturn, 0));
            Assert.AreEqual(11337, MissionTypeCatalog.IconId(MissionRollType.FindItem, 0));
            Assert.AreEqual(MissionRollType.Unknown, MissionTypeCatalog.TypeFromIcon(999999));
        }

        [TestMethod]
        public void DifficultyWireValuesAreOneBasedAndLevel60EasiestIsQl42()
        {
            int sliderIndex;
            Assert.IsTrue(MissionLevelTable.TryDecodeDifficultySlider(1, out sliderIndex));
            Assert.AreEqual(0, sliderIndex);
            Assert.IsTrue(MissionLevelTable.TryDecodeDifficultySlider(11, out sliderIndex));
            Assert.AreEqual(10, sliderIndex);
            Assert.IsFalse(MissionLevelTable.TryDecodeDifficultySlider(0, out sliderIndex));
            Assert.IsFalse(MissionLevelTable.TryDecodeDifficultySlider(12, out sliderIndex));

            int missionQuality;
            Assert.IsTrue(MissionLevelTable.TryGetMissionQuality(60, 1, out missionQuality));
            Assert.AreEqual(42, missionQuality);
            Assert.IsTrue(MissionLevelTable.TryGetMissionQuality(220, 11, out missionQuality));
            Assert.AreEqual(250, missionQuality);
            Assert.IsFalse(MissionLevelTable.TryGetMissionQuality(60, 0, out missionQuality));
            Assert.IsFalse(MissionLevelTable.TryGetMissionQuality(60, 12, out missionQuality));

            Assert.AreEqual(1, MissionLevelTable.ClampCharacterLevel(0));
            Assert.AreEqual(220, MissionLevelTable.ClampCharacterLevel(221));
        }

        [TestMethod]
        public void GeneratedRollUsesTheClampedCharacterLevelConsistently()
        {
            QuestAlternativeMessage request = Request(1, 0, 0, 0, 0, 0, 0);
            CollectionAssert.AreEqual(
                MissionRollService.SerializeBody(Build(request, 1, 707)),
                MissionRollService.SerializeBody(Build(request, 0, 707)));
            CollectionAssert.AreEqual(
                MissionRollService.SerializeBody(Build(request, 220, 808)),
                MissionRollService.SerializeBody(Build(request, 221, 808)));
        }

        [TestMethod]
        public void ContinuousSliderBytesDecodeAsSignedPercentAndRejectInvalidRange()
        {
            int value;
            Assert.IsTrue(MissionSliderProfile.TryDecodeSignedPercent(156, out value));
            Assert.AreEqual(-100, value);
            Assert.IsTrue(MissionSliderProfile.TryDecodeSignedPercent(0, out value));
            Assert.AreEqual(0, value);
            Assert.IsTrue(MissionSliderProfile.TryDecodeSignedPercent(100, out value));
            Assert.AreEqual(100, value);
            Assert.IsFalse(MissionSliderProfile.TryDecodeSignedPercent(101, out value));
            Assert.IsFalse(MissionSliderProfile.TryDecodeSignedPercent(155, out value));
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
            MissionSliderProfile neutral = Profile(Request(1, 0, 0, 0, 0, 0, 0));
            MissionSliderProfile capturedLeft = Profile(Request(1, 156, 156, 0, 0, 0, 156));
            MissionSliderProfile unresolved = Profile(Request(1, 25, 231, 100, 156, 50, 25));

            Assert.AreEqual(MissionSliderEvidenceProfile.Neutral, neutral.EvidenceProfile);
            Assert.AreEqual(
                MissionSliderEvidenceProfile.CapturedLeftGoodBadOrderChaosCreditsXp,
                capturedLeft.EvidenceProfile);
            Assert.AreEqual(MissionSliderEvidenceProfile.Unresolved, unresolved.EvidenceProfile);
            Assert.AreEqual(0, unresolved.SemanticDistance(0, 0, 0, 0, 0, 0));
            Assert.AreEqual(1, unresolved.SemanticDistance(-100, -100, 0, 0, 0, -100));
            Assert.AreEqual(0, capturedLeft.SemanticDistance(-100, -100, 0, 0, 0, -100));
            Assert.AreEqual(1, capturedLeft.SemanticDistance(0, 0, 0, 0, 0, 0));
        }

        [TestMethod]
        public void CapturedLibraryCoversEveryFinalizedMissionTypeWithCompatibleActions()
        {
            var types = new HashSet<MissionRollType>();
            for (int rollIndex = 0; rollIndex < MissionRollService.CapturedRollCount; rollIndex++)
            {
                QuestAlternativeMessage roll = MissionRollService.DecodeCapturedRoll(rollIndex);
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
                MissionRollType type = MissionTypeCatalog.TypeFromIcon(offer.MissionIconId);
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
                        MissionLevelTable.TryGetMissionQuality(
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
            byte[] first = MissionRollService.SerializeBody(Build(request, 60, 12345));
            byte[] second = MissionRollService.SerializeBody(Build(request, 60, 12345));
            CollectionAssert.AreEqual(first, second);

            byte[] different = MissionRollService.SerializeBody(Build(request, 60, 54321));
            Assert.IsFalse(AreEqual(first, different), "Different seeds should be able to select a different valid roll.");
        }

        [TestMethod]
        public void GeneratedRollsDoNotMutateCapturedBodies()
        {
            var before = new byte[MissionRollService.CapturedRollCount][];
            for (int i = 0; i < before.Length; i++)
            {
                before[i] = MissionRollService.CapturedRollBody(i);
            }

            for (int seed = 0; seed < 16; seed++)
            {
                Build(Request(1, 0, 0, 0, 0, 0, 0), 60, seed);
            }

            for (int i = 0; i < before.Length; i++)
            {
                CollectionAssert.AreEqual(before[i], MissionRollService.CapturedRollBody(i), "roll " + i);
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

                MissionSliderProfile sliders;
                string sliderError;
                Assert.IsTrue(MissionSliderProfile.TryCreate(request, out sliders, out sliderError));
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
                    Assert.AreEqual(MissionTypeCatalog.IconId(descriptor.Type, 0), offer.MissionIconId);
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
                    if (MissionTypeCatalog.TypeFromIcon(offer.MissionIconId)
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
            MissionSliderProfile sliders = Profile(request);
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
            MissionSliderProfile neutral = Profile(Request(1, 0, 0, 0, 0, 0, 0));
            MissionSliderProfile left = Profile(Request(1, 156, 156, 0, 0, 0, 156));

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
            QuestAlternativeMessage roll = MissionRollService.DecodeCapturedRoll(0);
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
            int fee;
            int cashAfter;
            Assert.IsFalse(MissionRollFeeRules.TryCalculateCharge(60, 59, out fee, out cashAfter));
            Assert.AreEqual(60, fee);
            Assert.AreEqual(59, cashAfter);

            Assert.IsTrue(MissionRollFeeRules.TryCalculateCharge(60, 60, out fee, out cashAfter));
            Assert.AreEqual(60, fee);
            Assert.AreEqual(0, cashAfter);
            Assert.AreEqual(1, MissionRollFeeRules.FeeForLevel(0));
        }

        private static QuestAlternativeMessage Build(
            QuestAlternativeMessage request,
            int characterLevel,
            int seed)
        {
            return MissionRollService.BuildRollResponseDeterministic(
                request,
                new Identity { Type = IdentityType.CanbeAffected, Instance = 0x12345678 },
                characterLevel,
                710,
                300f,
                300f,
                MissionLocationSide.Omni,
                seed,
                0x24681357,
                0x55660000,
                1201445827);
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

        private static MissionSliderProfile Profile(QuestAlternativeMessage request)
        {
            MissionSliderProfile profile;
            string error;
            Assert.IsTrue(MissionSliderProfile.TryCreate(request, out profile, out error), error);
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
            catch (ArgumentOutOfRangeException)
            {
            }
        }
    }
}
