namespace SmokeLounge.AOtomation.Messaging.Tests
{
    using System;
    using System.IO;

    using Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestClass]
    public class PlayfieldVisibilityLifecycleIntegrationTests
    {
















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

        private static void AssertBefore(string text, string first, string second)
        {
            int firstIndex = text.IndexOf(first, StringComparison.Ordinal);
            int secondIndex = text.IndexOf(second, StringComparison.Ordinal);
            Assert.IsTrue(firstIndex >= 0, "Missing source fragment: " + first);
            Assert.IsTrue(secondIndex >= 0, "Missing source fragment: " + second);
            Assert.IsTrue(firstIndex < secondIndex, "Expected source ordering was not preserved.");
        }
    }
}
