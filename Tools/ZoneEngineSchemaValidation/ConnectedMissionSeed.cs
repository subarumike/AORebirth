using System.Security.Cryptography;
using AORebirth.Database.Domain.Missions;
using AORebirth.Interfaces.Persistence.Missions;
using ZoneEngine_New.Core.Data;

/// <summary>Synthetic saved-state fixture, never a runtime mission generator or packet oracle.</summary>
static class ConnectedMissionSeed
{
    public static GeneratedMissionBinding Create(DisposableSchemaDatabase fixture, int owner)
    {
        var dao = new MySqlMissionDao(() => fixture.Open());
        int offerId = dao.ReserveIdentities("offer", 1);
        int quest = dao.ReserveIdentities("quest", 1);
        int playfield = dao.ReserveIdentities("playfield", 1);
        int key = new MySqlInventoryRepository(new SilentLogger()).LeaseInstanceIdBlock(1);
        long now = DateTime.UtcNow.Ticks, expires = now + TimeSpan.TicksPerHour * 48;
        byte[] opaqueStoredBody = [1, 2, 3, 4];
        string hash = Convert.ToHexString(SHA256.HashData(opaqueStoredBody)).ToLowerInvariant();
        var offer = new GeneratedMissionOffer
        {
            OwnerId = owner, BatchIdentity = "saved-mission-integrity", OfferIndex = 0,
            OfferType = 0xDAC3, OfferInstance = offerId, MissionType = 0, Quality = 1,
            DestinationType = 0xC9C6, DestinationInstance = 1, DestinationPlayfield = 710,
            DestinationX = 500, DestinationY = 5, DestinationZ = 500,
            EntranceType = 0xC9C6, EntranceInstance = 1, EntranceLow = 1, EntranceHigh = 1,
            CashReward = 50, ExperienceReward = 100, RewardLowId = 99, RewardHighId = 100,
            RewardQuality = 1, RewardCount = 1, Title = "Saved-state fixture",
            Description = "Synthetic persistence data; no mission gameplay activation.",
            FrozenWireBody = opaqueStoredBody, FrozenWireSha256 = hash,
            State = GeneratedMissionState.Offered, OfferedAtUtcTicks = now, ExpiresAtUtcTicks = expires, Version = 1
        };
        var batch = new GeneratedMissionOfferBatch
        {
            OwnerId = owner, OwnerType = 50000, BatchIdentity = offer.BatchIdentity,
            Fee = 0, CurrentCash = 1234, TerminalType = 0xDAC1, TerminalInstance = 12345,
            TerminalPlayfield = 710, RollSeed = 1234, ResponseNonce = 4567,
            OfferedAtUtcTicks = now, ExpiresAtUtcTicks = expires, Offers = [offer]
        };
        if (dao.PublishOffers(batch).Status != GeneratedMissionResultStatus.Applied)
            throw new FixtureFailure("connected-saved-offer-seed");
        var acceptance = new GeneratedMissionAcceptance
        {
            OwnerId = owner, OfferType = offer.OfferType, OfferInstance = offerId,
            QuestType = 0xDAC3, QuestInstance = quest, KeyInstance = key,
            BundleId = "synthetic-saved-state", BundleSha256 = hash,
            BuildingType = 1, BuildingInstance = 1, LivePlayfield = playfield,
            ObjectiveType = 50000, ObjectiveInstance = 1, ObjectiveTemplateId = 1,
            ObjectiveInteraction = 1, RequiredCount = 1, AcceptedAtUtcTicks = now, ExpiresAtUtcTicks = expires,
            Objects = [new GeneratedMissionObject
            {
                OwnerId = owner, QuestType = 0xDAC3, QuestInstance = quest,
                RuntimeType = 50000, RuntimeInstance = 1, CapturedType = 50000, CapturedInstance = 1,
                Kind = 1, TemplateId = 1, HeadingW = 1, Version = 1, UpdatedAtUtcTicks = now
            }],
            Artifacts = [new MissionItemInstanceData
            {
                InstanceId = key, ContainerType = 104, ContainerInstance = owner, ContainerPlacement = 67,
                ItemType = 0xC76D, LowId = 28577, HighId = 28577, Quality = 1, StackCount = 1, Source = 0
            }]
        };
        var accepted = dao.Accept(acceptance);
        if (accepted.Status != GeneratedMissionResultStatus.Applied)
            throw new FixtureFailure("connected-saved-binding-seed");
        return accepted.Binding;
    }
}
