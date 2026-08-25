namespace SmokeLounge.AOtomation.Messaging.Tests
{
    #region Usings ...

    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.IO;
    using System.Runtime.CompilerServices;
    using System.Security.Cryptography;
    using System.Text;

    using Microsoft.VisualStudio.TestTools.UnitTesting;

    using ZoneEngine.Core.Missions;

    #endregion

    [TestClass]
    public class MissionAcgSpatialRuntimeTests
    {
        private string temporaryDirectory;

        private MissionAcgLayoutCatalog catalog;

        [TestInitialize]
        public void Initialize()
        {
            this.temporaryDirectory =
                Path.Combine(
                    Path.GetTempPath(),
                    "AORebirth-MissionAcgSpatial-"
                    + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(this.temporaryDirectory);
            this.catalog = MissionAcgLegacyLayoutCatalogFactory.Create();
        }

        [TestCleanup]
        public void Cleanup()
        {
            if (Directory.Exists(this.temporaryDirectory))
            {
                Directory.Delete(this.temporaryDirectory, true);
            }
        }

        [TestMethod]
        public void EverySelectableBundleDerivesFiniteSpatialBounds()
        {
            int count = 0;
            foreach (MissionAcgLayoutBundle bundle in this.catalog.Layouts)
            {
                if (!bundle.IsSelectable)
                {
                    continue;
                }

                MissionAcgSpatialEnvelope envelope;
                string failure;
                Assert.IsTrue(
                    MissionAcgSpatialEnvelope.TryDerive(
                        bundle,
                        out envelope,
                        out failure),
                    failure);
                Assert.IsTrue(envelope.MinimumX < envelope.MaximumX);
                Assert.IsTrue(envelope.MinimumY < envelope.MaximumY);
                Assert.IsTrue(envelope.MinimumZ < envelope.MaximumZ);
                Assert.IsTrue(
                    envelope.CapturedCoordinateCount
                    >= MissionAcgSpatialEnvelope.MinimumDistinctCapturedCoordinates);
                count++;
            }

            Assert.AreEqual(5, count);
        }

        [TestMethod]
        public void RepeatedEnvelopeDerivationIsIdentical()
        {
            MissionAcgLayoutBundle bundle = this.FirstSelectable();
            MissionAcgSpatialEnvelope first = this.Derive(bundle);
            MissionAcgSpatialEnvelope second = this.Derive(bundle);
            AssertEnvelopeEqual(first, second);
        }

        [TestMethod]
        public void NonFiniteCapturedCoordinatesFailClosed()
        {
            MissionAcgSpatialEnvelope envelope;
            string failure;
            Assert.IsFalse(
                MissionAcgSpatialEnvelope.TryDerive(
                    "invalid",
                    new[]
                        {
                            new MissionAcgSpatialPoint(0, 0, 0),
                            new MissionAcgSpatialPoint(1, float.NaN, 1),
                            new MissionAcgSpatialPoint(2, 2, float.PositiveInfinity)
                        },
                    out envelope,
                    out failure));
            Assert.IsNull(envelope);
        }

        [TestMethod]
        public void EmptyCoordinateSetIsSpatiallyNonOperational()
        {
            MissionAcgSpatialEnvelope envelope;
            string failure;
            Assert.IsFalse(
                MissionAcgSpatialEnvelope.TryDerive(
                    "empty",
                    new MissionAcgSpatialPoint[0],
                    out envelope,
                    out failure));
        }

        [TestMethod]
        public void InsufficientDistinctCoordinatesFailClosed()
        {
            MissionAcgSpatialEnvelope envelope;
            string failure;
            Assert.IsFalse(
                MissionAcgSpatialEnvelope.TryDerive(
                    "insufficient",
                    new[]
                        {
                            new MissionAcgSpatialPoint(1, 1, 1),
                            new MissionAcgSpatialPoint(1, 1, 1),
                            new MissionAcgSpatialPoint(2, 2, 2)
                        },
                    out envelope,
                    out failure));
        }

        [TestMethod]
        public void DerivingAnotherLayoutDoesNotMutateExistingBounds()
        {
            MissionAcgLayoutBundle firstBundle = this.FirstSelectable();
            MissionAcgLayoutBundle secondBundle = null;
            foreach (MissionAcgLayoutBundle bundle in this.catalog.Layouts)
            {
                if (bundle.IsSelectable && bundle.LayoutId != firstBundle.LayoutId)
                {
                    secondBundle = bundle;
                    break;
                }
            }

            MissionAcgSpatialEnvelope first = this.Derive(firstBundle);
            float minimumX = first.MinimumX;
            float maximumZ = first.MaximumZ;
            this.Derive(secondBundle);
            Assert.AreEqual(minimumX, first.MinimumX);
            Assert.AreEqual(maximumZ, first.MaximumZ);
        }

        [TestMethod]
        public void EnvelopeToleranceIsExactlyTheDocumentedBound()
        {
            MissionAcgSpatialEnvelope envelope;
            string failure;
            Assert.IsTrue(
                MissionAcgSpatialEnvelope.TryDerive(
                    "manual",
                    new[]
                        {
                            new MissionAcgSpatialPoint(0, 1, 2),
                            new MissionAcgSpatialPoint(5, 6, 7),
                            new MissionAcgSpatialPoint(2, 3, 4)
                        },
                    out envelope,
                    out failure),
                failure);
            Assert.AreEqual(-MissionAcgSpatialEnvelope.CoordinateTolerance, envelope.MinimumX);
            Assert.AreEqual(
                7 + MissionAcgSpatialEnvelope.CoordinateTolerance,
                envelope.MaximumZ);
            Assert.AreEqual(2.0f, MissionAcgSpatialEnvelope.CoordinateTolerance);
        }

        [TestMethod]
        public void EntryExitObjectivesAndDynelsAreInsideTheirEnvelope()
        {
            foreach (MissionAcgLayoutBundle bundle in this.catalog.Layouts)
            {
                if (!bundle.IsSelectable)
                {
                    continue;
                }

                MissionAcgSpatialEnvelope envelope = this.Derive(bundle);
                Assert.IsTrue(envelope.Contains(bundle.EntryPoint));
                Assert.IsTrue(envelope.Contains(bundle.Exit.Position));
                foreach (MissionAcgDynelRecord dynel in bundle.Dynels)
                {
                    Assert.IsTrue(envelope.Contains(dynel.Position));
                }

                foreach (MissionAcgObjectiveSlotRecord objective in bundle.ObjectiveSlots)
                {
                    Assert.IsTrue(envelope.Contains(objective.Position));
                }
            }
        }

        [TestMethod]
        public void OutsideCoordinateIsRejectedByEnvelope()
        {
            MissionAcgSpatialEnvelope envelope = this.Derive(this.FirstSelectable());
            Assert.IsFalse(
                envelope.Contains(
                    envelope.MaximumX + MissionAcgSpatialEnvelope.CoordinateTolerance,
                    envelope.MaximumY,
                    envelope.MaximumZ));
        }

        [TestMethod]
        public void IncompleteShape1441804RemainsNonSelectableAndNonOperational()
        {
            MissionAcgLayoutBundle shape = this.catalog.FindBySourcePlayfield2(1441804);
            Assert.IsTrue(shape == null || !shape.IsSelectable);
        }

        [TestMethod]
        public void OwnershipAndRangeOnlyOperationDoesNotClaimClearLos()
        {
            Assert.AreEqual(
                MissionAcgLineOfSightDecision.AllowedRangeAndOwnershipOnly,
                MissionAcgLineOfSightPolicy.Evaluate(false, true, true, true));
        }

        [TestMethod]
        public void GeometryRequiredLosIsExplicitlyUnresolved()
        {
            Assert.AreEqual(
                MissionAcgLineOfSightDecision.UnresolvedGeometryUnavailable,
                MissionAcgLineOfSightPolicy.Evaluate(true, true, true, true));
        }

        [TestMethod]
        public void InvalidSpatialOwnershipDeniesBeforeLos()
        {
            Assert.AreEqual(
                MissionAcgLineOfSightDecision.DeniedInvalidSpatialOwnership,
                MissionAcgLineOfSightPolicy.Evaluate(false, false, true, true));
        }

        [TestMethod]
        public void SpatialFormatIsSeparateAndOperationalMigrationRemainsExplicit()
        {
            Assert.AreEqual(1, MissionAcgSpatialState.CurrentFormatVersion);
            Assert.AreEqual(
                1,
                MissionAcgOperationalState.LegacyCapturedDifficultyFormatVersion);
            Assert.AreEqual(3, MissionAcgOperationalState.CurrentFormatVersion);
            Assert.AreEqual(1, MissionAcgRuntimeState.CurrentFormatVersion);
            Assert.AreEqual(2, MissionAcgInstanceBinding.CurrentFormatVersion);
        }

        [TestMethod]
        public void SpatialStateRoundTripPreservesExactMissionAndPosition()
        {
            MissionAcgBindingRecord binding = this.CreateBinding(1, this.FirstPf());
            MissionAcgSpatialState initial = this.CreateState(binding, true, 8, 9, 10);
            MissionAcgSpatialState restored = this.RoundTrip(binding, initial);
            Assert.AreEqual(
                binding.Binding.AcceptedQuestIdentity.Instance,
                restored.AcceptedQuestIdentity.Instance);
            Assert.AreEqual(binding.Binding.AllocatedLivePlayfield2, restored.AllocatedLivePlayfield2);
            Assert.AreEqual(binding.Binding.SelectedBundleId, restored.BundleId);
            Assert.AreEqual(8.0f, restored.LastValidPlayerPosition.X);
            Assert.IsTrue(restored.HasLastValidPlayerPosition);
        }

        [TestMethod]
        public void SpatialStateForTwoMissionsKeepsDistinctPf2AndPositions()
        {
            int firstPf = this.FirstPf();
            MissionAcgBindingRecord first = this.CreateBinding(2, firstPf);
            MissionAcgBindingRecord second = this.CreateBinding(3, firstPf + 1);
            MissionAcgSpatialState one =
                this.RoundTrip(first, this.CreateState(first, true, 1, 2, 3));
            MissionAcgSpatialState two =
                this.RoundTrip(second, this.CreateState(second, true, 4, 5, 6));
            Assert.AreNotEqual(one.AllocatedLivePlayfield2, two.AllocatedLivePlayfield2);
            Assert.AreNotEqual(
                one.AcceptedQuestIdentity.Instance,
                two.AcceptedQuestIdentity.Instance);
            Assert.AreNotEqual(
                one.LastValidPlayerPosition.X,
                two.LastValidPlayerPosition.X);
        }

        [TestMethod]
        public void TamperedSpatialSidecarFailsIntegrity()
        {
            MissionAcgBindingRecord binding = this.CreateBinding(4, this.FirstPf());
            var store = new MissionAcgSpatialStateStore(this.temporaryDirectory);
            string failure;
            Assert.IsTrue(
                store.TryWrite(this.CreateState(binding, true, 1, 2, 3), false, out failure),
                failure);
            string path = store.ResolvePath(binding.Binding.AcceptedQuestIdentity);
            string text = File.ReadAllText(path).Replace("lastValidX=1", "lastValidX=9");
            File.WriteAllText(path, text);
            MissionAcgSpatialState restored;
            bool exists;
            Assert.IsFalse(
                store.TryLoad(binding.Binding, out restored, out exists, out failure));
        }

        [TestMethod]
        public void TruncatedSpatialSidecarFailsClosed()
        {
            MissionAcgBindingRecord binding = this.CreateBinding(5, this.FirstPf());
            var store = new MissionAcgSpatialStateStore(this.temporaryDirectory);
            string failure;
            Assert.IsTrue(
                store.TryWrite(this.CreateState(binding, true, 1, 2, 3), false, out failure),
                failure);
            string path = store.ResolvePath(binding.Binding.AcceptedQuestIdentity);
            File.WriteAllText(path, "formatVersion=1\n");
            MissionAcgSpatialState restored;
            bool exists;
            Assert.IsFalse(
                store.TryLoad(binding.Binding, out restored, out exists, out failure));
        }

        [TestMethod]
        public void UnknownSpatialVersionFailsClosedAfterValidRehash()
        {
            MissionAcgBindingRecord binding = this.CreateBinding(6, this.FirstPf());
            MissionAcgSpatialState state = this.CreateState(binding, true, 1, 2, 3);
            string serialized =
                MissionAcgSpatialStateStore.Serialize(state)
                    .Replace("formatVersion=1", "formatVersion=99");
            serialized = Rehash(serialized);
            MissionAcgSpatialState restored;
            string failure;
            Assert.IsFalse(
                MissionAcgSpatialStateStore.TryParse(
                    serialized,
                    out restored,
                    out failure));
            Assert.IsTrue(failure.IndexOf("version", StringComparison.OrdinalIgnoreCase) >= 0);
        }

        [TestMethod]
        public void BindingMismatchCannotRedirectPersistedPosition()
        {
            int firstPf = this.FirstPf();
            MissionAcgBindingRecord first = this.CreateBinding(7, firstPf);
            MissionAcgBindingRecord other = this.CreateBinding(8, firstPf + 1);
            var store = new MissionAcgSpatialStateStore(this.temporaryDirectory);
            string failure;
            Assert.IsTrue(
                store.TryWrite(this.CreateState(first, true, 1, 2, 3), false, out failure),
                failure);
            string firstPath = store.ResolvePath(first.Binding.AcceptedQuestIdentity);
            string otherPath = store.ResolvePath(other.Binding.AcceptedQuestIdentity);
            File.Copy(firstPath, otherPath);
            MissionAcgSpatialState restored;
            bool exists;
            Assert.IsFalse(
                store.TryLoad(other.Binding, out restored, out exists, out failure));
        }

        [TestMethod]
        public void AtomicReplacementExposesOnlyLatestValidPosition()
        {
            MissionAcgBindingRecord binding = this.CreateBinding(9, this.FirstPf());
            var store = new MissionAcgSpatialStateStore(this.temporaryDirectory);
            string failure;
            Assert.IsTrue(
                store.TryWrite(this.CreateState(binding, true, 1, 2, 3), false, out failure),
                failure);
            Assert.IsTrue(
                store.TryWrite(this.CreateState(binding, true, 7, 8, 9), true, out failure),
                failure);
            MissionAcgSpatialState restored;
            bool exists;
            Assert.IsTrue(
                store.TryLoad(binding.Binding, out restored, out exists, out failure),
                failure);
            Assert.AreEqual(7.0f, restored.LastValidPlayerPosition.X);
        }

        [TestMethod]
        public void CleanupTransitionsAreExactAndIdempotentAtModelBoundary()
        {
            MissionAcgBindingRecord binding = this.CreateBinding(10, this.FirstPf());
            MissionAcgSpatialState active = this.CreateState(binding, true, 1, 2, 3);
            MissionAcgSpatialState pending = active.BeginCleanup(DateTime.UtcNow);
            MissionAcgSpatialState completed = pending.CompleteCleanup(DateTime.UtcNow);
            Assert.AreEqual(MissionAcgSpatialCleanupState.CleanupPending, pending.CleanupState);
            Assert.AreEqual(MissionAcgSpatialCleanupState.Completed, completed.CleanupState);
            Assert.AreEqual(
                active.AcceptedQuestIdentity.Instance,
                completed.AcceptedQuestIdentity.Instance);
        }

        [TestMethod]
        public void SharedPf1419349CannotBecomeSpatialBindingPf()
        {
            Assert.AreNotEqual(
                MissionAcgAllocationService.LegacySharedPlayfield2,
                this.FirstPf());
        }

        [TestMethod]
        public void PlayerMovementIsValidatedBeforeControllerMove()
        {
            string source = ReadSource(
                @"AORebirth\Server\ZoneEngine\Core\MessageHandlers\CharDCMoveMessageHandler.cs");
            AssertTextBefore(
                source,
                "MissionAcgSpatialRuntime.TryValidatePlayerMove",
                "client.Controller.Move(moveType, coordinates, heading)");
        }

        [TestMethod]
        public void DoorChestObjectiveAndExitUseCentralSpatialValidation()
        {
            string source = ReadSource(
                @"AORebirth\Server\ZoneEngine\Core\Missions\MissionAcgRuntimeInteractionService.cs");
            Assert.IsTrue(source.Contains("MissionAcgSpatialRuntime.TryValidateInteraction"));
            Assert.IsTrue(source.Contains("MissionAcgRuntimeObjectKind.Door"));
            Assert.IsTrue(source.Contains("MissionAcgRuntimeObjectKind.Chest"));
            Assert.IsTrue(source.Contains("MissionAcgRuntimeObjectKind.Exit"));
        }

        [TestMethod]
        public void FindPersonAndRepairHaveExactSpatialChecks()
        {
            string source = ReadSource(
                @"AORebirth\Server\ZoneEngine\Core\Missions\MissionAcgObjectiveInteractionService.cs");
            Assert.IsTrue(source.Contains("\"find-person-info\""));
            Assert.IsTrue(source.Contains("\"repair-machine\""));
            Assert.IsTrue(
                source.Contains("MissionAcgSpatialRuntime.TryValidateObjectiveRuntimeInteraction"));
        }

        [TestMethod]
        public void PlayerAndNpcDamageBoundariesBothUseSpatialAuthority()
        {
            string attack = ReadSource(
                @"AORebirth\Server\ZoneEngine\Core\MessageHandlers\AttackMessageHandler.cs");
            string playfield = ReadSource(
                @"AORebirth\Server\ZoneEngine\Core\Playfields\Playfield.cs");
            string npc = ReadSource(
                @"AORebirth\Server\ZoneEngine\Core\Playfields\NpcCombatTickCoordinator.cs");
            Assert.IsTrue(attack.Contains("MissionAcgSpatialRuntime.TryValidateCombatPair"));
            Assert.IsTrue(playfield.Contains("MissionAcgSpatialRuntime.TryValidateCombatPair"));
            Assert.IsTrue(npc.Contains("MissionAcgSpatialRuntime.TryValidateCombatPair"));
        }

        [TestMethod]
        public void MissionNpcPursuitUsesExplicitStationaryFallback()
        {
            string movement = ReadSource(
                @"AORebirth\Server\ZoneEngine\Core\Playfields\PlayfieldNpcCombatMovementRuntimeService.cs");
            Assert.IsTrue(movement.Contains("MissionAcgSpatialRuntime.RequiresStationaryNpc"));
            Assert.IsTrue(movement.Contains("npcController.StopFollow()"));
            Assert.IsFalse(movement.Contains("MissionAcgRandom"));
        }

        [TestMethod]
        public void StartupRestoresSpatialAuthorityAfterOperationalState()
        {
            string source = ReadSource(
                @"AORebirth\Server\ZoneEngine\Core\Missions\MissionAcgBindingRuntime.cs");
            AssertTextBefore(
                source,
                "MissionAcgOperationalRuntime.Initialize",
                "MissionAcgSpatialRuntime.Initialize");
        }

        [TestMethod]
        public void EntryAndExitResolveTheExactSpatialBinding()
        {
            string source = ReadSource(
                @"AORebirth\Server\ZoneEngine\Core\Missions\MissionInstanceService.cs");
            Assert.IsTrue(source.Contains("MissionAcgSpatialRuntime.TryResolveEntryPosition"));
            Assert.IsTrue(source.Contains("MissionAcgSpatialRuntime.TryValidateExitPosition"));
            Assert.IsFalse(
                source.Contains(
                    "MissionAcgSpatialRuntime.TryValidateExitPosition(latest"));
        }

        [TestMethod]
        public void SpatialImplementationContainsNoGenerationSchemaRewardOrLootWork()
        {
            string authority = ReadSource(
                @"AORebirth\Server\ZoneEngine\Core\Missions\MissionAcgSpatialAuthority.cs");
            string runtime = ReadSource(
                @"AORebirth\Server\ZoneEngine\Core\Missions\MissionAcgSpatialRuntime.cs");
            string combined = authority + runtime;
            Assert.IsFalse(combined.Contains("Random("));
            Assert.IsFalse(combined.Contains("CREATE TABLE"));
            Assert.IsFalse(combined.Contains("ALTER TABLE"));
            Assert.IsFalse(combined.Contains("Reward"));
            Assert.IsFalse(combined.Contains("Loot"));
            Assert.IsFalse(combined.Contains("C79F"));
        }

        private MissionAcgSpatialEnvelope Derive(MissionAcgLayoutBundle bundle)
        {
            MissionAcgSpatialEnvelope envelope;
            string failure;
            Assert.IsTrue(
                MissionAcgSpatialEnvelope.TryDerive(bundle, out envelope, out failure),
                failure);
            return envelope;
        }

        private MissionAcgLayoutBundle FirstSelectable()
        {
            foreach (MissionAcgLayoutBundle bundle in this.catalog.Layouts)
            {
                if (bundle.IsSelectable)
                {
                    return bundle;
                }
            }

            Assert.Fail("No selectable mission ACG layout exists.");
            return null;
        }

        private MissionAcgSpatialState RoundTrip(
            MissionAcgBindingRecord binding,
            MissionAcgSpatialState state)
        {
            var store = new MissionAcgSpatialStateStore(this.temporaryDirectory);
            string failure;
            Assert.IsTrue(store.TryWrite(state, false, out failure), failure);
            MissionAcgSpatialState restored;
            bool exists;
            Assert.IsTrue(
                store.TryLoad(binding.Binding, out restored, out exists, out failure),
                failure);
            Assert.IsTrue(exists);
            return restored;
        }

        private MissionAcgSpatialState CreateState(
            MissionAcgBindingRecord record,
            bool hasPosition,
            float x,
            float y,
            float z)
        {
            return new MissionAcgSpatialState(
                MissionAcgSpatialState.CurrentFormatVersion,
                record.Binding.AcceptedQuestIdentity,
                record.Binding.OwnerIdentity,
                record.Binding.AllocatedLivePlayfield2,
                record.Binding.SelectedBundleId,
                record.Binding.SelectedBundlePayloadSha256,
                record.Binding.AcgBuildingIdentity,
                hasPosition,
                new MissionAcgPointRecord(x, y, z),
                MissionAcgSpatialCleanupState.Active,
                new DateTime(2026, 7, 28, 20, 0, 0, DateTimeKind.Utc));
        }

        private MissionAcgBindingRecord CreateBinding(int salt, int livePf)
        {
            var owner = new MissionAcgIdentityRecord(0xC350, 20000 + salt);
            MissionAcgLayoutBundle bundle =
                MissionAcgLayoutSelector.Select(
                    this.catalog,
                    new MissionAcgSelectionInput(
                        3000 + salt,
                        MissionRollType.KillPerson,
                        42,
                        owner));
            DateTime accepted =
                new DateTime(2026, 7, 28, 19, 0, 0, DateTimeKind.Utc).AddSeconds(salt);
            MissionAcgInstanceBinding binding =
                MissionAcgInstanceBinding.CreateDurable(
                    new MissionAcgIdentityRecord(
                        MissionAcgAllocationService.AcceptedQuestIdentityType,
                        0x51000000 + salt),
                    new MissionAcgIdentityRecord(0xDAC3, 0x02000000 + salt),
                    owner,
                    null,
                    MissionRollType.KillPerson,
                    42,
                    3000 + salt,
                    new MissionAcgIdentityRecord(
                        MissionAcgAllocationService.MissionKeyIdentityType,
                        0x61000000 + salt),
                    new MissionAcgIdentityRecord(0x9C50, 710),
                    43308,
                    27595,
                    229.605f,
                    6.504f,
                    452.042f,
                    new MissionAcgIdentityRecord(0xDAC1, 0x2000 + salt),
                    bundle,
                    livePf,
                    accepted,
                    accepted.AddHours(48));
            return new MissionAcgBindingRecord(
                binding,
                new MissionAcgInstanceState(
                    MissionAcgLifecycleState.Active,
                    MissionAcgCleanupState.None,
                    accepted,
                    null),
                string.Empty);
        }

        private int FirstPf()
        {
            int value = MissionAcgAllocationService.MinimumLivePlayfield2;
            while (value == MissionAcgAllocationService.LegacySharedPlayfield2
                   || this.catalog.FindBySourcePlayfield2(value) != null)
            {
                value++;
            }

            return value;
        }

        private static void AssertEnvelopeEqual(
            MissionAcgSpatialEnvelope first,
            MissionAcgSpatialEnvelope second)
        {
            Assert.AreEqual(first.BundleId, second.BundleId);
            Assert.AreEqual(first.MinimumX, second.MinimumX);
            Assert.AreEqual(first.MinimumY, second.MinimumY);
            Assert.AreEqual(first.MinimumZ, second.MinimumZ);
            Assert.AreEqual(first.MaximumX, second.MaximumX);
            Assert.AreEqual(first.MaximumY, second.MaximumY);
            Assert.AreEqual(first.MaximumZ, second.MaximumZ);
            Assert.AreEqual(first.CapturedCoordinateCount, second.CapturedCoordinateCount);
        }

        private static string Rehash(string serialized)
        {
            string normalized = serialized.Replace("\r\n", "\n");
            int hashLine = normalized.LastIndexOf("sha256=", StringComparison.Ordinal);
            string canonical = normalized.Substring(0, hashLine);
            using (SHA256 sha = SHA256.Create())
            {
                byte[] hash = sha.ComputeHash(new UTF8Encoding(false).GetBytes(canonical));
                var builder = new StringBuilder(hash.Length * 2);
                foreach (byte value in hash)
                {
                    builder.Append(value.ToString("x2", CultureInfo.InvariantCulture));
                }

                return canonical + "sha256=" + builder + "\n";
            }
        }

        private static string ReadSource(string relativePath)
        {
            return File.ReadAllText(Path.Combine(FindRepositoryRoot(), relativePath));
        }

        private static string FindRepositoryRoot(
            [CallerFilePath] string sourcePath = null)
        {
            return TestRepositoryRootResolver.Resolve(sourcePath);
        }

        private static void AssertTextBefore(
            string source,
            string first,
            string second)
        {
            int firstIndex = source.IndexOf(first, StringComparison.Ordinal);
            int secondIndex = source.IndexOf(second, StringComparison.Ordinal);
            Assert.IsTrue(firstIndex >= 0, "Missing source text: " + first);
            Assert.IsTrue(secondIndex > firstIndex, "Expected source order was not preserved.");
        }
    }
}
