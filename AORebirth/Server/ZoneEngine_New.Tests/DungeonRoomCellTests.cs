namespace ZoneEngine_New.Tests
{
    using System.Collections.Generic;

    using AORebirth.World.Collision;

    using Microsoft.VisualStudio.TestTools.UnitTesting;

    using SmokeLounge.AOtomation.Messaging.GameData;

    using ZoneEngine_New.Core.Entities;
    using ZoneEngine_New.Core.Playfield.Locality;

    using Vector3 = AORebirth.Core.Vector.Vector3;

    [TestClass]
    public sealed class DungeonRoomCellTests
    {
        [TestMethod]
        public void ApplyDungeonRooms_PlacesDynelInInnermostRoom()
        {
            var locality = new PlayfieldLocality(127, metaData: null);
            locality.ApplyDungeonRooms(
                new[]
                {
                    new DungeonRoomBounds(0, new System.Numerics.Vector3(0f, 0f, 0f), new System.Numerics.Vector3(40f, 16f, 40f)),
                    new DungeonRoomBounds(1, new System.Numerics.Vector3(10f, 0f, 10f), new System.Numerics.Vector3(20f, 16f, 20f))
                });

            var inner = new Dynel(new Identity { Type = IdentityType.CanbeAffected, Instance = 1 })
            {
                Position = new Vector3(15f, 4f, 15f)
            };
            var outer = new Dynel(new Identity { Type = IdentityType.CanbeAffected, Instance = 2 })
            {
                Position = new Vector3(2f, 4f, 2f)
            };
            locality.RegisterDynel(inner);
            locality.RegisterDynel(outer);

            Assert.AreEqual(1, inner.Cell?.Id);
            Assert.AreEqual(0, outer.Cell?.Id);
            Assert.IsTrue(locality.Grid.IsDungeon);
        }

        [TestMethod]
        public void IndoorAnnounce_ReachesOccupantsInOtherRooms()
        {
            var locality = new PlayfieldLocality(127, metaData: null);
            locality.ApplyDungeonRooms(
                new[]
                {
                    new DungeonRoomBounds(0, new System.Numerics.Vector3(0f, 0f, 0f), new System.Numerics.Vector3(10f, 16f, 10f)),
                    new DungeonRoomBounds(1, new System.Numerics.Vector3(50f, 0f, 50f), new System.Numerics.Vector3(60f, 16f, 60f))
                });

            var a = new Dynel(new Identity { Type = IdentityType.CanbeAffected, Instance = 1 })
            {
                Position = new Vector3(5f, 4f, 5f)
            };
            var b = new Dynel(new Identity { Type = IdentityType.CanbeAffected, Instance = 2 })
            {
                Position = new Vector3(55f, 4f, 55f)
            };
            locality.RegisterDynel(a);
            locality.RegisterDynel(b);

            var seen = new HashSet<int>();
            foreach (Dynel dynel in locality.Grid.OccupantsInAllCells())
                seen.Add(dynel.Identity.Instance);

            Assert.IsTrue(seen.Contains(1));
            Assert.IsTrue(seen.Contains(2));
        }
    }
}
