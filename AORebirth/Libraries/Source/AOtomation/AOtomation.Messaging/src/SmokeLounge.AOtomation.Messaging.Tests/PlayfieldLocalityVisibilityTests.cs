namespace SmokeLounge.AOtomation.Messaging.Tests
{
    using System.IO;

    using Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestClass]
    public class PlayfieldLocalityVisibilityTests
    {
        [TestMethod]
        public void OutdoorLayoutUsesExtractedGridAndBothWorldAxes()
        {
            string layout = ReadRepositoryFile(
                @"AORebirth\Server\ZoneEngine\Core\Playfields\Locality\PlayfieldCellLayout.cs");

            StringAssert.Contains(layout, "metaData.TryGetOutdoorGrid(");
            StringAssert.Contains(layout, "worldPosition.x / CellWorldSize");
            StringAssert.Contains(layout, "worldPosition.z / CellWorldSize");
            StringAssert.Contains(layout, "ix >= NumZonesX || iz >= NumZonesZ");
        }

        [TestMethod]
        public void CharacterRegistryTracksCellMovesAndExplicitNonLocalState()
        {
            string registry = ReadRepositoryFile(
                @"AORebirth\Server\ZoneEngine\Core\Playfields\Locality\PlayfieldDynelCellRegistry.cs");

            StringAssert.Contains(registry, "private const int NonLocalCellId = -1;");
            StringAssert.Contains(registry, "this.AssignCellUnlocked(character)");
            StringAssert.Contains(registry, "this.RemoveFromCellUnlocked(key, oldCellId);");
            StringAssert.Contains(registry, "this.cellByIdentity[key] = newCellId;");
            StringAssert.Contains(registry, "if (newCellId >= 0)");
        }

        [TestMethod]
        public void VisibilityMaintainsRecipientAndSourceIndexes()
        {
            string visibility = ReadRepositoryFile(
                @"AORebirth\Server\ZoneEngine\Core\Playfields\Locality\PlayfieldLocalityVisibility.cs");

            StringAssert.Contains(visibility, "visibleSourcesByRecipient");
            StringAssert.Contains(visibility, "visibleRecipientsBySource");
            StringAssert.Contains(visibility, "ReconcileRecipient(");
            StringAssert.Contains(visibility, "UnregisterSource(");
            StringAssert.Contains(visibility, "ForgetRecipient(");
            StringAssert.Contains(visibility, "this.cells.CollectNeighborCells(");
        }

        private static string ReadRepositoryFile(string relativePath)
        {
            return File.ReadAllText(
                Path.Combine(TestRepositoryRootResolver.FindFromCallerFilePath(), relativePath));
        }
    }
}
