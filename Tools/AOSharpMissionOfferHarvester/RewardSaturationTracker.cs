using System;
using System.Collections.Generic;
using System.Globalization;

namespace AORebirth.MissionEvidence
{
    internal sealed class RewardDescriptorObservation
    {
        internal int LowId { get; private set; }
        internal int HighId { get; private set; }
        internal int Ql { get; private set; }
        internal int Unknown { get; private set; }

        internal RewardDescriptorObservation(int lowId, int highId, int ql, int unknown)
        {
            LowId = lowId;
            HighId = highId;
            Ql = ql;
            Unknown = unknown;
        }

        internal string PairKey
        {
            get
            {
                return string.Format(CultureInfo.InvariantCulture, "{0}:{1}", LowId, HighId);
            }
        }

        internal string DescriptorKey
        {
            get
            {
                return string.Format(CultureInfo.InvariantCulture, "{0}:{1}:{2}:{3}", LowId, HighId, Ql, Unknown);
            }
        }

        internal IDictionary<string, object> ToPayload()
        {
            return new Dictionary<string, object>
            {
                ["low_id"] = LowId,
                ["high_id"] = HighId,
                ["ql"] = Ql,
                ["unknown"] = Unknown,
                ["pair_key"] = PairKey,
                ["descriptor_key"] = DescriptorKey
            };
        }
    }

    internal sealed class RewardSaturationUpdate
    {
        internal int StateIndex { get; set; }
        internal string StateLabel { get; set; }
        internal int NewPairCount { get; set; }
        internal int NewDescriptorCount { get; set; }
        internal IList<RewardDescriptorObservation> NewDescriptors { get; set; }
        internal bool RoundCompleted { get; set; }
        internal int CompletedRounds { get; set; }
        internal int NewDescriptorsInCompletedRound { get; set; }
        internal int ConsecutiveQuietRounds { get; set; }
        internal bool Saturated { get; set; }
    }

    internal sealed class RewardSaturationTracker
    {
        private readonly int _stateCount;
        private readonly int _quietRoundTarget;
        private readonly HashSet<string> _uniquePairs = new HashSet<string>(StringComparer.Ordinal);
        private readonly HashSet<string> _uniqueDescriptors = new HashSet<string>(StringComparer.Ordinal);
        private readonly IDictionary<int, HashSet<string>> _stateDescriptors =
            new Dictionary<int, HashSet<string>>();
        private int _requestsCompleted;
        private int _completedRounds;
        private int _consecutiveQuietRounds;
        private int _newDescriptorsThisRound;

        internal RewardSaturationTracker(int stateCount, int quietRoundTarget)
        {
            if (stateCount < 1)
                throw new ArgumentOutOfRangeException("stateCount");
            if (quietRoundTarget < 1)
                throw new ArgumentOutOfRangeException("quietRoundTarget");
            _stateCount = stateCount;
            _quietRoundTarget = quietRoundTarget;
            for (int index = 1; index <= stateCount; index++)
                _stateDescriptors[index] = new HashSet<string>(StringComparer.Ordinal);
        }

        internal int UniquePairCount { get { return _uniquePairs.Count; } }
        internal int UniqueDescriptorCount { get { return _uniqueDescriptors.Count; } }
        internal int RequestsCompleted { get { return _requestsCompleted; } }
        internal int CompletedRounds { get { return _completedRounds; } }
        internal int ConsecutiveQuietRounds { get { return _consecutiveQuietRounds; } }
        internal int QuietRoundTarget { get { return _quietRoundTarget; } }
        internal bool IsSaturated { get { return _consecutiveQuietRounds >= _quietRoundTarget; } }

        internal bool TryObserve(
            int stateIndex,
            string stateLabel,
            IEnumerable<RewardDescriptorObservation> observations,
            out RewardSaturationUpdate update,
            out string error)
        {
            update = null;
            error = null;
            int expectedStateIndex = (_requestsCompleted % _stateCount) + 1;
            if (stateIndex != expectedStateIndex)
            {
                error = "REWARD_SATURATION_STATE_ORDER_MISMATCH";
                return false;
            }

            var newDescriptors = new List<RewardDescriptorObservation>();
            int newPairs = 0;
            foreach (RewardDescriptorObservation observation in observations ?? new RewardDescriptorObservation[0])
            {
                if (observation == null)
                    continue;
                if (_uniquePairs.Add(observation.PairKey))
                    newPairs++;
                _stateDescriptors[stateIndex].Add(observation.DescriptorKey);
                if (_uniqueDescriptors.Add(observation.DescriptorKey))
                    newDescriptors.Add(observation);
            }

            _requestsCompleted++;
            _newDescriptorsThisRound += newDescriptors.Count;
            bool roundCompleted = stateIndex == _stateCount;
            int completedRoundNewDescriptors = 0;
            if (roundCompleted)
            {
                _completedRounds++;
                completedRoundNewDescriptors = _newDescriptorsThisRound;
                _consecutiveQuietRounds = _newDescriptorsThisRound == 0
                    ? _consecutiveQuietRounds + 1
                    : 0;
                _newDescriptorsThisRound = 0;
            }

            update = new RewardSaturationUpdate
            {
                StateIndex = stateIndex,
                StateLabel = stateLabel,
                NewPairCount = newPairs,
                NewDescriptorCount = newDescriptors.Count,
                NewDescriptors = newDescriptors,
                RoundCompleted = roundCompleted,
                CompletedRounds = _completedRounds,
                NewDescriptorsInCompletedRound = completedRoundNewDescriptors,
                ConsecutiveQuietRounds = _consecutiveQuietRounds,
                Saturated = IsSaturated
            };
            return true;
        }

        internal IDictionary<string, object> ToPayload()
        {
            var perState = new List<object>();
            for (int index = 1; index <= _stateCount; index++)
            {
                perState.Add(new Dictionary<string, object>
                {
                    ["state_index"] = index,
                    ["unique_descriptor_count"] = _stateDescriptors[index].Count
                });
            }
            return new Dictionary<string, object>
            {
                ["state_count"] = _stateCount,
                ["requests_completed"] = _requestsCompleted,
                ["completed_rounds"] = _completedRounds,
                ["consecutive_quiet_rounds"] = _consecutiveQuietRounds,
                ["quiet_round_target"] = _quietRoundTarget,
                ["unique_reward_pair_count"] = _uniquePairs.Count,
                ["unique_reward_descriptor_count"] = _uniqueDescriptors.Count,
                ["saturation_reached"] = IsSaturated,
                ["saturation_semantics"] = "NO_NEW_LOW_HIGH_QL_UNKNOWN_DESCRIPTOR_ACROSS_COMPLETE_13_STATE_ROUNDS",
                ["per_state"] = perState
            };
        }
    }

    internal static class RewardSaturationPlan
    {
        internal const int StateCount = 13;

        internal static bool TryBuild(int difficultyDetent, out IList<MissionSliderPlanEntry> plan, out string error)
        {
            plan = new List<MissionSliderPlanEntry>();
            error = null;
            string[] presets =
            {
                "CENTERED_BASELINE",
                "GOOD_BAD_FULL_LEFT", "GOOD_BAD_FULL_RIGHT",
                "ORDER_CHAOS_FULL_LEFT", "ORDER_CHAOS_FULL_RIGHT",
                "OPEN_HIDDEN_FULL_LEFT", "OPEN_HIDDEN_FULL_RIGHT",
                "PHYSICAL_MYSTICAL_FULL_LEFT", "PHYSICAL_MYSTICAL_FULL_RIGHT",
                "HEADON_STEALTH_FULL_LEFT", "HEADON_STEALTH_FULL_RIGHT",
                "MONEY_XP_FULL_LEFT", "MONEY_XP_FULL_RIGHT"
            };
            for (int index = 0; index < presets.Length; index++)
            {
                MissionSliderState state;
                if (!MissionSliderState.TryCreatePreset(difficultyDetent, presets[index], out state, out error))
                {
                    plan = null;
                    return false;
                }
                plan.Add(new MissionSliderPlanEntry(index + 1, presets[index], state));
            }
            return true;
        }
    }
}
