namespace SmokeLounge.AOtomation.Messaging.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Linq;

    using Microsoft.VisualStudio.TestTools.UnitTesting;

    using SmokeLounge.AOtomation.Messaging.GameData;

    using ZoneEngine.Core.Playfields;

    [TestClass]
    public class PlayfieldVisibilityInterestTests
    {
        [TestMethod]
        public void VisibilityPolicyDefaultsAreGloballyBounded()
        {
            PlayfieldVisibilityInterestPolicy policy = PlayfieldVisibilityInterestPolicy.Default;

            Assert.AreEqual(80.0f, policy.EnterRadius);
            Assert.AreEqual(100.0f, policy.LeaveRadius);
            Assert.AreEqual(32.0f, policy.CellSize);
            Assert.IsTrue(policy.EnterRadius >= PlayfieldVisibilityInterestPolicy.MinimumEnterRadius);
            Assert.IsTrue(policy.EnterRadius <= PlayfieldVisibilityInterestPolicy.MaximumEnterRadius);
            Assert.IsTrue(policy.LeaveRadius >= policy.EnterRadius);
            Assert.IsTrue(policy.LeaveRadius <= PlayfieldVisibilityInterestPolicy.MaximumLeaveRadius);
        }

        [TestMethod]
        public void VisibilityPolicyRejectsMalformedOrUnboundedValues()
        {
            ExpectException<ArgumentOutOfRangeException>(
                () => PlayfieldVisibilityInterestPolicy.Create(15.0f, 100.0f, 32.0f));
            ExpectException<ArgumentOutOfRangeException>(
                () => PlayfieldVisibilityInterestPolicy.Create(80.0f, 79.0f, 32.0f));
            ExpectException<ArgumentOutOfRangeException>(
                () => PlayfieldVisibilityInterestPolicy.Create(80.0f, 80.0f, 32.0f));
            ExpectException<ArgumentOutOfRangeException>(
                () => PlayfieldVisibilityInterestPolicy.Create(80.0f, 385.0f, 32.0f));
            ExpectException<ArgumentOutOfRangeException>(
                () => PlayfieldVisibilityInterestPolicy.Create(80.0f, 100.0f, 7.0f));
            ExpectException<ArgumentOutOfRangeException>(
                () => PlayfieldVisibilityInterestPolicy.Create(float.NaN, 100.0f, 32.0f));
            ExpectException<InvalidOperationException>(
                () => PlayfieldVisibilityInterestPolicy.FromSettings(
                    name => name == PlayfieldVisibilityInterestPolicy.EnterRadiusEnvironmentVariable
                                ? "unlimited"
                                : null));
        }

        [TestMethod]
        public void VisibilityPolicyAcceptsExplicitCentralOverrides()
        {
            var settings = new Dictionary<string, string>
                           {
                               { PlayfieldVisibilityInterestPolicy.EnterRadiusEnvironmentVariable, "96.5" },
                               { PlayfieldVisibilityInterestPolicy.LeaveRadiusEnvironmentVariable, "120" },
                               { PlayfieldVisibilityInterestPolicy.CellSizeEnvironmentVariable, "24" }
                           };

            PlayfieldVisibilityInterestPolicy policy =
                PlayfieldVisibilityInterestPolicy.FromSettings(
                    name => settings.ContainsKey(name) ? settings[name] : null);

            Assert.AreEqual(96.5f, policy.EnterRadius);
            Assert.AreEqual(120.0f, policy.LeaveRadius);
            Assert.AreEqual(24.0f, policy.CellSize);
        }

        [TestMethod]
        public void UniformGridInsertAndRadiusQueryAreDeterministic()
        {
            var index = NewIndex();
            TestCharacter nearest = Character(30, 5.0f, 0.0f, 0.0f);
            TestCharacter positiveBoundary = Character(20, 10.0f, 0.0f, 0.0f);
            TestCharacter negativeBoundary = Character(10, -10.0f, 0.0f, 0.0f);
            TestCharacter outside = Character(40, 10.1f, 0.0f, 0.0f);
            Upsert(index, positiveBoundary);
            Upsert(index, outside);
            Upsert(index, negativeBoundary);
            Upsert(index, nearest);

            IReadOnlyList<TestCharacter> result = index.Query(new VisibilityPosition(0.0f, 0.0f, 0.0f), 10.0f);

            CollectionAssert.AreEqual(
                new[] { 30, 10, 20 },
                result.Select(value => value.Identity.Instance).ToArray());
            Assert.IsTrue(index.LastCandidateInspectionCount >= result.Count);
            Assert.IsTrue(index.LastCandidateInspectionCount <= index.Count);
        }

        [TestMethod]
        public void UniformGridRejectsDuplicateIdentityObjects()
        {
            var index = NewIndex();
            TestCharacter first = Character(10, 0.0f, 0.0f, 0.0f);
            TestCharacter duplicate = Character(10, 1.0f, 0.0f, 0.0f);
            Upsert(index, first);

            ExpectException<InvalidOperationException>(() => Upsert(index, duplicate));

            Assert.AreEqual(1, index.Count);
        }

        [TestMethod]
        public void UniformGridUpsertUpdatesPositionWithoutDuplicating()
        {
            var index = NewIndex();
            TestCharacter character = Character(10, 0.0f, 0.0f, 0.0f);
            Upsert(index, character);
            character.Position = new VisibilityPosition(160.0f, 0.0f, 0.0f);
            Upsert(index, character);

            Assert.AreEqual(1, index.Count);
            Assert.AreEqual(0, index.Query(new VisibilityPosition(0.0f, 0.0f, 0.0f), 20.0f).Count);
            Assert.AreSame(
                character,
                index.Query(new VisibilityPosition(160.0f, 0.0f, 0.0f), 20.0f).Single());
        }

        [TestMethod]
        public void UniformGridVisibilityUsesAuthoritativeTwoDimensionalDistance()
        {
            var index = NewIndex();
            TestCharacter character = Character(10, 10.0f, 500.0f, 0.0f);
            Upsert(index, character);

            Assert.AreSame(
                character,
                index.Query(new VisibilityPosition(0.0f, -500.0f, 0.0f), 10.0f).Single());
        }

        [TestMethod]
        public void UniformGridRemoveAndClearReleaseAllState()
        {
            var index = NewIndex();
            TestCharacter first = Character(10, -1.0f, -1.0f, -1.0f);
            TestCharacter second = Character(20, 1.0f, 1.0f, 1.0f);
            Upsert(index, first);
            Upsert(index, second);

            Assert.IsTrue(index.Remove(first.Identity));
            Assert.IsFalse(index.Remove(first.Identity));
            Assert.AreEqual(1, index.Count);
            index.Clear();

            Assert.AreEqual(0, index.Count);
            Assert.AreEqual(0, index.LastCandidateInspectionCount);
            Assert.AreEqual(0, index.Query(new VisibilityPosition(0.0f, 0.0f, 0.0f), 20.0f).Count);
            Assert.AreEqual(0, index.LastCandidateInspectionCount);
        }

        [TestMethod]
        public void SeparatePlayfieldIndexesRemainIsolated()
        {
            var firstPlayfield = NewIndex();
            var secondPlayfield = NewIndex();
            TestCharacter first = Character(10, 0.0f, 0.0f, 0.0f);
            TestCharacter second = Character(20, 0.0f, 0.0f, 0.0f);
            Upsert(firstPlayfield, first);
            Upsert(secondPlayfield, second);

            Assert.AreSame(
                first,
                firstPlayfield.Query(new VisibilityPosition(0.0f, 0.0f, 0.0f), 20.0f).Single());
            Assert.AreSame(
                second,
                secondPlayfield.Query(new VisibilityPosition(0.0f, 0.0f, 0.0f), 20.0f).Single());
        }

        [TestMethod]
        public void Pf127SizedPopulationUsesBoundedCandidatesAcrossMovementAndChurn()
        {
            var index = NewIndex();
            var characters = new List<TestCharacter>();
            for (int ordinal = 0; ordinal < 259; ordinal++)
            {
                TestCharacter character = Character(
                    ordinal + 1,
                    (ordinal % 37) * 24.0f,
                    0.0f,
                    (ordinal / 37) * 24.0f);
                characters.Add(character);
                Upsert(index, character);
            }

            var center = new VisibilityPosition(432.0f, 0.0f, 72.0f);
            var initialQueryStopwatch = Stopwatch.StartNew();
            int initialSelected = AssertQueryMatchesOracle(index, characters, center, 80.0f);
            initialQueryStopwatch.Stop();
            int initialCandidateInspections = index.LastCandidateInspectionCount;
            Assert.AreEqual(56, initialCandidateInspections);
            Assert.IsTrue(index.LastCandidateInspectionCount < 259);

            var churnStopwatch = Stopwatch.StartNew();
            foreach (TestCharacter character in characters.Take(20))
            {
                character.Position = new VisibilityPosition(
                    center.X + (character.Identity.Instance % 5),
                    center.Y,
                    center.Z + (character.Identity.Instance % 7));
                Upsert(index, character);
            }

            foreach (TestCharacter character in characters.Skip(249))
            {
                Assert.IsTrue(index.Remove(character.Identity));
            }

            TestCharacter[] active = characters.Take(249).ToArray();
            Assert.AreEqual(249, index.Count);
            int churnSelected = AssertQueryMatchesOracle(index, active, center, 80.0f);
            churnStopwatch.Stop();
            Assert.AreEqual(71, index.LastCandidateInspectionCount);
            Assert.IsTrue(index.LastCandidateInspectionCount < index.Count);
            int initialPacketPreparations = initialSelected * 2;
            Assert.AreEqual(initialSelected * 2, initialPacketPreparations);
            Console.WriteLine(
                "PF127_VISIBILITY_INDEX total=259 initial_candidates={0} initial_selected={1} initial_packet_preparations={2} initial_query_ticks={3} active_after_churn={4} churn_candidates={5} churn_selected={6} churn_ticks={7}",
                initialCandidateInspections,
                initialSelected,
                initialPacketPreparations,
                initialQueryStopwatch.ElapsedTicks,
                index.Count,
                index.LastCandidateInspectionCount,
                churnSelected,
                churnStopwatch.ElapsedTicks);
        }

        [TestMethod]
        public void MultipleRecipientsProduceExplicitBoundaryDiffsAcrossMovementSpawnAndRemove()
        {
            var index = NewIndex();
            PlayfieldVisibilityInterestPolicy policy = PlayfieldVisibilityInterestPolicy.Default;
            TestCharacter first = Character(1, 10.0f, 0.0f, 0.0f, 0);
            TestCharacter second = Character(2, 79.0f, 0.0f, 0.0f, 1);
            TestCharacter third = Character(3, 90.0f, 0.0f, 0.0f, 2);
            TestCharacter fourth = Character(4, 101.0f, 0.0f, 0.0f, 0);
            TestCharacter fifth = Character(5, 170.0f, 0.0f, 0.0f, 1);
            foreach (TestCharacter source in new[] { first, second, third, fourth, fifth })
            {
                Upsert(index, source);
            }

            VisibilityDiff playerAInitial = Reconcile(
                index,
                new int[0],
                new VisibilityPosition(0.0f, 0.0f, 0.0f),
                policy);
            AssertDiff(playerAInitial, new[] { 1, 2 }, new int[0], new[] { 1, 2 }, 5);

            VisibilityDiff playerAMovedWithinHysteresis = Reconcile(
                index,
                playerAInitial.Visible,
                new VisibilityPosition(30.0f, 0.0f, 0.0f),
                policy);
            AssertDiff(
                playerAMovedWithinHysteresis,
                new[] { 3, 4 },
                new int[0],
                new[] { 1, 2, 3, 4 },
                6);

            VisibilityDiff playerAMovedAcrossLeaveBoundary = Reconcile(
                index,
                playerAMovedWithinHysteresis.Visible,
                new VisibilityPosition(130.0f, 0.0f, 0.0f),
                policy);
            AssertDiff(
                playerAMovedAcrossLeaveBoundary,
                new[] { 5 },
                new[] { 1 },
                new[] { 2, 3, 4, 5 },
                3);

            VisibilityDiff playerBInitial = Reconcile(
                index,
                new int[0],
                new VisibilityPosition(200.0f, 0.0f, 0.0f),
                policy);
            AssertDiff(playerBInitial, new[] { 5 }, new int[0], new[] { 5 }, 3);

            TestCharacter insertedSpawn = Character(6, 132.0f, 0.0f, 0.0f, 3);
            Upsert(index, insertedSpawn);
            VisibilityDiff playerAAfterSpawn = Reconcile(
                index,
                playerAMovedAcrossLeaveBoundary.Visible,
                new VisibilityPosition(130.0f, 0.0f, 0.0f),
                policy);
            VisibilityDiff playerBAfterSpawn = Reconcile(
                index,
                playerBInitial.Visible,
                new VisibilityPosition(200.0f, 0.0f, 0.0f),
                policy);
            AssertDiff(playerAAfterSpawn, new[] { 6 }, new int[0], new[] { 2, 3, 4, 5, 6 }, 5);
            AssertDiff(playerBAfterSpawn, new[] { 6 }, new int[0], new[] { 5, 6 }, 5);

            Assert.IsTrue(index.Remove(third.Identity));
            VisibilityDiff playerAAfterRemove = Reconcile(
                index,
                playerAAfterSpawn.Visible,
                new VisibilityPosition(130.0f, 0.0f, 0.0f),
                policy);
            VisibilityDiff playerBAfterRemove = Reconcile(
                index,
                playerBAfterSpawn.Visible,
                new VisibilityPosition(200.0f, 0.0f, 0.0f),
                policy);
            AssertDiff(playerAAfterRemove, new int[0], new[] { 3 }, new[] { 2, 4, 5, 6 }, 0);
            AssertDiff(playerBAfterRemove, new int[0], new int[0], new[] { 5, 6 }, 0);

            int packetPreparations = playerAInitial.EntryPacketPreparations
                                     + playerAMovedWithinHysteresis.EntryPacketPreparations
                                     + playerAMovedAcrossLeaveBoundary.EntryPacketPreparations
                                     + playerBInitial.EntryPacketPreparations
                                     + playerAAfterSpawn.EntryPacketPreparations
                                     + playerBAfterSpawn.EntryPacketPreparations;
            Assert.AreEqual(27, packetPreparations);
            Assert.AreEqual(2, playerAMovedAcrossLeaveBoundary.Leaving.Length + playerAAfterRemove.Leaving.Length);
            Console.WriteLine(
                "VISIBILITY_DIFF players=2 entry_packet_preparations={0} leave_notifications=2 a_initial_query_ticks={1} a_initial_diff_ticks={2} a_move_query_ticks={3} a_move_diff_ticks={4} spawn_a_query_ticks={5} spawn_a_diff_ticks={6} spawn_b_query_ticks={7} spawn_b_diff_ticks={8} remove_a_query_ticks={9} remove_a_diff_ticks={10}",
                packetPreparations,
                playerAInitial.QueryTicks,
                playerAInitial.DiffTicks,
                playerAMovedAcrossLeaveBoundary.QueryTicks,
                playerAMovedAcrossLeaveBoundary.DiffTicks,
                playerAAfterSpawn.QueryTicks,
                playerAAfterSpawn.DiffTicks,
                playerBAfterSpawn.QueryTicks,
                playerBAfterSpawn.DiffTicks,
                playerAAfterRemove.QueryTicks,
                playerAAfterRemove.DiffTicks);
        }

        [TestMethod]
        public void AllCapturedPf127CatalogRowsResolveProfilesAndEnterSharedSpatialIndex()
        {
            var catalog = new AORebirth.Core.Playfields.OrdinaryEnemyCatalog(
                new global::ZoneEngine.Core.Playfields.CapturedSubwayContentProvider(),
                new AORebirth.Core.Playfields.CapturedSubwayOrdinaryContentProvider());
            AORebirth.Core.Playfields.OrdinaryEnemyProfile[] profiles = catalog.GetProfiles();
            AORebirth.Core.Playfields.OrdinaryEnemySpawnDefinition[] spawns = catalog.GetSpawns();
            var index = NewIndex();

            Assert.AreEqual(321, spawns.Length);
            Assert.AreEqual(321, spawns.Select(value => value.SourceIdentity).Distinct().Count());
            Assert.AreEqual(321, spawns.Count(value => value.PlayfieldInstance == 127));
            foreach (AORebirth.Core.Playfields.OrdinaryEnemySpawnDefinition spawn in spawns)
            {
                AORebirth.Core.Playfields.OrdinaryEnemyProfile profile;
                Assert.IsTrue(catalog.TryGetProfile(spawn.ProfileKey, out profile));
                Assert.IsNotNull(profile);
                Upsert(index, Character(spawn.SourceIdentity, spawn.X, spawn.Y, spawn.Z));
            }

            Assert.AreEqual(321, index.Count);
            Assert.AreEqual(
                283,
                spawns.Count(
                    value => value.Disposition
                             == AORebirth.Core.Playfields.OrdinaryEnemyRuntimeDisposition.Active));
            Assert.AreEqual(
                38,
                spawns.Count(
                    value => value.Disposition
                             == AORebirth.Core.Playfields.OrdinaryEnemyRuntimeDisposition.Quarantined));
            Assert.AreEqual(26, profiles.Length);
            foreach (AORebirth.Core.Playfields.OrdinaryEnemySpawnDefinition spawn in spawns)
            {
                Assert.AreEqual(
                    spawn.SourceIdentity,
                    index.Query(new VisibilityPosition(spawn.X, spawn.Y, spawn.Z), 0.01f)
                        .Single(value => value.Identity.Instance == spawn.SourceIdentity)
                        .Identity.Instance);
            }
        }

        private static UniformSpatialIndex<TestCharacter> NewIndex()
        {
            return new UniformSpatialIndex<TestCharacter>(
                PlayfieldVisibilityInterestPolicy.Default.CellSize);
        }

        private static TestCharacter Character(int instance, float x, float y, float z)
        {
            return Character(instance, x, y, z, 0);
        }

        private static TestCharacter Character(
            int instance,
            float x,
            float y,
            float z,
            int weaponDefinitionCount)
        {
            return new TestCharacter(
                new Identity { Type = IdentityType.CanbeAffected, Instance = instance },
                new VisibilityPosition(x, y, z),
                weaponDefinitionCount);
        }

        private static void Upsert(
            UniformSpatialIndex<TestCharacter> index,
            TestCharacter character)
        {
            index.Upsert(character.Identity, character.Position, character);
        }

        private static int AssertQueryMatchesOracle(
            UniformSpatialIndex<TestCharacter> index,
            IEnumerable<TestCharacter> characters,
            VisibilityPosition center,
            float radius)
        {
            int[] expected = characters
                .Select(
                    value => new
                             {
                                 Character = value,
                                 DistanceSquared = DistanceSquared(center, value.Position)
                             })
                .Where(value => value.DistanceSquared <= ((double)radius * radius))
                .OrderBy(value => value.DistanceSquared)
                .ThenBy(value => (int)value.Character.Identity.Type)
                .ThenBy(value => value.Character.Identity.Instance)
                .Select(value => value.Character.Identity.Instance)
                .ToArray();
            int[] actual = index.Query(center, radius)
                .Select(value => value.Identity.Instance)
                .ToArray();
            CollectionAssert.AreEqual(expected, actual);
            return actual.Length;
        }

        private static double DistanceSquared(VisibilityPosition first, VisibilityPosition second)
        {
            double x = first.X - second.X;
            double z = first.Z - second.Z;
            return (x * x) + (z * z);
        }

        private static VisibilityDiff Reconcile(
            UniformSpatialIndex<TestCharacter> index,
            IEnumerable<int> currentlyVisible,
            VisibilityPosition center,
            PlayfieldVisibilityInterestPolicy policy)
        {
            var queryStopwatch = Stopwatch.StartNew();
            IReadOnlyList<TestCharacter> leaveCandidates = index.Query(center, policy.LeaveRadius);
            queryStopwatch.Stop();
            int candidateInspections = index.LastCandidateInspectionCount;

            var diffStopwatch = Stopwatch.StartNew();
            var current = new HashSet<int>(currentlyVisible);
            var candidatesByIdentity = leaveCandidates.ToDictionary(value => value.Identity.Instance);
            TestCharacter[] enteringCharacters = leaveCandidates
                .Where(
                    value => !current.Contains(value.Identity.Instance)
                             && DistanceSquared(center, value.Position)
                             <= ((double)policy.EnterRadius * policy.EnterRadius))
                .ToArray();
            int[] leaving = current
                .Where(identity => !candidatesByIdentity.ContainsKey(identity))
                .OrderBy(identity => identity)
                .ToArray();
            foreach (int identity in leaving)
            {
                current.Remove(identity);
            }

            foreach (TestCharacter entering in enteringCharacters)
            {
                current.Add(entering.Identity.Instance);
            }

            int[] visible = current.OrderBy(identity => identity).ToArray();
            int entryPacketPreparations = enteringCharacters.Sum(
                value => 2 + value.WeaponDefinitionCount);
            diffStopwatch.Stop();
            return new VisibilityDiff(
                enteringCharacters.Select(value => value.Identity.Instance).ToArray(),
                leaving,
                visible,
                candidateInspections,
                entryPacketPreparations,
                queryStopwatch.ElapsedTicks,
                diffStopwatch.ElapsedTicks);
        }

        private static void AssertDiff(
            VisibilityDiff actual,
            int[] entering,
            int[] leaving,
            int[] visible,
            int entryPacketPreparations)
        {
            CollectionAssert.AreEqual(entering, actual.Entering);
            CollectionAssert.AreEqual(leaving, actual.Leaving);
            CollectionAssert.AreEqual(visible, actual.Visible);
            Assert.AreEqual(entryPacketPreparations, actual.EntryPacketPreparations);
            Assert.IsTrue(actual.CandidateInspections >= actual.Entering.Length);
        }

        private static TException ExpectException<TException>(Action action)
            where TException : Exception
        {
            try
            {
                action();
            }
            catch (TException exception)
            {
                return exception;
            }

            Assert.Fail("Expected exception of type " + typeof(TException).Name + ".");
            return null;
        }

        private sealed class TestCharacter
        {
            internal TestCharacter(
                Identity identity,
                VisibilityPosition position,
                int weaponDefinitionCount)
            {
                this.Identity = identity;
                this.Position = position;
                this.WeaponDefinitionCount = weaponDefinitionCount;
            }

            internal Identity Identity { get; private set; }
            internal VisibilityPosition Position { get; set; }
            internal int WeaponDefinitionCount { get; private set; }
        }

        private sealed class VisibilityDiff
        {
            internal VisibilityDiff(
                int[] entering,
                int[] leaving,
                int[] visible,
                int candidateInspections,
                int entryPacketPreparations,
                long queryTicks,
                long diffTicks)
            {
                this.Entering = entering;
                this.Leaving = leaving;
                this.Visible = visible;
                this.CandidateInspections = candidateInspections;
                this.EntryPacketPreparations = entryPacketPreparations;
                this.QueryTicks = queryTicks;
                this.DiffTicks = diffTicks;
            }

            internal int[] Entering { get; private set; }
            internal int[] Leaving { get; private set; }
            internal int[] Visible { get; private set; }
            internal int CandidateInspections { get; private set; }
            internal int EntryPacketPreparations { get; private set; }
            internal long QueryTicks { get; private set; }
            internal long DiffTicks { get; private set; }
        }
    }
}
