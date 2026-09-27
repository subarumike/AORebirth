namespace ZoneEngine_New.Core.Missions;

using System;
using System.Collections.Generic;
using System.Linq;
using SmokeLounge.AOtomation.Messaging.GameData;
using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;
using ZoneEngine.Core.Missions;
using ZoneEngine_New.Core.Entities;

/// <summary>Composes editable mission content into a five-offer packet before the DAO publishes and charges it.</summary>
internal static class GeneratedMissionRollService
{
    sealed record Shell(QuestInfo Definition, Identity Terminal, MissionOfferDescriptor Description);

    internal static QuestAlternativeMessage Generate(QuestAlternativeMessage request, Identity character, int characterLevel,
        int terminalPlayfield, float terminalX, float terminalZ, MissionLocationSide side,
        int clientClockSeconds, int seed, int nonce, Func<int> durableIds)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(durableIds);
        if (!MissionRollSliders.TryCreate(request, out var sliders, out var error)) throw new ArgumentException(error, nameof(request));
        int level = MissionLevelRuntime.ClampCharacterLevel(characterLevel);
        int quality = MissionLevelRuntime.GetMissionQuality(characterLevel, request.LevelSlider);
        var random = new Random(seed);
        var policy = MissionRollPolicy.Current;
        var response = MissionOfferContent.Current.CopyResponse((int)((uint)nonce % (uint)MissionOfferContent.Current.Responses.Length));
        response.Identity = character;
        response.MissionTerminalIdentity = request.MissionTerminalIdentity;
        var locations = new MissionRollLocations(level, terminalPlayfield, terminalX, terminalZ, side);
        var types = MissionRollEvidenceCatalog.SelectTypeMix(level, request.LevelSlider, quality, sliders, random);
        if (types.Length != 5) throw new InvalidOperationException("A mission offer cohort must contain five entries.");
        var shells = ReadShells();
        var identities = new HashSet<int>();
        response.QuestInfos = types.Select(type =>
        {
            var candidates = shells.Where(s => s.Description.Type == type && MissionOfferCompatibility.IsCompatibleWithSliders(s.Description, sliders)).ToArray();
            if (candidates.Length == 0) throw new InvalidOperationException("No compatible typed offer definition exists for " + type);
            var template = candidates[random.Next(candidates.Length)];
            // Copy typed content so repeated types never share mutable arrays.
            var offer = MissionTypedJson.Copy(template.Definition);
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
            var destination = locations.Next(random);
            action.Playfield = new() { Type = IdentityType.Playfield2, Instance = destination.Playfield };
            action.X = destination.X; action.Y = destination.Y; action.Z = destination.Z;
            action.Unknown18 = destination.EntranceLow; action.Unknown19 = destination.EntranceHigh;
            offer.Quality = quality;
            MissionRewardEvidenceModel.Apply(offer, type, level, request.LevelSlider, quality, sliders, destination.Playfield, random);
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
        return response;
    }

    static Shell[] ReadShells()
    {
        var shells = new List<Shell>();
        foreach (var response in MissionOfferContent.Current.Responses)
            foreach (var offer in response.QuestInfos)
                if (MissionOfferCompatibility.TryDescribeContent(offer, response.MissionTerminalIdentity, out var descriptor, out _))
                    shells.Add(new(offer, response.MissionTerminalIdentity, descriptor));
        return shells.ToArray();
    }
}
