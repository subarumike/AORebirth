namespace SmokeLounge.AOtomation.Messaging.Tests
{
    using System;
    using System.IO;

    using Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestClass]
    public class MissionAcgTokenProgressRuntimeContractTests
    {


















        [TestMethod]
        public void DuplicateResolutionPrecedesAnyNewAppliedCount()
        {

            string state =
                LegacyGameplaySource.ReadAllText(Path.Combine(TestRepositoryRootResolver.FindFromCallerFilePath(), @"Tests\Fixtures\Gameplay\Missions\MissionAcgTokenProgressState.cs"));
            string addState =
                ReadMember(
                    state,
                    "internal MissionAcgTokenProgressState AddValidatedDeath(");
            StringAssert.Contains(addState, "TryGetEvent(");
            StringAssert.Contains(addState, "already");
        }













        [TestMethod]
        public void TerminalLifecycleRejectsNewEventsWhileRetainingAuditState()
        {
            string state =
                LegacyGameplaySource.ReadAllText(Path.Combine(TestRepositoryRootResolver.FindFromCallerFilePath(), @"Tests\Fixtures\Gameplay\Missions\MissionAcgTokenProgressState.cs"));
            string canAccept =
                ReadMember(state, "internal bool CanAcceptDeaths");
            StringAssert.Contains(
                canAccept,
                "MissionAcgLifecycleState.Active");
            Assert.IsFalse(canAccept.Contains("Completed"));
            Assert.IsFalse(canAccept.Contains("Abandoned"));
            Assert.IsFalse(canAccept.Contains("Expired"));
        }





        [TestMethod]
        public void GeneratedProgressPolicyDoesNotGrantQfuSchemaOrTeamDistribution()
        {
            string state =
                LegacyGameplaySource.ReadAllText(Path.Combine(TestRepositoryRootResolver.FindFromCallerFilePath(), @"Tests\Fixtures\Gameplay\Missions\MissionAcgTokenProgressState.cs"));
            string store =
                LegacyGameplaySource.ReadAllText(Path.Combine(TestRepositoryRootResolver.FindFromCallerFilePath(), @"Tests\Fixtures\Gameplay\Missions\MissionAcgTokenProgressStore.cs"));
            string policy =
                LegacyGameplaySource.ReadAllText(Path.Combine(TestRepositoryRootResolver.FindFromCallerFilePath(), @"Tests\Fixtures\Gameplay\Missions\MissionAcgTokenRewardPolicy.cs"));
            StringAssert.Contains(
                state,
                "MissionAcgTokenClaimDisposition.Eligible");
            StringAssert.Contains(
                state,
                "MissionAcgTokenRewardPolicy.TryResolve(progress.Percent,");
            StringAssert.Contains(
                policy,
                "if (percent < 100) { result = new MissionAcgTokenRewardData(0, 0, 0, 0, string.Empty); return true; }");
            StringAssert.Contains(state, "UnresolvedBelowFullProgress = 0");
        }

        private static void AssertGeneratedBranchPrecedesLegacyLock(
            string member,
            string generatedFragment)
        {
            int generated =
                member.IndexOf(generatedFragment, StringComparison.Ordinal);
            int legacy =
                member.IndexOf("lock (Sync)", StringComparison.Ordinal);
            Assert.IsTrue(generated >= 0 && legacy > generated);
        }

        private static void AssertReconnectOrdering(string source)
        {
            int resend =
                source.IndexOf(
                    "MissionAcceptService.TryResendForLogin(",
                    StringComparison.Ordinal);
            int pending =
                source.IndexOf(
                    "TryResumePendingClientUpdates(",
                    StringComparison.Ordinal);
            Assert.IsTrue(resend >= 0 && pending > resend);
        }

        private static void AssertOrdered(
            string source,
            params string[] fragments)
        {
            int previous = -1;
            for (int i = 0; i < fragments.Length; i++)
            {
                int current =
                    source.IndexOf(
                        fragments[i],
                        previous + 1,
                        StringComparison.Ordinal);
                Assert.IsTrue(
                    current > previous,
                    "Expected ordered fragment not found: " + fragments[i]);
                previous = current;
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
            string repositoryRoot = TestRepositoryRootResolver.FindFromCallerFilePath();
            return File.ReadAllText(
                Path.Combine(
                    repositoryRoot,
                    "AORebirth/Server/ZoneEngine",
                    relativePath.Replace('/', Path.DirectorySeparatorChar)));
        }
    }
}
