namespace ZoneEngine_New.Core.Missions;

using System;
using System.Collections.Generic;
using SmokeLounge.AOtomation.Messaging.GameData;
using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;
using Utility.GameData.Missions;
using ZoneEngine.Core.Missions;
using ZoneEngine_New.Core.Entities;

internal interface IGeneratedMissionOfferGenerator
{
    QuestAlternativeMessage Roll(QuestAlternativeMessage request, Identity character, int characterLevel,
        int terminalPlayfield, int clientClockSeconds, int seed, int nonce, Func<int> durableIds,
        MissionDestinationCatalog destinations, out IReadOnlyList<MissionPlacementIdentity> selectedEntrances);
}

/// <summary>Composes validated mission content before the DAO publishes and charges the roll.</summary>
internal sealed class GeneratedMissionRollService(MissionOfferCatalog offers) : IGeneratedMissionOfferGenerator
{
    static readonly Lazy<GeneratedMissionRollService> Current = new(() => new(MissionOfferCatalog.Load(MissionContentJson.RootPath)));

    internal static GeneratedMissionRollService Shared => Current.Value;

    internal MissionOfferCatalog OfferCatalog => offers;

    internal static QuestAlternativeMessage Generate(QuestAlternativeMessage request, Identity character, int characterLevel,
        int terminalPlayfield, float terminalX, float terminalZ, MissionLocationSide side,
        int clientClockSeconds, int seed, int nonce, Func<int> durableIds,
        MissionDestinationCatalog catalog, int breed, int profession,
        out IReadOnlyList<MissionPlacementIdentity> selectedEntrances)
        => Shared.Roll(request, character, characterLevel, terminalPlayfield, clientClockSeconds, seed, nonce, durableIds,
            catalog, out selectedEntrances);

    public QuestAlternativeMessage Roll(QuestAlternativeMessage request, Identity character, int characterLevel,
        int terminalPlayfield, int clientClockSeconds, int seed, int nonce, Func<int> durableIds,
        MissionDestinationCatalog destinations, out IReadOnlyList<MissionPlacementIdentity> selectedEntrances)
    {
        selectedEntrances = Array.Empty<MissionPlacementIdentity>();
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(durableIds);
        ArgumentNullException.ThrowIfNull(destinations);
        if (!MissionRollSliders.TryCreate(request, out var sliders, out var error)) throw new ArgumentException(error, nameof(request));
        int level = MissionLevelRuntime.ClampCharacterLevel(characterLevel);
        int quality = MissionLevelRuntime.GetMissionQuality(characterLevel, request.LevelSlider);
        // The catalog owns exact-QL selection and the optional nearest-QL origin policy.
        // The selected pool never changes mission QL; fallback eligibility is provisional.
        if (!destinations.TryGetDestinations(terminalPlayfield, quality, out var destinationPool, out _) || destinationPool.Count == 0)
            throw new NotSupportedException("No configured mission destinations are available at expected QL " + quality + ".");
        var random = new Random(seed);
        var policy = MissionRollPolicy.Current;
        var response = offers.CreateResponse(nonce, character, request.MissionTerminalIdentity);
        var types = MissionRollEvidenceCatalog.SelectTypeMix(level, request.LevelSlider, quality, sliders, random);
        if (types.Length != MissionOfferCatalog.OfferCount)
            throw new InvalidOperationException("A mission offer cohort must contain five entries.");
        var identities = new HashSet<int>();
        var entrances = new List<MissionPlacementIdentity>(types.Length);
        response.QuestInfos = new QuestInfo[types.Length];
        for (int index = 0; index < types.Length; index++)
        {
            var type = types[index];
            var eligible = offers.Select(type, random);
            var offer = eligible.Definition.Create();
            int identity = durableIds();
            if (identity <= 0 || !identities.Add(identity)) throw new InvalidOperationException("Durable mission identities must be positive and unique.");
            offer.QuestIdentity = new() { Type = IdentityType.Mission, Instance = identity };
            Identity Retarget(Identity value) => (int)value.Type == MissionTerminal.LiveIdentityType && value.Instance == eligible.SourceTerminal.Instance
                ? request.MissionTerminalIdentity : value;
            offer.Unknown5 = Retarget(offer.Unknown5);
            offer.Unknown14 = Retarget(offer.Unknown14);
            offer.Unknown23 = Retarget(offer.Unknown23);
            var action = offer.QuestActions[0];
            action.Unknown1 = Retarget(action.Unknown1);
            action.UnknownHash15 = checked(clientClockSeconds + policy.OfferLifetimeSeconds);
            // Uniform selection of distinct observed identities, with replacement. Observation
            // counts are evidence coverage, never retail probability weights.
            var destination = destinationPool[random.Next(destinationPool.Count)];
            if (!destinations.TryGetWorldPosition(destination.Identity, out var worldPosition))
                throw new InvalidOperationException("The selected entrance has no proven WorldPos offsets.");
            entrances.Add(destination.Identity);
            action.Playfield = new() { Type = (IdentityType)worldPosition.PlayfieldIdentityType, Instance = destination.PlayfieldId };
            action.X = destination.LocalX; action.Y = destination.LocalY; action.Z = destination.LocalZ;
            action.Unknown18 = worldPosition.WorldOffsetX; action.Unknown19 = worldPosition.WorldOffsetZ;
            offer.Quality = quality;
            MissionRewardEvidenceModel.Apply(offer, type, level, request.LevelSlider, quality, sliders, destination.PlayfieldId, random);
            var reward = MissionRollRewardItems.Select(quality, random);
            reward.Unknown1 = offer.ItemRewards[0].Unknown1;
            offer.ItemRewards[0] = reward;
            MissionOfferTextBuilder.Apply(offer, eligible.Descriptor, eligible.Text);
            if (!MissionOfferCompatibility.TryValidateGenerated(offer, eligible.Descriptor, sliders, request.MissionTerminalIdentity, out var descriptor, out var failure)
                || descriptor.Type != type) throw new InvalidOperationException("Invalid composed mission: " + failure);
            if (offer.Info is { } text && !text.EndsWith('\0')) offer.Info = text + '\0';
            response.QuestInfos[index] = offer;
        }
        selectedEntrances = entrances.AsReadOnly();
        return response;
    }

    internal static string DestinationEvidenceType(MissionRollType type) => type switch
    {
        MissionRollType.KillPerson => "KILL_PERSON",
        MissionRollType.FindPerson => "FIND_PERSON",
        MissionRollType.FindItem => "FIND_ITEM",
        MissionRollType.RepairMachine => "REPAIR",
        MissionRollType.FindItemReturn => "RETURN_ITEM",
        _ => throw new NotSupportedException("Unsupported mission destination type.")
    };
}
