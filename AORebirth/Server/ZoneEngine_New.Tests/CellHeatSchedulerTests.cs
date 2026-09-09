namespace ZoneEngine_New.Tests
{
    using System;

    using AORebirth.Core.GameData;

    using Vector3 = AORebirth.Core.Vector.Vector3;

    using Microsoft.VisualStudio.TestTools.UnitTesting;

    using SmokeLounge.AOtomation.Messaging.GameData;

    using Utility.Config;

    using ZoneEngine_New.Core.Entities;
    using ZoneEngine_New.Core.Inventory;
    using ZoneEngine_New.Core.Playfield.Locality;

    [TestClass]
    public sealed class CellHeatSchedulerTests
    {
        [TestMethod]
        public void VendorCellStaysColdAndDoesNotSleep()
        {
            CellHeatScheduler scheduler = CreateOutdoorScheduler(out CellGrid grid);
            int sleepCount = 0;
            int tickCount = 0;
            scheduler.ConfigureSpawnHooks(
                [],
                _ => sleepCount++,
                _ => tickCount++,
                () => { });

            NpcCharacter vendor = CreateVendor();
            Place(grid, vendor, new Vector3(20f, 0f, 20f));

            scheduler.Tick(new Dynel[] { vendor }, heartbeatDeltaTime: 0.2);
            scheduler.Tick(new Dynel[] { vendor }, heartbeatDeltaTime: 0.2);

            Assert.AreEqual(0, sleepCount);
            Assert.IsTrue(tickCount >= 1, "A vendor cell must tick at Cold instead of sleeping.");
        }

        [TestMethod]
        public void LinkDeadPlayerDoesNotKeepCellHeatWithoutASession()
        {
            CellHeatScheduler scheduler = CreateOutdoorScheduler(out CellGrid grid);
            int sleepCount = 0;
            int tickCount = 0;
            scheduler.ConfigureSpawnHooks(
                [],
                _ => sleepCount++,
                _ => tickCount++,
                () => { });

            Player player = TestWorld.CreatePlayer(18);
            player.EnterLinkDead(TimeSpan.FromSeconds(60));
            Place(grid, player, new Vector3(20f, 0f, 20f));

            NpcCharacter npc = new(
                new Identity { Type = IdentityType.CanbeAffected, Instance = 2 },
                new StubItemBuilder());
            Place(grid, npc, new Vector3(20f, 0f, 20f));

            scheduler.Tick(new Dynel[] { player, npc }, heartbeatDeltaTime: 0.2);

            Assert.AreEqual(0, tickCount);
        }

        [TestMethod]
        public void OrdinaryNpcCellSleepsWhenFarFromPlayers()
        {
            CellHeatScheduler scheduler = CreateOutdoorScheduler(out CellGrid grid);
            int sleepCount = 0;
            int tickCount = 0;
            scheduler.ConfigureSpawnHooks(
                [],
                _ => sleepCount++,
                _ => tickCount++,
                () => { });

            NpcCharacter npc = new(
                new Identity { Type = IdentityType.CanbeAffected, Instance = 2 },
                new StubItemBuilder());
            Place(grid, npc, new Vector3(20f, 0f, 20f));

            scheduler.Tick(new Dynel[] { npc }, heartbeatDeltaTime: 0.2);

            Assert.AreEqual(0, sleepCount);
            Assert.AreEqual(0, tickCount);
        }

        static CellHeatScheduler CreateOutdoorScheduler(out CellGrid grid)
        {
            var meta = new PlayfieldMetaData
            {
                SchemaVersion = 1,
                TilemapFormat = PlayfieldMetaData.ChunkedGroundFormat,
                TilemapResource = 1,
                Width = 100,
                Height = 100,
                TileSize = 1f
            };
            LocalityPolicy policy = LocalityPolicy.FromConfig(new LocalitySettings
            {
                EnableCellHeatScheduling = true,
                VisibilityNeighborLevel = 2,
                HotNeighborLevel = 1,
                WarmNeighborLevel = 2,
                CellSleepTime = 30
            });
            grid = new CellGrid(meta, policy.VisibilityNeighborLevel);
            return new CellHeatScheduler(500, grid, policy);
        }

        static NpcCharacter CreateVendor()
        {
            var npc = new NpcCharacter(
                new Identity { Type = IdentityType.CanbeAffected, Instance = 1 },
                new StubItemBuilder());
            var machine = new VendingMachine(
                new Identity { Type = IdentityType.VendingMachine, Instance = 1 },
                new ItemTemplate { Id = 1, Quality = 1 });
            npc.AttachShop(machine);
            return npc;
        }

        static void Place(CellGrid grid, Dynel dynel, Vector3 position)
        {
            dynel.Position = position;
            Cell cell = grid.ResolveCell(position);
            cell.Add(dynel);
            dynel.Cell = cell;
        }
    }
}
