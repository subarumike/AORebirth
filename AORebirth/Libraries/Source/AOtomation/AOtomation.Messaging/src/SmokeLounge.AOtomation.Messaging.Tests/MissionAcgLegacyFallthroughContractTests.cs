namespace SmokeLounge.AOtomation.Messaging.Tests
{
    using System;
    using System.IO;

    using Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestClass]
    public class MissionAcgLegacyFallthroughContractTests
    {


















        [TestMethod]
        public void LegacyTemplateScansCannotConsumeGeneratedArtifacts()
        {

            string keyStore =
                LegacyGameplaySource.ReadAllText(Path.Combine(TestRepositoryRootResolver.FindFromCallerFilePath(), @"Tests\Fixtures\Gameplay\Missions\MissionKeyStore.cs"));
            StringAssert.Contains(
                ReadMember(keyStore, "public static bool TryTakeExactNonGenerated("),
                "isGeneratedKey(mapped)");
            StringAssert.Contains(
                ReadMember(keyStore, "public static bool TryTakeLatestNonGenerated("),
                "isGeneratedKey(candidate)");
        }











        [TestMethod]
        public void TrueLegacyAndAuthoredQuestPathsRemainPresent()
        {

            string authoredTests =
                ReadRepositoryFile(
                    "AORebirth/Libraries/Source/AOtomation/"
                    + "AOtomation.Messaging/src/"
                    + "SmokeLounge.AOtomation.Messaging.Tests/"
                    + "PersistentMissionFoundationTests.cs");
            StringAssert.Contains(
                authoredTests,
                "class PersistentMissionFoundationTests");
        }

        private static void AssertOrdered(
            string source,
            params string[] fragments)
        {
            int cursor = -1;
            for (int i = 0; i < fragments.Length; i++)
            {
                int next =
                    source.IndexOf(
                        fragments[i],
                        cursor + 1,
                        StringComparison.Ordinal);
                Assert.IsTrue(
                    next > cursor,
                    "Expected fragment in order: " + fragments[i]);
                cursor = next;
            }
        }

        private static string ReadMember(string source, string signature)
        {
            int signatureIndex =
                source.IndexOf(signature, StringComparison.Ordinal);
            if (signatureIndex < 0)
            {
                return string.Empty;
            }

            int openingBrace = source.IndexOf('{', signatureIndex);
            if (openingBrace < 0)
            {
                return string.Empty;
            }

            int depth = 0;
            bool inString = false;
            bool inCharacter = false;
            bool escaped = false;
            bool inLineComment = false;
            bool inBlockComment = false;
            for (int i = openingBrace; i < source.Length; i++)
            {
                char current = source[i];
                char next = i + 1 < source.Length ? source[i + 1] : '\0';

                if (inLineComment)
                {
                    if (current == '\r' || current == '\n')
                    {
                        inLineComment = false;
                    }
                    continue;
                }

                if (inBlockComment)
                {
                    if (current == '*' && next == '/')
                    {
                        inBlockComment = false;
                        i++;
                    }
                    continue;
                }

                if (inString || inCharacter)
                {
                    if (escaped)
                    {
                        escaped = false;
                    }
                    else if (current == '\\')
                    {
                        escaped = true;
                    }
                    else if (inString && current == '"')
                    {
                        inString = false;
                    }
                    else if (inCharacter && current == '\'')
                    {
                        inCharacter = false;
                    }
                    continue;
                }

                if (current == '/' && next == '/')
                {
                    inLineComment = true;
                    i++;
                    continue;
                }

                if (current == '/' && next == '*')
                {
                    inBlockComment = true;
                    i++;
                    continue;
                }

                if (current == '"')
                {
                    inString = true;
                    continue;
                }

                if (current == '\'')
                {
                    inCharacter = true;
                    continue;
                }

                if (current == '{')
                {
                    depth++;
                }
                else if (current == '}')
                {
                    depth--;
                    if (depth == 0)
                    {
                        return source.Substring(
                            signatureIndex,
                            i - signatureIndex + 1);
                    }
                }
            }

            return string.Empty;
        }

        private static string ReadMissionSource(string fileName)
        {
            return ReadZoneSource("Core/Missions/" + fileName);
        }

        private static string ReadZoneSource(string relativePath)
        {
            return ReadRepositoryFile(
                "AORebirth/Server/ZoneEngine/"
                + relativePath.Replace('\\', '/'));
        }

        private static string ReadRepositoryFile(string relativePath)
        {
            string repositoryRoot = TestRepositoryRootResolver.FindFromCallerFilePath();
            return File.ReadAllText(
                Path.Combine(
                    repositoryRoot,
                    relativePath.Replace(
                        '/',
                        Path.DirectorySeparatorChar)));
        }
    }
}
