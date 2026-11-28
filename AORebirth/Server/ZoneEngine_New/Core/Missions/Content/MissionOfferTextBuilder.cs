using ZoneEngine_New.Core.Missions;
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
        internal sealed record Snapshot(int Icon, int Quality, int Cash, int Experience,
            int Playfield, float X, float Z, int RewardLow, int RewardHigh, int RewardQuality,
            Identity TerminalReferenceUnknown5, Identity TerminalReferenceUnknown14,
            Identity TerminalReferenceUnknown23, Identity ObjectiveTerminalReference);

        internal static Snapshot Capture(QuestInfo offer)
        {
            var action = offer.QuestActions is { Length: > 0 } ? offer.QuestActions[0] : null;
            var reward = offer.ItemRewards is { Length: > 0 } ? offer.ItemRewards[0] : null;
            return new Snapshot(offer.MissionIconId, offer.Quality, offer.CashReward, offer.ExperienceReward,
                action?.Playfield.Instance ?? 0, action?.X ?? 0, action?.Z ?? 0, reward?.LowId ?? 0,
                reward?.HighId ?? 0, reward?.Quality ?? 0, offer.Unknown5, offer.Unknown14, offer.Unknown23,
                action?.Unknown1 ?? default);
        }

        internal static void Apply(
            QuestInfo offer,
            MissionOfferDescriptor descriptor,
            Snapshot original,
            MissionTextContent? text = null,
            MissionGeographyContent? geography = null)
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
                LocationName(destination.Playfield.Instance, geography ?? MissionGeographyContent.Current));
            string target = string.IsNullOrEmpty(descriptor.TargetName)
                                ? "the assigned target"
                                : descriptor.TargetName.TrimEnd('\0');

            offer.Info = BuildDescription(
                descriptor.Type,
                target,
                location,
                offer.CashReward,
                offer.ExperienceReward,
                text ?? MissionTextContent.Current);
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
            int experience,
            MissionTextContent templates)
        {
            if (!templates.Objectives.TryGetValue((int)type, out var objective)) throw new ArgumentOutOfRangeException(nameof(type));
            return string.Format(CultureInfo.InvariantCulture, templates.RewardFormat,
                string.Format(CultureInfo.InvariantCulture, objective, location, target), experience, credits);
        }

        private static string LocationName(int playfieldId, MissionGeographyContent geography)
        {
            return geography.Names.TryGetValue(playfieldId, out var name) ? name : "playfield " + playfieldId.ToString(CultureInfo.InvariantCulture);
        }
    }
}
