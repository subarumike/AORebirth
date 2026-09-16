namespace ZoneEngine.Core.Missions
{
    using System;
    using System.Globalization;

    using SmokeLounge.AOtomation.Messaging.GameData;

    /// <summary>
    /// Rebuilds only the length-prefixed mutable description. The captured 32-byte title remains intact:
    /// live QuestAlternative readers treat that field as a non-zero fixed-width title ending immediately
    /// before the description length. Shorter zero-padded replacements misalign the remaining offer.
    /// </summary>
    internal static class MissionOfferTextBuilder
    {
        internal sealed class Snapshot
        {
            internal int Icon;
            internal int Quality;
            internal int Cash;
            internal int Experience;
            internal int Playfield;
            internal float X;
            internal float Z;
            internal int RewardLow;
            internal int RewardHigh;
            internal int RewardQuality;
            internal Identity TerminalReferenceUnknown5;
            internal Identity TerminalReferenceUnknown14;
            internal Identity TerminalReferenceUnknown23;
            internal Identity ObjectiveTerminalReference;
        }

        internal static Snapshot Capture(QuestInfo offer)
        {
            var snapshot = new Snapshot();
            if (offer == null)
            {
                return snapshot;
            }

            snapshot.Icon = offer.MissionIconId;
            snapshot.Quality = offer.Quality;
            snapshot.Cash = offer.CashReward;
            snapshot.Experience = offer.ExperienceReward;
            snapshot.TerminalReferenceUnknown5 = offer.Unknown5;
            snapshot.TerminalReferenceUnknown14 = offer.Unknown14;
            snapshot.TerminalReferenceUnknown23 = offer.Unknown23;
            if (offer.QuestActions != null && offer.QuestActions.Length > 0 && offer.QuestActions[0] != null)
            {
                snapshot.Playfield = offer.QuestActions[0].Playfield.Instance;
                snapshot.X = offer.QuestActions[0].X;
                snapshot.Z = offer.QuestActions[0].Z;
                snapshot.ObjectiveTerminalReference = offer.QuestActions[0].Unknown1;
            }

            if (offer.ItemRewards != null && offer.ItemRewards.Length > 0 && offer.ItemRewards[0] != null)
            {
                snapshot.RewardLow = offer.ItemRewards[0].LowId;
                snapshot.RewardHigh = offer.ItemRewards[0].HighId;
                snapshot.RewardQuality = offer.ItemRewards[0].Quality;
            }

            return snapshot;
        }

        internal static void Apply(
            QuestInfo offer,
            MissionOfferDescriptor descriptor,
            Snapshot original)
        {
            if (offer == null || descriptor == null || original == null || !HasMaterialChange(offer, original))
            {
                return;
            }

            QuestActionList destination = offer.QuestActions[0];
            string location = string.Format(
                CultureInfo.InvariantCulture,
                "{0:0.0}, {1:0.0} in {2}",
                destination.X,
                destination.Z,
                LocationName(destination.Playfield.Instance));
            string target = string.IsNullOrEmpty(descriptor.TargetName)
                                ? "the assigned target"
                                : descriptor.TargetName.TrimEnd('\0');

            offer.Info = BuildDescription(
                descriptor.Type,
                target,
                location,
                offer.CashReward,
                offer.ExperienceReward);
        }

        private static bool HasMaterialChange(QuestInfo offer, Snapshot original)
        {
            int playfield = 0;
            float x = 0;
            float z = 0;
            Identity objectiveTerminalReference = new Identity();
            if (offer.QuestActions != null && offer.QuestActions.Length > 0 && offer.QuestActions[0] != null)
            {
                playfield = offer.QuestActions[0].Playfield.Instance;
                x = offer.QuestActions[0].X;
                z = offer.QuestActions[0].Z;
                objectiveTerminalReference = offer.QuestActions[0].Unknown1;
            }

            int rewardLow = 0;
            int rewardHigh = 0;
            int rewardQuality = 0;
            if (offer.ItemRewards != null && offer.ItemRewards.Length > 0 && offer.ItemRewards[0] != null)
            {
                rewardLow = offer.ItemRewards[0].LowId;
                rewardHigh = offer.ItemRewards[0].HighId;
                rewardQuality = offer.ItemRewards[0].Quality;
            }

            return offer.MissionIconId != original.Icon
                   || offer.Quality != original.Quality
                   || offer.CashReward != original.Cash
                   || offer.ExperienceReward != original.Experience
                   || playfield != original.Playfield
                   || x != original.X
                   || z != original.Z
                   || rewardLow != original.RewardLow
                   || rewardHigh != original.RewardHigh
                   || rewardQuality != original.RewardQuality
                   || offer.Unknown5 != original.TerminalReferenceUnknown5
                   || offer.Unknown14 != original.TerminalReferenceUnknown14
                   || offer.Unknown23 != original.TerminalReferenceUnknown23
                   || objectiveTerminalReference != original.ObjectiveTerminalReference;
        }

        private static string BuildDescription(
            MissionRollType type,
            string target,
            string location,
            int credits,
            int experience)
        {
            string objective;
            switch (type)
            {
                case MissionRollType.KillPerson:
                    objective = "Go to " + location + " and terminate " + target + " within 48 hours.";
                    break;
                case MissionRollType.FindPerson:
                    objective = "Go to " + location + " and observe " + target
                                + " by selecting them within 48 hours.";
                    break;
                case MissionRollType.FindItem:
                    objective = "Go to " + location
                                + " and locate the assigned item within 48 hours. "
                                + "The objective is complete when you pick it up.";
                    break;
                case MissionRollType.FindItemReturn:
                    objective = "Go to " + location
                                + " and recover the assigned item, then bring it back to this mission "
                                + "terminal within 48 hours.";
                    break;
                case MissionRollType.RepairMachine:
                    objective = "Go to " + location
                                + " and repair the disabled machine with the supplied component within "
                                + "48 hours.";
                    break;
                default:
                    throw new ArgumentOutOfRangeException("type");
            }

            return string.Format(
                CultureInfo.InvariantCulture,
                "{0} Reward: {1} XP and {2} credits.",
                objective,
                experience,
                credits);
        }

        private static string LocationName(int playfieldId)
        {
            switch (playfieldId)
            {
                case 545:
                    return "West Athens";
                case 550:
                    return "Athen Shire";
                case 551:
                    return "Wailing Wastes";
                case 585:
                    return "Aegean";
                case 586:
                    return "Wartorn Valley";
                case 600:
                    return "Varmint Woods";
                case 635:
                    return "Stret East Bank";
                case 650:
                    return "Upper Stret East Bank";
                case 655:
                    return "Andromeda";
                case 665:
                    return "Broken Shores";
                case 670:
                    return "Clondyke";
                case 685:
                    return "Galway County";
                case 695:
                    return "Lush Fields";
                case 696:
                    return "Mutant Domain";
                case 710:
                    return "Omni-1 Trade";
                case 760:
                    return "4 Holes";
                case 790:
                    return "Stret West Bank";
                case 791:
                    return "Holes in the Wall";
                case 795:
                    return "The Longest Road";
                default:
                    return "playfield " + playfieldId.ToString(CultureInfo.InvariantCulture);
            }
        }
    }
}
