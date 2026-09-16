namespace ZoneEngine_New.Tests;

using AORebirth.Interfaces.Persistence.Missions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using ZoneEngine_New.Core.Characters;
using ZoneEngine_New.Core.Data;

[TestClass]
public sealed class SavedMissionLocationTests
{
    [TestMethod]
    public void OwnedSavedExteriorIsProjectedWithoutModifyingOriginalCharacterOrMission()
    {
        var stored = new CharacterRecord { Id = 5, Playfield = SavedMissionLocation.MinimumIdentity, X = 10, HeadingW = 1 };
        var offer = new GeneratedMissionOffer { OwnerId = 5, OfferType = 4, OfferInstance = 8, DestinationPlayfield = 800, DestinationX = 20, DestinationY = 30, DestinationZ = 40 };
        var binding = new GeneratedMissionBinding { OwnerId = 5, LivePlayfield = stored.Playfield, OfferType = 4, OfferInstance = 8, Offer = offer, Version = 9 };
        var restored = SavedMissionLocation.RestoreExterior(stored, [binding]);
        Assert.IsNotNull(restored);
        Assert.AreEqual(800, restored.Playfield); Assert.AreEqual(20f, restored.X);
        Assert.AreEqual(SavedMissionLocation.MinimumIdentity, stored.Playfield);
        Assert.AreEqual(10f, stored.X); Assert.AreEqual(9L, binding.Version);
    }

    [TestMethod]
    public void MissingAmbiguousForeignAndNonfiniteSavedReturnsFailClosed()
    {
        var stored = new CharacterRecord { Id = 5, Playfield = SavedMissionLocation.MinimumIdentity };
        var binding = new GeneratedMissionBinding { OwnerId = 5, LivePlayfield = stored.Playfield, OfferType = 4, OfferInstance = 8,
            Offer = new GeneratedMissionOffer { OwnerId = 5, OfferType = 4, OfferInstance = 8, DestinationPlayfield = 800 } };
        Assert.IsNull(SavedMissionLocation.RestoreExterior(stored, []));
        Assert.IsNull(SavedMissionLocation.RestoreExterior(stored, [binding, binding]));
        binding.Offer.OwnerId = 6; Assert.IsNull(SavedMissionLocation.RestoreExterior(stored, [binding]));
        binding.Offer.OwnerId = 5; binding.Offer.DestinationX = float.NaN;
        Assert.IsNull(SavedMissionLocation.RestoreExterior(stored, [binding]));
        binding.Offer.DestinationX = 0; binding.Offer.OfferInstance = binding.OfferInstance = 0;
        Assert.IsNull(SavedMissionLocation.RestoreExterior(stored, [binding]));
    }
}
