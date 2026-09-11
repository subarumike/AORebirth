namespace ZoneEngine_New.Tests
{
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    using ZoneEngine_New.Core.GameData;
    using ZoneEngine_New.Core.Playfield;
    using ZoneEngine_New.Core.WorldSimulation;

    [TestClass]
    public sealed class BuildingExitProxyTests
    {
        [TestMethod]
        [DataRow(954)]
        [DataRow(1186)]
        [DataRow(2064)]
        public void Pf800_building_destinations_bake_a_return_exit(int buildingPlayfieldId)
        {
            var data = new GameDataStore(new StubLogger());
            DestinationsCatalog.Instance.ConfigureRoot(data.RootPath);

            Assert.IsTrue(
                PlayfieldManager.RequiresWorldSimulation(data, buildingPlayfieldId),
                $"PF {buildingPlayfieldId} was routed to the non-zoning indoor runtime.");

            using PlayfieldWorldSimulation world = PlayfieldWorldSimulation.Create(
                buildingPlayfieldId,
                data.GetPlayfieldGeometry(buildingPlayfieldId),
                data.GetPlayfieldMetaData(buildingPlayfieldId),
                DestinationsCatalog.Instance,
                data,
                new StubLogger());

            Assert.IsTrue(
                world.ExitTriggerCount > 0,
                $"PF {buildingPlayfieldId} did not bake a return exit for its PF 800 TeleportProxy landing.");
        }
    }
}
