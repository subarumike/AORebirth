namespace ZoneEngine_New.Core.Characters;

using System.Collections.Generic;
using System.Linq;
using AORebirth.Interfaces.Persistence.Missions;
using ZoneEngine_New.Core.Data;

// Persisted identity allocation range and read-only conversion to an owned saved exterior.
// No world allocation, mission transition, reward, item mutation or database write.
internal static class SavedMissionLocation
{
    internal const int MinimumIdentity = 0x160000;
    internal const int MaximumIdentity = 0x16FFFF;
    internal static bool IsMission(int id) => id >= MinimumIdentity && id <= MaximumIdentity;

    internal static CharacterRecord? RestoreExterior(CharacterRecord stored, IEnumerable<GeneratedMissionBinding> bindings)
    {
        if (!IsMission(stored.Playfield)) return stored;
        var matches = bindings.Where(b => b.OwnerId == stored.Id && b.LivePlayfield == stored.Playfield).Take(2).ToArray();
        if (matches.Length != 1 || matches[0].Offer is not { } offer
            || stored.Id <= 0 || offer.OfferType <= 0 || offer.OfferInstance <= 0
            || offer.OwnerId != stored.Id || offer.OfferType != matches[0].OfferType || offer.OfferInstance != matches[0].OfferInstance
            || offer.DestinationPlayfield <= 0 || IsMission(offer.DestinationPlayfield)
            || !float.IsFinite(offer.DestinationX) || !float.IsFinite(offer.DestinationY) || !float.IsFinite(offer.DestinationZ))
            return null;
        return new CharacterRecord
        {
            Id = stored.Id, Name = stored.Name, FirstName = stored.FirstName, LastName = stored.LastName,
            Playfield = offer.DestinationPlayfield, X = offer.DestinationX, Y = offer.DestinationY, Z = offer.DestinationZ,
            HeadingX = stored.HeadingX, HeadingY = stored.HeadingY, HeadingZ = stored.HeadingZ, HeadingW = stored.HeadingW
        };
    }
}
