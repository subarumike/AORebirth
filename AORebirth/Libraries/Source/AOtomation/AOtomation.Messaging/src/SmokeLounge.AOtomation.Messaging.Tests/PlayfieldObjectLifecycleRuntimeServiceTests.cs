namespace SmokeLounge.AOtomation.Messaging.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Threading;

    using AORebirth.Core.Entities;
    using AORebirth.Core.Playfields;

    using Microsoft.VisualStudio.TestTools.UnitTesting;

    using SmokeLounge.AOtomation.Messaging.GameData;

    [TestClass]
    public class PlayfieldObjectLifecycleRuntimeServiceTests
    {
        [TestMethod]
        public void ProcessPendingCorpseSpawnsDropsMissingStateAndProcessesValidDueCorpseOnce()
        {
            const int missingStateKey = 1001;
            Identity deadNpcIdentity = new Identity { Instance = 1002 };
            Identity corpseIdentity = new Identity { Type = IdentityType.Corpse, Instance = 2002 };
            var dueCorpse = new TestCorpseState
            {
                SpawnsAtUtc = DateTime.UtcNow.AddSeconds(-1),
                CorpseIdentity = corpseIdentity,
                DeadNpcIdentity = deadNpcIdentity
            };
            var pending = new Dictionary<int, TestCorpseState>
            {
                { missingStateKey, null },
                { deadNpcIdentity.Instance, dueCorpse }
            };
            var target = new TestCharacter();
            int registrations = 0;
            int traces = 0;
            int updates = 0;

            var service = new PlayfieldObjectLifecycleRuntimeService();
            Action process = () => service.ProcessPendingCorpseSpawns(
                pending,
                corpse => corpse.SpawnsAtUtc,
                corpse => corpse.CorpseIdentity,
                corpse => corpse.DeadNpcIdentity,
                identity => identity == deadNpcIdentity ? target : null,
                (character, identity) =>
                {
                    registrations++;
                    Assert.AreSame(target, character);
                    Assert.AreEqual(corpseIdentity, identity);
                    return true;
                },
                (deadNpc, corpse) => Assert.Fail("A valid due corpse must not fail registration."),
                (corpse, deadNpc) =>
                {
                    traces++;
                    Assert.AreEqual(corpseIdentity, corpse);
                    Assert.AreEqual(deadNpcIdentity, deadNpc);
                },
                (character, corpse) =>
                {
                    updates++;
                    Assert.AreSame(target, character);
                    Assert.AreEqual(corpseIdentity, corpse);
                });

            process();
            process();

            Assert.AreEqual(0, pending.Count, "Missing and completed states must leave the pending queue.");
            Assert.AreEqual(1, registrations, "The valid corpse must register exactly once.");
            Assert.AreEqual(1, traces, "The valid corpse must trace exactly once.");
            Assert.AreEqual(1, updates, "The valid corpse update must send exactly once.");
        }

        [TestMethod]
        public void ProcessPendingCorpseSpawnsRunsSelectorOutsideMonitorAndPreservesSameKeyReplacement()
        {
            const int deadNpcInstance = 3001;
            Identity deadNpcIdentity = new Identity { Instance = deadNpcInstance };
            var original = new TestCorpseState
            {
                SpawnsAtUtc = DateTime.UtcNow.AddSeconds(-1),
                CorpseIdentity = new Identity { Type = IdentityType.Corpse, Instance = 4001 },
                DeadNpcIdentity = deadNpcIdentity
            };
            var replacement = new TestCorpseState
            {
                SpawnsAtUtc = DateTime.UtcNow.AddMinutes(1),
                CorpseIdentity = new Identity { Type = IdentityType.Corpse, Instance = 4002 },
                DeadNpcIdentity = deadNpcIdentity
            };
            var pending = new Dictionary<int, TestCorpseState>
            {
                { deadNpcInstance, original }
            };
            var selectorEntered = new ManualResetEvent(false);
            var replacementInserted = new ManualResetEvent(false);
            bool selectorHeldMonitor = true;
            int registrations = 0;
            var service = new PlayfieldObjectLifecycleRuntimeService();

            var producer = new Thread(
                () =>
                {
                    if (!selectorEntered.WaitOne(5000))
                    {
                        return;
                    }

                    lock (pending)
                    {
                        pending[deadNpcInstance] = replacement;
                    }

                    replacementInserted.Set();
                });
            producer.Start();

            service.ProcessPendingCorpseSpawns(
                pending,
                corpse =>
                {
                    if (ReferenceEquals(corpse, original))
                    {
                        selectorHeldMonitor = Monitor.IsEntered(pending);
                        selectorEntered.Set();
                        Assert.IsTrue(
                            replacementInserted.WaitOne(5000),
                            "Producer could not replace the state while the due-time selector was running.");
                    }

                    return corpse.SpawnsAtUtc;
                },
                corpse => corpse.CorpseIdentity,
                corpse => corpse.DeadNpcIdentity,
                identity => new TestCharacter(),
                (character, identity) =>
                {
                    registrations++;
                    return true;
                },
                (deadNpc, corpse) => Assert.Fail("A valid due corpse must not fail registration."),
                (corpse, deadNpc) => { },
                (character, corpse) => { });

            Assert.IsTrue(producer.Join(5000), "Producer did not finish.");

            Assert.IsFalse(
                selectorHeldMonitor,
                "Caller selectors must execute outside the pending corpse queue monitor.");
            Assert.AreEqual(1, pending.Count, "The concurrently scheduled event must remain pending.");
            Assert.AreSame(replacement, pending[deadNpcInstance]);
            Assert.AreEqual(0, registrations, "A replaced snapshot must not emit stale corpse callbacks.");

            replacement.SpawnsAtUtc = DateTime.UtcNow.AddSeconds(-1);
            service.ProcessPendingCorpseSpawns(
                pending,
                corpse => corpse.SpawnsAtUtc,
                corpse => corpse.CorpseIdentity,
                corpse => corpse.DeadNpcIdentity,
                identity => new TestCharacter(),
                (character, identity) =>
                {
                    registrations++;
                    return true;
                },
                (deadNpc, corpse) => Assert.Fail("The preserved event must not fail registration."),
                (corpse, deadNpc) => { },
                (character, corpse) => { });

            Assert.AreEqual(0, pending.Count, "The preserved event must process normally once due.");
            Assert.AreEqual(1, registrations, "Only the preserved replacement must process.");
        }

        [TestMethod]
        public void DespawnCorpsesRunsPredicateOutsideMonitorAndPreservesSameKeyReplacement()
        {
            const int deadNpcInstance = 5001;
            Identity deadNpcIdentity = new Identity { Instance = deadNpcInstance };
            var original = new TestCorpseState
            {
                CorpseIdentity = new Identity { Type = IdentityType.Corpse, Instance = 6001 },
                DeadNpcIdentity = deadNpcIdentity
            };
            var replacement = new TestCorpseState
            {
                CorpseIdentity = new Identity { Type = IdentityType.Corpse, Instance = 6002 },
                DeadNpcIdentity = deadNpcIdentity
            };
            var pending = new Dictionary<int, TestCorpseState>
            {
                { deadNpcInstance, original }
            };
            var corpses = new Dictionary<int, TestCorpseState>();
            bool predicateHeldMonitor = true;
            var service = new PlayfieldObjectLifecycleRuntimeService();

            int removed = service.DespawnCorpses(
                pending,
                corpses,
                (name, identity) =>
                {
                    predicateHeldMonitor = Monitor.IsEntered(pending);
                    lock (pending)
                    {
                        pending[deadNpcInstance] = replacement;
                    }

                    return true;
                },
                corpse => "Remains",
                corpse => corpse.DeadNpcIdentity,
                corpseInstance => Assert.Fail("No registered corpse should be despawned."));

            Assert.IsFalse(
                predicateHeldMonitor,
                "Caller predicates must execute outside the pending corpse queue monitor.");
            Assert.AreEqual(0, removed, "A stale snapshot must not report the replacement as removed.");
            Assert.AreEqual(1, pending.Count, "The same-key replacement must remain pending.");
            Assert.AreSame(replacement, pending[deadNpcInstance]);
        }

        private sealed class TestCorpseState
        {
            internal DateTime SpawnsAtUtc { get; set; }

            internal Identity CorpseIdentity { get; set; }

            internal Identity DeadNpcIdentity { get; set; }
        }

        private sealed class TestCharacter : ICharacter
        {
        }
    }
}
