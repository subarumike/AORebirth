using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Web.Script.Serialization;

namespace AORebirth.MissionEvidence
{
    internal static class Program
    {
        private static int _assertions;

        private static int Main()
        {
            try
            {
                TestLevelTwoDetents();
                TestSecondaryParsing();
                TestPresetsAndStateIds();
                TestLowLevelMatrix();
                TestMissionItemCampaignRoster();
                TestMissionItemCampaignResume();
                TestFailClosedGate();
                Console.WriteLine("MISSION_OFFER_HARVESTER_OFFLINE_TESTS=PASS assertions=" + _assertions);
                return 0;
            }
            catch (Exception exception)
            {
                Console.Error.WriteLine("MISSION_OFFER_HARVESTER_OFFLINE_TESTS=FAIL " + exception.Message);
                return 1;
            }
        }

        private static void TestLevelTwoDetents()
        {
            int ql;
            Assert(MissionQlResolver.TryGetMissionQl(2, 1, out ql) && ql == 1, "level 2 detent 1 must resolve QL1");
            Assert(MissionQlResolver.TryGetMissionQl(2, 6, out ql) && ql == 2, "level 2 detent 6 must resolve QL2");
            Assert(MissionQlResolver.TryGetMissionQl(2, 10, out ql) && ql == 3, "level 2 detent 10 must resolve QL3");
            Assert(!MissionQlResolver.TryGetMissionQl(2, 0, out ql), "detent 0 must fail");
            Assert(!MissionQlResolver.TryGetMissionQl(2, 12, out ql), "detent 12 must fail");
        }

        private static void TestSecondaryParsing()
        {
            SecondarySliderSetting setting;
            string error;
            Assert(SecondarySliderSetting.TryParse("FULL_LEFT", out setting, out error) && setting.RawValue == 156, "full left native byte");
            Assert(SecondarySliderSetting.TryParse("CENTER", out setting, out error) && setting.RawValue == 255, "center native byte");
            Assert(SecondarySliderSetting.TryParse("FULL_RIGHT", out setting, out error) && setting.RawValue == 100, "full right native byte");
            Assert(SecondarySliderSetting.TryParse("-37", out setting, out error) && setting.RawValue == 219, "signed negative native byte");
            Assert(SecondarySliderSetting.TryParse("raw:0", out setting, out error) && setting.RawValue == 0, "raw lower endpoint");
            Assert(SecondarySliderSetting.TryParse("raw:255", out setting, out error) && setting.RawValue == 255, "raw upper endpoint");
            Assert(!SecondarySliderSetting.TryParse("-101", out setting, out error), "semantic below range must fail");
            Assert(!SecondarySliderSetting.TryParse("101", out setting, out error), "semantic above range must fail");
            Assert(!SecondarySliderSetting.TryParse("raw:256", out setting, out error), "raw above byte range must fail");
        }

        private static void TestPresetsAndStateIds()
        {
            string[] names =
            {
                "CENTERED_BASELINE",
                "FIND_ITEM_HEAVY",
                "GOOD_BAD_FULL_LEFT", "GOOD_BAD_FULL_RIGHT",
                "ORDER_CHAOS_FULL_LEFT", "ORDER_CHAOS_FULL_RIGHT",
                "OPEN_HIDDEN_FULL_LEFT", "OPEN_HIDDEN_FULL_RIGHT",
                "PHYSICAL_MYSTICAL_FULL_LEFT", "PHYSICAL_MYSTICAL_FULL_RIGHT",
                "HEADON_STEALTH_FULL_LEFT", "HEADON_STEALTH_FULL_RIGHT",
                "MONEY_XP_FULL_LEFT", "MONEY_XP_FULL_RIGHT"
            };
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (string name in names)
            {
                MissionSliderState state;
                string error;
                Assert(MissionSliderState.TryCreatePreset(1, name, out state, out error), "preset must resolve: " + name);
                Assert(ids.Add(state.SliderStateId), "preset state id must be unique: " + name);
                NativeMissionSliderValues native = state.ToNativeValues();
                int nonCenter = (native.GoodBad == 255 ? 0 : 1)
                    + (native.OrderChaos == 255 ? 0 : 1)
                    + (native.OpenHidden == 255 ? 0 : 1)
                    + (native.PhysicalMystical == 255 ? 0 : 1)
                    + (native.HeadonStealth == 255 ? 0 : 1)
                    + (native.CreditsXp == 255 ? 0 : 1);
                int expectedNonCenter = name == "CENTERED_BASELINE" ? 0 : name == "FIND_ITEM_HEAVY" ? 2 : 1;
                Assert(nonCenter == expectedNonCenter, "preset must set the expected number of secondary sliders: " + name);
            }
            MissionSliderState first;
            MissionSliderState second;
            string ignored;
            MissionSliderState.TryCreatePreset(1, "CENTERED_BASELINE", out first, out ignored);
            MissionSliderState.TryCreatePreset(1, "centered_baseline", out second, out ignored);
            Assert(first.SliderStateId == second.SliderStateId, "state id must be deterministic");
            Assert(first.RequestedSemanticPayload().ContainsKey("money_xp"), "semantic payload must retain money/xp");
            byte[] raw = first.ToNativeValues().ToRawByteArray();
            Assert(raw.Length == 7, "raw slider serialization must contain seven bytes");
            Assert(raw[0] == 1 && raw[1] == 255 && raw[2] == 255 && raw[3] == 255
                && raw[4] == 255 && raw[5] == 255 && raw[6] == 255,
                "raw slider serialization must preserve native order and values");
            MissionSliderState findItemHeavy;
            Assert(MissionSliderState.TryCreatePreset(1, "FIND_ITEM_HEAVY", out findItemHeavy, out ignored), "find-item-heavy preset must resolve");
            NativeMissionSliderValues findItemHeavyNative = findItemHeavy.ToNativeValues();
            Assert(findItemHeavyNative.GoodBad == 100 && findItemHeavyNative.CreditsXp == 156,
                "find-item-heavy preset must use full Bad and full Credits");
            Assert(findItemHeavyNative.OrderChaos == 255 && findItemHeavyNative.OpenHidden == 255
                && findItemHeavyNative.PhysicalMystical == 255 && findItemHeavyNative.HeadonStealth == 255,
                "find-item-heavy preset must center the other secondary sliders");
            MissionSliderState invalid;
            Assert(!MissionSliderState.TryCreatePreset(1, "NOT_A_PRESET", out invalid, out ignored), "unknown preset must fail");
        }

        private static void TestFailClosedGate()
        {
            MissionSliderState state;
            string error;
            MissionSliderState.TryCreatePreset(1, "CENTERED_BASELINE", out state, out error);
            NativeMissionSliderValues expected = state.ToNativeValues();

            SliderRequestGate missingApi = new SliderRequestGate("r1", state);
            Assert(!missingApi.TryApplyNative(expected, false, out error) && error == "SLIDER_API_UNAVAILABLE", "missing slider API must fail closed");

            SliderRequestGate wrongOrder = new SliderRequestGate("r2", state);
            Assert(!wrongOrder.TryVerifySerialized(expected, out error) && error == "SLIDER_APPLICATION_ORDER_VIOLATION", "application order must be enforced");

            NativeMissionSliderValues mismatch = new NativeMissionSliderValues(1, 100, 255, 255, 255, 255, 255);
            SliderRequestGate nativeMismatch = new SliderRequestGate("r3", state);
            Assert(!nativeMismatch.TryApplyNative(mismatch, true, out error) && error == "NATIVE_READBACK_MISMATCH", "native mismatch must fail");

            SliderRequestGate serializedMismatch = new SliderRequestGate("r4", state);
            Assert(serializedMismatch.TryApplyNative(expected, true, out error), "native match");
            Assert(!serializedMismatch.TryVerifySerialized(mismatch, out error) && error == "SERIALIZED_REQUEST_SLIDER_MISMATCH", "serialized mismatch must fail");

            SliderRequestGate happy = new SliderRequestGate("r5", state);
            Assert(happy.TryApplyNative(expected, true, out error), "happy native apply");
            Assert(happy.TryVerifySerialized(expected, out error), "happy serialized verification");
            Assert(happy.TryMarkTransmitted("r5", expected, "abc", "abc", out error), "happy transmitted verification");
            Assert(happy.TryVerifyResponse(expected, out error), "happy returned verification");
            Assert(happy.TryAssociateCohort("r5", state.SliderStateId, out error), "happy cohort association");
            Assert(happy.Phase == "COHORT_ASSOCIATED", "happy phase complete");

            SliderRequestGate wrongRequest = PreparedGate("r6", state, expected);
            Assert(!wrongRequest.TryMarkTransmitted("other", expected, "abc", "abc", out error) && error == "RAW_REQUEST_ASSOCIATION_MISMATCH", "request association must match");

            SliderRequestGate wrongHash = PreparedGate("r7", state, expected);
            Assert(!wrongHash.TryMarkTransmitted("r7", expected, "abc", "def", out error) && error == "TRANSMITTED_REQUEST_PACKET_HASH_MISMATCH", "packet hash must match");

            SliderRequestGate wrongTransmitted = PreparedGate("r7b", state, expected);
            Assert(!wrongTransmitted.TryMarkTransmitted("r7b", mismatch, "abc", "abc", out error) && error == "TRANSMITTED_REQUEST_SLIDER_MISMATCH", "transmitted sliders must match");

            SliderRequestGate wrongResponse = PreparedGate("r8", state, expected);
            Assert(wrongResponse.TryMarkTransmitted("r8", expected, "abc", "abc", out error), "prepare response mismatch");
            Assert(!wrongResponse.TryVerifyResponse(mismatch, out error) && error == "RETURNED_RESPONSE_SLIDER_MISMATCH", "response slider mismatch must fail");

            SliderRequestGate wrongCohort = PreparedGate("r9", state, expected);
            Assert(wrongCohort.TryMarkTransmitted("r9", expected, "abc", "abc", out error), "prepare cohort mismatch transmit");
            Assert(wrongCohort.TryVerifyResponse(expected, out error), "prepare cohort mismatch response");
            Assert(!wrongCohort.TryAssociateCohort("r9", "wrong-state", out error) && error == "COHORT_ASSOCIATION_MISMATCH", "cohort slider state must match");
        }

        private static void TestLowLevelMatrix()
        {
            IList<MissionSliderPlanEntry> plan;
            string error;
            Assert(LowLevelSliderMatrix.TryBuild(1, 27, out plan, out error), "complete matrix must build");
            Assert(plan.Count == 27, "complete matrix must contain 27 states");
            Assert(plan[0].MatrixIndex == 1 && plan[0].Label == "CENTERED_BASELINE_D1", "matrix first state");
            Assert(plan[7].MatrixIndex == 8 && plan[7].Label == "ORDER_CHAOS_MINUS_50", "resumable state 8");
            Assert(plan[7].SliderState.ToNativeValues().OrderChaos == 206, "state 8 order/chaos raw -50");
            Assert(plan[8].SliderState.ToNativeValues().OrderChaos == 50, "state 9 order/chaos raw +50");
            Assert(plan[25].SliderState.DifficultyDetent == 6, "QL2 bridge detent");
            Assert(plan[26].SliderState.DifficultyDetent == 10, "QL3 bridge detent");
            var stateIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (MissionSliderPlanEntry entry in plan)
                Assert(stateIds.Add(entry.SliderState.SliderStateId), "matrix state IDs must be unique: " + entry.Label);
            Assert(LowLevelSliderMatrix.TryBuild(8, 27, out plan, out error) && plan.Count == 20, "matrix resume range 8-27");
            Assert(plan[0].MatrixIndex == 8 && plan[19].MatrixIndex == 27, "matrix resume boundaries");
            Assert(!LowLevelSliderMatrix.TryBuild(0, 27, out plan, out error), "matrix start below range must fail");
            Assert(!LowLevelSliderMatrix.TryBuild(8, 28, out plan, out error), "matrix end above range must fail");
            Assert(!LowLevelSliderMatrix.TryBuild(10, 9, out plan, out error), "matrix reversed range must fail");
        }

        private static void TestMissionItemCampaignRoster()
        {
            foreach (int level in new[] { 2, 7, 46, 199 })
            {
                MissionItemCampaignDefinition definition;
                string error;
                Assert(MissionItemCampaignDefinition.TryBuild(level, 10, out definition, out error), "spectrum capture must accept observed level: " + level);
                Assert(definition.DifficultyStates.Count == 11, "all eleven difficulty positions must be scheduled");
                for (int index = 0; index < definition.DifficultyStates.Count; index++)
                {
                    MissionItemQlCohort cohort = definition.DifficultyStates[index];
                    Assert(cohort.DifficultyDetent == index + 1, "difficulty detents must remain ordered");
                    NativeMissionSliderValues native = cohort.SliderState.ToNativeValues();
                    Assert(native.Difficulty == cohort.DifficultyDetent, "difficulty detent must be explicit");
                    Assert(native.GoodBad == 100 && native.OrderChaos == 255 && native.OpenHidden == 255
                        && native.PhysicalMystical == 255 && native.HeadonStealth == 255 && native.CreditsXp == 156,
                        "campaign must use full Bad and full Credits with the other sliders centered");
                }
            }

            MissionItemCampaignDefinition ten;
            MissionItemCampaignDefinition twenty;
            string manifestError;
            Assert(MissionItemCampaignDefinition.TryBuild(2, 10, out ten, out manifestError), "first deterministic manifest");
            Assert(MissionItemCampaignDefinition.TryBuild(2, 20, out twenty, out manifestError), "second deterministic manifest");
            Assert(ten.ManifestSha256 == twenty.ManifestSha256, "manifest identity must not change when extending request target");
            Assert(!MissionItemCampaignDefinition.TryBuild(2, 0, out ten, out manifestError), "zero request target must fail");
        }

        private static void TestMissionItemCampaignResume()
        {
            MissionItemCampaignDefinition definition;
            string error;
            Assert(MissionItemCampaignDefinition.TryBuild(2, 5, out definition, out error), "resume definition");
            string path = Path.Combine(Path.GetTempPath(), "aorebirth-mission-campaign-" + Guid.NewGuid().ToString("N") + ".jsonl");
            try
            {
                MissionItemCampaignProgress progress;
                Assert(MissionItemCampaignProgress.TryLoad(path, definition, 12345, out progress, out error), "missing progress file starts empty");
                Assert(progress.NextIncompleteCohort().DifficultyDetent == 1, "capture starts at first difficulty position");
                int[] actualQls = { 1, 1, 1, 1, 1, 2, 2, 2, 2, 3, 3 };
                for (int detent = 1; detent <= 11; detent++)
                {
                    Assert(progress.TryRecordMapping(detent, actualQls[detent - 1], out error), "difficulty mapping accepted");
                    Assert(progress.TryRecordCompletion(actualQls[detent - 1], "completion-" + detent, out error), "discovery request counts as captured evidence");
                }
                MissionItemQlCohort next = progress.NextIncompleteCohort();
                Assert(!next.IsDifficultyDiscovery && next.MissionQl == 2, "duplicate QLs are deduplicated after all positions are observed");

                var serializer = new JavaScriptSerializer();
                var payload = new Dictionary<string, object>
                {
                    ["campaign_manifest_sha256"] = definition.ManifestSha256,
                    ["character_level"] = 2,
                    ["character_identity_instance"] = 12345,
                    ["difficulty_detent"] = 1,
                    ["actual_mission_ql"] = 1,
                    ["required_requests_per_ql"] = 5,
                    ["offer_count"] = 5,
                    ["verification_status"] = "VERIFIED_COMPLETE_FIVE_OFFER_COHORT",
                    ["completion_id"] = "persisted-a"
                };
                string mappingLine = serializer.Serialize(new Dictionary<string, object> { ["event_type"] = "difficulty_mapping_verified", ["payload"] = payload });
                string completionLine = serializer.Serialize(new Dictionary<string, object> { ["event_type"] = "campaign_request_completed", ["payload"] = payload });
                File.WriteAllText(path, mappingLine + Environment.NewLine + completionLine + Environment.NewLine);
                Assert(MissionItemCampaignProgress.TryLoad(path, definition, 12345, out progress, out error), "persisted progress must load");
                Assert(progress.CompletedRequestCount(1) == 1, "persisted completion must count once");
                Assert(progress.CompletedDifficultyCount == 1, "persisted difficulty mapping must resume");
            }
            finally
            {
                if (File.Exists(path))
                    File.Delete(path);
            }
        }

        private static SliderRequestGate PreparedGate(string requestId, MissionSliderState state, NativeMissionSliderValues expected)
        {
            string error;
            var gate = new SliderRequestGate(requestId, state);
            Assert(gate.TryApplyNative(expected, true, out error), "prepared native");
            Assert(gate.TryVerifySerialized(expected, out error), "prepared serialized");
            return gate;
        }

        private static void Assert(bool condition, string message)
        {
            _assertions++;
            if (!condition)
                throw new InvalidOperationException(message);
        }
    }
}
