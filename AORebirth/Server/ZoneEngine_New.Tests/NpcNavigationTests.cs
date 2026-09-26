namespace ZoneEngine_New.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Reflection;
    using System.Runtime.CompilerServices;
    using System.Text;

    using AORebirth.Core.GameData;
    using AORebirth.Enums;
    using AORebirth.World.Pathfinding;

    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    using SmokeLounge.AOtomation.Messaging.GameData;

    using ZoneEngine_New.Core.Ai;
    using ZoneEngine_New.Core.Entities;
    using ZoneEngine_New.Core.GameData;
    using ZoneEngine_New.Core.Inventory;
    using ZoneEngine_New.Core.Playfield;
    using ZoneEngine_New.Core.WorldSimulation;

    using Vector3 = AORebirth.Core.Vector.Vector3;
    using NumVector3 = System.Numerics.Vector3;

    /// <summary>
    /// Chases on real playfield navmesh and collision, ticked at the playfield heartbeat rate.
    /// </summary>
    [TestClass]
    public sealed class NpcNavigationTests
    {
        /// <summary>Config.example.xml PlayfieldTickRate.</summary>
        const int TickRate = 32;
        const double TickSeconds = 1.0 / TickRate;
        const double TimeoutSeconds = 60;

        /// <summary>The route must get this much shorter within <see cref="StallSeconds"/>, or the NPC is looping.</summary>
        const float ProgressMeters = 1f;
        const double StallSeconds = 4;

        [TestMethod]
        public void Pf127_NpcRoundsTheTightCornerAndReachesThePlayer()
        {
            ChaseResult result = Chase(127, new Vector3(210, 73, 102), new Vector3(196, 73, 100));

            Assert.IsTrue(result.Reached, result.Report);
        }

        /// <summary>
        /// Live: Abmouth Supremus held 5.08m (centre) from Delmus on PF127 without swinging. The brain
        /// stops the chase inside the 4m default melee range, but a weapon only swings inside its own range.
        /// </summary>
        [TestMethod]
        [DataRow(0, DisplayName = "unarmed")]
        [DataRow(2, DisplayName = "2m weapon")]
        public void Pf127_AbmouthSwingsAtThePlayerFromTheLiveStandoff(int weaponRange)
        {
            ChaseResult result = Chase(127, new Vector3(356.14, 73.61, 94.97), new Vector3(361.22, 73.61, 94.97), weaponRange);

            Assert.IsTrue(result.Reached, result.Report);
        }

        static ChaseResult Chase(int playfieldId, Vector3 mobAt, Vector3 playerAt, int weaponRange = 0)
        {
            var data = new GameDataStore(new StubLogger());
            DestinationsCatalog.Instance.ConfigureRoot(data.RootPath);
            if (!System.IO.File.Exists(System.IO.Path.Combine(data.RootPath, GameDataPaths.PlayfieldNavMeshRelativePath(playfieldId))))
            {
                Assert.Inconclusive(
                    "No Navmesh.dat for playfield " + playfieldId.ToString(CultureInfo.InvariantCulture) + " under " + data.RootPath
                    + ". Point " + GameDataPaths.EnvironmentVariableName + " at a GameData folder that has one.");
            }

            Assert.IsTrue(
                NavMeshPathfinder.TryLoad(data.RootPath, playfieldId, out NavMeshPathfinder? finder, out string? failure),
                "Navmesh for playfield " + playfieldId.ToString(CultureInfo.InvariantCulture) + " did not load: " + failure);
            using PlayfieldWorldSimulation world = PlayfieldWorldSimulation.Create(
                playfieldId,
                data.GetPlayfieldGeometry(playfieldId),
                data.GetPlayfieldMetaData(playfieldId),
                DestinationsCatalog.Instance,
                data,
                new StubLogger());

            using ServiceProvider services = new ServiceCollection()
                .AddSingleton<IGameData>(data)
                .AddSingleton(new WorldSimulationAccess { Instance = world })
                .AddSingleton(new DynelRegistry())
                .BuildServiceProvider();
            Playfield playfield = CreatePlayfield(playfieldId, services, finder!);

            var catalog = new StubCatalog().AddWeapon(9001, 1);
            catalog.Require(9001).Stats[CharacterStat.AttackRange] = weaponRange;
            var items = new ItemBuilder(catalog, new StubLogger());
            NpcCharacter npc = new(new Identity { Type = IdentityType.CanbeAffected, Instance = 127001 }, items);
            Player player = TestWorld.CreatePlayer(127002);
            DynelRegistry registry = services.GetRequiredService<DynelRegistry>();
            foreach (Character character in new Character[] { npc, player })
            {
                character.Playfield = playfield;
                registry.Register(character);
            }

            npc.Position = playfield.SnapNpcSpawn(mobAt);
            player.Position = playfield.SnapFeetToFloor(playerAt);
            if (weaponRange > 0)
            {
                npc.EquipCombatWeapon(items.Create(9001, 9001, 1, ItemSource.Other));
                npc.RebaseWeapons();
            }

            NpcBrain brain = NpcBrain.Create(npc, npc.Position);
            brain.AddThreat(player.Identity, 10f);

            var trail = new List<Vector3>();
            var plans = new List<string>();
            string lastPlan = string.Empty;
            var route = new List<NumVector3>();
            float best = RouteMeters(finder!, npc.Position, player.Position, route);
            double bestAt = 0;
            double t = 0;
            for (int tick = 0; t < TimeoutSeconds; tick++, t += TickSeconds)
            {
                npc.Tick(TickSeconds);
                string plan = PlanText(npc);
                if (plan != lastPlan)
                {
                    lastPlan = plan;
                    plans.Add(string.Create(CultureInfo.InvariantCulture, $"{t:F2}s @({npc.Position.x:F2},{npc.Position.z:F2}) ->{plan}"));
                }

                if (tick % (TickRate / 4) == 0)
                    trail.Add(new Vector3(npc.Position.x, npc.Position.y, npc.Position.z));

                if (CanSwing(brain, npc, player))
                    return new ChaseResult(true, Describe("reached", t, brain, npc, player, trail, route, plans));

                float left = RouteMeters(finder!, npc.Position, player.Position, route);
                if (left < best - ProgressMeters)
                {
                    best = left;
                    bestAt = t;
                }
                else if (t - bestAt >= StallSeconds)
                {
                    return new ChaseResult(
                        false,
                        Describe(
                            string.Create(CultureInfo.InvariantCulture, $"no progress for {StallSeconds:F0}s (best route {best:F2}m, now {left:F2}m)"),
                            t,
                            brain,
                            npc,
                            player,
                            trail,
                            route,
                            plans));
                }
            }

            return new ChaseResult(false, Describe("timeout", t, brain, npc, player, trail, route, plans));
        }

        /// <summary>What the swing checks: any weapon in its own range with LOS, or the brain's range when unarmed.</summary>
        static bool CanSwing(NpcBrain brain, NpcCharacter npc, Player player)
        {
            bool armed = false;
            foreach (CharacterWeapon? weapon in npc.Weapons.Values)
            {
                if (weapon == null)
                    continue;
                armed = true;
                if (npc.GetEdgeDistanceTo(player) <= weapon.GetAttackRange() && npc.HasLineOfSightTo(player))
                    return true;
            }

            return !armed && brain.CanAttackNow(player);
        }

        static float RouteMeters(NavMeshPathfinder finder, Vector3 from, Vector3 to, List<NumVector3> scratch)
        {
            var start = new NumVector3((float)from.x, (float)from.y, (float)from.z);
            if (!finder.TryFindPath(start, new NumVector3((float)to.x, (float)to.y, (float)to.z), scratch)
                || scratch.Count == 0)
                return float.PositiveInfinity;

            float meters = 0f;
            NumVector3 previous = start;
            foreach (NumVector3 point in scratch)
            {
                meters += NumVector3.Distance(previous, point);
                previous = point;
            }

            return meters;
        }

        static string Describe(string outcome, double t, NpcBrain brain, NpcCharacter npc, Player player, List<Vector3> trail, List<NumVector3> route, List<string> plans)
        {
            var text = new StringBuilder();
            text.Append(CultureInfo.InvariantCulture, $"{outcome} at {t:F2}s; npc=({npc.Position.x:F2},{npc.Position.y:F2},{npc.Position.z:F2}) player=({player.Position.x:F2},{player.Position.y:F2},{player.Position.z:F2})");
            text.AppendLine();
            text.Append(CultureInfo.InvariantCulture, $"edge={npc.GetEdgeDistanceTo(player):F2}m inRange={brain.IsInAttackRange(player)} los={npc.HasLineOfSightTo(player)} canPath={brain.CanPathTo(player)}");
            text.AppendLine();
            var sim = npc.Motor.Vehicle;
            dynamic? npcSim = typeof(ZoneEngine_New.Core.Movement.CharacterMotor)
                .GetField("_npc", BindingFlags.Instance | BindingFlags.NonPublic)?.GetValue(npc.Motor);
            string guide = npcSim == null
                ? "n/a"
                : string.Create(CultureInfo.InvariantCulture, $"({(float)npcSim.Guide.GuidePos.X:F2},{(float)npcSim.Guide.GuidePos.Z:F2})");
            text.Append(CultureInfo.InvariantCulture, $"guide={guide} velocity=({sim.Velocity.X:F2},{sim.Velocity.Z:F2}) ");
            text.Append(CultureInfo.InvariantCulture, $"motor hasPath={npc.Motor.HasPath} waypoints:");
            foreach (var point in npc.Motor.CopyRemainingWaypoints())
                text.Append(CultureInfo.InvariantCulture, $" ({point.X:F1},{point.Z:F1})");
            text.AppendLine();
            text.Append("route:");
            foreach (NumVector3 point in route)
                text.Append(CultureInfo.InvariantCulture, $" ({point.X:F1},{point.Z:F1})");
            text.AppendLine();
            text.AppendLine("plans:");
            for (int i = Math.Max(0, plans.Count - 25); i < plans.Count; i++)
                text.AppendLine("  " + plans[i]);
            text.Append("trail (4/s):");
            int from = Math.Max(0, trail.Count - 40);
            for (int i = from; i < trail.Count; i++)
                text.Append(CultureInfo.InvariantCulture, $" ({trail[i].x:F1},{trail[i].z:F1})");
            return text.ToString();
        }

        static string PlanText(NpcCharacter npc)
        {
            var text = new StringBuilder();
            foreach (var point in npc.Motor.CopyRemainingWaypoints())
                text.Append(CultureInfo.InvariantCulture, $" ({point.X:F1},{point.Z:F1})");
            return text.ToString();
        }

        static Playfield CreatePlayfield(int id, ServiceProvider services, NavMeshPathfinder finder)
        {
            var playfield = (Playfield)RuntimeHelpers.GetUninitializedObject(typeof(Playfield));
            Set(playfield, "<Identity>k__BackingField", new Identity { Type = IdentityType.Playfield, Instance = id });
            Set(playfield, "_serviceProvider", services);
            Set(playfield, "<Pathfinder>k__BackingField", finder);
            return playfield;
        }

        static void Set(object target, string name, object value)
            => typeof(Playfield).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(target, value);

        readonly record struct ChaseResult(bool Reached, string Report);
    }
}
