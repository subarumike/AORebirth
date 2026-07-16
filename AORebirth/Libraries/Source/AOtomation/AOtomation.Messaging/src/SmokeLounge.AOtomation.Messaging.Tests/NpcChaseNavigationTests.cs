namespace SmokeLounge.AOtomation.Messaging.Tests
{
    using System;
    using System.Collections.Generic;
    using System.IO;

    using Microsoft.VisualStudio.TestTools.UnitTesting;

    using ZoneEngine.Core.Navigation;
    using ZoneEngine.Core.Playfields;

    [TestClass]
    public class NpcChaseNavigationTests
    {
        private static readonly DateTime Epoch =
            new DateTime(2026, 7, 16, 0, 0, 0, DateTimeKind.Utc);

        private static readonly Lazy<PlayfieldCollisionGeometryLoadResult> Pf127Geometry =
            new Lazy<PlayfieldCollisionGeometryLoadResult>(LoadPf127Geometry);

        [TestMethod]
        public void DirectUnobstructedPursuitDoesNotRequestRoute()
        {
            var provider = TestNavigationProvider.Clear();
            var service = Service(provider);

            NpcChaseUpdateResult result = service.UpdatePursuit(
                1,
                2,
                Point(0, 0),
                Point(10, 0),
                2.0,
                Epoch);

            Assert.AreEqual(NpcChaseMovementKind.Direct, result.Kind);
            Assert.AreEqual(0, service.TotalRouteRequests);
            Assert.IsTrue(result.HasDestination);
            Assert.AreEqual(8.0, result.Destination.X, 0.001);
        }

        [TestMethod]
        public void ObstructedPursuitRequestsRouteThroughSharedService()
        {
            var provider = TestNavigationProvider.WithWall(4.0, 6.0, -2.0, 2.0);
            var service = Service(provider);

            NpcChaseUpdateResult result = service.UpdatePursuit(
                11,
                22,
                Point(0, 0),
                Point(10, 0),
                1.0,
                Epoch);

            Assert.AreEqual(NpcChaseMovementKind.Route, result.Kind);
            Assert.AreEqual(1, service.TotalRouteRequests);
            Assert.IsTrue(result.RouteRequested);
            Assert.IsTrue(provider.RequestCount > 0);
        }

        [TestMethod]
        public void UnsupportedPlayfieldPreservesLegacyChaseAndDoesNotPretendToRoute()
        {
            var service = Service(new UnsupportedPlayfieldChaseNavigationProvider(6553));

            NpcChaseUpdateResult result = service.UpdatePursuit(
                1,
                2,
                Point(0, 0),
                Point(10, 0),
                1.0,
                Epoch);

            Assert.AreEqual(NpcChaseMovementKind.Unsupported, result.Kind);
            Assert.IsTrue(service.IsAttackPathTraversable(Point(0, 0), Point(10, 0)));
            Assert.AreEqual(0, service.ActiveStateCount);
        }

        [TestMethod]
        public void ExpectedProviderWithMissingGeometryFailsClosed()
        {
            var provider = new TestNavigationProvider(ChaseNavigationCapability.Unavailable);
            var service = Service(provider);

            NpcChaseUpdateResult result = service.UpdatePursuit(
                1,
                2,
                Point(0, 0),
                Point(10, 0),
                1.0,
                Epoch);

            Assert.AreEqual(NpcChaseMovementKind.Unavailable, result.Kind);
            Assert.IsFalse(service.IsAttackPathTraversable(Point(0, 0), Point(10, 0)));
            Assert.AreEqual(0, service.ActiveStateCount);
        }

        [TestMethod]
        public void GenericProviderProducesOnlyCollisionValidRouteSegments()
        {
            var provider = TestNavigationProvider.WithWall(4.0, 6.0, -2.0, 2.0);
            ChaseNavigationPoint start = Point(0, 0);
            ChaseRoutePlan route = provider.RequestRoute(
                start,
                Point(10, 0),
                Limits());

            Assert.IsTrue(route.IsSuccess);
            ChaseNavigationPoint previous = start;
            foreach (ChaseNavigationPoint point in route.Points)
            {
                Assert.IsTrue(provider.IsSegmentTraversable(previous, point));
                previous = point;
            }
        }

        [TestMethod]
        public void MinorTargetMovementReusesRouteWithoutReplanning()
        {
            var provider = TestNavigationProvider.WithWall(4.0, 6.0, -2.0, 2.0);
            var service = Service(provider);
            service.UpdatePursuit(1, 2, Point(0, 0), Point(10, 0), 1.0, Epoch);

            service.UpdatePursuit(
                1,
                2,
                Point(0.5, 0),
                Point(11.0, 0),
                1.0,
                Epoch + TimeSpan.FromMilliseconds(150));

            Assert.AreEqual(1, service.TotalRouteRequests);
        }

        [TestMethod]
        public void MaterialTargetMovementInvalidatesAndReplans()
        {
            var provider = TestNavigationProvider.WithWall(4.0, 6.0, -5.0, 5.0);
            var service = Service(provider);
            service.UpdatePursuit(1, 2, Point(0, 0), Point(10, 0), 1.0, Epoch);

            service.UpdatePursuit(
                1,
                2,
                Point(0, 0),
                Point(10, 4.0),
                1.0,
                Epoch + TimeSpan.FromMilliseconds(150));

            Assert.AreEqual(2, service.TotalRouteRequests);
        }

        [TestMethod]
        public void TargetIdentityReplacementInvalidatesAndReplans()
        {
            var provider = TestNavigationProvider.WithWall(4.0, 6.0, -5.0, 5.0);
            var service = Service(provider);
            service.UpdatePursuit(1, 2, Point(0, 0), Point(10, 0), 1.0, Epoch);

            service.UpdatePursuit(
                1,
                3,
                Point(0, 0),
                Point(10, 0),
                1.0,
                Epoch + TimeSpan.FromMilliseconds(150));

            Assert.AreEqual(2, service.TotalRouteRequests);
        }

        [TestMethod]
        public void GeometryVersionChangeInvalidatesAndReplans()
        {
            var provider = TestNavigationProvider.WithWall(4.0, 6.0, -5.0, 5.0);
            var service = Service(provider);
            service.UpdatePursuit(1, 2, Point(0, 0), Point(10, 0), 1.0, Epoch);
            provider.Version = "geometry-v2";

            service.UpdatePursuit(
                1,
                2,
                Point(0, 0),
                Point(10, 0),
                1.0,
                Epoch + TimeSpan.FromMilliseconds(150));

            Assert.AreEqual(2, service.TotalRouteRequests);
        }

        [TestMethod]
        public void StuckRouteInvalidatesWithinBoundedTime()
        {
            var provider = TestNavigationProvider.WithWall(4.0, 6.0, -5.0, 5.0);
            var service = Service(provider);
            service.UpdatePursuit(1, 2, Point(0, 0), Point(10, 0), 1.0, Epoch);

            service.UpdatePursuit(
                1,
                2,
                Point(0, 0),
                Point(10, 0),
                1.0,
                Epoch + NpcChaseRouteFollower.StuckTimeout + TimeSpan.FromMilliseconds(150));

            Assert.AreEqual(2, service.TotalRouteRequests);
        }

        [TestMethod]
        public void MaterialRouteDeviationIsRejectedBeforeMovementContinues()
        {
            var provider = TestNavigationProvider.Clear();
            var follower = new NpcChaseRouteFollower();
            var state = new NpcChaseRouteState
                        {
                            GeometryVersion = provider.GeometryVersion,
                            StartSample = Point(0, 0),
                            Route = ChaseRoutePlan.Success(
                                new[] { Point(5, 0), Point(10, 0) },
                                provider.GeometryVersion,
                                2,
                                2),
                            LastIssuedRouteIndex = -1,
                            LastProgressPoint = Point(0, 0),
                            LastProgressUtc = Epoch
                        };
            ChaseNavigationPoint destination;
            bool shouldIssue;
            NpcChaseInvalidationReason invalidation;

            bool selected = follower.TrySelectDestination(
                provider,
                state,
                Point(0, NpcChaseRouteFollower.MaximumRouteDeviation + 1.0),
                Epoch + TimeSpan.FromMilliseconds(150),
                out destination,
                out shouldIssue,
                out invalidation);

            Assert.IsFalse(selected);
            Assert.AreEqual(NpcChaseInvalidationReason.RouteDeviation, invalidation);
        }

        [TestMethod]
        public void UnreachableTargetEntersStableFailureWithoutPerTickRequests()
        {
            var provider = TestNavigationProvider.BlockedEverywhere();
            var service = Service(provider);
            NpcChaseUpdateResult first = service.UpdatePursuit(
                1,
                2,
                Point(0, 0),
                Point(10, 0),
                1.0,
                Epoch);
            NpcChaseUpdateResult second = service.UpdatePursuit(
                1,
                2,
                Point(0, 0),
                Point(10, 0),
                1.0,
                Epoch + TimeSpan.FromSeconds(1.0));

            Assert.AreEqual(NpcChaseMovementKind.Hold, first.Kind);
            Assert.AreEqual(NpcChaseMovementKind.Hold, second.Kind);
            Assert.AreEqual(1, service.TotalRouteRequests);
        }

        [TestMethod]
        public void FailedRouteRetriesOnlyAfterBoundedDelay()
        {
            var service = Service(TestNavigationProvider.BlockedEverywhere());
            service.UpdatePursuit(1, 2, Point(0, 0), Point(10, 0), 1.0, Epoch);
            service.UpdatePursuit(
                1,
                2,
                Point(0, 0),
                Point(10, 0),
                1.0,
                Epoch + NpcChaseNavigationRuntimeService.FailedRouteRetryDelay + TimeSpan.FromMilliseconds(1));

            Assert.AreEqual(2, service.TotalRouteRequests);
        }

        [TestMethod]
        public void MeaningfulNpcMovementAllowsEarlyRetryAfterFailure()
        {
            var service = Service(TestNavigationProvider.BlockedEverywhere());
            service.UpdatePursuit(1, 2, Point(0, 0), Point(10, 0), 1.0, Epoch);
            service.UpdatePursuit(
                1,
                2,
                Point(2.0, 0),
                Point(10, 0),
                1.0,
                Epoch + TimeSpan.FromMilliseconds(150));

            Assert.AreEqual(2, service.TotalRouteRequests);
        }

        [TestMethod]
        public void RouteStateClearsForEveryLifecycleBoundary()
        {
            NpcChaseInvalidationReason[] reasons =
            {
                NpcChaseInvalidationReason.TargetLost,
                NpcChaseInvalidationReason.CombatCancelled,
                NpcChaseInvalidationReason.Death,
                NpcChaseInvalidationReason.CorpseTransition,
                NpcChaseInvalidationReason.Despawn,
                NpcChaseInvalidationReason.LeashReset,
                NpcChaseInvalidationReason.EncounterReset
            };

            foreach (NpcChaseInvalidationReason reason in reasons)
            {
                var service = Service(TestNavigationProvider.WithWall(4, 6, -2, 2));
                service.UpdatePursuit(1, 2, Point(0, 0), Point(10, 0), 1.0, Epoch);
                Assert.IsTrue(service.HasState(1));
                service.Clear(1, reason);
                Assert.IsFalse(service.HasState(1), reason.ToString());
            }
        }

        [TestMethod]
        public void SubwayLeashResetsWhenNpcOrTargetLeavesHomeBoundary()
        {
            ChaseNavigationPoint home = Point(0, 0);

            Assert.IsTrue(
                NpcCombatLeashPolicy.ShouldResetCombat(
                    127,
                    false,
                    home,
                    Point(NpcCombatLeashPolicy.SubwayMaximumDistanceFromHome + 0.01, 0),
                    Point(0, 0)));
            Assert.IsTrue(
                NpcCombatLeashPolicy.ShouldResetCombat(
                    127,
                    false,
                    home,
                    Point(0, 0),
                    Point(NpcCombatLeashPolicy.SubwayMaximumDistanceFromHome + 0.01, 0)));
            Assert.IsFalse(
                NpcCombatLeashPolicy.ShouldResetCombat(
                    127,
                    false,
                    home,
                    Point(NpcCombatLeashPolicy.SubwayMaximumDistanceFromHome, 0),
                    Point(NpcCombatLeashPolicy.SubwayMaximumDistanceFromHome, 0)));

            Assert.IsFalse(
                NpcCombatLeashPolicy.ShouldResetCombat(
                    127,
                    false,
                    new ChaseNavigationPoint(278.045074, 73.01795, 98.80104),
                    new ChaseNavigationPoint(278.045074, 73.01795, 98.80104),
                    new ChaseNavigationPoint(188.2448, 73.01637, 98.84238)));
        }

        [TestMethod]
        public void SubwayLeashExcludesPlayerPetsAndUnsupportedPlayfields()
        {
            ChaseNavigationPoint home = Point(0, 0);
            ChaseNavigationPoint farAway =
                Point(NpcCombatLeashPolicy.SubwayMaximumDistanceFromHome + 1.0, 0);

            Assert.IsFalse(
                NpcCombatLeashPolicy.ShouldResetCombat(127, true, home, farAway, farAway));
            Assert.IsFalse(
                NpcCombatLeashPolicy.ShouldResetCombat(6553, false, home, farAway, farAway));
        }

        [TestMethod]
        public void SubwayLeashReturnCompletesOnlyNearHome()
        {
            ChaseNavigationPoint home = Point(0, 0);

            Assert.IsTrue(
                NpcCombatLeashPolicy.HasReturnedHome(
                    home,
                    Point(NpcCombatLeashPolicy.ReturnCompletionDistance, 0)));
            Assert.IsFalse(
                NpcCombatLeashPolicy.HasReturnedHome(
                    home,
                    Point(NpcCombatLeashPolicy.ReturnCompletionDistance + 0.01, 0)));
        }

        [TestMethod]
        public void PlayfieldResetAndRuntimeDisposalClearAllRoutes()
        {
            var service = Service(TestNavigationProvider.WithWall(4, 6, -2, 2));
            service.UpdatePursuit(1, 2, Point(0, 0), Point(10, 0), 1.0, Epoch);
            service.UpdatePursuit(3, 4, Point(0, 1), Point(10, 1), 1.0, Epoch);
            service.ClearAll(NpcChaseInvalidationReason.PlayfieldReset);
            Assert.AreEqual(0, service.ActiveStateCount);

            service.UpdatePursuit(1, 2, Point(0, 0), Point(10, 0), 1.0, Epoch);
            service.Dispose();
            Assert.AreEqual(0, service.ActiveStateCount);
        }

        [TestMethod]
        public void RespawnedRuntimeIdentityCannotInheritClearedRoute()
        {
            var service = Service(TestNavigationProvider.WithWall(4, 6, -2, 2));
            service.UpdatePursuit(100, 2, Point(0, 0), Point(10, 0), 1.0, Epoch);
            service.Clear(100, NpcChaseInvalidationReason.Despawn);

            NpcChaseUpdateResult result = service.UpdatePursuit(
                101,
                2,
                Point(0, 0),
                Point(10, 0),
                1.0,
                Epoch + TimeSpan.FromSeconds(1));

            Assert.IsFalse(service.HasState(100));
            Assert.IsTrue(service.HasState(101));
            Assert.IsTrue(result.RouteRequested);
        }

        [TestMethod]
        public void DirectPathRestorationReturnsControlToDirectChase()
        {
            var provider = TestNavigationProvider.WithWall(4, 6, -2, 2);
            var service = Service(provider);
            service.UpdatePursuit(1, 2, Point(0, 0), Point(10, 0), 1.0, Epoch);
            provider.WallEnabled = false;

            NpcChaseUpdateResult result = service.UpdatePursuit(
                1,
                2,
                Point(3, 3),
                Point(10, 0),
                1.0,
                Epoch + TimeSpan.FromMilliseconds(150));

            Assert.AreEqual(NpcChaseMovementKind.Direct, result.Kind);
            Assert.AreEqual(NpcChaseInvalidationReason.DirectPathRestored, result.InvalidationReason);
        }

        [TestMethod]
        public void LargeSimulationStepCannotBypassWholeSegmentCollisionValidation()
        {
            var provider = TestNavigationProvider.WithWall(4, 6, -2, 2);
            ChaseNavigationPoint start = Point(0, 0);
            ChaseNavigationPoint end = Point(10, 0);

            Assert.IsFalse(provider.IsSegmentTraversable(start, end));
            Assert.IsFalse(provider.IsSegmentTraversable(start, Interpolate(start, end, 1.0)));

            string root = FindRepositoryRoot();
            string navigationSource = File.ReadAllText(
                Path.Combine(
                    root,
                    @"AORebirth\Server\ZoneEngine\Core\Navigation\NpcChaseNavigationRuntimeService.cs"));
            string controllerSource = File.ReadAllText(
                Path.Combine(
                    root,
                    @"AORebirth\Server\ZoneEngine\Core\Controllers\NPCController.cs"));
            StringAssert.Contains(navigationSource, "this.provider.IsSegmentTraversable(current, target)");
            StringAssert.Contains(controllerSource, "double step = Math.Min(distance, maxDistance);");
        }

        [TestMethod]
        public void SearchLimitsBoundUnreachableWorkDeterministically()
        {
            var provider = TestNavigationProvider.BlockedEverywhere();
            ChaseRoutePlan first = provider.RequestRoute(Point(0, 0), Point(10, 0), Limits());
            ChaseRoutePlan second = provider.RequestRoute(Point(0, 0), Point(10, 0), Limits());

            Assert.AreEqual(first.Status, second.Status);
            Assert.AreEqual(first.ExpandedNodes, second.ExpandedNodes);
            Assert.IsTrue(first.ExpandedNodes <= Limits().MaximumExpandedNodes);
            Assert.IsTrue(first.SegmentChecks <= Limits().MaximumSegmentChecks);
        }

        [TestMethod]
        public void Pf127AdvertisesSupportedGeometryVersionThroughGenericContract()
        {
            Pf127ChaseNavigationProvider provider = LoadPf127Provider();

            Assert.AreEqual(ChaseNavigationCapability.Supported, provider.Capability);
            Assert.AreEqual(
                "6475b3bb25fc67db419c372f46807f682d02416ebaa43274a434a5525cbe62e5",
                provider.GeometryVersion);
        }

        [TestMethod]
        public void Pf127RepresentativeVergilWallBlocksDirectPursuit()
        {
            Pf127ChaseNavigationProvider provider = LoadPf127Provider();

            Assert.IsFalse(
                provider.IsSegmentTraversable(
                    new ChaseNavigationPoint(188.2448, 73.01637, 98.84238),
                    new ChaseNavigationPoint(278.045074, 73.01795, 98.80104)));
        }

        [TestMethod]
        public void Pf127OpenDoorwayAllowsAttackLineWhileMovementCorridorRemainsBlocked()
        {
            Pf127ChaseNavigationProvider provider = LoadPf127Provider();
            var vergil = new ChaseNavigationPoint(278.045074, 73.01795, 98.80104);
            var playerAtOpenDoorway = new ChaseNavigationPoint(246.9, 73.0, 95.5);

            Assert.IsTrue(provider.IsAttackLineTraversable(vergil, playerAtOpenDoorway));
            Assert.IsFalse(provider.IsSegmentTraversable(vergil, playerAtOpenDoorway));
        }

        [TestMethod]
        public void Pf127CapturedWallBlocksAttackLine()
        {
            Pf127ChaseNavigationProvider provider = LoadPf127Provider();

            Assert.IsFalse(
                provider.IsAttackLineTraversable(
                    new ChaseNavigationPoint(121.809868, 73.01637, 98.90472),
                    new ChaseNavigationPoint(187.0416, 73.3830261, 88.03114)));
        }

        [TestMethod]
        public void Pf127AttackLineDoesNotTreatVerticalSegmentsAsZeroLength()
        {
            var blockingSurface = new CollisionTriangle(
                1,
                new CollisionPoint3(-1.0, 1.5, -1.0),
                new CollisionPoint3(1.0, 1.5, -1.0),
                new CollisionPoint3(0.0, 1.5, 1.0));
            var geometry = new PlayfieldCollisionGeometry(
                1,
                127,
                "test",
                string.Empty,
                0.0,
                "vertical-attack-test",
                new[] { blockingSurface });
            var provider = new Pf127ChaseNavigationProvider(
                127,
                PlayfieldCollisionGeometryLoadResult.Loaded(geometry));

            Assert.IsFalse(
                provider.IsAttackLineTraversable(
                    new ChaseNavigationPoint(0.0, 0.0, 0.0),
                    new ChaseNavigationPoint(0.0, 1.0, 0.0)));
        }

        [TestMethod]
        public void Pf127PlansCollisionValidRouteAroundRepresentativeVergilWall()
        {
            Pf127ChaseNavigationProvider provider = LoadPf127Provider();
            var start = new ChaseNavigationPoint(188.2448, 73.01637, 98.84238);
            var goal = new ChaseNavigationPoint(278.045074, 73.01795, 98.80104);
            ChaseNavigationPoint projected;
            Assert.IsTrue(
                provider.TryProjectToSurface(start, start.X, start.Z, out projected),
                "The captured Vergil start must project to the supported same-elevation navigation plane.");
            ChaseRoutePlan route = provider.RequestRoute(start, goal, ChaseRouteSearchLimits.Default);

            Assert.IsTrue(
                route.IsSuccess,
                route.Status + " expanded=" + route.ExpandedNodes + " checks=" + route.SegmentChecks);
            Assert.IsTrue(route.ExpandedNodes <= ChaseRouteSearchLimits.Default.MaximumExpandedNodes);
            ChaseNavigationPoint previous = start;
            foreach (ChaseNavigationPoint point in route.Points)
            {
                Assert.IsTrue(provider.IsSegmentTraversable(previous, point));
                previous = point;
            }

            Assert.IsTrue(provider.IsSegmentTraversable(route.Points[route.Points.Length - 1], goal));
        }

        [TestMethod]
        public void Pf127SharedFollowerRoutesVergilCaseUntilDirectCombatPathIsRestored()
        {
            Pf127ChaseNavigationProvider provider = LoadPf127Provider();
            var service = new NpcChaseNavigationRuntimeService(provider);
            var current = new ChaseNavigationPoint(188.2448, 73.01637, 98.84238);
            var goal = new ChaseNavigationPoint(278.045074, 73.01795, 98.80104);
            DateTime now = Epoch;
            bool routed = false;
            bool directRestored = false;

            for (int step = 0; step < 128; step++)
            {
                NpcChaseUpdateResult result = service.UpdatePursuit(
                    203748,
                    50000,
                    current,
                    goal,
                    3.0,
                    now);
                routed |= result.Kind == NpcChaseMovementKind.Route;
                directRestored |= result.Kind == NpcChaseMovementKind.Direct;
                if (result.HasDestination)
                {
                    Assert.IsTrue(
                        provider.IsSegmentTraversable(current, result.Destination),
                        "Shared pursuit emitted a blocked PF127 segment at step " + step + ".");
                    current = result.Destination;
                }

                if (directRestored
                    && current.Distance2D(goal) <= 3.3
                    && service.IsAttackPathTraversable(current, goal))
                {
                    break;
                }

                now += TimeSpan.FromMilliseconds(150);
            }

            Assert.IsTrue(routed, "The representative blocked pursuit never entered route following.");
            Assert.IsTrue(directRestored, "The route never returned control to direct pursuit.");
            Assert.IsTrue(service.IsAttackPathTraversable(current, goal));
            Assert.IsTrue(current.Distance2D(goal) <= 3.3);
        }

        [TestMethod]
        public void Pf127ReturnToHomeUsesSharedCollisionValidRouting()
        {
            Pf127ChaseNavigationProvider provider = LoadPf127Provider();
            var service = new NpcChaseNavigationRuntimeService(provider);
            var current = new ChaseNavigationPoint(188.2448, 73.01637, 98.84238);
            var home = new ChaseNavigationPoint(278.045074, 73.01795, 98.80104);
            DateTime now = Epoch;
            bool routed = false;

            for (int step = 0; step < 128; step++)
            {
                NpcChaseUpdateResult result = service.UpdateReturnToHome(
                    203748,
                    current,
                    home,
                    NpcCombatLeashPolicy.ReturnNavigationStopDistance,
                    now);
                routed |= result.Kind == NpcChaseMovementKind.Route;
                if (result.HasDestination)
                {
                    Assert.IsTrue(provider.IsSegmentTraversable(current, result.Destination));
                    current = result.Destination;
                }

                if (current.Distance2D(home) <= NpcCombatLeashPolicy.ReturnCompletionDistance)
                {
                    break;
                }

                now += TimeSpan.FromMilliseconds(150);
            }

            Assert.IsTrue(routed);
            Assert.IsTrue(
                current.Distance2D(home) <= NpcCombatLeashPolicy.ReturnCompletionDistance);
        }

        [TestMethod]
        public void Pf127UnsupportedCrossElevationTargetFailsBoundedlyWithoutPerTickSearch()
        {
            Pf127ChaseNavigationProvider provider = LoadPf127Provider();
            var service = new NpcChaseNavigationRuntimeService(provider);
            var start = new ChaseNavigationPoint(188.2448, 73.01637, 98.84238);
            var goal = new ChaseNavigationPoint(188.2448, 76.01637, 98.84238);

            ChaseRoutePlan route = provider.RequestRoute(
                start,
                goal,
                ChaseRouteSearchLimits.Default);
            NpcChaseUpdateResult first = service.UpdatePursuit(
                203748,
                50000,
                start,
                goal,
                3.0,
                Epoch);
            NpcChaseUpdateResult repeated = service.UpdatePursuit(
                203748,
                50000,
                start,
                goal,
                3.0,
                Epoch + TimeSpan.FromSeconds(1));

            Assert.AreEqual(ChaseRoutePlanStatus.Unreachable, route.Status);
            Assert.AreEqual(NpcChaseMovementKind.Hold, first.Kind);
            Assert.AreEqual(NpcChaseMovementKind.Hold, repeated.Kind);
            Assert.AreEqual(1, service.TotalRouteRequests);
        }

        [TestMethod]
        public void SharedNavigationSourceContainsNoVergilOrEnemyIdentityDependency()
        {
            string root = FindRepositoryRoot();
            string navigationFolder = Path.Combine(
                root,
                @"AORebirth\Server\ZoneEngine\Core\Navigation");
            foreach (string path in Directory.GetFiles(navigationFolder, "*.cs"))
            {
                string source = File.ReadAllText(path);
                Assert.IsFalse(source.Contains("Vergil"), Path.GetFileName(path));
                Assert.IsFalse(source.Contains("203748"), Path.GetFileName(path));
            }
        }

        [TestMethod]
        public void SharedMovementUsesExistingControllerPipelineAndValidatesBeforeMoveTo()
        {
            string root = FindRepositoryRoot();
            string source = File.ReadAllText(
                Path.Combine(
                    root,
                    @"AORebirth\Server\ZoneEngine\Core\Playfields\PlayfieldNpcCombatMovementRuntimeService.cs"));

            StringAssert.Contains(source, "this.chaseNavigation.UpdatePursuit(");
            StringAssert.Contains(source, "npcController.MoveTo(");
            StringAssert.Contains(source, "navigationResult.HasDestination");
            Assert.IsTrue(
                source.IndexOf("navigationResult.HasDestination", StringComparison.Ordinal)
                < source.IndexOf("npcController.MoveTo(", StringComparison.Ordinal));
        }

        [TestMethod]
        public void CombatCoordinatorRoutesBlockedAttacksWithoutChangingDamageCalculation()
        {
            string root = FindRepositoryRoot();
            string source = File.ReadAllText(
                Path.Combine(
                    root,
                    @"AORebirth\Server\ZoneEngine\Core\Playfields\NpcCombatTickCoordinator.cs"));

            StringAssert.Contains(source, "this.playfield.TryMoveNpcIntoCombatRange(attacker, target, attackSource.Range);");
            StringAssert.Contains(source, "this.playfield.IsNpcAttackPathTraversable(attacker, target)");
            StringAssert.Contains(source, "this.CalculateCombatDamage(attacker, attackSource)");
            StringAssert.Contains(source, "if (!this.CanApplyNpcDamage(");
            StringAssert.Contains(source, "this.playfield.HoldNpcAtCombatPosition(attacker, target);");
            Assert.IsFalse(source.Contains("VergilAeneidMonsterData") && source.Contains("TryMoveNpcIntoCombatRange"));
        }

        [TestMethod]
        public void LifecycleRuntimeOwnsSharedRouteCleanup()
        {
            string root = FindRepositoryRoot();
            string source = File.ReadAllText(
                Path.Combine(
                    root,
                    @"AORebirth\Server\ZoneEngine\Core\Playfields\NPCRuntimeService.cs"));

            StringAssert.Contains(source, "NpcChaseInvalidationReason.TargetLost");
            StringAssert.Contains(source, "NpcChaseInvalidationReason.Death");
            StringAssert.Contains(source, "NpcChaseInvalidationReason.Despawn");
            StringAssert.Contains(source, "NpcChaseInvalidationReason.LeashReset");
            StringAssert.Contains(source, "NpcChaseInvalidationReason.EncounterReset");
            StringAssert.Contains(source, "NpcChaseInvalidationReason.PlayfieldReset");
            StringAssert.Contains(source, "this.RegisterNpcHome(character);");
            StringAssert.Contains(source, "this.TryBeginLeashReturn(attacker)");
            StringAssert.Contains(source, "home.ReturningHome");
            StringAssert.Contains(source, "this.chaseNavigation.UpdateReturnToHome(");
            StringAssert.Contains(source, "new StopFightMessage");
            StringAssert.Contains(source, "this.capturedSubwayEncounters.NotifyCombatReset(npc)");
            StringAssert.Contains(source, "owner.Controller is PlayerController");
            StringAssert.Contains(source, "controller.State = CharacterState.Idle;");
            StringAssert.Contains(source, "controller.State = home.ControllerStateBeforeReturn;");

            string systemsSource = File.ReadAllText(
                Path.Combine(
                    root,
                    @"AORebirth\Server\ZoneEngine\Core\Playfields\PlayfieldRuntimeSystems.cs"));
            StringAssert.Contains(systemsSource, "this.npcChaseNavigation.Dispose();");
        }

        private static NpcChaseNavigationRuntimeService Service(IPlayfieldChaseNavigationProvider provider)
        {
            return new NpcChaseNavigationRuntimeService(provider, Limits(), new NpcChaseRouteFollower());
        }

        private static ChaseRouteSearchLimits Limits()
        {
            return new ChaseRouteSearchLimits(1.0, 5.0, 50.0, 1024, 8192, 2.0, 1.5, 64);
        }

        private static ChaseNavigationPoint Point(double x, double z)
        {
            return new ChaseNavigationPoint(x, 0.0, z);
        }

        private static ChaseNavigationPoint Interpolate(
            ChaseNavigationPoint start,
            ChaseNavigationPoint end,
            double factor)
        {
            return new ChaseNavigationPoint(
                start.X + ((end.X - start.X) * factor),
                start.Y + ((end.Y - start.Y) * factor),
                start.Z + ((end.Z - start.Z) * factor));
        }

        private static Pf127ChaseNavigationProvider LoadPf127Provider()
        {
            return new Pf127ChaseNavigationProvider(127, Pf127Geometry.Value);
        }

        private static PlayfieldCollisionGeometryLoadResult LoadPf127Geometry()
        {
            string root = FindRepositoryRoot();
            string path = Path.Combine(
                root,
                @"AORebirth\Server\ZoneEngine\Content\Captured\Subway\pf127-geometry.json");
            return Pf127CollisionGeometryLoader.LoadPath(path);
        }

        private static string FindRepositoryRoot()
        {
            DirectoryInfo directory = new DirectoryInfo(AppDomain.CurrentDomain.BaseDirectory);
            while (directory != null)
            {
                if (File.Exists(Path.Combine(directory.FullName, "AI_START_HERE.md")))
                {
                    return directory.FullName;
                }

                directory = directory.Parent;
            }

            throw new DirectoryNotFoundException("AORebirth repository root was not found.");
        }

        private sealed class TestNavigationProvider : IPlayfieldChaseNavigationProvider
        {
            private readonly ChaseNavigationCapability capability;

            private readonly BoundedGridChaseRoutePlanner planner = new BoundedGridChaseRoutePlanner();

            private readonly bool blockEverywhere;

            private readonly double minimumX;

            private readonly double maximumX;

            private readonly double minimumZ;

            private readonly double maximumZ;

            internal TestNavigationProvider(ChaseNavigationCapability capability)
                : this(capability, false, false, 0, 0, 0, 0)
            {
            }

            private TestNavigationProvider(
                ChaseNavigationCapability capability,
                bool wallEnabled,
                bool blockEverywhere,
                double minimumX,
                double maximumX,
                double minimumZ,
                double maximumZ)
            {
                this.capability = capability;
                this.WallEnabled = wallEnabled;
                this.blockEverywhere = blockEverywhere;
                this.minimumX = minimumX;
                this.maximumX = maximumX;
                this.minimumZ = minimumZ;
                this.maximumZ = maximumZ;
                this.Version = "geometry-v1";
            }

            internal int RequestCount { get; private set; }

            internal bool WallEnabled { get; set; }

            internal string Version { get; set; }

            public int PlayfieldResource
            {
                get { return 127; }
            }

            public ChaseNavigationCapability Capability
            {
                get { return this.capability; }
            }

            public string GeometryVersion
            {
                get { return this.Version; }
            }

            internal static TestNavigationProvider Clear()
            {
                return new TestNavigationProvider(
                    ChaseNavigationCapability.Supported,
                    false,
                    false,
                    0,
                    0,
                    0,
                    0);
            }

            internal static TestNavigationProvider WithWall(
                double minimumX,
                double maximumX,
                double minimumZ,
                double maximumZ)
            {
                return new TestNavigationProvider(
                    ChaseNavigationCapability.Supported,
                    true,
                    false,
                    minimumX,
                    maximumX,
                    minimumZ,
                    maximumZ);
            }

            internal static TestNavigationProvider BlockedEverywhere()
            {
                return new TestNavigationProvider(
                    ChaseNavigationCapability.Supported,
                    false,
                    true,
                    0,
                    0,
                    0,
                    0);
            }

            public bool TryProjectToSurface(
                ChaseNavigationPoint reference,
                double x,
                double z,
                out ChaseNavigationPoint projected)
            {
                projected = new ChaseNavigationPoint(x, reference.Y, z);
                return this.capability == ChaseNavigationCapability.Supported;
            }

            public bool IsSegmentTraversable(ChaseNavigationPoint start, ChaseNavigationPoint end)
            {
                if (this.capability != ChaseNavigationCapability.Supported || this.blockEverywhere)
                {
                    return false;
                }

                return !this.WallEnabled
                       || !SegmentIntersectsRectangle(
                           start,
                           end,
                           this.minimumX,
                           this.maximumX,
                           this.minimumZ,
                           this.maximumZ);
            }

            public bool IsAttackLineTraversable(ChaseNavigationPoint start, ChaseNavigationPoint end)
            {
                return this.IsSegmentTraversable(start, end);
            }

            public ChaseRoutePlan RequestRoute(
                ChaseNavigationPoint start,
                ChaseNavigationPoint goal,
                ChaseRouteSearchLimits limits)
            {
                this.RequestCount++;
                return this.planner.Plan(this, start, goal, limits);
            }

            public bool IsRouteCurrent(ChaseRoutePlan route)
            {
                return route != null
                       && route.IsSuccess
                       && string.Equals(route.GeometryVersion, this.Version, StringComparison.Ordinal);
            }

            private static bool SegmentIntersectsRectangle(
                ChaseNavigationPoint start,
                ChaseNavigationPoint end,
                double minimumX,
                double maximumX,
                double minimumZ,
                double maximumZ)
            {
                if (Inside(start, minimumX, maximumX, minimumZ, maximumZ)
                    || Inside(end, minimumX, maximumX, minimumZ, maximumZ))
                {
                    return true;
                }

                return SegmentsIntersect(start.X, start.Z, end.X, end.Z, minimumX, minimumZ, maximumX, minimumZ)
                       || SegmentsIntersect(start.X, start.Z, end.X, end.Z, maximumX, minimumZ, maximumX, maximumZ)
                       || SegmentsIntersect(start.X, start.Z, end.X, end.Z, maximumX, maximumZ, minimumX, maximumZ)
                       || SegmentsIntersect(start.X, start.Z, end.X, end.Z, minimumX, maximumZ, minimumX, minimumZ);
            }

            private static bool Inside(
                ChaseNavigationPoint point,
                double minimumX,
                double maximumX,
                double minimumZ,
                double maximumZ)
            {
                return point.X >= minimumX
                       && point.X <= maximumX
                       && point.Z >= minimumZ
                       && point.Z <= maximumZ;
            }

            private static bool SegmentsIntersect(
                double ax,
                double az,
                double bx,
                double bz,
                double cx,
                double cz,
                double dx,
                double dz)
            {
                double first = Cross(ax, az, bx, bz, cx, cz);
                double second = Cross(ax, az, bx, bz, dx, dz);
                double third = Cross(cx, cz, dx, dz, ax, az);
                double fourth = Cross(cx, cz, dx, dz, bx, bz);
                return first * second <= 0.0 && third * fourth <= 0.0;
            }

            private static double Cross(
                double ax,
                double az,
                double bx,
                double bz,
                double px,
                double pz)
            {
                return ((bx - ax) * (pz - az)) - ((bz - az) * (px - ax));
            }
        }
    }
}
