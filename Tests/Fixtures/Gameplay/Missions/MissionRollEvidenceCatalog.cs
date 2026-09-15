namespace ZoneEngine.Core.Missions
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// Exact five-offer type cohorts observed in the finalized roll captures. Selection is nearest-evidence
    /// and deterministic; it does not infer probability weights from how often a cohort happened to appear.
    /// </summary>
    internal static class MissionRollEvidenceCatalog
    {
        private static readonly RollMixEvidence[] Mixes =
        {
            new RollMixEvidence(220, 1, 154, 0, 0, 0, 0, 0, 0, new[] { MissionRollType.FindItem, MissionRollType.FindItem, MissionRollType.FindItem, MissionRollType.KillPerson, MissionRollType.FindPerson }),
            new RollMixEvidence(220, 1, 154, 0, 0, 0, 0, 0, 0, new[] { MissionRollType.KillPerson, MissionRollType.KillPerson, MissionRollType.KillPerson, MissionRollType.FindItemReturn, MissionRollType.FindItem }),
            new RollMixEvidence(220, 1, 154, 0, 0, 0, 0, 0, 0, new[] { MissionRollType.RepairMachine, MissionRollType.RepairMachine, MissionRollType.RepairMachine, MissionRollType.FindItemReturn, MissionRollType.FindPerson }),
            new RollMixEvidence(220, 1, 154, 0, 0, 0, 0, 0, 0, new[] { MissionRollType.FindPerson, MissionRollType.FindPerson, MissionRollType.FindPerson, MissionRollType.RepairMachine, MissionRollType.KillPerson }),
            new RollMixEvidence(220, 1, 154, 0, 0, 0, 0, 0, 0, new[] { MissionRollType.FindPerson, MissionRollType.FindPerson, MissionRollType.FindPerson, MissionRollType.RepairMachine, MissionRollType.FindItem }),
            new RollMixEvidence(220, 1, 154, 0, 0, 0, 0, 0, 0, new[] { MissionRollType.KillPerson, MissionRollType.KillPerson, MissionRollType.KillPerson, MissionRollType.RepairMachine, MissionRollType.FindPerson }),
            new RollMixEvidence(220, 1, 154, 0, 0, 0, 0, 0, 0, new[] { MissionRollType.FindItem, MissionRollType.FindItem, MissionRollType.FindItem, MissionRollType.RepairMachine, MissionRollType.FindPerson }),
            new RollMixEvidence(220, 1, 154, 0, 0, 0, 0, 0, 0, new[] { MissionRollType.KillPerson, MissionRollType.KillPerson, MissionRollType.KillPerson, MissionRollType.RepairMachine, MissionRollType.FindItem }),
            new RollMixEvidence(220, 1, 154, 0, 0, 0, 0, 0, 0, new[] { MissionRollType.FindPerson, MissionRollType.FindPerson, MissionRollType.FindPerson, MissionRollType.RepairMachine, MissionRollType.FindItemReturn }),
            new RollMixEvidence(220, 2, 165, 0, 0, 0, 0, 0, 0, new[] { MissionRollType.FindItemReturn, MissionRollType.FindItemReturn, MissionRollType.FindItemReturn, MissionRollType.RepairMachine, MissionRollType.FindPerson }),
            new RollMixEvidence(220, 4, 187, 0, 0, 0, 0, 0, 0, new[] { MissionRollType.FindItemReturn, MissionRollType.FindItemReturn, MissionRollType.FindItemReturn, MissionRollType.RepairMachine, MissionRollType.FindPerson }),
            new RollMixEvidence(220, 6, 220, 0, 0, 0, 0, 0, 0, new[] { MissionRollType.KillPerson, MissionRollType.KillPerson, MissionRollType.KillPerson, MissionRollType.FindPerson, MissionRollType.RepairMachine }),
            new RollMixEvidence(220, 8, 250, 0, 0, 0, 0, 0, 0, new[] { MissionRollType.FindPerson, MissionRollType.FindPerson, MissionRollType.KillPerson, MissionRollType.RepairMachine, MissionRollType.FindItemReturn }),
            new RollMixEvidence(220, 10, 250, 0, 0, 0, 0, 0, 0, new[] { MissionRollType.KillPerson, MissionRollType.KillPerson, MissionRollType.KillPerson, MissionRollType.FindPerson, MissionRollType.FindItem }),
            new RollMixEvidence(220, 11, 250, 0, 0, 0, 0, 0, 0, new[] { MissionRollType.FindPerson, MissionRollType.FindPerson, MissionRollType.FindPerson, MissionRollType.KillPerson, MissionRollType.FindItem }),
            new RollMixEvidence(60, 1, 42, 0, 0, 0, 0, 0, 0, new[] { MissionRollType.FindPerson, MissionRollType.FindPerson, MissionRollType.FindPerson, MissionRollType.KillPerson, MissionRollType.FindItem }),
            new RollMixEvidence(60, 1, 42, 0, 0, 0, 0, 0, 0, new[] { MissionRollType.FindItem, MissionRollType.FindItem, MissionRollType.FindItem, MissionRollType.FindItemReturn, MissionRollType.FindPerson }),
            new RollMixEvidence(60, 1, 42, 0, 0, 0, 0, 0, 0, new[] { MissionRollType.FindItemReturn, MissionRollType.FindItemReturn, MissionRollType.FindItemReturn, MissionRollType.KillPerson, MissionRollType.FindItem }),
            new RollMixEvidence(60, 1, 42, 0, 0, 0, 0, 0, 0, new[] { MissionRollType.KillPerson, MissionRollType.KillPerson, MissionRollType.KillPerson, MissionRollType.FindItemReturn, MissionRollType.FindItem }),
            new RollMixEvidence(60, 1, 42, 0, 0, 0, 0, 0, 0, new[] { MissionRollType.FindPerson, MissionRollType.FindPerson, MissionRollType.FindPerson, MissionRollType.RepairMachine, MissionRollType.FindItem }),
            new RollMixEvidence(60, 1, 42, -100, -100, 0, 0, 0, -100, new[] { MissionRollType.RepairMachine, MissionRollType.RepairMachine, MissionRollType.RepairMachine, MissionRollType.KillPerson, MissionRollType.FindPerson })
        };

        internal static MissionRollType[] SelectTypeMix(
            int characterLevel,
            int difficultyWireValue,
            int missionQuality,
            MissionSliderProfile sliders,
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

            var best = new List<RollMixEvidence>();
            EvidenceRank bestRank = null;
            for (int i = 0; i < Mixes.Length; i++)
            {
                RollMixEvidence candidate = Mixes[i];
                EvidenceRank rank = candidate.Rank(
                    characterLevel,
                    difficultyWireValue,
                    missionQuality,
                    sliders);
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

            RollMixEvidence selected = best[rng.Next(best.Count)];
            return (MissionRollType[])selected.Types.Clone();
        }

        internal static bool HasExactMix(
            int characterLevel,
            int difficultyWireValue,
            int missionQuality,
            MissionSliderProfile sliders)
        {
            for (int i = 0; i < Mixes.Length; i++)
            {
                if (Mixes[i].IsExact(characterLevel, difficultyWireValue, missionQuality, sliders))
                {
                    return true;
                }
            }

            return false;
        }

        private sealed class RollMixEvidence
        {
            internal readonly int CharacterLevel;
            internal readonly int Difficulty;
            internal readonly int MissionQuality;
            internal readonly int GoodBad;
            internal readonly int OrderChaos;
            internal readonly int OpenHidden;
            internal readonly int PhysicalMystical;
            internal readonly int HeadOnStealth;
            internal readonly int MoneyExperience;
            internal readonly MissionRollType[] Types;

            internal RollMixEvidence(
                int characterLevel,
                int difficulty,
                int missionQuality,
                int goodBad,
                int orderChaos,
                int openHidden,
                int physicalMystical,
                int headOnStealth,
                int moneyExperience,
                MissionRollType[] types)
            {
                CharacterLevel = characterLevel;
                Difficulty = difficulty;
                MissionQuality = missionQuality;
                GoodBad = goodBad;
                OrderChaos = orderChaos;
                OpenHidden = openHidden;
                PhysicalMystical = physicalMystical;
                HeadOnStealth = headOnStealth;
                MoneyExperience = moneyExperience;
                Types = types;
            }

            internal EvidenceRank Rank(
                int characterLevel,
                int difficulty,
                int missionQuality,
                MissionSliderProfile sliders)
            {
                return new EvidenceRank(
                    Math.Abs(MissionQuality - missionQuality),
                    Math.Abs(CharacterLevel - characterLevel),
                    Math.Abs(Difficulty - difficulty),
                    sliders.SemanticDistance(
                        GoodBad,
                        OrderChaos,
                        OpenHidden,
                        PhysicalMystical,
                        HeadOnStealth,
                        MoneyExperience));
            }

            internal bool IsExact(
                int characterLevel,
                int difficulty,
                int missionQuality,
                MissionSliderProfile sliders)
            {
                return CharacterLevel == characterLevel
                       && Difficulty == difficulty
                       && MissionQuality == missionQuality
                       && sliders.Matches(
                           GoodBad,
                           OrderChaos,
                           OpenHidden,
                           PhysicalMystical,
                           HeadOnStealth,
                           MoneyExperience);
            }
        }

        internal sealed class EvidenceRank : IComparable<EvidenceRank>
        {
            private readonly int qualityDistance;
            private readonly int levelDistance;
            private readonly int difficultyDistance;
            private readonly int sliderDistance;

            internal EvidenceRank(
                int qualityDistance,
                int levelDistance,
                int difficultyDistance,
                int sliderDistance)
            {
                this.qualityDistance = qualityDistance;
                this.levelDistance = levelDistance;
                this.difficultyDistance = difficultyDistance;
                this.sliderDistance = sliderDistance;
            }

            public int CompareTo(EvidenceRank other)
            {
                int result = qualityDistance.CompareTo(other.qualityDistance);
                if (result != 0)
                {
                    return result;
                }

                result = levelDistance.CompareTo(other.levelDistance);
                if (result != 0)
                {
                    return result;
                }

                result = difficultyDistance.CompareTo(other.difficultyDistance);
                return result != 0 ? result : sliderDistance.CompareTo(other.sliderDistance);
            }
        }
    }
}
