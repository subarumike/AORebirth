namespace ZoneEngine_New.Core.Missions
{
    using System;
    using System.Collections.Generic;
    using System.Security.Cryptography;
    using AORebirth.Interfaces.Persistence.Missions;
    using SmokeLounge.AOtomation.Messaging.GameData;
    using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;
    using ZoneEngine.Core.Missions;

    /// <summary>Typed SQL projection of the accepted generator's actual output, never client-authored offers.</summary>
    internal static class GeneratedMissionRollProjection
    {
        internal static GeneratedMissionOfferBatch Create(QuestAlternativeMessage request,
            QuestAlternativeMessage response, int playfield, int fee, int seed, int nonce, DateTime issuedUtc)
        {
            if (response.QuestInfos == null || response.QuestInfos.Length != 5)
                throw new InvalidOperationException("The accepted mission generator must supply exactly five offers.");
            if (!MissionSliderProfile.TryCreate(request, out var sliders, out string error))
                throw new ArgumentException(error, nameof(request));
            byte[] wire = MissionRollService.SerializeBody(response);
            string wireHash = Convert.ToHexStringLower(SHA256.HashData(wire));
            string batchIdentity = Guid.NewGuid().ToString("N");
            long expires = issuedUtc.AddHours(48).Ticks;
            var offers = new List<GeneratedMissionOffer>();
            for (int index = 0; index < response.QuestInfos.Length; index++)
            {
                QuestInfo offer = response.QuestInfos[index];
                if (!MissionOfferCompatibility.TryDescribe(offer, out var description, out string failure)
                    || offer.QuestActions == null || offer.QuestActions.Length != 1
                    || offer.QuestActions[0] == null || offer.ItemRewards?.Length > 1)
                    throw new InvalidOperationException("Unsupported generated offer projection: " + failure);
                QuestActionList destination = offer.QuestActions[0];
                QuestItemShort? reward = offer.ItemRewards?.Length == 1 ? offer.ItemRewards[0] : null;
                offers.Add(new GeneratedMissionOffer
                {
                    OwnerId = response.Identity.Instance, BatchIdentity = batchIdentity, OfferIndex = index,
                    OfferType = (int)offer.QuestIdentity.Type, OfferInstance = offer.QuestIdentity.Instance,
                    MissionType = (int)description.Type, Quality = offer.Quality,
                    DestinationType = (int)destination.Playfield.Type, DestinationInstance = destination.Playfield.Instance,
                    DestinationPlayfield = destination.Playfield.Instance,
                    DestinationX = destination.X, DestinationY = destination.Y, DestinationZ = destination.Z,
                    // Legacy's frozen ExteriorEntranceIdentity is this exact Playfield identity;
                    // the building low/high pair is separate, not a guessed resource-to-door mapping.
                    EntranceType = (int)destination.Playfield.Type, EntranceInstance = destination.Playfield.Instance,
                    EntranceLow = destination.Unknown18, EntranceHigh = destination.Unknown19,
                    CashReward = offer.CashReward, ExperienceReward = offer.ExperienceReward,
                    RewardLowId = reward?.LowId ?? 0, RewardHighId = reward?.HighId ?? 0,
                    RewardQuality = reward?.Quality ?? 0, RewardCount = reward == null ? 0 : 1,
                    Title = offer.ShortInfo ?? string.Empty, Description = offer.Info ?? string.Empty,
                    FrozenWireBody = (byte[])wire.Clone(), FrozenWireSha256 = wireHash,
                    OfferedAtUtcTicks = issuedUtc.Ticks, ExpiresAtUtcTicks = expires,
                    State = GeneratedMissionState.Offered, Version = 1
                });
            }
            return new GeneratedMissionOfferBatch
            {
                OwnerType = (int)response.Identity.Type, OwnerId = response.Identity.Instance,
                BatchIdentity = batchIdentity, RollSeed = seed, ResponseNonce = nonce, Fee = fee,
                TerminalType = (int)response.MissionTerminalIdentity.Type, TerminalInstance = response.MissionTerminalIdentity.Instance,
                TerminalPlayfield = playfield, LevelSlider = request.LevelSlider,
                GoodBadSlider = sliders.GoodBad, OrderChaosSlider = sliders.OrderChaos, OpenHiddenSlider = sliders.OpenHidden,
                PhysicalMysticalSlider = sliders.PhysicalMystical, HeadOnStealthSlider = sliders.HeadOnStealth,
                MoneyExperienceSlider = sliders.MoneyExperience,
                OfferedAtUtcTicks = issuedUtc.Ticks, ExpiresAtUtcTicks = expires, Offers = offers
            };
        }
    }
}
