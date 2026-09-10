using AORebirth.Database.Domain.Missions;
using AORebirth.Interfaces.Persistence.Missions;
using SmokeLounge.AOtomation.Messaging.GameData;
using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;
using ZoneEngine.Core.Missions;
using ZoneEngine_New.Core.Data;
using ZoneEngine_New.Core.Missions;

/// <summary>Administrative pre-start fixture only. Uses accepted generator, captured bundles and production DAO.</summary>
static class ConnectedMissionSeed
{
    public static GeneratedMissionBinding Create(DisposableSchemaDatabase fixture, int owner)
    {
        var dao = new MySqlMissionDao(() => fixture.Open());
        var character = new Identity { Type = IdentityType.CanbeAffected, Instance = owner };
        var request = new QuestAlternativeMessage { Identity = character, LevelSlider = 1,
            MissionTerminalIdentity = new Identity { Type = (IdentityType)0xDAC1, Instance = 12345 }, QuestInfos = [] };
        int offerId = dao.ReserveIdentities("offer", 5);
        var response = MissionRollService.BuildRollResponseDeterministic(request, character, 60, 710,
            500, 500, MissionLocationSide.Omni, 1234, 4567, offerId, 1201445827);
        DateTime now = DateTime.UtcNow;
        var batch = GeneratedMissionRollProjection.Create(request, response, 710, 0, 1234, 4567, now);
        batch.CurrentCash = 1234;
        if (dao.PublishOffers(batch).Status != GeneratedMissionResultStatus.Applied)
            throw new FixtureFailure("connected-generated-offer-seed");
        var offer = dao.ReadOffers(owner).First(o => o.MissionType != (int)MissionRollType.RepairMachine);
        int seed;
        unchecked { seed = 17; foreach (int value in new[] { offer.OfferType, offer.OfferInstance, 50000, owner, offer.MissionType, offer.Quality }) seed = seed * 31 + value; }
        var catalog = MissionAcgLayoutCatalogLoader.Load(MissionAcgCapturedLayoutCatalog.CreateBundles(), []);
        var bundle = MissionAcgLayoutSelector.Select(catalog, new MissionAcgSelectionInput(seed,
            (MissionRollType)offer.MissionType, offer.Quality, new(50000, owner)));
        int quest = dao.ReserveIdentities("quest", 1), pf = dao.ReserveIdentities("playfield", 1);
        int key = new MySqlInventoryRepository(new SilentLogger()).LeaseInstanceIdBlock(1);
        var immutable = new MissionAcgInstanceBinding(MissionAcgInstanceBinding.CurrentFormatVersion,
            new(0xDAC3, quest), new(offer.OfferType, offer.OfferInstance), new(50000, owner), null,
            (MissionRollType)offer.MissionType, offer.Quality, seed, new(0xC76D, key),
            new(offer.EntranceType, offer.EntranceInstance), offer.EntranceLow, offer.EntranceHigh,
            offer.DestinationX, offer.DestinationY, offer.DestinationZ,
            new(offer.IssuingTerminalType, offer.IssuingTerminalInstance), bundle.LayoutId,
            bundle.GeneratorPayloadSha256, bundle.BuildingIdentity, pf, now, now.AddHours(48), true);
        var record = new MissionAcgBindingRecord(immutable,
            new MissionAcgInstanceState(MissionAcgLifecycleState.Active, MissionAcgCleanupState.None, now, null), string.Empty);
        if (!MissionAcgRuntimeMaterializer.TryMaterialize(record, bundle, null, now, out var instance, out string failure))
            throw new FixtureFailure("connected-generated-materialization-" + failure);
        var objects = instance.Objects.Select(source =>
        {
            var state = new GeneratedMissionObject { OwnerId = owner, QuestType = 0xDAC3, QuestInstance = quest,
                RuntimeType = source.Identity.RuntimeIdentity.Type, RuntimeInstance = source.Identity.RuntimeIdentity.Instance,
                CapturedType = source.Identity.CapturedIdentity.Type, CapturedInstance = source.Identity.CapturedIdentity.Instance,
                Kind = (int)source.Identity.Kind, TemplateId = source.TemplateId,
                X = source.Position.X, Y = source.Position.Y, Z = source.Position.Z,
                HeadingX = source.Heading.X, HeadingY = source.Heading.Y, HeadingZ = source.Heading.Z, HeadingW = source.Heading.W,
                Version = 1, UpdatedAtUtcTicks = now.Ticks };
            if (source.Identity.Kind is MissionAcgRuntimeObjectKind.ObjectiveNpc or MissionAcgRuntimeObjectKind.AmbientNpc)
            {
                var evidence = new GeneratedMissionNpcEvidence(source, bundle, offer.Quality, offer.MissionType);
                var random = new Random(unchecked(seed ^ evidence.CapturedSlot * 397 ^ evidence.CapturedInstance));
                state.Level = MissionNpcDifficultyPolicy.ResolveLevel(offer.Quality, random);
                state.MaxHealth = MissionNpcDifficultyPolicy.ResolveHealth(state.Level.Value, random);
                state.CurrentHealth = state.MaxHealth;
            }
            return state;
        }).ToList();
        var slot = bundle.ObjectiveSlots.Single();
        var objective = instance.Objects.Single(o => o.Identity.CapturedIdentity.Equals(slot.CapturedIdentity));
        var acceptance = new GeneratedMissionAcceptance { OwnerId = owner, OfferType = offer.OfferType,
            OfferInstance = offer.OfferInstance, QuestType = 0xDAC3, QuestInstance = quest, KeyInstance = key,
            BundleId = bundle.LayoutId, BundleSha256 = GeneratedMissionAcgService.CanonicalBundleHash(bundle),
            BuildingType = bundle.BuildingIdentity.Type, BuildingInstance = bundle.BuildingIdentity.Instance, LivePlayfield = pf,
            ObjectiveType = objective.Identity.RuntimeIdentity.Type, ObjectiveInstance = objective.Identity.RuntimeIdentity.Instance,
            ObjectiveTemplateId = slot.TemplateId, ObjectiveInteraction = (int)MissionAcgObjectiveContract.InteractionFor((MissionRollType)offer.MissionType),
            RequiredCount = 1, AcceptedAtUtcTicks = now.Ticks, ExpiresAtUtcTicks = now.AddHours(48).Ticks, Objects = objects,
            Artifacts = [new MissionItemInstanceData { InstanceId = key, ContainerType = 104, ContainerInstance = owner,
                ContainerPlacement = 67, ItemType = 0xC76D, LowId = 28577, HighId = 28577, Quality = 1, StackCount = 1, Source = 0 }] };
        Console.WriteLine("GENERATED_SEED=" + System.Text.Json.JsonSerializer.Serialize(new { acceptance.OwnerId, acceptance.OfferType, acceptance.OfferInstance,
            acceptance.QuestType, acceptance.QuestInstance, acceptance.KeyInstance, acceptance.BundleId, acceptance.BundleSha256, acceptance.BuildingType,
            acceptance.BuildingInstance, acceptance.LivePlayfield, acceptance.ObjectiveType, acceptance.ObjectiveInstance, acceptance.ObjectiveTemplateId,
            acceptance.ObjectiveInteraction, Duration = acceptance.ExpiresAtUtcTicks - acceptance.AcceptedAtUtcTicks,
            ObjectiveTemplate = objective.TemplateId, Objects = objects.Count }));
        var accepted = dao.Accept(acceptance);
        if (accepted.Status != GeneratedMissionResultStatus.Applied) throw new FixtureFailure("connected-generated-accept-seed");
        return accepted.Binding;
    }
}
