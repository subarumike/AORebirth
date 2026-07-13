namespace SmokeLounge.AOtomation.Messaging.Tests
{
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;

    using Microsoft.VisualStudio.TestTools.UnitTesting;

    using SmokeLounge.AOtomation.Messaging.GameData;

    using ZoneEngine.Core.Playfields;

    [TestClass]
    public class PlayfieldVisibilityInterestStateTests
    {
        [TestMethod]
        public void InitializationBuildsBidirectionalStateOnlyForSelectedSources()
        {
            PlayfieldVisibilityInterestState<TestValue> state = NewState();
            TestValue recipient = Value(100, 0.0f, 0.0f);
            TestValue near = Value(20, 40.0f, 0.0f);
            TestValue nearest = Value(10, 10.0f, 0.0f);
            TestValue far = Value(30, 120.0f, 0.0f);
            state.Synchronize(new[] { recipient, near, nearest, far });

            TestValue[] selected = state.SelectInitialValues(recipient).ToArray();
            CollectionAssert.AreEqual(new[] { 10, 20 }, Ids(selected));
            foreach (TestValue source in selected)
            {
                Assert.IsTrue(state.MarkVisibleEntry(recipient, source));
            }

            Assert.IsFalse(state.IsInitializedRecipient(recipient.Identity));
            Assert.AreEqual(0, state.VisibleRecipientsForSource(nearest.Identity).Count);
            state.CompleteInitialRecipient(recipient);

            Assert.IsTrue(state.IsInitializedRecipient(recipient.Identity));
            CollectionAssert.AreEqual(new[] { 10, 20 }, Ids(state.VisibleSourcesForRecipient(recipient.Identity)));
            CollectionAssert.AreEqual(new[] { 100 }, Ids(state.VisibleRecipientsForSource(nearest.Identity)));
            Assert.IsTrue(state.CanReceive(nearest, recipient));
            Assert.IsFalse(state.CanReceive(far, recipient));
        }

        [TestMethod]
        public void SourceMovementEntersOnceWithoutResendOrUnrelatedRecipientFanout()
        {
            PlayfieldVisibilityInterestState<TestValue> state = NewState();
            TestValue nearRecipient = Value(100, 0.0f, 0.0f);
            TestValue unrelatedRecipient = Value(200, 300.0f, 0.0f);
            TestValue source = Value(10, 180.0f, 0.0f);
            state.Synchronize(new[] { nearRecipient, unrelatedRecipient, source });
            state.CompleteInitialRecipient(nearRecipient);
            state.CompleteInitialRecipient(unrelatedRecipient);
            var log = new TransitionLog();

            source.MoveTo(40.0f, 0.0f);
            Reconcile(state, source, log);
            Reconcile(state, source, log);

            CollectionAssert.AreEqual(new[] { "enter:100:10" }, log.Events.ToArray());
            Assert.IsTrue(state.CanReceive(source, nearRecipient));
            Assert.IsFalse(state.CanReceive(source, unrelatedRecipient));
        }

        [TestMethod]
        public void HysteresisLeavesOnceAndRequiresEnterRadiusForReentry()
        {
            PlayfieldVisibilityInterestState<TestValue> state = NewState();
            TestValue recipient = Value(100, 0.0f, 0.0f);
            TestValue source = Value(10, 70.0f, 0.0f);
            state.Synchronize(new[] { recipient, source });
            InitializeSelected(state, recipient);
            var log = new TransitionLog();

            source.MoveTo(90.0f, 0.0f);
            Reconcile(state, source, log);
            source.MoveTo(101.0f, 0.0f);
            Reconcile(state, source, log);
            Reconcile(state, source, log);
            source.MoveTo(90.0f, 0.0f);
            Reconcile(state, source, log);
            source.MoveTo(79.0f, 0.0f);
            Reconcile(state, source, log);

            CollectionAssert.AreEqual(
                new[] { "leave:100:10", "enter:100:10" },
                log.Events.ToArray());
            Assert.IsTrue(state.CanReceive(source, recipient));
        }

        [TestMethod]
        public void RecipientMovementUsesDeterministicEnterAndLeaveOrder()
        {
            PlayfieldVisibilityInterestState<TestValue> state = NewState();
            TestValue recipient = Value(100, 300.0f, 0.0f);
            TestValue higherIdentity = Value(20, 10.0f, 0.0f);
            TestValue lowerIdentity = Value(10, -10.0f, 0.0f);
            state.Synchronize(new[] { recipient, higherIdentity, lowerIdentity });
            state.CompleteInitialRecipient(recipient);
            var log = new TransitionLog();

            recipient.MoveTo(0.0f, 0.0f);
            Reconcile(state, recipient, log);
            recipient.MoveTo(200.0f, 0.0f);
            Reconcile(state, recipient, log);

            CollectionAssert.AreEqual(
                new[]
                {
                    "enter:100:10",
                    "enter:100:20",
                    "leave:100:10",
                    "leave:100:20"
                },
                log.Events.ToArray());
        }

        [TestMethod]
        public void SpawnAndDespawnUpdateBothDirections()
        {
            PlayfieldVisibilityInterestState<TestValue> state = NewState();
            TestValue recipient = Value(100, 0.0f, 0.0f);
            TestValue spawned = Value(10, 20.0f, 0.0f);
            state.Synchronize(new[] { recipient });
            state.CompleteInitialRecipient(recipient);
            var log = new TransitionLog();

            Reconcile(state, spawned, log);
            Assert.IsTrue(state.CanReceive(spawned, recipient));
            CollectionAssert.AreEqual(new[] { 100 }, Ids(state.VisibleRecipientsForSource(spawned.Identity)));

            state.Unregister(spawned.Identity);
            Assert.IsFalse(state.CanReceive(spawned, recipient));
            Assert.AreEqual(0, state.VisibleSourcesForRecipient(recipient.Identity).Count);
            Assert.AreEqual(0, state.VisibleRecipientsForSource(spawned.Identity).Count);
        }

        [TestMethod]
        public void ForgetRecipientAndClearReleaseAllState()
        {
            PlayfieldVisibilityInterestState<TestValue> state = NewState();
            TestValue recipient = Value(100, 0.0f, 0.0f);
            TestValue source = Value(10, 20.0f, 0.0f);
            state.Synchronize(new[] { recipient, source });
            InitializeSelected(state, recipient);

            state.ForgetRecipient(recipient.Identity);
            Assert.IsFalse(state.IsInitializedRecipient(recipient.Identity));
            Assert.AreEqual(0, state.VisibleSourcesForRecipient(recipient.Identity).Count);
            Assert.AreEqual(0, state.VisibleRecipientsForSource(source.Identity).Count);

            state.Clear();
            Assert.AreEqual(0, state.LastCandidateInspectionCount);
            Assert.AreEqual(0, state.VisibleSourcesForRecipient(recipient.Identity).Count);
            Assert.AreEqual(0, state.VisibleRecipientsForSource(source.Identity).Count);
        }

        [TestMethod]
        public void PinnedOwnerReceivesAndRetainsRemoteSourceUntilPinIsRemoved()
        {
            PlayfieldVisibilityInterestState<TestValue> state = NewState();
            TestValue owner = Value(100, 0.0f, 0.0f);
            TestValue pet = Value(10, 500.0f, 0.0f);
            pet.OwnerInstance = owner.Identity.Instance;
            state.Synchronize(new[] { owner, pet });
            state.CompleteInitialRecipient(owner);
            var log = new TransitionLog();

            Reconcile(state, pet, log);
            pet.MoveTo(700.0f, 0.0f);
            Reconcile(state, pet, log);
            pet.OwnerInstance = 0;
            Reconcile(state, pet, log);

            CollectionAssert.AreEqual(
                new[] { "enter:100:10", "leave:100:10" },
                log.Events.ToArray());
            Assert.IsFalse(state.CanReceive(pet, owner));
        }

        [TestMethod]
        public void ConcurrentReconciliationReservesOneEntryBeforePacketDelivery()
        {
            PlayfieldVisibilityInterestState<TestValue> state = NewState();
            TestValue recipient = Value(100, 0.0f, 0.0f);
            TestValue source = Value(10, 20.0f, 0.0f);
            state.Synchronize(new[] { recipient, source });
            state.CompleteInitialRecipient(recipient);
            int deliveries = 0;

            Parallel.For(
                0,
                32,
                ignored => state.ReconcileInitializedRecipients(
                    source,
                    (target, entered) =>
                        {
                            Interlocked.Increment(ref deliveries);
                            Thread.SpinWait(50000);
                            return true;
                        },
                    (target, left) => Assert.Fail("Concurrent entry must not emit a leave.")));

            Assert.AreEqual(1, deliveries);
            Assert.IsTrue(state.CanReceive(source, recipient));
        }

        private static PlayfieldVisibilityInterestState<TestValue> NewState()
        {
            PlayfieldVisibilityInterestPolicy policy = PlayfieldVisibilityInterestPolicy.Default;
            return new PlayfieldVisibilityInterestState<TestValue>(
                policy,
                new UniformSpatialIndex<TestValue>(policy.CellSize),
                value => value.Identity,
                value => value.Position,
                (recipient, source) => recipient.Playfield == source.Playfield,
                recipient => recipient.Active,
                (recipient, source) => source.OwnerInstance == recipient.Identity.Instance);
        }

        private static TestValue Value(int identityInstance, float x, float z)
        {
            return new TestValue
                   {
                       Identity = new Identity
                                  {
                                      Type = IdentityType.CanbeAffected,
                                      Instance = identityInstance
                                  },
                       Position = new VisibilityPosition(x, 0.0f, z),
                       Playfield = 127,
                       Active = true
                   };
        }

        private static void InitializeSelected(
            PlayfieldVisibilityInterestState<TestValue> state,
            TestValue recipient)
        {
            foreach (TestValue source in state.SelectInitialValues(recipient))
            {
                state.MarkVisibleEntry(recipient, source);
            }

            state.CompleteInitialRecipient(recipient);
        }

        private static void Reconcile(
            PlayfieldVisibilityInterestState<TestValue> state,
            TestValue changed,
            TransitionLog log)
        {
            state.ReconcileInitializedRecipients(
                changed,
                (recipient, source) =>
                    {
                        log.Events.Add(
                            "enter:" + recipient.Identity.Instance + ":" + source.Identity.Instance);
                        return true;
                    },
                (recipient, sourceIdentity) => log.Events.Add(
                    "leave:" + recipient.Identity.Instance + ":" + sourceIdentity.Instance));
        }

        private static int[] Ids(IEnumerable<TestValue> values)
        {
            return values.Select(value => value.Identity.Instance).ToArray();
        }

        private sealed class TransitionLog
        {
            internal readonly List<string> Events = new List<string>();
        }

        private sealed class TestValue
        {
            internal Identity Identity { get; set; }
            internal VisibilityPosition Position { get; set; }
            internal int Playfield { get; set; }
            internal bool Active { get; set; }
            internal int OwnerInstance { get; set; }

            internal void MoveTo(float x, float z)
            {
                this.Position = new VisibilityPosition(x, 0.0f, z);
            }
        }
    }
}
