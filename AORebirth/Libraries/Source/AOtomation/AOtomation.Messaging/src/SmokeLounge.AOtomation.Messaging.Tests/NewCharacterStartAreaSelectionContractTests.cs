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
                @"AORebirth\Libraries\Source\AORebirth.Database\Domain\Missions\MySqlMissionDao.cs");

            StringAssert.Contains(characterName, "if (!startInSL)");
            StringAssert.Contains(characterName, "missionDao.MarkStartAreaSelectionPending(charid)");
            StringAssert.Contains(dao, "INSERT INTO missionflags");
            StringAssert.Contains(dao, "AND `Value`=@PendingState");
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
