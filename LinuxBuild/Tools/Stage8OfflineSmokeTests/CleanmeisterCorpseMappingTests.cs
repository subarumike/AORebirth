using System;
using System.IO;

using ZoneEngine.Core;
using ZoneEngine.Core.Playfields;

namespace AORebirth.LinuxBuild.Stage8OfflineSmokeTests
{
    internal static class CleanmeisterCorpseMappingTests
    {
        private const int ExpectedCorpseCatMesh = 297018;
        private const int ExpectedCorpseMonsterData = 297023;
        private const int ExpectedDeathActionParameter2 = 501;

        public static void Run(string repositoryRoot)
        {
            string playfieldSource = File.ReadAllText(
                Path.Combine(
                    repositoryRoot,
                    "AORebirth",
                    "Server",
                    "ZoneEngine",
                    "Core",
                    "Playfields",
                    "Playfield.cs"));
            string areteSource = File.ReadAllText(
                Path.Combine(
                    repositoryRoot,
                    "AORebirth",
                    "Server",
                    "ZoneEngine",
                    "Core",
                    "Playfields",
                    "AlexAreaMobRuntime.cs"));

            Require(
                areteSource.Contains(
                    "private const int CleanmeisterCombatSource = unchecked((int)0x798915E0);"),
                "Cleanmeister capture source identity changed");
            Require(
                areteSource.Contains(
                    "new MobSlot(\"Cleanmeister Intelligence Robot\", MobKind.CleaningRobot, 297023,"),
                "Cleanmeister Arete source identity or MonsterData changed");
            Require(
                areteSource.Contains(
                    "CleanmeisterCombatProfile, CleanmeisterCombatSource, CleanmeisterCombatRangeMicrometers"),
                "Cleanmeister spawn lost its capture-backed combat source");

            Require(
                playfieldSource.Contains(
                    "private const int CapturedCleaningRobotCorpseCatMesh = "
                    + ExpectedCorpseCatMesh
                    + ";"),
                "Cleanmeister corpse CatMesh is not 297018");
            Require(
                playfieldSource.Contains(
                    "private const int CapturedCleaningRobotMonsterData = "
                    + ExpectedCorpseMonsterData
                    + ";"),
                "Cleanmeister corpse MonsterData is not 297023");
            Require(
                playfieldSource.Contains(
                    "private const string CleanmeisterIntelligenceRobotName = \"Cleanmeister Intelligence Robot\";"),
                "Cleanmeister corpse mapping lost its exact name identity");
            Require(
                playfieldSource.Contains("if (IsCleanmeisterIntelligenceRobot(target))")
                && playfieldSource.Contains("return CapturedCleaningRobotCorpseCatMesh;"),
                "Cleanmeister death does not select the captured corpse CatMesh");
            Require(
                !playfieldSource.Contains(
                    "private const int CapturedCleaningRobotCorpseCatMesh = "
                    + ExpectedCorpseMonsterData
                    + ";"),
                "invalid Cleanmeister corpse CatMesh 297023 returned");
            Require(
                ExpectedCorpseCatMesh != ExpectedCorpseMonsterData,
                "Cleanmeister corpse CatMesh and MonsterData must intentionally differ");
            Require(
                CombatCorpseVisuals.CorpseMonsterDataFor(
                    ExpectedCorpseMonsterData,
                    ExpectedCorpseCatMesh) == ExpectedCorpseMonsterData,
                "Cleanmeister corpse mapping changed MonsterData");
            Require(
                NpcCorpseLifecycleRules.CapturedCleaningRobotDeathActionParameter2For(true)
                == ExpectedDeathActionParameter2,
                "Cleanmeister death action Parameter2 is not capture-backed value 501");
            Require(
                NpcCorpseLifecycleRules.CapturedCleaningRobotDeathActionParameter2For(false)
                == NpcCorpseLifecycleRules.CapturedCleaningRobotDeathActionParameter2
                && NpcCorpseLifecycleRules.CapturedCleaningRobotDeathActionParameter2
                == 503,
                "generic cleaning-robot death action Parameter2 changed from 503");
            Require(
                NpcCorpseLifecycleRules.CapturedCleaningRobotDeathActionParameter2For(true)
                != NpcCorpseLifecycleRules.CapturedCleaningRobotDeathActionParameter2,
                "Cleanmeister can silently fall through to generic death action 503");

            int deathMapping = playfieldSource.IndexOf(
                "private static int DeathAnimationKeyFor(ICharacter target)",
                StringComparison.Ordinal);
            int cleanmeisterDeathBranch = playfieldSource.IndexOf(
                "if (IsCleanmeisterIntelligenceRobot(target))",
                deathMapping,
                StringComparison.Ordinal);
            int cleanmeisterDeathReturn = playfieldSource.IndexOf(
                "return NpcCorpseLifecycleRules.CapturedCleaningRobotDeathActionParameter2For(true);",
                cleanmeisterDeathBranch,
                StringComparison.Ordinal);
            int genericCleaningRobotDeathBranch = playfieldSource.IndexOf(
                "if (IsCapturedCleaningRobot(target))",
                deathMapping,
                StringComparison.Ordinal);
            int genericCleaningRobotDeathReturn = playfieldSource.IndexOf(
                "return NpcCorpseLifecycleRules.CapturedCleaningRobotDeathActionParameter2For(false);",
                genericCleaningRobotDeathBranch,
                StringComparison.Ordinal);
            Require(
                deathMapping >= 0
                && cleanmeisterDeathBranch > deathMapping
                && cleanmeisterDeathReturn > cleanmeisterDeathBranch
                && genericCleaningRobotDeathBranch > cleanmeisterDeathReturn
                && genericCleaningRobotDeathReturn > genericCleaningRobotDeathBranch,
                "Cleanmeister death action is not selected before the generic cleaning-robot mapping");

            Console.WriteLine(
                "PASS: Cleanmeister death Parameter2 501 with corpse CatMesh 297018 and MonsterData 297023");
        }

        private static void Require(bool condition, string message)
        {
            if (!condition)
            {
                throw new InvalidOperationException(message);
            }
        }
    }
}
