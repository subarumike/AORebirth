namespace ZoneEngine_New.Tests
{
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    using ZoneEngine_New.Core.GameData;
    using ZoneEngine_New.Core.WorldSimulation;

    using Vector3 = AORebirth.Core.Vector.Vector3;

    [TestClass]
    public sealed class GridFloorSnapTests
    {
        const int GridPlayfieldId = 152;
        // Borealis exit pad C04C0098 from Dynels.dat
        const float PadX = 242.313232f;
        const float PadY = 4.199992f;
        const float PadZ = 199.412231f;

        [TestMethod]
        public void Grid_BorealisPad_HasNoSnapHit_RequiresAuthoredY()
        {
            var data = new GameDataStore(new StubLogger());
            DestinationsCatalog.Instance.ConfigureRoot(data.RootPath);
            var geometry = data.GetPlayfieldGeometry(GridPlayfieldId);
            Assert.IsNotNull(geometry.Dynels?.Dynels);

            bool found = false;
            foreach (var d in geometry.Dynels!.Dynels!)
            {
                if (d.IdentityInstance != unchecked((int)0xC04C0098))
                    continue;
                found = true;
                Assert.AreEqual(PadY, d.Position.Y, 0.05f);
                break;
            }

            Assert.IsTrue(found, "C04C0098 must exist in PF152 Dynels");

            using var world = PlayfieldWorldSimulation.Create(
                GridPlayfieldId,
                geometry,
                data.GetPlayfieldMetaData(GridPlayfieldId),
                DestinationsCatalog.Instance,
                data,
                new StubLogger());

            // Regression: Grid landings have no Bepu floor hit. Motor must not freefall.
            Assert.IsFalse(
                world.TrySnapToFloor(new Vector3(PadX, PadY + 5f, PadZ), out _),
                "PF152 still has no snap-able floor at Borealis pad; keep authored dynel Y.");
            Assert.IsTrue(
                data.GetPlayfieldMetaData(GridPlayfieldId)!.IsIndoor,
                "Grid must stay indoor so motor keeps authored Y when snap misses.");
        }
    }
}
