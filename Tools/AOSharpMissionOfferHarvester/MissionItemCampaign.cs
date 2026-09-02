using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Web.Script.Serialization;

namespace AORebirth.MissionEvidence
{
    internal sealed class MissionItemQlCohort
    {
        internal int CohortIndex { get; private set; }
        internal int CharacterLevel { get; private set; }
        internal int MissionQl { get; private set; }
        internal int StaticExpectedMissionQl { get; private set; }
        internal int DifficultyDetent { get; private set; }
        internal bool IsDifficultyDiscovery { get; private set; }
        internal MissionSliderState SliderState { get; private set; }

        internal MissionItemQlCohort(
            int cohortIndex,
            int characterLevel,
            int missionQl,
            int staticExpectedMissionQl,
            int difficultyDetent,
            bool isDifficultyDiscovery,
            MissionSliderState sliderState)
        {
            CohortIndex = cohortIndex;
            CharacterLevel = characterLevel;
            MissionQl = missionQl;
            StaticExpectedMissionQl = staticExpectedMissionQl;
            DifficultyDetent = difficultyDetent;
            IsDifficultyDiscovery = isDifficultyDiscovery;
            SliderState = sliderState;
        }
    }

    internal sealed class MissionItemCampaignDefinition
    {
        internal const int SchemaVersion = 2;
        internal const string CampaignName = "MISSION_QL_SPECTRUM_V2";

        internal int CharacterLevel { get; private set; }
        internal int RequiredRequestsPerQl { get; private set; }
        internal IList<MissionItemQlCohort> DifficultyStates { get; private set; }
        internal string ManifestSha256 { get; private set; }

        private MissionItemCampaignDefinition(
            int characterLevel,
            int requiredRequestsPerQl,
            IList<MissionItemQlCohort> difficultyStates)
        {
            CharacterLevel = characterLevel;
            RequiredRequestsPerQl = requiredRequestsPerQl;
            DifficultyStates = difficultyStates;
            ManifestSha256 = BuildManifestSha256(characterLevel, difficultyStates);
        }

        internal static bool TryBuild(
            int characterLevel,
            int requiredRequestsPerQl,
            out MissionItemCampaignDefinition definition,
            out string error)
        {
            definition = null;
            error = null;
            if (requiredRequestsPerQl < 1 || requiredRequestsPerQl > 100000)
            {
                error = "REQUESTS_PER_QL_OUT_OF_RANGE_1_TO_100000";
                return false;
            }
            var states = new List<MissionItemQlCohort>();
            for (int detent = 1; detent <= MissionQlResolver.DifficultyCount; detent++)
            {
                int staticQl;
                if (!MissionQlResolver.TryGetMissionQl(characterLevel, detent, out staticQl))
                {
                    error = "CHARACTER_LEVEL_NOT_SUPPORTED_BY_MISSION_QL_TABLE";
                    return false;
                }
                MissionSliderState centered;
                string sliderError;
                if (!MissionSliderState.TryCreatePreset(detent, "CENTERED_BASELINE", out centered, out sliderError))
                {
                    error = sliderError;
                    return false;
                }
                states.Add(new MissionItemQlCohort(detent, characterLevel, staticQl, staticQl, detent, true, centered));
            }
            definition = new MissionItemCampaignDefinition(characterLevel, requiredRequestsPerQl, states);
            return true;
        }

        internal int PlannedQlCohortCount
        {
            get
            {
                var qls = new HashSet<int>();
                foreach (MissionItemQlCohort state in DifficultyStates)
                    qls.Add(state.StaticExpectedMissionQl);
                return qls.Count;
            }
        }

        internal IDictionary<string, object> ToPayload()
        {
            var states = new List<object>();
            foreach (MissionItemQlCohort state in DifficultyStates)
            {
                states.Add(new Dictionary<string, object>
                {
                    ["difficulty_position"] = state.DifficultyDetent,
                    ["static_expected_mission_ql"] = state.StaticExpectedMissionQl,
                    ["slider_state_id"] = state.SliderState.SliderStateId,
                    ["native_slider_values"] = state.SliderState.ToNativeValues().ToPayload()
                });
            }
            return new Dictionary<string, object>
            {
                ["campaign_name"] = CampaignName,
                ["campaign_schema_version"] = SchemaVersion,
                ["campaign_manifest_sha256"] = ManifestSha256,
                ["observed_character_level"] = CharacterLevel,
                ["difficulty_position_count"] = MissionQlResolver.DifficultyCount,
                ["static_distinct_ql_count"] = PlannedQlCohortCount,
                ["required_requests_per_actual_ql"] = RequiredRequestsPerQl,
                ["semantic_state_count"] = 1,
                ["semantic_state"] = "CENTERED_ONLY",
                ["difficulty_states"] = states
            };
        }

        private static string BuildManifestSha256(int characterLevel, IList<MissionItemQlCohort> states)
        {
            var canonical = new StringBuilder(CampaignName).Append('|').Append(characterLevel);
            foreach (MissionItemQlCohort state in states)
                canonical.Append('|').Append(state.DifficultyDetent).Append(':').Append(state.StaticExpectedMissionQl);
            canonical.Append("|semantic=CENTERED_ONLY");
            using (SHA256 hash = SHA256.Create())
            {
                return BitConverter.ToString(hash.ComputeHash(Encoding.UTF8.GetBytes(canonical.ToString())))
                    .Replace("-", "")
                    .ToLowerInvariant();
            }
        }
    }

    internal sealed class MissionItemCampaignProgress
    {
        private readonly MissionItemCampaignDefinition _definition;
        private readonly int _characterIdentityInstance;
        private readonly IDictionary<int, int> _actualQlByDetent = new Dictionary<int, int>();
        private readonly IDictionary<int, HashSet<string>> _completionIdsByQl = new Dictionary<int, HashSet<string>>();

        private MissionItemCampaignProgress(MissionItemCampaignDefinition definition, int characterIdentityInstance)
        {
            _definition = definition;
            _characterIdentityInstance = characterIdentityInstance;
        }

        internal static bool TryLoad(
            string path,
            MissionItemCampaignDefinition definition,
            int characterIdentityInstance,
            out MissionItemCampaignProgress progress,
            out string error)
        {
            progress = new MissionItemCampaignProgress(definition, characterIdentityInstance);
            error = null;
            if (!File.Exists(path))
                return true;
            var serializer = new JavaScriptSerializer { MaxJsonLength = int.MaxValue };
            int lineNumber = 0;
            try
            {
                foreach (string line in File.ReadLines(path))
                {
                    lineNumber++;
                    if (string.IsNullOrWhiteSpace(line))
                        continue;
                    var record = serializer.Deserialize<Dictionary<string, object>>(line);
                    string eventType = Convert.ToString(record["event_type"], CultureInfo.InvariantCulture);
                    if (eventType != "difficulty_mapping_verified" && eventType != "campaign_request_completed")
                        continue;
                    var payload = record["payload"] as Dictionary<string, object>;
                    if (payload == null
                        || Convert.ToString(payload["campaign_manifest_sha256"], CultureInfo.InvariantCulture) != definition.ManifestSha256
                        || Convert.ToInt32(payload["character_level"], CultureInfo.InvariantCulture) != definition.CharacterLevel
                        || Convert.ToInt32(payload["character_identity_instance"], CultureInfo.InvariantCulture) != characterIdentityInstance)
                    {
                        error = "CAMPAIGN_PROGRESS_CONTRACT_MISMATCH_AT_LINE_" + lineNumber;
                        progress = null;
                        return false;
                    }
                    int detent = Convert.ToInt32(payload["difficulty_detent"], CultureInfo.InvariantCulture);
                    int actualQl = Convert.ToInt32(payload["actual_mission_ql"], CultureInfo.InvariantCulture);
                    if (!progress.TryRecordMapping(detent, actualQl, out error))
                    {
                        error += "_AT_LINE_" + lineNumber;
                        progress = null;
                        return false;
                    }
                    if (eventType == "campaign_request_completed")
                    {
                        if (Convert.ToInt32(payload["offer_count"], CultureInfo.InvariantCulture) != 5
                            || Convert.ToString(payload["verification_status"], CultureInfo.InvariantCulture) != "VERIFIED_COMPLETE_FIVE_OFFER_COHORT"
                            || !progress.TryRecordCompletion(actualQl, Convert.ToString(payload["completion_id"], CultureInfo.InvariantCulture), out error))
                        {
                            error = (error ?? "CAMPAIGN_COMPLETION_CONTRACT_MISMATCH") + "_AT_LINE_" + lineNumber;
                            progress = null;
                            return false;
                        }
                    }
                }
            }
            catch (Exception exception)
            {
                error = "CAMPAIGN_PROGRESS_READ_FAILED: " + exception.GetType().FullName + ": " + exception.Message;
                progress = null;
                return false;
            }
            return true;
        }

        internal MissionItemQlCohort NextIncompleteCohort()
        {
            foreach (MissionItemQlCohort state in _definition.DifficultyStates)
            {
                if (!_actualQlByDetent.ContainsKey(state.DifficultyDetent))
                    return state;
            }
            var seen = new HashSet<int>();
            int cohortIndex = 0;
            foreach (MissionItemQlCohort state in _definition.DifficultyStates)
            {
                int actualQl = _actualQlByDetent[state.DifficultyDetent];
                if (!seen.Add(actualQl))
                    continue;
                cohortIndex++;
                if (CompletedRequestCount(actualQl) >= _definition.RequiredRequestsPerQl)
                    continue;
                MissionSliderState centered;
                string error;
                if (!MissionSliderState.TryCreatePreset(state.DifficultyDetent, "CENTERED_BASELINE", out centered, out error))
                    throw new InvalidOperationException(error);
                return new MissionItemQlCohort(
                    cohortIndex,
                    _definition.CharacterLevel,
                    actualQl,
                    state.StaticExpectedMissionQl,
                    state.DifficultyDetent,
                    false,
                    centered);
            }
            return null;
        }

        internal bool TryRecordMapping(int detent, int actualQl, out string error)
        {
            error = null;
            if (detent < 1 || detent > MissionQlResolver.DifficultyCount || actualQl < 1 || actualQl > 250)
            {
                error = "DIFFICULTY_MAPPING_OUT_OF_RANGE";
                return false;
            }
            int existing;
            if (_actualQlByDetent.TryGetValue(detent, out existing))
            {
                if (existing != actualQl)
                {
                    error = "DIFFICULTY_MAPPING_CHANGED";
                    return false;
                }
                return true;
            }
            _actualQlByDetent[detent] = actualQl;
            if (!_completionIdsByQl.ContainsKey(actualQl))
                _completionIdsByQl[actualQl] = new HashSet<string>(StringComparer.Ordinal);
            return true;
        }

        internal bool TryRecordCompletion(int actualQl, string completionId, out string error)
        {
            error = null;
            if (!_completionIdsByQl.ContainsKey(actualQl) || string.IsNullOrEmpty(completionId))
            {
                error = "CAMPAIGN_COMPLETION_WITHOUT_VERIFIED_MAPPING";
                return false;
            }
            if (!_completionIdsByQl[actualQl].Add(completionId))
            {
                error = "CAMPAIGN_COMPLETION_ID_DUPLICATE";
                return false;
            }
            return true;
        }

        internal int CompletedRequestCount(int actualQl)
        {
            HashSet<string> ids;
            return _completionIdsByQl.TryGetValue(actualQl, out ids) ? ids.Count : 0;
        }

        internal int CompletedDifficultyCount { get { return _actualQlByDetent.Count; } }

        internal int DistinctActualQlCount { get { return _completionIdsByQl.Count; } }

        internal int TotalCompletedRequestCount
        {
            get
            {
                int total = 0;
                foreach (HashSet<string> ids in _completionIdsByQl.Values)
                    total += ids.Count;
                return total;
            }
        }

        internal int RemainingRequestCount
        {
            get
            {
                int remaining = MissionQlResolver.DifficultyCount - CompletedDifficultyCount;
                foreach (HashSet<string> ids in _completionIdsByQl.Values)
                    remaining += Math.Max(0, _definition.RequiredRequestsPerQl - ids.Count);
                return remaining;
            }
        }

        internal int MaximumRemainingRequestCount
        {
            get { return (MissionQlResolver.DifficultyCount - CompletedDifficultyCount) + (MissionQlResolver.DifficultyCount * _definition.RequiredRequestsPerQl); }
        }

        internal bool IsComplete { get { return CompletedDifficultyCount == MissionQlResolver.DifficultyCount && NextIncompleteCohort() == null; } }

        internal IDictionary<string, object> MappingPayload()
        {
            var states = new List<object>();
            foreach (MissionItemQlCohort state in _definition.DifficultyStates)
            {
                int actualQl;
                bool observed = _actualQlByDetent.TryGetValue(state.DifficultyDetent, out actualQl);
                states.Add(new Dictionary<string, object>
                {
                    ["difficulty_position"] = state.DifficultyDetent,
                    ["static_expected_mission_ql"] = state.StaticExpectedMissionQl,
                    ["actual_mission_ql"] = observed ? (object)actualQl : null,
                    ["validation_status"] = !observed ? "NOT_CAPTURED" : actualQl == state.StaticExpectedMissionQl ? "MATCH" : "LIVE_CONTRADICTION_RECORDED"
                });
            }
            return new Dictionary<string, object>
            {
                ["difficulty_positions_completed"] = CompletedDifficultyCount,
                ["difficulty_positions_required"] = MissionQlResolver.DifficultyCount,
                ["distinct_actual_mission_qls"] = DistinctActualQlCount,
                ["states"] = states
            };
        }

        internal IDictionary<string, object> ToPayload()
        {
            var qls = new List<object>();
            var seen = new HashSet<int>();
            foreach (MissionItemQlCohort state in _definition.DifficultyStates)
            {
                int actualQl;
                if (!_actualQlByDetent.TryGetValue(state.DifficultyDetent, out actualQl) || !seen.Add(actualQl))
                    continue;
                int completed = CompletedRequestCount(actualQl);
                qls.Add(new Dictionary<string, object>
                {
                    ["actual_mission_ql"] = actualQl,
                    ["representative_difficulty_position"] = state.DifficultyDetent,
                    ["semantic_state"] = "CENTERED_ONLY",
                    ["completed_verified_requests"] = completed,
                    ["required_verified_requests"] = _definition.RequiredRequestsPerQl,
                    ["offers_captured"] = completed * 5,
                    ["status"] = completed >= _definition.RequiredRequestsPerQl ? "COMPLETE" : "INCOMPLETE"
                });
            }
            return new Dictionary<string, object>
            {
                ["campaign_name"] = MissionItemCampaignDefinition.CampaignName,
                ["campaign_manifest_sha256"] = _definition.ManifestSha256,
                ["observed_character_level"] = _definition.CharacterLevel,
                ["character_identity_instance"] = _characterIdentityInstance,
                ["difficulty_mapping"] = MappingPayload(),
                ["ql_cohorts"] = qls,
                ["completed_verified_requests"] = TotalCompletedRequestCount,
                ["remaining_verified_requests_known_so_far"] = RemainingRequestCount,
                ["character_status"] = IsComplete ? "COMPLETE" : "INCOMPLETE"
            };
        }
    }
}
