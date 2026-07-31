namespace SmokeLounge.AOtomation.Messaging.Tests
{
    using System;
    using System.Collections.Generic;
    using System.IO;

    using Microsoft.VisualStudio.TestTools.UnitTesting;

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
