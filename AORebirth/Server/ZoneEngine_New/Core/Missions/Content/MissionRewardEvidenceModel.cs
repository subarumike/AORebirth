namespace ZoneEngine.Core.Missions
{
    using System;
    using System.Collections.Generic;

    using SmokeLounge.AOtomation.Messaging.GameData;

    /// <summary>
    /// Exact cash/XP pairs from the finalized mission-roll captures. Selection uses nearest captured
    /// evidence only; it does not calculate, scale, or interpolate rewards.
    /// </summary>
    internal static class MissionRewardEvidenceModel
    {
        // 115 captured offers collapse to 108 unique records on the full evidence key. Repeated rolls and
        // same-key offers are deliberately not retained as probability weights.
        private static readonly RewardEvidence[] Evidence = MissionContentJson.Read<RewardEvidence[]>("RewardObservations.json");

        internal static void Apply(
            QuestInfo offer,
            MissionRollType type,
            int characterLevel,
            int difficultyWire,
            int missionQl,
            MissionSliderProfile sliders,
            int playfieldId,
            Random rng)
        {
            if (offer == null)
            {
                throw new ArgumentNullException("offer");
            }

            RewardEvidence selected = Select(
                type,
                characterLevel,
                difficultyWire,
                missionQl,
                sliders,
                playfieldId,
                rng);
            offer.CashReward = selected.CashReward;
            offer.ExperienceReward = selected.ExperienceReward;
        }

        internal static bool HasExactEvidence(
            MissionRollType type,
            int characterLevel,
            int difficultyWire,
            int missionQl,
            MissionSliderProfile sliders,
            int playfieldId)
        {
            if (sliders == null)
            {
                return false;
            }

            for (int i = 0; i < Evidence.Length; i++)
            {
                RewardEvidence candidate = Evidence[i];
                if (candidate.Type == type
                    && candidate.PlayfieldId == playfieldId
                    && candidate.IsExact(characterLevel, difficultyWire, missionQl, sliders))
                {
                    return true;
                }
            }

            return false;
        }

        internal static bool HasExactEvidence(
            MissionRollType type,
            int characterLevel,
            int difficultyWire,
            int missionQl,
            MissionSliderProfile sliders)
        {
            if (sliders == null)
            {
                return false;
            }

            for (int i = 0; i < Evidence.Length; i++)
            {
                RewardEvidence candidate = Evidence[i];
                if (candidate.Type == type
                    && candidate.IsExact(characterLevel, difficultyWire, missionQl, sliders))
                {
                    return true;
                }
            }

            return false;
        }

        internal static bool IsCapturedPair(
            MissionRollType type,
            int characterLevel,
            int difficultyWire,
            int missionQl,
            MissionSliderProfile sliders,
            int playfieldId,
            int cashReward,
            int experienceReward)
        {
            if (sliders == null)
            {
                return false;
            }

            for (int i = 0; i < Evidence.Length; i++)
            {
                RewardEvidence candidate = Evidence[i];
                if (candidate.Type == type
                    && candidate.PlayfieldId == playfieldId
                    && candidate.CashReward == cashReward
                    && candidate.ExperienceReward == experienceReward
                    && candidate.IsExact(characterLevel, difficultyWire, missionQl, sliders))
                {
                    return true;
                }
            }

            return false;
        }

        internal static bool IsCapturedPair(
            MissionRollType type,
            int characterLevel,
            int difficultyWire,
            int missionQl,
            MissionSliderProfile sliders,
            int cashReward,
            int experienceReward)
        {
            if (sliders == null)
            {
                return false;
            }

            for (int i = 0; i < Evidence.Length; i++)
            {
                RewardEvidence candidate = Evidence[i];
                if (candidate.Type == type
                    && candidate.CashReward == cashReward
                    && candidate.ExperienceReward == experienceReward
                    && candidate.IsExact(characterLevel, difficultyWire, missionQl, sliders))
                {
                    return true;
                }
            }

            return false;
        }

        internal static bool IsCapturedPair(int cashReward, int experienceReward)
        {
            for (int i = 0; i < Evidence.Length; i++)
            {
                if (Evidence[i].CashReward == cashReward
                    && Evidence[i].ExperienceReward == experienceReward)
                {
                    return true;
                }
            }

            return false;
        }

        private static RewardEvidence Select(
            MissionRollType type,
            int characterLevel,
            int difficultyWire,
            int missionQl,
            MissionSliderProfile sliders,
            int playfieldId,
            Random rng)
        {
            if (sliders == null)
            {
                throw new ArgumentNullException("sliders");
            }

            if (rng == null)
            {
                throw new ArgumentNullException("rng");
            }

            var best = new List<RewardEvidence>();
            EvidenceRank bestRank = null;
            for (int i = 0; i < Evidence.Length; i++)
            {
                RewardEvidence candidate = Evidence[i];
                if (candidate.Type != type)
                {
                    continue;
                }

                EvidenceRank rank = candidate.Rank(
                    characterLevel,
                    difficultyWire,
                    missionQl,
                    sliders,
                    playfieldId);
                int comparison = bestRank == null ? -1 : rank.CompareTo(bestRank);
                if (comparison < 0)
                {
                    best.Clear();
                    best.Add(candidate);
                    bestRank = rank;
                }
                else if (comparison == 0)
                {
                    best.Add(candidate);
                }
            }

            if (best.Count == 0)
            {
                throw new ArgumentOutOfRangeException("type", "No captured reward evidence exists for this mission type.");
            }

            return best[rng.Next(best.Count)];
        }

        private sealed class RewardEvidence
        {
            internal readonly int CharacterLevel;
            internal readonly int DifficultyWire;
            internal readonly int MissionQl;
            internal readonly int GoodBad;
            internal readonly int OrderChaos;
            internal readonly int OpenHidden;
            internal readonly int PhysicalMystical;
            internal readonly int HeadOnStealth;
            internal readonly int MoneyExperience;
            internal readonly MissionRollType Type;
            internal readonly int PlayfieldId;
            internal readonly int CashReward;
            internal readonly int ExperienceReward;

            internal RewardEvidence(
                int characterLevel,
                int difficultyWire,
                int missionQl,
                int goodBad,
                int orderChaos,
                int openHidden,
                int physicalMystical,
                int headOnStealth,
                int moneyExperience,
                MissionRollType type,
                int playfieldId,
                int cashReward,
                int experienceReward)
            {
                CharacterLevel = characterLevel;
                DifficultyWire = difficultyWire;
                MissionQl = missionQl;
                GoodBad = goodBad;
                OrderChaos = orderChaos;
                OpenHidden = openHidden;
                PhysicalMystical = physicalMystical;
                HeadOnStealth = headOnStealth;
                MoneyExperience = moneyExperience;
                Type = type;
                PlayfieldId = playfieldId;
                CashReward = cashReward;
                ExperienceReward = experienceReward;
            }

            internal EvidenceRank Rank(
                int characterLevel,
                int difficultyWire,
                int missionQl,
                MissionSliderProfile sliders,
                int playfieldId)
            {
                return new EvidenceRank(
                    Distance(MissionQl, missionQl),
                    Distance(CharacterLevel, characterLevel),
                    Distance(DifficultyWire, difficultyWire),
                    sliders.SemanticDistance(
                        GoodBad,
                        OrderChaos,
                        OpenHidden,
                        PhysicalMystical,
                        HeadOnStealth,
                        MoneyExperience),
                    PlayfieldId == playfieldId ? 0 : 1);
            }

            internal bool IsExact(
                int characterLevel,
                int difficultyWire,
                int missionQl,
                MissionSliderProfile sliders)
            {
                return CharacterLevel == characterLevel
                       && DifficultyWire == difficultyWire
                       && MissionQl == missionQl
                       && sliders.Matches(
                           GoodBad,
                           OrderChaos,
                           OpenHidden,
                           PhysicalMystical,
                           HeadOnStealth,
                           MoneyExperience);
            }

            private static long Distance(int left, int right)
            {
                return Math.Abs((long)left - right);
            }
        }

        private sealed class EvidenceRank : IComparable<EvidenceRank>
        {
            private readonly long missionQlDistance;
            private readonly long characterLevelDistance;
            private readonly long difficultyDistance;
            private readonly int sliderDistance;
            private readonly int playfieldMismatch;

            internal EvidenceRank(
                long missionQlDistance,
                long characterLevelDistance,
                long difficultyDistance,
                int sliderDistance,
                int playfieldMismatch)
            {
                this.missionQlDistance = missionQlDistance;
                this.characterLevelDistance = characterLevelDistance;
                this.difficultyDistance = difficultyDistance;
                this.sliderDistance = sliderDistance;
                this.playfieldMismatch = playfieldMismatch;
            }

            public int CompareTo(EvidenceRank other)
            {
                int result = missionQlDistance.CompareTo(other.missionQlDistance);
                if (result != 0)
                {
                    return result;
                }

                result = characterLevelDistance.CompareTo(other.characterLevelDistance);
                if (result != 0)
                {
                    return result;
                }

                result = difficultyDistance.CompareTo(other.difficultyDistance);
                if (result != 0)
                {
                    return result;
                }

                result = sliderDistance.CompareTo(other.sliderDistance);
                return result != 0 ? result : playfieldMismatch.CompareTo(other.playfieldMismatch);
            }
        }
    }
}
