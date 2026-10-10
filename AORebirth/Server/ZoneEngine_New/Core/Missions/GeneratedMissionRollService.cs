namespace ZoneEngine_New.Core.Missions;

using System;
using System.Collections.Generic;
using System.Linq;
using SmokeLounge.AOtomation.Messaging.GameData;
using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;
using Utility.GameData.Missions;
using ZoneEngine.Core.Missions;
using ZoneEngine_New.Core.Entities;

/// <summary>Composes editable mission content into a five-offer packet before the DAO publishes and charges it.</summary>
internal static class GeneratedMissionRollService
{
    sealed record Shell(byte[] Body, int Index, Identity Terminal, MissionOfferDescriptor Description);

    internal static QuestAlternativeMessage Generate(QuestAlternativeMessage request, Identity character, int characterLevel,
        int terminalPlayfield, float terminalX, float terminalZ, MissionLocationSide side,
        int clientClockSeconds, int seed, int nonce, Func<int> durableIds,
        MissionDestinationCatalog catalog, int breed, int profession,
        out IReadOnlyList<MissionPlacementIdentity> selectedEntrances)
    {
        selectedEntrances = Array.Empty<MissionPlacementIdentity>();
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(durableIds);
        ArgumentNullException.ThrowIfNull(catalog);
        if (!MissionRollSliders.TryCreate(request, out var sliders, out var error)) throw new ArgumentException(error, nameof(request));
        int level = MissionLevelRuntime.ClampCharacterLevel(characterLevel);
        int quality = MissionLevelRuntime.GetMissionQuality(characterLevel, request.LevelSlider);
        var random = new Random(seed);
        var policy = MissionRollPolicy.Current;
        var response = GeneratedMissionWire.Read(GeneratedMissionWire.CapturedBody((int)((uint)nonce % (uint)GeneratedMissionWire.CapturedCount)));
        response.Identity = character;
        response.MissionTerminalIdentity = request.MissionTerminalIdentity;
        var types = MissionRollEvidenceCatalog.SelectTypeMix(level, request.LevelSlider, quality, sliders, random);
        if (types.Length != 5) throw new InvalidOperationException("A mission offer cohort must contain five entries.");
        // Each complete captured condition is its own evidence population. No nearest-QL,
        // faction, terminal, slider or mission-type fallback is permitted for destinations.
        var destinationPools = types.Distinct().ToDictionary(type => type, type =>
        {
            var condition = new MissionDestinationCondition(characterLevel, quality, request.LevelSlider,
                (int)side, breed, profession, terminalPlayfield,
                unchecked((uint)request.MissionTerminalIdentity.Type), unchecked((uint)request.MissionTerminalIdentity.Instance),
                [request.GoodBadSlider, request.OrderChaosSlider, request.OpenHiddenSlider,
                 request.PhysicalMysticalSlider, request.HeadOnStealthSlider, request.MoneyExperienceSlider],
                DestinationEvidenceType(type));
            if (!catalog.TryGetObservedDestinations(condition, out var placements) || placements.Count == 0)
                throw new NotSupportedException("No captured mission destinations support this complete roll condition.");
            return placements;
        });
        var shells = ReadShells();
        var identities = new HashSet<int>();
        var entrances = new List<MissionPlacementIdentity>(5);
        response.QuestInfos = types.Select(type =>
        {
            var candidates = shells.Where(s => s.Description.Type == type && MissionOfferCompatibility.IsCompatibleWithSliders(s.Description, sliders)).ToArray();
            if (candidates.Length == 0) throw new InvalidOperationException("No compatible packet shell exists for " + type);
            var template = candidates[random.Next(candidates.Length)];
            // Decode the complete captured envelope so repeated types never share mutable arrays.
            var offer = GeneratedMissionWire.Read(template.Body).QuestInfos[template.Index];
            var originalText = MissionOfferTextBuilder.Capture(offer);
            int identity = durableIds();
            if (identity <= 0 || !identities.Add(identity)) throw new InvalidOperationException("Durable mission identities must be positive and unique.");
            offer.QuestIdentity = new() { Type = (IdentityType)0xDAC3, Instance = identity };
            Identity Retarget(Identity value) => (int)value.Type == MissionTerminal.LiveIdentityType && value.Instance == template.Terminal.Instance
                ? request.MissionTerminalIdentity : value;
            offer.Unknown5 = Retarget(offer.Unknown5);
            offer.Unknown14 = Retarget(offer.Unknown14);
            offer.Unknown23 = Retarget(offer.Unknown23);
            var action = offer.QuestActions[0];
            action.Unknown1 = Retarget(action.Unknown1);
            action.UnknownHash15 = checked(clientClockSeconds + policy.OfferLifetimeSeconds);
            // Uniform selection of distinct observed identities, with replacement. Observation
            // counts are evidence coverage, never retail probability weights.
            var pool = destinationPools[type];
            var destination = pool[random.Next(pool.Count)];
            if (!catalog.TryGetWorldPosition(destination.Identity, out var worldPosition))
                throw new InvalidOperationException("The selected entrance has no proven WorldPos offsets.");
            entrances.Add(destination.Identity);
            action.Playfield = new() { Type = (IdentityType)worldPosition.PlayfieldIdentityType, Instance = destination.PlayfieldId };
            action.X = destination.LocalX; action.Y = destination.LocalY; action.Z = destination.LocalZ;
            action.Unknown18 = worldPosition.WorldOffsetX; action.Unknown19 = worldPosition.WorldOffsetZ;
            offer.Quality = quality;
            MissionRewardEvidenceModel.Apply(offer, type, level, request.LevelSlider, quality, sliders, destination.PlayfieldId, random);
            var reward = MissionRollRewardItems.Select(quality, random);
            if (offer.ItemRewards?.Length > 0)
            {
                offer.ItemRewards[0].LowId = reward.LowId;
                offer.ItemRewards[0].HighId = reward.HighId;
                offer.ItemRewards[0].Quality = reward.Quality;
            }
            else offer.ItemRewards = [reward];
            MissionOfferTextBuilder.Apply(offer, template.Description, originalText);
            if (!MissionOfferCompatibility.TryValidateGenerated(offer, template.Description, sliders, request.MissionTerminalIdentity, out var descriptor, out var failure)
                || descriptor.Type != type) throw new InvalidOperationException("Invalid composed mission: " + failure);
            if (offer.Info is { } text && !text.EndsWith('\0')) offer.Info = text + '\0';
            return offer;
        }).ToArray();
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

    static Shell[] ReadShells()
    {
        var shells = new List<Shell>();
        for (int index = 0; index < GeneratedMissionWire.CapturedCount; index++)
        {
            byte[] body = GeneratedMissionWire.CapturedBody(index);
            var packet = GeneratedMissionWire.Read(body);
            for (int offer = 0; offer < (packet.QuestInfos?.Length ?? 0); offer++)
                if (MissionOfferCompatibility.TryDescribeCaptured(packet.QuestInfos![offer], packet.MissionTerminalIdentity, out var descriptor, out _))
                    shells.Add(new(body, offer, packet.MissionTerminalIdentity, descriptor));
        }
        return shells.ToArray();
    }
}
