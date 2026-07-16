namespace SmokeLounge.AOtomation.Messaging.Tests
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Threading.Tasks;

    using AOSharpLiveCapture;

    using Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestClass]
    public class CaptureRuntimeSafetyTests
    {
        [TestMethod]
        public void BoundaryCircuitBreakerContainsFaultAndDisablesLaterCollectorUpdates()
        {
            var breaker = new CaptureRuntimeCircuitBreaker();
            int recordedErrors = 0;
            int actionRuns = 0;

            bool failed = breaker.TryExecute(
                () =>
                {
                    actionRuns++;
                    throw new NullReferenceException("AO wrapper failed");
                },
                ex =>
                {
                    recordedErrors++;
                    throw new InvalidOperationException("Error logging failed");
                });

            bool retried = breaker.TryExecute(
                () => actionRuns++,
                ex => recordedErrors++);

            Assert.IsFalse(failed);
            Assert.IsFalse(retried);
            Assert.IsTrue(breaker.IsTripped);
            Assert.AreEqual(1, breaker.FaultCount);
            Assert.AreEqual(1, recordedErrors);
            Assert.AreEqual(1, actionRuns);
        }

        [TestMethod]
        public void SnapshotContainsThrowingCollectionAndSkipsThrowingCharacterWrapper()
        {
            var errors = new List<string>();
            List<int> snapshots;

            bool collectionSucceeded = CaptureRuntimeSafety.TrySnapshot<ThrowingCharacter, int>(
                () => { throw new NullReferenceException("AO collection failed"); },
                character => character.Identity,
                (phase, ex) => errors.Add(phase + ":" + ex.GetType().Name),
                out snapshots);

            Assert.IsFalse(collectionSucceeded);
            Assert.AreEqual(0, snapshots.Count);
            CollectionAssert.Contains(errors, "collection:NullReferenceException");

            errors.Clear();
            bool wrapperSucceeded = CaptureRuntimeSafety.TrySnapshot<ThrowingCharacter, int>(
                () => new[]
                {
                    new ThrowingCharacter(() => { throw new NullReferenceException("AO identity failed"); }),
                    new ThrowingCharacter(() => 42)
                },
                character => character.Identity,
                (phase, ex) => errors.Add(phase + ":" + ex.GetType().Name),
                out snapshots);

            Assert.IsTrue(wrapperSucceeded);
            CollectionAssert.AreEqual(new[] { 42 }, snapshots);
            CollectionAssert.Contains(errors, "character:NullReferenceException");
        }

        [TestMethod]
        public void CombatRequestGateCannotClearNewerConcurrentGeneration()
        {
            var gate = new CaptureCombatRequestGate();
            DateTime nowUtc = new DateTime(2026, 7, 14, 12, 0, 0, DateTimeKind.Utc);

            gate.Request();
            long firstGeneration;
            Assert.IsTrue(gate.TryBegin(nowUtc, TimeSpan.FromSeconds(1), out firstGeneration));

            gate.Request();
            Assert.IsFalse(gate.Complete(firstGeneration));
            Assert.IsTrue(gate.IsPending);

            long secondGeneration;
            Assert.IsTrue(gate.TryBegin(nowUtc, TimeSpan.FromSeconds(1), out secondGeneration));
            Assert.AreNotEqual(firstGeneration, secondGeneration);
            Assert.IsTrue(gate.Complete(secondGeneration));
            Assert.IsFalse(gate.IsPending);
        }

        [TestMethod]
        public void CombatRequestGateRetainsPendingRequestAcrossRetryBackoff()
        {
            var gate = new CaptureCombatRequestGate();
            DateTime nowUtc = new DateTime(2026, 7, 14, 12, 0, 0, DateTimeKind.Utc);

            gate.Request();
            long firstGeneration;
            Assert.IsTrue(gate.TryBegin(nowUtc, TimeSpan.FromSeconds(1), out firstGeneration));

            gate.MarkRetryRequired();
            long retryGeneration;
            Assert.IsFalse(
                gate.TryBegin(nowUtc.AddMilliseconds(999), TimeSpan.FromSeconds(1), out retryGeneration));
            Assert.IsTrue(gate.IsPending);
            Assert.IsTrue(
                gate.TryBegin(nowUtc.AddSeconds(1), TimeSpan.FromSeconds(1), out retryGeneration));
            Assert.AreNotEqual(firstGeneration, retryGeneration);
        }

        [TestMethod]
        public void CallbackBoundaryContainsAndDurablyRecordsFullExceptionByCallback()
        {
            string directory = Path.Combine(Path.GetTempPath(), "aorebirth-callback-boundary-" + Guid.NewGuid().ToString("N"));
            string errorPath = Path.Combine(directory, "capture-callback-errors.log");
            Directory.CreateDirectory(directory);

            try
            {
                var boundary = new CaptureCallbackBoundary();
                boundary.BeginSession(errorPath, errorPath);

                bool failed = boundary.Dispatch(
                    "DynelManager.CharInPlay",
                    () =>
                    {
                        throw new InvalidOperationException(
                            "invalid AO wrapper",
                            new NullReferenceException("native character pointer unavailable"));
                    });
                bool recovered = boundary.Dispatch("DynelManager.CharInPlay", () => { });

                CaptureCallbackBoundarySnapshot snapshot = boundary.Snapshot();
                CaptureCallbackCounterSnapshot counter = Array.Find(
                    snapshot.Counters,
                    item => item.CallbackName == "DynelManager.CharInPlay");
                string durableEvidence = File.ReadAllText(errorPath);

                Assert.IsFalse(failed);
                Assert.IsTrue(recovered);
                Assert.AreEqual(2L, snapshot.TotalInvocationCount);
                Assert.AreEqual(1L, snapshot.TotalErrorCount);
                Assert.AreEqual(0L, snapshot.ErrorLogWriteFailureCount);
                Assert.IsNotNull(counter);
                Assert.AreEqual(2L, counter.InvocationCount);
                Assert.AreEqual(1L, counter.ErrorCount);
                StringAssert.Contains(durableEvidence, "callback=DynelManager.CharInPlay");
                StringAssert.Contains(durableEvidence, "System.InvalidOperationException: invalid AO wrapper");
                StringAssert.Contains(durableEvidence, "System.NullReferenceException: native character pointer unavailable");
            }
            finally
            {
                Directory.Delete(directory, true);
            }
        }

        [TestMethod]
        public void CallbackBoundaryIsThreadSafeAndLoggingFailureCannotEscape()
        {
            string directory = Path.Combine(Path.GetTempPath(), "aorebirth-callback-boundary-blocked-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);

            try
            {
                var boundary = new CaptureCallbackBoundary();
                boundary.BeginSession(directory, directory);

                Parallel.For(
                    0,
                    32,
                    index => boundary.Dispatch(
                        "Game.OnUpdate",
                        () =>
                        {
                            if (index % 4 == 0)
                            {
                                throw new InvalidOperationException("parallel callback failure");
                            }
                        }));

                CaptureCallbackBoundarySnapshot snapshot = boundary.Snapshot();
                CaptureCallbackCounterSnapshot counter = Array.Find(
                    snapshot.Counters,
                    item => item.CallbackName == "Game.OnUpdate");

                Assert.AreEqual(32L, snapshot.TotalInvocationCount);
                Assert.AreEqual(8L, snapshot.TotalErrorCount);
                Assert.AreEqual(8L, snapshot.ErrorLogWriteFailureCount);
                Assert.IsNotNull(counter);
                Assert.AreEqual(32L, counter.InvocationCount);
                Assert.AreEqual(8L, counter.ErrorCount);
            }
            finally
            {
                Directory.Delete(directory, true);
            }
        }

        private sealed class ThrowingCharacter
        {
            private readonly Func<int> getIdentity;

            public ThrowingCharacter(Func<int> getIdentity)
            {
                this.getIdentity = getIdentity;
            }

            public int Identity
            {
                get { return this.getIdentity(); }
            }
        }
    }
}
