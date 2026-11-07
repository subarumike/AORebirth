namespace ZoneEngine_New.Core.Missions;

using System;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using SmokeLounge.AOtomation.Messaging.GameData;
using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;
using ZoneEngine.Core.Missions;
using ZoneEngine_New.Core.Entities;

/// <summary>Validated typed definitions in evidence order, with reusable objective-type eligibility.</summary>
internal sealed class MissionOfferCatalog
{
    internal const string FileName = "MissionOffers.json";
    internal const int OfferCount = 5;

    internal static readonly JsonSerializerOptions JsonOptions = new()
    {
        RespectRequiredConstructorParameters = true,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        AllowDuplicateProperties = false,
        WriteIndented = true
    };

    readonly ImmutableArray<MissionResponseDefinition> _responses;
    readonly FrozenDictionary<MissionRollType, ImmutableArray<EligibleOffer>> _byType;

    internal string Sha256 { get; }
    internal string SourceSha256 { get; }
    internal int ResponseCount => _responses.Length;
    internal int DefinitionCount { get; }

    MissionOfferCatalog(MissionOfferContent content, string sha256)
    {
        if (content.FormatVersion != 1 || content.Responses.IsDefaultOrEmpty
            || content.Provenance is not { } provenance || !IsHex(provenance.InputSha256, 64)
            || !IsHex(provenance.SourceRevision, 40))
            throw new InvalidDataException("Invalid mission offer content version or provenance.");

        Sha256 = sha256;
        SourceSha256 = provenance.InputSha256;
        _responses = content.Responses;
        var hashes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var offers = new List<EligibleOffer>();
        foreach (var response in _responses)
        {
            if (response is null || !IsHex(response.BodySha256, 64) || !hashes.Add(response.BodySha256)
                || (int)response.SourceTerminal.Type != MissionTerminal.LiveIdentityType || response.SourceTerminal.Instance == 0
                || response.SourceCharacter.Type != IdentityType.CanbeAffected || response.SourceCharacter.Instance == 0
                || !MissionLevelRuntime.TryDecodeDifficultySlider(response.LevelSlider, out _)
                || response.Offers.IsDefault || response.Offers.Length != OfferCount)
                throw new InvalidDataException("Invalid mission response definition or repeated evidence hash.");

            Identity terminal = response.SourceTerminal.Create();
            foreach (var definition in response.Offers)
            {
                if (definition is null || definition.SourceIdentity.Type != IdentityType.Mission
                    || definition.SourceIdentity.Instance == 0 || definition.Quality <= 0
                    || definition.Reward is not { LowId: > 0, HighId: > 0, Quality: > 0 }
                    || definition.Action is not { } action || !float.IsFinite(action.X)
                    || !float.IsFinite(action.Y) || !float.IsFinite(action.Z)
                    || definition.ShortInfo is null || definition.ShortInfo.Any(c => c > 127 || c == '\0')
                    || definition.Info is null || definition.Info.Any(c => c > 127)
                    || definition.Info.IndexOf('\0') != definition.Info.Length - 1)
                    throw new InvalidDataException($"Incomplete mission offer definition for evidence {response.BodySha256}.");

                var offer = definition.Create();
                if (!MissionOfferCompatibility.TryDescribeCaptured(offer, terminal, out var descriptor, out string failure))
                    throw new InvalidDataException($"Invalid mission offer definition for evidence {response.BodySha256}: {failure}");

                offers.Add(new EligibleOffer(definition, terminal, descriptor, MissionOfferTextBuilder.Capture(offer)));
            }
        }

        _byType = offers.GroupBy(offer => offer.Descriptor.Type)
            .ToFrozenDictionary(group => group.Key, group => group.ToImmutableArray());
        if (Enum.GetValues<MissionRollType>().Any(type => type != MissionRollType.Unknown && !_byType.ContainsKey(type)))
            throw new InvalidDataException("The mission offer catalog does not cover every supported objective type.");
        DefinitionCount = offers.Count;
    }

    internal static MissionOfferCatalog Load(string root)
    {
        byte[] data = File.ReadAllBytes(Path.Combine(root, FileName));
        var content = JsonSerializer.Deserialize<MissionOfferContent>(data, JsonOptions)
            ?? throw new InvalidDataException("Missing mission offer content.");
        return new MissionOfferCatalog(content, Convert.ToHexStringLower(SHA256.HashData(data)));
    }

    internal QuestAlternativeMessage CreateResponse(int nonce, Identity character, Identity terminal)
        => _responses[(int)((uint)nonce % (uint)_responses.Length)].Create(character, terminal);

    internal EligibleOffer Select(MissionRollType type, Random random)
    {
        // Slider evidence selects a complete cohort. No individual offer exclusion
        // outside that cohort is established by the current evidence.
        var eligible = _byType[type];
        return eligible[random.Next(eligible.Length)];
    }

    static bool IsHex(string? value, int length)
        => value?.Length == length && value.All(Uri.IsHexDigit);

    internal sealed record EligibleOffer(MissionOfferDefinition Definition, Identity SourceTerminal,
        MissionOfferDescriptor Descriptor, MissionOfferTextBuilder.Snapshot Text);
}
