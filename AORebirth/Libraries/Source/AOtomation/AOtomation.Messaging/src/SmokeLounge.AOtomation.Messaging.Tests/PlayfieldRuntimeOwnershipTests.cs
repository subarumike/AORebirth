namespace SmokeLounge.AOtomation.Messaging.Tests
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Runtime.CompilerServices;
    using System.Threading;

    using Microsoft.VisualStudio.TestTools.UnitTesting;

    using AORebirth.Core.Playfields;

    using ZoneEngine.Core;
    using ZoneEngine.Core.Playfields;

    [TestClass]
    public class PlayfieldRuntimeOwnershipTests
    {
        [TestMethod]
        public void FirstEntryInitializesOnceAndReentryReusesPopulationAndCombatBinding()
        {
            int constructionCount = 0;
            int populationInitializationCount = 0;
            int combatBindingCount = 0;
            var registry =
                new RuntimeOwnershipRegistry<int, FakePlayfieldRuntime>(
                    key =>
                    {
                        Interlocked.Increment(ref constructionCount);
                        Interlocked.Increment(ref populationInitializationCount);
                        Interlocked.Increment(ref combatBindingCount);
                        return new FakePlayfieldRuntime(key);
                    });

            FakePlayfieldRuntime firstEntry = registry.GetOrCreate(1931);
            FakePlayfieldRuntime secondEntry = registry.GetOrCreate(1931);

            Assert.AreSame(firstEntry, secondEntry);
            Assert.AreEqual(1, constructionCount);
            Assert.AreEqual(1, populationInitializationCount);
            Assert.AreEqual(1, combatBindingCount);
        }

        [TestMethod]
        public void NearSimultaneousEntriesInitializeOneRuntime()
        {
            int constructionCount = 0;
            var registry =
                new RuntimeOwnershipRegistry<int, FakePlayfieldRuntime>(
                    key =>
                    {
                        Interlocked.Increment(ref constructionCount);
                        Thread.Sleep(25);
                        return new FakePlayfieldRuntime(key);
                    });
            var start = new ManualResetEvent(false);
            var results = new FakePlayfieldRuntime[8];
            var threads = new Thread[results.Length];

            for (int i = 0; i < threads.Length; i++)
            {
                int resultIndex = i;
                threads[i] =
                    new Thread(
                        () =>
                        {
                            start.WaitOne();
                            results[resultIndex] = registry.GetOrCreate(1931);
                        });
                threads[i].Start();
            }

            start.Set();
            foreach (Thread thread in threads)
            {
                Assert.IsTrue(thread.Join(5000));
            }

            Assert.AreEqual(1, constructionCount);
            Assert.IsTrue(results.All(runtime => object.ReferenceEquals(results[0], runtime)));
        }

        [TestMethod]
        public void ReplacementDisposesOldRuntimeBeforeConstructionAndOldScheduledWorkIsInert()
        {
            int constructionCount = 0;
            FakePlayfieldRuntime first = null;
            var registry =
                new RuntimeOwnershipRegistry<int, FakePlayfieldRuntime>(
                    key =>
                    {
                        int currentConstruction = Interlocked.Increment(ref constructionCount);
                        if (currentConstruction == 2)
                        {
                            Assert.IsTrue(first.Disposed, "The previous runtime must retire before replacement construction.");
                        }

                        return new FakePlayfieldRuntime(key);
                    });

            first = registry.GetOrCreate(1931);
            first.CombatTick();
            first.MoveTick();
            first.RespawnTick();
            first.VisibilityUpdate();

            FakePlayfieldRuntime replacement = registry.Replace(1931);
            first.CombatTick();
            first.MoveTick();
            first.RespawnTick();
            first.VisibilityUpdate();

            Assert.AreNotSame(first, replacement);
            Assert.IsTrue(first.Disposed);
            Assert.AreEqual(1, first.CombatTicks);
            Assert.AreEqual(1, first.MoveTicks);
            Assert.AreEqual(1, first.RespawnTicks);
            Assert.AreEqual(1, first.VisibilityUpdates);
            Assert.AreSame(replacement, registry.GetOrCreate(1931));
        }

        [TestMethod]
        public void OtherPlayfieldsKeepIndependentPersistentRuntimes()
        {
            int constructionCount = 0;
            var registry =
                new RuntimeOwnershipRegistry<int, FakePlayfieldRuntime>(
                    key =>
                    {
                        Interlocked.Increment(ref constructionCount);
                        return new FakePlayfieldRuntime(key);
                    });

            FakePlayfieldRuntime subway = registry.GetOrCreate(127);
            FakePlayfieldRuntime temple = registry.GetOrCreate(1931);

            Assert.AreSame(subway, registry.GetOrCreate(127));
            Assert.AreSame(temple, registry.GetOrCreate(1931));
            Assert.AreNotSame(subway, temple);
            Assert.AreEqual(2, constructionCount);
        }

        [TestMethod]
        public void ProductionOwnershipAndRetirementPathsAreConnected()
        {
            string repositoryRoot = FindRepositoryRoot();
            string zoneServerText = Read(repositoryRoot, @"Core\ZoneServer.cs");
            string playfieldText = Read(repositoryRoot, @"Core\Playfields\Playfield.cs");
            string runtimeSystemsText = Read(repositoryRoot, @"Core\Playfields\PlayfieldRuntimeSystems.cs");
            string npcRuntimeText = Read(repositoryRoot, @"Core\Playfields\NPCRuntimeService.cs");
            string corpseLifecycleText = Read(
                repositoryRoot,
                @"Core\Playfields\NpcCorpseLifecycleCoordinator.cs");
            string combatTickText = Read(repositoryRoot, @"Core\Playfields\NpcCombatTickCoordinator.cs");
            string dynelRegistryText = Read(repositoryRoot, @"Core\Playfields\PlayfieldDynelRegistry.cs");

            Assert.IsTrue(zoneServerText.Contains("RuntimeOwnershipRegistry<int, IPlayfield> playfields"));
            Assert.IsTrue(zoneServerText.Contains("this.playfields.GetOrCreate(id.Instance)"));
            Assert.IsTrue(zoneServerText.Contains("Type = IdentityType.Playfield"));
            Assert.IsTrue(zoneServerText.Contains("this.playfields.Replace(playfieldIdentity.Instance)"));
            Assert.IsTrue(zoneServerText.Contains("this.playfields.Dispose()"));

            Assert.IsTrue(playfieldText.Contains("return this.server.PlayfieldById(playfield);"));
            Assert.IsFalse(
                ExtractMethod(playfieldText, "private IPlayfield ResolveOrCreatePlayfieldTransferDestination")
                    .Contains("new Playfield("));
            Assert.IsTrue(playfieldText.Contains("private readonly object heartBeatSync"));
            Assert.IsTrue(playfieldText.Contains("if (!this.disposed)"));
            Assert.IsTrue(playfieldText.Contains("this.heartBeat.Dispose();"));
            Assert.IsTrue(playfieldText.Contains("lock (this.heartBeatSync)"));

            Assert.IsTrue(npcRuntimeText.Contains("character.DoNotDoTimers = true;"));
            Assert.IsTrue(npcRuntimeText.Contains("character.SetFightingTarget(Identity.None);"));
            Assert.IsTrue(npcRuntimeText.Contains("controller.StopFollow();"));
            Assert.IsTrue(npcRuntimeText.Contains("this.combatTick.ClearRuntimeState();"));
            Assert.IsTrue(npcRuntimeText.Contains("this.worldPopulation.ClearPlayfield"));
            Assert.IsTrue(npcRuntimeText.Contains("this.capturedTempleEncounters.ClearRuntimeState();"));
            Assert.IsTrue(combatTickText.Contains("internal void ClearRuntimeState()"));
            Assert.IsTrue(combatTickText.Contains("this.pendingCapturedAttackStarts.Clear();"));
            Assert.IsTrue(combatTickText.Contains("this.pendingCapturedMovementTransitions.Clear();"));
            Assert.IsTrue(corpseLifecycleText.Contains("this.deadNpcDespawnTicks.Clear();"));
            Assert.IsTrue(runtimeSystemsText.Contains("this.visibilityInterest.Clear();"));
            Assert.IsTrue(runtimeSystemsText.Contains("this.dynelRegistry.Clear();"));
            Assert.IsTrue(dynelRegistryText.Contains("internal void Clear()"));
        }

        [TestMethod]
        public void TemplePopulationContractRemainsOneHundredFiftyThreeOrdinaryAndElevenNamed()
        {
            string repositoryRoot = FindRepositoryRoot();
            string encounterText = Read(
                repositoryRoot,
                @"Core\Playfields\CapturedTempleOfThreeWindsEncounterRuntimeService.cs");

            Assert.AreEqual(153, new CapturedTempleOfThreeWindsContentProvider().GetSpawns().Length);
            Assert.AreEqual(9, CountOccurrences(encounterText, "new NamedEncounterState("));
            Assert.AreEqual(2, CountOccurrences(encounterText, "new ReanimatedSlotState("));
        }

        private static string Read(string repositoryRoot, string relativePath)
        {
            return File.ReadAllText(
                Path.Combine(repositoryRoot, @"AORebirth\Server\ZoneEngine", relativePath));
        }

        private static string ExtractMethod(string source, string signature)
        {
            int start = source.IndexOf(signature, StringComparison.Ordinal);
            Assert.IsTrue(start >= 0, "Missing method: " + signature);
            int open = source.IndexOf('{', start);
            int depth = 0;
            for (int i = open; i < source.Length; i++)
            {
                if (source[i] == '{')
                {
                    depth++;
                }
                else if (source[i] == '}' && --depth == 0)
                {
                    return source.Substring(start, i - start + 1);
                }
            }

            Assert.Fail("Unterminated method: " + signature);
            return string.Empty;
        }

        private static int CountOccurrences(string source, string value)
        {
            int count = 0;
            int index = 0;
            while ((index = source.IndexOf(value, index, StringComparison.Ordinal)) >= 0)
            {
                count++;
                index += value.Length;
            }

            return count;
        }

        private static string FindRepositoryRoot([CallerFilePath] string sourcePath = null)
        {
            DirectoryInfo directory = new FileInfo(sourcePath).Directory;
            while (directory != null)
            {
                if (Directory.Exists(Path.Combine(directory.FullName, "AORebirth"))
                    && File.Exists(Path.Combine(directory.FullName, "AI_START_HERE.md")))
                {
                    return directory.FullName;
                }

                directory = directory.Parent;
            }

            Assert.Fail("Could not find repository root.");
            return string.Empty;
        }

        private sealed class FakePlayfieldRuntime : IDisposable
        {
            internal FakePlayfieldRuntime(int playfieldInstance)
            {
                this.PlayfieldInstance = playfieldInstance;
            }

            internal int PlayfieldInstance { get; private set; }

            internal bool Disposed { get; private set; }

            internal int CombatTicks { get; private set; }

            internal int MoveTicks { get; private set; }

            internal int RespawnTicks { get; private set; }

            internal int VisibilityUpdates { get; private set; }

            internal void CombatTick()
            {
                if (!this.Disposed)
                {
                    this.CombatTicks++;
                }
            }

            internal void MoveTick()
            {
                if (!this.Disposed)
                {
                    this.MoveTicks++;
                }
            }

            internal void RespawnTick()
            {
                if (!this.Disposed)
                {
                    this.RespawnTicks++;
                }
            }

            internal void VisibilityUpdate()
            {
                if (!this.Disposed)
                {
                    this.VisibilityUpdates++;
                }
            }

            public void Dispose()
            {
                this.Disposed = true;
            }
        }
    }
}
