namespace SmokeLounge.AOtomation.Messaging.Tests
{
    using System;
    using System.IO;

    using Microsoft.VisualStudio.TestTools.UnitTesting;

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
            StringAssert.Contains(service, "statel.Identity.Type == IdentityType.Door");
            StringAssert.Contains(service, ".Distinct()");
            StringAssert.Contains(
                service,
                "DoorStatusUpdateMessageHandler.Default.SendStatus(character, door, false)");
            StringAssert.Contains(playfield, "this.statels");
            StringAssert.Contains(playfield, "SendInitialStatuses(");
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
            StringAssert.Contains(handler, "message.Unknown3 = open ? (byte)1 : (byte)0");
            StringAssert.Contains(handler, "message.Unknown6 = new Identity[0]");
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
