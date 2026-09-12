namespace ZoneEngine_New.Tests
{
    using AORebirth.Core.GameData;
    using AORebirth.Core.Playfields.OfficialPlacements;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using ZoneEngine_New.Core.Playfield;

    [TestClass]
    public sealed class OfficialHashSpawnAuthorizationTests
    {
        [TestMethod]
        public void ExactAuthorityAndExplicitTemplateBridgeAreRequired()
        {
            var record = ApprovedFixture();
            var source = SourceFixture();
            Assert.IsTrue(OfficialHashSpawnAuthorization.Matches(record, 1, 0, source));
            record.ResolvedMobTemplateHash = null!;
            Assert.IsFalse(OfficialHashSpawnAuthorization.Matches(record, 1, 0, source));
            record.ResolvedMobTemplateHash = "TEST";
            record.RuntimeActivationAuthorized = false;
            Assert.IsFalse(OfficialHashSpawnAuthorization.Matches(record, 1, 0, source));
        }

        [TestMethod]
        public void SimilarCoordinatesOrHashTextCannotAuthorizeAnotherRecord()
        {
            var record = ApprovedFixture();
            var source = SourceFixture();
            Assert.IsFalse(OfficialHashSpawnAuthorization.Matches(record, 1, 1, source));
            source.Position[0] += 0.001f;
            Assert.IsFalse(OfficialHashSpawnAuthorization.Matches(record, 1, 0, source));
            source = SourceFixture();
            source.HashText = "TSET";
            Assert.IsFalse(OfficialHashSpawnAuthorization.Matches(record, 1, 0, source));
            source = SourceFixture();
            source.RespawnTime++;
            Assert.IsFalse(OfficialHashSpawnAuthorization.Matches(record, 1, 0, source));
        }

        private static PlayfieldSpawnEntry SourceFixture() => new()
        {
            DistrictIndex = 0, Hash = 42, HashText = "TEST", Position = [1, 2, 3],
            MinLevel = 1, MaxLevel = 1, RespawnChance = 100, RespawnTime = 30,
            Angle = 0, AngleW = 1, Radius = 0
        };

        private static OfficialPlayfieldPlacement ApprovedFixture() => new()
        {
            PlayfieldId = 1, DistrictIndex = 0, DistrictRecordOrdinal = 0,
            OfficialAcgHashNativeUInt32 = 42, CanonicalAcgHashText = "TEST",
            PositionX = 1, PositionY = 2, PositionZ = 3, LevelMinimum = 1, LevelMaximum = 1,
            RespawnChance = 100, RespawnTime = 30, RotationMidEncoded = 0, RotationWidthEncoded = 1,
            Radius = 0, RuntimeActivationAuthorized = true, IdentityResolved = true, BehaviorReady = true,
            ExistingAoRebirthProfile = "test-fixture-only", ResolvedMobTemplateHash = "TEST",
            MobTemplateEvidenceSource = "test-fixture-only"
        };
    }
}
