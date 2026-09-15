namespace SmokeLounge.AOtomation.Messaging.Tests
{
    using System;
    using System.IO;

    using Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestClass]
    public class PlayfieldLocalitySafetyTests
    {
        [TestMethod]
        public void CellHeatSchedulingIsFailSafeAndExplicitlyOptIn()
        {
            string settings = ReadRepositoryFile(
                @"AORebirth\Libraries\Source\Utility\Config\LocalitySettings.cs");
            string config = ReadRepositoryFile(@"AORebirth\Config\Config.xml");
            string example = ReadRepositoryFile(@"AORebirth\Config\Config.example.xml");

            StringAssert.Contains(settings, "public bool EnableCellHeatScheduling { get; set; }");
            StringAssert.Contains(config, "<EnableCellHeatScheduling>false</EnableCellHeatScheduling>");
            StringAssert.Contains(example, "<EnableCellHeatScheduling>false</EnableCellHeatScheduling>");
            Assert.IsFalse(
                config.Contains("<EnableCellHeatScheduling>true</EnableCellHeatScheduling>"),
                "The checked-in runtime configuration must keep cell heat scheduling disabled by default.");
        }







        private static string ReadRepositoryFile(string relativePath)
        {
            return File.ReadAllText(
                Path.Combine(TestRepositoryRootResolver.FindFromCallerFilePath(), relativePath));
        }

        private static string ExtractBlock(string text, string signature)
        {
            int start = text.IndexOf(signature, StringComparison.Ordinal);
            Assert.IsTrue(start >= 0, "Missing source signature: " + signature);

            int open = text.IndexOf('{', start);
            Assert.IsTrue(open >= 0, "Missing opening brace for: " + signature);

            int depth = 0;
            for (int i = open; i < text.Length; i++)
            {
                if (text[i] == '{')
                {
                    depth++;
                }
                else if (text[i] == '}')
                {
                    depth--;
                    if (depth == 0)
                    {
                        return text.Substring(start, i - start + 1);
                    }
                }
            }

            Assert.Fail("Unterminated source block: " + signature);
            return string.Empty;
        }
    }
}
