namespace SmokeLounge.AOtomation.Messaging.Tests
{
    using System;
    using System.IO;
    using System.Runtime.CompilerServices;

    using Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestClass]
    public class NewCharacterStartAreaSelectionContractTests
    {
        [TestMethod]
        public void OfficialShadowlandsSelectorRemainsIndependent()
        {
            string root = FindRepositoryRoot();
            string handler = Read(root, @"AORebirth\Server\LoginEngine\MessageHandlers\CreateCharacterHandler.cs");
            string starterArea = Read(
                root,
                @"AORebirth\Libraries\Source\AOtomation\AOtomation.Messaging\src\SmokeLounge.AOtomation.Messaging\Messages\SystemMessages\StarterArea.cs");

            StringAssert.Contains(handler, "createCharacterMessage.StarterArea == StarterArea.Shadowlands");
            StringAssert.Contains(starterArea, "RubiKa = 0");
            StringAssert.Contains(starterArea, "Shadowlands = 1");
        }

        [TestMethod]
        public void OnlyNewRubiKaCharactersReceivePendingSelectionState()
        {
            string root = FindRepositoryRoot();
            string characterName = Read(root, @"AORebirth\Server\LoginEngine\Packets\CharacterName.cs");
            string dao = Read(
                root,
                @"AORebirth\Libraries\Source\AORebirth.Database\Dao\NewCharacterStartAreaSelectionDao.cs");

            StringAssert.Contains(characterName, "if (!startInSL)");
            StringAssert.Contains(characterName, "NewCharacterStartAreaSelectionDao.MarkPending(charid)");
            StringAssert.Contains(dao, "INSERT INTO missionflags");
            StringAssert.Contains(dao, "AND `Value`=@PendingState");
        }

        [TestMethod]
        public void RuntimeOffersExactDestinationsAndPersistsBeforeTransfer()
        {
            string root = FindRepositoryRoot();
            string runtime = Read(
                root,
                @"AORebirth\Server\ZoneEngine\Core\NewCharacterStartAreaSelectionRuntime.cs");
            string areteSpawn = Read(
                root,
                @"AORebirth\Server\ZoneEngine\Core\Playfields\AreteLandingSpawn.cs");

            StringAssert.Contains(runtime, "AreteOption = \"Arete\"");
            StringAssert.Contains(runtime, "IccShuttleportOption = \"ICC Shuttleport\"");
            StringAssert.Contains(runtime, "PromptSpeakerName = \"ICC Shuttleport Commander\"");
            StringAssert.Contains(areteSpawn, "Name = \"ICC Shuttleport Commander\"");
            StringAssert.Contains(runtime, "IccShuttleportPlayfieldId = 4582");
            StringAssert.Contains(runtime, "IccShuttleportX = 939.0f");
            StringAssert.Contains(runtime, "IccShuttleportY = 20.3f");
            StringAssert.Contains(runtime, "IccShuttleportZ = 732.0f");
            AssertTextBefore(
                runtime,
                "NewCharacterStartAreaSelectionDao.TryComplete",
                "TeleportToIccShuttleport(character)");
        }

        [TestMethod]
        public void LoginAndKnuBotHandlersOwnTheSelectionBeforeNpcDialogue()
        {
            string root = FindRepositoryRoot();
            string login = Read(root, @"AORebirth\Server\ZoneEngine\Core\PacketHandlers\ClientConnected.cs");
            string answer = Read(
                root,
                @"AORebirth\Server\ZoneEngine\Core\MessageHandlers\KnuBotAnswerMessageHandler.cs");
            string close = Read(
                root,
                @"AORebirth\Server\ZoneEngine\Core\MessageHandlers\KnuBotCloseChatWindowMessageHandler.cs");

            AssertTextBefore(
                login,
                "CompleteSessionInitialization",
                "NewCharacterStartAreaSelectionRuntime.Schedule(client)");
            AssertTextBefore(
                answer,
                "NewCharacterStartAreaSelectionRuntime.TryHandleAnswer",
                "ContentDrivenNpcDialogueRouter.TryHandleAnswer");
            AssertTextBefore(
                close,
                "NewCharacterStartAreaSelectionRuntime.TryHandleClose",
                "ContentDrivenNpcDialogueRouter.TryHandleClose");
        }

        private static string Read(string root, string relativePath)
        {
            return File.ReadAllText(Path.Combine(root, relativePath));
        }

        private static void AssertTextBefore(string text, string first, string second)
        {
            int firstIndex = text.IndexOf(first, StringComparison.Ordinal);
            int secondIndex = text.IndexOf(second, StringComparison.Ordinal);
            Assert.IsTrue(firstIndex >= 0, "Missing: " + first);
            Assert.IsTrue(secondIndex >= 0, "Missing: " + second);
            Assert.IsTrue(firstIndex < secondIndex, first + " must precede " + second);
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

            throw new DirectoryNotFoundException("Unable to locate repository root.");
        }
    }
}
