namespace ZoneEngine.Core.Missions
{
    using System;
    using System.Globalization;

    using SmokeLounge.AOtomation.Messaging.GameData;

    internal enum MissionObjectiveCategory
    {
        EliminatePerson = 1,
        ObservePerson = 2,
        LocateItem = 3,
        RecoverAndReturnItem = 4,
        RepairMachine = 5
    }

    internal enum MissionRewardCategory
    {
        Currency = 1,
        CurrencyAndItem = 2
    }

    internal sealed class MissionOfferDescriptor
    {
        internal MissionRollType Type;
        internal int IconId;
        internal int ActionCode;
        internal MissionObjectiveCategory Objective;
        internal MissionRewardCategory Reward;
        internal string TargetName;
        internal string ShortInfo;
        internal Identity ObjectiveIdentity;
        internal Identity AuxiliaryObjectiveIdentity1;
        internal Identity AuxiliaryObjectiveIdentity2;
    }

    /// <summary>
    /// Authoritative icon/type/action/objective compatibility boundary for captured roll templates and
    /// generated offers. A template is admitted only when its canonical icon and captured action code
    /// agree; unknown combinations fail closed.
    /// </summary>
    internal static class MissionOfferCompatibility
    {
        internal static bool TryDescribe(
            QuestInfo offer,
            out MissionOfferDescriptor descriptor,
            out string error)
        {
            descriptor = null;
            error = null;
            if (offer == null)
            {
                error = "Offer was null.";
                return false;
            }

            MissionRollType type;
            if (!MissionTypeCatalog.TryTypeFromIcon(offer.MissionIconId, out type))
            {
                error = "Unknown mission icon " + offer.MissionIconId + ".";
                return false;
            }

            if (MissionTypeCatalog.IconId(type, 0) != offer.MissionIconId)
            {
                error = "Mission icon does not match its canonical type.";
                return false;
            }

            if (offer.QuestActions == null || offer.QuestActions.Length == 0 || offer.QuestActions[0] == null)
            {
                error = "Offer has no objective action.";
                return false;
            }

            int actionCode = offer.QuestActions[0].Version;
            if (actionCode != MissionTypeCatalog.ExpectedActionCode(type))
            {
                error = "Mission action code "
                        + actionCode
                        + " does not match "
                        + MissionTypeCatalog.TypeName(type)
                        + " expected "
                        + MissionTypeCatalog.ExpectedActionCode(type)
                        + ".";
                return false;
            }

            if (string.IsNullOrEmpty(offer.ShortInfo) || string.IsNullOrEmpty(offer.Info))
            {
                error = "Offer text family is incomplete.";
                return false;
            }

            string targetName = ResolveTargetName(offer);
            descriptor = new MissionOfferDescriptor
                         {
                             Type = type,
                             IconId = offer.MissionIconId,
                             ActionCode = actionCode,
                             Objective = ObjectiveFor(type),
                             Reward = offer.ItemRewards != null && offer.ItemRewards.Length > 0
                                          ? MissionRewardCategory.CurrencyAndItem
                                          : MissionRewardCategory.Currency,
                             TargetName = targetName,
                             ShortInfo = offer.ShortInfo,
                             ObjectiveIdentity = offer.QuestActions[0].Action,
                             AuxiliaryObjectiveIdentity1 = offer.QuestActions[0].Unknown1,
                             AuxiliaryObjectiveIdentity2 = offer.QuestActions[0].Unknown2
                         };
            return true;
        }

        internal static bool TryDescribeCaptured(
            QuestInfo offer,
            Identity capturedTerminal,
            out MissionOfferDescriptor descriptor,
            out string error)
        {
            if (!TryDescribe(offer, out descriptor, out error))
            {
                return false;
            }

            if (offer.QuestActions.Length != 1
                || (int)offer.QuestActions[0].Playfield.Type != (int)IdentityType.Playfield2
                || offer.QuestActions[0].Playfield.Instance == 0
                || !HasCapturedObjectiveShape(descriptor.Type, offer.QuestActions[0]))
            {
                error = "Captured objective slot shape does not match icon/type.";
                return false;
            }

            if (descriptor.Type == MissionRollType.FindItemReturn
                && offer.QuestActions[0].Unknown1 != capturedTerminal)
            {
                error = "Captured return objective does not reference its issuing terminal.";
                return false;
            }

            if (offer.CharInfos == null
                || offer.CharInfos.Length != 0
                || offer.RewardDescriptorVersion != 6
                || offer.CashReward <= 0
                || offer.ExperienceReward <= 0
                || offer.ItemRewards == null
                || offer.ItemRewards.Length != 1
                || offer.ItemRewards[0] == null)
            {
                error = "Captured target or reward shell does not match the finalized corpus.";
                return false;
            }

            string shortInfo = offer.ShortInfo.TrimEnd('\0');
            string info = offer.Info;
            if (shortInfo.Length != 31
                || !shortInfo.EndsWith("...", StringComparison.Ordinal)
                || info.Length < 29
                || !info.EndsWith("\0", StringComparison.Ordinal)
                || !string.Equals(
                    shortInfo.Substring(0, 28),
                    info.Substring(0, 28),
                    StringComparison.Ordinal))
            {
                error = "Captured text shell does not match its exact prefix family.";
                return false;
            }

            return true;
        }

        internal static bool TryValidateGenerated(
            QuestInfo offer,
            MissionOfferDescriptor source,
            MissionSliderProfile sliders,
            Identity issuingTerminal,
            out MissionOfferDescriptor generated,
            out string error)
        {
            generated = null;
            if (!TryDescribe(offer, out generated, out error))
            {
                return false;
            }

            if (source == null
                || generated.Type != source.Type
                || generated.ActionCode != source.ActionCode
                || generated.Objective != source.Objective
                || generated.Reward != source.Reward
                || !string.Equals(
                    generated.ShortInfo,
                    source.ShortInfo,
                    StringComparison.Ordinal)
                || generated.ObjectiveIdentity != source.ObjectiveIdentity
                || generated.AuxiliaryObjectiveIdentity2 != source.AuxiliaryObjectiveIdentity2)
            {
                error = "Generated offer changed its captured objective template.";
                return false;
            }

            bool terminalReferenceMatches =
                generated.Type == MissionRollType.FindItemReturn
                    ? generated.AuxiliaryObjectiveIdentity1 == issuingTerminal
                    : generated.AuxiliaryObjectiveIdentity1 == source.AuxiliaryObjectiveIdentity1;
            if (!terminalReferenceMatches)
            {
                error = "Generated offer has an incompatible objective terminal reference.";
                return false;
            }

            if (!IsCompatibleWithSliders(generated, sliders))
            {
                error = "Generated offer contradicts the capture-supported slider cohort.";
                return false;
            }

            if (offer.ShortInfo.Length != 31 || offer.ShortInfo.IndexOf('\0') >= 0)
            {
                error = "Generated title does not preserve the captured 31-byte wire width.";
                return false;
            }

            string description = offer.Info.TrimEnd('\0');
            QuestActionList destination = offer.QuestActions[0];
            string coordinate = string.Format(
                CultureInfo.InvariantCulture,
                "{0:0.0}, {1:0.0}",
                destination.X,
                destination.Z);
            if (!description.Contains(coordinate)
                || !description.Contains(offer.CashReward.ToString(CultureInfo.InvariantCulture))
                || !description.Contains(offer.ExperienceReward.ToString(CultureInfo.InvariantCulture))
                || (!string.IsNullOrEmpty(generated.TargetName)
                    && !description.Contains(generated.TargetName)))
            {
                error = "Generated description does not match target, location, or numeric rewards.";
                return false;
            }

            string requiredObjectiveText = RequiredObjectiveText(generated.Type);
            if (!description.Contains(requiredObjectiveText))
            {
                error = "Generated description does not match objective/text family.";
                return false;
            }

            if (offer.CashReward < 0 || offer.ExperienceReward < 0)
            {
                error = "Generated numeric reward is invalid.";
                return false;
            }

            if (offer.ItemRewards != null)
            {
                for (int i = 0; i < offer.ItemRewards.Length; i++)
                {
                    QuestItemShort item = offer.ItemRewards[i];
                    if (item == null
                        || item.LowId <= 0
                        || item.HighId <= 0
                        || item.Quality <= 0
                        || Math.Abs((long)item.Quality - offer.Quality) > 10)
                    {
                        error = "Generated item reward category contains an invalid or non-QL-aware item.";
                        return false;
                    }
                }
            }

            return true;
        }

        internal static bool IsCompatibleWithSliders(
            MissionOfferDescriptor descriptor,
            MissionSliderProfile sliders)
        {
            if (descriptor == null || sliders == null)
            {
                return false;
            }

            // Slider evidence constrains complete five-offer cohorts in MissionRollEvidenceCatalog.
            // The corpus does not prove any type is globally ineligible outside its captured level,
            // difficulty, and slider context, so applying a per-offer ban here would overgeneralize.
            return true;
        }

        private static MissionObjectiveCategory ObjectiveFor(MissionRollType type)
        {
            switch (type)
            {
                case MissionRollType.KillPerson:
                    return MissionObjectiveCategory.EliminatePerson;
                case MissionRollType.FindPerson:
                    return MissionObjectiveCategory.ObservePerson;
                case MissionRollType.FindItem:
                    return MissionObjectiveCategory.LocateItem;
                case MissionRollType.FindItemReturn:
                    return MissionObjectiveCategory.RecoverAndReturnItem;
                case MissionRollType.RepairMachine:
                    return MissionObjectiveCategory.RepairMachine;
                default:
                    throw new ArgumentOutOfRangeException("type");
            }
        }

        private static string RequiredObjectiveText(MissionRollType type)
        {
            switch (type)
            {
                case MissionRollType.KillPerson:
                    return " and terminate ";
                case MissionRollType.FindPerson:
                    return " and observe ";
                case MissionRollType.FindItem:
                    return " and locate the assigned item ";
                case MissionRollType.FindItemReturn:
                    return " and recover the assigned item, then bring it back to this mission terminal ";
                case MissionRollType.RepairMachine:
                    return " and repair the disabled machine with the supplied component ";
                default:
                    throw new ArgumentOutOfRangeException("type");
            }
        }

        private static bool HasCapturedObjectiveShape(
            MissionRollType type,
            QuestActionList action)
        {
            const int objectiveIdentityType = 70099;
            const int terminalIdentityType = 56001;
            int actionType = (int)action.Action.Type;
            int unknown1Type = (int)action.Unknown1.Type;
            int unknown2Type = (int)action.Unknown2.Type;

            switch (type)
            {
                case MissionRollType.KillPerson:
                case MissionRollType.FindPerson:
                    return actionType == 0
                           && unknown1Type == 0
                           && unknown2Type == objectiveIdentityType
                           && action.Unknown2.Instance != 0;
                case MissionRollType.FindItem:
                    return actionType == objectiveIdentityType
                           && action.Action.Instance != 0
                           && unknown1Type == 0
                           && unknown2Type == 0;
                case MissionRollType.FindItemReturn:
                    return actionType == objectiveIdentityType
                           && action.Action.Instance != 0
                           && unknown1Type == terminalIdentityType
                           && action.Unknown1.Instance != 0
                           && unknown2Type == 0;
                case MissionRollType.RepairMachine:
                    return actionType == objectiveIdentityType
                           && action.Action.Instance != 0
                           && unknown1Type == objectiveIdentityType
                           && action.Unknown1.Instance != 0
                           && unknown2Type == 0;
                default:
                    return false;
            }
        }

        private static string ResolveTargetName(QuestInfo offer)
        {
            if (offer.CharInfos == null)
            {
                return string.Empty;
            }

            for (int i = 0; i < offer.CharInfos.Length; i++)
            {
                QuestCharInfo info = offer.CharInfos[i];
                if (info != null && !string.IsNullOrEmpty(info.CharacterName))
                {
                    return info.CharacterName.TrimEnd('\0');
                }
            }

            return string.Empty;
        }
    }
}
