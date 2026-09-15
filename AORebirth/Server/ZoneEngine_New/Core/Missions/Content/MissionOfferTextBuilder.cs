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
            var templates = MissionTextContent.Current;
            if (!templates.Objectives.TryGetValue((int)type, out var objective)) throw new ArgumentOutOfRangeException(nameof(type));
            return string.Format(CultureInfo.InvariantCulture, templates.RewardFormat,
                string.Format(CultureInfo.InvariantCulture, objective, location, target), experience, credits);
        }

        private static string LocationName(int playfieldId)
        {
            return MissionGeographyContent.Current.Names.TryGetValue(playfieldId, out var name) ? name : "playfield " + playfieldId.ToString(CultureInfo.InvariantCulture);
        }
    }
}
