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
        private static readonly RewardEvidence[] Evidence =
        {
            // Level 60, neutral sliders, five finalized roll cohorts.
            new RewardEvidence(60, 1, 42, 0, 0, 0, 0, 0, 0, MissionRollType.FindPerson, 635, 5206, 2178),
            new RewardEvidence(60, 1, 42, 0, 0, 0, 0, 0, 0, MissionRollType.FindPerson, 670, 4886, 2167),
            new RewardEvidence(60, 1, 42, 0, 0, 0, 0, 0, 0, MissionRollType.FindPerson, 655, 4811, 2165),
            new RewardEvidence(60, 1, 42, 0, 0, 0, 0, 0, 0, MissionRollType.KillPerson, 635, 4500, 2155),
            new RewardEvidence(60, 1, 42, 0, 0, 0, 0, 0, 0, MissionRollType.FindItem, 760, 5741, 2196),
            new RewardEvidence(60, 1, 42, 0, 0, 0, 0, 0, 0, MissionRollType.FindItem, 670, 13980, 1690),
            new RewardEvidence(60, 1, 42, 0, 0, 0, 0, 0, 0, MissionRollType.FindItem, 695, 10446, 1646),
            new RewardEvidence(60, 1, 42, 0, 0, 0, 0, 0, 0, MissionRollType.FindItem, 630, 7537, 1610),
            new RewardEvidence(60, 1, 42, 0, 0, 0, 0, 0, 0, MissionRollType.FindItemReturn, 670, 13007, 1808),
            new RewardEvidence(60, 1, 42, 0, 0, 0, 0, 0, 0, MissionRollType.FindPerson, 717, 2981, 2292),
            new RewardEvidence(60, 1, 42, 0, 0, 0, 0, 0, 0, MissionRollType.FindItemReturn, 695, 9079, 1750),
            new RewardEvidence(60, 1, 42, 0, 0, 0, 0, 0, 0, MissionRollType.FindItemReturn, 670, 11204, 1781),
            new RewardEvidence(60, 1, 42, 0, 0, 0, 0, 0, 0, MissionRollType.FindItemReturn, 670, 12176, 1796),
            new RewardEvidence(60, 1, 42, 0, 0, 0, 0, 0, 0, MissionRollType.KillPerson, 630, 4309, 2148),
            new RewardEvidence(60, 1, 42, 0, 0, 0, 0, 0, 0, MissionRollType.FindItem, 695, 6537, 2016),
            new RewardEvidence(60, 1, 42, 0, 0, 0, 0, 0, 0, MissionRollType.KillPerson, 635, 7301, 1724),
            new RewardEvidence(60, 1, 42, 0, 0, 0, 0, 0, 0, MissionRollType.KillPerson, 670, 7770, 1731),
            new RewardEvidence(60, 1, 42, 0, 0, 0, 0, 0, 0, MissionRollType.KillPerson, 635, 8262, 1738),
            new RewardEvidence(60, 1, 42, 0, 0, 0, 0, 0, 0, MissionRollType.FindItemReturn, 670, 10768, 1775),
            new RewardEvidence(60, 1, 42, 0, 0, 0, 0, 0, 0, MissionRollType.FindItem, 635, 8748, 1625),
            new RewardEvidence(60, 1, 42, 0, 0, 0, 0, 0, 0, MissionRollType.FindPerson, 670, 6413, 2218),
            new RewardEvidence(60, 1, 42, 0, 0, 0, 0, 0, 0, MissionRollType.FindPerson, 760, 5826, 2199),
            new RewardEvidence(60, 1, 42, 0, 0, 0, 0, 0, 0, MissionRollType.FindPerson, 670, 6257, 2213),
            new RewardEvidence(60, 1, 42, 0, 0, 0, 0, 0, 0, MissionRollType.RepairMachine, 635, 5627, 2124),
            new RewardEvidence(60, 1, 42, 0, 0, 0, 0, 0, 0, MissionRollType.FindItem, 760, 5740, 2196),

            // Level 60, signed left Good/Bad + Order/Chaos + Credits/XP profile.
            new RewardEvidence(60, 1, 42, -100, -100, 0, 0, 0, -100, MissionRollType.RepairMachine, 655, 5170, 2110),
            new RewardEvidence(60, 1, 42, -100, -100, 0, 0, 0, -100, MissionRollType.RepairMachine, 695, 5786, 2128),
            new RewardEvidence(60, 1, 42, -100, -100, 0, 0, 0, -100, MissionRollType.RepairMachine, 695, 6030, 2135),
            new RewardEvidence(60, 1, 42, -100, -100, 0, 0, 0, -100, MissionRollType.KillPerson, 635, 5867, 2064),
            new RewardEvidence(60, 1, 42, -100, -100, 0, 0, 0, -100, MissionRollType.FindPerson, 635, 5917, 2002),

            // Level 220, difficulty wire 1, mission QL 154.
            new RewardEvidence(220, 1, 154, 0, 0, 0, 0, 0, 0, MissionRollType.FindItem, 505, 55952, 38225500),
            new RewardEvidence(220, 1, 154, 0, 0, 0, 0, 0, 0, MissionRollType.FindItem, 505, 51983, 37683295),
            new RewardEvidence(220, 1, 154, 0, 0, 0, 0, 0, 0, MissionRollType.FindItem, 570, 62108, 39066604),
            new RewardEvidence(220, 1, 154, 0, 0, 0, 0, 0, 0, MissionRollType.KillPerson, 570, 40444, 41297630),
            new RewardEvidence(220, 1, 154, 0, 0, 0, 0, 0, 0, MissionRollType.FindPerson, 570, 61113, 38930693),
            new RewardEvidence(220, 1, 154, 0, 0, 0, 0, 0, 0, MissionRollType.KillPerson, 505, 52662, 37776062),
            new RewardEvidence(220, 1, 154, 0, 0, 0, 0, 0, 0, MissionRollType.KillPerson, 570, 62643, 39139698),
            new RewardEvidence(220, 1, 154, 0, 0, 0, 0, 0, 0, MissionRollType.KillPerson, 570, 62636, 39138738),
            new RewardEvidence(220, 1, 154, 0, 0, 0, 0, 0, 0, MissionRollType.FindItemReturn, 505, 57828, 36831660),
            new RewardEvidence(220, 1, 154, 0, 0, 0, 0, 0, 0, MissionRollType.FindItem, 505, 51250, 37583209),
            new RewardEvidence(220, 1, 154, 0, 0, 0, 0, 0, 0, MissionRollType.RepairMachine, 560, 63836, 37559145),
            new RewardEvidence(220, 1, 154, 0, 0, 0, 0, 0, 0, MissionRollType.RepairMachine, 560, 63981, 37576788),
            new RewardEvidence(220, 1, 154, 0, 0, 0, 0, 0, 0, MissionRollType.RepairMachine, 560, 63939, 37571605),
            new RewardEvidence(220, 1, 154, 0, 0, 0, 0, 0, 0, MissionRollType.FindItemReturn, 560, 32166, 41595831),
            new RewardEvidence(220, 1, 154, 0, 0, 0, 0, 0, 0, MissionRollType.FindPerson, 560, 54552, 38034323),
            new RewardEvidence(220, 1, 154, 0, 0, 0, 0, 0, 0, MissionRollType.FindPerson, 505, 11050, 45099851),
            new RewardEvidence(220, 1, 154, 0, 0, 0, 0, 0, 0, MissionRollType.FindPerson, 505, 12023, 45897105),
            new RewardEvidence(220, 1, 154, 0, 0, 0, 0, 0, 0, MissionRollType.FindPerson, 560, 10869, 44951456),
            new RewardEvidence(220, 1, 154, 0, 0, 0, 0, 0, 0, MissionRollType.RepairMachine, 560, 1000, 48168725),
            new RewardEvidence(220, 1, 154, 0, 0, 0, 0, 0, 0, MissionRollType.KillPerson, 505, 51983, 37683295),
            new RewardEvidence(220, 1, 154, 0, 0, 0, 0, 0, 0, MissionRollType.FindPerson, 505, 40006, 41205560),
            new RewardEvidence(220, 1, 154, 0, 0, 0, 0, 0, 0, MissionRollType.FindPerson, 560, 40823, 41377026),
            new RewardEvidence(220, 1, 154, 0, 0, 0, 0, 0, 0, MissionRollType.FindPerson, 560, 40761, 41364000),
            new RewardEvidence(220, 1, 154, 0, 0, 0, 0, 0, 0, MissionRollType.RepairMachine, 505, 57828, 36831660),
            new RewardEvidence(220, 1, 154, 0, 0, 0, 0, 0, 0, MissionRollType.KillPerson, 570, 111900, 31308255),
            new RewardEvidence(220, 1, 154, 0, 0, 0, 0, 0, 0, MissionRollType.KillPerson, 570, 111708, 31296568),
            new RewardEvidence(220, 1, 154, 0, 0, 0, 0, 0, 0, MissionRollType.KillPerson, 505, 99717, 30568466),
            new RewardEvidence(220, 1, 154, 0, 0, 0, 0, 0, 0, MissionRollType.FindItemReturn, 560, 94497, 30251513),
            new RewardEvidence(220, 1, 154, 0, 0, 0, 0, 0, 0, MissionRollType.FindItem, 560, 54469, 38022880),
            new RewardEvidence(220, 1, 154, 0, 0, 0, 0, 0, 0, MissionRollType.KillPerson, 560, 57641, 38456350),
            new RewardEvidence(220, 1, 154, 0, 0, 0, 0, 0, 0, MissionRollType.KillPerson, 505, 56122, 38248768),
            new RewardEvidence(220, 1, 154, 0, 0, 0, 0, 0, 0, MissionRollType.KillPerson, 560, 51898, 37671604),
            new RewardEvidence(220, 1, 154, 0, 0, 0, 0, 0, 0, MissionRollType.RepairMachine, 560, 27921, 42833272),
            new RewardEvidence(220, 1, 154, 0, 0, 0, 0, 0, 0, MissionRollType.FindPerson, 505, 11814, 45725932),
            new RewardEvidence(220, 1, 154, 0, 0, 0, 0, 0, 0, MissionRollType.FindItem, 560, 58154, 38526371),
            new RewardEvidence(220, 1, 154, 0, 0, 0, 0, 0, 0, MissionRollType.FindItem, 505, 59009, 38643212),
            new RewardEvidence(220, 1, 154, 0, 0, 0, 0, 0, 0, MissionRollType.FindItem, 570, 56820, 38344143),
            new RewardEvidence(220, 1, 154, 0, 0, 0, 0, 0, 0, MissionRollType.RepairMachine, 505, 59451, 37028164),
            new RewardEvidence(220, 1, 154, 0, 0, 0, 0, 0, 0, MissionRollType.FindPerson, 570, 60101, 38792423),
            new RewardEvidence(220, 1, 154, 0, 0, 0, 0, 0, 0, MissionRollType.KillPerson, 560, 58217, 38534980),
            new RewardEvidence(220, 1, 154, 0, 0, 0, 0, 0, 0, MissionRollType.KillPerson, 560, 54522, 38030163),
            new RewardEvidence(220, 1, 154, 0, 0, 0, 0, 0, 0, MissionRollType.KillPerson, 570, 58407, 38560920),
            new RewardEvidence(220, 1, 154, 0, 0, 0, 0, 0, 0, MissionRollType.RepairMachine, 505, 39571, 41114465),
            new RewardEvidence(220, 1, 154, 0, 0, 0, 0, 0, 0, MissionRollType.FindItem, 570, 63380, 39240395),
            new RewardEvidence(220, 1, 154, 0, 0, 0, 0, 0, 0, MissionRollType.FindPerson, 570, 43496, 41937960),
            new RewardEvidence(220, 1, 154, 0, 0, 0, 0, 0, 0, MissionRollType.FindPerson, 560, 40953, 41404442),
            new RewardEvidence(220, 1, 154, 0, 0, 0, 0, 0, 0, MissionRollType.FindPerson, 560, 40890, 41391123),
            new RewardEvidence(220, 1, 154, 0, 0, 0, 0, 0, 0, MissionRollType.RepairMachine, 505, 38704, 40932452),
            new RewardEvidence(220, 1, 154, 0, 0, 0, 0, 0, 0, MissionRollType.FindItemReturn, 560, 54552, 38034323),

            // Level 220 difficulty ladder.
            new RewardEvidence(220, 2, 165, 0, 0, 0, 0, 0, 0, MissionRollType.FindItemReturn, 570, 73548, 46922323),
            new RewardEvidence(220, 2, 165, 0, 0, 0, 0, 0, 0, MissionRollType.FindItemReturn, 560, 68480, 46217808),
            new RewardEvidence(220, 2, 165, 0, 0, 0, 0, 0, 0, MissionRollType.FindItemReturn, 505, 64481, 45661860),
            new RewardEvidence(220, 2, 165, 0, 0, 0, 0, 0, 0, MissionRollType.RepairMachine, 560, 68162, 46173527),
            new RewardEvidence(220, 2, 165, 0, 0, 0, 0, 0, 0, MissionRollType.FindPerson, 505, 87601, 40981934),
            new RewardEvidence(220, 4, 187, 0, 0, 0, 0, 0, 0, MissionRollType.FindItemReturn, 560, 72376, 66368491),
            new RewardEvidence(220, 4, 187, 0, 0, 0, 0, 0, 0, MissionRollType.FindItemReturn, 570, 72019, 66304870),
            new RewardEvidence(220, 4, 187, 0, 0, 0, 0, 0, 0, MissionRollType.RepairMachine, 560, 49356, 74083585),
            new RewardEvidence(220, 4, 187, 0, 0, 0, 0, 0, 0, MissionRollType.FindPerson, 565, 37919, 70545233),
            new RewardEvidence(220, 6, 220, 0, 0, 0, 0, 0, 0, MissionRollType.KillPerson, 795, 74515, 93777821),
            new RewardEvidence(220, 6, 220, 0, 0, 0, 0, 0, 0, MissionRollType.KillPerson, 505, 116478, 101299389),
            new RewardEvidence(220, 6, 220, 0, 0, 0, 0, 0, 0, MissionRollType.KillPerson, 505, 115037, 101040956),
            new RewardEvidence(220, 6, 220, 0, 0, 0, 0, 0, 0, MissionRollType.FindPerson, 560, 122716, 98158525),
            new RewardEvidence(220, 6, 220, 0, 0, 0, 0, 0, 0, MissionRollType.RepairMachine, 505, 91733, 109741004),
            new RewardEvidence(220, 8, 250, 0, 0, 0, 0, 0, 0, MissionRollType.FindPerson, 570, 130471, 167430989),
            new RewardEvidence(220, 8, 250, 0, 0, 0, 0, 0, 0, MissionRollType.FindPerson, 665, 83116, 155718928),
            new RewardEvidence(220, 8, 250, 0, 0, 0, 0, 0, 0, MissionRollType.KillPerson, 570, 89129, 192118973),
            new RewardEvidence(220, 8, 250, 0, 0, 0, 0, 0, 0, MissionRollType.RepairMachine, 560, 102726, 189113880),
            new RewardEvidence(220, 8, 250, 0, 0, 0, 0, 0, 0, MissionRollType.FindItemReturn, 795, 66049, 175330820),
            new RewardEvidence(220, 10, 250, 0, 0, 0, 0, 0, 0, MissionRollType.KillPerson, 570, 19287, 453565047),
            new RewardEvidence(220, 10, 250, 0, 0, 0, 0, 0, 0, MissionRollType.KillPerson, 570, 18954, 451907978),
            new RewardEvidence(220, 10, 250, 0, 0, 0, 0, 0, 0, MissionRollType.KillPerson, 795, 13095, 422799584),
            new RewardEvidence(220, 10, 250, 0, 0, 0, 0, 0, 0, MissionRollType.FindPerson, 570, 19115, 452710628),
            new RewardEvidence(220, 10, 250, 0, 0, 0, 0, 0, 0, MissionRollType.FindItem, 795, 60691, 351683074),
            new RewardEvidence(220, 11, 250, 0, 0, 0, 0, 0, 0, MissionRollType.FindPerson, 615, 69070, 610760533),
            new RewardEvidence(220, 11, 250, 0, 0, 0, 0, 0, 0, MissionRollType.FindPerson, 620, 56150, 592540050),
            new RewardEvidence(220, 11, 250, 0, 0, 0, 0, 0, 0, MissionRollType.FindPerson, 595, 58539, 595908591),
            new RewardEvidence(220, 11, 250, 0, 0, 0, 0, 0, 0, MissionRollType.KillPerson, 560, 110915, 562936508),
            new RewardEvidence(220, 11, 250, 0, 0, 0, 0, 0, 0, MissionRollType.FindItem, 560, 87812, 637192854)
        };

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
