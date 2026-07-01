// This source code is licensed under the MIT license that can be found in the LICENSE file.

namespace SmokeLounge.AOtomation.Messaging.Tests
{
    #region Usings ...

    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Runtime.CompilerServices;

    using Microsoft.VisualStudio.TestTools.UnitTesting;

    using SmokeLounge.AOtomation.Messaging.GameData;

    using ZoneEngine.Core.Playfields;

    #endregion

    [TestClass]
    public class PlayfieldLifecycleTraceTests
    {
        [TestMethod]
        public void PrivateCityReadyInitKeepsOrgStateBeforeFullCharacterAndReadyBlockAfter()
        {
            using (PlayfieldLifecycleCapture capture = PlayfieldLifecycleTrace.Capture())
            {
                RecordExpected(
                    PlayfieldLifecycleTrace.FlowPrivateCityReadyInit,
                    PlayfieldLifecycleTrace.ExpectedPrivateCityReadyInitOrder);

                AssertExpectedOrder(
                    capture.Events,
                    PlayfieldLifecycleTrace.FlowPrivateCityReadyInit,
                    PlayfieldLifecycleTrace.ExpectedPrivateCityReadyInitOrder);
                AssertStageBefore(
                    capture.Events,
                    PlayfieldLifecycleTrace.StagePrivateCityReadyBlockBegin,
                    PlayfieldLifecycleTrace.StagePrivateCityOrgInfoPacket);
                AssertStageBefore(
                    capture.Events,
                    PlayfieldLifecycleTrace.StagePrivateCityOrgInfoPacket,
                    PlayfieldLifecycleTrace.StagePrivateCityFullCharacter);
                AssertStageBefore(
                    capture.Events,
                    PlayfieldLifecycleTrace.StagePrivateCityOrgInitSent,
                    PlayfieldLifecycleTrace.StagePrivateCityFullCharacter);
                AssertStageBefore(
                    capture.Events,
                    PlayfieldLifecycleTrace.StagePrivateCityFullCharacter,
                    PlayfieldLifecycleTrace.StagePrivateCityPlayfieldAllTowers);
                AssertStageBefore(
                    capture.Events,
                    PlayfieldLifecycleTrace.StagePrivateCityPlayfieldAllTowers,
                    PlayfieldLifecycleTrace.StagePrivateCityPlayfieldAllCities);
                AssertStageBefore(
                    capture.Events,
                    PlayfieldLifecycleTrace.StagePrivateCityPlayfieldAllCities,
                    PlayfieldLifecycleTrace.StagePrivateCityTowersCitiesSent);
                AssertStageBefore(
                    capture.Events,
                    PlayfieldLifecycleTrace.StagePrivateCityTowersCitiesSent,
                    PlayfieldLifecycleTrace.StagePrivateCityReadyBlockEnd);
            }
        }

        [TestMethod]
        public void PrivateCityReadyInitRecorderGuardsPacketMessageOrderAndDetails()
        {
            using (PlayfieldLifecycleCapture capture = PlayfieldLifecycleTrace.Capture())
            {
                RecordPrivateCityReadyInitCurrentPacketSequence();

                AssertExpectedOrder(
                    capture.Events,
                    PlayfieldLifecycleTrace.FlowPrivateCityReadyInit,
                    PlayfieldLifecycleTrace.ExpectedPrivateCityReadyInitOrder);
                Assert.AreEqual(
                    PlayfieldLifecycleTrace.ExpectedPrivateCityReadyInitOrder.Length,
                    CountFlow(capture.Events, PlayfieldLifecycleTrace.FlowPrivateCityReadyInit));
                Assert.AreEqual(
                    4,
                    CountStage(capture.Events, PlayfieldLifecycleTrace.StagePrivateCitySocialStatus),
                    "Private-city org init must preserve the captured repeated SocialStatus=4 sequence.");

                AssertMessageForStage(
                    capture.Events,
                    PlayfieldLifecycleTrace.StagePrivateCityReadyBlockBegin,
                    PlayfieldLifecycleTrace.MessagePrivateCityReadyBlockBegin);
                AssertMessageForStage(
                    capture.Events,
                    PlayfieldLifecycleTrace.StagePrivateCitySimpleCharFullUpdateBroadcast,
                    PlayfieldLifecycleTrace.MessageSimpleCharFullUpdate);
                AssertMessageForStage(
                    capture.Events,
                    PlayfieldLifecycleTrace.StagePrivateCityOrgInfoPacket,
                    PlayfieldLifecycleTrace.MessageOrgInfoPacket);
                AssertMessageForStage(
                    capture.Events,
                    PlayfieldLifecycleTrace.StagePrivateCityOrgInitSent,
                    PlayfieldLifecycleTrace.MessagePrivateCityOrgInitSent);
                AssertMessageForStage(
                    capture.Events,
                    PlayfieldLifecycleTrace.StagePrivateCityFullCharacter,
                    PlayfieldLifecycleTrace.MessageFullCharacter);
                AssertMessageForStage(
                    capture.Events,
                    PlayfieldLifecycleTrace.StagePrivateCityPlayfieldAllTowers,
                    PlayfieldLifecycleTrace.MessagePlayfieldAllTowers);
                AssertMessageForStage(
                    capture.Events,
                    PlayfieldLifecycleTrace.StagePrivateCityPlayfieldAllCities,
                    PlayfieldLifecycleTrace.MessagePlayfieldAllCities);
                AssertMessageForStage(
                    capture.Events,
                    PlayfieldLifecycleTrace.StagePrivateCityTowersCitiesSent,
                    PlayfieldLifecycleTrace.MessagePrivateCityTowersCitiesSent);
                AssertMessageForStage(
                    capture.Events,
                    PlayfieldLifecycleTrace.StagePrivateCityReadyBlockEnd,
                    PlayfieldLifecycleTrace.MessagePrivateCityReadyBlockEnd);

                Assert.IsTrue(
                    HasDetail(capture.Events, PlayfieldLifecycleTrace.StagePrivateCityOrgInfoPacket, "Est. 2024"),
                    "Private-city org info must remain before FullCharacter.");
                Assert.IsTrue(
                    HasDetailContains(capture.Events, PlayfieldLifecycleTrace.StagePrivateCityOrgInitSent, "org=1970177"),
                    "Private-city org init summary must preserve the captured organization identity.");
                Assert.IsTrue(
                    HasDetailContains(capture.Events, PlayfieldLifecycleTrace.StagePrivateCityTowersCitiesSent, "cityPayloadBytes=0"),
                    "Captured Montroyal private-city ready block currently sends the empty towers/cities fallback.");
            }
        }

        [TestMethod]
        public void SamePlayfieldVisibilityKeepsCharInPlayAndExistingPlayerSnapshotOrder()
        {
            using (PlayfieldLifecycleCapture capture = PlayfieldLifecycleTrace.Capture())
            {
                RecordExpected(
                    PlayfieldLifecycleTrace.FlowSamePlayfieldVisibility,
                    PlayfieldLifecycleTrace.ExpectedCharInPlayEntryOrder);
                RecordExpected(
                    PlayfieldLifecycleTrace.FlowSamePlayfieldVisibility,
                    PlayfieldLifecycleTrace.ExpectedSamePlayfieldVisibilityOrder);

                AssertExpectedOrder(
                    capture.Events,
                    PlayfieldLifecycleTrace.FlowSamePlayfieldVisibility,
                    PlayfieldLifecycleTrace.ExpectedCharInPlayEntryOrder);
                AssertExpectedOrder(
                    capture.Events,
                    PlayfieldLifecycleTrace.FlowSamePlayfieldVisibility,
                    PlayfieldLifecycleTrace.ExpectedSamePlayfieldVisibilityOrder);
                AssertStageBefore(
                    capture.Events,
                    PlayfieldLifecycleTrace.StageVisibilityJoinerReady,
                    PlayfieldLifecycleTrace.StageExistingCharacterSimpleCharFullUpdate);
                AssertStageBefore(
                    capture.Events,
                    PlayfieldLifecycleTrace.StageExistingCharacterSimpleCharFullUpdate,
                    PlayfieldLifecycleTrace.StageExistingCharacterCharInPlay);
            }
        }

        [TestMethod]
        public void CleaningRobotDeathOrderIncludesStopFightDeathCorpseAndDespawnScheduling()
        {
            Identity attacker = new Identity { Type = IdentityType.CanbeAffected, Instance = 1001 };
            Identity robot = new Identity { Type = IdentityType.CanbeAffected, Instance = 2001 };
            Identity corpse = new Identity { Type = IdentityType.Corpse, Instance = 3001 };

            using (PlayfieldLifecycleCapture capture = PlayfieldLifecycleTrace.Capture())
            {
                PlayfieldLifecycleTrace.Record(
                    PlayfieldLifecycleTrace.FlowCleaningRobotDeathCorpseDespawn,
                    PlayfieldLifecycleTrace.StageAttackerStopFight,
                    PlayfieldLifecycleTrace.MessageStopFight,
                    attacker,
                    "deadTarget=" + robot);
                PlayfieldLifecycleTrace.Record(
                    PlayfieldLifecycleTrace.FlowCleaningRobotDeathCorpseDespawn,
                    PlayfieldLifecycleTrace.StageRobotStopFight,
                    PlayfieldLifecycleTrace.MessageStopFight,
                    robot);
                PlayfieldLifecycleTrace.Record(
                    PlayfieldLifecycleTrace.FlowCleaningRobotDeathCorpseDespawn,
                    PlayfieldLifecycleTrace.StageCharacterActionDeathParameter2,
                    PlayfieldLifecycleTrace.MessageCharacterActionDeath,
                    robot,
                    "Parameter2=500");
                PlayfieldLifecycleTrace.Record(
                    PlayfieldLifecycleTrace.FlowCleaningRobotDeathCorpseDespawn,
                    PlayfieldLifecycleTrace.StageCorpseSpawnScheduled,
                    "CorpseSpawnScheduled",
                    corpse,
                    "deadNpc=" + robot + " delayMs=600");
                PlayfieldLifecycleTrace.Record(
                    PlayfieldLifecycleTrace.FlowCleaningRobotDeathCorpseDespawn,
                    PlayfieldLifecycleTrace.StageDeadNpcDespawnScheduled,
                    "DeadNpcDespawnScheduled",
                    robot,
                    "delayMs=10000");
                PlayfieldLifecycleTrace.Record(
                    PlayfieldLifecycleTrace.FlowCleaningRobotDeathCorpseDespawn,
                    PlayfieldLifecycleTrace.StageCorpseFullUpdate,
                    PlayfieldLifecycleTrace.MessageCorpseFullUpdate,
                    corpse,
                    "deadNpc=" + robot);

                AssertExpectedOrder(
                    capture.Events,
                    PlayfieldLifecycleTrace.FlowCleaningRobotDeathCorpseDespawn,
                    PlayfieldLifecycleTrace.ExpectedCleaningRobotDeathOrder);
                AssertStageBefore(
                    capture.Events,
                    PlayfieldLifecycleTrace.StageAttackerStopFight,
                    PlayfieldLifecycleTrace.StageRobotStopFight);
                AssertStageBefore(
                    capture.Events,
                    PlayfieldLifecycleTrace.StageRobotStopFight,
                    PlayfieldLifecycleTrace.StageCharacterActionDeathParameter2);
                AssertStageBefore(
                    capture.Events,
                    PlayfieldLifecycleTrace.StageCharacterActionDeathParameter2,
                    PlayfieldLifecycleTrace.StageCorpseFullUpdate);
                Assert.IsTrue(
                    HasDetail(capture.Events, PlayfieldLifecycleTrace.StageCharacterActionDeathParameter2, "Parameter2=500"),
                    "Robot death trace must preserve captured CharacterAction Death Parameter2=500.");
            }
        }

        [TestMethod]
        public void CleaningRobotNpcAttackOrderKeepsSpecialAttackWeaponBeforeAttackInfo()
        {
            Identity robot = new Identity { Type = IdentityType.CanbeAffected, Instance = 2001 };
            Identity target = new Identity { Type = IdentityType.CanbeAffected, Instance = 1001 };

            using (PlayfieldLifecycleCapture capture = PlayfieldLifecycleTrace.Capture())
            {
                PlayfieldLifecycleTrace.Record(
                    PlayfieldLifecycleTrace.FlowCleaningRobotNpcAttack,
                    PlayfieldLifecycleTrace.StageRobotSpecialAttackWeaponContext,
                    PlayfieldLifecycleTrace.MessageSpecialAttackWeapon,
                    robot,
                    "target=" + target);
                PlayfieldLifecycleTrace.Record(
                    PlayfieldLifecycleTrace.FlowCleaningRobotNpcAttack,
                    PlayfieldLifecycleTrace.StageRobotAttackStartContext,
                    PlayfieldLifecycleTrace.MessageAttack,
                    robot,
                    "target=" + target);
                PlayfieldLifecycleTrace.Record(
                    PlayfieldLifecycleTrace.FlowCleaningRobotNpcAttack,
                    PlayfieldLifecycleTrace.StageRobotAttackInfo,
                    PlayfieldLifecycleTrace.MessageAttackInfo,
                    robot,
                    "target=" + target);

                AssertExpectedOrder(
                    capture.Events,
                    PlayfieldLifecycleTrace.FlowCleaningRobotNpcAttack,
                    PlayfieldLifecycleTrace.ExpectedCleaningRobotNpcAttackOrder);
                AssertStageBefore(
                    capture.Events,
                    PlayfieldLifecycleTrace.StageRobotSpecialAttackWeaponContext,
                    PlayfieldLifecycleTrace.StageRobotAttackStartContext);
                AssertStageBefore(
                    capture.Events,
                    PlayfieldLifecycleTrace.StageRobotAttackStartContext,
                    PlayfieldLifecycleTrace.StageRobotAttackInfo);
            }
        }

        [TestMethod]
        public void NpcCorpseLifecycleRulesPreserveCapturedCleaningRobotDeathTimings()
        {
            Assert.AreEqual(
                600,
                (int)NpcCorpseLifecycleRules.CorpseSpawnDelay.TotalMilliseconds,
                "Cleaning robot corpse spawn delay must stay capture-backed.");
            Assert.AreEqual(
                10000,
                (int)NpcCorpseLifecycleRules.DeadNpcDespawnDelay.TotalMilliseconds,
                "Dead NPC despawn delay must stay capture-backed.");
            Assert.AreEqual(
                500,
                NpcCorpseLifecycleRules.CapturedCleaningRobotDeathActionParameter2,
                "Cleaning robot CharacterAction Death Parameter2 must stay capture-backed.");
        }

        [TestMethod]
        public void NpcCombatAttackRulesPreserveCapturedCleaningRobotContextDecision()
        {
            Assert.AreEqual(10, NpcCombatAttackRules.CapturedCleaningRobotRightHandDamage);
            Assert.AreEqual(8, NpcCombatAttackRules.CapturedCleaningRobotLeftHandDamage);
            Assert.AreEqual(
                2700,
                (int)(NpcCombatAttackRules.CapturedCleaningRobotCombatTickSeconds * 1000));
            Assert.IsTrue(
                NpcCombatAttackRules.ShouldSendCapturedCleaningRobotAttackStartContext(
                    true,
                    false,
                    null,
                    1001));
            Assert.IsFalse(
                NpcCombatAttackRules.ShouldSendCapturedCleaningRobotAttackStartContext(
                    true,
                    false,
                    1001,
                    1001));
            Assert.IsFalse(
                NpcCombatAttackRules.ShouldSendCapturedCleaningRobotAttackStartContext(
                    true,
                    true,
                    null,
                    1001));
        }

        [TestMethod]
        public void CapturedAreteRobotContentProviderPreservesSpawnDefinitions()
        {
            var provider = new CapturedAreteRobotContentProvider();
            CapturedAreteRobotSpawnDefinition[] spawns = provider.GetSpawnDefinitions();

            Assert.AreEqual(7, spawns.Length);
            Assert.AreEqual("Malfunctioning Cleaning Robot", CapturedAreteRobotContentProvider.RobotName);
            Assert.AreEqual(297023, CapturedAreteRobotContentProvider.MonsterData);
            Assert.AreEqual(0x79225E7C, spawns[0].SourceInstance);
            Assert.AreEqual(12, spawns[0].Health);
            Assert.AreEqual(1, spawns[0].Level);
            Assert.AreEqual(6, spawns[0].RunSpeed);
            Assert.AreEqual(3617.86938f, spawns[0].X);
            Assert.AreEqual(51.7449989f, spawns[0].Y);
            Assert.AreEqual(784.657471f, spawns[0].Z);
            Assert.AreEqual(3622.77563f, spawns[0].PatrolX);
            Assert.AreEqual(52.5f, spawns[0].PatrolY);
            Assert.AreEqual(798.800964f, spawns[0].PatrolZ);
        }

        [TestMethod]
        public void CapturedAreteRobotContentProviderPreservesPatrolReplayPathAndMissingFileFallback()
        {
            Assert.AreEqual(
                @"Content\Captured\Arete\cleaning_robot_patrol_replay.csv",
                CapturedAreteRobotContentProvider.PatrolReplayRelativePath);
            Assert.AreEqual(
                @"tools-temp\AOSharpLiveCapture\bin\Debug\captures\20260629-193121\movement-packets.csv",
                CapturedAreteRobotContentProvider.EvidenceCapturePatrolReplayRelativePath);

            var provider = new CapturedAreteRobotContentProvider(
                new[] { Path.Combine(Path.GetTempPath(), Path.GetRandomFileName(), "movement-packets.csv") });

            Assert.AreEqual(string.Empty, provider.FindPatrolReplayPath());
            Assert.AreEqual(0, provider.GetPatrolReplaySegments(0x79225E7C).Length);
        }

        [TestMethod]
        public void CapturedAreteRobotContentProviderLoadsCommittedPatrolReplayData()
        {
            var provider = new CapturedAreteRobotContentProvider();
            string replayPath = provider.FindPatrolReplayPath();

            Assert.IsTrue(File.Exists(replayPath));
            Assert.IsTrue(
                replayPath.IndexOf("tools-temp", StringComparison.OrdinalIgnoreCase) < 0,
                "Runtime replay data must load from committed content, not tools-temp captures.");

            Assert.AreEqual(35, provider.GetPatrolReplaySegments(0x79225E7D).Length);
            Assert.AreEqual(40, provider.GetPatrolReplaySegments(0x79225E7C).Length);
            Assert.AreEqual(38, provider.GetPatrolReplaySegments(0x79225E77).Length);
            Assert.AreEqual(31, provider.GetPatrolReplaySegments(0x79225E7A).Length);
            Assert.AreEqual(39, provider.GetPatrolReplaySegments(0x79225E78).Length);
            Assert.AreEqual(29, provider.GetPatrolReplaySegments(0x79225E79).Length);
            Assert.AreEqual(18, provider.GetPatrolReplaySegments(0x79225E76).Length);

            CapturedAreteRobotPatrolReplaySegment first =
                provider.GetPatrolReplaySegments(0x79225E7D)[0];
            Assert.AreEqual(3605.55493f, first.StartX);
            Assert.AreEqual(51.7449989f, first.StartY);
            Assert.AreEqual(773.164246f, first.StartZ);
            Assert.AreEqual(3602.2915f, first.EndX);
            Assert.AreEqual(52.5f, first.EndY);
            Assert.AreEqual(787.929504f, first.EndZ);

            CapturedAreteRobotPatrolReplaySegment[] lastRoute =
                provider.GetPatrolReplaySegments(0x79225E7C);
            CapturedAreteRobotPatrolReplaySegment last = lastRoute[lastRoute.Length - 1];
            Assert.AreEqual(3612.93481f, last.StartX);
            Assert.AreEqual(52.1349983f, last.StartY);
            Assert.AreEqual(787.84082f, last.StartZ);
            Assert.AreEqual(3611.29053f, last.EndX);
            Assert.AreEqual(52.5f, last.EndY);
            Assert.AreEqual(778.074585f, last.EndZ);
        }

        [TestMethod]
        public void NpcPatrolReplayCoordinatorAssignsCapturedReplaySegmentsFromProvider()
        {
            string path = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
            File.WriteAllLines(
                path,
                new[]
                {
                    "CapturedUtc,MessageType,SourceInstance,FollowKind,CurrentX,CurrentY,CurrentZ,DestinationX,DestinationY,DestinationZ",
                    "2026-06-29T19:31:21.0000000Z,FollowTarget,79225E7C,NpcPath,1,2,3,4,5,6",
                    "2026-06-29T19:31:22.5000000Z,FollowTarget,79225E7C,NpcPath,4,5,6,7,8,9"
                });

            try
            {
                var provider = new CapturedAreteRobotContentProvider(new[] { path });
                var coordinator = new NpcPatrolReplayCoordinator(provider);
                NpcPatrolReplaySegment[] assigned = null;

                coordinator.AssignCapturedAreteRobotReplay(
                    0x79225E7C,
                    segments => assigned = segments);

                Assert.IsNotNull(assigned);
                Assert.AreEqual(2, assigned.Length);
                Assert.AreEqual(1.5, assigned[0].DelayAfterSeconds);
                Assert.AreEqual(1f, assigned[0].StartX);
                Assert.AreEqual(2f, assigned[0].StartY);
                Assert.AreEqual(3f, assigned[0].StartZ);
                Assert.AreEqual(4f, assigned[0].EndX);
                Assert.AreEqual(5f, assigned[0].EndY);
                Assert.AreEqual(6f, assigned[0].EndZ);
                Assert.AreEqual(0.25, assigned[1].DelayAfterSeconds);
            }
            finally
            {
                File.Delete(path);
            }
        }

        [TestMethod]
        public void NpcPatrolReplayCoordinatorAssignsEmptyReplayForMissingProviderData()
        {
            var provider = new CapturedAreteRobotContentProvider(
                new[] { Path.Combine(Path.GetTempPath(), Path.GetRandomFileName(), "movement-packets.csv") });
            var coordinator = new NpcPatrolReplayCoordinator(provider);
            NpcPatrolReplaySegment[] assigned = null;

            coordinator.AssignCapturedAreteRobotReplay(
                0x79225E7C,
                segments => assigned = segments);

            Assert.IsNotNull(assigned);
            Assert.AreEqual(0, assigned.Length);
        }

        [TestMethod]
        public void CapturedAreteRobotSpawnOrchestrationTraceKeepsSetupReplayAndScfuOrder()
        {
            var provider = new CapturedAreteRobotContentProvider();
            var coordinator = new NpcPatrolReplayCoordinator(provider);
            CapturedAreteRobotSpawnDefinition[] spawns = provider.GetSpawnDefinitions();
            CapturedAreteRobotSpawnDefinition spawn = spawns[0];
            Identity playfield = new Identity { Type = IdentityType.Playfield, Instance = 6553 };
            Identity robot = new Identity { Type = IdentityType.CanbeAffected, Instance = 2001 };
            string spawnCreatedDetail =
                PlayfieldLifecycleTrace.FormatCapturedAreteRobotSpawnCreatedDetail(
                    spawn.SourceInstance,
                    CapturedAreteRobotContentProvider.MonsterData,
                    spawn.Health,
                    spawn.Level,
                    spawn.RunSpeed,
                    spawn.X,
                    spawn.Y,
                    spawn.Z,
                    spawn.PatrolX,
                    spawn.PatrolY,
                    spawn.PatrolZ);

            using (PlayfieldLifecycleCapture capture = PlayfieldLifecycleTrace.Capture())
            {
                PlayfieldLifecycleTrace.Record(
                    PlayfieldLifecycleTrace.FlowCapturedAreteRobotSpawn,
                    PlayfieldLifecycleTrace.StageCapturedAreteRobotSpawnRowsLoaded,
                    PlayfieldLifecycleTrace.MessageCapturedAreteRobotSpawnRowsLoaded,
                    playfield,
                    PlayfieldLifecycleTrace.FormatCapturedAreteRobotSpawnRowsDetail(
                        spawns.Length,
                        CapturedAreteRobotContentProvider.MonsterData));
                PlayfieldLifecycleTrace.Record(
                    PlayfieldLifecycleTrace.FlowCapturedAreteRobotSpawn,
                    PlayfieldLifecycleTrace.StageCapturedAreteRobotSpawnCreated,
                    PlayfieldLifecycleTrace.MessageCapturedAreteRobotSpawnCreated,
                    robot,
                    spawnCreatedDetail);

                NpcPatrolReplaySegment[] assigned = null;
                coordinator.AssignCapturedAreteRobotReplay(spawn.SourceInstance, segments => assigned = segments);
                Assert.IsNotNull(assigned);
                Assert.AreEqual(40, assigned.Length);

                PlayfieldLifecycleTrace.Record(
                    PlayfieldLifecycleTrace.FlowCapturedAreteRobotSpawn,
                    PlayfieldLifecycleTrace.StageCapturedAreteRobotPatrolReplayAssigned,
                    PlayfieldLifecycleTrace.MessageCapturedAreteRobotPatrolReplayAssigned,
                    robot,
                    PlayfieldLifecycleTrace.FormatCapturedAreteRobotPatrolReplayAssignedDetail(
                        spawn.SourceInstance,
                        assigned.Length));
                PlayfieldLifecycleTrace.Record(
                    PlayfieldLifecycleTrace.FlowCapturedAreteRobotSpawn,
                    PlayfieldLifecycleTrace.StageCapturedAreteRobotSimpleCharFullUpdateBroadcast,
                    PlayfieldLifecycleTrace.MessageSimpleCharFullUpdate,
                    robot,
                    PlayfieldLifecycleTrace.FormatCapturedAreteRobotSimpleCharFullUpdateDetail(spawn.SourceInstance));

                AssertExpectedOrder(
                    capture.Events,
                    PlayfieldLifecycleTrace.FlowCapturedAreteRobotSpawn,
                    PlayfieldLifecycleTrace.ExpectedCapturedAreteRobotSpawnOrder);
                AssertStageBefore(
                    capture.Events,
                    PlayfieldLifecycleTrace.StageCapturedAreteRobotSpawnRowsLoaded,
                    PlayfieldLifecycleTrace.StageCapturedAreteRobotSpawnCreated);
                AssertStageBefore(
                    capture.Events,
                    PlayfieldLifecycleTrace.StageCapturedAreteRobotSpawnCreated,
                    PlayfieldLifecycleTrace.StageCapturedAreteRobotPatrolReplayAssigned);
                AssertStageBefore(
                    capture.Events,
                    PlayfieldLifecycleTrace.StageCapturedAreteRobotPatrolReplayAssigned,
                    PlayfieldLifecycleTrace.StageCapturedAreteRobotSimpleCharFullUpdateBroadcast);
                Assert.IsTrue(
                    HasDetail(
                        capture.Events,
                        PlayfieldLifecycleTrace.StageCapturedAreteRobotSpawnRowsLoaded,
                        "count=7 monsterData=297023"));
                Assert.IsTrue(
                    HasDetail(
                        capture.Events,
                        PlayfieldLifecycleTrace.StageCapturedAreteRobotSpawnCreated,
                        spawnCreatedDetail));
                Assert.IsTrue(
                    spawnCreatedDetail.IndexOf(
                        "sourceInstance=79225E7C monsterData=297023 hp=12 level=1 runSpeed=6",
                        StringComparison.Ordinal) >= 0);
                Assert.IsTrue(
                    HasDetail(
                        capture.Events,
                        PlayfieldLifecycleTrace.StageCapturedAreteRobotPatrolReplayAssigned,
                        "sourceInstance=79225E7C segments=40"));
            }
        }

        [TestMethod]
        public void PlayfieldContentModulesDoNotOwnRuntimeSystems()
        {
            string contentDirectory = Path.Combine(
                FindRepositoryRoot(),
                @"AORebirth\Server\ZoneEngine\Core\Playfields\Content");
            string[] sourceFiles = Directory.GetFiles(contentDirectory, "*.cs", SearchOption.AllDirectories);

            Assert.IsTrue(sourceFiles.Length >= 4, "Expected current Playfield content-module files to be scanned.");

            foreach (string sourceFile in sourceFiles)
            {
                string text = File.ReadAllText(sourceFile);
                Assert.IsTrue(
                    text.Contains("namespace ZoneEngine.Core.Playfields.Content"),
                    "Content guardrail only applies to the content namespace: " + sourceFile);

                for (int i = 0; i < ForbiddenContentModuleReferences.Length; i++)
                {
                    ForbiddenReference forbidden = ForbiddenContentModuleReferences[i];
                    Assert.IsFalse(
                        text.IndexOf(forbidden.Pattern, StringComparison.Ordinal) >= 0,
                        string.Format(
                            "Playfield content modules must define content only; forbidden {0} reference '{1}' found in {2}.",
                            forbidden.Category,
                            forbidden.Pattern,
                            sourceFile));
                }
            }
        }

        [TestMethod]
        public void PrivateCityContentModuleSkeletonIsRegisteredWithoutRuntimeOwnership()
        {
            string repositoryRoot = FindRepositoryRoot();
            string modulePath = Path.Combine(
                repositoryRoot,
                @"AORebirth\Server\ZoneEngine\Core\Playfields\Content\PrivateCityContentModule.cs");
            string playfieldPath = Path.Combine(
                repositoryRoot,
                @"AORebirth\Server\ZoneEngine\Core\Playfields\Playfield.cs");
            string projectPath = Path.Combine(
                repositoryRoot,
                @"AORebirth\Server\ZoneEngine\ZoneEngine.csproj");

            string moduleText = File.ReadAllText(modulePath);
            string playfieldText = File.ReadAllText(playfieldPath);
            string projectText = File.ReadAllText(projectPath);

            Assert.IsTrue(moduleText.Contains("public sealed class PrivateCityContentModule : IPlayfieldContentModule"));
            Assert.IsTrue(moduleText.Contains("public bool Supports(Identity playfieldIdentity)"));
            Assert.IsTrue(moduleText.Contains("public void Register(PlayfieldContentRegistration registration)"));
            Assert.IsTrue(
                playfieldText.Contains("new PrivateCityContentModule()"),
                "Playfield content coordinator must register the private-city content module skeleton.");
            Assert.IsTrue(
                projectText.Contains(@"Core\Playfields\Content\PrivateCityContentModule.cs"),
                "ZoneEngine project must compile the private-city content module skeleton.");
        }

        [TestMethod]
        public void MontroyalContentModuleSkeletonIsRegisteredWithoutRuntimeOwnership()
        {
            string repositoryRoot = FindRepositoryRoot();
            string modulePath = Path.Combine(
                repositoryRoot,
                @"AORebirth\Server\ZoneEngine\Core\Playfields\Content\MontroyalContentModule.cs");
            string playfieldPath = Path.Combine(
                repositoryRoot,
                @"AORebirth\Server\ZoneEngine\Core\Playfields\Playfield.cs");
            string projectPath = Path.Combine(
                repositoryRoot,
                @"AORebirth\Server\ZoneEngine\ZoneEngine.csproj");

            string moduleText = File.ReadAllText(modulePath);
            string playfieldText = File.ReadAllText(playfieldPath);
            string projectText = File.ReadAllText(projectPath);

            Assert.IsTrue(moduleText.Contains("public sealed class MontroyalContentModule : IPlayfieldContentModule"));
            Assert.IsTrue(moduleText.Contains("private const int MontroyalPlayfieldInstance = 655"));
            Assert.IsTrue(moduleText.Contains("public bool Supports(Identity playfieldIdentity)"));
            Assert.IsTrue(moduleText.Contains("public void Register(PlayfieldContentRegistration registration)"));
            Assert.IsTrue(
                playfieldText.Contains("new MontroyalContentModule()"),
                "Playfield content coordinator must register the Montroyal content module skeleton.");
            Assert.IsTrue(
                projectText.Contains(@"Core\Playfields\Content\MontroyalContentModule.cs"),
                "ZoneEngine project must compile the Montroyal content module skeleton.");
        }

        private static void AssertExpectedOrder(
            IList<PlayfieldLifecycleEvent> events,
            string flow,
            string[] expectedStages)
        {
            string failure;
            Assert.IsTrue(
                PlayfieldLifecycleTrace.ContainsExpectedOrder(events, flow, expectedStages, out failure),
                failure);
        }

        private static void AssertStageBefore(
            IList<PlayfieldLifecycleEvent> events,
            string firstStage,
            string secondStage)
        {
            int first = IndexOfStage(events, firstStage);
            int second = IndexOfStage(events, secondStage);
            Assert.IsTrue(first >= 0, "Missing lifecycle stage " + firstStage + ".");
            Assert.IsTrue(second >= 0, "Missing lifecycle stage " + secondStage + ".");
            Assert.IsTrue(first < second, firstStage + " must occur before " + secondStage + ".");
        }

        private static bool HasDetail(IList<PlayfieldLifecycleEvent> events, string stage, string detail)
        {
            for (int i = 0; i < events.Count; i++)
            {
                if (events[i].Stage == stage && events[i].Detail == detail)
                {
                    return true;
                }
            }

            return false;
        }

        private static bool HasDetailContains(IList<PlayfieldLifecycleEvent> events, string stage, string detail)
        {
            for (int i = 0; i < events.Count; i++)
            {
                if (events[i].Stage == stage
                    && events[i].Detail.IndexOf(detail, StringComparison.Ordinal) >= 0)
                {
                    return true;
                }
            }

            return false;
        }

        private static void AssertMessageForStage(
            IList<PlayfieldLifecycleEvent> events,
            string stage,
            string messageType)
        {
            int index = IndexOfStage(events, stage);
            Assert.IsTrue(index >= 0, "Missing lifecycle stage " + stage + ".");
            Assert.AreEqual(messageType, events[index].MessageType, "Unexpected message type for stage " + stage + ".");
        }

        private static int CountFlow(IList<PlayfieldLifecycleEvent> events, string flow)
        {
            int count = 0;
            for (int i = 0; i < events.Count; i++)
            {
                if (events[i].Flow == flow)
                {
                    count++;
                }
            }

            return count;
        }

        private static int CountStage(IList<PlayfieldLifecycleEvent> events, string stage)
        {
            int count = 0;
            for (int i = 0; i < events.Count; i++)
            {
                if (events[i].Stage == stage)
                {
                    count++;
                }
            }

            return count;
        }

        private static int IndexOfStage(IList<PlayfieldLifecycleEvent> events, string stage)
        {
            for (int i = 0; i < events.Count; i++)
            {
                if (events[i].Stage == stage)
                {
                    return i;
                }
            }

            return -1;
        }

        private static string FindRepositoryRoot([CallerFilePath] string sourcePath = null)
        {
            string current = Path.GetDirectoryName(sourcePath);
            while (!string.IsNullOrEmpty(current))
            {
                string candidate = Path.Combine(
                    current,
                    @"AORebirth\Server\ZoneEngine\Core\Playfields\Content");
                if (Directory.Exists(candidate))
                {
                    return current;
                }

                DirectoryInfo parent = Directory.GetParent(current);
                current = parent == null ? null : parent.FullName;
            }

            Assert.Fail("Unable to find AORebirth repository root from " + sourcePath + ".");
            return string.Empty;
        }

        private static void RecordExpected(string flow, string[] stages)
        {
            Identity identity = new Identity { Type = IdentityType.CanbeAffected, Instance = 1 };
            for (int i = 0; i < stages.Length; i++)
            {
                PlayfieldLifecycleTrace.Record(flow, stages[i], stages[i], identity);
            }
        }

        private static void RecordPrivateCityReadyInitCurrentPacketSequence()
        {
            Identity character = new Identity { Type = IdentityType.CanbeAffected, Instance = 1001 };
            Identity playfield = new Identity { Type = IdentityType.Playfield2, Instance = 1196034 };

            PlayfieldLifecycleTrace.Record(
                PlayfieldLifecycleTrace.FlowPrivateCityReadyInit,
                PlayfieldLifecycleTrace.StagePrivateCityReadyBlockBegin,
                PlayfieldLifecycleTrace.MessagePrivateCityReadyBlockBegin,
                character);
            PlayfieldLifecycleTrace.Record(
                PlayfieldLifecycleTrace.FlowPrivateCityReadyInit,
                PlayfieldLifecycleTrace.StagePrivateCitySimpleCharFullUpdateBroadcast,
                PlayfieldLifecycleTrace.MessageSimpleCharFullUpdate,
                character);
            PlayfieldLifecycleTrace.Record(
                PlayfieldLifecycleTrace.FlowPrivateCityReadyInit,
                PlayfieldLifecycleTrace.StagePrivateCityOrgInfoPacket,
                PlayfieldLifecycleTrace.MessageOrgInfoPacket,
                character,
                "Est. 2024");
            PlayfieldLifecycleTrace.Record(
                PlayfieldLifecycleTrace.FlowPrivateCityReadyInit,
                PlayfieldLifecycleTrace.StagePrivateCitySocialStatus,
                PlayfieldLifecycleTrace.MessageStat,
                character,
                "socialstatus=4");
            PlayfieldLifecycleTrace.Record(
                PlayfieldLifecycleTrace.FlowPrivateCityReadyInit,
                PlayfieldLifecycleTrace.StagePrivateCityClan,
                PlayfieldLifecycleTrace.MessageStat,
                character,
                "clan=1970177");
            PlayfieldLifecycleTrace.Record(
                PlayfieldLifecycleTrace.FlowPrivateCityReadyInit,
                PlayfieldLifecycleTrace.StagePrivateCityClanLevel,
                PlayfieldLifecycleTrace.MessageStat,
                character,
                "clanlevel=1");
            PlayfieldLifecycleTrace.Record(
                PlayfieldLifecycleTrace.FlowPrivateCityReadyInit,
                PlayfieldLifecycleTrace.StagePrivateCitySocialStatus,
                PlayfieldLifecycleTrace.MessageStat,
                character,
                "socialstatus=4");
            PlayfieldLifecycleTrace.Record(
                PlayfieldLifecycleTrace.FlowPrivateCityReadyInit,
                PlayfieldLifecycleTrace.StagePrivateCitySocialStatus,
                PlayfieldLifecycleTrace.MessageStat,
                character,
                "socialstatus=4");
            PlayfieldLifecycleTrace.Record(
                PlayfieldLifecycleTrace.FlowPrivateCityReadyInit,
                PlayfieldLifecycleTrace.StagePrivateCitySocialStatus,
                PlayfieldLifecycleTrace.MessageStat,
                character,
                "socialstatus=4");
            PlayfieldLifecycleTrace.Record(
                PlayfieldLifecycleTrace.FlowPrivateCityReadyInit,
                PlayfieldLifecycleTrace.StagePrivateCityOrgInitSent,
                PlayfieldLifecycleTrace.MessagePrivateCityOrgInitSent,
                character,
                "org=1970177 orgName=Est. 2024 socialStatus=4 repeats=4");
            PlayfieldLifecycleTrace.Record(
                PlayfieldLifecycleTrace.FlowPrivateCityReadyInit,
                PlayfieldLifecycleTrace.StagePrivateCityFullCharacter,
                PlayfieldLifecycleTrace.MessageFullCharacter,
                character);
            PlayfieldLifecycleTrace.Record(
                PlayfieldLifecycleTrace.FlowPrivateCityReadyInit,
                PlayfieldLifecycleTrace.StagePrivateCityPlayfieldAllTowers,
                PlayfieldLifecycleTrace.MessagePlayfieldAllTowers,
                playfield);
            PlayfieldLifecycleTrace.Record(
                PlayfieldLifecycleTrace.FlowPrivateCityReadyInit,
                PlayfieldLifecycleTrace.StagePrivateCityPlayfieldAllCities,
                PlayfieldLifecycleTrace.MessagePlayfieldAllCities,
                playfield);
            PlayfieldLifecycleTrace.Record(
                PlayfieldLifecycleTrace.FlowPrivateCityReadyInit,
                PlayfieldLifecycleTrace.StagePrivateCityTowersCitiesSent,
                PlayfieldLifecycleTrace.MessagePrivateCityTowersCitiesSent,
                playfield,
                "cityUnknown=0 cityPayloadBytes=0");
            PlayfieldLifecycleTrace.Record(
                PlayfieldLifecycleTrace.FlowPrivateCityReadyInit,
                PlayfieldLifecycleTrace.StagePrivateCityReadyBlockEnd,
                PlayfieldLifecycleTrace.MessagePrivateCityReadyBlockEnd,
                character);
        }

        private static readonly ForbiddenReference[] ForbiddenContentModuleReferences =
            new[]
            {
                new ForbiddenReference("combat logic", "NpcCombatTickCoordinator"),
                new ForbiddenReference("combat logic", "NpcCombatAttackRules"),
                new ForbiddenReference("combat logic", "CombatDamageRules"),
                new ForbiddenReference("combat packets", "AttackInfoMessage"),
                new ForbiddenReference("combat packets", "SpecialAttackWeaponMessage"),
                new ForbiddenReference("corpse lifecycle", "NpcCorpseLifecycleCoordinator"),
                new ForbiddenReference("corpse lifecycle", "NpcCorpseLifecycleRules"),
                new ForbiddenReference("player visibility", "CharInPlayMessageHandler"),
                new ForbiddenReference("player visibility", "SimpleCharFullUpdate"),
                new ForbiddenReference("player visibility", "FullCharacterMessageHandler"),
                new ForbiddenReference("GenericCmd routing", "GenericCmd"),
                new ForbiddenReference("GenericCmd routing", "GenericCmdMessageHandler"),
                new ForbiddenReference("inventory logic", "Inventory"),
                new ForbiddenReference("inventory logic", "ContainerAddItem"),
                new ForbiddenReference("inventory logic", "ClientMoveItem"),
                new ForbiddenReference("org commands", "OrgClient"),
                new ForbiddenReference("org commands", "OrgClientMessageHandler"),
                new ForbiddenReference("org commands", "OrgServer"),
                new ForbiddenReference("packet serialization internals", "SendCompressed"),
                new ForbiddenReference("packet serialization internals", "N3Messages"),
                new ForbiddenReference("packet serialization internals", "SystemMessages"),
                new ForbiddenReference("packet serialization internals", "Serializer"),
                new ForbiddenReference("database import", "AORebirth.Database"),
                new ForbiddenReference("database import", "ItemLoader"),
                new ForbiddenReference("database import", "NanoLoader"),
                new ForbiddenReference("database import", "CheckDatabase"),
                new ForbiddenReference("database import", "MessagePackZip"),
                new ForbiddenReference("capture tooling", "AOSharpLiveCapture"),
                new ForbiddenReference("capture tooling", "tools-temp")
            };

        private sealed class ForbiddenReference
        {
            public ForbiddenReference(string category, string pattern)
            {
                this.Category = category;
                this.Pattern = pattern;
            }

            public string Category { get; private set; }

            public string Pattern { get; private set; }
        }
    }
}
