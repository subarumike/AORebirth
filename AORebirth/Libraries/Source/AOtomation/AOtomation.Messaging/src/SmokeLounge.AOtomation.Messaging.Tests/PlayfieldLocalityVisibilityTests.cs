namespace SmokeLounge.AOtomation.Messaging.Tests
{
    using System.IO;

    using Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestClass]
    public class PlayfieldLocalityVisibilityTests
    {






        private static string ReadRepositoryFile(string relativePath)
        {
            return File.ReadAllText(
                Path.Combine(TestRepositoryRootResolver.FindFromCallerFilePath(), relativePath));
        }
    }
}
