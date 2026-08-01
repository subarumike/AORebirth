namespace SmokeLounge.AOtomation.Messaging.Tests
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;

    using AORebirth.Core.Statels;

    using Microsoft.VisualStudio.TestTools.UnitTesting;

    using SmokeLounge.AOtomation.Messaging.GameData;

    using ZoneEngine.Core.Playfields;

    [TestClass]
    public class TempleDoorStatusRuntimeTests
    {
        [TestMethod]
        public void TempleEntrySendsOneCapturedClosedStatusPerOfficialDoorStatel()
        {
            string service = ReadRepositoryFile(
                @"AORebirth\Server\ZoneEngine\Core\Playfields\CapturedPlayfieldDoorStatusRuntimeService.cs");
            string playfield = ReadRepositoryFile(
                @"AORebirth\Server\ZoneEngine\Core\Playfields\Playfield.cs");

            StringAssert.Contains(service, "TempleOfThreeWindsPlayfieldId = 1931");
            StringAssert.Contains(service, "ExpectedTempleInternalDoorCount = 43");
            StringAssert.Contains(service, "TempleExteriorEntryDoorInstance");
            StringAssert.Contains(service, "statel.Identity.Type == IdentityType.Door");
            StringAssert.Contains(service, ".GroupBy(statel => statel.Identity)");
            StringAssert.Contains(
                service,
                "DoorStatusUpdateMessageHandler.Default.SendStatus(character, door.Identity, false)");
            StringAssert.Contains(playfield, "this.statels");
            StringAssert.Contains(playfield, "SendInitialDoorStatuses(");
            StringAssert.Contains(playfield, "this.SendStaticDynelsToClient(character);");
        }

        [TestMethod]
        public void SubwayDoorEvidencePreservesExactCapturedIdentityAndStateCoverage()
        {
            CapturedDoorSnapshotEvidence[] evidence = CapturedSubwayDoorSnapshotEvidence.GetAll();

            Assert.AreEqual(127, CapturedSubwayDoorSnapshotEvidence.PlayfieldId);
            Assert.AreEqual(18, CapturedSubwayDoorSnapshotEvidence.ExpectedDoorCount);
            Assert.AreEqual(10, CapturedSubwayDoorSnapshotEvidence.ExpectedCollectorActivationBatchCount);
            CollectionAssert.AreEqual(
                new[]
                {
                    unchecked((int)0xC02D007F),
                    unchecked((int)0xC02E007F),
                    unchecked((int)0xC02F007F),
                    unchecked((int)0xC030007F),
                    unchecked((int)0xC031007F),
                    unchecked((int)0xC032007F),
                    unchecked((int)0xC033007F),
                    unchecked((int)0xC034007F),
                    unchecked((int)0xC035007F),
                    unchecked((int)0xC036007F),
                    unchecked((int)0xC037007F),
                    unchecked((int)0xC038007F),
                    unchecked((int)0xC03A007F),
                    unchecked((int)0xC03B007F),
                    unchecked((int)0xC03C007F),
                    unchecked((int)0xC03D007F),
                    unchecked((int)0xC03F007F),
                    unchecked((int)0xC040007F)
                },
                evidence.Select(door => door.Instance).ToArray());
            Assert.IsTrue(evidence.All(door => door.ObservedClosed));
            CollectionAssert.AreEqual(
                new[]
                {
                    unchecked((int)0xC02F007F),
                    unchecked((int)0xC030007F),
                    unchecked((int)0xC035007F),
                    unchecked((int)0xC03C007F),
                    unchecked((int)0xC040007F)
                },
                evidence.Where(door => door.ObservedOpen)
                    .Select(door => door.Instance)
                    .ToArray());
            CollectionAssert.AreEqual(
                new[]
                {
                    unchecked((int)0xC031007F),
                    unchecked((int)0xC032007F),
                    unchecked((int)0xC033007F),
                    unchecked((int)0xC034007F),
                    unchecked((int)0xC035007F),
                    unchecked((int)0xC036007F),
                    unchecked((int)0xC037007F),
                    unchecked((int)0xC038007F),
                    unchecked((int)0xC03C007F),
                    unchecked((int)0xC03D007F)
                },
                evidence.Where(door => door.ObservedInCollectorActivationBatch)
                    .Select(door => door.Instance)
                    .ToArray());
        }

        [TestMethod]
        public void SubwayExternalArrivalEvidenceMapsExactlySixOfficialStatels()
        {
            CapturedSubwayArrivalDoorEvidence[] evidence =
                CapturedSubwayArrivalDoorEvidenceSet.GetAll();

            Assert.AreEqual(127, CapturedSubwayArrivalDoorEvidenceSet.PlayfieldId);
            Assert.AreEqual(6, CapturedSubwayArrivalDoorEvidenceSet.ExpectedDoorCount);
            Assert.AreEqual(0.001f, CapturedSubwayArrivalDoorEvidenceSet.CoordinateTolerance);
            CollectionAssert.AreEqual(
                new[]
                {
                    unchecked((int)0xC006007F),
                    unchecked((int)0xC007007F),
                    unchecked((int)0xC00A007F),
                    unchecked((int)0xC00B007F),
                    unchecked((int)0xC00C007F),
                    unchecked((int)0xC00D007F)
                },
                evidence.Select(door => door.Instance).ToArray());
            CollectionAssert.AreEqual(
                new[] { 164818, 164818, 164818, 164818, 164818, 164815 },
                evidence.Select(door => door.TemplateId).ToArray());
            Assert.IsTrue(
                CapturedSubwayArrivalDoorEvidenceSet.MatchesOfficialStatel(
                    unchecked((int)0xC006007F),
                    164818,
                    64.008300781f,
                    115.693832397f,
                    318.987945557f));
            Assert.IsFalse(
                CapturedSubwayArrivalDoorEvidenceSet.MatchesOfficialStatel(
                    unchecked((int)0xC006007F),
                    164815,
                    64.008300781f,
                    115.693832397f,
                    318.987945557f));
            Assert.IsFalse(
                CapturedSubwayArrivalDoorEvidenceSet.MatchesOfficialStatel(
                    unchecked((int)0xC006007F),
                    164818,
                    64.010300781f,
                    115.693832397f,
                    318.987945557f));
        }

        [TestMethod]
        public void SubwayExternalArrivalSendsOnlySixCapturedClosedStatuses()
        {
            string service = ReadRepositoryFile(
                @"AORebirth\Server\ZoneEngine\Core\Playfields\CapturedPlayfieldDoorStatusRuntimeService.cs");
            string clientConnected = ReadRepositoryFile(
                @"AORebirth\Server\ZoneEngine\Core\PacketHandlers\ClientConnected.cs");

            StringAssert.Contains(service, "CapturedSubwayArrivalDoorEvidenceSet.ResolveInitialStatusStatels(");
            StringAssert.Contains(service, "playfieldId == SubwayPlayfieldId && isExternalPlayfieldArrival");
            StringAssert.Contains(
                service,
                "DoorStatusUpdateMessageHandler.Default.SendStatus(character, door.Identity, false)");
            StringAssert.Contains(
                clientConnected,
                "currentPlayfield.SendStaticDynelsToClientAfterExternalPlayfieldArrival(");
            StringAssert.Contains(clientConnected, "client.Controller.Character);");
        }

        [TestMethod]
        public void SubwayInitialStatusResolverReturnsExactlySixOnlyForExternalPf127Arrival()
        {
            StatelData[] officialStatels = CapturedSubwayArrivalDoorEvidenceSet.GetAll()
                .Select(
                    door => new StatelData
                    {
                        Identity = new Identity
                        {
                            Type = IdentityType.Door,
                            Instance = door.Instance
                        },
                        TemplateId = door.TemplateId,
                        X = door.CapturedX,
                        Y = door.CapturedY,
                        Z = door.CapturedZ
                    })
                .Concat(
                    new[]
                    {
                        new StatelData
                        {
                            Identity = new Identity
                            {
                                Type = IdentityType.Door,
                                Instance = unchecked((int)0xC02D007F)
                            },
                            TemplateId = 164818,
                            X = 1.0f,
                            Y = 2.0f,
                            Z = 3.0f
                        }
                    })
                .ToArray();

            CollectionAssert.AreEqual(
                CapturedSubwayArrivalDoorEvidenceSet.GetAll()
                    .Select(door => door.Instance)
                    .ToArray(),
                CapturedSubwayArrivalDoorEvidenceSet.ResolveInitialStatusStatels(
                        127,
                        officialStatels,
                        true)
                    .Select(statel => statel.Identity.Instance)
                    .ToArray());
            Assert.AreEqual(
                0,
                CapturedSubwayArrivalDoorEvidenceSet.ResolveInitialStatusStatels(
                    127,
                    officialStatels,
                    false).Length);
            Assert.AreEqual(
                0,
                CapturedSubwayArrivalDoorEvidenceSet.ResolveInitialStatusStatels(
                    1931,
                    officialStatels,
                    true).Length);
        }

        [TestMethod]
        public void SubwayDoorRuntimeDoesNotReplayOnDeathOrInventProximity()
        {
            string service = ReadRepositoryFile(
                @"AORebirth\Server\ZoneEngine\Core\Playfields\CapturedPlayfieldDoorStatusRuntimeService.cs");
            string playfield = ReadRepositoryFile(
                @"AORebirth\Server\ZoneEngine\Core\Playfields\Playfield.cs");

            StringAssert.Contains(
                service,
                "this.doors = playfieldId == TempleOfThreeWindsPlayfieldId");
            StringAssert.Contains(service, ": new TempleDoorDefinition[0]");
            StringAssert.Contains(
                playfield,
                "public void SendStaticDynelsToClient(ICharacter character)");
            StringAssert.Contains(
                playfield,
                "this.SendStaticDynelsToClient(character, false);");
            StringAssert.Contains(
                playfield,
                "this.SendStaticDynelsToClient(character, true);");
            StringAssert.Contains(playfield, "this.SendStaticDynelsToClient(character);");
        }

        [TestMethod]
        public void DoorStatusHandlerPreservesCapturedMutableAndCategoricalFields()
        {
            string handler = ReadRepositoryFile(
                @"AORebirth\Server\ZoneEngine\Core\MessageHandlers\DoorStatusUpdateMessageHandler.cs");

            StringAssert.Contains(handler, "door.Type != IdentityType.Door");
            StringAssert.Contains(handler, "message.Identity = door");
            StringAssert.Contains(handler, "message.Unknown = 0");
            StringAssert.Contains(handler, "message.Unknown1 = 2");
            StringAssert.Contains(handler, "message.Unknown3 = isOpen ? (byte)1 : (byte)0");
            StringAssert.Contains(handler, "message.Unknown6 = new Identity[0]");
        }

        [TestMethod]
        public void TempleDoorContactOpensOnceAndExpiryClosesOnce()
        {
            TempleDoorProximityRuntime runtime = new TempleDoorProximityRuntime();
            TempleDoorDefinition door = new TempleDoorDefinition(101, 10.0f, 5.0f, 20.0f);
            DateTime openedAt = new DateTime(2026, 7, 21, 9, 16, 5, DateTimeKind.Utc);

            TempleDoorTransition[] opened = runtime.Evaluate(
                7,
                10.25f,
                5.0f,
                20.0f,
                openedAt,
                new[] { door });
            Assert.AreEqual(1, opened.Length);
            Assert.IsTrue(opened[0].IsOpen);

            Assert.AreEqual(
                0,
                runtime.Evaluate(7, 10.25f, 5.0f, 20.0f, openedAt.AddSeconds(1), new[] { door }).Length);
            Assert.AreEqual(
                0,
                runtime.Evaluate(7, 12.0f, 5.0f, 20.0f, openedAt.AddSeconds(4.999), new[] { door }).Length);

            TempleDoorTransition[] closed = runtime.Evaluate(
                7,
                12.0f,
                5.0f,
                20.0f,
                openedAt.AddSeconds(5),
                new[] { door });
            Assert.AreEqual(1, closed.Length);
            Assert.IsFalse(closed[0].IsOpen);
            Assert.AreEqual(
                0,
                runtime.Evaluate(7, 12.0f, 5.0f, 20.0f, openedAt.AddSeconds(6), new[] { door }).Length);
        }

        [TestMethod]
        public void TempleDoorTriggerUsesCapturedHalfMeterBoundary()
        {
            TempleDoorProximityRuntime runtime = new TempleDoorProximityRuntime();
            TempleDoorDefinition door = new TempleDoorDefinition(102, 0.0f, 0.0f, 0.0f);
            DateTime now = DateTime.UtcNow;

            Assert.AreEqual(0, runtime.Evaluate(8, 0.501f, 0.0f, 0.0f, now, new[] { door }).Length);
            TempleDoorTransition[] boundary = runtime.Evaluate(
                8,
                0.5f,
                0.0f,
                0.0f,
                now.AddMilliseconds(1),
                new[] { door });
            Assert.AreEqual(1, boundary.Length);
            Assert.IsTrue(boundary[0].IsOpen);
        }

        [TestMethod]
        public void TempleDoorRecipientsAreIndependentAndDoNotDuplicateTransitions()
        {
            TempleDoorProximityRuntime runtime = new TempleDoorProximityRuntime();
            TempleDoorDefinition door = new TempleDoorDefinition(103, 1.0f, 2.0f, 3.0f);
            DateTime now = DateTime.UtcNow;

            Assert.AreEqual(1, runtime.Evaluate(11, 1.0f, 2.0f, 3.0f, now, new[] { door }).Length);
            Assert.AreEqual(1, runtime.Evaluate(12, 1.0f, 2.0f, 3.0f, now, new[] { door }).Length);
            Assert.AreEqual(0, runtime.Evaluate(11, 1.0f, 2.0f, 3.0f, now, new[] { door }).Length);
            Assert.AreEqual(2, runtime.ActiveRecipientCount);
        }

        [TestMethod]
        public void TempleDoorReentryAndDisposalRemoveRecipientState()
        {
            TempleDoorProximityRuntime runtime = new TempleDoorProximityRuntime();
            TempleDoorDefinition door = new TempleDoorDefinition(104, 1.0f, 2.0f, 3.0f);
            DateTime now = DateTime.UtcNow;

            runtime.Evaluate(21, 1.0f, 2.0f, 3.0f, now, new[] { door });
            runtime.ResetRecipient(21);
            Assert.AreEqual(0, runtime.ActiveRecipientCount);
            Assert.AreEqual(
                1,
                runtime.Evaluate(21, 1.0f, 2.0f, 3.0f, now.AddSeconds(1), new[] { door }).Length);

            runtime.RemoveInactiveRecipients(new HashSet<int>());
            Assert.AreEqual(0, runtime.ActiveRecipientCount);
            runtime.Evaluate(21, 1.0f, 2.0f, 3.0f, now.AddSeconds(2), new[] { door });
            runtime.ResetAll();
            Assert.AreEqual(0, runtime.ActiveRecipientCount);
        }

        [TestMethod]
        public void TempleWorldInteractionInventoryIsExactAndOfficial()
        {
            string rules = ReadRepositoryFile(
                @"AORebirth\Server\ZoneEngine\Core\Playfields\PlayfieldContentDataProvider.cs");
            string geometry = ReadRepositoryFile(
                @"AORebirth\Server\ZoneEngine\Core\Navigation\OfficialDungeonGeometry.cs");

            StringAssert.Contains(rules, "TemplePlayfieldId = 1931");
            StringAssert.Contains(rules, "TempleGatewayPlayfieldId = 647");
            StringAssert.Contains(rules, "TempleGatewayDoorInstance = unchecked((int)0xC0080287)");
            StringAssert.Contains(rules, "TempleExteriorDoorInstance = unchecked((int)0xC024078B)");
            StringAssert.Contains(rules, "templeDoors.Length == 44");
            StringAssert.Contains(rules, "temple.Destinations.Count == 1");
            StringAssert.Contains(rules, "TempleExteriorGeometryDoorIndex = 4468");
            StringAssert.Contains(geometry, "ExteriorDoorConnectionCount");
            StringAssert.Contains(geometry, "door.RoomIndex == -1");
        }

        [TestMethod]
        public void TempleProxyEntryPreservesExactLandingAndPacketOwnership()
        {
            string rules = ReadRepositoryFile(
                @"AORebirth\Server\ZoneEngine\Core\Playfields\PlayfieldContentDataProvider.cs");
            string teleportProxy = ReadRepositoryFile(
                @"AORebirth\Server\ZoneEngine\Core\Functions\GameFunctions\teleportproxy.cs");
            string teleportHandler = ReadRepositoryFile(
                @"AORebirth\Server\ZoneEngine\Core\MessageHandlers\TeleportMessageHandler.cs");
            string playfieldAnarchy = ReadRepositoryFile(
                @"AORebirth\Server\ZoneEngine\Core\MessageHandlers\PlayfieldAnarchyFMessageHandler.cs");

            StringAssert.Contains(rules, "CapturedEntryX = 172.989990234375f");
            StringAssert.Contains(rules, "CapturedEntryY = 24.011247634887695f");
            StringAssert.Contains(rules, "CapturedEntryZ = 7.81494140625f");
            StringAssert.Contains(teleportProxy, "character.RawHeading");
            StringAssert.Contains(teleportProxy, "character.RawCoordinates");
            StringAssert.Contains(teleportHandler, "SendOfficialDungeonProxyTransfer");
            StringAssert.Contains(teleportHandler, "sourceDoor.Instance,");
            StringAssert.Contains(teleportHandler, "x.SgId = sgId");
            StringAssert.Contains(teleportHandler, "Instance = destinationPlayfieldId");
            StringAssert.Contains(playfieldAnarchy, "IsTempleProxyArrival(character)");
            StringAssert.Contains(playfieldAnarchy, "TempleGatewayDoorInstance");
        }

        [TestMethod]
        public void TempleExteriorExitIsEdgeTriggeredAndUsesSharedExitOwner()
        {
            string transitions = ReadRepositoryFile(
                @"AORebirth\Server\ZoneEngine\Core\Playfields\PlayfieldStatelTransitionRuntimeService.cs");
            string content = ReadRepositoryFile(
                @"AORebirth\Server\ZoneEngine\Core\Playfields\PlayfieldContentDataProvider.cs");
            string exitProxy = ReadRepositoryFile(
                @"AORebirth\Server\ZoneEngine\Core\Functions\GameFunctions\exitproxyplayfield.cs");

            StringAssert.Contains(content, "TempleWorldInteractionRules.IsExteriorLinkStatel(statel)");
            StringAssert.Contains(content, "IsGatewayEntryStatel");
            StringAssert.Contains(content, "TempleDoorProximityRuntime.TriggerRadius");
            StringAssert.Contains(content, "CapturedExitX = 1813.9990234375f");
            StringAssert.Contains(content, "CapturedExitY = 26.806131362915039f");
            StringAssert.Contains(content, "CapturedExitZ = 2715.84521484375f");
            StringAssert.Contains(transitions, "if (!initialized)");
            StringAssert.Contains(transitions, "if (wasInRange)");
            StringAssert.Contains(transitions, "activeEnterContacts.Add(statelKey)");
            StringAssert.Contains(transitions, "exitproxyplayfield.TryExecute(dynel, sd)");
            StringAssert.Contains(transitions, "this.statelEnterContacts.Remove(dynelId)");
            StringAssert.Contains(exitProxy, "internal static bool TryExecute");
            StringAssert.Contains(exitProxy, "externaldoorinstance");
            StringAssert.Contains(exitProxy, "externalplayfieldinstance");
            StringAssert.Contains(exitProxy, "TryResolveProxyExit");
            StringAssert.Contains(exitProxy, "SendOfficialDungeonProxyExit");

            string teleportHandler = ReadRepositoryFile(
                @"AORebirth\Server\ZoneEngine\Core\MessageHandlers\TeleportMessageHandler.cs");
            StringAssert.Contains(teleportHandler, "Type = (IdentityType)51100");
            StringAssert.Contains(teleportHandler, "Type = (IdentityType)100003");
            StringAssert.Contains(teleportHandler, "new byte[] { 0, 0, 0, 1 }");
        }

        [TestMethod]
        public void ExistingCorpusDecoderRecoversWorldInteractionFamilies()
        {
            string decoder = ReadRepositoryFile(
                @"tools-temp\AOSharpLiveCapture\decode_world_interactions.py");

            StringAssert.Contains(decoder, "N3_TELEPORT = 0x43197D22");
            StringAssert.Contains(decoder, "PLAYFIELD_ANARCHY_F = 0x5F4B1A39");
            StringAssert.Contains(decoder, "GENERIC_CMD = 0x52526858");
            StringAssert.Contains(decoder, "DOOR_STATUS_UPDATE = 0x4C7D403B");
            StringAssert.Contains(decoder, "CHEST_FULL_UPDATE = 0x465A5D73");
            StringAssert.Contains(decoder, "previous_legacy");
        }

        private static string ReadRepositoryFile(string relativePath)
        {
            DirectoryInfo directory = new DirectoryInfo(AppDomain.CurrentDomain.BaseDirectory);
            while (directory != null)
            {
                string candidate = Path.Combine(directory.FullName, relativePath);
                if (File.Exists(candidate))
                {
                    return File.ReadAllText(candidate);
                }

                directory = directory.Parent;
            }

            Assert.Fail("Repository file not found: " + relativePath);
            return string.Empty;
        }
    }
}
