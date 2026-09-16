namespace SmokeLounge.AOtomation.Messaging.Tests
{
    using System;
    using System.IO;

    using Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestClass]
    public class MissionAcgAcceptanceIdempotencyContractTests
    {










        [TestMethod]
        public void RollFeeDebitAndBatchClaimShareOneExistingDatabaseTransaction()
        {

            string missionDao = ReadRepositoryFile(
                "AORebirth/Libraries/Source/AORebirth.Database/Domain/Missions/MySqlMissionDao.cs");
            string apply = ReadMember(
                missionDao,
                "public MissionRollFeeResult TryChargeRollFee(");
            AssertOrdered(
                apply,
                "connection.BeginTransaction()",
                "ReadCash(",
                "SELECT RewardType, Status, EffectReference FROM missionrewardledger",
                "INSERT INTO stats",
                "INSERT INTO missionrewardledger",
                "transaction.Commit();");
            string readCash = ReadMember(
                missionDao,
                "private static int ReadCash(");
            StringAssert.Contains(readCash, "SELECT StatValue FROM stats");
            StringAssert.Contains(readCash, "FOR UPDATE");
            Assert.IsFalse(
                apply.IndexOf("INSERT INTO missionstate", StringComparison.OrdinalIgnoreCase) >= 0);
            Assert.IsFalse(
                apply.IndexOf("CREATE TABLE", StringComparison.OrdinalIgnoreCase) >= 0);
        }



























        private static void AssertOrdered(string source, params string[] tokens)
        {
            int cursor = -1;
            for (int i = 0; i < tokens.Length; i++)
            {
                int next = source.IndexOf(tokens[i], cursor + 1, StringComparison.Ordinal);
                Assert.IsTrue(next > cursor, "Missing or out-of-order token: " + tokens[i]);
                cursor = next;
            }
        }

        private static string ReadMember(string source, string signature)
        {
            int start = source.IndexOf(signature, StringComparison.Ordinal);
            Assert.IsTrue(start >= 0, "Member signature not found: " + signature);
            int brace = source.IndexOf('{', start);
            Assert.IsTrue(brace >= 0, "Member body not found: " + signature);
            int depth = 0;
            for (int i = brace; i < source.Length; i++)
            {
                if (source[i] == '{')
                {
                    depth++;
                }
                else if (source[i] == '}')
                {
                    depth--;
                    if (depth == 0)
                    {
                        return source.Substring(start, i - start + 1);
                    }
                }
            }

            Assert.Fail("Member body was not balanced: " + signature);
            return string.Empty;
        }

        private static string ReadMissionSource(string fileName)
        {
            return ReadZoneSource("Core/Missions/" + fileName);
        }

        private static string ReadZoneSource(string relativePath)
        {
            return ReadRepositoryFile(
                "AORebirth/Server/ZoneEngine/" + relativePath.Replace('\\', '/'));
        }

        private static string ReadRepositoryFile(string relativePath)
        {
            DirectoryInfo current = new DirectoryInfo(AppDomain.CurrentDomain.BaseDirectory);
            while (current != null)
            {
                string gitEntry = Path.Combine(current.FullName, ".git");
                if (Directory.Exists(gitEntry) || File.Exists(gitEntry))
                {
                    return File.ReadAllText(
                        Path.Combine(
                            current.FullName,
                            relativePath.Replace('/', Path.DirectorySeparatorChar)));
                }

                current = current.Parent;
            }

            Assert.Fail("Repository root could not be located.");
            return string.Empty;
        }
    }
}
